using Frelon.Core;
using Xunit;

namespace Frelon.Reports.Tests;

public sealed class BasicValidatedIncidentMarkdownReportWriterTests
{
    private readonly BasicValidatedIncidentMarkdownReportWriter _writer = new();

    [Fact]
    public void CanWrite_AutoriseSeulementUneFraudeConfirmeeEtCategorisee()
    {
        var incidentId = Guid.NewGuid();

        Assert.True(_writer.CanWrite(BuildDecision(
            incidentId,
            ReviewVerdict.ConfirmedFraud,
            FraudClassification.Phishing)));
        Assert.False(_writer.CanWrite(BuildDecision(
            incidentId,
            ReviewVerdict.Suspicious,
            FraudClassification.Suspicious)));
        Assert.False(_writer.CanWrite(BuildDecision(
            incidentId,
            ReviewVerdict.Benign,
            null)));
        Assert.False(_writer.CanWrite(null));
    }

    [Fact]
    public void Write_ProduitUnSignalementTraceEtDistingueLaDecisionHumaine()
    {
        var incident = BuildIncident();
        var decision = BuildDecision(
            incident.IncidentId,
            ReviewVerdict.ConfirmedFraud,
            FraudClassification.Phishing,
            "Page de connexion frauduleuse vérifiée");

        var report = _writer.Write(incident, decision);

        Assert.Contains("# Signalement Frelon validé humainement", report, StringComparison.Ordinal);
        Assert.Contains("Frelon ne l'a envoyé à aucun tiers", report, StringComparison.Ordinal);
        Assert.Contains("Verdict", report, StringComparison.Ordinal);
        Assert.Contains("Fraude confirmée", report, StringComparison.Ordinal);
        Assert.Contains("Catégorie retenue", report, StringComparison.Ordinal);
        Assert.Contains("Hameçonnage", report, StringComparison.Ordinal);
        Assert.Contains(decision.ReviewId.ToString("D"), report, StringComparison.Ordinal);
        Assert.Contains(incident.IncidentId.ToString("D"), report, StringComparison.Ordinal);
        Assert.Contains(new string('a', 64), report, StringComparison.Ordinal);
        Assert.Contains("Page de connexion frauduleuse vérifiée", report, StringComparison.Ordinal);
        Assert.Contains("Classification automatique", report, StringComparison.Ordinal);
        Assert.Contains("example.test", report, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_DecisionNonEligible_RefuseLeSignalement()
    {
        var incident = BuildIncident();
        var decision = BuildDecision(
            incident.IncidentId,
            ReviewVerdict.Suspicious,
            FraudClassification.Suspicious);

        var exception = Assert.Throws<InvalidOperationException>(() => _writer.Write(incident, decision));

        Assert.Contains("fraude confirmée", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Write_DecisionDUnAutreIncident_RefuseLeSignalement()
    {
        var incident = BuildIncident();
        var decision = BuildDecision(
            Guid.NewGuid(),
            ReviewVerdict.ConfirmedFraud,
            FraudClassification.Phishing);

        Assert.Throws<ArgumentException>(() => _writer.Write(incident, decision));
    }

    [Fact]
    public void Write_ValeursHostiles_RestentDuTexte()
    {
        var incident = BuildIncident() with
        {
            Identity = new MailIdentity
            {
                Subject = "<script>alert(1)</script> [ouvrir](https://evil.test)",
                From = "*faux*"
            }
        };
        var decision = BuildDecision(
            incident.IncidentId,
            ReviewVerdict.ConfirmedFraud,
            FraudClassification.Phishing,
            "# titre\r\n<script>danger</script>");

        var report = _writer.Write(incident, decision);

        Assert.DoesNotContain("<script>", report, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("[ouvrir](https://evil.test)", report, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", report, StringComparison.Ordinal);
        Assert.Contains("\\[ouvrir\\]\\(https://evil.test\\)", report, StringComparison.Ordinal);
        Assert.Contains("\\*faux\\*", report, StringComparison.Ordinal);
    }

    private static IncidentReviewDecision BuildDecision(
        Guid incidentId,
        ReviewVerdict verdict,
        FraudClassification? classification,
        string? notes = null)
        => new(
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            incidentId,
            verdict,
            classification,
            new DateTimeOffset(2026, 7, 16, 12, 0, 0, TimeSpan.Zero),
            notes);

    private static FraudIncident BuildIncident()
        => new()
        {
            IncidentId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            CreatedAt = new DateTimeOffset(2026, 7, 16, 10, 0, 0, TimeSpan.Zero),
            Evidence = new EvidenceSource
            {
                FileName = "preuve.eml",
                Sha256 = new string('a', 64),
                ImportedAt = new DateTimeOffset(2026, 7, 16, 10, 5, 0, TimeSpan.Zero)
            },
            Identity = new MailIdentity
            {
                Subject = "Connexion requise",
                From = "fraude@example.test",
                MessageId = "<message@example.test>"
            },
            Authentication = new AuthenticationAssessment
            {
                SpfResult = "fail",
                DkimResult = "none",
                DmarcResult = "fail",
                IsSuspicious = true
            },
            Iocs =
            [
                new Ioc
                {
                    Type = IocType.Domain,
                    Value = "example.test",
                    Confidence = 0.8,
                    Source = "test"
                }
            ],
            Attachments =
            [
                new AttachmentIndicator
                {
                    FileName = "facture.pdf.exe",
                    ContentType = "application/octet-stream",
                    Sha256 = new string('b', 64),
                    IsSuspicious = true
                }
            ],
            Classification = FraudClassification.Unknown,
            RiskScore = new RiskScore
            {
                Value = 70,
                Level = RiskLevel.High,
                Reasons = ["Authentification incohérente"]
            }
        };
}
