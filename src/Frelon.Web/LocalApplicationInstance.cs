using System.Text;

namespace Frelon.Web;

/// <summary>
/// Verrouille une instance Frelon par dossier de données et publie son adresse locale active.
/// </summary>
public sealed class LocalApplicationInstance : IDisposable
{
    private const string LockFileName = ".frelon.lock";
    private const string ActiveUrlFileName = ".frelon-url";
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    private readonly FileStream _lockStream;
    private readonly string _lockPath;
    private readonly string _activeUrlPath;
    private bool _disposed;

    private LocalApplicationInstance(FileStream lockStream, string lockPath, string activeUrlPath)
    {
        _lockStream = lockStream;
        _lockPath = lockPath;
        _activeUrlPath = activeUrlPath;
    }

    /// <summary>Tente d'acquérir l'instance associée au dossier sans attendre.</summary>
    public static LocalApplicationInstance? TryAcquire(string dataDirectory)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            throw new ArgumentException("Le dossier de données est obligatoire.", nameof(dataDirectory));
        }

        var fullDirectory = Path.GetFullPath(dataDirectory);
        Directory.CreateDirectory(fullDirectory);
        var lockPath = Path.Combine(fullDirectory, LockFileName);

        try
        {
            var stream = new FileStream(
                lockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.None);
            return new LocalApplicationInstance(
                stream,
                lockPath,
                Path.Combine(fullDirectory, ActiveUrlFileName));
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>Enregistre l'adresse réellement écoutée pour une seconde tentative de lancement.</summary>
    public void PublishActiveUrl(Uri localUrl)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(localUrl);
        if (!LocalBrowserLauncher.IsSafeLocalUrl(localUrl))
        {
            throw new ArgumentException("L'adresse active doit être une adresse HTTP locale.", nameof(localUrl));
        }

        File.WriteAllText(_activeUrlPath, localUrl.AbsoluteUri, Utf8WithoutBom);
    }

    /// <summary>Lit l'adresse publiée par l'instance déjà active, si elle reste sûre.</summary>
    public static Uri? TryReadActiveUrl(string dataDirectory)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            return null;
        }

        try
        {
            var path = Path.Combine(Path.GetFullPath(dataDirectory), ActiveUrlFileName);
            var value = File.ReadAllText(path, Encoding.UTF8).Trim();
            return Uri.TryCreate(value, UriKind.Absolute, out var url)
                && LocalBrowserLauncher.IsSafeLocalUrl(url)
                    ? url
                    : null;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        TryDelete(_activeUrlPath);
        _lockStream.Dispose();
        TryDelete(_lockPath);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Le nettoyage ne doit pas masquer l'arrêt de l'application.
        }
    }
}
