using Xunit;

namespace Frelon.Core.Tests;

/// <summary>Vérifie les invariants d'une décision humaine de campagne.</summary>
public sealed class CampaignReviewDecisionTests
{
    private static readonly Guid ReviewId =
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateTimeOffset DecidedAt =
        new(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);
    private static readonly CampaignCandidate Candidate = BuildCandidate();

    [Theory]
    [InlineData(CampaignReviewVerdict.Inconclusive)]
    [InlineData(CampaignReviewVerdict.Rejected)]
    [InlineData(CampaignReviewVerdict.Confirmed)]
    public void Constructeur_VerdictConnu_ConserveLeSnapshotEtLaDecision(
        CampaignReviewVerdict verdict)
    {
        var decision = new CampaignReviewDecision(
            ReviewId,
            Candidate,
            verdict,
            DecidedAt,
            "  Vérification manuelle.  ");

        Assert.Same(Candidate, decision.CandidateSnapshot);
        Assert.Equal(Candidate.Fingerprint, decision.CandidateFingerprint);
        Assert.Equal(verdict, decision.Verdict);
        Assert.Equal(DecidedAt, decision.DecidedAt);
        Assert.Equal("Vérification manuelle.", decision.Notes);
    }

    [Fact]
    public void Constructeur_NotesBlanches_LesNormaliseEnNull()
    {
        var decision = new CampaignReviewDecision(
            ReviewId,
            Candidate,
            CampaignReviewVerdict.Inconclusive,
            DecidedAt,
            "   ");

        Assert.Null(decision.Notes);
    }

    [Fact]
    public void Constructeur_IdentifiantVide_RefuseLaDecision()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new CampaignReviewDecision(
                Guid.Empty,
                Candidate,
                CampaignReviewVerdict.Confirmed,
                DecidedAt));

        Assert.Equal("reviewId", exception.ParamName);
    }

    [Fact]
    public void Constructeur_SansCandidate_RefuseLaDecision()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => new CampaignReviewDecision(
                ReviewId,
                null!,
                CampaignReviewVerdict.Confirmed,
                DecidedAt));

        Assert.Equal("candidateSnapshot", exception.ParamName);
    }

    [Fact]
    public void Constructeur_VerdictInconnu_RefuseLaDecision()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new CampaignReviewDecision(
                ReviewId,
                Candidate,
                (CampaignReviewVerdict)99,
                DecidedAt));

        Assert.Equal("verdict", exception.ParamName);
    }

    [Fact]
    public void Constructeur_SansDate_RefuseLaDecision()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new CampaignReviewDecision(
                ReviewId,
                Candidate,
                CampaignReviewVerdict.Confirmed,
                default));

        Assert.Equal("decidedAt", exception.ParamName);
    }

    [Fact]
    public void Constructeur_NotesTropLongues_RefuseLaDecision()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new CampaignReviewDecision(
                ReviewId,
                Candidate,
                CampaignReviewVerdict.Confirmed,
                DecidedAt,
                new string('a', CampaignReviewDecision.MaxNotesLength + 1)));

        Assert.Equal("notes", exception.ParamName);
    }

    private static CampaignCandidate BuildCandidate()
    {
        var firstIncidentId =
            Guid.Parse("11111111-1111-1111-1111-111111111111");
        var secondIncidentId =
            Guid.Parse("22222222-2222-2222-2222-222222222222");

        return new CampaignCandidate(
            [firstIncidentId, secondIncidentId],
            new DateTimeOffset(2026, 7, 23, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 23, 9, 0, 0, TimeSpan.Zero),
            [
                new IncidentCorrelationLink(
                    firstIncidentId,
                    secondIncidentId,
                    [
                        new SharedIocMatch(
                            IocType.Url,
                            "https://fraud.example/login",
                            BasicIncidentCorrelator.UrlWeight),
                    ]),
            ]);
    }
}
