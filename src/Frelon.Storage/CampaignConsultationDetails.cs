using Frelon.Core;

namespace Frelon.Storage;

/// <summary>
/// Détail d'une composition courante ou uniquement conservée par son historique de revues.
/// </summary>
public sealed record CampaignConsultationDetails
{
    /// <summary>Crée un détail à partir d'une campagne courante et/ou d'un historique.</summary>
    public CampaignConsultationDetails(
        CampaignCandidate? currentCandidate,
        IReadOnlyList<CampaignReviewDecision> reviewHistory)
    {
        ArgumentNullException.ThrowIfNull(reviewHistory);

        if (reviewHistory.Any(review => review is null) ||
            reviewHistory.Select(review => review.ReviewId).Distinct().Count() != reviewHistory.Count)
        {
            throw new ArgumentException(
                "L'historique doit contenir des décisions présentes et distinctes.",
                nameof(reviewHistory));
        }

        if (currentCandidate is null && reviewHistory.Count == 0)
        {
            throw new ArgumentException(
                "Le détail exige une campagne courante ou au moins une revue historique.",
                nameof(reviewHistory));
        }

        var orderedReviews = reviewHistory
            .OrderByDescending(review => review.DecidedAt)
            .ThenBy(review => review.ReviewId)
            .ToArray();
        var fingerprint = currentCandidate?.Fingerprint ??
            orderedReviews[0].CandidateFingerprint;
        if (orderedReviews.Any(review =>
            !string.Equals(
                review.CandidateFingerprint,
                fingerprint,
                StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "Toutes les revues doivent concerner la même composition de campagne.",
                nameof(reviewHistory));
        }

        CurrentCandidate = currentCandidate;
        CandidateSnapshot = currentCandidate ??
            orderedReviews[0].CandidateSnapshot;
        ReviewHistory = orderedReviews;
    }

    /// <summary>Empreinte stable de la composition consultée.</summary>
    public string Fingerprint => CandidateSnapshot.Fingerprint;

    /// <summary>
    /// Campagne recalculée dans la fenêtre demandée, ou <see langword="null"/> si elle est historique.
    /// </summary>
    public CampaignCandidate? CurrentCandidate { get; }

    /// <summary>
    /// Snapshot à présenter : calcul courant lorsqu'il existe, sinon dernier snapshot humain conservé.
    /// </summary>
    public CampaignCandidate CandidateSnapshot { get; }

    /// <summary>Décisions de la plus récente à la plus ancienne.</summary>
    public IReadOnlyList<CampaignReviewDecision> ReviewHistory { get; }

    /// <summary>Dernière décision connue pour cette composition.</summary>
    public CampaignReviewDecision? LatestReview =>
        ReviewHistory.Count == 0 ? null : ReviewHistory[0];

    /// <summary>Indique si la composition figure dans la fenêtre récente consultée.</summary>
    public bool IsCurrent => CurrentCandidate is not null;
}
