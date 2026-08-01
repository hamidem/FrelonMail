using System.Diagnostics;

namespace Frelon.Web;

/// <summary>Ouvre uniquement une adresse HTTP de boucle locale avec le navigateur du système.</summary>
public static class LocalBrowserLauncher
{
    /// <summary>Vérifie qu'une adresse ne peut pas provoquer une navigation distante.</summary>
    public static bool IsSafeLocalUrl(Uri? url)
        => url is { IsAbsoluteUri: true }
            && string.Equals(url.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && url.IsLoopback;

    /// <summary>Tente d'ouvrir le navigateur, sans faire échouer Frelon si le système le refuse.</summary>
    public static bool TryOpen(Uri localUrl)
    {
        ArgumentNullException.ThrowIfNull(localUrl);
        if (!IsSafeLocalUrl(localUrl))
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo(localUrl.AbsoluteUri)
            {
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or System.ComponentModel.Win32Exception or PlatformNotSupportedException)
        {
            return false;
        }
    }
}
