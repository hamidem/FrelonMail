using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Frelon.Core;

namespace Frelon.Mail;

/// <summary>
/// Héberge l'analyse dans un processus jetable afin qu'un parseur bloqué ou
/// défaillant ne compromette pas le processus principal de Frelon.
/// </summary>
public static class IsolatedEmailAnalysis
{
    /// <summary>Argument privé réservé au processus d'analyse enfant.</summary>
    public const string WorkerArgument = "--frelon-internal-analysis-worker";

    private const int InvalidInputExitCode = 11;
    private const int LimitExceededExitCode = 12;
    private const int InternalFailureExitCode = 13;
    private const int MaxFileNameBytes = 4 * 1024;
    private const int MaxResponseBytes = 16 * 1024 * 1024;
    private const int MaxErrorBytes = 64 * 1024;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    /// <summary>Indique si le processus courant a été lancé comme worker interne.</summary>
    public static bool IsWorkerInvocation(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return arguments.Count == 1
            && string.Equals(arguments[0], WorkerArgument, StringComparison.Ordinal);
    }

    /// <summary>Crée l'analyseur isolé destiné aux points d'entrée Web et CLI.</summary>
    public static IEmailIncidentAnalyzer CreateAnalyzer()
        => CreateAnalyzer(CreateCurrentProcessStartInfo, DefaultTimeout);

    internal static IEmailIncidentAnalyzer CreateAnalyzer(
        Func<ProcessStartInfo> startInfoFactory,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(startInfoFactory);
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        return new ProcessIsolatedAnalyzer(startInfoFactory, timeout);
    }

    /// <summary>
    /// Exécute une requête reçue sur l'entrée standard et écrit uniquement le
    /// résultat structuré sur la sortie standard.
    /// </summary>
    public static async Task<int> RunWorkerAsync(
        Stream standardInput,
        Stream standardOutput,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(standardInput);
        ArgumentNullException.ThrowIfNull(standardOutput);

        try
        {
            var sourceFileName = await ReadFileNameAsync(
                    standardInput,
                    cancellationToken)
                .ConfigureAwait(false);
            var incident = await EmailIncidentAnalyzerFactory
                .CreateDefault()
                .AnalyzeAsync(standardInput, sourceFileName, cancellationToken)
                .ConfigureAwait(false);

            await JsonSerializer
                .SerializeAsync(standardOutput, incident, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            await standardOutput.FlushAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }
        catch (EmailAnalysisLimitException)
        {
            return LimitExceededExitCode;
        }
        catch (Exception exception) when (
            exception is InvalidDataException
                or NotSupportedException
                or DecoderFallbackException)
        {
            return InvalidInputExitCode;
        }
        catch (OperationCanceledException)
        {
            return InternalFailureExitCode;
        }
        catch
        {
            return InternalFailureExitCode;
        }
    }

    private static async Task<string?> ReadFileNameAsync(
        Stream source,
        CancellationToken cancellationToken)
    {
        var lengthBuffer = new byte[sizeof(int)];
        await ReadExactlyAsync(source, lengthBuffer, cancellationToken).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32LittleEndian(lengthBuffer);
        if (length < 0 || length > MaxFileNameBytes)
        {
            throw new InvalidDataException("Le protocole d'analyse est invalide.");
        }

        if (length == 0)
        {
            return null;
        }

        var fileNameBuffer = new byte[length];
        await ReadExactlyAsync(source, fileNameBuffer, cancellationToken).ConfigureAwait(false);
        return new UTF8Encoding(false, true).GetString(fileNameBuffer);
    }

    private static async Task ReadExactlyAsync(
        Stream source,
        Memory<byte> destination,
        CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < destination.Length)
        {
            var read = await source
                .ReadAsync(destination[total..], cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                throw new InvalidDataException("Le protocole d'analyse est incomplet.");
            }

            total += read;
        }
    }

