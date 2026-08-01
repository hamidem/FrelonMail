namespace Frelon.Core;

/// <summary>
/// Représente une action défensive recommandée suite à l'analyse d'un incident.
/// Aucune action n'est exécutée automatiquement — une validation humaine est requise lorsque indiqué.
/// </summary>
public sealed record RecommendedAction
{
    /// <summary>Type de l'action recommandée.</summary>
    public required RecommendedActionType Type { get; init; }

    /// <summary>Libellé court de l'action, destiné à l'affichage ou au rapport.</summary>
    public required string Label { get; init; }

    /// <summary>Description détaillée de l'action et de son contexte, si disponible.</summary>
    public string? Description { get; init; }

    /// <summary>Indique si cette action nécessite une validation humaine avant d'être entreprise.</summary>
    public bool RequiresHumanValidation { get; init; }
}
