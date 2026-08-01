using Xunit;

namespace Frelon.Core.Tests;

public class FraudIncidentTests
{
    private static FraudIncident BuildMinimalIncident() => new()
    {
        IncidentId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        CreatedAt = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
        Evidence = new EvidenceSource { FileName = "suspicious.eml" },
        Identity = new MailIdentity(),
        Authentication = new AuthenticationAssessment(),
        Classification = FraudClassification.Phishing,
        RiskScore = new RiskScore { Value = 75.0, Level = RiskLevel.High }
    };

    [Fact]
    public void FraudIncident_PeutEtreInstancieAvecLesValeursRequises()
    {
        var incident = BuildMinimalIncident();

        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), incident.IncidentId);
        Assert.Equal(FraudClassification.Phishing, incident.Classification);
        Assert.Equal(RiskLevel.High, incident.RiskScore.Level);
        Assert.Equal("suspicious.eml", incident.Evidence.FileName);
    }

    [Fact]
    public void FraudIncident_CollectionsSontVidesParDefaut()
    {
        var incident = BuildMinimalIncident();

        Assert.Empty(incident.ReceivedChain);
        Assert.Empty(incident.Urls);
        Assert.Empty(incident.Attachments);
        Assert.Empty(incident.Iocs);
        Assert.Empty(incident.RecommendedActions);
    }

    [Fact]
    public void RiskScore_PeutRepresenterUnNiveauDeRisqueSimple()
    {
        var score = new RiskScore
        {
            Value = 90.0,
            Level = RiskLevel.Critical,
            Reasons = ["Échec d'authentification SPF", "URL suspecte détectée"]
        };

        Assert.Equal(90.0, score.Value);
        Assert.Equal(RiskLevel.Critical, score.Level);
        Assert.Equal(2, score.Reasons.Count);
    }

    [Fact]
    public void RecommendedAction_NecessiteValidationHumaine_QuandIndique()
    {
        var action = new RecommendedAction
        {
            Type = RecommendedActionType.PrepareAbuseReport,
            Label = "Préparer un signalement abuse",
            Description = "Signaler le domaine frauduleux à l'hébergeur.",
            RequiresHumanValidation = true
        };

        Assert.True(action.RequiresHumanValidation);
        Assert.Equal(RecommendedActionType.PrepareAbuseReport, action.Type);
    }

    [Fact]
    public void Ioc_PeutRepresenterUnDomaine()
    {
        var ioc = new Ioc
        {
            Type = IocType.Domain,
            Value = "evil-domain.example.com",
            Confidence = 0.9
        };

        Assert.Equal(IocType.Domain, ioc.Type);
        Assert.Equal("evil-domain.example.com", ioc.Value);
    }

    [Fact]
    public void Ioc_PeutRepresenterUneUrl()
    {
        var ioc = new Ioc
        {
            Type = IocType.Url,
            Value = "http://evil-domain.example.com/login",
            Confidence = 0.85
        };

        Assert.Equal(IocType.Url, ioc.Type);
        Assert.Equal(0.85, ioc.Confidence);
    }

    [Fact]
    public void Ioc_PeutRepresenterUnHash()
    {
        var ioc = new Ioc
        {
            Type = IocType.Hash,
            Value = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            Confidence = 1.0
        };

        Assert.Equal(IocType.Hash, ioc.Type);
        Assert.Equal(1.0, ioc.Confidence);
    }
}
