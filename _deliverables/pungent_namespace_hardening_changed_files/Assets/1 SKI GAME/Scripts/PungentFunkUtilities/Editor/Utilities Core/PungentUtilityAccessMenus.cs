namespace PungentFunk.Utilities.Editor.Core
{
    #if UNITY_EDITOR
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Lightweight access points for common PungentFunk utilities.
    /// These route through the central registry so future launcher/tray/overlay systems can share one source of truth.
    /// </summary>
    public static class PungentUtilityAccessMenus
    {
        [MenuItem("Tools/Utilities/Open Control Panel", priority = -201)]
        public static void OpenControlPanelFromTools()
        {
            PungentUtilityControlPanelWindow.Open();
        }

        [MenuItem("Assets/PungentFunk Utilities/Open Control Panel", priority = 0)]
        public static void OpenControlPanelFromAssets()
        {
            PungentUtilityControlPanelWindow.Open();
        }

        [MenuItem("GameObject/PungentFunk Utilities/Open Control Panel", false, 0)]
        public static void OpenControlPanelFromGameObject()
        {
            PungentUtilityControlPanelWindow.Open();
        }

        [MenuItem("Assets/PungentFunk Utilities/Open Font Preview", priority = 20)]
        public static void OpenFontPreviewFromAssets()
        {
            PungentUtilityRegistry.Open("font-preview");
        }

        [MenuItem("Assets/PungentFunk Utilities/Open Prefab Icon Generator", priority = 21)]
        public static void OpenPrefabIconGeneratorFromAssets()
        {
            PungentUtilityRegistry.Open("prefab-icon-generator");
        }

        [MenuItem("Assets/PungentFunk Utilities/Open Prefab Asset Exporter", priority = 22)]
        public static void OpenPrefabAssetExporterFromAssets()
        {
            PungentUtilityRegistry.Open("prefab-asset-exporter");
        }


        [MenuItem("Assets/PungentFunk Utilities/Open Palette Designer", priority = 23)]
        public static void OpenPaletteDesignerFromAssets()
        {
            PungentUtilityRegistry.Open("palette-designer");
        }

        [MenuItem("Assets/PungentFunk Utilities/Open Audio Setup Coverage", priority = 30)]
        public static void OpenAudioSetupCoverageFromAssets()
        {
            PungentUtilityRegistry.Open("audio-setup-coverage");
        }

        [MenuItem("Assets/PungentFunk Utilities/Open Audio Catalog Coverage", priority = 31)]
        public static void OpenAudioCatalogCoverageFromAssets()
        {
            PungentUtilityRegistry.Open("audio-catalog-coverage");
        }

        [MenuItem("GameObject/PungentFunk Utilities/Open Scene Navigation", false, 24)]
        public static void OpenSceneNavigationFromGameObject()
        {
            PungentUtilityRegistry.Open("scene-navigation");
        }

        [MenuItem("GameObject/PungentFunk Utilities/Add Scene Gizmo Source", false, 25)]
        public static void AddSceneGizmoSourceToSelection()
        {
            foreach (GameObject go in Selection.gameObjects)
            {
                if (go == null)
                    continue;

                if (go.GetComponent<PungentSceneGizmoSource>() == null)
                    Undo.AddComponent<PungentSceneGizmoSource>(go);
            }
        }

        [MenuItem("GameObject/PungentFunk Utilities/Open Tooltip Notes", false, 26)]
        public static void OpenTooltipNotesFromGameObject()
        {
            PungentUtilityRegistry.Open("tooltip-notes");
        }

        [MenuItem("GameObject/PungentFunk Utilities/Open Surface Align Tool", false, 20)]
        public static void OpenSurfaceAlignFromGameObject()
        {
            PungentUtilityRegistry.Open("surface-align");
        }

        [MenuItem("GameObject/PungentFunk Utilities/Open Component Tuning Copy", false, 21)]
        public static void OpenComponentTuningCopyFromGameObject()
        {
            PungentUtilityRegistry.Open("component-tuning-copy");
        }

        [MenuItem("GameObject/PungentFunk Utilities/Open Reference Assignment Scanner", false, 22)]
        public static void OpenReferenceAssignmentScannerFromGameObject()
        {
            PungentUtilityRegistry.Open("reference-assignment-scanner");
        }

        [MenuItem("GameObject/PungentFunk Utilities/Open Modular Path Builder", false, 23)]
        public static void OpenModularPathBuilderFromGameObject()
        {
            PungentUtilityRegistry.Open("modular-path-builder");
        }

        [MenuItem("GameObject/PungentFunk Utilities/Open Surface Align Tool", true)]
        private static bool ValidateOpenSurfaceAlignFromGameObject() => Selection.activeGameObject != null;

        [MenuItem("GameObject/PungentFunk Utilities/Open Component Tuning Copy", true)]
        private static bool ValidateOpenComponentTuningCopyFromGameObject() => Selection.activeGameObject != null;

        [MenuItem("GameObject/PungentFunk Utilities/Open Reference Assignment Scanner", true)]
        private static bool ValidateOpenReferenceAssignmentScannerFromGameObject() => Selection.activeGameObject != null;

        [MenuItem("GameObject/PungentFunk Utilities/Open Modular Path Builder", true)]
        private static bool ValidateOpenModularPathBuilderFromGameObject() => Selection.activeGameObject != null;

        [MenuItem("GameObject/PungentFunk Utilities/Open Scene Navigation", true)]
        private static bool ValidateOpenSceneNavigationFromGameObject() => Selection.activeGameObject != null;

        [MenuItem("GameObject/PungentFunk Utilities/Add Scene Gizmo Source", true)]
        private static bool ValidateAddSceneGizmoSourceToSelection() => Selection.activeGameObject != null;

        [MenuItem("GameObject/PungentFunk Utilities/Open Tooltip Notes", true)]
        private static bool ValidateOpenTooltipNotesFromGameObject() => Selection.activeGameObject != null;
    }
    #endif

}