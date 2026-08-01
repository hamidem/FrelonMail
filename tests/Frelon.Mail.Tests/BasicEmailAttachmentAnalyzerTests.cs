using System.Text;
using Frelon.Core;
using Xunit;

namespace Frelon.Mail.Tests;

public class BasicEmailAttachmentAnalyzerTests
{
    [Fact]
    public void AnalyzeAttachments_RetourneUneListeVidePourUnEmailSansPieceJointe()
    {
        var analyzer = new BasicEmailAttachmentAnalyzer();
        var email = new ParsedEmail
        {
            RawContent = "raw",
            SourceSha256 = string.Empty,
            Headers = []
        };

        var result = analyzer.AnalyzeAttachments(email);

        Assert.Empty(result);
    }

    [Fact]
    public void AnalyzeAttachments_ProduitUnAttachmentIndicatorAvecLesMetadonneesAttendues()
    {
        var analyzer = new BasicEmailAttachmentAnalyzer();
        var email = new ParsedEmail
        {
            RawContent = "raw",
            SourceSha256 = string.Empty,
            Headers = [],
            Attachments =
            [
                new ParsedEmailAttachment
                {
                    FileName = "facture.txt",
                    ContentType = "text/plain",
                    Content = Encoding.UTF8.GetBytes("contenu factice")
                }
            ]
        };

        var result = analyzer.AnalyzeAttachments(email);

        var attachment = Assert.Single(result);
        Assert.Equal("facture.txt", attachment.FileName);
        Assert.Equal("text/plain", attachment.ContentType);
        Assert.Equal(15, attachment.SizeBytes);
        Assert.Equal("566a194e17b9cced887226f71d117300e8e51314531d6cae8cd0c9a82ac588f8", attachment.Sha256);
        Assert.False(attachment.IsSuspicious);
        Assert.Empty(attachment.Reasons);
    }

    [Fact]
    public void AnalyzeAttachments_UtiliseUnnamedAttachmentQuandLeNomEstAbsent()
    {
        var analyzer = new BasicEmailAttachmentAnalyzer();
        var email = new ParsedEmail
        {
            RawContent = "raw",
            SourceSha256 = string.Empty,
            Headers = [],
            Attachments =
            [
                new ParsedEmailAttachment
                {
                    FileName = "   ",
                    ContentType = null,
                    Content = Encoding.UTF8.GetBytes("abc")
                }
            ]
        };

        var result = analyzer.AnalyzeAttachments(email);

        var attachment = Assert.Single(result);
        Assert.Equal("unnamed-attachment", attachment.FileName);
    }

    [Fact]
    public void AnalyzeAttachments_ProduitUnSha256EnHexadecimalMinuscule()
    {
        var analyzer = new BasicEmailAttachmentAnalyzer();
        var email = new ParsedEmail
        {
            RawContent = "raw",
            SourceSha256 = string.Empty,
            Headers = [],
            Attachments =
            [
                new ParsedEmailAttachment
                {
                    FileName = "sample.bin",
                    ContentType = "application/octet-stream",
                    Content = new byte[] { 0x00, 0xFF, 0x10 }
                }
            ]
        };

        var result = analyzer.AnalyzeAttachments(email);

        var attachment = Assert.Single(result);
        Assert.Equal(64, attachment.Sha256?.Length);
        Assert.Equal(attachment.Sha256, attachment.Sha256?.ToLowerInvariant());
        Assert.All(attachment.Sha256!, c => Assert.True(Uri.IsHexDigit(c)));
    }

    [Theory]
    [InlineData("facture.exe")]
    [InlineData("script.PS1")]
    [InlineData("raccourci.lnk")]
    public void AnalyzeAttachments_ExtensionExecutable_SignaleLaPieceJointe(string fileName)
    {
        var attachment = AnalyzeSingle(fileName, "application/octet-stream");

        Assert.True(attachment.IsSuspicious);
        Assert.Contains(BasicEmailAttachmentAnalyzer.ExecutableExtensionReason, attachment.Reasons);
    }

    [Fact]
    public void AnalyzeAttachments_DoubleExtension_SignaleLeNomTrompeur()
    {
        var attachment = AnalyzeSingle("facture.pdf.exe", "application/octet-stream");

        Assert.True(attachment.IsSuspicious);
        Assert.Equal(
            [
                BasicEmailAttachmentAnalyzer.ExecutableExtensionReason,
                BasicEmailAttachmentAnalyzer.MisleadingDoubleExtensionReason
            ],
            attachment.Reasons);
    }

    [Theory]
    [InlineData("page.html", "text/html")]
    [InlineData("tableau.xlsm", "application/vnd.ms-excel.sheet.macroEnabled.12")]
    public void AnalyzeAttachments_ContenuActif_SignaleLaPieceJointe(string fileName, string contentType)
    {
        var attachment = AnalyzeSingle(fileName, contentType);

        Assert.True(attachment.IsSuspicious);
        Assert.Equal([BasicEmailAttachmentAnalyzer.ActiveContentReason], attachment.Reasons);
    }

    [Fact]
    public void AnalyzeAttachments_TypeMimeExecutableSansExtension_SignaleLaPieceJointe()
    {
        var attachment = AnalyzeSingle("charge", "application/x-msdownload");

        Assert.True(attachment.IsSuspicious);
        Assert.Equal([BasicEmailAttachmentAnalyzer.ExecutableContentTypeReason], attachment.Reasons);
    }

    [Fact]
    public void AnalyzeAttachments_TypeMimeExecutableAvecParametre_SignaleLaPieceJointe()
    {
        var attachment = AnalyzeSingle("charge", "application/x-msdownload; name=charge.exe");

        Assert.True(attachment.IsSuspicious);
        Assert.Equal([BasicEmailAttachmentAnalyzer.ExecutableContentTypeReason], attachment.Reasons);
    }

    [Theory]
    [InlineData("documents.zip", "application/zip")]
    [InlineData("rapport.pdf", "application/pdf")]
    public void AnalyzeAttachments_FormatPassifOrdinaire_NeLeQualifiePasSansAutreSignal(
        string fileName,
        string contentType)
    {
        var attachment = AnalyzeSingle(fileName, contentType);

        Assert.False(attachment.IsSuspicious);
        Assert.Empty(attachment.Reasons);
    }

    [Fact]
    public void AnalyzeAttachments_LeveArgumentNullExceptionSiEmailEstNull()
    {
        var analyzer = new BasicEmailAttachmentAnalyzer();

        Assert.Throws<ArgumentNullException>(() => analyzer.AnalyzeAttachments(null!));
    }

    private static AttachmentIndicator AnalyzeSingle(string fileName, string? contentType)
    {
        var analyzer = new BasicEmailAttachmentAnalyzer();
        var email = new ParsedEmail
        {
            RawContent = "raw",
            SourceSha256 = string.Empty,
            Headers = [],
            Attachments =
            [
                new ParsedEmailAttachment
                {
                    FileName = fileName,
                    ContentType = contentType,
                    Content = Encoding.UTF8.GetBytes("contenu factice")
                }
            ]
        };

        return Assert.Single(analyzer.AnalyzeAttachments(email));
    }
}
