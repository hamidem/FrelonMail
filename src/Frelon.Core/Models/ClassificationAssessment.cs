namespace Frelon.Core;

/// <summary>
/// Piste automatique explicable, distincte d'une classification validée humainement.
/// </summary>
public sealed record ClassificationAssessment
{
    /// <summary>Représente l'absence de piste suffisamment étayée.</summary>
    public static ClassificationAssessment None { get; } = new(
        FraudClassification.Unknown,
        ClassificationConfidence.None,
        []);

    /// <summary>Crée une piste de classification cohérente.</summary>
    public ClassificationAssessment(
        FraudClassification classification,
        ClassificationConfidence confidence,
        IReadOnlyList<string> reasons)
    {
        ArgumentNullException.ThrowIfNull(reasons);

        if (classification == FraudClassification.Unknown)
        {
            if (confidence != ClassificationConfidence.None || reasons.Count != 0)
            {
                throw new ArgumentException(
                    "Une classification inconnue ne peut porter ni confiance ni raison.",
                    nameof(classification));
            }
        }
        else
        {
            if (confidence == ClassificationConfidence.None)
            {
                throw new ArgumentException(
                    "Une piste de classification doit indiquer un niveau de confiance.",
                    nameof(confidence));
            }

            if (reasons.Count == 0 || reasons.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException(
                    "Une piste de classification doit être expliquée par au moins une raison.",
                    nameof(reasons));
            }
        }

        Classification = classification;
        Confidence = confidence;
        Reasons = [.. reasons];
    }

    /// <summary>Catégorie suggérée, ou <see cref="FraudClassification.Unknown"/>.</summary>
    public FraudClassification Classification { get; }

    /// <summary>Confiance attachée à la piste.</summary>
    public ClassificationConfidence Confidence { get; }

    /// <summary>Signaux locaux ayant conduit à la piste.</summary>
    public IReadOnlyList<string> Reasons { get; }
}
