namespace Frelon.Core;

/// <summary>
/// Représente l'évaluation des mécanismes d'authentification du mail : SPF, DKIM et DMARC.
/// </summary>
public sealed record AuthenticationAssessment
{
    /// <summary>Résultat de la vérification SPF (ex. : pass, fail, softfail, neutral).</summary>
    public string? SpfResult { get; init; }

    /// <summary>Résultat de la vérification DKIM (ex. : pass, fail, none).</summary>
    public string? DkimResult { get; init; }

    /// <summary>Résultat de la vérification DMARC (ex. : pass, fail, none).</summary>
    public string? DmarcResult { get; init; }

    /// <summary>Valeur brute du header Authentication-Results, telle qu'extraite du mail.</summary>
    public string? AuthenticationResultsRaw { get; init; }

    /// <summary>Indique si les résultats d'authentification paraissent suspects ou incohérents.</summary>
    public bool IsSuspicious { get; init; }
}
