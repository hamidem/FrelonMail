using System.Net;
using System.Text.RegularExpressions;
using Frelon.Core;

namespace Frelon.Mail;

/// <summary>
/// Implémentation de base de <see cref="IEmailUrlExtractor"/>.
/// Extrait les URLs depuis <see cref="ParsedEmail.BodyText"/> et <see cref="ParsedEmail.BodyHtml"/>
/// à l'aide d'une expression régulière.
/// Ne fait aucun appel réseau, ne résout aucun domaine et n'ouvre aucune URL.
/// </summary>
public sealed class BasicEmailUrlExtractor : IEmailUrlExtractor
{
    /// <summary>Raison associée à un hôte exprimé directement sous forme d'adresse IP.</summary>
    public const string IpLiteralHostReason = "L'URL utilise directement une adresse IP";

    /// <summary>Raison associée à une identité intégrée avant le nom d'hôte.</summary>
    public const string EmbeddedIdentityReason = "L'URL contient une identité susceptible de masquer l'hôte réel";

    /// <summary>Raison associée à un chemin sensible transmis sans HTTPS.</summary>
    public const string SensitivePathWithoutTlsReason = "Un chemin sensible est exposé sans HTTPS";

    /// <summary>Raison associée à un domaine internationalisé et un chemin sensible.</summary>
    public const string InternationalizedSensitiveUrlReason = "Un domaine internationalisé cible un chemin sensible";

    private static readonly Regex UrlRegex = new(
        @"https?://[^\s<>""']+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly char[] TrailingPunctuation =
        ['.', ',', ';', ':', ')', ']', '}', '"', '\''];

    private static readonly string[] SensitivePathMarkers =
        ["login", "signin", "sign-in", "account", "verify", "verification", "password", "credential", "security"];

    /// <inheritdoc/>
    public IReadOnlyList<UrlIndicator> ExtractUrls(ParsedEmail email)
    {
        ArgumentNullException.ThrowIfNull(email);

        var seen   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<UrlIndicator>();

        ExtractFromText(email.BodyText, seen, result);
        ExtractFromText(email.BodyHtml, seen, result);

        return result.AsReadOnly();
    }

    private static void ExtractFromText(
        string? text,
        HashSet<string> seen,
        List<UrlIndicator> result)
    {
        if (string.IsNullOrEmpty(text))
            return;

        foreach (Match match in UrlRegex.Matches(text))
        {
            var raw = match.Value.TrimEnd(TrailingPunctuation);

            if (string.IsNullOrEmpty(raw))
                continue;

            if (!seen.Add(raw))
                continue;

            result.Add(BuildIndicator(raw));
        }
    }

    private static UrlIndicator BuildIndicator(string raw)
    {
        string? host   = null;
        string? scheme = null;
        var reasons = new List<string>();

        if (Uri.TryCreate(raw, UriKind.Absolute, out var uri))
        {
            host   = uri.Host;
            scheme = uri.Scheme;
            var hasSensitivePath = HasSensitivePath(uri.AbsolutePath);

            if (IPAddress.TryParse(uri.Host.Trim('[', ']'), out _))
            {
                reasons.Add(IpLiteralHostReason);
            }

            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                reasons.Add(EmbeddedIdentityReason);
            }

            if (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && hasSensitivePath)
            {
                reasons.Add(SensitivePathWithoutTlsReason);
            }

            if (hasSensitivePath && HasInternationalizedLabel(uri))
            {
                reasons.Add(InternationalizedSensitiveUrlReason);
            }
        }

        return new UrlIndicator
        {
            RawValue        = raw,
            NormalizedValue = raw,
            Host            = host,
            Scheme          = scheme,
            IsSuspicious    = reasons.Count != 0,
            Reasons         = reasons,
        };
    }

    private static bool HasSensitivePath(string path)
    {
        var normalizedPath = path.Replace("sign-in", "signin", StringComparison.OrdinalIgnoreCase);
        var segments = normalizedPath.Split(
            ['/', '.', '_', '-'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Any(segment => SensitivePathMarkers.Contains(segment, StringComparer.OrdinalIgnoreCase));
    }

    private static bool HasInternationalizedLabel(Uri uri)
    {
        try
        {
            return uri.IdnHost
                .Split('.')
                .Any(label => label.StartsWith("xn--", StringComparison.OrdinalIgnoreCase));
        }
        catch (UriFormatException)
        {
            return false;
        }
    }
}
