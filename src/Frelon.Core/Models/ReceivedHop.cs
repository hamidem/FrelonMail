namespace Frelon.Core;

/// <summary>
/// Représente un relais dans la chaîne des headers Received du mail.
/// Chaque hop correspond à un serveur par lequel le message a transité.
/// </summary>
public sealed record ReceivedHop
{
    /// <summary>Position du relais dans la chaîne (0 = premier relais émetteur).</summary>
    public required int Position { get; init; }

    /// <summary>Nom ou adresse du serveur émetteur déclaré dans ce relais.</summary>
    public string? From { get; init; }

    /// <summary>Nom ou adresse du serveur récepteur déclaré dans ce relais.</summary>
    public string? By { get; init; }

    /// <summary>Protocole de transport utilisé (ex. : SMTP, ESMTPS).</summary>
    public string? With { get; init; }

    /// <summary>Adresse IP extraite du relais, si identifiable.</summary>
    public string? IpAddress { get; init; }

    /// <summary>Horodatage déclaré dans ce relais, si présent.</summary>
    public DateTimeOffset? Timestamp { get; init; }

    /// <summary>Valeur brute du header Received correspondant à ce relais.</summary>
    public required string RawValue { get; init; }
}
