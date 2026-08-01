using Frelon.Core;
using Frelon.Exporters;
using Frelon.Storage;

namespace Frelon.Application;

/// <summary>
/// Assemble en lecture seule les sources locales d'un export d'IOC contrôlé.
/// </summary>
public sealed class LocalShareableIocPreparationService
    : IShareableIocPreparationService
{
    private readonly IIncidentStore _incidentStore;
    private readonly IIncidentReviewStore _reviewStore;
    private readonly IShareableIocExporter _exporter;

    /// <summary>Crée le cas d'usage avec ses dépendances locales explicites.</summary>
    public LocalShareableIocPreparationService(
        IIncidentStore incidentStore,
        IIncidentReviewStore reviewStore,
        IShareableIocExporter exporter)
    {
        ArgumentNullException.ThrowIfNull(incidentStore);
        ArgumentNullException.ThrowIfNull(reviewStore);
        ArgumentNullException.ThrowIfNull(exporter);

        _incidentStore = incidentStore;
        _reviewStore = reviewStore;
        _exporter = exporter;
    }

    /// <inheritdoc />
    public async Task<ShareableIocExportResult> PrepareAsync(
        ShareableIocPreparationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var incidents = new List<FraudIncident>(request.IncidentIds.Count);
        var reviews = new List<IncidentReviewDecision>(request.IncidentIds.Count);

        foreach (var incidentId in request.IncidentIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var incident = await _incidentStore
                .GetByIdAsync(incidentId, cancellationToken)
                .ConfigureAwait(false);
            if (incident is null)
            {
                throw new InvalidDataException(
                    $"Le snapshot de l'incident '{incidentId:D}' est introuvable.");
            }

            var review = await _reviewStore
                .GetLatestReviewAsync(incidentId, cancellationToken)
                .ConfigureAwait(false);
            if (review is null)
            {
                throw new InvalidOperationException(
                    $"L'incident '{incidentId:D}' ne possède aucune décision humaine.");
            }

            if (review.IncidentId != incidentId)
            {
                throw new InvalidDataException(
                    $"La revue locale de l'incident '{incidentId:D}' est incohérente.");
            }

            incidents.Add(incident);
            reviews.Add(review);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return _exporter.Export(new ShareableIocExportRequest(
            request.ExportId,
            request.PreparedAt,
            incidents,
            reviews,
            request.ApprovedIocs));
    }
}
