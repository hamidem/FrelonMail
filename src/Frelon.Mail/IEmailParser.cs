namespace Frelon.Mail;

/// <summary>Contrat d'un parseur de preuve de courrier électronique.</summary>
public interface IEmailParser
{
    /// <summary>
    /// Analyse un flux de message et retourne un résultat structuré.
    /// </summary>
    /// <param name="emlStream">Flux du fichier de message à analyser.</param>
    /// <param name="cancellationToken">Jeton d'annulation optionnel.</param>
    Task<ParsedEmail> ParseAsync(
        Stream emlStream,
        CancellationToken cancellationToken = default);
}
