using Frelon.Core;

namespace Frelon.Storage;

/// <summary>
/// Enregistre une décision humaine uniquement sur le snapshot de campagne réellement examiné.
/// </summary>
public interface ICampaignReviewService
{
    /// <summary>
    /// Vérifie que le snapshot est encore courant avant de conserver la décision append-only.
    /// </summary>
    Task<CampaignReviewDecision> RecordCurrentAsync(
        CampaignReviewDecision decision,
        int incidentLimit = 100,
        CancellationToken cancellationToken = default);
}
