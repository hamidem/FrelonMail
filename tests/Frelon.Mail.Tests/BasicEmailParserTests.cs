using System.Text;
using Xunit;

namespace Frelon.Mail.Tests;

public class BasicEmailParserTests
{
    private const string MinimalEml =
        "From: sender@example.com\r\n" +
        "To: victim@example.com\r\n" +
        "Subject: Test suspicious mail\r\n" +
        "\r\n" +
        "Hello,\r\n" +
        "This is a suspicious email.\r\n";

    // ── Tests existants ────────────────────────────────────────────────────────

    [Fact]
    public async Task ParseAsync_LitUnFluxMinimalSansErreur()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(MinimalEml));
        var parser = new BasicEmailParser();

        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task ParseAsync_ExtraitLesHeadersFrom_To_Subject()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(MinimalEml));
        var parser = new BasicEmailParser();

        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        Assert.Contains(result.Headers, h => h.Name == "From" && h.Value == "sender@example.com");
        Assert.Contains(result.Headers, h => h.Name == "To" && h.Value == "victim@example.com");
        Assert.Contains(result.Headers, h => h.Name == "Subject" && h.Value == "Test suspicious mail");
    }

    [Fact]
    public async Task ParseAsync_ExtraitLeCorpsTextuelApresLaLigneVide()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(MinimalEml));
        var parser = new BasicEmailParser();

        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        Assert.NotNull(result.BodyText);
        Assert.Contains("Hello,", result.BodyText);
        Assert.Contains("This is a suspicious email.", result.BodyText);
    }

    [Fact]
    public async Task ParseAsync_ConserveLeContenuBrut()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(MinimalEml));
        var parser = new BasicEmailParser();

        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        Assert.Equal(MinimalEml, result.RawContent);
    }

    [Fact]
    public async Task ParseAsync_RetourneBodyHtmlNullPourEmailTexteSimple()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(MinimalEml));
        var parser = new BasicEmailParser();

        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        Assert.Null(result.BodyHtml);
    }

    // ── Nouveaux tests — Mission 002B ──────────────────────────────────────────

    [Fact]
    public async Task ParseAsync_GereLeSeparateurLfUniquement()
    {
        var eml =
            "From: sender@example.com\n" +
            "To: victim@example.com\n" +
            "Subject: Test LF\n" +
            "\n" +
            "Corps LF uniquement.\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(eml));
        var parser = new BasicEmailParser();

        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        Assert.Contains(result.Headers, h => h.Name == "From" && h.Value == "sender@example.com");
        Assert.Contains(result.Headers, h => h.Name == "To" && h.Value == "victim@example.com");
        Assert.Contains(result.Headers, h => h.Name == "Subject" && h.Value == "Test LF");
        Assert.NotNull(result.BodyText);
        Assert.Contains("Corps LF uniquement.", result.BodyText);
    }

    [Fact]
    public async Task ParseAsync_GereLesHeadersRepliésAvecEspace()
    {
        var eml =
            "From: sender@example.com\r\n" +
            "Subject: Votre compte nécessite\r\n" +
            " une vérification urgente\r\n" +
            "\r\n" +
            "Corps.\r\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(eml));
        var parser = new BasicEmailParser();

        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        Assert.Contains(result.Headers, h =>
            h.Name == "Subject" &&
            h.Value == "Votre compte nécessite une vérification urgente");
    }

    [Fact]
    public async Task ParseAsync_GereLesHeadersRepliésAvecTabulation()
    {
        var eml =
            "From: sender@example.com\r\n" +
            "Subject: Votre compte nécessite\r\n" +
            "\tune vérification urgente\r\n" +
            "\r\n" +
            "Corps.\r\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(eml));
        var parser = new BasicEmailParser();

        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        Assert.Contains(result.Headers, h =>
            h.Name == "Subject" &&
            h.Value == "Votre compte nécessite une vérification urgente");
    }

    [Fact]
    public async Task ParseAsync_ConserveLesHeadersDupliqués()
    {
        var eml =
            "From: sender@example.com\r\n" +
            "Received: from first.example\r\n" +
            "Received: from second.example\r\n" +
            "\r\n" +
            "Corps.\r\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(eml));
        var parser = new BasicEmailParser();

        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        var received = result.Headers.Where(h => h.Name == "Received").ToList();
        Assert.Equal(2, received.Count);
        Assert.Contains(received, h => h.Value == "from first.example");
        Assert.Contains(received, h => h.Value == "from second.example");
    }

    [Fact]
    public async Task ParseAsync_IgnoreUneLigneDeHeaderMalformée()
    {
        var eml =
            "From: sender@example.com\r\n" +
            "Ceci nest pas un header valide\r\n" +
            "Subject: Test\r\n" +
            "\r\n" +
            "Corps.\r\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(eml));
        var parser = new BasicEmailParser();

        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        // Aucune exception — la ligne malformée est silencieusement ignorée.
        Assert.Contains(result.Headers, h => h.Name == "From" && h.Value == "sender@example.com");
        Assert.Contains(result.Headers, h => h.Name == "Subject" && h.Value == "Test");
    }

    [Fact]
    public async Task ParseAsync_RefuseUnFluxVide()
    {
        using var stream = new MemoryStream([]);
        var parser = new BasicEmailParser();

        await Assert.ThrowsAsync<EmailAnalysisLimitException>(
            () => parser.ParseAsync(stream, TestContext.Current.CancellationToken));
    }
}
