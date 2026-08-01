using Frelon.Core;

namespace Frelon.Reports;

/// <summary>Produit un signalement local adossé à une confirmation humaine explicite.</summary>
public interface IValidatedIncidentMarkdownReportWriter
{
    /// <summary>Indique si la décision autorise la génération d'un signalement.</summary>
    bool CanWrite(IncidentReviewDecision? decision);

    /// <summary>Génère le signalement sans l'envoyer ni écrire sur le disque.</summary>
    string Write(FraudIncident incident, IncidentReviewDecision decision);
}
