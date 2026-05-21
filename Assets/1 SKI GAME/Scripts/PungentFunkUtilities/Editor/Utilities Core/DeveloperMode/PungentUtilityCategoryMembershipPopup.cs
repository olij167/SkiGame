using PungentFunk.Utilities.Editor.Developer;
using PungentFunk.Utilities.Editor.Theme;

namespace PungentFunk.Utilities.Editor.Core
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Developer-mode category reference, category appearance override, and bulk membership editor.
    /// Edits project-local overrides only; factory registry defaults remain unchanged.
    /// </summary>
    public sealed class PungentUtilityCategoryMembershipPopup : EditorWindow
    {
        private const string PrefPrefix = "PungentFunkUtilities.CategoryMembership.";
        private const string PrefSelectedCategory = PrefPrefix + "SelectedCategory";
        private const string PrefSearch = PrefPrefix + "Search";
        private const string PrefFilter = PrefPrefix + "Filter";
        private const string PrefIncludeHidden = PrefPrefix + "IncludeHidden";
        private const string PrefScrollX = PrefPrefix + "ScrollX";
        private const string PrefScrollY = PrefPrefix + "ScrollY";
        private const string PrefCategoryScrollX = PrefPrefix + "CategoryScrollX";
        private const string PrefCategoryScrollY = PrefPrefix + "CategoryScrollY";

        private enum MembershipFilter
        {
            All,
            Included,
            NotIncluded,
            EditableOnly,
            PrimaryLocked
        }

        private string _selectedCategory;
        private string _search = string.Empty;
        private MembershipFilter _filter = MembershipFilter.All;
        private bool _includeHidden;
        private Vector2 _scroll;
        private Vector2 _categoryScroll;
        private string _status = "Select a category to review utility membership.";

        public static void Open(string categoryId = null)
        {
            if (!PungentDeveloperMode.Available || !PungentDeveloperMode.Enabled)
            {
                EditorUtility.DisplayDialog("Category Reference", "Category reference and membership editing is only available when PungentFunk developer mode is enabled.", "OK");
                return;
            }

            PungentUtilityCategoryMembershipPopup window = GetWindow<PungentUtilityCategoryMembershipPopup>(true, "Category Reference", true);
            window.minSize = new Vector2(780f, 580f);
            window.Load(categoryId);
            window.ShowUtility();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Category Reference");
            PungentUtilityRegistry.Changed -= HandleRegistryChanged;
            PungentUtilityRegistry.Changed += HandleRegistryChanged;
            PungentUtilityCategoryOverrides.Changed -= HandleCategoryOverridesChanged;
            PungentUtilityCategoryOverrides.Changed += HandleCategoryOverridesChanged;
        }

        private void OnDisable()
        {
            PungentUtilityRegistry.Changed -= HandleRegistryChanged;
            PungentUtilityCategoryOverrides.Changed -= HandleCategoryOverridesChanged;
            EditorPrefs.SetString(PrefSelectedCategory, _selectedCategory ?? string.Empty);
            EditorPrefs.SetString(PrefSearch, _search ?? string.Empty);
            EditorPrefs.SetInt(PrefFilter, (int)_filter);
            EditorPrefs.SetBool(PrefIncludeHidden, _includeHidden);
            UtilityWindowPrefs.SetFloat(PrefScrollX, _scroll.x);
            UtilityWindowPrefs.SetFloat(PrefScrollY, _scroll.y);
            UtilityWindowPrefs.SetFloat(PrefCategoryScrollX, _categoryScroll.x);
            UtilityWindowPrefs.SetFloat(PrefCategoryScrollY, _categoryScroll.y);
        }

        private void HandleRegistryChanged()
        {
            Repaint();
        }

        private void HandleCategoryOverridesChanged()
        {
            Repaint();
        }

        private void Load(string categoryId)
        {
            string fallback = EditorPrefs.GetString(PrefSelectedCategory, PungentUtilityCategories.Core);
            _selectedCategory = PungentUtilityCategories.Normalize(string.IsNullOrWhiteSpace(categoryId) ? fallback : categoryId);
            _search = EditorPrefs.GetString(PrefSearch, string.Empty);
            _filter = (MembershipFilter)Mathf.Clamp(EditorPrefs.GetInt(PrefFilter, 0), 0, Enum.GetValues(typeof(MembershipFilter)).Length - 1);
            _includeHidden = EditorPrefs.GetBool(PrefIncludeHidden, false);
            _scroll = new Vector2(UtilityWindowPrefs.GetFloat(PrefScrollX, 0f), UtilityWindowPrefs.GetFloat(PrefScrollY, 0f));
            _categoryScroll = new Vector2(UtilityWindowPrefs.GetFloat(PrefCategoryScrollX, 0f), UtilityWindowPrefs.GetFloat(PrefCategoryScrollY, 0f));
        }

        private void OnGUI()
        {
            if (!PungentDeveloperMode.Available || !PungentDeveloperMode.Enabled)
            {
                UtilityWindowTheme.Header("Category Reference", "Developer-only category metadata tools.", "Developer mode is not enabled.");
                EditorGUILayout.HelpBox("Enable PungentFunk Developer Mode before editing category metadata or membership.", MessageType.Warning);
                return;
            }

            UtilityWindowTheme.Header("Category Reference", "Map display names to stable category IDs, edit category appearance, and manage utility membership.", _status);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawCategoryReferencePanel(GUILayout.Width(Mathf.Clamp(position.width * 0.46f, 340f, 470f)));
                DrawMembershipPanel();
            }
        }

        private void DrawCategoryReferencePanel(params GUILayoutOption[] options)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple), options))
            {
                UtilityWindowTheme.SectionTitle("Category IDs", UtilityWindowTheme.Purple, "reference + appearance");
                EditorGUILayout.HelpBox("Display names and colours can be overridden here. Category IDs are stable package keys and remain locked so existing registry defaults, facets, preferences, and saved overrides do not become ambiguous.", MessageType.Info);

                _categoryScroll = EditorGUILayout.BeginScrollView(_categoryScroll);
                string[] categoryIds = GetAllCategoryIds();
                for (int i = 0; i < categoryIds.Length; i++)
                    DrawCategoryReferenceRow(categoryIds[i]);
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawCategoryReferenceRow(string categoryId)
        {
            bool selected = string.Equals(_selectedCategory, categoryId, StringComparison.OrdinalIgnoreCase);
            bool hasOverride = PungentUtilityCategories.HasAppearanceOverride(categoryId);
            string factoryName = PungentUtilityCategories.GetFactoryDisplayName(categoryId);
            Color factoryTint = PungentUtilityCategories.GetFactoryTint(categoryId);
            PungentUtilityCategoryOverrides.CategoryOverride editable = PungentUtilityCategoryOverrides.instance.CreateEditableCopy(categoryId, factoryName, factoryTint);
            Color tint = PungentUtilityCategories.GetTint(categoryId);
            string displayName = PungentUtilityCategories.GetDisplayName(categoryId);

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(selected ? tint : UtilityWindowTheme.Neutral, selected ? 0.24f : 0.08f, 0.04f, 4, 2)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    bool nextOverrideTint = EditorGUILayout.Toggle(new GUIContent(string.Empty, "Enable or disable this category's colour override."), editable.overrideTint, GUILayout.Width(18f));
                    Color nextTint = EditorGUILayout.ColorField(new GUIContent(string.Empty, "Project-local category colour override."), editable.overrideTint ? editable.tint : tint, false, false, false, GUILayout.Width(42f));
                    if (EditorGUI.EndChangeCheck())
                    {
                        editable.overrideTint = nextOverrideTint;
                        editable.tint = nextTint;
                        SaveCategoryOverride(editable, "Set Utility Category Colour");
                    }

                    if (GUILayout.Button(new GUIContent(displayName, "Select this category for membership editing."), EditorStyles.miniButton, GUILayout.Height(20f)))
                    {
                        _selectedCategory = categoryId;
                        _status = "Selected " + displayName + ".";
                        GUI.FocusControl(null);
                    }

                    if (hasOverride)
                        UtilityWindowTheme.CountPill("Override", UtilityWindowTheme.Amber, 72f);

                    if (GUILayout.Button(new GUIContent("Copy ID", "Copy the stable category ID to the clipboard."), EditorStyles.miniButton, GUILayout.Width(58f), GUILayout.Height(20f)))
                    {
                        EditorGUIUtility.systemCopyBuffer = categoryId;
                        _status = "Copied category ID: " + categoryId;
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(new GUIContent("ID", "Stable package category ID. This is intentionally not project-overridable."), UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(22f));
                    EditorGUILayout.SelectableLabel(categoryId, EditorStyles.textField, GUILayout.Height(18f));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    bool nextOverrideName = EditorGUILayout.Toggle(new GUIContent("Display name override", "Enable a project-local display name for this category."), editable.overrideDisplayName, GUILayout.Width(150f));
                    using (new EditorGUI.DisabledScope(!nextOverrideName))
                    {
                        string nextName = EditorGUILayout.DelayedTextField(editable.overrideDisplayName ? editable.displayName : displayName);
                        if (EditorGUI.EndChangeCheck())
                        {
                            editable.overrideDisplayName = nextOverrideName;
                            editable.displayName = nextName;
                            SaveCategoryOverride(editable, "Set Utility Category Name");
                        }
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Factory", UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(48f));
                    EditorGUILayout.SelectableLabel(factoryName, EditorStyles.textField, GUILayout.Height(18f));

                    if (GUILayout.Button(new GUIContent("Copy Slug", "Copy a suggested slug based on the current display name. This does not rename the canonical category ID."), EditorStyles.miniButton, GUILayout.Width(72f), GUILayout.Height(18f)))
                    {
                        EditorGUIUtility.systemCopyBuffer = ToKebabCase(displayName);
                        _status = "Copied suggested slug for " + displayName + ".";
                    }
                }

                if (hasOverride)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button(new GUIContent("Reset Category Override", "Remove this category's project-local display-name and colour overrides."), EditorStyles.miniButton, GUILayout.Width(148f), GUILayout.Height(20f)))
                        {
                            Undo.RecordObject(PungentUtilityCategoryOverrides.instance, "Reset Utility Category Override");
                            PungentUtilityCategoryOverrides.instance.RemoveOverride(categoryId);
                            _status = "Reset category override for " + factoryName + ".";
                            GUI.FocusControl(null);
                        }
                    }
                }
            }
        }

        private void DrawMembershipPanel()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                UtilityWindowTheme.SectionTitle("Membership", UtilityWindowTheme.Blue, PungentUtilityCategories.GetDisplayName(_selectedCategory));

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(new GUIContent("Search", "Filter utilities by name, id, module, status, tags, or menu path."), GUILayout.Width(48f));
                    _search = EditorGUILayout.TextField(_search ?? string.Empty, EditorStyles.toolbarSearchField);
                    _filter = (MembershipFilter)EditorGUILayout.EnumPopup(_filter, GUILayout.Width(116f));
                    _includeHidden = GUILayout.Toggle(_includeHidden, new GUIContent("Hidden", "Include utilities hidden from the Utilities Browser."), EditorStyles.toolbarButton, GUILayout.Width(62f));
                }

                EditorGUILayout.HelpBox("Toggling a utility here updates that utility's Category Facets override. Utilities whose primary Area Category matches this category are shown as locked because they belong here through Area Category, not facets.", MessageType.None);

                DrawMembershipSummary();

                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                List<PungentUtilityDescriptor> utilities = GetFilteredUtilities().ToList();
                if (utilities.Count == 0)
                {
                    EditorGUILayout.HelpBox("No utilities match the current search and filter.", MessageType.Info);
                }
                else
                {
                    for (int i = 0; i < utilities.Count; i++)
                        DrawUtilityMembershipRow(utilities[i]);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawMembershipSummary()
        {
            int total = 0;
            int included = 0;
            int primaryLocked = 0;

            foreach (PungentUtilityDescriptor utility in PungentUtilityRegistry.All)
            {
                if (utility == null || (!_includeHidden && !utility.IsVisibleInDefaultBrowser))
                    continue;

                total++;
                if (IsPrimaryLocked(utility, _selectedCategory))
                    primaryLocked++;
                if (utility.HasCategory(_selectedCategory))
                    included++;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                UtilityWindowTheme.CountPill("Total " + total, UtilityWindowTheme.Neutral, 86f);
                UtilityWindowTheme.CountPill("Included " + included, UtilityWindowTheme.Green, 104f);
                UtilityWindowTheme.CountPill("Primary " + primaryLocked, UtilityWindowTheme.Amber, 96f);
                GUILayout.FlexibleSpace();
            }
        }

        private IEnumerable<PungentUtilityDescriptor> GetFilteredUtilities()
        {
            IEnumerable<PungentUtilityDescriptor> source = PungentUtilityRegistry.All.Where(u => u != null);
            if (!_includeHidden)
                source = source.Where(u => u.IsVisibleInDefaultBrowser);

            if (!string.IsNullOrWhiteSpace(_search))
            {
                string q = _search.Trim();
                source = source.Where(u => u.Matches(q) || Contains(u.Id, q));
            }

            source = source.Where(PassesMembershipFilter);

            return source
                .OrderByDescending(u => IsPrimaryLocked(u, _selectedCategory))
                .ThenByDescending(u => u.HasCategory(_selectedCategory))
                .ThenBy(u => PungentUtilityCategories.SortKey(u.AreaCategory))
                .ThenBy(u => u.DisplayName, StringComparer.OrdinalIgnoreCase);
        }

        private bool PassesMembershipFilter(PungentUtilityDescriptor utility)
        {
            bool included = utility.HasCategory(_selectedCategory);
            bool primaryLocked = IsPrimaryLocked(utility, _selectedCategory);

            switch (_filter)
            {
                case MembershipFilter.Included:
                    return included;
                case MembershipFilter.NotIncluded:
                    return !included;
                case MembershipFilter.EditableOnly:
                    return !primaryLocked;
                case MembershipFilter.PrimaryLocked:
                    return primaryLocked;
                default:
                    return true;
            }
        }

        private void DrawUtilityMembershipRow(PungentUtilityDescriptor utility)
        {
            bool primaryLocked = IsPrimaryLocked(utility, _selectedCategory);
            bool included = utility.HasCategory(_selectedCategory);
            bool overrideActive = PungentUtilityRegistry.HasMetadataOverride(utility.Id);

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(included ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, included ? 0.18f : 0.07f, 0.04f, 4, 2)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(primaryLocked))
                    {
                        EditorGUI.BeginChangeCheck();
                        bool next = EditorGUILayout.ToggleLeft(new GUIContent(utility.DisplayName, utility.Description), included, GUILayout.MinWidth(190f));
                        if (EditorGUI.EndChangeCheck() && next != included)
                            SetUtilityMembership(utility, _selectedCategory, next);
                    }

                    if (primaryLocked)
                        UtilityWindowTheme.CountPill("Primary", UtilityWindowTheme.Amber, 70f);
                    else if (included)
                        UtilityWindowTheme.CountPill("Facet", UtilityWindowTheme.Green, 58f);
                    else
                        UtilityWindowTheme.CountPill("Off", UtilityWindowTheme.Neutral, 46f);

                    if (overrideActive)
                        UtilityWindowTheme.CountPill("Override", UtilityWindowTheme.Amber, 74f);

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button(new GUIContent("Edit", "Open the unified Developer Tools metadata tab for this utility."), EditorStyles.miniButton, GUILayout.Width(44f), GUILayout.Height(20f)))
                        PungentUtilityDeveloperToolsWindow.OpenMetadata(utility.Id);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("ID", UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(22f));
                    EditorGUILayout.SelectableLabel(utility.Id, EditorStyles.textField, GUILayout.Height(18f));
                    GUILayout.Label("Area", UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(32f));
                    EditorGUILayout.SelectableLabel(utility.AreaCategory, EditorStyles.textField, GUILayout.Height(18f));
                }

                if (primaryLocked)
                    GUILayout.Label("Membership comes from Area Category. Change Area Category in this utility's metadata editor if it should move categories.", UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void SetUtilityMembership(PungentUtilityDescriptor descriptor, string categoryId, bool shouldInclude)
        {
            if (descriptor == null || string.IsNullOrWhiteSpace(categoryId))
                return;

            string normalizedCategory = PungentUtilityCategories.Normalize(categoryId);
            if (IsPrimaryLocked(descriptor, normalizedCategory))
            {
                _status = descriptor.DisplayName + " uses " + PungentUtilityCategories.GetDisplayName(normalizedCategory) + " as its primary Area Category.";
                return;
            }

            PungentUtilityMetadataOverrides.UtilityMetadataOverride metadata = PungentUtilityRegistry.CreateEditableMetadataOverride(descriptor.Id);
            if (metadata == null)
                return;

            List<string> facets = (descriptor.CategoryFacets ?? Array.Empty<string>())
                .Select(PungentUtilityCategories.Normalize)
                .Where(c => !string.IsNullOrWhiteSpace(c) && !string.Equals(c, PungentUtilityCategories.Other, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            bool currentlyFacet = facets.Any(c => string.Equals(c, normalizedCategory, StringComparison.OrdinalIgnoreCase));
            if (shouldInclude && !currentlyFacet)
                facets.Add(normalizedCategory);
            else if (!shouldInclude)
                facets.RemoveAll(c => string.Equals(c, normalizedCategory, StringComparison.OrdinalIgnoreCase));

            metadata.overrideCategoryFacets = true;
            metadata.categoryFacets = PungentUtilityCategories.NormalizeMany(facets);

            Undo.RecordObject(PungentUtilityMetadataOverrides.instance, "Set Utility Category Membership");
            PungentUtilityRegistry.SaveMetadataOverride(metadata);
            _status = (shouldInclude ? "Added " : "Removed ") + descriptor.DisplayName + (shouldInclude ? " to " : " from ") + PungentUtilityCategories.GetDisplayName(normalizedCategory) + ".";
        }

        private void SaveCategoryOverride(PungentUtilityCategoryOverrides.CategoryOverride categoryOverride, string undoName)
        {
            if (categoryOverride == null || string.IsNullOrWhiteSpace(categoryOverride.categoryId))
                return;

            if (categoryOverride.overrideDisplayName && string.IsNullOrWhiteSpace(categoryOverride.displayName))
            {
                categoryOverride.overrideDisplayName = false;
                categoryOverride.displayName = PungentUtilityCategories.GetFactoryDisplayName(categoryOverride.categoryId);
            }

            Undo.RecordObject(PungentUtilityCategoryOverrides.instance, undoName);
            PungentUtilityCategoryOverrides.instance.SetOverride(categoryOverride);
            _status = "Updated category override for " + PungentUtilityCategories.GetDisplayName(categoryOverride.categoryId) + ".";
        }

        private static bool IsPrimaryLocked(PungentUtilityDescriptor descriptor, string categoryId)
        {
            return descriptor != null && string.Equals(descriptor.AreaCategory, PungentUtilityCategories.Normalize(categoryId), StringComparison.OrdinalIgnoreCase);
        }

        private static string[] GetAllCategoryIds()
        {
            return PungentUtilityCategories.CurrentCategories
                .Concat(new[] { PungentUtilityCategories.Other })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(PungentUtilityCategories.SortKey)
                .ThenBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static bool Contains(string value, string query)
        {
            return !string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(query) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ToKebabCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            List<char> chars = new List<char>(value.Length);
            bool previousDash = false;

            for (int i = 0; i < value.Length; i++)
            {
                char c = char.ToLowerInvariant(value[i]);
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                {
                    chars.Add(c);
                    previousDash = false;
                }
                else if (!previousDash && chars.Count > 0)
                {
                    chars.Add('-');
                    previousDash = true;
                }
            }

            while (chars.Count > 0 && chars[chars.Count - 1] == '-')
                chars.RemoveAt(chars.Count - 1);

            return new string(chars.ToArray());
        }
    }
#endif
}
