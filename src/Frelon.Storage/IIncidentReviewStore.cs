using Frelon.Core;

namespace Frelon.Storage;

/// <summary>Persiste l'historique append-only des décisions humaines.</summary>
public interface IIncidentReviewStore
{
    /// <summary>Initialise explicitement le schéma de revue.</summary>
    Task InitializeAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Ajoute une décision sans modifier les décisions précédentes.</summary>
    Task SaveReviewAsync(
        IncidentReviewDecision decision,
        CancellationToken cancellationToken = default);

    /// <summary>Retourne la décision humaine la plus récente d'un incident.</summary>
    Task<IncidentReviewDecision?> GetLatestReviewAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default);

    /// <summary>Liste l'historique des décisions, de la plus récente à la plus ancienne.</summary>
    Task<IReadOnlyList<IncidentReviewDecision>> ListReviewsAsync(
        Guid incidentId,
        int limit = 100,
        CancellationToken cancellationToken = default);
}
