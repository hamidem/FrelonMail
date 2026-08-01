using Frelon.Core;

namespace Frelon.Exporters;

/// <summary>
/// Sélection locale des incidents autorisés par une décision humaine pour un export minimisé.
/// </summary>
public sealed record ShareableIocExportRequest
{
    /// <summary>Crée une demande explicite sans produire ni transmettre de document.</summary>
    public ShareableIocExportRequest(
        Guid exportId,
        DateTimeOffset preparedAt,
        IReadOnlyList<FraudIncident> incidents,
        IReadOnlyList<IncidentReviewDecision> incidentReviews,
        IReadOnlyList<ShareableIocSelection> approvedIocs)
    {
        if (exportId == Guid.Empty)
        {
            throw new ArgumentException(
                "L'identifiant de l'export ne peut pas être vide.",
                nameof(exportId));
        }

        if (preparedAt == default)
        {
            throw new ArgumentException(
                "La date de préparation est obligatoire.",
                nameof(preparedAt));
        }

        ArgumentNullException.ThrowIfNull(incidents);
        ArgumentNullException.ThrowIfNull(incidentReviews);
        ArgumentNullException.ThrowIfNull(approvedIocs);

        if (incidents.Count == 0 ||
            incidents.Any(incident => incident is null) ||
            incidents.Any(incident => incident.IncidentId == Guid.Empty) ||
            incidents.Select(incident => incident.IncidentId).Distinct().Count() != incidents.Count)
        {
            throw new ArgumentException(
                "Les incidents doivent être présents et distincts.",
                nameof(incidents));
        }

        if (incidentReviews.Count == 0 ||
            incidentReviews.Any(review => review is null) ||
            incidentReviews.Select(review => review.IncidentId).Distinct().Count() != incidentReviews.Count ||
            incidentReviews.Select(review => review.ReviewId).Distinct().Count() != incidentReviews.Count)
        {
            throw new ArgumentException(
                "Les décisions d'incident doivent être présentes et distinctes.",
                nameof(incidentReviews));
        }

        if (incidents.Any(incident => incident.IncidentId == exportId) ||
            incidentReviews.Any(review => review.ReviewId == exportId))
        {
            throw new ArgumentException(
                "L'identifiant de l'export doit être indépendant des références locales.",
                nameof(exportId));
        }

        if (approvedIocs.Count == 0 ||
            approvedIocs.Any(ioc => ioc is null) ||
            approvedIocs
                .Select(ioc => (ioc.Type, ioc.Value))
                .Distinct()
                .Count() != approvedIocs.Count)
        {
            throw new ArgumentException(
                "Au moins un IOC distinct doit être sélectionné explicitement.",
                nameof(approvedIocs));
        }

        ExportId = exportId;
        PreparedAt = preparedAt;
        Incidents = [.. incidents];
        IncidentReviews = [.. incidentReviews];
        ApprovedIocs = [.. approvedIocs];
    }

    /// <summary>Identifiant propre à ce partage, sans lien dérivable avec un incident.</summary>
    public Guid ExportId { get; }

    /// <summary>Date et heure locales conservées uniquement dans l'audit.</summary>
    public DateTimeOffset PreparedAt { get; }

    /// <summary>Incidents sélectionnés pour l'agrégation.</summary>
    public IReadOnlyList<FraudIncident> Incidents { get; }

    /// <summary>Décisions humaines individuelles considérées comme courantes.</summary>
    public IReadOnlyList<IncidentReviewDecision> IncidentReviews { get; }

    /// <summary>Valeurs explicitement approuvées pour ce partage.</summary>
    public IReadOnlyList<ShareableIocSelection> ApprovedIocs { get; }
}
