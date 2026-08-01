using System.Text;
using MimeKit;
using Xunit;

namespace Frelon.Mail.Tests;

public class MimeKitEmailParserTests
{
    [Fact]
    public async Task ParseAsync_LitUnEmailTexteSimple()
    {
        var eml = "From: sender@example.com\r\n" +
                  "To: victim@example.com\r\n" +
                  "Subject: Test simple\r\n" +
                  "\r\n" +
                  "Bonjour monde.\r\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(eml));
        var parser = new MimeKitEmailParser();

        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(eml, result.RawContent);
        Assert.Contains(result.Headers, h => h.Name == "From" && h.Value == "sender@example.com");
        Assert.Contains(result.Headers, h => h.Name == "To" && h.Value == "victim@example.com");
        Assert.Contains(result.Headers, h => h.Name == "Subject" && h.Value == "Test simple");
        Assert.Equal("Bonjour monde.", result.BodyText?.TrimEnd('\r', '\n'));
        Assert.Null(result.BodyHtml);
        Assert.Empty(result.Attachments);
    }

    [Fact]
    public async Task ParseAsync_ConserveLesHeadersFromToAndSubject()
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("sender@example.com"));
        message.To.Add(MailboxAddress.Parse("victim@example.com"));
        message.Subject = "Sujet de test";
        message.Body = new TextPart("plain") { Text = "Corps texte" };

        using var stream = new MemoryStream();
        await message.WriteToAsync(stream, TestContext.Current.CancellationToken);
        stream.Position = 0;

        var parser = new MimeKitEmailParser();
        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        Assert.Contains(result.Headers, h => h.Name == "From" && h.Value == "sender@example.com");
        Assert.Contains(result.Headers, h => h.Name == "To" && h.Value == "victim@example.com");
        Assert.Contains(result.Headers, h => h.Name == "Subject" && h.Value == "Sujet de test");
    }

    [Fact]
    public async Task ParseAsync_ConservePlusieursHeadersReceivedEtLeurOrdre()
    {
        var message = new MimeMessage();
        message.Headers.Add("Received", "from first.example");
        message.Headers.Add("Received", "from second.example");
        message.Headers.Add("Received", "from third.example");
        message.Body = new TextPart("plain") { Text = "Corps" };

        using var stream = new MemoryStream();
        await message.WriteToAsync(stream, TestContext.Current.CancellationToken);
        stream.Position = 0;

        var parser = new MimeKitEmailParser();
        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        var received = result.Headers.Where(h => h.Name == "Received").ToList();
        Assert.Equal(3, received.Count);
        Assert.Equal(new[] { "from first.example", "from second.example", "from third.example" }, received.Select(h => h.Value).ToArray());
    }

    [Fact]
    public async Task ParseAsync_ExtraitLeCorpsTextuelPourUnEmailTextPlain()
    {
        var message = new MimeMessage();
        message.Body = new TextPart("plain") { Text = "Corps texte simple" };

        using var stream = new MemoryStream();
        await message.WriteToAsync(stream, TestContext.Current.CancellationToken);
        stream.Position = 0;

        var parser = new MimeKitEmailParser();
        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        Assert.Equal("Corps texte simple", result.BodyText?.TrimEnd('\r', '\n'));
        Assert.Null(result.BodyHtml);
    }

    [Fact]
    public async Task ParseAsync_ExtraitLeCorpsHtmlPourUnEmailTextHtml()
    {
        var message = new MimeMessage();
        message.Body = new TextPart("html") { Text = "<p>Bonjour</p>" };

        using var stream = new MemoryStream();
        await message.WriteToAsync(stream, TestContext.Current.CancellationToken);
        stream.Position = 0;

        var parser = new MimeKitEmailParser();
        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        Assert.Null(result.BodyText);
        Assert.Equal("<p>Bonjour</p>", result.BodyHtml?.TrimEnd('\r', '\n'));
    }

    [Fact]
    public async Task ParseAsync_RecupereLeCorpsTexteEtHtmlPourUnMessageMultipartAlternative()
    {
        var message = new MimeMessage();
        var multipart = new MultipartAlternative();
        multipart.Add(new TextPart("plain") { Text = "Version texte" });
        multipart.Add(new TextPart("html") { Text = "<p>Version HTML</p>" });
        message.Body = multipart;

        using var stream = new MemoryStream();
        await message.WriteToAsync(stream, TestContext.Current.CancellationToken);
        stream.Position = 0;

        var parser = new MimeKitEmailParser();
        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        Assert.Equal("Version texte", result.BodyText);
        Assert.Equal("<p>Version HTML</p>", result.BodyHtml);
    }

    [Fact]
    public async Task ParseAsync_ExposeLeSujetMimeEncodedDansLesHeadersParses()
    {
        var message = new MimeMessage();
        message.Headers.Add("Subject", "=?utf-8?b?U3VqZXQgdGVzdGU=?=");
        message.Body = new TextPart("plain") { Text = "Corps" };

        using var stream = new MemoryStream();
        await message.WriteToAsync(stream, TestContext.Current.CancellationToken);
        stream.Position = 0;

        var parser = new MimeKitEmailParser();
        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        Assert.Contains(result.Headers, h => h.Name == "Subject" && h.Value == "Sujet teste");
    }

    [Fact]
    public async Task ParseAsync_LeveArgumentNullExceptionPourUnFluxNull()
    {
        var parser = new MimeKitEmailParser();

        await Assert.ThrowsAsync<ArgumentNullException>(() => parser.ParseAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ParseAsync_ConserveExactementLesOctetsDecodesDeLaPieceJointe()
    {
        byte[] originalBytes =
        [
            0x00,
        0xFF,
        0x10,
        0x7F,
        0x80,
        0x42
        ];

        var message = new MimeMessage
        {
            Body = new Multipart("mixed")
        {
            new MimePart
            {
                Content = new MimeContent(new MemoryStream(originalBytes)),
                ContentDisposition =
                    new ContentDisposition(ContentDisposition.Attachment),
                ContentTransferEncoding = ContentEncoding.Base64,
                FileName = "sample.bin"
            }
        }
        };

        using var stream = new MemoryStream();
        await message.WriteToAsync(stream, TestContext.Current.CancellationToken);
        stream.Position = 0;

        var parser = new MimeKitEmailParser();

        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        var attachment = Assert.Single(result.Attachments);

        Assert.Equal(originalBytes, attachment.Content.ToArray());
    }

    [Fact]
    public async Task ParseAsync_ExtraitUnePieceJointeMimePartEtConserveSesOctetsDecodees()
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
                FileName = "facture.txt"
            }
        };

        using var stream = new MemoryStream();
        await message.WriteToAsync(stream, TestContext.Current.CancellationToken);
        stream.Position = 0;

        var parser = new MimeKitEmailParser();
        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        Assert.Single(result.Attachments);
        var attachment = result.Attachments[0];
        Assert.Equal("facture.txt", attachment.FileName);
        Assert.Equal("application/octet-stream", attachment.ContentType);
        Assert.Equal("contenu factice", Encoding.UTF8.GetString(attachment.Content.Span));
    }

    [Fact]
    public async Task ParseAsync_ExposeUnePieceJointeMimeVideSansLeverException()
    {
        const string rawEml =
            "From: sender@example.test\r\n" +
            "To: analyst@example.test\r\n" +
            "Subject: Empty attachment\r\n" +
            "MIME-Version: 1.0\r\n" +
            "Content-Type: multipart/mixed; boundary=boundary\r\n" +
            "\r\n" +
            "--boundary\r\n" +
            "Content-Type: text/plain\r\n" +
            "\r\n" +
            "Body\r\n" +
            "--boundary\r\n" +
            "Content-Type: application/octet-stream; name=empty.bin\r\n" +
            "Content-Disposition: attachment; filename=empty.bin\r\n" +
            "Content-Transfer-Encoding: base64\r\n" +
            "\r\n" +
            "--boundary--\r\n";

        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(rawEml));
        var parser = new MimeKitEmailParser();

        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        var attachment = Assert.Single(result.Attachments);
        Assert.Equal("empty.bin", attachment.FileName);
        Assert.Equal("application/octet-stream", attachment.ContentType);
        Assert.Empty(attachment.Content.ToArray());
    }

    [Fact]
    public async Task ParseAsync_DecodeCorrectementUnePieceJointeBase64()
    {
        var message = new MimeMessage();
        var payload = Encoding.UTF8.GetBytes("contenu base64");
        message.Body = new Multipart("mixed")
        {
            new MimePart("text", "plain")
            {
                Content = new MimeContent(new MemoryStream(payload)),
                ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                ContentTransferEncoding = ContentEncoding.Base64,
                FileName = "base64.txt"
            }
        };

        using var stream = new MemoryStream();
        await message.WriteToAsync(stream, TestContext.Current.CancellationToken);
        stream.Position = 0;

        var parser = new MimeKitEmailParser();
        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        Assert.Single(result.Attachments);
        Assert.Equal("contenu base64", Encoding.UTF8.GetString(result.Attachments[0].Content.Span));
    }

    [Fact]
    public async Task ParseAsync_ConserveLOrdreDePlusieursPiecesJointes()
    {
        var message = new MimeMessage();
        message.Body = new Multipart("mixed")
        {
            new MimePart
            {
                Content = new MimeContent(new MemoryStream(Encoding.UTF8.GetBytes("premier"))),
                ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                ContentTransferEncoding = ContentEncoding.Base64,
                FileName = "first.txt"
            },
            new MimePart
            {
                Content = new MimeContent(new MemoryStream(Encoding.UTF8.GetBytes("deuxieme"))),
                ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                ContentTransferEncoding = ContentEncoding.Base64,
                FileName = "second.txt"
            }
        };

        using var stream = new MemoryStream();
        await message.WriteToAsync(stream, TestContext.Current.CancellationToken);
        stream.Position = 0;

        var parser = new MimeKitEmailParser();
        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "first.txt", "second.txt" }, result.Attachments.Select(a => a.FileName).ToArray());
        Assert.Equal(new[] { "premier", "deuxieme" }, result.Attachments.Select(a => Encoding.UTF8.GetString(a.Content.Span)).ToArray());
    }

    [Fact]
    public async Task ParseAsync_ExposeUneMessagePartCommePieceJointe()
    {
        var attachedMessage = new MimeMessage();
        attachedMessage.Subject = "Message attaché";
        attachedMessage.Body = new TextPart("plain") { Text = "Corps du message attaché" };

        var message = new MimeMessage();
        message.Body = new Multipart("mixed")
        {
            new MessagePart
            {
                ContentDisposition = new ContentDisposition(ContentDisposition.Attachment) { FileName = "forwarded.eml" },
                Message = attachedMessage
            }
        };

        using var stream = new MemoryStream();
        await message.WriteToAsync(stream, TestContext.Current.CancellationToken);
        stream.Position = 0;

        var parser = new MimeKitEmailParser();
        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        Assert.Single(result.Attachments);
        var attachment = result.Attachments[0];
        Assert.Equal("forwarded.eml", attachment.FileName);
        Assert.Equal("message/rfc822", attachment.ContentType);

        using var attachedStream = new MemoryStream(attachment.Content.ToArray());
        var reloadedMessage = MimeMessage.Load(attachedStream, TestContext.Current.CancellationToken);

        Assert.Equal("Message attaché", reloadedMessage.Subject);
    }

    [Fact]
    public async Task ParseAsync_IgnoreUneEntiteAttachmentNonSupporteeSansLeverException()
    {
        var unsupportedAttachment = new Multipart("mixed")
        {
            ContentDisposition =
                new ContentDisposition(ContentDisposition.Attachment)
        };

        unsupportedAttachment.Add(
            new TextPart("plain")
            {
                Text = "ignored"
            });

        var message = new MimeMessage
        {
            Body = new Multipart("mixed")
        {
            unsupportedAttachment,
            new MimePart
            {
                Content = new MimeContent(
                    new MemoryStream(
                        Encoding.UTF8.GetBytes("piece jointe"))),
                ContentDisposition =
                    new ContentDisposition(ContentDisposition.Attachment),
                ContentTransferEncoding = ContentEncoding.Base64,
                FileName = "supported.txt"
            }
        }
        };

        using var stream = new MemoryStream();
        await message.WriteToAsync(stream, TestContext.Current.CancellationToken);
        stream.Position = 0;

        var parser = new MimeKitEmailParser();

        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        var attachment = Assert.Single(result.Attachments);
        Assert.Equal("supported.txt", attachment.FileName);
    }

    [Fact]
    public async Task ParseAsync_ParseUnMessageEntierementEnMemoire()
    {
        var message = new MimeMessage();
        message.Subject = "No network";
        message.Body = new TextPart("plain") { Text = "Aucune requête réseau" };

        using var stream = new MemoryStream();
        await message.WriteToAsync(stream, TestContext.Current.CancellationToken);
        stream.Position = 0;

        var parser = new MimeKitEmailParser();
        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        Assert.Equal("Aucune requête réseau", result.BodyText?.TrimEnd('\r', '\n'));
    }

    [Fact]
    public async Task ParseAsync_RawContentPreserveLesOctetsSourceDeManiereReversible()
    {
        byte[] sourceBytes =
        [
            (byte)'F', (byte)'r', (byte)'o', (byte)'m', (byte)':', (byte)' ',
            (byte)'a', (byte)'@', (byte)'b', (byte)'.', (byte)'c',
            13, 10,
            13, 10,
            0xE9
        ];

        using var stream = new MemoryStream(sourceBytes);
        var parser = new MimeKitEmailParser();

        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        byte[] reconstructed = Encoding.Latin1.GetBytes(result.RawContent);

        Assert.Equal(sourceBytes, reconstructed);
    }
}
