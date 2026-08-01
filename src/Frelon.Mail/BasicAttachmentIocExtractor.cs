using Frelon.Core;

namespace Frelon.Mail;

/// <summary>
/// Implémentation de base de <see cref="IAttachmentIocExtractor"/>.
/// Transforme les SHA-256 déjà calculés sur les pièces jointes en IOC de type hash.
/// Ne recalcule aucun hash, n'effectue aucun appel réseau et ne modifie aucune pièce jointe.
/// </summary>
public sealed class BasicAttachmentIocExtractor : IAttachmentIocExtractor
{
    /// <summary>
    /// Confiance dans l'exactitude de l'empreinte observée.
    /// Ne représente pas un niveau de malveillance de la pièce jointe.
    /// </summary>
    public const double DefaultConfidence = 1.0;

    /// <summary>Source identifiant l'origine des IOC produits.</summary>
    public const string SourceName = "email-attachment";

    /// <inheritdoc/>
    public IReadOnlyList<Ioc> ExtractIocs(
        IReadOnlyList<AttachmentIndicator> attachments,
        DateTimeOffset observedAt)
    {
        ArgumentNullException.ThrowIfNull(attachments);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<Ioc>();

        foreach (AttachmentIndicator attachment in attachments)
        {
            string? sha256 = NormalizeSha256(attachment.Sha256);
            if (sha256 is null)
            {
                continue;
            }

            if (!seen.Add(sha256))
            {
                continue;
            }

            result.Add(new Ioc
            {
                Type = IocType.Hash,
                Value = sha256,
                Confidence = DefaultConfidence,
                Source = SourceName,
                FirstSeen = observedAt,
            });
        }

        return result.AsReadOnly();
    }

    private static string? NormalizeSha256(string? sha256)
    {
        if (string.IsNullOrWhiteSpace(sha256))
        {
            return null;
        }

        string normalized = sha256.Trim().ToLowerInvariant();

        if (normalized.Length != 64)
        {
            return null;
        }

        foreach (char c in normalized)
        {
            if (!Uri.IsHexDigit(c))
            {
                return null;
            }
        }

        return normalized;
    }
}