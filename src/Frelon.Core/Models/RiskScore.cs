namespace Frelon.Core;

/// <summary>
/// Représente le score de risque calculé pour un incident.
/// </summary>
public sealed record RiskScore
{
    /// <summary>Valeur numérique du score de risque (ex. : entre 0.0 et 100.0).</summary>
    public required double Value { get; init; }

    /// <summary>Niveau de risque qualitatif associé à ce score.</summary>
    public required RiskLevel Level { get; init; }

    /// <summary>Raisons ou facteurs ayant contribué à ce score de risque.</summary>
    public IReadOnlyList<string> Reasons { get; init; } = [];
}
