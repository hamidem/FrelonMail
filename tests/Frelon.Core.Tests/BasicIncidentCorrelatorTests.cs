using Xunit;

namespace Frelon.Core.Tests;

/// <summary>
/// Tests du <see cref="BasicIncidentCorrelator"/>.
/// </summary>
public sealed class BasicIncidentCorrelatorTests
{
    private static readonly DateTimeOffset BaseDate =
        new(2026, 7, 23, 8, 0, 0, TimeSpan.Zero);

    private readonly BasicIncidentCorrelator _correlator = new();

    [Fact]
    public void FindCandidates_AvecCollectionNull_LeveArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => _correlator.FindCandidates(null!));

        Assert.Equal("incidents", exception.ParamName);
    }

    [Fact]
    public void FindCandidates_SansIncident_RetourneUneListeVide()
    {
        var candidates = _correlator.FindCandidates([]);

        Assert.Empty(candidates);
    }

    [Fact]
    public void FindCandidates_AvecUnSeulIncident_RetourneUneListeVide()
    {
        var candidates = _correlator.FindCandidates([BuildIncident(1)]);

        Assert.Empty(candidates);
    }

    [Fact]
    public void FindCandidates_AvecIdentifiantDuplique_RefuseUneEntreeAmbigue()
    {
        var incident = BuildIncident(1);

        var exception = Assert.Throws<ArgumentException>(
            () => _correlator.FindCandidates([incident, incident with { CreatedAt = BaseDate.AddMinutes(1) }]));

        Assert.Equal("incidents", exception.ParamName);
    }

    [Fact]
    public void FindCandidates_UnSeulDomaineCommun_NeSuffitPas()
    {
        var first = BuildIncident(1, Ioc(IocType.Domain, "fraud.example"));
        var second = BuildIncident(2, Ioc(IocType.Domain, "FRAUD.EXAMPLE."));

        var candidates = _correlator.FindCandidates([first, second]);

        Assert.Empty(candidates);
    }

    [Fact]
    public void FindCandidates_DeuxDomainesCommuns_FranchissentLeSeuil()
    {
        var first = BuildIncident(
            1,
            Ioc(IocType.Domain, "one.example"),
            Ioc(IocType.Domain, "two.example"));
        var second = BuildIncident(
            2,
            Ioc(IocType.Domain, "ONE.EXAMPLE."),
            Ioc(IocType.Domain, "two.example"));

        var candidate = Assert.Single(_correlator.FindCandidates([first, second]));
        var link = Assert.Single(candidate.Links);

        Assert.Equal(80, link.Score);
        Assert.All(link.Matches, match => Assert.Equal(IocType.Domain, match.Type));
    }

    [Fact]
    public void FindCandidates_UrlExacte_NormaliseCasseEtPortParDefaut()
    {
        var first = BuildIncident(
            1,
            Ioc(IocType.Url, "HTTP://Login.Example:80/account"));
        var second = BuildIncident(
            2,
            Ioc(IocType.Url, "http://login.example/account"));

        var link = Assert.Single(
            Assert.Single(_correlator.FindCandidates([first, second])).Links);
        var match = Assert.Single(link.Matches);

        Assert.Equal(IocType.Url, match.Type);
        Assert.Equal("http://login.example/account", match.Value);
        Assert.Equal(BasicIncidentCorrelator.UrlWeight, match.Weight);
    }

    [Fact]
    public void FindCandidates_HashExact_EstInsensibleALaCasse()
    {
        const string lowerHash =
            "abcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcd";
        var first = BuildIncident(1, Ioc(IocType.Hash, lowerHash));
        var second = BuildIncident(2, Ioc(IocType.Hash, lowerHash.ToUpperInvariant()));

        var link = Assert.Single(
            Assert.Single(_correlator.FindCandidates([first, second])).Links);

        Assert.Equal(BasicIncidentCorrelator.HashWeight, link.Score);
        Assert.Equal(lowerHash, Assert.Single(link.Matches).Value);
    }

    [Fact]
    public void FindCandidates_AdresseIpv6_UtiliseUneFormeCanonique()
    {
        var first = BuildIncident(1, Ioc(IocType.IpAddress, "2001:0db8:0:0:0:0:0:1"));
        var second = BuildIncident(2, Ioc(IocType.IpAddress, "2001:db8::1"));

        var link = Assert.Single(
            Assert.Single(_correlator.FindCandidates([first, second])).Links);

        Assert.Equal("2001:db8::1", Assert.Single(link.Matches).Value);
        Assert.Equal(BasicIncidentCorrelator.IpAddressWeight, link.Score);
    }

    [Fact]
    public void FindCandidates_Email_NormaliseUniquementLeDomaine()
    {
        var first = BuildIncident(1, Ioc(IocType.Email, "operator@Fraud.Example"));
        var second = BuildIncident(2, Ioc(IocType.Email, "operator@fraud.example"));

        var match = Assert.Single(
            Assert.Single(
                Assert.Single(_correlator.FindCandidates([first, second])).Links)
            .Matches);

        Assert.Equal("operator@fraud.example", match.Value);
        Assert.Equal(BasicIncidentCorrelator.EmailWeight, match.Weight);
    }

    [Fact]
    public void FindCandidates_NomDeFichierCommun_EstIgnore()
    {
        var first = BuildIncident(1, Ioc(IocType.FileName, "facture.zip", 1));
        var second = BuildIncident(2, Ioc(IocType.FileName, "facture.zip", 1));

        var candidates = _correlator.FindCandidates([first, second]);

        Assert.Empty(candidates);
    }

    [Fact]
    public void FindCandidates_IocSousLeSeuilDeConfiance_EstIgnore()
    {
        var first = BuildIncident(1, Ioc(IocType.Hash, Hash('a'), 0.49));
        var second = BuildIncident(2, Ioc(IocType.Hash, Hash('a'), 0.49));

        var candidates = _correlator.FindCandidates([first, second]);

        Assert.Empty(candidates);
    }

    [Fact]
    public void FindCandidates_DoublonsDansUnIncident_NeGonflentPasLeScore()
    {
        var first = BuildIncident(
            1,
            Ioc(IocType.Domain, "fraud.example"),
            Ioc(IocType.Domain, "FRAUD.EXAMPLE."));
        var second = BuildIncident(
            2,
            Ioc(IocType.Domain, "fraud.example"),
            Ioc(IocType.Domain, "fraud.example"));

        var candidates = _correlator.FindCandidates([first, second]);

        Assert.Empty(candidates);
    }

    [Fact]
    public void FindCandidates_MemePreuveImporteeDeuxFois_NeCreePasDeCampagne()
    {
        var first = BuildIncident(
            1,
            evidenceHash: Hash('e'),
            Ioc(IocType.Url, "https://fraud.example/login"));
        var second = BuildIncident(
            2,
            evidenceHash: Hash('E'),
            Ioc(IocType.Url, "https://fraud.example/login"));

        var candidates = _correlator.FindCandidates([first, second]);

        Assert.Empty(candidates);
    }

    [Fact]
    public void FindCandidates_LiensTransitif_ProduitUneSeuleCampagneExpliquee()
    {
        var first = BuildIncident(
            1,
            createdAt: BaseDate,
            Ioc(IocType.IpAddress, "203.0.113.10"));
        var second = BuildIncident(
            2,
            createdAt: BaseDate.AddMinutes(10),
            Ioc(IocType.IpAddress, "203.0.113.10"),
            Ioc(IocType.Email, "operator@fraud.example"));
        var third = BuildIncident(
            3,
            createdAt: BaseDate.AddMinutes(20),
            Ioc(IocType.Email, "operator@fraud.example"));

        var candidate = Assert.Single(
            _correlator.FindCandidates([third, first, second]));

        Assert.Equal([first.IncidentId, second.IncidentId, third.IncidentId], candidate.IncidentIds);
        Assert.Equal(BaseDate, candidate.FirstObservedAt);
        Assert.Equal(BaseDate.AddMinutes(20), candidate.LastObservedAt);
        Assert.Equal(2, candidate.Links.Count);
    }

    [Fact]
    public void FindCandidates_GroupesDisjoints_RestentDeuxCampagnes()
    {
        var first = BuildIncident(1, Ioc(IocType.Hash, Hash('a')));
        var second = BuildIncident(2, Ioc(IocType.Hash, Hash('a')));
        var third = BuildIncident(3, Ioc(IocType.Hash, Hash('b')));
        var fourth = BuildIncident(4, Ioc(IocType.Hash, Hash('b')));

        var candidates = _correlator.FindCandidates([fourth, second, third, first]);

        Assert.Collection(
            candidates,
            candidate => Assert.Equal([first.IncidentId, second.IncidentId], candidate.IncidentIds),
            candidate => Assert.Equal([third.IncidentId, fourth.IncidentId], candidate.IncidentIds));
    }

    private static FraudIncident BuildIncident(
        int id,
        params Ioc[] iocs)
        => BuildIncident(id, BaseDate.AddMinutes(id), null, iocs);

    private static FraudIncident BuildIncident(
        int id,
        DateTimeOffset createdAt,
        params Ioc[] iocs)
        => BuildIncident(id, createdAt, null, iocs);

    private static FraudIncident BuildIncident(
        int id,
        string? evidenceHash,
        params Ioc[] iocs)
        => BuildIncident(id, BaseDate.AddMinutes(id), evidenceHash, iocs);

    private static FraudIncident BuildIncident(
        int id,
        DateTimeOffset createdAt,
        string? evidenceHash,
        params Ioc[] iocs)
        => new()
        {
            IncidentId = Guid.Parse($"00000000-0000-0000-0000-{id:D12}"),
            CreatedAt = createdAt,
            Evidence = new EvidenceSource
            {
                FileName = $"incident-{id}.eml",
                Sha256 = evidenceHash,
            },
            Identity = new MailIdentity(),
            Authentication = new AuthenticationAssessment(),
            Iocs = iocs,
            Classification = FraudClassification.Unknown,
            RiskScore = new RiskScore
            {
                Value = 0,
                Level = RiskLevel.Unknown,
            },
        };

    private static Ioc Ioc(
        IocType type,
        string value,
        double confidence = BasicIncidentCorrelator.MinimumIocConfidence)
        => new()
        {
            Type = type,
            Value = value,
            Confidence = confidence,
        };

    private static string Hash(char character)
        => new(character, 64);
}
