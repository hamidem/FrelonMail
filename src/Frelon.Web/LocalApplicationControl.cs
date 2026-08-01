using System.Security.Cryptography;

namespace Frelon.Web;

/// <summary>Protège les commandes qui agissent sur le cycle de vie de l'application locale.</summary>
public sealed class LocalApplicationControl
{
    private readonly byte[] _shutdownToken = RandomNumberGenerator.GetBytes(32);

    /// <summary>Jeton éphémère communiqué uniquement à l'interface servie par cette instance.</summary>
    public string ShutdownToken => Convert.ToHexString(_shutdownToken);

    /// <summary>Vérifie le jeton sans comparaison temporelle variable.</summary>
    public bool IsShutdownAuthorized(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        try
        {
            var candidateBytes = Convert.FromHexString(candidate);
            return candidateBytes.Length == _shutdownToken.Length
                && CryptographicOperations.FixedTimeEquals(candidateBytes, _shutdownToken);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
