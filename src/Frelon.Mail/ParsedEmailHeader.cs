namespace Frelon.Mail;

/// <summary>Représente un header extrait d'un email.</summary>
public sealed record ParsedEmailHeader
{
    /// <summary>Nom du header (ex : "From", "Subject").</summary>
    public required string Name { get; init; }

    /// <summary>Valeur du header, repliage résolu et espaces de début/fin supprimés.</summary>
    public required string Value { get; init; }
}
