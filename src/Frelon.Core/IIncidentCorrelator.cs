namespace Frelon.Core;

/// <summary>
/// Recherche des campagnes candidates parmi des incidents déjà analysés.
/// </summary>
public interface IIncidentCorrelator
{
    /// <summary>
    /// Retourne uniquement les groupes soutenus par des rapprochements explicables.
    /// Une campagne candidate reste une hypothèse et non un verdict.
    /// </summary>
    IReadOnlyList<CampaignCandidate> FindCandidates(
        IReadOnlyCollection<FraudIncident> incidents);
}
