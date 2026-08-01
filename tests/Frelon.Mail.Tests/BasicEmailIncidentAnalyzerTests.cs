using System.Linq;
using System.Text;
using Frelon.Core;
using MimeKit;
using Xunit;

namespace Frelon.Mail.Tests;

/// <summary>
/// Tests de <see cref="BasicEmailIncidentAnalyzer"/>.
/// </summary>
public class BasicEmailIncidentAnalyzerTests
{
    private sealed class FixedIncidentRiskScorer : IIncidentRiskScorer
    {
        public RiskScore Score(FraudIncident incident)
            => new()
            {
                Value = 42,
                Level = RiskLevel.Medium,
                Reasons = ["score fixe de test"],
            };
    }

    // ── Fixture ──────────────────────────────────────────────────────────────

    private const string MinimalEml =
        "Return-Path: <bounce@example.net>\r\n" +
        "Received: from first.example by mx.example.org\r\n" +
        "Received: from second.example by first.example\r\n" +
        "Authentication-Results: mx.example.org; spf=pass smtp.mailfrom=example.net; dkim=fail; dmarc=none\r\n" +
        "From: Fake Support <support@example.net>\r\n" +
        "Reply-To: reply@example.net\r\n" +
        "Message-ID: <abc123@example.net>\r\n" +
        "Subject: Suspicious login attempt\r\n" +
        "\r\n" +
        "Hello.\r\n";

    private const string EmlWithUrl =
        "From: Fake Support <support@example.net>\r\n" +
        "Subject: Suspicious login attempt\r\n" +
        "\r\n" +
        "Bonjour,\r\n" +
        "Veuillez consulter https://evil.example.com/login.\r\n";

    private static Stream ToStream(string content)
        => new MemoryStream(Encoding.UTF8.GetBytes(content));

    private static BasicEmailIncidentAnalyzer CreateAnalyzer()
        => CreateAnalyzer(new BasicEmailParser());

    private static BasicEmailIncidentAnalyzer CreateAnalyzer(IEmailParser parser)
        => CreateAnalyzer(parser, new BasicIncidentRiskScorer());

    private static BasicEmailIncidentAnalyzer CreateAnalyzer(IEmailParser parser, IIncidentRiskScorer riskScorer)
        => new(
            parser,
            new BasicEmailHeaderAnalyzer(),
            new BasicEmailUrlExtractor(),
            new BasicUrlIocExtractor(),
            new BasicEmailAttachmentAnalyzer(),
            new BasicAttachmentIocExtractor(),
            riskScorer,
            new CautiousIncidentClassifier());

    private static MimeMessage CreateMessageWithMimeAttachment()
    {
        var message = new MimeMessage();
        message.Subject = "Facture";
        message.Body = new Multipart("mixed")
        {
            new TextPart("plain") { Text = "Bonjour" },
            new MimePart
            {
                Content = new MimeContent(new MemoryStream(Encoding.UTF8.GetBytes("contenu factice"))),
                ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                ContentTransferEncoding = ContentEncoding.Base64,
                FileName = "facture.txt"
            }
        };

        return message;
    }

