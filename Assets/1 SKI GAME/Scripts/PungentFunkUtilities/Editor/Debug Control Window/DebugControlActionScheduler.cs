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
        // Scheduled action panel, persistence, reference resolution, and execution logic.

        private void DrawSchedulePanel()
        {
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.VerticalScope(_schedulePanelStyle))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    int activeCount = _scheduledCalls.Count(call => call != null && call.enabled && IsScheduledCallRunnable(call));
                    EditorGUILayout.LabelField(new GUIContent("Scheduler", "Run or schedule debug actions, router logs, router events, and router state changes under selected conditions."), _sectionHeaderStyle);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(new GUIContent($"{activeCount}/{_scheduledCalls.Count} active", "Active runnable scheduled actions / total scheduled actions."), _mutedMiniLabelStyle, GUILayout.Width(82f));
                }

                DrawSchedulerHeaderControls();

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    _schedulerCleanupOpen = EditorGUILayout.Foldout(_schedulerCleanupOpen, new GUIContent("Cleanup", "Show safe cleanup actions for missing, disabled, or all scheduled debug actions."), true);
                    if (EditorGUI.EndChangeCheck())
                        UtilityWindowPrefs.SetBool(SchedulerCleanupOpenPrefsKey, _schedulerCleanupOpen);
                }

                if (_schedulerCleanupOpen)
                {
                    using (new EditorGUI.DisabledScope(_scheduledCalls.Count == 0))
                    {
                        DrawResponsiveCommandRow(
                            () => { if (DrawTintedButton(new GUIContent(_drawingSupportColumn ? "Missing" : "Remove Missing", "Remove scheduled actions whose target or method can no longer be resolved."), UtilityWindowTheme.Red, GUILayout.Width(_drawingSupportColumn ? 78f : 118f))) RemoveMissingScheduledCalls(); },
                            () => { if (DrawTintedButton(new GUIContent(_drawingSupportColumn ? "Disabled" : "Remove Disabled", "Remove scheduled actions that are currently paused/disabled."), UtilityWindowTheme.Neutral, GUILayout.Width(_drawingSupportColumn ? 82f : 118f))) RemoveDisabledScheduledCalls(); },
                            () => { if (DrawTintedButton(new GUIContent(_drawingSupportColumn ? "Clear" : "Clear All", "Remove every scheduled debug action after confirmation."), UtilityWindowTheme.Red, GUILayout.Width(_drawingSupportColumn ? 62f : 86f))) ClearAllScheduledCalls(); });
                    }
                }

                if (_scheduledCalls.Count == 0)
                {
                    DrawSchedulableTargetsBrowser();
                    return;
                }

                List<ScheduledCall> visibleCalls = GetFilteredScheduledCalls();
                if (visibleCalls.Count == 0)
                {
                    EditorGUILayout.HelpBox("Search excludes every scheduled action. Clear search or match a target, method, display name, status, condition, or scene path.", MessageType.None);
                    return;
                }

                float contentHeight = EstimateScheduleContentHeight(visibleCalls);
                if (!_drawingSupportColumn && visibleCalls.Count > 4)
                {
                    float scheduleHeight = GetSupportListHeight(contentHeight, _drawingSupportColumn ? 220f : 260f);
                    _scheduleScroll = EditorGUILayout.BeginScrollView(_scheduleScroll, GUILayout.Height(scheduleHeight));
                    try
                    {
                        DrawScheduledRows(visibleCalls);
                    }
                    finally
                    {
                        EditorGUILayout.EndScrollView();
                    }
                }
                else
                {
                    DrawScheduledRows(visibleCalls);
                }
            }
        }

        private void DrawSchedulerHeaderControls()
        {
            float width = GetCurrentContentWidth(36f);

            // These compact widths allow the full row to fit in the right rail at normal support-column sizes.
            float startWidth = _drawingSupportColumn ? 48f : 76f;
            float stopWidth = _drawingSupportColumn ? 46f : 72f;
            float resolveWidth = _drawingSupportColumn ? 58f : 70f;
            float logWidth = _drawingSupportColumn ? 54f : 76f;
            float eventWidth = _drawingSupportColumn ? 58f : 82f;

            float totalWidth = startWidth + stopWidth + resolveWidth + logWidth + eventWidth + 20f;

            if (width >= totalWidth)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawSchedulerStartButton(startWidth);
                    DrawSchedulerStopButton(stopWidth);
                    DrawSchedulerResolveButton(resolveWidth);
                    DrawSchedulerNewLogButton(logWidth);
                    DrawSchedulerNewEventButton(eventWidth);
                }

                return;
            }

            DrawResponsiveCommandRow(
                width,
                () => DrawSchedulerStartButton(startWidth),
                () => DrawSchedulerStopButton(stopWidth),
                () => DrawSchedulerResolveButton(resolveWidth),
                () => DrawSchedulerNewLogButton(logWidth),
                () => DrawSchedulerNewEventButton(eventWidth));
        }

        private void DrawSchedulerStartButton(float width)
        {
            using (new EditorGUI.DisabledScope(_scheduledCalls.Count == 0))
            {
                if (!DrawTintedButton(new GUIContent(_drawingSupportColumn ? "Start" : "Start All", "Enable every scheduled debug action."), UtilityWindowTheme.Green, GUILayout.Width(width)))
                    return;

                foreach (ScheduledCall call in _scheduledCalls)
                {
                    if (call != null)
                        call.enabled = true;
                }

                SaveScheduledCalls();
            }
        }

        private void DrawSchedulerStopButton(float width)
        {
            using (new EditorGUI.DisabledScope(_scheduledCalls.Count == 0))
            {
                if (!DrawTintedButton(new GUIContent(_drawingSupportColumn ? "Stop" : "Stop All", "Pause every scheduled debug action without deleting it."), UtilityWindowTheme.Red, GUILayout.Width(width)))
                    return;

                foreach (ScheduledCall call in _scheduledCalls)
                {
                    if (call != null)
                        call.enabled = false;
                }

                SaveScheduledCalls();
            }
        }

        private void DrawSchedulerResolveButton(float width)
        {
            if (DrawTintedButton(new GUIContent("Resolve", "Re-resolve saved target references and method names after scene or domain changes."), UtilityWindowTheme.Blue, GUILayout.Width(width)))
                ResolveScheduledReferences();
        }

        private void DrawSchedulerNewLogButton(float width)
        {
            if (DrawTintedButton(new GUIContent(_drawingSupportColumn ? "Log" : "New Log", "Create a scheduled DebugRouter log action."), UtilityWindowTheme.Cyan, GUILayout.Width(width)))
                AddScheduledRouterLog();
        }

        private void DrawSchedulerNewEventButton(float width)
        {
            if (DrawTintedButton(new GUIContent(_drawingSupportColumn ? "Event" : "New Event", "Create a scheduled DebugRouter event/signal action."), UtilityWindowTheme.Purple, GUILayout.Width(width)))
                AddScheduledRouterSignal();
        }

        private void DrawScheduledRows(List<ScheduledCall> visibleCalls)
        {
            for (int i = 0; i < visibleCalls.Count; i++)
            {
                ScheduledCall call = visibleCalls[i];
                if (call == null)
                    continue;

                DrawScheduledCallRow(call);
            }
        }

        private void DrawScheduledCallRow(ScheduledCall call)
        {
            string statusLabel = GetScheduledStatusLabel(call);
            Color rowTint = GetScheduledStatusTint(call);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(rowTint, 0.14f, 0.07f, 6, 4)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    bool nextEnabled;
                    using (new GuiBackgroundScope(call.enabled ? UtilityWindowTheme.Green : UtilityWindowTheme.Red))
                        nextEnabled = EditorGUILayout.Toggle(new GUIContent(string.Empty, call.enabled ? "Disable this scheduled action." : "Enable this scheduled action."), call.enabled, GUILayout.Width(18f));
                    if (EditorGUI.EndChangeCheck())
                    {
                        call.enabled = nextEnabled;
                        SaveScheduledCalls();
                    }

                    call.foldout = EditorGUILayout.Foldout(call.foldout, new GUIContent(GetScheduledDisplayName(call), "Expand to edit this scheduled action's target, interval, condition, and router/context action settings."), true);
                    GUILayout.FlexibleSpace();
                    DrawCountPill(statusLabel, rowTint, _drawingSupportColumn ? 74f : 96f);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(18f);
                    DrawCountPill($"#{call.fireCount}", call.enabled ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, 48f);
                    if (DrawTintedButton(new GUIContent("Run", "Run this scheduled action once immediately."), UtilityWindowTheme.Blue, GUILayout.Width(48f)) && IsScheduledCallRunnable(call))
                    {
                        call.lastResult = $"{DateTime.Now:HH:mm:ss} - {ExecuteScheduledCall(call)}";
                        call.fireCount++;
                        SaveScheduledCalls();
                    }
                    if (DrawTintedButton(new GUIContent(_drawingSupportColumn ? "Del" : "Remove", "Delete this scheduled action after confirmation."), UtilityWindowTheme.Red, GUILayout.Width(_drawingSupportColumn ? 44f : 66f)))
                    {
                        if (EditorUtility.DisplayDialog("Remove Scheduled Debug Action", $"Remove '{GetScheduledDisplayName(call)}'?", "Remove", "Cancel"))
                        {
                            _scheduledCalls.Remove(call);
                            SaveScheduledCalls();
                        }
                        return;
                    }
                    GUILayout.FlexibleSpace();
                }

                if (!call.foldout)
                {
                    EditorGUILayout.LabelField($"Every {call.intervalSeconds:0.##}s | {call.conditionMode} | Last: {call.lastResult}", _mutedMiniLabelStyle);
                    return;
                }

                EditorGUI.BeginChangeCheck();
                call.actionKind = (ScheduledActionKind)EditorGUILayout.EnumPopup(new GUIContent("Action", "Choose whether this schedule runs a context method, router log, router event, or router state update."), call.actionKind);
                using (new EditorGUILayout.HorizontalScope())
                {
                    call.target = EditorGUILayout.ObjectField(new GUIContent("Target", "Scene object or component used by this scheduled action."), call.target, typeof(UnityEngine.Object), true);
                    call.intervalSeconds = Mathf.Max(0.1f, EditorGUILayout.FloatField(new GUIContent("Interval", "Seconds between each automatic execution while enabled and conditions pass."), call.intervalSeconds, GUILayout.MaxWidth(_drawingSupportColumn ? 150f : 220f)));
                }

                DrawScheduledActionFields(call);

                using (new EditorGUILayout.HorizontalScope())
                {
                    call.conditionMode = (ConditionMode)EditorGUILayout.EnumPopup(new GUIContent("Condition", "Condition that must be true before this scheduled action can run."), call.conditionMode);
                    if (call.conditionMode == ConditionMode.BoolMemberTrue || call.conditionMode == ConditionMode.BoolMemberFalse)
                    {
                        call.conditionTarget = EditorGUILayout.ObjectField(call.conditionTarget != null ? call.conditionTarget : call.target, typeof(UnityEngine.Object), true, GUILayout.MaxWidth(_drawingSupportColumn ? 140f : 180f));
                        UnityEngine.Object conditionTarget = call.conditionTarget != null ? call.conditionTarget : call.target;
                        DrawBoolMemberPopup(conditionTarget, ref call.conditionMemberName, GUILayout.MaxWidth(_drawingSupportColumn ? 160f : 220f));
                    }
                }

                if (EditorGUI.EndChangeCheck())
                {
                    CaptureScheduleIdentity(call);
                    call.nextRunTime = EditorApplication.timeSinceStartup + Mathf.Max(0.1f, call.intervalSeconds);
                    SaveScheduledCalls();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"Last: {call.lastResult}", _mutedMiniLabelStyle);
                    GUILayout.FlexibleSpace();
                    double remaining = Math.Max(0.0, call.nextRunTime - EditorApplication.timeSinceStartup);
                    DrawCountPill(call.enabled ? $"Next: {remaining:0.0}s" : "Paused", call.enabled ? UtilityWindowTheme.Green : UtilityWindowTheme.Red, 92f);
                }
            }
        }

        private void DrawScheduledActionFields(ScheduledCall call)
        {
            switch (call.actionKind)
            {
                case ScheduledActionKind.RouterLog:
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        call.routerChannel = EditorGUILayout.TextField(new GUIContent("Channel", "DebugRouter channel name used by this scheduled log."), string.IsNullOrWhiteSpace(call.routerChannel) ? "Scheduler" : call.routerChannel);
                        call.routerLevel = (int)(DebugRouter.Level)EditorGUILayout.EnumPopup((DebugRouter.Level)Mathf.Clamp(call.routerLevel, 0, 3), GUILayout.MaxWidth(120f));
                    }
                    call.routerMessage = EditorGUILayout.TextField(new GUIContent("Message", "Message body emitted by this scheduled router log."), string.IsNullOrWhiteSpace(call.routerMessage) ? "Scheduled debug log" : call.routerMessage);
                    break;
                case ScheduledActionKind.RouterSignal:
                    call.routerSignalName = EditorGUILayout.TextField(new GUIContent("Event", "DebugRouter signal/event name emitted by this scheduled action."), string.IsNullOrWhiteSpace(call.routerSignalName) ? "ScheduledEvent" : call.routerSignalName);
                    call.routerMessage = EditorGUILayout.TextField(new GUIContent("Details", "Additional details attached to the scheduled router event/state."), call.routerMessage ?? string.Empty);
                    break;
                case ScheduledActionKind.RouterState:
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        call.routerStateName = EditorGUILayout.TextField(new GUIContent("State", "DebugRouter state name updated by this scheduled action."), string.IsNullOrWhiteSpace(call.routerStateName) ? "ScheduledState" : call.routerStateName);
                        call.routerStateValue = EditorGUILayout.Toggle(new GUIContent("Value", "Boolean value assigned to the router state when this scheduled action runs."), call.routerStateValue, GUILayout.MaxWidth(130f));
                    }
                    call.routerMessage = EditorGUILayout.TextField(new GUIContent("Details", "Additional details attached to the scheduled router event/state."), call.routerMessage ?? string.Empty);
                    break;
                case ScheduledActionKind.ContextMethod:
                default:
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.TextField(new GUIContent("Method", "Resolved reflected [ContextMenu] method name. This is read-only for context-method schedules."), call.methodName ?? string.Empty);
                    break;
            }
        }

        private void RemoveMissingScheduledCalls()
        {
            int count = _scheduledCalls.Count(call => call == null || !IsScheduledCallRunnable(call));
            if (count == 0)
            {
                _status = "No missing scheduled debug actions to remove.";
                return;
            }

            if (!EditorUtility.DisplayDialog("Remove Missing Scheduled Actions", $"Remove {count} scheduled action(s) with missing targets or methods?", "Remove Missing", "Cancel"))
                return;

            _scheduledCalls.RemoveAll(call => call == null || !IsScheduledCallRunnable(call));
            SaveScheduledCalls();
            _status = $"Removed {count} missing scheduled debug action(s).";
        }

        private void RemoveDisabledScheduledCalls()
        {
            int count = _scheduledCalls.Count(call => call != null && !call.enabled);
            if (count == 0)
            {
                _status = "No disabled scheduled debug actions to remove.";
                return;
            }

            if (!EditorUtility.DisplayDialog("Remove Disabled Scheduled Actions", $"Remove {count} disabled scheduled action(s)?", "Remove Disabled", "Cancel"))
                return;

            _scheduledCalls.RemoveAll(call => call != null && !call.enabled);
            SaveScheduledCalls();
            _status = $"Removed {count} disabled scheduled debug action(s).";
        }

        private void ClearAllScheduledCalls()
        {
            if (_scheduledCalls.Count == 0)
                return;

            if (!EditorUtility.DisplayDialog("Clear All Scheduled Debug Actions", $"Remove all {_scheduledCalls.Count} scheduled debug action(s)?", "Clear All", "Cancel"))
                return;

            _scheduledCalls.Clear();
            SaveScheduledCalls();
            _status = "Cleared all scheduled debug actions.";
        }

        private void AddScheduledCall(UnityEngine.Object target, MethodInfo method)
        {
            if (target == null || method == null)
                return;

            CaptureObjectIdentity(target, out string targetId, out string targetPath, out string targetType);
            ScheduledCall existing = _scheduledCalls.FirstOrDefault(c => c != null && c.actionKind == ScheduledActionKind.ContextMethod && c.targetGlobalId == targetId && c.methodName == method.Name);
            if (existing != null)
            {
                existing.target = target;
                existing.enabled = true;
                existing.foldout = true;
                existing.displayName = $"{target.name}.{GetContextMenuName(method)}";
                existing.nextRunTime = EditorApplication.timeSinceStartup + Mathf.Max(0.1f, existing.intervalSeconds);
                SaveScheduledCalls();
                return;
            }

            ScheduledCall call = new ScheduledCall
            {
                target = target,
                targetGlobalId = targetId,
                targetScenePath = targetPath,
                targetTypeName = targetType,
                actionKind = ScheduledActionKind.ContextMethod,
                methodName = method.Name,
                displayName = $"{target.name}.{GetContextMenuName(method)}",
                intervalSeconds = 5f,
                nextRunTime = EditorApplication.timeSinceStartup + 5f,
                conditionMode = ConditionMode.PlayModeOnly
            };
            _scheduledCalls.Add(call);
            SaveScheduledCalls();
        }

        private void AddScheduledRouterLog()
        {
            UnityEngine.Object target = Selection.activeObject;
            var call = new ScheduledCall
            {
                target = target,
                actionKind = ScheduledActionKind.RouterLog,
                displayName = target != null ? $"{target.name}.RouterLog" : "RouterLog",
                routerChannel = "Scheduler",
                routerMessage = "Scheduled debug log",
                routerLevel = (int)DebugRouter.Level.Info,
                conditionMode = ConditionMode.Always,
                intervalSeconds = 5f,
                nextRunTime = EditorApplication.timeSinceStartup + 5f,
                foldout = true
            };
            if (target != null)
                CaptureObjectIdentity(target, out call.targetGlobalId, out call.targetScenePath, out call.targetTypeName);
            _scheduledCalls.Add(call);
            SaveScheduledCalls();
        }

        private void AddScheduledRouterSignal()
        {
            UnityEngine.Object target = Selection.activeObject;
            var call = new ScheduledCall
            {
                target = target,
                actionKind = ScheduledActionKind.RouterSignal,
                displayName = target != null ? $"{target.name}.ScheduledEvent" : "ScheduledEvent",
                routerSignalName = "ScheduledEvent",
                routerMessage = "Scheduled event fired",
                conditionMode = ConditionMode.Always,
                intervalSeconds = 5f,
                nextRunTime = EditorApplication.timeSinceStartup + 5f,
                foldout = true
            };
            if (target != null)
                CaptureObjectIdentity(target, out call.targetGlobalId, out call.targetScenePath, out call.targetTypeName);
            _scheduledCalls.Add(call);
            SaveScheduledCalls();
        }

        private void TickScheduledActions(double now)
        {
            if (_scheduledCalls.Count == 0)
                return;

            bool changed = false;
            for (int i = 0; i < _scheduledCalls.Count; i++)
            {
                ScheduledCall call = _scheduledCalls[i];
                if (call == null || !call.enabled || !IsScheduledCallRunnable(call))
                    continue;

                if (call.target == null)
                    TryResolveScheduledCall(call);

                if (call.nextRunTime <= 0)
                    call.nextRunTime = now + Mathf.Max(0.1f, call.intervalSeconds);

                if (now < call.nextRunTime)
                    continue;

                call.nextRunTime = now + Mathf.Max(0.1f, call.intervalSeconds);

                if (!EvaluateCondition(call))
                    continue;

                string result = ExecuteScheduledCall(call);
                call.fireCount++;
                call.lastResult = $"{DateTime.Now:HH:mm:ss} - {result}";
                changed = true;
            }

            if (changed)
            {
                SaveScheduledCalls();
                RequestRepaintThrottled();
            }
        }

        private string ExecuteScheduledCall(ScheduledCall call)
        {
            if (call == null)
                return "Invalid call";

            switch (call.actionKind)
            {
                case ScheduledActionKind.RouterLog:
                    DebugRouter.Log(call.target, string.IsNullOrWhiteSpace(call.routerChannel) ? "Scheduler" : call.routerChannel, string.IsNullOrWhiteSpace(call.routerMessage) ? "Scheduled debug log" : call.routerMessage, (DebugRouter.Level)Mathf.Clamp(call.routerLevel, 0, 3), true);
                    RefreshRouterSnapshots();
                    return "Router log emitted";
                case ScheduledActionKind.RouterSignal:
                    DebugRouter.Signal(call.target, string.IsNullOrWhiteSpace(call.routerSignalName) ? "ScheduledEvent" : call.routerSignalName, call.routerMessage, true);
                    RefreshRouterSnapshots();
                    return "Router event fired";
                case ScheduledActionKind.RouterState:
                    DebugRouter.SetState(call.target, string.IsNullOrWhiteSpace(call.routerStateName) ? "ScheduledState" : call.routerStateName, call.routerStateValue, call.routerMessage, true);
                    RefreshRouterSnapshots();
                    return "Router state set";
                case ScheduledActionKind.ContextMethod:
                default:
                    return InvokeContextAction(call.target, call.methodName);
            }
        }

        private bool IsScheduledCallRunnable(ScheduledCall call)
        {
            if (call == null)
                return false;

            if (call.actionKind != ScheduledActionKind.ContextMethod)
                return true;

            return call.target != null && !string.IsNullOrEmpty(call.methodName) && FindMethod(call.target.GetType(), call.methodName) != null;
        }

        private string GetScheduledDisplayName(ScheduledCall call)
        {
            if (call == null)
                return "Invalid";
            if (!string.IsNullOrWhiteSpace(call.displayName))
                return call.displayName;

            switch (call.actionKind)
            {
                case ScheduledActionKind.RouterLog:
                    return $"Router Log / {(string.IsNullOrWhiteSpace(call.routerChannel) ? "Scheduler" : call.routerChannel)}";
                case ScheduledActionKind.RouterSignal:
                    return $"Router Event / {(string.IsNullOrWhiteSpace(call.routerSignalName) ? "ScheduledEvent" : call.routerSignalName)}";
                case ScheduledActionKind.RouterState:
                    return $"Router State / {(string.IsNullOrWhiteSpace(call.routerStateName) ? "ScheduledState" : call.routerStateName)}";
                default:
                    return call.methodName ?? "Context Action";
            }
        }

        private void DrawSchedulableTargetsBrowser()
        {
            List<DebugComponentInfo> candidates = GetFilteredComponents()
                .Where(info => info != null && info.component != null && info.contextActions.Count > 0)
                .ToList();

            EditorGUILayout.HelpBox("No scheduled actions are active. Select a component below to focus it in the component browser, or create a Router log/event with the buttons above.", MessageType.None);

            if (candidates.Count == 0)
            {
                EditorGUILayout.HelpBox("No visible components expose schedulable [ContextMenu] actions. Refresh the scene or adjust search/filter settings.", MessageType.None);
                return;
            }

            if (_groupMode == GroupMode.SceneObject)
            {
                foreach (var group in candidates.GroupBy(info => info.objectPath).OrderBy(g => g.Key))
                    DrawSchedulableCandidateGroup(group.Key, group.ToList());
            }
            else
            {
                foreach (var group in candidates.GroupBy(info => info.component.GetType()).OrderBy(g => g.Key.Name))
                    DrawSchedulableCandidateGroup(group.Key.Name, group.OrderBy(info => info.objectPath).ToList());
            }
        }

        private void DrawSchedulableCandidateGroup(string label, List<DebugComponentInfo> infos)
        {
            using (new EditorGUILayout.VerticalScope(_fieldPanelStyle))
            {
                EditorGUILayout.LabelField(new GUIContent(label, $"Schedulable target group: {label}."), _categoryLabelStyle);
                for (int i = 0; i < infos.Count; i++)
                {
                    DebugComponentInfo info = infos[i];
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(new GUIContent($"{info.component.GetType().Name} · {info.contextActions.Count} actions", info.objectPath), _mutedMiniLabelStyle);
                        if (DrawTintedButton(new GUIContent("Focus", "Select, ping, and expand this schedulable target in the component browser."), UtilityWindowTheme.Blue, GUILayout.Width(54f)))
                            FocusComponentFromScheduler(info);
                    }
                }
            }
        }

        private void FocusComponentFromScheduler(DebugComponentInfo info)
        {
            if (info == null || info.component == null)
                return;

            int id = info.component.GetInstanceID();
            SetInstanceFoldout(id, true);
            if (info.component != null)
            {
                SetTypeFoldout(info.component.GetType().FullName, true);
                SetSceneObjectFoldout(info.objectPath, true);
                Selection.activeObject = info.component.gameObject;
                EditorGUIUtility.PingObject(info.component.gameObject);
                _status = $"Focused {info.component.GetType().Name} for scheduling.";
            }
        }

        private float EstimateScheduleContentHeight(List<ScheduledCall> calls)
        {
            float height = 8f;
            foreach (ScheduledCall call in calls)
            {
                if (call == null)
                    continue;

                height += call.foldout ? 150f : 46f;
            }
            return Mathf.Clamp(height, 120f, 1200f);
        }

        private void LoadScheduledCalls()
        {
            _scheduledCalls.Clear();
            string json = UtilityWindowPrefs.GetString(SchedulePrefsKey, string.Empty);
            if (string.IsNullOrEmpty(json))
                return;

            try
            {
                ScheduledCallSaveData data = JsonUtility.FromJson<ScheduledCallSaveData>(json);
                if (data == null || data.calls == null)
                    return;

                foreach (ScheduledCallSave saved in data.calls)
                {
                    if (saved == null)
                        continue;

                    ScheduledCall call = new ScheduledCall
                    {
                        targetGlobalId = saved.targetGlobalId,
                        targetScenePath = saved.targetScenePath,
                        targetTypeName = saved.targetTypeName,
                        actionKind = (ScheduledActionKind)Mathf.Clamp(saved.actionKind, 0, Enum.GetValues(typeof(ScheduledActionKind)).Length - 1),
                        methodName = saved.methodName,
                        displayName = saved.displayName,
                        routerChannel = string.IsNullOrEmpty(saved.routerChannel) ? "Scheduler" : saved.routerChannel,
                        routerMessage = string.IsNullOrEmpty(saved.routerMessage) ? "Scheduled debug log" : saved.routerMessage,
                        routerLevel = Mathf.Clamp(saved.routerLevel, 0, 3),
                        routerSignalName = string.IsNullOrEmpty(saved.routerSignalName) ? "ScheduledEvent" : saved.routerSignalName,
                        routerStateName = string.IsNullOrEmpty(saved.routerStateName) ? "ScheduledState" : saved.routerStateName,
                        routerStateValue = saved.routerStateValue,
                        enabled = saved.enabled,
                        foldout = saved.foldout,
                        intervalSeconds = Mathf.Max(0.1f, saved.intervalSeconds),
                        conditionMode = (ConditionMode)Mathf.Clamp(saved.conditionMode, 0, Enum.GetValues(typeof(ConditionMode)).Length - 1),
                        conditionTargetGlobalId = saved.conditionTargetGlobalId,
                        conditionScenePath = saved.conditionScenePath,
                        conditionTargetTypeName = saved.conditionTargetTypeName,
                        conditionMemberName = saved.conditionMemberName,
                        fireCount = saved.fireCount,
                        lastResult = string.IsNullOrEmpty(saved.lastResult) ? "Never" : saved.lastResult,
                        nextRunTime = EditorApplication.timeSinceStartup + Mathf.Max(0.1f, saved.intervalSeconds)
                    };
                    _scheduledCalls.Add(call);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DebugControlWindow] Failed to load scheduled debug actions. " + ex.Message);
            }
        }

        private void SaveScheduledCalls()
        {
            ScheduledCallSaveData data = new ScheduledCallSaveData();
            foreach (ScheduledCall call in _scheduledCalls)
            {
                if (call == null)
                    continue;

                CaptureScheduleIdentity(call);
                data.calls.Add(new ScheduledCallSave
                {
                    targetGlobalId = call.targetGlobalId,
                    targetScenePath = call.targetScenePath,
                    targetTypeName = call.targetTypeName,
                    actionKind = (int)call.actionKind,
                    methodName = call.methodName,
                    displayName = call.displayName,
                    routerChannel = call.routerChannel,
                    routerMessage = call.routerMessage,
                    routerLevel = call.routerLevel,
                    routerSignalName = call.routerSignalName,
                    routerStateName = call.routerStateName,
                    routerStateValue = call.routerStateValue,
                    enabled = call.enabled,
                    foldout = call.foldout,
                    intervalSeconds = call.intervalSeconds,
                    conditionMode = (int)call.conditionMode,
                    conditionTargetGlobalId = call.conditionTargetGlobalId,
                    conditionScenePath = call.conditionScenePath,
                    conditionTargetTypeName = call.conditionTargetTypeName,
                    conditionMemberName = call.conditionMemberName,
                    fireCount = call.fireCount,
                    lastResult = call.lastResult
                });
            }

            UtilityWindowPrefs.SetString(SchedulePrefsKey, JsonUtility.ToJson(data));
        }

        private void CaptureScheduleIdentity(ScheduledCall call)
        {
            if (call == null)
                return;

            if (call.target != null)
                CaptureObjectIdentity(call.target, out call.targetGlobalId, out call.targetScenePath, out call.targetTypeName);

            if (call.conditionTarget != null)
                CaptureObjectIdentity(call.conditionTarget, out call.conditionTargetGlobalId, out call.conditionScenePath, out call.conditionTargetTypeName);
        }

        private static void CaptureObjectIdentity(UnityEngine.Object obj, out string globalId, out string scenePath, out string typeName)
        {
            globalId = string.Empty;
            scenePath = string.Empty;
            typeName = string.Empty;

            if (obj == null)
                return;

            typeName = obj.GetType().AssemblyQualifiedName;
            Component component = obj as Component;
            GameObject go = obj as GameObject;
            Transform transform = component != null ? component.transform : (go != null ? go.transform : null);
            if (transform != null)
                scenePath = GetTransformPath(transform);

            try
            {
                globalId = GlobalObjectId.GetGlobalObjectIdSlow(obj).ToString();
            }
            catch
            {
                globalId = string.Empty;
            }
        }

        private void ResolveScheduledReferences()
        {
            foreach (ScheduledCall call in _scheduledCalls)
                TryResolveScheduledCall(call);
        }

        private bool TryResolveScheduledCall(ScheduledCall call)
        {
            if (call == null)
                return false;

            if (call.target == null)
                call.target = ResolveObject(call.targetGlobalId, call.targetScenePath, call.targetTypeName);

            if (call.conditionTarget == null)
                call.conditionTarget = ResolveObject(call.conditionTargetGlobalId, call.conditionScenePath, call.conditionTargetTypeName);

            return call.target != null;
        }

        private static UnityEngine.Object ResolveObject(string globalId, string scenePath, string typeName)
        {
            if (!string.IsNullOrEmpty(globalId))
            {
                try
                {
                    GlobalObjectId parsed;
                    if (GlobalObjectId.TryParse(globalId, out parsed))
                    {
                        UnityEngine.Object obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(parsed);
                        if (obj != null)
                            return obj;
                    }
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(scenePath))
            {
                GameObject go = FindSceneObjectByPath(scenePath);
                if (go != null)
                {
                    Type type = ResolveType(typeName);
                    if (type == null || type == typeof(GameObject))
                        return go;

                    Component component = go.GetComponent(type);
                    if (component != null)
                        return component;
                }
            }

            return null;
        }

        private static GameObject FindSceneObjectByPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            string[] parts = path.Split('/');
            if (parts.Length == 0)
                return null;

            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                GameObject[] roots = scene.GetRootGameObjects();
                for (int r = 0; r < roots.Length; r++)
                {
                    if (roots[r].name != parts[0])
                        continue;

                    Transform current = roots[r].transform;
                    bool matched = true;
                    for (int i = 1; i < parts.Length; i++)
                    {
                        Transform child = current.Find(parts[i]);
                        if (child == null)
                        {
                            matched = false;
                            break;
                        }
                        current = child;
                    }

                    if (matched)
                        return current.gameObject;
                }
            }

            return null;
        }

        private static Type ResolveType(string assemblyQualifiedName)
        {
            if (string.IsNullOrEmpty(assemblyQualifiedName))
                return null;

            Type type = Type.GetType(assemblyQualifiedName);
            if (type != null)
                return type;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(assemblyQualifiedName);
                if (type != null)
                    return type;
            }
            return null;
        }

        private bool EvaluateCondition(ScheduledCall call)
        {
            if (call == null)
                return false;

            switch (call.conditionMode)
            {
                case ConditionMode.Always:
                    return true;
                case ConditionMode.PlayModeOnly:
                    return EditorApplication.isPlaying;
                case ConditionMode.EditModeOnly:
                    return !EditorApplication.isPlaying;
                case ConditionMode.SelectedTarget:
                    return IsSelected(call.target);
                case ConditionMode.SelectionInsideTarget:
                    return IsSelectionInsideTarget(call.target);
                case ConditionMode.TargetEnabled:
                    return IsObjectEnabled(call.target);
                case ConditionMode.TargetDisabled:
                    return !IsObjectEnabled(call.target);
                case ConditionMode.TargetActiveInHierarchy:
                    return IsObjectActiveInHierarchy(call.target);
                case ConditionMode.TargetInactiveInHierarchy:
                    return !IsObjectActiveInHierarchy(call.target);
                case ConditionMode.BoolMemberTrue:
                    return TryGetBoolMember(call.conditionTarget != null ? call.conditionTarget : call.target, call.conditionMemberName, out bool vTrue) && vTrue;
                case ConditionMode.BoolMemberFalse:
                    return TryGetBoolMember(call.conditionTarget != null ? call.conditionTarget : call.target, call.conditionMemberName, out bool vFalse) && !vFalse;
                default:
                    return true;
            }
        }

        private static bool IsSelected(UnityEngine.Object target)
        {
            if (target == null)
                return false;

            GameObject targetGo = null;
            if (target is GameObject go)
                targetGo = go;
            else if (target is Component c)
                targetGo = c.gameObject;

            if (targetGo == null || Selection.activeGameObject == null)
                return false;

            return Selection.activeGameObject == targetGo || Selection.activeGameObject.transform.IsChildOf(targetGo.transform) || targetGo.transform.IsChildOf(Selection.activeGameObject.transform);
        }

        private static bool IsSelectionInsideTarget(UnityEngine.Object target)
        {
            GameObject targetGo = null;
            if (target is GameObject go)
                targetGo = go;
            else if (target is Component c)
                targetGo = c.gameObject;

            if (targetGo == null || Selection.activeGameObject == null)
                return false;

            return Selection.activeGameObject == targetGo || Selection.activeGameObject.transform.IsChildOf(targetGo.transform);
        }

        private static bool IsObjectActiveInHierarchy(UnityEngine.Object target)
        {
            if (target == null)
                return false;
            if (target is GameObject go)
                return go.activeInHierarchy;
            if (target is Component c)
                return c.gameObject.activeInHierarchy;
            return true;
        }

        private static bool IsUnderCurrentSelection(GameObject go)
        {
            if (go == null)
                return false;

            GameObject[] selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0)
                return false;

            for (int i = 0; i < selected.Length; i++)
            {
                GameObject selectedGo = selected[i];
                if (selectedGo == null)
                    continue;

                if (go == selectedGo)
                    return true;

                if (go.transform.IsChildOf(selectedGo.transform))
                    return true;
            }

            return false;
        }

        private static bool IsObjectEnabled(UnityEngine.Object target)
        {
            if (target == null)
                return false;

            if (target is Behaviour b)
                return b.enabled && b.gameObject.activeInHierarchy;

            if (target is Component c)
                return c.gameObject.activeInHierarchy;

            if (target is GameObject go)
                return go.activeInHierarchy;

            return true;
        }

        private static string InvokeContextAction(UnityEngine.Object target, string methodName)
        {
            if (target == null)
                return "Target missing";

            MethodInfo method = FindMethod(target.GetType(), methodName);
            if (method == null)
                return $"Method not found: {methodName}";

            if (method.GetParameters().Length != 0)
                return $"Skipped parameterized method: {methodName}";

            try
            {
                Undo.RecordObject(target, $"Run {methodName}");
                method.Invoke(target, null);
                EditorUtility.SetDirty(target);
                return $"Ran {methodName}";
            }
            catch (TargetInvocationException ex)
            {
                Debug.LogException(ex.InnerException ?? ex, target);
                return $"Exception: {(ex.InnerException != null ? ex.InnerException.Message : ex.Message)}";
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, target);
                return $"Exception: {ex.Message}";
            }
        }

        private void DrawBoolMemberPopup(UnityEngine.Object target, ref string memberName, params GUILayoutOption[] options)
        {
            string[] names = GetBoolMemberNames(target).ToArray();
            if (names.Length == 0)
            {
                memberName = EditorGUILayout.TextField(new GUIContent(GUIContent.none.text, "Name of the bool member used by the selected condition."), memberName, options);
                return;
            }

            int index = Array.IndexOf(names, memberName);
            if (index < 0)
                index = 0;

            int next = EditorGUILayout.Popup(index, names, options);
            memberName = names[Mathf.Clamp(next, 0, names.Length - 1)];
        }

        private static IEnumerable<string> GetBoolMemberNames(UnityEngine.Object target)
        {
            if (target == null)
                yield break;

            Type type = target.GetType();
            var seen = new HashSet<string>();

            foreach (FieldInfo field in EnumerateFields(type, includeStatic: false))
            {
                if (field.FieldType != typeof(bool))
                    continue;

                if (seen.Add(field.Name))
                    yield return field.Name;
            }

            for (Type t = type; t != null && t != typeof(object); t = t.BaseType)
            {
                foreach (PropertyInfo property in t.GetProperties(InstanceFlags | BindingFlags.DeclaredOnly))
                {
                    if (property.PropertyType != typeof(bool))
                        continue;

                    if (property.GetIndexParameters().Length != 0)
                        continue;

                    MethodInfo getter = property.GetGetMethod(nonPublic: true);
                    if (getter == null)
                        continue;

                    if (seen.Add(property.Name))
                        yield return property.Name;
                }
            }
        }

        private static bool TryGetBoolMember(UnityEngine.Object target, string memberName, out bool value)
        {
            value = false;
            if (target == null || string.IsNullOrWhiteSpace(memberName))
                return false;

            Type type = target.GetType();

            foreach (FieldInfo field in EnumerateFields(type, includeStatic: false))
            {
                if (field.FieldType == typeof(bool) && string.Equals(field.Name, memberName, StringComparison.Ordinal))
                {
                    value = (bool)field.GetValue(target);
                    return true;
                }
            }

            for (Type t = type; t != null && t != typeof(object); t = t.BaseType)
            {
                PropertyInfo property = t.GetProperty(memberName, InstanceFlags | BindingFlags.DeclaredOnly);
                if (property == null || property.PropertyType != typeof(bool) || property.GetIndexParameters().Length != 0)
                    continue;

                MethodInfo getter = property.GetGetMethod(nonPublic: true);
                if (getter == null)
                    continue;

                value = (bool)getter.Invoke(target, null);
                return true;
            }

            return false;
        }

    }
#endif
}
