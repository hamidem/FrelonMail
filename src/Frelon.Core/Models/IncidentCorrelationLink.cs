namespace Frelon.Core;

/// <summary>
/// Rapprochement explicable entre deux incidents distincts.
/// </summary>
public sealed record IncidentCorrelationLink
{
    /// <summary>Crée un lien cohérent avec ses raisons.</summary>
    public IncidentCorrelationLink(
        Guid firstIncidentId,
        Guid secondIncidentId,
        IReadOnlyList<SharedIocMatch> matches)
    {
        ArgumentNullException.ThrowIfNull(matches);

        if (firstIncidentId == Guid.Empty)
        {
            throw new ArgumentException(
                "L'identifiant du premier incident est obligatoire.",
                nameof(firstIncidentId));
        }

        if (secondIncidentId == Guid.Empty)
        {
            throw new ArgumentException(
                "L'identifiant du second incident est obligatoire.",
                nameof(secondIncidentId));
        }

        if (firstIncidentId == secondIncidentId)
        {
            throw new ArgumentException(
                "Un incident ne peut pas être corrélé avec lui-même.",
                nameof(secondIncidentId));
        }

        if (matches.Count == 0 || matches.Any(match => match is null))
        {
            throw new ArgumentException(
                "Un lien doit conserver au moins un indicateur partagé.",
                nameof(matches));
        }

        FirstIncidentId = firstIncidentId;
        SecondIncidentId = secondIncidentId;
        Matches = [.. matches];
        Score = matches.Sum(match => match.Weight);
    }

    /// <summary>Identifiant du premier incident.</summary>
    public Guid FirstIncidentId { get; }

    /// <summary>Identifiant du second incident.</summary>
    public Guid SecondIncidentId { get; }

    /// <summary>
    /// Score de rapprochement déterministe. Il s'agit d'un cumul de règles,
    /// jamais d'une probabilité.
    /// </summary>
    public int Score { get; }

    /// <summary>Indicateurs partagés justifiant le rapprochement.</summary>
    public IReadOnlyList<SharedIocMatch> Matches { get; }
}
