using Frelon.Core;

namespace Frelon.Web;

/// <summary>
/// Lecture immédiate d'un incident, destinée à guider sans masquer la preuve technique.
/// </summary>
public sealed record IncidentGuidancePresentation(
    string Headline,
    string Explanation,
    IReadOnlyList<string> KeyObservations,
    IReadOnlyList<string> RecommendedActions,
    string Boundary)
{
    private const int MaximumKeyObservations = 3;

    /// <summary>Construit une synthèse prudente à partir des résultats déterministes existants.</summary>
    public static IncidentGuidancePresentation FromIncident(FraudIncident incident)
    {
        ArgumentNullException.ThrowIfNull(incident);

        var observations = BuildKeyObservations(incident.RiskScore.Reasons);

        if (observations.Count == 0)
        {
            observations.Add(
                "Aucun élément inhabituel n'a été relevé par les vérifications actuelles. Cela ne prouve pas que le message est sûr.");
        }

        return new IncidentGuidancePresentation(
            HeadlineFor(incident.RiskScore.Level),
            ExplanationFor(incident.RiskScore.Level),
            observations,
            RecommendedActionsFor(incident.RiskScore.Level),
            "Frelon observe le message sans ouvrir ses liens ni exécuter ses pièces jointes. " +
            "Cette analyse automatique doit rester soumise à une validation humaine.");
    }

    private static string HeadlineFor(RiskLevel level)
        => level switch
        {
            RiskLevel.Low => "Vérifiez l'expéditeur avant d'agir",
            RiskLevel.Medium => "N'agissez pas avant d'avoir vérifié ce message",
            RiskLevel.High => "Traitez ce message comme suspect jusqu'à vérification",
            RiskLevel.Critical => "N'interagissez pas avec ce message",
            _ => "Frelon ne peut pas se prononcer sur ce message"
        };

    private static string ExplanationFor(RiskLevel level)
        => level switch
        {
            RiskLevel.Low =>
                "Un élément inhabituel a été relevé, sans suffire à déterminer si le message est frauduleux.",
            RiskLevel.Medium =>
                "Des éléments inhabituels justifient une vérification avant toute interaction.",
            RiskLevel.High =>
                "Plusieurs vérifications n'ont pas permis de confirmer la fiabilité apparente du message.",
            RiskLevel.Critical =>
                "L'analyse cumule plusieurs signaux importants ou a repéré un contenu particulièrement risqué.",
            _ =>
                "Aucun signal décisif n'a été relevé, mais ce résultat ne constitue pas un feu vert."
        };

    private static IReadOnlyList<string> RecommendedActionsFor(RiskLevel level)
        => level switch
        {
            RiskLevel.Medium =>
            [
                "N'utilisez pas les liens ou pièces jointes contenus dans le message.",
                "Vérifiez la demande et l'expéditeur par un autre canal.",
                "En cas de doute, conservez et transmettez le dossier d'analyse à votre référent sécurité ou informatique."
            ],
            RiskLevel.High =>
            [
                "Ne cliquez sur aucun lien et n'ouvrez aucune pièce jointe.",
                "Contactez l'expéditeur supposé par un moyen habituel, sans répondre à ce message.",
                "Si le doute persiste, conservez et transmettez le dossier d'analyse à votre référent sécurité ou informatique."
            ],
            RiskLevel.Critical =>
            [
                "Ne répondez pas et n'utilisez aucun contenu du message.",
                "Conservez le message et le dossier d'analyse comme éléments de vérification.",
                "Signalez rapidement le cas à votre référent sécurité ou informatique."
            ],
            _ =>
            [
                "Relisez la demande sans utiliser ses liens ni ses pièces jointes.",
                "Si elle est inattendue, vérifiez l'expéditeur par un autre canal.",
                "En cas de doute, demandez l'avis de votre référent sécurité ou informatique."
            ]
        };

    private static List<string> BuildKeyObservations(IReadOnlyList<string> reasons)
    {
        var observations = new List<string>();

        if (reasons.Contains(
                BasicIncidentRiskScorer.SuspiciousAttachmentReason,
                StringComparer.Ordinal))
        {
            observations.Add(
                "Au moins une pièce jointe présente une caractéristique habituellement risquée.");
        }

        if (reasons.Contains(
                BasicIncidentRiskScorer.SuspiciousUrlReason,
                StringComparer.Ordinal))
        {
            observations.Add(
                "Au moins un lien présente une caractéristique habituellement risquée.");
        }

        var authenticationFailureCount = new[]
        {
            BasicIncidentRiskScorer.SpfFailReason,
            BasicIncidentRiskScorer.DkimFailReason,
            BasicIncidentRiskScorer.DmarcFailReason
        }.Count(reason => reasons.Contains(reason, StringComparer.Ordinal));

        if (authenticationFailureCount >= 2)
        {
            observations.Add(
                "Plusieurs vérifications n'ont pas confirmé que le message provient réellement de l'expéditeur affiché.");
        }
        else if (reasons.Contains(BasicIncidentRiskScorer.SpfFailReason, StringComparer.Ordinal))
        {
            observations.Add(
                "Le message ne semble pas provenir d'un serveur autorisé par le domaine affiché.");
        }
        else if (reasons.Contains(BasicIncidentRiskScorer.DkimFailReason, StringComparer.Ordinal))
        {
            observations.Add(
                "La vérification de l'intégrité et de l'origine du message a échoué.");
        }
        else if (reasons.Contains(BasicIncidentRiskScorer.DmarcFailReason, StringComparer.Ordinal))
        {
            observations.Add(
                "Les informations d'envoi ne correspondent pas au domaine affiché.");
        }

        var knownReasons = new HashSet<string>(
        [
            BasicIncidentRiskScorer.SuspiciousAttachmentReason,
            BasicIncidentRiskScorer.SuspiciousUrlReason,
            BasicIncidentRiskScorer.SpfFailReason,
            BasicIncidentRiskScorer.DkimFailReason,
            BasicIncidentRiskScorer.DmarcFailReason
        ], StringComparer.Ordinal);

        observations.AddRange(reasons.Where(reason => !knownReasons.Contains(reason)));

        return observations
            .Distinct(StringComparer.Ordinal)
            .Take(MaximumKeyObservations)
            .ToList();
    }
}
