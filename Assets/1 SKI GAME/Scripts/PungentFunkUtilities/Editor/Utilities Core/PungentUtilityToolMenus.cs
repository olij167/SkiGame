using PungentFunk.Utilities.Editor.PreviewExport;
using PungentFunk.Utilities.Editor.SceneTools;
using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.UI;
using PungentFunk.Utilities.Editor.Core.Help;
using PungentFunk.Utilities.Editor.Checklists;
using PungentFunk.Utilities.Editor.DataSheets;
using PungentFunk.Utilities.Editor.ProjectAudit;

namespace PungentFunk.Utilities.Editor.Core
{
#if UNITY_EDITOR
    using UnityEditor;
    using UnityEditor.ShortcutManagement;

    /// <summary>
    /// Central owner for direct Tools/PungentFunk shortcut menu entries.
    /// Utility implementations keep their Open methods; this class owns the menu surface.
    /// </summary>
    public static class PungentUtilityToolMenus
    {
        [MenuItem(PungentUtilityMenuPaths.OpenUtilitiesBrowser, priority = -200)]
        public static void OpenUtilitiesBrowser() => PungentUtilityControlPanelWindow.Open();

        [MenuItem(PungentUtilityMenuPaths.DesignValidationAudit, priority = 0)]
        public static void OpenDesignValidationAudit() => PungentUtilityRegistry.Open("design-validation-audit");

        [MenuItem(PungentUtilityMenuPaths.HelpBrowser, priority = 5)]
        public static void OpenHelpBrowser() => PungentUtilityRegistry.Open("help-browser");

        public static void OpenDeveloperTools() => PungentUtilityDeveloperToolsWindow.Open();

        [MenuItem(PungentUtilityMenuPaths.UtilityTray, priority = 10)]
        public static void OpenUtilityTray() => PungentUtilityMinimizer.ToggleOverlayTrayFromAccess(new UnityEngine.Rect());

        [MenuItem(PungentUtilityMenuPaths.CreateSupportRequest, priority = 11)]
        public static void CreateSupportRequest()
        {
            PungentSupportRequestBridge.OpenFromBugReportContext(new PungentBugReportContext
            {
                contextLabel = "Global support request",
                contextPath = "Tools > PungentFunk > Support",
                sourceWindow = "Tools Menu"
            });
        }

        [MenuItem(PungentUtilityMenuPaths.OpenSupportRequests, priority = 12)]
        public static void OpenSupportRequests() => PungentSupportRequestBridge.OpenRequests();

        [MenuItem(PungentUtilityMenuPaths.ChecklistUtility, priority = 14)]
        public static void OpenChecklistUtility() => PungentChecklistUtilityWindow.Open();

        [MenuItem(PungentUtilityMenuPaths.LegacyChecklistUtility, priority = 15)]
        public static void OpenLegacyChecklistUtility() => PungentChecklistUtilityWindow.Open();

        [MenuItem(PungentUtilityMenuPaths.ParkFocusedUtility, priority = 1)]
        public static void MinimizeFocusedEditorWindow() => PungentUtilityMinimizer.MinimizeLastEditorWindowFromCommand();

        [MenuItem(PungentUtilityMenuPaths.LegacyParkFocusedUtility, priority = 1)]
        public static void LegacyMinimizeFocusedEditorWindow() => PungentUtilityMinimizer.MinimizeLastEditorWindowFromCommand();

        [Shortcut("PungentFunk Utilities/Minimize Focused Editor Window")]
        public static void MinimizeFocusedEditorWindowShortcut() => PungentUtilityMinimizer.MinimizeCurrentEditorWindowFromShortcut();

        public static void MinimizeFocusedUtility() => PungentUtilityMinimizer.MinimizeCurrentEditorWindowFromShortcut();

        public static void ParkFocusedUtility() => PungentUtilityMinimizer.MinimizeCurrentEditorWindowFromShortcut();

        [MenuItem(PungentUtilityMenuPaths.UtilityWindowTheme, priority = 20)]
        public static void OpenUtilityWindowTheme() => PungentUtilityRegistry.Open("theme-customizer");

        [MenuItem(PungentUtilityMenuPaths.EditorStyleExplorer, priority = 21)]
        public static void OpenEditorStyleExplorer() => PungentUtilityRegistry.Open("editor-style-explorer");

        [MenuItem(PungentUtilityMenuPaths.DebugControlCenter, priority = 100)]
        public static void OpenDebugControlCenter() => PungentUtilityRegistry.Open("debug-control");

        [MenuItem(PungentUtilityMenuPaths.AssetPlacementLab, priority = 200)]
        public static void OpenAssetPlacementLab() => PungentUtilityRegistry.Open("asset-placement-lab");

