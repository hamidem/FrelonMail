using System.Text.Json;
using System.Text.Json.Serialization;
using Frelon.Core;
using Microsoft.Data.Sqlite;

namespace Frelon.Storage.Tests;

/// <summary>
/// Tests unitaires de <see cref="SqliteIncidentStore"/>.
/// </summary>
public partial class SqliteIncidentStoreTests
{
    private const string FixedIncidentId = "11111111-2222-3333-4444-555555555555";
    private static readonly Guid FixedIncidentGuid = Guid.Parse(FixedIncidentId);

    private const string DefaultSourceFileName = "facture.pdf";
    private const string HostileSourceFileName = "facture'); DROP TABLE incidents; --.eml";

    private static readonly DateTimeOffset CreatedAt = new(2026, 7, 3, 14, 15, 16, TimeSpan.Zero);
    private static readonly DateTimeOffset ImportedAt = new(2026, 7, 3, 14, 10, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ReceivedHop1At = new(2026, 7, 3, 14, 11, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ReceivedHop2At = new(2026, 7, 3, 14, 12, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Ioc1FirstSeen = new(2026, 7, 3, 14, 10, 5, TimeSpan.Zero);
    private static readonly DateTimeOffset Ioc2FirstSeen = new(2026, 7, 3, 14, 10, 6, TimeSpan.Zero);
    private static readonly DateTimeOffset Ioc3FirstSeen = new(2026, 7, 3, 14, 10, 7, TimeSpan.Zero);

    private const double RiskValue = 87.5;

    private static readonly JsonSerializerOptions SnapshotJsonOptions = CreateSnapshotJsonOptions();

    [Fact]
    public async Task InitializeAsync_CreeLaTableIncidents_AvecSchemaAttendu()
    {
        using var database = TemporarySqliteDatabase.Create();
        var store = new SqliteIncidentStore(database.ConnectionString);

        await store.InitializeAsync(CancellationToken.None);

        Assert.True(await TableExistsAsync(database.ConnectionString));
        Assert.Equal(
            [
                "incident_id",
                "schema_version",
                "created_at",
                "imported_at",
                "source_file_name",
                "risk_value",
                "risk_level",
                "classification",
                "payload_json"
            ],
            await GetTableColumnNamesAsync(database.ConnectionString));
    }

    [Fact]
    public async Task InitializeAsync_PeutEtreAppeleeDeuxFoisSansException()
    {
        using var database = TemporarySqliteDatabase.Create();
        var store = new SqliteIncidentStore(database.ConnectionString);

        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        Assert.True(await TableExistsAsync(database.ConnectionString));
    }

    [Fact]
    public void Constructeur_AvecConnectionStringNull_LeveArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => new SqliteIncidentStore(null!));

        Assert.Equal("connectionString", exception.ParamName);
    }

    [Fact]
    public void Constructeur_AvecConnectionStringVide_LeveArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => new SqliteIncidentStore(string.Empty));

