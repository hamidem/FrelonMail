using Frelon.Core;
using Frelon.Reports;
using Frelon.Storage;

namespace Frelon.Application.Tests;

/// <summary>
/// Vérifie l'assemblage en lecture seule des données locales d'un takedown pack.
/// </summary>
public sealed class LocalTakedownPackPreparationServiceTests
{
    private static readonly Guid FirstIncidentId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SecondIncidentId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid PackId =
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateTimeOffset PreparedAt =
        new(2026, 7, 23, 13, 0, 0, TimeSpan.Zero);
    private static readonly CampaignCandidate Candidate = BuildCandidate();

    [Fact]
    public void Request_EntreesValides_NormaliseLesChoix()
    {
        var recipients = new[]
        {
            TakedownRecipientType.DomainRegistrar,
        };

        var request = new TakedownPackPreparationRequest(
            PackId,
            PreparedAt,
            $"  {Candidate.Fingerprint.ToUpperInvariant()}  ",
            recipients,
            "  Vérification locale.  ");
        recipients[0] = TakedownRecipientType.HostingProvider;

        Assert.Equal(Candidate.Fingerprint, request.CampaignFingerprint);
        Assert.Equal([TakedownRecipientType.DomainRegistrar], request.Recipients);
        Assert.Equal("Vérification locale.", request.AnalystNotes);
    }

