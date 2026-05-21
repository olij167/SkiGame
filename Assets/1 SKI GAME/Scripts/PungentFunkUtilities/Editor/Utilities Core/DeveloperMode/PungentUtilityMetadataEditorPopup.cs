using PungentFunk.Utilities.Editor.Developer;
using PungentFunk.Utilities.Editor.Theme;

namespace PungentFunk.Utilities.Editor.Core
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Developer-mode utility metadata override editor. Edits project-local card metadata only; it does not alter menu item declarations.
    /// </summary>
    public sealed class PungentUtilityMetadataEditorPopup : EditorWindow
    {
        private const string PrefPrefix = "PungentFunkUtilities.MetadataEditor.";
        private const string PrefScrollX = PrefPrefix + "ScrollX";
        private const string PrefScrollY = PrefPrefix + "ScrollY";

        private string _utilityId;
        private PungentUtilityDescriptor _descriptor;
        private PungentUtilityMetadataOverrides.UtilityMetadataOverride _metadata;
        private Vector2 _scroll;
        private string _tagsText = string.Empty;
        private string _relatedText = string.Empty;
        private string _facetsText = string.Empty;
        private string _providedCapabilitiesText = string.Empty;
        private string _requiredPackageIdsText = string.Empty;
        private string _optionalPackageIdsText = string.Empty;
        private string _status = "Ready.";

        public static void Open(string utilityId)
        {
            if (!PungentDeveloperMode.Available || !PungentDeveloperMode.Enabled)
            {
                EditorUtility.DisplayDialog("Utility Metadata", "Metadata editing is only available when PungentFunk developer mode is enabled.", "OK");
                return;
            }

            PungentUtilityMetadataEditorPopup window = GetWindow<PungentUtilityMetadataEditorPopup>(true, "Utility Metadata", true);
            window.minSize = new Vector2(560f, 560f);
            window.Load(utilityId);
            window.ShowUtility();
        }

        public static bool TryOpenForWindow(EditorWindow owner)
        {
            if (owner == null)
                return false;

            PungentUtilityDescriptor descriptor = PungentUtilityRegistry.FindForWindow(owner);
            if (descriptor == null)
            {
                EditorUtility.DisplayDialog(
                    "Utility Metadata",
                    "No registered PungentUtilityRegistry descriptor was found for this window type:\n\n" + owner.GetType().FullName,
                    "OK");
                return false;
            }

            Open(descriptor.Id);
            return true;
        }

        public static bool TryOpenForWindow<TWindow>() where TWindow : EditorWindow
        {
            PungentUtilityDescriptor descriptor = PungentUtilityRegistry.FindByWindowType(typeof(TWindow));
            if (descriptor == null)
            {
                EditorUtility.DisplayDialog(
                    "Utility Metadata",
                    "No registered PungentUtilityRegistry descriptor was found for this window type:\n\n" + typeof(TWindow).FullName,
                    "OK");
                return false;
            }

            Open(descriptor.Id);
            return true;
        }

        public static void DrawDeveloperMetadataButton(EditorWindow owner, string label = "Edit Card", float width = 92f)
        {
            if (!PungentDeveloperMode.Available || !PungentDeveloperMode.Enabled || owner == null)
                return;

            if (GUILayout.Button(new GUIContent(label, "Open the unified Developer Tools metadata tab for this window."), EditorStyles.miniButton, GUILayout.Width(width), GUILayout.Height(22f)))
            {
                PungentUtilityDescriptor descriptor = PungentUtilityRegistry.FindForWindow(owner);
                if (descriptor != null)
                    PungentUtilityDeveloperToolsWindow.OpenMetadata(descriptor.Id);
                else
                    TryOpenForWindow(owner);
            }
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Utility Metadata");
            _scroll = new Vector2(UtilityWindowPrefs.GetFloat(PrefScrollX, 0f), UtilityWindowPrefs.GetFloat(PrefScrollY, 0f));
            PungentUtilityRegistry.Changed -= HandleRegistryChanged;
            PungentUtilityRegistry.Changed += HandleRegistryChanged;
        }

        private void OnDisable()
        {
            UtilityWindowPrefs.SetFloat(PrefScrollX, _scroll.x);
            UtilityWindowPrefs.SetFloat(PrefScrollY, _scroll.y);
            PungentUtilityRegistry.Changed -= HandleRegistryChanged;
        }

        private void HandleRegistryChanged()
        {
            if (!string.IsNullOrWhiteSpace(_utilityId))
                Load(_utilityId);
            Repaint();
        }

        private void Load(string utilityId)
        {
            _utilityId = utilityId ?? string.Empty;
            _descriptor = PungentUtilityRegistry.Find(_utilityId);
            _metadata = PungentUtilityMetadataOverrides.instance.CreateEditableCopy(_descriptor);

            if (_metadata != null)
            {
                _tagsText = FormatList(_metadata.tags);
                _relatedText = FormatList(_metadata.relatedUtilityIds);
                _facetsText = FormatList(_metadata.categoryFacets);
                _providedCapabilitiesText = FormatList(_metadata.providedCapabilities);
                _requiredPackageIdsText = FormatList(_metadata.requiredPackageIds);
                _optionalPackageIdsText = FormatList(_metadata.optionalPackageIds);
                _status = PungentUtilityRegistry.HasMetadataOverride(_utilityId) ? "Loaded existing project override." : "Editing factory-default metadata.";
            }
            else
            {
                _tagsText = string.Empty;
                _relatedText = string.Empty;
                _facetsText = string.Empty;
                _providedCapabilitiesText = string.Empty;
                _requiredPackageIdsText = string.Empty;
                _optionalPackageIdsText = string.Empty;
                _status = "Could not find utility descriptor.";
            }
        }

        private void OnGUI()
        {
            if (!PungentDeveloperMode.Available || !PungentDeveloperMode.Enabled)
            {
                UtilityWindowTheme.Header("Utility Metadata", "Developer-only metadata overrides.", "Developer mode is not enabled.");
                EditorGUILayout.HelpBox("Enable PungentFunk developer mode before editing utility card metadata.", MessageType.Warning);
                return;
            }

            if (_descriptor == null || _metadata == null)
            {
                UtilityWindowTheme.Header("Utility Metadata", "Developer-only metadata overrides.", _status);
                EditorGUILayout.HelpBox("No utility is selected or the descriptor no longer exists.", MessageType.Warning);
                return;
            }

            UtilityWindowTheme.Header("Utility Metadata", "Edit project-local utility card metadata overrides.", _descriptor.DisplayName + " · " + _status);
            DrawIdentityPanel();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawCoreMetadataPanel();
            DrawCapabilityPanel();
            DrawPackageDistributionPanel();
            DrawRelationshipPanel();
            DrawVisibilityPanel();
            EditorGUILayout.EndScrollView();

            DrawFooterActions();
        }

        private void DrawIdentityPanel()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("ID", GUILayout.Width(96f));
                    EditorGUILayout.SelectableLabel(_descriptor.Id, EditorStyles.textField, GUILayout.Height(18f));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Window Type", GUILayout.Width(96f));
                    EditorGUILayout.SelectableLabel(_descriptor.WindowTypeName, EditorStyles.textField, GUILayout.Height(18f));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill(_descriptor.CanRun ? "Can Run" : "Unavailable", _descriptor.CanRun ? UtilityWindowTheme.Green : UtilityWindowTheme.Red, 96f);
                    UtilityWindowTheme.CountPill(_descriptor.ItemKind.ToString(), _descriptor.IsAction ? UtilityWindowTheme.Purple : UtilityWindowTheme.Cyan, 88f);
                    UtilityWindowTheme.CountPill(_descriptor.Visibility.ToString(), GetVisibilityTint(_descriptor.Visibility), 104f);
                    if (PungentUtilityRegistry.HasMetadataOverride(_descriptor.Id))
                        UtilityWindowTheme.CountPill("Override Active", UtilityWindowTheme.Amber, 108f);
                    else
                        UtilityWindowTheme.CountPill("Factory Default", UtilityWindowTheme.Neutral, 108f);
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawCoreMetadataPanel()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                UtilityWindowTheme.SectionTitle("Card Text", UtilityWindowTheme.Blue, "display metadata");
                DrawStringOverride("Display Name", ref _metadata.overrideDisplayName, ref _metadata.displayName, "User-facing card/window title.");
                DrawStringOverride("Utility Type", ref _metadata.overrideUtilityType, ref _metadata.utilityType, "Granular workflow/type label used by browser filters.");
                DrawCategoryOverride("Area Category", ref _metadata.overrideAreaCategory, ref _metadata.areaCategory, "Broad product category used by the browser sidebar.");
                DrawStringOverride("Module", ref _metadata.overrideModule, ref _metadata.module, "Sub-area shown on cards and details.");
                DrawDescriptionOverride();
                DrawStringOverride("Menu Path", ref _metadata.overrideMenuPath, ref _metadata.menuPath, "Displayed/searchable menu metadata only. This does not create or move Unity menu items.");
                DrawStatusOverride();
                DrawIntOverride("Sort Order", ref _metadata.overrideSortOrder, ref _metadata.sortOrder, "Order within category/type groups.");
                DrawListOverride("Tags", ref _metadata.overrideTags, ref _tagsText, "Search tags. Separate with commas or new lines.");
            }
        }

        private void DrawCapabilityPanel()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                UtilityWindowTheme.SectionTitle("Capabilities", UtilityWindowTheme.Teal, "card badges");
                DrawBoolOverride("Supports Scene Overlay", ref _metadata.overrideSupportsSceneOverlay, ref _metadata.supportsSceneOverlay, "Show the Scene Overlay capability badge.");
                DrawBoolOverride("Supports Context Menu", ref _metadata.overrideSupportsContextMenu, ref _metadata.supportsContextMenu, "Show the Context capability badge.");
                DrawBoolOverride("Supports Selection", ref _metadata.overrideSupportsSelection, ref _metadata.supportsSelection, "Show the Selection capability badge.");
                DrawBoolOverride("Workspace / Lab Hub", ref _metadata.overrideIsLabHub, ref _metadata.isLabHub, "Mark as a primary workspace/lab hub.");
            }
        }

        private void DrawPackageDistributionPanel()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan)))
            {
                UtilityWindowTheme.SectionTitle("Package & Distribution", UtilityWindowTheme.Cyan, PungentUtilityPackageCatalog.ResolveAvailability(_descriptor).ToString());

                DrawStringOverride("Package ID", ref _metadata.overridePackageId, ref _metadata.packageId, "Stable package ID that owns this utility.");
                DrawStringOverride("Package Display Name", ref _metadata.overridePackageDisplayName, ref _metadata.packageDisplayName, "User-facing package name.");
                DrawEnumOverride("Package Tier", ref _metadata.overridePackageTier, ref _metadata.packageTier, "Core, Extension, Bridge, Bundle, Project Adapter, or Internal.");
                DrawStringOverride("Package Manager ID", ref _metadata.overridePackageManagerId, ref _metadata.packageManagerId, "Optional UPM package ID or scoped identifier.");
                DrawStringOverride("Asset Store URL", ref _metadata.overrideAssetStoreUrl, ref _metadata.assetStoreUrl, "Optional absolute URL for package information.");
                DrawStringOverride("Import Package Path", ref _metadata.overrideImportPackagePath, ref _metadata.importPackagePath, "Optional package-specific .unitypackage path.");
                DrawStringOverride("Install Hint", ref _metadata.overrideInstallHint, ref _metadata.installHint, "Fallback install instructions shown when routing is missing.");
                DrawStringOverride("Missing Dependency", ref _metadata.overrideMissingDependencyMessage, ref _metadata.missingDependencyMessage, "Message shown when package dependencies are missing.");
                DrawListOverride("Provided Capabilities", ref _metadata.overrideProvidedCapabilities, ref _providedCapabilitiesText, "Capability tags this package/utility provides. Separate with commas or new lines.");
                DrawListOverride("Required Package IDs", ref _metadata.overrideRequiredPackageIds, ref _requiredPackageIdsText, "Required package IDs. Separate with commas or new lines.");
                DrawListOverride("Optional Package IDs", ref _metadata.overrideOptionalPackageIds, ref _optionalPackageIdsText, "Optional package IDs. Separate with commas or new lines.");
                DrawPackageValidation();

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Copy Package ID", "Copy the current package ID."), EditorStyles.miniButton, GUILayout.Width(112f), GUILayout.Height(22f)))
                    {
                        EditorGUIUtility.systemCopyBuffer = PungentUtilityPackageCatalog.NormalizePackageId(_metadata.packageId);
                        _status = "Copied package ID.";
                    }

                    using (new EditorGUI.DisabledScope(!PungentUtilityExternalLinksSettings.IsValidAbsoluteUrl(_metadata.assetStoreUrl)))
                    {
                        if (GUILayout.Button(new GUIContent("Open Asset Store URL", "Open the configured Asset Store URL."), EditorStyles.miniButton, GUILayout.Width(142f), GUILayout.Height(22f)))
                        {
                            if (PungentUtilityExternalLinksSettings.TryOpenUrl(_metadata.assetStoreUrl, "Asset Store", out string message))
                                _status = message;
                            else
                                _status = message;
                        }
                    }

                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawRelationshipPanel()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple)))
            {
                UtilityWindowTheme.SectionTitle("Relationships", UtilityWindowTheme.Purple, "optional links");

                DrawListOverride("Related Utility IDs", ref _metadata.overrideRelatedUtilityIds, ref _relatedText, "Complementary registry IDs. Separate with commas or new lines.");
                DrawRelatedValidation();

                EditorGUILayout.Space(4f);
                DrawCategoryReferenceToolbar();

                DrawListOverride("Category Facets", ref _metadata.overrideCategoryFacets, ref _facetsText, "Additional stable category IDs/facets. Separate with commas or new lines.");
                DrawFacetValidation();
            }
        }

        private void DrawCategoryReferenceToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.10f, 0.04f, 4, 2)))
            {
                GUILayout.Label(new GUIContent("Category IDs", "Display names can be overridden. Facets must use stable category IDs."), UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(82f));

                if (GUILayout.Button(new GUIContent("Reference / Membership", "Open the unified Developer Tools metadata tab focused on category facets."), EditorStyles.miniButton, GUILayout.Width(164f), GUILayout.Height(22f)))
                    PungentUtilityDeveloperToolsWindow.OpenCategory(GetPreferredCategoryForReference());

                if (GUILayout.Button(new GUIContent("Copy IDs", "Copy all known category IDs to the clipboard."), EditorStyles.miniButton, GUILayout.Width(72f), GUILayout.Height(22f)))
                {
                    EditorGUIUtility.systemCopyBuffer = string.Join("\n", GetAllKnownCategoryIds());
                    _status = "Copied category IDs.";
                }

                GUILayout.FlexibleSpace();

                GUILayout.Label("Use IDs, not edited names.", UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void DrawVisibilityPanel()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber)))
            {
                UtilityWindowTheme.SectionTitle("Visibility", UtilityWindowTheme.Amber, "browser state");
                DrawBoolOverride("Show in Utilities Browser", ref _metadata.overrideShowInUtilitiesBrowser, ref _metadata.showInUtilitiesBrowser, "Hide this item from the normal browser while keeping direct registry/menu access intact.");
                DrawEnumOverride("Item Kind", ref _metadata.overrideItemKind, ref _metadata.itemKind, "Utility, callable action, developer/internal tool, or legacy alias.");
                DrawEnumOverride("Visibility", ref _metadata.overrideVisibility, ref _metadata.visibility, "Visible, Hidden, DeveloperOnly, or Archived. Developer filters can reveal hidden/internal/archived items.");

                if (_metadata.overrideMenuPath)
                    EditorGUILayout.HelpBox("Menu Path is metadata only. Real Unity menu items still come from [MenuItem] declarations in menu/access scripts.", MessageType.Info);
            }
        }

        private void DrawFooterActions()
        {
            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.12f, 0.05f, 6, 3)))
            {
                if (GUILayout.Button(new GUIContent("Override All Displayed Fields", "Enable all override toggles for the values currently shown."), EditorStyles.miniButton, GUILayout.Width(176f), GUILayout.Height(24f)))
                    EnableAllOverrides();

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(!PungentUtilityRegistry.HasMetadataOverride(_descriptor.Id)))
                {
                    if (GUILayout.Button(new GUIContent("Reset to Defaults", "Remove this utility's project-local metadata override."), EditorStyles.miniButton, GUILayout.Width(118f), GUILayout.Height(24f)))
                    {
                        if (EditorUtility.DisplayDialog("Reset Utility Metadata", "Remove the metadata override for '" + _descriptor.DisplayName + "' and return to registry defaults?", "Reset", "Cancel"))
                        {
                            PungentUtilityRegistry.ClearMetadataOverride(_descriptor.Id);
                            _status = "Override removed.";
                            Close();
                        }
                    }
                }

                if (GUILayout.Button(new GUIContent("Cancel", "Close without saving."), EditorStyles.miniButton, GUILayout.Width(72f), GUILayout.Height(24f)))
                    Close();

                if (UtilityWindowTheme.TintedButton("Save Override", UtilityWindowTheme.Green, GUILayout.Width(112f), GUILayout.Height(26f)))
                    SaveOverride();
            }
        }

        private void DrawStringOverride(string label, ref bool enabled, ref string value, string tooltip)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                enabled = EditorGUILayout.ToggleLeft(new GUIContent(label, tooltip), enabled, GUILayout.Width(156f));
                using (new EditorGUI.DisabledScope(!enabled))
                    value = EditorGUILayout.TextField(value ?? string.Empty);
            }
        }

        private void DrawIntOverride(string label, ref bool enabled, ref int value, string tooltip)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                enabled = EditorGUILayout.ToggleLeft(new GUIContent(label, tooltip), enabled, GUILayout.Width(156f));
                using (new EditorGUI.DisabledScope(!enabled))
                    value = EditorGUILayout.IntField(value);
            }
        }

        private void DrawBoolOverride(string label, ref bool enabled, ref bool value, string tooltip)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                enabled = EditorGUILayout.ToggleLeft(new GUIContent(label, tooltip), enabled, GUILayout.Width(190f));
                using (new EditorGUI.DisabledScope(!enabled))
                    value = EditorGUILayout.Toggle(value, GUILayout.Width(22f));
                GUILayout.Label(value ? "Enabled" : "Disabled", UtilityWindowTheme.MutedMiniLabelStyle);
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawEnumOverride<TEnum>(string label, ref bool enabled, ref TEnum value, string tooltip) where TEnum : struct
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                enabled = EditorGUILayout.ToggleLeft(new GUIContent(label, tooltip), enabled, GUILayout.Width(190f));
                using (new EditorGUI.DisabledScope(!enabled))
                    value = (TEnum)(object)EditorGUILayout.EnumPopup((Enum)(object)value);
            }
        }

        private static Color GetVisibilityTint(PungentUtilityVisibility visibility)
        {
            switch (visibility)
            {
                case PungentUtilityVisibility.Visible:
                    return UtilityWindowTheme.Green;
                case PungentUtilityVisibility.DeveloperOnly:
                    return UtilityWindowTheme.Cyan;
                case PungentUtilityVisibility.Archived:
                    return UtilityWindowTheme.Amber;
                default:
                    return UtilityWindowTheme.Neutral;
            }
        }

        private void DrawDescriptionOverride()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _metadata.overrideDescription = EditorGUILayout.ToggleLeft(new GUIContent("Description", "One-sentence promise shown on utility cards."), _metadata.overrideDescription, GUILayout.Width(156f));
                using (new EditorGUI.DisabledScope(!_metadata.overrideDescription))
                    _metadata.description = EditorGUILayout.TextArea(_metadata.description ?? string.Empty, GUILayout.MinHeight(48f));
            }
        }

        private void DrawListOverride(string label, ref bool enabled, ref string text, string tooltip)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                enabled = EditorGUILayout.ToggleLeft(new GUIContent(label, tooltip), enabled, GUILayout.Width(156f));
                using (new EditorGUI.DisabledScope(!enabled))
                    text = EditorGUILayout.TextArea(text ?? string.Empty, GUILayout.MinHeight(38f));
            }
        }

        private void DrawCategoryOverride(string label, ref bool enabled, ref string value, string tooltip)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                enabled = EditorGUILayout.ToggleLeft(new GUIContent(label, tooltip), enabled, GUILayout.Width(156f));
                using (new EditorGUI.DisabledScope(!enabled))
                    value = DrawCategoryPopup(value);
            }
        }

        private void DrawStatusOverride()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _metadata.overridePackageStatus = EditorGUILayout.ToggleLeft(new GUIContent("Package Status", "Stable, Experimental, In Progress, Deprecated, or Project Adapter."), _metadata.overridePackageStatus, GUILayout.Width(156f));
                using (new EditorGUI.DisabledScope(!_metadata.overridePackageStatus))
                {
                    string status = PungentUtilityPackageStatus.Normalize(_metadata.packageStatus);
                    string[] statuses = PungentUtilityPackageStatus.All;
                    int index = Mathf.Max(0, Array.IndexOf(statuses, status));
                    int next = EditorGUILayout.Popup(index, statuses);
                    _metadata.packageStatus = statuses[Mathf.Clamp(next, 0, statuses.Length - 1)];
                }
            }
        }

        private string DrawCategoryPopup(string value)
        {
            List<string> ids = PungentUtilityCategories.CurrentCategories.Concat(new[] { PungentUtilityCategories.Other }).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            string normalized = PungentUtilityCategories.Normalize(value);
            if (!ids.Any(id => string.Equals(id, normalized, StringComparison.OrdinalIgnoreCase)))
                ids.Add(normalized);

            string[] labels = ids
    .Select(id => PungentUtilityCategories.GetDisplayName(id) + "  [" + id + "]")
    .ToArray();
            int index = Mathf.Max(0, ids.FindIndex(id => string.Equals(id, normalized, StringComparison.OrdinalIgnoreCase)));
            int next = EditorGUILayout.Popup(index, labels);
            return ids[Mathf.Clamp(next, 0, ids.Count - 1)];
        }

        private void DrawRelatedValidation()
        {
            if (!_metadata.overrideRelatedUtilityIds)
                return;

            string[] relatedIds = ParseList(_relatedText);
            List<string> missing = new List<string>();
            for (int i = 0; i < relatedIds.Length; i++)
            {
                string id = relatedIds[i];
                if (string.Equals(id, _descriptor.Id, StringComparison.OrdinalIgnoreCase))
                {
                    missing.Add(id + " (self-link)");
                    continue;
                }

                if (PungentUtilityRegistry.Find(id) == null)
                    missing.Add(id);
            }

            if (missing.Count > 0)
                EditorGUILayout.HelpBox("Unresolved related utility IDs: " + string.Join(", ", missing.ToArray()), MessageType.Warning);
        }

        private void DrawFacetValidation()
        {
            if (!_metadata.overrideCategoryFacets)
                return;

            string[] facets = ParseList(_facetsText);
            List<string> unknown = new List<string>();

            for (int i = 0; i < facets.Length; i++)
            {
                string normalized = PungentUtilityCategories.Normalize(facets[i]);
                if (!GetAllKnownCategoryIds().Any(id => string.Equals(id, normalized, StringComparison.OrdinalIgnoreCase)))
                    unknown.Add(facets[i]);
            }

            if (unknown.Count > 0)
                EditorGUILayout.HelpBox("Unknown category IDs: " + string.Join(", ", unknown.ToArray()), MessageType.Warning);
        }

        private void DrawPackageValidation()
        {
            string packageId = PungentUtilityPackageCatalog.NormalizePackageId(_metadata.packageId);

            if (_metadata.overridePackageId && string.IsNullOrWhiteSpace(_metadata.packageId))
                EditorGUILayout.HelpBox("Package ID should not be blank.", MessageType.Warning);

            if (_metadata.overrideAssetStoreUrl && !string.IsNullOrWhiteSpace(_metadata.assetStoreUrl) && !PungentUtilityExternalLinksSettings.IsValidAbsoluteUrl(_metadata.assetStoreUrl))
                EditorGUILayout.HelpBox("Asset Store URL must be empty or an absolute http/https URL.", MessageType.Warning);

            if (_metadata.overrideImportPackagePath && !string.IsNullOrWhiteSpace(_metadata.importPackagePath))
            {
                bool isUnityPackage = _metadata.importPackagePath.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase);
                bool exists = File.Exists(_metadata.importPackagePath);
                if (!isUnityPackage)
                    EditorGUILayout.HelpBox("Import package path should point to a .unitypackage file.", MessageType.Warning);
                else if (!exists)
                    EditorGUILayout.HelpBox("Import package path does not currently exist.", MessageType.Warning);
            }

            if (_metadata.overrideRequiredPackageIds)
            {
                string[] required = ParseList(_requiredPackageIdsText);
                if (required.Any(id => string.Equals(PungentUtilityPackageCatalog.NormalizePackageId(id), packageId, StringComparison.OrdinalIgnoreCase)))
                    EditorGUILayout.HelpBox("Required package IDs should not include the utility's own package ID.", MessageType.Warning);
            }
        }

        private void EnableAllOverrides()
        {
            _metadata.overrideDisplayName = true;
            _metadata.overrideUtilityType = true;
            _metadata.overrideAreaCategory = true;
            _metadata.overrideModule = true;
            _metadata.overrideDescription = true;
            _metadata.overrideMenuPath = true;
            _metadata.overrideTags = true;
            _metadata.overrideSortOrder = true;
            _metadata.overrideSupportsSceneOverlay = true;
            _metadata.overrideSupportsContextMenu = true;
            _metadata.overrideSupportsSelection = true;
            _metadata.overrideIsLabHub = true;
            _metadata.overridePackageStatus = true;
            _metadata.overridePackageId = true;
            _metadata.overridePackageDisplayName = true;
            _metadata.overridePackageTier = true;
            _metadata.overrideAssetStoreUrl = true;
            _metadata.overridePackageManagerId = true;
            _metadata.overrideImportPackagePath = true;
            _metadata.overrideInstallHint = true;
            _metadata.overrideMissingDependencyMessage = true;
            _metadata.overrideProvidedCapabilities = true;
            _metadata.overrideRequiredPackageIds = true;
            _metadata.overrideOptionalPackageIds = true;
            _metadata.overrideRelatedUtilityIds = true;
            _metadata.overrideCategoryFacets = true;
            _metadata.overrideShowInUtilitiesBrowser = true;
            _metadata.overrideItemKind = true;
            _metadata.overrideVisibility = true;
            Repaint();
        }

        private void SaveOverride()
        {
            _metadata.tags = ParseList(_tagsText);
            _metadata.packageId = PungentUtilityPackageCatalog.NormalizePackageId(_metadata.packageId);
            _metadata.providedCapabilities = ParseList(_providedCapabilitiesText);
            _metadata.requiredPackageIds = ParseList(_requiredPackageIdsText)
                .Select(PungentUtilityPackageCatalog.NormalizePackageId)
                .Where(id => !string.Equals(id, PungentUtilityPackageCatalog.NormalizePackageId(_metadata.packageId), StringComparison.OrdinalIgnoreCase))
                .ToArray();
            _metadata.optionalPackageIds = ParseList(_optionalPackageIdsText)
                .Select(PungentUtilityPackageCatalog.NormalizePackageId)
                .Where(id => !string.Equals(id, PungentUtilityPackageCatalog.NormalizePackageId(_metadata.packageId), StringComparison.OrdinalIgnoreCase))
                .ToArray();
            _metadata.relatedUtilityIds = ParseList(_relatedText)
                .Where(id => !string.Equals(id, _descriptor.Id, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            _metadata.categoryFacets = ParseList(_facetsText);

            Undo.RecordObject(PungentUtilityMetadataOverrides.instance, "Save Utility Metadata Override");
            PungentUtilityRegistry.SaveMetadataOverride(_metadata);
            _status = "Saved project metadata override.";
            Close();
        }

        private static string FormatList(IEnumerable<string> values)
        {
            if (values == null)
                return string.Empty;

            return string.Join("\n", values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        }

        private static string[] ParseList(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new string[0];

            return text
                .Split(new[] { '\n', '\r', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private string GetPreferredCategoryForReference()
        {
            if (_metadata != null && _metadata.overrideAreaCategory && !string.IsNullOrWhiteSpace(_metadata.areaCategory))
                return PungentUtilityCategories.Normalize(_metadata.areaCategory);

            if (_descriptor != null && !string.IsNullOrWhiteSpace(_descriptor.AreaCategory))
                return PungentUtilityCategories.Normalize(_descriptor.AreaCategory);

            string[] facets = ParseList(_facetsText);
            if (facets.Length > 0)
                return PungentUtilityCategories.Normalize(facets[0]);

            return PungentUtilityCategories.Core;
        }

        private static string[] GetAllKnownCategoryIds()
        {
            return PungentUtilityCategories.CurrentCategories
                .Concat(new[] { PungentUtilityCategories.Other })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(PungentUtilityCategories.SortKey)
                .ThenBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
#endif
}
