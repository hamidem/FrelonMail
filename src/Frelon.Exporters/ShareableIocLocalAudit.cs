using Frelon.Core;

namespace Frelon.Exporters;

/// <summary>
/// Trace contenant les références locales nécessaires à l'audit.
/// Cette structure ne doit jamais être ajoutée au paquet partageable.
/// </summary>
public sealed record ShareableIocLocalAudit(
    Guid ExportId,
    DateTimeOffset PreparedAt,
    IReadOnlyList<ShareableIocAuditSource> Sources,
    IReadOnlyList<ShareableIocArtifactDigest> ArtifactDigests,
    int InputIocCount,
    int ExportedIocCount,
    int FilteredIocCount);

/// <summary>Référence locale ayant autorisé l'utilisation des IOC d'un incident.</summary>
public sealed record ShareableIocAuditSource(
    Guid IncidentId,
    string EvidenceSha256,
    Guid IncidentReviewId,
    FraudClassification Classification);

/// <summary>
/// SHA-256 local du contenu UTF-8 sans BOM d'un document partageable produit.
/// </summary>
public sealed record ShareableIocArtifactDigest(
    string FileName,
    string Sha256);
