namespace Frelon.Core;

/// <summary>
/// Classifieur déterministe volontairement prudent : il ne transforme jamais une piste en verdict.
/// </summary>
public sealed class CautiousIncidentClassifier : IIncidentClassifier
{
    /// <summary>Raison associée à une pièce jointe déjà signalée comme suspecte.</summary>
    public const string SuspiciousAttachmentReason = "Une pièce jointe est explicitement signalée comme suspecte";

    /// <summary>Raison associée à une URL déjà signalée comme suspecte.</summary>
    public const string SuspiciousUrlReason = "Une URL est explicitement signalée comme suspecte";

    /// <summary>Raison associée à plusieurs échecs d'authentification.</summary>
    public const string AuthenticationFailuresReason = "Plusieurs mécanismes d'authentification sont en échec";

    /// <summary>Raison associée à au moins un échec d'authentification.</summary>
    public const string AuthenticationFailureReason = "Au moins un mécanisme d'authentification est en échec";

    /// <summary>Raison associée à une incohérence explicitement signalée.</summary>
    public const string SuspiciousAuthenticationReason = "Les résultats d'authentification sont signalés comme incohérents";

    /// <summary>Raison associée à des signaux de natures différentes.</summary>
    public const string MixedSignalsReason = "Des signaux suspects de natures différentes sont présents";

    /// <inheritdoc />
    public ClassificationAssessment Assess(FraudIncident incident)
    {
        ArgumentNullException.ThrowIfNull(incident);

        var hasSuspiciousAttachment = incident.Attachments.Any(attachment => attachment.IsSuspicious);
        var hasSuspiciousUrl = incident.Urls.Any(url => url.IsSuspicious);
        var authenticationFailureCount = CountAuthenticationFailures(incident.Authentication);

        if (hasSuspiciousAttachment && hasSuspiciousUrl)
        {
            return new ClassificationAssessment(
                FraudClassification.Suspicious,
                ClassificationConfidence.Medium,
                [SuspiciousAttachmentReason, SuspiciousUrlReason, MixedSignalsReason]);
        }

        if (hasSuspiciousAttachment)
        {
            return new ClassificationAssessment(
                FraudClassification.Malware,
                ClassificationConfidence.Medium,
                [SuspiciousAttachmentReason]);
        }

        if (hasSuspiciousUrl && authenticationFailureCount > 0)
        {
            return new ClassificationAssessment(
                FraudClassification.Phishing,
                ClassificationConfidence.Medium,
                [SuspiciousUrlReason, AuthenticationFailureReason]);
        }

        if (hasSuspiciousUrl)
        {
            return new ClassificationAssessment(
                FraudClassification.Suspicious,
                ClassificationConfidence.Low,
                [SuspiciousUrlReason]);
        }

        if (authenticationFailureCount >= 2)
        {
            return new ClassificationAssessment(
                FraudClassification.Suspicious,
                ClassificationConfidence.Low,
                [AuthenticationFailuresReason]);
        }

        if (incident.Authentication.IsSuspicious)
        {
            return new ClassificationAssessment(
                FraudClassification.Suspicious,
                ClassificationConfidence.Low,
                [SuspiciousAuthenticationReason]);
        }

        return ClassificationAssessment.None;
    }

    private static int CountAuthenticationFailures(AuthenticationAssessment authentication)
        => new[]
        {
            authentication.SpfResult,
            authentication.DkimResult,
            authentication.DmarcResult
        }.Count(IsFail);

    private static bool IsFail(string? value)
        => string.Equals(value?.Trim(), "fail", StringComparison.OrdinalIgnoreCase);
}
