namespace Frelon.Core;

/// <summary>Niveau de confiance d'une piste de classification automatique.</summary>
public enum ClassificationConfidence
{
    /// <summary>Aucune piste suffisamment étayée.</summary>
    None,
    /// <summary>Piste faible nécessitant une attention humaine.</summary>
    Low,
    /// <summary>Piste soutenue par plusieurs signaux cohérents.</summary>
    Medium
}
