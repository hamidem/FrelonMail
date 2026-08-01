using Frelon.Core;

namespace Frelon.Mail;

/// <summary>
/// Définit le contrat de transformation des pièces jointes déjà analysées en IOC.
/// </summary>
public interface IAttachmentIocExtractor
{
    /// <summary>
    /// Transforme des indicateurs de pièces jointes en IOC de type hash.
    /// </summary>
    /// <param name="attachments">Pièces jointes déjà analysées.</param>
    /// <param name="observedAt">Instant logique d'observation des indicateurs.</param>
    /// <returns>Liste dédupliquée d'IOC hash.</returns>
    IReadOnlyList<Ioc> ExtractIocs(
        IReadOnlyList<AttachmentIndicator> attachments,
        DateTimeOffset observedAt);
}