namespace Frelon.Core;

/// <summary>
/// Type d'indicateur de compromission (IOC).
/// </summary>
public enum IocType
{
    /// <summary>Type non déterminé.</summary>
    Unknown,
    /// <summary>Adresse IP suspecte.</summary>
    IpAddress,
    /// <summary>Nom de domaine suspect.</summary>
    Domain,
    /// <summary>URL suspecte.</summary>
    Url,
    /// <summary>Adresse email suspecte.</summary>
    Email,
    /// <summary>Empreinte cryptographique (hash) d'un fichier suspect.</summary>
    Hash,
    /// <summary>Nom de fichier suspect.</summary>
    FileName
}
