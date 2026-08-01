using System.IO.Compression;
using System.Text;
using Frelon.Core;

namespace Frelon.Web.Tests;

/// <summary>Vérifie les téléchargements produits pour l'interface locale.</summary>
public sealed class IncidentExportServiceTests
{
    private readonly IncidentExportService _service = IncidentExportService.CreateDefault();

    [Theory]
    [InlineData("incident-json", "incident.json", "application/json; charset=utf-8")]
    [InlineData("report-markdown", "report.md", "text/markdown; charset=utf-8")]
    [InlineData("iocs-json", "iocs.json", "application/json; charset=utf-8")]
    [InlineData("iocs-csv", "iocs.csv", "text/csv; charset=utf-8")]
    public void TryCreate_FormatConnu_ProduitUnFichierUtf8SansBom(
        string format,
        string expectedFileName,
        string expectedContentType)
    {
        var created = _service.TryCreate(BuildIncident(), format, out var artifact);

        Assert.True(created);
        Assert.NotNull(artifact);
        Assert.Equal(expectedFileName, artifact.FileName);
        Assert.Equal(expectedContentType, artifact.ContentType);
        Assert.NotEmpty(artifact.Content);
        Assert.False(artifact.Content.AsSpan().StartsWith(Encoding.UTF8.Preamble));
    }

    [Fact]
    public void TryCreate_FormatInconnu_NeProduitRien()
    {
        var created = _service.TryCreate(BuildIncident(), "executable", out var artifact);

        Assert.False(created);
        Assert.Null(artifact);
    }

    [Fact]
    public void CreateBundle_RegroupeLesQuatreExportsAttendus()
    {
        var incident = BuildIncident();

        var bundle = _service.CreateBundle(incident);

        Assert.Equal($"frelon-{incident.IncidentId:N}.zip", bundle.FileName);
        Assert.Equal("application/zip", bundle.ContentType);
        using var archive = new ZipArchive(new MemoryStream(bundle.Content), ZipArchiveMode.Read);
        Assert.Equal(
            ["incident.json", "report.md", "iocs.json", "iocs.csv"],
            archive.Entries.Select(entry => entry.FullName));
    }

    [Fact]
    public void CreateBundle_ConserveLaNeutralisationCsvDesFormules()
    {
        var bundle = _service.CreateBundle(BuildIncident());

        using var archive = new ZipArchive(new MemoryStream(bundle.Content), ZipArchiveMode.Read);
        var csvEntry = Assert.Single(archive.Entries, entry => entry.FullName == "iocs.csv");
        using var reader = new StreamReader(csvEntry.Open(), Encoding.UTF8);
        var csv = reader.ReadToEnd();

        Assert.Contains("'=dangerous.example", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateBundle_AvecDecision_AjouteUnJsonDeRevueDistinct()
    {
        var incident = BuildIncident();
        var review = new IncidentReviewDecision(
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            incident.IncidentId,
            ReviewVerdict.ConfirmedFraud,
            FraudClassification.Phishing,
            new DateTimeOffset(2026, 7, 16, 11, 0, 0, TimeSpan.Zero),
            "Revue humaine");

        var bundle = _service.CreateBundle(incident, review);

        using var archive = new ZipArchive(new MemoryStream(bundle.Content), ZipArchiveMode.Read);
        Assert.Equal(
            ["incident.json", "report.md", "iocs.json", "iocs.csv", "review.json", "signalement.md"],
            archive.Entries.Select(entry => entry.FullName));
        var reviewEntry = Assert.Single(archive.Entries, entry => entry.FullName == "review.json");
        using var reader = new StreamReader(reviewEntry.Open(), Encoding.UTF8);
        var json = reader.ReadToEnd();
        Assert.Contains("\"verdict\": \"ConfirmedFraud\"", json, StringComparison.Ordinal);
        Assert.Contains("\"classification\": \"Phishing\"", json, StringComparison.Ordinal);
        var reportEntry = Assert.Single(archive.Entries, entry => entry.FullName == "signalement.md");
        using var reportReader = new StreamReader(reportEntry.Open(), Encoding.UTF8);
        var report = reportReader.ReadToEnd();
        Assert.Contains("Signalement Frelon validé humainement", report, StringComparison.Ordinal);
        Assert.Contains(review.ReviewId.ToString("D"), report, StringComparison.Ordinal);
    }

    [Fact]
    public void TryCreateValidatedReport_DecisionConfirmee_ProduitLeSignalementUtf8()
    {
        var incident = BuildIncident();
        var review = new IncidentReviewDecision(
            Guid.NewGuid(),
            incident.IncidentId,
            ReviewVerdict.ConfirmedFraud,
            FraudClassification.CredentialTheft,
            DateTimeOffset.UtcNow,
            "Formulaire frauduleux confirmé");

        var created = _service.TryCreateValidatedReport(incident, review, out var artifact);

        Assert.True(created);
        Assert.NotNull(artifact);
        Assert.Equal("signalement.md", artifact.FileName);
        Assert.Equal("text/markdown; charset=utf-8", artifact.ContentType);
        Assert.False(artifact.Content.AsSpan().StartsWith(Encoding.UTF8.Preamble));
    }

    [Fact]
    public void TryCreateValidatedReport_DecisionNonConclusive_NeProduitRien()
    {
        var incident = BuildIncident();
        var review = new IncidentReviewDecision(
            Guid.NewGuid(),
            incident.IncidentId,
            ReviewVerdict.Inconclusive,
            null,
            DateTimeOffset.UtcNow);

        var created = _service.TryCreateValidatedReport(incident, review, out var artifact);

        Assert.False(created);
        Assert.Null(artifact);
    }

    [Fact]
    public void CreateBundle_DecisionNonConfirmee_ConserveLaRevueSansSignalement()
    {
        var incident = BuildIncident();
        var review = new IncidentReviewDecision(
            Guid.NewGuid(),
            incident.IncidentId,
            ReviewVerdict.Benign,
            null,
            DateTimeOffset.UtcNow);

        var bundle = _service.CreateBundle(incident, review);

        using var archive = new ZipArchive(new MemoryStream(bundle.Content), ZipArchiveMode.Read);
        Assert.Contains(archive.Entries, entry => entry.FullName == "review.json");
        Assert.DoesNotContain(archive.Entries, entry => entry.FullName == "signalement.md");
    }

    private static FraudIncident BuildIncident()
        => new()
        {
            IncidentId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            CreatedAt = new DateTimeOffset(2026, 7, 16, 10, 0, 0, TimeSpan.Zero),
            Evidence = new EvidenceSource
            {
                FileName = "preuve.eml",
                Sha256 = new string('a', 64),
                ImportedAt = new DateTimeOffset(2026, 7, 16, 10, 0, 0, TimeSpan.Zero)
            },
            Identity = new MailIdentity { Subject = "Test" },
            Authentication = new AuthenticationAssessment(),
            Iocs =
            [
                new Ioc
                {
                    Type = IocType.Domain,
                    Value = "=dangerous.example",
                    Confidence = 0.5,
                    Source = "test"
                }
            ],
            Classification = FraudClassification.Unknown,
            RiskScore = new RiskScore { Value = 20, Level = RiskLevel.Low }
        };
}