        [MenuItem(PungentUtilityMenuPaths.SurfaceAlignTool, priority = 210)]
        public static void OpenSurfaceAlignTool() => PungentUtilityRegistry.Open("surface-align");

        [MenuItem(PungentUtilityMenuPaths.SceneNavigation, priority = 300)]
        public static void OpenSceneNavigation() => PungentUtilityRegistry.Open("scene-navigation");

        [MenuItem(PungentUtilityMenuPaths.SceneGizmoBrowser, priority = 310)]
        public static void OpenSceneGizmoBrowser() => PungentUtilityRegistry.Open("scene-gizmo-browser");

        [MenuItem("Tools/PungentFunk Utilities/Scene Tools/Scene Gizmos", priority = 310)]
        public static void OpenSceneGizmoBrowserAlias() => PungentUtilityRegistry.Open("scene-gizmo-browser");

        [MenuItem("Tools/PungentFunk Utilities/Scene Tools/Add Scene Beacon To Selection", priority = 311)]
        public static void AddSceneBeaconToSelection()
        {
            if (PungentUtilityRegistry.CanOpen("add-scene-beacon", true))
                PungentSceneGizmoPresetActions.AddBeaconToSelection(out _);
        }

        [MenuItem("Tools/PungentFunk Utilities/Scene Tools/Add Collision Sensor To Selection", priority = 312)]
        public static void AddCollisionSensorToSelection()
        {
            if (PungentUtilityRegistry.CanOpen("add-collision-sensor-gizmo", true))
                PungentSceneGizmoPresetActions.AddCollisionSensorToSelection(out _);
        }

        [MenuItem("Tools/PungentFunk Utilities/Scene Tools/Add Trigger Sensor To Selection", priority = 313)]
        public static void AddTriggerSensorToSelection()
        {
            if (PungentUtilityRegistry.CanOpen("add-trigger-sensor-gizmo", true))
                PungentSceneGizmoPresetActions.AddTriggerSensorToSelection(out _);
        }

        [MenuItem("Tools/PungentFunk Utilities/Scene Tools/Add Trajectory Visualizer To Selection", priority = 314)]
        public static void AddTrajectoryVisualizerToSelection()
        {
            if (PungentUtilityRegistry.CanOpen("add-trajectory-visualizer", true))
                PungentSceneGizmoPresetActions.AddTrajectoryVisualizerToSelection(out _);
        }

        [MenuItem(PungentUtilityMenuPaths.SpatialAuthoringWorkbench, priority = 320)]
        public static void OpenSpatialAuthoringWorkbench() => PungentUtilityRegistry.Open("spatial-authoring-workbench");

        [MenuItem(PungentUtilityMenuPaths.LegacyPathAuthoringToolkit, priority = 321)]
        public static void OpenLegacyPathAuthoringToolkit() => PungentUtilityRegistry.Open("path-authoring-toolkit");

        [MenuItem(PungentUtilityMenuPaths.PaletteDesigner, priority = 400)]
        public static void OpenPaletteDesigner() => PungentUtilityRegistry.Open("palette-designer");

        [MenuItem(PungentUtilityMenuPaths.ProceduralTextureLab, priority = 500)]
        public static void OpenProceduralTextureLab() => PungentUtilityRegistry.Open("procedural-texture-lab");

        [MenuItem(PungentUtilityMenuPaths.TextureArrayBaker, priority = 510)]
        public static void OpenTextureArrayBaker() => PungentUtilityRegistry.Open("texture-array-baker");

        [MenuItem(PungentUtilityMenuPaths.AudioSetupCoverage, priority = 600)]
        public static void OpenAudioSetupCoverage() => PungentUtilityRegistry.Open("audio-setup-coverage");

        [MenuItem(PungentUtilityMenuPaths.AudioCatalogCoverage, priority = 610)]
        public static void OpenAudioCatalogCoverage() => PungentUtilityRegistry.Open("audio-catalog-coverage");

        [MenuItem(PungentUtilityMenuPaths.FontPreview, priority = 700)]
        public static void OpenFontPreview() => PungentUtilityRegistry.Open("font-preview");

        [MenuItem(PungentUtilityMenuPaths.PrefabIconGenerator, priority = 710)]
        public static void OpenPrefabIconGenerator() => PungentUtilityRegistry.Open("prefab-icon-generator");

        [MenuItem(PungentUtilityMenuPaths.PrefabAssetExporter, priority = 720)]
        public static void OpenPrefabAssetExporter() => PungentUtilityRegistry.Open("prefab-asset-exporter");

