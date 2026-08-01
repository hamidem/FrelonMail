using System.Text;

namespace Frelon.Web.Tests;

public sealed class LocalApplicationInstanceTests
{
    [Fact]
    public void TryAcquire_MemeDossier_AutoriseUneSeuleInstance()
    {
        using var workspace = new TemporaryWorkspace();
        using var first = LocalApplicationInstance.TryAcquire(workspace.Root);

        using var second = LocalApplicationInstance.TryAcquire(workspace.Root);

        Assert.NotNull(first);
        Assert.Null(second);
    }

    [Fact]
    public void Dispose_LibereLeDossierPourUneNouvelleInstance()
    {
        using var workspace = new TemporaryWorkspace();
        var first = LocalApplicationInstance.TryAcquire(workspace.Root);
        Assert.NotNull(first);
        first.Dispose();

        using var next = LocalApplicationInstance.TryAcquire(workspace.Root);

        Assert.NotNull(next);
    }

    [Fact]
    public void PublishActiveUrl_PermetDeRetrouverLAdresseLocale()
    {
        using var workspace = new TemporaryWorkspace();
        using var instance = LocalApplicationInstance.TryAcquire(workspace.Root);
        Assert.NotNull(instance);
        var expected = new Uri("http://localhost:53127/");

        instance.PublishActiveUrl(expected);
        var result = LocalApplicationInstance.TryReadActiveUrl(workspace.Root);

        Assert.Equal(expected, result);
        var bytes = File.ReadAllBytes(Path.Combine(workspace.Root, ".frelon-url"));
        Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
    }

    [Fact]
    public void PublishActiveUrl_AdresseDistante_Refuse()
    {
        using var workspace = new TemporaryWorkspace();
        using var instance = LocalApplicationInstance.TryAcquire(workspace.Root);
        Assert.NotNull(instance);

        Assert.Throws<ArgumentException>(() =>
            instance.PublishActiveUrl(new Uri("https://example.test/")));
    }

    [Fact]
    public void TryReadActiveUrl_EtatManipuleVersInternet_Ignore()
    {
        using var workspace = new TemporaryWorkspace();
        File.WriteAllText(Path.Combine(workspace.Root, ".frelon-url"), "http://example.test:5127/");

        var result = LocalApplicationInstance.TryReadActiveUrl(workspace.Root);

        Assert.Null(result);
    }

    private sealed class TemporaryWorkspace : IDisposable
    {
        public TemporaryWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), $"frelon-instance-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch
            {
                // Le nettoyage ne doit pas masquer le résultat du test.
            }
        }
    }
}
