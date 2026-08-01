namespace Frelon.Reports;

/// <summary>Rôle du destinataire d'un document de signalement préparé localement.</summary>
public enum TakedownRecipientType
{
    /// <summary>Opérateur hébergeant le contenu ou l'infrastructure observée.</summary>
    HostingProvider,

    /// <summary>Registrar responsable d'un nom de domaine observé.</summary>
    DomainRegistrar,

    /// <summary>Fournisseur de messagerie susceptible d'enquêter sur l'acheminement.</summary>
    EmailProvider,

    /// <summary>Service spécialisé dans le traitement des campagnes de phishing.</summary>
    AntiPhishingService,
}
