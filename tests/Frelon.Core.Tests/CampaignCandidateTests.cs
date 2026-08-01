using Xunit;

namespace Frelon.Core.Tests;

/// <summary>Vérifie l'identité et les invariants d'une campagne candidate.</summary>
public sealed class CampaignCandidateTests
{
    private static readonly Guid FirstIncidentId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SecondIncidentId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ThirdIncidentId =
        Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset FirstObservedAt =
        new(2026, 7, 23, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LastObservedAt =
        new(2026, 7, 23, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructeur_TrieLesIncidentsEtCalculeUneEmpreinteStable()
    {
        var first = BuildCandidate(
            [SecondIncidentId, FirstIncidentId],
            [BuildLink(FirstIncidentId, SecondIncidentId, "https://fraud.example/one")]);
        var second = BuildCandidate(
            [FirstIncidentId, SecondIncidentId],
            [BuildLink(SecondIncidentId, FirstIncidentId, "https://fraud.example/two")]);

        Assert.Equal([FirstIncidentId, SecondIncidentId], first.IncidentIds);
        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.Equal(CampaignCandidate.FingerprintLength, first.Fingerprint.Length);
        Assert.True(CampaignCandidate.IsValidFingerprint(first.Fingerprint));
    }

    [Fact]
    public void Constructeur_CompositionDifferente_ProduitUneAutreEmpreinte()
    {
        var first = BuildCandidate(
            [FirstIncidentId, SecondIncidentId],
            [BuildLink(FirstIncidentId, SecondIncidentId)]);
        var second = BuildCandidate(
            [FirstIncidentId, ThirdIncidentId],
            [BuildLink(FirstIncidentId, ThirdIncidentId)]);

        Assert.NotEqual(first.Fingerprint, second.Fingerprint);
    }

    [Fact]
    public void Constructeur_IncidentSansLien_RefuseLaCampagne()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => BuildCandidate(
                [FirstIncidentId, SecondIncidentId, ThirdIncidentId],
                [BuildLink(FirstIncidentId, SecondIncidentId)]));

        Assert.Equal("links", exception.ParamName);
    }

    [Fact]
    public void Constructeur_GroupesDisjoints_RefuseLaCampagne()
    {
        var fourthIncidentId =
            Guid.Parse("44444444-4444-4444-4444-444444444444");

        var exception = Assert.Throws<ArgumentException>(
            () => BuildCandidate(
                [FirstIncidentId, SecondIncidentId, ThirdIncidentId, fourthIncidentId],
                [
                    BuildLink(FirstIncidentId, SecondIncidentId),
                    BuildLink(ThirdIncidentId, fourthIncidentId),
                ]));

        Assert.Equal("links", exception.ParamName);
    }

    [Fact]
    public void Constructeur_LienDupliqueDansLAutreSens_RefuseLaCampagne()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => BuildCandidate(
                [FirstIncidentId, SecondIncidentId],
                [
                    BuildLink(FirstIncidentId, SecondIncidentId),
                    BuildLink(SecondIncidentId, FirstIncidentId),
                ]));

        Assert.Equal("links", exception.ParamName);
    }

    [Fact]
    public void HasSameSnapshotAs_OrdresEtSensDifferents_ReconnaitLeMemeSnapshot()
    {
        var first = new CampaignCandidate(
            [FirstIncidentId, SecondIncidentId],
            FirstObservedAt,
            LastObservedAt,
            [
                new IncidentCorrelationLink(
                    FirstIncidentId,
                    SecondIncidentId,
                    [
                        new SharedIocMatch(
                            IocType.Url,
                            "https://fraud.example/login",
                            BasicIncidentCorrelator.UrlWeight),
                        new SharedIocMatch(
                            IocType.Hash,
                            new string('a', 64),
                            BasicIncidentCorrelator.HashWeight),
                    ]),
            ]);
        var second = new CampaignCandidate(
            [SecondIncidentId, FirstIncidentId],
            FirstObservedAt,
            LastObservedAt,
            [
                new IncidentCorrelationLink(
                    SecondIncidentId,
                    FirstIncidentId,
                    [
                        new SharedIocMatch(
                            IocType.Hash,
                            new string('a', 64),
                            BasicIncidentCorrelator.HashWeight),
                        new SharedIocMatch(
                            IocType.Url,
                            "https://fraud.example/login",
                            BasicIncidentCorrelator.UrlWeight),
                    ]),
            ]);

        Assert.True(first.HasSameSnapshotAs(second));
        Assert.True(second.HasSameSnapshotAs(first));
    }

    [Fact]
    public void HasSameSnapshotAs_MemeCompositionMaisJustificationModifiee_RetourneFalse()
    {
        var first = BuildCandidate(
            [FirstIncidentId, SecondIncidentId],
            [BuildLink(FirstIncidentId, SecondIncidentId, "https://fraud.example/one")]);
        var second = BuildCandidate(
            [FirstIncidentId, SecondIncidentId],
            [BuildLink(FirstIncidentId, SecondIncidentId, "https://fraud.example/two")]);

        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.False(first.HasSameSnapshotAs(second));
    }

    [Fact]
    public void HasSameSnapshotAs_HorodatageAvecOffsetDifferent_RetourneFalse()
    {
        var first = BuildCandidate(
            [FirstIncidentId, SecondIncidentId],
            [BuildLink(FirstIncidentId, SecondIncidentId)]);
        var second = new CampaignCandidate(
            [FirstIncidentId, SecondIncidentId],
            FirstObservedAt.ToOffset(TimeSpan.FromHours(1)),
            LastObservedAt.ToOffset(TimeSpan.FromHours(1)),
            [BuildLink(FirstIncidentId, SecondIncidentId)]);

        Assert.False(first.HasSameSnapshotAs(second));
        Assert.False(first.HasSameSnapshotAs(null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    public void IsValidFingerprint_ValeurInvalide_RetourneFalse(string? value)
    {
        Assert.False(CampaignCandidate.IsValidFingerprint(value));
    }

    private static CampaignCandidate BuildCandidate(
        IReadOnlyList<Guid> incidentIds,
        IReadOnlyList<IncidentCorrelationLink> links)
        => new(
            incidentIds,
            FirstObservedAt,
            LastObservedAt,
            links);

    private static IncidentCorrelationLink BuildLink(
        Guid firstIncidentId,
        Guid secondIncidentId,
        string value = "https://fraud.example/login")
        => new(
            firstIncidentId,
            secondIncidentId,
            [
                new SharedIocMatch(
                    IocType.Url,
                    value,
                    BasicIncidentCorrelator.UrlWeight),
            ]);
}
