namespace Frelon.Web.Tests;

public sealed class EmailEvidenceFilePolicyTests
{
    [Theory]
    [InlineData("message.eml", "message.eml")]
    [InlineData("Message%20suspect.EML", "Message suspect.EML")]
    [InlineData("caf%C3%A9.eml", "café.eml")]
    [InlineData("message.msg", "message.msg")]
    [InlineData("MESSAGE.MSG", "MESSAGE.MSG")]
    public void ValidateEncodedFileName_FormatValide_AccepteEtNormalise(
        string encodedFileName,
        string expectedFileName)
    {
        var result = EmailEvidenceFilePolicy.ValidateEncodedFileName(encodedFileName);

        Assert.True(result.IsAccepted);
        Assert.Equal(expectedFileName, result.FileName);
        Assert.Equal(EmailEvidenceFileRejection.None, result.Rejection);
        Assert.Null(result.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ValidateEncodedFileName_NomAbsent_ExpliqueLeBesoin(string? encodedFileName)
    {
        var result = EmailEvidenceFilePolicy.ValidateEncodedFileName(encodedFileName);

        Assert.False(result.IsAccepted);
        Assert.Equal(EmailEvidenceFileRejection.Missing, result.Rejection);
        Assert.Contains("message suspect", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("message.pdf")]
    [InlineData("message.txt")]
    [InlineData("message")]
    public void ValidateEncodedFileName_AutreFormat_RappelleLeFormatActuel(string fileName)
    {
        var result = EmailEvidenceFilePolicy.ValidateEncodedFileName(fileName);

        Assert.False(result.IsAccepted);
        Assert.Equal(EmailEvidenceFileRejection.UnsupportedFormat, result.Rejection);
        Assert.Contains(".eml", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".msg", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("../message.eml")]
    [InlineData("folder%2Fmessage.eml")]
    [InlineData("folder%5Cmessage.eml")]
    [InlineData(".eml")]
    public void ValidateEncodedFileName_NomDangereuxOuVide_Refuse(string fileName)
    {
        var result = EmailEvidenceFilePolicy.ValidateEncodedFileName(fileName);

        Assert.False(result.IsAccepted);
        Assert.Equal(EmailEvidenceFileRejection.InvalidName, result.Rejection);
        Assert.Null(result.FileName);
    }
}
