using Frelon.Core;

namespace Frelon.Storage;

/// <summary>
/// Protège la revue humaine contre une campagne disparue ou modifiée depuis sa consultation.
/// </summary>
public sealed class LocalCampaignReviewService : ICampaignReviewService
{
    /// <summary>Nombre maximal d'incidents pouvant être recalculés avant une décision.</summary>
    public const int MaximumIncidentLimit = 500;

    private readonly ICampaignCorrelationService _correlationService;
    private readonly ICampaignReviewStore _reviewStore;

    /// <summary>Crée le workflow avec ses dépendances locales explicites.</summary>
    public LocalCampaignReviewService(
        ICampaignCorrelationService correlationService,
        ICampaignReviewStore reviewStore)
    {
        ArgumentNullException.ThrowIfNull(correlationService);
        ArgumentNullException.ThrowIfNull(reviewStore);

        _correlationService = correlationService;
        _reviewStore = reviewStore;
    }

    /// <inheritdoc />
    public async Task<CampaignReviewDecision> RecordCurrentAsync(
        CampaignReviewDecision decision,
        int incidentLimit = 100,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decision);

        if (incidentLimit is < 1 or > MaximumIncidentLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(incidentLimit),
                incidentLimit,
                $"La limite doit être comprise entre 1 et {MaximumIncidentLimit}.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var candidates = await _correlationService
            .FindRecentCandidatesAsync(incidentLimit, cancellationToken)
            .ConfigureAwait(false);

        if (candidates.Any(candidate => candidate is null) ||
            candidates
                .Select(candidate => candidate.Fingerprint)
                .Distinct(StringComparer.Ordinal)
                .Count() != candidates.Count)
        {
            throw new InvalidDataException(
                "La corrélation locale a retourné des campagnes absentes ou dupliquées.");
        }

        var currentCandidate = candidates.SingleOrDefault(candidate =>
            string.Equals(
                candidate.Fingerprint,
                decision.CandidateFingerprint,
                StringComparison.Ordinal));
        if (currentCandidate is null)
        {
            throw new InvalidOperationException(
                "La campagne examinée n'est plus présente dans la fenêtre courante. " +
                "Actualisez la consultation avant de décider.");
        }

        if (!decision.CandidateSnapshot.HasSameSnapshotAs(currentCandidate))
        {
            throw new InvalidOperationException(
                "La campagne a changé depuis sa consultation. " +
                "Examinez son nouveau snapshot avant d'enregistrer une décision.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        await _reviewStore
            .SaveCampaignReviewAsync(decision, cancellationToken)
            .ConfigureAwait(false);

        return decision;
    }
}
