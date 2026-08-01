using Frelon.Reports;

namespace Frelon.Application;

/// <summary>
/// Prépare un takedown pack depuis les décisions et incidents conservés localement.
/// </summary>
public interface ITakedownPackPreparationService
{
    /// <summary>
    /// Recharge les validations courantes puis produit le pack en mémoire, sans écriture ni envoi.
    /// </summary>
    Task<TakedownPack> PrepareAsync(
        TakedownPackPreparationRequest request,
        CancellationToken cancellationToken = default);
}
