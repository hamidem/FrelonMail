using Frelon.Core;
using Frelon.Reports;
using Frelon.Storage;

namespace Frelon.Application;

/// <summary>
/// Orchestre exclusivement en lecture les données locales requises par un takedown pack.
/// </summary>
public sealed class LocalTakedownPackPreparationService
    : ITakedownPackPreparationService
{
    private readonly IIncidentStore _incidentStore;
    private readonly IIncidentReviewStore _incidentReviewStore;
    private readonly ICampaignReviewStore _campaignReviewStore;
    private readonly ITakedownPackWriter _writer;

    /// <summary>Crée le cas d'usage avec ses dépendances explicites.</summary>
    public LocalTakedownPackPreparationService(
        IIncidentStore incidentStore,
        IIncidentReviewStore incidentReviewStore,
        ICampaignReviewStore campaignReviewStore,
        ITakedownPackWriter writer)
    {
        ArgumentNullException.ThrowIfNull(incidentStore);
        ArgumentNullException.ThrowIfNull(incidentReviewStore);
        ArgumentNullException.ThrowIfNull(campaignReviewStore);
        ArgumentNullException.ThrowIfNull(writer);

        _incidentStore = incidentStore;
        _incidentReviewStore = incidentReviewStore;
        _campaignReviewStore = campaignReviewStore;
        _writer = writer;
    }

    /// <inheritdoc />
    public async Task<TakedownPack> PrepareAsync(
        TakedownPackPreparationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var campaignReview = await _campaignReviewStore
            .GetLatestCampaignReviewAsync(
                request.CampaignFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (campaignReview is null)
        {
            throw new InvalidOperationException(
                "La campagne ne possède aucune décision humaine.");
        }

        if (!string.Equals(
                campaignReview.CandidateFingerprint,
                request.CampaignFingerprint,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "La revue de campagne locale ne correspond pas à la composition demandée.");
        }

        if (campaignReview.Verdict != CampaignReviewVerdict.Confirmed)
        {
            throw new InvalidOperationException(
                "Le takedown pack exige que la dernière décision confirme la campagne.");
        }

        var incidents = new List<FraudIncident>(
            campaignReview.CandidateSnapshot.IncidentIds.Count);
        var incidentReviews = new List<IncidentReviewDecision>(
            campaignReview.CandidateSnapshot.IncidentIds.Count);

        foreach (var incidentId in campaignReview.CandidateSnapshot.IncidentIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var incident = await _incidentStore
                .GetByIdAsync(incidentId, cancellationToken)
                .ConfigureAwait(false);
            if (incident is null)
            {
                throw new InvalidDataException(
                    $"Le snapshot de l'incident '{incidentId:D}' est introuvable.");
            }

            var incidentReview = await _incidentReviewStore
                .GetLatestReviewAsync(incidentId, cancellationToken)
                .ConfigureAwait(false);
            if (incidentReview is null)
            {
                throw new InvalidOperationException(
                    $"L'incident '{incidentId:D}' ne possède aucune décision humaine.");
            }

            if (incidentReview.IncidentId != incidentId)
            {
                throw new InvalidDataException(
                    $"La revue locale de l'incident '{incidentId:D}' est incohérente.");
            }

            incidents.Add(incident);
            incidentReviews.Add(incidentReview);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return _writer.Write(new TakedownPackRequest(
            request.PackId,
            request.PreparedAt,
            campaignReview,
            incidents,
            incidentReviews,
            request.Recipients,
            request.AnalystNotes));
    }
}
