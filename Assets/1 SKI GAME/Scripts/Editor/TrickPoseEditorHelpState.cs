using UnityEditor;

public static class TrickPoseEditorHelpState
{
    private const string Prefix = "TrickPose.Help.";

    public static bool ShowInlineHelp
    {
        get => EditorPrefs.GetBool(Prefix + "ShowInlineHelp", true);
        set => EditorPrefs.SetBool(Prefix + "ShowInlineHelp", value);
    }

    public static bool ShowAdvancedHelp
    {
        get => EditorPrefs.GetBool(Prefix + "ShowAdvancedHelp", false);
        set => EditorPrefs.SetBool(Prefix + "ShowAdvancedHelp", value);
    }

    public static bool ShowFirstTimeBanner
    {
        get => EditorPrefs.GetBool(Prefix + "ShowFirstTimeBanner", true);
        set => EditorPrefs.SetBool(Prefix + "ShowFirstTimeBanner", value);
    }

    public static bool CoverageGettingStartedDismissed
    {
        get => EditorPrefs.GetBool(Prefix + "CoverageGettingStartedDismissed", false);
        set => EditorPrefs.SetBool(Prefix + "CoverageGettingStartedDismissed", value);
    }

    public static bool PreviewGettingStartedDismissed
    {
        get => EditorPrefs.GetBool(Prefix + "PreviewGettingStartedDismissed", false);
        set => EditorPrefs.SetBool(Prefix + "PreviewGettingStartedDismissed", value);
    }

    public static bool InspectorHelpExpanded
    {
        get => EditorPrefs.GetBool(Prefix + "InspectorHelpExpanded", true);
        set => EditorPrefs.SetBool(Prefix + "InspectorHelpExpanded", value);
    }

    public static bool GlossaryExpanded
    {
        get => EditorPrefs.GetBool(Prefix + "GlossaryExpanded", false);
        set => EditorPrefs.SetBool(Prefix + "GlossaryExpanded", value);
    }

    public static void ResetAll()
    {
        ShowInlineHelp = true;
        ShowAdvancedHelp = false;
        ShowFirstTimeBanner = true;
        CoverageGettingStartedDismissed = false;
        PreviewGettingStartedDismissed = false;
        InspectorHelpExpanded = true;
        GlossaryExpanded = false;
    }
}
