using System.Globalization;
using System.Text;
using Frelon.Core;

namespace Frelon.Reports;

/// <summary>
/// Produit un signalement Markdown local seulement après confirmation humaine de la fraude.
/// Les valeurs issues de l'email sont neutralisées pour rester du texte dans un lecteur Markdown.
/// </summary>
public sealed class BasicValidatedIncidentMarkdownReportWriter : IValidatedIncidentMarkdownReportWriter
{
    private const string NonRenseigne = "Non renseigné";

    /// <inheritdoc />
    public bool CanWrite(IncidentReviewDecision? decision)
        => decision is
        {
            Verdict: ReviewVerdict.ConfirmedFraud,
            Classification: not null and not FraudClassification.Unknown and not FraudClassification.Suspicious
        };

    /// <inheritdoc />
    public string Write(FraudIncident incident, IncidentReviewDecision decision)
    {
        ArgumentNullException.ThrowIfNull(incident);
        ArgumentNullException.ThrowIfNull(decision);

        if (decision.IncidentId != incident.IncidentId)
        {
            throw new ArgumentException(
                "La décision humaine ne correspond pas à l'incident signalé.",
                nameof(decision));
        }

        if (!CanWrite(decision))
        {
            throw new InvalidOperationException(
                "Un signalement exige une fraude confirmée et catégorisée par une décision humaine.");
        }

        var output = new StringBuilder();
        output.AppendLine("# Signalement Frelon validé humainement");
        output.AppendLine();
        output.AppendLine("> Document préparé localement pour transmission manuelle. Frelon ne l'a envoyé à aucun tiers.");
        output.AppendLine();

        AddHumanValidation(output, decision);
        AddIncidentTraceability(output, incident);
        AddDeclaredIdentity(output, incident.Identity);
        AddAutomatedObservations(output, incident);
        AddIndicators(output, incident.Iocs);
        AddAttachments(output, incident.Attachments);

        output.AppendLine("## Cadre d'utilisation");
        output.AppendLine();
        output.AppendLine("Ce document synthétise une décision humaine et des observations techniques locales. " +
            "Le destinataire et le canal de transmission doivent être vérifiés par l'analyste avant tout envoi.");

        return output.ToString();
    }

