using System.Text;
using Frelon.Core;

namespace Frelon.Reports;

/// <summary>
/// Implémentation de <see cref="IIncidentMarkdownReportWriter"/> produisant un rapport Markdown
/// lisible par un humain à partir d'un <see cref="FraudIncident"/>.
/// N'effectue aucun appel réseau et n'écrit pas sur le disque.
/// </summary>
public sealed class BasicIncidentMarkdownReportWriter : IIncidentMarkdownReportWriter
{
    private const string NonRenseigne = "Non renseigné";
    private const string AucunElement = "Aucun élément détecté.";
    private const string AucuneRaisonRisque = "Aucune raison de risque identifiée.";

    /// <inheritdoc/>
    public string Write(FraudIncident incident)
    {
        ArgumentNullException.ThrowIfNull(incident);

        var sb = new StringBuilder();

        sb.AppendLine("# Rapport d'incident Frelon");
        sb.AppendLine();

        AjouterResume(sb, incident);
        AjouterPisteClassification(sb, incident.ClassificationAssessment);
        AjouterExplicationScore(sb, incident.RiskScore);
        AjouterPreuveSource(sb, incident.Evidence);
        AjouterIdentite(sb, incident.Identity);
        AjouterAuthentification(sb, incident.Authentication);
        AjouterChaineReceived(sb, incident.ReceivedChain);
        AjouterUrls(sb, incident.Urls);
        AjouterPiecesJointes(sb, incident.Attachments);
        AjouterIoc(sb, incident.Iocs);
        AjouterActionsRecommandees(sb, incident.RecommendedActions);

        return sb.ToString();
    }

    private static void AjouterResume(StringBuilder sb, FraudIncident incident)
    {
        sb.AppendLine("## Résumé");
        sb.AppendLine();
        sb.AppendLine($"- **Identifiant** : {incident.IncidentId}");
        sb.AppendLine($"- **Date de création** : {incident.CreatedAt:O}");
        sb.AppendLine($"- **Fichier source** : {incident.Evidence.FileName}");
        sb.AppendLine($"- **Classification de l'analyse** : {incident.Classification}");
        sb.AppendLine($"- **Score de risque** : {incident.RiskScore.Value}");
        sb.AppendLine($"- **Niveau de risque** : {incident.RiskScore.Level}");
        sb.AppendLine();
    }

    private static void AjouterPisteClassification(
        StringBuilder sb,
        ClassificationAssessment assessment)
    {
        sb.AppendLine("## Piste de classification automatique");
        sb.AppendLine();
        sb.AppendLine("> Cette piste locale aide la revue ; elle ne constitue ni une preuve ni un verdict.");
        sb.AppendLine();

        if (assessment.Classification == FraudClassification.Unknown)
        {
            sb.AppendLine("Aucune catégorie n'est suffisamment étayée par les signaux disponibles.");
            sb.AppendLine();
            return;
        }

        sb.AppendLine($"- **Catégorie suggérée** : {assessment.Classification}");
        sb.AppendLine($"- **Confiance** : {assessment.Confidence}");
        sb.AppendLine();
        sb.AppendLine("### Signaux explicatifs");
        sb.AppendLine();
        foreach (var reason in assessment.Reasons)
        {
            sb.AppendLine($"- {reason}");
        }

        sb.AppendLine();
    }

    private static void AjouterExplicationScore(StringBuilder sb, RiskScore riskScore)
    {
        sb.AppendLine("## Explication du score de risque");
        sb.AppendLine();

        if (riskScore.Reasons.Count == 0)
        {
            sb.AppendLine(AucuneRaisonRisque);
            sb.AppendLine();
            return;
        }

        foreach (var reason in riskScore.Reasons)
        {
            sb.AppendLine($"- {reason}");
        }

        sb.AppendLine();
    }

    private static void AjouterPreuveSource(StringBuilder sb, EvidenceSource evidence)
    {
        sb.AppendLine("## Preuve source");
        sb.AppendLine();
        sb.AppendLine($"- **Fichier** : {evidence.FileName}");
        sb.AppendLine($"- **Chemin** : {evidence.FilePath ?? NonRenseigne}");
        sb.AppendLine($"- **SHA-256** : {evidence.Sha256 ?? NonRenseigne}");
        sb.AppendLine($"- **Importé le** : {(evidence.ImportedAt.HasValue ? evidence.ImportedAt.Value.ToString("O") : NonRenseigne)}");
        sb.AppendLine();
    }

    private static void AjouterIdentite(StringBuilder sb, MailIdentity identity)
    {
        sb.AppendLine("## Identité déclarée");
        sb.AppendLine();
        sb.AppendLine($"- **Sujet** : {identity.Subject ?? NonRenseigne}");
        sb.AppendLine($"- **From** : {identity.From ?? NonRenseigne}");
        sb.AppendLine($"- **Reply-To** : {identity.ReplyTo ?? NonRenseigne}");
        sb.AppendLine($"- **Return-Path** : {identity.ReturnPath ?? NonRenseigne}");
        sb.AppendLine($"- **Message-ID** : {identity.MessageId ?? NonRenseigne}");
        sb.AppendLine();
    }

