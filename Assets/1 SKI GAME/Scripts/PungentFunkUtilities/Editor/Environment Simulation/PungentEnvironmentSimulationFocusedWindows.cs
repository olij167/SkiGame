using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.EnvironmentSimulation;

namespace PungentFunk.Utilities.Editor.EnvironmentSimulation
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    public sealed class PungentCalendarClockWindow : EditorWindow
    {
        private const string PrefPrefix = "PungentFunk.Environment.CalendarClock.";
        private PungentClockController _clock;
        private Vector2 _leftScroll;
        private Vector2 _centerScroll;
        private Vector2 _rightScroll;
        private float _previewHour = 8f;
        private int _selectedMonth;
        private int _detailTab;
        private float _leftWidth;
        private float _rightWidth;
        private bool _showEvents;
        private bool _showAdvanced;
        private string _sequenceRuleSearch;
        private int _selectedSequenceRule;
        private readonly List<PungentTimeSequenceRule> _sequenceRules = new List<PungentTimeSequenceRule>();
        private readonly List<PungentTimeSequenceListener> _sequenceListeners = new List<PungentTimeSequenceListener>();
        private readonly PungentEnvironmentSimulationValidationReport _validation = new PungentEnvironmentSimulationValidationReport();

        public static void Open()
        {
            PungentCalendarClockWindow window = GetWindow<PungentCalendarClockWindow>("Calendar Clock");
            window.minSize = new Vector2(420f, 380f);
            window.Show();
        }

        private void OnEnable()
        {
            minSize = new Vector2(420f, 380f);
            _selectedMonth = PungentEnvironmentSimulationEditorGUI.GetInt(PrefPrefix + "SelectedMonth", 0, 0, 64);
            _detailTab = PungentEnvironmentSimulationEditorGUI.GetInt(PrefPrefix + "DetailTab", 0, 0, 3);
            _leftWidth = PungentEnvironmentSimulationEditorGUI.GetFloat(PrefPrefix + "LeftWidth", 270f, 220f, 420f);
            _rightWidth = PungentEnvironmentSimulationEditorGUI.GetFloat(PrefPrefix + "RightWidth", 310f, 240f, 460f);
            _showEvents = PungentEnvironmentSimulationEditorGUI.GetBool(PrefPrefix + "Events", false);
            _showAdvanced = PungentEnvironmentSimulationEditorGUI.GetBool(PrefPrefix + "Advanced", false);
            _sequenceRuleSearch = PungentEnvironmentSimulationEditorGUI.GetString(PrefPrefix + "SequenceSearch", string.Empty);
            _selectedSequenceRule = PungentEnvironmentSimulationEditorGUI.GetInt(PrefPrefix + "SelectedSequenceRule", 0, 0, 999);
            Refresh();
        }

        private void OnDisable()
        {
            PungentEnvironmentSimulationEditorGUI.SetInt(PrefPrefix + "SelectedMonth", _selectedMonth);
            PungentEnvironmentSimulationEditorGUI.SetInt(PrefPrefix + "DetailTab", _detailTab);
            PungentEnvironmentSimulationEditorGUI.SetFloat(PrefPrefix + "LeftWidth", _leftWidth);
            PungentEnvironmentSimulationEditorGUI.SetFloat(PrefPrefix + "RightWidth", _rightWidth);
            PungentEnvironmentSimulationEditorGUI.SetBool(PrefPrefix + "Events", _showEvents);
            PungentEnvironmentSimulationEditorGUI.SetBool(PrefPrefix + "Advanced", _showAdvanced);
            PungentEnvironmentSimulationEditorGUI.SetString(PrefPrefix + "SequenceSearch", _sequenceRuleSearch);
            PungentEnvironmentSimulationEditorGUI.SetInt(PrefPrefix + "SelectedSequenceRule", _selectedSequenceRule);
        }

        private void OnGUI()
        {
            UtilityWindowTheme.EnsureStyles();
            PungentEnvironmentSimulationEditorGUI.Header("Calendar Clock", "Time, date, seasons, sky output, and time-sequence authoring.", "calendar-clock");
            PungentEnvironmentSimulationEditorGUI.DrawLayoutQAStrip(
                position.width,
                position.height,
                "Calendar: understand and adjust simulated time.",
                "Single scroll: command, timeline, months, actions, setup, details.",
                "Timeline first; setup and detail follow in two columns when space allows.",
                "Setup rail, large time workspace, detail rail with clamped splitters.");
            DrawCommandStrip();

            if (_clock == null)
            {
                DrawCreateMissing();
                return;
            }

            _validation.Refresh(_clock, null, PungentEnvironmentSimulationEditorGUI.ObjectValue(_clock, "deltaTimeController") as PungentDeltaTimeController);
            PungentEnvironmentSimulationEditorGUI.ResponsiveWorkspaceLayout(
                position.width,
                PrefPrefix + "LeftWidth",
                PrefPrefix + "RightWidth",
                ref _leftScroll,
                ref _centerScroll,
                ref _rightScroll,
                ref _leftWidth,
                ref _rightWidth,
                230f,
                420f,
                420f,
                260f,
                460f,
                DrawClockSetupRail,
                DrawClockTimelineWorkspace,
                DrawClockDetailRail);
        }

        private void DrawCommandStrip()
        {
            PungentEnvironmentSimulationEditorGUI.DrawTargetRow(ref _clock, "Clock Controller", Refresh, PungentEnvironmentSimulationWindow.Open);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan, 0.13f, 0.06f, 5, 3)))
            {
                if (_clock != null)
                {
                    SerializedObject serializedClock = new SerializedObject(_clock);
                    PungentClockSnapshot snapshot = _clock.Snapshot;
                    PungentEnvironmentSimulationEditorGUI.AdaptiveToolbarRow(position.width,
                        PungentEnvironmentSimulationEditorGUI.ToolbarGroup(340f, () =>
                        {
                            PungentEnvironmentSimulationEditorGUI.Chip("Time " + snapshot.TimeLabel24, UtilityWindowTheme.Cyan, 94f);
                            PungentEnvironmentSimulationEditorGUI.Chip("Season " + snapshot.seasonName, UtilityWindowTheme.Teal, 122f);
                            PungentEnvironmentSimulationEditorGUI.Chip(snapshot.monthName + " " + snapshot.date.dayOfMonth, UtilityWindowTheme.Purple, 118f);
                        }),
                        PungentEnvironmentSimulationEditorGUI.ToolbarGroup(300f, () =>
                        {
                            PungentEnvironmentSimulationEditorGUI.DrawProperties(serializedClock, "runOnStart", "secondsPerInGameMinute");
                        }),
                        PungentEnvironmentSimulationEditorGUI.ToolbarGroup(250f, () =>
                        {
                            _previewHour = EditorGUILayout.Slider(new GUIContent("Set Hour", "Choose the clock hour to preview or apply."), _previewHour, 0f, 23.99f, GUILayout.MinWidth(170f));
                        }),
                        PungentEnvironmentSimulationEditorGUI.ToolbarGroup(310f, () =>
                        {
                            if (GUILayout.Button(new GUIContent("Apply Hour", "Set the clock to the chosen hour."), GUILayout.MinWidth(86f)))
                                SetHour();
                            if (GUILayout.Button(new GUIContent("Advance Day", "Advance one in-game day."), GUILayout.MinWidth(104f)))
                                AdvanceDay();
                            if (GUILayout.Button(new GUIContent("Reset Start", "Reset to configured start date/time."), GUILayout.MinWidth(92f)))
                                ResetClock();
                        }));
                }
                else
                {
                    EditorGUILayout.LabelField("No clock controller selected.", UtilityWindowTheme.MutedMiniLabelStyle);
                }
            }
        }

        private void DrawCreateMissing()
        {
            PungentEnvironmentSimulationEditorGUI.StatusCard("Calendar Clock missing", "Create a scene controller to configure calendar, season, and sky output.", false, () =>
            {
                if (GUILayout.Button(new GUIContent("Create Clock Controller", "Create a Pungent Clock Controller in the open scene.")))
                {
                    GameObject go = PungentEnvironmentSimulationEditorGUI.CreateController<PungentClockController>("Pungent Clock");
                    _clock = go.GetComponent<PungentClockController>();
                }
            });
        }

        private void DrawClockSetupRail()
        {
            SerializedObject serializedClock = new SerializedObject(_clock);
            PungentEnvironmentSimulationEditorGUI.Section("Setup", UtilityWindowTheme.Blue, () =>
            {
                PungentEnvironmentSimulationEditorGUI.DrawObjectPropertyWithCreate<PungentEnvironmentCalendarProfile>(serializedClock, "calendarProfile", "Calendar Profile", "Pungent Calendar Profile");
                PungentEnvironmentSimulationEditorGUI.DrawObjectPropertyWithCreate<PungentClockSkyProfile>(serializedClock, "skyProfile", "Sky Profile", "Pungent Clock Sky Profile");
                PungentEnvironmentSimulationEditorGUI.DrawProperties(serializedClock, "startYear", "startMonthIndex", "startDayOfMonth", "startHour");
            });

            PungentEnvironmentSimulationEditorGUI.Section("Integration", UtilityWindowTheme.Teal, () =>
            {
                PungentEnvironmentSimulationEditorGUI.DrawProperties(serializedClock, "useDeltaTimeController", "deltaTimeController");
            });

            PungentEnvironmentSimulationEditorGUI.Section("Outputs", UtilityWindowTheme.Purple, () =>
            {
                PungentEnvironmentSimulationEditorGUI.DrawProperties(serializedClock, "sunTransform", "moonTransform", "seasonalTiltTransform", "sunLight", "moonLight");
            });

            DrawClockSetupHealth();
        }

        private void DrawClockSetupHealth()
        {
            bool hasCalendar = PungentEnvironmentSimulationEditorGUI.ObjectValue(_clock, "calendarProfile") != null;
            bool hasSky = PungentEnvironmentSimulationEditorGUI.ObjectValue(_clock, "skyProfile") != null;
            bool hasOutput = _validation.hasClockOutput;
            PungentEnvironmentSimulationEditorGUI.StatusCard("Calendar profile", hasCalendar ? "Calendar profile is assigned." : "Assign a calendar profile to author months and seasons.", hasCalendar);
            PungentEnvironmentSimulationEditorGUI.StatusCard("Sky profile", hasSky ? "Sky output profile is assigned." : "Optional, but needed for lighting and fog previews.", hasSky);
            PungentEnvironmentSimulationEditorGUI.StatusCard("Scene outputs", hasOutput ? "At least one sun, moon, or light output is assigned." : "Assign scene outputs when this clock should drive lighting or sky previews.", hasOutput);
            PungentEnvironmentSimulationEditorGUI.Section("Unity System Fit", UtilityWindowTheme.Cyan, () =>
            {
                PungentEnvironmentSimulationEditorGUI.OpportunityCard(_validation.timeline);
                PungentEnvironmentSimulationEditorGUI.OpportunityCard(_validation.testFramework);
            });
        }

        private void DrawClockTimelineWorkspace()
        {
            using (PungentEnvironmentSimulationEditorGUI.StretchPanelScope(UtilityWindowTheme.Cyan))
            {
                UtilityWindowTheme.SectionTitle("Time Workspace", UtilityWindowTheme.Cyan);
                PungentClockSnapshot snapshot = _clock.Snapshot;
                EditorGUILayout.LabelField(snapshot.TimeLabel24 + "  " + snapshot.DateLabel, UtilityWindowTheme.CardLabelStyle);
                DrawDayBand(snapshot);
                DrawMonthSeasonSummary();
                DrawClockActions();
            }
        }

        private void DrawDayBand(PungentClockSnapshot snapshot)
        {
            Rect rect = PungentEnvironmentSimulationEditorGUI.TimelineRect(86f);
            EditorGUI.DrawRect(rect, new Color(0.08f, 0.09f, 0.10f, 0.72f));

            float sunrise = snapshot.sunriseHour / 24f;
            float sunset = snapshot.sunsetHour / 24f;
            Rect dayRect = new Rect(Mathf.Lerp(rect.xMin, rect.xMax, sunrise), rect.yMin + 16f, Mathf.Max(2f, Mathf.Lerp(rect.xMin, rect.xMax, sunset) - Mathf.Lerp(rect.xMin, rect.xMax, sunrise)), 34f);
            EditorGUI.DrawRect(dayRect, new Color(1f, 0.78f, 0.28f, 0.30f));
            PungentEnvironmentSimulationEditorGUI.DrawMarker(rect, 0f, UtilityWindowTheme.Neutral, "00");
            PungentEnvironmentSimulationEditorGUI.DrawMarker(rect, 0.25f, UtilityWindowTheme.Neutral, "06");
            PungentEnvironmentSimulationEditorGUI.DrawMarker(rect, 0.5f, UtilityWindowTheme.Neutral, "12");
            PungentEnvironmentSimulationEditorGUI.DrawMarker(rect, 0.75f, UtilityWindowTheme.Neutral, "18");
            PungentEnvironmentSimulationEditorGUI.DrawMarker(rect, sunrise, UtilityWindowTheme.Green, "rise");
            PungentEnvironmentSimulationEditorGUI.DrawMarker(rect, sunset, UtilityWindowTheme.Amber, "set");
            PungentEnvironmentSimulationEditorGUI.DrawMarker(rect, snapshot.dayPercent, UtilityWindowTheme.Cyan, "now");
        }

        private void DrawMonthSeasonSummary()
        {
            PungentEnvironmentCalendarProfile profile = PungentEnvironmentSimulationEditorGUI.ObjectValue(_clock, "calendarProfile") as PungentEnvironmentCalendarProfile;
            if (profile == null)
            {
                EditorGUILayout.HelpBox("Assign a Calendar Profile to see month and season structure.", MessageType.Info);
                return;
            }

            PungentEnvironmentSimulationEditorGUI.Section("Months and Seasons", UtilityWindowTheme.Purple, () =>
            {
                for (int i = 0; i < profile.MonthCount; i++)
                {
                    PungentEnvironmentCalendarProfile.MonthDefinition month = profile.GetMonth(i);
                    bool active = i == _clock.Snapshot.date.monthIndex;
                    bool selected = i == _selectedMonth;
                    using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(selected ? UtilityWindowTheme.Purple : active ? UtilityWindowTheme.Cyan : UtilityWindowTheme.Neutral, selected ? 0.22f : 0.10f, 0.04f, 4, 2)))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button(new GUIContent(month != null ? month.name : "Month " + (i + 1), "Select this month for detail editing."), EditorStyles.miniButton, GUILayout.MinWidth(110f)))
                                _selectedMonth = i;
                            GUILayout.FlexibleSpace();
                            UtilityWindowTheme.CountPill(active ? "Current" : "Month", active ? UtilityWindowTheme.Cyan : UtilityWindowTheme.Neutral, 72f);
                        }

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            UtilityWindowTheme.CountPill(month != null ? month.season : "Default", UtilityWindowTheme.Teal, 88f);
                            UtilityWindowTheme.CountPill((month != null ? month.days : 0) + " days", active ? UtilityWindowTheme.Cyan : UtilityWindowTheme.Neutral, 72f);
                            UtilityWindowTheme.CountPill("rise " + (month != null ? month.sunriseHour.ToString("0.0") : "-"), UtilityWindowTheme.Green, 74f);
                            UtilityWindowTheme.CountPill("set " + (month != null ? month.sunsetHour.ToString("0.0") : "-"), UtilityWindowTheme.Amber, 70f);
                        }
                    }
                }
            });
        }

        private void DrawClockActions()
        {
            PungentEnvironmentSimulationEditorGUI.AdaptiveToolbarRow(position.width,
                PungentEnvironmentSimulationEditorGUI.ToolbarGroup(220f, () =>
                {
                    if (GUILayout.Button(new GUIContent("Create Time Sequence Rule", "Create a reusable time/date/season sequence rule asset."), GUILayout.Height(24f)))
                        CreateSequenceRule(false);
                }),
                PungentEnvironmentSimulationEditorGUI.ToolbarGroup(220f, () =>
                {
                    if (GUILayout.Button(new GUIContent("Rule From Current Time", "Create a sequence rule matching the current clock date, month, season, and hour."), GUILayout.Height(24f)))
                        CreateSequenceRule(true);
                }),
                PungentEnvironmentSimulationEditorGUI.ToolbarGroup(180f, () =>
                {
                    if (GUILayout.Button(new GUIContent("Add Timeline Bridge", "Add a PlayableDirector bridge that can scrub the clock from a Timeline."), GUILayout.Height(24f)))
                        AddTimelineClockBridge();
                }),
                PungentEnvironmentSimulationEditorGUI.ToolbarGroup(100f, () =>
                {
                    if (GUILayout.Button(new GUIContent("Open Hub", "Open Environment Simulation dashboard."), GUILayout.MinWidth(90f)))
                        PungentEnvironmentSimulationWindow.Open();
                }));
        }

        private void DrawClockDetailRail()
        {
            _detailTab = PungentEnvironmentSimulationEditorGUI.ToolbarButton(_detailTab, new[] { "Month", "Sky", "Events", "Advanced" }, "Clock detail");
            PungentEnvironmentSimulationEditorGUI.SetInt(PrefPrefix + "DetailTab", _detailTab);

            if (_detailTab == 0)
                DrawSelectedMonthEditor();
            else if (_detailTab == 1)
                DrawSkyProfileEditor();
            else if (_detailTab == 2)
                DrawClockEvents();
            else
                DrawClockAdvanced();
        }

        private void DrawSelectedMonthEditor()
        {
            UnityEngine.Object profile = PungentEnvironmentSimulationEditorGUI.ObjectValue(_clock, "calendarProfile");
            if (profile == null)
            {
                EditorGUILayout.HelpBox("Assign a Calendar Profile before editing month details.", MessageType.Info);
                return;
            }

            SerializedObject serializedProfile = new SerializedObject(profile);
            SerializedProperty months = serializedProfile.FindProperty("months");
            if (months == null || months.arraySize == 0)
            {
                EditorGUILayout.HelpBox("Calendar profile has no months.", MessageType.Warning);
                return;
            }

            _selectedMonth = Mathf.Clamp(_selectedMonth, 0, months.arraySize - 1);
            serializedProfile.Update();
            EditorGUILayout.PropertyField(serializedProfile.FindProperty("firstDayOfYear"));
            EditorGUILayout.PropertyField(months.GetArrayElementAtIndex(_selectedMonth), new GUIContent("Selected Month"), true);
            serializedProfile.ApplyModifiedProperties();
        }

        private void DrawSkyProfileEditor()
        {
            UnityEngine.Object profile = PungentEnvironmentSimulationEditorGUI.ObjectValue(_clock, "skyProfile");
            if (profile == null)
            {
                EditorGUILayout.HelpBox("Assign a Sky Profile before editing sky curves.", MessageType.Info);
                return;
            }

            SerializedObject serializedProfile = new SerializedObject(profile);
            PungentEnvironmentSimulationEditorGUI.DrawProperties(serializedProfile, "ambientColor", "fogColor", "sunColor", "sunIntensity", "moonIntensity");
        }

        private void DrawClockEvents()
        {
            PungentEnvironmentSimulationEditorGUI.Section("Sequence Rules", UtilityWindowTheme.Green, () =>
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _sequenceRuleSearch = EditorGUILayout.TextField(new GUIContent("Search", "Filter time sequence rules by id, name, season, or asset name."), _sequenceRuleSearch);
                    if (GUILayout.Button(new GUIContent("Refresh", "Refresh the cached sequence rule asset list."), GUILayout.Width(72f)))
                        RefreshSequenceRules();
                }

                if (_sequenceRules.Count == 0)
                {
                    EditorGUILayout.HelpBox("No time sequence rules found. Create a reusable rule asset, then add a listener to invoke scene events.", MessageType.Info);
                    if (GUILayout.Button(new GUIContent("Create Rule From Current Time", "Create a rule matching the current clock date, season, and hour.")))
                        CreateSequenceRule(true);
                    return;
                }

                DrawSequenceOverview();
                DrawSequenceRuleCards();
                DrawSelectedSequenceRuleEditor();
            });

            _showEvents = EditorGUILayout.Foldout(_showEvents, "Controller Unity Events", true);
            if (_showEvents)
                PungentEnvironmentSimulationEditorGUI.DrawProperties(new SerializedObject(_clock), "onTimeChanged", "onHourChanged", "onDayChanged", "onMonthChanged", "onSeasonChanged", "onYearChanged");
        }

        private void DrawClockAdvanced()
        {
            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Skipped Time", true);
            if (_showAdvanced)
                PungentEnvironmentSimulationEditorGUI.DrawProperties(new SerializedObject(_clock), "fireMissedSequenceEventsWhenSkipping");
        }

        private void SetHour()
        {
            Undo.RecordObject(_clock, "Set Pungent Clock Hour");
            _clock.SetHour(_previewHour);
            EditorUtility.SetDirty(_clock);
        }

        private void AdvanceDay()
        {
            Undo.RecordObject(_clock, "Advance Pungent Clock Day");
            _clock.AdvanceDays(1);
            EditorUtility.SetDirty(_clock);
        }

        private void ResetClock()
        {
            Undo.RecordObject(_clock, "Reset Pungent Clock");
            _clock.ResetToStart();
            EditorUtility.SetDirty(_clock);
        }

        private void AddTimelineClockBridge()
        {
            PungentTimelineClockBridge bridge = _clock.GetComponent<PungentTimelineClockBridge>();
            if (bridge == null)
                bridge = Undo.AddComponent<PungentTimelineClockBridge>(_clock.gameObject);

            SerializedObject serializedBridge = new SerializedObject(bridge);
            SerializedProperty clock = serializedBridge.FindProperty("clock");
            if (clock != null)
                clock.objectReferenceValue = _clock;
            serializedBridge.ApplyModifiedProperties();
            Selection.activeObject = bridge;
        }

        private void CreateSequenceRule(bool fromCurrentTime)
        {
            PungentTimeSequenceRule rule = PungentEnvironmentSimulationEditorGUI.CreateAsset<PungentTimeSequenceRule>("Pungent Time Sequence Rule");
            if (rule == null)
                return;

            if (fromCurrentTime && _clock != null)
            {
                Undo.RecordObject(rule, "Configure Time Sequence Rule");
                PungentClockSnapshot snapshot = _clock.Snapshot;
                rule.ruleId = "time." + snapshot.monthName + "." + snapshot.date.dayOfMonth + "." + snapshot.hour.ToString("00");
                rule.hour = snapshot.hour24;
                rule.matchMonth = true;
                rule.monthIndex = snapshot.date.monthIndex;
                rule.matchDayOfMonth = true;
                rule.dayOfMonth = snapshot.date.dayOfMonth;
                rule.matchSeason = !string.IsNullOrWhiteSpace(snapshot.seasonName);
                rule.season = snapshot.seasonName;
                EditorUtility.SetDirty(rule);
            }

            RefreshSequenceRules();
            _selectedSequenceRule = Mathf.Max(0, _sequenceRules.IndexOf(rule));
            Selection.activeObject = rule;
        }

        private void DrawSequenceRuleCards()
        {
            int visibleIndex = 0;
            for (int i = 0; i < _sequenceRules.Count; i++)
            {
                PungentTimeSequenceRule rule = _sequenceRules[i];
                if (rule == null || !MatchesSequenceRuleFilter(rule))
                    continue;

                bool selected = i == _selectedSequenceRule;
                bool matchesNow = _clock != null && rule.Matches(_clock.Snapshot, int.MinValue, int.MinValue);
                int listenerCount = CountListeners(rule);
                int conflictCount = CountTriggerShapeConflicts(rule);
                using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(selected ? UtilityWindowTheme.Green : matchesNow ? UtilityWindowTheme.Cyan : UtilityWindowTheme.Neutral, selected ? 0.22f : 0.10f, 0.04f, 4, 2)))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        string label = string.IsNullOrWhiteSpace(rule.ruleId) ? rule.name : rule.ruleId;
                        if (GUILayout.Button(new GUIContent(label, "Select this time sequence rule for editing."), EditorStyles.miniButton, GUILayout.MinWidth(130f)))
                        {
                            _selectedSequenceRule = i;
                            Selection.activeObject = rule;
                        }
                        GUILayout.FlexibleSpace();
                        UtilityWindowTheme.CountPill(rule.repeatMode.ToString(), rule.repeatMode == PungentTimeSequenceRepeatMode.Once ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green, 82f);
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        UtilityWindowTheme.CountPill("Hour " + rule.hour.ToString("0.00"), UtilityWindowTheme.Cyan, 82f);
                        UtilityWindowTheme.CountPill(rule.matchSeason ? rule.season : "Any Season", UtilityWindowTheme.Teal, 102f);
                        UtilityWindowTheme.CountPill(matchesNow ? "Matches Now" : "Not Now", matchesNow ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, 92f);
                        UtilityWindowTheme.CountPill("Listeners " + listenerCount, listenerCount > 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 90f);
                        if (conflictCount > 0)
                            UtilityWindowTheme.CountPill("Similar " + conflictCount, UtilityWindowTheme.Amber, 84f);
                    }
                }

                visibleIndex++;
            }

            if (visibleIndex == 0)
            {
                EditorGUILayout.HelpBox("No sequence rules match the current filter.", MessageType.Info);
                if (GUILayout.Button(new GUIContent("Clear Search", "Show all sequence rules.")))
                    _sequenceRuleSearch = string.Empty;
            }
        }

        private void DrawSelectedSequenceRuleEditor()
        {
            if (_sequenceRules.Count == 0)
                return;

            _selectedSequenceRule = Mathf.Clamp(_selectedSequenceRule, 0, _sequenceRules.Count - 1);
            PungentTimeSequenceRule rule = _sequenceRules[_selectedSequenceRule];
            if (rule == null)
                return;

            PungentEnvironmentSimulationEditorGUI.Section("Selected Rule", UtilityWindowTheme.Amber, () =>
            {
                PungentClockSnapshot snapshot = _clock.Snapshot;
                bool matchesNow = rule.Matches(snapshot, int.MinValue, int.MinValue);
                List<PungentTimeSequenceListener> listeners = GetListeners(rule);
                List<PungentTimeSequenceRule> conflicts = GetTriggerShapeConflicts(rule);
                EditorGUILayout.LabelField("Current check: " + (matchesNow ? "matches current date/time filters" : "does not match current filters"), UtilityWindowTheme.MutedMiniLabelStyle);
                EditorGUILayout.LabelField("Next trigger preview: " + DescribeNextOccurrence(rule), UtilityWindowTheme.MutedMiniLabelStyle);
                EditorGUILayout.LabelField("Rule shape: " + DescribeRulePreview(rule), UtilityWindowTheme.MutedMiniLabelStyle);
                EditorGUILayout.LabelField("Listeners: " + listeners.Count + " scene listener(s). Similar triggers: " + conflicts.Count + ".", UtilityWindowTheme.MutedMiniLabelStyle);

                SerializedObject serializedRule = new SerializedObject(rule);
                PungentEnvironmentSimulationEditorGUI.DrawProperties(serializedRule, "ruleId", "repeatMode", "hour", "matchWeekday", "weekday", "matchMonth", "monthIndex", "matchDayOfMonth", "dayOfMonth", "matchSeason", "season", "matchYearRange", "firstYear", "lastYear", "intervalDays");

                DrawListenerDiagnostics(listeners);
                DrawConflictDiagnostics(conflicts);

                PungentEnvironmentSimulationEditorGUI.AdaptiveToolbarRow(position.width,
                    PungentEnvironmentSimulationEditorGUI.ToolbarGroup(180f, () =>
                    {
                        if (GUILayout.Button(new GUIContent("Add Listener", "Add a scene listener for this rule on the clock GameObject.")))
                            AddSequenceListener(rule);
                    }),
                    PungentEnvironmentSimulationEditorGUI.ToolbarGroup(180f, () =>
                    {
                        if (GUILayout.Button(new GUIContent("Select Asset", "Select the sequence rule asset.")))
                            Selection.activeObject = rule;
                    }));
            });
        }

        private void AddSequenceListener(PungentTimeSequenceRule rule)
        {
            PungentTimeSequenceListener listener = Undo.AddComponent<PungentTimeSequenceListener>(_clock.gameObject);
            SerializedObject serializedListener = new SerializedObject(listener);
            SerializedProperty clock = serializedListener.FindProperty("clock");
            if (clock != null)
                clock.objectReferenceValue = _clock;
            SerializedProperty ruleProperty = serializedListener.FindProperty("rule");
            if (ruleProperty != null)
                ruleProperty.objectReferenceValue = rule;
            serializedListener.ApplyModifiedProperties();
            Selection.activeObject = listener;
            RefreshSequenceListeners();
        }

        private bool MatchesSequenceRuleFilter(PungentTimeSequenceRule rule)
        {
            if (string.IsNullOrWhiteSpace(_sequenceRuleSearch))
                return true;

            string search = _sequenceRuleSearch.Trim();
            return rule.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (!string.IsNullOrWhiteSpace(rule.ruleId) && rule.ruleId.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                   (!string.IsNullOrWhiteSpace(rule.season) && rule.season.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private string DescribeRulePreview(PungentTimeSequenceRule rule)
        {
            string date = rule.matchMonth ? "month " + (rule.monthIndex + 1) : "any month";
            if (rule.matchDayOfMonth)
                date += ", day " + rule.dayOfMonth;
            string season = rule.matchSeason ? ", " + rule.season : string.Empty;
            return rule.hour.ToString("0.00") + " on " + date + season + ".";
        }

        private void DrawSequenceOverview()
        {
            int rulesWithListeners = 0;
            int conflictPairs = 0;
            for (int i = 0; i < _sequenceRules.Count; i++)
            {
                if (_sequenceRules[i] == null)
                    continue;

                if (CountListeners(_sequenceRules[i]) > 0)
                    rulesWithListeners++;
                conflictPairs += CountTriggerShapeConflicts(_sequenceRules[i]);
            }

            PungentEnvironmentSimulationEditorGUI.AdaptiveToolbarRow(position.width,
                PungentEnvironmentSimulationEditorGUI.ToolbarGroup(160f, () =>
                {
                    PungentEnvironmentSimulationEditorGUI.Chip("Rules " + _sequenceRules.Count, UtilityWindowTheme.Green, 78f);
                    PungentEnvironmentSimulationEditorGUI.Chip("Listeners " + _sequenceListeners.Count, _sequenceListeners.Count > 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 96f);
                }),
                PungentEnvironmentSimulationEditorGUI.ToolbarGroup(240f, () =>
                {
                    PungentEnvironmentSimulationEditorGUI.Chip("Wired " + rulesWithListeners, rulesWithListeners == _sequenceRules.Count ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 82f);
                    PungentEnvironmentSimulationEditorGUI.Chip("Similar " + (conflictPairs / 2), conflictPairs > 0 ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, 88f);
                }));

            bool skippedEvents = _clock == null || _clock.FireMissedSequenceEventsWhenSkipping;
            EditorGUILayout.HelpBox("Skipped-time policy: controller fast-forward " + (skippedEvents ? "can fire missed sequence crossings" : "will not fire missed sequence crossings") + ". Individual listeners can further opt out.", skippedEvents ? MessageType.Info : MessageType.Warning);
        }

        private void DrawListenerDiagnostics(List<PungentTimeSequenceListener> listeners)
        {
            if (listeners.Count == 0)
            {
                EditorGUILayout.HelpBox("No scene listener uses this rule yet. Add a listener to invoke UnityEvents from clock changes.", MessageType.Warning);
                return;
            }

            PungentEnvironmentSimulationEditorGUI.Section("Scene Listeners", UtilityWindowTheme.Cyan, () =>
            {
                for (int i = 0; i < listeners.Count; i++)
                {
                    PungentTimeSequenceListener listener = listeners[i];
                    if (listener == null)
                        continue;

                    using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.10f, 0.04f, 4, 2)))
                    {
                        if (GUILayout.Button(new GUIContent(listener.gameObject.name, "Select this listener."), EditorStyles.miniButton, GUILayout.MinWidth(120f)))
                            Selection.activeObject = listener;
                        GUILayout.FlexibleSpace();
                        UtilityWindowTheme.CountPill(listener.TriggerWhenCrossingHour ? "Cross Hour" : "Any Change", UtilityWindowTheme.Cyan, 88f);
                        UtilityWindowTheme.CountPill(listener.FireMissedEventsOnSkippedTime ? "Missed On" : "Missed Off", listener.FireMissedEventsOnSkippedTime ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 88f);
                        UtilityWindowTheme.CountPill(listener.HasTriggered ? "Triggered" : "Waiting", listener.HasTriggered ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, 78f);
                    }
                }
            });
        }

        private void DrawConflictDiagnostics(List<PungentTimeSequenceRule> conflicts)
        {
            if (conflicts.Count == 0)
                return;

            PungentEnvironmentSimulationEditorGUI.Section("Similar Trigger Rules", UtilityWindowTheme.Amber, () =>
            {
                EditorGUILayout.HelpBox("These rules share the same trigger shape. This may be intentional, but it can also cause duplicate event firing if multiple listeners are wired.", MessageType.Warning);
                for (int i = 0; i < conflicts.Count; i++)
                {
                    PungentTimeSequenceRule conflict = conflicts[i];
                    if (conflict == null)
                        continue;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(conflict.ruleId) ? conflict.name : conflict.ruleId, UtilityWindowTheme.MutedMiniLabelStyle);
                        if (GUILayout.Button(new GUIContent("Select", "Select this similar rule."), GUILayout.Width(58f)))
                        {
                            _selectedSequenceRule = Mathf.Max(0, _sequenceRules.IndexOf(conflict));
                            Selection.activeObject = conflict;
                        }
                    }
                }
            });
        }

        private int CountListeners(PungentTimeSequenceRule rule)
        {
            return GetListeners(rule).Count;
        }

        private List<PungentTimeSequenceListener> GetListeners(PungentTimeSequenceRule rule)
        {
            List<PungentTimeSequenceListener> listeners = new List<PungentTimeSequenceListener>();
            if (rule == null)
                return listeners;

            for (int i = 0; i < _sequenceListeners.Count; i++)
            {
                PungentTimeSequenceListener listener = _sequenceListeners[i];
                if (listener != null && listener.Rule == rule)
                    listeners.Add(listener);
            }

            return listeners;
        }

        private int CountTriggerShapeConflicts(PungentTimeSequenceRule rule)
        {
            return GetTriggerShapeConflicts(rule).Count;
        }

        private List<PungentTimeSequenceRule> GetTriggerShapeConflicts(PungentTimeSequenceRule rule)
        {
            List<PungentTimeSequenceRule> conflicts = new List<PungentTimeSequenceRule>();
            if (rule == null)
                return conflicts;

            for (int i = 0; i < _sequenceRules.Count; i++)
            {
                PungentTimeSequenceRule other = _sequenceRules[i];
                if (other != null && other != rule && rule.HasSameTriggerShape(other))
                    conflicts.Add(other);
            }

            return conflicts;
        }

        private string DescribeNextOccurrence(PungentTimeSequenceRule rule)
        {
            if (rule == null || _clock == null)
                return "No clock or rule selected.";

            PungentEnvironmentCalendarProfile profile = _clock.CalendarProfile;
            PungentClockSnapshot current = _clock.Snapshot;
            int maxDays = profile != null ? Mathf.Max(1, profile.DaysInYear * 2) : 730;
            for (int offset = 0; offset <= maxDays; offset++)
            {
                PungentCalendarDate date = profile != null
                    ? profile.AdvanceDays(current.date, offset)
                    : new PungentCalendarDate(current.date.year, current.date.monthIndex, current.date.dayOfMonth + offset, current.date.weekday, current.date.dayOfYear + offset);

                PungentClockSnapshot candidate = BuildSequencePreviewSnapshot(profile, date, rule.hour);
                if (offset == 0 && rule.hour <= current.hour24)
                    continue;
                if (rule.Matches(candidate, int.MinValue, int.MinValue))
                    return candidate.DateLabel + " at " + FormatHour(rule.hour) + " (" + offset + " day" + (offset == 1 ? string.Empty : "s") + " from now).";
            }

            return "No trigger found within " + maxDays + " simulated days.";
        }

        private static PungentClockSnapshot BuildSequencePreviewSnapshot(PungentEnvironmentCalendarProfile profile, PungentCalendarDate date, float hour)
        {
            int wholeHour = Mathf.FloorToInt(hour);
            float minuteFloat = (hour - wholeHour) * 60f;
            int minute = Mathf.FloorToInt(minuteFloat);
            int second = Mathf.FloorToInt((minuteFloat - minute) * 60f);
            PungentEnvironmentCalendarProfile.MonthDefinition month = profile != null ? profile.GetMonth(date.monthIndex) : null;
            int daysInYear = profile != null ? Mathf.Max(1, profile.DaysInYear) : 365;
            return new PungentClockSnapshot
            {
                date = date,
                hour24 = Mathf.Repeat(hour, 24f),
                hour = wholeHour,
                minute = minute,
                second = second,
                dayPercent = Mathf.Repeat(hour, 24f) / 24f,
                monthName = month != null ? month.name : string.Empty,
                seasonName = month != null && !string.IsNullOrWhiteSpace(month.season) ? month.season : "Default",
                sunriseHour = month != null ? month.sunriseHour : 6f,
                sunsetHour = month != null ? month.sunsetHour : 18f,
                seasonPercent = Mathf.Clamp01(date.dayOfYear / (float)daysInYear)
            };
        }

        private static string FormatHour(float hour)
        {
            hour = Mathf.Repeat(hour, 24f);
            int wholeHour = Mathf.FloorToInt(hour);
            int minute = Mathf.FloorToInt((hour - wholeHour) * 60f);
            return wholeHour.ToString("00") + ":" + minute.ToString("00");
        }

        private void RefreshSequenceRules()
        {
            _sequenceRules.Clear();
            string[] guids = AssetDatabase.FindAssets("t:PungentTimeSequenceRule");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                PungentTimeSequenceRule rule = AssetDatabase.LoadAssetAtPath<PungentTimeSequenceRule>(path);
                if (rule != null)
                    _sequenceRules.Add(rule);
            }

            _sequenceRules.Sort((a, b) => string.Compare(a != null ? a.name : string.Empty, b != null ? b.name : string.Empty, StringComparison.OrdinalIgnoreCase));
            _selectedSequenceRule = Mathf.Clamp(_selectedSequenceRule, 0, Mathf.Max(0, _sequenceRules.Count - 1));
        }

        private void RefreshSequenceListeners()
        {
            _sequenceListeners.Clear();
            PungentTimeSequenceListener[] listeners = UnityEngine.Object.FindObjectsByType<PungentTimeSequenceListener>();
            for (int i = 0; i < listeners.Length; i++)
            {
                if (listeners[i] != null && (_clock == null || listeners[i].Clock == null || listeners[i].Clock == _clock))
                    _sequenceListeners.Add(listeners[i]);
            }
        }

        private void Refresh()
        {
            _clock = PungentEnvironmentSimulationEditorGUI.FindSceneObject<PungentClockController>();
            _validation.Refresh(_clock, PungentEnvironmentSimulationEditorGUI.FindSceneObject<PungentWeatherController>(), PungentEnvironmentSimulationEditorGUI.FindSceneObject<PungentDeltaTimeController>());
            RefreshSequenceRules();
            RefreshSequenceListeners();
            Repaint();
        }
    }

    public sealed class PungentWeatherUtilityWindow : EditorWindow
    {
        private const string PrefPrefix = "PungentFunk.Environment.Weather.";
        private PungentWeatherController _weather;
        private Vector2 _leftScroll;
        private Vector2 _centerScroll;
        private Vector2 _rightScroll;
        private string _presetSearch = string.Empty;
        private int _selectedPreset;
        private int _selectedHour;
        private int _detailTab;
        private float _leftWidth;
        private float _rightWidth;
        private bool _precipitationOnly;
        private bool _advancedOnly;
        private readonly PungentEnvironmentSimulationValidationReport _validation = new PungentEnvironmentSimulationValidationReport();

        public static void Open()
        {
            PungentWeatherUtilityWindow window = GetWindow<PungentWeatherUtilityWindow>("Weather Utility");
            window.minSize = new Vector2(420f, 400f);
            window.Show();
        }

        private void OnEnable()
        {
            minSize = new Vector2(420f, 400f);
            _selectedPreset = PungentEnvironmentSimulationEditorGUI.GetInt(PrefPrefix + "SelectedPreset", 0, 0, 256);
            _selectedHour = PungentEnvironmentSimulationEditorGUI.GetInt(PrefPrefix + "SelectedHour", 0, 0, 23);
            _detailTab = PungentEnvironmentSimulationEditorGUI.GetInt(PrefPrefix + "DetailTab", 0, 0, 3);
            _leftWidth = PungentEnvironmentSimulationEditorGUI.GetFloat(PrefPrefix + "LeftWidth", 285f, 230f, 440f);
            _rightWidth = PungentEnvironmentSimulationEditorGUI.GetFloat(PrefPrefix + "RightWidth", 330f, 250f, 480f);
            _presetSearch = PungentEnvironmentSimulationEditorGUI.GetString(PrefPrefix + "Search", string.Empty);
            _precipitationOnly = PungentEnvironmentSimulationEditorGUI.GetBool(PrefPrefix + "PrecipOnly", false);
            _advancedOnly = PungentEnvironmentSimulationEditorGUI.GetBool(PrefPrefix + "AdvancedOnly", false);
            Refresh();
        }

        private void OnDisable()
        {
            PungentEnvironmentSimulationEditorGUI.SetInt(PrefPrefix + "SelectedPreset", _selectedPreset);
            PungentEnvironmentSimulationEditorGUI.SetInt(PrefPrefix + "SelectedHour", _selectedHour);
            PungentEnvironmentSimulationEditorGUI.SetInt(PrefPrefix + "DetailTab", _detailTab);
            PungentEnvironmentSimulationEditorGUI.SetFloat(PrefPrefix + "LeftWidth", _leftWidth);
            PungentEnvironmentSimulationEditorGUI.SetFloat(PrefPrefix + "RightWidth", _rightWidth);
            PungentEnvironmentSimulationEditorGUI.SetString(PrefPrefix + "Search", _presetSearch);
            PungentEnvironmentSimulationEditorGUI.SetBool(PrefPrefix + "PrecipOnly", _precipitationOnly);
            PungentEnvironmentSimulationEditorGUI.SetBool(PrefPrefix + "AdvancedOnly", _advancedOnly);
        }

        private void OnGUI()
        {
            UtilityWindowTheme.EnsureStyles();
            PungentEnvironmentSimulationEditorGUI.Header("Weather Utility", "Preset browser, 24-hour forecast timeline, and environment output routing.", "weather-utility");
            PungentEnvironmentSimulationEditorGUI.DrawLayoutQAStrip(
                position.width,
                position.height,
                "Weather: choose presets, inspect forecast, tune selected output.",
                "Single scroll: command, forecast, selected hour, presets, outputs/details.",
                "Forecast first; preset browser and detail pane below.",
                "Preset rail, forecast workspace, selected detail rail with splitters.");
            DrawWeatherCommandStrip();

            if (_weather == null)
            {
                DrawCreateMissing();
                return;
            }

            _validation.Refresh(PungentEnvironmentSimulationEditorGUI.ObjectValue(_weather, "clock") as PungentClockController, _weather, PungentEnvironmentSimulationEditorGUI.ObjectValue(_weather, "deltaTimeController") as PungentDeltaTimeController);
            PungentEnvironmentSimulationEditorGUI.ResponsiveWorkspaceLayout(
                position.width,
                PrefPrefix + "LeftWidth",
                PrefPrefix + "RightWidth",
                ref _leftScroll,
                ref _centerScroll,
                ref _rightScroll,
                ref _leftWidth,
                ref _rightWidth,
                240f,
                440f,
                430f,
                270f,
                480f,
                DrawPresetBrowser,
                DrawForecastWorkspace,
                DrawWeatherDetailRail);
        }

        private void DrawWeatherCommandStrip()
        {
            PungentEnvironmentSimulationEditorGUI.DrawTargetRow(ref _weather, "Weather Controller", Refresh, PungentEnvironmentSimulationWindow.Open);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal, 0.13f, 0.06f, 5, 3)))
            {
                if (_weather == null)
                {
                    EditorGUILayout.LabelField("No weather controller selected.", UtilityWindowTheme.MutedMiniLabelStyle);
                    return;
                }

                PungentWeatherState state = _weather.CurrentState;
                PungentEnvironmentSimulationEditorGUI.AdaptiveToolbarRow(position.width,
                    PungentEnvironmentSimulationEditorGUI.ToolbarGroup(360f, () =>
                    {
                        PungentEnvironmentSimulationEditorGUI.Chip(string.IsNullOrWhiteSpace(state.displayName) ? "No Weather" : state.displayName, UtilityWindowTheme.Teal, 128f);
                        PungentEnvironmentSimulationEditorGUI.Chip(state.temperature.ToString("0") + " C", UtilityWindowTheme.Cyan, 58f);
                        PungentEnvironmentSimulationEditorGUI.Chip("Rain " + state.precipitationChance.ToString("0") + "%", UtilityWindowTheme.Blue, 78f);
                        PungentEnvironmentSimulationEditorGUI.Chip("Wind " + state.windSpeed.ToString("0.0"), UtilityWindowTheme.Amber, 76f);
                    }),
                    PungentEnvironmentSimulationEditorGUI.ToolbarGroup(410f, () =>
                    {
                        if (GUILayout.Button(new GUIContent("Preview Preset", "Preview the selected weather preset."), GUILayout.MinWidth(112f)))
                            PreviewSelectedPreset();
                        if (GUILayout.Button(new GUIContent("Regenerate Forecast", "Generate a new 24-hour forecast."), GUILayout.MinWidth(142f)))
                            GenerateForecast();
                        if (GUILayout.Button(new GUIContent("Add Output Applier", "Add and assign an optional weather output applier."), GUILayout.MinWidth(136f)))
                            AddOutputApplier();
                    }));
            }
        }

        private void DrawCreateMissing()
        {
            PungentEnvironmentSimulationEditorGUI.StatusCard("Weather Controller missing", "Create a scene controller to configure weather and forecast output.", false, () =>
            {
                if (GUILayout.Button(new GUIContent("Create Weather Controller", "Create a Pungent Weather Controller with an output applier.")))
                {
                    GameObject go = PungentEnvironmentSimulationEditorGUI.CreateController<PungentWeatherController>("Pungent Weather");
                    if (go.GetComponent<PungentWeatherOutputApplier>() == null)
                        Undo.AddComponent<PungentWeatherOutputApplier>(go);
                    _weather = go.GetComponent<PungentWeatherController>();
                }
            });
        }

        private void DrawPresetBrowser()
        {
            SerializedObject serializedWeather = new SerializedObject(_weather);
            PungentEnvironmentSimulationEditorGUI.Section("Weather Setup", UtilityWindowTheme.Blue, () =>
            {
                PungentEnvironmentSimulationEditorGUI.DrawObjectPropertyWithCreate<PungentWeatherProfile>(serializedWeather, "weatherProfile", "Weather Profile", "Pungent Weather Profile");
                PungentEnvironmentSimulationEditorGUI.DrawProperties(serializedWeather, "clock", "regenerateForecastOnDayChange", "preserveManualForecastOverridesOnRegenerate", "useDeltaTimeController", "deltaTimeController", "interpolationSpeed");
            });

            PungentEnvironmentSimulationEditorGUI.Section("Preset Browser", UtilityWindowTheme.Purple, () =>
            {
                _presetSearch = EditorGUILayout.TextField(new GUIContent("Search", "Filter weather presets by name or season."), _presetSearch);
                _precipitationOnly = EditorGUILayout.ToggleLeft(new GUIContent("Precipitation Only", "Show only rain/snow style presets."), _precipitationOnly);
                _advancedOnly = EditorGUILayout.ToggleLeft(new GUIContent("Advanced Overrides", "Show only presets using advanced output overrides."), _advancedOnly);
                DrawPresetCards();
            });

            PungentEnvironmentSimulationEditorGUI.Section("Unity System Fit", UtilityWindowTheme.Cyan, () =>
            {
                PungentEnvironmentSimulationEditorGUI.OpportunityCard(_validation.urp);
                PungentEnvironmentSimulationEditorGUI.OpportunityCard(_validation.shaderGraph);
                PungentEnvironmentSimulationEditorGUI.OpportunityCard(_validation.terrainTools);
            });
        }

        private void DrawPresetCards()
        {
            PungentWeatherProfile profile = PungentEnvironmentSimulationEditorGUI.ObjectValue(_weather, "weatherProfile") as PungentWeatherProfile;
            if (profile == null || profile.Presets == null || profile.Presets.Length == 0)
            {
                EditorGUILayout.HelpBox("Assign a Weather Profile with presets.", MessageType.Info);
                return;
            }

            bool anyVisible = false;
            for (int i = 0; i < profile.Presets.Length; i++)
            {
                PungentWeatherPreset preset = profile.Presets[i];
                if (preset == null || !MatchesPresetFilter(preset))
                    continue;

                anyVisible = true;
                bool selected = i == _selectedPreset;
                using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(selected ? UtilityWindowTheme.Purple : UtilityWindowTheme.Neutral, selected ? 0.22f : 0.10f, 0.04f, 5, 2)))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button(new GUIContent(string.IsNullOrEmpty(preset.displayName) ? "Weather Preset" : preset.displayName, "Select this weather preset."), EditorStyles.miniButton, GUILayout.MinWidth(120f)))
                            _selectedPreset = i;
                        GUILayout.FlexibleSpace();
                        UtilityWindowTheme.CountPill(preset.isPrecipitating ? "Precip" : "Dry", preset.isPrecipitating ? UtilityWindowTheme.Blue : UtilityWindowTheme.Neutral, 62f);
                    }
                    UtilityWindowTheme.CountPill(string.IsNullOrEmpty(preset.season) ? "Any Season" : preset.season, UtilityWindowTheme.Teal, 110f);
                    EditorGUILayout.LabelField("Temp " + preset.temperatureRange.x.ToString("0") + " to " + preset.temperatureRange.y.ToString("0") + " C | Rain " + preset.precipitationChanceRange.x.ToString("0") + " to " + preset.precipitationChanceRange.y.ToString("0") + "%", UtilityWindowTheme.MutedMiniLabelStyle);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        UtilityWindowTheme.CountPill("Wind " + preset.windiness.ToString("0.00"), UtilityWindowTheme.Amber, 74f);
                        UtilityWindowTheme.CountPill("Cloud " + preset.cloudCoverage.ToString("0.00"), UtilityWindowTheme.Cyan, 80f);
                        UtilityWindowTheme.CountPill("Fog " + preset.fogginess.ToString("0.00"), UtilityWindowTheme.Neutral, 66f);
                    }
                }
            }

            if (!anyVisible)
            {
                EditorGUILayout.HelpBox("No presets match the current filters.", MessageType.Info);
                if (GUILayout.Button(new GUIContent("Clear Filters", "Show all weather presets again.")))
                {
                    _presetSearch = string.Empty;
                    _precipitationOnly = false;
                    _advancedOnly = false;
                }
            }
        }

        private bool MatchesPresetFilter(PungentWeatherPreset preset)
        {
            if (_precipitationOnly && !preset.isPrecipitating)
                return false;
            if (_advancedOnly && !preset.useAdvancedOverrides)
                return false;
            if (string.IsNullOrWhiteSpace(_presetSearch))
                return true;
            return (preset.displayName != null && preset.displayName.IndexOf(_presetSearch, StringComparison.OrdinalIgnoreCase) >= 0) ||
                   (preset.season != null && preset.season.IndexOf(_presetSearch, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void DrawForecastWorkspace()
        {
            using (PungentEnvironmentSimulationEditorGUI.StretchPanelScope(UtilityWindowTheme.Teal))
            {
                UtilityWindowTheme.SectionTitle("Forecast Timeline", UtilityWindowTheme.Teal);
                DrawForecastBand();
                using (new EditorGUILayout.HorizontalScope())
                {
                    PungentEnvironmentSimulationEditorGUI.Chip("Manual " + _weather.ManualOverrideCount, _weather.ManualOverrideCount > 0 ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, 86f);
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(_weather.ManualOverrideCount == 0))
                    {
                        if (GUILayout.Button(new GUIContent("Clear Manual Overrides", "Clear all hourly manual override flags without regenerating the forecast."), GUILayout.Width(162f)))
                        {
                            Undo.RecordObject(_weather, "Clear Manual Forecast Overrides");
                            _weather.ClearManualOverrides();
                            EditorUtility.SetDirty(_weather);
                        }
                    }
                }
                SerializedObject serializedWeather = new SerializedObject(_weather);
                SerializedProperty forecast = serializedWeather.FindProperty("forecast");
                if (forecast == null || forecast.arraySize == 0)
                {
                    EditorGUILayout.HelpBox("No forecast entries yet. Generate a forecast to populate 24 hours.", MessageType.Info);
                    if (GUILayout.Button(new GUIContent("Generate Forecast", "Generate a forecast from the active weather profile.")))
                        GenerateForecast();
                    return;
                }

                _selectedHour = Mathf.Clamp(_selectedHour, 0, Mathf.Min(23, forecast.arraySize - 1));
                float contentWidth = position.width >= 1120f
                    ? Mathf.Max(300f, position.width - _leftWidth - _rightWidth - 92f)
                    : PungentEnvironmentSimulationEditorGUI.CurrentContentWidth(position.width);
                int columns = Mathf.Clamp(Mathf.FloorToInt(contentWidth / 116f), 2, 6);
                int rows = Mathf.CeilToInt(forecast.arraySize / (float)columns);
                for (int row = 0; row < rows; row++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        for (int col = 0; col < columns; col++)
                        {
                            int index = row * columns + col;
                            if (index >= forecast.arraySize)
                                continue;

                            SerializedProperty item = forecast.GetArrayElementAtIndex(index);
                            SerializedProperty state = item.FindPropertyRelative("state");
                            string name = state.FindPropertyRelative("displayName").stringValue;
                            float temp = state.FindPropertyRelative("temperature").floatValue;
                            float rain = state.FindPropertyRelative("precipitationChance").floatValue;
                            bool selected = index == _selectedHour;
                            bool manual = item.FindPropertyRelative("manualOverride").boolValue;
                            Color tint = selected ? UtilityWindowTheme.Teal : rain > 50f ? UtilityWindowTheme.Blue : UtilityWindowTheme.Neutral;
                            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, selected ? 0.22f : 0.10f, 0.04f, 4, 2), GUILayout.MinWidth(96f), GUILayout.ExpandWidth(true)))
                            {
                                if (GUILayout.Button(new GUIContent(index.ToString("00") + ":00", "Select this forecast hour."), EditorStyles.miniButton))
                                    _selectedHour = index;
                                EditorGUILayout.LabelField((selected ? "Editing " : string.Empty) + (manual ? "Manual " : string.Empty) + (string.IsNullOrEmpty(name) ? "Weather" : name), UtilityWindowTheme.MutedMiniLabelStyle);
                                EditorGUILayout.LabelField("Temp " + temp.ToString("0") + " C", UtilityWindowTheme.MutedMiniLabelStyle);
                                EditorGUILayout.LabelField("Rain " + rain.ToString("0") + "%", UtilityWindowTheme.MutedMiniLabelStyle);
                            }
                        }
                    }
                }
                serializedWeather.ApplyModifiedProperties();
            }
        }

        private void DrawForecastBand()
        {
            Rect rect = PungentEnvironmentSimulationEditorGUI.TimelineRect(54f);
            EditorGUI.DrawRect(rect, new Color(0.05f, 0.08f, 0.09f, 0.75f));
            for (int i = 0; i <= 24; i += 6)
                PungentEnvironmentSimulationEditorGUI.DrawMarker(rect, i / 24f, UtilityWindowTheme.Neutral, i.ToString("00"));
            PungentEnvironmentSimulationEditorGUI.DrawMarker(rect, _selectedHour / 23f, UtilityWindowTheme.Teal, "edit");
            if (_weather != null)
            {
                PungentClockController clock = PungentEnvironmentSimulationEditorGUI.ObjectValue(_weather, "clock") as PungentClockController;
                if (clock != null)
                    PungentEnvironmentSimulationEditorGUI.DrawMarker(rect, clock.Snapshot.dayPercent, UtilityWindowTheme.Cyan, "now");
            }
        }

        private void DrawWeatherDetailRail()
        {
            _detailTab = PungentEnvironmentSimulationEditorGUI.ToolbarButton(_detailTab, new[] { "Preset", "Hour", "Outputs", "Events" }, "Weather detail");
            PungentEnvironmentSimulationEditorGUI.SetInt(PrefPrefix + "DetailTab", _detailTab);
            if (_detailTab == 0)
                DrawSelectedPresetEditor();
            else if (_detailTab == 1)
                DrawSelectedHourEditor();
            else if (_detailTab == 2)
                DrawOutputEditor();
            else
                DrawWeatherEvents();
        }

        private void DrawSelectedPresetEditor()
        {
            UnityEngine.Object profile = PungentEnvironmentSimulationEditorGUI.ObjectValue(_weather, "weatherProfile");
            if (profile == null)
            {
                EditorGUILayout.HelpBox("Assign a Weather Profile before editing presets.", MessageType.Info);
                return;
            }

            SerializedObject serializedProfile = new SerializedObject(profile);
            SerializedProperty presets = serializedProfile.FindProperty("presets");
            if (presets == null || presets.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No weather presets found.", MessageType.Info);
                return;
            }

            _selectedPreset = Mathf.Clamp(_selectedPreset, 0, presets.arraySize - 1);
            serializedProfile.Update();
            EditorGUILayout.PropertyField(serializedProfile.FindProperty("seed"));
            EditorGUILayout.PropertyField(serializedProfile.FindProperty("seasonRules"), true);
            EditorGUILayout.PropertyField(presets.GetArrayElementAtIndex(_selectedPreset), new GUIContent("Selected Preset"), true);
            serializedProfile.ApplyModifiedProperties();
        }

        private void DrawSelectedHourEditor()
        {
            SerializedObject serializedWeather = new SerializedObject(_weather);
            SerializedProperty forecast = serializedWeather.FindProperty("forecast");
            if (forecast == null || forecast.arraySize == 0)
            {
                EditorGUILayout.HelpBox("Generate a forecast before editing hourly entries.", MessageType.Info);
                return;
            }

            _selectedHour = Mathf.Clamp(_selectedHour, 0, forecast.arraySize - 1);
            serializedWeather.Update();
            EditorGUILayout.PropertyField(forecast.GetArrayElementAtIndex(_selectedHour), new GUIContent("Forecast Hour " + _selectedHour.ToString("00")), true);
            serializedWeather.ApplyModifiedProperties();
        }

        private void DrawOutputEditor()
        {
            SerializedObject serializedWeather = new SerializedObject(_weather);
            PungentEnvironmentSimulationEditorGUI.DrawProperties(serializedWeather, "outputApplier");
            if (GUILayout.Button(new GUIContent("Add Output Applier", "Add and assign the optional weather output applier.")))
                AddOutputApplier();

            UnityEngine.Object applier = PungentEnvironmentSimulationEditorGUI.ObjectValue(_weather, "outputApplier");
            if (applier == null)
            {
                PungentEnvironmentSimulationEditorGUI.StatusCard("Output applier missing", "Add an output applier before configuring fog, shader globals, clouds, particles, and audio.", false);
                return;
            }

            DrawWeatherOutputRoutingHealth(applier);
            SerializedObject serializedApplier = new SerializedObject(applier);
            PungentEnvironmentSimulationEditorGUI.DrawProperties(serializedApplier, "writeGlobalShaderProperties", "writeRenderSettingsFog", "fogDensityMultiplier", "sceneCloudRenderers", "precipitationParticles", "ambientWeatherAudio");

            if (GUILayout.Button(new GUIContent("Add Volume Adapter", "Add an optional weather-to-Volume adapter for URP/HDRP style Volume profiles.")))
                AddVolumeAdapter();
            PungentEnvironmentSimulationEditorGUI.Section("Wind Routing", UtilityWindowTheme.Amber, () =>
            {
                EditorGUILayout.HelpBox("Generic PFU adapters can drive Unity WindZone, ParticleSystem force, Cloth acceleration, or custom IPungentWeatherWindReceiver components without referencing SkiGame-specific wind controllers.", MessageType.Info);
                PungentEnvironmentSimulationEditorGUI.AdaptiveToolbarRow(position.width,
                    PungentEnvironmentSimulationEditorGUI.ToolbarGroup(220f, () =>
                    {
                        if (GUILayout.Button(new GUIContent("Add WindZone Adapter", "Add a generic weather-to-WindZone adapter.")))
                            AddWeatherAdapter<PungentWeatherWindZoneAdapter>();
                    }),
                    PungentEnvironmentSimulationEditorGUI.ToolbarGroup(220f, () =>
                    {
                        if (GUILayout.Button(new GUIContent("Add Particle Wind", "Add a generic weather-to-ParticleSystem force adapter.")))
                            AddWeatherAdapter<PungentWeatherParticleWindAdapter>();
                    }),
                    PungentEnvironmentSimulationEditorGUI.ToolbarGroup(220f, () =>
                    {
                        if (GUILayout.Button(new GUIContent("Add Cloth Wind", "Add a generic weather-to-Cloth acceleration adapter.")))
                            AddWeatherAdapter<PungentWeatherClothWindAdapter>();
                    }),
                    PungentEnvironmentSimulationEditorGUI.ToolbarGroup(220f, () =>
                    {
                        if (GUILayout.Button(new GUIContent("Add Receiver Adapter", "Add a generic weather wind receiver adapter.")))
                            AddWeatherAdapter<PungentWeatherWindReceiverAdapter>();
                    }));
            });
        }

        private void DrawWeatherOutputRoutingHealth(UnityEngine.Object applier)
        {
            SerializedObject serializedApplier = new SerializedObject(applier);
            bool writesShaderGlobals = BoolProperty(serializedApplier, "writeGlobalShaderProperties");
            bool writesFog = BoolProperty(serializedApplier, "writeRenderSettingsFog");
            bool hasCloudRenderers = ArraySize(serializedApplier, "sceneCloudRenderers") > 0;
            bool hasParticles = ArraySize(serializedApplier, "precipitationParticles") > 0;
            bool hasAudio = ObjectProperty(serializedApplier, "ambientWeatherAudio") != null;
            bool hasVolume = _weather.GetComponent<PungentWeatherVolumeAdapter>() != null;
            bool hasWind = _weather.GetComponent<PungentWeatherWindAdapterBase>() != null;

            PungentEnvironmentSimulationEditorGUI.Section("Output Routing Health", UtilityWindowTheme.Teal, () =>
            {
                PungentEnvironmentSimulationEditorGUI.StatusCard("Shader globals", writesShaderGlobals ? "Wetness, snow, cloud, and terrain-shadow globals are enabled." : "Enable shader globals to drive generic material and sky shader outputs.", writesShaderGlobals);
                PungentEnvironmentSimulationEditorGUI.StatusCard("Render fog", writesFog ? "RenderSettings fog output is enabled." : "Enable fog output when this controller should tune scene fog.", writesFog);
                PungentEnvironmentSimulationEditorGUI.StatusCard("Cloud renderers", hasCloudRenderers ? "Scene cloud renderer property blocks are configured." : "Assign cloud renderers for per-renderer cloud alpha, power, softness, tint, and drift.", hasCloudRenderers);
                PungentEnvironmentSimulationEditorGUI.StatusCard("Particles/audio", hasParticles || hasAudio ? "Precipitation particles or ambient weather audio are configured." : "Assign precipitation particles and/or ambient audio for visible weather feedback.", hasParticles || hasAudio);
                PungentEnvironmentSimulationEditorGUI.StatusCard("Volume adapter", hasVolume ? "Weather-to-Volume adapter is present." : "Optional: add a Volume adapter for URP/HDRP-style profile parameters.", hasVolume);
                PungentEnvironmentSimulationEditorGUI.StatusCard("Wind routing", hasWind ? "At least one generic weather wind adapter is present." : "Optional: add WindZone, ParticleSystem, Cloth, or custom receiver wind routing.", hasWind);
            });
        }

        private static bool BoolProperty(SerializedObject serializedObject, string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            return property != null && property.boolValue;
        }

        private static int ArraySize(SerializedObject serializedObject, string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            return property != null && property.isArray ? property.arraySize : 0;
        }

        private static UnityEngine.Object ObjectProperty(SerializedObject serializedObject, string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            return property != null && property.propertyType == SerializedPropertyType.ObjectReference ? property.objectReferenceValue : null;
        }

        private void DrawWeatherEvents()
        {
            PungentEnvironmentSimulationEditorGUI.DrawProperties(new SerializedObject(_weather), "onWeatherChanged", "onForecastRegenerated", "currentState");
        }

        private void PreviewSelectedPreset()
        {
            Undo.RecordObject(_weather, "Preview Pungent Weather");
            _weather.PreviewPreset(_selectedPreset);
            EditorUtility.SetDirty(_weather);
        }

        private void GenerateForecast()
        {
            Undo.RecordObject(_weather, "Regenerate Pungent Forecast");
            _weather.GenerateDailyForecast();
            EditorUtility.SetDirty(_weather);
        }

        private void AddOutputApplier()
        {
            Undo.RecordObject(_weather, "Assign Weather Output Applier");
            PungentWeatherOutputApplier applier = _weather.GetComponent<PungentWeatherOutputApplier>();
            if (applier == null)
                applier = Undo.AddComponent<PungentWeatherOutputApplier>(_weather.gameObject);
            SerializedObject serialized = new SerializedObject(_weather);
            SerializedProperty property = serialized.FindProperty("outputApplier");
            property.objectReferenceValue = applier;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(_weather);
        }

        private void AddVolumeAdapter()
        {
            PungentWeatherVolumeAdapter adapter = _weather.GetComponent<PungentWeatherVolumeAdapter>();
            if (adapter == null)
                adapter = Undo.AddComponent<PungentWeatherVolumeAdapter>(_weather.gameObject);

            SerializedObject serializedAdapter = new SerializedObject(adapter);
            SerializedProperty weather = serializedAdapter.FindProperty("weather");
            if (weather != null)
                weather.objectReferenceValue = _weather;
            serializedAdapter.ApplyModifiedProperties();
            Selection.activeObject = adapter;
        }

        private void AddWeatherAdapter<TAdapter>() where TAdapter : Component
        {
            TAdapter adapter = _weather.GetComponent<TAdapter>();
            if (adapter == null)
                adapter = Undo.AddComponent<TAdapter>(_weather.gameObject);

            SerializedObject serializedAdapter = new SerializedObject(adapter);
            SerializedProperty weather = serializedAdapter.FindProperty("weather");
            if (weather != null)
                weather.objectReferenceValue = _weather;
            serializedAdapter.ApplyModifiedProperties();
            Selection.activeObject = adapter;
        }

        private void Refresh()
        {
            _weather = PungentEnvironmentSimulationEditorGUI.FindSceneObject<PungentWeatherController>();
            _validation.Refresh(PungentEnvironmentSimulationEditorGUI.FindSceneObject<PungentClockController>(), _weather, PungentEnvironmentSimulationEditorGUI.FindSceneObject<PungentDeltaTimeController>());
            Repaint();
        }
    }

    public sealed class PungentDeltaTimeUtilityWindow : EditorWindow
    {
        private const string PrefPrefix = "PungentFunk.Environment.DeltaTime.";
        private const string EditorOwner = "Delta Time Utility";
        private PungentDeltaTimeController _deltaTime;
        private Vector2 _centerScroll;
        private Vector2 _rightScroll;
        private int _selectedChannel;
        private float _rightWidth;
        private float _scaleValue = 1f;
        private readonly PungentEnvironmentSimulationValidationReport _validation = new PungentEnvironmentSimulationValidationReport();

        private static readonly PungentTimeChannel[] CommonChannels =
        {
            PungentTimeChannel.Global,
            PungentTimeChannel.Gameplay,
            PungentTimeChannel.UI,
            PungentTimeChannel.Physics,
            PungentTimeChannel.Animation,
            PungentTimeChannel.Audio,
            PungentTimeChannel.Particles,
            PungentTimeChannel.Clock,
            PungentTimeChannel.Weather
        };

        public static void Open()
        {
            PungentDeltaTimeUtilityWindow window = GetWindow<PungentDeltaTimeUtilityWindow>("Delta Time");
            window.minSize = new Vector2(420f, 380f);
            window.Show();
        }

        private void OnEnable()
        {
            minSize = new Vector2(420f, 380f);
            _selectedChannel = PungentEnvironmentSimulationEditorGUI.GetInt(PrefPrefix + "SelectedChannel", 0, 0, CommonChannels.Length - 1);
            _rightWidth = PungentEnvironmentSimulationEditorGUI.GetFloat(PrefPrefix + "RightWidth", 330f, 260f, 480f);
            _scaleValue = PungentEnvironmentSimulationEditorGUI.GetFloat(PrefPrefix + "ScaleValue", 1f, 0f, 8f);
            Refresh();
        }

        private void OnDisable()
        {
            PungentEnvironmentSimulationEditorGUI.SetInt(PrefPrefix + "SelectedChannel", _selectedChannel);
            PungentEnvironmentSimulationEditorGUI.SetFloat(PrefPrefix + "RightWidth", _rightWidth);
            PungentEnvironmentSimulationEditorGUI.SetFloat(PrefPrefix + "ScaleValue", _scaleValue);
        }

        private void OnGUI()
        {
            UtilityWindowTheme.EnsureStyles();
            PungentEnvironmentSimulationEditorGUI.Header("Delta Time", "Channel console for pause stacks, slow motion, and opt-in time scaling.", "delta-time-controller");
            PungentEnvironmentSimulationEditorGUI.DrawLayoutQAStrip(
                position.width,
                position.height,
                "Delta Time: control and diagnose time channels.",
                "Single scroll: command, channel cards, scenarios, selected detail.",
                "Channel console and scenarios first; selected channel detail below.",
                "Channel matrix owns main area; selected channel detail rail on the right.");
            DrawDeltaCommandStrip();

            if (_deltaTime == null)
            {
                DrawCreateMissing();
                return;
            }

            _validation.Refresh(null, null, _deltaTime);
            PungentEnvironmentSimulationEditorGUI.ResizableRightRailLayout(
                position.width,
                PrefPrefix + "RightWidth",
                ref _centerScroll,
                ref _rightScroll,
                ref _rightWidth,
                520f,
                270f,
                480f,
                () =>
                {
                    DrawChannelConsole();
                    DrawScenarioPanel();
                },
                DrawChannelDetail);
        }

        private void DrawDeltaCommandStrip()
        {
            PungentEnvironmentSimulationEditorGUI.DrawTargetRow(ref _deltaTime, "Delta Time Controller", Refresh, PungentEnvironmentSimulationWindow.Open);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber, 0.13f, 0.06f, 5, 3)))
            {
                if (_deltaTime == null)
                {
                    EditorGUILayout.LabelField("No delta-time controller selected.", UtilityWindowTheme.MutedMiniLabelStyle);
                    return;
                }

                PungentEnvironmentSimulationEditorGUI.AdaptiveToolbarRow(position.width,
                    PungentEnvironmentSimulationEditorGUI.ToolbarGroup(190f, () =>
                    {
                        PungentEnvironmentSimulationEditorGUI.Chip("Time " + Time.timeScale.ToString("0.00"), UtilityWindowTheme.Amber, 80f);
                        PungentEnvironmentSimulationEditorGUI.Chip("Fixed " + Time.fixedDeltaTime.ToString("0.000"), UtilityWindowTheme.Cyan, 92f);
                    }),
                    PungentEnvironmentSimulationEditorGUI.ToolbarGroup(400f, () =>
                    {
                        if (GUILayout.Button(new GUIContent("Global Pause", "Push a global pause request."), GUILayout.MinWidth(104f)))
                            _deltaTime.PushPause(PungentTimeChannel.Global, EditorOwner);
                        if (GUILayout.Button(new GUIContent("Resume", "Pop one global pause request."), GUILayout.MinWidth(72f)))
                            _deltaTime.PopPause(PungentTimeChannel.Global, EditorOwner);
                        if (GUILayout.Button(new GUIContent("Reset All", "Clear pauses and restore profile defaults."), GUILayout.MinWidth(82f)))
                            _deltaTime.ResetAll();
                        if (GUILayout.Button(new GUIContent("Rebuild Channels", "Reload channel settings from the profile."), GUILayout.MinWidth(124f)))
                            _deltaTime.RebuildChannels();
                    }));
            }
        }

        private void DrawCreateMissing()
        {
            PungentEnvironmentSimulationEditorGUI.StatusCard("Delta Time Controller missing", "Create a scene controller to configure time-scale channels and pause stacks.", false, () =>
            {
                if (GUILayout.Button(new GUIContent("Create Delta Time Controller", "Create a Pungent Delta Time Controller in the open scene.")))
                {
                    GameObject go = PungentEnvironmentSimulationEditorGUI.CreateController<PungentDeltaTimeController>("Pungent Delta Time");
                    _deltaTime = go.GetComponent<PungentDeltaTimeController>();
                }
            });
        }

        private void DrawChannelConsole()
        {
            using (PungentEnvironmentSimulationEditorGUI.StretchPanelScope(UtilityWindowTheme.Green))
            {
                UtilityWindowTheme.SectionTitle("Channel Matrix", UtilityWindowTheme.Green);
                DrawDeltaSetup();
                for (int i = 0; i < CommonChannels.Length; i++)
                    DrawChannelRow(i, CommonChannels[i]);
            }
        }

        private void DrawDeltaSetup()
        {
            SerializedObject serializedDelta = new SerializedObject(_deltaTime);
            PungentEnvironmentSimulationEditorGUI.DrawObjectPropertyWithCreate<PungentDeltaTimeProfile>(serializedDelta, "profile", "Delta Time Profile", "Pungent Delta Time Profile");
            PungentEnvironmentSimulationEditorGUI.DrawProperties(serializedDelta, "claimSingleton", "applyUnityTimeScale", "fixedDeltaTimeBase");
            PungentEnvironmentSimulationEditorGUI.StatusCard("Unity time writers", _validation.hasUnityTimeWriter ? _validation.unityTimeWriterCount + " channel(s) can drive Time.timeScale." : "No profile channel is marked as a Unity time-scale writer.", _validation.hasUnityTimeWriter);
        }

        private void DrawChannelRow(int index, PungentTimeChannel channel)
        {
            float consoleWidth = position.width >= 920f ? Mathf.Max(300f, position.width - _rightWidth - 52f) : position.width;
            if (consoleWidth < 780f)
            {
                DrawChannelCard(index, channel);
                return;
            }

            bool selected = index == _selectedChannel;
            bool unityWriter = IsUnityTimeScaleWriter(channel);
            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(selected ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, selected ? 0.22f : 0.10f, 0.04f, 4, 2)))
            {
                if (GUILayout.Button(new GUIContent(channel.ToString(), "Select channel details."), EditorStyles.miniButton, GUILayout.Width(92f)))
                    _selectedChannel = index;
                PungentEnvironmentSimulationEditorGUI.Chip("Scale " + _deltaTime.GetScale(channel).ToString("0.00"), UtilityWindowTheme.Cyan, 82f);
                PungentEnvironmentSimulationEditorGUI.Chip("Pause " + _deltaTime.GetPauseStack(channel), _deltaTime.GetPauseStack(channel) > 0 ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, 76f);
                if (unityWriter)
                    PungentEnvironmentSimulationEditorGUI.Chip("Unity Time", UtilityWindowTheme.Purple, 88f);
                _scaleValue = EditorGUILayout.Slider(_scaleValue, 0f, 4f);
                if (GUILayout.Button(new GUIContent("Set", "Set selected scale on this channel."), GUILayout.Width(48f)))
                    _deltaTime.SetScale(channel, EditorOwner, _scaleValue);
                if (GUILayout.Button(new GUIContent("Pause", "Push a pause request."), GUILayout.Width(58f)))
                    _deltaTime.PushPause(channel, EditorOwner);
                if (GUILayout.Button(new GUIContent("Pop", "Pop one pause request."), GUILayout.Width(44f)))
                    _deltaTime.PopPause(channel, EditorOwner);
                if (GUILayout.Button(new GUIContent("Clear", "Clear this channel pause stack."), GUILayout.Width(52f)))
                    _deltaTime.ClearPause(channel);
            }
        }

        private void DrawChannelCard(int index, PungentTimeChannel channel)
        {
            bool selected = index == _selectedChannel;
            bool unityWriter = IsUnityTimeScaleWriter(channel);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(selected ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, selected ? 0.22f : 0.10f, 0.04f, 4, 2)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent(channel.ToString(), "Select channel details."), EditorStyles.miniButton, GUILayout.MinWidth(104f)))
                        _selectedChannel = index;
                    GUILayout.FlexibleSpace();
                    if (unityWriter)
                        PungentEnvironmentSimulationEditorGUI.Chip("Unity Time", UtilityWindowTheme.Purple, 88f);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    PungentEnvironmentSimulationEditorGUI.Chip("Scale " + _deltaTime.GetScale(channel).ToString("0.00"), UtilityWindowTheme.Cyan, 86f);
                    PungentEnvironmentSimulationEditorGUI.Chip("Pause " + _deltaTime.GetPauseStack(channel), _deltaTime.GetPauseStack(channel) > 0 ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, 78f);
                }

                _scaleValue = EditorGUILayout.Slider(new GUIContent("Target Scale", "Scale value applied when Set is pressed."), _scaleValue, 0f, 4f);
                PungentEnvironmentSimulationEditorGUI.AdaptiveToolbarRow(position.width,
                    PungentEnvironmentSimulationEditorGUI.ToolbarGroup(180f, () =>
                    {
                        if (GUILayout.Button(new GUIContent("Set Scale", "Set selected scale on this channel.")))
                            _deltaTime.SetScale(channel, EditorOwner, _scaleValue);
                        if (GUILayout.Button(new GUIContent("Pause", "Push a pause request.")))
                            _deltaTime.PushPause(channel, EditorOwner);
                    }),
                    PungentEnvironmentSimulationEditorGUI.ToolbarGroup(190f, () =>
                    {
                        if (GUILayout.Button(new GUIContent("Pop Pause", "Pop one pause request.")))
                            _deltaTime.PopPause(channel, EditorOwner);
                        if (GUILayout.Button(new GUIContent("Clear Pause", "Clear this channel pause stack.")))
                            _deltaTime.ClearPause(channel);
                    }));
            }
        }

        private bool IsUnityTimeScaleWriter(PungentTimeChannel channel)
        {
            UnityEngine.Object profile = PungentEnvironmentSimulationEditorGUI.ObjectValue(_deltaTime, "profile");
            if (profile == null)
                return channel == PungentTimeChannel.Global;

            SerializedObject serializedProfile = new SerializedObject(profile);
            SerializedProperty channels = serializedProfile.FindProperty("channels");
            if (channels == null)
                return false;

            for (int i = 0; i < channels.arraySize; i++)
            {
                SerializedProperty item = channels.GetArrayElementAtIndex(i);
                if (item.FindPropertyRelative("channel").enumValueIndex == (int)channel)
                    return item.FindPropertyRelative("applyToUnityTimeScale").boolValue;
            }

            return false;
        }

        private void DrawScenarioPanel()
        {
            PungentEnvironmentSimulationEditorGUI.Section("Quick Scenarios", UtilityWindowTheme.Amber, () =>
            {
                PungentEnvironmentSimulationEditorGUI.AdaptiveToolbarRow(position.width,
                    PungentEnvironmentSimulationEditorGUI.ToolbarGroup(280f, () =>
                    {
                        if (GUILayout.Button(new GUIContent("Global Pause", "Pause all Unity-time systems.")))
                            _deltaTime.PushPause(PungentTimeChannel.Global, "Scenario Global Pause");
                        if (GUILayout.Button(new GUIContent("Gameplay Slow Motion", "Set gameplay channel to 0.25.")))
                            _deltaTime.SetScale(PungentTimeChannel.Gameplay, "Scenario Slow Motion", 0.25f);
                    }),
                    PungentEnvironmentSimulationEditorGUI.ToolbarGroup(440f, () =>
                    {
                        if (GUILayout.Button(new GUIContent("Clock Freeze", "Pause clock channel without pausing UI.")))
                            _deltaTime.PushPause(PungentTimeChannel.Clock, "Scenario Clock Freeze");
                        if (GUILayout.Button(new GUIContent("Weather Fast Forward", "Speed weather channel to 4x.")))
                            _deltaTime.SetScale(PungentTimeChannel.Weather, "Scenario Weather Fast Forward", 4f);
                        if (GUILayout.Button(new GUIContent("Restore", "Reset all channels.")))
                            _deltaTime.ResetAll();
                    }));
            });
        }

        private void DrawChannelDetail()
        {
            PungentTimeChannel channel = CommonChannels[Mathf.Clamp(_selectedChannel, 0, CommonChannels.Length - 1)];
            PungentEnvironmentSimulationEditorGUI.Section("Selected Channel", UtilityWindowTheme.Amber, () =>
            {
                EditorGUILayout.LabelField(channel.ToString(), UtilityWindowTheme.CardLabelStyle);
                EditorGUILayout.LabelField("Effective scale " + _deltaTime.GetScale(channel).ToString("0.00") + " | Pause stack " + _deltaTime.GetPauseStack(channel), UtilityWindowTheme.MutedMiniLabelStyle);
                EditorGUILayout.LabelField("Pause owners: " + _deltaTime.GetPauseOwnerSummary(channel), UtilityWindowTheme.MutedMiniLabelStyle);
                EditorGUILayout.LabelField("Scale requests: " + _deltaTime.GetScaleOwnerSummary(channel), UtilityWindowTheme.MutedMiniLabelStyle);
                PungentEnvironmentSimulationEditorGUI.AdaptiveToolbarRow(position.width,
                    PungentEnvironmentSimulationEditorGUI.ToolbarGroup(190f, () =>
                    {
                        if (GUILayout.Button(new GUIContent("Clear Utility Scale", "Clear the Delta Time Utility owner scale request for this channel.")))
                            _deltaTime.ClearScale(channel, EditorOwner);
                    }),
                    PungentEnvironmentSimulationEditorGUI.ToolbarGroup(190f, () =>
                    {
                        if (GUILayout.Button(new GUIContent("Clear Scenario Scale", "Clear common scenario scale requests for this channel.")))
                            ClearScenarioScale(channel);
                    }));
                DrawProfileChannelEditor(channel);
                PungentEnvironmentSimulationEditorGUI.Section("Unity System Fit", UtilityWindowTheme.Cyan, () =>
                {
                    PungentEnvironmentSimulationEditorGUI.OpportunityCard(_validation.inputSystem);
                    PungentEnvironmentSimulationEditorGUI.OpportunityCard(_validation.timeline);
                    PungentEnvironmentSimulationEditorGUI.OpportunityCard(_validation.testFramework);
                    if (GUILayout.Button(new GUIContent("Add PlayableDirector Adapter", "Add an adapter that applies this channel's scale to a PlayableDirector graph.")))
                        AddPlayableDirectorAdapter();
                    if (GUILayout.Button(new GUIContent("Add Animator Adapter", "Add an adapter that applies this channel's scale to Animator speeds.")))
                        AddDeltaAdapter<PungentDeltaTimeAnimatorAdapter>();
                    if (GUILayout.Button(new GUIContent("Add Audio Adapter", "Add an adapter that applies this channel's scale to AudioSource pitch/mute policy.")))
                        AddDeltaAdapter<PungentDeltaTimeAudioAdapter>();
                    if (GUILayout.Button(new GUIContent("Add Particle Adapter", "Add an adapter that applies this channel's scale to ParticleSystem simulation speed.")))
                        AddDeltaAdapter<PungentDeltaTimeParticleAdapter>();
                });
                PungentEnvironmentSimulationEditorGUI.DrawProperties(new SerializedObject(_deltaTime), "onChannelScaleChanged", "onPauseStackChanged");
            });
        }

        private void DrawProfileChannelEditor(PungentTimeChannel channel)
        {
            UnityEngine.Object profile = PungentEnvironmentSimulationEditorGUI.ObjectValue(_deltaTime, "profile");
            if (profile == null)
            {
                EditorGUILayout.HelpBox("Assign a Delta Time Profile to edit channel definitions.", MessageType.Info);
                return;
            }

            SerializedObject serializedProfile = new SerializedObject(profile);
            SerializedProperty channels = serializedProfile.FindProperty("channels");
            if (channels == null)
                return;

            serializedProfile.Update();
            for (int i = 0; i < channels.arraySize; i++)
            {
                SerializedProperty item = channels.GetArrayElementAtIndex(i);
                if (item.FindPropertyRelative("channel").enumValueIndex == (int)channel)
                {
                    EditorGUILayout.PropertyField(item, new GUIContent(channel + " Settings"), true);
                    serializedProfile.ApplyModifiedProperties();
                    return;
                }
            }

            EditorGUILayout.PropertyField(channels, true);
            serializedProfile.ApplyModifiedProperties();
        }

        private void AddPlayableDirectorAdapter()
        {
            PungentDeltaTimePlayableDirectorAdapter adapter = _deltaTime.GetComponent<PungentDeltaTimePlayableDirectorAdapter>();
            if (adapter == null)
                adapter = Undo.AddComponent<PungentDeltaTimePlayableDirectorAdapter>(_deltaTime.gameObject);

            SerializedObject serializedAdapter = new SerializedObject(adapter);
            SerializedProperty delta = serializedAdapter.FindProperty("deltaTime");
            if (delta != null)
                delta.objectReferenceValue = _deltaTime;
            SerializedProperty channel = serializedAdapter.FindProperty("channel");
            if (channel != null)
                channel.enumValueIndex = (int)CommonChannels[Mathf.Clamp(_selectedChannel, 0, CommonChannels.Length - 1)];
            serializedAdapter.ApplyModifiedProperties();
            Selection.activeObject = adapter;
        }

        private void ClearScenarioScale(PungentTimeChannel channel)
        {
            _deltaTime.ClearScale(channel, "Scenario Slow Motion");
            _deltaTime.ClearScale(channel, "Scenario Weather Fast Forward");
        }

        private void AddDeltaAdapter<TAdapter>() where TAdapter : Component
        {
            TAdapter adapter = _deltaTime.GetComponent<TAdapter>();
            if (adapter == null)
                adapter = Undo.AddComponent<TAdapter>(_deltaTime.gameObject);

            SerializedObject serializedAdapter = new SerializedObject(adapter);
            SerializedProperty delta = serializedAdapter.FindProperty("deltaTime");
            if (delta != null)
                delta.objectReferenceValue = _deltaTime;
            SerializedProperty channel = serializedAdapter.FindProperty("channel");
            if (channel != null)
                channel.enumValueIndex = (int)CommonChannels[Mathf.Clamp(_selectedChannel, 0, CommonChannels.Length - 1)];
            serializedAdapter.ApplyModifiedProperties();
            Selection.activeObject = adapter;
        }

        private void Refresh()
        {
            _deltaTime = PungentEnvironmentSimulationEditorGUI.FindSceneObject<PungentDeltaTimeController>();
            _validation.Refresh(PungentEnvironmentSimulationEditorGUI.FindSceneObject<PungentClockController>(), PungentEnvironmentSimulationEditorGUI.FindSceneObject<PungentWeatherController>(), _deltaTime);
            Repaint();
        }
    }
#endif
}
