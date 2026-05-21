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
        // Window enum/state helpers and foldout persistence accessors.

        private enum GroupMode
        {
            ComponentType,
            SceneObject
        }

        private enum ConditionMode
        {
            Always = 0,
            PlayModeOnly = 1,
            SelectedTarget = 2,
            TargetEnabled = 3,
            BoolMemberTrue = 4,
            BoolMemberFalse = 5,
            EditModeOnly = 6,
            SelectionInsideTarget = 7,
            TargetDisabled = 8,
            TargetActiveInHierarchy = 9,
            TargetInactiveInHierarchy = 10
        }

        private enum ScheduledActionKind
        {
            ContextMethod = 0,
            RouterLog = 1,
            RouterSignal = 2,
            RouterState = 3
        }

        private enum BoolToggleCategory
        {
            Any,
            Debug,
            Log,
            Gizmo,
            Diagnostic,
            Other
        }

        private struct ToggleStats
        {
            public int total;
            public int onCount;

            public int offCount
            {
                get { return total - onCount; }
            }

            public bool hasAny
            {
                get { return total > 0; }
            }

            public bool allOn
            {
                get { return total > 0 && onCount == total; }
            }

            public bool mixed
            {
                get { return total > 0 && onCount > 0 && onCount < total; }
            }
        }

        private sealed class ComponentCapabilityFilterState
        {
            public bool toggles = true;
            public bool debug = true;
            public bool logs = true;
            public bool gizmos = true;
            public bool actions = true;
            public bool other = true;
        }

        private bool GetTypeFoldout(string key, bool defaultValue)
        {
            if (!_typeFoldouts.TryGetValue(key, out bool value))
            {
                value = UtilityWindowPrefs.GetBool(TypeFoldoutPrefsPrefix + key, defaultValue);
                _typeFoldouts[key] = value;
            }
            return value;
        }

        private void SetTypeFoldout(string key, bool value)
        {
            _typeFoldouts[key] = value;
            UtilityWindowPrefs.SetBool(TypeFoldoutPrefsPrefix + key, value);
        }

        private bool GetInstanceFoldout(int key, bool defaultValue)
        {
            if (!_instanceFoldouts.TryGetValue(key, out bool value))
            {
                value = defaultValue;
                _instanceFoldouts[key] = value;
            }
            return value;
        }

        private void SetInstanceFoldout(int key, bool value)
        {
            _instanceFoldouts[key] = value;
        }

        private bool GetSceneObjectFoldout(string key, bool defaultValue)
        {
            if (!_sceneObjectFoldouts.TryGetValue(key, out bool value))
            {
                value = UtilityWindowPrefs.GetBool(SceneObjectFoldoutPrefsPrefix + key, defaultValue);
                _sceneObjectFoldouts[key] = value;
            }
            return value;
        }

        private void SetSceneObjectFoldout(string key, bool value)
        {
            _sceneObjectFoldouts[key] = value;
            UtilityWindowPrefs.SetBool(SceneObjectFoldoutPrefsPrefix + key, value);
        }

        private bool GetGroupBoolFoldout(string key, bool defaultValue)
        {
            if (!_groupBoolFoldouts.TryGetValue(key, out bool value))
            {
                value = UtilityWindowPrefs.GetBool(GroupBoolFoldoutPrefsPrefix + key, defaultValue);
                _groupBoolFoldouts[key] = value;
            }
            return value;
        }

        private void SetGroupBoolFoldout(string key, bool value)
        {
            _groupBoolFoldouts[key] = value;
            UtilityWindowPrefs.SetBool(GroupBoolFoldoutPrefsPrefix + key, value);
        }

        private bool GetBoolSectionFoldout(int key, bool defaultValue)
        {
            if (!_boolSectionFoldouts.TryGetValue(key, out bool value))
            {
                value = defaultValue;
                _boolSectionFoldouts[key] = value;
            }
            return value;
        }

        private void SetBoolSectionFoldout(int key, bool value)
        {
            _boolSectionFoldouts[key] = value;
        }

        private bool GetActionSectionFoldout(int key, bool defaultValue)
        {
            if (!_actionSectionFoldouts.TryGetValue(key, out bool value))
            {
                value = defaultValue;
                _actionSectionFoldouts[key] = value;
            }
            return value;
        }

        private void SetActionSectionFoldout(int key, bool value)
        {
            _actionSectionFoldouts[key] = value;
        }

        private bool GetSnapshotSectionFoldout(int key, bool defaultValue)
        {
            if (!_snapshotSectionFoldouts.TryGetValue(key, out bool value))
            {
                value = defaultValue;
                _snapshotSectionFoldouts[key] = value;
            }
            return value;
        }

        private void SetSnapshotSectionFoldout(int key, bool value)
        {
            _snapshotSectionFoldouts[key] = value;
        }

        private bool GetTargetSectionFoldout(int key, bool defaultValue)
        {
            if (!_targetSectionFoldouts.TryGetValue(key, out bool value))
            {
                value = defaultValue;
                _targetSectionFoldouts[key] = value;
            }

            return value;
        }

        private void SetTargetSectionFoldout(int key, bool value)
        {
            _targetSectionFoldouts[key] = value;
        }

        private ComponentCapabilityFilterState GetComponentCapabilityFilters(int key)
        {
            if (!_componentCapabilityFilters.TryGetValue(key, out ComponentCapabilityFilterState state) || state == null)
            {
                state = new ComponentCapabilityFilterState();
                _componentCapabilityFilters[key] = state;
            }

            return state;
        }

        private void RequestRepaintThrottled()
        {
            PungentEditorPerformanceUtility.RequestWindowRepaintThrottled(this, ref _nextAllowedRepaintTime, RepaintThrottleInterval);
        }

        private void LoadWindowPrefs()
        {
            _search = UtilityWindowPrefs.GetString(SearchPrefsKey, string.Empty);
            _autoRefreshSceneScan = UtilityWindowPrefs.GetBool("GenericDebugControlWindow.AutoRefreshSceneScan", _autoRefreshSceneScan);
            _includeInactive = UtilityWindowPrefs.GetBool(IncludeInactivePrefsKey, _includeInactive);
            _selectedHierarchyOnly = UtilityWindowPrefs.GetBool(SelectedHierarchyOnlyPrefsKey, _selectedHierarchyOnly);
            _showOnlyDebuggable = UtilityWindowPrefs.GetBool(ShowOnlyDebuggablePrefsKey, _showOnlyDebuggable);
            _showStaticDebugFields = UtilityWindowPrefs.GetBool(ShowStaticDebugFieldsPrefsKey, _showStaticDebugFields);
            _showContextActions = UtilityWindowPrefs.GetBool(ShowContextActionsPrefsKey, _showContextActions);
            _showBoolToggles = UtilityWindowPrefs.GetBool(ShowBoolTogglesPrefsKey, _showBoolToggles);
            _showSnapshotButtons = UtilityWindowPrefs.GetBool(ShowSnapshotButtonsPrefsKey, _showSnapshotButtons);
            int groupMode = UtilityWindowPrefs.GetInt(GroupModePrefsKey, (int)GroupMode.ComponentType);
            _groupMode = Enum.IsDefined(typeof(GroupMode), groupMode) ? (GroupMode)groupMode : GroupMode.ComponentType;
            _masterControlsAffectFiltered = UtilityWindowPrefs.GetBool(MasterFilteredPrefsKey, _masterControlsAffectFiltered);
            _helpFoldout = UtilityWindowPrefs.GetBool(HelpFoldoutPrefsKey, _helpFoldout);
            _bulkControlsOpen = UtilityWindowPrefs.GetBool(BulkControlsOpenPrefsKey, false);
            _routerAdvancedOpen = UtilityWindowPrefs.GetBool(RouterAdvancedOpenPrefsKey, false);
            _schedulerCleanupOpen = UtilityWindowPrefs.GetBool(SchedulerCleanupOpenPrefsKey, false);
        }

        private void SaveWindowPrefs()
        {
            UtilityWindowPrefs.SetString(SearchPrefsKey, _search);
            UtilityWindowPrefs.SetBool("GenericDebugControlWindow.AutoRefreshSceneScan", _autoRefreshSceneScan);
            UtilityWindowPrefs.SetBool(IncludeInactivePrefsKey, _includeInactive);
            UtilityWindowPrefs.SetBool(SelectedHierarchyOnlyPrefsKey, _selectedHierarchyOnly);
            UtilityWindowPrefs.SetBool(ShowOnlyDebuggablePrefsKey, _showOnlyDebuggable);
            UtilityWindowPrefs.SetBool(ShowStaticDebugFieldsPrefsKey, _showStaticDebugFields);
            UtilityWindowPrefs.SetBool(ShowContextActionsPrefsKey, _showContextActions);
            UtilityWindowPrefs.SetBool(ShowBoolTogglesPrefsKey, _showBoolToggles);
            UtilityWindowPrefs.SetBool(ShowSnapshotButtonsPrefsKey, _showSnapshotButtons);
            UtilityWindowPrefs.SetInt(GroupModePrefsKey, (int)_groupMode);
            UtilityWindowPrefs.SetBool(MasterFilteredPrefsKey, _masterControlsAffectFiltered);
            UtilityWindowPrefs.SetBool(HelpFoldoutPrefsKey, _helpFoldout);
            UtilityWindowPrefs.SetBool(BulkControlsOpenPrefsKey, _bulkControlsOpen);
            UtilityWindowPrefs.SetBool(RouterAdvancedOpenPrefsKey, _routerAdvancedOpen);
            UtilityWindowPrefs.SetBool(SchedulerCleanupOpenPrefsKey, _schedulerCleanupOpen);
        }

        private void ApplyBrowseFirstDefaultsIfNeeded()
        {
            if (UtilityWindowPrefs.GetBool(BrowseFirstDefaultsAppliedPrefsKey, false))
                return;

            _includeInactive = true;
            _selectedHierarchyOnly = false;
            _showOnlyDebuggable = false;
            _showBoolToggles = true;
            _showContextActions = true;
            _showSnapshotButtons = false;
            _showStaticDebugFields = true;
            _groupMode = GroupMode.SceneObject;
            _bulkControlsOpen = false;
            _routerAdvancedOpen = false;
            _schedulerCleanupOpen = false;
            _helpFoldout = false;

            UtilityWindowPrefs.SetBool(IncludeInactivePrefsKey, _includeInactive);
            UtilityWindowPrefs.SetBool(SelectedHierarchyOnlyPrefsKey, _selectedHierarchyOnly);
            UtilityWindowPrefs.SetBool(ShowOnlyDebuggablePrefsKey, _showOnlyDebuggable);
            UtilityWindowPrefs.SetBool(ShowBoolTogglesPrefsKey, _showBoolToggles);
            UtilityWindowPrefs.SetBool(ShowContextActionsPrefsKey, _showContextActions);
            UtilityWindowPrefs.SetBool(ShowSnapshotButtonsPrefsKey, _showSnapshotButtons);
            UtilityWindowPrefs.SetBool(ShowStaticDebugFieldsPrefsKey, _showStaticDebugFields);
            UtilityWindowPrefs.SetInt(GroupModePrefsKey, (int)_groupMode);
            UtilityWindowPrefs.SetBool(BulkControlsOpenPrefsKey, _bulkControlsOpen);
            UtilityWindowPrefs.SetBool(RouterAdvancedOpenPrefsKey, _routerAdvancedOpen);
            UtilityWindowPrefs.SetBool(SchedulerCleanupOpenPrefsKey, _schedulerCleanupOpen);
            UtilityWindowPrefs.SetBool(HelpFoldoutPrefsKey, _helpFoldout);
            UtilityWindowPrefs.SetBool(BrowseFirstDefaultsAppliedPrefsKey, true);
        }

        private bool ShouldRefreshRouterForVisibleContent()
        {
            return _routerPanelOpen || _autoRefreshSceneScan;
        }

        private bool ShouldUseTwoColumnWorkbench()
        {
            float requiredWidth = MinPrimaryWorkbenchWidth + MinSupportColumnWidth + WorkbenchResizeHandleWidth + 28f;
            return position.width >= TwoColumnWorkbenchBreakpoint && position.width >= requiredWidth;
        }

        private float GetClampedSupportColumnWidth()
        {
            return GetClampedSupportColumnWidth(position.width);
        }

        private float GetClampedSupportColumnWidth(float availableWidth)
        {
            float maxByRatio = Mathf.Max(MinSupportColumnWidth, availableWidth * MaxSupportColumnWidthRatio);
            float maxByPrimary = Mathf.Max(MinSupportColumnWidth, availableWidth - MinPrimaryWorkbenchWidth - WorkbenchResizeHandleWidth);
            float max = Mathf.Min(maxByRatio, maxByPrimary);
            return Mathf.Clamp(_supportColumnWidth <= 0f ? PreferredSupportColumnWidth : _supportColumnWidth, MinSupportColumnWidth, max);
        }

        private float GetCurrentContentWidth(float padding = 24f)
        {
            float width;
            if (_drawingSupportColumn)
                width = _supportColumnWidth;
            else if (_stretchWorkbench && _primaryWorkbenchWidth > 0f)
                width = _primaryWorkbenchWidth;
            else
                width = position.width;

            return Mathf.Max(180f, width - padding);
        }

        private bool ShouldUseInternalComponentScroll(int filteredCount)
        {
            return filteredCount >= ComponentInternalScrollThreshold;
        }

        private float GetComponentListHeight(float contentHeight)
        {
            float visibleBudget = _stretchWorkbench ? Mathf.Max(260f, _workbenchHeight - 104f) : Mathf.Max(260f, position.height - 240f);
            return Mathf.Clamp(Mathf.Min(contentHeight, visibleBudget), 220f, Mathf.Min(720f, visibleBudget));
        }

        private float GetSupportListHeight(float contentHeight, float maxHeight)
        {
            float min = Mathf.Min(90f, maxHeight);
            return Mathf.Clamp(contentHeight, min, maxHeight);
        }

        private List<DebugComponentInfo> GetFilteredComponents()
        {
            return _components.Where(PassesFilters).ToList();
        }

        private List<StaticDebugFieldInfo> GetFilteredStaticToggles()
        {
            return _staticBoolToggles
                .Where(info => info != null && info.field != null && MatchesSearch($"{info.declaringType.Name}.{info.field.Name}"))
                .OrderBy(info => info.declaringType.Name)
                .ThenBy(info => info.field.Name)
                .ToList();
        }

        private List<ScheduledCall> GetFilteredScheduledCalls()
        {
            return _scheduledCalls
                .Where(call => call != null && ScheduledCallMatchesSearch(call))
                .OrderBy(GetScheduledCallSortRank)
                .ThenBy(call => call.displayName ?? call.methodName)
                .ToList();
        }

        private bool ScheduledCallMatchesSearch(ScheduledCall call)
        {
            if (call == null)
                return false;

            if (string.IsNullOrWhiteSpace(_search))
                return true;

            string needle = _search.Trim();
            return Contains(GetScheduledDisplayName(call), needle)
                   || Contains(call.methodName, needle)
                   || Contains(call.routerChannel, needle)
                   || Contains(call.routerMessage, needle)
                   || Contains(call.routerSignalName, needle)
                   || Contains(call.routerStateName, needle)
                   || Contains(call.target != null ? call.target.name : null, needle)
                   || Contains(call.targetScenePath, needle)
                   || Contains(call.conditionMode.ToString(), needle)
                   || Contains(call.conditionMemberName, needle)
                   || Contains(GetScheduledStatusLabel(call), needle);
        }

        private static bool Contains(string text, string needle)
        {
            return !string.IsNullOrEmpty(text) && text.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private int GetScheduledCallSortRank(ScheduledCall call)
        {
            if (call == null)
                return 99;

            if (!IsScheduledCallRunnable(call))
                return 3;
            if (!call.enabled)
                return 2;
            return EvaluateCondition(call) ? 0 : 1;
        }

        private string GetScheduledStatusLabel(ScheduledCall call)
        {
            if (call == null)
                return "Invalid";
            if (!IsScheduledCallRunnable(call))
                return call.actionKind == ScheduledActionKind.ContextMethod ? "Missing method" : "Invalid";
            if (!call.enabled)
                return "Paused";
            return EvaluateCondition(call) ? "Active" : "Waiting";
        }

        private Color GetScheduledStatusTint(ScheduledCall call)
        {
            string status = GetScheduledStatusLabel(call);
            if (status == "Active")
                return UtilityWindowTheme.Green;
            if (status == "Waiting")
                return UtilityWindowTheme.Amber;
            if (status == "Paused")
                return UtilityWindowTheme.Neutral;
            return UtilityWindowTheme.Red;
        }

        private int CountInvalidScheduledCalls()
        {
            int count = 0;
            foreach (ScheduledCall call in _scheduledCalls)
            {
                if (call == null || !IsScheduledCallRunnable(call))
                    count++;
            }
            return count;
        }

        private void DrawDebuggableModeToggle()
        {
            int mode = _showOnlyDebuggable ? 1 : 0;
            DrawLabelledToolbar(
                "Mode",
                "Choose whether the component list shows all scanned components or only components with debug toggles/actions.",
                ref mode,
                new[]
                {
                    new GUIContent("All", "Show every scanned scene component that matches the other filters."),
                    new GUIContent("Debuggable", "Show only components with reflected debug bools or context actions.")
                },
                176f,
                () =>
                {
                    _showOnlyDebuggable = mode == 1;
                    UtilityWindowPrefs.SetBool(ShowOnlyDebuggablePrefsKey, _showOnlyDebuggable);
                });
        }

        private void DrawScopeModeToggle()
        {
            int mode = _selectedHierarchyOnly ? 1 : 0;
            DrawLabelledToolbar(
                "Scope",
                "Choose whether the scan browser shows the whole scene cache or only the selected hierarchy branch.",
                ref mode,
                new[]
                {
                    new GUIContent("Scene", "Browse the full cached scene component scan."),
                    new GUIContent("Selected", "Show only components under the current Hierarchy selection.")
                },
                158f,
                () =>
                {
                    _selectedHierarchyOnly = mode == 1;
                    UtilityWindowPrefs.SetBool(SelectedHierarchyOnlyPrefsKey, _selectedHierarchyOnly);
                });
        }

        private void DrawVisibilityModeToggle()
        {
            int mode = _includeInactive ? 1 : 0;
            DrawLabelledToolbar(
                "Visibility",
                "Choose whether inactive GameObjects are included in the component scan/browser.",
                ref mode,
                new[]
                {
                    new GUIContent("Active", "Show only active objects in the cached component browser."),
                    new GUIContent("Inactive", "Include inactive GameObjects in the cached component browser.")
                },
                174f,
                () =>
                {
                    _includeInactive = mode == 1;
                    UtilityWindowPrefs.SetBool(IncludeInactivePrefsKey, _includeInactive);
                });
        }

        private void DrawGroupModeToggle()
        {
            int mode = _groupMode == GroupMode.SceneObject ? 1 : 0;
            DrawLabelledToolbar(
                "Group",
                "Choose how component cards are grouped in the browser.",
                ref mode,
                new[]
                {
                    new GUIContent("Type", "Group by component type, such as SkiController or DebugRouterSource."),
                    new GUIContent("Object", "Group by scene object path, matching the Hierarchy structure.")
                },
                148f,
                () =>
                {
                    _groupMode = mode == 1 ? GroupMode.SceneObject : GroupMode.ComponentType;
                    UtilityWindowPrefs.SetInt(GroupModePrefsKey, (int)_groupMode);
                });
        }

        private void DrawLabelledToolbar(string label, string tooltip, ref int mode, GUIContent[] labels, float width, Action onChanged)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent(label, tooltip), _mutedMiniLabelStyle, GUILayout.Width(label.Length > 5 ? 52f : 38f));
                EditorGUI.BeginChangeCheck();
                mode = GUILayout.Toolbar(mode, labels, GUILayout.Width(width));
                if (EditorGUI.EndChangeCheck())
                    onChanged?.Invoke();
            }
        }

        private void DrawBulkControlsPanel()
        {
            using (new EditorGUILayout.VerticalScope(_masterPanelStyle))
            {
                List<DebugComponentInfo> scope = (_masterControlsAffectFiltered ? _components.Where(PassesFilters) : _components)
                    .Where(i => i != null && i.component != null)
                    .ToList();

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    _bulkControlsOpen = EditorGUILayout.Foldout(_bulkControlsOpen, new GUIContent("Bulk Controls", "Show broad category controls for the scanned or filtered component set."), true, _sectionHeaderStyle);
                    if (EditorGUI.EndChangeCheck())
                        UtilityWindowPrefs.SetBool(BulkControlsOpenPrefsKey, _bulkControlsOpen);

                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(new GUIContent($"{scope.Sum(info => info.boolToggles.Count)} toggles", "Number of bool toggles affected by broad controls in the current scope."), _mutedMiniLabelStyle, GUILayout.Width(82f));
                }

                if (_bulkControlsOpen)
                    DrawMasterControls();
            }
        }

        private void DrawPersistentToolbarToggle(ref bool value, string prefsKey, string label, Color tint, string tooltip = null)
        {
            EditorGUI.BeginChangeCheck();
            DrawToolbarToggle(ref value, label, tint, tooltip);
            if (EditorGUI.EndChangeCheck())
            {
                UtilityWindowPrefs.SetBool(prefsKey, value);
                _scanCacheDirty = true;
            }
        }

        private void DrawResponsiveCommandRow(params Action[] drawers)
        {
            DrawResponsiveCommandRow(GetCurrentContentWidth(), drawers);
        }

        private void DrawResponsiveCommandRow(float availableWidth, params Action[] drawers)
        {
            if (drawers == null || drawers.Length == 0)
                return;

            float width = Mathf.Max(180f, availableWidth);

            int perRow;
            if (_drawingSupportColumn)
            {
                if (width >= 340f)
                    perRow = 5;
                else if (width >= 280f)
                    perRow = 4;
                else if (width >= 220f)
                    perRow = 3;
                else
                    perRow = 2;
            }
            else if (width < 430f)
            {
                perRow = 1;
            }
            else if (width < 640f)
            {
                perRow = 2;
            }
            else if (width < 860f)
            {
                perRow = 3;
            }
            else
            {
                perRow = 4;
            }

            perRow = Mathf.Clamp(perRow, 1, drawers.Length);

            for (int i = 0; i < drawers.Length; i += perRow)
            {
                int end = Mathf.Min(i + perRow, drawers.Length);

                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int j = i; j < end; j++)
                        drawers[j]?.Invoke();

                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawHelpCard()
        {
            using (new EditorGUILayout.VerticalScope(_fieldPanelStyle))
            {
                EditorGUI.BeginChangeCheck();
                _helpFoldout = EditorGUILayout.Foldout(_helpFoldout, new GUIContent("Help", "Show what this tool changes, what it never changes, and performance notes."), true, _sectionHeaderStyle);
                if (EditorGUI.EndChangeCheck())
                    UtilityWindowPrefs.SetBool(HelpFoldoutPrefsKey, _helpFoldout);

                if (!_helpFoldout)
                    return;

                EditorGUILayout.HelpBox(
                    "Changes scene instance debug/log/gizmo bools, static debug toggles, DebugRouter runtime channel/signal/source settings, and scheduled [ContextMenu] diagnostic calls.\n\n" +
                    "Does not permanently modify project assets unless the target object or field itself does so. It does not scan automatically unless Auto Refresh is enabled, and scheduled actions only run when enabled and their conditions pass.\n\n" +
                    "Refresh is explicit by default. Auto Refresh and router snapshots are throttled; disable Auto Refresh when working in very large scenes.",
                    MessageType.Info);
            }
        }

        private void SetAllFoldouts(bool value)
        {
            foreach (DebugComponentInfo info in _components)
            {
                if (info == null || info.component == null)
                    continue;

                SetTypeFoldout(info.component.GetType().FullName, value);
                _instanceFoldouts[info.component.GetInstanceID()] = value;
                SetSceneObjectFoldout(info.objectPath, value);
                SetGroupBoolFoldout(info.component.GetType().FullName, value);
                _boolSectionFoldouts[info.component.GetInstanceID()] = value;
                _actionSectionFoldouts[info.component.GetInstanceID()] = value;
            }

            SetTypeFoldout("__STATIC_DEBUG_FIELDS__", value);
            _routerPanelOpen = value;
            _routerChannelsOpen = value;
            _routerSignalsOpen = value;
            _routerStatesOpen = value;
            _routerSourcesOpen = value;
            _scheduledPanelOpen = value;
            _staticFieldsPanelOpen = value;
            _discoveredComponentsPanelOpen = value;
            foreach (ScheduledCall call in _scheduledCalls)
            {
                if (call != null)
                    call.foldout = value;
            }
        }

    }
#endif
}