    private static MimeMessage CreateMessageWithExecutableAttachment()
    {
        var message = new MimeMessage();
        message.Subject = "Facture";
        message.Body = new Multipart("mixed")
        {
            new TextPart("plain") { Text = "Bonjour" },
            new MimePart("application", "octet-stream")
            {
                Content = new MimeContent(new MemoryStream(Encoding.UTF8.GetBytes("contenu factice"))),
                ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                ContentTransferEncoding = ContentEncoding.Base64,
                FileName = "facture.pdf.exe"
            }
        };

        return message;
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AnalyzeAsync_RetourneUnFraudIncidentNonNull()
    {
        var analyzer = CreateAnalyzer();

        FraudIncident incident = await analyzer.AnalyzeAsync(ToStream(MinimalEml), null, TestContext.Current.CancellationToken);

        Assert.NotNull(incident);
    }

    [Fact]
    public async Task AnalyzeAsync_IncidentIdEstNonVide()
    {
        var analyzer = CreateAnalyzer();

        FraudIncident incident = await analyzer.AnalyzeAsync(ToStream(MinimalEml), null, TestContext.Current.CancellationToken);

        Assert.NotEqual(Guid.Empty, incident.IncidentId);
    }

    [Fact]
    public async Task AnalyzeAsync_CreatedAtEstRenseigne()
    {
        var avant    = DateTimeOffset.UtcNow;
        var analyzer = CreateAnalyzer();

        FraudIncident incident = await analyzer.AnalyzeAsync(ToStream(MinimalEml), null, TestContext.Current.CancellationToken);

        Assert.True(incident.CreatedAt >= avant);
        Assert.True(incident.CreatedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task AnalyzeAsync_IdentiteMailEstReporteeDepuisLesHeaders()
    {
        var analyzer = CreateAnalyzer();

        FraudIncident incident = await analyzer.AnalyzeAsync(ToStream(MinimalEml), null, TestContext.Current.CancellationToken);

        Assert.Equal("Fake Support <support@example.net>", incident.Identity.From);
        Assert.Equal("reply@example.net",                  incident.Identity.ReplyTo);
        Assert.Equal("<bounce@example.net>",               incident.Identity.ReturnPath);
        Assert.Equal("<abc123@example.net>",               incident.Identity.MessageId);
        Assert.Equal("Suspicious login attempt",           incident.Identity.Subject);
    }

    [Fact]
    public async Task AnalyzeAsync_AuthenticationEstReporteeDepuisLesHeaders()
    {
        var analyzer = CreateAnalyzer();

        FraudIncident incident = await analyzer.AnalyzeAsync(ToStream(MinimalEml), null, TestContext.Current.CancellationToken);

        Assert.NotNull(incident.Authentication.AuthenticationResultsRaw);
        Assert.Equal("pass", incident.Authentication.SpfResult);
        Assert.Equal("fail", incident.Authentication.DkimResult);
        Assert.Equal("none", incident.Authentication.DmarcResult);
    }

    [Fact]
    public async Task AnalyzeAsync_ReceivedChainEstReporteeDepuisLesHeaders()
    {
        var analyzer = CreateAnalyzer();

        FraudIncident incident = await analyzer.AnalyzeAsync(ToStream(MinimalEml), null, TestContext.Current.CancellationToken);

        Assert.Equal(2, incident.ReceivedChain.Count);
    }

    [Fact]
    public async Task AnalyzeAsync_CollectionsUrls_Attachments_Iocs_RecommendedActionsVidees()
    {
        var analyzer = CreateAnalyzer();

        FraudIncident incident = await analyzer.AnalyzeAsync(ToStream(MinimalEml), null, TestContext.Current.CancellationToken);

        Assert.Empty(incident.Urls);
        Assert.Empty(incident.Attachments);
        Assert.Empty(incident.Iocs);
        Assert.Empty(incident.RecommendedActions);
    }

    [Fact]
    public async Task AnalyzeAsync_ClassificationParDefautEstUnknown()
    {
        var analyzer = CreateAnalyzer();

        FraudIncident incident = await analyzer.AnalyzeAsync(ToStream(MinimalEml), null, TestContext.Current.CancellationToken);

        Assert.Equal(FraudClassification.Unknown, incident.Classification);
    }

    [Fact]
    public async Task AnalyzeAsync_ScoreDeRisqueParDefautPrendEnCompteLeDKIMFail()
    {
        var analyzer = CreateAnalyzer();

        FraudIncident incident = await analyzer.AnalyzeAsync(ToStream(MinimalEml), null, TestContext.Current.CancellationToken);

        Assert.Equal(15, incident.RiskScore.Value);
        Assert.Equal(RiskLevel.Low, incident.RiskScore.Level);
        Assert.Equal([BasicIncidentRiskScorer.DkimFailReason], incident.RiskScore.Reasons);
    }

    [Fact]
    public async Task AnalyzeAsync_UneUrlExtraiteMaisNonSuspecteNeScoringPasLeMessage()
    {
        var analyzer = CreateAnalyzer();

        FraudIncident incident = await analyzer.AnalyzeAsync(ToStream(EmlWithUrl), null, TestContext.Current.CancellationToken);

        Assert.Equal(0, incident.RiskScore.Value);
        Assert.Equal(RiskLevel.Unknown, incident.RiskScore.Level);
    }

    [Fact]
    public async Task AnalyzeAsync_UnePieceJointeAnalyseeNonSuspecteEtSonIocHashNeScoringPasLeMessage()
    {
        var analyzer = CreateAnalyzer(new MimeKitEmailParser());

        FraudIncident incident = await analyzer.AnalyzeAsync(CreateMessageWithMimeAttachment().WriteToStream(), null, TestContext.Current.CancellationToken);

        Assert.Equal(0, incident.RiskScore.Value);
        Assert.Equal(RiskLevel.Unknown, incident.RiskScore.Level);
    }

    [Fact]
    public async Task AnalyzeAsync_UrlLocaleSuspecte_AlimenteLeScoreEtLaPiste()
    {
        const string eml =
            "From: support@example.test\r\n" +
            "Subject: Vérification\r\n" +
            "\r\n" +
            "Voir http://203.0.113.10/account/verify.\r\n";
        var analyzer = CreateAnalyzer();

        var incident = await analyzer.AnalyzeAsync(
            ToStream(eml),
            null,
            TestContext.Current.CancellationToken);

        var url = Assert.Single(incident.Urls);
        Assert.True(url.IsSuspicious);
        Assert.Contains(incident.Iocs, ioc => ioc.Type == IocType.IpAddress && ioc.Value == "203.0.113.10");
        Assert.DoesNotContain(incident.Iocs, ioc => ioc.Type == IocType.Domain);
        Assert.Equal(20, incident.RiskScore.Value);
        Assert.Contains(BasicIncidentRiskScorer.SuspiciousUrlReason, incident.RiskScore.Reasons);
        Assert.Equal(FraudClassification.Suspicious, incident.ClassificationAssessment.Classification);
        Assert.Equal(FraudClassification.Unknown, incident.Classification);
    }

    [Fact]
    public async Task AnalyzeAsync_PieceJointeExecutable_AlimenteLeScoreEtLaPisteMalware()
    {
        var analyzer = CreateAnalyzer(new MimeKitEmailParser());

        var incident = await analyzer.AnalyzeAsync(
            CreateMessageWithExecutableAttachment().WriteToStream(),
            null,
            TestContext.Current.CancellationToken);

        var attachment = Assert.Single(incident.Attachments);
        Assert.True(attachment.IsSuspicious);
        Assert.Equal(30, incident.RiskScore.Value);
        Assert.Contains(BasicIncidentRiskScorer.SuspiciousAttachmentReason, incident.RiskScore.Reasons);
        Assert.Equal(FraudClassification.Malware, incident.ClassificationAssessment.Classification);
        Assert.Equal(ClassificationConfidence.Medium, incident.ClassificationAssessment.Confidence);
        Assert.Equal(FraudClassification.Unknown, incident.Classification);
    }

    [Fact]
    public async Task AnalyzeAsync_ConserveLaClassificationUnknownApresScoring()
    {
        var analyzer = CreateAnalyzer();

        FraudIncident incident = await analyzer.AnalyzeAsync(ToStream(MinimalEml), null, TestContext.Current.CancellationToken);

        Assert.Equal(FraudClassification.Unknown, incident.Classification);
    }

    [Fact]
    public async Task AnalyzeAsync_ExposeUnePisteSansModifierLaClassification()
    {
        const string eml =
            "Authentication-Results: mx.example.org; spf=fail; dkim=fail; dmarc=none\r\n" +
            "From: support@example.net\r\n" +
            "Subject: Vérification\r\n" +
            "\r\n" +
            "Bonjour.\r\n";
        var analyzer = CreateAnalyzer();

        var incident = await analyzer.AnalyzeAsync(
            ToStream(eml),
            null,
            TestContext.Current.CancellationToken);

        Assert.Equal(FraudClassification.Unknown, incident.Classification);
        Assert.Equal(FraudClassification.Suspicious, incident.ClassificationAssessment.Classification);
        Assert.Equal(ClassificationConfidence.Low, incident.ClassificationAssessment.Confidence);
        Assert.NotEmpty(incident.ClassificationAssessment.Reasons);
    }

    [Fact]
    public async Task AnalyzeAsync_UnSeulEchecAuthentification_NeForcePasUnePiste()
    {
        var analyzer = CreateAnalyzer();

        var incident = await analyzer.AnalyzeAsync(
            ToStream(MinimalEml),
            null,
            TestContext.Current.CancellationToken);

        Assert.Equal(ClassificationAssessment.None, incident.ClassificationAssessment);
    }

    [Fact]
    public async Task AnalyzeAsync_ConserveLesRecommendedActionsVidesApresScoring()
    {
        var analyzer = CreateAnalyzer();

        FraudIncident incident = await analyzer.AnalyzeAsync(ToStream(MinimalEml), null, TestContext.Current.CancellationToken);

        Assert.Empty(incident.RecommendedActions);
    }

    [Fact]
    public void Constructeur_LeveArgumentNullExceptionSiRiskScorerEstNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => new BasicEmailIncidentAnalyzer(
                new BasicEmailParser(),
                new BasicEmailHeaderAnalyzer(),
                new BasicEmailUrlExtractor(),
                new BasicUrlIocExtractor(),
                new BasicEmailAttachmentAnalyzer(),
                new BasicAttachmentIocExtractor(),
                null!,
                new CautiousIncidentClassifier()));
    }

    [Fact]
    public async Task AnalyzeAsync_UtiliseLeScorerInjecte()
    {
        var analyzer = CreateAnalyzer(new BasicEmailParser(), new FixedIncidentRiskScorer());

        FraudIncident incident = await analyzer.AnalyzeAsync(ToStream(MinimalEml), null, TestContext.Current.CancellationToken);

        Assert.Equal(42, incident.RiskScore.Value);
        Assert.Equal(RiskLevel.Medium, incident.RiskScore.Level);
        Assert.Equal(["score fixe de test"], incident.RiskScore.Reasons);
    }

    [Fact]
    public async Task AnalyzeAsync_NomDeFichierSourceReporteeDansEvidence()
    {
        var analyzer = CreateAnalyzer();

        FraudIncident incident = await analyzer.AnalyzeAsync(ToStream(MinimalEml), sourceFileName: "suspicious.eml", TestContext.Current.CancellationToken);

        Assert.Equal("suspicious.eml", incident.Evidence.FileName);
    }

    [Fact]
    public async Task AnalyzeAsync_EvidenceFileNameDefautSiSourceFileNameNull()
    {
        var analyzer = CreateAnalyzer();

        FraudIncident incident = await analyzer.AnalyzeAsync(ToStream(MinimalEml), null, TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrWhiteSpace(incident.Evidence.FileName));
    }

    // ── Tests URLs ────────────────────────────────────────────────────────────

    [Fact]
    public async Task AnalyzeAsync_RenseigneUrlsLorsquUrlePresenteDansBodyText()
    {
        var analyzer = CreateAnalyzer();

        FraudIncident incident = await analyzer.AnalyzeAsync(ToStream(EmlWithUrl), null, TestContext.Current.CancellationToken);

        Assert.Single(incident.Urls);
        Assert.Equal("https://evil.example.com/login", incident.Urls[0].RawValue);
        Assert.Equal("evil.example.com",               incident.Urls[0].Host);
        Assert.Equal("https",                          incident.Urls[0].Scheme);
    }

    [Fact]
    public async Task AnalyzeAsync_LaissUrlsVideSiAucuneUrlDansLeBody()
    {
        var analyzer = CreateAnalyzer();

        FraudIncident incident = await analyzer.AnalyzeAsync(ToStream(MinimalEml), null, TestContext.Current.CancellationToken);

        Assert.Empty(incident.Urls);
    }

    [Fact]
    public async Task AnalyzeAsync_RenseigneIocsLorsquUneUrlePresente()
    {
        var analyzer = CreateAnalyzer();

        FraudIncident incident = await analyzer.AnalyzeAsync(ToStream(EmlWithUrl), null, TestContext.Current.CancellationToken);

        Assert.NotEmpty(incident.Urls);
        Assert.NotEmpty(incident.Iocs);
    }

    [Fact]
    public async Task AnalyzeAsync_UneUrlProduitUnIocUrl()
    {
        var analyzer = CreateAnalyzer();

        FraudIncident incident = await analyzer.AnalyzeAsync(ToStream(EmlWithUrl), null, TestContext.Current.CancellationToken);

        Assert.Contains(incident.Iocs, i => i.Type == IocType.Url && i.Value == "https://evil.example.com/login");
    }

    [Fact]
    public async Task AnalyzeAsync_UneUrlProduitUnIocDomain()
    {
        var analyzer = CreateAnalyzer();

        FraudIncident incident = await analyzer.AnalyzeAsync(ToStream(EmlWithUrl), null, TestContext.Current.CancellationToken);

        Assert.Contains(incident.Iocs, i => i.Type == IocType.Domain && i.Value == "evil.example.com");
    }

    [Fact]
    public async Task AnalyzeAsync_PlusieursUrlsDuMemeDomaineNeProduisentQuUnSeulIocDomain()
    {
        const string emlWithTwoUrls =
            "From: Fake Support <support@example.net>\r\n" +
            "Subject: Suspicious login attempt\r\n" +
            "\r\n" +
            "Bonjour,\r\n" +
            "Veuillez consulter https://evil.example.com/login ou https://evil.example.com/reset.\r\n";

        var analyzer = CreateAnalyzer();

        FraudIncident incident = await analyzer.AnalyzeAsync(ToStream(emlWithTwoUrls), null, TestContext.Current.CancellationToken);

        Assert.Equal(2, incident.Iocs.Count(i => i.Type == IocType.Url));
        Assert.Single(incident.Iocs, i => i.Type == IocType.Domain && i.Value == "evil.example.com");
    }

    [Fact]
    public async Task AnalyzeAsync_IocsRestentVidesLorsquAucuneUrlNestDetectee()
    {
        var analyzer = CreateAnalyzer();

        FraudIncident incident = await analyzer.AnalyzeAsync(ToStream(MinimalEml), null, TestContext.Current.CancellationToken);

        Assert.Empty(incident.Urls);
        Assert.Empty(incident.Iocs);
    }

    [Fact]
    public async Task AnalyzeAsync_TimestampsCohérentsPourCreatedAtImportedAtEtIocsFirstSeen()
    {
        var analyzer = CreateAnalyzer();

        FraudIncident incident = await analyzer.AnalyzeAsync(ToStream(EmlWithUrl), null, TestContext.Current.CancellationToken);

        Assert.All(
            incident.Iocs,
            ioc => Assert.Equal(incident.CreatedAt, ioc.FirstSeen));
        Assert.Equal(incident.CreatedAt, incident.Evidence.ImportedAt);
    }

    [Fact]
    public async Task AnalyzeAsync_RenseigneIocHashPourUnMessageMIMEAvecPieceJointe()
    {
        var analyzer = CreateAnalyzer(new MimeKitEmailParser());

        FraudIncident incident = await analyzer.AnalyzeAsync(CreateMessageWithMimeAttachment().WriteToStream(), null, TestContext.Current.CancellationToken);

        var attachment = Assert.Single(incident.Attachments);
        Assert.Equal("facture.txt", attachment.FileName);
        Assert.Equal("566a194e17b9cced887226f71d117300e8e51314531d6cae8cd0c9a82ac588f8", attachment.Sha256);
        var hashIoc = Assert.Single(incident.Iocs, i => i.Type == IocType.Hash);
        Assert.Equal("566a194e17b9cced887226f71d117300e8e51314531d6cae8cd0c9a82ac588f8", hashIoc.Value);
        Assert.Equal("email-attachment", hashIoc.Source);
        Assert.Equal(1.0, hashIoc.Confidence);
        Assert.Equal(incident.CreatedAt, hashIoc.FirstSeen);
    }

    [Fact]
    public async Task AnalyzeAsync_LaisseAttachmentsVidesLorsquAucunePieceJointeNExiste()
    {
        var analyzer = CreateAnalyzer(new MimeKitEmailParser());

        FraudIncident incident = await analyzer.AnalyzeAsync(CreateMessageWithNoAttachment().WriteToStream(), null, TestContext.Current.CancellationToken);

        Assert.Empty(incident.Attachments);
        Assert.DoesNotContain(incident.Iocs, i => i.Type == IocType.Hash);
    }

    [Fact]
    public async Task AnalyzeAsync_ConserveLesIocsUrlEtDomainEtAjouteLIocHash()
    {
        var analyzer = CreateAnalyzer(new MimeKitEmailParser());

        FraudIncident incident = await analyzer.AnalyzeAsync(CreateMessageWithUrlAndAttachment().WriteToStream(), null, TestContext.Current.CancellationToken);

        Assert.Contains(incident.Iocs, i => i.Type == IocType.Url && i.Value == "https://evil.example.com/login");
        Assert.Contains(incident.Iocs, i => i.Type == IocType.Domain && i.Value == "evil.example.com");
        Assert.Contains(incident.Iocs, i => i.Type == IocType.Hash && i.Value == "566a194e17b9cced887226f71d117300e8e51314531d6cae8cd0c9a82ac588f8");
        Assert.All(incident.Iocs, ioc => Assert.Equal(incident.CreatedAt, ioc.FirstSeen));
        Assert.Equal(incident.CreatedAt, incident.Evidence.ImportedAt);
    }

    [Fact]
    public async Task AnalyzeAsync_DeuxPiecesJointesIdentiquesNeProduisentQuUnSeulIocHash()
    {
        var analyzer = CreateAnalyzer(new MimeKitEmailParser());

        FraudIncident incident = await analyzer.AnalyzeAsync(CreateMessageWithDuplicateAttachments().WriteToStream(), null, TestContext.Current.CancellationToken);

        Assert.Equal(2, incident.Attachments.Count);
        Assert.Single(incident.Iocs, i => i.Type == IocType.Hash);
    }

    [Fact]
    public void Constructeur_LeveArgumentNullExceptionSiUrlExtractorEstNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => new BasicEmailIncidentAnalyzer(
                new BasicEmailParser(),
                new BasicEmailHeaderAnalyzer(),
                null!,
                new BasicUrlIocExtractor(),
                new BasicEmailAttachmentAnalyzer(),
                new BasicAttachmentIocExtractor(),
                new BasicIncidentRiskScorer(),
                new CautiousIncidentClassifier()));
    }

    [Fact]
    public void Constructeur_LeveArgumentNullExceptionSiUrlIocExtractorEstNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => new BasicEmailIncidentAnalyzer(
                new BasicEmailParser(),
                new BasicEmailHeaderAnalyzer(),
                new BasicEmailUrlExtractor(),
                null!,
                new BasicEmailAttachmentAnalyzer(),
                new BasicAttachmentIocExtractor(),
                new BasicIncidentRiskScorer(),
                new CautiousIncidentClassifier()));
    }

    [Fact]
    public void Constructeur_LeveArgumentNullExceptionSiAttachmentAnalyzerEstNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => new BasicEmailIncidentAnalyzer(
                new BasicEmailParser(),
                new BasicEmailHeaderAnalyzer(),
                new BasicEmailUrlExtractor(),
                new BasicUrlIocExtractor(),
                null!,
                new BasicAttachmentIocExtractor(),
                new BasicIncidentRiskScorer(),
                new CautiousIncidentClassifier()));
    }

    [Fact]
    public void Constructeur_LeveArgumentNullExceptionSiAttachmentIocExtractorEstNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => new BasicEmailIncidentAnalyzer(
                new BasicEmailParser(),
                new BasicEmailHeaderAnalyzer(),
                new BasicEmailUrlExtractor(),
                new BasicUrlIocExtractor(),
                new BasicEmailAttachmentAnalyzer(),
                null!,
                new BasicIncidentRiskScorer(),
                new CautiousIncidentClassifier()));
    }

    [Fact]
    public void Constructeur_LeveArgumentNullExceptionSiClassifierEstNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => new BasicEmailIncidentAnalyzer(
                new BasicEmailParser(),
                new BasicEmailHeaderAnalyzer(),
                new BasicEmailUrlExtractor(),
                new BasicUrlIocExtractor(),
                new BasicEmailAttachmentAnalyzer(),
                new BasicAttachmentIocExtractor(),
                new BasicIncidentRiskScorer(),
                null!));
    }
    
    [Fact]
    public async Task AnalyzeAsync_AjouteLesIocsHashApresLesIocsUrlEtDomain()
    {
        var analyzer = CreateAnalyzer(new MimeKitEmailParser());

        FraudIncident incident = await analyzer.AnalyzeAsync(
            CreateMessageWithUrlAndAttachment().WriteToStream(), null, TestContext.Current.CancellationToken);

        Assert.Collection(
            incident.Iocs,
            ioc => Assert.Equal(IocType.Url, ioc.Type),
            ioc => Assert.Equal(IocType.Domain, ioc.Type),
            ioc => Assert.Equal(IocType.Hash, ioc.Type));
    }

    private static MimeMessage CreateMessageWithNoAttachment()
    {
        var message = new MimeMessage();
        message.Body = new TextPart("plain") { Text = "Bonjour" };
        return message;
    }

    private static MimeMessage CreateMessageWithUrlAndAttachment()
    {
        var message = new MimeMessage();
        message.Body = new Multipart("mixed")
        {
            new TextPart("plain")
            {
                Text = "Veuillez consulter https://evil.example.com/login."
            },
            new MimePart
            {
                Content = new MimeContent(new MemoryStream(Encoding.UTF8.GetBytes("contenu factice"))),
                ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                ContentTransferEncoding = ContentEncoding.Base64,
                FileName = "facture.txt"
            }
        };

        return message;
    }

    private static MimeMessage CreateMessageWithDuplicateAttachments()
    {
        var message = new MimeMessage();
        message.Body = new Multipart("mixed")
        {
            new TextPart("plain") { Text = "Bonjour" },
            new MimePart
            {
                Content = new MimeContent(new MemoryStream(Encoding.UTF8.GetBytes("contenu factice"))),
                ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                ContentTransferEncoding = ContentEncoding.Base64,
                FileName = "facture-1.txt"
            },
            new MimePart
            {
                Content = new MimeContent(new MemoryStream(Encoding.UTF8.GetBytes("contenu factice"))),
                ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                ContentTransferEncoding = ContentEncoding.Base64,
                FileName = "facture-2.txt"
            }
        };

        return message;
    }
}

internal static class MimeMessageExtensions
{
    internal static Stream WriteToStream(this MimeMessage message)
    {
        var stream = new MemoryStream();
        message.WriteTo(stream);
        stream.Position = 0;
        return stream;
    }
}
