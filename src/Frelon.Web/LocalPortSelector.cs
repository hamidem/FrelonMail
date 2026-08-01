using System.Net;
using System.Net.Sockets;

namespace Frelon.Web;

/// <summary>Sélectionne un port disponible uniquement pour l'écoute locale.</summary>
public static class LocalPortSelector
{
    /// <summary>Conserve le port préféré lorsqu'il est libre, sinon choisit un port dynamique.</summary>
    public static int SelectAvailable(int preferredPort)
    {
        if (preferredPort is < 1 or > IPEndPoint.MaxPort)
        {
            throw new ArgumentOutOfRangeException(nameof(preferredPort));
        }

        if (IsAvailable(preferredPort))
        {
            return preferredPort;
        }

        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var candidate = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            if (IsAvailable(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("Aucun port local n'est disponible pour démarrer Frelon.");
    }

    /// <summary>Vérifie la disponibilité du port sur les boucles locales IPv4 et IPv6.</summary>
    public static bool IsAvailable(int port)
    {
        if (port is < 1 or > IPEndPoint.MaxPort)
        {
            return false;
        }

        if (!CanBind(IPAddress.Loopback, port))
        {
            return false;
        }

        return !Socket.OSSupportsIPv6 || CanBind(IPAddress.IPv6Loopback, port);
    }

    private static bool CanBind(IPAddress address, int port)
    {
        TcpListener? listener = null;
        try
        {
            listener = new TcpListener(address, port);
            listener.Server.ExclusiveAddressUse = true;
            listener.Start();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        finally
        {
            listener?.Stop();
        }
    }
}
