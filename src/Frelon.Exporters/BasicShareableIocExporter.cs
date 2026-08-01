using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Frelon.Core;

namespace Frelon.Exporters;

/// <summary>
/// Produit un paquet strictement minimisé : domaines et SHA-256 uniquement.
/// </summary>
public sealed class BasicShareableIocExporter : IShareableIocExporter
{
    /// <summary>Version du contrat JSON partageable.</summary>
    public const int SchemaVersion = 1;

    /// <summary>Confiance minimale requise pour partager une observation.</summary>
    public const double MinimumIocConfidence = 0.5;

    private const string JsonContentType = "application/json; charset=utf-8";
    private const string CsvContentType = "text/csv; charset=utf-8";
    private const string MarkdownContentType = "text/markdown; charset=utf-8";
    private const string PrivacyProfile = "StrictMinimization";
    private const string PrivacyNotice =
        "Cet export supprime les références locales et minimise les IOC, mais ne constitue pas une garantie " +
        "d'anonymisation absolue : un domaine ou un hash peut rester attribuable. " +
        "Une vérification humaine et juridique demeure nécessaire avant partage.";

    private static readonly JsonSerializerOptions JsonOptions =
        CreateJsonOptions();

    /// <inheritdoc />
    public ShareableIocExportResult Export(ShareableIocExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var input = Validate(request);
        var buildResult = BuildShareableIocs(
            input.Incidents,
            input.EvidenceHashes,
            request.ApprovedIocs);
        var sharedIocs = buildResult.Entries;

        if (sharedIocs.Count == 0)
        {
            throw new InvalidOperationException(
                "Aucun domaine ou SHA-256 ne satisfait le profil strict de partage.");
        }

        var generatedOn = DateOnly.FromDateTime(request.PreparedAt.UtcDateTime);
        var artifacts = new[]
        {
            new ShareableIocArtifact(
                "LISEZ-MOI.md",
                MarkdownContentType,
                WriteReadme(request.ExportId, generatedOn, sharedIocs.Count)),
            new ShareableIocArtifact(
                "iocs-partage.json",
                JsonContentType,
                WriteJson(request.ExportId, generatedOn, sharedIocs)),
            new ShareableIocArtifact(
                "iocs-partage.csv",
                CsvContentType,
                WriteCsv(sharedIocs)),
        };
        var package = new ShareableIocPackage(
            request.ExportId,
            generatedOn,
            artifacts);
        var inputIocCount = input.Incidents.Sum(incident => incident.Iocs.Count);
        var audit = new ShareableIocLocalAudit(
            request.ExportId,
            request.PreparedAt,
            input.Sources,
            artifacts.Select(artifact => new ShareableIocArtifactDigest(
                artifact.FileName,
                ComputeSha256(artifact.Content))).ToArray(),
            inputIocCount,
            sharedIocs.Count,
            inputIocCount - buildResult.AcceptedObservationCount);

        return new ShareableIocExportResult(package, audit);
    }

    private static ValidatedInput Validate(ShareableIocExportRequest request)
    {
        var incidentById = request.Incidents.ToDictionary(
            incident => incident.IncidentId);
        var reviewByIncidentId = request.IncidentReviews.ToDictionary(
            review => review.IncidentId);

        if (!incidentById.Keys.ToHashSet().SetEquals(reviewByIncidentId.Keys))
        {
            throw new InvalidOperationException(
                "Chaque incident sélectionné doit posséder exactement une décision humaine.");
        }

        var sources = new List<ShareableIocAuditSource>(incidentById.Count);
        var evidenceHashes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var incident in incidentById.Values.OrderBy(incident => incident.IncidentId))
        {
            var review = reviewByIncidentId[incident.IncidentId];
            if (review.Verdict != ReviewVerdict.ConfirmedFraud ||
                review.Classification is null or
                    FraudClassification.Unknown or
                    FraudClassification.Suspicious)
            {
                throw new InvalidOperationException(
                    $"L'incident '{incident.IncidentId:D}' doit être confirmé comme fraude et catégorisé.");
            }

            if (review.DecidedAt > request.PreparedAt)
            {
                throw new InvalidOperationException(
                    $"L'export ne peut pas précéder la revue de l'incident '{incident.IncidentId:D}'.");
            }

            var evidenceHash = NormalizeSha256(incident.Evidence.Sha256);
            if (evidenceHash is null)
            {
                throw new InvalidOperationException(
                    $"L'incident '{incident.IncidentId:D}' ne possède pas de SHA-256 de preuve exploitable.");
            }

            evidenceHashes.Add(evidenceHash);
            sources.Add(new ShareableIocAuditSource(
                incident.IncidentId,
                evidenceHash,
                review.ReviewId,
                review.Classification.Value));
        }

