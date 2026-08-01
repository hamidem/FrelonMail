using Frelon.Web;

namespace Frelon.Web.Tests;

public sealed class LocalApplicationControlTests
{
    [Fact]
    public void ShutdownToken_IsAcceptedByItsOwnInstance()
    {
        var control = new LocalApplicationControl();

        Assert.True(control.IsShutdownAuthorized(control.ShutdownToken));
    }

    [Fact]
    public void ShutdownToken_IsUniqueToEachInstance()
    {
        var first = new LocalApplicationControl();
        var second = new LocalApplicationControl();

        Assert.NotEqual(first.ShutdownToken, second.ShutdownToken);
        Assert.False(first.IsShutdownAuthorized(second.ShutdownToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-hexadecimal")]
    [InlineData("00")]
    public void InvalidShutdownToken_IsRejected(string? candidate)
    {
        var control = new LocalApplicationControl();

        Assert.False(control.IsShutdownAuthorized(candidate));
    }
}
