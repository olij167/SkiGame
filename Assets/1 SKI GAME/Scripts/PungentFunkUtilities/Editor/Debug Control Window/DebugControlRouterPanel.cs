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
    using System.IO;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    public sealed partial class DebugControlWindow
    {
        // DebugRouter panel rendering, filtering, and throttled snapshot refresh logic.

        private void DrawRouterPanel()
        {
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.VerticalScope(_routerPanelStyle))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(new GUIContent("Router", "Inspect and control DebugRouter runtime channels, signals, states, and sources."), _sectionHeaderStyle);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(new GUIContent($"{_routerChannels.Count}ch | {_routerSignals.Count}sig | {_routerSources.Count}src", "Current DebugRouter channels, signals, and sources captured in runtime snapshots."), _mutedMiniLabelStyle, GUILayout.Width(118f));
                }

                DrawRouterUsageSummary();
                DrawRouterTopControls();

                List<DebugRouter.ChannelSnapshot> channels = FilteredRouterChannels();
                List<DebugRouter.SignalSnapshot> signals = FilteredRouterSignals();
                List<DebugRouter.StateSnapshot> states = FilteredRouterStates();
                List<DebugRouter.SourceSnapshot> sources = FilteredRouterSources();

                bool noRuntimeData = _routerChannels.Count == 0 && _routerSignals.Count == 0 && _routerStates.Count == 0 && _routerSources.Count == 0;
                if (noRuntimeData)
                {
                    DrawRouterSetupHelpPanel();
                    return;
                }

                if (!string.IsNullOrWhiteSpace(_search) && channels.Count + signals.Count + states.Count + sources.Count == 0)
                    EditorGUILayout.HelpBox("Search has no matching router channels, signals, states, or sources. Clear search or try an owner, channel, signal, state, or message term.", MessageType.None);

                EditorGUILayout.LabelField($"{_routerStates.Count} states | {_routerSources.Count} sources", _mutedMiniLabelStyle);

                float contentHeight = EstimateRouterContentHeight(channels.Count, signals.Count, states.Count, sources.Count);
                if (!_drawingSupportColumn && contentHeight > 280f)
                {
                    float listHeight = GetSupportListHeight(contentHeight, 280f);
                    _routerScroll = EditorGUILayout.BeginScrollView(_routerScroll, GUILayout.Height(listHeight));
                    try
                    {
                        DrawRouterSections(channels, signals, states, sources);
                    }
                    finally
                    {
                        EditorGUILayout.EndScrollView();
                    }
                }
                else
                {
                    DrawRouterSections(channels, signals, states, sources);
                }
            }
        }

        private void DrawRouterUsageSummary()
        {
            string audit = _routerScriptAuditPerformed
                ? $"Script refs: {_routerAuditLogCalls} logs · {_routerAuditSignalCalls} events · {_routerAuditStateCalls} states · {_routerAuditFiles.Count} files"
                : "Script refs: not audited";
            EditorGUILayout.LabelField(new GUIContent(audit, "Audit counts are based on explicit DebugRouter.Log/Signal/SetState/RegisterSignal calls found in project C# scripts."), _mutedMiniLabelStyle);
        }

        private void DrawRouterSetupHelpPanel()
        {
            if (_routerScriptAuditPerformed && _routerAuditLogCalls + _routerAuditSignalCalls + _routerAuditStateCalls + _routerAuditRegisterCalls == 0)
            {
                EditorGUILayout.HelpBox(
                    "No DebugRouter script references were found. Add DebugRouter.Log, DebugRouter.Signal, or DebugRouter.SetState calls to runtime/editor code to populate this panel. The router is a central runtime debug stream; it does not automatically read reflected inspector bools.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "No router runtime data yet. Data appears after code calls DebugRouter.Log(...), DebugRouter.Signal(...), DebugRouter.SetState(...), or DebugRouter.RegisterSignal(...). Use the audit button to check whether your project contains router references.",
                    MessageType.None);
            }

            using (new EditorGUILayout.VerticalScope(_fieldPanelStyle))
            {
                EditorGUILayout.LabelField(new GUIContent("Scripting reference", "Examples of code calls that populate the DebugRouter panel."), _categoryLabelStyle);
                EditorGUILayout.LabelField(new GUIContent("DebugRouter.Log(this, \"Movement\", \"Grounded state changed\", DebugRouter.Level.Info, fallbackEnabled: showDebugLogs);", "Example router log call: owner, channel, message, level, and fallback bool."), _pathLabelStyle);
                EditorGUILayout.LabelField(new GUIContent("DebugRouter.Signal(this, \"Player/Landed\", \"Impact speed: \" + speed);", "Example router signal/event call with owner, signal name, and details."), _pathLabelStyle);
                EditorGUILayout.LabelField(new GUIContent("DebugRouter.SetState(this, \"WallRunning\", isWallRunning, \"Wall side: \" + side);", "Example router state call with owner, state name, bool value, and details."), _pathLabelStyle);
            }
        }

        private void DrawRouterSections(
            List<DebugRouter.ChannelSnapshot> channels,
            List<DebugRouter.SignalSnapshot> signals,
            List<DebugRouter.StateSnapshot> states,
            List<DebugRouter.SourceSnapshot> sources)
        {
            DrawRouterChannels(channels);
            DrawRouterSignals(signals);
            DrawRouterStates(states);
            DrawRouterSources(sources);
        }

        private void DrawRouterTopControls()
        {
            DrawResponsiveCommandRow(
                () => DrawRouterTopToggle(_drawingSupportColumn ? "Global" : "Global Enabled", DebugRouter.GlobalEnabled, value => DebugRouter.GlobalEnabled = value, UtilityWindowTheme.Green, _drawingSupportColumn ? 78f : 108f),
                () => DrawRouterTopToggle(_drawingSupportColumn ? "Prefix" : "Prefix Messages", DebugRouter.PrefixMessages, value => DebugRouter.PrefixMessages = value, UtilityWindowTheme.Cyan, _drawingSupportColumn ? 78f : 118f),
                () => DrawRouterTopToggle(_drawingSupportColumn ? "Signals" : "Signals Enabled", DebugRouter.SignalsEnabled, value => DebugRouter.SignalsEnabled = value, UtilityWindowTheme.Purple, _drawingSupportColumn ? 78f : 112f),
                () =>
                {
                    if (DrawTintedButton(new GUIContent(_drawingSupportColumn ? "Refresh" : "Refresh Router Now", "Refresh cached DebugRouter channels, signals, states, and sources."), UtilityWindowTheme.Blue, GUILayout.Width(_drawingSupportColumn ? 78f : 138f)))
                    {
                        RefreshRouterSnapshots();
                        _nextRouterRefreshTime = EditorApplication.timeSinceStartup + RouterRefreshInterval;
                        _status = "Refreshed DebugRouter snapshots.";
                    }
                },
                () =>
                {
                    if (DrawTintedButton(new GUIContent(_drawingSupportColumn ? "Audit" : "Audit Script Usage", "Scan project C# files for explicit DebugRouter.Log/Signal/SetState/RegisterSignal calls."), UtilityWindowTheme.Amber, GUILayout.Width(_drawingSupportColumn ? 70f : 128f)))
                        AuditDebugRouterScriptUsage();
                });

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                _routerAdvancedOpen = EditorGUILayout.Foldout(_routerAdvancedOpen, new GUIContent("Advanced", "Show destructive or less-frequent DebugRouter controls."), true);
                if (EditorGUI.EndChangeCheck())
                    UtilityWindowPrefs.SetBool(RouterAdvancedOpenPrefsKey, _routerAdvancedOpen);
            }

            if (_routerAdvancedOpen && DrawTintedButton(new GUIContent(_drawingSupportColumn ? "Clear Runtime" : "Clear Runtime Data", "Clear all DebugRouter runtime channels, signals, states, and sources after confirmation."), UtilityWindowTheme.Red, GUILayout.Width(_drawingSupportColumn ? 108f : 132f)))
            {
                if (EditorUtility.DisplayDialog("Clear DebugRouter Runtime Data", "Clear all DebugRouter runtime channels, signals, states, and sources? This cannot be undone.", "Clear Runtime Data", "Cancel"))
                {
                    DebugRouter.ClearRuntimeData();
                    RefreshRouterSnapshots();
                    _status = "Cleared DebugRouter runtime data.";
                }
            }
        }

        private void DrawRouterTopToggle(string label, bool current, Action<bool> assign, Color tint, float width)
        {
            EditorGUI.BeginChangeCheck();
            bool next = DrawRouterToolbarToggle(current, label, tint, width);
            if (EditorGUI.EndChangeCheck())
            {
                assign(next);
                _status = "Updated DebugRouter top-level controls.";
                RefreshRouterSnapshots();
            }
        }

        private static bool DrawRouterToolbarToggle(bool value, string label, Color activeTint, float width)
        {
            Color inactive = EditorGUIUtility.isProSkin ? new Color(0.42f, 0.43f, 0.48f) : new Color(0.75f, 0.76f, 0.80f);
            using (new GuiBackgroundScope(value ? activeTint : inactive))
                return GUILayout.Toggle(value, new GUIContent(label, GetRouterTopToggleTooltip(label)), EditorStyles.toolbarButton, GUILayout.Width(width));
        }

        private static string GetRouterTopToggleTooltip(string label)
        {
            if (label.IndexOf("Global", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Enable or disable DebugRouter output globally.";
            if (label.IndexOf("Prefix", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Prefix router console messages with channel/source metadata.";
            if (label.IndexOf("Signal", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Enable or disable DebugRouter signal/event emission.";
            return "Toggle this DebugRouter option.";
        }

        private void DrawRouterChannels(List<DebugRouter.ChannelSnapshot> rows)
        {
            bool open = DrawRouterFoldout(ref _routerChannelsOpen, RouterChannelsOpenPrefsKey, $"Channels ({rows.Count}/{_routerChannels.Count})", UtilityWindowTheme.Cyan);
            if (!open)
                return;

            EditorGUILayout.LabelField(new GUIContent("Channels appear after DebugRouter.Log(...) or explicit registration.", "A channel is a named debug stream that can be enabled or disabled."), _mutedMiniLabelStyle);
            if (rows.Count == 0)
            {
                EditorGUILayout.HelpBox(_routerChannels.Count == 0 ? "No channels yet." : "Search has no matching channels.", MessageType.None);
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                DebugRouter.ChannelSnapshot row = rows[i];
                string channel = row.channel ?? "General";
                using (new EditorGUILayout.HorizontalScope(_fieldPanelStyle))
                {
                    EditorGUI.BeginChangeCheck();
                    bool next = DrawRouterSmallToggle(row.enabled, row.enabled ? "On" : "Off", UtilityWindowTheme.Cyan, 46f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        DebugRouter.SetChannelEnabled(channel, next);
                        RefreshRouterSnapshots();
                        _status = $"Set DebugRouter channel '{channel}' = {next}.";
                    }

                    EditorGUILayout.LabelField(new GUIContent(Shorten(channel, 42), channel), _categoryLabelStyle, GUILayout.MinWidth(120f));
                    GUILayout.FlexibleSpace();
                    DrawCountPill($"{row.sourceCount} src", UtilityWindowTheme.Teal, 58f);
                    DrawCountPill($"{row.logCount} logs", UtilityWindowTheme.Blue, 64f);
                    if (!_drawingSupportColumn)
                    {
                        EditorGUILayout.LabelField(new GUIContent(Shorten(row.lastOwner, 18), row.lastOwner), _pathLabelStyle, GUILayout.Width(110f));
                        EditorGUILayout.LabelField(new GUIContent(Shorten(row.lastMessage, 42), row.lastMessage), _pathLabelStyle, GUILayout.Width(210f));
                    }
                }
                if (_drawingSupportColumn && (!string.IsNullOrEmpty(row.lastOwner) || !string.IsNullOrEmpty(row.lastMessage)))
                    EditorGUILayout.LabelField(new GUIContent($"{Shorten(row.lastOwner, 24)}  {Shorten(row.lastMessage, 42)}", $"{row.lastOwner}\n{row.lastMessage}"), _pathLabelStyle);
            }
        }

        private void DrawRouterSignals(List<DebugRouter.SignalSnapshot> rows)
        {
            bool open = DrawRouterFoldout(ref _routerSignalsOpen, RouterSignalsOpenPrefsKey, $"Signals ({rows.Count}/{_routerSignals.Count})", UtilityWindowTheme.Purple);
            if (!open)
                return;

            EditorGUILayout.LabelField(new GUIContent("Signals appear after DebugRouter.Signal(...) or explicit signal registration.", "Signals are named runtime events that can optionally echo to the Console."), _mutedMiniLabelStyle);
            if (rows.Count == 0)
            {
                EditorGUILayout.HelpBox(_routerSignals.Count == 0 ? "No signals yet." : "Search has no matching signals.", MessageType.None);
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                DebugRouter.SignalSnapshot row = rows[i];
                string signalName = row.signalName ?? "Signal";
                using (new EditorGUILayout.HorizontalScope(_fieldPanelStyle))
                {
                    EditorGUI.BeginChangeCheck();
                    bool next = DrawRouterSmallToggle(row.consoleEnabled, row.consoleEnabled ? "Console" : "Muted", UtilityWindowTheme.Purple, 64f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        DebugRouter.SetSignalConsoleEnabled(signalName, next);
                        RefreshRouterSnapshots();
                        _status = $"Set DebugRouter signal console '{signalName}' = {next}.";
                    }

                    EditorGUILayout.LabelField(new GUIContent(Shorten(signalName, 44), signalName), _categoryLabelStyle, GUILayout.MinWidth(120f));
                    GUILayout.FlexibleSpace();
                    DrawCountPill($"{row.fireCount} fires", UtilityWindowTheme.Purple, 70f);
                    if (!_drawingSupportColumn)
                    {
                        EditorGUILayout.LabelField(new GUIContent(Shorten(row.lastOwner, 18), row.lastOwner), _pathLabelStyle, GUILayout.Width(110f));
                        EditorGUILayout.LabelField(new GUIContent(Shorten(row.lastDetails, 44), row.lastDetails), _pathLabelStyle, GUILayout.Width(220f));
                    }
                }
                if (_drawingSupportColumn && (!string.IsNullOrEmpty(row.lastOwner) || !string.IsNullOrEmpty(row.lastDetails)))
                    EditorGUILayout.LabelField(new GUIContent($"{Shorten(row.lastOwner, 24)}  {Shorten(row.lastDetails, 42)}", $"{row.lastOwner}\n{row.lastDetails}"), _pathLabelStyle);
            }
        }

        private void DrawRouterStates(List<DebugRouter.StateSnapshot> rows)
        {
            bool open = DrawRouterFoldout(ref _routerStatesOpen, RouterStatesOpenPrefsKey, $"States ({rows.Count}/{_routerStates.Count})", UtilityWindowTheme.Blue);
            if (!open)
                return;

            EditorGUILayout.LabelField(new GUIContent("States appear after DebugRouter.SetState(...).", "States are named boolean snapshots reported by code through the DebugRouter."), _mutedMiniLabelStyle);
            if (rows.Count == 0)
            {
                EditorGUILayout.HelpBox(_routerStates.Count == 0 ? "No states yet." : "Search has no matching states.", MessageType.None);
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                DebugRouter.StateSnapshot row = rows[i];
                using (new EditorGUILayout.HorizontalScope(_fieldPanelStyle))
                {
                    DrawCountPill(row.value ? "True" : "False", row.value ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, 54f);
                    EditorGUILayout.LabelField(new GUIContent(Shorten(row.stateName, 40), row.stateName), _categoryLabelStyle, GUILayout.MinWidth(120f));
                    GUILayout.FlexibleSpace();
                    DrawCountPill($"{row.changeCount} changes", UtilityWindowTheme.Blue, 86f);
                    if (!_drawingSupportColumn)
                    {
                        EditorGUILayout.LabelField(new GUIContent(Shorten(row.ownerName, 18), row.ownerName), _pathLabelStyle, GUILayout.Width(110f));
                        EditorGUILayout.LabelField(new GUIContent(Shorten(row.details, 44), row.details), _pathLabelStyle, GUILayout.Width(220f));
                    }
                }
                if (_drawingSupportColumn && (!string.IsNullOrEmpty(row.ownerName) || !string.IsNullOrEmpty(row.details)))
                    EditorGUILayout.LabelField(new GUIContent($"{Shorten(row.ownerName, 24)}  {Shorten(row.details, 42)}", $"{row.ownerName}\n{row.details}"), _pathLabelStyle);
            }
        }

        private void DrawRouterSources(List<DebugRouter.SourceSnapshot> rows)
        {
            bool open = DrawRouterFoldout(ref _routerSourcesOpen, RouterSourcesOpenPrefsKey, $"Sources ({rows.Count}/{_routerSources.Count})", UtilityWindowTheme.Teal);
            if (!open)
                return;

            EditorGUILayout.LabelField(new GUIContent("Sources appear when an owner object logs/registers against a channel.", "Sources represent individual owner objects/components that emit through a channel."), _mutedMiniLabelStyle);
            if (rows.Count == 0)
            {
                EditorGUILayout.HelpBox(_routerSources.Count == 0 ? "No sources yet." : "Search has no matching sources.", MessageType.None);
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                DebugRouter.SourceSnapshot row = rows[i];
                UnityEngine.Object owner = row.ownerInstanceId != 0 ? EditorUtility.InstanceIDToObject(row.ownerInstanceId) : null;
                string channel = row.channel ?? "General";
                string ownerLabel = string.IsNullOrEmpty(row.ownerName) ? "Global" : row.ownerName;

                using (new EditorGUILayout.HorizontalScope(_fieldPanelStyle))
                {
                    using (new EditorGUI.DisabledScope(owner == null))
                    {
                        EditorGUI.BeginChangeCheck();
                        bool next = DrawRouterSmallToggle(row.enabled, row.enabled ? "On" : "Off", UtilityWindowTheme.Teal, 46f);
                        if (EditorGUI.EndChangeCheck())
                        {
                            DebugRouter.SetSourceEnabled(owner, channel, next);
                            RefreshRouterSnapshots();
                            _status = $"Set DebugRouter source '{ownerLabel}' / '{channel}' = {next}.";
                        }
                    }

                    string main = $"{ownerLabel} / {channel}";
                    EditorGUILayout.LabelField(new GUIContent(Shorten(main, 48), main), _categoryLabelStyle, GUILayout.MinWidth(140f));
                    GUILayout.FlexibleSpace();
                    DrawCountPill($"{row.logCount} logs", UtilityWindowTheme.Teal, 68f);
                    if (!_drawingSupportColumn)
                    {
                        EditorGUILayout.LabelField(new GUIContent(Shorten(row.hierarchyPath, 28), row.hierarchyPath), _pathLabelStyle, GUILayout.Width(170f));
                        EditorGUILayout.LabelField(new GUIContent(Shorten(row.lastMessage, 34), row.lastMessage), _pathLabelStyle, GUILayout.Width(180f));
                    }
                    using (new EditorGUI.DisabledScope(owner == null))
                    {
                        if (DrawTintedButton(new GUIContent("Ping", "Ping this router source owner in the Hierarchy/Project."), UtilityWindowTheme.Blue, GUILayout.Width(44f)))
                            EditorGUIUtility.PingObject(owner);
                    }
                }
                if (_drawingSupportColumn && (!string.IsNullOrEmpty(row.hierarchyPath) || !string.IsNullOrEmpty(row.lastMessage)))
                    EditorGUILayout.LabelField(new GUIContent($"{Shorten(row.hierarchyPath, 32)}  {Shorten(row.lastMessage, 42)}", $"{row.hierarchyPath}\n{row.lastMessage}"), _pathLabelStyle);
            }
        }

        private bool DrawRouterFoldout(ref bool foldout, string prefsKey, string label, Color tint)
        {
            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(tint, 0.10f, 0.05f, 5, 3)))
            {
                bool next = EditorGUILayout.Foldout(foldout, new GUIContent(label, $"Show or hide the DebugRouter {label} section."), true, _sectionHeaderStyle);
                if (next != foldout)
                {
                    foldout = next;
                    UtilityWindowPrefs.SetBool(prefsKey, foldout);
                }

                GUILayout.FlexibleSpace();
                DrawCountPill(foldout ? "Open" : "Closed", tint, 62f);
            }

            return foldout;
        }

        private static bool DrawRouterSmallToggle(bool value, string label, Color activeTint, float width)
        {
            Color tint = value ? activeTint : WithValue(activeTint, 0.55f);
            using (new GuiBackgroundScope(tint))
                return GUILayout.Toggle(value, new GUIContent(label, value ? "Disable this DebugRouter row." : "Enable this DebugRouter row."), EditorStyles.miniButton, GUILayout.Width(width));
        }

        private void AuditDebugRouterScriptUsage()
        {
            _routerScriptAuditPerformed = true;
            _routerAuditLogCalls = 0;
            _routerAuditSignalCalls = 0;
            _routerAuditStateCalls = 0;
            _routerAuditRegisterCalls = 0;
            _routerAuditFiles.Clear();

            string[] guids = AssetDatabase.FindAssets("t:MonoScript");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || path.EndsWith("DebugRouter.cs", StringComparison.OrdinalIgnoreCase))
                    continue;

                string text;
                try { text = File.ReadAllText(path); }
                catch { continue; }

                int logs = CountOccurrences(text, "DebugRouter.Log(");
                int signals = CountOccurrences(text, "DebugRouter.Signal(");
                int states = CountOccurrences(text, "DebugRouter.SetState(");
                int registers = CountOccurrences(text, "DebugRouter.RegisterSignal(");
                if (logs + signals + states + registers <= 0)
                    continue;

                _routerAuditLogCalls += logs;
                _routerAuditSignalCalls += signals;
                _routerAuditStateCalls += states;
                _routerAuditRegisterCalls += registers;
                _routerAuditFiles.Add(path);
            }

            _status = _routerAuditFiles.Count == 0
                ? "No DebugRouter script references found."
                : $"Found DebugRouter references in {_routerAuditFiles.Count} script(s).";
        }

        private static int CountOccurrences(string text, string needle)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(needle))
                return 0;

            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += needle.Length;
            }
            return count;
        }

        private bool RefreshRouterSnapshots()
        {
            try
            {
                List<DebugRouter.ChannelSnapshot> nextChannels = DebugRouter.GetChannels() ?? new List<DebugRouter.ChannelSnapshot>();
                List<DebugRouter.SourceSnapshot> nextSources = DebugRouter.GetSources() ?? new List<DebugRouter.SourceSnapshot>();
                List<DebugRouter.StateSnapshot> nextStates = DebugRouter.GetStates() ?? new List<DebugRouter.StateSnapshot>();
                List<DebugRouter.SignalSnapshot> nextSignals = DebugRouter.GetSignals() ?? new List<DebugRouter.SignalSnapshot>();
                bool changed = RouterSnapshotSignature(_routerChannels, _routerSignals, _routerStates, _routerSources) != RouterSnapshotSignature(nextChannels, nextSignals, nextStates, nextSources);
                _routerChannels = nextChannels;
                _routerSources = nextSources;
                _routerStates = nextStates;
                _routerSignals = nextSignals;
                return changed;
            }
            catch (Exception ex)
            {
                _routerChannels.Clear();
                _routerSources.Clear();
                _routerStates.Clear();
                _routerSignals.Clear();
                _status = "DebugRouter snapshot refresh failed.";
                Debug.LogWarning("[DebugControlWindow] DebugRouter snapshot refresh failed. " + ex.Message);
                return true;
            }
        }

        private static string RouterSnapshotSignature(
            List<DebugRouter.ChannelSnapshot> channels,
            List<DebugRouter.SignalSnapshot> signals,
            List<DebugRouter.StateSnapshot> states,
            List<DebugRouter.SourceSnapshot> sources)
        {
            int channelLogs = channels != null ? channels.Sum(row => row != null ? row.logCount : 0) : 0;
            int signalFires = signals != null ? signals.Sum(row => row != null ? row.fireCount : 0) : 0;
            int stateChanges = states != null ? states.Sum(row => row != null ? row.changeCount : 0) : 0;
            int sourceLogs = sources != null ? sources.Sum(row => row != null ? row.logCount : 0) : 0;
            return $"{channels?.Count ?? 0}:{signals?.Count ?? 0}:{states?.Count ?? 0}:{sources?.Count ?? 0}:{channelLogs}:{signalFires}:{stateChanges}:{sourceLogs}";
        }

        private float EstimateRouterContentHeight(int channelCount, int signalCount, int stateCount, int sourceCount)
        {
            float height = 8f;
            height += EstimateRouterSectionHeight(_routerChannelsOpen, channelCount, 24f);
            height += EstimateRouterSectionHeight(_routerSignalsOpen, signalCount, 24f);
            height += EstimateRouterSectionHeight(_routerStatesOpen, stateCount, 24f);
            height += EstimateRouterSectionHeight(_routerSourcesOpen, sourceCount, 24f);
            return Mathf.Clamp(height, 72f, 1800f);
        }

        private float EstimateRouterPanelPreferredHeightForSupport()
        {
            bool noRuntimeData =
                _routerChannels.Count == 0 &&
                _routerSignals.Count == 0 &&
                _routerStates.Count == 0 &&
                _routerSources.Count == 0;

            // Header + usage summary + top controls + margin.
            float height = 92f;

            if (_routerAdvancedOpen)
                height += 34f;

            if (noRuntimeData)
            {
                // Setup/help state: help box + compact scripting examples.
                height += 150f;
                return Mathf.Clamp(height, MinSupportPanelHeight, MaxSupportPanelHeight);
            }

            int channelCount = FilteredRouterChannels().Count;
            int signalCount = FilteredRouterSignals().Count;
            int stateCount = FilteredRouterStates().Count;
            int sourceCount = FilteredRouterSources().Count;

            height += 18f; // states/sources metadata line
            height += EstimateRouterContentHeight(channelCount, signalCount, stateCount, sourceCount);

            return Mathf.Clamp(height, MinSupportPanelHeight, MaxSupportPanelHeight);
        }

        private static float EstimateRouterSectionHeight(bool open, int rowCount, float rowHeight)
        {
            if (!open)
                return 32f;

            return 58f + Mathf.Max(1, rowCount) * rowHeight;
        }

        private List<DebugRouter.ChannelSnapshot> FilteredRouterChannels()
        {
            List<DebugRouter.ChannelSnapshot> result = new List<DebugRouter.ChannelSnapshot>();
            for (int i = 0; i < _routerChannels.Count; i++)
            {
                DebugRouter.ChannelSnapshot row = _routerChannels[i];
                if (row != null && RouterMatchesSearch(row.channel, row.lastOwner, row.lastMessage))
                    result.Add(row);
            }
            return result;
        }

        private List<DebugRouter.SignalSnapshot> FilteredRouterSignals()
        {
            List<DebugRouter.SignalSnapshot> result = new List<DebugRouter.SignalSnapshot>();
            for (int i = 0; i < _routerSignals.Count; i++)
            {
                DebugRouter.SignalSnapshot row = _routerSignals[i];
                if (row != null && RouterMatchesSearch(row.signalName, row.lastOwner, row.lastDetails))
                    result.Add(row);
            }
            return result;
        }

        private List<DebugRouter.StateSnapshot> FilteredRouterStates()
        {
            List<DebugRouter.StateSnapshot> result = new List<DebugRouter.StateSnapshot>();
            for (int i = 0; i < _routerStates.Count; i++)
            {
                DebugRouter.StateSnapshot row = _routerStates[i];
                if (row != null && RouterMatchesSearch(row.stateName, row.ownerName, row.hierarchyPath, row.details))
                    result.Add(row);
            }
            return result;
        }

        private List<DebugRouter.SourceSnapshot> FilteredRouterSources()
        {
            List<DebugRouter.SourceSnapshot> result = new List<DebugRouter.SourceSnapshot>();
            for (int i = 0; i < _routerSources.Count; i++)
            {
                DebugRouter.SourceSnapshot row = _routerSources[i];
                if (row != null && RouterMatchesSearch(row.channel, row.ownerName, row.ownerType, row.hierarchyPath, row.lastMessage))
                    result.Add(row);
            }
            return result;
        }

        private bool RouterMatchesSearch(params string[] parts)
        {
            if (string.IsNullOrWhiteSpace(_search))
                return true;

            string needle = _search.Trim();
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                if (!string.IsNullOrEmpty(part) && part.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static string Shorten(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            if (maxChars < 4 || text.Length <= maxChars)
                return text;

            return text.Substring(0, maxChars - 1) + "…";
        }

    }
#endif
}
