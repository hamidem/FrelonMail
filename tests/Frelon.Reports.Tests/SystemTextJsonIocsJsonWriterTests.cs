using Frelon.Core;
using Xunit;

namespace Frelon.Reports.Tests;

/// <summary>
/// Tests de <see cref="SystemTextJsonIocsJsonWriter"/>.
/// </summary>
public class SystemTextJsonIocsJsonWriterTests
{
    // ── Fixture ───────────────────────────────────────────────────────────────

    private static FraudIncident BuildTestIncident() => new()
    {
        IncidentId     = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        CreatedAt      = new DateTimeOffset(2026, 7, 3, 12, 0, 0, TimeSpan.Zero),
        Evidence       = new EvidenceSource { FileName = "suspicious.eml" },
        Identity       = new MailIdentity(),
        Authentication = new AuthenticationAssessment(),
        Classification = FraudClassification.Unknown,
        RiskScore      = new RiskScore { Value = 0, Level = RiskLevel.Unknown },
        Iocs           =
        [
            new Ioc
            {
                Type       = IocType.Domain,
                Value      = "evil.example.com",
                Confidence = 0.9,
            },
            new Ioc
            {
                Type       = IocType.Url,
                Value      = "http://evil.example.com/login",
                Confidence = 0.85,
            },
            new Ioc
            {
                Type       = IocType.Hash,
                Value      = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                Confidence = 1.0,
            },
        ],
    };

    private static FraudIncident BuildEmptyIocsIncident() => new()
    {
        IncidentId     = Guid.Parse("22222222-2222-2222-2222-222222222222"),
        CreatedAt      = new DateTimeOffset(2026, 7, 3, 12, 0, 0, TimeSpan.Zero),
        Evidence       = new EvidenceSource { FileName = "empty.eml" },
        Identity       = new MailIdentity(),
        Authentication = new AuthenticationAssessment(),
        Classification = FraudClassification.Unknown,
        RiskScore      = new RiskScore { Value = 0, Level = RiskLevel.Unknown },
    };

    private static readonly SystemTextJsonIocsJsonWriter Writer = new();

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Write_RetourneUneChainJsonNonVide()
    {
        string json = Writer.Write(BuildTestIncident());

        Assert.False(string.IsNullOrWhiteSpace(json));
    }

    [Fact]
    public void Write_ContientIncidentId()
    {
        string json = Writer.Write(BuildTestIncident());

        using var document = System.Text.Json.JsonDocument.Parse(json);

        Assert.Equal(
            "11111111-1111-1111-1111-111111111111",
            document.RootElement.GetProperty("incidentId").GetString());
    }

    [Fact]
    public void Write_ContientProprietéIocs()
    {
        string json = Writer.Write(BuildTestIncident());

        Assert.Contains("iocs", json);
    }

    [Fact]
    public void Write_ContientLesIocsDeIncident()
    {
        string json = Writer.Write(BuildTestIncident());

        Assert.Contains("evil.example.com", json);
        Assert.Contains("http://evil.example.com/login", json);
        Assert.Contains("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", json);
    }

    [Fact]
    public void Write_ConserveLeTypeDeIoc()
    {
        string json = Writer.Write(BuildTestIncident());

        Assert.Contains("Domain", json);
        Assert.Contains("Url", json);
        Assert.Contains("Hash", json);
    }

    [Fact]
    public void Write_ConserveLaValeurDeIoc()
    {
        string json = Writer.Write(BuildTestIncident());

        Assert.Contains("evil.example.com", json);
    }

    [Fact]
    public void Write_ConserveLaConfianceDeIoc()
    {
        string json = Writer.Write(BuildTestIncident());

        Assert.Contains("0.9", json);
        Assert.Contains("0.85", json);
    }

    [Fact]
    public void Write_IocsVideSiAucunIocDansIncident()
    {
        string json = Writer.Write(BuildEmptyIocsIncident());

        Assert.Contains("\"iocs\": []", json);
    }

    [Fact]
    public void Write_LeveArgumentNullExceptionSiIncidentEstNull()
    {
        Assert.Throws<ArgumentNullException>(() => Writer.Write(null!));
    }

    [Fact]
    public void Write_ProduiteUnJsonIndente()
    {
        string json = Writer.Write(BuildTestIncident());

        Assert.Contains('\n', json);
    }
}
