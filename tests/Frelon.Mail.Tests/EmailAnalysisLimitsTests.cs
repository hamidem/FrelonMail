using System.Text;
using MimeKit;
using Xunit;

namespace Frelon.Mail.Tests;

/// <summary>Corpus synthétique d'entrées conçues pour épuiser les ressources.</summary>
public sealed class EmailAnalysisLimitsTests
{
    [Fact]
    public async Task EmailEvidenceParser_RefuseUnFluxVide()
    {
        var parser = new EmailEvidenceParser();

        var exception = await Assert.ThrowsAsync<EmailAnalysisLimitException>(
            () => parser.ParseAsync(
                new MemoryStream(),
                TestContext.Current.CancellationToken));

        Assert.Contains("vide", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EmailEvidenceParser_RefuseUnFluxNonSeekableTropGrand()
    {
        var limits = EmailAnalysisLimits.Default with { MaxSourceBytes = 16 };
        using var source = new NonSeekableReadStream(new byte[17]);
        var parser = new EmailEvidenceParser(limits);

        await Assert.ThrowsAsync<EmailAnalysisLimitException>(
            () => parser.ParseAsync(source, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task BasicEmailParser_AccepteExactementLaTailleMaximale()
    {
        var limits = EmailAnalysisLimits.Default with { MaxSourceBytes = 32 };
        var source = Encoding.ASCII.GetBytes(new string('x', 32));
        var parser = new BasicEmailParser(limits);

        var parsed = await parser.ParseAsync(
            new MemoryStream(source),
            TestContext.Current.CancellationToken);

        Assert.Equal(32, parsed.RawContent.Length);
    }

    [Fact]
    public async Task MimeKitEmailParser_RefuseTropDEnTetes()
    {
        const string source =
            "X-One: 1\r\n" +
            "X-Two: 2\r\n" +
            "\r\n" +
            "body";
        var limits = EmailAnalysisLimits.Default with { MaxHeaderCount = 1 };
        var parser = new MimeKitEmailParser(limits);

        await Assert.ThrowsAsync<EmailAnalysisLimitException>(
            () => parser.ParseAsync(
                new MemoryStream(Encoding.ASCII.GetBytes(source)),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MimeKitEmailParser_RefuseUnCorpsDecodeTropGrand()
    {
        const string source =
            "Content-Type: text/plain; charset=utf-8\r\n" +
            "\r\n" +
            "12345";
        var limits = EmailAnalysisLimits.Default with { MaxBodyCharacters = 4 };
        var parser = new MimeKitEmailParser(limits);

        await Assert.ThrowsAsync<EmailAnalysisLimitException>(
            () => parser.ParseAsync(
                new MemoryStream(Encoding.ASCII.GetBytes(source)),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MimeKitEmailParser_RefuseTropDePiecesJointes()
    {
        var message = new MimeMessage
        {
            Body = new Multipart("mixed")
            {
                CreateAttachment("one.bin", [1]),
                CreateAttachment("two.bin", [2])
            }
        };
        var limits = EmailAnalysisLimits.Default with { MaxAttachmentCount = 1 };
        var parser = new MimeKitEmailParser(limits);

        await Assert.ThrowsAsync<EmailAnalysisLimitException>(
            () => parser.ParseAsync(
                message.WriteToStream(),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MimeKitEmailParser_InterromptUnePieceJointeDecodeeTropGrande()
    {
        var message = new MimeMessage
        {
            Body = new Multipart("mixed")
            {
                CreateAttachment("payload.bin", [1, 2, 3, 4, 5])
            }
        };
        var limits = EmailAnalysisLimits.Default with { MaxAttachmentBytes = 4 };
        var parser = new MimeKitEmailParser(limits);

        await Assert.ThrowsAsync<EmailAnalysisLimitException>(
            () => parser.ParseAsync(
                message.WriteToStream(),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MimeKitEmailParser_InterromptLeVolumeCumuleDesPiecesJointes()
    {
        var message = new MimeMessage
        {
            Body = new Multipart("mixed")
            {
                CreateAttachment("one.bin", [1, 2, 3]),
                CreateAttachment("two.bin", [4, 5, 6])
            }
        };
        var limits = EmailAnalysisLimits.Default with
        {
            MaxAttachmentBytes = 4,
            MaxTotalAttachmentBytes = 5
        };
        var parser = new MimeKitEmailParser(limits);

        await Assert.ThrowsAsync<EmailAnalysisLimitException>(
            () => parser.ParseAsync(
                message.WriteToStream(),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Constructeur_RefuseUnQuotaInvalide()
    {
        var limits = EmailAnalysisLimits.Default with { MaxMimeDepth = 0 };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new EmailEvidenceParser(limits));
    }

    [Fact]
    public async Task EmailEvidenceParser_NormaliseUnMsgTronque()
    {
        byte[] truncatedMsg =
        [
            0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1,
            0x00, 0x00, 0x00, 0x00
        ];
        var parser = new EmailEvidenceParser();

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => parser.ParseAsync(
                new MemoryStream(truncatedMsg),
                TestContext.Current.CancellationToken));

        Assert.Equal(
            "Le fichier ne contient pas un message EML ou MSG valide.",
            exception.Message);
    }

    private static MimePart CreateAttachment(string fileName, byte[] content)
        => new()
        {
            Content = new MimeContent(new MemoryStream(content)),
            ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
            ContentTransferEncoding = ContentEncoding.Base64,
            FileName = fileName
        };

    private sealed class NonSeekableReadStream(byte[] content) : Stream
    {
        private readonly MemoryStream _inner = new(content, writable: false);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
            => _inner.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
            => _inner.ReadAsync(buffer, cancellationToken);

        public override void Flush()
            => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
