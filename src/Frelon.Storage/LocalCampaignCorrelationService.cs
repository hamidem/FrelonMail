using Frelon.Core;

namespace Frelon.Storage;

/// <summary>
/// Orchestre la corrélation à partir des snapshots conservés localement.
/// </summary>
public sealed class LocalCampaignCorrelationService : ICampaignCorrelationService
{
    private readonly IIncidentStore _incidentStore;
    private readonly IIncidentCorrelator _correlator;

    /// <summary>Crée le service avec ses dépendances explicites.</summary>
    public LocalCampaignCorrelationService(
        IIncidentStore incidentStore,
        IIncidentCorrelator correlator)
    {
        ArgumentNullException.ThrowIfNull(incidentStore);
        ArgumentNullException.ThrowIfNull(correlator);

        _incidentStore = incidentStore;
        _correlator = correlator;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CampaignCandidate>> FindRecentCandidatesAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                "La limite doit être comprise entre 1 et 500.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var summaries = await _incidentStore
            .ListRecentAsync(limit, cancellationToken)
            .ConfigureAwait(false);
        var incidents = new List<FraudIncident>(summaries.Count);

        foreach (var summary in summaries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var incident = await _incidentStore
                .GetByIdAsync(summary.IncidentId, cancellationToken)
                .ConfigureAwait(false);

            if (incident is null)
            {
                throw new InvalidDataException(
                    $"Le snapshot de l'incident {summary.IncidentId:D} est introuvable.");
            }

            incidents.Add(incident);
        }

        return _correlator.FindCandidates(incidents);
    }
}
