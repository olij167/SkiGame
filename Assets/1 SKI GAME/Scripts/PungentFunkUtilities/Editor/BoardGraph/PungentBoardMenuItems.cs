using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.Editor.Checklists;
using PungentFunk.Utilities.Editor.Core;
using UnityEditor;

namespace PungentFunk.Utilities.Editor.BoardGraph
{
#if UNITY_EDITOR
    [InitializeOnLoad]
    public static class PungentBoardMenuItems
    {
        public const string BoardEditorPath = "Tools/PungentFunk Utilities/Board & Graph/Board Editor";
        public const string BoardEditorCorePath = "Tools/PungentFunk/Board & Graph/Board Editor";
        public const string FeatureChecklistPath = "Tools/PungentFunk Utilities/Board & Graph/Feature Test Checklist";
        public const string BrowserPath = "Tools/PungentFunk Utilities/Board & Graph/Open in Utilities Browser";
        public const string FeatureChecklistUtilityId = PungentChecklistUtilityWindow.UtilityId;

        private static bool _registeringDescriptor;

        static PungentBoardMenuItems()
        {
            PungentBoardProviderBootstrap.RegisterProvider();
            RegisterUtilityDescriptor();
            PungentUtilityRegistry.Changed += RegisterUtilityDescriptor;
        }

        [MenuItem(BoardEditorPath, priority = 875)]
        public static void OpenBoardEditor()
        {
            PungentBoardProviderBootstrap.RegisterProvider();
            RegisterUtilityDescriptor();
            PungentUtilityRegistry.Open(PungentBoardProvider.UtilityId);
        }

        [MenuItem(BoardEditorCorePath, priority = 875)]
        public static void OpenBoardEditorCorePath()
        {
            OpenBoardEditor();
        }

        [MenuItem(BrowserPath, priority = 876)]
        public static void OpenBoardInUtilitiesBrowser()
        {
            RegisterUtilityDescriptor();
            PungentUtilityControlPanelWindow.OpenCategory(PungentUtilityCategories.DocumentationPlanning);
        }

        [MenuItem(FeatureChecklistPath, priority = 877)]
        public static void OpenFeatureTestChecklist()
        {
            PungentChecklistUtilityWindow.OpenBoardGraphFeatureTest();
        }

        [MenuItem("Assets/PungentFunk Utilities/Open Board Editor", priority = 24)]
        public static void OpenBoardEditorFromAssets()
        {
            OpenBoardEditor();
        }

        [MenuItem("GameObject/PungentFunk Utilities/Open Board Editor", false, 29)]
        public static void OpenBoardEditorFromGameObject()
        {
            OpenBoardEditor();
        }

        public static void RegisterUtilityDescriptor()
        {
            if (_registeringDescriptor)
                return;

            _registeringDescriptor = true;
            try
            {
                if (PungentUtilityRegistry.Find(PungentBoardProvider.UtilityId) == null)
                {
                    PungentUtilityDescriptor descriptor = new PungentUtilityDescriptor(
                        PungentBoardProvider.UtilityId,
                        "Board / Whiteboard / Node Graph",
                        "Board / Graph",
                        "Spatial board editor for planning cards, authoring references, relationship edges, groups, and whiteboard-style node maps.",
                        BoardEditorPath,
                        typeof(PungentBoardEditorWindow).FullName,
                        new[] { "board", "whiteboard", "node graph", "graph", "canvas", "planning", "dependencies", "quest flow", "audit follow-up", "authoring references", "feature test checklist" },
                        supportsSceneOverlay: false,
                        supportsContextMenu: true,
                        supportsSelection: false,
                        openAction: PungentBoardEditorWindow.Open,
                        lab: PungentUtilityCategories.DocumentationPlanning,
                        module: "Board / Graph",
                        sortOrder: 46,
                        isLabHub: false,
                        packageStatus: PungentUtilityPackageStatus.InProgress,
                        relatedUtilityIds: new[] { "utilities-browser", FeatureChecklistUtilityId, "tooltip-notes", "documentation-links", "coverage-matrix", "token-validator", "help-browser" },
                        categoryFacets: new[] { PungentUtilityCategories.DocumentationPlanning, PungentUtilityCategories.Audit, PungentUtilityCategories.ScanningCoverage, PungentUtilityCategories.Creation },
                        itemKind: PungentUtilityItemKind.Utility,
                        visibility: PungentUtilityVisibility.Visible,
                        canRunAction: null,
                        disabledReasonProvider: null,
                        packageId: PungentBoardProvider.PackageId,
                        packageDisplayName: PungentBoardProvider.PackageDisplayName,
                        packageTier: PungentUtilityPackageTier.Extension,
                        requiredPackageIds: new[] { PungentAuthoringPackageCapabilities.Core, PungentAuthoringPackageCapabilities.AuthoringCore },
                        optionalPackageIds: new[] { PungentAuthoringPackageCapabilities.RichDocuments, PungentAuthoringPackageCapabilities.ProjectAudit, PungentAuthoringPackageCapabilities.DataSheet, PungentAuthoringPackageCapabilities.TokenSystem, PungentAuthoringPackageCapabilities.HelpDocumentation },
                        providedCapabilities: new[] { PungentAuthoringPackageCapabilities.BoardWhiteboard, "board-editor", "whiteboard-canvas", "node-graph-authoring", "board-authoring-provider" },
                        consumedCapabilities: new[] { PungentAuthoringPackageCapabilities.AuthoringCore, "authoring-provider-registry", "utility-registry" },
                        extensionPointsProvided: new[] { "board-document-provider", "board-editor-window", "board-template-source", "authoring-reference-graph-surface" },
                        extensionPointsConsumed: new[] { "authoring-provider-registry", "utilities-browser-launcher", "authoring-preview-provider" },
                        missingDependencyMessage: "Board Editor requires the Authoring Data Foundation. Rich documents, data sheets, audit, token, and help links degrade gracefully when those providers are absent.",
                        fallbackBehavior: "Board documents remain visible in JSON storage; missing linked providers show readable fallback states and keep node cards intact.",
                        installHint: "Enable the Board / Graph / Visualization extension package.",
                        minimumCompatibleVersion: "1.0.0",
                        documentationTopicId: "board-whiteboard-node-graph");

                    PungentUtilityRegistry.Register(descriptor);
                }

            }
            finally
            {
                _registeringDescriptor = false;
            }
        }
    }
#endif
}
