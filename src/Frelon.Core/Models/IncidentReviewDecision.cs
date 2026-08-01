namespace Frelon.Core;

/// <summary>
/// Décision humaine horodatée et append-only, distincte du résultat automatique de l'analyse.
/// </summary>
public sealed record IncidentReviewDecision
{
    /// <summary>Longueur maximale d'une note de revue.</summary>
    public const int MaxNotesLength = 1000;

    /// <summary>Crée une décision cohérente et entièrement traçable.</summary>
    public IncidentReviewDecision(
        Guid reviewId,
        Guid incidentId,
        ReviewVerdict verdict,
        FraudClassification? classification,
        DateTimeOffset decidedAt,
        string? notes = null)
    {
        if (reviewId == Guid.Empty)
        {
            throw new ArgumentException("L'identifiant de revue ne peut pas être vide.", nameof(reviewId));
        }

        if (incidentId == Guid.Empty)
        {
            throw new ArgumentException("L'identifiant d'incident ne peut pas être vide.", nameof(incidentId));
        }

        if (!Enum.IsDefined(verdict))
        {
            throw new ArgumentOutOfRangeException(nameof(verdict), verdict, "Le verdict est inconnu.");
        }

        if (classification is not null && !Enum.IsDefined(classification.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(classification),
                classification,
                "La classification est inconnue.");
        }

        ValidateClassification(verdict, classification);

        if (decidedAt == default)
        {
            throw new ArgumentException("La date de décision est obligatoire.", nameof(decidedAt));
        }

        var normalizedNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (normalizedNotes?.Length > MaxNotesLength)
        {
            throw new ArgumentException(
                $"La note ne peut pas dépasser {MaxNotesLength} caractères.",
                nameof(notes));
        }

        ReviewId = reviewId;
        IncidentId = incidentId;
        Verdict = verdict;
        Classification = classification;
        DecidedAt = decidedAt;
        Notes = normalizedNotes;
    }

    /// <summary>Identifiant unique de cette décision.</summary>
    public Guid ReviewId { get; }

    /// <summary>Incident concerné par la décision.</summary>
    public Guid IncidentId { get; }

    /// <summary>Conclusion de la revue humaine.</summary>
    public ReviewVerdict Verdict { get; }

    /// <summary>Catégorie précise lorsque le verdict l'autorise.</summary>
    public FraudClassification? Classification { get; }

    /// <summary>Date et heure de la décision humaine.</summary>
    public DateTimeOffset DecidedAt { get; }

    /// <summary>Note locale facultative justifiant la décision.</summary>
    public string? Notes { get; }

    private static void ValidateClassification(
        ReviewVerdict verdict,
        FraudClassification? classification)
    {
        switch (verdict)
        {
            case ReviewVerdict.Inconclusive:
            case ReviewVerdict.Benign:
                if (classification is not null)
                {
                    throw new ArgumentException(
                        "Ce verdict ne doit pas porter de classification de fraude.",
                        nameof(classification));
                }

                break;

            case ReviewVerdict.Suspicious:
                if (classification != FraudClassification.Suspicious)
                {
                    throw new ArgumentException(
                        "Un verdict suspect doit conserver la classification Suspicious.",
                        nameof(classification));
                }

                break;

            case ReviewVerdict.ConfirmedFraud:
                if (classification is null or FraudClassification.Unknown or FraudClassification.Suspicious)
                {
                    throw new ArgumentException(
                        "Une fraude confirmée exige une catégorie précise.",
                        nameof(classification));
                }

                break;
        }
    }
}
