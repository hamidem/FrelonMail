using System.Text.RegularExpressions;
using Frelon.Core;

namespace Frelon.Mail;

/// <summary>
/// Implémentation de base de l'analyse des headers d'un email parsé.
/// Opère uniquement sur les headers déjà présents dans <see cref="ParsedEmail"/>.
/// Ne fait aucun appel réseau, n'ouvre aucune URL et n'exécute aucune pièce jointe.
/// </summary>
public sealed class BasicEmailHeaderAnalyzer : IEmailHeaderAnalyzer
{
    /// <inheritdoc/>
    public MailIdentity ExtractIdentity(ParsedEmail email)
    {
        ArgumentNullException.ThrowIfNull(email);

        return new MailIdentity
        {
            From       = GetFirstHeaderValue(email, "From"),
            ReplyTo    = GetFirstHeaderValue(email, "Reply-To"),
            ReturnPath = GetFirstHeaderValue(email, "Return-Path"),
            MessageId  = GetFirstHeaderValue(email, "Message-ID"),
            Subject    = GetFirstHeaderValue(email, "Subject"),
        };
    }

    /// <inheritdoc/>
    public AuthenticationAssessment ExtractAuthentication(ParsedEmail email)
    {
        ArgumentNullException.ThrowIfNull(email);

        var raw = GetFirstHeaderValue(email, "Authentication-Results");

        if (raw is null)
        {
            return new AuthenticationAssessment();
        }

        return new AuthenticationAssessment
        {
            AuthenticationResultsRaw = raw,
            SpfResult   = ExtractAuthResult(raw, "spf"),
            DkimResult  = ExtractAuthResult(raw, "dkim"),
            DmarcResult = ExtractAuthResult(raw, "dmarc"),
        };
    }

    /// <inheritdoc/>
    public IReadOnlyList<ReceivedHop> ExtractReceivedChain(ParsedEmail email)
    {
        ArgumentNullException.ThrowIfNull(email);

        var hops = new List<ReceivedHop>();
        var position = 0;

        foreach (var header in email.Headers)
        {
            if (header.Name.Equals("Received", StringComparison.OrdinalIgnoreCase))
            {
                hops.Add(new ReceivedHop
                {
                    Position = position++,
                    RawValue = header.Value,
                });
            }
        }

        return hops.AsReadOnly();
    }

    // ── Helpers privés ────────────────────────────────────────────────────────

    private static string? GetFirstHeaderValue(ParsedEmail email, string name)
    {
        foreach (var header in email.Headers)
        {
            if (header.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return header.Value.Trim();
            }
        }

        return null;
    }

    /// <summary>
    /// Extrait le résultat d'un mécanisme d'authentification (spf, dkim ou dmarc)
    /// depuis la valeur brute du header Authentication-Results.
    /// Détection textuelle simple : recherche du fragment "meca=valeur".
    /// </summary>
    private static string? ExtractAuthResult(string raw, string mechanism)
    {
        var match = Regex.Match(
            raw,
            $@"(?:^|[;\s]){Regex.Escape(mechanism)}\s*=\s*([A-Za-z0-9_-]+)",
            RegexOptions.IgnoreCase);

        return match.Success ? match.Groups[1].Value.ToLowerInvariant() : null;
    }
}
