using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Debugging;

namespace PungentFunk.Utilities.Editor.Debugging
{
#if UNITY_EDITOR
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    public sealed partial class DebugControlWindow
    {
        // Static toggle, component group, bool toggle, action, and master-control panels.

        private void DrawStaticDebugFields()
        {
            if (!_showStaticDebugFields)
            {
                using (new EditorGUILayout.VerticalScope(_staticPanelStyle))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(new GUIContent("Static Toggles Hidden", "Static debug/log/gizmo bool controls are currently hidden from the popup tray."), _sectionHeaderStyle);
                        GUILayout.FlexibleSpace();
                        if (DrawTintedButton(new GUIContent("Show", "Show static debug/log/gizmo bool controls again."), UtilityWindowTheme.Purple, GUILayout.Width(58f)))
                        {
                            _showStaticDebugFields = true;
                            UtilityWindowPrefs.SetBool(ShowStaticDebugFieldsPrefsKey, true);
                        }
                    }
                }
                return;
            }

            List<StaticDebugFieldInfo> filtered = GetFilteredStaticToggles();

            using (new EditorGUILayout.VerticalScope(_staticPanelStyle))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(new GUIContent("Static Toggles", "Static bool fields that look like debug/log/gizmo controls in scanned assemblies."), _sectionHeaderStyle);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(new GUIContent($"{filtered.Count}/{_staticBoolToggles.Count}", "Visible static toggles / total cached static toggles."), _mutedMiniLabelStyle, GUILayout.Width(62f));
                }

                if (_staticBoolToggles.Count == 0)
                {
                    EditorGUILayout.HelpBox("No static debug/log/gizmo bools are cached yet. Press Refresh to scan project runtime assemblies for static debug toggles.", MessageType.None);
                    return;
                }

                DrawResponsiveCommandRow(
                    () => DrawStaticScopeToggle("All", BoolToggleCategory.Any, 52f),
                    () => DrawStaticScopeToggle("Debug", BoolToggleCategory.Debug, 62f),
                    () => DrawStaticScopeToggle("Logs", BoolToggleCategory.Log, 56f),
                    () => DrawStaticScopeToggle("Gizmos", BoolToggleCategory.Gizmo, 64f));

                if (filtered.Count == 0)
                {
                    EditorGUILayout.HelpBox("Search excludes every static toggle. Clear search or use a type/field/category term that matches a static debug bool.", MessageType.None);
                    return;
                }

                float contentHeight = Mathf.Clamp(34f + filtered.Count * 24f, 70f, 900f);
                if (!_drawingSupportColumn && filtered.Count > 10)
                {
                    float height = GetSupportListHeight(contentHeight, _drawingSupportColumn ? 190f : 240f);
                    _staticScroll = EditorGUILayout.BeginScrollView(_staticScroll, GUILayout.Height(height));
                    try
                    {
                        DrawStaticRows(filtered);
                    }
                    finally
                    {
                        EditorGUILayout.EndScrollView();
                    }
                }
                else
                {
                    DrawStaticRows(filtered);
                }
            }
        }

        private void DrawStaticRows(List<StaticDebugFieldInfo> filtered)
        {
            foreach (StaticDebugFieldInfo info in filtered)
            {
                string label = $"{info.declaringType.Name}.{info.field.Name}";
                BoolToggleCategory category = ClassifyBoolField(info.field);
                bool oldValue = false;
                try { oldValue = (bool)info.field.GetValue(null); }
                catch { }

                using (new EditorGUILayout.VerticalScope(_fieldPanelStyle))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        DrawCategoryPill(category, _drawingSupportColumn ? 58f : 78f);
                        EditorGUILayout.LabelField(new GUIContent(info.field.Name, $"Static toggle {label}. Click On/Off to set this static bool value."), _categoryLabelStyle);
                        using (new GuiBackgroundScope(oldValue ? DebugTint(category) : WithValue(DebugTint(category), 0.55f)))
                        {
                            bool newValue = GUILayout.Toggle(oldValue, new GUIContent(oldValue ? "On" : "Off", $"Set static toggle {label} {(oldValue ? "off" : "on")}."), EditorStyles.miniButton, GUILayout.Width(52f));
                            if (newValue != oldValue)
                            {
                                info.field.SetValue(null, newValue);
                                _status = $"Set {label} = {newValue}";
                            }
                        }
                    }
                    if (_drawingSupportColumn)
                        EditorGUILayout.LabelField(new GUIContent(info.declaringType.Name, label), _pathLabelStyle);
                }
            }
        }

        private void DrawDiscoveredComponentsPanel()
        {
            List<DebugComponentInfo> filtered = GetFilteredComponents();
            int boolCount = filtered.Sum(info => info.boolToggles.Count);
            int actionCount = filtered.Sum(info => info.contextActions.Count);
            bool compactHeader = GetCurrentContentWidth() < 440f;

            GUILayoutOption[] panelOptions = _stretchWorkbench
                ? new[] { GUILayout.ExpandWidth(true), GUILayout.Height(Mathf.Max(260f, _workbenchHeight)) }
                : new[] { GUILayout.ExpandWidth(true) };

            using (new EditorGUILayout.VerticalScope(_componentPanelStyle, panelOptions))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _discoveredComponentsPanelOpen = EditorGUILayout.Foldout(
                        _discoveredComponentsPanelOpen,
                        new GUIContent("Components", "Browse cached scene components and their debug toggles, context actions, and snapshot tools."),
                        true,
                        _sectionHeaderStyle);

                    GUILayout.FlexibleSpace();

                    if (GetCurrentContentWidth() > 520f)
                        EditorGUILayout.LabelField(new GUIContent($"{boolCount} toggles | {actionCount} actions", "Total visible reflected bool toggles and context actions in the current component browser filter."), _mutedMiniLabelStyle, GUILayout.MaxWidth(170f));

                    EditorGUILayout.LabelField(new GUIContent($"{filtered.Count}/{_components.Count} visible", "Visible components / total cached components after search, scope, visibility, and mode filters."), _mutedMiniLabelStyle, GUILayout.Width(104f));

                    if (!compactHeader)
                    {
                        if (DrawTintedButton(new GUIContent("Expand", "Expand all visible component groups."), UtilityWindowTheme.Blue, GUILayout.Width(66f)))
                            SetAllComponentGroupFoldouts(filtered, true);

                        if (DrawTintedButton(new GUIContent("Collapse", "Collapse all visible component groups."), UtilityWindowTheme.Neutral, GUILayout.Width(74f)))
                            SetAllComponentGroupFoldouts(filtered, false);
                    }
                }

                if (!_discoveredComponentsPanelOpen)
                    return;

                if (GetCurrentContentWidth() <= 520f)
                    EditorGUILayout.LabelField(new GUIContent($"{boolCount} toggles | {actionCount} actions", "Total visible reflected bool toggles and context actions in the current component browser filter."), _mutedMiniLabelStyle);

                if (compactHeader)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (DrawTintedButton(new GUIContent("Expand", "Expand all visible component groups."), UtilityWindowTheme.Blue, GUILayout.Width(66f)))
                            SetAllComponentGroupFoldouts(filtered, true);

                        if (DrawTintedButton(new GUIContent("Collapse", "Collapse all visible component groups."), UtilityWindowTheme.Neutral, GUILayout.Width(74f)))
                            SetAllComponentGroupFoldouts(filtered, false);

                        GUILayout.FlexibleSpace();
                    }
                }

                if (filtered.Count == 0)
                {
                    DrawComponentsEmptyState();
                    return;
                }

                if (_stretchWorkbench)
                {
                    // The parent workbench now reserves a concrete height. Use the
                    // remaining vertical space instead of content-derived sizing so
                    // the component list fills the left panel.
                    _componentsScroll = EditorGUILayout.BeginScrollView(
                        _componentsScroll,
                        GUILayout.ExpandWidth(true),
                        GUILayout.ExpandHeight(true));
                    try
                    {
                        DrawComponentGroups(filtered);
                    }
                    finally
                    {
                        EditorGUILayout.EndScrollView();
                    }
                }
                else if (ShouldUseInternalComponentScroll(filtered.Count))
                {
                    float contentHeight = EstimateComponentsContentHeight(filtered);
                    float height = GetComponentListHeight(contentHeight);

                    _componentsScroll = EditorGUILayout.BeginScrollView(
                        _componentsScroll,
                        GUILayout.MinHeight(220f),
                        GUILayout.Height(height));

                    try
                    {
                        DrawComponentGroups(filtered);
                    }
                    finally
                    {
                        EditorGUILayout.EndScrollView();
                    }
                }
                else
                {
                    DrawComponentGroups(filtered);
                }
            }
        }

        private void DrawComponentGroups(List<DebugComponentInfo> filtered)
        {
            if (_groupMode == GroupMode.ComponentType)
                DrawGroupedByComponentType(filtered);
            else
                DrawGroupedBySceneObject(filtered);
        }

        private void SetAllComponentGroupFoldouts(List<DebugComponentInfo> filtered, bool open)
        {
            if (filtered == null)
                return;

            foreach (DebugComponentInfo info in filtered)
            {
                if (info == null || info.component == null)
                    continue;

                SetTypeFoldout(info.component.GetType().FullName, open);
                SetSceneObjectFoldout(info.objectPath, open);
            }
        }

        private void DrawComponentsEmptyState()
        {
            if (_components.Count == 0)
            {
                EditorGUILayout.HelpBox(_scanCacheDirty
                    ? "No cached scene components. Press Refresh to scan the open scene."
                    : "The last scan found no matching scene components. Disable Only debuggable if you want to include components without debug bools or context actions.",
                    MessageType.None);
                if (DrawTintedButton(new GUIContent("Refresh Scene", "Scan the open scene and rebuild the Debug Control Center cache."), UtilityWindowTheme.Amber, GUILayout.Width(112f)))
                    Refresh();
                return;
            }

            if (_selectedHierarchyOnly && (Selection.gameObjects == null || Selection.gameObjects.Length == 0))
            {
                EditorGUILayout.HelpBox("Selected hierarchy only is enabled, but nothing is selected in the Hierarchy. Select a GameObject or disable the filter.", MessageType.Warning);
                return;
            }

            if (_selectedHierarchyOnly)
            {
                EditorGUILayout.HelpBox(_scanCacheDirty
                    ? "Selected hierarchy only is enabled and the scene scan is stale. Press Refresh to rescan the selected hierarchy scope."
                    : "The current hierarchy selection contains no components matching the active filters.",
                    MessageType.None);
                return;
            }

            if (!string.IsNullOrWhiteSpace(_search))
            {
                EditorGUILayout.HelpBox("Search excludes every discovered component, bool field, and context action. Clear search or try a component, object path, field, or action name.", MessageType.None);
                if (DrawTintedButton(new GUIContent("Clear Search", "Clear the search filter."), UtilityWindowTheme.Neutral, GUILayout.Width(96f)))
                {
                    _search = string.Empty;
                    UtilityWindowPrefs.SetString(SearchPrefsKey, _search);
                }
                return;
            }

            EditorGUILayout.HelpBox("No components match the current filters. Try disabling Only debuggable or Include inactive, then press Refresh if the scene changed.", MessageType.None);
        }

        private void DrawScrollingEllipsisGUILayoutLabel(string key, string text, string tooltip, GUIStyle style, float minWidth)
        {
            string safeText = string.IsNullOrEmpty(text) ? "<unnamed>" : text;
            GUIContent content = new GUIContent(safeText, tooltip ?? safeText);
            Rect rect = GUILayoutUtility.GetRect(
                content,
                style,
                GUILayout.MinWidth(minWidth),
                GUILayout.ExpandWidth(true),
                GUILayout.Height(EditorGUIUtility.singleLineHeight + 2f));

            DrawScrollingEllipsisLabel(rect, key, safeText, tooltip ?? safeText, style);
        }

        private void DrawScrollingEllipsisLabel(Rect rect, string key, string text, string tooltip, GUIStyle style)
        {
            if (string.IsNullOrEmpty(text))
                text = "<unnamed>";

            GUIContent fullContent = new GUIContent(text, tooltip ?? text);
            float textWidth = style.CalcSize(fullContent).x;
            if (textWidth <= rect.width)
            {
                GUI.Label(rect, fullContent, style);
                _hoverLabelStartTimes.Remove(key);
                return;
            }

            bool hovering = rect.Contains(Event.current.mousePosition);
            if (!hovering)
            {
                _hoverLabelStartTimes.Remove(key);
                GUI.Label(rect, new GUIContent(Ellipsize(text, style, rect.width), tooltip ?? text), style);
                return;
            }

            if (!_hoverLabelStartTimes.TryGetValue(key, out double startTime))
            {
                startTime = EditorApplication.timeSinceStartup;
                _hoverLabelStartTimes[key] = startTime;
            }

            float maxOffset = Mathf.Max(0f, textWidth - rect.width + 24f);
            float offset = Mathf.Min(maxOffset, (float)((EditorApplication.timeSinceStartup - startTime) * 42f));

            GUI.BeginGroup(rect);
            Rect labelRect = new Rect(-offset, 0f, textWidth + 8f, rect.height);
            GUI.Label(labelRect, fullContent, style);
            GUI.EndGroup();

            RequestRepaintThrottled();
        }

        private static string Ellipsize(string text, GUIStyle style, float width)
        {
            if (string.IsNullOrEmpty(text) || width <= 12f)
                return string.Empty;

            const string suffix = "…";
            if (style.CalcSize(new GUIContent(text)).x <= width)
                return text;

            int low = 0;
            int high = text.Length;
            while (low < high)
            {
                int mid = (low + high + 1) / 2;
                string candidate = text.Substring(0, mid) + suffix;
                if (style.CalcSize(new GUIContent(candidate)).x <= width)
                    low = mid;
                else
                    high = mid - 1;
            }

            return low <= 0 ? suffix : text.Substring(0, low) + suffix;
        }

        private float EstimateComponentsContentHeight(List<DebugComponentInfo> filtered)
        {
            if (filtered == null || filtered.Count == 0)
                return 90f;

            float height = 18f;
            if (_groupMode == GroupMode.ComponentType)
            {
                foreach (var group in filtered.GroupBy(info => info.component.GetType()))
                {
                    string key = group.Key.FullName;
                    height += 34f;
                    if (GetGroupBoolFoldout("__TYPE_FIELDS__" + key, false))
                        height += 34f + group.SelectMany(i => i.boolToggles).GroupBy(f => f.Name).Count() * 24f;
                    if (GetTypeFoldout(key, false))
                        height += group.Count() * 40f;
                }
            }
            else
            {
                foreach (var group in filtered.GroupBy(info => info.objectPath))
                {
                    height += 34f;
                    if (GetGroupBoolFoldout("__SCENE_FIELDS__" + group.Key, false))
                        height += 34f + group.SelectMany(i => i.boolToggles).GroupBy(f => f.Name).Count() * 24f;
                    if (GetSceneObjectFoldout(group.Key, false))
                        height += group.Count() * 40f;
                }
            }

            return Mathf.Clamp(height, 90f, 2400f);
        }

        private void DrawGroupedByComponentType(List<DebugComponentInfo> filtered)
        {
            bool largeListMode = ShouldUseLargeListMode(filtered);
            IEnumerable<IGrouping<Type, DebugComponentInfo>> groups = filtered
                .GroupBy(c => c.component.GetType())
                .OrderBy(g => g.Key.Name);

            foreach (IGrouping<Type, DebugComponentInfo> group in groups)
            {
                List<DebugComponentInfo> groupList = group.ToList();
                string key = group.Key.FullName;
                bool open = GetGroupOpenForLargeList(_typeFoldouts, TypeFoldoutPrefsPrefix, key, largeListMode, true);

                using (new EditorGUILayout.VerticalScope(_fieldPanelStyle))
                {
                    open = DrawGroupHeader(
                        open,
                        group.Key.Name,
                        group.Key.FullName,
                        BuildGroupMetadata(groupList, "instances"),
                        "type:" + key);
                    SetTypeFoldout(key, open);

                    if (!open)
                        continue;

                    DrawGroupCategoryChips(groupList);

                    foreach (DebugComponentInfo info in groupList.OrderBy(i => i.objectPath))
                        DrawComponentInfo(info);

                    DrawGroupPerFieldControls("__TYPE_FIELDS__" + key, groupList);
                }
            }
        }

        private void DrawGroupedBySceneObject(List<DebugComponentInfo> filtered)
        {
            bool largeListMode = ShouldUseLargeListMode(filtered);
            IEnumerable<IGrouping<string, DebugComponentInfo>> groups = filtered
                .GroupBy(c => c.objectPath)
                .OrderBy(g => g.Key);

            foreach (IGrouping<string, DebugComponentInfo> group in groups)
            {
                List<DebugComponentInfo> groupList = group.ToList();
                bool open = GetGroupOpenForLargeList(_sceneObjectFoldouts, SceneObjectFoldoutPrefsPrefix, group.Key, largeListMode, true);

                using (new EditorGUILayout.VerticalScope(_fieldPanelStyle))
                {
                    open = DrawGroupHeader(
                        open,
                        group.Key,
                        group.Key,
                        BuildGroupMetadata(groupList, "components"),
                        "scene:" + group.Key);
                    SetSceneObjectFoldout(group.Key, open);

                    if (!open)
                        continue;

                    DrawGroupCategoryChips(groupList);

                    foreach (DebugComponentInfo info in groupList.OrderBy(i => i.component.GetType().Name))
                        DrawComponentInfo(info);

                    DrawGroupPerFieldControls("__SCENE_FIELDS__" + group.Key, groupList);
                }
            }
        }

        private bool DrawGroupHeader(bool open, string title, string tooltip, string metadata, string labelKey)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                Rect foldoutRect = GUILayoutUtility.GetRect(16f, EditorGUIUtility.singleLineHeight, GUILayout.Width(16f));
                open = EditorGUI.Foldout(foldoutRect, open, GUIContent.none, true);

                DrawScrollingEllipsisGUILayoutLabel(labelKey, title, tooltip, _sectionHeaderStyle, 120f);

                if (GetCurrentContentWidth() > 430f)
                    EditorGUILayout.LabelField(new GUIContent(metadata, metadata), _mutedMiniLabelStyle, GUILayout.MaxWidth(_stretchWorkbench ? 240f : 300f));
            }

            if (GetCurrentContentWidth() <= 430f)
                DrawScrollingEllipsisGUILayoutLabel(labelKey + ":meta", metadata, metadata, _mutedMiniLabelStyle, 120f);

            return open;
        }

        private bool ShouldUseLargeListMode(List<DebugComponentInfo> filtered)
        {
            return _stretchWorkbench && filtered != null && filtered.Count >= 160 && string.IsNullOrWhiteSpace(_search);
        }

        private bool GetGroupOpenForLargeList(Dictionary<string, bool> foldouts, string prefsPrefix, string key, bool largeListMode, bool normalDefault)
        {
            if (!largeListMode)
            {
                if (!foldouts.TryGetValue(key, out bool normalValue))
                {
                    normalValue = UtilityWindowPrefs.GetBool(prefsPrefix + key, normalDefault);
                    foldouts[key] = normalValue;
                }

                return normalValue;
            }

            if (!foldouts.TryGetValue(key, out bool value))
            {
                // Large scenes are much cheaper to scroll when groups start closed.
                // Users can still expand any group, and searches still use normal defaults.
                value = false;
                foldouts[key] = value;
            }

            return value;
        }

        private void DrawComponentInfo(DebugComponentInfo info)
        {
            if (info == null || info.component == null)
                return;

            int id = info.component.GetInstanceID();
            bool open = GetInstanceFoldout(id, false);
            ComponentCapabilityFilterState filters = GetComponentCapabilityFilters(id);

            using (new EditorGUILayout.VerticalScope(_instancePanelStyle))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    Rect foldoutRect = GUILayoutUtility.GetRect(16f, EditorGUIUtility.singleLineHeight, GUILayout.Width(16f));
                    open = EditorGUI.Foldout(foldoutRect, open, GUIContent.none, true);
                    SetInstanceFoldout(id, open);

                    DrawScrollingEllipsisGUILayoutLabel("component:" + id, info.component.GetType().Name, info.component.GetType().FullName, _categoryLabelStyle, 120f);

                    GUILayout.FlexibleSpace();
                    DrawComponentHeaderCapabilityChipRow(info, filters, open);

                    if (DrawTintedButton(new GUIContent("Ping", "Ping this component in the Project/Hierarchy."), UtilityWindowTheme.Blue, GUILayout.Width(44f)))
                        EditorGUIUtility.PingObject(info.component);

                    if (DrawTintedButton(new GUIContent("Select", "Select this component's GameObject in the Hierarchy."), UtilityWindowTheme.Teal, GUILayout.Width(54f)))
                        Selection.activeObject = info.component.gameObject;
                }

                DrawScrollingEllipsisGUILayoutLabel("path:" + id, info.objectPath, info.objectPath, _pathLabelStyle, 100f);

                if (!open)
                    return;

                DrawComponentBulkCapabilityChipRow(info);

                bool targetOpen = GetTargetSectionFoldout(id, false);
                targetOpen = EditorGUILayout.Foldout(targetOpen, new GUIContent("Target", "Show the target component ObjectField for pinging, inspecting, or drag/reference use."), true, _sectionHeaderStyle);
                SetTargetSectionFoldout(id, targetOpen);
                if (targetOpen)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.ObjectField(new GUIContent("Component", "The scene component represented by this debug card."), info.component, typeof(MonoBehaviour), true);
                    }
                }

                if (_showBoolToggles && info.boolToggles.Count > 0)
                {
                    bool boolsOpen = GetBoolSectionFoldout(id, true);
                    int visibleToggleCount = CountVisibleToggles(info, filters);
                    boolsOpen = EditorGUILayout.Foldout(boolsOpen, new GUIContent($"Toggles ({visibleToggleCount}/{info.boolToggles.Count})", "Show reflected bool fields. Header chips filter which toggle categories are visible."), true, _sectionHeaderStyle);
                    SetBoolSectionFoldout(id, boolsOpen);

                    if (boolsOpen)
                        DrawBoolToggles(info, filters);
                }

                if (_showContextActions && info.contextActions.Count > 0)
                {
                    bool actionsOpen = GetActionSectionFoldout(id, true);
                    string actionLabel = filters.actions ? $"Actions ({info.contextActions.Count})" : $"Actions (hidden/{info.contextActions.Count})";
                    actionsOpen = EditorGUILayout.Foldout(actionsOpen, new GUIContent(actionLabel, "Show reflected [ContextMenu] diagnostic actions for this component."), true, _sectionHeaderStyle);
                    SetActionSectionFoldout(id, actionsOpen);

                    if (actionsOpen)
                        DrawContextActions(info, filters);
                }

                if (_showSnapshotButtons)
                    DrawSnapshotTools(info, id);
            }
        }

        private void DrawComponentHeaderCapabilityChipRow(DebugComponentInfo info, ComponentCapabilityFilterState filters, bool interactive)
        {
            if (info == null || filters == null)
                return;

            bool hasToggles = info.boolToggles.Count > 0;
            bool hasDebug = HasCategory(info, BoolToggleCategory.Debug);
            bool hasLogs = HasCategory(info, BoolToggleCategory.Log);
            bool hasGizmos = HasCategory(info, BoolToggleCategory.Gizmo);
            bool hasActions = info.contextActions.Count > 0;
            bool hasOther = HasCategory(info, BoolToggleCategory.Other) || HasCategory(info, BoolToggleCategory.Diagnostic);

            DrawComponentFilterChip("Toggles", hasToggles, interactive, ref filters.toggles, UtilityWindowTheme.Cyan, "Show/hide all toggle rows for this component.");
            DrawComponentFilterChip("Debug", hasDebug, interactive, ref filters.debug, DebugTint(BoolToggleCategory.Debug), "Show/hide Debug toggles for this component.");
            DrawComponentFilterChip("Logs", hasLogs, interactive, ref filters.logs, DebugTint(BoolToggleCategory.Log), "Show/hide log toggles for this component.");
            DrawComponentFilterChip("Gizmos", hasGizmos, interactive, ref filters.gizmos, DebugTint(BoolToggleCategory.Gizmo), "Show/hide gizmo toggles for this component.");
            DrawComponentFilterChip("Actions", hasActions, interactive, ref filters.actions, UtilityWindowTheme.Amber, "Show/hide context actions for this component.");
            DrawComponentFilterChip("Other", hasOther, interactive, ref filters.other, DebugTint(BoolToggleCategory.Other), "Show/hide other diagnostic toggles for this component.");
        }

        private void DrawComponentBulkCapabilityChipRow(DebugComponentInfo info)
        {
            if (info == null || info.component == null)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent("Apply", "Toggle matching fields on this component, or open the actions bulk menu."), _mutedMiniLabelStyle, GUILayout.Width(40f));
                DrawComponentBulkToggleChip("Toggles", info, BoolToggleCategory.Any, UtilityWindowTheme.Cyan, "Toggle all bool fields on this component.");
                DrawComponentBulkToggleChip("Debug", info, BoolToggleCategory.Debug, DebugTint(BoolToggleCategory.Debug), "Toggle all Debug fields on this component.");
                DrawComponentBulkToggleChip("Logs", info, BoolToggleCategory.Log, DebugTint(BoolToggleCategory.Log), "Toggle all log fields on this component.");
                DrawComponentBulkToggleChip("Gizmos", info, BoolToggleCategory.Gizmo, DebugTint(BoolToggleCategory.Gizmo), "Toggle all gizmo fields on this component.");
                DrawComponentActionsMenuChip(info);
                DrawComponentBulkToggleChip("Other", info, BoolToggleCategory.Other, DebugTint(BoolToggleCategory.Other), "Toggle all other diagnostic fields on this component.");
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawComponentFilterChip(string label, bool available, bool interactive, ref bool enabled, Color tint, string tooltip)
        {
            Color chipTint = !available
                ? WithValue(UtilityWindowTheme.Neutral, 0.62f)
                : interactive && !enabled
                    ? WithValue(tint, 0.48f)
                    : tint;

            GUIContent content = new GUIContent(label, available ? tooltip : $"No {label} capability is available on this component.");
            using (new GuiBackgroundScope(chipTint))
            {
                if (interactive && available)
                {
                    EditorGUI.BeginChangeCheck();
                    enabled = GUILayout.Toggle(enabled, content, EditorStyles.toolbarButton, GUILayout.Width(GetCapabilityChipWidth(label)));
                    if (EditorGUI.EndChangeCheck())
                        _status = $"{label} component filter {(enabled ? "shown" : "hidden")}.";
                }
                else
                {
                    using (new EditorGUI.DisabledScope(!available))
                        GUILayout.Button(content, EditorStyles.toolbarButton, GUILayout.Width(GetCapabilityChipWidth(label)));
                }
            }
        }

        private void DrawComponentBulkToggleChip(string label, DebugComponentInfo info, BoolToggleCategory category, Color tint, string tooltip)
        {
            List<DebugComponentInfo> scope = new List<DebugComponentInfo> { info };
            ToggleStats stats = GetToggleStats(scope, category, null);
            bool current = stats.allOn;
            Color chipTint = !stats.hasAny
                ? WithValue(UtilityWindowTheme.Neutral, 0.62f)
                : stats.mixed
                    ? UtilityWindowTheme.Amber
                    : current
                        ? tint
                        : WithValue(tint, 0.52f);

            using (new EditorGUI.DisabledScope(!stats.hasAny))
            {
                EditorGUI.showMixedValue = stats.mixed;
                EditorGUI.BeginChangeCheck();
                bool next;
                using (new GuiBackgroundScope(chipTint))
                    next = GUILayout.Toggle(current, new GUIContent(label, tooltip + $" {stats.onCount}/{stats.total} currently on."), EditorStyles.toolbarButton, GUILayout.Width(GetCapabilityChipWidth(label)));
                bool changed = EditorGUI.EndChangeCheck();
                EditorGUI.showMixedValue = false;

                if (changed)
                    SetBoolToggles(scope, category, null, next);
            }
        }

        private void DrawComponentActionsMenuChip(DebugComponentInfo info)
        {
            bool hasActions = info != null && info.contextActions.Count > 0;
            using (new EditorGUI.DisabledScope(!hasActions))
            using (new GuiBackgroundScope(hasActions ? UtilityWindowTheme.Amber : WithValue(UtilityWindowTheme.Neutral, 0.62f)))
            {
                if (GUILayout.Button(new GUIContent("Actions", hasActions ? "Open bulk action commands for this component." : "No context actions are available on this component."), EditorStyles.toolbarButton, GUILayout.Width(GetCapabilityChipWidth("Actions"))))
                    ShowComponentActionsMenu(info);
            }
        }

        private void ShowComponentActionsMenu(DebugComponentInfo info)
        {
            if (info == null || info.component == null || info.contextActions.Count == 0)
                return;

            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Run All Actions"), false, () => RunAllContextActions(info));
            menu.AddItem(new GUIContent("Schedule All Actions"), false, () => ScheduleAllContextActions(info));
            menu.ShowAsContext();
        }

        private void RunAllContextActions(DebugComponentInfo info)
        {
            if (info == null || info.component == null)
                return;

            int ran = 0;
            foreach (MethodInfo method in info.contextActions.OrderBy(GetContextMenuName))
            {
                InvokeContextAction(info.component, method.Name);
                ran++;
            }
            _status = $"Ran {ran} context action(s) on {info.component.GetType().Name}.";
        }

        private void ScheduleAllContextActions(DebugComponentInfo info)
        {
            if (info == null || info.component == null)
                return;

            int scheduled = 0;
            foreach (MethodInfo method in info.contextActions.OrderBy(GetContextMenuName))
            {
                AddScheduledCall(info.component, method);
                scheduled++;
            }
            _status = $"Scheduled {scheduled} context action(s) on {info.component.GetType().Name}.";
        }

        private static float GetCapabilityChipWidth(string label)
        {
            switch (label)
            {
                case "Toggles": return 66f;
                case "Debug": return 58f;
                case "Logs": return 52f;
                case "Gizmos": return 62f;
                case "Actions": return 68f;
                case "Other": return 56f;
                default: return 58f;
            }
        }

        private bool HasCategory(DebugComponentInfo info, BoolToggleCategory category)
        {
            if (info == null)
                return false;

            return info.boolToggles.Any(field => FieldMatches(field, category, null));
        }

        private int CountVisibleToggles(DebugComponentInfo info, ComponentCapabilityFilterState filters)
        {
            if (info == null || filters == null)
                return 0;

            return info.boolToggles.Count(field => ComponentFilterAllowsField(field, filters));
        }

        private bool ComponentFilterAllowsField(FieldInfo field, ComponentCapabilityFilterState filters)
        {
            if (field == null || filters == null || !filters.toggles)
                return false;

            BoolToggleCategory category = ClassifyBoolField(field);
            switch (category)
            {
                case BoolToggleCategory.Debug:
                    return filters.debug;
                case BoolToggleCategory.Log:
                    return filters.logs;
                case BoolToggleCategory.Gizmo:
                    return filters.gizmos;
                case BoolToggleCategory.Diagnostic:
                case BoolToggleCategory.Other:
                    return filters.other;
                case BoolToggleCategory.Any:
                default:
                    return true;
            }
        }

        private void DrawSnapshotTools(DebugComponentInfo info, int id)
        {
            bool snapshotsOpen = GetSnapshotSectionFoldout(id, false);
            snapshotsOpen = EditorGUILayout.Foldout(snapshotsOpen, new GUIContent("Snapshots", "Copy serialized, debug-only, reflection, or object snapshots to the clipboard."), true, _sectionHeaderStyle);
            SetSnapshotSectionFoldout(id, snapshotsOpen);
            if (!snapshotsOpen)
                return;

            DrawResponsiveCommandRow(
                () => { if (DrawTintedButton(new GUIContent("Copy Serialized Vars", "Copy this component's serialized fields to the clipboard."), UtilityWindowTheme.Cyan, GUILayout.Width(145f))) CopySerializedSnapshot(info.component, debugOnly: false); },
                () => { if (DrawTintedButton(new GUIContent("Copy Debug Vars", "Copy only serialized fields whose names look debug-related."), DebugTint(BoolToggleCategory.Debug), GUILayout.Width(125f))) CopySerializedSnapshot(info.component, debugOnly: true); },
                () => { if (DrawTintedButton(new GUIContent("Copy Full Field Snapshot", "Copy public and non-public reflected field values for this component."), UtilityWindowTheme.Teal, GUILayout.Width(165f))) CopyReflectionSnapshot(info.component); },
                () => { if (DrawTintedButton(new GUIContent("Copy Object Snapshot", "Copy a snapshot of the component's GameObject and related components."), UtilityWindowTheme.Purple, GUILayout.Width(150f))) CopyObjectSnapshot(info.component.gameObject); });
        }

        private void DrawGroupCategoryChips(List<DebugComponentInfo> groupList)
        {
            DrawCategoryChipRow(groupList);
        }

        private void DrawGroupPerFieldControls(string groupKey, List<DebugComponentInfo> groupList)
        {
            if (groupList == null || groupList.Sum(info => info.boolToggles.Count) == 0)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                bool fieldsOpen = GetGroupBoolFoldout(groupKey, false);
                bool nextFieldsOpen = EditorGUILayout.Foldout(fieldsOpen, new GUIContent("Per-field controls", "Show detailed group operations for each reflected bool field across this group."), true);
                if (nextFieldsOpen != fieldsOpen)
                    SetGroupBoolFoldout(groupKey, nextFieldsOpen);

                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(new GUIContent("Detailed field-level group operations", "These controls apply one specific field value across every matching component in the group."), _mutedMiniLabelStyle, GUILayout.MaxWidth(220f));
            }

            if (GetGroupBoolFoldout(groupKey, false))
                DrawGroupFieldControls(groupKey, groupList);
        }

        private void DrawComponentCategoryChips(DebugComponentInfo info)
        {
            if (info == null || info.boolToggles.Count == 0)
            {
                EditorGUILayout.LabelField(new GUIContent("No debug bool toggles on this component.", "This component has no reflected bool fields that match debug/log/gizmo naming conventions."), _pathLabelStyle);
                return;
            }

            DrawCategoryChipRow(new[] { info });
        }

        private void DrawCategoryChipRow(IEnumerable<DebugComponentInfo> infos)
        {
            List<DebugComponentInfo> scope = infos != null
                ? infos.Where(i => i != null && i.component != null).ToList()
                : new List<DebugComponentInfo>();

            string[] labels = { "All", "Debug", "Logs", "Gizmos", "Other" };
            BoolToggleCategory[] categories =
            {
                BoolToggleCategory.Any,
                BoolToggleCategory.Debug,
                BoolToggleCategory.Log,
                BoolToggleCategory.Gizmo,
                BoolToggleCategory.Other
            };
            float[] widths = { 48f, 58f, 52f, 60f, 54f };

            float availableWidth = GetCurrentContentWidth(32f);
            float chipWidth = widths.Sum() + 10f;
            bool showInlineLabel = false;
            bool labelDrawn = false;

            int perRow;
            if (availableWidth >= chipWidth)
                perRow = 5;
            else if (availableWidth >= 225f)
                perRow = 4;
            else if (availableWidth >= 170f)
                perRow = 3;
            else
                perRow = 2;

            perRow = Mathf.Max(1, perRow);

            for (int i = 0; i < labels.Length; i += perRow)
            {
                int end = Mathf.Min(i + perRow, labels.Length);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (showInlineLabel && !labelDrawn)
                    {
                        EditorGUILayout.LabelField(new GUIContent("Categories", "Category chips toggle all matching fields in this group."), _mutedMiniLabelStyle, GUILayout.Width(72f));
                        labelDrawn = true;
                    }

                    for (int j = i; j < end; j++)
                        DrawCategoryToggleChip(labels[j], scope, categories[j], widths[j]);

                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawCategoryToggleChip(string label, IEnumerable<DebugComponentInfo> infos, BoolToggleCategory category, float width)
        {
            List<DebugComponentInfo> list = infos != null ? infos.Where(i => i != null && i.component != null).ToList() : new List<DebugComponentInfo>();
            ToggleStats stats = GetToggleStats(list, category, null);
            bool current = stats.allOn;
            Color tint = !stats.hasAny
                ? UtilityWindowTheme.Neutral
                : stats.mixed
                    ? UtilityWindowTheme.Amber
                    : current
                        ? DebugTint(category)
                        : WithValue(DebugTint(category), 0.52f);

            using (new EditorGUI.DisabledScope(!stats.hasAny))
            {
                EditorGUI.showMixedValue = stats.mixed;
                EditorGUI.BeginChangeCheck();
                bool next;
                using (new GuiBackgroundScope(tint))
                    next = GUILayout.Toggle(current, new GUIContent(label, BuildCategoryChipTooltip(label, category, stats)), EditorStyles.toolbarButton, GUILayout.Width(width));
                bool changed = EditorGUI.EndChangeCheck();
                EditorGUI.showMixedValue = false;

                if (changed)
                    SetBoolToggles(list, category, null, next);
            }
        }

        private static string BuildGroupMetadata(List<DebugComponentInfo> groupList, string unitLabel)
        {
            int components = groupList != null ? groupList.Count : 0;
            int bools = groupList != null ? groupList.Sum(i => i.boolToggles.Count) : 0;
            int actions = groupList != null ? groupList.Sum(i => i.contextActions.Count) : 0;
            return $"{components} {unitLabel} | {bools} toggles | {actions} actions";
        }

        private string BuildCapabilitySummary(DebugComponentInfo info)
        {
            if (info == null)
                return "No debug controls";

            if (info.boolToggles.Count == 0 && info.contextActions.Count == 0)
                return "No debug controls";

            string categories = BuildCategorySummary(info);
            string counts = $"{info.boolToggles.Count} toggles | {info.contextActions.Count} actions";
            return string.IsNullOrEmpty(categories) ? counts : $"{counts} | {categories}";
        }

        private string BuildDetailedCapabilitySummary(DebugComponentInfo info)
        {
            if (info == null)
                return string.Empty;

            return $"{info.component.GetType().Name}: {info.boolToggles.Count} bool toggles, {info.contextActions.Count} context actions. Categories: {BuildCategorySummary(info)}";
        }

        private string BuildCategorySummary(DebugComponentInfo info)
        {
            if (info == null || info.boolToggles.Count == 0)
                return string.Empty;

            List<string> labels = new List<string>();
            if (info.boolToggles.Any(field => ClassifyBoolField(field) == BoolToggleCategory.Debug))
                labels.Add("Debug");
            if (info.boolToggles.Any(field => ClassifyBoolField(field) == BoolToggleCategory.Log))
                labels.Add("Logs");
            if (info.boolToggles.Any(field => ClassifyBoolField(field) == BoolToggleCategory.Gizmo))
                labels.Add("Gizmos");
            if (info.boolToggles.Any(field => ClassifyBoolField(field) == BoolToggleCategory.Other || ClassifyBoolField(field) == BoolToggleCategory.Diagnostic))
                labels.Add("Other");

            return labels.Count > 0 ? string.Join(" | ", labels) : "Other";
        }

        private void DrawBoolToggles(DebugComponentInfo info, ComponentCapabilityFilterState filters)
        {
            List<FieldInfo> fields = info.boolToggles
                .Where(field => ComponentFilterAllowsField(field, filters))
                .OrderBy(GetBoolSortKey)
                .ToList();

            if (fields.Count == 0)
            {
                EditorGUILayout.HelpBox("The active capability chips hide every toggle on this component. Re-enable a chip above to show matching toggles.", MessageType.None);
                return;
            }

            foreach (FieldInfo field in fields)
            {
                bool oldValue = false;
                try { oldValue = (bool)field.GetValue(info.component); }
                catch { continue; }

                BoolToggleCategory category = ClassifyBoolField(field);
                using (new EditorGUILayout.HorizontalScope(_fieldPanelStyle))
                {
                    DrawCategoryPill(category, 78f);
                    DrawScrollingEllipsisGUILayoutLabel("field:" + info.component.GetInstanceID() + ":" + field.Name, field.Name, field.DeclaringType.Name + "." + field.Name, _categoryLabelStyle, 90f);
                    using (new GuiBackgroundScope(oldValue ? DebugTint(category) : WithValue(DebugTint(category), 0.55f)))
                    {
                        bool next = GUILayout.Toggle(oldValue, new GUIContent(oldValue ? "On" : "Off", $"Set {info.component.GetType().Name}.{field.Name} {(oldValue ? "off" : "on")}."), EditorStyles.miniButton, GUILayout.Width(52f));
                        if (next != oldValue)
                        {
                            Undo.RecordObject(info.component, $"Set {field.Name}");
                            field.SetValue(info.component, next);
                            EditorUtility.SetDirty(info.component);
                            _status = $"Set {info.component.GetType().Name}.{field.Name} = {next}";
                        }
                    }
                }
            }
        }

        private void DrawContextActions(DebugComponentInfo info, ComponentCapabilityFilterState filters)
        {
            if (filters != null && !filters.actions)
            {
                EditorGUILayout.HelpBox("The Actions chip is disabled for this component. Re-enable it above to show context actions.", MessageType.None);
                return;
            }

            foreach (MethodInfo method in info.contextActions.OrderBy(GetContextMenuName))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    string label = GetContextMenuName(method);
                    DrawScrollingEllipsisGUILayoutLabel("action:" + info.component.GetInstanceID() + ":" + method.Name, label, method.DeclaringType.Name + "." + method.Name, _categoryLabelStyle, 120f);
                    if (DrawTintedButton(new GUIContent("Run", "Invoke this reflected [ContextMenu] action immediately on the component."), UtilityWindowTheme.Blue, GUILayout.Width(58f)))
                        _status = InvokeContextAction(info.component, method.Name);
                    if (DrawTintedButton(new GUIContent("Schedule", "Create a scheduled debug action for this reflected [ContextMenu] method."), UtilityWindowTheme.Amber, GUILayout.Width(78f)))
                        AddScheduledCall(info.component, method);
                }
            }
        }

        private void DrawMasterControls()
        {
            IEnumerable<DebugComponentInfo> scope = _masterControlsAffectFiltered ? _components.Where(PassesFilters) : _components;
            List<DebugComponentInfo> list = scope.Where(i => i != null && i.component != null).ToList();

            string scopeLabel = _masterControlsAffectFiltered ? "filtered visible results" : "the whole scanned scene cache";
            int boolCount = list.Sum(i => i.boolToggles.Count);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent("Broad Controls", "Apply category toggles across the scanned or filtered component set."), _sectionHeaderStyle);
                GUILayout.FlexibleSpace();
                DrawCountPill(_drawingSupportColumn ? $"{boolCount} bools" : $"{list.Count} components | {boolCount} bools", UtilityWindowTheme.Cyan, _drawingSupportColumn ? 78f : 158f);
            }

            EditorGUILayout.HelpBox(
                $"These controls affect {scopeLabel}. Use Filtered only when search/selection filters are active.",
                MessageType.Warning);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent(_masterControlsAffectFiltered ? "Scope: filtered" : "Scope: scanned scene", "Choose whether broad controls affect only the currently filtered browser result or the full cached scene scan."), _categoryLabelStyle, GUILayout.Width(130f));

                EditorGUI.BeginChangeCheck();
                using (new GuiBackgroundScope(_masterControlsAffectFiltered ? UtilityWindowTheme.Blue : UtilityWindowTheme.Neutral))
                    _masterControlsAffectFiltered = GUILayout.Toggle(_masterControlsAffectFiltered, new GUIContent("Filtered only", "Limit broad controls to the current search/filter/selection result instead of the full cached scene scan."), EditorStyles.toolbarButton, GUILayout.Width(92f));

                if (EditorGUI.EndChangeCheck())
                    UtilityWindowPrefs.SetBool(MasterFilteredPrefsKey, _masterControlsAffectFiltered);

                GUILayout.FlexibleSpace();
            }

            DrawResponsiveCommandRow(
                () => DrawScopeToggle("All", list, BoolToggleCategory.Any, 58f),
                () => DrawScopeToggle("Debug", list, BoolToggleCategory.Debug, 70f),
                () => DrawScopeToggle("Logs", list, BoolToggleCategory.Log, 62f),
                () => DrawScopeToggle("Gizmos", list, BoolToggleCategory.Gizmo, 72f),
                () => DrawScopeToggle("Other", list, BoolToggleCategory.Other, 66f));
        }

        private void DrawScopeToggle(string label, IEnumerable<DebugComponentInfo> infos, BoolToggleCategory category, float width)
        {
            List<DebugComponentInfo> list = infos != null ? infos.Where(i => i != null && i.component != null).ToList() : new List<DebugComponentInfo>();
            ToggleStats stats = GetToggleStats(list, category, null);
            using (new EditorGUI.DisabledScope(!stats.hasAny))
            {
                bool current = stats.allOn;
                EditorGUI.showMixedValue = stats.mixed;
                EditorGUI.BeginChangeCheck();
                Color tint = stats.mixed ? UtilityWindowTheme.Amber : current ? DebugTint(category) : WithValue(DebugTint(category), 0.5f);
                bool next;
                using (new GuiBackgroundScope(tint))
                    next = GUILayout.Toggle(current, new GUIContent(label, BuildToggleTooltip(category, stats)), EditorStyles.toolbarButton, GUILayout.Width(width));
                bool changed = EditorGUI.EndChangeCheck();
                EditorGUI.showMixedValue = false;

                if (changed)
                    SetBoolToggles(list, category, null, next);
            }
        }

        private void DrawStaticScopeToggle(string label, BoolToggleCategory category, float width)
        {
            ToggleStats stats = GetStaticToggleStats(category);
            using (new EditorGUI.DisabledScope(!stats.hasAny))
            {
                bool current = stats.allOn;
                EditorGUI.showMixedValue = stats.mixed;
                EditorGUI.BeginChangeCheck();
                Color tint = stats.mixed ? UtilityWindowTheme.Amber : current ? DebugTint(category) : WithValue(DebugTint(category), 0.5f);
                bool next;
                using (new GuiBackgroundScope(tint))
                    next = GUILayout.Toggle(current, new GUIContent(label, BuildToggleTooltip(category, stats)), EditorStyles.toolbarButton, GUILayout.Width(width));
                bool changed = EditorGUI.EndChangeCheck();
                EditorGUI.showMixedValue = false;

                if (changed)
                    SetStaticBoolToggles(category, next);
            }
        }

        private void DrawGroupFieldControls(string groupKey, List<DebugComponentInfo> groupList)
        {
            if (groupList == null || groupList.Count == 0)
                return;

            List<FieldInfo> fields = groupList
                .SelectMany(g => g.boolToggles)
                .GroupBy(f => f.Name)
                .Select(g => g.First())
                .OrderBy(GetBoolSortKey)
                .ToList();

            if (fields.Count == 0)
                return;

            using (new EditorGUILayout.VerticalScope(_fieldPanelStyle))
            {
                EditorGUILayout.LabelField(new GUIContent("Per-field controls for every visible instance of this component type", "Toggle a specific reflected bool field across all visible instances in this group."), EditorStyles.miniBoldLabel);

                for (int i = 0; i < fields.Count; i++)
                {
                    FieldInfo field = fields[i];
                    ToggleStats stats = GetToggleStats(groupList, BoolToggleCategory.Any, field);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        BoolToggleCategory category = ClassifyBoolField(field);
                        DrawCategoryPill(category, 78f);
                        EditorGUI.showMixedValue = stats.mixed;
                        EditorGUI.BeginChangeCheck();
                        bool next;
                        Color tint = stats.mixed ? UtilityWindowTheme.Amber : stats.allOn ? DebugTint(category) : WithValue(DebugTint(category), 0.55f);
                        using (new GuiBackgroundScope(tint))
                            next = EditorGUILayout.ToggleLeft(new GUIContent(field.Name, BuildToggleTooltip(BoolToggleCategory.Any, stats)), stats.allOn);
                        bool changed = EditorGUI.EndChangeCheck();
                        EditorGUI.showMixedValue = false;

                        GUILayout.FlexibleSpace();
                        DrawCountPill($"{stats.onCount}/{stats.total} on", tint, 78f);

                        if (changed)
                            SetBoolToggles(groupList, BoolToggleCategory.Any, field, next);
                    }
                }
            }
        }

        private static string BuildToggleTooltip(BoolToggleCategory category, ToggleStats stats)
        {
            return $"{category}: {stats.onCount}/{stats.total} on. Click to apply this value to all matching toggles in this scope.";
        }

        private static string BuildCategoryChipTooltip(string label, BoolToggleCategory category, ToggleStats stats)
        {
            if (!stats.hasAny)
                return $"No {label} fields found in this scope.";

            return $"Toggle all {label} fields in this scope. {stats.onCount}/{stats.total} currently on.";
        }

        private ToggleStats GetToggleStats(IEnumerable<DebugComponentInfo> infos, BoolToggleCategory category, FieldInfo specificField)
        {
            ToggleStats stats = new ToggleStats();
            if (infos == null)
                return stats;

            foreach (DebugComponentInfo info in infos)
            {
                if (info == null || info.component == null)
                    continue;

                foreach (FieldInfo field in info.boolToggles)
                {
                    if (!FieldMatches(field, category, specificField))
                        continue;

                    try
                    {
                        stats.total++;
                        object value = field.GetValue(info.component);
                        if (value is bool b && b)
                            stats.onCount++;
                    }
                    catch
                    {
                        // Ignore inaccessible/invalid fields; the serialized setter will be the source of truth when applying.
                    }
                }
            }

            return stats;
        }

        private ToggleStats GetStaticToggleStats(BoolToggleCategory category)
        {
            ToggleStats stats = new ToggleStats();
            foreach (StaticDebugFieldInfo info in _staticBoolToggles)
            {
                if (info == null || info.field == null || !FieldMatches(info.field, category, null))
                    continue;

                stats.total++;
                try
                {
                    if ((bool)info.field.GetValue(null))
                        stats.onCount++;
                }
                catch
                {
                    stats.total--;
                }
            }
            return stats;
        }

        private void SetBoolToggles(IEnumerable<DebugComponentInfo> infos, BoolToggleCategory category, FieldInfo specificField, bool value)
        {
            int changedCount = 0;
            if (infos == null)
                return;

            foreach (DebugComponentInfo info in infos)
            {
                if (info == null || info.component == null)
                    continue;

                bool undoRecorded = false;
                foreach (FieldInfo field in info.boolToggles)
                {
                    if (!FieldMatches(field, category, specificField))
                        continue;

                    try
                    {
                        bool current = (bool)field.GetValue(info.component);
                        if (current == value)
                            continue;

                        if (!undoRecorded)
                        {
                            Undo.RecordObject(info.component, $"Set {category} debug bools on {info.component.GetType().Name}");
                            undoRecorded = true;
                        }

                        field.SetValue(info.component, value);
                        changedCount++;
                    }
                    catch
                    {
                        // Ignore inaccessible/invalid fields.
                    }
                }

                if (undoRecorded)
                    EditorUtility.SetDirty(info.component);
            }

            _status = specificField != null
                ? $"Set {specificField.Name} = {value} on {changedCount} instances."
                : $"Set {category} toggles = {value} on {changedCount} fields.";
        }

        private void SetStaticBoolToggles(BoolToggleCategory category, bool value)
        {
            int changedCount = 0;
            foreach (StaticDebugFieldInfo info in _staticBoolToggles)
            {
                if (info == null || info.field == null || !FieldMatches(info.field, category, null))
                    continue;

                try
                {
                    if ((bool)info.field.GetValue(null) == value)
                        continue;

                    info.field.SetValue(null, value);
                    changedCount++;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[DebugControlCenter] Could not set static toggle {info.declaringType.Name}.{info.field.Name}: {ex.Message}");
                }
            }

            _status = $"Set static {category} toggles = {value} on {changedCount} fields.";
        }

        private static bool FieldMatches(FieldInfo field, BoolToggleCategory category, FieldInfo specificField)
        {
            if (field == null)
                return false;

            if (specificField != null && !string.Equals(field.Name, specificField.Name, StringComparison.Ordinal))
                return false;

            if (category == BoolToggleCategory.Any)
                return true;

            if (category == BoolToggleCategory.Other)
            {
                BoolToggleCategory classified = ClassifyBoolField(field);
                return classified == BoolToggleCategory.Other || classified == BoolToggleCategory.Diagnostic;
            }

            return ClassifyBoolField(field) == category;
        }

    }
#endif
}
