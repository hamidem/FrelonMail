using Frelon.Core;

namespace Frelon.Storage.Tests;

/// <summary>
/// Tests de l'orchestration locale des corrélations.
/// </summary>
public sealed class LocalCampaignCorrelationServiceTests
{
    [Fact]
    public void Constructeur_SansStore_LeveArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => new LocalCampaignCorrelationService(
                null!,
                new BasicIncidentCorrelator()));

        Assert.Equal("incidentStore", exception.ParamName);
    }

    [Fact]
    public void Constructeur_SansCorrelateur_LeveArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => new LocalCampaignCorrelationService(
                new MemoryIncidentStore([]),
                null!));

        Assert.Equal("correlator", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    public async Task FindRecentCandidatesAsync_AvecLimiteInvalide_LeveArgumentOutOfRangeException(
        int limit)
    {
        var service = CreateService([]);

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.FindRecentCandidatesAsync(limit, CancellationToken.None));

        Assert.Equal("limit", exception.ParamName);
    }

    [Fact]
    public async Task FindRecentCandidatesAsync_TransmetLaLimiteAuStore()
    {
        var store = new MemoryIncidentStore([]);
        var service = new LocalCampaignCorrelationService(
            store,
            new BasicIncidentCorrelator());

        await service.FindRecentCandidatesAsync(37, TestContext.Current.CancellationToken);

        Assert.Equal(37, store.LastLimit);
    }

    [Fact]
    public async Task FindRecentCandidatesAsync_ChargeLesSnapshotsEtRetourneLaCampagne()
    {
        var first = BuildIncident(1, "https://fraud.example/login");
        var second = BuildIncident(2, "HTTPS://FRAUD.EXAMPLE:443/login");
        var service = CreateService([first, second]);

        var candidate = Assert.Single(
            await service.FindRecentCandidatesAsync(
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal([first.IncidentId, second.IncidentId], candidate.IncidentIds);
        Assert.Equal(BasicIncidentCorrelator.UrlWeight, Assert.Single(candidate.Links).Score);
    }

    [Fact]
    public async Task FindRecentCandidatesAsync_SnapshotAbsent_SignaleUneIncoherenceLocale()
    {
        var missingIncidentId = Guid.Parse("00000000-0000-0000-0000-000000000099");
        var store = new MemoryIncidentStore(
            [],
            [BuildSummary(missingIncidentId, 99)]);
        var service = new LocalCampaignCorrelationService(
            store,
            new BasicIncidentCorrelator());

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => service.FindRecentCandidatesAsync(
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains(missingIncidentId.ToString("D"), exception.Message);
    }

    [Fact]
    public async Task FindRecentCandidatesAsync_AvecTokenAnnule_NInterrogePasLeStore()
    {
        var store = new MemoryIncidentStore([]);
        var service = new LocalCampaignCorrelationService(
            store,
            new BasicIncidentCorrelator());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.FindRecentCandidatesAsync(
                cancellationToken: cancellation.Token));

        Assert.Null(store.LastLimit);
    }

    private static LocalCampaignCorrelationService CreateService(
        IReadOnlyList<FraudIncident> incidents)
        => new(
            new MemoryIncidentStore(incidents),
            new BasicIncidentCorrelator());

    private static FraudIncident BuildIncident(int id, string url)
        => new()
        {
            IncidentId = Guid.Parse($"00000000-0000-0000-0000-{id:D12}"),
            CreatedAt = new DateTimeOffset(2026, 7, 23, 9, id, 0, TimeSpan.Zero),
            Evidence = new EvidenceSource
            {
                FileName = $"incident-{id}.eml",
                Sha256 = new((char)('a' + id), 64),
                ImportedAt = new DateTimeOffset(2026, 7, 23, 9, id, 0, TimeSpan.Zero),
            },
            Identity = new MailIdentity(),
            Authentication = new AuthenticationAssessment(),
            Iocs =
            [
                new Ioc
                {
                    Type = IocType.Url,
                    Value = url,
                    Confidence = 0.8,
                },
            ],
            Classification = FraudClassification.Unknown,
            RiskScore = new RiskScore
            {
                Value = 0,
                Level = RiskLevel.Unknown,
            },
        };

    private static IncidentSummary BuildSummary(Guid incidentId, int minute)
    {
        var observedAt = new DateTimeOffset(2026, 7, 23, 9, 0, 0, TimeSpan.Zero)
            .AddMinutes(minute);

        return new IncidentSummary
        {
            IncidentId = incidentId,
            CreatedAt = observedAt,
            ImportedAt = observedAt,
            SourceFileName = $"incident-{minute}.eml",
            RiskValue = 0,
            RiskLevel = RiskLevel.Unknown,
            Classification = FraudClassification.Unknown,
        };
    }

    private sealed class MemoryIncidentStore : IIncidentStore
    {
        private readonly IReadOnlyDictionary<Guid, FraudIncident> _incidents;
        private readonly IReadOnlyList<IncidentSummary> _summaries;

        public MemoryIncidentStore(
            IReadOnlyList<FraudIncident> incidents,
            IReadOnlyList<IncidentSummary>? summaries = null)
        {
            _incidents = incidents.ToDictionary(incident => incident.IncidentId);
            _summaries = summaries ??
                incidents
                    .Select((incident, index) => BuildSummary(incident.IncidentId, index))
                    .ToArray();
        }

        public int? LastLimit { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SaveAsync(
            FraudIncident incident,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<FraudIncident?> GetByIdAsync(
            Guid incidentId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _incidents.TryGetValue(incidentId, out var incident);
            return Task.FromResult(incident);
        }

        public Task<IReadOnlyList<IncidentSummary>> ListRecentAsync(
            int limit = 100,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastLimit = limit;

            IReadOnlyList<IncidentSummary> result = _summaries
                .Take(limit)
                .ToArray();
            return Task.FromResult(result);
        }
    }
}
