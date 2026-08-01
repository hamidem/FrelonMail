using Frelon.Core;
using Xunit;

namespace Frelon.Mail.Tests;

/// <summary>
/// Tests de <see cref="BasicEmailHeaderAnalyzer"/>.
/// </summary>
public class BasicEmailHeaderAnalyzerTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Construit un <see cref="ParsedEmail"/> à partir d'une liste de paires nom/valeur.
    /// </summary>
    private static ParsedEmail BuildEmail(params (string Name, string Value)[] headers)
    {
        return new ParsedEmail
        {
            RawContent = string.Empty,
            SourceSha256 = string.Empty,
            Headers    = headers.Select(h => new ParsedEmailHeader { Name = h.Name, Value = h.Value }).ToList(),
        };
    }

    private static readonly BasicEmailHeaderAnalyzer Analyzer = new();

    // ── ExtractIdentity ───────────────────────────────────────────────────────

    [Fact]
    public void ExtractIdentity_ExtraitFromReplyToReturnPathMessageIdEtSubject()
    {
        var email = BuildEmail(
            ("Return-Path", "<bounce@example.net>"),
            ("From",        "Fake Support <support@example.net>"),
            ("Reply-To",    "reply@example.net"),
            ("Message-ID",  "<abc123@example.net>"),
            ("Subject",     "Suspicious login attempt"));

        MailIdentity identity = Analyzer.ExtractIdentity(email);

        Assert.Equal("Fake Support <support@example.net>", identity.From);
        Assert.Equal("reply@example.net",                  identity.ReplyTo);
        Assert.Equal("<bounce@example.net>",               identity.ReturnPath);
        Assert.Equal("<abc123@example.net>",               identity.MessageId);
        Assert.Equal("Suspicious login attempt",           identity.Subject);
    }

    [Fact]
    public void ExtractIdentity_NePlantePasQuandCertainsHeadersSontAbsents()
    {
        var email = BuildEmail(("From", "sender@example.com"));

        MailIdentity identity = Analyzer.ExtractIdentity(email);

        Assert.Equal("sender@example.com", identity.From);
        Assert.Null(identity.ReplyTo);
        Assert.Null(identity.ReturnPath);
        Assert.Null(identity.MessageId);
        Assert.Null(identity.Subject);
    }

    [Fact]
    public void ExtractIdentity_NePlantePasSiAucunHeaderPresent()
    {
        var email = BuildEmail();

        MailIdentity identity = Analyzer.ExtractIdentity(email);

        Assert.Null(identity.From);
        Assert.Null(identity.ReplyTo);
        Assert.Null(identity.ReturnPath);
        Assert.Null(identity.MessageId);
        Assert.Null(identity.Subject);
    }

    // ── ExtractAuthentication ─────────────────────────────────────────────────

    [Fact]
    public void ExtractAuthentication_ConserveLaValeurBruteAuthenticationResults()
    {
        const string rawValue =
            "mx.example.org; spf=pass smtp.mailfrom=example.net; dkim=fail; dmarc=none";

        var email = BuildEmail(("Authentication-Results", rawValue));

        AuthenticationAssessment auth = Analyzer.ExtractAuthentication(email);

        Assert.Equal(rawValue, auth.AuthenticationResultsRaw);
    }

    [Fact]
    public void ExtractAuthentication_DetecteSpfPassDkimFailDmarcNone()
    {
        var email = BuildEmail(
            ("Authentication-Results",
             "mx.example.org; spf=pass smtp.mailfrom=example.net; dkim=fail; dmarc=none"));

        AuthenticationAssessment auth = Analyzer.ExtractAuthentication(email);

        Assert.Equal("pass", auth.SpfResult);
        Assert.Equal("fail", auth.DkimResult);
        Assert.Equal("none", auth.DmarcResult);
    }

    [Fact]
    public void ExtractAuthentication_RetourneValeursNulleSiHeaderAbsent()
    {
        var email = BuildEmail(("From", "sender@example.com"));

        AuthenticationAssessment auth = Analyzer.ExtractAuthentication(email);

        Assert.Null(auth.AuthenticationResultsRaw);
        Assert.Null(auth.SpfResult);
        Assert.Null(auth.DkimResult);
        Assert.Null(auth.DmarcResult);
    }

    // ── ExtractReceivedChain ──────────────────────────────────────────────────

    [Fact]
    public void ExtractReceivedChain_ConservePlusieursHeadersReceived()
    {
        var email = BuildEmail(
            ("Received", "from first.example by mx.example.org"),
            ("Received", "from second.example by first.example"));

        IReadOnlyList<ReceivedHop> chain = Analyzer.ExtractReceivedChain(email);

        Assert.Equal(2, chain.Count);
    }

    [Fact]
    public void ExtractReceivedChain_ConserveLordreDesHeadersReceived()
    {
        var email = BuildEmail(
            ("Received", "from first.example by mx.example.org"),
            ("Received", "from second.example by first.example"));

        IReadOnlyList<ReceivedHop> chain = Analyzer.ExtractReceivedChain(email);

        Assert.Equal(0, chain[0].Position);
        Assert.Equal("from first.example by mx.example.org",  chain[0].RawValue);
        Assert.Equal(1, chain[1].Position);
        Assert.Equal("from second.example by first.example", chain[1].RawValue);
    }

    [Fact]
    public void ExtractReceivedChain_RetourneListeVideSiAucunHeaderReceived()
    {
        var email = BuildEmail(("From", "sender@example.com"));

        IReadOnlyList<ReceivedHop> chain = Analyzer.ExtractReceivedChain(email);

        Assert.Empty(chain);
    }

    // ── Garanties défensives ──────────────────────────────────────────────────

    [Fact]
    public void ExtractIdentity_NeProvoqueAucunAppelReseau()
    {
        // Ce test est un test de conception : aucune exception de type réseau
        // ne doit être levée lors de l'extraction de l'identité.
        var email = BuildEmail(("From", "sender@example.com"));

        var exception = Record.Exception(() => { Analyzer.ExtractIdentity(email); });

        Assert.Null(exception);
    }

    [Fact]
    public void ExtractAuthentication_NeProvoqueAucunAppelReseau()
    {
        var email = BuildEmail(
            ("Authentication-Results",
             "mx.example.org; spf=pass smtp.mailfrom=example.net; dkim=fail; dmarc=none"));

        var exception = Record.Exception(() => { Analyzer.ExtractAuthentication(email); });

        Assert.Null(exception);
    }

    [Fact]
    public void ExtractReceivedChain_NeProvoqueAucunAppelReseau()
    {
        var email = BuildEmail(("Received", "from first.example by mx.example.org"));

        var exception = Record.Exception(() => { Analyzer.ExtractReceivedChain(email); });

        Assert.Null(exception);
    }
}
