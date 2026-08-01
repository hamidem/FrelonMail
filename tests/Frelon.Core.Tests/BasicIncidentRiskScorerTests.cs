using Xunit;

namespace Frelon.Core.Tests;

/// <summary>
/// Tests du <see cref="BasicIncidentRiskScorer"/>.
/// </summary>
public class BasicIncidentRiskScorerTests
{
    private static BasicIncidentRiskScorer CreateScorer() => new();

    private static FraudIncident BuildIncident(
        AuthenticationAssessment? authentication = null,
        IReadOnlyList<UrlIndicator>? urls = null,
        IReadOnlyList<AttachmentIndicator>? attachments = null,
        IReadOnlyList<Ioc>? iocs = null,
        RiskScore? riskScore = null,
        FraudClassification classification = FraudClassification.Unknown)
        => new()
        {
            IncidentId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            CreatedAt = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Evidence = new EvidenceSource { FileName = "suspicious.eml" },
            Identity = new MailIdentity(),
            Authentication = authentication ?? new AuthenticationAssessment(),
            Urls = urls ?? [],
            Attachments = attachments ?? [],
            Iocs = iocs ?? [],
            Classification = classification,
            RiskScore = riskScore ?? new RiskScore { Value = 0, Level = RiskLevel.Unknown },
        };

    private static UrlIndicator CreateUrl(bool isSuspicious)
        => new()
        {
            RawValue = "https://example.test",
            IsSuspicious = isSuspicious,
        };

    private static AttachmentIndicator CreateAttachment(bool isSuspicious)
        => new()
        {
            FileName = "piece-jointe.bin",
            IsSuspicious = isSuspicious,
        };

    [Fact]
    public void Score_LeveArgumentNullExceptionSiIncidentEstNull()
    {
        var scorer = CreateScorer();

        Assert.Throws<ArgumentNullException>(() => scorer.Score(null!));
    }

    [Fact]
    public void Score_AucunSignalReconnuProduitZero()
    {
        var scorer = CreateScorer();

        RiskScore score = scorer.Score(BuildIncident());

        Assert.Equal(0, score.Value);
    }

    [Fact]
    public void Score_AucunSignalReconnuProduitUnknown()
    {
        var scorer = CreateScorer();

        RiskScore score = scorer.Score(BuildIncident());

        Assert.Equal(RiskLevel.Unknown, score.Level);
    }

    [Fact]
    public void Score_AucunSignalReconnuProduitUneListeDeRaisonsVide()
    {
        var scorer = CreateScorer();

        RiskScore score = scorer.Score(BuildIncident());

        Assert.Empty(score.Reasons);
    }

    [Fact]
    public void Score_SpfFailProduitQuinzePoints()
    {
        var scorer = CreateScorer();

        RiskScore score = scorer.Score(BuildIncident(authentication: new AuthenticationAssessment { SpfResult = "fail" }));

        Assert.Equal(15, score.Value);
    }

    [Fact]
    public void Score_SpfFailProduitLow()
    {
        var scorer = CreateScorer();

        RiskScore score = scorer.Score(BuildIncident(authentication: new AuthenticationAssessment { SpfResult = "fail" }));

        Assert.Equal(RiskLevel.Low, score.Level);
    }

    [Fact]
    public void Score_SpfFailProduitLaRaisonExacte()
    {
        var scorer = CreateScorer();

        RiskScore score = scorer.Score(BuildIncident(authentication: new AuthenticationAssessment { SpfResult = "fail" }));

        Assert.Equal(["Échec d'authentification SPF"], score.Reasons);
    }

    [Fact]
    public void Score_DkimFailProduitQuinzePoints()
    {
        var scorer = CreateScorer();

        RiskScore score = scorer.Score(BuildIncident(authentication: new AuthenticationAssessment { DkimResult = "fail" }));

        Assert.Equal(15, score.Value);
    }

