namespace Frelon.Core;

/// <summary>
/// Représente une pièce jointe détectée dans le mail.
/// Ce type ne contient pas le contenu binaire de la pièce jointe.
/// </summary>
public sealed record AttachmentIndicator
{
    /// <summary>Nom du fichier tel que déclaré dans le mail.</summary>
    public required string FileName { get; init; }

    /// <summary>Type MIME de la pièce jointe (ex. : application/pdf, text/html).</summary>
    public string? ContentType { get; init; }

    /// <summary>Taille de la pièce jointe en octets, si disponible.</summary>
    public long? SizeBytes { get; init; }

    /// <summary>Empreinte SHA-256 de la pièce jointe, calculée à l'extraction.</summary>
    public string? Sha256 { get; init; }

    /// <summary>Indique si cette pièce jointe a été identifiée comme suspecte.</summary>
    public bool IsSuspicious { get; init; }

    /// <summary>Raisons ayant conduit à qualifier cette pièce jointe de suspecte.</summary>
    public IReadOnlyList<string> Reasons { get; init; } = [];
}
