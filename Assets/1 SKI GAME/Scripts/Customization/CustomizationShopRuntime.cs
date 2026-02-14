using UnityEngine;

public static class CustomizationShopRuntime
{
    public static bool IsOpen { get; private set; }
    public static CustomizationSceneBootstrap ActiveBootstrap { get; private set; }

    public static void Register(CustomizationSceneBootstrap bootstrap)
    {
        ActiveBootstrap = bootstrap;
        IsOpen = bootstrap != null;
    }

    public static void Unregister(CustomizationSceneBootstrap bootstrap)
    {
        if (ActiveBootstrap == bootstrap)
            ActiveBootstrap = null;

        IsOpen = ActiveBootstrap != null;
    }

    public static void RequestExit(bool applyChanges)
    {
        if (ActiveBootstrap != null)
            ActiveBootstrap.RequestExit(applyChanges);
    }
}
