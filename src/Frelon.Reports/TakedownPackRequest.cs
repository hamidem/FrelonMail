using Frelon.Core;

namespace Frelon.Reports;

/// <summary>
/// Données explicitement sélectionnées par l'analyste pour préparer un takedown pack.
/// </summary>
public sealed record TakedownPackRequest
{
    /// <summary>Longueur maximale d'une note propre au pack.</summary>
    public const int MaxAnalystNotesLength = 2000;

    /// <summary>Crée une demande locale cohérente, sans déclencher aucun envoi.</summary>
    public TakedownPackRequest(
        Guid packId,
        DateTimeOffset preparedAt,
        CampaignReviewDecision campaignReview,
        IReadOnlyList<FraudIncident> incidents,
        IReadOnlyList<IncidentReviewDecision> incidentReviews,
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

        ArgumentNullException.ThrowIfNull(campaignReview);
        ArgumentNullException.ThrowIfNull(incidents);
        ArgumentNullException.ThrowIfNull(incidentReviews);
        ArgumentNullException.ThrowIfNull(recipients);

        if (incidents.Count == 0 ||
            incidents.Any(incident => incident is null) ||
            incidents.Select(incident => incident.IncidentId).Distinct().Count() != incidents.Count)
        {
            throw new ArgumentException(
                "Les incidents doivent être présents et distincts.",
                nameof(incidents));
        }

        if (incidentReviews.Count == 0 ||
            incidentReviews.Any(review => review is null) ||
            incidentReviews.Select(review => review.IncidentId).Distinct().Count() != incidentReviews.Count)
        {
            throw new ArgumentException(
                "Les décisions d'incident doivent être présentes et distinctes.",
                nameof(incidentReviews));
        }

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
        if (normalizedNotes?.Length > MaxAnalystNotesLength)
        {
            throw new ArgumentException(
                $"La note ne peut pas dépasser {MaxAnalystNotesLength} caractères.",
                nameof(analystNotes));
        }

        PackId = packId;
        PreparedAt = preparedAt;
        CampaignReview = campaignReview;
        Incidents = [.. incidents];
        IncidentReviews = [.. incidentReviews];
        Recipients = [.. recipients];
        AnalystNotes = normalizedNotes;
    }

    /// <summary>Identifiant traçable du pack.</summary>
    public Guid PackId { get; }

    /// <summary>Date de préparation locale.</summary>
    public DateTimeOffset PreparedAt { get; }

    /// <summary>Décision humaine confirmant la composition de campagne examinée.</summary>
    public CampaignReviewDecision CampaignReview { get; }

    /// <summary>Snapshots des incidents inclus.</summary>
    public IReadOnlyList<FraudIncident> Incidents { get; }

    /// <summary>Décisions humaines individuelles considérées comme courantes.</summary>
    public IReadOnlyList<IncidentReviewDecision> IncidentReviews { get; }

    /// <summary>Rôles de destinataires choisis manuellement.</summary>
    public IReadOnlyList<TakedownRecipientType> Recipients { get; }

    /// <summary>Note locale facultative de l'analyste.</summary>
    public string? AnalystNotes { get; }
}
