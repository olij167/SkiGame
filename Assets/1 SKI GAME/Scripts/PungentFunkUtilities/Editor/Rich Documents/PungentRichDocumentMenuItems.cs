using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.Editor.Authoring;
using PungentFunk.Utilities.Editor.Core;
using UnityEditor;

namespace PungentFunk.Utilities.Editor.RichDocuments
{
#if UNITY_EDITOR
    [InitializeOnLoad]
    public static class PungentRichDocumentMenuItems
    {
        public const string MenuPath = "Tools/PungentFunk Utilities/Documentation & Authoring/Rich Document Editor";

        private static bool _registeringProvider;
        private static bool _registeringDescriptor;

        static PungentRichDocumentMenuItems()
        {
            RegisterProvider();
            RegisterUtilityDescriptor();
            PungentAuthoringProviderRegistry.Changed += RegisterProvider;
            PungentUtilityRegistry.Changed += RegisterUtilityDescriptor;
        }

        [MenuItem(MenuPath, priority = 1710)]
        public static void Open()
        {
            RegisterProvider();
            RegisterUtilityDescriptor();
            PungentUtilityRegistry.Open(PungentRichDocumentProvider.UtilityId);
        }

        public static void RegisterProvider()
        {
            if (_registeringProvider)
                return;

            _registeringProvider = true;
            try
            {
                PungentAuthoringProviderRegistry.Register(new PungentRichDocumentProvider());
            }
            finally
            {
                _registeringProvider = false;
            }
        }

        public static void RegisterUtilityDescriptor()
        {
            if (_registeringDescriptor)
                return;

            _registeringDescriptor = true;
            try
            {
                if (PungentUtilityRegistry.Find(PungentRichDocumentProvider.UtilityId) != null)
                    return;

                PungentUtilityDescriptor descriptor = new PungentUtilityDescriptor(
                    PungentRichDocumentProvider.UtilityId,
                    "Rich Document Editor",
                    "Documentation",
                    "Focused rich document authoring for long-form docs, templates, token-aware copy, and linked legacy-note conversions.",
                    MenuPath,
                    typeof(PungentRichDocumentEditorWindow).FullName,
                    new[] { "rich document", "authoring", "documentation", "templates", "game text", "notes" },
                    supportsSceneOverlay: false,
                    supportsContextMenu: false,
                    supportsSelection: false,
                    openAction: PungentRichDocumentEditorWindow.Open,
                    lab: PungentUtilityCategories.DocumentationPlanning,
                    module: "Rich Documents",
                    sortOrder: 48,
                    isLabHub: false,
                    packageStatus: PungentUtilityPackageStatus.Experimental,
                    relatedUtilityIds: new[] { "tooltip-notes", "help-browser", "documentation-links", "token-validator" },
                    categoryFacets: new[] { PungentUtilityCategories.DocumentationPlanning, PungentUtilityCategories.UI },
                    itemKind: PungentUtilityItemKind.Utility,
                    visibility: PungentUtilityVisibility.Visible,
                    canRunAction: null,
                    disabledReasonProvider: null,
                    packageId: "documentation-authoring",
                    packageDisplayName: PungentRichDocumentProvider.PackageDisplayName,
                    packageTier: PungentUtilityPackageTier.Extension,
                    requiredPackageIds: new[] { PungentAuthoringPackageCapabilities.Core, PungentAuthoringPackageCapabilities.AuthoringCore },
                    optionalPackageIds: new[] { PungentAuthoringPackageCapabilities.NotesBrowser, PungentAuthoringPackageCapabilities.TokenSystem, PungentAuthoringPackageCapabilities.HelpDocumentation },
                    providedCapabilities: new[] { PungentAuthoringPackageCapabilities.RichDocuments, "rich-document-editor", "legacy-note-linked-copy" },
                    consumedCapabilities: new[] { PungentAuthoringPackageCapabilities.AuthoringCore, PungentAuthoringPackageCapabilities.NotesBrowser, PungentAuthoringPackageCapabilities.TokenSystem, PungentAuthoringPackageCapabilities.HelpDocumentation },
                    extensionPointsProvided: new[] { "rich-document-authoring", "rich-document-provider", "game-text-document-conventions" },
                    extensionPointsConsumed: new[] { "authoring-provider-registry", "legacy-note-conversion", "token-validator-handoff", "help-browser-handoff" },
                    missingDependencyMessage: "Rich Document Editor requires the Authoring Data Foundation. Token, Help, and Documentation Links features degrade when those utilities are absent.",
                    fallbackBehavior: "Documents remain in JSON storage; browser/provider metadata and raw body text stay readable even when optional integrations are missing.",
                    installHint: "Install or enable the Documentation & Authoring extension package.",
                    minimumCompatibleVersion: "1.0.0",
                    documentationTopicId: "rich-document-editor");

                PungentUtilityRegistry.Register(descriptor);
            }
            finally
            {
                _registeringDescriptor = false;
            }
        }
    }
#endif
}