    [Fact]
    public void Request_IdentifiantVide_RefuseLaCreation()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => BuildRequest(packId: Guid.Empty));

        Assert.Equal("packId", exception.ParamName);
    }

    [Fact]
    public void Request_DateVide_RefuseLaCreation()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => BuildRequest(preparedAt: default(DateTimeOffset)));

        Assert.Equal("preparedAt", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void Request_EmpreinteInvalide_RefuseLaCreation(string? fingerprint)
    {
        var exception = Assert.ThrowsAny<ArgumentException>(
            () => new TakedownPackPreparationRequest(
                PackId,
                PreparedAt,
                fingerprint!,
                [TakedownRecipientType.DomainRegistrar]));

        Assert.Equal("campaignFingerprint", exception.ParamName);
    }

    [Fact]
    public void Request_DestinataireDuplique_RefuseLaCreation()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => BuildRequest(recipients:
            [
                TakedownRecipientType.DomainRegistrar,
                TakedownRecipientType.DomainRegistrar,
            ]));

        Assert.Equal("recipients", exception.ParamName);
    }

    [Fact]
    public void Request_NoteTropLongue_RefuseLaCreation()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => BuildRequest(
                analystNotes: new string(
                    'a',
                    TakedownPackRequest.MaxAnalystNotesLength + 1)));

        Assert.Equal("analystNotes", exception.ParamName);
    }

    [Fact]
    public void Constructeur_DependancesNulles_RefuseLaCreation()
    {
        var incidents = new MemoryIncidentStore();
        var incidentReviews = new MemoryIncidentReviewStore();
        var campaignReviews = new MemoryCampaignReviewStore();
        var writer = new RecordingTakedownPackWriter();

        Assert.Equal(
            "incidentStore",
            Assert.Throws<ArgumentNullException>(() =>
                new LocalTakedownPackPreparationService(
                    null!,
                    incidentReviews,
                    campaignReviews,
                    writer)).ParamName);
        Assert.Equal(
            "incidentReviewStore",
            Assert.Throws<ArgumentNullException>(() =>
                new LocalTakedownPackPreparationService(
                    incidents,
                    null!,
                    campaignReviews,
                    writer)).ParamName);
        Assert.Equal(
            "campaignReviewStore",
            Assert.Throws<ArgumentNullException>(() =>
                new LocalTakedownPackPreparationService(
                    incidents,
                    incidentReviews,
                    null!,
                    writer)).ParamName);
        Assert.Equal(
            "writer",
            Assert.Throws<ArgumentNullException>(() =>
                new LocalTakedownPackPreparationService(
                    incidents,
                    incidentReviews,
                    campaignReviews,
                    null!)).ParamName);
    }

    [Fact]
    public async Task PrepareAsync_SansRequete_LeveArgumentNullException()
    {
        var service = CreateService(
            new MemoryIncidentStore(),
            new MemoryIncidentReviewStore(),
            new MemoryCampaignReviewStore(),
            new RecordingTakedownPackWriter());

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.PrepareAsync(null!, CancellationToken.None));

        Assert.Equal("request", exception.ParamName);
    }

    [Fact]
    public async Task PrepareAsync_SansRevueCampagne_RefuseAvantDeChargerLesIncidents()
    {
        var incidents = new MemoryIncidentStore();
        var incidentReviews = new MemoryIncidentReviewStore();
        var writer = new RecordingTakedownPackWriter();
        var service = CreateService(
            incidents,
            incidentReviews,
            new MemoryCampaignReviewStore(),
            writer);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PrepareAsync(
                BuildRequest(),
                TestContext.Current.CancellationToken));

        Assert.Contains("aucune décision", exception.Message, StringComparison.Ordinal);
        Assert.Empty(incidents.Requests);
        Assert.Empty(incidentReviews.Requests);
        Assert.Null(writer.LastRequest);
    }

    [Fact]
    public async Task PrepareAsync_DerniereRevueNonConfirmee_RefuseLePack()
    {
        var campaignReview = BuildCampaignReview(
            Candidate,
            CampaignReviewVerdict.Rejected);
        var incidents = new MemoryIncidentStore();
        var writer = new RecordingTakedownPackWriter();
        var service = CreateService(
            incidents,
            new MemoryIncidentReviewStore(),
            new MemoryCampaignReviewStore(campaignReview),
            writer);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PrepareAsync(
                BuildRequest(),
                TestContext.Current.CancellationToken));

        Assert.Contains("dernière décision", exception.Message, StringComparison.Ordinal);
        Assert.Empty(incidents.Requests);
        Assert.Null(writer.LastRequest);
    }

    [Fact]
    public async Task PrepareAsync_RevueCampagneEtrangere_SignaleUneIncoherence()
    {
        var otherCandidate = BuildCandidate(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Guid.Parse("44444444-4444-4444-4444-444444444444"));
        var service = CreateService(
            new MemoryIncidentStore(),
            new MemoryIncidentReviewStore(),
            new MemoryCampaignReviewStore(BuildCampaignReview(otherCandidate)),
            new RecordingTakedownPackWriter());

        await Assert.ThrowsAsync<InvalidDataException>(
            () => service.PrepareAsync(
                BuildRequest(),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PrepareAsync_IncidentAbsent_SignaleUneIncoherenceLocale()
    {
        var incidents = new MemoryIncidentStore(
            [BuildIncident(FirstIncidentId, 'a')]);
        var incidentReviews = new MemoryIncidentReviewStore(
            BuildIncidentReviews());
        var writer = new RecordingTakedownPackWriter();
        var service = CreateService(
            incidents,
            incidentReviews,
            new MemoryCampaignReviewStore(BuildCampaignReview(Candidate)),
            writer);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => service.PrepareAsync(
                BuildRequest(),
                TestContext.Current.CancellationToken));

        Assert.Contains(SecondIncidentId.ToString("D"), exception.Message, StringComparison.Ordinal);
        Assert.Null(writer.LastRequest);
    }

    [Fact]
    public async Task PrepareAsync_RevueIncidentAbsente_RefuseLePack()
    {
        var incidents = new MemoryIncidentStore(BuildIncidents());
        var incidentReviews = new MemoryIncidentReviewStore(
            [BuildIncidentReview(FirstIncidentId, 1)]);
        var writer = new RecordingTakedownPackWriter();
        var service = CreateService(
            incidents,
            incidentReviews,
            new MemoryCampaignReviewStore(BuildCampaignReview(Candidate)),
            writer);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PrepareAsync(
                BuildRequest(),
                TestContext.Current.CancellationToken));

        Assert.Contains(SecondIncidentId.ToString("D"), exception.Message, StringComparison.Ordinal);
        Assert.Null(writer.LastRequest);
    }

    [Fact]
    public async Task PrepareAsync_RevueIncidentEtrangere_SignaleUneIncoherence()
    {
        var reviews = new Dictionary<Guid, IncidentReviewDecision>
        {
            [FirstIncidentId] = BuildIncidentReview(SecondIncidentId, 1),
            [SecondIncidentId] = BuildIncidentReview(SecondIncidentId, 2),
        };
        var writer = new RecordingTakedownPackWriter();
        var service = CreateService(
            new MemoryIncidentStore(BuildIncidents()),
            new MemoryIncidentReviewStore(reviews),
            new MemoryCampaignReviewStore(BuildCampaignReview(Candidate)),
            writer);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => service.PrepareAsync(
                BuildRequest(),
                TestContext.Current.CancellationToken));

        Assert.Null(writer.LastRequest);
    }

    [Fact]
    public async Task PrepareAsync_EtatValide_TransmetLesSnapshotsEtDecisionsAuWriter()
    {
        var campaignReview = BuildCampaignReview(Candidate);
        var incidents = BuildIncidents();
        var incidentReviews = BuildIncidentReviews();
        var writer = new RecordingTakedownPackWriter();
        var service = CreateService(
            new MemoryIncidentStore(incidents),
            new MemoryIncidentReviewStore(incidentReviews),
            new MemoryCampaignReviewStore(campaignReview),
            writer);
        var request = BuildRequest(analystNotes: "  Note analyste.  ");

        var pack = await service.PrepareAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Same(writer.Result, pack);
        Assert.NotNull(writer.LastRequest);
        Assert.Same(campaignReview, writer.LastRequest.CampaignReview);
        Assert.Equal(incidents.Select(incident => incident.IncidentId),
            writer.LastRequest.Incidents.Select(incident => incident.IncidentId));
        Assert.Equal(
            incidentReviews.Values.Select(review => review.ReviewId).Order(),
            writer.LastRequest.IncidentReviews.Select(review => review.ReviewId).Order());
        Assert.Equal("Note analyste.", writer.LastRequest.AnalystNotes);
    }

    [Fact]
    public async Task PrepareAsync_AvecWriterReel_ProduitLePackEnMemoire()
    {
        var service = CreateService(
            new MemoryIncidentStore(BuildIncidents()),
            new MemoryIncidentReviewStore(BuildIncidentReviews()),
            new MemoryCampaignReviewStore(BuildCampaignReview(Candidate)),
            new BasicTakedownPackWriter());

        var pack = await service.PrepareAsync(
            BuildRequest(),
            TestContext.Current.CancellationToken);

        Assert.Equal(PackId, pack.PackId);
        Assert.Equal(Candidate.Fingerprint, pack.CampaignFingerprint);
        Assert.Contains(
            pack.Artifacts,
            artifact => artifact.Recipient == TakedownRecipientType.DomainRegistrar);
    }

    [Fact]
    public async Task PrepareAsync_TokenAnnule_NInterrogeAucuneSource()
    {
        var campaignReviews = new MemoryCampaignReviewStore(
            BuildCampaignReview(Candidate));
        var service = CreateService(
            new MemoryIncidentStore(BuildIncidents()),
            new MemoryIncidentReviewStore(BuildIncidentReviews()),
            campaignReviews,
            new RecordingTakedownPackWriter());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.PrepareAsync(BuildRequest(), cancellation.Token));

        Assert.Empty(campaignReviews.Requests);
    }

    private static LocalTakedownPackPreparationService CreateService(
        IIncidentStore incidentStore,
        IIncidentReviewStore incidentReviewStore,
        ICampaignReviewStore campaignReviewStore,
        ITakedownPackWriter writer)
        => new(
            incidentStore,
            incidentReviewStore,
            campaignReviewStore,
            writer);

    private static TakedownPackPreparationRequest BuildRequest(
        Guid? packId = null,
        DateTimeOffset? preparedAt = null,
        string? campaignFingerprint = null,
        IReadOnlyList<TakedownRecipientType>? recipients = null,
        string? analystNotes = null)
        => new(
            packId ?? PackId,
            preparedAt ?? PreparedAt,
            campaignFingerprint ?? Candidate.Fingerprint,
            recipients ??
            [
                TakedownRecipientType.DomainRegistrar,
            ],
            analystNotes);

    private static CampaignReviewDecision BuildCampaignReview(
        CampaignCandidate candidate,
        CampaignReviewVerdict verdict = CampaignReviewVerdict.Confirmed)
        => new(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            candidate,
            verdict,
            new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero));

    private static IReadOnlyList<FraudIncident> BuildIncidents()
        =>
        [
            BuildIncident(FirstIncidentId, 'a'),
            BuildIncident(SecondIncidentId, 'b'),
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
                    Value = "fraud.example",
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

    private static IReadOnlyDictionary<Guid, IncidentReviewDecision> BuildIncidentReviews()
        => new Dictionary<Guid, IncidentReviewDecision>
        {
            [FirstIncidentId] = BuildIncidentReview(FirstIncidentId, 1),
            [SecondIncidentId] = BuildIncidentReview(SecondIncidentId, 2),
        };

    private static IncidentReviewDecision BuildIncidentReview(
        Guid incidentId,
        int reviewNumber)
        => new(
            Guid.Parse($"dddddddd-dddd-dddd-dddd-{reviewNumber:D12}"),
            incidentId,
            ReviewVerdict.ConfirmedFraud,
            FraudClassification.Phishing,
            new DateTimeOffset(2026, 7, 23, 11, reviewNumber, 0, TimeSpan.Zero));

    private static CampaignCandidate BuildCandidate(
        Guid? firstIncidentId = null,
        Guid? secondIncidentId = null)
    {
        var firstId = firstIncidentId ?? FirstIncidentId;
        var secondId = secondIncidentId ?? SecondIncidentId;
        return new CampaignCandidate(
            [firstId, secondId],
            new DateTimeOffset(2026, 7, 23, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 23, 9, 5, 0, TimeSpan.Zero),
            [
                new IncidentCorrelationLink(
                    firstId,
                    secondId,
                    [
                        new SharedIocMatch(
                            IocType.Domain,
                            "fraud.example",
                            BasicIncidentCorrelator.DomainWeight),
                    ]),
            ]);
    }

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

        public MemoryIncidentReviewStore(
            IReadOnlyList<IncidentReviewDecision> reviews)
            : this(reviews.ToDictionary(review => review.IncidentId))
        {
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

    private sealed class MemoryCampaignReviewStore(
        CampaignReviewDecision? campaignReview = null)
        : ICampaignReviewStore
    {
        public List<string> Requests { get; } = [];

        public Task InitializeAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SaveCampaignReviewAsync(
            CampaignReviewDecision decision,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<CampaignReviewDecision?> GetLatestCampaignReviewAsync(
            string candidateFingerprint,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(candidateFingerprint);
            return Task.FromResult(campaignReview);
        }

        public Task<IReadOnlyList<CampaignReviewDecision>> ListCampaignReviewsAsync(
            string candidateFingerprint,
            int limit = 100,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class RecordingTakedownPackWriter : ITakedownPackWriter
    {
        public RecordingTakedownPackWriter()
        {
            Result = new TakedownPack(
                PackId,
                PreparedAt,
                Candidate.Fingerprint,
                [
                    new TakedownPackArtifact("LISEZ-MOI.md", "text/markdown", "Guide"),
                    new TakedownPackArtifact("manifest.json", "application/json", "{}"),
                    new TakedownPackArtifact(
                        "signalement.md",
                        "text/markdown",
                        "Signalement",
                        TakedownRecipientType.DomainRegistrar),
                ]);
        }

        public TakedownPackRequest? LastRequest { get; private set; }

        public TakedownPack Result { get; }

        public TakedownPack Write(TakedownPackRequest request)
        {
            LastRequest = request;
            return Result;
        }
    }
}
