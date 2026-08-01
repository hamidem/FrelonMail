using Frelon.Core;
using Xunit;

namespace Frelon.Mail.Tests;

/// <summary>
/// Tests de <see cref="BasicAttachmentIocExtractor"/>.
/// </summary>
public class BasicAttachmentIocExtractorTests
{
    private const string Sha256One = "566a194e17b9cced887226f71d117300e8e51314531d6cae8cd0c9a82ac588f8";
    private const string Sha256Two = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    private static BasicAttachmentIocExtractor CreateExtractor() => new();

    private static AttachmentIndicator CreateAttachment(string? sha256)
        => new()
        {
            FileName = "piece-jointe.bin",
            Sha256 = sha256,
            Reasons = [],
        };

    [Fact]
    public void ExtractIocs_ListeVideProduitListeVide()
    {
        var extractor = CreateExtractor();

        IReadOnlyList<Ioc> iocs = extractor.ExtractIocs([], DateTimeOffset.UtcNow);

        Assert.Empty(iocs);
    }

    [Fact]
    public void ExtractIocs_Sha256ValideProduitUnIocHash()
    {
        var extractor = CreateExtractor();
        var observedAt = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);

        IReadOnlyList<Ioc> iocs = extractor.ExtractIocs(
            [CreateAttachment($"  {Sha256One.ToUpperInvariant()}  ")],
            observedAt);

        var ioc = Assert.Single(iocs);
        Assert.Equal(IocType.Hash, ioc.Type);
        Assert.Equal(Sha256One, ioc.Value);
        Assert.Equal(1.0, ioc.Confidence);
        Assert.Equal(BasicAttachmentIocExtractor.SourceName, ioc.Source);
        Assert.Equal(observedAt, ioc.FirstSeen);
    }

    [Fact]
    public void ExtractIocs_Sha256NullEstIgnore()
    {
        var extractor = CreateExtractor();

        IReadOnlyList<Ioc> iocs = extractor.ExtractIocs([CreateAttachment(null)], DateTimeOffset.UtcNow);

        Assert.Empty(iocs);
    }

    [Fact]
    public void ExtractIocs_Sha256VideOuEspacesEstIgnore()
    {
        var extractor = CreateExtractor();

        IReadOnlyList<Ioc> iocs = extractor.ExtractIocs(
            [CreateAttachment("   "), CreateAttachment(string.Empty)],
            DateTimeOffset.UtcNow);

        Assert.Empty(iocs);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("566a194e17b9cced887226f71d117300e8e51314531d6cae8cd0c9a82ac588f")]
    public void ExtractIocs_Sha256DeLongueurInvalideEstIgnore(string sha256)
    {
        var extractor = CreateExtractor();

        IReadOnlyList<Ioc> iocs = extractor.ExtractIocs([CreateAttachment(sha256)], DateTimeOffset.UtcNow);

        Assert.Empty(iocs);
    }

    [Fact]
    public void ExtractIocs_Sha256AvecCaractereNonHexadécimalEstIgnore()
    {
        var extractor = CreateExtractor();
        var sha256 = "566a194e17b9cced887226f71d117300e8e51314531d6cae8cd0c9a82ac588fx";

        IReadOnlyList<Ioc> iocs = extractor.ExtractIocs([CreateAttachment(sha256)], DateTimeOffset.UtcNow);

        Assert.Empty(iocs);
    }

    [Fact]
    public void ExtractIocs_DeuxPiecesJointesAvecLeMemeSha256NeProduisentQuUnSeulIocHash()
    {
        var extractor = CreateExtractor();

        IReadOnlyList<Ioc> iocs = extractor.ExtractIocs(
            [CreateAttachment(Sha256One), CreateAttachment(Sha256One)],
            DateTimeOffset.UtcNow);

        Assert.Single(iocs);
    }

    [Fact]
    public void ExtractIocs_DeuxSha256DifferantQueParLaCasseNeProduisentQuUnSeulIocHash()
    {
        var extractor = CreateExtractor();

        IReadOnlyList<Ioc> iocs = extractor.ExtractIocs(
            [CreateAttachment(Sha256One.ToUpperInvariant()), CreateAttachment(Sha256One)],
            DateTimeOffset.UtcNow);

        Assert.Single(iocs);
        Assert.Equal(Sha256One, iocs[0].Value);
    }

    [Fact]
    public void ExtractIocs_DeuxSha256DifferentsProduisentDeuxIocHash()
    {
        var extractor = CreateExtractor();

        IReadOnlyList<Ioc> iocs = extractor.ExtractIocs(
            [CreateAttachment(Sha256One), CreateAttachment(Sha256Two)],
            DateTimeOffset.UtcNow);

        Assert.Equal(2, iocs.Count);
        Assert.All(iocs, i => Assert.Equal(IocType.Hash, i.Type));
    }

    [Fact]
    public void ExtractIocs_OrdreDePremiereApparitionEstPreserve()
    {
        var extractor = CreateExtractor();

        IReadOnlyList<Ioc> iocs = extractor.ExtractIocs(
            [CreateAttachment(Sha256Two), CreateAttachment(Sha256One), CreateAttachment(Sha256Two.ToUpperInvariant())],
            DateTimeOffset.UtcNow);

        Assert.Collection(
            iocs,
            ioc => Assert.Equal(Sha256Two, ioc.Value),
            ioc => Assert.Equal(Sha256One, ioc.Value));
    }

    [Fact]
    public void ExtractIocs_LeveArgumentNullExceptionSiAttachmentsEstNull()
    {
        var extractor = CreateExtractor();

        Assert.Throws<ArgumentNullException>(() => extractor.ExtractIocs(null!, DateTimeOffset.UtcNow));
    }
}