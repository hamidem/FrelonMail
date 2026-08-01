namespace Frelon.Core;

/// <summary>Conclusion explicite d'une revue humaine d'incident.</summary>
public enum ReviewVerdict
{
    /// <summary>Les éléments disponibles ne permettent pas de conclure.</summary>
    Inconclusive,

    /// <summary>Le message a été considéré comme bénin après revue.</summary>
    Benign,

    /// <summary>Le message reste suspect mais la fraude n'est pas confirmée.</summary>
    Suspicious,

    /// <summary>La fraude a été confirmée par une décision humaine.</summary>
    ConfirmedFraud
}
