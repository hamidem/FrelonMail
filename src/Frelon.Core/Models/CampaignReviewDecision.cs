namespace Frelon.Core;

/// <summary>
/// Décision humaine append-only portant sur le snapshot exact d'une campagne candidate.
/// </summary>
public sealed record CampaignReviewDecision
{
    /// <summary>Longueur maximale d'une note de revue.</summary>
    public const int MaxNotesLength = 1000;

    /// <summary>Crée une décision humaine entièrement traçable.</summary>
    public CampaignReviewDecision(
        Guid reviewId,
        CampaignCandidate candidateSnapshot,
        CampaignReviewVerdict verdict,
        DateTimeOffset decidedAt,
        string? notes = null)
    {
        if (reviewId == Guid.Empty)
        {
            throw new ArgumentException(
                "L'identifiant de revue ne peut pas être vide.",
                nameof(reviewId));
        }

        ArgumentNullException.ThrowIfNull(candidateSnapshot);

        if (!Enum.IsDefined(verdict))
        {
            throw new ArgumentOutOfRangeException(
                nameof(verdict),
                verdict,
                "Le verdict est inconnu.");
        }

        if (decidedAt == default)
        {
            throw new ArgumentException(
                "La date de décision est obligatoire.",
                nameof(decidedAt));
        }

        var normalizedNotes = string.IsNullOrWhiteSpace(notes)
            ? null
            : notes.Trim();
        if (normalizedNotes?.Length > MaxNotesLength)
        {
            throw new ArgumentException(
                $"La note ne peut pas dépasser {MaxNotesLength} caractères.",
                nameof(notes));
        }

        ReviewId = reviewId;
        CandidateSnapshot = candidateSnapshot;
        Verdict = verdict;
        DecidedAt = decidedAt;
        Notes = normalizedNotes;
    }

    /// <summary>Identifiant unique de cette décision.</summary>
    public Guid ReviewId { get; }

    /// <summary>Proposition exacte qui a été examinée par l'humain.</summary>
    public CampaignCandidate CandidateSnapshot { get; }

    /// <summary>Empreinte stable de la composition examinée.</summary>
    public string CandidateFingerprint => CandidateSnapshot.Fingerprint;

    /// <summary>Conclusion de la revue humaine.</summary>
    public CampaignReviewVerdict Verdict { get; }

    /// <summary>Date et heure de la décision humaine.</summary>
    public DateTimeOffset DecidedAt { get; }

    /// <summary>Note locale facultative justifiant la décision.</summary>
    public string? Notes { get; }
}
