namespace Frelon.Web.Tests;

public sealed class LocalBrowserLauncherTests
{
    [Theory]
    [InlineData("http://localhost:5127/")]
    [InlineData("http://127.0.0.1:5127/")]
    [InlineData("http://[::1]:5127/")]
    public void IsSafeLocalUrl_BoucleLocaleHttp_Accepte(string value)
        => Assert.True(LocalBrowserLauncher.IsSafeLocalUrl(new Uri(value)));

    [Theory]
    [InlineData("https://localhost:5127/")]
    [InlineData("http://example.test:5127/")]
    [InlineData("file:///tmp/frelon")]
    public void IsSafeLocalUrl_AutreDestination_Refuse(string value)
        => Assert.False(LocalBrowserLauncher.IsSafeLocalUrl(new Uri(value)));

    [Fact]
    public void IsSafeLocalUrl_ValeurNulle_Refuse()
        => Assert.False(LocalBrowserLauncher.IsSafeLocalUrl(null));
}
