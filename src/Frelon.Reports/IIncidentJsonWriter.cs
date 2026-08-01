using Frelon.Core;

namespace Frelon.Reports;

/// <summary>
/// Définit le contrat de sérialisation d'un <see cref="FraudIncident"/> en JSON.
/// </summary>
public interface IIncidentJsonWriter
{
    /// <summary>
    /// Sérialise l'incident en une chaîne JSON lisible.
    /// </summary>
    /// <param name="incident">L'incident à sérialiser.</param>
    /// <returns>Chaîne JSON représentant l'incident.</returns>
    string Write(FraudIncident incident);
}
