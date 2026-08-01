namespace Frelon.Mail;

/// <summary>Représente une pièce jointe MIME déjà décodée en mémoire.</summary>
public sealed record ParsedEmailAttachment
{
    /// <summary>Nom de fichier observé dans le message, si disponible.</summary>
    public string? FileName { get; init; }

    /// <summary>Type MIME observé pour la pièce jointe, si disponible.</summary>
    public string? ContentType { get; init; }

    /// <summary>Contenu binaire décodé de la pièce jointe.</summary>
    public required ReadOnlyMemory<byte> Content { get; init; }
}