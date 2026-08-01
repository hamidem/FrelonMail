namespace Frelon.Reports;

/// <summary>
/// Ensemble de documents préparés localement et encore non transmis.
/// </summary>
public sealed record TakedownPack
{
    /// <summary>Crée un pack en mémoire avec des noms de documents distincts.</summary>
    public TakedownPack(
        Guid packId,
        DateTimeOffset preparedAt,
        string campaignFingerprint,
        IReadOnlyList<TakedownPackArtifact> artifacts)
    {
        if (packId == Guid.Empty)
        {
            throw new ArgumentException(
                "L'identifiant du pack ne peut pas être vide.",
                nameof(packId));
        }

        if (preparedAt == default)
        {
            throw new ArgumentException(
                "La date de préparation est obligatoire.",
                nameof(preparedAt));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(campaignFingerprint);
        if (!Frelon.Core.CampaignCandidate.IsValidFingerprint(campaignFingerprint))
        {
            throw new ArgumentException(
                "L'empreinte de campagne doit être une valeur SHA-256 hexadécimale.",
                nameof(campaignFingerprint));
        }
        ArgumentNullException.ThrowIfNull(artifacts);

        if (artifacts.Count < 3 ||
            artifacts.Any(artifact => artifact is null) ||
            artifacts.Select(artifact => artifact.FileName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() != artifacts.Count)
        {
            throw new ArgumentException(
                "Le pack doit contenir des documents présents et nommés distinctement.",
                nameof(artifacts));
        }

        PackId = packId;
        PreparedAt = preparedAt;
        CampaignFingerprint = campaignFingerprint;
        SuggestedArchiveFileName = $"frelon-takedown-{packId:N}.zip";
        Artifacts = [.. artifacts];
    }

    /// <summary>Identifiant traçable du pack.</summary>
    public Guid PackId { get; }

    /// <summary>Date de préparation locale.</summary>
    public DateTimeOffset PreparedAt { get; }

    /// <summary>Empreinte de la composition de campagne utilisée.</summary>
    public string CampaignFingerprint { get; }

    /// <summary>Nom suggéré si une couche de présentation crée ensuite une archive ZIP.</summary>
    public string SuggestedArchiveFileName { get; }

    /// <summary>Documents communs et adaptés aux destinataires.</summary>
    public IReadOnlyList<TakedownPackArtifact> Artifacts { get; }
}
