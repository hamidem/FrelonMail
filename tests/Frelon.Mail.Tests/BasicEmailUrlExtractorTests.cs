using Xunit;

namespace Frelon.Mail.Tests;

/// <summary>
/// Tests de <see cref="BasicEmailUrlExtractor"/>.
/// Aucun test n'effectue d'appel réseau.
/// </summary>
public class BasicEmailUrlExtractorTests
{
    // ── Fixture ───────────────────────────────────────────────────────────────

    private static readonly BasicEmailUrlExtractor Extractor = new();

    private static ParsedEmail Build(string? bodyText = null, string? bodyHtml = null) => new()
    {
        RawContent = string.Empty,
        SourceSha256 = string.Empty,
        Headers    = [],
        BodyText   = bodyText,
        BodyHtml   = bodyHtml,
    };

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ExtractUrls_RetourneListeVideSiAucuneUrl()
    {
        var email = Build(bodyText: "Bonjour, ceci est un email sans URL.");

        var urls = Extractor.ExtractUrls(email);

        Assert.Empty(urls);
    }

    [Fact]
    public void ExtractUrls_ExtraitUrlHttpDepuisBodyText()
    {
        var email = Build(bodyText: "Consultez http://example.com pour plus d'infos.");

        var urls = Extractor.ExtractUrls(email);

        Assert.Single(urls);
        Assert.Equal("http://example.com", urls[0].RawValue);
    }

    [Fact]
    public void ExtractUrls_ExtraitUrlHttpsDepuisBodyText()
    {
        var email = Build(bodyText: "Accédez à https://example.com/login pour vous connecter.");

        var urls = Extractor.ExtractUrls(email);

        Assert.Single(urls);
        Assert.Equal("https://example.com/login", urls[0].RawValue);
    }

    [Fact]
    public void ExtractUrls_ExtraitPlusieursUrls()
    {
        var email = Build(bodyText:
            "Bonjour,\n" +
            "Veuillez consulter https://evil.example.com/login.\n" +
            "Puis vérifiez http://backup.example.net/path?x=1\n");

        var urls = Extractor.ExtractUrls(email);

        Assert.Equal(2, urls.Count);
    }

    [Fact]
    public void ExtractUrls_ExtraitUrlDepuisBodyHtml()
    {
        var email = Build(bodyHtml: "<a href=\"https://evil.example.com/login\">Cliquez ici</a>");

        var urls = Extractor.ExtractUrls(email);

        Assert.Single(urls);
        Assert.Equal("https://evil.example.com/login", urls[0].RawValue);
    }

    [Fact]
    public void ExtractUrls_RetireLaPonctuationFinale()
    {
        var email = Build(bodyText: "Consultez https://example.com/login.");

        var urls = Extractor.ExtractUrls(email);

        Assert.Single(urls);
        Assert.Equal("https://example.com/login", urls[0].RawValue);
    }

    [Fact]
    public void ExtractUrls_DedupliqueUrlsIdentiques()
    {
        var email = Build(
            bodyText: "Visitez https://example.com et aussi https://example.com encore.",
            bodyHtml: "<a href=\"https://example.com\">lien</a>");

        var urls = Extractor.ExtractUrls(email);

        Assert.Single(urls);
    }

    [Fact]
    public void ExtractUrls_RenseigneHost()
    {
        var email = Build(bodyText: "Voir https://evil.example.com/login.");

        var urls = Extractor.ExtractUrls(email);

        Assert.Equal("evil.example.com", urls[0].Host);
    }

    [Fact]
    public void ExtractUrls_RenseigneScheme()
    {
        var email = Build(bodyText: "Voir https://evil.example.com/login.");

        var urls = Extractor.ExtractUrls(email);

        Assert.Equal("https", urls[0].Scheme);
    }

    [Fact]
    public void ExtractUrls_AdresseIpBrute_SignaleLaRaison()
    {
        var email = Build(bodyText: "Voir https://203.0.113.10/portal.");

        var url = Assert.Single(Extractor.ExtractUrls(email));

        Assert.True(url.IsSuspicious);
        Assert.Equal([BasicEmailUrlExtractor.IpLiteralHostReason], url.Reasons);
    }

    [Fact]
    public void ExtractUrls_IdentiteIntegree_SignaleLeMasquagePossibleDeLHost()
    {
        var email = Build(bodyText: "Voir https://support.example@evil.example/login.");

        var url = Assert.Single(Extractor.ExtractUrls(email));

        Assert.True(url.IsSuspicious);
        Assert.Contains(BasicEmailUrlExtractor.EmbeddedIdentityReason, url.Reasons);
        Assert.Equal("evil.example", url.Host);
    }

    [Fact]
    public void ExtractUrls_CheminSensibleSansHttps_SignaleLeRisque()
    {
        var email = Build(bodyText: "Voir http://example.test/account/verify.");

        var url = Assert.Single(Extractor.ExtractUrls(email));

        Assert.True(url.IsSuspicious);
        Assert.Equal([BasicEmailUrlExtractor.SensitivePathWithoutTlsReason], url.Reasons);
    }

    [Fact]
    public void ExtractUrls_CheminSensibleAvecHttpsOrdinaire_NeSuffitPasAQualifierLUrl()
    {
        var email = Build(bodyText: "Voir https://example.test/account/verify.");

        var url = Assert.Single(Extractor.ExtractUrls(email));

        Assert.False(url.IsSuspicious);
        Assert.Empty(url.Reasons);
    }

    [Fact]
    public void ExtractUrls_MotContenantLoginSansEtreUnSegment_NeDeclenchePasLaRegle()
    {
        var email = Build(bodyText: "Voir http://example.test/catalogin/items.");

        var url = Assert.Single(Extractor.ExtractUrls(email));

        Assert.False(url.IsSuspicious);
        Assert.Empty(url.Reasons);
    }

    [Fact]
    public void ExtractUrls_DomaineInternationaliseEtCheminSensible_SignaleLaCombinaison()
    {
        var email = Build(bodyText: "Voir https://xn--exmple-cua.test/login.");

        var url = Assert.Single(Extractor.ExtractUrls(email));

        Assert.True(url.IsSuspicious);
        Assert.Equal([BasicEmailUrlExtractor.InternationalizedSensitiveUrlReason], url.Reasons);
    }

    [Fact]
    public void ExtractUrls_LeveArgumentNullExceptionSiEmailEstNull()
    {
        Assert.Throws<ArgumentNullException>(() => Extractor.ExtractUrls(null!));
    }
}
