namespace Frelon.Core;

/// <summary>
/// Représente une URL extraite du corps ou des en-têtes du mail.
/// Aucun appel réseau ne doit être effectué à partir de ce type.
/// </summary>
public sealed record UrlIndicator
{
    /// <summary>Valeur brute de l'URL telle qu'extraite du mail.</summary>
    public required string RawValue { get; init; }

    /// <summary>Valeur normalisée de l'URL (décodée, sans tracking, etc.), si calculable.</summary>
    public string? NormalizedValue { get; init; }

    /// <summary>Nom d'hôte extrait de l'URL.</summary>
    public string? Host { get; init; }

    /// <summary>Schéma de l'URL (ex. : http, https, ftp).</summary>
    public string? Scheme { get; init; }

    /// <summary>Indique si cette URL a été identifiée comme suspecte.</summary>
    public bool IsSuspicious { get; init; }

    /// <summary>Raisons ayant conduit à qualifier cette URL de suspecte.</summary>
    public IReadOnlyList<string> Reasons { get; init; } = [];
}
