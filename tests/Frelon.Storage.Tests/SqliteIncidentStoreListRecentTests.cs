using Frelon.Core;
using Microsoft.Data.Sqlite;

namespace Frelon.Storage.Tests;

public partial class SqliteIncidentStoreTests
{
    [Fact]
    public async Task ListRecentAsync_TableVide_RetourneListeVide()
    {
        using var database = TemporarySqliteDatabase.Create();
        var store = new SqliteIncidentStore(database.ConnectionString);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        var summaries = await store.ListRecentAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(summaries);
    }

    [Fact]
    public async Task ListRecentAsync_UnIncident_RetourneResumeExact()
    {
        using var database = TemporarySqliteDatabase.Create();
        var store = new SqliteIncidentStore(database.ConnectionString);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.SaveAsync(BuildRichIncident(), TestContext.Current.CancellationToken);

        var summary = Assert.Single(
            await store.ListRecentAsync(cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(FixedIncidentGuid, summary.IncidentId);
        Assert.Equal(CreatedAt, summary.CreatedAt);
        Assert.Equal(ImportedAt, summary.ImportedAt);
        Assert.Equal(DefaultSourceFileName, summary.SourceFileName);
        Assert.Equal(RiskValue, summary.RiskValue);
        Assert.Equal(RiskLevel.Critical, summary.RiskLevel);
        Assert.Equal(FraudClassification.Phishing, summary.Classification);
        Assert.Null(summary.LatestReviewVerdict);
        Assert.Null(summary.LatestReviewClassification);
        Assert.Null(summary.LatestReviewAt);
    }

    [Fact]
    public async Task ListRecentAsync_AvecRevues_ExposeSeulementLaDerniereDecision()
    {
        using var database = TemporarySqliteDatabase.Create();
        var store = new SqliteIncidentStore(database.ConnectionString);
        var incident = BuildRichIncident();
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.SaveAsync(incident, TestContext.Current.CancellationToken);
        await store.SaveReviewAsync(
            new IncidentReviewDecision(
                Guid.NewGuid(),
                incident.IncidentId,
                ReviewVerdict.Inconclusive,
                null,
                CreatedAt.AddMinutes(1),
                "Première lecture"),
            TestContext.Current.CancellationToken);
        var latestReview = new IncidentReviewDecision(
            Guid.NewGuid(),
            incident.IncidentId,
            ReviewVerdict.ConfirmedFraud,
            FraudClassification.CredentialTheft,
            CreatedAt.AddMinutes(2),
            "Validation finale");
        await store.SaveReviewAsync(latestReview, TestContext.Current.CancellationToken);

        var summary = Assert.Single(
            await store.ListRecentAsync(cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(latestReview.Verdict, summary.LatestReviewVerdict);
        Assert.Equal(latestReview.Classification, summary.LatestReviewClassification);
        Assert.Equal(latestReview.DecidedAt, summary.LatestReviewAt);
    }

    [Fact]
    public async Task ListRecentAsync_PlusieursIncidents_OrdonneParDateDecroissante()
    {
        using var database = TemporarySqliteDatabase.Create();
        var store = new SqliteIncidentStore(database.ConnectionString);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        var older = BuildRichIncident("11111111-1111-1111-1111-111111111111") with
        {
            CreatedAt = CreatedAt.AddMinutes(-1)
        };
        var newer = BuildRichIncident("22222222-2222-2222-2222-222222222222") with
        {
            CreatedAt = CreatedAt.AddMinutes(1)
        };
        await store.SaveAsync(older, TestContext.Current.CancellationToken);
        await store.SaveAsync(newer, TestContext.Current.CancellationToken);

        var summaries = await store.ListRecentAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal([newer.IncidentId, older.IncidentId], summaries.Select(summary => summary.IncidentId));
    }

    [Fact]
    public async Task ListRecentAsync_DatesEgales_OrdonneParIdentifiantCroissant()
    {
        using var database = TemporarySqliteDatabase.Create();
        var store = new SqliteIncidentStore(database.ConnectionString);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        var second = BuildRichIncident("22222222-2222-2222-2222-222222222222");
        var first = BuildRichIncident("11111111-1111-1111-1111-111111111111");
        await store.SaveAsync(second, TestContext.Current.CancellationToken);
        await store.SaveAsync(first, TestContext.Current.CancellationToken);

        var summaries = await store.ListRecentAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal([first.IncidentId, second.IncidentId], summaries.Select(summary => summary.IncidentId));
    }

    [Fact]
    public async Task ListRecentAsync_Limit_BorneLeResultat()
    {
        using var database = TemporarySqliteDatabase.Create();
        var store = new SqliteIncidentStore(database.ConnectionString);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        for (var index = 1; index <= 3; index++)
        {
            var incident = BuildRichIncident($"00000000-0000-0000-0000-{index:D12}") with
            {
                CreatedAt = CreatedAt.AddMinutes(index)
            };
            await store.SaveAsync(incident, TestContext.Current.CancellationToken);
        }

        var summaries = await store.ListRecentAsync(2, TestContext.Current.CancellationToken);

        Assert.Equal(2, summaries.Count);
        Assert.Equal(
            [Guid.Parse("00000000-0000-0000-0000-000000000003"), Guid.Parse("00000000-0000-0000-0000-000000000002")],
            summaries.Select(summary => summary.IncidentId));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(500)]
    public async Task ListRecentAsync_LimitesValides_SontAcceptees(int limit)
    {
        using var database = TemporarySqliteDatabase.Create();
        var store = new SqliteIncidentStore(database.ConnectionString);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        var summaries = await store.ListRecentAsync(limit, TestContext.Current.CancellationToken);

        Assert.Empty(summaries);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    public async Task ListRecentAsync_LimiteInvalide_EchoueAvantOuvertureDeConnexion(int limit)
    {
        var store = new SqliteIncidentStore("connexion volontairement invalide");

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => store.ListRecentAsync(limit, TestContext.Current.CancellationToken));

        Assert.Equal("limit", exception.ParamName);
    }

    [Fact]
    public async Task ListRecentAsync_TokenAnnule_EchoueAvantOuvertureDeConnexion()
    {
        var store = new SqliteIncidentStore("connexion volontairement invalide");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => store.ListRecentAsync(cancellationToken: cts.Token));
    }

    [Fact]
    public async Task ListRecentAsync_SansInitializeAsync_NeCreePasLeSchemaImplicitement()
    {
        using var database = TemporarySqliteDatabase.Create();
        var store = new SqliteIncidentStore(database.ConnectionString);

        await Assert.ThrowsAsync<SqliteException>(
            () => store.ListRecentAsync(cancellationToken: TestContext.Current.CancellationToken));

        Assert.False(await TableExistsAsync(database.ConnectionString));
    }

    [Fact]
    public async Task ListRecentAsync_PayloadJsonInvalide_RetourneLeResumeSansLireLeSnapshot()
    {
        using var database = TemporarySqliteDatabase.Create();
        var store = new SqliteIncidentStore(database.ConnectionString);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.SaveAsync(BuildRichIncident(), TestContext.Current.CancellationToken);
        await UpdateColumnAsync(database.ConnectionString, "payload_json", "{json invalide");

        var summary = Assert.Single(
            await store.ListRecentAsync(cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(FixedIncidentGuid, summary.IncidentId);
    }

    [Theory]
    [InlineData("incident_id", "pas-un-guid")]
    [InlineData("incident_id", "11111111222233334444555555555555")]
    [InlineData("created_at", "pas-une-date")]
    [InlineData("imported_at", "pas-une-date")]
    [InlineData("risk_level", "Inconnu")]
    [InlineData("risk_level", "critical")]
    [InlineData("risk_level", "3")]
    [InlineData("classification", "Inconnue")]
    [InlineData("classification", "phishing")]
    [InlineData("classification", "1")]
    public async Task ListRecentAsync_MetadonneeInvalide_LeveInvalidDataExceptionAvecColonne(
        string columnName,
        string invalidValue)
    {
        using var database = TemporarySqliteDatabase.Create();
        var store = new SqliteIncidentStore(database.ConnectionString);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.SaveAsync(BuildRichIncident(), TestContext.Current.CancellationToken);
        await UpdateColumnAsync(database.ConnectionString, columnName, invalidValue);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => store.ListRecentAsync(cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains(columnName, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListRecentAsync_NomDeFichierHostile_ResteUneValeurEtPreserveLaTable()
    {
        using var database = TemporarySqliteDatabase.Create();
        var store = new SqliteIncidentStore(database.ConnectionString);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.SaveAsync(
            BuildRichIncident(sourceFileName: HostileSourceFileName),
            TestContext.Current.CancellationToken);

        var summary = Assert.Single(
            await store.ListRecentAsync(cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(HostileSourceFileName, summary.SourceFileName);
        Assert.True(await TableExistsAsync(database.ConnectionString));
    }

    private static async Task UpdateColumnAsync(string connectionString, string columnName, string value)
    {
        var allowedColumns = new HashSet<string>(StringComparer.Ordinal)
        {
            "incident_id",
            "created_at",
            "imported_at",
            "risk_level",
            "classification",
            "payload_json"
        };
        if (!allowedColumns.Contains(columnName))
        {
            throw new ArgumentOutOfRangeException(nameof(columnName));
        }

        using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = $"UPDATE incidents SET {columnName} = $value;";
        command.Parameters.AddWithValue("$value", value);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
