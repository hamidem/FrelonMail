using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Frelon.Core;

namespace Frelon.Exporters.Tests;

/// <summary>
/// Vérifie que le paquet partageable reste utile sans divulguer les références locales.
/// </summary>
public sealed class BasicShareableIocExporterTests
{
    private static readonly Guid FirstIncidentId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SecondIncidentId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ExportId =
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateTimeOffset PreparedAt =
        new(2026, 7, 24, 0, 30, 0, TimeSpan.FromHours(2));

    private readonly BasicShareableIocExporter _exporter = new();

    [Fact]
    public void Export_EntreesValidees_ProduitTroisDocumentsEnMemoire()
    {
        var result = _exporter.Export(BuildRequest());

        Assert.Equal(ExportId, result.ShareablePackage.ExportId);
        Assert.Equal(new DateOnly(2026, 7, 23), result.ShareablePackage.GeneratedOn);
        Assert.Equal(
            $"frelon-iocs-partage-{ExportId:N}.zip",
            result.ShareablePackage.SuggestedArchiveFileName);
        Assert.Equal(
            ["LISEZ-MOI.md", "iocs-partage.json", "iocs-partage.csv"],
            result.ShareablePackage.Artifacts.Select(artifact => artifact.FileName));
    }

    [Fact]
    public void Export_Json_NExposeQueDomainesEtSha256Normalises()
    {
        var jsonArtifact = GetArtifact(
            _exporter.Export(BuildRequest()),
            "iocs-partage.json");
        using var json = JsonDocument.Parse(jsonArtifact.Content);
        var iocs = json.RootElement.GetProperty("iocs");

        Assert.Equal(2, iocs.GetArrayLength());
        Assert.Equal("Domain", iocs[0].GetProperty("type").GetString());
        Assert.Equal("xn--bcher-kva.example", iocs[0].GetProperty("value").GetString());
        Assert.Equal("Medium", iocs[0].GetProperty("observationConfidence").GetString());
        Assert.Equal(2, iocs[0].GetProperty("occurrenceCount").GetInt32());
        Assert.Equal("Hash", iocs[1].GetProperty("type").GetString());
        Assert.Equal(new string('c', 64), iocs[1].GetProperty("value").GetString());
        Assert.Equal("High", iocs[1].GetProperty("observationConfidence").GetString());
    }

