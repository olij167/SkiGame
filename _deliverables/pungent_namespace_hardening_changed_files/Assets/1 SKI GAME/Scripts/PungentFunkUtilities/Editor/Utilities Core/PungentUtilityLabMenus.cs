namespace PungentFunk.Utilities.Editor.Core
{
    #if UNITY_EDITOR
    using UnityEditor;

    /// <summary>
    /// Direct lab entry points for the control panel. Individual tools keep their existing menu items.
    /// Empty future labs are intentionally omitted until they have at least one concrete utility module.
    /// </summary>
    public static class PungentUtilityLabMenus
    {
        [MenuItem("Tools/Utilities/Labs/Utility Core", priority = -110)]
        public static void OpenUtilityCore() => PungentUtilityControlPanelWindow.OpenLab(PungentUtilityLabs.UtilityCore);

        [MenuItem("Tools/Utilities/Labs/Debug Lab", priority = -100)]
        public static void OpenDebugLab() => PungentUtilityControlPanelWindow.OpenLab(PungentUtilityLabs.DebugLab);

        [MenuItem("Tools/Utilities/Labs/Asset Placement Lab", priority = -90)]
        public static void OpenAssetPlacementLab() => PungentUtilityControlPanelWindow.OpenLab(PungentUtilityLabs.AssetPlacementLab);

        [MenuItem("Tools/Utilities/Labs/Scene Workflow Lab", priority = -80)]
        public static void OpenSceneWorkflowLab() => PungentUtilityControlPanelWindow.OpenLab(PungentUtilityLabs.SceneWorkflowLab);

        [MenuItem("Tools/Utilities/Labs/Colour Lab", priority = -70)]
        public static void OpenColourLab() => PungentUtilityControlPanelWindow.OpenLab(PungentUtilityLabs.ColourLab);

        [MenuItem("Tools/Utilities/Labs/Texture Lab", priority = -60)]
        public static void OpenTextureLab() => PungentUtilityControlPanelWindow.OpenLab(PungentUtilityLabs.TextureLab);

        [MenuItem("Tools/Utilities/Labs/Audio Lab", priority = -50)]
        public static void OpenAudioLab() => PungentUtilityControlPanelWindow.OpenLab(PungentUtilityLabs.AudioLab);

        [MenuItem("Tools/Utilities/Labs/Asset Preview and Export Lab", priority = -40)]
        public static void OpenAssetPreviewExportLab() => PungentUtilityControlPanelWindow.OpenLab(PungentUtilityLabs.AssetPreviewExportLab);

        [MenuItem("Tools/Utilities/Labs/Project Audit and Authoring Lab", priority = -30)]
        public static void OpenProjectAuditAuthoringLab() => PungentUtilityControlPanelWindow.OpenLab(PungentUtilityLabs.ProjectAuditAuthoringLab);

        [MenuItem("Tools/Utilities/Labs/Content Generation Lab", priority = -20)]
        public static void OpenContentGenerationLab() => PungentUtilityControlPanelWindow.OpenLab(PungentUtilityLabs.ContentGenerationLab);

        [MenuItem("Tools/Utilities/Labs/UI and Feedback Lab", priority = -10)]
        public static void OpenUIFeedbackLab() => PungentUtilityControlPanelWindow.OpenLab(PungentUtilityLabs.UIFeedbackLab);
    }
    #endif

}