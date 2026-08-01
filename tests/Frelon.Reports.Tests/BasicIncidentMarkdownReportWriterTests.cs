using Frelon.Core;
using Xunit;

namespace Frelon.Reports.Tests;

/// <summary>
/// Tests de <see cref="BasicIncidentMarkdownReportWriter"/>.
/// </summary>
public class BasicIncidentMarkdownReportWriterTests
{
    private const string ExplicationScoreHeading = "## Explication du score de risque";
    private const string PreuveSourceHeading = "## Preuve source";

    private static FraudIncident BuildIncident(
        IReadOnlyList<string> reasons,
        double riskValue = 15,
        RiskLevel riskLevel = RiskLevel.Low,
        IReadOnlyList<UrlIndicator>? urls = null)
    {
        return new FraudIncident
        {
            IncidentId     = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            CreatedAt      = new DateTimeOffset(2026, 7, 3, 12, 0, 0, TimeSpan.Zero),
            Evidence       = new EvidenceSource
            {
                FileName   = "suspicious.eml",
                ImportedAt = new DateTimeOffset(2026, 7, 3, 12, 0, 0, TimeSpan.Zero),
            },
            Identity       = new MailIdentity
            {
                From       = "Fake Support <support@example.net>",
                ReplyTo    = "reply@example.net",
                ReturnPath = "<bounce@example.net>",
                MessageId  = "<abc123@example.net>",
                Subject    = "Suspicious login attempt",
            },
            Authentication = new AuthenticationAssessment
            {
                AuthenticationResultsRaw = "spf=pass; dkim=fail; dmarc=none",
                SpfResult                = "pass",
                DkimResult               = "fail",
                DmarcResult              = "none",
            },
            ReceivedChain  =
            [
                new ReceivedHop
                {
                    Position = 0,
                    RawValue = "from first.example by mx.example.org",
                },
            ],
            Urls           = urls ??
            [
                new UrlIndicator
                {
                    RawValue = "https://example.test/login",
                    IsSuspicious = true,
                    Reasons = ["Hôte trompeur"],
                },
            ],
            Classification = FraudClassification.Unknown,
            ClassificationAssessment = new ClassificationAssessment(
                FraudClassification.Phishing,
                ClassificationConfidence.Medium,
                ["URL explicitement suspecte"]),
            RiskScore      = new RiskScore
            {
                Value = riskValue,
                Level = riskLevel,
                Reasons = reasons,
            },
        };
    }

    private static FraudIncident BuildTestIncident() => BuildIncident(["Échec d'authentification DKIM"]);

    private static string GetExplicationSection(string markdown)
    {
        int start = markdown.IndexOf(ExplicationScoreHeading, StringComparison.Ordinal);
        int end = markdown.IndexOf(PreuveSourceHeading, start + ExplicationScoreHeading.Length, StringComparison.Ordinal);

        return markdown.Substring(start, end - start);
    }

