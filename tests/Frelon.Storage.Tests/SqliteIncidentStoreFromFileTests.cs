using Microsoft.Data.Sqlite;

namespace Frelon.Storage.Tests;

public partial class SqliteIncidentStoreTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromFile_CheminVide_LeveArgumentException(string? databasePath)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => SqliteIncidentStore.FromFile(databasePath!));

        Assert.Equal("databasePath", exception.ParamName);
    }

    [Fact]
    public async Task FromFile_InitialiseEnregistreEtRelitUnIncident()
    {
        using var database = TemporarySqliteDatabase.Create();
        var store = SqliteIncidentStore.FromFile(database.FilePath);
        var incident = BuildRichIncident();

        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.SaveAsync(incident, TestContext.Current.CancellationToken);
        var reloaded = await store.GetByIdAsync(
            incident.IncidentId,
            TestContext.Current.CancellationToken);

        Assert.NotNull(reloaded);
        Assert.Equal(incident.IncidentId, reloaded.IncidentId);
        Assert.True(File.Exists(database.FilePath));
    }

    [Fact]
    public async Task FromFile_TraiteLesSeparateursDeConnectionStringCommePartieDuNomDeFichier()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"frelon;Mode=Memory;Cache=Shared-{Guid.NewGuid():N}.db");

        try
        {
            var store = SqliteIncidentStore.FromFile(databasePath);

            await store.InitializeAsync(TestContext.Current.CancellationToken);

            Assert.True(File.Exists(databasePath));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    [Fact]
    public async Task FromFile_NeCreePasImplicitementLeRepertoireParent()
    {
        var parentPath = Path.Combine(
            Path.GetTempPath(),
            $"frelon-missing-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(parentPath, "incidents.db");
        var store = SqliteIncidentStore.FromFile(databasePath);

        await Assert.ThrowsAsync<SqliteException>(
            () => store.InitializeAsync(TestContext.Current.CancellationToken));

        Assert.False(Directory.Exists(parentPath));
    }
}
