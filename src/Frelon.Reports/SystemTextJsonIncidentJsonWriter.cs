using System.Text.Json;
using Frelon.Core;

namespace Frelon.Reports;

/// <summary>
/// Implémentation de <see cref="IIncidentJsonWriter"/> utilisant <see cref="System.Text.Json"/>.
/// Produit un JSON indenté avec une politique de nommage camelCase.
/// N'effectue aucun appel réseau et n'écrit pas sur le disque.
/// </summary>
public sealed class SystemTextJsonIncidentJsonWriter : IIncidentJsonWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented        = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <inheritdoc/>
    public string Write(FraudIncident incident)
    {
        ArgumentNullException.ThrowIfNull(incident);

        return JsonSerializer.Serialize(incident, Options);
    }
}
