using Frelon.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Frelon.Storage.Tests;

/// <summary>
/// Vérifie l'historique SQLite append-only des décisions humaines de campagne.
/// </summary>
public sealed class SqliteCampaignReviewStoreTests
{
    private static readonly Guid FirstIncidentId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SecondIncidentId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ReviewId =
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task InitializeAsync_CreeLeSchemaDeRevueDeCampagneDeFaconIdempotente()
    {
        await using var database = new TemporaryDatabase();
        var store = database.CreateStore();

        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        await using var connection = new SqliteConnection($"Data Source={database.Path}");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
SELECT COUNT(*)
FROM sqlite_master
WHERE type = 'table' AND name = 'campaign_reviews';
""";

        Assert.Equal(
            1L,
            await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveCampaignReviewAsync_PuisLecture_ConserveDecisionEtSnapshot()
    {
        await using var database = new TemporaryDatabase();
        var store = await CreateInitializedStoreWithIncidentsAsync(database);
        var candidate = BuildCandidate();
        var decision = BuildDecision(
            candidate,
            CampaignReviewVerdict.Confirmed,
            new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero),
            notes: "Campagne vérifiée.");

        await store.SaveCampaignReviewAsync(
            decision,
            TestContext.Current.CancellationToken);
        var reloaded = await store.GetLatestCampaignReviewAsync(
            candidate.Fingerprint,
            TestContext.Current.CancellationToken);

        Assert.NotNull(reloaded);
        Assert.Equal(decision.ReviewId, reloaded.ReviewId);
        Assert.Equal(decision.Verdict, reloaded.Verdict);
        Assert.Equal(decision.DecidedAt, reloaded.DecidedAt);
        Assert.Equal(decision.Notes, reloaded.Notes);
        Assert.Equal(candidate.Fingerprint, reloaded.CandidateFingerprint);
        Assert.Equal(candidate.IncidentIds, reloaded.CandidateSnapshot.IncidentIds);

        var link = Assert.Single(reloaded.CandidateSnapshot.Links);
        var match = Assert.Single(link.Matches);
        Assert.Equal(BasicIncidentCorrelator.UrlWeight, link.Score);
        Assert.Equal("https://fraud.example/login", match.Value);
    }

    [Fact]
    public async Task ListCampaignReviewsAsync_ConserveLHistoriqueDuPlusRecentAuPlusAncien()
    {
        await using var database = new TemporaryDatabase();
        var store = await CreateInitializedStoreWithIncidentsAsync(database);
        var candidate = BuildCandidate();
        var first = BuildDecision(
            candidate,
            CampaignReviewVerdict.Inconclusive,
            new DateTimeOffset(2026, 7, 23, 10, 0, 0, TimeSpan.Zero));
        var second = BuildDecision(
            candidate,
            CampaignReviewVerdict.Confirmed,
            new DateTimeOffset(2026, 7, 23, 11, 0, 0, TimeSpan.Zero),
            reviewId: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        await store.SaveCampaignReviewAsync(first, TestContext.Current.CancellationToken);
        await store.SaveCampaignReviewAsync(second, TestContext.Current.CancellationToken);

        var reviews = await store.ListCampaignReviewsAsync(
            candidate.Fingerprint,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(
            [second.ReviewId, first.ReviewId],
            reviews.Select(review => review.ReviewId));
        Assert.Equal(
            second.ReviewId,
            (await store.GetLatestCampaignReviewAsync(
                candidate.Fingerprint,
                TestContext.Current.CancellationToken))?.ReviewId);
    }

    [Fact]
    public async Task SaveCampaignReviewAsync_IncidentAbsent_RefuseTouteLaDecision()
    {
        await using var database = new TemporaryDatabase();
        var store = database.CreateStore();
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.SaveAsync(
            BuildIncident(FirstIncidentId, 1),
            TestContext.Current.CancellationToken);
        var candidate = BuildCandidate();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.SaveCampaignReviewAsync(
                BuildDecision(candidate, CampaignReviewVerdict.Confirmed),
                TestContext.Current.CancellationToken));

        Assert.Empty(await store.ListCampaignReviewsAsync(
            candidate.Fingerprint,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveCampaignReviewAsync_IdentifiantDuplique_RefuseSansRemplacer()
    {
        await using var database = new TemporaryDatabase();
        var store = await CreateInitializedStoreWithIncidentsAsync(database);
        var candidate = BuildCandidate();
        var first = BuildDecision(candidate, CampaignReviewVerdict.Inconclusive);
        var duplicate = BuildDecision(
            candidate,
            CampaignReviewVerdict.Rejected,
            reviewId: first.ReviewId);
        await store.SaveCampaignReviewAsync(first, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.SaveCampaignReviewAsync(
                duplicate,
                TestContext.Current.CancellationToken));

        var stored = Assert.Single(await store.ListCampaignReviewsAsync(
            candidate.Fingerprint,
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(CampaignReviewVerdict.Inconclusive, stored.Verdict);
    }

    [Fact]
    public async Task MemeCompositionAvecNouvellesRaisons_ConserveUnHistoriqueCommun()
    {
        await using var database = new TemporaryDatabase();
        var store = await CreateInitializedStoreWithIncidentsAsync(database);
        var original = BuildCandidate("https://fraud.example/first");
        var refreshed = BuildCandidate("https://fraud.example/refreshed");
        var first = BuildDecision(original, CampaignReviewVerdict.Inconclusive);
        var second = BuildDecision(
            refreshed,
            CampaignReviewVerdict.Confirmed,
            new DateTimeOffset(2026, 7, 23, 13, 0, 0, TimeSpan.Zero),
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        await store.SaveCampaignReviewAsync(first, TestContext.Current.CancellationToken);
        await store.SaveCampaignReviewAsync(second, TestContext.Current.CancellationToken);

        var reviews = await store.ListCampaignReviewsAsync(
            original.Fingerprint,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(original.Fingerprint, refreshed.Fingerprint);
        Assert.Equal(2, reviews.Count);
        Assert.Equal(
            "https://fraud.example/refreshed",
            Assert.Single(reviews[0].CandidateSnapshot.Links).Matches[0].Value);
        Assert.Equal(
            "https://fraud.example/first",
            Assert.Single(reviews[1].CandidateSnapshot.Links).Matches[0].Value);
    }

    [Fact]
    public async Task GetLatestCampaignReviewAsync_EmpreinteMajuscule_RetrouveLaDecision()
    {
        await using var database = new TemporaryDatabase();
        var store = await CreateInitializedStoreWithIncidentsAsync(database);
        var candidate = BuildCandidate();
        var decision = BuildDecision(candidate, CampaignReviewVerdict.Confirmed);
        await store.SaveCampaignReviewAsync(decision, TestContext.Current.CancellationToken);

        var reloaded = await store.GetLatestCampaignReviewAsync(
            candidate.Fingerprint.ToUpperInvariant(),
            TestContext.Current.CancellationToken);

        Assert.Equal(decision.ReviewId, reloaded?.ReviewId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    public async Task ListCampaignReviewsAsync_EmpreinteInvalide_RefuseLaLecture(
        string fingerprint)
    {
        await using var database = new TemporaryDatabase();
        var store = database.CreateStore();

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => store.ListCampaignReviewsAsync(
                fingerprint,
                cancellationToken: CancellationToken.None));

        Assert.Equal("candidateFingerprint", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    public async Task ListCampaignReviewsAsync_LimiteInvalide_RefuseLaLecture(int limit)
    {
        await using var database = new TemporaryDatabase();
        var store = database.CreateStore();

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => store.ListCampaignReviewsAsync(
                new string('a', CampaignCandidate.FingerprintLength),
                limit,
                CancellationToken.None));

        Assert.Equal("limit", exception.ParamName);
    }

    [Fact]
    public async Task SaveCampaignReviewAsync_TokenAnnule_NecritRien()
    {
        await using var database = new TemporaryDatabase();
        var store = database.CreateStore();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => store.SaveCampaignReviewAsync(
                BuildDecision(BuildCandidate(), CampaignReviewVerdict.Confirmed),
                cancellation.Token));
    }

    [Fact]
    public async Task ListCampaignReviewsAsync_SnapshotCorrompu_SignaleLesDonneesInvalides()
    {
        await using var database = new TemporaryDatabase();
        var store = await CreateInitializedStoreWithIncidentsAsync(database);
        var candidate = BuildCandidate();
        await store.SaveCampaignReviewAsync(
            BuildDecision(candidate, CampaignReviewVerdict.Confirmed),
            TestContext.Current.CancellationToken);

        await using (var connection = new SqliteConnection($"Data Source={database.Path}"))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
UPDATE campaign_reviews
SET candidate_json = '{invalid-json'
WHERE candidate_fingerprint = $candidateFingerprint;
""";
            command.Parameters.AddWithValue(
                "$candidateFingerprint",
                candidate.Fingerprint);
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await Assert.ThrowsAsync<InvalidDataException>(
            () => store.ListCampaignReviewsAsync(
                candidate.Fingerprint,
                cancellationToken: TestContext.Current.CancellationToken));
    }

