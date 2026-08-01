using Frelon.Core;

namespace Frelon.Exporters;

/// <summary>Exporte les IOC d'un incident dans un document CSV défensif.</summary>
public interface IIocCsvExporter
{
    /// <summary>Produit le document CSV sans écrire sur le système de fichiers.</summary>
    /// <param name="incident">Incident contenant les IOC à exporter.</param>
    /// <returns>Document CSV complet.</returns>
    string Export(FraudIncident incident);
}
