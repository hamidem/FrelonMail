namespace Frelon.Core;

/// <summary>Produit une piste de classification sans rendre de verdict.</summary>
public interface IIncidentClassifier
{
    /// <summary>Évalue les signaux locaux d'un incident.</summary>
    ClassificationAssessment Assess(FraudIncident incident);
}
