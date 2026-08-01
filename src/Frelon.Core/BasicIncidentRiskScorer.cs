namespace Frelon.Core;

/// <summary>
/// Implémentation locale et déterministe de <see cref="IIncidentRiskScorer"/>.
/// </summary>
public sealed class BasicIncidentRiskScorer : IIncidentRiskScorer
{
    /// <summary>Poids appliqué lorsqu'une authentification SPF est en échec.</summary>
    public const double SpfFailWeight = 15.0;

    /// <summary>Poids appliqué lorsqu'une authentification DKIM est en échec.</summary>
    public const double DkimFailWeight = 15.0;

    /// <summary>Poids appliqué lorsqu'une authentification DMARC est en échec.</summary>
    public const double DmarcFailWeight = 30.0;

    /// <summary>Poids appliqué lorsqu'au moins une URL est explicitement suspecte.</summary>
    public const double SuspiciousUrlWeight = 20.0;

    /// <summary>Poids appliqué lorsqu'au moins une pièce jointe est explicitement suspecte.</summary>
    public const double SuspiciousAttachmentWeight = 30.0;

    /// <summary>Valeur maximale autorisée pour le score.</summary>
    public const double MaxScore = 100.0;

    /// <summary>Raison ajoutée lorsqu'un résultat SPF vaut fail.</summary>
    public const string SpfFailReason = "Échec d'authentification SPF";

    /// <summary>Raison ajoutée lorsqu'un résultat DKIM vaut fail.</summary>
    public const string DkimFailReason = "Échec d'authentification DKIM";

    /// <summary>Raison ajoutée lorsqu'un résultat DMARC vaut fail.</summary>
    public const string DmarcFailReason = "Échec d'authentification DMARC";

    /// <summary>Raison ajoutée lorsqu'au moins une URL est suspecte.</summary>
    public const string SuspiciousUrlReason = "URL suspecte détectée";

    /// <summary>Raison ajoutée lorsqu'au moins une pièce jointe est suspecte.</summary>
    public const string SuspiciousAttachmentReason = "Pièce jointe suspecte détectée";

    /// <inheritdoc/>
    public RiskScore Score(FraudIncident incident)
    {
        ArgumentNullException.ThrowIfNull(incident);

        var score = 0.0;
        var reasons = new List<string>(5);

        if (IsFail(incident.Authentication.SpfResult))
        {
            score += SpfFailWeight;
            reasons.Add(SpfFailReason);
        }

        if (IsFail(incident.Authentication.DkimResult))
        {
            score += DkimFailWeight;
            reasons.Add(DkimFailReason);
        }

        if (IsFail(incident.Authentication.DmarcResult))
        {
            score += DmarcFailWeight;
            reasons.Add(DmarcFailReason);
        }

        if (HasSuspiciousUrl(incident.Urls))
        {
            score += SuspiciousUrlWeight;
            reasons.Add(SuspiciousUrlReason);
        }

        if (HasSuspiciousAttachment(incident.Attachments))
        {
            score += SuspiciousAttachmentWeight;
            reasons.Add(SuspiciousAttachmentReason);
        }

        score = Math.Min(score, MaxScore);

        return new RiskScore
        {
            Value = score,
            Level = MapLevel(score),
            Reasons = reasons,
        };
    }

    private static bool IsFail(string? value)
        => value is not null && string.Equals(value.Trim(), "fail", StringComparison.OrdinalIgnoreCase);

    private static bool HasSuspiciousUrl(IEnumerable<UrlIndicator> urls)
    {
        foreach (var url in urls)
        {
            if (url.IsSuspicious)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasSuspiciousAttachment(IEnumerable<AttachmentIndicator> attachments)
    {
        foreach (var attachment in attachments)
        {
            if (attachment.IsSuspicious)
            {
                return true;
            }
        }

        return false;
    }

    private static RiskLevel MapLevel(double score)
        => score == 0
            ? RiskLevel.Unknown
            : score < 25
                ? RiskLevel.Low
                : score < 50
                    ? RiskLevel.Medium
                    : score < 75
                        ? RiskLevel.High
                        : RiskLevel.Critical;
}