        return new ValidatedInput(
            incidentById.Values.OrderBy(incident => incident.IncidentId).ToArray(),
            sources,
            evidenceHashes);
    }

    private static ShareableIocBuildResult BuildShareableIocs(
        IReadOnlyList<FraudIncident> incidents,
        IReadOnlySet<string> evidenceHashes,
        IReadOnlyList<ShareableIocSelection> approvedIocs)
    {
        var eligibleEntries = new Dictionary<string, ShareableIocBuilder>(StringComparer.Ordinal);

        foreach (var incident in incidents)
        {
            foreach (var ioc in incident.Iocs)
            {
                if (ioc is null ||
                    !double.IsFinite(ioc.Confidence) ||
                    ioc.Confidence < MinimumIocConfidence ||
                    ioc.Confidence > 1)
                {
                    continue;
                }

                var normalized = ioc.Type switch
                {
                    IocType.Domain => NormalizeDomain(ioc.Value),
                    IocType.Hash => NormalizeSha256(ioc.Value),
                    _ => null,
                };

                if (normalized is null ||
                    (ioc.Type == IocType.Hash && evidenceHashes.Contains(normalized)))
                {
                    continue;
                }

                var key = $"{(int)ioc.Type}\0{normalized}";
                if (!eligibleEntries.TryGetValue(key, out var builder))
                {
                    builder = new ShareableIocBuilder(ioc.Type, normalized);
                    eligibleEntries.Add(key, builder);
                }

                builder.IncidentIds.Add(incident.IncidentId);
                builder.MinimumConfidence = Math.Min(
                    builder.MinimumConfidence,
                    ioc.Confidence);
            }
        }

        var approvedEntries = new List<ShareableIocBuilder>(approvedIocs.Count);
        var acceptedObservationCount = 0;
        var approvedKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var approvedIoc in approvedIocs)
        {
            var normalized = approvedIoc.Type switch
            {
                IocType.Domain => NormalizeDomain(approvedIoc.Value),
                IocType.Hash => NormalizeSha256(approvedIoc.Value),
                _ => null,
            };

            if (normalized is null)
            {
                throw new InvalidOperationException(
                    $"L'IOC sélectionné '{approvedIoc.Value}' est invalide pour le profil strict.");
            }

            if (approvedIoc.Type == IocType.Hash && evidenceHashes.Contains(normalized))
            {
                throw new InvalidOperationException(
                    "Une empreinte de preuve source ne peut jamais être sélectionnée pour le partage.");
            }

            var key = $"{(int)approvedIoc.Type}\0{normalized}";
            if (!approvedKeys.Add(key))
            {
                throw new InvalidOperationException(
                    $"L'IOC sélectionné '{normalized}' est présent plusieurs fois après normalisation.");
            }

            if (!eligibleEntries.TryGetValue(key, out var entry))
            {
                throw new InvalidOperationException(
                    $"L'IOC sélectionné '{normalized}' n'est pas une observation éligible des incidents validés.");
            }

            approvedEntries.Add(entry);
            acceptedObservationCount += entry.IncidentIds.Count;
        }

        var sharedEntries = approvedEntries
            .Select(builder => new ShareableIocEntry(
                builder.Type,
                builder.Value,
                ConfidenceLabel(builder.MinimumConfidence),
                builder.IncidentIds.Count))
            .OrderBy(entry => entry.Type)
            .ThenBy(entry => entry.Value, StringComparer.Ordinal)
            .ToArray();

        return new ShareableIocBuildResult(
            sharedEntries,
            acceptedObservationCount);
    }

    private static string WriteReadme(
        Guid exportId,
        DateOnly generatedOn,
        int iocCount)
    {
        var output = new StringBuilder();
        output.AppendLine("# Export d'IOC à partage contrôlé");
        output.AppendLine();
        output.AppendLine("> Paquet préparé localement. Frelon ne l'a publié ni transmis.");
        output.AppendLine();
        output.AppendLine($"- **Identifiant de l'export** : `{exportId:D}`");
        output.AppendLine($"- **Date UTC** : {generatedOn:yyyy-MM-dd}");
        output.AppendLine($"- **Profil** : {PrivacyProfile}");
        output.AppendLine($"- **IOC agrégés** : {iocCount.ToString(CultureInfo.InvariantCulture)}");
        output.AppendLine("- **Sélection** : valeurs approuvées explicitement par l'analyste");
        output.AppendLine();
        output.AppendLine("## Données volontairement absentes");
        output.AppendLine();
        output.AppendLine("- identifiants d'incident et de revue ;");
        output.AppendLine("- noms et empreintes des preuves sources ;");
        output.AppendLine("- identités mail, adresses email, adresses IP et noms de fichiers ;");
        output.AppendLine("- URL complètes, sources internes, notes et horodatages précis.");
        output.AppendLine();
        output.AppendLine("## Limite importante");
        output.AppendLine();
        output.AppendLine(PrivacyNotice);
        output.AppendLine();
        output.AppendLine("Relire `iocs-partage.json` ou `iocs-partage.csv`, vérifier le destinataire, " +
            "la base légale et la nécessité de chaque valeur avant toute publication.");
        return output.ToString();
    }

    private static string WriteJson(
        Guid exportId,
        DateOnly generatedOn,
        IReadOnlyList<ShareableIocEntry> iocs)
    {
        var document = new ShareableIocJsonDocument(
            SchemaVersion,
            exportId,
            generatedOn,
            PrivacyProfile,
            PrivacyNotice,
            iocs);
        return JsonSerializer.Serialize(document, JsonOptions);
    }

    private static string WriteCsv(IReadOnlyList<ShareableIocEntry> iocs)
    {
        var output = new StringBuilder(
            "type,value,observationConfidence,occurrenceCount\r\n");

        foreach (var ioc in iocs)
        {
            AppendCsvCell(output, ioc.Type.ToString());
            output.Append(',');
            AppendCsvCell(output, ioc.Value);
            output.Append(',');
            AppendCsvCell(output, ioc.ObservationConfidence);
            output.Append(',');
            output.Append(ioc.OccurrenceCount.ToString(CultureInfo.InvariantCulture));
            output.Append("\r\n");
        }

        return output.ToString();
    }

    private static string? NormalizeDomain(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim().TrimEnd('.');
        if (!trimmed.Contains('.', StringComparison.Ordinal) ||
            trimmed.Any(char.IsWhiteSpace))
        {
            return null;
        }

        try
        {
            var ascii = new IdnMapping().GetAscii(trimmed);
            return Uri.CheckHostName(ascii) == UriHostNameType.Dns
                ? ascii.ToLowerInvariant()
                : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string? NormalizeSha256(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized.Length == 64 && normalized.All(Uri.IsHexDigit)
            ? normalized
            : null;
    }

    private static string ConfidenceLabel(double confidence)
        => confidence >= 0.8 ? "High" : "Medium";

    private static string ComputeSha256(string content)
        => Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    private static void AppendCsvCell(StringBuilder output, string value)
    {
        var safeValue = ProtectFromSpreadsheetFormula(value);
        if (safeValue.IndexOfAny([',', '"', '\r', '\n']) < 0)
        {
            output.Append(safeValue);
            return;
        }

        output.Append('"');
        output.Append(safeValue.Replace("\"", "\"\"", StringComparison.Ordinal));
        output.Append('"');
    }

    private static string ProtectFromSpreadsheetFormula(string value)
    {
        var trimmed = value.AsSpan().TrimStart();
        return !trimmed.IsEmpty && trimmed[0] is '=' or '+' or '-' or '@'
            ? $"'{value}"
            : value;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(
            namingPolicy: null,
            allowIntegerValues: false));
        return options;
    }

    private sealed record ValidatedInput(
        IReadOnlyList<FraudIncident> Incidents,
        IReadOnlyList<ShareableIocAuditSource> Sources,
        IReadOnlySet<string> EvidenceHashes);

    private sealed record ShareableIocBuildResult(
        IReadOnlyList<ShareableIocEntry> Entries,
        int AcceptedObservationCount);

    private sealed record ShareableIocEntry(
        IocType Type,
        string Value,
        string ObservationConfidence,
        int OccurrenceCount);

    private sealed class ShareableIocBuilder(IocType type, string value)
    {
        public IocType Type { get; } = type;

        public string Value { get; } = value;

        public double MinimumConfidence { get; set; } = 1;

        public HashSet<Guid> IncidentIds { get; } = [];
    }

    private sealed record ShareableIocJsonDocument(
        int SchemaVersion,
        Guid ExportId,
        DateOnly GeneratedOn,
        string PrivacyProfile,
        string PrivacyNotice,
        IReadOnlyList<ShareableIocEntry> Iocs);
}
