using System;

public static class CustomizationShopRuntime
{
    public static bool IsOpen { get; private set; }
    public static CustomizationSceneBootstrap ActiveBootstrap { get; private set; }

    /// <summary>Fires whenever the shop open state changes. Arg = isOpen.</summary>
    public static event Action<bool> OnOpenChanged;

    public static void Register(CustomizationSceneBootstrap bootstrap)
    {
        ActiveBootstrap = bootstrap;
        SetOpenState(bootstrap != null);
    }

    public static void Unregister(CustomizationSceneBootstrap bootstrap)
    {
        if (ActiveBootstrap == bootstrap)
            ActiveBootstrap = null;

        SetOpenState(ActiveBootstrap != null);
    }

    public static void RequestExit(bool applyChanges)
    {
        if (ActiveBootstrap != null)
            ActiveBootstrap.RequestExit(applyChanges);
    }

    private static void SetOpenState(bool open)
    {
        if (IsOpen == open) return;
        IsOpen = open;
        OnOpenChanged?.Invoke(IsOpen);
    }
}
