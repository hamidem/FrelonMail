using Frelon.Core;
using Frelon.Storage;

namespace Frelon.Web;

/// <summary>
/// Initialise le stockage local avant de consulter ou valider les campagnes calculées.
/// </summary>
public sealed class LocalCampaignWorkspace
{
    private readonly IIncidentStore _incidentStore;
    private readonly ICampaignConsultationService _consultationService;
    private readonly ICampaignReviewService _reviewService;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    /// <summary>Crée l'espace de travail avec ses dépendances explicites.</summary>
    public LocalCampaignWorkspace(
        IIncidentStore incidentStore,
        ICampaignConsultationService consultationService,
        ICampaignReviewService reviewService)
    {
        _incidentStore = incidentStore ?? throw new ArgumentNullException(nameof(incidentStore));
        _consultationService = consultationService ??
            throw new ArgumentNullException(nameof(consultationService));
        _reviewService = reviewService ?? throw new ArgumentNullException(nameof(reviewService));
    }

    /// <summary>Liste les campagnes détectées dans les incidents récents.</summary>
    public async Task<IReadOnlyList<CampaignConsultationSummary>> ListCurrentAsync(
        int incidentLimit = 100,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        return await _consultationService
            .ListCurrentAsync(incidentLimit, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Charge le détail courant ou historique d'une campagne.</summary>
    public async Task<CampaignConsultationDetails?> GetDetailsAsync(
        string fingerprint,
        int incidentLimit = 100,
        int reviewLimit = 50,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        return await _consultationService
            .GetDetailsAsync(fingerprint, incidentLimit, reviewLimit, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Enregistre une décision seulement si le snapshot affiché est encore courant.</summary>
    public async Task<CampaignReviewDecision> RecordCurrentAsync(
        CampaignReviewDecision decision,
        int incidentLimit = 100,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        return await _reviewService
            .RecordCurrentAsync(decision, incidentLimit, cancellationToken)
            .ConfigureAwait(false);
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
                await _incidentStore.InitializeAsync(cancellationToken).ConfigureAwait(false);
                _initialized = true;
            }
        }
        finally
        {
            _initializationLock.Release();
        }
    }
}
