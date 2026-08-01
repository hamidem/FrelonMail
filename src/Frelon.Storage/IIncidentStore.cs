using Frelon.Core;

namespace Frelon.Storage;

/// <summary>
/// Définit la persistance locale des incidents.
/// </summary>
public interface IIncidentStore
{
    /// <summary>Initialise explicitement le schéma de stockage.</summary>
    Task InitializeAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Enregistre un nouvel incident.</summary>
    Task SaveAsync(
        FraudIncident incident,
        CancellationToken cancellationToken = default);

    /// <summary>Récupère un incident par son identifiant.</summary>
    Task<FraudIncident?> GetByIdAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default);

    /// <summary>Liste les incidents les plus récents sans charger leurs snapshots.</summary>
    Task<IReadOnlyList<IncidentSummary>> ListRecentAsync(
        int limit = 100,
        CancellationToken cancellationToken = default);
}
