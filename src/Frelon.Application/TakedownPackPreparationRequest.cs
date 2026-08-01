using Frelon.Core;
using Frelon.Reports;

namespace Frelon.Application;

/// <summary>
/// Choix explicites de l'analyste nécessaires à la préparation d'un takedown pack.
/// </summary>
public sealed record TakedownPackPreparationRequest
{
    /// <summary>Crée une demande sans accéder au stockage ni générer de document.</summary>
    public TakedownPackPreparationRequest(
        Guid packId,
        DateTimeOffset preparedAt,
        string campaignFingerprint,
        IReadOnlyList<TakedownRecipientType> recipients,
        string? analystNotes = null)
    {
        if (packId == Guid.Empty)
        {
            throw new ArgumentException(
                "L'identifiant du pack ne peut pas être vide.",
                nameof(packId));
        }

        if (preparedAt == default)
        {
            throw new ArgumentException(
                "La date de préparation est obligatoire.",
                nameof(preparedAt));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(campaignFingerprint);
        var normalizedFingerprint = campaignFingerprint.Trim().ToLowerInvariant();
        if (!CampaignCandidate.IsValidFingerprint(normalizedFingerprint))
        {
            throw new ArgumentException(
                "L'empreinte de campagne doit être une valeur SHA-256 hexadécimale.",
                nameof(campaignFingerprint));
        }

        ArgumentNullException.ThrowIfNull(recipients);
        if (recipients.Count == 0 ||
            recipients.Any(recipient => !Enum.IsDefined(recipient)) ||
            recipients.Distinct().Count() != recipients.Count)
        {
            throw new ArgumentException(
                "Au moins un rôle de destinataire valide et distinct est requis.",
                nameof(recipients));
        }

        var normalizedNotes = string.IsNullOrWhiteSpace(analystNotes)
            ? null
            : analystNotes.Trim();
        if (normalizedNotes?.Length > TakedownPackRequest.MaxAnalystNotesLength)
        {
            throw new ArgumentException(
                $"La note ne peut pas dépasser {TakedownPackRequest.MaxAnalystNotesLength} caractères.",
                nameof(analystNotes));
        }

        PackId = packId;
        PreparedAt = preparedAt;
        CampaignFingerprint = normalizedFingerprint;
        Recipients = [.. recipients];
        AnalystNotes = normalizedNotes;
    }

    /// <summary>Identifiant nouveau attribué au pack.</summary>
    public Guid PackId { get; }

    /// <summary>Date locale de préparation.</summary>
    public DateTimeOffset PreparedAt { get; }

    /// <summary>Composition de campagne explicitement choisie.</summary>
    public string CampaignFingerprint { get; }

    /// <summary>Rôles de destinataires retenus par l'analyste.</summary>
    public IReadOnlyList<TakedownRecipientType> Recipients { get; }

    /// <summary>Note locale facultative propre au pack.</summary>
    public string? AnalystNotes { get; }
}
