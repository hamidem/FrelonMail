using Frelon.Core;
using Frelon.Storage;

namespace Frelon.Web.Tests;

/// <summary>Vérifie l'orchestration des campagnes utilisée par le cockpit local.</summary>
public sealed class LocalCampaignWorkspaceTests
{
    [Fact]
    public async Task ConsultationsSuccessives_InitialisentLeStockageUneSeuleFois()
    {
        var store = new RecordingStore();
        var consultation = new RecordingConsultationService();
        var workspace = new LocalCampaignWorkspace(
            store,
            consultation,
            new RecordingReviewService());

        await workspace.ListCurrentAsync(75, TestContext.Current.CancellationToken);
        await workspace.GetDetailsAsync(
            new string('a', CampaignCandidate.FingerprintLength),
            80,
            30,
            TestContext.Current.CancellationToken);

        Assert.Equal(1, store.InitializeCount);
        Assert.Equal(75, consultation.ListIncidentLimit);
        Assert.Equal(80, consultation.DetailsIncidentLimit);
        Assert.Equal(30, consultation.ReviewLimit);
    }

    [Fact]
    public async Task InitialisationsConcurrentes_NExecutentLeSchemaQuUneFois()
    {
        var store = new RecordingStore
        {
            InitializationDelay = TimeSpan.FromMilliseconds(20)
        };
        var workspace = new LocalCampaignWorkspace(
            store,
            new RecordingConsultationService(),
            new RecordingReviewService());

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            workspace.ListCurrentAsync(
                cancellationToken: TestContext.Current.CancellationToken)));

        Assert.Equal(1, store.InitializeCount);
    }

    [Fact]
    public async Task RecordCurrentAsync_TransmetLeSnapshotEtLaFenetre()
    {
        var candidate = BuildCandidate();
        var decision = new CampaignReviewDecision(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            candidate,
            CampaignReviewVerdict.Confirmed,
            new DateTimeOffset(2026, 7, 23, 10, 0, 0, TimeSpan.Zero));
        var reviewService = new RecordingReviewService();
        var workspace = new LocalCampaignWorkspace(
            new RecordingStore(),
            new RecordingConsultationService(),
            reviewService);

        var result = await workspace.RecordCurrentAsync(
            decision,
            125,
            TestContext.Current.CancellationToken);

        Assert.Same(decision, result);
        Assert.Same(decision, reviewService.Decision);
        Assert.Equal(125, reviewService.IncidentLimit);
    }

    private static CampaignCandidate BuildCandidate()
    {
        var first = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var second = Guid.Parse("22222222-2222-2222-2222-222222222222");
        return new CampaignCandidate(
            [first, second],
            new DateTimeOffset(2026, 7, 22, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 23, 8, 0, 0, TimeSpan.Zero),
            [
                new IncidentCorrelationLink(
                    first,
                    second,
                    [new SharedIocMatch(IocType.Domain, "example.test", 60)])
            ]);
    }

    private sealed class RecordingStore : IIncidentStore
    {
        public int InitializeCount { get; private set; }
        public TimeSpan InitializationDelay { get; init; }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            InitializeCount++;
            await Task.Delay(InitializationDelay, cancellationToken);
        }

        public Task SaveAsync(
            FraudIncident incident,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<FraudIncident?> GetByIdAsync(
            Guid incidentId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<FraudIncident?>(null);

        public Task<IReadOnlyList<IncidentSummary>> ListRecentAsync(
            int limit = 100,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<IncidentSummary>>([]);
    }

    private sealed class RecordingConsultationService : ICampaignConsultationService
    {
        public int? ListIncidentLimit { get; private set; }
        public int? DetailsIncidentLimit { get; private set; }
        public int? ReviewLimit { get; private set; }

        public Task<IReadOnlyList<CampaignConsultationSummary>> ListCurrentAsync(
            int incidentLimit = 100,
            CancellationToken cancellationToken = default)
        {
            ListIncidentLimit = incidentLimit;
            return Task.FromResult<IReadOnlyList<CampaignConsultationSummary>>([]);
        }

        public Task<CampaignConsultationDetails?> GetDetailsAsync(
            string candidateFingerprint,
            int incidentLimit = 100,
            int reviewLimit = 100,
            CancellationToken cancellationToken = default)
        {
            DetailsIncidentLimit = incidentLimit;
            ReviewLimit = reviewLimit;
            return Task.FromResult<CampaignConsultationDetails?>(null);
        }
    }

    private sealed class RecordingReviewService : ICampaignReviewService
    {
        public CampaignReviewDecision? Decision { get; private set; }
        public int? IncidentLimit { get; private set; }

        public Task<CampaignReviewDecision> RecordCurrentAsync(
            CampaignReviewDecision decision,
            int incidentLimit = 100,
            CancellationToken cancellationToken = default)
        {
            Decision = decision;
            IncidentLimit = incidentLimit;
            return Task.FromResult(decision);
        }
    }
}