    [Fact]
    public void Score_DkimFailProduitLaRaisonExacte()
    {
        var scorer = CreateScorer();

        RiskScore score = scorer.Score(BuildIncident(authentication: new AuthenticationAssessment { DkimResult = "fail" }));

        Assert.Equal([BasicIncidentRiskScorer.DkimFailReason], score.Reasons);
    }

    [Fact]
    public void Score_DmarcFailProduitTrentePoints()
    {
        var scorer = CreateScorer();

        RiskScore score = scorer.Score(BuildIncident(authentication: new AuthenticationAssessment { DmarcResult = "fail" }));

        Assert.Equal(30, score.Value);
    }

    [Fact]
    public void Score_DmarcFailProduitMedium()
    {
        var scorer = CreateScorer();

        RiskScore score = scorer.Score(BuildIncident(authentication: new AuthenticationAssessment { DmarcResult = "fail" }));

        Assert.Equal(RiskLevel.Medium, score.Level);
    }

    [Fact]
    public void Score_DmarcFailProduitLaRaisonExacte()
    {
        var scorer = CreateScorer();

        RiskScore score = scorer.Score(BuildIncident(authentication: new AuthenticationAssessment { DmarcResult = "fail" }));

        Assert.Equal([BasicIncidentRiskScorer.DmarcFailReason], score.Reasons);
    }

    [Theory]
    [InlineData("FAIL")]
    [InlineData("Fail")]
    [InlineData("  fail  ")]
    public void Score_FailEstReconnuSansSensibiliteALaCasseEtAvecEspaces(string value)
    {
        var scorer = CreateScorer();

        RiskScore score = scorer.Score(BuildIncident(authentication: new AuthenticationAssessment { SpfResult = value }));

        Assert.Equal(15, score.Value);
    }

    [Theory]
    [InlineData("pass")]
    [InlineData("none")]
    [InlineData("neutral")]
    [InlineData("softfail")]
    [InlineData("temperror")]
    [InlineData("permerror")]
    public void Score_LesValeursNonFailNeRapportentAucunPoint(string value)
    {
        var scorer = CreateScorer();

        RiskScore score = scorer.Score(BuildIncident(authentication: new AuthenticationAssessment { SpfResult = value }));

        Assert.Equal(0, score.Value);
    }

    [Fact]
    public void Score_UneUrlExplicitementSuspecteAjouteVingtPoints()
    {
        var scorer = CreateScorer();

        RiskScore score = scorer.Score(BuildIncident(urls: [CreateUrl(true)]));

        Assert.Equal(20, score.Value);
    }

    [Fact]
    public void Score_PlusieursUrlsSuspectesNAjoutentQuUneSeuleContribution()
    {
        var scorer = CreateScorer();

        RiskScore score = scorer.Score(BuildIncident(urls: [CreateUrl(true), CreateUrl(true)]));

        Assert.Equal(20, score.Value);
    }

    [Fact]
    public void Score_PlusieursUrlsSuspectesNAjoutentQuUneSeuleRaison()
    {
        var scorer = CreateScorer();

        RiskScore score = scorer.Score(BuildIncident(urls: [CreateUrl(true), CreateUrl(true)]));

        Assert.Equal([BasicIncidentRiskScorer.SuspiciousUrlReason], score.Reasons);
    }

    [Fact]
    public void Score_UneUrlNonSuspecteNAjouteAucunPoint()
    {
        var scorer = CreateScorer();

        RiskScore score = scorer.Score(BuildIncident(urls: [CreateUrl(false)]));

        Assert.Equal(0, score.Value);
    }

    [Fact]
    public void Score_UnePieceJointeExplicitementSuspecteAjouteTrentePoints()
    {
        var scorer = CreateScorer();

        RiskScore score = scorer.Score(BuildIncident(attachments: [CreateAttachment(true)]));

        Assert.Equal(30, score.Value);
    }

    [Fact]
    public void Score_PlusieursPiecesJointesSuspectesNAjoutentQuUneSeuleContribution()
    {
        var scorer = CreateScorer();

        RiskScore score = scorer.Score(BuildIncident(attachments: [CreateAttachment(true), CreateAttachment(true)]));

        Assert.Equal(30, score.Value);
    }