    private static void AjouterAuthentification(StringBuilder sb, AuthenticationAssessment auth)
    {
        sb.AppendLine("## Authentification");
        sb.AppendLine();
        sb.AppendLine($"- **Authentication-Results** : {auth.AuthenticationResultsRaw ?? NonRenseigne}");
        sb.AppendLine($"- **SPF** : {auth.SpfResult ?? NonRenseigne}");
        sb.AppendLine($"- **DKIM** : {auth.DkimResult ?? NonRenseigne}");
        sb.AppendLine($"- **DMARC** : {auth.DmarcResult ?? NonRenseigne}");
        sb.AppendLine($"- **Suspect** : {(auth.IsSuspicious ? "Oui" : "Non")}");
        sb.AppendLine();
    }

    private static void AjouterChaineReceived(StringBuilder sb, IReadOnlyList<ReceivedHop> chain)
    {
        sb.AppendLine("## Chaîne Received");
        sb.AppendLine();

        if (chain.Count == 0)
        {
            sb.AppendLine(AucunElement);
            sb.AppendLine();
            return;
        }

        foreach (var hop in chain)
        {
            sb.AppendLine($"### Relais position {hop.Position}");
            sb.AppendLine();
            sb.AppendLine($"- **From** : {hop.From ?? NonRenseigne}");
            sb.AppendLine($"- **By** : {hop.By ?? NonRenseigne}");
            sb.AppendLine($"- **With** : {hop.With ?? NonRenseigne}");
            sb.AppendLine($"- **IP** : {hop.IpAddress ?? NonRenseigne}");
            sb.AppendLine($"- **Horodatage** : {(hop.Timestamp.HasValue ? hop.Timestamp.Value.ToString("O") : NonRenseigne)}");
            sb.AppendLine($"- **Brut** : `{hop.RawValue}`");
            sb.AppendLine();
        }
    }

    private static void AjouterUrls(StringBuilder sb, IReadOnlyList<UrlIndicator> urls)
    {
        sb.AppendLine("## URLs détectées");
        sb.AppendLine();

        if (urls.Count == 0)
        {
            sb.AppendLine(AucunElement);
            sb.AppendLine();
            return;
        }

        foreach (var url in urls)
        {
            sb.AppendLine($"- `{url.RawValue}`{(url.IsSuspicious ? " ⚠ Suspecte" : string.Empty)}");
            foreach (var reason in url.Reasons)
            {
                sb.AppendLine($"  - Raison : {reason}");
            }
        }

        sb.AppendLine();
    }

    private static void AjouterPiecesJointes(StringBuilder sb, IReadOnlyList<AttachmentIndicator> attachments)
    {
        sb.AppendLine("## Pièces jointes détectées");
        sb.AppendLine();

        if (attachments.Count == 0)
        {
            sb.AppendLine(AucunElement);
            sb.AppendLine();
            return;
        }

        foreach (var att in attachments)
        {
            sb.AppendLine($"- **{att.FileName}**{(att.IsSuspicious ? " ⚠ Suspecte" : string.Empty)}");
            if (att.ContentType is not null)
                sb.AppendLine($"  - Type : {att.ContentType}");
            if (att.SizeBytes.HasValue)
                sb.AppendLine($"  - Taille : {att.SizeBytes} octets");
            if (att.Sha256 is not null)
                sb.AppendLine($"  - SHA-256 : {att.Sha256}");
            foreach (var reason in att.Reasons)
            {
                sb.AppendLine($"  - Raison : {reason}");
            }
        }

        sb.AppendLine();
    }

    private static void AjouterIoc(StringBuilder sb, IReadOnlyList<Ioc> iocs)
    {
        sb.AppendLine("## IOC");
        sb.AppendLine();

        if (iocs.Count == 0)
        {
            sb.AppendLine(AucunElement);
            sb.AppendLine();
            return;
        }

        foreach (var ioc in iocs)
        {
            sb.AppendLine($"- [{ioc.Type}] `{ioc.Value}` (confiance : {ioc.Confidence:P0})");
        }

        sb.AppendLine();
    }

    private static void AjouterActionsRecommandees(StringBuilder sb, IReadOnlyList<RecommendedAction> actions)
    {
        sb.AppendLine("## Actions recommandées");
        sb.AppendLine();

        if (actions.Count == 0)
        {
            sb.AppendLine(AucunElement);
            return;
        }

        foreach (var action in actions)
        {
            sb.AppendLine($"- **{action.Label}**{(action.RequiresHumanValidation ? " *(validation humaine requise)*" : string.Empty)}");
            if (action.Description is not null)
                sb.AppendLine($"  {action.Description}");
        }
    }
}
