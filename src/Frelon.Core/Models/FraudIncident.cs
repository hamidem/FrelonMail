namespace Frelon.Core;

/// <summary>
/// Agrégat principal représentant un incident de fraude par email.
/// Regroupe l'ensemble des preuves, indicateurs, scores et actions recommandées
/// issus de l'analyse d'un mail suspect.
/// </summary>
public sealed record FraudIncident
{
    /// <summary>Identifiant unique de l'incident, généré à la création.</summary>
    public required Guid IncidentId { get; init; }

    /// <summary>Date et heure de création de l'incident.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Source de preuve associée à cet incident (fichier .eml analysé).</summary>
    public required EvidenceSource Evidence { get; init; }

    /// <summary>Identités déclarées dans les en-têtes du mail.</summary>
    public required MailIdentity Identity { get; init; }

    /// <summary>Évaluation des mécanismes d'authentification (SPF, DKIM, DMARC).</summary>
    public required AuthenticationAssessment Authentication { get; init; }

    /// <summary>Chaîne des relais de messagerie extraits des headers Received.</summary>
    public IReadOnlyList<ReceivedHop> ReceivedChain { get; init; } = [];

    /// <summary>URLs extraites du mail.</summary>
    public IReadOnlyList<UrlIndicator> Urls { get; init; } = [];

    /// <summary>Pièces jointes détectées dans le mail.</summary>
    public IReadOnlyList<AttachmentIndicator> Attachments { get; init; } = [];

    /// <summary>Indicateurs de compromission extraits ou déduits de l'incident.</summary>
    public IReadOnlyList<Ioc> Iocs { get; init; } = [];

    /// <summary>Classification de la fraude détectée.</summary>
    public required FraudClassification Classification { get; init; }

    /// <summary>Piste automatique explicable, sans valeur de verdict.</summary>
    public ClassificationAssessment ClassificationAssessment { get; init; } = ClassificationAssessment.None;

    /// <summary>Score de risque calculé pour cet incident.</summary>
    public required RiskScore RiskScore { get; init; }

    /// <summary>Actions défensives recommandées suite à l'analyse.</summary>
    public IReadOnlyList<RecommendedAction> RecommendedActions { get; init; } = [];
}
