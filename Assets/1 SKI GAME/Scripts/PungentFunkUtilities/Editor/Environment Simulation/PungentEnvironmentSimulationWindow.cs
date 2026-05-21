using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.EnvironmentSimulation;

namespace PungentFunk.Utilities.Editor.EnvironmentSimulation
{
#if UNITY_EDITOR
    using UnityEditor;
    using UnityEngine;

    public sealed class PungentEnvironmentSimulationWindow : EditorWindow
    {
        private readonly PungentEnvironmentSimulationEditorState _state = new PungentEnvironmentSimulationEditorState();
        private Vector2 _scroll;
        private float _setHour = 8f;
        private float _slowMotionScale = 0.25f;
        private string _status = "Use Refresh to locate environment simulation controllers in the open scene.";

        public static void Open()
        {
            PungentEnvironmentSimulationWindow window = GetWindow<PungentEnvironmentSimulationWindow>("Environment Simulation");
            window.minSize = new Vector2(420f, 360f);
            window.Show();
        }

        private void OnEnable()
        {
            minSize = new Vector2(420f, 360f);
            RefreshSceneReferences();
        }

        private void OnGUI()
        {
            UtilityWindowTheme.EnsureStyles();
            PungentEnvironmentSimulationEditorGUI.Header(
                "Environment Simulation",
                "Scene-level control panel for calendar, weather, and delta-time systems.",
                "environment-simulation");
            PungentEnvironmentSimulationEditorGUI.DrawLayoutQAStrip(
                position.width,
                position.height,
                "Hub: scene status, health, quick previews, and focused-tool routing.",
                "Single scroll: summary, health, integrations, previews.",
                "Dashboard first; cards stack without competing panes.",
                "Dashboard overview stays dominant; no deep configuration.");
            DrawToolbar();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            try
            {
                DrawSystemOverview();
                DrawHealthPanel();
                DrawUnityIntegrationPanel();
                DrawQuickPreviewPanel();
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.10f, 0.05f, 5, 4)))
            {
                if (GUILayout.Button(new GUIContent("Refresh", "Find clock, weather, and delta-time controllers in the loaded scenes."), GUILayout.Width(80f)))
                    RefreshSceneReferences();

                if (GUILayout.Button(new GUIContent("Calendar Clock", "Open the focused Calendar Clock utility."), GUILayout.Width(116f)))
                    PungentCalendarClockWindow.Open();
                if (GUILayout.Button(new GUIContent("Weather", "Open the focused Weather utility."), GUILayout.Width(82f)))
                    PungentWeatherUtilityWindow.Open();
                if (GUILayout.Button(new GUIContent("Delta Time", "Open the focused Delta Time utility."), GUILayout.Width(92f)))
                    PungentDeltaTimeUtilityWindow.Open();

                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(_status, UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.MinWidth(220f));
            }
        }

        private void DrawSystemOverview()
        {
            PungentEnvironmentSimulationEditorGUI.Section("Systems", UtilityWindowTheme.Blue, () =>
            {
                DrawSystemCard(
                    "Calendar Clock",
                    _state.clock,
                    _state.HasClock ? DescribeClock(_state.clock) : "No Pungent Clock Controller found in the open scene.",
                    UtilityWindowTheme.Cyan,
                    PungentCalendarClockWindow.Open,
                    () => CreateAndRefresh<PungentClockController>("Pungent Clock"));

                DrawSystemCard(
                    "Weather",
                    _state.weather,
                    _state.HasWeather ? DescribeWeather(_state.weather) : "No Pungent Weather Controller found in the open scene.",
                    UtilityWindowTheme.Teal,
                    PungentWeatherUtilityWindow.Open,
                    () =>
                    {
                        GameObject go = CreateAndRefresh<PungentWeatherController>("Pungent Weather");
                        if (go != null && go.GetComponent<PungentWeatherOutputApplier>() == null)
                            Undo.AddComponent<PungentWeatherOutputApplier>(go);
                    });

                DrawSystemCard(
                    "Delta Time",
                    _state.deltaTime,
                    _state.HasDeltaTime ? DescribeDeltaTime(_state.deltaTime) : "No Pungent Delta Time Controller found in the open scene.",
                    UtilityWindowTheme.Amber,
                    PungentDeltaTimeUtilityWindow.Open,
                    () => CreateAndRefresh<PungentDeltaTimeController>("Pungent Delta Time"));

                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill("Systems " + _state.validation.configuredSystems + "/3", UtilityWindowTheme.Blue, 92f);
                    UtilityWindowTheme.CountPill("Profiles " + _state.validation.readySystems + "/3", _state.validation.readySystems == 3 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 94f);
                    UtilityWindowTheme.CountPill("Unity Writers " + _state.validation.unityTimeWriterCount, _state.validation.hasUnityTimeWriter ? UtilityWindowTheme.Purple : UtilityWindowTheme.Amber, 112f);
                    GUILayout.FlexibleSpace();
                }
            });
        }

        private void DrawSystemCard(string title, Object target, string detail, Color tint, System.Action open, System.Action create)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, target != null ? 0.12f : 0.18f, 0.06f, 5, 3)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(title, UtilityWindowTheme.CardLabelStyle);
                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill(target != null ? "Found" : "Missing", target != null ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 76f);
                }

                EditorGUILayout.LabelField(detail, UtilityWindowTheme.MutedMiniLabelStyle);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Open", "Open the focused configuration surface."), GUILayout.Width(72f)))
                        open?.Invoke();
                    using (new EditorGUI.DisabledScope(target == null))
                    {
                        if (GUILayout.Button(new GUIContent("Select", "Select this scene controller."), GUILayout.Width(72f)))
                            Selection.activeObject = target;
                    }
                    if (target == null && GUILayout.Button(new GUIContent("Create", "Create a scene controller for this system."), GUILayout.Width(72f)))
                    {
                        create?.Invoke();
                        RefreshSceneReferences();
                    }
                }
            }
        }

        private void DrawHealthPanel()
        {
            PungentEnvironmentSimulationEditorGUI.Section("System Health", UtilityWindowTheme.Purple, () =>
            {
                bool clockProfile = _state.clock != null && PungentEnvironmentSimulationEditorGUI.ObjectValue(_state.clock, "calendarProfile") != null;
                bool skyProfile = _state.clock != null && PungentEnvironmentSimulationEditorGUI.ObjectValue(_state.clock, "skyProfile") != null;
                PungentEnvironmentSimulationEditorGUI.StatusCard("Calendar profile", clockProfile ? "Calendar profile assigned." : "Assign or create a Calendar Profile in the Calendar Clock utility.", clockProfile, () =>
                {
                    if (GUILayout.Button(new GUIContent("Open Calendar Clock", "Configure calendar profile, sky output, and clock progression.")))
                        PungentCalendarClockWindow.Open();
                });
                PungentEnvironmentSimulationEditorGUI.StatusCard("Sky output", skyProfile ? "Clock sky profile assigned." : "Sky profile is optional, but assigning one enables shared lighting and fog preview.", skyProfile);

                bool weatherProfile = _state.weather != null && PungentEnvironmentSimulationEditorGUI.ObjectValue(_state.weather, "weatherProfile") != null;
                bool output = _state.weather != null && PungentEnvironmentSimulationEditorGUI.ObjectValue(_state.weather, "outputApplier") != null;
                bool forecast = _state.weather != null && PungentEnvironmentSimulationEditorGUI.ArraySize(_state.weather, "forecast") > 0;
                PungentEnvironmentSimulationEditorGUI.StatusCard("Weather profile", weatherProfile ? "Weather profile assigned." : "Assign or create a Weather Profile in the Weather utility.", weatherProfile, () =>
                {
                    if (GUILayout.Button(new GUIContent("Open Weather", "Configure weather presets, forecasts, and environment outputs.")))
                        PungentWeatherUtilityWindow.Open();
                });
                PungentEnvironmentSimulationEditorGUI.StatusCard("Weather output", output ? "Output applier assigned." : "Output applier is optional, but required for fog, shader, particle, and audio outputs.", output);
                PungentEnvironmentSimulationEditorGUI.StatusCard("Forecast", forecast ? "Forecast entries are available." : "Generate a forecast from the Weather utility.", forecast);

                bool deltaProfile = _state.deltaTime != null && PungentEnvironmentSimulationEditorGUI.ObjectValue(_state.deltaTime, "profile") != null;
                PungentEnvironmentSimulationEditorGUI.StatusCard("Delta profile", deltaProfile ? "Delta-time profile assigned." : "Assign or create a Delta Time Profile in the Delta Time utility.", deltaProfile, () =>
                {
                    if (GUILayout.Button(new GUIContent("Open Delta Time", "Configure channel scales and pause behaviour.")))
                        PungentDeltaTimeUtilityWindow.Open();
                });

                PungentEnvironmentSimulationEditorGUI.StatusCard("Clock Timeline bridge", _state.validation.hasTimelineClockBridge ? "PlayableDirector clock bridge is present." : "Optional: add from Calendar Clock when Timeline should scrub time-of-day.", _state.validation.hasTimelineClockBridge);
                PungentEnvironmentSimulationEditorGUI.StatusCard("Weather Volume bridge", _state.validation.hasWeatherVolumeAdapter ? "Weather Volume adapter is present." : "Optional: add from Weather Outputs to drive Volume-style profile parameters.", _state.validation.hasWeatherVolumeAdapter);
                PungentEnvironmentSimulationEditorGUI.StatusCard("Weather Wind routing", _state.validation.hasWeatherWindAdapter ? "At least one generic weather wind adapter is present." : "Optional: add WindZone, ParticleSystem, Cloth, or receiver adapters from Weather Outputs.", _state.validation.hasWeatherWindAdapter);
                PungentEnvironmentSimulationEditorGUI.StatusCard("Delta Timeline bridge", _state.validation.hasPlayableDirectorAdapter ? "PlayableDirector delta-time adapter is present." : "Optional: add from Delta Time to scale Timeline/PlayableDirector playback.", _state.validation.hasPlayableDirectorAdapter);
            });
        }

        private void DrawUnityIntegrationPanel()
        {
            PungentEnvironmentSimulationEditorGUI.Section("Unity Integration Opportunities", UtilityWindowTheme.Cyan, () =>
            {
                EditorGUILayout.LabelField("Installed packages and optional bridges this environment suite can use without hard runtime dependencies.", UtilityWindowTheme.MutedMiniLabelStyle);
                PungentEnvironmentSimulationEditorGUI.OpportunityCard(_state.validation.timeline);
                PungentEnvironmentSimulationEditorGUI.OpportunityCard(_state.validation.urp);
                PungentEnvironmentSimulationEditorGUI.OpportunityCard(_state.validation.shaderGraph);
                PungentEnvironmentSimulationEditorGUI.OpportunityCard(_state.validation.terrainTools);
                PungentEnvironmentSimulationEditorGUI.OpportunityCard(_state.validation.inputSystem);
                PungentEnvironmentSimulationEditorGUI.OpportunityCard(_state.validation.testFramework);
            });
        }

        private void DrawQuickPreviewPanel()
        {
            PungentEnvironmentSimulationEditorGUI.Section("Quick Preview", UtilityWindowTheme.Green, () =>
            {
                if (_state.clock != null)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _setHour = EditorGUILayout.Slider(new GUIContent("Clock Hour", "Preview or set the active clock hour."), _setHour, 0f, 23.99f);
                        if (GUILayout.Button(new GUIContent("Set", "Set the clock to this hour."), GUILayout.Width(54f)))
                        {
                            Undo.RecordObject(_state.clock, "Set Pungent Clock Hour");
                            _state.clock.SetHour(_setHour);
                            EditorUtility.SetDirty(_state.clock);
                        }
                        if (GUILayout.Button(new GUIContent("Advance Day", "Advance the calendar one day."), GUILayout.Width(104f)))
                        {
                            Undo.RecordObject(_state.clock, "Advance Pungent Clock Day");
                            _state.clock.AdvanceDays(1);
                            EditorUtility.SetDirty(_state.clock);
                        }
                    }
                }

                if (_state.weather != null)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button(new GUIContent("Regenerate Forecast", "Generate a 24-hour forecast for the current day.")))
                        {
                            Undo.RecordObject(_state.weather, "Generate Pungent Weather Forecast");
                            _state.weather.GenerateDailyForecast();
                            EditorUtility.SetDirty(_state.weather);
                        }
                    }
                }

                if (_state.deltaTime != null)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button(new GUIContent("Pause", "Push a global pause request.")))
                            _state.deltaTime.PushPause(PungentTimeChannel.Global);
                        if (GUILayout.Button(new GUIContent("Resume", "Pop one global pause request.")))
                            _state.deltaTime.PopPause(PungentTimeChannel.Global);
                        _slowMotionScale = EditorGUILayout.Slider(new GUIContent("Gameplay Scale", "Set Gameplay channel scale."), _slowMotionScale, 0f, 1f);
                        if (GUILayout.Button(new GUIContent("Apply", "Apply Gameplay scale."), GUILayout.Width(62f)))
                            _state.deltaTime.SetScale(PungentTimeChannel.Gameplay, _slowMotionScale);
                        if (GUILayout.Button(new GUIContent("Restore", "Restore delta-time defaults."), GUILayout.Width(72f)))
                            _state.deltaTime.ResetAll();
                    }
                }
            });
        }

        private string DescribeClock(PungentClockController clock)
        {
            PungentClockSnapshot snapshot = clock.Snapshot;
            return snapshot.TimeLabel24 + "  " + snapshot.DateLabel + "  " + snapshot.seasonName;
        }

        private string DescribeWeather(PungentWeatherController weather)
        {
            PungentWeatherState state = weather.CurrentState;
            string name = string.IsNullOrWhiteSpace(state.displayName) ? "No active weather" : state.displayName;
            return name + "  " + state.temperature.ToString("0") + " C  rain " + state.precipitationChance.ToString("0") + "%  wind " + state.windSpeed.ToString("0.00");
        }

        private string DescribeDeltaTime(PungentDeltaTimeController deltaTime)
        {
            return "Global " + deltaTime.GetScale(PungentTimeChannel.Global).ToString("0.00") +
                   "  Gameplay " + deltaTime.GetScale(PungentTimeChannel.Gameplay).ToString("0.00") +
                   "  Pauses " + deltaTime.GetPauseStack(PungentTimeChannel.Global);
        }

        private GameObject CreateAndRefresh<T>(string name) where T : Component
        {
            GameObject go = PungentEnvironmentSimulationEditorGUI.CreateController<T>(name);
            RefreshSceneReferences();
            return go;
        }

        private void RefreshSceneReferences()
        {
            _state.Refresh();
            _status = _state.Summary();
            Repaint();
        }
    }

    public sealed class PungentEnvironmentSimulationLayoutQAWindow : EditorWindow
    {
        private Vector2 _scroll;

        public static void Open()
        {
            PungentEnvironmentSimulationLayoutQAWindow window = GetWindow<PungentEnvironmentSimulationLayoutQAWindow>("Environment Layout QA");
            window.minSize = new Vector2(420f, 360f);
            window.Show();
        }

        private void OnGUI()
        {
            UtilityWindowTheme.EnsureStyles();
            PungentEnvironmentSimulationEditorGUI.Header(
                "Environment Layout QA",
                "Open the four Environment Simulation tools at repeatable visual QA sizes.",
                "environment-simulation");

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            try
            {
                PungentEnvironmentSimulationEditorGUI.Section("Workflow Targets", UtilityWindowTheme.Blue, () =>
                {
                    EditorGUILayout.LabelField("Hub: dashboard/control panel, not a deep editor.", UtilityWindowTheme.MutedMiniLabelStyle);
                    EditorGUILayout.LabelField("Calendar: time authoring timeline first; setup/detail secondary.", UtilityWindowTheme.MutedMiniLabelStyle);
                    EditorGUILayout.LabelField("Weather: forecast first; preset browser and selected details secondary.", UtilityWindowTheme.MutedMiniLabelStyle);
                    EditorGUILayout.LabelField("Delta Time: channel console first; scenarios below; selected channel details secondary.", UtilityWindowTheme.MutedMiniLabelStyle);
                });

                PungentEnvironmentSimulationEditorGUI.Section("Open QA Set", UtilityWindowTheme.Green, () =>
                {
                    if (GUILayout.Button(new GUIContent("Open All at Medium", "Open all four windows in a medium floating QA arrangement.")))
                        OpenAll(LayoutSize.Medium);
                    if (GUILayout.Button(new GUIContent("Open All at Narrow", "Open all four windows at a narrow size for single-scroll QA.")))
                        OpenAll(LayoutSize.Narrow);
                    if (GUILayout.Button(new GUIContent("Open All at Wide", "Open all four windows at a wide size for split-panel QA.")))
                        OpenAll(LayoutSize.Wide);
                });

                PungentEnvironmentSimulationEditorGUI.Section("Focused Sizes", UtilityWindowTheme.Purple, () =>
                {
                    DrawSizeRow("Narrow", LayoutSize.Narrow);
                    DrawSizeRow("Medium", LayoutSize.Medium);
                    DrawSizeRow("Wide", LayoutSize.Wide);
                    DrawSizeRow("Short", LayoutSize.Short);
                    DrawSizeRow("Tall", LayoutSize.Tall);
                });

                PungentEnvironmentSimulationEditorGUI.Section("Manual Docking Checklist", UtilityWindowTheme.Amber, () =>
                {
                    EditorGUILayout.HelpBox("After opening the QA set, dock each window and resize it through narrow, medium, wide, short, and tall states. The Layout QA strip in each tool should report the expected mode and workflow.", MessageType.Info);
                    EditorGUILayout.LabelField("Check: no clipped command strips, field labels, tabs, month cards, preset cards, forecast cards, channel rows, or detail panels.", UtilityWindowTheme.MutedMiniLabelStyle);
                    EditorGUILayout.LabelField("Check: only the content-owning region scrolls for dense lists, and the primary workspace remains visually dominant.", UtilityWindowTheme.MutedMiniLabelStyle);
                });
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }
        }

        private static void DrawSizeRow(string label, LayoutSize size)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, UtilityWindowTheme.CardLabelStyle, GUILayout.Width(72f));
                if (GUILayout.Button(new GUIContent("Hub", "Open Environment Simulation at this QA size.")))
                    SetWindow<PungentEnvironmentSimulationWindow>("Environment Simulation", RectFor(size, 0));
                if (GUILayout.Button(new GUIContent("Calendar", "Open Calendar Clock at this QA size.")))
                    SetWindow<PungentCalendarClockWindow>("Calendar Clock", RectFor(size, 1));
                if (GUILayout.Button(new GUIContent("Weather", "Open Weather Utility at this QA size.")))
                    SetWindow<PungentWeatherUtilityWindow>("Weather Utility", RectFor(size, 2));
                if (GUILayout.Button(new GUIContent("Delta", "Open Delta Time at this QA size.")))
                    SetWindow<PungentDeltaTimeUtilityWindow>("Delta Time", RectFor(size, 3));
            }
        }

        private static void OpenAll(LayoutSize size)
        {
            SetWindow<PungentEnvironmentSimulationWindow>("Environment Simulation", RectFor(size, 0));
            SetWindow<PungentCalendarClockWindow>("Calendar Clock", RectFor(size, 1));
            SetWindow<PungentWeatherUtilityWindow>("Weather Utility", RectFor(size, 2));
            SetWindow<PungentDeltaTimeUtilityWindow>("Delta Time", RectFor(size, 3));
        }

        private static void SetWindow<T>(string title, Rect rect) where T : EditorWindow
        {
            T window = GetWindow<T>(title);
            window.minSize = new Vector2(420f, 320f);
            window.position = rect;
            window.Show();
            window.Repaint();
        }

        private static Rect RectFor(LayoutSize size, int index)
        {
            Vector2 dimensions;
            switch (size)
            {
                case LayoutSize.Narrow: dimensions = new Vector2(460f, 720f); break;
                case LayoutSize.Wide: dimensions = new Vector2(1320f, 760f); break;
                case LayoutSize.Short: dimensions = new Vector2(960f, 440f); break;
                case LayoutSize.Tall: dimensions = new Vector2(920f, 900f); break;
                default: dimensions = new Vector2(920f, 680f); break;
            }

            float x = 80f + index * 36f;
            float y = 80f + index * 36f;
            return new Rect(x, y, dimensions.x, dimensions.y);
        }

        private enum LayoutSize
        {
            Narrow,
            Medium,
            Wide,
            Short,
            Tall
        }
    }
#endif
}
