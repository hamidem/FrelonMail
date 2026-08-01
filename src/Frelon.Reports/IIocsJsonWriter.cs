using Frelon.Core;

namespace Frelon.Reports;

/// <summary>
/// Définit le contrat de sérialisation des IOC d'un <see cref="FraudIncident"/> en JSON.
/// </summary>
public interface IIocsJsonWriter
{
    /// <summary>
    /// Sérialise les IOC de l'incident en une chaîne JSON lisible.
    /// </summary>
    /// <param name="incident">L'incident dont les IOC sont à sérialiser.</param>
    /// <returns>Chaîne JSON représentant les IOC de l'incident.</returns>
    string Write(FraudIncident incident);
}
