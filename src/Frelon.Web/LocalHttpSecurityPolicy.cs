using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Frelon.Web;

/// <summary>
/// Ferme le serveur local aux interfaces réseau, origines navigateur et noms d'hôte non attendus.
/// </summary>
public static class LocalHttpSecurityPolicy
{
    /// <summary>Politique de contenu appliquée à toutes les réponses locales.</summary>
    public const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "base-uri 'none'; " +
        "connect-src 'self'; " +
        "font-src 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'; " +
        "img-src 'self' data:; " +
        "manifest-src 'none'; " +
        "object-src 'none'; " +
        "script-src 'self'; " +
        "style-src 'self'; " +
        "worker-src 'none'";

    /// <summary>Vérifie qu'une requête ne peut provenir que de l'application en boucle locale.</summary>
    public static bool IsAllowedRequest(HttpContext context, int expectedPort)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (expectedPort is < IPEndPoint.MinPort or > IPEndPoint.MaxPort)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedPort));
        }

        return IsLoopbackAddress(context.Connection.RemoteIpAddress) &&
               HasExpectedHost(context.Request.Host, expectedPort) &&
               HasAllowedOrigin(context.Request.Headers.Origin, expectedPort) &&
               HasAllowedFetchSite(context.Request.Headers["Sec-Fetch-Site"]);
    }

    /// <summary>Ajoute les en-têtes qui empêchent l'intégration et l'exécution de contenu tiers.</summary>
    public static void ApplyResponseHeaders(IHeaderDictionary headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        headers["Content-Security-Policy"] = ContentSecurityPolicy;
        headers["Cache-Control"] = "no-store";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        headers["Cross-Origin-Resource-Policy"] = "same-origin";
        headers["Permissions-Policy"] =
            "camera=(), geolocation=(), microphone=(), payment=(), usb=()";
        headers["Referrer-Policy"] = "no-referrer";
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
    }

    private static bool IsLoopbackAddress(IPAddress? address)
        => address is not null && IPAddress.IsLoopback(address);

    private static bool HasExpectedHost(HostString host, int expectedPort)
        => host.Port == expectedPort && IsLoopbackHost(host.Host);

    private static bool HasAllowedOrigin(StringValues values, int expectedPort)
    {
        if (StringValues.IsNullOrEmpty(values))
        {
            return true;
        }

        if (values.Count != 1 ||
            !Uri.TryCreate(values[0], UriKind.Absolute, out var origin))
        {
            return false;
        }

        return string.Equals(origin.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) &&
               origin.Port == expectedPort &&
               IsLoopbackHost(origin.DnsSafeHost);
    }

    private static bool HasAllowedFetchSite(StringValues values)
    {
        if (StringValues.IsNullOrEmpty(values))
        {
            return true;
        }

        return values.Count == 1 &&
            (string.Equals(values[0], "same-origin", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(values[0], "none", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsLoopbackHost(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host.Trim('[', ']'), out var address) &&
               IPAddress.IsLoopback(address);
    }
}
