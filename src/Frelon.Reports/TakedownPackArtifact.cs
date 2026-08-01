namespace Frelon.Reports;

/// <summary>Document textuel préparé en mémoire dans un takedown pack.</summary>
public sealed record TakedownPackArtifact
{
    /// <summary>Crée un document sans l'écrire sur le disque.</summary>
    public TakedownPackArtifact(
        string fileName,
        string contentType,
        string content,
        TakedownRecipientType? recipient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentNullException.ThrowIfNull(content);

        if (Path.GetFileName(fileName) != fileName)
        {
            throw new ArgumentException(
                "Le nom du document ne doit pas contenir de chemin.",
                nameof(fileName));
        }

        if (recipient is not null && !Enum.IsDefined(recipient.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(recipient));
        }

        FileName = fileName;
        ContentType = contentType;
        Content = content;
        Recipient = recipient;
    }

    /// <summary>Nom de fichier suggéré dans une future archive.</summary>
    public string FileName { get; }

    /// <summary>Type MIME textuel.</summary>
    public string ContentType { get; }

    /// <summary>Contenu intégral du document.</summary>
    public string Content { get; }

    /// <summary>Rôle destinataire, ou null pour les documents communs au pack.</summary>
    public TakedownRecipientType? Recipient { get; }
}
