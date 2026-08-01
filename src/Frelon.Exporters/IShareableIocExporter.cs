namespace Frelon.Exporters;

/// <summary>
/// Prépare un paquet d'IOC minimisé et une trace d'audit qui doit rester locale.
/// </summary>
public interface IShareableIocExporter
{
    /// <summary>
    /// Produit les documents en mémoire sans publication, écriture disque ni appel réseau.
    /// </summary>
    ShareableIocExportResult Export(ShareableIocExportRequest request);
}