        Assert.Equal("connectionString", exception.ParamName);
    }

    [Fact]
    public void Constructeur_AvecConnectionStringEspaces_LeveArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => new SqliteIncidentStore("   "));

        Assert.Equal("connectionString", exception.ParamName);
    }

    [Fact]
    public async Task SaveAsync_AvecIncidentNull_LeveArgumentNullException()
    {
        using var database = TemporarySqliteDatabase.Create();
        var store = new SqliteIncidentStore(database.ConnectionString);

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => store.SaveAsync(null!, CancellationToken.None));

        Assert.Equal("incident", exception.ParamName);
    }

    [Fact]
    public async Task SaveAsync_SansInitializeAsync_NeCreePasLeSchemaImplicitement()
    {
        using var database = TemporarySqliteDatabase.Create();
        var store = new SqliteIncidentStore(database.ConnectionString);

        await Assert.ThrowsAsync<SqliteException>(
            () => store.SaveAsync(BuildRichIncident(), CancellationToken.None));

        Assert.False(await TableExistsAsync(database.ConnectionString));
    }

    [Fact]
    public async Task GetByIdAsync_SansInitializeAsync_NeCreePasLeSchemaImplicitement()
    {
        using var database = TemporarySqliteDatabase.Create();
        var store = new SqliteIncidentStore(database.ConnectionString);

        await Assert.ThrowsAsync<SqliteException>(
            () => store.GetByIdAsync(FixedIncidentGuid, CancellationToken.None));

        Assert.False(await TableExistsAsync(database.ConnectionString));
    }

    [Fact]
    public async Task InitializeAsync_AvecCancellationTokenAnnule_LeveOperationCanceledException()
    {
        using var database = TemporarySqliteDatabase.Create();
        var store = new SqliteIncidentStore(database.ConnectionString);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => store.InitializeAsync(cts.Token));
    }

    [Fact]
    public async Task SaveAsync_AvecCancellationTokenAnnule_LeveOperationCanceledException()
    {
        using var database = TemporarySqliteDatabase.Create();
        var store = new SqliteIncidentStore(database.ConnectionString);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => store.SaveAsync(BuildRichIncident(), cts.Token));
    }

    [Fact]
    public async Task GetByIdAsync_AvecCancellationTokenAnnule_LeveOperationCanceledException()
    {
        using var database = TemporarySqliteDatabase.Create();
        var store = new SqliteIncidentStore(database.ConnectionString);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => store.GetByIdAsync(FixedIncidentGuid, cts.Token));
    }

    [Fact]
    public async Task GetByIdAsync_IncidentAbsent_RetourneNull()
    {
        using var database = TemporarySqliteDatabase.Create();
        var store = new SqliteIncidentStore(database.ConnectionString);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        FraudIncident? incident = await store.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Null(incident);
    }

    [Fact]
    public async Task SaveAsync_PuisGetByIdAsync_RetourneUnIncident()
    {
        var incident = await SaveAndReloadIncidentAsync();

        Assert.NotNull(incident);
    }

    [Fact]
    public async Task Roundtrip_ConserveIncidentId()
    {
        var incident = await SaveAndReloadIncidentAsync();

        Assert.Equal(FixedIncidentGuid, incident.IncidentId);
    }

    [Fact]
    public async Task Roundtrip_ConserveCreatedAt()
    {
        var incident = await SaveAndReloadIncidentAsync();

        Assert.Equal(CreatedAt, incident.CreatedAt);
    }

    [Fact]
    public async Task Roundtrip_ConserveEvidenceImportedAt()
    {
        var incident = await SaveAndReloadIncidentAsync();

        Assert.Equal(ImportedAt, incident.Evidence.ImportedAt);
    }

    [Fact]
    public async Task Roundtrip_ConserveEvidenceSourceFileName()
    {
        var incident = await SaveAndReloadIncidentAsync();

        Assert.Equal(DefaultSourceFileName, incident.Evidence.FileName);
    }

    [Fact]
    public async Task Roundtrip_ConserveIdentiteDeclaree()
    {
        var incident = await SaveAndReloadIncidentAsync();

        Assert.Equal("Support Frelon <support@example.test>", incident.Identity.From);
        Assert.Equal("reponse@example.test", incident.Identity.ReplyTo);
        Assert.Equal("bounce@example.test", incident.Identity.ReturnPath);
        Assert.Equal("<abc-123@example.test>", incident.Identity.MessageId);
        Assert.Equal("Facture en attente", incident.Identity.Subject);
    }

    [Fact]
    public async Task Roundtrip_ConserveAuthentification()
    {
        var incident = await SaveAndReloadIncidentAsync();

        Assert.Equal("pass", incident.Authentication.SpfResult);
        Assert.Equal("fail", incident.Authentication.DkimResult);
        Assert.Equal("fail", incident.Authentication.DmarcResult);
        Assert.Equal("spf=pass; dkim=fail; dmarc=fail", incident.Authentication.AuthenticationResultsRaw);
        Assert.True(incident.Authentication.IsSuspicious);
    }

    [Fact]
    public async Task Roundtrip_ConserveLaPisteDeClassificationExpliquee()
    {
        var incident = await SaveAndReloadIncidentAsync();

        Assert.Equal(FraudClassification.Suspicious, incident.ClassificationAssessment.Classification);
        Assert.Equal(ClassificationConfidence.Medium, incident.ClassificationAssessment.Confidence);
        Assert.Equal(["Signaux hétérogènes"], incident.ClassificationAssessment.Reasons);
    }

    [Fact]
    public async Task Roundtrip_ConserveReceivedChainEtOrdre()
    {
        var incident = await SaveAndReloadIncidentAsync();

        Assert.Collection(
            incident.ReceivedChain,
            first =>
            {
                Assert.Equal(0, first.Position);
                Assert.Equal("mx1.example.test", first.From);
                Assert.Equal("mx2.example.test", first.By);
                Assert.Equal("ESMTPS", first.With);
                Assert.Equal("203.0.113.10", first.IpAddress);
                Assert.Equal(ReceivedHop1At, first.Timestamp);
                Assert.Equal(
                    "from mx1.example.test by mx2.example.test with ESMTPS; Fri, 03 Jul 2026 14:11:00 +0000",
                    first.RawValue);
            },
            second =>
            {
                Assert.Equal(1, second.Position);
                Assert.Equal("gateway.example.test", second.From);
                Assert.Equal("mx1.example.test", second.By);
                Assert.Equal("SMTP", second.With);
                Assert.Equal("198.51.100.20", second.IpAddress);
                Assert.Equal(ReceivedHop2At, second.Timestamp);
                Assert.Equal(
                    "from gateway.example.test by mx1.example.test with SMTP; Fri, 03 Jul 2026 14:12:00 +0000",
                    second.RawValue);
            });
    }

    [Fact]
    public async Task Roundtrip_ConserveUrls()
    {
        var incident = await SaveAndReloadIncidentAsync();

        Assert.Collection(
            incident.Urls,
            first =>
            {
                Assert.Equal("http://login.example.test/secure", first.RawValue);
                Assert.Equal("http://login.example.test/secure", first.NormalizedValue);
                Assert.Equal("login.example.test", first.Host);
                Assert.Equal("http", first.Scheme);
                Assert.True(first.IsSuspicious);
                Assert.Equal(["Hôte inconnu", "Chemin de connexion"], first.Reasons);
            },
            second =>
            {
                Assert.Equal("https://cdn.example.test/logo.png", second.RawValue);
                Assert.Equal("https://cdn.example.test/logo.png", second.NormalizedValue);
                Assert.Equal("cdn.example.test", second.Host);
                Assert.Equal("https", second.Scheme);
                Assert.False(second.IsSuspicious);
                Assert.Equal(["Ressource distante"], second.Reasons);
            });
    }

    [Fact]
    public async Task Roundtrip_ConservePiecesJointes()
    {
        var incident = await SaveAndReloadIncidentAsync();

        Assert.Collection(
            incident.Attachments,
            first =>
            {
                Assert.Equal("facture.pdf", first.FileName);
                Assert.Equal("application/pdf", first.ContentType);
                Assert.Equal(12345, first.SizeBytes);
                Assert.Equal("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", first.Sha256);
                Assert.True(first.IsSuspicious);
                Assert.Equal(["Nom trompeur"], first.Reasons);
            },
            second =>
            {
                Assert.Equal("archive.zip", second.FileName);
                Assert.Equal("application/zip", second.ContentType);
                Assert.Equal(54321, second.SizeBytes);
                Assert.Equal("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", second.Sha256);
                Assert.False(second.IsSuspicious);
                Assert.Equal(["Archive chiffrée"], second.Reasons);
            });
    }

    [Fact]
    public async Task Roundtrip_ConserveIocsEtOrdre()
    {
        var incident = await SaveAndReloadIncidentAsync();

        Assert.Collection(
            incident.Iocs,
            first =>
            {
                Assert.Equal(IocType.Url, first.Type);
                Assert.Equal("http://login.example.test/secure", first.Value);
                Assert.Equal(0.95, first.Confidence);
                Assert.Equal("url-extractor", first.Source);
                Assert.Equal(Ioc1FirstSeen, first.FirstSeen);
            },
            second =>
            {
                Assert.Equal(IocType.Domain, second.Type);
                Assert.Equal("example.test", second.Value);
                Assert.Equal(0.90, second.Confidence);
                Assert.Equal("domain-extractor", second.Source);
                Assert.Equal(Ioc2FirstSeen, second.FirstSeen);
            },
            third =>
            {
                Assert.Equal(IocType.Hash, third.Type);
                Assert.Equal("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", third.Value);
                Assert.Equal(1.0, third.Confidence);
                Assert.Equal("attachment-hash", third.Source);
                Assert.Equal(Ioc3FirstSeen, third.FirstSeen);
            });
    }

    [Fact]
    public async Task Roundtrip_ConserveRiskScoreValue()
    {
        var incident = await SaveAndReloadIncidentAsync();

        Assert.Equal(RiskValue, incident.RiskScore.Value);
    }

    [Fact]
    public async Task Roundtrip_ConserveRiskScoreLevel()
    {
        var incident = await SaveAndReloadIncidentAsync();

        Assert.Equal(RiskLevel.Critical, incident.RiskScore.Level);
    }

    [Fact]
    public async Task Roundtrip_ConserveRiskScoreReasonsEtOrdre()
    {
        var incident = await SaveAndReloadIncidentAsync();

        Assert.Collection(
            incident.RiskScore.Reasons,
            first => Assert.Equal("Échec SPF", first),
            second => Assert.Equal("Lien de connexion suspect", second),
            third => Assert.Equal("Pièce jointe douteuse", third));
    }

    [Fact]
    public async Task Roundtrip_ConserveClassification()
    {
        var incident = await SaveAndReloadIncidentAsync();

        Assert.Equal(FraudClassification.Phishing, incident.Classification);
    }

    [Fact]
    public async Task Roundtrip_ConserveRecommendedActions()
    {
        var incident = await SaveAndReloadIncidentAsync();

        Assert.Collection(
            incident.RecommendedActions,
            first =>
            {
                Assert.Equal(RecommendedActionType.ReviewManually, first.Type);
                Assert.Equal("Relire manuellement", first.Label);
                Assert.Equal("Vérifier l'incident dans la boîte de réception.", first.Description);
                Assert.True(first.RequiresHumanValidation);
            },
            second =>
            {
                Assert.Equal(RecommendedActionType.PrepareAbuseReport, second.Type);
                Assert.Equal("Préparer un signalement abuse", second.Label);
                Assert.Equal("Préparer un signalement à l'hébergeur du domaine.", second.Description);
                Assert.True(second.RequiresHumanValidation);
            });
    }

    [Fact]
    public async Task SaveAsync_Doublon_LeveInvalidOperationException()
    {
        using var database = TemporarySqliteDatabase.Create();
        var store = new SqliteIncidentStore(database.ConnectionString);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        var original = BuildRichIncident(FixedIncidentId, DefaultSourceFileName);
        var duplicate = BuildRichIncident(FixedIncidentId, "autre-fichier.eml");

        await store.SaveAsync(original, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.SaveAsync(duplicate, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveAsync_Doublon_MessageExact()
    {
        using var database = TemporarySqliteDatabase.Create();
        var store = new SqliteIncidentStore(database.ConnectionString);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        var original = BuildRichIncident(FixedIncidentId, DefaultSourceFileName);
        var duplicate = BuildRichIncident(FixedIncidentId, "autre-fichier.eml");

        await store.SaveAsync(original, TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.SaveAsync(duplicate, TestContext.Current.CancellationToken));

        Assert.Equal(
            "Un incident avec l'identifiant '11111111-2222-3333-4444-555555555555' existe déjà.",
            exception.Message);
    }

    [Fact]
    public async Task SaveAsync_Doublon_ConserveLIncidentInitial()
    {
        using var database = TemporarySqliteDatabase.Create();
        var store = new SqliteIncidentStore(database.ConnectionString);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        var original = BuildRichIncident(FixedIncidentId, DefaultSourceFileName);
        var duplicate = BuildRichIncident(FixedIncidentId, "autre-fichier.eml");

        await store.SaveAsync(original, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.SaveAsync(duplicate, TestContext.Current.CancellationToken));

        var reloaded = await store.GetByIdAsync(FixedIncidentGuid, TestContext.Current.CancellationToken);

        Assert.NotNull(reloaded);
        Assert.Equal(DefaultSourceFileName, reloaded!.Evidence.FileName);
        Assert.Equal(RiskValue, reloaded.RiskScore.Value);
        Assert.Equal(FraudClassification.Phishing, reloaded.Classification);
    }

    [Fact]
    public async Task SaveAsync_Doublon_ConserveInnerExceptionSqlite()
    {
        using var database = TemporarySqliteDatabase.Create();
        var store = new SqliteIncidentStore(database.ConnectionString);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        var original = BuildRichIncident(FixedIncidentId, DefaultSourceFileName);
        var duplicate = BuildRichIncident(FixedIncidentId, "autre-fichier.eml");

        await store.SaveAsync(original, TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.SaveAsync(duplicate, TestContext.Current.CancellationToken));

        Assert.NotNull(exception.InnerException);
        Assert.IsType<SqliteException>(exception.InnerException);
    }

    [Fact]
    public async Task SaveAsync_ValeurSourceFileNameHostile_RetourneExactementLaValeur()
    {
        var incident = await SaveAndReloadIncidentAsync(FixedIncidentId, HostileSourceFileName);

        Assert.Equal(HostileSourceFileName, incident.Evidence.FileName);
    }

    [Fact]
    public async Task SaveAsync_ValeurSourceFileNameHostile_LaisseLeStoreFonctionnelPourUnSecondIncident()
    {
        using var database = TemporarySqliteDatabase.Create();
        var store = new SqliteIncidentStore(database.ConnectionString);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        var hostileIncident = BuildRichIncident(FixedIncidentId, HostileSourceFileName);
        var secondIncident = BuildRichIncident("22222222-3333-4444-5555-666666666666", DefaultSourceFileName);
        var secondIncidentId = secondIncident.IncidentId;

        await store.SaveAsync(hostileIncident, TestContext.Current.CancellationToken);
        await store.SaveAsync(secondIncident, TestContext.Current.CancellationToken);

        Assert.True(await TableExistsAsync(database.ConnectionString));

        var reloaded = await store.GetByIdAsync(secondIncidentId, TestContext.Current.CancellationToken);

        Assert.NotNull(reloaded);
        Assert.Equal(DefaultSourceFileName, reloaded!.Evidence.FileName);
        Assert.Equal(FixedIncidentGuid, (await store.GetByIdAsync(FixedIncidentGuid, TestContext.Current.CancellationToken))!.IncidentId);
    }

    [Fact]
    public async Task MetadonneesSql_SchemaVersionVaut1()
    {
        var row = await SaveAndReadRowAsync();

        Assert.Equal(1, row.SchemaVersion);
    }

    [Fact]
    public async Task MetadonneesSql_CreatedAtCorrespondAuFormatO()
    {
        var row = await SaveAndReadRowAsync();

        Assert.Equal(CreatedAt.ToString("O"), row.CreatedAt);
    }

    [Fact]
    public async Task MetadonneesSql_ImportedAtCorrespondAuFormatO()
    {
        var row = await SaveAndReadRowAsync();

        Assert.Equal(ImportedAt.ToString("O"), row.ImportedAt);
    }

    [Fact]
    public async Task MetadonneesSql_SourceFileNameCorrespondExactement()
    {
        var row = await SaveAndReadRowAsync();

        Assert.Equal(DefaultSourceFileName, row.SourceFileName);
    }

    [Fact]
    public async Task MetadonneesSql_RiskValueCorrespondExactement()
    {
        var row = await SaveAndReadRowAsync();

        Assert.Equal(RiskValue, row.RiskValue);
    }

    [Fact]
    public async Task MetadonneesSql_RiskLevelCorrespondAuNomEnum()
    {
        var row = await SaveAndReadRowAsync();

        Assert.Equal(RiskLevel.Critical.ToString(), row.RiskLevel);
    }

    [Fact]
    public async Task MetadonneesSql_ClassificationCorrespondAuNomEnum()
    {
        var row = await SaveAndReadRowAsync();

        Assert.Equal(FraudClassification.Phishing.ToString(), row.Classification);
    }

    [Fact]
    public async Task PayloadJson_EstUnObjetJsonValide()
    {
        var row = await SaveAndReadRowAsync();

        using var document = JsonDocument.Parse(row.PayloadJson);

        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
    }

    [Fact]
    public async Task PayloadJson_SerialiseLesEnumsEnChaines()
    {
        var row = await SaveAndReadRowAsync();

        using var document = JsonDocument.Parse(row.PayloadJson);

        Assert.Equal(JsonValueKind.String, document.RootElement.GetProperty("classification").ValueKind);
        Assert.Equal("Phishing", document.RootElement.GetProperty("classification").GetString());

        var riskScore = document.RootElement.GetProperty("riskScore");
        Assert.Equal(JsonValueKind.String, riskScore.GetProperty("level").ValueKind);
        Assert.Equal("Critical", riskScore.GetProperty("level").GetString());

        var iocs = document.RootElement.GetProperty("iocs");
        var enumerator = iocs.EnumerateArray();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(JsonValueKind.String, enumerator.Current.GetProperty("type").ValueKind);
        Assert.Equal("Url", enumerator.Current.GetProperty("type").GetString());

        var recommendedActions = document.RootElement.GetProperty("recommendedActions");
        var actionEnumerator = recommendedActions.EnumerateArray();
        Assert.True(actionEnumerator.MoveNext());
        Assert.Equal(JsonValueKind.String, actionEnumerator.Current.GetProperty("type").ValueKind);
        Assert.Equal("ReviewManually", actionEnumerator.Current.GetProperty("type").GetString());
    }

    [Fact]
    public async Task GetByIdAsync_SchemaVersion2_LeveNotSupportedException()
    {
        using var database = TemporarySqliteDatabase.Create();
        var store = new SqliteIncidentStore(database.ConnectionString);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        var storedIncident = BuildRichIncident(FixedIncidentId, DefaultSourceFileName);
        await InsertIncidentRowAsync(database.ConnectionString, storedIncident, 2, SerializeSnapshot(storedIncident));

        await Assert.ThrowsAsync<NotSupportedException>(
            () => store.GetByIdAsync(FixedIncidentGuid, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetByIdAsync_SchemaVersion2_MessageExact()
    {
        using var database = TemporarySqliteDatabase.Create();
        var store = new SqliteIncidentStore(database.ConnectionString);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        var storedIncident = BuildRichIncident(FixedIncidentId, DefaultSourceFileName);
        await InsertIncidentRowAsync(database.ConnectionString, storedIncident, 2, SerializeSnapshot(storedIncident));

        var exception = await Assert.ThrowsAsync<NotSupportedException>(
            () => store.GetByIdAsync(FixedIncidentGuid, TestContext.Current.CancellationToken));

        Assert.Equal("Version de schéma de stockage non supportée : 2.", exception.Message);
    }

    [Fact]
    public async Task GetByIdAsync_SnapshotIncidentIdMismatch_LeveInvalidDataException()
    {
        using var database = TemporarySqliteDatabase.Create();
        var store = new SqliteIncidentStore(database.ConnectionString);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        var storedIncident = BuildRichIncident(FixedIncidentId, DefaultSourceFileName);
        var wrongSnapshot = BuildRichIncident("22222222-3333-4444-5555-666666666666", DefaultSourceFileName);

        await InsertIncidentRowAsync(database.ConnectionString, storedIncident, 1, SerializeSnapshot(wrongSnapshot));

        await Assert.ThrowsAsync<InvalidDataException>(
            () => store.GetByIdAsync(FixedIncidentGuid, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetByIdAsync_SnapshotIncidentIdMismatch_MessageExact()
    {
        using var database = TemporarySqliteDatabase.Create();
        var store = new SqliteIncidentStore(database.ConnectionString);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        var storedIncident = BuildRichIncident(FixedIncidentId, DefaultSourceFileName);
        var wrongSnapshot = BuildRichIncident("22222222-3333-4444-5555-666666666666", DefaultSourceFileName);

        await InsertIncidentRowAsync(database.ConnectionString, storedIncident, 1, SerializeSnapshot(wrongSnapshot));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => store.GetByIdAsync(FixedIncidentGuid, TestContext.Current.CancellationToken));

        Assert.Equal(
            "L'identifiant du snapshot ne correspond pas à l'identifiant stocké '11111111-2222-3333-4444-555555555555'.",
            exception.Message);
    }

    [Fact]
    public async Task GetByIdAsync_PayloadJsonNull_LeveInvalidDataException_MessageExact()
    {
        using var database = TemporarySqliteDatabase.Create();
        var store = new SqliteIncidentStore(database.ConnectionString);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        var storedIncident = BuildRichIncident(FixedIncidentId, DefaultSourceFileName);

        await InsertIncidentRowAsync(database.ConnectionString, storedIncident, 1, "null");

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => store.GetByIdAsync(FixedIncidentGuid, TestContext.Current.CancellationToken));

        Assert.Equal(
            "Le snapshot de l'incident '11111111-2222-3333-4444-555555555555' est invalide.",
            exception.Message);
    }

    private static async Task<FraudIncident> SaveAndReloadIncidentAsync(
        string incidentId = FixedIncidentId,
        string sourceFileName = DefaultSourceFileName)
    {
        using var database = TemporarySqliteDatabase.Create();
        var store = new SqliteIncidentStore(database.ConnectionString);
        await store.InitializeAsync();

        var incident = BuildRichIncident(incidentId, sourceFileName);
        await store.SaveAsync(incident);

        return await store.GetByIdAsync(Guid.Parse(incidentId))
            ?? throw new InvalidOperationException("L'incident attendu n'a pas été relu.");
    }

    private static async Task<StoredIncidentRow> SaveAndReadRowAsync(
        string incidentId = FixedIncidentId,
        string sourceFileName = DefaultSourceFileName)
    {
        using var database = TemporarySqliteDatabase.Create();
        var store = new SqliteIncidentStore(database.ConnectionString);
        await store.InitializeAsync();

        var incident = BuildRichIncident(incidentId, sourceFileName);
        await store.SaveAsync(incident);

        return await ReadPersistedRowAsync(database.ConnectionString, incidentId)
            ?? throw new InvalidOperationException("La ligne attendue n'a pas été lue.");
    }

    private static async Task<StoredIncidentRow?> ReadPersistedRowAsync(
        string connectionString,
        string incidentId)
    {
        using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = """
SELECT incident_id,
       schema_version,
       created_at,
       imported_at,
       source_file_name,
       risk_value,
       risk_level,
       classification,
       payload_json
FROM incidents
WHERE incident_id = $incidentId;
""";
        command.Parameters.AddWithValue("$incidentId", incidentId);

        using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new StoredIncidentRow(
            reader.GetString(0),
            reader.GetInt32(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetDouble(5),
            reader.GetString(6),
            reader.GetString(7),
            reader.GetString(8));
    }

    private static async Task InsertIncidentRowAsync(
        string connectionString,
        FraudIncident incident,
        int schemaVersion,
        string payloadJson)
    {
        using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

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
        command.Parameters.AddWithValue("$incidentId", incident.IncidentId.ToString("D"));
        command.Parameters.AddWithValue("$schemaVersion", schemaVersion);
        command.Parameters.AddWithValue("$createdAt", incident.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$importedAt", incident.Evidence.ImportedAt!.Value.ToString("O"));
        command.Parameters.AddWithValue("$sourceFileName", incident.Evidence.FileName);
        command.Parameters.AddWithValue("$riskValue", incident.RiskScore.Value);
        command.Parameters.AddWithValue("$riskLevel", incident.RiskScore.Level.ToString());
        command.Parameters.AddWithValue("$classification", incident.Classification.ToString());
        command.Parameters.AddWithValue("$payloadJson", payloadJson);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<bool> TableExistsAsync(string connectionString)
    {
        using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = """
SELECT 1
FROM sqlite_master
WHERE type = 'table'
  AND name = 'incidents'
LIMIT 1;
""";

        using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync();
    }

    private static async Task<IReadOnlyList<string>> GetTableColumnNamesAsync(string connectionString)
    {
        using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(incidents);";

        using var reader = await command.ExecuteReaderAsync();
        var columnNames = new List<string>();

        while (await reader.ReadAsync())
        {
            columnNames.Add(reader.GetString(1));
        }

        return columnNames;
    }

    private static string SerializeSnapshot(FraudIncident incident)
    {
        return JsonSerializer.Serialize(incident, SnapshotJsonOptions);
    }

    private static JsonSerializerOptions CreateSnapshotJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static FraudIncident BuildRichIncident(
        string incidentId = FixedIncidentId,
        string sourceFileName = DefaultSourceFileName)
    {
        return new FraudIncident
        {
            IncidentId = Guid.Parse(incidentId),
            CreatedAt = CreatedAt,
            Evidence = new EvidenceSource
            {
                FileName = sourceFileName,
                ImportedAt = ImportedAt,
                FilePath = @"C:\Temp\suspicious.eml",
                Sha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
            },
            Identity = new MailIdentity
            {
                From = "Support Frelon <support@example.test>",
                ReplyTo = "reponse@example.test",
                ReturnPath = "bounce@example.test",
                MessageId = "<abc-123@example.test>",
                Subject = "Facture en attente"
            },
            Authentication = new AuthenticationAssessment
            {
                SpfResult = "pass",
                DkimResult = "fail",
                DmarcResult = "fail",
                AuthenticationResultsRaw = "spf=pass; dkim=fail; dmarc=fail",
                IsSuspicious = true
            },
            ReceivedChain =
            [
                new ReceivedHop
                {
                    Position = 0,
                    From = "mx1.example.test",
                    By = "mx2.example.test",
                    With = "ESMTPS",
                    IpAddress = "203.0.113.10",
                    Timestamp = ReceivedHop1At,
                    RawValue = "from mx1.example.test by mx2.example.test with ESMTPS; Fri, 03 Jul 2026 14:11:00 +0000"
                },
                new ReceivedHop
                {
                    Position = 1,
                    From = "gateway.example.test",
                    By = "mx1.example.test",
                    With = "SMTP",
                    IpAddress = "198.51.100.20",
                    Timestamp = ReceivedHop2At,
                    RawValue = "from gateway.example.test by mx1.example.test with SMTP; Fri, 03 Jul 2026 14:12:00 +0000"
                }
            ],
            Urls =
            [
                new UrlIndicator
                {
                    RawValue = "http://login.example.test/secure",
                    NormalizedValue = "http://login.example.test/secure",
                    Host = "login.example.test",
                    Scheme = "http",
                    IsSuspicious = true,
                    Reasons = ["Hôte inconnu", "Chemin de connexion"]
                },
                new UrlIndicator
                {
                    RawValue = "https://cdn.example.test/logo.png",
                    NormalizedValue = "https://cdn.example.test/logo.png",
                    Host = "cdn.example.test",
                    Scheme = "https",
                    IsSuspicious = false,
                    Reasons = ["Ressource distante"]
                }
            ],
            Attachments =
            [
                new AttachmentIndicator
                {
                    FileName = "facture.pdf",
                    ContentType = "application/pdf",
                    SizeBytes = 12345,
                    Sha256 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                    IsSuspicious = true,
                    Reasons = ["Nom trompeur"]
                },
                new AttachmentIndicator
                {
                    FileName = "archive.zip",
                    ContentType = "application/zip",
                    SizeBytes = 54321,
                    Sha256 = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                    IsSuspicious = false,
                    Reasons = ["Archive chiffrée"]
                }
            ],
            Iocs =
            [
                new Ioc
                {
                    Type = IocType.Url,
                    Value = "http://login.example.test/secure",
                    Confidence = 0.95,
                    Source = "url-extractor",
                    FirstSeen = Ioc1FirstSeen
                },
                new Ioc
                {
                    Type = IocType.Domain,
                    Value = "example.test",
                    Confidence = 0.90,
                    Source = "domain-extractor",
                    FirstSeen = Ioc2FirstSeen
                },
                new Ioc
                {
                    Type = IocType.Hash,
                    Value = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                    Confidence = 1.0,
                    Source = "attachment-hash",
                    FirstSeen = Ioc3FirstSeen
                }
            ],
            Classification = FraudClassification.Phishing,
            ClassificationAssessment = new ClassificationAssessment(
                FraudClassification.Suspicious,
                ClassificationConfidence.Medium,
                ["Signaux hétérogènes"]),
            RiskScore = new RiskScore
            {
                Value = RiskValue,
                Level = RiskLevel.Critical,
                Reasons = ["Échec SPF", "Lien de connexion suspect", "Pièce jointe douteuse"]
            },
            RecommendedActions =
            [
                new RecommendedAction
                {
                    Type = RecommendedActionType.ReviewManually,
                    Label = "Relire manuellement",
                    Description = "Vérifier l'incident dans la boîte de réception.",
                    RequiresHumanValidation = true
                },
                new RecommendedAction
                {
                    Type = RecommendedActionType.PrepareAbuseReport,
                    Label = "Préparer un signalement abuse",
                    Description = "Préparer un signalement à l'hébergeur du domaine.",
                    RequiresHumanValidation = true
                }
            ]
        };
    }

    private sealed record StoredIncidentRow(
        string IncidentId,
        int SchemaVersion,
        string CreatedAt,
        string ImportedAt,
        string SourceFileName,
        double RiskValue,
        string RiskLevel,
        string Classification,
        string PayloadJson);

    private sealed class TemporarySqliteDatabase : IDisposable
    {
        public string FilePath { get; }

        public string ConnectionString { get; }

        private TemporarySqliteDatabase(string filePath)
        {
            FilePath = filePath;
            ConnectionString = new SqliteConnectionStringBuilder
            {
                DataSource = filePath
            }.ToString();
        }

        public static TemporarySqliteDatabase Create()
        {
            var filePath = Path.Combine(Path.GetTempPath(), $"frelon-storage-tests-{Guid.NewGuid():N}.db");
            return new TemporarySqliteDatabase(filePath);
        }

        public void Dispose()
        {
            DeleteIfExists(FilePath);
            DeleteIfExists(FilePath + "-wal");
            DeleteIfExists(FilePath + "-shm");
            DeleteIfExists(FilePath + "-journal");
        }

        private static void DeleteIfExists(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Nettoyage best-effort pour les fichiers SQLite temporaires.
            }
        }
    }
}
