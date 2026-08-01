namespace Frelon.Web.Tests;

/// <summary>Vérifie l'identité réellement intégrée à l'application distribuée.</summary>
public sealed class ApplicationIdentityTests
{
    [Fact]
    public void FromAssembly_RetourneLeProduitEtLaVersionDeLaBeta()
    {
        var identity = ApplicationIdentity.FromAssembly(typeof(Program).Assembly);

        Assert.Equal("Frelon", identity.ProductName);
        Assert.Equal("0.1.0-beta.1", identity.Version);
    }

    [Fact]
    public void Constructeur_NormaliseLesValeurs()
    {
        var identity = new ApplicationIdentity(" Frelon ", " 0.1.0-beta.1 ");

        Assert.Equal("Frelon", identity.ProductName);
        Assert.Equal("0.1.0-beta.1", identity.Version);
    }

    [Theory]
    [InlineData("", "0.1.0")]
    [InlineData("Frelon", " ")]
    public void Constructeur_ValeurVide_Refuse(
        string productName,
        string version)
    {
        Assert.Throws<ArgumentException>(() =>
            new ApplicationIdentity(productName, version));
    }
}
