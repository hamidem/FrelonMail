using Frelon.Core;

namespace Frelon.Mail;

/// <summary>
/// Définit le contrat d'analyse des headers d'un email parsé.
/// Transforme un <see cref="ParsedEmail"/> en objets métier du domaine Core.
/// </summary>
public interface IEmailHeaderAnalyzer
{
    /// <summary>
    /// Extrait les identités déclarées dans les headers du mail.
    /// </summary>
    MailIdentity ExtractIdentity(ParsedEmail email);

    /// <summary>
    /// Extrait l'évaluation des mécanismes d'authentification depuis les headers.
    /// </summary>
    AuthenticationAssessment ExtractAuthentication(ParsedEmail email);

    /// <summary>
    /// Extrait la chaîne des relais depuis les headers Received, dans leur ordre d'apparition.
    /// </summary>
    IReadOnlyList<ReceivedHop> ExtractReceivedChain(ParsedEmail email);
}
