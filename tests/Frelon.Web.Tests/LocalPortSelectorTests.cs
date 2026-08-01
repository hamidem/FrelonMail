using System.Net;
using System.Net.Sockets;

namespace Frelon.Web.Tests;

public sealed class LocalPortSelectorTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(65536)]
    public void SelectAvailable_PortInvalide_Refuse(int port)
        => Assert.Throws<ArgumentOutOfRangeException>(() => LocalPortSelector.SelectAvailable(port));

    [Fact]
    public void IsAvailable_PortDejaEcoute_Refuse()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        Assert.False(LocalPortSelector.IsAvailable(port));
    }

    [Fact]
    public void SelectAvailable_PortOccupe_ChoisitUnAutrePortLocal()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var occupiedPort = ((IPEndPoint)listener.LocalEndpoint).Port;

        var selectedPort = LocalPortSelector.SelectAvailable(occupiedPort);

        Assert.NotEqual(occupiedPort, selectedPort);
        Assert.InRange(selectedPort, IPEndPoint.MinPort, IPEndPoint.MaxPort);
        Assert.True(LocalPortSelector.IsAvailable(selectedPort));
    }

    [Fact]
    public void SelectAvailable_PortLibre_ConserveLePortPrefere()
    {
        int freePort;
        using (var listener = new TcpListener(IPAddress.Loopback, 0))
        {
            listener.Start();
            freePort = ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        Assert.Equal(freePort, LocalPortSelector.SelectAvailable(freePort));
    }
}
