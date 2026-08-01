using System.Text.Json;
using Frelon.Core;
using Frelon.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Frelon.Reports.Tests;

/// <summary>
/// Tests de <see cref="SystemTextJsonIncidentJsonWriter"/>.
/// </summary>
public class SystemTextJsonIncidentJsonWriterTests
{
    private static FraudIncident BuildIncident(IReadOnlyList<string> reasons) => new()
    {
        IncidentId     = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        CreatedAt      = new DateTimeOffset(2026, 7, 3, 12, 0, 0, TimeSpan.Zero),
        Evidence       = new EvidenceSource
        {
            FileName   = "suspicious.eml",
            ImportedAt = new DateTimeOffset(2026, 7, 3, 12, 0, 0, TimeSpan.Zero),
        },
        Identity       = new MailIdentity
        {
            From    = "Fake Support <support@example.net>",
            Subject = "Suspicious login attempt",
        },
        Authentication = new AuthenticationAssessment
        {
            AuthenticationResultsRaw = "spf=pass; dkim=fail; dmarc=none",
            SpfResult                = "pass",
            DkimResult               = "fail",
            DmarcResult              = "none",
        },
        Classification = FraudClassification.Unknown,
        RiskScore      = new RiskScore
        {
            Value = 60,
            Level = RiskLevel.High,
            Reasons = reasons,
        },
    };

    private static readonly SystemTextJsonIncidentJsonWriter Writer = new();

    [Fact]
    public void Write_RetourneUneChainJsonNonVide()
    {
        string json = Writer.Write(BuildIncident(
            [
                "Échec d'authentification SPF",
                "Échec d'authentification DKIM",
                "Échec d'authentification DMARC",
            ]));

        Assert.False(string.IsNullOrWhiteSpace(json));
    }

