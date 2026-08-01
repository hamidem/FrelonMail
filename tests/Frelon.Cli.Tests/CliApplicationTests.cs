using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Frelon.Cli;
using Frelon.Core;
using Frelon.Exporters;
using Frelon.Mail;
using Frelon.Reports;
using Frelon.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Frelon.Cli.Tests;

/// <summary>
/// Vérifie le contrat utilisateur et la sécurité des écritures de la commande analyze.
/// </summary>
public sealed class CliApplicationTests
{
    private const string SensitiveValue = "ULTRA-SECRET-MAIL-CONTENT";

    [Theory]
    [InlineData()]
    [InlineData("analyze")]
    [InlineData("analyze", "message.eml", "--output")]
    [InlineData("analyze", "message.eml", "--output", "out", "extra")]
    [InlineData("analyze", "message.eml", "--output", "out", "--database")]
    [InlineData("analyze", "message.eml", "--output", "out", "--database", "--csv")]
    [InlineData("analyze", "message.eml", "--output", "out", "--csv", "--csv")]
    [InlineData("analyze", "message.eml", "--output", "out", "--csv", "extra")]
    [InlineData("analyze", "message.eml", "--database", "incidents.db", "--output", "out")]
    [InlineData("inspect", "message.eml", "--output", "out")]
    [InlineData("analyze", "message.eml", "out", "--output")]
    public async Task InvalidSyntaxReturnsUsageError(params string[] arguments)
    {
        using var workspace = new TemporaryWorkspace();
        var result = await RunAsync(arguments);

        Assert.Equal(2, result.ExitCode);
        Assert.Empty(result.StandardOutput);
        Assert.Contains("Usage:", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingSourceReturnsUsageError()
    {
        using var workspace = new TemporaryWorkspace();
        var result = await RunAsync("analyze", workspace.PathOf("missing.eml"), "--output", workspace.PathOf("out"));

        Assert.Equal(2, result.ExitCode);
    }

    [Fact]
    public async Task NonEmlSourceReturnsUsageError()
    {
        using var workspace = new TemporaryWorkspace();
        var source = workspace.WriteFile("message.txt", MinimalEmail());

        var result = await RunAsync("analyze", source, "--output", workspace.PathOf("out"));

        Assert.Equal(2, result.ExitCode);
        Assert.False(Directory.Exists(workspace.PathOf("out")));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(EmailAnalysisLimits.DefaultMaxSourceBytes + 1L)]
    public async Task UnsafeSourceSizeIsRejectedBeforeAnalysis(long sourceSize)
    {
        using var workspace = new TemporaryWorkspace();
        var source = workspace.PathOf("message.eml");
        await using (var file = new FileStream(source, FileMode.CreateNew, FileAccess.Write))
        {
            file.SetLength(sourceSize);
        }

        var analyzer = new CountingAnalyzer();
        var stderr = new StringWriter();
        var application = CreateApplication(new StringWriter(), stderr, analyzer);

        var exitCode = await application.RunAsync(
            ["analyze", source, "--output", workspace.PathOf("out")],
            TestContext.Current.CancellationToken);

        Assert.Equal(2, exitCode);
        Assert.Equal(0, analyzer.CallCount);
        Assert.Contains("25 MB", stderr.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(workspace.PathOf("out")));
    }

    [Fact]
    public async Task MsgSourceIsAcceptedByTheCommandContract()
    {
        using var workspace = new TemporaryWorkspace();
        var source = workspace.WriteFile("message.msg", "synthetic-msg-placeholder");
        var output = workspace.PathOf("out");
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var application = CreateApplication(stdout, stderr, new PassthroughAnalyzer());

        var exitCode = await application.RunAsync(
            ["analyze", source, "--output", output],
            TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(Path.Combine(output, "incident.json")));
        Assert.DoesNotContain(".eml file", stderr.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OutputPathEqualToSourceReturnsUsageError()
    {
        using var workspace = new TemporaryWorkspace();
        var source = workspace.WriteFile("message.eml", MinimalEmail());

        var result = await RunAsync("analyze", source, "--output", source);

        Assert.Equal(2, result.ExitCode);
    }

    [Fact]
    public async Task OutputPathAlreadyOccupiedByFileReturnsUsageErrorWithoutAnalysis()
    {
        using var workspace = new TemporaryWorkspace();
        var source = workspace.WriteFile("message.eml", MinimalEmail());
        var outputFile = workspace.WriteFile("existing-output", "keep-me");
        var analyzer = new CountingAnalyzer();
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var application = CreateApplication(stdout, stderr, analyzer);

        var exitCode = await application.RunAsync(
            ["analyze", source, "--output", outputFile],
            TestContext.Current.CancellationToken);

        Assert.Equal(2, exitCode);
        Assert.Equal(0, analyzer.CallCount);
        Assert.Equal("keep-me", await File.ReadAllTextAsync(outputFile, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("--output")]
    [InlineData("-o")]
    public async Task AnalyzeCreatesThreeValidArtifactsWithRealPipeline(string outputOption)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var workspace = new TemporaryWorkspace();
        var source = workspace.WriteFile("courriel étrange.EML", MinimalEmail());
        var output = workspace.PathOf("résultats avec espaces");

        var result = await RunAsync("analyze", source, outputOption, output);

        Assert.Equal(0, result.ExitCode);
        Assert.True(Directory.Exists(output));
        var incidentPath = Path.Combine(output, "incident.json");
        var reportPath = Path.Combine(output, "report.md");
        var iocsPath = Path.Combine(output, "iocs.json");
        Assert.True(File.Exists(incidentPath));
        Assert.True(File.Exists(reportPath));
        Assert.True(File.Exists(iocsPath));

        using var incident = JsonDocument.Parse(await File.ReadAllTextAsync(incidentPath, cancellationToken));
        Assert.Equal("courriel étrange.EML", incident.RootElement.GetProperty("evidence").GetProperty("fileName").GetString());
        using var iocs = JsonDocument.Parse(await File.ReadAllTextAsync(iocsPath, cancellationToken));
        Assert.Equal(JsonValueKind.Object, iocs.RootElement.ValueKind);
        Assert.Contains("# Rapport d'incident Frelon", await File.ReadAllTextAsync(reportPath, cancellationToken), StringComparison.Ordinal);

        AssertUtf8WithoutBom(incidentPath);
        AssertUtf8WithoutBom(reportPath);
        AssertUtf8WithoutBom(iocsPath);
    }

    [Fact]
    public async Task AnalyzeWithCsvCreatesDefensiveCsvAsFourthArtifact()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var workspace = new TemporaryWorkspace();
        var source = workspace.WriteFile("message.eml", MinimalEmail());
        var output = workspace.PathOf("out");

        var result = await RunAsync("analyze", source, "--output", output, "--csv");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("iocs.csv", result.StandardOutput, StringComparison.Ordinal);
        var csvPath = Path.Combine(output, "iocs.csv");
        Assert.True(File.Exists(csvPath));
        var csv = await File.ReadAllTextAsync(csvPath, cancellationToken);
        Assert.StartsWith("type,value,confidence,source,firstSeen\r\n", csv, StringComparison.Ordinal);
        Assert.Contains("Url,https://example.test/path", csv, StringComparison.Ordinal);
        AssertUtf8WithoutBom(csvPath);
        Assert.Equal(4, Directory.GetFiles(output).Length);
    }

    [Theory]
    [InlineData("--csv", "--database")]
    [InlineData("--database", "--csv")]
    public async Task AnalyzeCombinesCsvAndDatabaseInEitherOptionOrder(string firstOption, string secondOption)
    {
        using var workspace = new TemporaryWorkspace();
        var source = workspace.WriteFile("message.eml", MinimalEmail());
        var output = workspace.PathOf("out");
        var databasePath = workspace.PathOf("data", "incidents.db");
        var arguments = new List<string> { "analyze", source, "--output", output };
        foreach (var option in new[] { firstOption, secondOption })
        {
            arguments.Add(option);
            if (option == "--database")
            {
                arguments.Add(databasePath);
            }
        }

        var result = await RunAsync([.. arguments]);

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(output, "iocs.csv")));
        Assert.True(File.Exists(databasePath));
        SqliteConnection.ClearAllPools();
    }

    [Theory]
    [InlineData("--database")]
    [InlineData("-d")]
    public async Task AnalyzeWithDatabasePersistsTheSameIncident(string databaseOption)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var workspace = new TemporaryWorkspace();
        var source = workspace.WriteFile("message.eml", MinimalEmail());
        var output = workspace.PathOf("out");
        var databasePath = workspace.PathOf("data", "incidents.db");

        var result = await RunAsync(
            "analyze", source, "--output", output, databaseOption, databasePath);

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(databasePath));
        Assert.Contains("saved locally", result.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(
            await File.ReadAllTextAsync(Path.Combine(output, "incident.json"), cancellationToken));
        var incidentId = document.RootElement.GetProperty("incidentId").GetGuid();
        var store = SqliteIncidentStore.FromFile(databasePath);
        var persisted = await store.GetByIdAsync(incidentId, cancellationToken);

        Assert.NotNull(persisted);
        Assert.Equal(incidentId, persisted.IncidentId);
        SqliteConnection.ClearAllPools();
    }

    [Fact]
    public async Task DatabasePathEqualToSourceReturnsUsageErrorWithoutAnalysis()
    {
        using var workspace = new TemporaryWorkspace();
        var source = workspace.WriteFile("message.eml", MinimalEmail());
        var analyzer = new CountingAnalyzer();
        var application = CreateApplication(
            new StringWriter(),
            new StringWriter(),
            analyzer);

        var exitCode = await application.RunAsync(
            ["analyze", source, "--output", workspace.PathOf("out"), "--database", source],
            TestContext.Current.CancellationToken);

        Assert.Equal(2, exitCode);
        Assert.Equal(0, analyzer.CallCount);
        Assert.Equal(MinimalEmail(), await File.ReadAllTextAsync(source, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DatabasePathEqualToReportReturnsUsageErrorWithoutAnalysis()
    {
        using var workspace = new TemporaryWorkspace();
        var source = workspace.WriteFile("message.eml", MinimalEmail());
        var output = workspace.PathOf("out");
        var analyzer = new CountingAnalyzer();
        var application = CreateApplication(
            new StringWriter(),
            new StringWriter(),
            analyzer);

        var exitCode = await application.RunAsync(
            [
                "analyze",
                source,
                "--output",
                output,
                "--database",
                Path.Combine(output, "incident.json")
            ],
            TestContext.Current.CancellationToken);

        Assert.Equal(2, exitCode);
        Assert.Equal(0, analyzer.CallCount);
        Assert.False(Directory.Exists(output));
    }

    [Fact]
    public async Task PersistenceFailureRollsBackReportsAndDoesNotLeakSensitiveContent()
    {
        using var workspace = new TemporaryWorkspace();
        var source = workspace.WriteFile("message.eml", MinimalEmail());
        var output = workspace.PathOf("out");
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var application = CreateApplication(
            stdout,
            stderr,
            new PassthroughAnalyzer(),
            _ => new ThrowingIncidentStore(SensitiveValue));

        var exitCode = await application.RunAsync(
            ["analyze", source, "--output", output, "--csv", "--database", workspace.PathOf("incidents.db")],
            TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.DoesNotContain(SensitiveValue, stdout.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(SensitiveValue, stderr.ToString(), StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(output));
        Assert.Empty(Directory.GetFiles(output, ".frelon-*.tmp"));
    }

    [Fact]
    public async Task ExistingCsvIsIgnoredWhenCsvExportIsNotRequested()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var workspace = new TemporaryWorkspace();
        var source = workspace.WriteFile("message.eml", MinimalEmail());
        var output = workspace.CreateDirectory("out");
        var existingCsv = workspace.WriteFile(Path.Combine("out", "iocs.csv"), "keep-me");

        var result = await RunAsync("analyze", source, "--output", output);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("keep-me", await File.ReadAllTextAsync(existingCsv, cancellationToken));
        Assert.Equal(4, Directory.GetFiles(output).Length);
    }

    [Theory]
    [InlineData("incident.json")]
    [InlineData("report.md")]
    [InlineData("iocs.json")]
    [InlineData("iocs.csv")]
    public async Task ExistingOutputIsPreservedAndNoPartialOutputIsCreated(string conflictingName)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var workspace = new TemporaryWorkspace();
        var source = workspace.WriteFile("message.eml", MinimalEmail());
        var output = workspace.CreateDirectory("out");
        var conflictPath = Path.Combine(output, conflictingName);
        await File.WriteAllTextAsync(conflictPath, "keep-me", cancellationToken);

        var arguments = conflictingName == "iocs.csv"
            ? new[] { "analyze", source, "--output", output, "--csv" }
            : ["analyze", source, "--output", output];
        var result = await RunAsync(arguments);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("keep-me", await File.ReadAllTextAsync(conflictPath, cancellationToken));
        Assert.Single(Directory.GetFiles(output));
        Assert.Empty(Directory.GetFiles(output, ".frelon-*.tmp"));
    }

    [Fact]
    public async Task AnalysisFailureReturnsOneCleansTemporaryFilesAndDoesNotLeakSensitiveContent()
    {
        using var workspace = new TemporaryWorkspace();
        var source = workspace.WriteFile("message.eml", MinimalEmail(SensitiveValue));
        var output = workspace.PathOf("out");
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var application = CreateApplication(stdout, stderr, new ThrowingAnalyzer(SensitiveValue));

        var exitCode = await application.RunAsync(
            ["analyze", source, "--output", output],
            TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.DoesNotContain(SensitiveValue, stdout.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(SensitiveValue, stderr.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(output));
        Assert.Empty(Directory.GetFiles(workspace.Root, ".frelon-*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task CliAssemblyCanExecuteAnalyzeCommand()
    {
        using var workspace = new TemporaryWorkspace();
        var source = workspace.WriteFile("process message.eml", MinimalEmail());
        var output = workspace.PathOf("process output");
        var assemblyPath = typeof(CliApplication).Assembly.Location;
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(assemblyPath);
        startInfo.ArgumentList.Add("analyze");
        startInfo.ArgumentList.Add(source);
        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add(output);

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);
        var cancellationToken = TestContext.Current.CancellationToken;
        var standardOutput = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        Assert.Equal(0, process.ExitCode);
        Assert.Contains("Analysis complete", standardOutput, StringComparison.Ordinal);
        Assert.Empty(standardError);
        Assert.True(File.Exists(Path.Combine(output, "incident.json")));
    }

    private static async Task<CliResult> RunAsync(params string[] arguments)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exitCode = await CliApplication.CreateDefault(stdout, stderr)
            .RunAsync(arguments, TestContext.Current.CancellationToken);
        return new CliResult(exitCode, stdout.ToString(), stderr.ToString());
    }

    private static CliApplication CreateApplication(
        TextWriter stdout,
        TextWriter stderr,
        IEmailIncidentAnalyzer analyzer,
        Func<string, IIncidentStore>? incidentStoreFactory = null)
        => new(
            analyzer,
            new SystemTextJsonIncidentJsonWriter(),
            new BasicIncidentMarkdownReportWriter(),
            new SystemTextJsonIocsJsonWriter(),
            new BasicIocCsvExporter(),
            incidentStoreFactory ?? SqliteIncidentStore.FromFile,
            stdout,
            stderr);

    private static string MinimalEmail(string body = "Visit https://example.test/path")
        => $"From: sender@example.test\r\nTo: analyst@example.test\r\nSubject: Test\r\nMessage-ID: <test@example.test>\r\nContent-Type: text/plain; charset=utf-8\r\n\r\n{body}\r\n";

    private static void AssertUtf8WithoutBom(string path)
    {
        var bytes = File.ReadAllBytes(path);
        Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
    }

    private sealed record CliResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed class ThrowingAnalyzer(string sensitiveValue) : IEmailIncidentAnalyzer
    {
        public Task<FraudIncident> AnalyzeAsync(
            Stream emlStream,
            string? sourceFileName = null,
            CancellationToken cancellationToken = default)
            => throw new InvalidDataException(sensitiveValue);
    }

    private sealed class CountingAnalyzer : IEmailIncidentAnalyzer
    {
        public int CallCount { get; private set; }

        public Task<FraudIncident> AnalyzeAsync(
            Stream emlStream,
            string? sourceFileName = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            throw new InvalidOperationException("The analyzer must not be called.");
        }
    }

    private sealed class PassthroughAnalyzer : IEmailIncidentAnalyzer
    {
        public Task<FraudIncident> AnalyzeAsync(
            Stream emlStream,
            string? sourceFileName = null,
            CancellationToken cancellationToken = default)
        {
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new FraudIncident
            {
                IncidentId = Guid.NewGuid(),
                CreatedAt = now,
                Evidence = new EvidenceSource
                {
                    FileName = sourceFileName ?? "unknown.eml",
                    ImportedAt = now
                },
                Identity = new MailIdentity(),
                Authentication = new AuthenticationAssessment(),
                Classification = FraudClassification.Unknown,
                RiskScore = new RiskScore { Value = 0, Level = RiskLevel.Unknown }
            });
        }
    }

    private sealed class ThrowingIncidentStore(string sensitiveValue) : IIncidentStore
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SaveAsync(FraudIncident incident, CancellationToken cancellationToken = default)
            => throw new InvalidDataException(sensitiveValue);

        public Task<FraudIncident?> GetByIdAsync(
            Guid incidentId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<IncidentSummary>> ListRecentAsync(
            int limit = 100,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class TemporaryWorkspace : IDisposable
    {
        public TemporaryWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), $"frelon-cli-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string PathOf(params string[] segments)
            => segments.Aggregate(Root, Path.Combine);

        public string WriteFile(string relativePath, string content)
        {
            var path = PathOf(relativePath);
            File.WriteAllText(path, content, new UTF8Encoding(false));
            return path;
        }

        public string CreateDirectory(string relativePath)
        {
            var path = PathOf(relativePath);
            Directory.CreateDirectory(path);
            return path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch
            {
                // Test cleanup is best effort.
            }
        }
    }
}
