namespace Frelon.Core;

/// <summary>
/// Niveau de risque associé à un incident.
/// </summary>
public enum RiskLevel
{
    /// <summary>Niveau non déterminé.</summary>
    Unknown,
    /// <summary>Risque faible.</summary>
    Low,
    /// <summary>Risque modéré.</summary>
    Medium,
    /// <summary>Risque élevé.</summary>
    High,
    /// <summary>Risque critique nécessitant une action immédiate.</summary>
    Critical
}
