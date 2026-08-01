using System.Security.Cryptography;
using System.Text;
using Frelon.Core;
using MsgKitEmail = MsgKit.Email;
using MsgKitSender = MsgKit.Sender;
using Xunit;

namespace Frelon.Mail.Tests;

public sealed class OutlookMsgEmailParserTests
{
    [Fact]
    public async Task ParseAsync_ExtraitHeadersCorpsEtPieceJointeDUnMsgSynthetique()
    {
        var attachmentBytes = Encoding.UTF8.GetBytes("contenu de test inoffensif");
        var sourceBytes = CreateMsg(attachmentBytes);
        using var stream = new MemoryStream(sourceBytes);
        var parser = new OutlookMsgEmailParser();

        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        Assert.Contains(result.Headers, header =>
            header.Name == "From"
            && header.Value.Contains("sender@example.test", StringComparison.Ordinal));
        Assert.Contains(result.Headers, header =>
            header.Name == "Subject"
            && header.Value == "Facture en attente");
        Assert.Contains(result.Headers, header =>
            header.Name == "Authentication-Results"
            && header.Value.Contains("spf=fail", StringComparison.Ordinal));
        Assert.Equal("Consultez http://fraud.example/login", result.BodyText?.TrimEnd('\r', '\n'));
        Assert.Equal(
            "<p>Consultez <a href=\"http://fraud.example/login\">votre facture</a></p>",
            result.BodyHtml?.TrimEnd('\r', '\n'));

        var attachment = Assert.Single(result.Attachments);
        Assert.Equal("facture.txt", attachment.FileName);
        Assert.Equal(attachmentBytes, attachment.Content.ToArray());
    }

    [Fact]
    public async Task ParseAsync_ConserveLEmpreinteEtLesOctetsExactsDuMsg()
    {
        var sourceBytes = CreateMsg();
        using var stream = new MemoryStream(sourceBytes);
        var parser = new OutlookMsgEmailParser();

        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(sourceBytes)).ToLowerInvariant(),
            result.SourceSha256);
        Assert.Equal(sourceBytes, Encoding.Latin1.GetBytes(result.RawContent));
    }

    [Fact]
    public async Task ParseAsync_RefuseUnePieceJointeQuiDepasseLeQuota()
    {
        var sourceBytes = CreateMsg([1, 2, 3, 4, 5]);
        using var stream = new MemoryStream(sourceBytes);
        var limits = EmailAnalysisLimits.Default with { MaxAttachmentBytes = 4 };
        var parser = new OutlookMsgEmailParser(limits);

        await Assert.ThrowsAsync<EmailAnalysisLimitException>(
            () => parser.ParseAsync(stream, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EmailEvidenceParser_DetecteLeMsgParSaSignatureBinaire()
    {
        var sourceBytes = CreateMsg();
        using var stream = new MemoryStream(sourceBytes);
        var parser = new EmailEvidenceParser();

        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        Assert.Equal("Consultez http://fraud.example/login", result.BodyText?.TrimEnd('\r', '\n'));
        Assert.Contains(result.Headers, header =>
            header.Name == "Subject"
            && header.Value == "Facture en attente");
    }

    [Fact]
    public async Task EmailEvidenceParser_ContinueDeLireUnEml()
    {
        const string eml =
            "From: sender@example.test\r\n" +
            "Subject: EML intact\r\n" +
            "\r\n" +
            "Corps EML";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(eml));
        var parser = new EmailEvidenceParser();

        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        Assert.Equal("Corps EML", result.BodyText);
        Assert.Contains(result.Headers, header =>
            header.Name == "Subject"
            && header.Value == "EML intact");
    }

    [Theory]
    [InlineData(8)]
    [InlineData(512)]
    [InlineData(4096)]
    public async Task EmailEvidenceParser_NormaliseLesTroncaturesDUnMsgSynthetique(
        int retainedBytes)
    {
        var validMessage = CreateMsg(Encoding.UTF8.GetBytes("piece jointe"));
        Assert.True(validMessage.Length > retainedBytes);
        var truncatedMessage = validMessage[..retainedBytes];
        using var stream = new MemoryStream(truncatedMessage, writable: false);
        var parser = new EmailEvidenceParser();

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => parser.ParseAsync(stream, TestContext.Current.CancellationToken));

        Assert.Equal(
            "Le fichier ne contient pas un message EML ou MSG valide.",
            exception.Message);
    }

    [Fact]
    public async Task EmailEvidenceParser_NormaliseUnRepertoireOleCorrompu()
    {
        var corruptedMessage = CreateMsg();
        corruptedMessage.AsSpan(48, sizeof(int)).Fill(byte.MaxValue);
        using var stream = new MemoryStream(corruptedMessage, writable: false);
        var parser = new EmailEvidenceParser();

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => parser.ParseAsync(stream, TestContext.Current.CancellationToken));

        Assert.Equal(
            "Le fichier ne contient pas un message EML ou MSG valide.",
            exception.Message);
    }

    [Fact]
    public async Task PipelineParDefaut_AnalyseUnMsgSansChangerLeMoteurMetier()
    {
        var sourceBytes = CreateMsg(Encoding.UTF8.GetBytes("piece jointe"));
        using var stream = new MemoryStream(sourceBytes);
        var analyzer = EmailIncidentAnalyzerFactory.CreateDefault();

        var incident = await analyzer.AnalyzeAsync(
            stream,
            "message-outlook.msg",
            TestContext.Current.CancellationToken);

        Assert.Equal("message-outlook.msg", incident.Evidence.FileName);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(sourceBytes)).ToLowerInvariant(),
            incident.Evidence.Sha256);
        Assert.Equal("Facture en attente", incident.Identity.Subject);
        Assert.Contains(incident.Iocs, ioc =>
            ioc.Type == IocType.Url
            && ioc.Value == "http://fraud.example/login");
        Assert.Contains(incident.Iocs, ioc =>
            ioc.Type == IocType.Domain
            && ioc.Value == "fraud.example");
        Assert.Single(incident.Attachments);
    }

    [Fact]
    public async Task ParseAsync_LeveArgumentNullExceptionPourUnFluxNull()
    {
        var parser = new OutlookMsgEmailParser();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => parser.ParseAsync(null!, TestContext.Current.CancellationToken));
    }

    private static byte[] CreateMsg(byte[]? attachmentBytes = null)
    {
        using var email = new MsgKitEmail(
            new MsgKitSender("sender@example.test", "Service facturation"),
            "Facture en attente");

        email.Recipients.AddTo("analyst@example.test", "Analyste");
        email.BodyText = "Consultez http://fraud.example/login";
        email.BodyHtml = "<p>Consultez <a href=\"http://fraud.example/login\">votre facture</a></p>";
        email.InternetMessageId = "<synthetic-frelon@example.test>";
        email.TransportMessageHeadersText =
            "Received: from suspicious.example by mail.example.test\r\n" +
            "From: Service facturation <sender@example.test>\r\n" +
            "To: Analyste <analyst@example.test>\r\n" +
            "Subject: Facture en attente\r\n" +
            "Message-ID: <synthetic-frelon@example.test>\r\n" +
            "Authentication-Results: mail.example.test; spf=fail smtp.mailfrom=example.test\r\n";

        if (attachmentBytes is not null)
        {
            email.Attachments.Add(new MemoryStream(attachmentBytes), "facture.txt");
        }

        using var stream = new MemoryStream();
        email.Save(stream);
        return stream.ToArray();
    }
}