    private static ProcessStartInfo CreateCurrentProcessStartInfo()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            throw new InvalidOperationException(
                "Le chemin du processus Frelon est indisponible.");
        }

        var startInfo = new ProcessStartInfo(processPath)
        {
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        if (string.Equals(
                Path.GetFileNameWithoutExtension(processPath),
                "dotnet",
                StringComparison.OrdinalIgnoreCase))
        {
            var entryAssemblyPath = Assembly.GetEntryAssembly()?.Location;
            if (string.IsNullOrWhiteSpace(entryAssemblyPath))
            {
                throw new InvalidOperationException(
                    "Le point d'entrée Frelon est indisponible.");
            }

            startInfo.ArgumentList.Add(entryAssemblyPath);
        }

        startInfo.ArgumentList.Add(WorkerArgument);
        return startInfo;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            MaxDepth = 64,
        };
        options.Converters.Add(new JsonStringEnumConverter(
            namingPolicy: null,
            allowIntegerValues: false));
        return options;
    }

    private sealed class ProcessIsolatedAnalyzer(
        Func<ProcessStartInfo> startInfoFactory,
        TimeSpan timeout) : IEmailIncidentAnalyzer
    {
        private readonly SemaphoreSlim _analysisLock = new(1, 1);

        public async Task<FraudIncident> AnalyzeAsync(
            Stream emlStream,
            string? sourceFileName = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(emlStream);

            await _analysisLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var content = await EmailContentBuffer
                    .ReadAsync(emlStream, EmailAnalysisLimits.Default, cancellationToken)
                    .ConfigureAwait(false);
                var fileNameBytes = string.IsNullOrEmpty(sourceFileName)
                    ? []
                    : new UTF8Encoding(false, true).GetBytes(sourceFileName);
                if (fileNameBytes.Length > MaxFileNameBytes)
                {
                    throw new InvalidDataException(
                        "Le nom du fichier source est trop long.");
                }

                return await AnalyzeInChildProcessAsync(
                        content,
                        fileNameBytes,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                _analysisLock.Release();
            }
        }

        private async Task<FraudIncident> AnalyzeInChildProcessAsync(
            byte[] content,
            byte[] fileNameBytes,
            CancellationToken cancellationToken)
        {
            using var worker = AnalysisWorkerProcess.Start(startInfoFactory());
            var process = worker.Process;

            using var timeoutSource = new CancellationTokenSource(timeout);
            using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutSource.Token);
            var executionToken = linkedSource.Token;
            using var killRegistration = executionToken.Register(
                static state => KillProcess((Process)state!),
                process);

            try
            {
                var writeTask = WriteRequestAsync(
                    worker.StandardInput,
                    fileNameBytes,
                    content,
                    executionToken);
                var outputTask = ReadBoundedAsync(
                    worker.StandardOutput,
                    MaxResponseBytes,
                    executionToken);
                var errorTask = ReadBoundedAsync(
                    worker.StandardError,
                    MaxErrorBytes,
                    executionToken);
                var waitTask = process.WaitForExitAsync(executionToken);

                await Task.WhenAll(writeTask, outputTask, errorTask, waitTask)
                    .ConfigureAwait(false);

                var output = await outputTask.ConfigureAwait(false);
                var exitCode = worker.ExitCode;
                return exitCode switch
                {
                    0 => DeserializeIncident(output),
                    InvalidInputExitCode => throw new InvalidDataException(
                        "Le fichier transmis ne peut pas être analysé."),
                    LimitExceededExitCode => throw new EmailAnalysisLimitException(
                        "Le fichier dépasse les limites d'analyse autorisées."),
                    _ => throw new IOException(
                        "Le processus d'analyse isolé a échoué."),
                };
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested)
            {
                KillProcess(process);
                throw new OperationCanceledException(cancellationToken);
            }
            catch (Exception) when (timeoutSource.IsCancellationRequested)
            {
                KillProcess(process);
                throw new EmailAnalysisTimeoutException();
            }
            catch
            {
                KillProcess(process);
                throw;
            }
        }

        private static async Task WriteRequestAsync(
            Stream destination,
            byte[] fileNameBytes,
            byte[] content,
            CancellationToken cancellationToken)
        {
            try
            {
                var lengthBuffer = new byte[sizeof(int)];
                BinaryPrimitives.WriteInt32LittleEndian(
                    lengthBuffer,
                    fileNameBytes.Length);
                await destination
                    .WriteAsync(lengthBuffer, cancellationToken)
                    .ConfigureAwait(false);
                await destination
                    .WriteAsync(fileNameBytes, cancellationToken)
                    .ConfigureAwait(false);
                await destination
                    .WriteAsync(content, cancellationToken)
                    .ConfigureAwait(false);
                await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                await destination.DisposeAsync().ConfigureAwait(false);
            }
        }

        private static async Task<byte[]> ReadBoundedAsync(
            Stream source,
            int maximumBytes,
            CancellationToken cancellationToken)
        {
            await using var destination = new MemoryStream();
            var buffer = new byte[64 * 1024];
            while (true)
            {
                var read = await source
                    .ReadAsync(buffer, cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    return destination.ToArray();
                }

                if (destination.Length + read > maximumBytes)
                {
                    throw new InvalidDataException(
                        "La réponse du processus d'analyse est trop volumineuse.");
                }

                await destination
                    .WriteAsync(buffer.AsMemory(0, read), cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        private static FraudIncident DeserializeIncident(byte[] content)
        {
            try
            {
                var incident = JsonSerializer.Deserialize<FraudIncident>(
                    content,
                    JsonOptions);
                if (incident is null
                    || incident.IncidentId == Guid.Empty
                    || incident.Evidence is null
                    || incident.Identity is null
                    || incident.Authentication is null
                    || incident.RiskScore is null)
                {
                    throw new JsonException();
                }

                return incident;
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException(
                    "La réponse du processus d'analyse est invalide.",
                    exception);
            }
        }

        private static void KillProcess(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception exception) when (
                exception is InvalidOperationException
                    or System.ComponentModel.Win32Exception
                    or NotSupportedException)
            {
                // Le processus est déjà terminé ou ne peut plus être contrôlé.
            }
        }
    }
}
