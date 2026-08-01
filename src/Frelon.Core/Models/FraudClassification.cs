namespace Frelon.Core;

/// <summary>
/// Classification de la fraude détectée dans un incident.
/// </summary>
public enum FraudClassification
{
    /// <summary>Classification non déterminée.</summary>
    Unknown,
    /// <summary>Message non sollicité (spam).</summary>
    Spam,
    /// <summary>Tentative d'hameçonnage visant à dérober des identifiants ou des données personnelles.</summary>
    Phishing,
    /// <summary>Diffusion de logiciel malveillant.</summary>
    Malware,
    /// <summary>Arnaque ou escroquerie financière.</summary>
    Scam,
    /// <summary>Usurpation de l'identité d'une marque ou d'une organisation.</summary>
    BrandImpersonation,
    /// <summary>Tentative de vol d'identifiants ou de mots de passe.</summary>
    CredentialTheft,
    /// <summary>Comportement suspect ne correspondant pas à une catégorie précise.</summary>
    Suspicious
}
