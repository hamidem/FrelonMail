using System.Globalization;
using Frelon.Reports;
using Frelon.Storage;

namespace Frelon.Cli;

/// <summary>Exécute les consultations en lecture seule de la base locale d'incidents.</summary>
internal sealed class IncidentConsultationRunner
{
    private readonly IIncidentJsonWriter _incidentWriter;
    private readonly Func<string, IIncidentStore> _incidentStoreFactory;
    private readonly TextWriter _standardOutput;
    private readonly TextWriter _standardError;

    public IncidentConsultationRunner(
        IIncidentJsonWriter incidentWriter,
        Func<string, IIncidentStore> incidentStoreFactory,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        _incidentWriter = incidentWriter;
        _incidentStoreFactory = incidentStoreFactory;
        _standardOutput = standardOutput;
        _standardError = standardError;
    }

    public async Task<int> RunAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        if (!TryParse(arguments, out var command))
        {
            await WriteUsageAsync().ConfigureAwait(false);
            return 2;
        }

        string fullDatabasePath;
        try
        {
            fullDatabasePath = Path.GetFullPath(command.DatabasePath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            await _standardError.WriteLineAsync("Invalid database path.").ConfigureAwait(false);
            return 2;
        }

        if (!File.Exists(fullDatabasePath))
        {
            await _standardError.WriteLineAsync("The incident database does not exist.").ConfigureAwait(false);
            return 2;
        }

        try
        {
            var store = _incidentStoreFactory(fullDatabasePath);
            if (command.IncidentId is Guid incidentId)
            {
                return await ShowAsync(store, incidentId, cancellationToken).ConfigureAwait(false);
            }

            return await ListAsync(store, command.Limit, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await _standardError.WriteLineAsync("Consultation cancelled.").ConfigureAwait(false);
            return 1;
        }
        catch (Exception)
        {
            await _standardError.WriteLineAsync("Incident consultation failed.").ConfigureAwait(false);
            return 1;
        }
    }

    private async Task<int> ShowAsync(
        IIncidentStore store,
        Guid incidentId,
        CancellationToken cancellationToken)
    {
        var incident = await store.GetByIdAsync(incidentId, cancellationToken).ConfigureAwait(false);
        if (incident is null)
        {
            await _standardError.WriteLineAsync("Incident not found.").ConfigureAwait(false);
            return 1;
        }

        await _standardOutput.WriteLineAsync(_incidentWriter.Write(incident)).ConfigureAwait(false);
        return 0;
    }

    private async Task<int> ListAsync(
        IIncidentStore store,
        int limit,
        CancellationToken cancellationToken)
    {
        var incidents = await store.ListRecentAsync(limit, cancellationToken).ConfigureAwait(false);
        if (incidents.Count == 0)
        {
            await _standardOutput.WriteLineAsync("No incidents found.").ConfigureAwait(false);
            return 0;
        }

        await _standardOutput
            .WriteLineAsync("IncidentId\tCreatedAt\tSource\tRisk\tLevel\tClassification\tReview")
            .ConfigureAwait(false);
        foreach (var incident in incidents)
        {
            var review = incident.LatestReviewVerdict?.ToString() ?? "Pending";
            await _standardOutput.WriteLineAsync(string.Join(
                '\t',
                incident.IncidentId,
                incident.CreatedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                SanitizeColumn(incident.SourceFileName),
                incident.RiskValue.ToString("0.##", CultureInfo.InvariantCulture),
                incident.RiskLevel,
                incident.Classification,
                review)).ConfigureAwait(false);
        }

        return 0;
    }

    private static bool TryParse(
        IReadOnlyList<string> arguments,
        out IncidentConsultationCommand command)
    {
        command = default;
        if (arguments.Count < 4 || arguments[0] != "incidents")
        {
            return false;
        }

        if (arguments[1] == "list")
        {
            string? databasePath = null;
            var limit = 100;
            var limitSeen = false;

            for (var index = 2; index < arguments.Count; index++)
            {
                if (arguments[index] is "--database" or "-d"
                    && databasePath is null
                    && index + 1 < arguments.Count
                    && !string.IsNullOrWhiteSpace(arguments[index + 1])
                    && !IsOption(arguments[index + 1]))
                {
                    databasePath = arguments[++index];
                    continue;
                }

                if (arguments[index] == "--limit"
                    && !limitSeen
                    && index + 1 < arguments.Count
                    && int.TryParse(arguments[index + 1], NumberStyles.None, CultureInfo.InvariantCulture, out limit)
                    && limit is >= 1 and <= 500)
                {
                    limitSeen = true;
                    index++;
                    continue;
                }

                return false;
            }

            if (databasePath is null)
            {
                return false;
            }

            command = new IncidentConsultationCommand(databasePath, limit, null);
            return true;
        }

        if (arguments[1] == "show"
            && arguments.Count == 5
            && Guid.TryParse(arguments[2], out var incidentId)
            && arguments[3] is "--database" or "-d"
            && !string.IsNullOrWhiteSpace(arguments[4])
            && !IsOption(arguments[4]))
        {
            command = new IncidentConsultationCommand(arguments[4], 1, incidentId);
            return true;
        }

        return false;
    }

    private async Task WriteUsageAsync()
    {
        await _standardError
            .WriteLineAsync("Usage: frelon incidents list --database <sqlite-file> [--limit <1-500>]")
            .ConfigureAwait(false);
        await _standardError
            .WriteLineAsync("       frelon incidents show <incident-id> --database <sqlite-file>")
            .ConfigureAwait(false);
    }

    private static bool IsOption(string value)
        => value is "--database" or "-d" or "--limit";

    private static string SanitizeColumn(string value)
        => string.Concat(value.Select(character => char.IsControl(character) ? ' ' : character));

    private readonly record struct IncidentConsultationCommand(
        string DatabasePath,
        int Limit,
        Guid? IncidentId);
}
