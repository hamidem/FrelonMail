using Frelon.Core;

namespace Frelon.Storage.Tests;

public partial class SqliteIncidentStoreTests
{
    [Fact]
    public async Task CampaignCorrelation_ReloadDeuxSnapshotsSqlite_EtProduitUnLienExplique()
    {
        using var database = TemporarySqliteDatabase.Create();
        var store = new SqliteIncidentStore(database.ConnectionString);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        var first = BuildRichIncident(
            "11111111-1111-1111-1111-111111111111",
            "premier.eml") with
        {
            Evidence = new EvidenceSource
            {
                FileName = "premier.eml",
                ImportedAt = ImportedAt,
                Sha256 = new string('a', 64),
            },
            Iocs =
            [
                new Ioc
                {
                    Type = IocType.Url,
                    Value = "https://fraud.example/login",
                    Confidence = 0.8,
                },
            ],
        };
        var second = BuildRichIncident(
            "22222222-2222-2222-2222-222222222222",
            "second.eml") with
        {
            Evidence = new EvidenceSource
            {
                FileName = "second.eml",
                ImportedAt = ImportedAt.AddMinutes(1),
                Sha256 = new string('b', 64),
            },
            Iocs =
            [
                new Ioc
                {
                    Type = IocType.Url,
                    Value = "HTTPS://FRAUD.EXAMPLE:443/login",
                    Confidence = 0.8,
                },
            ],
        };

        await store.SaveAsync(first, TestContext.Current.CancellationToken);
        await store.SaveAsync(second, TestContext.Current.CancellationToken);
        var service = new LocalCampaignCorrelationService(
            store,
            new BasicIncidentCorrelator());

        var candidate = Assert.Single(
            await service.FindRecentCandidatesAsync(
                cancellationToken: TestContext.Current.CancellationToken));
        var link = Assert.Single(candidate.Links);
        var match = Assert.Single(link.Matches);

        Assert.Equal([first.IncidentId, second.IncidentId], candidate.IncidentIds);
        Assert.Equal(BasicIncidentCorrelator.UrlWeight, link.Score);
        Assert.Equal("https://fraud.example/login", match.Value);
    }

    [Fact]
    public async Task CampaignWorkflow_RelitCorrelationRevueEtConsultationSqlite()
    {
        using var database = TemporarySqliteDatabase.Create();
        var store = new SqliteIncidentStore(database.ConnectionString);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        var first = BuildRichIncident(
            "33333333-3333-3333-3333-333333333333",
            "troisieme.eml") with
        {
            Evidence = new EvidenceSource
            {
                FileName = "troisieme.eml",
                ImportedAt = ImportedAt.AddMinutes(2),
                Sha256 = new string('c', 64),
            },
            Iocs =
            [
                new Ioc
                {
                    Type = IocType.Url,
                    Value = "https://campaign.example/login",
                    Confidence = 0.8,
                },
            ],
        };
        var second = BuildRichIncident(
            "44444444-4444-4444-4444-444444444444",
            "quatrieme.eml") with
        {
            Evidence = new EvidenceSource
            {
                FileName = "quatrieme.eml",
                ImportedAt = ImportedAt.AddMinutes(3),
                Sha256 = new string('d', 64),
            },
            Iocs =
            [
                new Ioc
                {
                    Type = IocType.Url,
                    Value = "HTTPS://CAMPAIGN.EXAMPLE:443/login",
                    Confidence = 0.8,
                },
            ],
        };

        await store.SaveAsync(first, TestContext.Current.CancellationToken);
        await store.SaveAsync(second, TestContext.Current.CancellationToken);
        var correlation = new LocalCampaignCorrelationService(
            store,
            new BasicIncidentCorrelator());
        var candidate = Assert.Single(
            await correlation.FindRecentCandidatesAsync(
                cancellationToken: TestContext.Current.CancellationToken));
        var review = new CampaignReviewDecision(
            Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
            candidate,
            CampaignReviewVerdict.Confirmed,
            new DateTimeOffset(2026, 7, 23, 13, 0, 0, TimeSpan.Zero),
            "Composition confirmée.");
        var reviewWorkflow = new LocalCampaignReviewService(
            correlation,
            store);
        await reviewWorkflow.RecordCurrentAsync(
            review,
            cancellationToken: TestContext.Current.CancellationToken);
        var consultation = new LocalCampaignConsultationService(
            correlation,
            store);

        var summary = Assert.Single(
            await consultation.ListCurrentAsync(
                cancellationToken: TestContext.Current.CancellationToken));
        var details = await consultation.GetDetailsAsync(
            candidate.Fingerprint,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(candidate.Fingerprint, summary.Candidate.Fingerprint);
        Assert.Equal(review.ReviewId, summary.LatestReview?.ReviewId);
        Assert.NotNull(details);
        Assert.True(details.IsCurrent);
        Assert.Equal(candidate.Fingerprint, details.Fingerprint);
        Assert.Equal(review.ReviewId, Assert.Single(details.ReviewHistory).ReviewId);
    }
}
