using System.Text;
using Frelon.Core;
using Frelon.Mail;
using Microsoft.Data.Sqlite;

namespace Frelon.Storage.Tests;

/// <summary>
/// Vérifie le contrat d'identité entre l'analyse réelle d'un email et sa persistance.
/// </summary>
public sealed class IncidentIdentityIntegrationTests
{
    private const string MinimalEml =
        "From: support@example.test\r\n" +
        "Subject: Vérification du contrat d'identité\r\n" +
        "\r\n" +
        "Message de test.\r\n";

    [Fact]
    public async Task AnalyzeAsync_PuisSaveAsyncEtGetByIdAsync_ConserventLeMemeGuid()
    {
        var analyzer = new BasicEmailIncidentAnalyzer(
            new BasicEmailParser(),
            new BasicEmailHeaderAnalyzer(),
            new BasicEmailUrlExtractor(),
            new BasicUrlIocExtractor(),
            new BasicEmailAttachmentAnalyzer(),
            new BasicAttachmentIocExtractor(),
            new BasicIncidentRiskScorer(),
            new CautiousIncidentClassifier());

        await using var emlStream = new MemoryStream(Encoding.UTF8.GetBytes(MinimalEml));
        var incident = await analyzer.AnalyzeAsync(
            emlStream,
            "identity-contract.eml",
            TestContext.Current.CancellationToken);

        Assert.NotEqual(Guid.Empty, incident.IncidentId);

        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"frelon-identity-{Guid.NewGuid():N}.db");

        try
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Pooling = false,
            }.ToString();
            var store = new SqliteIncidentStore(connectionString);

            await store.InitializeAsync(TestContext.Current.CancellationToken);
            await store.SaveAsync(incident, TestContext.Current.CancellationToken);

            var reloaded = await store.GetByIdAsync(
                incident.IncidentId,
                TestContext.Current.CancellationToken);

            Assert.NotNull(reloaded);
            Assert.Equal(incident.IncidentId, reloaded.IncidentId);
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }
}
