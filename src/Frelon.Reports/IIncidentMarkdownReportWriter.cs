using Frelon.Core;

namespace Frelon.Reports;

/// <summary>
/// Définit le contrat de génération d'un rapport Markdown lisible depuis un <see cref="FraudIncident"/>.
/// </summary>
public interface IIncidentMarkdownReportWriter
{
    /// <summary>
    /// Génère un rapport Markdown à partir de l'incident fourni.
    /// </summary>
    /// <param name="incident">L'incident à transformer en rapport.</param>
    /// <returns>Chaîne Markdown représentant le rapport de l'incident.</returns>
    string Write(FraudIncident incident);
}
