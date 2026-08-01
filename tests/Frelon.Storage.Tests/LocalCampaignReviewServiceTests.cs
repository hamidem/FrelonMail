using Frelon.Core;

namespace Frelon.Storage.Tests;

/// <summary>
/// Vérifie que seule la campagne effectivement examinée peut recevoir une décision.
/// </summary>
public sealed class LocalCampaignReviewServiceTests
{
    private static readonly DateTimeOffset FirstObservedAt =
        new(2026, 7, 23, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LastObservedAt =
        new(2026, 7, 23, 9, 5, 0, TimeSpan.Zero);

    [Fact]
    public void Constructeur_SansCorrelation_LeveArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => new LocalCampaignReviewService(
                null!,
                new RecordingCampaignReviewStore()));

        Assert.Equal("correlationService", exception.ParamName);
    }

    [Fact]
    public void Constructeur_SansStore_LeveArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => new LocalCampaignReviewService(
                new StubCampaignCorrelationService([]),
                null!));

        Assert.Equal("reviewStore", exception.ParamName);
    }

    [Fact]
    public async Task RecordCurrentAsync_SansDecision_LeveArgumentNullException()
    {
        var service = new LocalCampaignReviewService(
            new StubCampaignCorrelationService([]),
            new RecordingCampaignReviewStore());

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.RecordCurrentAsync(
                null!,
                cancellationToken: CancellationToken.None));

        Assert.Equal("decision", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    public async Task RecordCurrentAsync_LimiteInvalide_RefuseSansEffet(
        int incidentLimit)
    {
        var candidate = BuildCandidate(1, "https://fraud.example/login");
        var correlation = new StubCampaignCorrelationService([candidate]);
        var store = new RecordingCampaignReviewStore();
        var service = new LocalCampaignReviewService(correlation, store);

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.RecordCurrentAsync(
                BuildDecision(candidate),
                incidentLimit,
                CancellationToken.None));

        Assert.Equal("incidentLimit", exception.ParamName);
        Assert.Equal(0, correlation.CallCount);
        Assert.Empty(store.SavedDecisions);
    }

    [Fact]
    public async Task RecordCurrentAsync_SnapshotEquivalent_ConserveExactementLaDecisionExaminee()
    {
        var currentCandidate = BuildCandidate(
            1,
            "https://fraud.example/login");
        var reviewedCandidate = BuildCandidate(
            1,
            "https://fraud.example/login",
            reverseLink: true);
        var decision = BuildDecision(reviewedCandidate);
        var correlation = new StubCampaignCorrelationService([currentCandidate]);
        var store = new RecordingCampaignReviewStore();
        var service = new LocalCampaignReviewService(correlation, store);

        var saved = await service.RecordCurrentAsync(
            decision,
            incidentLimit: 37,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Same(decision, saved);
        Assert.Same(decision, Assert.Single(store.SavedDecisions));
        Assert.Same(reviewedCandidate, saved.CandidateSnapshot);
        Assert.Equal(37, correlation.LastLimit);
    }

    [Fact]
    public async Task RecordCurrentAsync_CampagneDisparue_RefuseLaDecision()
    {
        var candidate = BuildCandidate(1, "https://fraud.example/login");
        var store = new RecordingCampaignReviewStore();
        var service = new LocalCampaignReviewService(
            new StubCampaignCorrelationService([]),
            store);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RecordCurrentAsync(
                BuildDecision(candidate),
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("n'est plus présente", exception.Message, StringComparison.Ordinal);
        Assert.Empty(store.SavedDecisions);
    }

    [Fact]
    public async Task RecordCurrentAsync_MemeCompositionMaisLiensModifies_RefuseLaDecision()
    {
        var reviewedCandidate = BuildCandidate(
            1,
            "https://fraud.example/ancienne");
        var currentCandidate = BuildCandidate(
            1,
            "https://fraud.example/nouvelle");
        var store = new RecordingCampaignReviewStore();
        var service = new LocalCampaignReviewService(
            new StubCampaignCorrelationService([currentCandidate]),
            store);

        Assert.Equal(reviewedCandidate.Fingerprint, currentCandidate.Fingerprint);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RecordCurrentAsync(
                BuildDecision(reviewedCandidate),
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("a changé", exception.Message, StringComparison.Ordinal);
        Assert.Empty(store.SavedDecisions);
    }

    [Fact]
    public async Task RecordCurrentAsync_CorrelationDupliquee_SignaleUneIncoherence()
    {
        var candidate = BuildCandidate(1, "https://fraud.example/login");
        var store = new RecordingCampaignReviewStore();
        var service = new LocalCampaignReviewService(
            new StubCampaignCorrelationService([candidate, candidate]),
            store);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => service.RecordCurrentAsync(
                BuildDecision(candidate),
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Empty(store.SavedDecisions);
    }

    [Fact]
    public async Task RecordCurrentAsync_TokenDejaAnnule_NInterrogeAucuneDependance()
    {
        var candidate = BuildCandidate(1, "https://fraud.example/login");
        var correlation = new StubCampaignCorrelationService([candidate]);
        var store = new RecordingCampaignReviewStore();
        var service = new LocalCampaignReviewService(correlation, store);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.RecordCurrentAsync(
                BuildDecision(candidate),
                cancellationToken: cancellation.Token));

        Assert.Equal(0, correlation.CallCount);
        Assert.Empty(store.SavedDecisions);
    }

    [Fact]
    public async Task RecordCurrentAsync_AnnulationApresCorrelation_NEcritPasLaDecision()
    {
        var candidate = BuildCandidate(1, "https://fraud.example/login");
        using var cancellation = new CancellationTokenSource();
        var correlation = new StubCampaignCorrelationService(
            [candidate],
            () => cancellation.Cancel());
        var store = new RecordingCampaignReviewStore();
        var service = new LocalCampaignReviewService(correlation, store);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.RecordCurrentAsync(
                BuildDecision(candidate),
                cancellationToken: cancellation.Token));

        Assert.Equal(1, correlation.CallCount);
        Assert.Empty(store.SavedDecisions);
    }

    [Fact]
    public async Task RecordCurrentAsync_EchecDuStore_EstPropage()
    {
        var candidate = BuildCandidate(1, "https://fraud.example/login");
        var expected = new IOException("Stockage indisponible.");
        var store = new RecordingCampaignReviewStore
        {
            SaveException = expected,
        };
        var service = new LocalCampaignReviewService(
            new StubCampaignCorrelationService([candidate]),
            store);

        var exception = await Assert.ThrowsAsync<IOException>(
            () => service.RecordCurrentAsync(
                BuildDecision(candidate),
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Same(expected, exception);
    }

    private static CampaignCandidate BuildCandidate(
        int seed,
        string sharedUrl,
        bool reverseLink = false)
    {
        var firstIncidentId = Guid.Parse(
            $"00000000-0000-0000-{seed:D4}-000000000001");
        var secondIncidentId = Guid.Parse(
            $"00000000-0000-0000-{seed:D4}-000000000002");
        var link = reverseLink
            ? new IncidentCorrelationLink(
                secondIncidentId,
                firstIncidentId,
                [
                    new SharedIocMatch(
                        IocType.Url,
                        sharedUrl,
                        BasicIncidentCorrelator.UrlWeight),
                ])
            : new IncidentCorrelationLink(
                firstIncidentId,
                secondIncidentId,
                [
                    new SharedIocMatch(
                        IocType.Url,
                        sharedUrl,
                        BasicIncidentCorrelator.UrlWeight),
                ]);

        return new CampaignCandidate(
            [firstIncidentId, secondIncidentId],
            FirstObservedAt,
            LastObservedAt,
            [link]);
    }

    private static CampaignReviewDecision BuildDecision(
        CampaignCandidate candidate)
        => new(
            Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            candidate,
            CampaignReviewVerdict.Confirmed,
            new DateTimeOffset(2026, 7, 23, 11, 0, 0, TimeSpan.Zero),
            "Snapshot vérifié par l'analyste.");

    private sealed class StubCampaignCorrelationService : ICampaignCorrelationService
    {
        private readonly IReadOnlyList<CampaignCandidate> _candidates;
        private readonly Action? _afterRead;

        public StubCampaignCorrelationService(
            IReadOnlyList<CampaignCandidate> candidates,
            Action? afterRead = null)
        {
            _candidates = candidates;
            _afterRead = afterRead;
        }

        public int CallCount { get; private set; }

        public int? LastLimit { get; private set; }

        public Task<IReadOnlyList<CampaignCandidate>> FindRecentCandidatesAsync(
            int limit = 100,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastLimit = limit;
            _afterRead?.Invoke();
            return Task.FromResult(_candidates);
        }
    }

    private sealed class RecordingCampaignReviewStore : ICampaignReviewStore
    {
        public Exception? SaveException { get; init; }

        public List<CampaignReviewDecision> SavedDecisions { get; } = [];

        public Task InitializeAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SaveCampaignReviewAsync(
            CampaignReviewDecision decision,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (SaveException is not null)
            {
                return Task.FromException(SaveException);
            }

            SavedDecisions.Add(decision);
            return Task.CompletedTask;
        }

        public Task<CampaignReviewDecision?> GetLatestCampaignReviewAsync(
            string candidateFingerprint,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<CampaignReviewDecision>> ListCampaignReviewsAsync(
            string candidateFingerprint,
            int limit = 100,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
