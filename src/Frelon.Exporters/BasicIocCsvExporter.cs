using System.Globalization;
using System.Text;
using Frelon.Core;

namespace Frelon.Exporters;

/// <summary>
/// Produit un CSV d'IOC stable, invariant et protégé contre l'injection de formules de tableur.
/// </summary>
public sealed class BasicIocCsvExporter : IIocCsvExporter
{
    private const string Header = "type,value,confidence,source,firstSeen\r\n";

    /// <inheritdoc />
    public string Export(FraudIncident incident)
    {
        ArgumentNullException.ThrowIfNull(incident);

        var csv = new StringBuilder(Header);
        foreach (var ioc in incident.Iocs)
        {
            AppendCell(csv, ioc.Type.ToString());
            csv.Append(',');
            AppendCell(csv, ioc.Value);
            csv.Append(',');
            AppendCell(csv, ioc.Confidence.ToString("R", CultureInfo.InvariantCulture));
            csv.Append(',');
            AppendCell(csv, ioc.Source ?? string.Empty);
            csv.Append(',');
            AppendCell(csv, ioc.FirstSeen?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty);
            csv.Append("\r\n");
        }

        return csv.ToString();
    }

    private static void AppendCell(StringBuilder csv, string value)
    {
        var safeValue = ProtectFromSpreadsheetFormula(value);
        if (safeValue.IndexOfAny([',', '"', '\r', '\n']) < 0)
        {
            csv.Append(safeValue);
            return;
        }

        csv.Append('"');
        csv.Append(safeValue.Replace("\"", "\"\"", StringComparison.Ordinal));
        csv.Append('"');
    }

    private static string ProtectFromSpreadsheetFormula(string value)
    {
        var trimmed = value.AsSpan().TrimStart();
        if (!trimmed.IsEmpty && trimmed[0] is '=' or '+' or '-' or '@')
        {
            return $"'{value}";
        }

        return value;
    }
}
