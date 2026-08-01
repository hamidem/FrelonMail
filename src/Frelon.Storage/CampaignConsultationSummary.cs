using Frelon.Core;

namespace Frelon.Storage;

/// <summary>
/// Campagne candidate actuelle accompagnée de sa dernière décision humaine connue.
/// </summary>
public sealed record CampaignConsultationSummary
{
    /// <summary>Crée une ligne de consultation cohérente.</summary>
    public CampaignConsultationSummary(
        CampaignCandidate candidate,
        CampaignReviewDecision? latestReview)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (latestReview is not null &&
            !string.Equals(
                candidate.Fingerprint,
                latestReview.CandidateFingerprint,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "La revue doit concerner la même composition de campagne.",
                nameof(latestReview));
        }

        Candidate = candidate;
        LatestReview = latestReview;
    }

    /// <summary>Campagne recalculée dans la fenêtre récente.</summary>
    public CampaignCandidate Candidate { get; }

    /// <summary>Dernière décision append-only, ou <see langword="null"/> si elle reste à examiner.</summary>
    public CampaignReviewDecision? LatestReview { get; }

    /// <summary>Indique si une décision humaine existe pour cette composition.</summary>
    public bool IsReviewed => LatestReview is not null;
}