    [Fact]
    public void Export_PaquetPartageable_NExposeAucuneReferenceLocaleOuDonneeEcartee()
    {
        var result = _exporter.Export(BuildRequest());
        var content = string.Join(
            "\n",
            result.ShareablePackage.Artifacts.Select(artifact => artifact.Content));

        foreach (var forbidden in new[]
        {
            FirstIncidentId.ToString("D"),
            SecondIncidentId.ToString("D"),
            ReviewId(1).ToString("D"),
            ReviewId(2).ToString("D"),
            new string('a', 64),
            new string('b', 64),
            "incident-personnel-1.eml",
            "incident-personnel-2.eml",
            "victime@example.test",
            "https://xn--bcher-kva.example/login?email=victime@example.test",
            "203.0.113.10",
            "Jean-Dupont-facture.pdf",
            "module-interne-secret",
            "2026-07-23T09:01:02",
        })
        {
            Assert.DoesNotContain(forbidden, content, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Export_HashIdentiqueAUnePreuveSource_EstToujoursExclu()
    {
        var result = _exporter.Export(BuildRequest());
        var json = GetArtifact(result, "iocs-partage.json").Content;

        Assert.DoesNotContain(new string('a', 64), json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(new string('b', 64), json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(new string('c', 64), json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Export_AuditLocal_ConserveLesReferencesEtEmpreintesDesDocuments()
    {
        var result = _exporter.Export(BuildRequest());
        var audit = result.LocalAudit;

        Assert.Equal(ExportId, audit.ExportId);
        Assert.Equal(PreparedAt, audit.PreparedAt);
        Assert.Equal([FirstIncidentId, SecondIncidentId], audit.Sources.Select(source => source.IncidentId));
        Assert.Equal([ReviewId(1), ReviewId(2)], audit.Sources.Select(source => source.IncidentReviewId));
        Assert.Equal(2, audit.ExportedIocCount);
        Assert.True(audit.FilteredIocCount > 0);

        foreach (var artifact in result.ShareablePackage.Artifacts)
        {
            var digest = Assert.Single(
                audit.ArtifactDigests,
                item => item.FileName == artifact.FileName);
            Assert.Equal(ComputeSha256(artifact.Content), digest.Sha256);
        }
    }

    [Fact]
    public void Export_Readme_DitHonnetementQueLAnonymisationNestPasAbsolue()
    {
        var readme = GetArtifact(
            _exporter.Export(BuildRequest()),
            "LISEZ-MOI.md");

        Assert.Contains("ne l'a publié ni transmis", readme.Content, StringComparison.Ordinal);
        Assert.Contains("ne constitue pas une garantie", readme.Content, StringComparison.Ordinal);
        Assert.Contains("vérification humaine et juridique", readme.Content, StringComparison.Ordinal);
        Assert.Contains("URL complètes", readme.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_Csv_EstStableMinimalEtCompatibleTableur()
    {
        var csv = GetArtifact(
            _exporter.Export(BuildRequest()),
            "iocs-partage.csv").Content;

        Assert.StartsWith(
            "type,value,observationConfidence,occurrenceCount\r\n",
            csv,
            StringComparison.Ordinal);
        Assert.Contains(
            "Domain,xn--bcher-kva.example,Medium,2\r\n",
            csv,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            '\n',
            csv.Replace("\r\n", string.Empty, StringComparison.Ordinal));
    }

    [Fact]
    public void Export_Json_ExposeVersionProfilEtDateSeulementAuJour()
    {
        var jsonArtifact = GetArtifact(
            _exporter.Export(BuildRequest()),
            "iocs-partage.json");
        using var json = JsonDocument.Parse(jsonArtifact.Content);
        var root = json.RootElement;

        Assert.Equal(BasicShareableIocExporter.SchemaVersion, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(ExportId, root.GetProperty("exportId").GetGuid());
        Assert.Equal("2026-07-23", root.GetProperty("generatedOn").GetString());
        Assert.Equal("StrictMinimization", root.GetProperty("privacyProfile").GetString());
        Assert.False(root.TryGetProperty("incidentId", out _));
    }

    [Fact]
    public void Export_RevueManquante_RefuseLePartage()
    {
        var reviews = BuildReviews();
        var request = BuildRequest(reviews: [reviews[0]]);

        var exception = Assert.Throws<InvalidOperationException>(
            () => _exporter.Export(request));

        Assert.Contains("chaque incident", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Export_FraudeNonConfirmee_RefuseLePartage()
    {
        var reviews = BuildReviews();
        reviews[1] = new IncidentReviewDecision(
            ReviewId(2),
            SecondIncidentId,
            ReviewVerdict.Suspicious,
            FraudClassification.Suspicious,
            PreparedAt.AddHours(-1));
        var request = BuildRequest(reviews: reviews);

        var exception = Assert.Throws<InvalidOperationException>(
            () => _exporter.Export(request));

        Assert.Contains(
            SecondIncidentId.ToString("D"),
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Export_PreparationAnterieureAUneRevue_RefuseLePartage()
    {
        var reviews = BuildReviews();
        reviews[1] = new IncidentReviewDecision(
            ReviewId(2),
            SecondIncidentId,
            ReviewVerdict.ConfirmedFraud,
            FraudClassification.Phishing,
            PreparedAt.AddMinutes(1));

        var exception = Assert.Throws<InvalidOperationException>(
            () => _exporter.Export(BuildRequest(reviews: reviews)));

        Assert.Contains(
            SecondIncidentId.ToString("D"),
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Export_PreuveSansSha256_RefuseLePartage()
    {
        var incidents = BuildIncidents();
        incidents[0] = incidents[0] with
        {
            Evidence = incidents[0].Evidence with { Sha256 = null },
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => _exporter.Export(BuildRequest(incidents)));

        Assert.Contains("SHA-256", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_SelectionAbsenteDesObservations_RefuseLePartage()
    {
        var incidents = BuildIncidents(iocs: _ =>
        [
            BuildIoc(IocType.Url, "https://fraud.example/login"),
            BuildIoc(IocType.Email, "victime@example.test"),
            BuildIoc(IocType.IpAddress, "203.0.113.10"),
        ]);
        var approvedIocs = new[]
        {
            new ShareableIocSelection(IocType.Domain, "fraud.example"),
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => _exporter.Export(BuildRequest(incidents, approvedIocs: approvedIocs)));

        Assert.Contains(
            "n'est pas une observation éligible",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Export_DomainesEtHashInvalides_SontFiltres()
    {
        var incidents = BuildIncidents(iocs: _ =>
        [
            BuildIoc(IocType.Domain, "=HYPERLINK(evil)"),
            BuildIoc(IocType.Domain, "localhost"),
            BuildIoc(IocType.Hash, "abcd"),
            BuildIoc(IocType.Domain, "valid.example"),
        ]);
        var result = _exporter.Export(BuildRequest(
            incidents,
            approvedIocs:
            [
                new ShareableIocSelection(IocType.Domain, "valid.example"),
            ]));
        var csv = GetArtifact(result, "iocs-partage.csv").Content;

        Assert.Contains("valid.example", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("HYPERLINK", csv, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("localhost", csv, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("abcd", csv, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Export_IocSousLeSeuilDeConfiance_EstFiltre()
    {
        var incidents = BuildIncidents(iocs: _ =>
        [
            BuildIoc(IocType.Domain, "low.example", confidence: 0.49),
            BuildIoc(IocType.Domain, "kept.example", confidence: 0.5),
        ]);
        var csv = GetArtifact(
            _exporter.Export(BuildRequest(
                incidents,
                approvedIocs:
                [
                    new ShareableIocSelection(IocType.Domain, "kept.example"),
                ])),
            "iocs-partage.csv").Content;

        Assert.DoesNotContain("low.example", csv, StringComparison.Ordinal);
        Assert.Contains("kept.example", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_IocEligibleMaisNonSelectionne_ResteAbsent()
    {
        var incidents = BuildIncidents(iocs: _ =>
        [
            BuildIoc(IocType.Domain, "approved.example"),
            BuildIoc(IocType.Domain, "not-approved.example"),
        ]);
        var result = _exporter.Export(BuildRequest(
            incidents,
            approvedIocs:
            [
                new ShareableIocSelection(IocType.Domain, "approved.example"),
            ]));
        var content = string.Join(
            "\n",
            result.ShareablePackage.Artifacts.Select(artifact => artifact.Content));

        Assert.Contains("approved.example", content, StringComparison.Ordinal);
        Assert.DoesNotContain("not-approved.example", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_IocSelectionneSousLeSeuilDeConfiance_RefuseLePartage()
    {
        var incidents = BuildIncidents(iocs: _ =>
        [
            BuildIoc(IocType.Domain, "low.example", confidence: 0.49),
        ]);

        var exception = Assert.Throws<InvalidOperationException>(
            () => _exporter.Export(BuildRequest(
                incidents,
                approvedIocs:
                [
                    new ShareableIocSelection(IocType.Domain, "low.example"),
                ])));

        Assert.Contains(
            "n'est pas une observation éligible",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Export_HashDePreuveExplicitementSelectionne_RefuseLePartage()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => _exporter.Export(BuildRequest(
                approvedIocs:
                [
                    new ShareableIocSelection(IocType.Hash, new string('a', 64)),
                ])));

        Assert.Contains(
            "empreinte de preuve source",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Export_SelectionsDupliqueesApresNormalisation_RefuseLAmbiguite()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => _exporter.Export(BuildRequest(
                approvedIocs:
                [
                    new ShareableIocSelection(IocType.Domain, "BÜCHER.Example."),
                    new ShareableIocSelection(IocType.Domain, "xn--bcher-kva.example"),
                ])));

        Assert.Contains(
            "plusieurs fois après normalisation",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Export_EntreesDansUnAutreOrdre_ConserveLeMemePaquet()
    {
        var incidents = BuildIncidents();
        var reviews = BuildReviews();
        var first = _exporter.Export(BuildRequest(incidents, reviews));
        var second = _exporter.Export(BuildRequest(
            [.. incidents.AsEnumerable().Reverse()],
            [.. reviews.AsEnumerable().Reverse()]));

        Assert.Equal(
            first.ShareablePackage.Artifacts.Select(artifact => (artifact.FileName, artifact.Content)),
            second.ShareablePackage.Artifacts.Select(artifact => (artifact.FileName, artifact.Content)));
    }

    [Fact]
    public void Export_NeModifieAucunIncidentNiIoc()
    {
        var incidents = BuildIncidents();
        var firstIoc = incidents[0].Iocs[0];
        var originalValue = firstIoc.Value;

        _exporter.Export(BuildRequest(incidents));

        Assert.Same(firstIoc, incidents[0].Iocs[0]);
        Assert.Equal(originalValue, firstIoc.Value);
    }

    [Fact]
    public void Export_RequeteNull_LeveArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => _exporter.Export(null!));

        Assert.Equal("request", exception.ParamName);
    }

    [Fact]
    public void Request_IncidentDuplique_RefuseLAmbiguite()
    {
        var incident = BuildIncidents()[0];

        var exception = Assert.Throws<ArgumentException>(
            () => BuildRequest([incident, incident]));

        Assert.Equal("incidents", exception.ParamName);
    }

    [Fact]
    public void Request_IdentifiantRevueDuplique_RefuseLAmbiguite()
    {
        var reviews = BuildReviews();
        reviews[1] = new IncidentReviewDecision(
            reviews[0].ReviewId,
            SecondIncidentId,
            ReviewVerdict.ConfirmedFraud,
            FraudClassification.Phishing,
            PreparedAt.AddHours(-1));

        var exception = Assert.Throws<ArgumentException>(
            () => BuildRequest(reviews: reviews));

        Assert.Equal("incidentReviews", exception.ParamName);
    }

    [Fact]
    public void Request_SelectionDupliquee_RefuseLAmbiguite()
    {
        var selection = new ShareableIocSelection(
            IocType.Domain,
            "xn--bcher-kva.example");

        var exception = Assert.Throws<ArgumentException>(
            () => BuildRequest(approvedIocs: [selection, selection]));

        Assert.Equal("approvedIocs", exception.ParamName);
    }

    [Fact]
    public void Request_IdentifiantExportReutilisantUneReferenceLocale_RefuseLaCorrelation()
    {
        foreach (var localId in new[] { FirstIncidentId, ReviewId(1) })
        {
            var exception = Assert.Throws<ArgumentException>(
                () => new ShareableIocExportRequest(
                    localId,
                    PreparedAt,
                    BuildIncidents(),
                    BuildReviews(),
                    BuildApprovedIocs()));

            Assert.Equal("exportId", exception.ParamName);
        }
    }

    [Fact]
    public void Selection_TypeNonPartageable_RefuseLaCreation()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new ShareableIocSelection(
                IocType.Email,
                "victime@example.test"));

        Assert.Equal("type", exception.ParamName);
    }

    private static ShareableIocExportRequest BuildRequest(
        IReadOnlyList<FraudIncident>? incidents = null,
        IReadOnlyList<IncidentReviewDecision>? reviews = null,
        IReadOnlyList<ShareableIocSelection>? approvedIocs = null)
        => new(
            ExportId,
            PreparedAt,
            incidents ?? BuildIncidents(),
            reviews ?? BuildReviews(),
            approvedIocs ?? BuildApprovedIocs());

    private static IReadOnlyList<ShareableIocSelection> BuildApprovedIocs()
        =>
        [
            new ShareableIocSelection(IocType.Domain, "BÜCHER.Example."),
            new ShareableIocSelection(IocType.Hash, new string('c', 64)),
        ];

    private static List<FraudIncident> BuildIncidents(
        Func<Guid, IReadOnlyList<Ioc>>? iocs = null)
        =>
        [
            BuildIncident(FirstIncidentId, 1, 'a', iocs?.Invoke(FirstIncidentId)),
            BuildIncident(SecondIncidentId, 2, 'b', iocs?.Invoke(SecondIncidentId)),
        ];

    private static FraudIncident BuildIncident(
        Guid incidentId,
        int minute,
        char evidenceHashCharacter,
        IReadOnlyList<Ioc>? iocs)
        => new()
        {
            IncidentId = incidentId,
            CreatedAt = new DateTimeOffset(2026, 7, 23, 9, minute, 2, TimeSpan.Zero),
            Evidence = new EvidenceSource
            {
                FileName = $"incident-personnel-{minute}.eml",
                ImportedAt = new DateTimeOffset(2026, 7, 23, 9, minute, 2, TimeSpan.Zero),
                Sha256 = new string(evidenceHashCharacter, 64),
            },
            Identity = new MailIdentity
            {
                From = "victime@example.test",
                ReplyTo = "personne.privee@example.test",
                MessageId = $"<local-{minute}@example.test>",
            },
            Authentication = new AuthenticationAssessment
            {
                SpfResult = "fail",
                DmarcResult = "fail",
            },
            Iocs = iocs ??
            [
                BuildIoc(
                    IocType.Domain,
                    minute == 1 ? "BÜCHER.Example." : "xn--bcher-kva.example",
                    minute == 1 ? 0.9 : 0.6),
                BuildIoc(IocType.Hash, new string('c', 64), 0.9),
                BuildIoc(IocType.Hash, new string(evidenceHashCharacter, 64), 1),
                BuildIoc(
                    IocType.Url,
                    "https://xn--bcher-kva.example/login?email=victime@example.test"),
                BuildIoc(IocType.Email, "victime@example.test"),
                BuildIoc(IocType.IpAddress, "203.0.113.10"),
                BuildIoc(IocType.FileName, "Jean-Dupont-facture.pdf"),
            ],
            Classification = FraudClassification.Unknown,
            RiskScore = new RiskScore
            {
                Value = 80,
                Level = RiskLevel.Critical,
            },
        };

    private static Ioc BuildIoc(
        IocType type,
        string value,
        double confidence = 0.8)
        => new()
        {
            Type = type,
            Value = value,
            Confidence = confidence,
            Source = "module-interne-secret",
            FirstSeen = new DateTimeOffset(2026, 7, 23, 9, 1, 2, TimeSpan.Zero),
        };

    private static List<IncidentReviewDecision> BuildReviews()
        =>
        [
            BuildReview(FirstIncidentId, 1),
            BuildReview(SecondIncidentId, 2),
        ];

    private static IncidentReviewDecision BuildReview(Guid incidentId, int minute)
        => new(
            ReviewId(minute),
            incidentId,
            ReviewVerdict.ConfirmedFraud,
            FraudClassification.Phishing,
            new DateTimeOffset(2026, 7, 23, 11, minute, 0, TimeSpan.Zero),
            "Validation interne confidentielle");

    private static Guid ReviewId(int minute)
        => Guid.Parse($"dddddddd-dddd-dddd-dddd-{minute:D12}");

    private static ShareableIocArtifact GetArtifact(
        ShareableIocExportResult result,
        string fileName)
        => Assert.Single(
            result.ShareablePackage.Artifacts,
            artifact => artifact.FileName == fileName);

    private static string ComputeSha256(string content)
        => Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(content)));
}
