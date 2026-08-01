using System.Text;
using Frelon.Core;
using Frelon.Exporters;
using Frelon.Mail;
using Frelon.Reports;
using Frelon.Storage;

namespace Frelon.Cli;

/// <summary>Exécute les commandes de la ligne de commande Frelon.</summary>
public sealed class CliApplication
{
    private const string IncidentFileName = "incident.json";
    private const string ReportFileName = "report.md";
    private const string IocsFileName = "iocs.json";
    private const string IocsCsvFileName = "iocs.csv";
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    private readonly IEmailIncidentAnalyzer _analyzer;
    private readonly IIncidentJsonWriter _incidentWriter;
    private readonly IIncidentMarkdownReportWriter _reportWriter;
    private readonly IIocsJsonWriter _iocsWriter;
    private readonly IIocCsvExporter _iocCsvExporter;
    private readonly Func<string, IIncidentStore> _incidentStoreFactory;
    private readonly TextWriter _standardOutput;
    private readonly TextWriter _standardError;

    /// <summary>Initialise une application CLI avec l'exporteur CSV défensif par défaut.</summary>
    /// <param name="analyzer">Pipeline d'analyse des emails.</param>
    /// <param name="incidentWriter">Sérialiseur du rapport d'incident JSON.</param>
    /// <param name="reportWriter">Générateur du rapport Markdown.</param>
    /// <param name="iocsWriter">Sérialiseur du rapport des IOC JSON.</param>
    /// <param name="incidentStoreFactory">Fabrique de stockage local à partir d'un chemin SQLite.</param>
    /// <param name="standardOutput">Destination des messages de succès.</param>
    /// <param name="standardError">Destination des messages d'erreur.</param>
    public CliApplication(
        IEmailIncidentAnalyzer analyzer,
        IIncidentJsonWriter incidentWriter,
        IIncidentMarkdownReportWriter reportWriter,
        IIocsJsonWriter iocsWriter,
        Func<string, IIncidentStore> incidentStoreFactory,
        TextWriter standardOutput,
        TextWriter standardError)
        : this(
            analyzer,
            incidentWriter,
            reportWriter,
            iocsWriter,
            new BasicIocCsvExporter(),
            incidentStoreFactory,
            standardOutput,
            standardError)
    {
    }

    /// <summary>Initialise une application CLI avec ses dépendances explicites.</summary>
    /// <param name="analyzer">Pipeline d'analyse des emails.</param>
    /// <param name="incidentWriter">Sérialiseur du rapport d'incident JSON.</param>
    /// <param name="reportWriter">Générateur du rapport Markdown.</param>
    /// <param name="iocsWriter">Sérialiseur du rapport des IOC JSON.</param>
    /// <param name="iocCsvExporter">Exporteur CSV défensif des IOC.</param>
    /// <param name="incidentStoreFactory">Fabrique de stockage local à partir d'un chemin SQLite.</param>
    /// <param name="standardOutput">Destination des messages de succès.</param>
    /// <param name="standardError">Destination des messages d'erreur.</param>
    public CliApplication(
        IEmailIncidentAnalyzer analyzer,
        IIncidentJsonWriter incidentWriter,
        IIncidentMarkdownReportWriter reportWriter,
        IIocsJsonWriter iocsWriter,
        IIocCsvExporter iocCsvExporter,
        Func<string, IIncidentStore> incidentStoreFactory,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
        _incidentWriter = incidentWriter ?? throw new ArgumentNullException(nameof(incidentWriter));
        _reportWriter = reportWriter ?? throw new ArgumentNullException(nameof(reportWriter));
        _iocsWriter = iocsWriter ?? throw new ArgumentNullException(nameof(iocsWriter));
        _iocCsvExporter = iocCsvExporter ?? throw new ArgumentNullException(nameof(iocCsvExporter));
        _incidentStoreFactory = incidentStoreFactory ?? throw new ArgumentNullException(nameof(incidentStoreFactory));
        _standardOutput = standardOutput ?? throw new ArgumentNullException(nameof(standardOutput));
        _standardError = standardError ?? throw new ArgumentNullException(nameof(standardError));
    }

    /// <summary>Crée l'application avec le pipeline local réel.</summary>
    public static CliApplication CreateDefault(TextWriter standardOutput, TextWriter standardError)
    {
        return new CliApplication(
            EmailIncidentAnalyzerFactory.CreateDefault(),
            new SystemTextJsonIncidentJsonWriter(),
            new BasicIncidentMarkdownReportWriter(),
            new SystemTextJsonIocsJsonWriter(),
            new BasicIocCsvExporter(),
            SqliteIncidentStore.FromFile,
            standardOutput,
            standardError);
    }

