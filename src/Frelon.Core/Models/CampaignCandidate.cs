using System.Security.Cryptography;
using System.Text;

namespace Frelon.Core;

/// <summary>
/// Groupe calculé d'incidents susceptibles d'appartenir à une même campagne.
/// Ce résultat n'est ni persisté ni confirmé automatiquement.
/// </summary>
public sealed record CampaignCandidate
{
    /// <summary>Longueur de l'empreinte hexadécimale d'une composition de campagne.</summary>
    public const int FingerprintLength = 64;

    /// <summary>Crée une campagne candidate entièrement justifiée par ses liens.</summary>
    public CampaignCandidate(
        IReadOnlyList<Guid> incidentIds,
        DateTimeOffset firstObservedAt,
        DateTimeOffset lastObservedAt,
        IReadOnlyList<IncidentCorrelationLink> links)
    {
        ArgumentNullException.ThrowIfNull(incidentIds);
        ArgumentNullException.ThrowIfNull(links);

        if (incidentIds.Count < 2 ||
            incidentIds.Any(id => id == Guid.Empty) ||
            incidentIds.Distinct().Count() != incidentIds.Count)
        {
            throw new ArgumentException(
                "Une campagne candidate doit contenir au moins deux incidents distincts.",
                nameof(incidentIds));
        }

        if (lastObservedAt < firstObservedAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lastObservedAt),
                "La dernière observation ne peut pas précéder la première.");
        }

        var orderedIncidentIds = incidentIds.Order().ToArray();
        var incidentIdSet = orderedIncidentIds.ToHashSet();

        if (links.Count == 0 ||
            links.Any(link =>
                link is null ||
                !incidentIdSet.Contains(link.FirstIncidentId) ||
                !incidentIdSet.Contains(link.SecondIncidentId)))
        {
            throw new ArgumentException(
                "Les liens doivent relier les incidents de la campagne candidate.",
                nameof(links));
        }

        var linkedIncidentIds = links
            .SelectMany(link => new[] { link.FirstIncidentId, link.SecondIncidentId })
            .ToHashSet();
        if (!linkedIncidentIds.SetEquals(incidentIdSet) ||
            !LinksFormConnectedGroup(orderedIncidentIds, links))
        {
            throw new ArgumentException(
                "Les liens doivent former un groupe connecté couvrant tous les incidents.",
                nameof(links));
        }

        var distinctLinkCount = links
            .Select(link => link.FirstIncidentId.CompareTo(link.SecondIncidentId) < 0
                ? (link.FirstIncidentId, link.SecondIncidentId)
                : (link.SecondIncidentId, link.FirstIncidentId))
            .Distinct()
            .Count();
        if (distinctLinkCount != links.Count)
        {
            throw new ArgumentException(
                "Deux incidents ne peuvent être reliés qu'une seule fois.",
                nameof(links));
        }

        IncidentIds = orderedIncidentIds;
        Fingerprint = ComputeFingerprint(orderedIncidentIds);
        FirstObservedAt = firstObservedAt;
        LastObservedAt = lastObservedAt;
        Links = [.. links];
    }

    /// <summary>Identifiants des incidents rapprochés, dans un ordre stable.</summary>
    public IReadOnlyList<Guid> IncidentIds { get; }

    /// <summary>
    /// Empreinte stable de la composition de la campagne, indépendante de l'ordre
    /// d'entrée et des évolutions des règles de corrélation.
    /// </summary>
    public string Fingerprint { get; }

    /// <summary>Date du plus ancien incident du groupe.</summary>
    public DateTimeOffset FirstObservedAt { get; }

    /// <summary>Date du plus récent incident du groupe.</summary>
    public DateTimeOffset LastObservedAt { get; }

    /// <summary>Liens qualifiés qui expliquent la constitution du groupe.</summary>
    public IReadOnlyList<IncidentCorrelationLink> Links { get; }

    /// <summary>Indique si une valeur peut représenter une empreinte de campagne.</summary>
    public static bool IsValidFingerprint(string? value)
        => value is not null &&
           value.Length == FingerprintLength &&
           value.All(Uri.IsHexDigit);

    /// <summary>
    /// Compare le snapshot complet examiné, indépendamment de l'ordre des liens et indicateurs.
    /// L'empreinte seule ne suffit pas car elle représente uniquement la composition en incidents.
    /// </summary>
    public bool HasSameSnapshotAs(CampaignCandidate? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (!string.Equals(Fingerprint, other.Fingerprint, StringComparison.Ordinal) ||
            !HasExactTimestamp(FirstObservedAt, other.FirstObservedAt) ||
            !HasExactTimestamp(LastObservedAt, other.LastObservedAt) ||
            !IncidentIds.SequenceEqual(other.IncidentIds) ||
            Links.Count != other.Links.Count)
        {
            return false;
        }

        foreach (var link in Links)
        {
            var endpoints = NormalizeEndpoints(link);
            var otherLink = other.Links.SingleOrDefault(candidateLink =>
                NormalizeEndpoints(candidateLink) == endpoints);

            if (otherLink is null ||
                !NormalizeMatches(link.Matches)
                    .SequenceEqual(NormalizeMatches(otherLink.Matches)))
            {
                return false;
            }
        }

        return true;
    }

    private static string ComputeFingerprint(IReadOnlyList<Guid> incidentIds)
    {
        var canonicalComposition = string.Join(
            "|",
            incidentIds.Select(incidentId => incidentId.ToString("N")));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalComposition));
        return Convert.ToHexStringLower(hash);
    }

    private static bool HasExactTimestamp(
        DateTimeOffset first,
        DateTimeOffset second)
        => first.Ticks == second.Ticks &&
           first.Offset == second.Offset;

    private static (Guid First, Guid Second) NormalizeEndpoints(
        IncidentCorrelationLink link)
        => link.FirstIncidentId.CompareTo(link.SecondIncidentId) < 0
            ? (link.FirstIncidentId, link.SecondIncidentId)
            : (link.SecondIncidentId, link.FirstIncidentId);

    private static IReadOnlyList<MatchIdentity> NormalizeMatches(
        IReadOnlyList<SharedIocMatch> matches)
        => matches
            .Select(match => new MatchIdentity(
                match.Type,
                match.Value,
                match.Weight))
            .OrderBy(match => match.Type)
            .ThenBy(match => match.Value, StringComparer.Ordinal)
            .ThenBy(match => match.Weight)
            .ToArray();

    private static bool LinksFormConnectedGroup(
        IReadOnlyList<Guid> incidentIds,
        IReadOnlyList<IncidentCorrelationLink> links)
    {
        var visited = new HashSet<Guid>();
        var pending = new Queue<Guid>();
        pending.Enqueue(incidentIds[0]);

        while (pending.TryDequeue(out var incidentId))
        {
            if (!visited.Add(incidentId))
            {
                continue;
            }

            foreach (var link in links)
            {
                if (link.FirstIncidentId == incidentId)
                {
                    pending.Enqueue(link.SecondIncidentId);
                }
                else if (link.SecondIncidentId == incidentId)
                {
                    pending.Enqueue(link.FirstIncidentId);
                }
            }
        }

        return visited.Count == incidentIds.Count;
    }

    private readonly record struct MatchIdentity(
        IocType Type,
        string Value,
        int Weight);
}
