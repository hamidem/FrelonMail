using Frelon.Exporters;

namespace Frelon.Application;

/// <summary>
/// Sélection explicite des incidents et IOC autorisés pour un partage contrôlé.
/// </summary>
public sealed record ShareableIocPreparationRequest
{
    /// <summary>Crée une demande sans lire les incidents ni produire de document.</summary>
    public ShareableIocPreparationRequest(
        Guid exportId,
        DateTimeOffset preparedAt,
        IReadOnlyList<Guid> incidentIds,
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

        ArgumentNullException.ThrowIfNull(incidentIds);
        if (incidentIds.Count == 0 ||
            incidentIds.Any(incidentId => incidentId == Guid.Empty) ||
            incidentIds.Distinct().Count() != incidentIds.Count)
        {
            throw new ArgumentException(
                "Au moins un incident présent et distinct doit être sélectionné.",
                nameof(incidentIds));
        }

        if (incidentIds.Contains(exportId))
        {
            throw new ArgumentException(
                "L'identifiant de l'export doit être indépendant des incidents locaux.",
                nameof(exportId));
        }

        ArgumentNullException.ThrowIfNull(approvedIocs);
        if (approvedIocs.Count == 0 ||
            approvedIocs.Any(ioc => ioc is null) ||
            approvedIocs
                .Select(ioc => (ioc.Type, ioc.Value))
                .Distinct()
                .Count() != approvedIocs.Count)
        {
            throw new ArgumentException(
                "Au moins un IOC distinct doit être approuvé explicitement.",
                nameof(approvedIocs));
        }

        ExportId = exportId;
        PreparedAt = preparedAt;
        IncidentIds = incidentIds.Order().ToArray();
        ApprovedIocs = [.. approvedIocs];
    }

    /// <summary>Identifiant propre au paquet partageable.</summary>
    public Guid ExportId { get; }

    /// <summary>Date locale conservée dans l'audit sensible.</summary>
    public DateTimeOffset PreparedAt { get; }

    /// <summary>Incidents locaux choisis dans un ordre stable.</summary>
    public IReadOnlyList<Guid> IncidentIds { get; }

    /// <summary>Valeurs que l'analyste autorise explicitement à quitter Frelon.</summary>
    public IReadOnlyList<ShareableIocSelection> ApprovedIocs { get; }
}
