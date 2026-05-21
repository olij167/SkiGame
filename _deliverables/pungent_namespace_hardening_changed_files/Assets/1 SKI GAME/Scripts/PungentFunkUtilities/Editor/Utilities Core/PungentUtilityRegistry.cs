using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Audio;
using PungentFunk.Utilities.Editor.Colour;
using PungentFunk.Utilities.Editor.Content;
using PungentFunk.Utilities.Editor.Debugging;
using PungentFunk.Utilities.Editor.Generation;
using PungentFunk.Utilities.Editor.Placement;
using PungentFunk.Utilities.Editor.PreviewExport;
using PungentFunk.Utilities.Editor.ProjectAudit;
using PungentFunk.Utilities.Editor.SceneTools;
using PungentFunk.Utilities.Editor.UI;

namespace PungentFunk.Utilities.Editor.Core
{
    #if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Central registry for reusable PungentFunk Utilities.
    /// New utilities should register here or through a consistent per-lab registrar with equivalent metadata.
    /// </summary>
    public static class PungentUtilityRegistry
    {
        private static readonly List<PungentUtilityDescriptor> Utilities = new List<PungentUtilityDescriptor>();
        private static bool _initialized;

        public static IReadOnlyList<PungentUtilityDescriptor> All
        {
            get
            {
                EnsureInitialized();
                return Utilities;
            }
        }

        public static void Register(PungentUtilityDescriptor descriptor)
        {
            if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.Id))
                return;

