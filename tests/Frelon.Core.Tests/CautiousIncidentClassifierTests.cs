using Frelon.Core;
using Xunit;

namespace Frelon.Core.Tests;

public sealed class CautiousIncidentClassifierTests
{
    private readonly CautiousIncidentClassifier _classifier = new();

    [Fact]
    public void Assess_SansSignalSuffisant_NeSuggereRien()
    {
        var result = _classifier.Assess(BuildIncident());

        Assert.Equal(ClassificationAssessment.None, result);
    }

    [Fact]
    public void Assess_PlusieursEchecsAuthentification_SuggereSeulementSuspicious()
    {
        var incident = BuildIncident() with
        {
            Authentication = new AuthenticationAssessment
            {
                SpfResult = "fail",
                DkimResult = "FAIL"
            }
        };

        var result = _classifier.Assess(incident);

        Assert.Equal(FraudClassification.Suspicious, result.Classification);
        Assert.Equal(ClassificationConfidence.Low, result.Confidence);
        Assert.Contains(CautiousIncidentClassifier.AuthenticationFailuresReason, result.Reasons);
        Assert.Equal(FraudClassification.Unknown, incident.Classification);
    }

    [Fact]
    public void Assess_UrlSuspecteEtEchecAuthentification_SuggerePhishing()
    {
        var incident = BuildIncident() with
        {
            Authentication = new AuthenticationAssessment { DmarcResult = "fail" },
            Urls = [new UrlIndicator { RawValue = "https://example.test", IsSuspicious = true }]
        };

        var result = _classifier.Assess(incident);

        Assert.Equal(FraudClassification.Phishing, result.Classification);
        Assert.Equal(ClassificationConfidence.Medium, result.Confidence);
        Assert.Equal(2, result.Reasons.Count);
    }

    [Fact]
    public void Assess_PieceJointeSuspecteSeule_SuggereMalware()
    {
        var incident = BuildIncident() with
        {
            Attachments = [new AttachmentIndicator { FileName = "charge.exe", IsSuspicious = true }]
        };

        var result = _classifier.Assess(incident);

        Assert.Equal(FraudClassification.Malware, result.Classification);
        Assert.Equal(ClassificationConfidence.Medium, result.Confidence);
    }

    [Fact]
    public void Assess_SignauxHeterogenes_ResteSurSuspicious()
    {
        var incident = BuildIncident() with
        {
            Urls = [new UrlIndicator { RawValue = "https://example.test", IsSuspicious = true }],
            Attachments = [new AttachmentIndicator { FileName = "charge.exe", IsSuspicious = true }]
        };

        var result = _classifier.Assess(incident);

        Assert.Equal(FraudClassification.Suspicious, result.Classification);
        Assert.Equal(ClassificationConfidence.Medium, result.Confidence);
        Assert.Contains(CautiousIncidentClassifier.MixedSignalsReason, result.Reasons);
    }

    [Fact]
    public void Assess_AuthentificationSignaleeIncoherente_ExpliqueLaPisteSansInventerDEchec()
    {
        var incident = BuildIncident() with
        {
            Authentication = new AuthenticationAssessment { IsSuspicious = true }
        };

        var result = _classifier.Assess(incident);

        Assert.Equal(FraudClassification.Suspicious, result.Classification);
        Assert.Equal([CautiousIncidentClassifier.SuspiciousAuthenticationReason], result.Reasons);
    }

    [Fact]
    public void Assess_IncidentNull_LeveArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _classifier.Assess(null!));
    }

    private static FraudIncident BuildIncident()
        => new()
        {
            IncidentId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            Evidence = new EvidenceSource { FileName = "preuve.eml" },
            Identity = new MailIdentity(),
            Authentication = new AuthenticationAssessment(),
            Classification = FraudClassification.Unknown,
            RiskScore = new RiskScore { Value = 0, Level = RiskLevel.Unknown }
        };
}
