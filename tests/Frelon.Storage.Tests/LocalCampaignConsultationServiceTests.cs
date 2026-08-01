using Frelon.Core;

namespace Frelon.Storage.Tests;

/// <summary>
/// Vérifie la vue de lecture qui réunit corrélation éphémère et revues append-only.
/// </summary>
public sealed class LocalCampaignConsultationServiceTests
{
    [Fact]
    public void Constructeur_SansServiceCorrelation_LeveArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => new LocalCampaignConsultationService(
                null!,
                new MemoryCampaignReviewStore()));

        Assert.Equal("correlationService", exception.ParamName);
    }

    [Fact]
    public void Constructeur_SansStoreRevue_LeveArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => new LocalCampaignConsultationService(
                new StubCampaignCorrelationService([]),
                null!));

        Assert.Equal("reviewStore", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    public async Task ListCurrentAsync_LimiteInvalide_RefuseSansInterrogerLesSources(
        int limit)
    {
        var correlation = new StubCampaignCorrelationService([]);
        var reviews = new MemoryCampaignReviewStore();
        var service = new LocalCampaignConsultationService(correlation, reviews);

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.ListCurrentAsync(limit, CancellationToken.None));

        Assert.Equal("incidentLimit", exception.ParamName);
        Assert.Equal(0, correlation.CallCount);
        Assert.Equal(0, reviews.ReadCount);
    }

    [Fact]
    public async Task ListCurrentAsync_OrdonneLesCampagnesEtJointLaDerniereRevue()
    {
        var older = BuildCandidate(1, lastObservedMinute: 10);
        var recent = BuildCandidate(2, lastObservedMinute: 20);
        var review = BuildReview(
            older,
            1,
            CampaignReviewVerdict.Confirmed,
            decidedMinute: 30);
        var correlation = new StubCampaignCorrelationService([older, recent]);
        var reviews = new MemoryCampaignReviewStore(
            new Dictionary<string, IReadOnlyList<CampaignReviewDecision>>
            {
                [older.Fingerprint] = [review],
            });
        var service = new LocalCampaignConsultationService(correlation, reviews);

        var result = await service.ListCurrentAsync(
            37,
            TestContext.Current.CancellationToken);

        Assert.Collection(
            result,
            first =>
            {
                Assert.Same(recent, first.Candidate);
                Assert.False(first.IsReviewed);
                Assert.Null(first.LatestReview);
            },
            second =>
            {
                Assert.Same(older, second.Candidate);
                Assert.True(second.IsReviewed);
                Assert.Same(review, second.LatestReview);
            });
        Assert.Equal(37, correlation.LastLimit);
        Assert.Equal(
            [recent.Fingerprint, older.Fingerprint],
            reviews.LatestRequests);
    }

    [Fact]
    public async Task ListCurrentAsync_CampagnesDupliquees_SignaleUneIncoherence()
    {
        var candidate = BuildCandidate(1, lastObservedMinute: 10);
        var correlation = new StubCampaignCorrelationService([candidate, candidate]);
        var reviews = new MemoryCampaignReviewStore();
        var service = new LocalCampaignConsultationService(correlation, reviews);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => service.ListCurrentAsync(
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(0, reviews.ReadCount);
    }

    [Fact]
    public async Task ListCurrentAsync_DerniereRevueEtrangere_SignaleUneIncoherence()
    {
        var candidate = BuildCandidate(1, lastObservedMinute: 10);
        var otherCandidate = BuildCandidate(2, lastObservedMinute: 20);
        var reviews = new MemoryCampaignReviewStore
        {
            LatestOverride = BuildReview(
                otherCandidate,
                1,
                CampaignReviewVerdict.Confirmed,
                decidedMinute: 30),
        };
        var service = new LocalCampaignConsultationService(
            new StubCampaignCorrelationService([candidate]),
            reviews);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => service.ListCurrentAsync(
                cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetDetailsAsync_CampagneCourante_RetourneHistoriqueDansUnOrdreStable()
    {
        var candidate = BuildCandidate(1, lastObservedMinute: 10);
        var firstReview = BuildReview(
            candidate,
            1,
            CampaignReviewVerdict.Inconclusive,
            decidedMinute: 20);
        var latestReview = BuildReview(
            candidate,
            2,
            CampaignReviewVerdict.Confirmed,
            decidedMinute: 30);
        var reviews = new MemoryCampaignReviewStore(
            new Dictionary<string, IReadOnlyList<CampaignReviewDecision>>
            {
                [candidate.Fingerprint] = [firstReview, latestReview],
            });
        var correlation = new StubCampaignCorrelationService([candidate]);
        var service = new LocalCampaignConsultationService(correlation, reviews);

        var details = await service.GetDetailsAsync(
            $"  {candidate.Fingerprint.ToUpperInvariant()}  ",
            incidentLimit: 42,
            reviewLimit: 17,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(details);
        Assert.True(details.IsCurrent);
        Assert.Same(candidate, details.CurrentCandidate);
        Assert.Same(candidate, details.CandidateSnapshot);
        Assert.Same(latestReview, details.LatestReview);
        Assert.Equal([latestReview, firstReview], details.ReviewHistory);
        Assert.Equal(42, correlation.LastLimit);
        Assert.Equal((candidate.Fingerprint, 17), reviews.HistoryRequests.Single());
    }

    [Fact]
    public async Task GetDetailsAsync_CampagneHistorique_ResteConsultableDepuisSonSnapshot()
    {
        var historicalCandidate = BuildCandidate(1, lastObservedMinute: 10);
        var review = BuildReview(
            historicalCandidate,
            1,
            CampaignReviewVerdict.Rejected,
            decidedMinute: 30);
        var reviews = new MemoryCampaignReviewStore(
            new Dictionary<string, IReadOnlyList<CampaignReviewDecision>>
            {
                [historicalCandidate.Fingerprint] = [review],
            });
        var service = new LocalCampaignConsultationService(
            new StubCampaignCorrelationService([]),
            reviews);

        var details = await service.GetDetailsAsync(
            historicalCandidate.Fingerprint,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(details);
        Assert.False(details.IsCurrent);
        Assert.Null(details.CurrentCandidate);
        Assert.Same(historicalCandidate, details.CandidateSnapshot);
        Assert.Same(review, details.LatestReview);
    }

    [Fact]
    public async Task GetDetailsAsync_EmpreinteInconnue_RetourneNull()
    {
        var candidate = BuildCandidate(1, lastObservedMinute: 10);
        var service = new LocalCampaignConsultationService(
            new StubCampaignCorrelationService([]),
            new MemoryCampaignReviewStore());

        var details = await service.GetDetailsAsync(
            candidate.Fingerprint,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(details);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public async Task GetDetailsAsync_EmpreinteInvalide_RefuseSansInterrogerLesSources(
        string? fingerprint)
    {
        var correlation = new StubCampaignCorrelationService([]);
        var reviews = new MemoryCampaignReviewStore();
        var service = new LocalCampaignConsultationService(correlation, reviews);

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => service.GetDetailsAsync(
                fingerprint!,
                cancellationToken: CancellationToken.None));

        Assert.Equal(0, correlation.CallCount);
        Assert.Equal(0, reviews.ReadCount);
    }

    [Theory]
    [InlineData(0, 100, "incidentLimit")]
    [InlineData(501, 100, "incidentLimit")]
    [InlineData(100, 0, "reviewLimit")]
    [InlineData(100, 501, "reviewLimit")]
    public async Task GetDetailsAsync_LimiteInvalide_RefuseSansInterrogerLesSources(
        int incidentLimit,
        int reviewLimit,
        string parameterName)
    {
        var candidate = BuildCandidate(1, lastObservedMinute: 10);
        var correlation = new StubCampaignCorrelationService([]);
        var reviews = new MemoryCampaignReviewStore();
        var service = new LocalCampaignConsultationService(correlation, reviews);

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.GetDetailsAsync(
                candidate.Fingerprint,
                incidentLimit,
                reviewLimit,
                CancellationToken.None));

        Assert.Equal(parameterName, exception.ParamName);
        Assert.Equal(0, correlation.CallCount);
        Assert.Equal(0, reviews.ReadCount);
    }

    [Fact]
    public async Task GetDetailsAsync_HistoriqueEtranger_SignaleUneIncoherence()
    {
        var requested = BuildCandidate(1, lastObservedMinute: 10);
        var other = BuildCandidate(2, lastObservedMinute: 20);
        var reviews = new MemoryCampaignReviewStore(
            new Dictionary<string, IReadOnlyList<CampaignReviewDecision>>
            {
                [requested.Fingerprint] =
                [
                    BuildReview(
                        other,
                        1,
                        CampaignReviewVerdict.Confirmed,
                        decidedMinute: 30),
                ],
            });
        var service = new LocalCampaignConsultationService(
            new StubCampaignCorrelationService([]),
            reviews);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => service.GetDetailsAsync(
                requested.Fingerprint,
                cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Consultation_AvecTokenDejaAnnule_NInterrogeAucuneSource()
    {
        var candidate = BuildCandidate(1, lastObservedMinute: 10);
        var correlation = new StubCampaignCorrelationService([candidate]);
        var reviews = new MemoryCampaignReviewStore();
        var service = new LocalCampaignConsultationService(correlation, reviews);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.ListCurrentAsync(cancellationToken: cancellation.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.GetDetailsAsync(
                candidate.Fingerprint,
                cancellationToken: cancellation.Token));

        Assert.Equal(0, correlation.CallCount);
        Assert.Equal(0, reviews.ReadCount);
    }

    [Fact]
    public void Details_SansCampagneNiHistorique_RefuseUnEtatVide()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new CampaignConsultationDetails(null, []));

        Assert.Equal("reviewHistory", exception.ParamName);
    }

    [Fact]
    public void Summary_RevueEtrangere_RefuseLaComposition()
    {
        var candidate = BuildCandidate(1, lastObservedMinute: 10);
        var other = BuildCandidate(2, lastObservedMinute: 20);
        var review = BuildReview(
            other,
            1,
            CampaignReviewVerdict.Confirmed,
            decidedMinute: 30);

        var exception = Assert.Throws<ArgumentException>(
            () => new CampaignConsultationSummary(candidate, review));

        Assert.Equal("latestReview", exception.ParamName);
    }

    private static CampaignCandidate BuildCandidate(
        int seed,
        int lastObservedMinute)
    {
        var firstIncidentId = Guid.Parse(
            $"00000000-0000-0000-{seed:D4}-000000000001");
        var secondIncidentId = Guid.Parse(
            $"00000000-0000-0000-{seed:D4}-000000000002");
        var firstObservedAt = new DateTimeOffset(
            2026,
            7,
            23,
            9,
            seed,
            0,
            TimeSpan.Zero);
        var lastObservedAt = new DateTimeOffset(
            2026,
            7,
            23,
            9,
            lastObservedMinute,
            0,
            TimeSpan.Zero);
        var link = new IncidentCorrelationLink(
            firstIncidentId,
            secondIncidentId,
            [
                new SharedIocMatch(
                    IocType.Url,
                    $"https://fraud-{seed}.example/login",
                    BasicIncidentCorrelator.UrlWeight),
            ]);

        return new CampaignCandidate(
            [firstIncidentId, secondIncidentId],
            firstObservedAt,
            lastObservedAt,
            [link]);
    }

    private static CampaignReviewDecision BuildReview(
        CampaignCandidate candidate,
        int reviewNumber,
        CampaignReviewVerdict verdict,
        int decidedMinute)
        => new(
            Guid.Parse($"dddddddd-dddd-dddd-dddd-{reviewNumber:D12}"),
            candidate,
            verdict,
            new DateTimeOffset(
                2026,
                7,
                23,
                10,
                decidedMinute,
                0,
                TimeSpan.Zero));

    private sealed class StubCampaignCorrelationService(
        IReadOnlyList<CampaignCandidate> candidates)
        : ICampaignCorrelationService
    {
        public int CallCount { get; private set; }

        public int? LastLimit { get; private set; }

        public Task<IReadOnlyList<CampaignCandidate>> FindRecentCandidatesAsync(
            int limit = 100,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastLimit = limit;
            return Task.FromResult(candidates);
        }
    }

    private sealed class MemoryCampaignReviewStore : ICampaignReviewStore
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyList<CampaignReviewDecision>>
            _reviews;

        public MemoryCampaignReviewStore(
            IReadOnlyDictionary<string, IReadOnlyList<CampaignReviewDecision>>? reviews = null)
        {
            _reviews = reviews ??
                new Dictionary<string, IReadOnlyList<CampaignReviewDecision>>();
        }

        public CampaignReviewDecision? LatestOverride { get; init; }

        public List<string> LatestRequests { get; } = [];

        public List<(string Fingerprint, int Limit)> HistoryRequests { get; } = [];

        public int ReadCount => LatestRequests.Count + HistoryRequests.Count;

        public Task InitializeAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SaveCampaignReviewAsync(
            CampaignReviewDecision decision,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<CampaignReviewDecision?> GetLatestCampaignReviewAsync(
            string candidateFingerprint,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LatestRequests.Add(candidateFingerprint);

            if (LatestOverride is not null)
            {
                return Task.FromResult<CampaignReviewDecision?>(LatestOverride);
            }

            var latest = _reviews.TryGetValue(candidateFingerprint, out var reviews)
                ? reviews
                    .OrderByDescending(review => review.DecidedAt)
                    .ThenBy(review => review.ReviewId)
                    .FirstOrDefault()
                : null;
            return Task.FromResult(latest);
        }

        public Task<IReadOnlyList<CampaignReviewDecision>> ListCampaignReviewsAsync(
            string candidateFingerprint,
            int limit = 100,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            HistoryRequests.Add((candidateFingerprint, limit));

            IReadOnlyList<CampaignReviewDecision> result =
                _reviews.TryGetValue(candidateFingerprint, out var reviews)
                    ? reviews.Take(limit).ToArray()
                    : [];
            return Task.FromResult(result);
        }
    }
}
