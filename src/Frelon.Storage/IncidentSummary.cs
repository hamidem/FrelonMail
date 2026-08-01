using Frelon.Core;

namespace Frelon.Storage;

/// <summary>
/// Résume les métadonnées persistées d'un incident sans charger son snapshot complet.
/// </summary>
public sealed record IncidentSummary
{
    /// <summary>Identifiant unique de l'incident.</summary>
    public required Guid IncidentId { get; init; }

    /// <summary>Date de création de l'incident.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Date d'import de la preuve source.</summary>
    public required DateTimeOffset ImportedAt { get; init; }

    /// <summary>Nom du fichier source analysé.</summary>
    public required string SourceFileName { get; init; }

    /// <summary>Valeur numérique du score de risque.</summary>
    public required double RiskValue { get; init; }

    /// <summary>Niveau qualitatif du risque.</summary>
    public required RiskLevel RiskLevel { get; init; }

    /// <summary>Classification de fraude attribuée à l'incident.</summary>
    public required FraudClassification Classification { get; init; }

    /// <summary>Conclusion de la dernière revue humaine, lorsqu'elle existe.</summary>
    public ReviewVerdict? LatestReviewVerdict { get; init; }

    /// <summary>Classification retenue par la dernière revue humaine, lorsqu'elle existe.</summary>
    public FraudClassification? LatestReviewClassification { get; init; }

    /// <summary>Date de la dernière revue humaine, lorsqu'elle existe.</summary>
    public DateTimeOffset? LatestReviewAt { get; init; }
}