    private static async Task<SqliteIncidentStore> CreateInitializedStoreWithIncidentsAsync(
        TemporaryDatabase database)
    {
        var store = database.CreateStore();
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.SaveAsync(
            BuildIncident(FirstIncidentId, 1),
            TestContext.Current.CancellationToken);
        await store.SaveAsync(
            BuildIncident(SecondIncidentId, 2),
            TestContext.Current.CancellationToken);
        return store;
    }

    private static CampaignReviewDecision BuildDecision(
        CampaignCandidate candidate,
        CampaignReviewVerdict verdict,
        DateTimeOffset? decidedAt = null,
        Guid? reviewId = null,
        string notes = "Décision locale")
        => new(
            reviewId ?? ReviewId,
            candidate,
            verdict,
            decidedAt ?? new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero),
            notes);

    private static CampaignCandidate BuildCandidate(
        string url = "https://fraud.example/login")
        => new(
            [SecondIncidentId, FirstIncidentId],
            new DateTimeOffset(2026, 7, 23, 9, 1, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 23, 9, 2, 0, TimeSpan.Zero),
            [
                new IncidentCorrelationLink(
                    FirstIncidentId,
                    SecondIncidentId,
                    [
                        new SharedIocMatch(
                            IocType.Url,
                            url,
                            BasicIncidentCorrelator.UrlWeight),
                    ]),
            ]);

    private static FraudIncident BuildIncident(Guid incidentId, int minute)
        => new()
        {
            IncidentId = incidentId,
            CreatedAt = new DateTimeOffset(2026, 7, 23, 9, minute, 0, TimeSpan.Zero),
            Evidence = new EvidenceSource
            {
                FileName = $"incident-{minute}.eml",
                ImportedAt = new DateTimeOffset(2026, 7, 23, 9, minute, 0, TimeSpan.Zero),
                Sha256 = new string((char)('a' + minute), 64),
            },
            Identity = new MailIdentity(),
            Authentication = new AuthenticationAssessment(),
            Classification = FraudClassification.Unknown,
            RiskScore = new RiskScore
            {
                Value = 0,
                Level = RiskLevel.Unknown,
            },
        };

    private sealed class TemporaryDatabase : IAsyncDisposable
    {
        public TemporaryDatabase()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"frelon-campaign-review-tests-{Guid.NewGuid():N}.db");
        }

        public string Path { get; }

        public SqliteIncidentStore CreateStore()
            => SqliteIncidentStore.FromFile(Path);

        public ValueTask DisposeAsync()
        {
            SqliteConnection.ClearAllPools();

            try
            {
                File.Delete(Path);
            }
            catch
            {
                // Nettoyage best effort des données temporaires.
            }

            return ValueTask.CompletedTask;
        }
    }
}
