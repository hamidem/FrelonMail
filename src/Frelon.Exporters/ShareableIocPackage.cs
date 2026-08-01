namespace Frelon.Exporters;

/// <summary>
/// Partie minimisée de l'export. Elle ne contient aucune référence locale aux incidents.
/// </summary>
public sealed record ShareableIocPackage
{
    /// <summary>Crée un paquet en mémoire contenant ses trois documents attendus.</summary>
    public ShareableIocPackage(
        Guid exportId,
        DateOnly generatedOn,
        IReadOnlyList<ShareableIocArtifact> artifacts)
    {
        if (exportId == Guid.Empty)
        {
            throw new ArgumentException(
                "L'identifiant de l'export ne peut pas être vide.",
                nameof(exportId));
        }

        if (generatedOn == default)
        {
            throw new ArgumentException(
                "La date UTC du paquet est obligatoire.",
                nameof(generatedOn));
        }

        ArgumentNullException.ThrowIfNull(artifacts);

        if (artifacts.Count != 3 ||
            artifacts.Any(artifact => artifact is null) ||
            artifacts.Select(artifact => artifact.FileName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() != artifacts.Count)
        {
            throw new ArgumentException(
                "Le paquet doit contenir trois documents présents et nommés distinctement.",
                nameof(artifacts));
        }

        ExportId = exportId;
        GeneratedOn = generatedOn;
        SuggestedArchiveFileName = $"frelon-iocs-partage-{exportId:N}.zip";
        Artifacts = [.. artifacts];
    }

    /// <summary>Identifiant propre à cet export partageable.</summary>
    public Guid ExportId { get; }

    /// <summary>Date UTC arrondie au jour, seule temporalité présente dans le paquet.</summary>
    public DateOnly GeneratedOn { get; }

    /// <summary>Nom suggéré pour une future archive créée par la présentation.</summary>
    public string SuggestedArchiveFileName { get; }

    /// <summary>Guide, JSON et CSV minimisés.</summary>
    public IReadOnlyList<ShareableIocArtifact> Artifacts { get; }
}
