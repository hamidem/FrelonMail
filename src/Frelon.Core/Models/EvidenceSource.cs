namespace Frelon.Core;

/// <summary>
/// Représente la source de preuve d'un incident — généralement le fichier .eml analysé.
/// </summary>
public sealed record EvidenceSource
{
    /// <summary>Chemin absolu ou relatif du fichier source, si disponible.</summary>
    public string? FilePath { get; init; }

    /// <summary>Nom du fichier source.</summary>
    public required string FileName { get; init; }

    /// <summary>Empreinte SHA-256 du fichier source, calculée à l'import.</summary>
    public string? Sha256 { get; init; }

    /// <summary>Date et heure d'import ou de réception du fichier, si disponible.</summary>
    public DateTimeOffset? ImportedAt { get; init; }
}
