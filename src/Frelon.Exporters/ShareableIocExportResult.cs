namespace Frelon.Exporters;

/// <summary>
/// Sépare explicitement le paquet transmissible de la trace qui doit rester locale.
/// </summary>
public sealed record ShareableIocExportResult
{
    /// <summary>Crée un résultat composé de deux zones de confidentialité distinctes.</summary>
    public ShareableIocExportResult(
        ShareableIocPackage shareablePackage,
        ShareableIocLocalAudit localAudit)
    {
        ArgumentNullException.ThrowIfNull(shareablePackage);
        ArgumentNullException.ThrowIfNull(localAudit);

        if (shareablePackage.ExportId != localAudit.ExportId)
        {
            throw new ArgumentException(
                "Le paquet et son audit doivent porter le même identifiant d'export.",
                nameof(localAudit));
        }

        ShareablePackage = shareablePackage;
        LocalAudit = localAudit;
    }

    /// <summary>Documents minimisés pouvant être examinés avant partage.</summary>
    public ShareableIocPackage ShareablePackage { get; }

    /// <summary>Références sensibles à conserver exclusivement dans Frelon.</summary>
    public ShareableIocLocalAudit LocalAudit { get; }
}