    [Fact]
    public void Score_PlusieursPiecesJointesSuspectesNAjoutentQuUneSeuleRaison()
    {
        var scorer = CreateScorer();

        RiskScore score = scorer.Score(BuildIncident(attachments: [CreateAttachment(true), CreateAttachment(true)]));

        Assert.Equal([BasicIncidentRiskScorer.SuspiciousAttachmentReason], score.Reasons);
    }

    [Fact]
    public void Score_UnePieceJointeNonSuspecteNAjouteAucunPoint()
    {
        var scorer = CreateScorer();

        RiskScore score = scorer.Score(BuildIncident(attachments: [CreateAttachment(false)]));

        Assert.Equal(0, score.Value);
    }

    [Fact]
    public void Score_SpfFailDkimFailEtDmarcFailProduisentSoixantePoints()
    {
        var scorer = CreateScorer();

        RiskScore score = scorer.Score(
            BuildIncident(authentication: new AuthenticationAssessment
            {
                SpfResult = "fail",
                DkimResult = "fail",
                DmarcResult = "fail",
            }));

        Assert.Equal(60, score.Value);
    }

    [Fact]
    public void Score_SoixantePointsCorrespondentAuNiveauHigh()
    {
        var scorer = CreateScorer();

        RiskScore score = scorer.Score(
            BuildIncident(authentication: new AuthenticationAssessment
            {
                SpfResult = "fail",
                DkimResult = "fail",
                DmarcResult = "fail",
            }));

        Assert.Equal(RiskLevel.High, score.Level);
    }

    [Fact]
    public void Score_UnScoreBrutDeCentDixEstPlafonneACent()
    {
        var scorer = CreateScorer();

        RiskScore score = scorer.Score(
            BuildIncident(
                authentication: new AuthenticationAssessment
                {
                    SpfResult = "fail",
                    DkimResult = "fail",
                    DmarcResult = "fail",
                },
                urls: [CreateUrl(true)],
                attachments: [CreateAttachment(true)]));

        Assert.Equal(100, score.Value);
    }

    [Fact]
    public void Score_VingtPlusTrentePointsCorrespondentAuNiveauHigh()
    {
        var scorer = CreateScorer();

        RiskScore score = scorer.Score(
            BuildIncident(
                urls: [CreateUrl(true)],
                attachments: [CreateAttachment(true)]));

        Assert.Equal(50, score.Value);
        Assert.Equal(RiskLevel.High, score.Level);
    }

    [Fact]
    public void Score_QuinzePlusTrentePlusTrentePointsCorrespondentAuNiveauCritical()
    {
        var scorer = CreateScorer();

        RiskScore score = scorer.Score(
            BuildIncident(
                authentication: new AuthenticationAssessment
                {
                    SpfResult = "fail",
                    DmarcResult = "fail",
                },
                attachments: [CreateAttachment(true)]));

        Assert.Equal(75, score.Value);
        Assert.Equal(RiskLevel.Critical, score.Level);
    }

    [Fact]
    public void Score_CentPointsCorrespondentAuNiveauCritical()
    {
        var scorer = CreateScorer();

        RiskScore score = scorer.Score(
            BuildIncident(
                authentication: new AuthenticationAssessment
                {
                    SpfResult = "fail",
                    DkimResult = "fail",
                    DmarcResult = "fail",
                },
                urls: [CreateUrl(true)],
                attachments: [CreateAttachment(true)]));

        Assert.Equal(RiskLevel.Critical, score.Level);
    }

    [Fact]
    public void Score_LePlafonnementConserveLesCinqRaisons()
    {
        var scorer = CreateScorer();

        RiskScore score = scorer.Score(
            BuildIncident(
                authentication: new AuthenticationAssessment
                {
                    SpfResult = "fail",
                    DkimResult = "fail",
                    DmarcResult = "fail",
                },
                urls: [CreateUrl(true)],
                attachments: [CreateAttachment(true)]));

        Assert.Collection(
            score.Reasons,
            reason => Assert.Equal("Échec d'authentification SPF", reason),
            reason => Assert.Equal("Échec d'authentification DKIM", reason),
            reason => Assert.Equal("Échec d'authentification DMARC", reason),
            reason => Assert.Equal("URL suspecte détectée", reason),
            reason => Assert.Equal("Pièce jointe suspecte détectée", reason));
    }

