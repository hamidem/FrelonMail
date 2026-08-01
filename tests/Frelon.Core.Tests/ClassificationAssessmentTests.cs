using Frelon.Core;
using Xunit;

namespace Frelon.Core.Tests;

public sealed class ClassificationAssessmentTests
{
    [Fact]
    public void None_EstUneAbsenceDePisteCoherente()
    {
        Assert.Equal(FraudClassification.Unknown, ClassificationAssessment.None.Classification);
        Assert.Equal(ClassificationConfidence.None, ClassificationAssessment.None.Confidence);
        Assert.Empty(ClassificationAssessment.None.Reasons);
    }

    [Fact]
    public void Constructeur_PisteSansRaison_RefuseUneConclusionOpaque()
    {
        Assert.Throws<ArgumentException>(() => new ClassificationAssessment(
            FraudClassification.Phishing,
            ClassificationConfidence.Medium,
            []));
    }

    [Fact]
    public void Constructeur_UnknownAvecConfiance_RefuseLaContradiction()
    {
        Assert.Throws<ArgumentException>(() => new ClassificationAssessment(
            FraudClassification.Unknown,
            ClassificationConfidence.Low,
            ["raison"]));
    }

    [Fact]
    public void Constructeur_RaisonVide_RefuseUneExplicationInexploitable()
    {
        Assert.Throws<ArgumentException>(() => new ClassificationAssessment(
            FraudClassification.Suspicious,
            ClassificationConfidence.Low,
            ["  "]));
    }
}
