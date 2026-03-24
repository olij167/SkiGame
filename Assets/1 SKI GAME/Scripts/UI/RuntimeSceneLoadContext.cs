using System;

public static class RuntimeSceneLoadContext
{
    public static bool IsMenuBackgroundPreview { get; private set; }

    public static event Action MenuBackgroundPreviewStarted;
    public static event Action MenuBackgroundPreviewEnded;

    public static void EnterMenuBackgroundPreview()
    {
        if (IsMenuBackgroundPreview)
            return;

        IsMenuBackgroundPreview = true;
        MenuBackgroundPreviewStarted?.Invoke();
    }

    public static void ExitMenuBackgroundPreview()
    {
        if (!IsMenuBackgroundPreview)
            return;

        IsMenuBackgroundPreview = false;
        MenuBackgroundPreviewEnded?.Invoke();
    }
}