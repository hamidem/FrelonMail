using System.Globalization;
using System.Net;

namespace Frelon.Core;

/// <summary>
/// Corrélateur local, déterministe et volontairement prudent.
/// </summary>
public sealed class BasicIncidentCorrelator : IIncidentCorrelator
{
    /// <summary>Confiance minimale requise pour exploiter un IOC.</summary>
    public const double MinimumIocConfidence = 0.5;

    /// <summary>Score minimal nécessaire pour relier deux incidents.</summary>
    public const int MinimumCorrelationScore = 60;

    /// <summary>Poids d'une empreinte cryptographique exacte.</summary>
    public const int HashWeight = 100;

    /// <summary>Poids d'une URL exacte après normalisation prudente.</summary>
    public const int UrlWeight = 80;

    /// <summary>Poids d'une adresse IP exacte.</summary>
    public const int IpAddressWeight = 70;

    /// <summary>Poids d'une adresse email exacte.</summary>
    public const int EmailWeight = 60;

    /// <summary>Poids d'un domaine exact, insuffisant à lui seul.</summary>
    public const int DomainWeight = 40;

    /// <inheritdoc/>
    public IReadOnlyList<CampaignCandidate> FindCandidates(
        IReadOnlyCollection<FraudIncident> incidents)
    {
        ArgumentNullException.ThrowIfNull(incidents);

        if (incidents.Any(incident => incident is null))
        {
            throw new ArgumentException(
                "La collection ne peut pas contenir d'incident null.",
                nameof(incidents));
        }

        if (incidents.Any(incident => incident.IncidentId == Guid.Empty))
        {
            throw new ArgumentException(
                "Chaque incident doit posséder un identifiant.",
                nameof(incidents));
        }

        if (incidents.Select(incident => incident.IncidentId).Distinct().Count() != incidents.Count)
        {
            throw new ArgumentException(
                "La collection ne peut pas contenir deux fois le même incident.",
                nameof(incidents));
        }

        if (incidents.Count < 2)
        {
            return [];
        }

        var orderedIncidents = incidents
            .OrderBy(incident => incident.IncidentId)
            .ToArray();
        var normalizedIocs = orderedIncidents.ToDictionary(
            incident => incident.IncidentId,
            BuildNormalizedIocs);
        var links = FindQualifiedLinks(orderedIncidents, normalizedIocs);

        if (links.Count == 0)
        {
            return [];
        }

        return BuildCandidates(orderedIncidents, links);
    }

    private static List<IncidentCorrelationLink> FindQualifiedLinks(
        IReadOnlyList<FraudIncident> incidents,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<IndicatorKey, int>> normalizedIocs)
    {
        var links = new List<IncidentCorrelationLink>();

        for (var firstIndex = 0; firstIndex < incidents.Count - 1; firstIndex++)
        {
            var first = incidents[firstIndex];

            for (var secondIndex = firstIndex + 1; secondIndex < incidents.Count; secondIndex++)
            {
                var second = incidents[secondIndex];

                if (HaveSameEvidence(first, second))
                {
                    continue;
                }

                var matches = normalizedIocs[first.IncidentId]
                    .Where(pair => normalizedIocs[second.IncidentId].ContainsKey(pair.Key))
                    .Select(pair => new SharedIocMatch(pair.Key.Type, pair.Key.Value, pair.Value))
                    .OrderByDescending(match => match.Weight)
                    .ThenBy(match => match.Type)
                    .ThenBy(match => match.Value, StringComparer.Ordinal)
                    .ToArray();

                if (matches.Sum(match => match.Weight) >= MinimumCorrelationScore)
                {
                    links.Add(new IncidentCorrelationLink(
                        first.IncidentId,
                        second.IncidentId,
                        matches));
                }
            }
        }

        return links;
    }

    private static IReadOnlyList<CampaignCandidate> BuildCandidates(
        IReadOnlyList<FraudIncident> incidents,
        IReadOnlyList<IncidentCorrelationLink> links)
    {
        var parentByIncidentId = incidents.ToDictionary(
            incident => incident.IncidentId,
            incident => incident.IncidentId);

        foreach (var link in links)
        {
            Union(parentByIncidentId, link.FirstIncidentId, link.SecondIncidentId);
        }

        var incidentById = incidents.ToDictionary(incident => incident.IncidentId);
        var candidates = links
            .GroupBy(link => FindRoot(parentByIncidentId, link.FirstIncidentId))
            .Select(group =>
            {
                var groupLinks = group
                    .OrderBy(link => link.FirstIncidentId)
                    .ThenBy(link => link.SecondIncidentId)
                    .ToArray();
                var incidentIds = groupLinks
                    .SelectMany(link => new[] { link.FirstIncidentId, link.SecondIncidentId })
                    .Distinct()
                    .Order()
                    .ToArray();
                var observedAt = incidentIds
                    .Select(incidentId => incidentById[incidentId].CreatedAt)
                    .ToArray();

                return new CampaignCandidate(
                    incidentIds,
                    observedAt.Min(),
                    observedAt.Max(),
                    groupLinks);
            })
            .OrderBy(candidate => candidate.FirstObservedAt)
            .ThenBy(candidate => candidate.IncidentIds[0])
            .ToArray();

        return candidates;
    }

