using Frelon.Exporters;

namespace Frelon.Application;

/// <summary>
/// Prépare un export d'IOC minimisé depuis des incidents conservés localement.
/// </summary>
public interface IShareableIocPreparationService
{
    /// <summary>
    /// Recharge les dernières validations humaines puis produit le résultat uniquement en mémoire.
    /// </summary>
    Task<ShareableIocExportResult> PrepareAsync(
        ShareableIocPreparationRequest request,
        CancellationToken cancellationToken = default);
}
