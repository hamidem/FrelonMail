using System.Text.Json;
using Frelon.Core;
using Xunit;

namespace Frelon.Reports.Tests;

/// <summary>Vérifie la préparation locale et prudente des takedown packs.</summary>
public sealed class BasicTakedownPackWriterTests
{
    private static readonly Guid FirstIncidentId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SecondIncidentId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CampaignReviewId =
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid PackId =
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly BasicTakedownPackWriter _writer = new();

    [Fact]
    public void Write_EntreesConfirmees_ProduitDocumentsCommunsEtQuatreDestinataires()
    {
        var pack = _writer.Write(BuildRequest());

        Assert.Equal(PackId, pack.PackId);
        Assert.Equal($"frelon-takedown-{PackId:N}.zip", pack.SuggestedArchiveFileName);
        Assert.Equal(
            [
                "LISEZ-MOI.md",
                "manifest.json",
                "signalement-hebergeur.md",
                "signalement-registrar.md",
                "signalement-fournisseur-messagerie.md",
                "signalement-anti-phishing.md",
            ],
            pack.Artifacts.Select(artifact => artifact.FileName));
        Assert.Equal(4, pack.Artifacts.Count(artifact => artifact.Recipient is not null));
    }

    [Fact]
    public void Write_DocumentsDestinataires_SontReellementAdaptes()
    {
        var pack = _writer.Write(BuildRequest());
        var hosting = GetRecipient(pack, TakedownRecipientType.HostingProvider);
        var registrar = GetRecipient(pack, TakedownRecipientType.DomainRegistrar);
        var email = GetRecipient(pack, TakedownRecipientType.EmailProvider);
        var antiPhishing = GetRecipient(pack, TakedownRecipientType.AntiPhishingService);

        Assert.Contains("préserver les éléments", hosting.Content, StringComparison.Ordinal);
        Assert.Contains("https://fraud.example/login", hosting.Content, StringComparison.Ordinal);

        Assert.Contains("données d'enregistrement", registrar.Content, StringComparison.Ordinal);
        Assert.Contains("fraud.example", registrar.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("https://fraud.example/login", registrar.Content, StringComparison.Ordinal);

        Assert.Contains("Traces de messagerie", email.Content, StringComparison.Ordinal);
        Assert.Contains("sender@fraud.example", email.Content, StringComparison.Ordinal);

        Assert.Contains("mécanismes de détection", antiPhishing.Content, StringComparison.Ordinal);
        Assert.Contains(new string('c', 64), antiPhishing.Content, StringComparison.Ordinal);

        Assert.DoesNotContain("sender@fraud.example", registrar.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_ManifestJson_TraceToutesLesDecisionsEtPreuves()
    {
        var request = BuildRequest();
        var pack = _writer.Write(request);
        var manifest = Assert.Single(
            pack.Artifacts,
            artifact => artifact.FileName == "manifest.json");
        using var json = JsonDocument.Parse(manifest.Content);
        var root = json.RootElement;

        Assert.Equal(PackId, root.GetProperty("packId").GetGuid());
        Assert.Equal(
            CampaignReviewId,
            root.GetProperty("campaignReviewId").GetGuid());
        Assert.Equal(
            request.CampaignReview.CandidateFingerprint,
            root.GetProperty("campaignFingerprint").GetString());
        Assert.Equal(2, root.GetProperty("incidents").GetArrayLength());
        Assert.Equal(4, root.GetProperty("documents").GetArrayLength());
        Assert.Equal(
            new string('a', 64),
            root.GetProperty("incidents")[0].GetProperty("evidenceSha256").GetString());
    }

    [Fact]
    public void Write_Readme_RappelleQuAucunEnvoiNaEteEffectue()
    {
        var readme = Assert.Single(
            _writer.Write(BuildRequest()).Artifacts,
            artifact => artifact.FileName == "LISEZ-MOI.md");

        Assert.Contains("Frelon ne l'a envoyé à aucun tiers", readme.Content, StringComparison.Ordinal);
        Assert.Contains("Vérifier manuellement l'identité", readme.Content, StringComparison.Ordinal);
        Assert.Contains("Ne joindre les messages sources", readme.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_CampagneNonConfirmee_RefuseLePack()
    {
        var request = BuildRequest(
            campaignVerdict: CampaignReviewVerdict.Inconclusive);

        var exception = Assert.Throws<InvalidOperationException>(
            () => _writer.Write(request));

        Assert.Contains("campagne confirmée", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Write_PreparationAnterieureALaRevueCampagne_RefuseLePack()
    {
        var request = BuildRequest(
            preparedAt: new DateTimeOffset(2026, 7, 23, 11, 30, 0, TimeSpan.Zero));

        var exception = Assert.Throws<InvalidOperationException>(
            () => _writer.Write(request));

        Assert.Contains("avant la décision de campagne", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_PreparationAnterieureAUneRevueIncident_RefuseLePack()
    {
        var reviews = BuildIncidentReviews();
        reviews[1] = new IncidentReviewDecision(
            reviews[1].ReviewId,
            SecondIncidentId,
            ReviewVerdict.ConfirmedFraud,
            FraudClassification.Phishing,
            new DateTimeOffset(2026, 7, 23, 14, 0, 0, TimeSpan.Zero));
        var request = BuildRequest(incidentReviews: reviews);

        var exception = Assert.Throws<InvalidOperationException>(
            () => _writer.Write(request));

        Assert.Contains(SecondIncidentId.ToString("D"), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_CompositionIncomplete_RefuseLePack()
    {
        var incidents = BuildIncidents();
        var request = BuildRequest(incidents: [incidents[0]]);

        var exception = Assert.Throws<InvalidOperationException>(
            () => _writer.Write(request));

        Assert.Contains("exactement", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Write_RevueDIncidentManquante_RefuseLePack()
    {
        var reviews = BuildIncidentReviews();
        var request = BuildRequest(incidentReviews: [reviews[0]]);

        var exception = Assert.Throws<InvalidOperationException>(
            () => _writer.Write(request));

        Assert.Contains("chaque incident", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Write_FraudeIndividuelleNonConfirmee_RefuseLePack()
    {
        var reviews = BuildIncidentReviews();
        reviews[1] = new IncidentReviewDecision(
            reviews[1].ReviewId,
            SecondIncidentId,
            ReviewVerdict.Suspicious,
            FraudClassification.Suspicious,
            reviews[1].DecidedAt);
        var request = BuildRequest(incidentReviews: reviews);

        var exception = Assert.Throws<InvalidOperationException>(
            () => _writer.Write(request));

        Assert.Contains(
            SecondIncidentId.ToString("D"),
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Write_PreuveSansSha256_RefuseLePack()
    {
        var incidents = BuildIncidents();
        incidents[0] = incidents[0] with
        {
            Evidence = incidents[0].Evidence with { Sha256 = null },
        };
        var request = BuildRequest(incidents: incidents);

        var exception = Assert.Throws<InvalidOperationException>(
            () => _writer.Write(request));

        Assert.Contains("SHA-256", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_RegistrarSansDomaineQualifie_RefuseLeDestinataire()
    {
        var incidents = BuildIncidents(iocs: incidentId =>
        [
            BuildIoc(IocType.Url, $"https://{incidentId:N}.example/login"),
        ]);
        var request = BuildRequest(
            incidents: incidents,
            recipients: [TakedownRecipientType.DomainRegistrar]);

        var exception = Assert.Throws<InvalidOperationException>(
            () => _writer.Write(request));

        Assert.Contains(
            nameof(TakedownRecipientType.DomainRegistrar),
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Write_IocSousLeSeuil_NAutorisePasLeDestinataire()
    {
        var incidents = BuildIncidents(iocs: _ =>
        [
            BuildIoc(IocType.Domain, "fraud.example", confidence: 0.49),
        ]);
        var request = BuildRequest(
            incidents: incidents,
            recipients: [TakedownRecipientType.DomainRegistrar]);

        Assert.Throws<InvalidOperationException>(() => _writer.Write(request));
    }

    [Fact]
    public void Write_IocCommun_EstDedoublonneEtCompteLesIncidents()
    {
        var pack = _writer.Write(BuildRequest(
            recipients: [TakedownRecipientType.DomainRegistrar]));
        var registrar = GetRecipient(pack, TakedownRecipientType.DomainRegistrar);

        Assert.Equal(
            1,
            CountOccurrences(registrar.Content, "**Domain** : fraud.example"));
        Assert.Contains("observé dans 2 incident(s)", registrar.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_UrlsAuxCheminsDeCasseDifferente_RestentDeuxIndicateurs()
    {
        var incidents = BuildIncidents(iocs: incidentId =>
        [
            BuildIoc(
                IocType.Url,
                incidentId == FirstIncidentId
                    ? "https://fraud.example/Login"
                    : "https://fraud.example/login"),
        ]);
        var pack = _writer.Write(BuildRequest(
            incidents: incidents,
            recipients: [TakedownRecipientType.HostingProvider]));
        var hosting = GetRecipient(pack, TakedownRecipientType.HostingProvider);

        Assert.Contains("https://fraud.example/Login", hosting.Content, StringComparison.Ordinal);
        Assert.Contains("https://fraud.example/login", hosting.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_ValeursHostiles_RestentDuTexteMarkdown()
    {
        var incidents = BuildIncidents();
        incidents[0] = incidents[0] with
        {
            Evidence = incidents[0].Evidence with
            {
                FileName = "<script>alert(1)</script> [ouvrir](https://evil.test).eml",
            },
            Identity = incidents[0].Identity with
            {
                From = "*faux* <script>danger</script>",
            },
        };
        var request = BuildRequest(
            incidents: incidents,
            recipients: [TakedownRecipientType.EmailProvider],
            analystNotes: "# urgence\r\n<script>danger</script>");

        var pack = _writer.Write(request);
        var email = GetRecipient(pack, TakedownRecipientType.EmailProvider);
        var readme = Assert.Single(
            pack.Artifacts,
            artifact => artifact.FileName == "LISEZ-MOI.md");

        Assert.DoesNotContain("<script>", email.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;", email.Content, StringComparison.Ordinal);
        Assert.Contains("\\[ouvrir\\]\\(https://evil.test\\)", email.Content, StringComparison.Ordinal);
        Assert.Contains("\\*faux\\*", email.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", readme.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Write_EntreesDansUnAutreOrdre_ConserveUnPackDeterministe()
    {
        var incidents = BuildIncidents();
        var reviews = BuildIncidentReviews();
        var first = _writer.Write(BuildRequest(
            incidents,
            reviews,
            [
                TakedownRecipientType.AntiPhishingService,
                TakedownRecipientType.HostingProvider,
            ]));
        var second = _writer.Write(BuildRequest(
            [.. incidents.AsEnumerable().Reverse()],
            [.. reviews.AsEnumerable().Reverse()],
            [
                TakedownRecipientType.HostingProvider,
                TakedownRecipientType.AntiPhishingService,
            ]));

        Assert.Equal(
            first.Artifacts.Select(artifact => (artifact.FileName, artifact.Content)),
            second.Artifacts.Select(artifact => (artifact.FileName, artifact.Content)));
    }

    [Fact]
    public void Write_RequeteNull_LeveArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => _writer.Write(null!));

        Assert.Equal("request", exception.ParamName);
    }

    [Fact]
    public void Request_DestinataireDuplique_RefuseLAmbiguite()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => BuildRequest(recipients:
            [
                TakedownRecipientType.HostingProvider,
                TakedownRecipientType.HostingProvider,
            ]));

        Assert.Equal("recipients", exception.ParamName);
    }

    [Fact]
    public void Request_IncidentsDupliques_RefuseLAmbiguite()
    {
        var incident = BuildIncidents()[0];

        var exception = Assert.Throws<ArgumentException>(
            () => BuildRequest(incidents: [incident, incident]));

        Assert.Equal("incidents", exception.ParamName);
    }

    [Fact]
    public void Request_NoteEstNormalisee()
    {
        var request = BuildRequest(analystNotes: "  Vérifié localement.  ");

        Assert.Equal("Vérifié localement.", request.AnalystNotes);
    }

    private static TakedownPackRequest BuildRequest(
        IReadOnlyList<FraudIncident>? incidents = null,
        IReadOnlyList<IncidentReviewDecision>? incidentReviews = null,
        IReadOnlyList<TakedownRecipientType>? recipients = null,
        CampaignReviewVerdict campaignVerdict = CampaignReviewVerdict.Confirmed,
        string? analystNotes = "Campagne examinée localement.",
        DateTimeOffset? preparedAt = null)
    {
        incidents ??= BuildIncidents();
        incidentReviews ??= BuildIncidentReviews();
        var candidate = BuildCandidate();
        var campaignReview = new CampaignReviewDecision(
            CampaignReviewId,
            candidate,
            campaignVerdict,
            new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero),
            "Composition vérifiée.");

        return new TakedownPackRequest(
            PackId,
            preparedAt ??
                new DateTimeOffset(2026, 7, 23, 13, 0, 0, TimeSpan.Zero),
            campaignReview,
            incidents,
            incidentReviews,
            recipients ??
            [
                TakedownRecipientType.HostingProvider,
                TakedownRecipientType.DomainRegistrar,
                TakedownRecipientType.EmailProvider,
                TakedownRecipientType.AntiPhishingService,
            ],
            analystNotes);
    }

    private static List<FraudIncident> BuildIncidents(
        Func<Guid, IReadOnlyList<Ioc>>? iocs = null)
        =>
        [
            BuildIncident(FirstIncidentId, 1, 'a', iocs?.Invoke(FirstIncidentId)),
            BuildIncident(SecondIncidentId, 2, 'b', iocs?.Invoke(SecondIncidentId)),
        ];

    private static FraudIncident BuildIncident(
        Guid incidentId,
        int minute,
        char evidenceHashCharacter,
        IReadOnlyList<Ioc>? iocs)
        => new()
        {
            IncidentId = incidentId,
            CreatedAt = new DateTimeOffset(2026, 7, 23, 9, minute, 0, TimeSpan.Zero),
            Evidence = new EvidenceSource
            {
                FileName = $"incident-{minute}.eml",
                ImportedAt = new DateTimeOffset(2026, 7, 23, 9, minute, 0, TimeSpan.Zero),
                Sha256 = new string(evidenceHashCharacter, 64),
            },
            Identity = new MailIdentity
            {
                From = "sender@fraud.example",
                ReplyTo = "reply@fraud.example",
                ReturnPath = "bounce@fraud.example",
                MessageId = $"<message-{minute}@fraud.example>",
                Subject = "Connexion urgente",
            },
            Authentication = new AuthenticationAssessment
            {
                SpfResult = "fail",
                DkimResult = "none",
                DmarcResult = "fail",
                IsSuspicious = true,
            },
            Iocs = iocs ??
            [
                BuildIoc(IocType.Url, "https://fraud.example/login"),
                BuildIoc(IocType.Domain, "fraud.example"),
                BuildIoc(IocType.Email, "sender@fraud.example"),
                BuildIoc(IocType.Hash, new string('c', 64)),
            ],
            Classification = FraudClassification.Unknown,
            RiskScore = new RiskScore
            {
                Value = 80,
                Level = RiskLevel.Critical,
                Reasons = ["Signaux convergents"],
            },
        };

    private static Ioc BuildIoc(
        IocType type,
        string value,
        double confidence = 0.8)
        => new()
        {
            Type = type,
            Value = value,
            Confidence = confidence,
            Source = "test",
        };

    private static List<IncidentReviewDecision> BuildIncidentReviews()
        =>
        [
            BuildIncidentReview(FirstIncidentId, 1),
            BuildIncidentReview(SecondIncidentId, 2),
        ];

    private static IncidentReviewDecision BuildIncidentReview(
        Guid incidentId,
        int minute)
        => new(
            Guid.Parse($"dddddddd-dddd-dddd-dddd-{minute:D12}"),
            incidentId,
            ReviewVerdict.ConfirmedFraud,
            FraudClassification.Phishing,
            new DateTimeOffset(2026, 7, 23, 11, minute, 0, TimeSpan.Zero),
            "Fraude vérifiée.");

    private static CampaignCandidate BuildCandidate()
        => new(
            [FirstIncidentId, SecondIncidentId],
            new DateTimeOffset(2026, 7, 23, 9, 1, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 23, 9, 2, 0, TimeSpan.Zero),
            [
                new IncidentCorrelationLink(
                    FirstIncidentId,
                    SecondIncidentId,
                    [
                        new SharedIocMatch(
                            IocType.Url,
                            "https://fraud.example/login",
                            BasicIncidentCorrelator.UrlWeight),
                    ]),
            ]);

    private static TakedownPackArtifact GetRecipient(
        TakedownPack pack,
        TakedownRecipientType recipient)
        => Assert.Single(pack.Artifacts, artifact => artifact.Recipient == recipient);

    private static int CountOccurrences(string value, string pattern)
    {
        var count = 0;
        var startIndex = 0;

        while ((startIndex = value.IndexOf(
                   pattern,
                   startIndex,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            startIndex += pattern.Length;
        }

        return count;
    }
}
