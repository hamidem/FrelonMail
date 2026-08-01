using Frelon.Core;

namespace Frelon.Storage;

/// <summary>
/// Persiste l'historique append-only des décisions humaines sur les campagnes candidates.
/// </summary>
public interface ICampaignReviewStore
{
    /// <summary>Initialise explicitement le schéma de revue.</summary>
    Task InitializeAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ajoute une décision et son snapshot sans modifier les décisions précédentes.
    /// </summary>
    Task SaveCampaignReviewAsync(
        CampaignReviewDecision decision,
        CancellationToken cancellationToken = default);

    /// <summary>Retourne la décision la plus récente pour une même composition.</summary>
    Task<CampaignReviewDecision?> GetLatestCampaignReviewAsync(
        string candidateFingerprint,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Liste l'historique d'une composition, de la décision la plus récente à la plus ancienne.
    /// </summary>
    Task<IReadOnlyList<CampaignReviewDecision>> ListCampaignReviewsAsync(
        string candidateFingerprint,
        int limit = 100,
        CancellationToken cancellationToken = default);
}
