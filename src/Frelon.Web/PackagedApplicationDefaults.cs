namespace Frelon.Web;

/// <summary>Valeurs intégrées uniquement dans la publication destinée au double-clic.</summary>
public static class PackagedApplicationDefaults
{
#if FRELON_PACKAGED_APP
    public const bool OpenBrowser = true;
#else
    public const bool OpenBrowser = false;
#endif
}