    [Fact]
    public void Write_ExposeLeContratCompletDesRaisonsDuScore()
    {
        string json = Writer.Write(BuildIncident(
            [
                "Échec d'authentification SPF",
                "Échec d'authentification DKIM",
                "Échec d'authentification DMARC",
            ]));

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement riskScore = document.RootElement.GetProperty("riskScore");

        Assert.Equal(60, riskScore.GetProperty("value").GetInt32());
        JsonElement level = riskScore.GetProperty("level");
        Assert.Equal(JsonValueKind.Number, level.ValueKind);
        Assert.Equal(3, level.GetInt32());

        JsonElement reasons = riskScore.GetProperty("reasons");
        Assert.Equal(JsonValueKind.Array, reasons.ValueKind);
        Assert.Equal(3, reasons.GetArrayLength());

        var enumerator = reasons.EnumerateArray();
        Assert.True(enumerator.MoveNext());
        Assert.Equal("Échec d'authentification SPF", enumerator.Current.GetString());
        Assert.True(enumerator.MoveNext());
        Assert.Equal("Échec d'authentification DKIM", enumerator.Current.GetString());
        Assert.True(enumerator.MoveNext());
        Assert.Equal("Échec d'authentification DMARC", enumerator.Current.GetString());
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void Write_ExposeUnTableauVideQuandAucuneRaisonEstPresente()
    {
        string json = Writer.Write(BuildIncident(Array.Empty<string>()));

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement reasons = document.RootElement.GetProperty("riskScore").GetProperty("reasons");

        Assert.Equal(JsonValueKind.Array, reasons.ValueKind);
        Assert.Equal(0, reasons.GetArrayLength());
    }

    [Fact]
    public void Write_ContientIncidentId()
    {
        string json = Writer.Write(BuildIncident(Array.Empty<string>()));

        using JsonDocument document = JsonDocument.Parse(json);

        Assert.Equal(
            "11111111-1111-1111-1111-111111111111",
            document.RootElement.GetProperty("incidentId").GetString());
    }

    [Fact]
    public void Write_ContientCreatedAt()
    {
        string json = Writer.Write(BuildIncident(Array.Empty<string>()));

        Assert.Contains("createdAt", json);
    }

    [Fact]
    public void Write_ContientEvidence()
    {
        string json = Writer.Write(BuildIncident(Array.Empty<string>()));

        Assert.Contains("evidence", json);
        Assert.Contains("suspicious.eml", json);
    }

    [Fact]
    public void Write_ContientIdentity()
    {
        string json = Writer.Write(BuildIncident(Array.Empty<string>()));

        Assert.Contains("identity", json);
        Assert.Contains("support@example.net", json);
    }

    [Fact]
    public void Write_ContientAuthentication()
    {
        string json = Writer.Write(BuildIncident(Array.Empty<string>()));

        Assert.Contains("authentication", json);
    }

    [Fact]
    public void Write_ContientClassification()
    {
        string json = Writer.Write(BuildIncident(Array.Empty<string>()));

        Assert.Contains("classification", json);
    }

    [Fact]
    public void Write_ContientLaPisteDeClassificationDistincte()
    {
        string json = Writer.Write(BuildIncident(Array.Empty<string>()));

        Assert.Contains("classificationAssessment", json);
        Assert.Contains("confidence", json);
        Assert.Contains("reasons", json);
    }

    [Fact]
    public void Write_ContientRiskScore()
    {
        string json = Writer.Write(BuildIncident(Array.Empty<string>()));

        Assert.Contains("riskScore", json);
    }

    [Fact]
    public void Write_ProduiteUnJsonIndente()
    {
        string json = Writer.Write(BuildIncident(Array.Empty<string>()));

        Assert.Contains('\n', json);
    }

    [Fact]
    public void Write_LeveArgumentNullExceptionSiIncidentEstNull()
    {
        Assert.Throws<ArgumentNullException>(() => Writer.Write(null!));
    }

    [Fact]
    public async Task InitializeAsync_CreeLaTableIncidents_AvecIncidentIdClePrimaire()
    {
        using var database = new TemporarySqliteDatabase();
        var store = new SqliteIncidentStore(database.ConnectionString);

        await store.InitializeAsync(TestContext.Current.CancellationToken);

        using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(incidents);";

        using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var columns = new List<(string Name, string Type, int NotNull, int PrimaryKey)>();

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            columns.Add((
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetInt32(5)));
        }

        Assert.Equal(9, columns.Count);
        Assert.Equal(("incident_id", "TEXT", 1, 1), columns[0]);
        Assert.Equal(("schema_version", "INTEGER", 1, 0), columns[1]);
        Assert.Equal(("created_at", "TEXT", 1, 0), columns[2]);
        Assert.Equal(("imported_at", "TEXT", 1, 0), columns[3]);
        Assert.Equal(("source_file_name", "TEXT", 1, 0), columns[4]);
        Assert.Equal(("risk_value", "REAL", 1, 0), columns[5]);
        Assert.Equal(("risk_level", "TEXT", 1, 0), columns[6]);
        Assert.Equal(("classification", "TEXT", 1, 0), columns[7]);
        Assert.Equal(("payload_json", "TEXT", 1, 0), columns[8]);
    }

    [Fact]
    public async Task SaveAsync_EtGetByIdAsync_ConserventEvidence_FilePath()
    {
        using var database = new TemporarySqliteDatabase();
        var store = new SqliteIncidentStore(database.ConnectionString);
        var incident = BuildIncident();

        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.SaveAsync(incident, TestContext.Current.CancellationToken);

        FraudIncident? roundTrip = await store.GetByIdAsync(incident.IncidentId, TestContext.Current.CancellationToken);

        Assert.NotNull(roundTrip);
        Assert.Equal(incident.Evidence.FilePath, roundTrip!.Evidence.FilePath);
    }

    [Fact]
    public async Task SaveAsync_EtGetByIdAsync_ConserventEvidence_Sha256()
    {
        using var database = new TemporarySqliteDatabase();
        var store = new SqliteIncidentStore(database.ConnectionString);
        var incident = BuildIncident();

        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.SaveAsync(incident, TestContext.Current.CancellationToken);

        FraudIncident? roundTrip = await store.GetByIdAsync(incident.IncidentId, TestContext.Current.CancellationToken);

        Assert.NotNull(roundTrip);
        Assert.Equal(incident.Evidence.Sha256, roundTrip!.Evidence.Sha256);
    }