    /// <summary>Crée l'application avec le moteur réel exécuté dans un processus isolé.</summary>
    public static CliApplication CreateIsolated(TextWriter standardOutput, TextWriter standardError)
    {
        return new CliApplication(
            IsolatedEmailAnalysis.CreateAnalyzer(),
            new SystemTextJsonIncidentJsonWriter(),
            new BasicIncidentMarkdownReportWriter(),
            new SystemTextJsonIocsJsonWriter(),
            new BasicIocCsvExporter(),
            SqliteIncidentStore.FromFile,
            standardOutput,
            standardError);
    }

    /// <summary>Exécute une commande et retourne son code de sortie.</summary>
    public async Task<int> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (arguments.Count > 0
            && string.Equals(arguments[0], "incidents", StringComparison.Ordinal))
        {
            var consultation = new IncidentConsultationRunner(
                _incidentWriter,
                _incidentStoreFactory,
                _standardOutput,
                _standardError);
            return await consultation.RunAsync(arguments, cancellationToken).ConfigureAwait(false);
        }

        if (!TryParse(arguments, out var sourcePath, out var outputDirectory, out var databasePath, out var exportCsv))
        {
            await _standardError
                .WriteLineAsync("Usage: frelon analyze <message-path> --output <directory> [--csv] [--database <sqlite-file>]")
                .ConfigureAwait(false);
            return 2;
        }

