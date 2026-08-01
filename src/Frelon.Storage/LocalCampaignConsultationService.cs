using Frelon.Core;

namespace Frelon.Storage;

/// <summary>
/// Compose localement les résultats éphémères de corrélation et les revues persistées.
/// </summary>
public sealed class LocalCampaignConsultationService : ICampaignConsultationService
{
    /// <summary>Limite maximale d'incidents ou de revues acceptée par une consultation.</summary>
    public const int MaximumLimit = 500;

    private readonly ICampaignCorrelationService _correlationService;
    private readonly ICampaignReviewStore _reviewStore;

    /// <summary>Crée le service avec ses deux sources locales explicites.</summary>
    public LocalCampaignConsultationService(
        ICampaignCorrelationService correlationService,
        ICampaignReviewStore reviewStore)
    {
        ArgumentNullException.ThrowIfNull(correlationService);
        ArgumentNullException.ThrowIfNull(reviewStore);

        _correlationService = correlationService;
        _reviewStore = reviewStore;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CampaignConsultationSummary>> ListCurrentAsync(
        int incidentLimit = 100,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(incidentLimit, nameof(incidentLimit));
        cancellationToken.ThrowIfCancellationRequested();

        var candidates = await LoadCurrentCandidatesAsync(
                incidentLimit,
                cancellationToken)
            .ConfigureAwait(false);
        var result = new List<CampaignConsultationSummary>(candidates.Count);

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var latestReview = await _reviewStore
                .GetLatestCampaignReviewAsync(
                    candidate.Fingerprint,
                    cancellationToken)
                .ConfigureAwait(false);
            EnsureReviewMatches(candidate.Fingerprint, latestReview);
            result.Add(new CampaignConsultationSummary(candidate, latestReview));
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<CampaignConsultationDetails?> GetDetailsAsync(
        string candidateFingerprint,
        int incidentLimit = 100,
        int reviewLimit = 100,
        CancellationToken cancellationToken = default)
    {
        var normalizedFingerprint = NormalizeFingerprint(candidateFingerprint);
        ValidateLimit(incidentLimit, nameof(incidentLimit));
        ValidateLimit(reviewLimit, nameof(reviewLimit));
        cancellationToken.ThrowIfCancellationRequested();

        var candidates = await LoadCurrentCandidatesAsync(
                incidentLimit,
                cancellationToken)
            .ConfigureAwait(false);
        var currentCandidate = candidates.SingleOrDefault(candidate =>
            string.Equals(
                candidate.Fingerprint,
                normalizedFingerprint,
                StringComparison.Ordinal));

        var storedReviews = await _reviewStore
            .ListCampaignReviewsAsync(
                normalizedFingerprint,
                reviewLimit,
                cancellationToken)
            .ConfigureAwait(false);
        var reviewHistory = ValidateAndOrderReviews(
            normalizedFingerprint,
            storedReviews);

        return currentCandidate is null && reviewHistory.Count == 0
            ? null
            : new CampaignConsultationDetails(currentCandidate, reviewHistory);
    }

    private async Task<IReadOnlyList<CampaignCandidate>> LoadCurrentCandidatesAsync(
        int incidentLimit,
        CancellationToken cancellationToken)
    {
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

        return candidates
            .OrderByDescending(candidate => candidate.LastObservedAt)
            .ThenByDescending(candidate => candidate.FirstObservedAt)
            .ThenBy(candidate => candidate.Fingerprint, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<CampaignReviewDecision> ValidateAndOrderReviews(
        string fingerprint,
        IReadOnlyList<CampaignReviewDecision> reviews)
    {
        if (reviews.Any(review => review is null) ||
            reviews.Any(review =>
                !string.Equals(
                    review.CandidateFingerprint,
                    fingerprint,
                    StringComparison.Ordinal)) ||
            reviews.Select(review => review.ReviewId).Distinct().Count() != reviews.Count)
        {
            throw new InvalidDataException(
                "L'historique local des revues de campagne est incohérent.");
        }

        return reviews
            .OrderByDescending(review => review.DecidedAt)
            .ThenBy(review => review.ReviewId)
            .ToArray();
    }

    private static void EnsureReviewMatches(
        string fingerprint,
        CampaignReviewDecision? review)
    {
        if (review is not null &&
            !string.Equals(
                review.CandidateFingerprint,
                fingerprint,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "La dernière revue locale ne correspond pas à la campagne demandée.");
        }
    }

    private static string NormalizeFingerprint(string candidateFingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateFingerprint);

        var normalized = candidateFingerprint.Trim().ToLowerInvariant();
        if (!CampaignCandidate.IsValidFingerprint(normalized))
        {
            throw new ArgumentException(
                "L'empreinte de campagne doit être une valeur SHA-256 hexadécimale.",
                nameof(candidateFingerprint));
        }

        return normalized;
    }

    private static void ValidateLimit(int limit, string parameterName)
    {
        if (limit is < 1 or > MaximumLimit)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                limit,
                $"La limite doit être comprise entre 1 et {MaximumLimit}.");
        }
    }
}
