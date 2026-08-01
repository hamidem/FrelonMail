namespace Frelon.Core;

/// <summary>
/// Indicateur commun ayant contribué au rapprochement de deux incidents.
/// </summary>
public sealed record SharedIocMatch
{
    /// <summary>Crée une raison de corrélation validée.</summary>
    public SharedIocMatch(IocType type, string value, int weight)
    {
        if (type is IocType.Unknown or IocType.FileName)
        {
            throw new ArgumentOutOfRangeException(
                nameof(type),
                "Le type d'IOC ne peut pas contribuer à une corrélation.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (weight <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(weight),
                "Le poids doit être strictement positif.");
        }

        Type = type;
        Value = value;
        Weight = weight;
    }

    /// <summary>Type de l'indicateur partagé.</summary>
    public IocType Type { get; }

    /// <summary>Valeur normalisée comparée par le moteur.</summary>
    public string Value { get; }

    /// <summary>Contribution déterministe de cet indicateur au score du lien.</summary>
    public int Weight { get; }
}
