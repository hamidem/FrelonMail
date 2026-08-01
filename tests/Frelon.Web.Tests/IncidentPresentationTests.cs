using Frelon.Core;

namespace Frelon.Web.Tests;

/// <summary>Vérifie la projection sûre du métier vers l'interface.</summary>
public sealed class IncidentPresentationTests
{
    [Fact]
    public void FromIncident_ProjetteLaSyntheseSansValeurBruteDAuthentification()
    {
        var incident = BuildIncident();

        var result = IncidentPresentation.FromIncident(incident);

        Assert.Equal(incident.IncidentId, result.IncidentId);
        Assert.Equal("Facture urgente", result.Subject);
        Assert.Equal(72, result.RiskValue);
        Assert.Equal(RiskLevel.High, result.RiskLevel);
        Assert.Equal("Traitez ce message comme suspect jusqu'à vérification", result.Guidance.Headline);
        Assert.Contains("serveur autorisé", Assert.Single(result.Guidance.KeyObservations));
        Assert.Contains(
            result.Guidance.RecommendedActions,
            action => action.Contains("référent sécurité", StringComparison.Ordinal));
        Assert.Equal(FraudClassification.Suspicious, result.ClassificationAssessment.Classification);
        Assert.Equal(ClassificationConfidence.Low, result.ClassificationAssessment.Confidence);
        Assert.Equal("fail", result.Authentication.Spf);
        Assert.Equal(2, result.UrlCount);
        Assert.Empty(result.DefensiveFindings);
        Assert.Equal(IocType.Domain, Assert.Single(result.Iocs).Type);
        Assert.DoesNotContain("valeur brute sensible", result.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void FromIncident_CopieLesCollectionsExposees()
    {
        var incident = BuildIncident();

        var result = IncidentPresentation.FromIncident(incident);

        Assert.NotSame(incident.RiskScore.Reasons, result.RiskReasons);
        Assert.NotSame(incident.Iocs, result.Iocs);
    }

    [Fact]
    public void FromIncident_ExposeSeulementLesReglesDefensivesDeclenchees()
    {
        var incident = BuildIncident() with
        {
            Urls =
            [
                new UrlIndicator
                {
                    RawValue = "http://203.0.113.10/login",
                    IsSuspicious = true,
                    Reasons = ["Adresse IP brute"]
                },
                new UrlIndicator { RawValue = "https://example.test" }
            ]
        };

        var result = IncidentPresentation.FromIncident(incident);

        var finding = Assert.Single(result.DefensiveFindings);
        Assert.Equal("Url", finding.Kind);
        Assert.Equal("http://203.0.113.10/login", finding.Value);
        Assert.Equal(["Adresse IP brute"], finding.Reasons);
        Assert.NotSame(incident.Urls[0].Reasons, finding.Reasons);
    }

    [Fact]
    public void FromIncident_IncidentNull_LeveArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => IncidentPresentation.FromIncident(null!));
    }

    [Fact]
    public void FromIncident_SynthesePrioriseLesRisquesDirectsSansMasquerLesRaisonsTechniques()
    {
        var reasons = new[]
        {
            BasicIncidentRiskScorer.SpfFailReason,
            BasicIncidentRiskScorer.DkimFailReason,
            BasicIncidentRiskScorer.DmarcFailReason,
            BasicIncidentRiskScorer.SuspiciousUrlReason,
            BasicIncidentRiskScorer.SuspiciousAttachmentReason
        };
        var incident = BuildIncident() with
        {
            RiskScore = new RiskScore
            {
                Value = 100,
                Level = RiskLevel.Critical,
                Reasons = reasons
            }
        };

        var result = IncidentPresentation.FromIncident(incident);

        Assert.Equal(3, result.Guidance.KeyObservations.Count);
        Assert.Contains("pièce jointe", result.Guidance.KeyObservations[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("lien", result.Guidance.KeyObservations[1], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Plusieurs vérifications", result.Guidance.KeyObservations[2], StringComparison.Ordinal);
        Assert.DoesNotContain(
            result.Guidance.KeyObservations,
            observation => observation.Contains("SPF", StringComparison.Ordinal)
                || observation.Contains("DKIM", StringComparison.Ordinal)
                || observation.Contains("DMARC", StringComparison.Ordinal));
        Assert.Equal(reasons, result.RiskReasons);
    }

    [Fact]
    public void FromIncident_ScoreIndetermineNePresenteJamaisLeMessageCommeSur()
    {
        var incident = BuildIncident() with
        {
            RiskScore = new RiskScore
            {
                Value = 0,
                Level = RiskLevel.Unknown,
                Reasons = []
            }
        };

        var result = IncidentPresentation.FromIncident(incident);

        Assert.Contains("feu vert", result.Guidance.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ne prouve pas", Assert.Single(result.Guidance.KeyObservations), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, result.Guidance.RecommendedActions.Count);
    }

    private static FraudIncident BuildIncident()
        => new()
        {
            IncidentId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            CreatedAt = new DateTimeOffset(2026, 7, 16, 9, 0, 0, TimeSpan.Zero),
            Evidence = new EvidenceSource { FileName = "facture.eml", Sha256 = "abc" },
            Identity = new MailIdentity { Subject = "Facture urgente", From = "sender@example.test" },
            Authentication = new AuthenticationAssessment
            {
                SpfResult = "fail",
                DkimResult = "none",
                DmarcResult = "fail",
                AuthenticationResultsRaw = "valeur brute sensible",
                IsSuspicious = true
            },
            Urls =
            [
                new UrlIndicator { RawValue = "https://one.example" },
                new UrlIndicator { RawValue = "https://two.example" }
            ],
            Iocs =
            [
                new Ioc { Type = IocType.Domain, Value = "one.example", Confidence = 0.8 }
            ],
            Classification = FraudClassification.Unknown,
            ClassificationAssessment = new ClassificationAssessment(
                FraudClassification.Suspicious,
                ClassificationConfidence.Low,
                ["Authentification incohérente"]),
            RiskScore = new RiskScore
            {
                Value = 72,
                Level = RiskLevel.High,
                Reasons = [BasicIncidentRiskScorer.SpfFailReason]
            }
        };
}
