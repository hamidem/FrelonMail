using Frelon.Core;
using Frelon.Exporters;
using Frelon.Storage;

namespace Frelon.Application.Tests;

/// <summary>
/// Vérifie la préparation d'un partage IOC depuis des incidents locaux choisis.
/// </summary>
public sealed class LocalShareableIocPreparationServiceTests
{
    private static readonly Guid FirstIncidentId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SecondIncidentId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ExportId =
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateTimeOffset PreparedAt =
        new(2026, 7, 23, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Request_EntreesValides_TrieEtCopieLesSelections()
    {
        var selection = new ShareableIocSelection(
            IocType.Domain,
            "valid.example");
        var incidentIds = new[] { SecondIncidentId, FirstIncidentId };
        var selections = new[] { selection };

        var request = new ShareableIocPreparationRequest(
            ExportId,
            PreparedAt,
            incidentIds,
            selections);
        incidentIds[0] = Guid.NewGuid();
        selections[0] = new ShareableIocSelection(
            IocType.Domain,
            "other.example");

        Assert.Equal([FirstIncidentId, SecondIncidentId], request.IncidentIds);
        Assert.Same(selection, Assert.Single(request.ApprovedIocs));
    }

    [Fact]
    public void Request_IdentifiantExportVide_RefuseLaCreation()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new ShareableIocPreparationRequest(
                Guid.Empty,
                PreparedAt,
                [FirstIncidentId],
                BuildApprovedIocs()));