    [Fact]
    public void Score_LOrdreDesRaisonsEstStrictementSpfDkimDmarcUrlPieceJointe()
    {
        var scorer = CreateScorer();

        RiskScore score = scorer.Score(
            BuildIncident(
                authentication: new AuthenticationAssessment
                {
                    SpfResult = "fail",
                    DkimResult = "fail",
                    DmarcResult = "fail",
                },
                urls: [CreateUrl(true)],
                attachments: [CreateAttachment(true)]));

        Assert.Equal(
            [
                BasicIncidentRiskScorer.SpfFailReason,
                BasicIncidentRiskScorer.DkimFailReason,
                BasicIncidentRiskScorer.DmarcFailReason,
                BasicIncidentRiskScorer.SuspiciousUrlReason,
                BasicIncidentRiskScorer.SuspiciousAttachmentReason,
            ],
            score.Reasons);
    }

    [Fact]
    public void Score_LaPresenceDUrlDomainOuHashSeulsNeChangePasLeScore()
    {
        var scorer = CreateScorer();
        var incident = BuildIncident(
            iocs:
            [
                new Ioc { Type = IocType.Url, Value = "https://example.test" },
                new Ioc { Type = IocType.Domain, Value = "example.test" },
                new Ioc { Type = IocType.Hash, Value = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef" },
            ]);

        RiskScore score = scorer.Score(incident);

        Assert.Equal(0, score.Value);
    }

    [Fact]
    public void Score_UnIocHashAvecUneConfianceMaximaleNeChangePasLeScore()
    {
        var scorer = CreateScorer();
        var incident = BuildIncident(
            iocs:
            [
                new Ioc
                {
                    Type = IocType.Hash,
                    Value = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                    Confidence = 1.0,
                },
            ]);

        RiskScore score = scorer.Score(incident);

        Assert.Equal(0, score.Value);
    }

    [Fact]
    public void Score_UnRiskScorePreexistantEleveEstIgnore()
    {
        var scorer = CreateScorer();
        var incident = BuildIncident(riskScore: new RiskScore { Value = 99, Level = RiskLevel.Critical, Reasons = ["ancien"] });

        RiskScore score = scorer.Score(incident);

        Assert.Equal(0, score.Value);
    }

    [Fact]
    public void Score_LaClassificationExistanteEstIgnoree()
    {
        var scorer = CreateScorer();
        var incident = BuildIncident(classification: FraudClassification.Phishing);

        RiskScore score = scorer.Score(incident);

        Assert.Equal(0, score.Value);
    }

    [Fact]
    public void Score_NeModifiePasLIncidentFourni()
    {
        var scorer = CreateScorer();
        var incident = BuildIncident(
            authentication: new AuthenticationAssessment { SpfResult = "fail" },
            urls: [CreateUrl(true)],
            attachments: [CreateAttachment(true)],
            iocs:
            [
                new Ioc { Type = IocType.Url, Value = "https://example.test" },
            ],
            riskScore: new RiskScore { Value = 7, Level = RiskLevel.Low, Reasons = ["original"] },
            classification: FraudClassification.Unknown);

        _ = scorer.Score(incident);

        Assert.Equal(7, incident.RiskScore.Value);
        Assert.Equal(RiskLevel.Low, incident.RiskScore.Level);
        Assert.Equal(["original"], incident.RiskScore.Reasons);
        Assert.Equal(FraudClassification.Unknown, incident.Classification);
        Assert.Equal("fail", incident.Authentication.SpfResult);
        Assert.Single(incident.Urls);
        Assert.Single(incident.Attachments);
        Assert.Single(incident.Iocs);
    }
}
