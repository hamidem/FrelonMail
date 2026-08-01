using Frelon.Core;

namespace Frelon.Mail;

/// <summary>
/// Définit le contrat d'analyse complète d'un flux .eml en incident de fraude structuré.
/// Orchestre le parsing MIME et l'analyse des headers pour produire un <see cref="FraudIncident"/> minimal.
/// </summary>
public interface IEmailIncidentAnalyzer
{
    /// <summary>
    /// Analyse un flux .eml et construit un <see cref="FraudIncident"/> minimal.
    /// </summary>
    /// <param name="emlStream">Flux du fichier .eml à analyser.</param>
    /// <param name="sourceFileName">Nom du fichier source, si disponible.</param>
    /// <param name="cancellationToken">Jeton d'annulation optionnel.</param>
    Task<FraudIncident> AnalyzeAsync(
        Stream emlStream,
        string? sourceFileName = null,
        CancellationToken cancellationToken = default);
}