        string fullSourcePath;
        string fullOutputDirectory;
        string? fullDatabasePath;
        try
        {
            fullSourcePath = Path.GetFullPath(sourcePath);
            fullOutputDirectory = Path.GetFullPath(outputDirectory);
            fullDatabasePath = databasePath is null ? null : Path.GetFullPath(databasePath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            await _standardError.WriteLineAsync("Invalid source or output path.").ConfigureAwait(false);
            return 2;
        }

        var sourceExtension = Path.GetExtension(fullSourcePath);
        var hasSupportedExtension =
            string.Equals(sourceExtension, ".eml", StringComparison.OrdinalIgnoreCase)
            || string.Equals(sourceExtension, ".msg", StringComparison.OrdinalIgnoreCase);

        if (!File.Exists(fullSourcePath)
            || !hasSupportedExtension
            || PathsEqual(fullSourcePath, fullOutputDirectory))
        {
            await _standardError.WriteLineAsync("The source must be an existing .eml or .msg file and the output must be a different path.").ConfigureAwait(false);
            return 2;
        }

        long sourceLength;
        try
        {
            sourceLength = new FileInfo(fullSourcePath).Length;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await _standardError
                .WriteLineAsync("The source email could not be inspected safely.")
                .ConfigureAwait(false);
            return 2;
        }

        if (sourceLength is 0 or > EmailAnalysisLimits.DefaultMaxSourceBytes)
        {
            await _standardError
                .WriteLineAsync("The source email must be between 1 byte and 25 MB.")
                .ConfigureAwait(false);
            return 2;
        }

        if (File.Exists(fullOutputDirectory))
        {
            await _standardError.WriteLineAsync("The output path must be a directory.").ConfigureAwait(false);
            return 2;
        }

        if (fullDatabasePath is not null
            && (PathsEqual(fullSourcePath, fullDatabasePath) || Directory.Exists(fullDatabasePath)))
        {
            await _standardError
                .WriteLineAsync("The database path must be a file distinct from the source email.")
                .ConfigureAwait(false);
            return 2;
        }

        var finalPaths = new List<string>
        {
            Path.Combine(fullOutputDirectory, IncidentFileName),
            Path.Combine(fullOutputDirectory, ReportFileName),
            Path.Combine(fullOutputDirectory, IocsFileName),
        };
        if (exportCsv)
        {
            finalPaths.Add(Path.Combine(fullOutputDirectory, IocsCsvFileName));
        }

        if (fullDatabasePath is not null
            && finalPaths.Any(finalPath => PathsEqual(finalPath, fullDatabasePath)))
        {
            await _standardError
                .WriteLineAsync("The database path must be distinct from report files.")
                .ConfigureAwait(false);
            return 2;
        }

        if (finalPaths.Any(Path.Exists))
        {
            await _standardError.WriteLineAsync("Output conflict: Frelon never overwrites an existing report.").ConfigureAwait(false);
            return 2;
        }

        var temporaryPaths = new List<string>(finalPaths.Count);
        var createdFinalPaths = new List<string>(finalPaths.Count);
        var operationCompleted = false;

        try
        {
            await using var source = new FileStream(
                fullSourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            var incident = await _analyzer
                .AnalyzeAsync(source, Path.GetFileName(fullSourcePath), cancellationToken)
                .ConfigureAwait(false);

            var contents = new List<string>
            {
                _incidentWriter.Write(incident),
                _reportWriter.Write(incident),
                _iocsWriter.Write(incident),
            };
            if (exportCsv)
            {
                contents.Add(_iocCsvExporter.Export(incident));
            }

            Directory.CreateDirectory(fullOutputDirectory);
            foreach (var content in contents)
            {
                var temporaryPath = Path.Combine(fullOutputDirectory, $".frelon-{Guid.NewGuid():N}.tmp");
                await using var temporaryFile = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 4096,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                temporaryPaths.Add(temporaryPath);
                await using var writer = new StreamWriter(temporaryFile, Utf8WithoutBom, leaveOpen: false);
                await writer.WriteAsync(content.AsMemory(), cancellationToken).ConfigureAwait(false);
            }

            // Recheck immediately before publication to handle a concurrent writer safely.
            if (finalPaths.Any(Path.Exists))
            {
                await _standardError.WriteLineAsync("Output conflict: Frelon never overwrites an existing report.").ConfigureAwait(false);
                return 2;
            }

            for (var index = 0; index < finalPaths.Count; index++)
            {
                File.Move(temporaryPaths[index], finalPaths[index], overwrite: false);
                createdFinalPaths.Add(finalPaths[index]);
            }

            if (fullDatabasePath is not null)
            {
                var databaseDirectory = Path.GetDirectoryName(fullDatabasePath)
                    ?? throw new InvalidOperationException("The database parent directory is unavailable.");
                Directory.CreateDirectory(databaseDirectory);

                var store = _incidentStoreFactory(fullDatabasePath);
                await store.InitializeAsync(cancellationToken).ConfigureAwait(false);
                await store.SaveAsync(incident, cancellationToken).ConfigureAwait(false);
            }

            operationCompleted = true;

            var successMessage = fullDatabasePath is not null
                ? "Analysis complete: reports created and incident saved locally."
                : exportCsv
                    ? "Analysis complete: incident.json, report.md, iocs.json, iocs.csv created."
                    : "Analysis complete: incident.json, report.md, iocs.json created.";
            await _standardOutput.WriteLineAsync(successMessage).ConfigureAwait(false);
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await _standardError.WriteLineAsync("Analysis cancelled.").ConfigureAwait(false);
            return 1;
        }
        catch (EmailAnalysisLimitException)
        {
            await _standardError
                .WriteLineAsync("Analysis refused: the message exceeds Frelon's safety limits.")
                .ConfigureAwait(false);
            return 1;
        }
        catch (EmailAnalysisTimeoutException)
        {
            await _standardError
                .WriteLineAsync("Analysis stopped: the message exceeded Frelon's time limit.")
                .ConfigureAwait(false);
            return 1;
        }
        catch (Exception)
        {
            await _standardError.WriteLineAsync("Analysis or file operation failed.").ConfigureAwait(false);
            return 1;
        }
        finally
        {
            foreach (var path in temporaryPaths)
            {
                TryDelete(path);
            }

            // A publication or persistence failure must not leave Frelon-created reports behind.
            if (createdFinalPaths.Count != 0
                && (!operationCompleted || createdFinalPaths.Count != finalPaths.Count))
            {
                foreach (var path in createdFinalPaths)
                {
                    TryDelete(path);
                }
            }
        }
    }

    private static bool TryParse(
        IReadOnlyList<string> arguments,
        out string sourcePath,
        out string outputDirectory,
        out string? databasePath,
        out bool exportCsv)
    {
        sourcePath = string.Empty;
        outputDirectory = string.Empty;
        databasePath = null;
        exportCsv = false;

        if (arguments.Count < 4
            || !string.Equals(arguments[0], "analyze", StringComparison.Ordinal)
            || (arguments[2] != "--output" && arguments[2] != "-o")
            || string.IsNullOrWhiteSpace(arguments[1])
            || string.IsNullOrWhiteSpace(arguments[3]))
        {
            return false;
        }

        sourcePath = arguments[1];
        outputDirectory = arguments[3];

        for (var index = 4; index < arguments.Count; index++)
        {
            if (arguments[index] == "--csv" && !exportCsv)
            {
                exportCsv = true;
                continue;
            }

            if ((arguments[index] == "--database" || arguments[index] == "-d")
                && databasePath is null
                && index + 1 < arguments.Count
                && !string.IsNullOrWhiteSpace(arguments[index + 1])
                && arguments[index + 1] is not ("--csv" or "--database" or "-d"))
            {
                databasePath = arguments[++index];
                continue;
            }

            return false;
        }

        return true;
    }

    private static bool PathsEqual(string left, string right)
        => string.Equals(
            Path.TrimEndingDirectorySeparator(left),
            Path.TrimEndingDirectorySeparator(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup must not hide the original failure.
        }
    }
}
