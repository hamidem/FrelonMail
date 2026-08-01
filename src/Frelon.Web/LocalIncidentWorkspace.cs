using Frelon.Core;
using Frelon.Mail;
using Frelon.Storage;

namespace Frelon.Web;

/// <summary>Orchestre l'analyse et l'historique local pour les interfaces utilisateur.</summary>
public sealed class LocalIncidentWorkspace
{
    private readonly IEmailIncidentAnalyzer _analyzer;
    private readonly IIncidentStore _store;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    /// <summary>Initialise l'espace de travail avec des dépendances explicites.</summary>
    public LocalIncidentWorkspace(IEmailIncidentAnalyzer analyzer, IIncidentStore store)
    {
        _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>Analyse un email puis conserve le même incident dans l'historique local.</summary>
    public async Task<FraudIncident> AnalyzeAndSaveAsync(
        Stream emlStream,
        string sourceFileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(emlStream);
        if (string.IsNullOrWhiteSpace(sourceFileName))
        {
            throw new ArgumentException("Le nom du fichier source est obligatoire.", nameof(sourceFileName));
        }

        var incident = await _analyzer
            .AnalyzeAsync(emlStream, sourceFileName, cancellationToken)
            .ConfigureAwait(false);

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await _store.SaveAsync(incident, cancellationToken).ConfigureAwait(false);
        return incident;
    }

    /// <summary>Retourne les incidents locaux les plus récents.</summary>
    public async Task<IReadOnlyList<IncidentSummary>> ListRecentAsync(
        int limit = 25,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        return await _store.ListRecentAsync(limit, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Charge le détail d'un incident local.</summary>
    public async Task<FraudIncident?> GetByIdAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        return await _store.GetByIdAsync(incidentId, cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_initialized)
            {
                await _store.InitializeAsync(cancellationToken).ConfigureAwait(false);
                _initialized = true;
            }
        }
        finally
        {
            _initializationLock.Release();
        }
    }
}
