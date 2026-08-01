using System.IO.Compression;
using System.Text;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Frelon.Core;
using Frelon.Exporters;
using Frelon.Reports;

namespace Frelon.Web;

/// <summary>Prépare les productions téléchargeables d'un incident sans écrire sur le disque.</summary>
public sealed class IncidentExportService
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);
    private static readonly JsonSerializerOptions ReviewJsonOptions = CreateReviewJsonOptions();

    private readonly IIncidentJsonWriter _incidentWriter;
    private readonly IIncidentMarkdownReportWriter _reportWriter;
    private readonly IIocsJsonWriter _iocsWriter;
    private readonly IIocCsvExporter _iocCsvExporter;
    private readonly IValidatedIncidentMarkdownReportWriter _validatedReportWriter;

    /// <summary>Initialise le service avec les générateurs métier existants.</summary>
    public IncidentExportService(
        IIncidentJsonWriter incidentWriter,
        IIncidentMarkdownReportWriter reportWriter,
        IIocsJsonWriter iocsWriter,
        IIocCsvExporter iocCsvExporter,
        IValidatedIncidentMarkdownReportWriter validatedReportWriter)
    {
        _incidentWriter = incidentWriter ?? throw new ArgumentNullException(nameof(incidentWriter));
        _reportWriter = reportWriter ?? throw new ArgumentNullException(nameof(reportWriter));
        _iocsWriter = iocsWriter ?? throw new ArgumentNullException(nameof(iocsWriter));
        _iocCsvExporter = iocCsvExporter ?? throw new ArgumentNullException(nameof(iocCsvExporter));
        _validatedReportWriter = validatedReportWriter ?? throw new ArgumentNullException(nameof(validatedReportWriter));
    }

    /// <summary>Crée le service avec les générateurs locaux de référence.</summary>
    public static IncidentExportService CreateDefault()
        => new(
            new SystemTextJsonIncidentJsonWriter(),
            new BasicIncidentMarkdownReportWriter(),
            new SystemTextJsonIocsJsonWriter(),
            new BasicIocCsvExporter(),
            new BasicValidatedIncidentMarkdownReportWriter());

    /// <summary>Essaie de créer un export individuel à partir de son identifiant public.</summary>
    public bool TryCreate(
        FraudIncident incident,
        string format,
        [NotNullWhen(true)]
        out IncidentExportArtifact? artifact)
    {
        ArgumentNullException.ThrowIfNull(incident);

        artifact = format switch
        {
            "incident-json" => CreateText(
                "incident.json",
                "application/json; charset=utf-8",
                _incidentWriter.Write(incident)),
            "report-markdown" => CreateText(
                "report.md",
                "text/markdown; charset=utf-8",
                _reportWriter.Write(incident)),
            "iocs-json" => CreateText(
                "iocs.json",
                "application/json; charset=utf-8",
                _iocsWriter.Write(incident)),
            "iocs-csv" => CreateText(
                "iocs.csv",
                "text/csv; charset=utf-8",
                _iocCsvExporter.Export(incident)),
            _ => null
        };

        return artifact is not null;
    }

    /// <summary>Crée un signalement seulement si la dernière décision humaine l'autorise.</summary>
    public bool TryCreateValidatedReport(
        FraudIncident incident,
        IncidentReviewDecision decision,
        [NotNullWhen(true)]
        out IncidentExportArtifact? artifact)
    {
        ArgumentNullException.ThrowIfNull(incident);
        ArgumentNullException.ThrowIfNull(decision);

        if (decision.IncidentId != incident.IncidentId)
        {
            throw new ArgumentException(
                "La décision humaine ne correspond pas à l'incident exporté.",
                nameof(decision));
        }

        if (!_validatedReportWriter.CanWrite(decision))
        {
            artifact = null;
            return false;
        }

        artifact = CreateText(
            "signalement.md",
            "text/markdown; charset=utf-8",
            _validatedReportWriter.Write(incident, decision));
        return true;
    }

    /// <summary>Regroupe tous les exports dans une archive ZIP en mémoire.</summary>
    public IncidentExportArtifact CreateBundle(
        FraudIncident incident,
        IncidentReviewDecision? review = null)
    {
        ArgumentNullException.ThrowIfNull(incident);

        var artifacts = new List<IncidentExportArtifact>
        {
            CreateRequired(incident, "incident-json"),
            CreateRequired(incident, "report-markdown"),
            CreateRequired(incident, "iocs-json"),
            CreateRequired(incident, "iocs-csv")
        };
        if (review is not null)
        {
            if (review.IncidentId != incident.IncidentId)
            {
                throw new ArgumentException(
                    "La décision humaine ne correspond pas à l'incident exporté.",
                    nameof(review));
            }

            artifacts.Add(CreateReview(review));
            if (TryCreateValidatedReport(incident, review, out var validatedReport))
            {
                artifacts.Add(validatedReport);
            }
        }

        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var artifact in artifacts)
            {
                var entry = archive.CreateEntry(artifact.FileName, CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                entryStream.Write(artifact.Content);
            }
        }

        return new IncidentExportArtifact(
            $"frelon-{incident.IncidentId:N}.zip",
            "application/zip",
            output.ToArray());
    }

    /// <summary>Crée le fichier JSON d'une décision humaine.</summary>
    public IncidentExportArtifact CreateReview(IncidentReviewDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        return CreateText(
            "review.json",
            "application/json; charset=utf-8",
            JsonSerializer.Serialize(decision, ReviewJsonOptions));
    }

    private IncidentExportArtifact CreateRequired(FraudIncident incident, string format)
    {
        if (TryCreate(incident, format, out var artifact))
        {
            return artifact;
        }

        throw new InvalidOperationException($"Le format interne '{format}' est indisponible.");
    }

    private static IncidentExportArtifact CreateText(string fileName, string contentType, string content)
        => new(fileName, contentType, Utf8WithoutBom.GetBytes(content));

    private static JsonSerializerOptions CreateReviewJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter(
            namingPolicy: null,
            allowIntegerValues: false));
        return options;
    }
}

/// <summary>Fichier produit en mémoire et prêt à être téléchargé.</summary>
public sealed record IncidentExportArtifact(
    string FileName,
    string ContentType,
    byte[] Content);