    private static FraudIncident BuildIncident() => new()
    {
        IncidentId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        CreatedAt = new DateTimeOffset(2026, 7, 3, 12, 34, 56, TimeSpan.Zero),
        Evidence = new EvidenceSource
        {
            FilePath = @"C:\temp\cases\phishing\invoice.eml",
            FileName = "invoice.eml",
            Sha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            ImportedAt = new DateTimeOffset(2026, 7, 3, 12, 35, 0, TimeSpan.Zero),
        },
        Identity = new MailIdentity
        {
            From = "Acme Support <support@acme.test>",
            ReplyTo = "help@acme.test",
            ReturnPath = "<bounce@mailer.acme.test>",
            MessageId = "<msg-001@acme.test>",
            Subject = "Mise à jour de sécurité",
        },
        Authentication = new AuthenticationAssessment
        {
            AuthenticationResultsRaw = "spf=fail; dkim=pass; dmarc=fail",
            SpfResult = "fail",
            DkimResult = "pass",
            DmarcResult = "fail",
            IsSuspicious = true,
        },
        ReceivedChain =
        [
            new ReceivedHop
            {
                Position = 0,
                From = "mx1.acme.test",
                By = "mx2.acme.test",
                With = "ESMTP",
                IpAddress = "203.0.113.10",
                Timestamp = new DateTimeOffset(2026, 7, 3, 12, 33, 0, TimeSpan.Zero),
                RawValue = "from mx1.acme.test by mx2.acme.test",
            },
        ],
        Urls =
        [
            new UrlIndicator
            {
                RawValue = "https://login.acme.test",
                NormalizedValue = "https://login.acme.test",
                Host = "login.acme.test",
                Scheme = "https",
                IsSuspicious = true,
                Reasons = ["Domaine ressemblant"],
            },
        ],
        Attachments =
        [
            new AttachmentIndicator
            {
                FileName = "invoice.pdf",
                ContentType = "application/pdf",
                SizeBytes = 1234,
                Sha256 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                IsSuspicious = true,
                Reasons = ["Pièce jointe inattendue"],
            },
        ],
        Iocs =
        [
            new Ioc
            {
                Type = IocType.Url,
                Value = "https://login.acme.test",
                Confidence = 0.95,
                Source = "url-extractor",
                FirstSeen = new DateTimeOffset(2026, 7, 3, 12, 33, 0, TimeSpan.Zero),
            },
            new Ioc
            {
                Type = IocType.Domain,
                Value = "acme.test",
                Confidence = 0.80,
                Source = "domain-extractor",
                FirstSeen = new DateTimeOffset(2026, 7, 3, 12, 33, 0, TimeSpan.Zero),
            },
            new Ioc
            {
                Type = IocType.Hash,
                Value = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                Confidence = 1.0,
                Source = "attachment-hash",
                FirstSeen = new DateTimeOffset(2026, 7, 3, 12, 33, 0, TimeSpan.Zero),
            },
        ],
        Classification = FraudClassification.Phishing,
        RiskScore = new RiskScore
        {
            Value = 87.5,
            Level = RiskLevel.Critical,
            Reasons = ["Échec SPF", "URL suspecte", "Pièce jointe suspecte"],
        },
        RecommendedActions =
        [
            new RecommendedAction
            {
                Type = RecommendedActionType.ReviewManually,
                Label = "Revoir manuellement",
                Description = "Vérifier l'incident",
                RequiresHumanValidation = true,
            },
        ],
    };

    private sealed class TemporarySqliteDatabase : IDisposable
    {
        private readonly string _path;

        private bool _disposed;

        public TemporarySqliteDatabase()
        {
            _path = Path.Combine(
                Path.GetTempPath(),
                $"frelon-storage-{Guid.NewGuid():N}.sqlite");

            ConnectionString = new SqliteConnectionStringBuilder
            {
                DataSource = _path,
                Pooling = false
            }.ToString();
        }

        public string ConnectionString { get; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            SqliteConnection.ClearAllPools();

            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }
    }
}
