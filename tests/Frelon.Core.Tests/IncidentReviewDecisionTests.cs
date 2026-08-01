using Frelon.Core;
using Xunit;

namespace Frelon.Core.Tests;

/// <summary>Vérifie les invariants d'une décision humaine.</summary>
public sealed class IncidentReviewDecisionTests
{
    private static readonly Guid ReviewId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid IncidentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset DecidedAt =
        new(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(ReviewVerdict.Inconclusive, null)]
    [InlineData(ReviewVerdict.Benign, null)]
    [InlineData(ReviewVerdict.Suspicious, FraudClassification.Suspicious)]
    [InlineData(ReviewVerdict.ConfirmedFraud, FraudClassification.Phishing)]
    [InlineData(ReviewVerdict.ConfirmedFraud, FraudClassification.Malware)]
    public void Constructeur_CombinaisonCoherente_ConserveLaDecision(
        ReviewVerdict verdict,
        FraudClassification? classification)
    {
        var decision = new IncidentReviewDecision(
            ReviewId,
            IncidentId,
            verdict,
            classification,
            DecidedAt,
            "  Vérifié manuellement.  ");

        Assert.Equal(verdict, decision.Verdict);
        Assert.Equal(classification, decision.Classification);
        Assert.Equal("Vérifié manuellement.", decision.Notes);
    }

    [Theory]
    [InlineData(ReviewVerdict.Inconclusive, FraudClassification.Phishing)]
    [InlineData(ReviewVerdict.Benign, FraudClassification.Spam)]
    [InlineData(ReviewVerdict.Suspicious, null)]
    [InlineData(ReviewVerdict.Suspicious, FraudClassification.Phishing)]
    [InlineData(ReviewVerdict.ConfirmedFraud, null)]
    [InlineData(ReviewVerdict.ConfirmedFraud, FraudClassification.Unknown)]
    [InlineData(ReviewVerdict.ConfirmedFraud, FraudClassification.Suspicious)]
    public void Constructeur_CombinaisonAmbigue_RefuseLaDecision(
        ReviewVerdict verdict,
        FraudClassification? classification)
    {
        Assert.Throws<ArgumentException>(() => new IncidentReviewDecision(
            ReviewId,
            IncidentId,
            verdict,
            classification,
            DecidedAt));
    }

    [Fact]
    public void Constructeur_NotesBlanches_LesNormaliseEnNull()
    {
        var decision = new IncidentReviewDecision(
            ReviewId,
            IncidentId,
            ReviewVerdict.Benign,
            null,
            DecidedAt,
            "   ");

        Assert.Null(decision.Notes);
    }

    [Fact]
    public void Constructeur_NotesTropLongues_RefuseLaDecision()
    {
        Assert.Throws<ArgumentException>(() => new IncidentReviewDecision(
            ReviewId,
            IncidentId,
            ReviewVerdict.Benign,
            null,
            DecidedAt,
            new string('a', IncidentReviewDecision.MaxNotesLength + 1)));
    }

    [Theory]
    [InlineData("review")]
    [InlineData("incident")]
    public void Constructeur_IdentifiantVide_RefuseLaDecision(string identifier)
    {
        Assert.Throws<ArgumentException>(() => new IncidentReviewDecision(
            identifier == "review" ? Guid.Empty : ReviewId,
            identifier == "incident" ? Guid.Empty : IncidentId,
            ReviewVerdict.Benign,
            null,
            DecidedAt));
    }
}
