using Frelon.Core;

namespace Frelon.Mail;

/// <summary>
/// Définit le contrat d'extraction d'URLs depuis un <see cref="ParsedEmail"/>.
/// Aucun appel réseau ne doit être effectué par les implémentations.
/// </summary>
public interface IEmailUrlExtractor
{
    /// <summary>
    /// Extrait les URLs présentes dans le corps de l'email.
    /// </summary>
    /// <param name="email">L'email déjà parsé à analyser.</param>
    /// <returns>Liste dédupliquée de <see cref="UrlIndicator"/> extraits.</returns>
    IReadOnlyList<UrlIndicator> ExtractUrls(ParsedEmail email);
}
