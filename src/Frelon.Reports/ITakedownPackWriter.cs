namespace Frelon.Reports;

/// <summary>
/// Prépare un dossier multi-destinataires sans écriture disque ni transmission réseau.
/// </summary>
public interface ITakedownPackWriter
{
    /// <summary>
    /// Valide toutes les décisions humaines puis produit les documents en mémoire.
    /// </summary>
    TakedownPack Write(TakedownPackRequest request);
}