        [MenuItem(PungentUtilityMenuPaths.SaveSelectedPrefabWithMeshAssets, priority = 730)]
        public static void SaveSelectedPrefabWithMeshAssets()
        {
            if (PungentUtilityRegistry.CanOpen("save-selected-prefab-with-mesh-assets", true))
                PrefabAssetExporter.SaveSelectedAsPrefabWithMeshAssets();
        }

        [MenuItem(PungentUtilityMenuPaths.SaveSelectedPrefabWithMeshAssets, true)]
        public static bool ValidateSaveSelectedPrefabWithMeshAssets() => PrefabAssetExporter.HasSelectedRoot;

        [MenuItem(PungentUtilityMenuPaths.ComponentTuningCopy, priority = 800)]
        public static void OpenComponentTuningCopy() => PungentUtilityRegistry.Open("component-tuning-copy");

        [MenuItem(PungentUtilityMenuPaths.ReferenceAssignmentScanner, priority = 810)]
        public static void OpenReferenceAssignmentScanner() => PungentUtilityRegistry.Open("reference-assignment-scanner");

        [MenuItem(PungentUtilityMenuPaths.TerrainUsageScanner, priority = 820)]
        public static void OpenTerrainUsageScanner() => PungentUtilityRegistry.Open("terrain-usage-scanner");

        [MenuItem(PungentUtilityMenuPaths.SceneIssueScanner, priority = 825)]
        public static void OpenSceneIssueScanner() => PungentUtilityRegistry.Open("scene-issue-scanner");

        [MenuItem(PungentUtilityMenuPaths.BulkRename, priority = 830)]
        public static void OpenBulkRename() => PungentUtilityRegistry.Open("bulk-rename");

        [MenuItem(PungentUtilityMenuPaths.TooltipNotesBrowser, priority = 840)]
        public static void OpenTooltipNotesBrowser() => PungentUtilityRegistry.Open("tooltip-notes");

        [MenuItem(PungentUtilityMenuPaths.LegacyNotesRoadmapBrowser, priority = 841)]
        public static void OpenLegacyNotesRoadmapBrowser() => PungentUtilityRegistry.Open("tooltip-notes");

        [MenuItem(PungentUtilityMenuPaths.LegacyTooltipNotesBrowser, priority = 842)]
        public static void OpenLegacyTooltipNotesBrowser() => PungentUtilityRegistry.Open("tooltip-notes");

        [MenuItem(PungentUtilityMenuPaths.CoverageMatrix, priority = 850)]
        public static void OpenCoverageMatrix() => PungentUtilityRegistry.Open("coverage-matrix");

        [MenuItem(PungentUtilityMenuPaths.DataSheetEditor, priority = 855)]
        public static void OpenDataSheetEditor()
        {
            PungentDataSheetProviderRegistration.RegisterProvider();
            PungentUtilityRegistry.Open("data-sheet-editor");
        }

        public static void OpenDataSheetQaChecklist() => PungentChecklistUtilityWindow.OpenDataSheetCurrentQa();

        [MenuItem(PungentUtilityMenuPaths.TokenValidator, priority = 860)]
        public static void OpenTokenValidator() => PungentUtilityRegistry.Open("token-validator");

        [MenuItem(PungentUtilityMenuPaths.NameGenerator, priority = 900)]
        public static void OpenNameGenerator() => PungentUtilityRegistry.Open("name-generator");

        [MenuItem(PungentUtilityMenuPaths.PopulateInputPromptIconLibrary, priority = 1000)]
        public static void OpenInputPromptIconLibraryPopulator() => PungentUtilityRegistry.Open("input-prompt-icon-library");

        [MenuItem(PungentUtilityMenuPaths.PopulateSelectedInputPromptLibrary, priority = 1001)]
        public static void PopulateSelectedInputPromptLibrary()
        {
            if (PungentUtilityRegistry.CanOpen("populate-selected-input-prompt-library", true))
                InputPromptIconLibraryAutoFill.PopulateSelectedLibrary();
        }

        [MenuItem(PungentUtilityMenuPaths.PopulateSelectedInputPromptLibrary, true)]
        public static bool ValidatePopulateSelectedInputPromptLibrary() => InputPromptIconLibraryAutoFill.CanPopulateSelectedLibrary();

        [MenuItem(PungentUtilityMenuPaths.PopulateDefaultInputPromptLibrary, priority = 1002)]
        public static void PopulateDefaultInputPromptLibrary()
        {
            if (PungentUtilityRegistry.CanOpen("populate-default-input-prompt-library", true))
                InputPromptIconLibraryAutoFill.PopulateDefaultLibrary();
        }
    }
#endif
}
