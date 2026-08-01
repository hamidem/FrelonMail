using Frelon.Core;

namespace Frelon.Mail;

/// <summary>
/// Définit le contrat de transformation de <see cref="UrlIndicator"/> déjà extraits
/// en <see cref="Ioc"/>.
/// Ne parcourt pas le corps du mail, ne fait aucun appel réseau et n'enrichit pas les URLs.
/// </summary>
public interface IUrlIocExtractor
{
    /// <summary>
    /// Transforme une liste d'URLs extraits en indicateurs de compromission.
    /// </summary>
    /// <param name="urls">URLs déjà extraits d'un email.</param>
    /// <param name="observedAt">Instant logique d'observation des indicateurs.</param>
    /// <returns>Liste dédupliquée d'<see cref="Ioc"/> de type <see cref="IocType.Url"/> et, le cas échéant, <see cref="IocType.Domain"/>.</returns>
    IReadOnlyList<Ioc> ExtractIocs(
        IReadOnlyList<UrlIndicator> urls,
        DateTimeOffset observedAt);
}
