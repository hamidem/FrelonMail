using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Frelon.Core;
using Microsoft.Data.Sqlite;

namespace Frelon.Storage;

/// <summary>
/// Persiste les incidents dans une base SQLite locale.
/// </summary>
public sealed class SqliteIncidentStore :
    IIncidentStore,
    IIncidentReviewStore,
    ICampaignReviewStore
{
    /// <summary>Version courante du schéma interne de stockage.</summary>
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly string _connectionString;

    /// <summary>Crée un store SQLite à partir d'une chaîne de connexion.</summary>
    public SqliteIncidentStore(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("La chaîne de connexion ne peut pas être vide.", nameof(connectionString));
        }

        _connectionString = connectionString;
    }

    /// <summary>
    /// Crée un store pour un fichier SQLite local sans exposer la syntaxe des chaînes de connexion.
    /// </summary>
    /// <param name="databasePath">Chemin absolu ou relatif du fichier de base de données.</param>
    /// <returns>Un store configuré pour créer ou ouvrir le fichier demandé.</returns>
    public static SqliteIncidentStore FromFile(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Le chemin de la base de données ne peut pas être vide.", nameof(databasePath));
        }

        var fullPath = Path.GetFullPath(databasePath);
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private
        }.ToString();

        return new SqliteIncidentStore(connectionString);
    }

    /// <inheritdoc />
    public async Task InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        using var command = connection.CreateCommand();
        command.CommandText = """
CREATE TABLE IF NOT EXISTS incidents
(
    incident_id      TEXT PRIMARY KEY NOT NULL,
    schema_version   INTEGER NOT NULL,
    created_at       TEXT NOT NULL,
    imported_at      TEXT NOT NULL,
    source_file_name TEXT NOT NULL,
    risk_value       REAL NOT NULL,
    risk_level       TEXT NOT NULL,
    classification   TEXT NOT NULL,
    payload_json     TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS incident_reviews
(
    review_id      TEXT PRIMARY KEY NOT NULL,
    incident_id    TEXT NOT NULL,
    verdict        TEXT NOT NULL,
    classification TEXT NULL,
    decided_at     TEXT NOT NULL,
    notes          TEXT NULL,
    FOREIGN KEY (incident_id) REFERENCES incidents (incident_id)
);

CREATE INDEX IF NOT EXISTS ix_incident_reviews_latest
ON incident_reviews (incident_id, decided_at DESC, review_id ASC);

CREATE TABLE IF NOT EXISTS campaign_reviews
(
    review_id             TEXT PRIMARY KEY NOT NULL,
    candidate_fingerprint TEXT NOT NULL,
    verdict               TEXT NOT NULL,
    decided_at            TEXT NOT NULL,
    notes                 TEXT NULL,
    candidate_json        TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_campaign_reviews_latest
ON campaign_reviews (candidate_fingerprint, decided_at DESC, review_id ASC);
""";

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SaveAsync(
        FraudIncident incident,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(incident);

        var incidentId = incident.IncidentId.ToString("D");
        var snapshotJson = JsonSerializer.Serialize(incident, JsonOptions);
        var createdAt = incident.CreatedAt.ToString("O");
        var importedAt = GetRequiredTimestamp(incident.Evidence.ImportedAt, nameof(incident.Evidence.ImportedAt));
        var sourceFileName = incident.Evidence.FileName;
        var riskValue = incident.RiskScore.Value;
        var riskLevel = incident.RiskScore.Level.ToString();
        var classification = incident.Classification.ToString();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        using var command = connection.CreateCommand();
        command.CommandText = """
INSERT INTO incidents
(
    incident_id,
    schema_version,
    created_at,
    imported_at,
    source_file_name,
    risk_value,
    risk_level,
    classification,
    payload_json
)
VALUES
(
    $incidentId,
    $schemaVersion,
    $createdAt,
    $importedAt,
    $sourceFileName,
    $riskValue,
    $riskLevel,
    $classification,
    $payloadJson
);
""";
        command.Parameters.AddWithValue("$incidentId", incidentId);
        command.Parameters.AddWithValue("$schemaVersion", CurrentSchemaVersion);
        command.Parameters.AddWithValue("$createdAt", createdAt);
        command.Parameters.AddWithValue("$importedAt", importedAt);
        command.Parameters.AddWithValue("$sourceFileName", sourceFileName);
        command.Parameters.AddWithValue("$riskValue", riskValue);
        command.Parameters.AddWithValue("$riskLevel", riskLevel);
        command.Parameters.AddWithValue("$classification", classification);
        command.Parameters.AddWithValue("$payloadJson", snapshotJson);

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqliteException ex) when (IsDuplicateKeyViolation(ex))
        {
            throw new InvalidOperationException($"Un incident avec l'identifiant '{incident.IncidentId}' existe déjà.", ex);
        }
    }

    /// <inheritdoc />
    public async Task<FraudIncident?> GetByIdAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        using var command = connection.CreateCommand();
        command.CommandText = """
SELECT schema_version, payload_json
FROM incidents
WHERE incident_id = $incidentId;
""";
        command.Parameters.AddWithValue("$incidentId", incidentId.ToString("D"));

        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var schemaVersion = reader.GetInt32(0);
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new NotSupportedException($"Version de schéma de stockage non supportée : {schemaVersion}.");
        }

        var payloadJson = reader.GetString(1);
        var incident = JsonSerializer.Deserialize<FraudIncident>(payloadJson, JsonOptions);
        if (incident is null)
        {
            throw new InvalidDataException($"Le snapshot de l'incident '{incidentId}' est invalide.");
        }

        if (incident.IncidentId != incidentId)
        {
            throw new InvalidDataException($"L'identifiant du snapshot ne correspond pas à l'identifiant stocké '{incidentId}'.");
        }

        return incident;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IncidentSummary>> ListRecentAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "La limite doit être comprise entre 1 et 500.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        using var command = connection.CreateCommand();
        command.CommandText = """
SELECT incident.incident_id,
       incident.created_at,
       incident.imported_at,
       incident.source_file_name,
       incident.risk_value,
       incident.risk_level,
       incident.classification,
       latest_review.verdict,
       latest_review.classification,
       latest_review.decided_at
FROM incidents AS incident
LEFT JOIN incident_reviews AS latest_review
       ON latest_review.review_id =
          (
              SELECT review.review_id
              FROM incident_reviews AS review
              WHERE review.incident_id = incident.incident_id
              ORDER BY review.decided_at DESC, review.review_id ASC
              LIMIT 1
          )
ORDER BY incident.created_at DESC, incident.incident_id ASC
LIMIT $limit;
""";
        command.Parameters.AddWithValue("$limit", limit);

        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var incidents = new List<IncidentSummary>();

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            ReviewVerdict? latestReviewVerdict = reader.IsDBNull(7)
                ? null
                : ParseEnum<ReviewVerdict>(reader, 7, "verdict");
            FraudClassification? latestReviewClassification = reader.IsDBNull(8)
                ? null
                : ParseEnum<FraudClassification>(reader, 8, "review_classification");
            DateTimeOffset? latestReviewAt = reader.IsDBNull(9)
                ? null
                : ParseDateTimeOffset(reader, 9, "decided_at");

            if ((latestReviewVerdict is null) != (latestReviewAt is null))
            {
                throw InvalidColumnData("latest_review");
            }

            incidents.Add(new IncidentSummary
            {
                IncidentId = ParseGuid(reader, 0, "incident_id"),
                CreatedAt = ParseDateTimeOffset(reader, 1, "created_at"),
                ImportedAt = ParseDateTimeOffset(reader, 2, "imported_at"),
                SourceFileName = ReadString(reader, 3, "source_file_name"),
                RiskValue = ReadDouble(reader, 4, "risk_value"),
                RiskLevel = ParseEnum<RiskLevel>(reader, 5, "risk_level"),
                Classification = ParseEnum<FraudClassification>(reader, 6, "classification"),
                LatestReviewVerdict = latestReviewVerdict,
                LatestReviewClassification = latestReviewClassification,
                LatestReviewAt = latestReviewAt
            });
        }

        return incidents;
    }

    /// <inheritdoc />
    public async Task SaveReviewAsync(
        IncidentReviewDecision decision,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(decision);

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        using var command = connection.CreateCommand();
        command.CommandText = """
INSERT INTO incident_reviews
(
    review_id,
    incident_id,
    verdict,
    classification,
    decided_at,
    notes
)
SELECT $reviewId,
       $incidentId,
       $verdict,
       $classification,
       $decidedAt,
       $notes
WHERE EXISTS
(
    SELECT 1
    FROM incidents
    WHERE incident_id = $incidentId
);
""";
        command.Parameters.AddWithValue("$reviewId", decision.ReviewId.ToString("D"));
        command.Parameters.AddWithValue("$incidentId", decision.IncidentId.ToString("D"));
        command.Parameters.AddWithValue("$verdict", decision.Verdict.ToString());
        command.Parameters.AddWithValue(
            "$classification",
            decision.Classification?.ToString() ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$decidedAt", decision.DecidedAt.ToString("O"));
        command.Parameters.AddWithValue("$notes", decision.Notes ?? (object)DBNull.Value);

        try
        {
            var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            if (affectedRows == 0)
            {
                throw new InvalidOperationException(
                    $"L'incident '{decision.IncidentId}' n'existe pas dans le stockage local.");
            }
        }
        catch (SqliteException ex) when (IsDuplicateKeyViolation(ex))
        {
            throw new InvalidOperationException(
                $"Une décision avec l'identifiant '{decision.ReviewId}' existe déjà.",
                ex);
        }
    }

    /// <inheritdoc />
    public async Task<IncidentReviewDecision?> GetLatestReviewAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default)
    {
        var reviews = await ListReviewsAsync(incidentId, 1, cancellationToken).ConfigureAwait(false);
        return reviews.Count == 0 ? null : reviews[0];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IncidentReviewDecision>> ListReviewsAsync(
        Guid incidentId,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (incidentId == Guid.Empty)
        {
            throw new ArgumentException("L'identifiant d'incident ne peut pas être vide.", nameof(incidentId));
        }

        if (limit is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "La limite doit être comprise entre 1 et 500.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        using var command = connection.CreateCommand();
        command.CommandText = """
SELECT review_id,
       incident_id,
       verdict,
       classification,
       decided_at,
       notes
FROM incident_reviews
WHERE incident_id = $incidentId
ORDER BY decided_at DESC, review_id ASC
LIMIT $limit;
""";
        command.Parameters.AddWithValue("$incidentId", incidentId.ToString("D"));
        command.Parameters.AddWithValue("$limit", limit);

        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var reviews = new List<IncidentReviewDecision>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            FraudClassification? classification = reader.IsDBNull(3)
                ? null
                : ParseEnum<FraudClassification>(reader, 3, "classification");
            var notes = reader.IsDBNull(5) ? null : ReadString(reader, 5, "notes");

            reviews.Add(new IncidentReviewDecision(
                ParseGuid(reader, 0, "review_id"),
                ParseGuid(reader, 1, "incident_id"),
                ParseEnum<ReviewVerdict>(reader, 2, "verdict"),
                classification,
                ParseDateTimeOffset(reader, 4, "decided_at"),
                notes));
        }

        return reviews;
    }

    /// <inheritdoc />
    public async Task SaveCampaignReviewAsync(
        CampaignReviewDecision decision,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(decision);

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        using var command = connection.CreateCommand();
        var incidentParameters = decision.CandidateSnapshot.IncidentIds
            .Select((_, index) => $"$incidentId{index}")
            .ToArray();
        command.CommandText = $"""
INSERT INTO campaign_reviews
(
    review_id,
    candidate_fingerprint,
    verdict,
    decided_at,
    notes,
    candidate_json
)
SELECT $reviewId,
       $candidateFingerprint,
       $verdict,
       $decidedAt,
       $notes,
       $candidateJson
WHERE
(
    SELECT COUNT(*)
    FROM incidents
    WHERE incident_id IN ({string.Join(", ", incidentParameters)})
) = $incidentCount;
""";
        command.Parameters.AddWithValue("$reviewId", decision.ReviewId.ToString("D"));
        command.Parameters.AddWithValue("$candidateFingerprint", decision.CandidateFingerprint);
        command.Parameters.AddWithValue("$verdict", decision.Verdict.ToString());
        command.Parameters.AddWithValue("$decidedAt", decision.DecidedAt.ToString("O"));
        command.Parameters.AddWithValue("$notes", decision.Notes ?? (object)DBNull.Value);
        command.Parameters.AddWithValue(
            "$candidateJson",
            JsonSerializer.Serialize(decision.CandidateSnapshot, JsonOptions));
        command.Parameters.AddWithValue(
            "$incidentCount",
            decision.CandidateSnapshot.IncidentIds.Count);

        for (var index = 0; index < incidentParameters.Length; index++)
        {
            command.Parameters.AddWithValue(
                incidentParameters[index],
                decision.CandidateSnapshot.IncidentIds[index].ToString("D"));
        }

        try
        {
            var affectedRows = await command
                .ExecuteNonQueryAsync(cancellationToken)
                .ConfigureAwait(false);
            if (affectedRows == 0)
            {
                throw new InvalidOperationException(
                    "Un ou plusieurs incidents de la campagne candidate " +
                    "n'existent pas dans le stockage local.");
            }
        }
        catch (SqliteException ex) when (IsDuplicateKeyViolation(ex))
        {
            throw new InvalidOperationException(
                $"Une décision avec l'identifiant '{decision.ReviewId}' existe déjà.",
                ex);
        }
    }

    /// <inheritdoc />
    public async Task<CampaignReviewDecision?> GetLatestCampaignReviewAsync(
        string candidateFingerprint,
        CancellationToken cancellationToken = default)
    {
        var reviews = await ListCampaignReviewsAsync(
                candidateFingerprint,
                1,
                cancellationToken)
            .ConfigureAwait(false);
        return reviews.Count == 0 ? null : reviews[0];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CampaignReviewDecision>> ListCampaignReviewsAsync(
        string candidateFingerprint,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var normalizedFingerprint = NormalizeCandidateFingerprint(candidateFingerprint);

        if (limit is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                limit,
                "La limite doit être comprise entre 1 et 500.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        using var command = connection.CreateCommand();
        command.CommandText = """
SELECT review_id,
       candidate_fingerprint,
       candidate_json,
       verdict,
       decided_at,
       notes
FROM campaign_reviews
WHERE candidate_fingerprint = $candidateFingerprint
ORDER BY decided_at DESC, review_id ASC
LIMIT $limit;
""";
        command.Parameters.AddWithValue("$candidateFingerprint", normalizedFingerprint);
        command.Parameters.AddWithValue("$limit", limit);

        using var reader = await command
            .ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);
        var reviews = new List<CampaignReviewDecision>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var storedFingerprint = ReadString(reader, 1, "candidate_fingerprint");
            var candidate = DeserializeCampaignCandidate(
                ReadString(reader, 2, "candidate_json"));

            if (!string.Equals(
                    storedFingerprint,
                    candidate.Fingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    normalizedFingerprint,
                    candidate.Fingerprint,
                    StringComparison.Ordinal))
            {
                throw InvalidColumnData("candidate_json");
            }

            try
            {
                reviews.Add(new CampaignReviewDecision(
                    ParseGuid(reader, 0, "review_id"),
                    candidate,
                    ParseEnum<CampaignReviewVerdict>(reader, 3, "verdict"),
                    ParseDateTimeOffset(reader, 4, "decided_at"),
                    reader.IsDBNull(5) ? null : ReadString(reader, 5, "notes")));
            }
            catch (ArgumentException exception)
            {
                throw InvalidColumnData("campaign_review", exception);
            }
        }

        return reviews;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static string GetRequiredTimestamp(DateTimeOffset? value, string parameterName)
    {
        return value?.ToString("O") ?? throw new InvalidOperationException($"La date '{parameterName}' est manquante.");
    }

    private static bool IsDuplicateKeyViolation(SqliteException exception)
    {
        return exception.SqliteErrorCode == 19
            && (exception.SqliteExtendedErrorCode == 1555 || exception.SqliteExtendedErrorCode == 2067);
    }

    private static string NormalizeCandidateFingerprint(string candidateFingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateFingerprint);

        var normalized = candidateFingerprint.Trim().ToLowerInvariant();
        if (!CampaignCandidate.IsValidFingerprint(normalized))
        {
            throw new ArgumentException(
                "L'empreinte de campagne candidate doit être une valeur SHA-256 hexadécimale.",
                nameof(candidateFingerprint));
        }

        return normalized;
    }

    private static CampaignCandidate DeserializeCampaignCandidate(string candidateJson)
    {
        try
        {
            return JsonSerializer.Deserialize<CampaignCandidate>(candidateJson, JsonOptions)
                ?? throw InvalidColumnData("candidate_json");
        }
        catch (Exception exception) when (
            exception is JsonException or NotSupportedException or ArgumentException)
        {
            throw InvalidColumnData("candidate_json", exception);
        }
    }

    private static Guid ParseGuid(SqliteDataReader reader, int ordinal, string columnName)
    {
        var value = ReadString(reader, ordinal, columnName);
        if (Guid.TryParseExact(value, "D", out var result))
        {
            return result;
        }

        throw InvalidColumnData(columnName);
    }

    private static DateTimeOffset ParseDateTimeOffset(SqliteDataReader reader, int ordinal, string columnName)
    {
        var value = ReadString(reader, ordinal, columnName);
        if (DateTimeOffset.TryParseExact(
                value,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var result))
        {
            return result;
        }

        throw InvalidColumnData(columnName);
    }

    private static TEnum ParseEnum<TEnum>(SqliteDataReader reader, int ordinal, string columnName)
        where TEnum : struct, Enum
    {
        var value = ReadString(reader, ordinal, columnName);
        if (Enum.TryParse<TEnum>(value, ignoreCase: false, out var result)
            && string.Equals(Enum.GetName(result), value, StringComparison.Ordinal))
        {
            return result;
        }

        throw InvalidColumnData(columnName);
    }

    private static string ReadString(SqliteDataReader reader, int ordinal, string columnName)
    {
        try
        {
            return reader.GetString(ordinal);
        }
        catch (Exception exception) when (exception is InvalidCastException or SqliteException)
        {
            throw InvalidColumnData(columnName, exception);
        }
    }

    private static double ReadDouble(SqliteDataReader reader, int ordinal, string columnName)
    {
        try
        {
            return reader.GetDouble(ordinal);
        }
        catch (Exception exception) when (exception is InvalidCastException or SqliteException)
        {
            throw InvalidColumnData(columnName, exception);
        }
    }

    private static InvalidDataException InvalidColumnData(string columnName, Exception? innerException = null)
    {
        return new InvalidDataException($"La colonne '{columnName}' contient une valeur invalide.", innerException);
    }
}
