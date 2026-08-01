namespace Frelon.Core;

/// <summary>
/// Type d'action défensive recommandée suite à l'analyse d'un incident.
/// </summary>
public enum RecommendedActionType
{
    /// <summary>Aucune action recommandée.</summary>
    None,
    /// <summary>Relire et analyser manuellement l'incident.</summary>
    ReviewManually,
    /// <summary>Générer un rapport structuré de l'incident.</summary>
    GenerateReport,
    /// <summary>Exporter les indicateurs de compromission (IOC).</summary>
    ExportIocs,
    /// <summary>Créer une règle de filtrage locale (SpamAssassin, Sieve, etc.).</summary>
    CreateLocalRule,
    /// <summary>Préparer un signalement vers l'hébergeur ou le registrar (abuse report).</summary>
    PrepareAbuseReport,
    /// <summary>Préparer un signalement vers Signal Spam.</summary>
    PrepareSignalSpamReport,
    /// <summary>Préparer un signalement vers l'initiative anti-phishing (Phishing Initiative).</summary>
    PreparePhishingInitiativeReport
}
