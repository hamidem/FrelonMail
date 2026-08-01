using System.Net;
using Frelon.Core;

namespace Frelon.Mail;

/// <summary>
/// Implémentation de base de <see cref="IUrlIocExtractor"/>.
/// Transforme les <see cref="UrlIndicator"/> déjà extraits en <see cref="Ioc"/>
/// de type <see cref="IocType.Url"/> puis <see cref="IocType.Domain"/> ou
/// <see cref="IocType.IpAddress"/> selon la nature de l'hôte.
/// Ne fait aucun appel réseau, ne résout aucun domaine et n'ouvre aucune URL.
/// </summary>
public sealed class BasicUrlIocExtractor : IUrlIocExtractor
{
    /// <summary>Confiance par défaut attribuée aux IOC produits depuis une URL.</summary>
    public const double DefaultConfidence = 0.5;

    /// <summary>Source identifiant l'origine des IOC produits.</summary>
    public const string SourceName = "email-url";

    /// <inheritdoc/>
    public IReadOnlyList<Ioc> ExtractIocs(
        IReadOnlyList<UrlIndicator> urls,
        DateTimeOffset observedAt)
    {
        ArgumentNullException.ThrowIfNull(urls);

        var seen   = new HashSet<(IocType Type, string Value)>();
        var result = new List<Ioc>();

        foreach (UrlIndicator url in urls)
        {
            string? urlValue = NormalizedUrlValue(url);
            if (!string.IsNullOrWhiteSpace(urlValue))
            {
                AddIfNew(seen, result, IocType.Url, urlValue, observedAt);
            }

            string? host = NormalizedHostValue(url);
            if (!string.IsNullOrWhiteSpace(host))
            {
                if (IPAddress.TryParse(host.Trim('[', ']'), out var ipAddress))
                {
                    AddIfNew(seen, result, IocType.IpAddress, ipAddress.ToString(), observedAt);
                }
                else
                {
                    AddIfNew(seen, result, IocType.Domain, host, observedAt);
                }
            }
        }

        return result.AsReadOnly();
    }

    private static string? NormalizedUrlValue(UrlIndicator url)
    {
        if (!string.IsNullOrWhiteSpace(url.NormalizedValue))
            return url.NormalizedValue.Trim();

        if (!string.IsNullOrWhiteSpace(url.RawValue))
            return url.RawValue.Trim();

        return null;
    }

    private static string? NormalizedHostValue(UrlIndicator url)
    {
        if (string.IsNullOrWhiteSpace(url.Host))
            return null;

        return url.Host.Trim().ToLowerInvariant();
    }

    private static void AddIfNew(
    HashSet<(IocType Type, string Value)> seen,
    List<Ioc> result,
    IocType type,
    string value,
    DateTimeOffset observedAt)
    {
        var deduplicationValue = type == IocType.Domain
            ? value.ToLowerInvariant()
            : value;

        var key = (type, deduplicationValue);

        if (!seen.Add(key))
            return;

        result.Add(new Ioc
        {
            Type = type,
            Value = value,
            Confidence = DefaultConfidence,
            Source = SourceName,
            FirstSeen = observedAt,
        });
    }
}
