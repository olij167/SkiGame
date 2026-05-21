using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Audio;
using PungentFunk.Utilities.Editor.Checklists;
using PungentFunk.Utilities.Editor.Colour;
using PungentFunk.Utilities.Editor.Content;
using PungentFunk.Utilities.Editor.DataSheets;
using PungentFunk.Utilities.Editor.Debugging;
using PungentFunk.Utilities.Editor.Generation;
using PungentFunk.Utilities.Editor.Placement;
using PungentFunk.Utilities.Editor.PreviewExport;
using PungentFunk.Utilities.Editor.ProjectAudit;
using PungentFunk.Utilities.Editor.SceneTools;
using PungentFunk.Utilities.Editor.UI;
using PungentFunk.Utilities.Editor.Core.Help;
using PungentFunk.Utilities.Editor.EnvironmentSimulation;

namespace PungentFunk.Utilities.Editor.Core
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Central registry for reusable PungentFunk utilities.
    /// The registry owns discovery metadata only. Unity Tools menu attributes should remain in menu-routing scripts.
    /// </summary>
    public static class PungentUtilityRegistry
    {
        private static readonly List<PungentUtilityDescriptor> Utilities = new List<PungentUtilityDescriptor>();
        private static bool _initialized;

        public static event Action Changed;

        public static IReadOnlyList<PungentUtilityDescriptor> All
        {
            get
            {
                EnsureInitialized();
                return Utilities;
            }
        }

        public static IEnumerable<PungentUtilityDescriptor> BrowserUtilities
        {
            get
            {
                EnsureInitialized();
                return Utilities.Where(u => u != null && u.IsVisibleInDefaultBrowser);
            }
        }

        public static void Register(PungentUtilityDescriptor descriptor)
        {
            if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.Id))
                return;

            EnsureInitialized();
            RegisterInternal(descriptor);
            NotifyChanged();
        }

        public static void RegisterRange(IEnumerable<PungentUtilityDescriptor> descriptors)
        {
            if (descriptors == null)
                return;

            EnsureInitialized();

            bool changed = false;
            foreach (PungentUtilityDescriptor descriptor in descriptors)
            {
                if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.Id))
                    continue;

                RegisterInternal(descriptor);
                changed = true;
            }

            if (changed)
                NotifyChanged();
        }

        public static PungentUtilityDescriptor Find(string id)
        {
            EnsureInitialized();
            return Utilities.FirstOrDefault(u => string.Equals(u.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        public static PungentUtilityDescriptor FindByWindowType(System.Type windowType)
        {
            EnsureInitialized();

            if (windowType == null)
                return null;

            string fullName = windowType.FullName;
            return Utilities.FirstOrDefault(u => string.Equals(u.WindowTypeName, fullName, StringComparison.OrdinalIgnoreCase));
        }

        public static PungentUtilityDescriptor FindForWindow(EditorWindow window)
        {
            return window == null ? null : FindByWindowType(window.GetType());
        }

        public static bool Open(string id)
        {
            PungentUtilityDescriptor descriptor = Find(id);
            if (descriptor == null)
                return false;

            if (!descriptor.CanRun)
            {
                descriptor.Open();
                return false;
            }

            descriptor.Open();
            PungentUtilityControlPanelWindow.RecordRecentUtility(id);
            PungentUtilityMinimizer.PruneOpenedUtility(id);
            return true;
        }

        public static bool CanOpen(string id, bool showDialog = false)
        {
            PungentUtilityDescriptor descriptor = Find(id);
            if (descriptor == null)
            {
                if (showDialog)
                    EditorUtility.DisplayDialog("PungentFunk Utilities", "Utility is not registered: " + id, "OK");
                return false;
            }

            if (descriptor.CanRun)
                return true;

            if (showDialog)
                EditorUtility.DisplayDialog("PungentFunk Utilities", descriptor.DisabledReason, "OK");
            return false;
        }

        public static string[] UtilityCategories(bool includeEmptyKnownCategories = false, bool includeHidden = false)
        {
            EnsureInitialized();
            IEnumerable<string> categories = GetQueryableSource(includeHidden).SelectMany(u => u.GetAllCategoryIds());
            if (includeEmptyKnownCategories)
                categories = categories.Concat(PungentUtilityCategories.CurrentCategories);

            return categories
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(PungentUtilityCategories.SortKey)
                .ThenBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static string[] Types(string areaCategory = null, bool includeHidden = false)
        {
            EnsureInitialized();
            return GetQueryableSource(includeHidden)
                .Where(u => string.IsNullOrEmpty(areaCategory) || u.HasCategory(areaCategory))
                .Select(GetUtilityType)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static string[] Statuses(string areaCategory = null, bool includeHidden = false)
        {
            EnsureInitialized();
            return GetQueryableSource(includeHidden)
                .Where(u => string.IsNullOrEmpty(areaCategory) || u.HasCategory(areaCategory))
                .Select(GetStatus)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(PungentUtilityPackageStatus.SortKey)
                .ThenBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static IEnumerable<PungentUtilityDescriptor> Query(string areaCategory = null, string utilityType = null, string search = null, string status = null, bool includeHidden = false)
        {
            EnsureInitialized();
            IEnumerable<PungentUtilityDescriptor> source = GetQueryableSource(includeHidden);
            return source
                .Where(u => string.IsNullOrEmpty(areaCategory) || u.HasCategory(areaCategory))
                .Where(u => string.IsNullOrEmpty(utilityType) || string.Equals(GetUtilityType(u), utilityType, StringComparison.OrdinalIgnoreCase))
                .Where(u => string.IsNullOrEmpty(status) || string.Equals(GetStatus(u), status, StringComparison.OrdinalIgnoreCase))
                .Where(u => u.Matches(search));
        }

        private static IEnumerable<PungentUtilityDescriptor> GetQueryableSource(bool includeHidden)
        {
            return includeHidden ? Utilities.Where(u => u != null) : BrowserUtilities;
        }

        // Backwards-compatible aliases for older callers. Product-facing UI should use UtilityCategories/Types.
        public static string[] Labs(bool includeEmptyKnownLabs = false) => UtilityCategories(includeEmptyKnownLabs);
        public static string[] Categories(string areaCategory = null) => Types(areaCategory);
        public static string[] Modules(string areaCategory = null) => Types(areaCategory);

        public static IEnumerable<PungentUtilityDescriptor> RelatedUtilities(PungentUtilityDescriptor descriptor)
        {
            EnsureInitialized();
            if (descriptor == null || descriptor.RelatedUtilityIds == null)
                yield break;

            for (int i = 0; i < descriptor.RelatedUtilityIds.Length; i++)
            {
                PungentUtilityDescriptor related = Find(descriptor.RelatedUtilityIds[i]);
                if (related != null && related.IsVisibleInDefaultBrowser)
                    yield return related;
            }
        }

        public static string GetAreaCategory(PungentUtilityDescriptor descriptor)
        {
            return descriptor == null ? PungentUtilityCategories.Other : PungentUtilityCategories.Normalize(descriptor.AreaCategory);
        }

        public static string GetUtilityType(PungentUtilityDescriptor descriptor)
        {
            return descriptor == null || string.IsNullOrWhiteSpace(descriptor.UtilityType) ? "Other" : descriptor.UtilityType.Trim();
        }

        public static string GetModule(PungentUtilityDescriptor descriptor)
        {
            if (descriptor == null)
                return PungentUtilityCategories.Other;

            if (!string.IsNullOrWhiteSpace(descriptor.Module))
                return descriptor.Module;

            return GetUtilityType(descriptor);
        }

        public static string GetStatus(PungentUtilityDescriptor descriptor)
        {
            return descriptor == null ? PungentUtilityPackageStatus.Experimental : PungentUtilityPackageStatus.Normalize(descriptor.PackageStatus);
        }

        // Backwards-compatible names. Their return values now represent product-facing Category and Type.
        public static string GetLab(PungentUtilityDescriptor descriptor) => GetAreaCategory(descriptor);
        public static string GetCategory(PungentUtilityDescriptor descriptor) => GetUtilityType(descriptor);

        public static int CountByCategory(string areaCategory, bool includeHidden = false)
        {
            EnsureInitialized();
            return GetQueryableSource(includeHidden).Count(u => u.HasCategory(areaCategory));
        }

        public static int CountByLab(string lab) => CountByCategory(lab);

        public static bool HasMetadataOverride(string utilityId)
        {
            return PungentUtilityMetadataOverrides.instance.HasOverride(utilityId);
        }

        public static PungentUtilityMetadataOverrides.UtilityMetadataOverride CreateEditableMetadataOverride(string utilityId)
        {
            EnsureInitialized();
            return PungentUtilityMetadataOverrides.instance.CreateEditableCopy(Find(utilityId));
        }

        public static void SaveMetadataOverride(PungentUtilityMetadataOverrides.UtilityMetadataOverride metadataOverride)
        {
            if (metadataOverride == null || string.IsNullOrWhiteSpace(metadataOverride.utilityId))
                return;

            PungentUtilityMetadataOverrides.instance.SetOverride(metadataOverride);
            Rebuild();
        }

        public static void ClearMetadataOverride(string utilityId)
        {
            if (string.IsNullOrWhiteSpace(utilityId))
                return;

            PungentUtilityMetadataOverrides.instance.RemoveOverride(utilityId);
            Rebuild();
        }

        public static void Rebuild()
        {
            _initialized = false;
            Utilities.Clear();
            EnsureInitialized();
            NotifyChanged();
        }

        private static void NotifyChanged()
        {
            Changed?.Invoke();
        }

        private static void EnsureInitialized()
        {
            if (_initialized)
                return;

            _initialized = true;
            RegisterDefaults();
        }

        private static void RegisterDefaults()
        {
            Utilities.Clear();

            Add<PungentUtilityControlPanelWindow>("utilities-browser", "Utilities Browser", "Browser", PungentUtilityCategories.Core, "Dashboard", "Browse, search, filter, and launch registered PungentFunk utilities.", PungentUtilityMenuPaths.OpenUtilitiesBrowser, new[] { "utilities", "browser", "control panel", "dashboard", "launcher", "registry" }, 0, isLabHub: true, packageStatus: PungentUtilityPackageStatus.Stable, categoryFacets: new[] { PungentUtilityCategories.Core }, showInUtilitiesBrowser: false);
            Add<UtilityWindowThemeCustomizer>("theme-customizer", "Utility Window Theme", "Theme", PungentUtilityCategories.AppearanceStyling, "Appearance", "Browse presets, tune typography and panels, and optionally generate experimental Unity editor USS skin files.", PungentUtilityMenuPaths.UtilityWindowTheme, new[] { "theme", "appearance", "style", "skin" }, 10, isLabHub: true, packageStatus: PungentUtilityPackageStatus.Stable, /*relatedUtilityIds: new[] { "palette-designer" },*/ categoryFacets: new[] { PungentUtilityCategories.Core, PungentUtilityCategories.AppearanceStyling });

            // Internal/development-only diagnostic. Keep available by direct menu/ID during development, but hide from the shipped Utilities Browser.
            Add<PungentUtilityDesignAuditWindow>("design-validation-audit", "Project Audit", "Audit", PungentUtilityCategories.Core, "Internal", "Validate registered utilities, window layout signals, menu taxonomy, CreateAssetMenu roots, namespaces, and asmdef readiness against the design bible.", PungentUtilityMenuPaths.DesignValidationAudit, new[] { "audit", "architecture", "layout", "validation", "package", "design bible", "internal" }, 15, isLabHub: false, packageStatus: PungentUtilityPackageStatus.InProgress, /*relatedUtilityIds: new[] { "theme-customizer", "asset-placement-lab", "reference-assignment-scanner" },*/ categoryFacets: new[] { PungentUtilityCategories.Core, PungentUtilityCategories.Audit, PungentUtilityCategories.DocumentationPlanning }, showInUtilitiesBrowser: true);
            Add<PungentUtilityHelpBrowserWindow>("help-browser", "Help Browser", "Documentation", PungentUtilityCategories.Core, "Documentation", "Browse utility guides, curated feature notes, generated scripting entries, troubleshooting, and related resources.", PungentUtilityMenuPaths.HelpBrowser, new[] { "help", "docs", "guide", "scripting", "reference", "support" }, 16, isLabHub: false, packageStatus: PungentUtilityPackageStatus.Stable, relatedUtilityIds: new[] { "utilities-browser", "documentation-links", "debug-control" }, categoryFacets: new[] { PungentUtilityCategories.Core, PungentUtilityCategories.DocumentationPlanning });
            Add<PungentUtilityTrayWindow>("utility-tray", "Minimized Utilities", "Workflow", PungentUtilityCategories.Core, "Workflow", "Inspect, restore, or clear minimized PungentFunk utility windows from a movable overlay tray.", PungentUtilityMenuPaths.UtilityTray, new[] { "minimized", "minimize", "workflow", "tabs", "overlay", "tray" }, 20, packageStatus: PungentUtilityPackageStatus.Stable, categoryFacets: new[] { PungentUtilityCategories.Core }, showInUtilitiesBrowser: true);
            Add<PungentUtilityDeveloperToolsWindow>("developer-tools", "Developer Tools", "Developer Tool", PungentUtilityCategories.Core, "Developer Tools", "Unified workbench for package metadata, utility metadata, category facets, and generated-help maintenance workflows.", PungentUtilityMenuPaths.DeveloperTools, new[] { "developer", "metadata", "packages", "categories", "generation", "help", "registry" }, 23, isLabHub: true, packageStatus: PungentUtilityPackageStatus.Experimental, relatedUtilityIds: new[] { "utilities-browser", "help-browser", "utility-metadata-editor", "category-reference-membership" }, categoryFacets: new[] { PungentUtilityCategories.Core, PungentUtilityCategories.DocumentationPlanning }, itemKind: PungentUtilityItemKind.Internal, visibility: PungentUtilityVisibility.DeveloperOnly, showInUtilitiesBrowser: false);

            AddAction("minimize-focused-window", "Minimize Focused Editor Window", "Action", PungentUtilityCategories.Core, "Workflow", "Minimize the currently focused eligible editor window into the PungentFunk minimized utilities tray.", PungentUtilityMenuPaths.ParkFocusedUtility, new[] { "minimize", "focused", "window", "tray", "workflow", "action" }, 21, () => PungentUtilityMinimizer.MinimizeLastEditorWindowFromCommand(), () => EditorWindow.focusedWindow != null, () => "Focus an eligible editor window before running this action.", packageStatus: PungentUtilityPackageStatus.Stable, relatedUtilityIds: new[] { "utility-tray" }, categoryFacets: new[] { PungentUtilityCategories.Core, PungentUtilityCategories.BatchRepair }, showInUtilitiesBrowser: true);
            AddAction("open-minimized-utilities-tray", "Open Minimized Utilities", "Action", PungentUtilityCategories.Core, "Workflow", "Open or toggle the minimized utilities overlay tray without changing the current window layout.", PungentUtilityMenuPaths.UtilityTray, new[] { "minimized", "tray", "overlay", "restore", "workflow", "action" }, 22, () => PungentUtilityMinimizer.ToggleOverlayTrayFromAccess(new Rect()), null, null, packageStatus: PungentUtilityPackageStatus.Stable, relatedUtilityIds: new[] { "utility-tray", "utilities-browser" }, categoryFacets: new[] { PungentUtilityCategories.Core, PungentUtilityCategories.BatchRepair }, showInUtilitiesBrowser: false);
            Add<DocumentationLinkEditorPopup>("documentation-links", "Documentation Links", "Documentation", PungentUtilityCategories.Core, "Documentation", "Assign current and backlog documentation links to registered utilities.", PungentUtilityMenuPaths.Root + "/Core/Documentation Links", new[] { "documentation", "docs", "links", "metadata", "planning" }, 30, isLabHub: false, packageStatus: PungentUtilityPackageStatus.Experimental, relatedUtilityIds: new[] { "utilities-browser", "help-browser", "developer-tools" }, categoryFacets: new[] { PungentUtilityCategories.Core, PungentUtilityCategories.DocumentationPlanning }, showInUtilitiesBrowser: true);
            Add<PungentEditorStyleExplorerWindow>("editor-style-explorer", "Editor Style Explorer", "Theme Accessory", PungentUtilityCategories.AppearanceStyling, "Appearance", "Theme Editor accessory for experimental editor fonts, icons, and GUIStyle mappings.", PungentUtilityMenuPaths.EditorStyleExplorer, new[] { "style", "gui style", "uss", "fonts", "icons", "theme", "appearance" }, 35, packageStatus: PungentUtilityPackageStatus.Experimental, relatedUtilityIds: new[] { "theme-customizer" }, categoryFacets: new[] { PungentUtilityCategories.AppearanceStyling, PungentUtilityCategories.Core }, showInUtilitiesBrowser: false);
            AddAction("utility-metadata-editor", "Utility Metadata Editor", "Developer Tool", PungentUtilityCategories.Core, "Registry Metadata", "Compatibility route into the unified Developer Tools metadata tab.", PungentUtilityMenuPaths.DeveloperTools, new[] { "metadata", "registry", "cards", "developer", "visibility" }, 36, () => PungentUtilityDeveloperToolsWindow.OpenMetadata("utilities-browser"), null, null, windowTypeName: typeof(PungentUtilityDeveloperToolsWindow).FullName, packageStatus: PungentUtilityPackageStatus.Experimental, relatedUtilityIds: new[] { "developer-tools", "utilities-browser" }, categoryFacets: new[] { PungentUtilityCategories.Core, PungentUtilityCategories.DocumentationPlanning }, itemKind: PungentUtilityItemKind.Internal, visibility: PungentUtilityVisibility.DeveloperOnly, showInUtilitiesBrowser: false);
            AddAction("category-reference-membership", "Category Reference / Membership", "Developer Tool", PungentUtilityCategories.Core, "Registry Metadata", "Compatibility route into the unified Developer Tools metadata tab with Category Facets field focus.", PungentUtilityMenuPaths.DeveloperTools, new[] { "category", "facets", "membership", "developer", "metadata" }, 37, () => PungentUtilityDeveloperToolsWindow.OpenCategory(PungentUtilityCategories.Core), null, null, windowTypeName: typeof(PungentUtilityDeveloperToolsWindow).FullName, packageStatus: PungentUtilityPackageStatus.Experimental, relatedUtilityIds: new[] { "developer-tools", "utility-metadata-editor" }, categoryFacets: new[] { PungentUtilityCategories.Core, PungentUtilityCategories.DocumentationPlanning }, itemKind: PungentUtilityItemKind.Internal, visibility: PungentUtilityVisibility.DeveloperOnly, showInUtilitiesBrowser: false);

            Add<DebugControlWindow>("debug-control", "Debug Controller", "Debugging", PungentUtilityCategories.Debug, "Debugging", "Discover debug flags, gizmos, scheduled actions, and reflected component state across the scene.", PungentUtilityMenuPaths.DebugControlCenter, new[] { "debug", "gizmo", "reflection", "scene" }, 10, supportsSelection: true, isLabHub: true, packageStatus: PungentUtilityPackageStatus.Stable, /*relatedUtilityIds: new[] { "scene-gizmo-browser", "coverage-matrix" },*/ categoryFacets: new[] { PungentUtilityCategories.Debug, PungentUtilityCategories.Audit });

            Add<AssetPlacementLabWindow>("asset-placement-lab", "Asset Placement Lab", "Placement", PungentUtilityCategories.SceneTools, "Asset Placement", "Workspace for reusable scene asset placement, scatter, grid, socket, group, validation, and modular assembly workflows.", PungentUtilityMenuPaths.AssetPlacementLab, new[] { "placement", "scatter", "grid", "socket", "asset", "brush", "scene" }, 0, supportsSceneOverlay: true, supportsContextMenu: true, supportsSelection: true, isLabHub: true, packageStatus: PungentUtilityPackageStatus.InProgress, relatedUtilityIds: new[] { "surface-align", "spatial-authoring-workbench" }, categoryFacets: new[] { PungentUtilityCategories.SceneTools, PungentUtilityCategories.Creation, PungentUtilityCategories.Assets });

            AddAction("create-placement-asset-set-from-selection", "Create Placement Asset Set From Selected Prefabs", "Action", PungentUtilityCategories.SceneTools, "Asset Placement", "Create a reusable placement asset set from selected prefab assets or project objects.", "Assets/PungentFunk Utilities/Create Placement Asset Set From Selected Prefabs", new[] { "placement", "asset set", "prefab", "selection", "action" }, 1, PungentAssetPlacementAccessMenus.CreateAssetSetFromSelectedPrefabs, () => Selection.gameObjects != null && Selection.gameObjects.Length > 0, () => "Select one or more prefab/assets before creating a placement asset set.", packageStatus: PungentUtilityPackageStatus.Experimental, relatedUtilityIds: new[] { "asset-placement-lab" }, categoryFacets: new[] { PungentUtilityCategories.SceneTools, PungentUtilityCategories.Assets, PungentUtilityCategories.Creation }, showInUtilitiesBrowser: true);
            AddAction("add-placement-socket", "Add Placement Socket", "Action", PungentUtilityCategories.SceneTools, "Asset Placement", "Add a placement socket component to the selected scene GameObject(s).", "GameObject/PungentFunk Utilities/Add Placement Socket", new[] { "placement", "socket", "selection", "scene", "action" }, 2, PungentAssetPlacementAccessMenus.AddPlacementSocketToSelected, () => Selection.gameObjects != null && Selection.gameObjects.Length > 0, () => "Select one or more scene GameObjects before adding placement sockets.", packageStatus: PungentUtilityPackageStatus.Experimental, relatedUtilityIds: new[] { "asset-placement-lab" }, categoryFacets: new[] { PungentUtilityCategories.SceneTools, PungentUtilityCategories.Assets }, showInUtilitiesBrowser: true);
            Add<SurfaceAlignToolWindow>("surface-align", "Surface Align Tool", "Alignment", PungentUtilityCategories.SceneTools, "Placement Assist", "Align selected scene objects to collider surfaces with optional rotation and preview behaviour.", PungentUtilityMenuPaths.SurfaceAlignTool, new[] { "surface", "align", "terrain", "placement", "scene" }, 10, supportsSceneOverlay: true, supportsContextMenu: true, supportsSelection: true, packageStatus: PungentUtilityPackageStatus.Stable, /*relatedUtilityIds: new[] { "asset-placement-lab" },*/ categoryFacets: new[] { PungentUtilityCategories.SceneTools, PungentUtilityCategories.BatchRepair });
            Add<PungentSceneNavigationWindow>("scene-navigation", "Scene Navigation", "Navigation", PungentUtilityCategories.SceneTools, "Navigation", "Create scene waypoints, fast travel the SceneView, and optionally track objects by configurable conditions.", PungentUtilityMenuPaths.SceneNavigation, new[] { "scene", "navigation", "waypoint", "tracking", "focus" }, 10, supportsSceneOverlay: true, supportsContextMenu: true, supportsSelection: true, isLabHub: false, packageStatus: PungentUtilityPackageStatus.Stable, relatedUtilityIds: new[] { "scene-gizmo-browser", "spatial-authoring-workbench" }, categoryFacets: new[] { PungentUtilityCategories.SceneTools });
            Add<PungentGizmoBrowserWindow>("scene-gizmo-browser", "Scene Gizmo Browser", "Gizmo", PungentUtilityCategories.SceneTools, "Gizmos", "Inspect, validate, preset, and batch-manage scene gizmo sources, beacons, sensors, trajectories, and performance triage in open scenes.", PungentUtilityMenuPaths.SceneGizmoBrowser, new[] { "gizmo", "scene", "debug", "visualization", "preset", "state color", "beacon", "sensor", "trajectory", "performance" }, 20, supportsSceneOverlay: true, supportsContextMenu: true, supportsSelection: true, packageStatus: PungentUtilityPackageStatus.Experimental, relatedUtilityIds: new[] { "debug-control", "scene-navigation", "spatial-authoring-workbench" }, categoryFacets: new[] { PungentUtilityCategories.SceneTools, PungentUtilityCategories.Debug });

            AddAction("fast-travel-sceneview-to-selection", "Fast Travel SceneView To Selection", "Action", PungentUtilityCategories.SceneTools, "Navigation", "Frame the Scene View camera around the current scene selection.", "GameObject/PungentFunk Utilities/Fast Travel SceneView To Selection", new[] { "scene", "navigation", "frame", "selection", "action" }, 21, PungentSceneNavigationWindow.FastTravelToSelection, () => Selection.transforms != null && Selection.transforms.Length > 0, () => "Select one or more scene objects before framing the SceneView.", packageStatus: PungentUtilityPackageStatus.Stable, relatedUtilityIds: new[] { "scene-navigation" }, categoryFacets: new[] { PungentUtilityCategories.SceneTools }, showInUtilitiesBrowser: true);
            AddAction("add-scene-gizmo-source", "Add Scene Gizmo Source", "Action", PungentUtilityCategories.SceneTools, "Gizmos", "Add a generic scene gizmo source component to the selected GameObject(s).", "GameObject/PungentFunk Utilities/Add Scene Gizmo Source", new[] { "gizmo", "scene", "source", "selection", "action" }, 22, PungentUtilityAccessMenus.AddSceneGizmoSourceToSelection, () => Selection.gameObjects != null && Selection.gameObjects.Length > 0, () => "Select one or more scene GameObjects before adding gizmo sources.", packageStatus: PungentUtilityPackageStatus.Experimental, relatedUtilityIds: new[] { "scene-gizmo-browser", "debug-control" }, categoryFacets: new[] { PungentUtilityCategories.SceneTools, PungentUtilityCategories.Debug }, showInUtilitiesBrowser: true);
            AddAction("add-scene-beacon", "Add Scene Beacon", "Action", PungentUtilityCategories.SceneTools, "Gizmos", "Add a pingable scene beacon provider component to the selected GameObject(s).", "GameObject/PungentFunk Utilities/Scene Tools/Add Scene Beacon", new[] { "gizmo", "scene", "beacon", "ping", "selection", "action" }, 23, PungentUtilityAccessMenus.AddSceneBeaconToSelection, () => Selection.gameObjects != null && Selection.gameObjects.Length > 0, () => "Select one or more scene GameObjects before adding scene beacons.", packageStatus: PungentUtilityPackageStatus.Experimental, relatedUtilityIds: new[] { "scene-gizmo-browser", "scene-navigation" }, categoryFacets: new[] { PungentUtilityCategories.SceneTools, PungentUtilityCategories.Debug }, showInUtilitiesBrowser: true);
            AddAction("add-collision-sensor-gizmo", "Add Collision Sensor Gizmo", "Action", PungentUtilityCategories.SceneTools, "Gizmos", "Add a runtime collision sensor gizmo provider component to the selected GameObject(s).", "GameObject/PungentFunk Utilities/Scene Tools/Add Collision Sensor", new[] { "gizmo", "scene", "collision", "sensor", "contact", "selection", "action" }, 24, PungentUtilityAccessMenus.AddCollisionSensorToSelection, () => Selection.gameObjects != null && Selection.gameObjects.Length > 0, () => "Select one or more scene GameObjects before adding collision sensor gizmos.", packageStatus: PungentUtilityPackageStatus.Experimental, relatedUtilityIds: new[] { "scene-gizmo-browser" }, categoryFacets: new[] { PungentUtilityCategories.SceneTools, PungentUtilityCategories.Debug }, showInUtilitiesBrowser: true);
            AddAction("add-trigger-sensor-gizmo", "Add Trigger Sensor Gizmo", "Action", PungentUtilityCategories.SceneTools, "Gizmos", "Add a runtime trigger sensor gizmo provider component to the selected GameObject(s).", "GameObject/PungentFunk Utilities/Scene Tools/Add Trigger Sensor", new[] { "gizmo", "scene", "trigger", "sensor", "overlap", "selection", "action" }, 25, PungentUtilityAccessMenus.AddTriggerSensorToSelection, () => Selection.gameObjects != null && Selection.gameObjects.Length > 0, () => "Select one or more scene GameObjects before adding trigger sensor gizmos.", packageStatus: PungentUtilityPackageStatus.Experimental, relatedUtilityIds: new[] { "scene-gizmo-browser" }, categoryFacets: new[] { PungentUtilityCategories.SceneTools, PungentUtilityCategories.Debug }, showInUtilitiesBrowser: true);
            AddAction("add-trajectory-visualizer", "Add Trajectory Visualizer", "Action", PungentUtilityCategories.SceneTools, "Gizmos", "Add a configurable trajectory visualizer provider component to the selected GameObject(s).", "GameObject/PungentFunk Utilities/Scene Tools/Add Trajectory Visualizer", new[] { "gizmo", "scene", "trajectory", "physics", "vector", "selection", "action" }, 26, PungentUtilityAccessMenus.AddTrajectoryVisualizerToSelection, () => Selection.gameObjects != null && Selection.gameObjects.Length > 0, () => "Select one or more scene GameObjects before adding trajectory visualizers.", packageStatus: PungentUtilityPackageStatus.Experimental, relatedUtilityIds: new[] { "scene-gizmo-browser" }, categoryFacets: new[] { PungentUtilityCategories.SceneTools, PungentUtilityCategories.Debug }, showInUtilitiesBrowser: true);
            Add<PungentSpatialAuthoringWindow>("spatial-authoring-workbench", "Spatial Authoring Workbench", "Spatial Authoring", PungentUtilityCategories.SceneTools, "Spatial Authoring", "Workbench for generic spatial paths, areas, shared-border regions, 2D plan authoring, background baking, validation, and scene-provider handoffs.", PungentUtilityMenuPaths.SpatialAuthoringWorkbench, new[] { "spatial", "path", "area", "region", "map", "background", "bake", "plan", "2d", "scene", "waypoint", "handles", "authoring", "output", "corridor" }, 30, supportsSceneOverlay: true, supportsSelection: true, isLabHub: true, packageStatus: PungentUtilityPackageStatus.Experimental, relatedUtilityIds: new[] { "scene-navigation", "scene-gizmo-browser", "asset-placement-lab", "path-authoring-toolkit" }, categoryFacets: new[] { PungentUtilityCategories.SceneTools, PungentUtilityCategories.Creation });
            AddAction("path-authoring-toolkit", "Path Authoring Toolkit", "Spatial Authoring", PungentUtilityCategories.SceneTools, "Spatial Authoring", "Compatibility route that opens the Spatial Authoring Workbench for existing path-authoring menu items, registry IDs, and saved references.", PungentUtilityMenuPaths.LegacyPathAuthoringToolkit, new[] { "path", "area", "spatial", "waypoint", "handles", "authoring", "output", "corridor", "legacy" }, 31, () => PungentSpatialAuthoringWindow.Open(), windowTypeName: typeof(PungentSpatialAuthoringWindow).FullName, packageStatus: PungentUtilityPackageStatus.Experimental, relatedUtilityIds: new[] { "spatial-authoring-workbench", "scene-navigation", "scene-gizmo-browser", "asset-placement-lab" }, categoryFacets: new[] { PungentUtilityCategories.SceneTools, PungentUtilityCategories.Creation }, itemKind: PungentUtilityItemKind.LegacyAlias, visibility: PungentUtilityVisibility.Visible);

            Add<PaletteDesignerWindow>("palette-designer", "Palette Designer", "Palette", PungentUtilityCategories.AppearanceStyling, "Colour", "Create, generate, analyze, save, and apply reusable colour palettes.", PungentUtilityMenuPaths.PaletteDesigner, new[] { "palette", "colour", "color", "harmony", "contrast", "swatch", "generation" }, 10, supportsContextMenu: true, supportsSelection: true, isLabHub: true, packageStatus: PungentUtilityPackageStatus.Stable, /*relatedUtilityIds: new[] { "theme-customizer", "procedural-texture-lab" },*/ categoryFacets: new[] { PungentUtilityCategories.AppearanceStyling, PungentUtilityCategories.Creation });

            Add<ProceduralTextureLabWindow>("procedural-texture-lab", "Texture Generator", "Texture", PungentUtilityCategories.Creation, "Procedural Textures", "Generate procedural texture masks from reusable stamp, scatter, lattice, sequence, and density-map placement modes.", PungentUtilityMenuPaths.ProceduralTextureLab, new[] { "texture", "pattern", "stamp", "procedural", "generation", "mask" }, 10, isLabHub: true, packageStatus: PungentUtilityPackageStatus.Experimental, /*relatedUtilityIds: new[] { "texture-array-baker", "palette-designer" },*/ categoryFacets: new[] { PungentUtilityCategories.Creation, PungentUtilityCategories.Assets, PungentUtilityCategories.AppearanceStyling });
            Add<TextureArrayBakerWindow>("texture-array-baker", "Texture Array Baker", "Baking", PungentUtilityCategories.Assets, "Texture Assets", "Bake selected textures or material texture properties into Texture2DArray assets with fallback slices and preview support.", PungentUtilityMenuPaths.TextureArrayBaker, new[] { "texture", "array", "material", "bake", "asset" }, 20, supportsContextMenu: true, supportsSelection: true, packageStatus: PungentUtilityPackageStatus.Stable, /*relatedUtilityIds: new[] { "procedural-texture-lab" },*/ categoryFacets: new[] { PungentUtilityCategories.Assets, PungentUtilityCategories.BatchRepair });

            Add<AudioCoverageContextWindow>("audio-setup-coverage", "Audio Setup Coverage", "Coverage", PungentUtilityCategories.Audio, "Setup Coverage", "Validate reusable audio setup coverage profiles against scenes, prefabs, fields, and assets.", PungentUtilityMenuPaths.AudioSetupCoverage, new[] { "audio", "coverage", "profile", "scan" }, 10, supportsSelection: true, isLabHub: false, packageStatus: PungentUtilityPackageStatus.Experimental, /*relatedUtilityIds: new[] { "audio-catalog-coverage", "reference-assignment-scanner" },*/ categoryFacets: new[] { PungentUtilityCategories.Audio, PungentUtilityCategories.Audit, PungentUtilityCategories.ScanningCoverage });
            Add<AudioCoverageWindow>("audio-catalog-coverage", "Audio Catalog Coverage", "Coverage", PungentUtilityCategories.Audio, "Catalog Coverage", "Audit reusable audio catalog coverage and identify missing or incomplete audio mappings.", PungentUtilityMenuPaths.AudioCatalogCoverage, new[] { "audio", "coverage", "catalog", "cue", "scan" }, 20, supportsSelection: true, packageStatus: PungentUtilityPackageStatus.Experimental, /*relatedUtilityIds: new[] { "audio-setup-coverage" },*/ categoryFacets: new[] { PungentUtilityCategories.Audio, PungentUtilityCategories.Audit, PungentUtilityCategories.ScanningCoverage });

            Add<FontPreviewWindow>("font-preview", "Font Preview", "Preview", PungentUtilityCategories.AppearanceStyling, "Font Assets", "Preview fonts and font assets with cached editor previews.", PungentUtilityMenuPaths.FontPreview, new[] { "font", "preview", "asset" }, 10, supportsContextMenu: true, packageStatus: PungentUtilityPackageStatus.Experimental, /*relatedUtilityIds: new[] { "prefab-icon-generator" },*/ categoryFacets: new[] { PungentUtilityCategories.AppearanceStyling, PungentUtilityCategories.Assets });
            Add<PrefabIconGeneratorWindow>("prefab-icon-generator", "Prefab Icon Generator", "Preview", PungentUtilityCategories.Assets, "Prefab Assets", "Generate icon textures for prefab assets.", PungentUtilityMenuPaths.PrefabIconGenerator, new[] { "prefab", "icon", "preview", "asset" }, 20, supportsContextMenu: true, supportsSelection: true, packageStatus: PungentUtilityPackageStatus.Stable, /*relatedUtilityIds: new[] { "prefab-asset-exporter", "font-preview" },*/ categoryFacets: new[] { PungentUtilityCategories.Assets, PungentUtilityCategories.AppearanceStyling });
            Add<PrefabAssetExporterWindow>("prefab-asset-exporter", "Prefab Asset Exporter", "Export", PungentUtilityCategories.Assets, "Prefab Assets", "Duplicate meshes, materials, and textures used by a prefab into an export-ready folder.", PungentUtilityMenuPaths.PrefabAssetExporter, new[] { "prefab", "export", "asset" }, 30, supportsContextMenu: true, supportsSelection: true, packageStatus: PungentUtilityPackageStatus.Stable, /*relatedUtilityIds: new[] { "prefab-icon-generator" },*/ categoryFacets: new[] { PungentUtilityCategories.Assets, PungentUtilityCategories.BatchRepair });

            AddAction("save-selected-prefab-with-mesh-assets", "Save Selected as Prefab With Mesh Assets", "Action", PungentUtilityCategories.Assets, "Prefab Assets", "Save the selected scene root as a prefab and extract generated mesh assets into the chosen project folder.", PungentUtilityMenuPaths.SaveSelectedPrefabWithMeshAssets, new[] { "prefab", "mesh", "export", "selected", "action" }, 31, PrefabAssetExporter.SaveSelectedAsPrefabWithMeshAssets, () => PrefabAssetExporter.HasSelectedRoot, () => "Select a scene GameObject root before saving it as a prefab with mesh assets.", packageStatus: PungentUtilityPackageStatus.Stable, relatedUtilityIds: new[] { "prefab-asset-exporter" }, categoryFacets: new[] { PungentUtilityCategories.Assets, PungentUtilityCategories.BatchRepair }, showInUtilitiesBrowser: true);

            Add<ComponentTuningCopyWindow>("component-tuning-copy", "Component Matcher", "Authoring", PungentUtilityCategories.BatchRepair, "Component Tools", "Copy serialized tuning values from matching source components to target components with dry-run support.", PungentUtilityMenuPaths.ComponentTuningCopy, new[] { "component", "copy", "tuning", "serialized" }, 10, supportsContextMenu: true, supportsSelection: true, packageStatus: PungentUtilityPackageStatus.Stable, /*relatedUtilityIds: new[] { "reference-assignment-scanner" },*/ categoryFacets: new[] { PungentUtilityCategories.BatchRepair, PungentUtilityCategories.Audit });
            Add<ReferenceAssignmentScannerWindow>("reference-assignment-scanner", "Reference Assignment Scanner", "Scanner", PungentUtilityCategories.Audit, "Reference Repair", "Scan scene objects for missing assignable component references and optionally apply candidates.", PungentUtilityMenuPaths.ReferenceAssignmentScanner, new[] { "reference", "assignment", "scanner", "scene" }, 20, supportsContextMenu: true, supportsSelection: true, packageStatus: PungentUtilityPackageStatus.Experimental, /*relatedUtilityIds: new[] { "component-tuning-copy", "audio-setup-coverage" },*/ categoryFacets: new[] { PungentUtilityCategories.Audit, PungentUtilityCategories.BatchRepair, PungentUtilityCategories.SceneTools, PungentUtilityCategories.ScanningCoverage });
            Add<TerrainUsageScannerWindow>("terrain-usage-scanner", "Terrain Usage Scanner", "Scanner", PungentUtilityCategories.Audit, "Terrain Assets", "Find Terrain and TerrainData usage across scenes and assets.", PungentUtilityMenuPaths.TerrainUsageScanner, new[] { "terrain", "scan", "asset", "scene" }, 30, packageStatus: PungentUtilityPackageStatus.Experimental, categoryFacets: new[] { PungentUtilityCategories.Audit, PungentUtilityCategories.SceneTools, PungentUtilityCategories.Assets, PungentUtilityCategories.ScanningCoverage });
            Add<SceneIssueScannerWindow>("scene-issue-scanner", "Scene Issue Scanner", "Scanner", PungentUtilityCategories.Audit, "Scene Validation", "Scan loaded scenes manually or participate in the Design Validation Audit shared scene pass for missing scripts, broken renderer assets, duplicate scene services, and advisory scene-health findings.", PungentUtilityMenuPaths.SceneIssueScanner, new[] { "scene", "issue", "scanner", "missing script", "material", "camera", "audit" }, 35, supportsSelection: true, packageStatus: PungentUtilityPackageStatus.Experimental, relatedUtilityIds: new[] { "terrain-usage-scanner", "reference-assignment-scanner" }, categoryFacets: new[] { PungentUtilityCategories.Audit, PungentUtilityCategories.SceneTools, PungentUtilityCategories.ScanningCoverage });
            Add<BulkRenameWindow>("bulk-rename", "Bulk Rename", "Renaming", PungentUtilityCategories.BatchRepair, "Batch Authoring", "Rename selected scene objects using shared patterns and numbering rules.", PungentUtilityMenuPaths.BulkRename, new[] { "rename", "selection", "scene" }, 40, supportsContextMenu: true, supportsSelection: true, packageStatus: PungentUtilityPackageStatus.Stable, categoryFacets: new[] { PungentUtilityCategories.BatchRepair, PungentUtilityCategories.SceneTools });
            Add<PungentNotesRoadmapWindow>("tooltip-notes", "Sticky Notes", "Notes", PungentUtilityCategories.DocumentationPlanning, "Context Notes", "Create quick reminders, checklists, and inspector/scene-linked sticky notes. Rich Documents owns longer notebook-style writing.", PungentUtilityMenuPaths.TooltipNotesBrowser, new[] { "notes", "sticky", "reminder", "checklist", "tooltip", "documentation", "audit", "planning", "future features" }, 50, supportsContextMenu: true, supportsSelection: true, packageStatus: PungentUtilityPackageStatus.Experimental, /*relatedUtilityIds: new[] { "design-validation-audit", "token-validator" },*/ categoryFacets: new[] { PungentUtilityCategories.DocumentationPlanning, PungentUtilityCategories.Audit });
            AddAction("open-sticky-notes-overlay", "Open Sticky Notes Overlay", "Action", PungentUtilityCategories.DocumentationPlanning, "Context Notes", "Open the compact movable Sticky Notes authoring finder overlay.", PungentUtilityMenuPaths.Root + "/Project Audit and Authoring/Sticky Notes Overlay", new[] { "notes", "sticky", "overlay", "tray", "authoring", "finder" }, 51, PungentStickyNoteMiniFinderOverlayWindow.Open, null, null, windowTypeName: typeof(PungentStickyNoteMiniFinderOverlayWindow).FullName, packageStatus: PungentUtilityPackageStatus.Experimental, relatedUtilityIds: new[] { "tooltip-notes", "utility-tray" }, categoryFacets: new[] { PungentUtilityCategories.DocumentationPlanning, PungentUtilityCategories.Audit }, showInUtilitiesBrowser: true);
            Add<PungentCoverageMatrixWindow>("coverage-matrix", "Coverage Matrix", "Coverage", PungentUtilityCategories.Audit, "Coverage", "Define expected authoring dimensions and inspect coverage gaps, duplicates, and notes across assets or manual entries.", PungentUtilityMenuPaths.CoverageMatrix, new[] { "coverage", "matrix", "audit", "gaps" }, 60, packageStatus: PungentUtilityPackageStatus.Experimental, /*relatedUtilityIds: new[] { "token-validator" },*/ categoryFacets: new[] { PungentUtilityCategories.Audit, PungentUtilityCategories.DocumentationPlanning, PungentUtilityCategories.ScanningCoverage });
            Add<PungentDataSheetEditorWindow>("data-sheet-editor", "Data Sheet Editor", "Data Sheet", PungentUtilityCategories.DocumentationPlanning, "Structured Data", "Create, edit, validate, import, and export structured authoring tables with shared Authoring references.", PungentUtilityMenuPaths.DataSheetEditor, new[] { "data sheet", "spreadsheet", "table", "csv", "token", "dialogue", "quest", "inventory", "checklist", "authoring" }, 65, supportsContextMenu: true, packageStatus: PungentUtilityPackageStatus.Stable, relatedUtilityIds: new[] { "coverage-matrix", "token-validator", "tooltip-notes", "documentation-links", PungentChecklistUtilityWindow.UtilityId }, categoryFacets: new[] { PungentUtilityCategories.DocumentationPlanning, PungentUtilityCategories.Audit, PungentUtilityCategories.ScanningCoverage });
            Add<PungentChecklistUtilityWindow>(PungentChecklistUtilityWindow.UtilityId, "Checklist Utility", "Checklist", PungentUtilityCategories.DocumentationPlanning, "Checklists", "Run package, project, and imported checklists with pass, partial, fail, notes, guidance prompts, shared JSON definitions, and result export.", PungentUtilityMenuPaths.ChecklistUtility, new[] { "qa", "checklist", "test", "validation", "manual test", "data sheet", "rich document", "board", "graph", "json", "results" }, 66, supportsContextMenu: true, packageStatus: PungentUtilityPackageStatus.Stable, relatedUtilityIds: new[] { "data-sheet-editor", "coverage-matrix", "tooltip-notes", "token-validator", "board-editor" }, categoryFacets: new[] { PungentUtilityCategories.DocumentationPlanning, PungentUtilityCategories.Audit, PungentUtilityCategories.ScanningCoverage });
            Add<PungentTokenValidatorWindow>("token-validator", "Token Validator", "Validator", PungentUtilityCategories.Audit, "Text Validation", "Define reusable brace tokens, link tokens to editor targets, and validate text, assets, notes, and token bindings.", PungentUtilityMenuPaths.TokenValidator, new[] { "token", "validator", "notes", "text", "audit", "metadata", "binding" }, 70, packageStatus: PungentUtilityPackageStatus.Experimental, /*relatedUtilityIds: new[] { "coverage-matrix", "tooltip-notes" },*/ categoryFacets: new[] { PungentUtilityCategories.Audit, PungentUtilityCategories.DocumentationPlanning, PungentUtilityCategories.UI });

            Add<InputPromptIconLibraryAutoFill>("input-prompt-icon-library", "Input Prompt Icon Library Populator", "Input Prompts", PungentUtilityCategories.UI, "Input Prompts", "Populate input prompt icon libraries by serialized field names without a project-specific compile dependency.", PungentUtilityMenuPaths.PopulateInputPromptIconLibrary, new[] { "input", "icons", "library", "ui", "feedback" }, 10, supportsContextMenu: true, supportsSelection: true, isLabHub: true, packageStatus: PungentUtilityPackageStatus.Experimental, categoryFacets: new[] { PungentUtilityCategories.UI, PungentUtilityCategories.Assets });

            AddAction("populate-selected-input-prompt-library", "Populate Selected Input Prompt Library", "Action", PungentUtilityCategories.UI, "Input Prompts", "Populate the currently selected InputPromptIconLibrary-style asset from matching sprite filenames.", PungentUtilityMenuPaths.PopulateSelectedInputPromptLibrary, new[] { "input", "icons", "selected", "library", "action" }, 11, InputPromptIconLibraryAutoFill.PopulateSelectedLibrary, InputPromptIconLibraryAutoFill.CanPopulateSelectedLibrary, () => "Select an InputPromptIconLibrary asset before populating it.", packageStatus: PungentUtilityPackageStatus.Experimental, relatedUtilityIds: new[] { "input-prompt-icon-library" }, categoryFacets: new[] { PungentUtilityCategories.UI, PungentUtilityCategories.Assets }, showInUtilitiesBrowser: true);
            AddAction("populate-default-input-prompt-library", "Populate Default Input Prompt Library", "Action", PungentUtilityCategories.UI, "Input Prompts", "Find and populate the default InputPromptIconLibrary asset if one exists in the project.", PungentUtilityMenuPaths.PopulateDefaultInputPromptLibrary, new[] { "input", "icons", "default", "library", "action" }, 12, InputPromptIconLibraryAutoFill.PopulateDefaultLibrary, null, null, packageStatus: PungentUtilityPackageStatus.Experimental, relatedUtilityIds: new[] { "input-prompt-icon-library" }, categoryFacets: new[] { PungentUtilityCategories.UI, PungentUtilityCategories.Assets }, showInUtilitiesBrowser: true);

            Add<NameGeneratorWindow>("name-generator", "Name Generator", "Naming", PungentUtilityCategories.Creation, "Names", "Generate names from reusable name-list assets and pattern presets.", PungentUtilityMenuPaths.NameGenerator, new[] { "name", "generation", "asset", "content" }, 20, isLabHub: true, packageStatus: PungentUtilityPackageStatus.Stable, /*relatedUtilityIds: new[] { "token-validator" },*/ categoryFacets: new[] { PungentUtilityCategories.Creation, PungentUtilityCategories.DocumentationPlanning });

            Add<PungentEnvironmentSimulationWindow>("environment-simulation", "Environment Simulation", "Environment", PungentUtilityCategories.Creation, "Time & Weather", "Author and preview game-agnostic calendar clocks, weather states, forecasts, environmental outputs, and delta-time channels.", PungentUtilityMenuPaths.EnvironmentSimulationWorkbench, new[] { "environment", "time", "weather", "calendar", "clock", "season", "delta time", "forecast", "wind" }, 25, isLabHub: true, packageStatus: PungentUtilityPackageStatus.Experimental, relatedUtilityIds: new[] { "calendar-clock", "weather-utility", "delta-time-controller" }, categoryFacets: new[] { PungentUtilityCategories.Creation, PungentUtilityCategories.SceneTools, PungentUtilityCategories.UI });
            Add<PungentCalendarClockWindow>("calendar-clock", "Calendar Clock", "Environment", PungentUtilityCategories.Creation, "Time & Weather", "Configure in-game days, months, years, seasons, sunrise/sunset, sun/moon output, and date-based sequence triggers.", PungentUtilityMenuPaths.CalendarClock, new[] { "calendar", "clock", "time", "day", "month", "year", "season", "sun", "sequencing" }, 26, packageStatus: PungentUtilityPackageStatus.Experimental, relatedUtilityIds: new[] { "environment-simulation", "weather-utility", "delta-time-controller" }, categoryFacets: new[] { PungentUtilityCategories.Creation, PungentUtilityCategories.SceneTools });
            Add<PungentWeatherUtilityWindow>("weather-utility", "Weather Utility", "Environment", PungentUtilityCategories.Creation, "Time & Weather", "Author weather presets, generate hourly forecasts, and drive optional fog, cloud, particle, audio, wetness, snow, and wind outputs.", PungentUtilityMenuPaths.Weather, new[] { "weather", "forecast", "wind", "cloud", "rain", "snow", "fog", "wetness", "skybox" }, 27, packageStatus: PungentUtilityPackageStatus.Experimental, relatedUtilityIds: new[] { "environment-simulation", "calendar-clock", "delta-time-controller" }, categoryFacets: new[] { PungentUtilityCategories.Creation, PungentUtilityCategories.SceneTools });
            Add<PungentDeltaTimeUtilityWindow>("delta-time-controller", "Delta Time Controller", "Environment", PungentUtilityCategories.Creation, "Time & Weather", "Control pause, slow motion, fast-forward, and opt-in channel scales for gameplay, UI, physics, animation, clock, and weather systems.", PungentUtilityMenuPaths.DeltaTime, new[] { "delta time", "time scale", "pause", "slow motion", "fast forward", "channel" }, 28, packageStatus: PungentUtilityPackageStatus.Experimental, relatedUtilityIds: new[] { "environment-simulation", "calendar-clock", "weather-utility" }, categoryFacets: new[] { PungentUtilityCategories.Creation, PungentUtilityCategories.Debug, PungentUtilityCategories.SceneTools });
        }

        private static void Add<TWindow>(
            string id,
            string displayName,
            string utilityType,
            string areaCategory,
            string module,
            string description,
            string menuPath,
            string[] tags,
            int sortOrder,
            bool supportsSceneOverlay = false,
            bool supportsContextMenu = false,
            bool supportsSelection = false,
            bool isLabHub = false,
            string packageStatus = null,
            string[] relatedUtilityIds = null,
            string[] categoryFacets = null,
            bool showInUtilitiesBrowser = true,
            PungentUtilityItemKind itemKind = PungentUtilityItemKind.Utility,
            PungentUtilityVisibility visibility = PungentUtilityVisibility.Visible)
            where TWindow : EditorWindow
        {
            PungentUtilityDescriptor descriptor = new PungentUtilityDescriptor(
                id,
                displayName,
                utilityType,
                description,
                menuPath,
                typeof(TWindow).FullName,
                tags,
                supportsSceneOverlay,
                supportsContextMenu,
                supportsSelection,
                () =>
                {
                    if (typeof(TWindow) == typeof(PungentUtilityTrayWindow))
                    {
                        PungentUtilityMinimizer.ToggleOverlayTrayFromAccess(new Rect());
                        return;
                    }

                    TWindow window = EditorWindow.GetWindow<TWindow>(false, displayName, true);
                    window.titleContent = new GUIContent(displayName);
                    window.Show();
                },
                areaCategory,
                module,
                sortOrder,
                isLabHub,
                packageStatus,
                relatedUtilityIds,
                categoryFacets,
                itemKind,
                visibility);
            descriptor.ShowInUtilitiesBrowser = showInUtilitiesBrowser;
            RegisterInternal(descriptor);
        }

        private static void Add(
            string id,
            string displayName,
            string utilityType,
            string areaCategory,
            string module,
            string description,
            string menuPath,
            string windowTypeName,
            string[] tags,
            int sortOrder,
            bool supportsSceneOverlay = false,
            bool supportsContextMenu = false,
            bool supportsSelection = false,
            bool isLabHub = false,
            string packageStatus = null,
            string[] relatedUtilityIds = null,
            string[] categoryFacets = null,
            bool showInUtilitiesBrowser = true,
            PungentUtilityItemKind itemKind = PungentUtilityItemKind.Utility,
            PungentUtilityVisibility visibility = PungentUtilityVisibility.Visible)
        {
            PungentUtilityDescriptor descriptor = new PungentUtilityDescriptor(
                id,
                displayName,
                utilityType,
                description,
                menuPath,
                windowTypeName,
                tags,
                supportsSceneOverlay,
                supportsContextMenu,
                supportsSelection,
                null,
                areaCategory,
                module,
                sortOrder,
                isLabHub,
                packageStatus,
                relatedUtilityIds,
                categoryFacets,
                itemKind,
                visibility);
            descriptor.ShowInUtilitiesBrowser = showInUtilitiesBrowser;
            RegisterInternal(descriptor);
        }

        private static void AddAction(
            string id,
            string displayName,
            string utilityType,
            string areaCategory,
            string module,
            string description,
            string menuPath,
            string[] tags,
            int sortOrder,
            Action action,
            Func<bool> canRunAction = null,
            Func<string> disabledReasonProvider = null,
            string windowTypeName = null,
            string packageStatus = null,
            string[] relatedUtilityIds = null,
            string[] categoryFacets = null,
            bool showInUtilitiesBrowser = true,
            PungentUtilityItemKind itemKind = PungentUtilityItemKind.Action,
            PungentUtilityVisibility visibility = PungentUtilityVisibility.Visible)
        {
            PungentUtilityDescriptor descriptor = new PungentUtilityDescriptor(
                id,
                displayName,
                utilityType,
                description,
                menuPath,
                windowTypeName ?? string.Empty,
                tags,
                supportsSceneOverlay: false,
                supportsContextMenu: true,
                supportsSelection: false,
                openAction: action,
                lab: areaCategory,
                module: module,
                sortOrder: sortOrder,
                isLabHub: false,
                packageStatus: packageStatus,
                relatedUtilityIds: relatedUtilityIds,
                categoryFacets: categoryFacets,
                itemKind: itemKind,
                visibility: visibility,
                canRunAction: canRunAction,
                disabledReasonProvider: disabledReasonProvider);
            descriptor.ShowInUtilitiesBrowser = showInUtilitiesBrowser;
            RegisterInternal(descriptor);
        }

        private static void RegisterInternal(PungentUtilityDescriptor descriptor)
        {
            if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.Id))
                return;

            PungentUtilityPackageCatalog.ApplyDescriptorDefaults(descriptor);
            PungentUtilityMetadataOverrides.instance.ApplyTo(descriptor);

            descriptor.AreaCategory = PungentUtilityCategories.Normalize(descriptor.AreaCategory);
            descriptor.CategoryFacets = PungentUtilityCategories.NormalizeMany(descriptor.CategoryFacets);
            descriptor.UtilityType = string.IsNullOrWhiteSpace(descriptor.UtilityType) ? "Other" : descriptor.UtilityType.Trim();
            descriptor.PackageStatus = PungentUtilityPackageStatus.Normalize(descriptor.PackageStatus);
            PungentUtilityPackageCatalog.NormalizeDescriptorMetadata(descriptor);
            ResolveBrowserSurfaceDefaults(descriptor);

            if (!Enum.IsDefined(typeof(PungentUtilityItemKind), descriptor.ItemKind))
                descriptor.ItemKind = PungentUtilityItemKind.Utility;
            if (!Enum.IsDefined(typeof(PungentUtilityVisibility), descriptor.Visibility))
                descriptor.Visibility = PungentUtilityVisibility.Visible;

            int existing = Utilities.FindIndex(u => string.Equals(u.Id, descriptor.Id, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0)
                Utilities[existing] = descriptor;
            else
                Utilities.Add(descriptor);

            Utilities.Sort(CompareDescriptors);
        }

        private static void ResolveBrowserSurfaceDefaults(PungentUtilityDescriptor descriptor)
        {
            if (descriptor == null)
                return;

            if (descriptor.BrowserPriority == 0 && descriptor.SortOrder != 0)
                descriptor.BrowserPriority = descriptor.SortOrder;

            if (descriptor.ItemKind == PungentUtilityItemKind.Action)
            {
                descriptor.BrowserRole = PungentUtilityBrowserRole.Action;
                if (string.IsNullOrWhiteSpace(descriptor.ParentUtilityId))
                    descriptor.ParentUtilityId = FirstRelatedNonActionId(descriptor);
            }
            else if (descriptor.ItemKind == PungentUtilityItemKind.Internal || descriptor.Visibility == PungentUtilityVisibility.DeveloperOnly)
            {
                descriptor.BrowserRole = PungentUtilityBrowserRole.Internal;
                descriptor.BrowserProminence = PungentUtilityBrowserProminence.DeveloperOnly;
            }
            else if (descriptor.ItemKind == PungentUtilityItemKind.LegacyAlias || descriptor.Visibility == PungentUtilityVisibility.Archived)
            {
                descriptor.BrowserRole = PungentUtilityBrowserRole.LegacyAlias;
                descriptor.BrowserProminence = PungentUtilityBrowserProminence.HiddenUnlessSearched;
            }
            else if (!string.IsNullOrWhiteSpace(descriptor.ParentUtilityId) && descriptor.BrowserRole == PungentUtilityBrowserRole.CoreUtility)
            {
                descriptor.BrowserRole = PungentUtilityBrowserRole.AccessoryUtility;
            }

            descriptor.ParentUtilityId = descriptor.ParentUtilityId ?? string.Empty;
        }

        private static string FirstRelatedNonActionId(PungentUtilityDescriptor descriptor)
        {
            if (descriptor == null || descriptor.RelatedUtilityIds == null)
                return string.Empty;

            for (int i = 0; i < descriptor.RelatedUtilityIds.Length; i++)
            {
                PungentUtilityDescriptor related = Utilities.FirstOrDefault(u => u != null && string.Equals(u.Id, descriptor.RelatedUtilityIds[i], StringComparison.OrdinalIgnoreCase));
                if (related != null && !related.IsAction)
                    return related.Id;
            }

            return string.Empty;
        }

        private static int CompareDescriptors(PungentUtilityDescriptor a, PungentUtilityDescriptor b)
        {
            int category = PungentUtilityCategories.SortKey(GetAreaCategory(a)).CompareTo(PungentUtilityCategories.SortKey(GetAreaCategory(b)));
            if (category != 0)
                return category;

            int categoryName = string.Compare(GetAreaCategory(a), GetAreaCategory(b), StringComparison.OrdinalIgnoreCase);
            if (categoryName != 0)
                return categoryName;

            int type = string.Compare(GetUtilityType(a), GetUtilityType(b), StringComparison.OrdinalIgnoreCase);
            if (type != 0)
                return type;

            int kind = a.ItemKind.CompareTo(b.ItemKind);
            if (kind != 0)
                return kind;

            int status = PungentUtilityPackageStatus.SortKey(GetStatus(a)).CompareTo(PungentUtilityPackageStatus.SortKey(GetStatus(b)));
            if (status != 0)
                return status;

            int order = a.SortOrder.CompareTo(b.SortOrder);
            if (order != 0)
                return order;

            return string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
        }
    }
#endif
}
