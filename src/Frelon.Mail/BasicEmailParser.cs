using System.Security.Cryptography;
using System.Text;

namespace Frelon.Mail;

/// <summary>
/// Parser d'emails minimal basé sur la lecture de texte brut.
/// Gère les séparateurs CRLF et LF, les headers repliés, les headers dupliqués
/// et les lignes malformées. Ne réalise pas de parsing MIME complet.
/// </summary>
public sealed class BasicEmailParser : IEmailParser
{
    private readonly EmailAnalysisLimits _limits;

    /// <summary>Crée le parseur avec les quotas de référence.</summary>
    public BasicEmailParser()
        : this(EmailAnalysisLimits.Default)
    {
    }

    /// <summary>Crée le parseur avec des quotas explicites.</summary>
    public BasicEmailParser(EmailAnalysisLimits limits)
    {
        _limits = limits ?? throw new ArgumentNullException(nameof(limits));
        _limits.Validate();
    }

    /// <inheritdoc/>
    public async Task<ParsedEmail> ParseAsync(Stream emlStream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(emlStream);
        cancellationToken.ThrowIfCancellationRequested();

        var sourceBuffer = await EmailContentBuffer
            .ReadAsync(emlStream, _limits, cancellationToken)
            .ConfigureAwait(false);
        var sourceBytes = sourceBuffer.AsSpan();
        var sourceSha256 = Convert.ToHexString(SHA256.HashData(sourceBytes)).ToLowerInvariant();

        using var memoryStream = new MemoryStream(sourceBuffer, writable: false);
        using var reader = new StreamReader(memoryStream, leaveOpen: true);
        var content = await reader.ReadToEndAsync(cancellationToken);

        string headerSection;
        string bodyText;

        var crlf = content.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        if (crlf >= 0)
        {
            headerSection = content[..crlf];
            bodyText = content[(crlf + 4)..];
        }
        else
        {
            var lf = content.IndexOf("\n\n", StringComparison.Ordinal);
            if (lf >= 0)
            {
                headerSection = content[..lf];
                bodyText = content[(lf + 2)..];
            }
            else
            {
                headerSection = content;
                bodyText = string.Empty;
            }
        }

        var parsedEmail = new ParsedEmail
        {
            RawContent = content,
            SourceSha256 = sourceSha256,
            Headers = ParseHeaders(headerSection),
            BodyText = bodyText.Length > 0 ? bodyText : null,
            BodyHtml = null
        };
        ParsedEmailLimitGuard.Validate(parsedEmail, _limits);
        return parsedEmail;
    }

    /// <summary>
    /// Extrait les headers depuis la section brute.
    /// Gère : séparateurs CRLF/LF, repliage (espace ou tabulation),
    /// headers dupliqués et lignes sans séparateur ':' (ignorées sans exception).
    /// </summary>
    private static IReadOnlyList<ParsedEmailHeader> ParseHeaders(string headerSection)
    {
        var headers = new List<ParsedEmailHeader>();
        var lines = headerSection.Split('\n');

        string? currentName = null;
        var currentValue = new StringBuilder();

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');

            // Ligne repliée : commence par un espace ou une tabulation.
            if (line.Length > 0 && (line[0] == ' ' || line[0] == '\t'))
            {
                if (currentName is not null)
                    currentValue.Append(' ').Append(line.Trim());
                continue;
            }

            // Flush du header précédent avant de traiter la nouvelle ligne.
            if (currentName is not null)
            {
                headers.Add(new ParsedEmailHeader
                {
                    Name = currentName,
                    Value = currentValue.ToString().Trim()
                });
                currentName = null;
                currentValue.Clear();
            }

            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon > 0)
            {
                currentName = line[..colon].Trim();
                currentValue.Append(line[(colon + 1)..]);
            }
            // Ligne sans ':' : ignorée silencieusement.
        }

        // Flush du dernier header.
        if (currentName is not null)
        {
            headers.Add(new ParsedEmailHeader
            {
                Name = currentName,
                Value = currentValue.ToString().Trim()
            });
        }

        return headers;
    }
}
