namespace Frelon.Core;

/// <summary>
/// Représente un indicateur de compromission (IOC) extrait ou déduit de l'incident.
/// </summary>
public sealed record Ioc
{
    /// <summary>Type de l'indicateur (adresse IP, domaine, URL, hash, etc.).</summary>
    public required IocType Type { get; init; }

    /// <summary>Valeur de l'indicateur (ex. : une adresse IP, un nom de domaine, un hash SHA-256).</summary>
    public required string Value { get; init; }

    /// <summary>Niveau de confiance dans cet indicateur, entre 0.0 (incertain) et 1.0 (certain).</summary>
    public double Confidence { get; init; }

    /// <summary>Source ayant produit cet indicateur (ex. : nom du module d'analyse).</summary>
    public string? Source { get; init; }

    /// <summary>Date et heure de la première observation de cet indicateur dans l'incident.</summary>
    public DateTimeOffset? FirstSeen { get; init; }
}