    private static int CountReasonLines(string section)
    {
        int count = 0;

        foreach (string line in section.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private static readonly BasicIncidentMarkdownReportWriter Writer = new();

    [Fact]
    public void Write_RetourneUnMarkdownNonVide()
    {
        string markdown = Writer.Write(BuildTestIncident());

        Assert.False(string.IsNullOrWhiteSpace(markdown));
    }

    [Fact]
    public void Write_ContientLeTitrePrincipal()
    {
        string markdown = Writer.Write(BuildTestIncident());

        Assert.Contains("# Rapport d'incident Frelon", markdown);
    }

    [Fact]
    public void Write_ContientIncidentId()
    {
        string markdown = Writer.Write(BuildTestIncident());

        Assert.Contains("11111111-1111-1111-1111-111111111111", markdown);
    }

    [Fact]
    public void Write_ContientClassification()
    {
        string markdown = Writer.Write(BuildTestIncident());

        Assert.Contains("Unknown", markdown);
    }

    [Fact]
    public void Write_DistingueLaPisteAutomatiqueDuVerdict()
    {
        string markdown = Writer.Write(BuildTestIncident());

        Assert.Contains("Classification de l'analyse", markdown);
        Assert.Contains("Piste de classification automatique", markdown);
        Assert.Contains("Catégorie suggérée** : Phishing", markdown);
        Assert.Contains("elle ne constitue ni une preuve ni un verdict", markdown);
        Assert.Contains("URL explicitement suspecte", markdown);
    }

    [Fact]
    public void Write_ContientNomDuFichierSource()
    {
        string markdown = Writer.Write(BuildTestIncident());

        Assert.Contains("suspicious.eml", markdown);
    }

    [Fact]
    public void Write_ContientInformationsIdentite()
    {
        string markdown = Writer.Write(BuildTestIncident());

        Assert.Contains("## Identité déclarée", markdown);
        Assert.Contains("support@example.net", markdown);
        Assert.Contains("reply@example.net", markdown);
        Assert.Contains("bounce@example.net", markdown);
        Assert.Contains("abc123@example.net", markdown);
        Assert.Contains("Suspicious login attempt", markdown);
    }

    [Fact]
    public void Write_ContientInformationsAuthentification()
    {
        string markdown = Writer.Write(BuildTestIncident());

        Assert.Contains("## Authentification", markdown);
        Assert.Contains("spf=pass; dkim=fail; dmarc=none", markdown);
        Assert.Contains("pass", markdown);
        Assert.Contains("fail", markdown);
        Assert.Contains("none", markdown);
    }

    [Fact]
    public void Write_ExpliqueLesReglesDefensivesAssocieesAuxUrls()
    {
        string markdown = Writer.Write(BuildTestIncident());

        Assert.Contains("⚠ Suspecte", markdown);
        Assert.Contains("Raison : Hôte trompeur", markdown);
    }

    [Fact]
    public void Write_ExpliqueLesReglesDefensivesAssocieesAuxPiecesJointes()
    {
        var incident = BuildTestIncident() with
        {
            Attachments =
            [
                new AttachmentIndicator
                {
                    FileName = "facture.pdf.exe",
                    IsSuspicious = true,
                    Reasons = ["Double extension trompeuse"]
                }
            ]
        };

        string markdown = Writer.Write(incident);

        Assert.Contains("facture.pdf.exe** ⚠ Suspecte", markdown);
        Assert.Contains("Raison : Double extension trompeuse", markdown);
    }

    [Fact]
    public void Write_ContientHeadersReceivedSiPresents()
    {
        string markdown = Writer.Write(BuildTestIncident());

        Assert.Contains("## Chaîne Received", markdown);
        Assert.Contains("from first.example by mx.example.org", markdown);
    }

    [Fact]
    public void Write_ContientLeScoreEtLeNiveauDansLeResume()
    {
        string markdown = Writer.Write(BuildTestIncident());

        Assert.Contains("- **Score de risque** : 15", markdown);
        Assert.Contains("- **Niveau de risque** : Low", markdown);
    }

    [Fact]
    public void Write_ContientLaSectionExplicationDuScoreDeRisque()
    {
        string markdown = Writer.Write(BuildTestIncident());

        Assert.Contains(ExplicationScoreHeading, markdown);
    }

    [Fact]
    public void Write_AfficheLaRaisonDuScoreDansLOrdreRecu()
    {
        string markdown = Writer.Write(BuildIncident(
            [
                "Échec d'authentification SPF",
                "Échec d'authentification DKIM",
                "URL suspecte détectée",
            ],
            60,
            RiskLevel.High));

        string section = GetExplicationSection(markdown);

        Assert.Contains("- Échec d'authentification SPF", section);
        Assert.Contains("- Échec d'authentification DKIM", section);
        Assert.Contains("- URL suspecte détectée", section);
        Assert.True(section.IndexOf("- Échec d'authentification SPF", StringComparison.Ordinal) < section.IndexOf("- Échec d'authentification DKIM", StringComparison.Ordinal));
        Assert.True(section.IndexOf("- Échec d'authentification DKIM", StringComparison.Ordinal) < section.IndexOf("- URL suspecte détectée", StringComparison.Ordinal));
        Assert.Equal(3, CountReasonLines(section));
    }

    [Fact]
    public void Write_AfficheUneLigneParRaison()
    {
        string markdown = Writer.Write(BuildIncident(
            [
                "Échec d'authentification SPF",
                "Échec d'authentification DKIM",
            ],
            60,
            RiskLevel.High));

        string section = GetExplicationSection(markdown);

        Assert.Equal(2, CountReasonLines(section));
        Assert.Contains("- Échec d'authentification SPF", section);
        Assert.Contains("- Échec d'authentification DKIM", section);
    }

    [Fact]
    public void Write_AfficheLeMessageParDefautQuandAucuneRaisonNestPresente()
    {
        string markdown = Writer.Write(BuildIncident(Array.Empty<string>()));

        string section = GetExplicationSection(markdown);

        Assert.Contains("Aucune raison de risque identifiée.", section);
        Assert.DoesNotContain("- Échec d'authentification", section);
        Assert.DoesNotContain("- URL suspecte détectée", section);
    }

    [Fact]
    public void Write_PlaceLaSectionExplicationApresLeResumeEtAvantLaPreuveSource()
    {
        string markdown = Writer.Write(BuildTestIncident());

        int resumeIndex = markdown.IndexOf("## Résumé", StringComparison.Ordinal);
        int explicationIndex = markdown.IndexOf(ExplicationScoreHeading, StringComparison.Ordinal);
        int preuveIndex = markdown.IndexOf(PreuveSourceHeading, StringComparison.Ordinal);

        Assert.True(resumeIndex >= 0);
        Assert.True(explicationIndex > resumeIndex);
        Assert.True(preuveIndex > explicationIndex);
    }

    [Fact]
    public void Write_NInventeAucuneRaisonDepuisLAuthentificationOuLesUrls()
    {
        string markdown = Writer.Write(BuildIncident(Array.Empty<string>()));

        string section = GetExplicationSection(markdown);

        string nl = Environment.NewLine;

        string expected =
            $"## Explication du score de risque{nl}{nl}" +
            $"Aucune raison de risque identifiée.{nl}{nl}";

        Assert.Equal(expected, section);

        Assert.DoesNotContain("Échec d'authentification DKIM", section);
        Assert.DoesNotContain("URL suspecte détectée", section);
    }

    [Fact]
    public void Write_PreserveUneRaisonArbitraireSansReformulation()
    {
        string markdown = Writer.Write(BuildIncident(["Raison arbitraire conservée"]));

        string section = GetExplicationSection(markdown);

        Assert.Contains("- Raison arbitraire conservée", section);
        Assert.DoesNotContain("Aucune raison de risque identifiée.", section);
    }

    [Fact]
    public void Write_AfficheAucunElementPourCollectionsVides()
    {
        var incident = new FraudIncident
        {
            IncidentId     = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            CreatedAt      = new DateTimeOffset(2026, 7, 3, 12, 0, 0, TimeSpan.Zero),
            Evidence       = new EvidenceSource { FileName = "empty.eml" },
            Identity       = new MailIdentity(),
            Authentication = new AuthenticationAssessment(),
            Classification = FraudClassification.Unknown,
            RiskScore      = new RiskScore { Value = 0, Level = RiskLevel.Unknown },
        };

        string markdown = Writer.Write(incident);

        Assert.Contains("Aucun élément détecté.", markdown);
    }

    [Fact]
    public void Write_LeveArgumentNullExceptionSiIncidentEstNull()
    {
        Assert.Throws<ArgumentNullException>(() => Writer.Write(null!));
    }
}
