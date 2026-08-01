namespace Frelon.Mail;

/// <summary>Résultat du parsing d'une preuve de courrier électronique.</summary>
public sealed record ParsedEmail
{
    /// <summary>Contenu brut réversible du fichier source tel que lu depuis le flux.</summary>
    public required string RawContent { get; init; }

    /// <summary>Empreinte SHA-256 des octets source exacts, en hexadécimal minuscule.</summary>
    public required string SourceSha256 { get; init; }

    /// <summary>Liste des headers extraits, y compris les doublons.</summary>
    public required IReadOnlyList<ParsedEmailHeader> Headers { get; init; }

    /// <summary>Corps textuel de l'email, ou <see langword="null"/> si absent.</summary>
    public string? BodyText { get; init; }

    /// <summary>
    /// Corps HTML de l'email, ou <see langword="null"/> si absent.
    /// Non parsé dans le MVP actuel.
    /// </summary>
    public string? BodyHtml { get; init; }

    /// <summary>Pièces jointes MIME décodées en mémoire.</summary>
    public IReadOnlyList<ParsedEmailAttachment> Attachments { get; init; } = [];
}
