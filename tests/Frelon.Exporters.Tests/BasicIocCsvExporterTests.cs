using System.Globalization;
using Frelon.Core;

namespace Frelon.Exporters.Tests;

/// <summary>Vérifie le contrat stable et défensif de l'export CSV des IOC.</summary>
public sealed class BasicIocCsvExporterTests
{
    private static readonly DateTimeOffset FirstSeen =
        new(2026, 7, 15, 18, 30, 45, TimeSpan.FromHours(2));

    private readonly BasicIocCsvExporter _exporter = new();

    [Fact]
    public void Export_IncidentNull_LeveArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _exporter.Export(null!));
    }

    [Fact]
    public void Export_SansIoc_ProduitUniquementLEntete()
    {
        var csv = _exporter.Export(BuildIncident());

        Assert.Equal("type,value,confidence,source,firstSeen\r\n", csv);
    }

    [Fact]
    public void Export_IocComplet_ProduitLesColonnesAttendues()
    {
        var csv = _exporter.Export(BuildIncident(new Ioc
        {
            Type = IocType.Domain,
            Value = "phishing.example",
            Confidence = 0.95,
            Source = "url-extractor",
            FirstSeen = FirstSeen
        }));

        Assert.Equal(
            "type,value,confidence,source,firstSeen\r\n" +
            "Domain,phishing.example,0.95,url-extractor,2026-07-15T18:30:45.0000000+02:00\r\n",
            csv);
    }

    [Fact]
    public void Export_ConserveLOrdreDesIoc()
    {
        var csv = _exporter.Export(BuildIncident(
            CreateIoc(IocType.Url, "https://first.example"),
            CreateIoc(IocType.Hash, "aaaaaaaa")));

        Assert.True(csv.IndexOf("https://first.example", StringComparison.Ordinal)
            < csv.IndexOf("aaaaaaaa", StringComparison.Ordinal));
    }

    [Fact]
    public void Export_EchappeVirgulesGuillemetsEtRetoursLigne()
    {
        var csv = _exporter.Export(BuildIncident(new Ioc
        {
            Type = IocType.FileName,
            Value = "invoice,\"urgent\"\r\n.exe",
            Confidence = 1,
            Source = "attachment"
        }));

        Assert.Contains("\"invoice,\"\"urgent\"\"\r\n.exe\"", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_ChampsOptionnelsAbsents_ProduitDesCellulesVides()
    {
        var csv = _exporter.Export(BuildIncident(CreateIoc(IocType.Hash, "abcd")));

        Assert.Contains("Hash,abcd,0,,\r\n", csv, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("=HYPERLINK(\"https://evil.example\")")]
    [InlineData("+SUM(1,1)")]
    [InlineData("-1+2")]
    [InlineData("@malicious")]
    [InlineData("  =cmd")]
    public void Export_CelluleInterpretableCommeFormule_EstNeutralisee(string dangerousValue)
    {
        var csv = _exporter.Export(BuildIncident(CreateIoc(IocType.Domain, dangerousValue)));

        var separatorIndex = dangerousValue.IndexOfAny([',', '"', '\r', '\n']);
        var prefixBeforeCsvEscaping = separatorIndex < 0
            ? dangerousValue
            : dangerousValue[..separatorIndex];

        Assert.Contains($"'{prefixBeforeCsvEscaping}", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_UtiliseUneCultureInvariantePourLaConfiance()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

            var csv = _exporter.Export(BuildIncident(new Ioc
            {
                Type = IocType.Domain,
                Value = "example.test",
                Confidence = 0.75
            }));

            Assert.Contains(",0.75,", csv, StringComparison.Ordinal);
            Assert.DoesNotContain(",0,75,", csv, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public void Export_UtiliseToujoursCrLf()
    {
        var csv = _exporter.Export(BuildIncident(CreateIoc(IocType.Domain, "example.test")));

        Assert.DoesNotContain('\n', csv.Replace("\r\n", string.Empty, StringComparison.Ordinal));
    }

    [Fact]
    public void Export_NeModifiePasLIncident()
    {
        var ioc = CreateIoc(IocType.Domain, "=dangerous");
        var incident = BuildIncident(ioc);

        _exporter.Export(incident);

        Assert.Equal("=dangerous", ioc.Value);
        Assert.Same(ioc, Assert.Single(incident.Iocs));
    }

    private static Ioc CreateIoc(IocType type, string value)
        => new()
        {
            Type = type,
            Value = value,
            Confidence = 0
        };

    private static FraudIncident BuildIncident(params Ioc[] iocs)
        => new()
        {
            IncidentId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            CreatedAt = new DateTimeOffset(2026, 7, 15, 18, 0, 0, TimeSpan.Zero),
            Evidence = new EvidenceSource { FileName = "suspicious.eml" },
            Identity = new MailIdentity(),
            Authentication = new AuthenticationAssessment(),
            Iocs = iocs,
            Classification = FraudClassification.Unknown,
            RiskScore = new RiskScore { Value = 0, Level = RiskLevel.Unknown }
        };
}
