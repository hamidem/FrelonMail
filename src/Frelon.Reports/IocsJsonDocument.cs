using Frelon.Core;

namespace Frelon.Reports;

/// <summary>
/// Document JSON interne regroupant les IOC d'un incident pour la sérialisation.
/// Ce type est interne au module <c>Frelon.Reports</c>.
/// </summary>
internal sealed record IocsJsonDocument
{
    /// <summary>Identifiant de l'incident source.</summary>
    public required Guid IncidentId { get; init; }

    /// <summary>Date et heure de génération du document.</summary>
    public required DateTimeOffset GeneratedAt { get; init; }

    /// <summary>Liste des IOC extraits de l'incident.</summary>
    public IReadOnlyList<Ioc> Iocs { get; init; } = [];
}
