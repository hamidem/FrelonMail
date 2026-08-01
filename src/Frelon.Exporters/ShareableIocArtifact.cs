namespace Frelon.Exporters;

/// <summary>Document textuel minimisé destiné à quitter éventuellement Frelon.</summary>
public sealed record ShareableIocArtifact
{
    /// <summary>Crée un document en mémoire avec un nom de fichier simple.</summary>
    public ShareableIocArtifact(
        string fileName,
        string contentType,
        string content)
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

        FileName = fileName;
        ContentType = contentType;
        Content = content;
    }

    /// <summary>Nom de fichier suggéré.</summary>
    public string FileName { get; }

    /// <summary>Type MIME textuel.</summary>
    public string ContentType { get; }

    /// <summary>Contenu destiné au paquet partageable.</summary>
    public string Content { get; }
}
