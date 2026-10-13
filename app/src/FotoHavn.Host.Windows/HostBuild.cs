namespace FotoHavn.Host.Windows;

/// <summary>What kind of build this is. Field builds are Release builds without <c>E2E_HOOKS</c>.</summary>
internal static class HostBuild
{
#if E2E_HOOKS
    /// <summary>Whether the E2E test hooks are compiled in (<c>-p:FotoHavnE2E=true</c>).</summary>
    public static bool IsE2E => true;
#else
    public static bool IsE2E => false;
#endif

#if DEBUG
    public static bool IsDebug => true;
#else
    public static bool IsDebug => false;
#endif

    /// <summary>DevTools open in E2E and Debug builds only; field builds disable them.</summary>
    public static bool AllowsDevTools => IsE2E || IsDebug;

    /// <summary>The Guest window stays topmost in field builds; E2E builds leave it in the normal z-order.</summary>
    public static bool GuestWindowTopmost => !IsE2E;
}
