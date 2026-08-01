namespace Frelon.Core;

/// <summary>
/// Représente les identités déclarées dans les en-têtes du mail.
/// Ces valeurs sont issues du mail lui-même et peuvent être falsifiées.
/// </summary>
public sealed record MailIdentity
{
    /// <summary>Adresse de l'expéditeur déclaré (header From).</summary>
    public string? From { get; init; }

    /// <summary>Adresse de réponse déclarée (header Reply-To), si différente de From.</summary>
    public string? ReplyTo { get; init; }

    /// <summary>Adresse de retour des bounces (header Return-Path).</summary>
    public string? ReturnPath { get; init; }

    /// <summary>Identifiant unique du message (header Message-ID).</summary>
    public string? MessageId { get; init; }

    /// <summary>Objet du message (header Subject).</summary>
    public string? Subject { get; init; }
}
