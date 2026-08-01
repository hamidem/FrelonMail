using Frelon.Core;

namespace Frelon.Exporters;

/// <summary>
/// IOC explicitement sélectionné par l'analyste pour un partage contrôlé.
/// </summary>
public sealed record ShareableIocSelection
{
    /// <summary>Crée une sélection limitée aux types admis par le profil strict.</summary>
    public ShareableIocSelection(IocType type, string value)
    {
        if (type is not (IocType.Domain or IocType.Hash))
        {
            throw new ArgumentOutOfRangeException(
                nameof(type),
                "Le profil strict accepte uniquement les domaines et SHA-256.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        Type = type;
        Value = value.Trim();
    }

    /// <summary>Type d'IOC approuvé.</summary>
    public IocType Type { get; }

    /// <summary>Valeur choisie par l'analyste avant normalisation défensive.</summary>
    public string Value { get; }
}
