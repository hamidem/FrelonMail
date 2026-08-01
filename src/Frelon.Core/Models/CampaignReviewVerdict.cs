namespace Frelon.Core;

/// <summary>Conclusion explicite d'une revue humaine de campagne candidate.</summary>
public enum CampaignReviewVerdict
{
    /// <summary>Les éléments disponibles ne permettent pas encore de conclure.</summary>
    Inconclusive,

    /// <summary>Le rapprochement proposé ne représente pas une même campagne.</summary>
    Rejected,

    /// <summary>Les incidents ont été confirmés comme appartenant à une même campagne.</summary>
    Confirmed,
}