    private static void AddHumanValidation(StringBuilder output, IncidentReviewDecision decision)
    {
        output.AppendLine("## Validation humaine");
        output.AppendLine();
        AddField(output, "Verdict", "Fraude confirmée");
        AddField(output, "Catégorie retenue", ClassificationLabel(decision.Classification!.Value));
        AddField(output, "Décision prise le", decision.DecidedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        AddField(output, "Identifiant de revue", decision.ReviewId.ToString("D"));
        AddField(output, "Note de l'analyste", decision.Notes);
        output.AppendLine();
    }

    private static void AddIncidentTraceability(StringBuilder output, FraudIncident incident)
    {
        output.AppendLine("## Traçabilité de l'incident");
        output.AppendLine();
        AddField(output, "Identifiant", incident.IncidentId.ToString("D"));
        AddField(output, "Créé le", incident.CreatedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        AddField(output, "Fichier source", incident.Evidence.FileName);
        AddField(output, "SHA-256 de la preuve", incident.Evidence.Sha256);
        AddField(
            output,
            "Importé le",
            incident.Evidence.ImportedAt?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        output.AppendLine();
    }

    private static void AddDeclaredIdentity(StringBuilder output, MailIdentity identity)
    {
        output.AppendLine("## Identité déclarée par le message");
        output.AppendLine();
        output.AppendLine("> Ces informations proviennent du message et peuvent avoir été falsifiées.");
        output.AppendLine();
        AddField(output, "Sujet", identity.Subject);
        AddField(output, "From", identity.From);
        AddField(output, "Reply-To", identity.ReplyTo);
        AddField(output, "Return-Path", identity.ReturnPath);
        AddField(output, "Message-ID", identity.MessageId);
        output.AppendLine();
    }

    private static void AddAutomatedObservations(StringBuilder output, FraudIncident incident)
    {
        output.AppendLine("## Observations automatiques locales");
        output.AppendLine();
        output.AppendLine("> Ces observations expliquent l'analyse mais ne remplacent pas la décision humaine ci-dessus.");
        output.AppendLine();
        AddField(output, "Score de risque", incident.RiskScore.Value.ToString("0.##", CultureInfo.InvariantCulture));
        AddField(output, "Niveau de risque", incident.RiskScore.Level.ToString());
        AddField(output, "Classification automatique", incident.Classification.ToString());
        AddField(output, "SPF", incident.Authentication.SpfResult);
        AddField(output, "DKIM", incident.Authentication.DkimResult);
        AddField(output, "DMARC", incident.Authentication.DmarcResult);

        if (incident.RiskScore.Reasons.Count != 0)
        {
            output.AppendLine();
            output.AppendLine("### Raisons du score");
            output.AppendLine();
            foreach (var reason in incident.RiskScore.Reasons)
            {
                output.AppendLine($"- {Escape(reason)}");
            }
        }

        output.AppendLine();
    }

    private static void AddIndicators(StringBuilder output, IReadOnlyList<Ioc> indicators)
    {
        output.AppendLine("## Indicateurs techniques");
        output.AppendLine();
        if (indicators.Count == 0)
        {
            output.AppendLine("Aucun indicateur technique structuré.");
            output.AppendLine();
            return;
        }

        foreach (var indicator in indicators)
        {
            output.AppendLine(
                $"- **{Escape(indicator.Type.ToString())}** : {Escape(indicator.Value)} " +
                $"(confiance {indicator.Confidence.ToString("P0", CultureInfo.InvariantCulture)})");
        }

        output.AppendLine();
    }

    private static void AddAttachments(StringBuilder output, IReadOnlyList<AttachmentIndicator> attachments)
    {
        output.AppendLine("## Pièces jointes observées");
        output.AppendLine();
        if (attachments.Count == 0)
        {
            output.AppendLine("Aucune pièce jointe observée.");
            output.AppendLine();
            return;
        }

        foreach (var attachment in attachments)
        {
            output.AppendLine($"- **Nom** : {Escape(attachment.FileName)}");
            output.AppendLine($"  - Type : {Escape(attachment.ContentType)}");
            output.AppendLine($"  - SHA-256 : {Escape(attachment.Sha256)}");
            output.AppendLine($"  - Signal défensif : {(attachment.IsSuspicious ? "Oui" : "Non")}");
        }

        output.AppendLine();
    }

    private static void AddField(StringBuilder output, string label, string? value)
        => output.AppendLine($"- **{label}** : {Escape(value)}");

    private static string Escape(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return NonRenseigne;
        }

        var normalized = string.Concat(
            value.Trim().Select(character => char.IsControl(character) ? ' ' : character));
        var encoded = normalized
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
        var output = new StringBuilder(encoded.Length);
        foreach (var character in encoded)
        {
            if (character is '\\' or '`' or '*' or '_' or '{' or '}' or '[' or ']' or '(' or ')' or '!' or '|')
            {
                output.Append('\\');
            }

            output.Append(character);
        }

        return output.ToString();
    }

    private static string ClassificationLabel(FraudClassification classification)
        => classification switch
        {
            FraudClassification.Spam => "Spam",
            FraudClassification.Phishing => "Hameçonnage",
            FraudClassification.Malware => "Logiciel malveillant",
            FraudClassification.Scam => "Escroquerie",
            FraudClassification.BrandImpersonation => "Usurpation de marque",
            FraudClassification.CredentialTheft => "Vol d'identifiants",
            _ => throw new ArgumentOutOfRangeException(nameof(classification), classification, null)
        };
}
