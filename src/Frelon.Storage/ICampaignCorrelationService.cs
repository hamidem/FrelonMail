using Frelon.Core;

namespace Frelon.Storage;

/// <summary>
/// Recherche des campagnes candidates dans l'historique local d'incidents.
/// </summary>
public interface ICampaignCorrelationService
{
    /// <summary>
    /// Analyse un nombre borné d'incidents récents sans persister ni confirmer
    /// automatiquement les campagnes candidates.
    /// </summary>
    Task<IReadOnlyList<CampaignCandidate>> FindRecentCandidatesAsync(
        int limit = 100,
        CancellationToken cancellationToken = default);
}
