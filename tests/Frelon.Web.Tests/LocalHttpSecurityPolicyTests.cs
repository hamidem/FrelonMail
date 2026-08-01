using System.Net;
using Microsoft.AspNetCore.Http;

namespace Frelon.Web.Tests;

/// <summary>Vérifie la fermeture du serveur embarqué aux requêtes non locales.</summary>
public sealed class LocalHttpSecurityPolicyTests
{
    private const int Port = 5127;

    [Theory]
    [InlineData("localhost", "127.0.0.1")]
    [InlineData("LOCALHOST", "::1")]
    [InlineData("127.0.0.1", "127.0.0.1")]
    [InlineData("::1", "::1")]
    public void IsAllowedRequest_BoucleLocaleAutorisee(
        string host,
        string remoteAddress)
    {
        var context = CreateContext(host, IPAddress.Parse(remoteAddress));

        Assert.True(LocalHttpSecurityPolicy.IsAllowedRequest(context, Port));
    }

    [Fact]
    public void IsAllowedRequest_OrigineLocaleEtNavigationDirecte_Autorisees()
    {
        var sameOrigin = CreateContext("localhost", IPAddress.Loopback);
        sameOrigin.Request.Headers.Origin = $"http://localhost:{Port}";
        sameOrigin.Request.Headers["Sec-Fetch-Site"] = "same-origin";

        var directNavigation = CreateContext("localhost", IPAddress.Loopback);
        directNavigation.Request.Headers["Sec-Fetch-Site"] = "none";

        Assert.True(LocalHttpSecurityPolicy.IsAllowedRequest(sameOrigin, Port));
        Assert.True(LocalHttpSecurityPolicy.IsAllowedRequest(directNavigation, Port));
    }

    [Theory]
    [InlineData("attacker.test", "127.0.0.1", null, null)]
    [InlineData("localhost", "192.0.2.10", null, null)]
    [InlineData("localhost", "127.0.0.1", "https://attacker.test", null)]
    [InlineData("localhost", "127.0.0.1", "http://localhost:9999", null)]
    [InlineData("localhost", "127.0.0.1", null, "cross-site")]
    [InlineData("localhost", "127.0.0.1", null, "same-site")]
    public void IsAllowedRequest_SourceNonAttendue_Refuse(
        string host,
        string remoteAddress,
        string? origin,
        string? fetchSite)
    {
        var context = CreateContext(host, IPAddress.Parse(remoteAddress));
        if (origin is not null)
        {
            context.Request.Headers.Origin = origin;
        }

        if (fetchSite is not null)
        {
            context.Request.Headers["Sec-Fetch-Site"] = fetchSite;
        }

        Assert.False(LocalHttpSecurityPolicy.IsAllowedRequest(context, Port));
    }

    [Fact]
    public void IsAllowedRequest_PortDifferent_Refuse()
    {
        var context = CreateContext("localhost", IPAddress.Loopback, Port + 1);

        Assert.False(LocalHttpSecurityPolicy.IsAllowedRequest(context, Port));
    }

    [Fact]
    public void IsAllowedRequest_AdresseDistanteAbsente_RefuseParDefaut()
    {
        var context = CreateContext("localhost", null);

        Assert.False(LocalHttpSecurityPolicy.IsAllowedRequest(context, Port));
    }

    [Fact]
    public void ApplyResponseHeaders_InterditContenuEtIntegrationTiers()
    {
        var context = new DefaultHttpContext();

        LocalHttpSecurityPolicy.ApplyResponseHeaders(context.Response.Headers);

        Assert.Equal(
            LocalHttpSecurityPolicy.ContentSecurityPolicy,
            context.Response.Headers["Content-Security-Policy"]);
        Assert.Equal("no-store", context.Response.Headers["Cache-Control"]);
        Assert.Equal("same-origin", context.Response.Headers["Cross-Origin-Opener-Policy"]);
        Assert.Equal("same-origin", context.Response.Headers["Cross-Origin-Resource-Policy"]);
        Assert.Equal("no-referrer", context.Response.Headers["Referrer-Policy"]);
        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"]);
        Assert.Equal("DENY", context.Response.Headers["X-Frame-Options"]);
    }

    private static DefaultHttpContext CreateContext(
        string host,
        IPAddress? remoteAddress,
        int port = Port)
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(host, port);
        context.Connection.RemoteIpAddress = remoteAddress;
        return context;
    }
}
