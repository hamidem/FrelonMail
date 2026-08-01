using System.Text.Json;
using System.Text.Json.Serialization;
using Frelon.Core;

namespace Frelon.Reports;

/// <summary>
/// Implémentation de <see cref="IIocsJsonWriter"/> utilisant <see cref="System.Text.Json"/>.
/// Produit un JSON indenté avec une politique de nommage camelCase et les enums en chaîne.
/// N'effectue aucun appel réseau et n'écrit pas sur le disque.
/// </summary>
public sealed class SystemTextJsonIocsJsonWriter : IIocsJsonWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented        = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters           = { new JsonStringEnumConverter() },
    };

    /// <inheritdoc/>
    public string Write(FraudIncident incident)
    {
        ArgumentNullException.ThrowIfNull(incident);

        var document = new IocsJsonDocument
        {
            IncidentId  = incident.IncidentId,
            GeneratedAt = DateTimeOffset.UtcNow,
            Iocs        = incident.Iocs,
        };

        return JsonSerializer.Serialize(document, Options);
    }
}
