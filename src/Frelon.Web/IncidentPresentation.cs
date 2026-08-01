using Frelon.Core;

namespace Frelon.Web;

/// <summary>Projection d'un incident destinée à l'interface locale.</summary>
public sealed record IncidentPresentation(
    Guid IncidentId,
    DateTimeOffset CreatedAt,
    string SourceFileName,
    string? SourceSha256,
    string? Subject,
    string? From,
    string? ReplyTo,
    double RiskValue,
    RiskLevel RiskLevel,
    FraudClassification Classification,
    IncidentGuidancePresentation Guidance,
    ClassificationAssessmentPresentation ClassificationAssessment,
    IReadOnlyList<string> RiskReasons,
    AuthenticationPresentation Authentication,
    int UrlCount,
    int AttachmentCount,
    IReadOnlyList<DefensiveFindingPresentation> DefensiveFindings,
    IReadOnlyList<IocPresentation> Iocs)
{
    /// <summary>Crée une projection sans modifier l'incident source.</summary>
    public static IncidentPresentation FromIncident(FraudIncident incident)
    {
        ArgumentNullException.ThrowIfNull(incident);

        return new IncidentPresentation(
            incident.IncidentId,
            incident.CreatedAt,
            incident.Evidence.FileName,
            incident.Evidence.Sha256,
            incident.Identity.Subject,
            incident.Identity.From,
            incident.Identity.ReplyTo,
            incident.RiskScore.Value,
            incident.RiskScore.Level,
            incident.Classification,
            IncidentGuidancePresentation.FromIncident(incident),
            new ClassificationAssessmentPresentation(
                incident.ClassificationAssessment.Classification,
                incident.ClassificationAssessment.Confidence,
                [.. incident.ClassificationAssessment.Reasons]),
            [.. incident.RiskScore.Reasons],
            new AuthenticationPresentation(
                incident.Authentication.SpfResult,
                incident.Authentication.DkimResult,
                incident.Authentication.DmarcResult,
                incident.Authentication.IsSuspicious),
            incident.Urls.Count,
            incident.Attachments.Count,
            [
                .. incident.Urls
                    .Where(url => url.IsSuspicious)
                    .Select(url => new DefensiveFindingPresentation(
                        "Url",
                        url.RawValue,
                        [.. url.Reasons])),
                .. incident.Attachments
                    .Where(attachment => attachment.IsSuspicious)
                    .Select(attachment => new DefensiveFindingPresentation(
                        "Attachment",
                        attachment.FileName,
                        [.. attachment.Reasons]))
            ],
            [.. incident.Iocs.Select(ioc => new IocPresentation(
                ioc.Type,
                ioc.Value,
                ioc.Confidence,
                ioc.Source))]);
    }
}

/// <summary>Piste automatique explicitement distincte de la validation humaine.</summary>
public sealed record ClassificationAssessmentPresentation(
    FraudClassification Classification,
    ClassificationConfidence Confidence,
    IReadOnlyList<string> Reasons);

/// <summary>Résultats d'authentification utiles à l'affichage.</summary>
public sealed record AuthenticationPresentation(
    string? Spf,
    string? Dkim,
    string? Dmarc,
    bool IsSuspicious);

/// <summary>IOC projeté pour l'affichage local.</summary>
public sealed record IocPresentation(
    IocType Type,
    string Value,
    double Confidence,
    string? Source);

/// <summary>Signal local déclenché par une règle défensive explicable.</summary>
public sealed record DefensiveFindingPresentation(
    string Kind,
    string Value,
    IReadOnlyList<string> Reasons);
