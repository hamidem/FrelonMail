namespace Frelon.Mail;

/// <summary>Quotas défensifs appliqués à toute preuve analysée par Frelon.</summary>
public sealed record EmailAnalysisLimits
{
    /// <summary>Limite produit commune au Web, à la CLI et aux parseurs.</summary>
    public const int DefaultMaxSourceBytes = 25 * 1024 * 1024;

    /// <summary>Quotas utilisés par le pipeline de référence.</summary>
    public static EmailAnalysisLimits Default { get; } = new();

    /// <summary>Taille maximale du fichier source.</summary>
    public int MaxSourceBytes { get; init; } = DefaultMaxSourceBytes;

    /// <summary>Profondeur maximale des entités MIME imbriquées.</summary>
    public int MaxMimeDepth { get; init; } = 32;

    /// <summary>Profondeur maximale des groupes d'adresses imbriqués.</summary>
    public int MaxAddressGroupDepth { get; init; } = 16;

    /// <summary>Nombre maximal d'en-têtes conservés.</summary>
    public int MaxHeaderCount { get; init; } = 1_000;

    /// <summary>Nombre maximal de caractères cumulé des en-têtes.</summary>
    public int MaxHeaderCharacters { get; init; } = 1_000_000;

    /// <summary>Nombre maximal de caractères pour chaque représentation du corps.</summary>
    public int MaxBodyCharacters { get; init; } = 10_000_000;

    /// <summary>Nombre maximal de pièces jointes.</summary>
    public int MaxAttachmentCount { get; init; } = 100;

    /// <summary>Taille maximale d'une pièce jointe décodée.</summary>
    public int MaxAttachmentBytes { get; init; } = 20 * 1024 * 1024;

    /// <summary>Taille maximale cumulée des pièces jointes décodées.</summary>
    public int MaxTotalAttachmentBytes { get; init; } = DefaultMaxSourceBytes;

    internal void Validate()
    {
        ValidatePositive(MaxSourceBytes, nameof(MaxSourceBytes));
        ValidatePositive(MaxMimeDepth, nameof(MaxMimeDepth));
        ValidatePositive(MaxAddressGroupDepth, nameof(MaxAddressGroupDepth));
        ValidatePositive(MaxHeaderCount, nameof(MaxHeaderCount));
        ValidatePositive(MaxHeaderCharacters, nameof(MaxHeaderCharacters));
        ValidatePositive(MaxBodyCharacters, nameof(MaxBodyCharacters));
        ValidatePositive(MaxAttachmentCount, nameof(MaxAttachmentCount));
        ValidatePositive(MaxAttachmentBytes, nameof(MaxAttachmentBytes));
        ValidatePositive(MaxTotalAttachmentBytes, nameof(MaxTotalAttachmentBytes));
    }

    private static void ValidatePositive(int value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
