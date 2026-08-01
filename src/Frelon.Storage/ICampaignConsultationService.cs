namespace Frelon.Storage;

/// <summary>
/// Réunit les campagnes calculées et leurs décisions humaines dans une vue en lecture seule.
/// </summary>
public interface ICampaignConsultationService
{
    /// <summary>
    /// Liste les campagnes présentes dans la fenêtre récente avec leur dernière revue connue.
    /// </summary>
    Task<IReadOnlyList<CampaignConsultationSummary>> ListCurrentAsync(
        int incidentLimit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrouve une campagne courante ou historique et son historique de revues.
    /// </summary>
    Task<CampaignConsultationDetails?> GetDetailsAsync(
        string candidateFingerprint,
        int incidentLimit = 100,
        int reviewLimit = 100,
        CancellationToken cancellationToken = default);
}