            EnsureInitialized();
            RegisterInternal(descriptor);
        }

        public static void RegisterRange(IEnumerable<PungentUtilityDescriptor> descriptors)
        {
            if (descriptors == null)
                return;

            EnsureInitialized();
            foreach (PungentUtilityDescriptor descriptor in descriptors)
                RegisterInternal(descriptor);
        }

        public static PungentUtilityDescriptor Find(string id)
        {
            EnsureInitialized();
            return Utilities.FirstOrDefault(u => string.Equals(u.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        public static bool Open(string id)
        {
            PungentUtilityDescriptor descriptor = Find(id);
            if (descriptor == null)
                return false;

            descriptor.Open();
            PungentUtilityControlPanelWindow.RecordRecentUtility(id);
            return true;
        }

        public static string[] Labs(bool includeEmptyKnownLabs = false)
        {
            EnsureInitialized();
            IEnumerable<string> labs = Utilities.Select(GetLab);
            if (includeEmptyKnownLabs)
                labs = labs.Concat(PungentUtilityLabs.CurrentLabs);

            return labs
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(PungentUtilityLabs.SortKey)
                .ThenBy(l => l, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static string[] Categories(string lab = null)
        {
            EnsureInitialized();
            return Utilities
                .Where(u => string.IsNullOrEmpty(lab) || string.Equals(GetLab(u), lab, StringComparison.OrdinalIgnoreCase))
                .Select(GetCategory)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static string[] Modules(string lab = null)
        {
            EnsureInitialized();
            return Utilities
                .Where(u => string.IsNullOrEmpty(lab) || string.Equals(GetLab(u), lab, StringComparison.OrdinalIgnoreCase))
                .Select(GetModule)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(m => m, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static string[] Statuses(string lab = null)
        {
            EnsureInitialized();
            return Utilities
                .Where(u => string.IsNullOrEmpty(lab) || string.Equals(GetLab(u), lab, StringComparison.OrdinalIgnoreCase))
                .Select(GetStatus)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(PungentUtilityPackageStatus.SortKey)
                .ThenBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static IEnumerable<PungentUtilityDescriptor> Query(string lab = null, string category = null, string search = null, string status = null)
        {
            EnsureInitialized();
            return Utilities
                .Where(u => string.IsNullOrEmpty(lab) || string.Equals(GetLab(u), lab, StringComparison.OrdinalIgnoreCase))
                .Where(u => string.IsNullOrEmpty(category) || string.Equals(GetCategory(u), category, StringComparison.OrdinalIgnoreCase))
                .Where(u => string.IsNullOrEmpty(status) || string.Equals(GetStatus(u), status, StringComparison.OrdinalIgnoreCase))
                .Where(u => u.Matches(search));
        }

        public static IEnumerable<PungentUtilityDescriptor> RelatedUtilities(PungentUtilityDescriptor descriptor)
        {
            EnsureInitialized();
            if (descriptor == null || descriptor.RelatedUtilityIds == null)
                yield break;

            for (int i = 0; i < descriptor.RelatedUtilityIds.Length; i++)
            {
                PungentUtilityDescriptor related = Find(descriptor.RelatedUtilityIds[i]);
                if (related != null)
                    yield return related;
            }
        }

        public static string GetLab(PungentUtilityDescriptor descriptor)
        {
            return descriptor == null ? PungentUtilityLabs.Other : PungentUtilityLabs.Normalize(descriptor.Lab);
        }

        public static string GetModule(PungentUtilityDescriptor descriptor)
        {
            if (descriptor == null)
                return PungentUtilityLabs.Other;

            if (!string.IsNullOrWhiteSpace(descriptor.Module))
                return descriptor.Module;

            return GetCategory(descriptor);
        }

        public static string GetCategory(PungentUtilityDescriptor descriptor)
        {
            return descriptor == null || string.IsNullOrWhiteSpace(descriptor.Category) ? "Other" : descriptor.Category;
        }

        public static string GetStatus(PungentUtilityDescriptor descriptor)
        {
            return descriptor == null ? PungentUtilityPackageStatus.Experimental : PungentUtilityPackageStatus.Normalize(descriptor.PackageStatus);
        }

        public static int CountByLab(string lab)
        {
            EnsureInitialized();
            return Utilities.Count(u => string.Equals(GetLab(u), lab, StringComparison.OrdinalIgnoreCase));
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

            Add("theme-customizer", "Utility Window Theme", "Appearance", PungentUtilityLabs.UtilityCore, "Theme", "Customize the shared visual style used by PungentFunk utility windows.", "Tools/Utilities/Appearance/Utility Window Theme", "UtilityWindowThemeCustomizer", new[] { "theme", "appearance", "style", "lab" }, 10, isLabHub: true, packageStatus: PungentUtilityPackageStatus.Stable, relatedUtilityIds: new[] { "palette-designer", "design-validation-audit" });
            Add("design-validation-audit", "Design Validation Audit", "Core", PungentUtilityLabs.UtilityCore, "Architecture", "Validate registered utilities, window layout signals, menu taxonomy, CreateAssetMenu roots, namespaces, and asmdef readiness against the design bible.", "Tools/Utilities/Core/Design Validation Audit", "PungentUtilityDesignAuditWindow", new[] { "audit", "architecture", "layout", "validation", "package", "design bible" }, 15, isLabHub: true, packageStatus: PungentUtilityPackageStatus.InProgress, relatedUtilityIds: new[] { "theme-customizer", "asset-placement-lab", "reference-assignment-scanner" });
            Add("utility-tray", "Utility Tray", "Workflow", PungentUtilityLabs.UtilityCore, "Workflow", "Reopen, pin, and close parked PungentFunk utility windows from a compact tray.", "Tools/Utilities/Workflow/Utility Tray", "PungentUtilityTrayWindow", new[] { "tray", "park", "workflow", "launcher" }, 20, packageStatus: PungentUtilityPackageStatus.Experimental, relatedUtilityIds: new[] { "design-validation-audit" });

            Add("debug-control", "Debug Control Center", "Debug", PungentUtilityLabs.DebugLab, "Debug", "Discover debug flags, gizmos, scheduled actions, and reflected component state across the scene.", "Tools/Utilities/Debug/Debug Control Center", "DebugControlWindow", new[] { "debug", "gizmo", "reflection", "scene", "lab" }, 10, supportsSelection: true, isLabHub: true, packageStatus: PungentUtilityPackageStatus.Stable, relatedUtilityIds: new[] { "scene-gizmo-browser", "coverage-matrix" });

            Add("asset-placement-lab", "Asset Placement Lab", "Placement", PungentUtilityLabs.AssetPlacementLab, "Lab Hub", "Hub for reusable scene asset placement, scatter, grid, socket, group, validation, and modular assembly workflows.", "Tools/Utilities/Placement/Asset Placement Lab", "AssetPlacementLabWindow", new[] { "placement", "scatter", "grid", "socket", "asset", "lab" }, 0, supportsSceneOverlay: true, supportsContextMenu: true, supportsSelection: true, isLabHub: true, packageStatus: PungentUtilityPackageStatus.InProgress, relatedUtilityIds: new[] { "surface-align", "modular-path-builder", "path-authoring-toolkit", "design-validation-audit" });
            Add("surface-align", "Surface Align Tool", "Scene", PungentUtilityLabs.AssetPlacementLab, "Manual Assist", "Align selected scene objects to collider surfaces with optional rotation and preview behaviour.", "Tools/Utilities/Scene/Surface Align Tool", "SurfaceAlignToolWindow", new[] { "surface", "align", "terrain", "placement", "scene" }, 10, supportsSceneOverlay: true, supportsContextMenu: true, supportsSelection: true, packageStatus: PungentUtilityPackageStatus.Stable, relatedUtilityIds: new[] { "asset-placement-lab" });
            Add("modular-path-builder", "Modular Path Builder", "Scene", PungentUtilityLabs.AssetPlacementLab, "Modular Assembly", "Place repeated modular prefabs with scene handles and reusable path settings.", "Tools/Utilities/Scene/Modular Path Builder", "ModularPathWindow", new[] { "path", "modular", "scene", "builder", "placement" }, 30, supportsSceneOverlay: true, supportsContextMenu: true, supportsSelection: true, packageStatus: PungentUtilityPackageStatus.Experimental, relatedUtilityIds: new[] { "path-authoring-toolkit", "asset-placement-lab" });

            Add("scene-navigation", "Scene Navigation", "Scene", PungentUtilityLabs.SceneWorkflowLab, "Navigation", "Create scene waypoints, fast travel the SceneView, and optionally track objects by configurable conditions.", "Tools/Utilities/Scene/Scene Navigation", "PungentSceneNavigationWindow", new[] { "scene", "navigation", "waypoint", "tracking", "focus" }, 10, supportsSceneOverlay: true, supportsContextMenu: true, supportsSelection: true, isLabHub: true, packageStatus: PungentUtilityPackageStatus.Stable, relatedUtilityIds: new[] { "scene-gizmo-browser", "path-authoring-toolkit" });
            Add("scene-gizmo-browser", "Scene Gizmo Browser", "Scene", PungentUtilityLabs.SceneWorkflowLab, "Gizmos", "Inspect and manage generic scene gizmo sources in the open scene.", "Tools/Utilities/Scene/Scene Gizmo Browser", "PungentGizmoBrowserWindow", new[] { "gizmo", "scene", "debug", "visualization" }, 20, supportsSceneOverlay: true, supportsContextMenu: true, supportsSelection: true, packageStatus: PungentUtilityPackageStatus.Experimental, relatedUtilityIds: new[] { "debug-control", "scene-navigation" });
            Add("path-authoring-toolkit", "Path Authoring Toolkit", "Scene", PungentUtilityLabs.SceneWorkflowLab, "Path Authoring", "Reusable path handle helpers, validation, sampling, and scene overlay utilities for path-based editor tools.", "Tools/Utilities/Scene/Path Authoring Toolkit", "PungentPathAuthoringToolkitWindow", new[] { "path", "waypoint", "handles", "authoring" }, 30, supportsSceneOverlay: true, supportsSelection: true, packageStatus: PungentUtilityPackageStatus.Experimental, relatedUtilityIds: new[] { "modular-path-builder", "scene-navigation" });

            Add("palette-designer", "Palette Designer", "Colour", PungentUtilityLabs.ColourLab, "Palettes", "Create, generate, analyze, save, and apply reusable colour palettes.", "Tools/Utilities/Colour/Palette Designer", "PaletteDesignerWindow", new[] { "palette", "colour", "color", "harmony", "contrast", "swatch", "generation", "lab" }, 10, supportsContextMenu: true, supportsSelection: true, isLabHub: true, packageStatus: PungentUtilityPackageStatus.Stable, relatedUtilityIds: new[] { "theme-customizer", "procedural-texture-lab" });

            Add("procedural-texture-lab", "Procedural Texture Lab", "Texture", PungentUtilityLabs.TextureLab, "Procedural Textures", "Generate procedural texture masks from reusable stamp, scatter, lattice, sequence, and density-map placement modes.", "Tools/Utilities/Texture/Procedural Texture Lab", "ProceduralTextureLabWindow", new[] { "texture", "pattern", "stamp", "procedural", "generation", "mask", "lab" }, 10, isLabHub: true, packageStatus: PungentUtilityPackageStatus.Experimental, relatedUtilityIds: new[] { "texture-array-baker", "palette-designer" });
            Add("texture-array-baker", "Texture Array Baker", "Texture", PungentUtilityLabs.TextureLab, "Texture Arrays", "Bake selected textures or material texture properties into Texture2DArray assets with fallback slices and preview support.", "Tools/Utilities/Texture/Texture Array Baker", "TextureArrayBakerWindow", new[] { "texture", "array", "material", "bake", "asset" }, 20, supportsContextMenu: true, supportsSelection: true, packageStatus: PungentUtilityPackageStatus.Stable, relatedUtilityIds: new[] { "procedural-texture-lab" });

            Add("audio-setup-coverage", "Audio Setup Coverage", "Audio", PungentUtilityLabs.AudioLab, "Coverage", "Validate reusable audio setup coverage profiles against scenes, prefabs, fields, and assets.", "Tools/Utilities/Audio/Audio Setup Coverage", "AudioCoverageContextWindow", new[] { "audio", "coverage", "profile", "scan", "lab" }, 10, supportsSelection: true, isLabHub: true, packageStatus: PungentUtilityPackageStatus.Experimental, relatedUtilityIds: new[] { "audio-catalog-coverage", "reference-assignment-scanner" });
            Add("audio-catalog-coverage", "Audio Catalog Coverage", "Audio", PungentUtilityLabs.AudioLab, "Coverage", "Audit reusable audio catalog coverage and identify missing or incomplete audio mappings.", "Tools/Utilities/Audio/Audio Catalog Coverage", "AudioCoverageWindow", new[] { "audio", "coverage", "catalog", "cue", "scan" }, 20, supportsSelection: true, packageStatus: PungentUtilityPackageStatus.Experimental, relatedUtilityIds: new[] { "audio-setup-coverage" });

            Add("font-preview", "Font Preview", "Assets", PungentUtilityLabs.AssetPreviewExportLab, "Preview", "Preview fonts and font assets with cached editor previews.", "Tools/Utilities/Assets/Font Preview", "FontPreviewWindow", new[] { "font", "preview", "asset" }, 10, supportsContextMenu: true, packageStatus: PungentUtilityPackageStatus.Experimental, relatedUtilityIds: new[] { "prefab-icon-generator" });
            Add("prefab-icon-generator", "Prefab Icon Generator", "Assets", PungentUtilityLabs.AssetPreviewExportLab, "Preview", "Generate icon textures for prefab assets.", "Tools/Utilities/Assets/Prefab Icon Generator", "PrefabIconGeneratorWindow", new[] { "prefab", "icon", "preview", "asset" }, 20, supportsContextMenu: true, supportsSelection: true, packageStatus: PungentUtilityPackageStatus.Stable, relatedUtilityIds: new[] { "prefab-asset-exporter", "font-preview" });
            Add("prefab-asset-exporter", "Prefab Asset Exporter", "Prefabs", PungentUtilityLabs.AssetPreviewExportLab, "Export", "Duplicate meshes, materials, and textures used by a prefab into an export-ready folder.", "Tools/Utilities/Prefabs/Prefab Asset Exporter", "PrefabAssetExporterWindow", new[] { "prefab", "export", "asset" }, 30, supportsContextMenu: true, supportsSelection: true, packageStatus: PungentUtilityPackageStatus.Stable, relatedUtilityIds: new[] { "prefab-icon-generator" });

            Add("component-tuning-copy", "Component Tuning Copy", "Components", PungentUtilityLabs.ProjectAuditAuthoringLab, "Authoring", "Copy serialized tuning values from matching source components to target components with dry-run support.", "Tools/Utilities/Components/Copy Component Tuning", "ComponentTuningCopyWindow", new[] { "component", "copy", "tuning", "serialized" }, 10, supportsContextMenu: true, supportsSelection: true, packageStatus: PungentUtilityPackageStatus.Stable, relatedUtilityIds: new[] { "reference-assignment-scanner" });
            Add("reference-assignment-scanner", "Reference Assignment Scanner", "Scene", PungentUtilityLabs.ProjectAuditAuthoringLab, "Scanners", "Scan scene objects for missing assignable component references and optionally apply candidates.", "Tools/Utilities/Scene/Reference Assignment Scanner", "ReferenceAssignmentScannerWindow", new[] { "reference", "assignment", "scanner", "scene" }, 20, supportsContextMenu: true, supportsSelection: true, packageStatus: PungentUtilityPackageStatus.Experimental, relatedUtilityIds: new[] { "component-tuning-copy", "audio-setup-coverage" });
            Add("terrain-usage-scanner", "Terrain Usage Scanner", "Terrain", PungentUtilityLabs.ProjectAuditAuthoringLab, "Scanners", "Find Terrain and TerrainData usage across scenes and assets.", "Tools/Utilities/Terrain/Terrain Usage Scanner", "TerrainUsageScannerWindow", new[] { "terrain", "scan", "asset", "scene" }, 30, packageStatus: PungentUtilityPackageStatus.Experimental);
            Add("bulk-rename", "Bulk Rename", "Scene", PungentUtilityLabs.ProjectAuditAuthoringLab, "Authoring", "Rename selected scene objects using shared patterns and numbering rules.", "Tools/Utilities/Scene/Bulk Rename", "BulkRenameWindow", new[] { "rename", "selection", "scene" }, 40, supportsContextMenu: true, supportsSelection: true, packageStatus: PungentUtilityPackageStatus.Stable);
            Add("tooltip-notes", "Tooltip Notes Browser", "Components", PungentUtilityLabs.ProjectAuditAuthoringLab, "Documentation", "Create, browse, edit, and delete custom editor notes attached to components and serialized properties.", "Tools/Utilities/Components/Tooltip Notes Browser", "PungentTooltipNotesBrowserWindow", new[] { "tooltip", "notes", "inspector", "documentation", "todo" }, 50, supportsContextMenu: true, supportsSelection: true, packageStatus: PungentUtilityPackageStatus.Experimental);
            Add("coverage-matrix", "Coverage Matrix", "Generation", PungentUtilityLabs.ProjectAuditAuthoringLab, "Coverage", "Define expected authoring dimensions and inspect coverage gaps, duplicates, and notes across assets or manual entries.", "Tools/Utilities/Generation/Coverage Matrix", "PungentCoverageMatrixWindow", new[] { "coverage", "matrix", "audit", "gaps" }, 60, packageStatus: PungentUtilityPackageStatus.Experimental, relatedUtilityIds: new[] { "token-validator" });
            Add("token-validator", "Token Validator", "Generation", PungentUtilityLabs.ProjectAuditAuthoringLab, "Validation", "Validate text assets or pasted text against reusable token schemas.", "Tools/Utilities/Generation/Token Validator", "PungentTokenValidatorWindow", new[] { "token", "validation", "text", "schema" }, 70, packageStatus: PungentUtilityPackageStatus.Stable, relatedUtilityIds: new[] { "coverage-matrix" });

            Add("input-prompt-icon-library", "Input Prompt Icon Library Populator", "Input", PungentUtilityLabs.UIFeedbackLab, "Input Prompts", "Populate input prompt icon libraries by serialized field names without a project-specific compile dependency.", "Tools/Utilities/Input/Populate Input Prompt Icon Library", "InputPromptIconLibraryAutoFill", new[] { "input", "icons", "library", "ui", "feedback" }, 10, supportsContextMenu: true, supportsSelection: true, isLabHub: true, packageStatus: PungentUtilityPackageStatus.Experimental);

            Add("name-generator", "Name Generator", "Generation", PungentUtilityLabs.ContentGenerationLab, "Names", "Generate names from reusable name-list assets and pattern presets.", "Tools/Utilities/Generation/Name Generator", "NameGeneratorWindow", new[] { "name", "generation", "asset", "content" }, 10, isLabHub: true, packageStatus: PungentUtilityPackageStatus.Stable, relatedUtilityIds: new[] { "token-validator" });
        }

        private static void Add(
            string id,
            string displayName,
            string category,
            string lab,
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
            string[] relatedUtilityIds = null)
        {
            RegisterInternal(new PungentUtilityDescriptor(
                id,
                displayName,
                category,
                description,
                menuPath,
                windowTypeName,
                tags,
                supportsSceneOverlay,
                supportsContextMenu,
                supportsSelection,
                null,
                lab,
                module,
                sortOrder,
                isLabHub,
                packageStatus,
                relatedUtilityIds));
        }

        private static void RegisterInternal(PungentUtilityDescriptor descriptor)
        {
            if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.Id))
                return;

            descriptor.Lab = PungentUtilityLabs.Normalize(descriptor.Lab);
            descriptor.PackageStatus = PungentUtilityPackageStatus.Normalize(descriptor.PackageStatus);

            int existing = Utilities.FindIndex(u => string.Equals(u.Id, descriptor.Id, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0)
                Utilities[existing] = descriptor;
            else
                Utilities.Add(descriptor);

            Utilities.Sort(CompareDescriptors);
        }

        private static int CompareDescriptors(PungentUtilityDescriptor a, PungentUtilityDescriptor b)
        {
            int lab = PungentUtilityLabs.SortKey(GetLab(a)).CompareTo(PungentUtilityLabs.SortKey(GetLab(b)));
            if (lab != 0)
                return lab;

            int labName = string.Compare(GetLab(a), GetLab(b), StringComparison.OrdinalIgnoreCase);
            if (labName != 0)
                return labName;

            int module = string.Compare(GetModule(a), GetModule(b), StringComparison.OrdinalIgnoreCase);
            if (module != 0)
                return module;

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