        Assert.Equal("exportId", exception.ParamName);
    }

    [Fact]
    public void Request_DateVide_RefuseLaCreation()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new ShareableIocPreparationRequest(
                ExportId,
                default,
                [FirstIncidentId],
                BuildApprovedIocs()));

        Assert.Equal("preparedAt", exception.ParamName);
    }

    [Fact]
    public void Request_IncidentsDupliques_RefuseLaCreation()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new ShareableIocPreparationRequest(
                ExportId,
                PreparedAt,
                [FirstIncidentId, FirstIncidentId],
                BuildApprovedIocs()));

        Assert.Equal("incidentIds", exception.ParamName);
    }

    [Fact]
    public void Request_IdentifiantExportReutilisantIncident_RefuseLaCorrelation()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new ShareableIocPreparationRequest(
                FirstIncidentId,
                PreparedAt,
                [FirstIncidentId],
                BuildApprovedIocs()));

        Assert.Equal("exportId", exception.ParamName);
    }

    [Fact]
    public void Request_SelectionDupliquee_RefuseLaCreation()
    {
        var selection = new ShareableIocSelection(
            IocType.Domain,
            "valid.example");
        var exception = Assert.Throws<ArgumentException>(
            () => new ShareableIocPreparationRequest(
                ExportId,
                PreparedAt,
                [FirstIncidentId],
                [selection, selection]));

        Assert.Equal("approvedIocs", exception.ParamName);
    }

    [Fact]
    public void Constructeur_DependancesNulles_RefuseLaCreation()
    {
        var incidents = new MemoryIncidentStore();
        var reviews = new MemoryIncidentReviewStore();
        var exporter = new RecordingShareableIocExporter();

        Assert.Equal(
            "incidentStore",
            Assert.Throws<ArgumentNullException>(() =>
                new LocalShareableIocPreparationService(
                    null!,
                    reviews,
                    exporter)).ParamName);
        Assert.Equal(
            "reviewStore",
            Assert.Throws<ArgumentNullException>(() =>
                new LocalShareableIocPreparationService(
                    incidents,
                    null!,
                    exporter)).ParamName);
        Assert.Equal(
            "exporter",
            Assert.Throws<ArgumentNullException>(() =>
                new LocalShareableIocPreparationService(
                    incidents,
                    reviews,
                    null!)).ParamName);
    }

    [Fact]
    public async Task PrepareAsync_SansRequete_LeveArgumentNullException()
    {
        var service = CreateService(
            new MemoryIncidentStore(),
            new MemoryIncidentReviewStore(),
            new RecordingShareableIocExporter());

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.PrepareAsync(null!, CancellationToken.None));

        Assert.Equal("request", exception.ParamName);
    }

    [Fact]
    public async Task PrepareAsync_IncidentAbsent_SignaleUneIncoherenceLocale()
    {
        var incidents = new MemoryIncidentStore(
            [BuildIncident(FirstIncidentId, 'b')]);
        var reviews = new MemoryIncidentReviewStore(BuildReviews());
        var exporter = new RecordingShareableIocExporter();
        var service = CreateService(incidents, reviews, exporter);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => service.PrepareAsync(
                BuildRequest(),
                TestContext.Current.CancellationToken));

        Assert.Contains(SecondIncidentId.ToString("D"), exception.Message, StringComparison.Ordinal);
        Assert.Null(exporter.LastRequest);
    }

    [Fact]
    public async Task PrepareAsync_RevueAbsente_RefuseLePartage()
    {
        var reviews = new MemoryIncidentReviewStore(
            new Dictionary<Guid, IncidentReviewDecision>
            {
                [FirstIncidentId] = BuildReview(FirstIncidentId, 1),
            });
        var exporter = new RecordingShareableIocExporter();
        var service = CreateService(
            new MemoryIncidentStore(BuildIncidents()),
            reviews,
            exporter);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PrepareAsync(
                BuildRequest(),
                TestContext.Current.CancellationToken));

        Assert.Contains(SecondIncidentId.ToString("D"), exception.Message, StringComparison.Ordinal);
        Assert.Null(exporter.LastRequest);
    }

    [Fact]
    public async Task PrepareAsync_RevueEtrangere_SignaleUneIncoherence()
    {
        var reviews = new Dictionary<Guid, IncidentReviewDecision>
        {
            [FirstIncidentId] = BuildReview(SecondIncidentId, 1),
            [SecondIncidentId] = BuildReview(SecondIncidentId, 2),
        };
        var exporter = new RecordingShareableIocExporter();
        var service = CreateService(
            new MemoryIncidentStore(BuildIncidents()),
            new MemoryIncidentReviewStore(reviews),
            exporter);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => service.PrepareAsync(
                BuildRequest(),
                TestContext.Current.CancellationToken));

        Assert.Null(exporter.LastRequest);
    }

    [Fact]
    public async Task PrepareAsync_EtatValide_TransmetLesSourcesEtSelectionsAExporter()
    {
        var incidents = BuildIncidents();
        var reviews = BuildReviews();
        var exporter = new RecordingShareableIocExporter();
        var service = CreateService(
            new MemoryIncidentStore(incidents),
            new MemoryIncidentReviewStore(reviews),
            exporter);
        var request = BuildRequest();

        var result = await service.PrepareAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Same(exporter.Result, result);
        Assert.NotNull(exporter.LastRequest);
        Assert.Equal(
            [FirstIncidentId, SecondIncidentId],
            exporter.LastRequest.Incidents.Select(incident => incident.IncidentId));
        Assert.Equal(
            reviews.Values.Select(review => review.ReviewId).Order(),
            exporter.LastRequest.IncidentReviews.Select(review => review.ReviewId).Order());
        Assert.Same(
            request.ApprovedIocs[0],
            Assert.Single(exporter.LastRequest.ApprovedIocs));
    }

    [Fact]
    public async Task PrepareAsync_AvecExporterReel_ProduitPaquetEtAuditSepares()
    {
        var service = CreateService(
            new MemoryIncidentStore(BuildIncidents()),
            new MemoryIncidentReviewStore(BuildReviews()),
            new BasicShareableIocExporter());

        var result = await service.PrepareAsync(
            BuildRequest(),
            TestContext.Current.CancellationToken);

        Assert.Equal(ExportId, result.ShareablePackage.ExportId);
        Assert.Equal(ExportId, result.LocalAudit.ExportId);
        Assert.Equal(2, result.LocalAudit.Sources.Count);
        Assert.Contains(
            result.ShareablePackage.Artifacts,
            artifact => artifact.FileName == "iocs-partage.json");
    }

    [Fact]
    public async Task PrepareAsync_IdentifiantExportEgalAUneRevue_RefuseAvantExporter()
    {
        var reviews = BuildReviews();
        var exporter = new RecordingShareableIocExporter();
        var service = CreateService(
            new MemoryIncidentStore(BuildIncidents()),
            new MemoryIncidentReviewStore(reviews),
            exporter);
        var request = new ShareableIocPreparationRequest(
            reviews[FirstIncidentId].ReviewId,
            PreparedAt,
            [FirstIncidentId, SecondIncidentId],
            BuildApprovedIocs());

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => service.PrepareAsync(
                request,
                TestContext.Current.CancellationToken));

        Assert.Equal("exportId", exception.ParamName);
        Assert.Null(exporter.LastRequest);
    }

    [Fact]
    public async Task PrepareAsync_TokenAnnule_NInterrogeAucuneSource()
    {
        var incidents = new MemoryIncidentStore(BuildIncidents());
        var service = CreateService(
            incidents,
            new MemoryIncidentReviewStore(BuildReviews()),
            new RecordingShareableIocExporter());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.PrepareAsync(BuildRequest(), cancellation.Token));

        Assert.Empty(incidents.Requests);
    }

    private static LocalShareableIocPreparationService CreateService(
        IIncidentStore incidentStore,
        IIncidentReviewStore reviewStore,
        IShareableIocExporter exporter)
        => new(incidentStore, reviewStore, exporter);

    private static ShareableIocPreparationRequest BuildRequest()
        => new(
            ExportId,
            PreparedAt,
            [SecondIncidentId, FirstIncidentId],
            BuildApprovedIocs());

    private static IReadOnlyList<ShareableIocSelection> BuildApprovedIocs()
        =>
        [
            new ShareableIocSelection(IocType.Domain, "valid.example"),
        ];

    private static IReadOnlyList<FraudIncident> BuildIncidents()
        =>
        [
            BuildIncident(FirstIncidentId, 'b'),
            BuildIncident(SecondIncidentId, 'c'),
        ];

    private static FraudIncident BuildIncident(Guid incidentId, char hashCharacter)
        => new()
        {
            IncidentId = incidentId,
            CreatedAt = new DateTimeOffset(2026, 7, 23, 9, 0, 0, TimeSpan.Zero),
            Evidence = new EvidenceSource
            {
                FileName = $"{incidentId:N}.eml",
                ImportedAt = new DateTimeOffset(2026, 7, 23, 9, 0, 0, TimeSpan.Zero),
                Sha256 = new string(hashCharacter, 64),
            },
            Identity = new MailIdentity(),
            Authentication = new AuthenticationAssessment(),
            Iocs =
            [
                new Ioc
                {
                    Type = IocType.Domain,
                    Value = "valid.example",
                    Confidence = 0.8,
                },
            ],
            Classification = FraudClassification.Unknown,
            RiskScore = new RiskScore
            {
                Value = 80,
                Level = RiskLevel.Critical,
            },
        };

    private static IReadOnlyDictionary<Guid, IncidentReviewDecision> BuildReviews()
        => new Dictionary<Guid, IncidentReviewDecision>
        {
            [FirstIncidentId] = BuildReview(FirstIncidentId, 1),
            [SecondIncidentId] = BuildReview(SecondIncidentId, 2),
        };

    private static IncidentReviewDecision BuildReview(
        Guid incidentId,
        int reviewNumber)
        => new(
            Guid.Parse($"dddddddd-dddd-dddd-dddd-{reviewNumber:D12}"),
            incidentId,
            ReviewVerdict.ConfirmedFraud,
            FraudClassification.Phishing,
            new DateTimeOffset(2026, 7, 23, 11, reviewNumber, 0, TimeSpan.Zero));

    private sealed class MemoryIncidentStore : IIncidentStore
    {
        private readonly IReadOnlyDictionary<Guid, FraudIncident> _incidents;

        public MemoryIncidentStore(IReadOnlyList<FraudIncident>? incidents = null)
        {
            _incidents = (incidents ?? [])
                .ToDictionary(incident => incident.IncidentId);
        }

        public List<Guid> Requests { get; } = [];

        public Task InitializeAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SaveAsync(
            FraudIncident incident,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<FraudIncident?> GetByIdAsync(
            Guid incidentId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(incidentId);
            _incidents.TryGetValue(incidentId, out var incident);
            return Task.FromResult(incident);
        }

        public Task<IReadOnlyList<IncidentSummary>> ListRecentAsync(
            int limit = 100,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class MemoryIncidentReviewStore : IIncidentReviewStore
    {
        private readonly IReadOnlyDictionary<Guid, IncidentReviewDecision> _reviews;

        public MemoryIncidentReviewStore(
            IReadOnlyDictionary<Guid, IncidentReviewDecision>? reviews = null)
        {
            _reviews = reviews ??
                new Dictionary<Guid, IncidentReviewDecision>();
        }

        public List<Guid> Requests { get; } = [];

        public Task InitializeAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SaveReviewAsync(
            IncidentReviewDecision decision,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IncidentReviewDecision?> GetLatestReviewAsync(
            Guid incidentId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(incidentId);
            _reviews.TryGetValue(incidentId, out var review);
            return Task.FromResult(review);
        }

        public Task<IReadOnlyList<IncidentReviewDecision>> ListReviewsAsync(
            Guid incidentId,
            int limit = 100,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class RecordingShareableIocExporter : IShareableIocExporter
    {
        private readonly BasicShareableIocExporter _inner = new();

        public ShareableIocExportRequest? LastRequest { get; private set; }

        public ShareableIocExportResult? Result { get; private set; }

        public ShareableIocExportResult Export(ShareableIocExportRequest request)
        {
            LastRequest = request;
            Result = _inner.Export(request);
            return Result;
        }
    }
}