    private static IReadOnlyDictionary<IndicatorKey, int> BuildNormalizedIocs(
        FraudIncident incident)
    {
        var result = new Dictionary<IndicatorKey, int>();

        foreach (var ioc in incident.Iocs)
        {
            if (ioc is null ||
                !double.IsFinite(ioc.Confidence) ||
                ioc.Confidence < MinimumIocConfidence ||
                ioc.Confidence > 1)
            {
                continue;
            }

            var normalizedValue = Normalize(ioc.Type, ioc.Value);
            var weight = GetWeight(ioc.Type);

            if (normalizedValue is null || weight == 0)
            {
                continue;
            }

            result.TryAdd(new IndicatorKey(ioc.Type, normalizedValue), weight);
        }

        return result;
    }

    private static string? Normalize(IocType type, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return type switch
        {
            IocType.IpAddress => NormalizeIpAddress(trimmed),
            IocType.Domain => NormalizeDomain(trimmed),
            IocType.Url => NormalizeUrl(trimmed),
            IocType.Email => NormalizeEmail(trimmed),
            IocType.Hash => NormalizeHash(trimmed),
            _ => null,
        };
    }

    private static string? NormalizeIpAddress(string value)
        => IPAddress.TryParse(value, out var address)
            ? address.ToString()
            : null;

    private static string? NormalizeDomain(string value)
    {
        var withoutTrailingDot = value.TrimEnd('.');

        if (withoutTrailingDot.Length == 0 ||
            withoutTrailingDot.Any(char.IsWhiteSpace) ||
            IPAddress.TryParse(withoutTrailingDot, out _))
        {
            return null;
        }

        try
        {
            var ascii = new IdnMapping().GetAscii(withoutTrailingDot);
            return Uri.CheckHostName(ascii) == UriHostNameType.Dns
                ? ascii.ToLowerInvariant()
                : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string? NormalizeUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            string.IsNullOrWhiteSpace(uri.Host) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        var builder = new UriBuilder(uri)
        {
            Scheme = uri.Scheme.ToLowerInvariant(),
            Host = uri.IdnHost.ToLowerInvariant(),
        };

        if ((builder.Scheme == Uri.UriSchemeHttp && builder.Port == 80) ||
            (builder.Scheme == Uri.UriSchemeHttps && builder.Port == 443))
        {
            builder.Port = -1;
        }

        return builder.Uri.AbsoluteUri;
    }

    private static string? NormalizeEmail(string value)
    {
        if (value.Any(char.IsWhiteSpace))
        {
            return null;
        }

        var separatorIndex = value.LastIndexOf('@');

        if (separatorIndex <= 0 ||
            separatorIndex == value.Length - 1 ||
            value.IndexOf('@') != separatorIndex)
        {
            return null;
        }

        var domain = NormalizeDomain(value[(separatorIndex + 1)..]);
        return domain is null
            ? null
            : $"{value[..separatorIndex]}@{domain}";
    }

    private static string? NormalizeHash(string value)
    {
        if (value.Length is not (32 or 40 or 64 or 96 or 128) ||
            value.Any(character => !Uri.IsHexDigit(character)))
        {
            return null;
        }

        return value.ToLowerInvariant();
    }

    private static int GetWeight(IocType type)
        => type switch
        {
            IocType.Hash => HashWeight,
            IocType.Url => UrlWeight,
            IocType.IpAddress => IpAddressWeight,
            IocType.Email => EmailWeight,
            IocType.Domain => DomainWeight,
            _ => 0,
        };

    private static bool HaveSameEvidence(FraudIncident first, FraudIncident second)
    {
        var firstHash = NormalizeHash(first.Evidence.Sha256?.Trim() ?? string.Empty);
        var secondHash = NormalizeHash(second.Evidence.Sha256?.Trim() ?? string.Empty);

        return firstHash is not null &&
               string.Equals(firstHash, secondHash, StringComparison.Ordinal);
    }

    private static Guid FindRoot(
        IDictionary<Guid, Guid> parentByIncidentId,
        Guid incidentId)
    {
        var parent = parentByIncidentId[incidentId];

        if (parent == incidentId)
        {
            return incidentId;
        }

        var root = FindRoot(parentByIncidentId, parent);
        parentByIncidentId[incidentId] = root;
        return root;
    }

    private static void Union(
        IDictionary<Guid, Guid> parentByIncidentId,
        Guid firstIncidentId,
        Guid secondIncidentId)
    {
        var firstRoot = FindRoot(parentByIncidentId, firstIncidentId);
        var secondRoot = FindRoot(parentByIncidentId, secondIncidentId);

        if (firstRoot == secondRoot)
        {
            return;
        }

        var lowerRoot = firstRoot.CompareTo(secondRoot) < 0
            ? firstRoot
            : secondRoot;
        var higherRoot = lowerRoot == firstRoot
            ? secondRoot
            : firstRoot;

        parentByIncidentId[higherRoot] = lowerRoot;
    }

    private readonly record struct IndicatorKey(IocType Type, string Value);
}
