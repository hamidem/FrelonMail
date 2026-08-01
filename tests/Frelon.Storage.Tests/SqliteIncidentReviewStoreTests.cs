using Frelon.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Frelon.Storage.Tests;

/// <summary>Vérifie l'historique SQLite append-only des décisions humaines.</summary>
public sealed class SqliteIncidentReviewStoreTests
{
    [Fact]
    public async Task SaveReviewAsync_PuisLecture_ConserveExactementLaDecision()
    {
        await using var database = new TemporaryDatabase();
        var store = database.CreateStore();
        var incident = BuildIncident();
        var decision = BuildDecision(incident.IncidentId, ReviewVerdict.ConfirmedFraud, FraudClassification.Phishing);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.SaveAsync(incident, TestContext.Current.CancellationToken);

        await store.SaveReviewAsync(decision, TestContext.Current.CancellationToken);
        var reloaded = await store.GetLatestReviewAsync(incident.IncidentId, TestContext.Current.CancellationToken);

        Assert.Equal(decision, reloaded);
    }

    [Fact]
    public async Task ListReviewsAsync_ConserveLHistoriqueDuPlusRecentAuPlusAncien()
    {
        await using var database = new TemporaryDatabase();
        var store = database.CreateStore();
        var incident = BuildIncident();
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.SaveAsync(incident, TestContext.Current.CancellationToken);
        var first = BuildDecision(
            incident.IncidentId,
            ReviewVerdict.Inconclusive,
            null,
            new DateTimeOffset(2026, 7, 16, 10, 0, 0, TimeSpan.Zero));
        var second = BuildDecision(
            incident.IncidentId,
            ReviewVerdict.Suspicious,
            FraudClassification.Suspicious,
            new DateTimeOffset(2026, 7, 16, 11, 0, 0, TimeSpan.Zero));
        await store.SaveReviewAsync(first, TestContext.Current.CancellationToken);
        await store.SaveReviewAsync(second, TestContext.Current.CancellationToken);

        var reviews = await store.ListReviewsAsync(
            incident.IncidentId,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal([second, first], reviews);
        Assert.Equal(second, await store.GetLatestReviewAsync(
            incident.IncidentId,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveReviewAsync_IncidentAbsent_RefuseLaDecision()
    {
        await using var database = new TemporaryDatabase();
        var store = database.CreateStore();
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        var decision = BuildDecision(Guid.NewGuid(), ReviewVerdict.Benign, null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.SaveReviewAsync(decision, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveReviewAsync_IdentifiantDuplique_RefuseSansRemplacer()
    {
        await using var database = new TemporaryDatabase();
        var store = database.CreateStore();
        var incident = BuildIncident();
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.SaveAsync(incident, TestContext.Current.CancellationToken);
        var decision = BuildDecision(incident.IncidentId, ReviewVerdict.Benign, null);
        await store.SaveReviewAsync(decision, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.SaveReviewAsync(decision, TestContext.Current.CancellationToken));

        Assert.Single(await store.ListReviewsAsync(
            incident.IncidentId,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InitializeAsync_ApresInitialisationExistante_AjouteLeSchemaDeRevue()
    {
        await using var database = new TemporaryDatabase();
        var store = database.CreateStore();

        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        await using var connection = new SqliteConnection($"Data Source={database.Path}");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'incident_reviews';";
        Assert.Equal(1L, await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    private static IncidentReviewDecision BuildDecision(
        Guid incidentId,
        ReviewVerdict verdict,
        FraudClassification? classification,
        DateTimeOffset? decidedAt = null)
        => new(
            Guid.NewGuid(),
            incidentId,
            verdict,
            classification,
            decidedAt ?? new DateTimeOffset(2026, 7, 16, 12, 0, 0, TimeSpan.Zero),
            "Décision locale");

    private static FraudIncident BuildIncident()
        => new()
        {
            IncidentId = Guid.NewGuid(),
            CreatedAt = new DateTimeOffset(2026, 7, 16, 9, 0, 0, TimeSpan.Zero),
            Evidence = new EvidenceSource
            {
                FileName = "preuve.eml",
                ImportedAt = new DateTimeOffset(2026, 7, 16, 9, 0, 0, TimeSpan.Zero)
            },
            Identity = new MailIdentity(),
            Authentication = new AuthenticationAssessment(),
            Classification = FraudClassification.Unknown,
            RiskScore = new RiskScore { Value = 10, Level = RiskLevel.Low }
        };

    private sealed class TemporaryDatabase : IAsyncDisposable
    {
        public TemporaryDatabase()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"frelon-review-tests-{Guid.NewGuid():N}.db");
        }

        public string Path { get; }

        public SqliteIncidentStore CreateStore() => SqliteIncidentStore.FromFile(Path);

        public ValueTask DisposeAsync()
        {
            SqliteConnection.ClearAllPools();
            try
            {
                File.Delete(Path);
            }
            catch
            {
                // Best effort for temporary test data.
            }

            return ValueTask.CompletedTask;
        }
    }
}
