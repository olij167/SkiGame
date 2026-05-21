namespace PungentFunk.Utilities.Editor.Core
{
    #if UNITY_EDITOR
    using PungentFunk.Utilities.Editor.SceneTools;
    using PungentFunk.Utilities.SceneTools;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Context-sensitive access points for common PungentFunk utilities.
    /// Direct Tools/PungentFunk shortcut ownership lives in PungentUtilityToolMenus.
    /// </summary>
    public static class PungentUtilityAccessMenus
    {
        [MenuItem("Assets/PungentFunk Utilities/Open Utilities Browser", priority = 0)]
        public static void OpenControlPanelFromAssets()
        {
            PungentUtilityControlPanelWindow.Open();
        }

        [MenuItem("GameObject/PungentFunk Utilities/Open Utilities Browser", false, 0)]
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
            if (!PungentUtilityRegistry.CanOpen("add-scene-gizmo-source", true))
                return;

            foreach (GameObject go in Selection.gameObjects)
            {
                if (go == null)
                    continue;

                if (go.GetComponent<PungentSceneGizmoSource>() == null)
                    Undo.AddComponent<PungentSceneGizmoSource>(go);
            }
        }

        [MenuItem("GameObject/PungentFunk Utilities/Scene Tools/Add Scene Gizmo Source", false, 25)]
        public static void AddSceneGizmoSourceToSelectionAlias()
        {
            AddSceneGizmoSourceToSelection();
        }

        [MenuItem("GameObject/PungentFunk Utilities/Scene Tools/Add Scene Beacon", false, 26)]
        public static void AddSceneBeaconToSelection()
        {
            if (PungentUtilityRegistry.CanOpen("add-scene-beacon", true))
                PungentSceneGizmoPresetActions.AddBeaconToSelection(out _);
        }

        [MenuItem("GameObject/PungentFunk Utilities/Scene Tools/Add Collision Sensor", false, 27)]
        public static void AddCollisionSensorToSelection()
        {
            if (PungentUtilityRegistry.CanOpen("add-collision-sensor-gizmo", true))
                PungentSceneGizmoPresetActions.AddCollisionSensorToSelection(out _);
        }

        [MenuItem("GameObject/PungentFunk Utilities/Scene Tools/Add Trigger Sensor", false, 28)]
        public static void AddTriggerSensorToSelection()
        {
            if (PungentUtilityRegistry.CanOpen("add-trigger-sensor-gizmo", true))
                PungentSceneGizmoPresetActions.AddTriggerSensorToSelection(out _);
        }

        [MenuItem("GameObject/PungentFunk Utilities/Scene Tools/Add Trajectory Visualizer", false, 29)]
        public static void AddTrajectoryVisualizerToSelection()
        {
            if (PungentUtilityRegistry.CanOpen("add-trajectory-visualizer", true))
                PungentSceneGizmoPresetActions.AddTrajectoryVisualizerToSelection(out _);
        }

        [MenuItem("GameObject/PungentFunk Utilities/Open Scene Gizmo Browser", false, 25)]
        public static void OpenSceneGizmoBrowserFromGameObject()
        {
            PungentUtilityRegistry.Open("scene-gizmo-browser");
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

        [MenuItem("GameObject/PungentFunk Utilities/Open Surface Align Tool", true)]
        private static bool ValidateOpenSurfaceAlignFromGameObject() => Selection.activeGameObject != null;

        [MenuItem("GameObject/PungentFunk Utilities/Open Component Tuning Copy", true)]
        private static bool ValidateOpenComponentTuningCopyFromGameObject() => Selection.activeGameObject != null;

        [MenuItem("GameObject/PungentFunk Utilities/Open Reference Assignment Scanner", true)]
        private static bool ValidateOpenReferenceAssignmentScannerFromGameObject() => Selection.activeGameObject != null;

        [MenuItem("GameObject/PungentFunk Utilities/Open Scene Navigation", true)]
        private static bool ValidateOpenSceneNavigationFromGameObject() => Selection.activeGameObject != null;

        [MenuItem("GameObject/PungentFunk Utilities/Add Scene Gizmo Source", true)]
        private static bool ValidateAddSceneGizmoSourceToSelection() => Selection.activeGameObject != null;

        [MenuItem("GameObject/PungentFunk Utilities/Scene Tools/Add Scene Gizmo Source", true)]
        private static bool ValidateAddSceneGizmoSourceToSelectionAlias() => Selection.activeGameObject != null;

        [MenuItem("GameObject/PungentFunk Utilities/Scene Tools/Add Scene Beacon", true)]
        private static bool ValidateAddSceneBeaconToSelection() => Selection.activeGameObject != null;

        [MenuItem("GameObject/PungentFunk Utilities/Scene Tools/Add Collision Sensor", true)]
        private static bool ValidateAddCollisionSensorToSelection() => Selection.activeGameObject != null;

        [MenuItem("GameObject/PungentFunk Utilities/Scene Tools/Add Trigger Sensor", true)]
        private static bool ValidateAddTriggerSensorToSelection() => Selection.activeGameObject != null;

        [MenuItem("GameObject/PungentFunk Utilities/Scene Tools/Add Trajectory Visualizer", true)]
        private static bool ValidateAddTrajectoryVisualizerToSelection() => Selection.activeGameObject != null;

        [MenuItem("GameObject/PungentFunk Utilities/Open Scene Gizmo Browser", true)]
        private static bool ValidateOpenSceneGizmoBrowserFromGameObject() => Selection.activeGameObject != null;

        [MenuItem("GameObject/PungentFunk Utilities/Open Tooltip Notes", true)]
        private static bool ValidateOpenTooltipNotesFromGameObject() => Selection.activeGameObject != null;
    }
    #endif

}
