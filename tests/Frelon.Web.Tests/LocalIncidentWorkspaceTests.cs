using Frelon.Core;
using Frelon.Mail;
using Frelon.Storage;

namespace Frelon.Web.Tests;

/// <summary>Vérifie l'orchestration métier utilisée par l'interface locale.</summary>
public sealed class LocalIncidentWorkspaceTests
{
    [Fact]
    public async Task AnalyzeAndSaveAsync_ConserveExactementLIncidentAnalyse()
    {
        var incident = BuildIncident();
        var analyzer = new StubAnalyzer(incident);
        var store = new RecordingStore();
        var workspace = new LocalIncidentWorkspace(analyzer, store);
        await using var source = new MemoryStream([1, 2, 3]);

        var result = await workspace.AnalyzeAndSaveAsync(
            source,
            "preuve.eml",
            TestContext.Current.CancellationToken);

        Assert.Same(incident, result);
        Assert.Equal("preuve.eml", analyzer.SourceFileName);
        Assert.Same(incident, Assert.Single(store.SavedIncidents));
        Assert.Equal(1, store.InitializeCount);
    }

    [Fact]
    public async Task LecturesSuccessives_InitialisentLeStockageUneSeuleFois()
    {
        var incident = BuildIncident();
        var store = new RecordingStore { Incident = incident };
        var workspace = new LocalIncidentWorkspace(new StubAnalyzer(incident), store);

        await workspace.ListRecentAsync(cancellationToken: TestContext.Current.CancellationToken);
        var result = await workspace.GetByIdAsync(incident.IncidentId, TestContext.Current.CancellationToken);

        Assert.Same(incident, result);
        Assert.Equal(1, store.InitializeCount);
    }

    [Fact]
    public async Task InitialisationsConcurrentes_NExecutentLeSchemaQuUneFois()
    {
        var incident = BuildIncident();
        var store = new RecordingStore { InitializationDelay = TimeSpan.FromMilliseconds(20) };
        var workspace = new LocalIncidentWorkspace(new StubAnalyzer(incident), store);

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            workspace.ListRecentAsync(cancellationToken: TestContext.Current.CancellationToken)));

        Assert.Equal(1, store.InitializeCount);
    }

    [Fact]
    public async Task AnalyzeAndSaveAsync_NomVide_RefuseAvantLAnalyse()
    {
        var incident = BuildIncident();
        var analyzer = new StubAnalyzer(incident);
        var workspace = new LocalIncidentWorkspace(analyzer, new RecordingStore());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            workspace.AnalyzeAndSaveAsync(
                Stream.Null,
                " ",
                TestContext.Current.CancellationToken));

        Assert.Equal(0, analyzer.CallCount);
    }

    private static FraudIncident BuildIncident()
        => new()
        {
            IncidentId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            CreatedAt = new DateTimeOffset(2026, 7, 16, 8, 0, 0, TimeSpan.Zero),
            Evidence = new EvidenceSource
            {
                FileName = "preuve.eml",
                ImportedAt = new DateTimeOffset(2026, 7, 16, 8, 0, 0, TimeSpan.Zero)
            },
            Identity = new MailIdentity(),
            Authentication = new AuthenticationAssessment(),
            Classification = FraudClassification.Unknown,
            RiskScore = new RiskScore { Value = 20, Level = RiskLevel.Low }
        };

    private sealed class StubAnalyzer(FraudIncident incident) : IEmailIncidentAnalyzer
    {
        public int CallCount { get; private set; }
        public string? SourceFileName { get; private set; }

        public Task<FraudIncident> AnalyzeAsync(
            Stream emlStream,
            string? sourceFileName = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            SourceFileName = sourceFileName;
            return Task.FromResult(incident);
        }
    }

    private sealed class RecordingStore : IIncidentStore
    {
        public int InitializeCount { get; private set; }
        public List<FraudIncident> SavedIncidents { get; } = [];
        public FraudIncident? Incident { get; init; }
        public TimeSpan InitializationDelay { get; init; }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            InitializeCount++;
            await Task.Delay(InitializationDelay, cancellationToken);
        }

        public Task SaveAsync(FraudIncident incident, CancellationToken cancellationToken = default)
        {
            SavedIncidents.Add(incident);
            return Task.CompletedTask;
        }

        public Task<FraudIncident?> GetByIdAsync(Guid incidentId, CancellationToken cancellationToken = default)
            => Task.FromResult(Incident);

        public Task<IReadOnlyList<IncidentSummary>> ListRecentAsync(
            int limit = 100,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<IncidentSummary>>([]);
    }
}
