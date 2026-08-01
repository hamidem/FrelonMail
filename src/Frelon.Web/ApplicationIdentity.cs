using System.Reflection;

namespace Frelon.Web;

/// <summary>Identité de distribution intégrée à l'application locale.</summary>
public sealed record ApplicationIdentity
{
    /// <summary>Crée une identité produit explicite.</summary>
    public ApplicationIdentity(string productName, string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productName);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        ProductName = productName.Trim();
        Version = version.Trim();
    }

    /// <summary>Nom public du produit.</summary>
    public string ProductName { get; }

    /// <summary>Version informative affichable, sans métadonnée de compilation.</summary>
    public string Version { get; }

    /// <summary>Lit l'identité réellement intégrée à une assembly publiée.</summary>
    public static ApplicationIdentity FromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var productName = assembly
            .GetCustomAttribute<AssemblyProductAttribute>()?
            .Product;
        if (string.IsNullOrWhiteSpace(productName))
        {
            productName = assembly.GetName().Name ?? "Frelon";
        }

        var version = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (string.IsNullOrWhiteSpace(version))
        {
            version = assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        }

        var metadataSeparator = version.IndexOf('+', StringComparison.Ordinal);
        if (metadataSeparator >= 0)
        {
            version = version[..metadataSeparator];
        }

        return new ApplicationIdentity(productName, version);
    }
}
