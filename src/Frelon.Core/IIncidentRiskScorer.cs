namespace Frelon.Core;

/// <summary>
/// Définit le contrat de calcul local du score de risque d'un incident.
/// </summary>
public interface IIncidentRiskScorer
{
    /// <summary>
    /// Calcule le score de risque pour un incident donné.
    /// </summary>
    /// <param name="incident">Incident à scorer.</param>
    /// <returns>Score de risque calculé localement.</returns>
    RiskScore Score(FraudIncident incident);
}