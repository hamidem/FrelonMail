using System.Text.Json;
using Frelon.Cli;
using Frelon.Core;
using Frelon.Storage;
using Xunit;

namespace Frelon.Cli.Tests;

/// <summary>Vérifie la consultation locale des incidents depuis la ligne de commande.</summary>
public sealed class CliIncidentConsultationTests
{
    [Fact]
    public async Task List_AfficheLesIncidentsRecentsSansChargerDeFichierEmail()
    {
        using var workspace = new TemporaryWorkspace();
        var databasePath = workspace.PathOf("incidents.db");
        var older = BuildIncident(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            new DateTimeOffset(2026, 7, 15, 10, 0, 0, TimeSpan.Zero),
            "ancien.eml",
            20,
            RiskLevel.Low);
        var recent = BuildIncident(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            new DateTimeOffset(2026, 7, 16, 10, 0, 0, TimeSpan.Zero),
            "récent.eml",
            75.5,
            RiskLevel.High);
        await SaveAsync(databasePath, older, recent);

        var result = await RunAsync("incidents", "list", "--database", databasePath, "--limit", "1");

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.StandardError);
        Assert.Contains("IncidentId\tCreatedAt\tSource\tRisk\tLevel\tClassification\tReview", result.StandardOutput);
        Assert.Contains(recent.IncidentId.ToString(), result.StandardOutput);
        Assert.Contains("récent.eml\t75.5\tHigh\tUnknown\tPending", result.StandardOutput);
        Assert.DoesNotContain(older.IncidentId.ToString(), result.StandardOutput);
    }

    [Fact]
    public async Task List_BaseVide_AfficheUnResultatExplicite()
    {
        using var workspace = new TemporaryWorkspace();
        var databasePath = workspace.PathOf("incidents.db");
        var store = SqliteIncidentStore.FromFile(databasePath);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        var result = await RunAsync("incidents", "list", "-d", databasePath);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal($"No incidents found.{Environment.NewLine}", result.StandardOutput);
        Assert.Empty(result.StandardError);
    }

    [Fact]
    public async Task Show_AfficheLeSnapshotJsonComplet()
    {
        using var workspace = new TemporaryWorkspace();
        var databasePath = workspace.PathOf("incidents.db");
        var incident = BuildIncident(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            new DateTimeOffset(2026, 7, 16, 11, 0, 0, TimeSpan.Zero),
            "preuve.eml",
            45,
            RiskLevel.Medium);
        await SaveAsync(databasePath, incident);

        var result = await RunAsync(
            "incidents", "show", incident.IncidentId.ToString(), "--database", databasePath);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.StandardError);
        using var json = JsonDocument.Parse(result.StandardOutput);
        Assert.Equal(incident.IncidentId, json.RootElement.GetProperty("incidentId").GetGuid());
        Assert.Equal("preuve.eml", json.RootElement.GetProperty("evidence").GetProperty("fileName").GetString());
    }

    [Fact]
    public async Task Show_IdentifiantAbsent_RetourneUneErreurClaire()
    {
        using var workspace = new TemporaryWorkspace();
        var databasePath = workspace.PathOf("incidents.db");
        var store = SqliteIncidentStore.FromFile(databasePath);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        var result = await RunAsync(
            "incidents", "show", Guid.NewGuid().ToString(), "--database", databasePath);

        Assert.Equal(1, result.ExitCode);
        Assert.Empty(result.StandardOutput);
        Assert.Contains("Incident not found", result.StandardError, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("incidents", "list", "--database", "db", "--limit", "0")]
    [InlineData("incidents", "list", "--database", "db", "--limit", "501")]
    [InlineData("incidents", "list", "--limit", "10")]
    [InlineData("incidents", "list", "--database", "--limit")]
    [InlineData("incidents", "show", "not-a-guid", "--database", "db")]
    public async Task SyntaxeInvalide_AfficheLeModeDEmploi(params string[] arguments)
    {
        var result = await RunAsync(arguments);

        Assert.Equal(2, result.ExitCode);
        Assert.Empty(result.StandardOutput);
        Assert.Contains("Usage: frelon incidents", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BaseAbsente_NEstPasCreeeParUneConsultation()
    {
        using var workspace = new TemporaryWorkspace();
        var databasePath = workspace.PathOf("absente.db");

        var result = await RunAsync("incidents", "list", "--database", databasePath);

        Assert.Equal(2, result.ExitCode);
        Assert.False(File.Exists(databasePath));
        Assert.Contains("does not exist", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BaseInvalide_NExposePasSonContenuDansLErreur()
    {
        using var workspace = new TemporaryWorkspace();
        var databasePath = workspace.PathOf("invalide.db");
        await File.WriteAllTextAsync(
            databasePath,
            "ULTRA-SECRET-DATABASE-CONTENT",
            TestContext.Current.CancellationToken);

        var result = await RunAsync("incidents", "list", "--database", databasePath);

        Assert.Equal(1, result.ExitCode);
        Assert.Empty(result.StandardOutput);
        Assert.Contains("consultation failed", result.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain("ULTRA-SECRET", result.StandardError, StringComparison.Ordinal);
    }

    private static async Task SaveAsync(string databasePath, params FraudIncident[] incidents)
    {
        var store = SqliteIncidentStore.FromFile(databasePath);
        var cancellationToken = TestContext.Current.CancellationToken;
        await store.InitializeAsync(cancellationToken);
        foreach (var incident in incidents)
        {
            await store.SaveAsync(incident, cancellationToken);
        }
    }

    private static FraudIncident BuildIncident(
        Guid id,
        DateTimeOffset createdAt,
        string sourceFileName,
        double riskValue,
        RiskLevel riskLevel)
        => new()
        {
            IncidentId = id,
            CreatedAt = createdAt,
            Evidence = new EvidenceSource
            {
                FileName = sourceFileName,
                ImportedAt = createdAt
            },
            Identity = new MailIdentity(),
            Authentication = new AuthenticationAssessment(),
            Classification = FraudClassification.Unknown,
            RiskScore = new RiskScore { Value = riskValue, Level = riskLevel }
        };

    private static async Task<CliResult> RunAsync(params string[] arguments)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exitCode = await CliApplication.CreateDefault(stdout, stderr)
            .RunAsync(arguments, TestContext.Current.CancellationToken);
        return new CliResult(exitCode, stdout.ToString(), stderr.ToString());
    }

    private sealed record CliResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed class TemporaryWorkspace : IDisposable
    {
        public TemporaryWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), $"frelon-cli-consultation-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string PathOf(string fileName) => Path.Combine(Root, fileName);

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch
            {
                // Le nettoyage de test ne doit pas masquer le résultat fonctionnel.
            }
        }
    }
}
