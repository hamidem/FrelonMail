using System.Security.Cryptography;
using System.Text;
using Frelon.Core;
using Xunit;

namespace Frelon.Mail.Tests;

/// <summary>
/// Vérifie que les parseurs calculent l'empreinte de la preuve sur ses octets exacts.
/// </summary>
public class EmailSourceSha256Tests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ParseAsync_CalculeLeSha256DesOctetsAscii(bool useMimeKit)
    {
        var bytes = Encoding.ASCII.GetBytes("From: a@example.test\r\n\r\nHello\r\n");

        var result = await CreateParser(useMimeKit).ParseAsync(
            new MemoryStream(bytes), TestContext.Current.CancellationToken);

        Assert.Equal(ExpectedSha256(bytes), result.SourceSha256);
        Assert.Equal(64, result.SourceSha256.Length);
        Assert.Equal(result.SourceSha256.ToLowerInvariant(), result.SourceSha256);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ParseAsync_ConserveLesCrLfDansLeCalcul(bool useMimeKit)
    {
        var crlfBytes = Encoding.ASCII.GetBytes("From: a@example.test\r\nSubject: Test\r\n\r\nLine 1\r\nLine 2\r\n");
        var lfBytes = Encoding.ASCII.GetBytes("From: a@example.test\nSubject: Test\n\nLine 1\nLine 2\n");
        var parser = CreateParser(useMimeKit);

        var result = await parser.ParseAsync(new MemoryStream(crlfBytes), TestContext.Current.CancellationToken);

        Assert.Equal(ExpectedSha256(crlfBytes), result.SourceSha256);
        Assert.NotEqual(ExpectedSha256(lfBytes), result.SourceSha256);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ParseAsync_CalculeSurLesOctetsNonAsciiExacts(bool useMimeKit)
    {
        byte[] bytes = [.. Encoding.ASCII.GetBytes("From: a@example.test\r\n\r\n"), 0xC3, 0xA9, 0x00, 0xFF];

        var result = await CreateParser(useMimeKit).ParseAsync(
            new MemoryStream(bytes), TestContext.Current.CancellationToken);

        Assert.Equal(ExpectedSha256(bytes), result.SourceSha256);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ParseAsync_SupporteUnFluxNonSeekable(bool useMimeKit)
    {
        var bytes = Encoding.ASCII.GetBytes("From: a@example.test\r\n\r\nBody\r\n");
        using var stream = new NonSeekableReadStream(bytes);

        var result = await CreateParser(useMimeKit).ParseAsync(stream, TestContext.Current.CancellationToken);

        Assert.Equal(ExpectedSha256(bytes), result.SourceSha256);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ParseAsync_LitDepuisLaPositionCourante(bool useMimeKit)
    {
        var prefix = Encoding.ASCII.GetBytes("ignored-prefix");
        var eml = Encoding.ASCII.GetBytes("From: a@example.test\r\n\r\nBody\r\n");
        using var stream = new MemoryStream([.. prefix, .. eml]);
        stream.Position = prefix.Length;

        var result = await CreateParser(useMimeKit).ParseAsync(stream, TestContext.Current.CancellationToken);

        Assert.Equal(ExpectedSha256(eml), result.SourceSha256);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ParseAsync_RespecteUnTokenDejaAnnule(bool useMimeKit)
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("From: a@example.test\r\n\r\nBody"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateParser(useMimeKit).ParseAsync(stream, cancellation.Token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ParseAsync_LaisseLeFluxAppelantOuvert(bool useMimeKit)
    {
        var bytes = Encoding.ASCII.GetBytes("From: a@example.test\r\n\r\nBody");
        var stream = new TrackingMemoryStream(bytes);

        await CreateParser(useMimeKit).ParseAsync(stream, TestContext.Current.CancellationToken);

        Assert.False(stream.WasDisposed);
        Assert.True(stream.CanRead);
        stream.Dispose();
    }

    [Fact]
    public async Task ParseAsync_LesDeuxParseursProduisentLaMemeEmpreinte()
    {
        byte[] bytes = [.. Encoding.ASCII.GetBytes("From: a@example.test\r\n\r\n"), 0xC3, 0xA9];

        var basic = await new BasicEmailParser().ParseAsync(
            new MemoryStream(bytes), TestContext.Current.CancellationToken);
        var mimeKit = await new MimeKitEmailParser().ParseAsync(
            new MemoryStream(bytes), TestContext.Current.CancellationToken);

        Assert.Equal(ExpectedSha256(bytes), basic.SourceSha256);
        Assert.Equal(basic.SourceSha256, mimeKit.SourceSha256);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ParseAsync_UnOctetDifferentProduitUneEmpreinteDifferente(bool useMimeKit)
    {
        var first = Encoding.ASCII.GetBytes("From: a@example.test\r\n\r\nBody A");
        var second = (byte[])first.Clone();
        second[^1] = (byte)'B';
        var parser = CreateParser(useMimeKit);

        var firstResult = await parser.ParseAsync(new MemoryStream(first), TestContext.Current.CancellationToken);
        var secondResult = await parser.ParseAsync(new MemoryStream(second), TestContext.Current.CancellationToken);

        Assert.NotEqual(firstResult.SourceSha256, secondResult.SourceSha256);
        Assert.Equal(ExpectedSha256(second), secondResult.SourceSha256);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnalyzeAsync_ExposeLEmpreinteDansLaPreuve(bool useMimeKit)
    {
        var bytes = Encoding.ASCII.GetBytes("From: a@example.test\r\nSubject: Test\r\n\r\nBody\r\n");
        var analyzer = new BasicEmailIncidentAnalyzer(
            CreateParser(useMimeKit),
            new BasicEmailHeaderAnalyzer(),
            new BasicEmailUrlExtractor(),
            new BasicUrlIocExtractor(),
            new BasicEmailAttachmentAnalyzer(),
            new BasicAttachmentIocExtractor(),
            new BasicIncidentRiskScorer(),
            new CautiousIncidentClassifier());

        var incident = await analyzer.AnalyzeAsync(
            new MemoryStream(bytes), "evidence.eml", TestContext.Current.CancellationToken);

        Assert.Equal(ExpectedSha256(bytes), incident.Evidence.Sha256);
    }

    private static IEmailParser CreateParser(bool useMimeKit)
        => useMimeKit ? new MimeKitEmailParser() : new BasicEmailParser();

    private static string ExpectedSha256(byte[] bytes)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private sealed class TrackingMemoryStream(byte[] bytes) : MemoryStream(bytes)
    {
        public bool WasDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class NonSeekableReadStream(byte[] bytes) : Stream
    {
        private readonly MemoryStream _inner = new(bytes);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => _inner.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

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
