using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.EnvironmentSimulation;

namespace PungentFunk.Utilities.Editor.EnvironmentSimulation
{
#if UNITY_EDITOR
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(PungentClockController))]
    public sealed class PungentClockControllerEditor : Editor
    {
        private float _hour;
        private bool _showEvents;
        private bool _showAdvanced;

        public override void OnInspectorGUI()
        {
            UtilityWindowTheme.EnsureStyles();
            PungentClockController clock = (PungentClockController)target;

            DrawClockState(clock);
            DrawClockSetup();
            DrawClockProfiles();
            DrawClockOutputs();
            DrawClockActions(clock);
            DrawClockOptional();
        }

        private void DrawClockState(PungentClockController clock)
        {
            PungentEnvironmentSimulationEditorGUI.Section("Current State", UtilityWindowTheme.Cyan, () =>
            {
                PungentClockSnapshot snapshot = clock.Snapshot;
                EditorGUILayout.LabelField(snapshot.TimeLabel24, UtilityWindowTheme.CardLabelStyle);
                EditorGUILayout.LabelField(snapshot.DateLabel + " | " + snapshot.seasonName, UtilityWindowTheme.MutedMiniLabelStyle);
            });
        }

        private void DrawClockSetup()
        {
            PungentEnvironmentSimulationEditorGUI.Section("Core Setup", UtilityWindowTheme.Blue, () =>
            {
                PungentEnvironmentSimulationEditorGUI.DrawProperties(serializedObject,
                    "startYear",
                    "startMonthIndex",
                    "startDayOfMonth",
                    "startHour",
                    "runOnStart",
                    "secondsPerInGameMinute",
                    "useDeltaTimeController",
                    "deltaTimeController");
            });
        }

        private void DrawClockProfiles()
        {
            PungentEnvironmentSimulationEditorGUI.Section("Profiles", UtilityWindowTheme.Purple, () =>
            {
                PungentEnvironmentSimulationEditorGUI.DrawObjectPropertyWithCreate<PungentEnvironmentCalendarProfile>(serializedObject, "calendarProfile", "Calendar Profile", "Pungent Calendar Profile");
                PungentEnvironmentSimulationEditorGUI.DrawObjectPropertyWithCreate<PungentClockSkyProfile>(serializedObject, "skyProfile", "Sky Profile", "Pungent Clock Sky Profile");
            });
        }

        private void DrawClockOutputs()
        {
            PungentEnvironmentSimulationEditorGUI.Section("Outputs", UtilityWindowTheme.Teal, () =>
            {
                PungentEnvironmentSimulationEditorGUI.DrawProperties(serializedObject, "sunTransform", "moonTransform", "seasonalTiltTransform", "sunLight", "moonLight");
            });
        }

        private void DrawClockActions(PungentClockController clock)
        {
            PungentEnvironmentSimulationEditorGUI.Section("Preview Actions", UtilityWindowTheme.Green, () =>
            {
                _hour = EditorGUILayout.Slider(new GUIContent("Hour", "Set the current hour for previewing lighting, weather, and time-sequence events."), _hour <= 0f ? clock.Snapshot.hour24 : _hour, 0f, 23.99f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Set Hour", "Set this clock to the chosen hour.")))
                    {
                        Undo.RecordObject(clock, "Set Pungent Clock Hour");
                        clock.SetHour(_hour);
                        EditorUtility.SetDirty(clock);
                    }
                    if (GUILayout.Button(new GUIContent("Advance Day", "Advance the calendar one day.")))
                    {
                        Undo.RecordObject(clock, "Advance Pungent Clock Day");
                        clock.AdvanceDays(1);
                        EditorUtility.SetDirty(clock);
                    }
                    if (GUILayout.Button(new GUIContent("Open Utility", "Open the focused Calendar Clock utility.")))
                        PungentCalendarClockWindow.Open();
                    if (GUILayout.Button(new GUIContent("Hub", "Open the Environment Simulation hub."), GUILayout.Width(52f)))
                        PungentEnvironmentSimulationWindow.Open();
                }
            });
        }

        private void DrawClockOptional()
        {
            _showEvents = EditorGUILayout.Foldout(_showEvents, "Events", true);
            if (_showEvents)
            {
                PungentEnvironmentSimulationEditorGUI.Section("Events", UtilityWindowTheme.Neutral, () =>
                {
                    PungentEnvironmentSimulationEditorGUI.DrawProperties(serializedObject, "onTimeChanged", "onHourChanged", "onDayChanged", "onMonthChanged", "onSeasonChanged", "onYearChanged");
                });
            }

            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Advanced", true);
            if (_showAdvanced)
            {
                PungentEnvironmentSimulationEditorGUI.Section("Advanced", UtilityWindowTheme.Neutral, () =>
                {
                    PungentEnvironmentSimulationEditorGUI.DrawProperties(serializedObject, "fireMissedSequenceEventsWhenSkipping");
                });
            }
        }
    }

    [CustomEditor(typeof(PungentWeatherController))]
    public sealed class PungentWeatherControllerEditor : Editor
    {
        private int _presetIndex;
        private bool _showForecast;
        private bool _showEvents;
        private bool _showAdvanced;

        public override void OnInspectorGUI()
        {
            UtilityWindowTheme.EnsureStyles();
            PungentWeatherController weather = (PungentWeatherController)target;

            DrawWeatherState(weather);
            DrawWeatherSetup();
            DrawWeatherProfile();
            DrawWeatherOutput(weather);
            DrawWeatherActions(weather);
            DrawWeatherOptional();
        }

        private void DrawWeatherState(PungentWeatherController weather)
        {
            PungentEnvironmentSimulationEditorGUI.Section("Current Weather", UtilityWindowTheme.Teal, () =>
            {
                PungentWeatherState state = weather.CurrentState;
                EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(state.displayName) ? "No active weather" : state.displayName, UtilityWindowTheme.CardLabelStyle);
                EditorGUILayout.LabelField("Temp " + state.temperature.ToString("0") + " C | Rain " + state.precipitationChance.ToString("0") + "% | Wind " + state.windSpeed.ToString("0.00") + " | Fog " + state.fogStrength.ToString("0.00"), UtilityWindowTheme.MutedMiniLabelStyle);
            });
        }

        private void DrawWeatherSetup()
        {
            PungentEnvironmentSimulationEditorGUI.Section("Core Setup", UtilityWindowTheme.Blue, () =>
            {
                PungentEnvironmentSimulationEditorGUI.DrawProperties(serializedObject,
                    "clock",
                    "regenerateForecastOnDayChange",
                    "useDeltaTimeController",
                    "deltaTimeController",
                    "interpolationSpeed");
            });
        }

        private void DrawWeatherProfile()
        {
            PungentEnvironmentSimulationEditorGUI.Section("Profiles", UtilityWindowTheme.Purple, () =>
            {
                PungentEnvironmentSimulationEditorGUI.DrawObjectPropertyWithCreate<PungentWeatherProfile>(serializedObject, "weatherProfile", "Weather Profile", "Pungent Weather Profile");
            });
        }

        private void DrawWeatherOutput(PungentWeatherController weather)
        {
            PungentEnvironmentSimulationEditorGUI.Section("Outputs", UtilityWindowTheme.Cyan, () =>
            {
                PungentEnvironmentSimulationEditorGUI.DrawProperties(serializedObject, "outputApplier");
                if (PungentEnvironmentSimulationEditorGUI.ObjectValue(weather, "outputApplier") == null)
                {
                    if (GUILayout.Button(new GUIContent("Add Output Applier", "Add and assign the optional weather output applier component."), GUILayout.Width(150f)))
                    {
                        Undo.RecordObject(weather, "Assign Weather Output Applier");
                        PungentWeatherOutputApplier applier = weather.GetComponent<PungentWeatherOutputApplier>();
                        if (applier == null)
                            applier = Undo.AddComponent<PungentWeatherOutputApplier>(weather.gameObject);
                        SerializedObject serializedWeather = new SerializedObject(weather);
                        SerializedProperty property = serializedWeather.FindProperty("outputApplier");
                        property.objectReferenceValue = applier;
                        serializedWeather.ApplyModifiedProperties();
                        EditorUtility.SetDirty(weather);
                    }
                }
            });
        }

        private void DrawWeatherActions(PungentWeatherController weather)
        {
            PungentEnvironmentSimulationEditorGUI.Section("Preview Actions", UtilityWindowTheme.Green, () =>
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _presetIndex = EditorGUILayout.IntField(new GUIContent("Preset", "Weather profile preset index to preview."), _presetIndex);
                    if (GUILayout.Button(new GUIContent("Preview", "Apply selected preset to current weather output."), GUILayout.Width(82f)))
                    {
                        Undo.RecordObject(weather, "Preview Pungent Weather");
                        weather.PreviewPreset(_presetIndex);
                        EditorUtility.SetDirty(weather);
                    }
                    if (GUILayout.Button(new GUIContent("Forecast", "Regenerate the 24-hour forecast."), GUILayout.Width(82f)))
                    {
                        Undo.RecordObject(weather, "Regenerate Pungent Forecast");
                        weather.GenerateDailyForecast();
                        EditorUtility.SetDirty(weather);
                    }
                    if (GUILayout.Button(new GUIContent("Open Utility", "Open the focused Weather utility."), GUILayout.Width(96f)))
                        PungentWeatherUtilityWindow.Open();
                    if (GUILayout.Button(new GUIContent("Hub", "Open the Environment Simulation hub."), GUILayout.Width(52f)))
                        PungentEnvironmentSimulationWindow.Open();
                }
            });
        }

        private void DrawWeatherOptional()
        {
            _showForecast = EditorGUILayout.Foldout(_showForecast, "Forecast Entries", true);
            if (_showForecast)
            {
                PungentEnvironmentSimulationEditorGUI.Section("Forecast Entries", UtilityWindowTheme.Amber, () =>
                {
                    PungentEnvironmentSimulationEditorGUI.DrawProperties(serializedObject, "forecast");
                });
            }

            _showEvents = EditorGUILayout.Foldout(_showEvents, "Events", true);
            if (_showEvents)
            {
                PungentEnvironmentSimulationEditorGUI.Section("Events", UtilityWindowTheme.Neutral, () =>
                {
                    PungentEnvironmentSimulationEditorGUI.DrawProperties(serializedObject, "onWeatherChanged", "onForecastRegenerated");
                });
            }

            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Advanced Runtime State", true);
            if (_showAdvanced)
            {
                PungentEnvironmentSimulationEditorGUI.Section("Advanced Runtime State", UtilityWindowTheme.Neutral, () =>
                {
                    PungentEnvironmentSimulationEditorGUI.DrawProperties(serializedObject, "currentState");
                });
            }
        }
    }

    [CustomEditor(typeof(PungentDeltaTimeController))]
    public sealed class PungentDeltaTimeControllerEditor : Editor
    {
        private float _gameplayScale = 0.25f;
        private bool _showEvents;
        private bool _showAdvanced;

        public override void OnInspectorGUI()
        {
            UtilityWindowTheme.EnsureStyles();
            PungentDeltaTimeController delta = (PungentDeltaTimeController)target;

            DrawDeltaState(delta);
            DrawDeltaSetup();
            DrawDeltaProfile();
            DrawDeltaActions(delta);
            DrawDeltaOptional();
        }

        private void DrawDeltaState(PungentDeltaTimeController delta)
        {
            PungentEnvironmentSimulationEditorGUI.Section("Current State", UtilityWindowTheme.Amber, () =>
            {
                EditorGUILayout.LabelField("Global " + delta.GetScale(PungentTimeChannel.Global).ToString("0.00") + " | Gameplay " + delta.GetScale(PungentTimeChannel.Gameplay).ToString("0.00") + " | Pause stack " + delta.GetPauseStack(PungentTimeChannel.Global), UtilityWindowTheme.CardLabelStyle);
                EditorGUILayout.LabelField("Clock " + delta.GetScale(PungentTimeChannel.Clock).ToString("0.00") + " | Weather " + delta.GetScale(PungentTimeChannel.Weather).ToString("0.00"), UtilityWindowTheme.MutedMiniLabelStyle);
            });
        }

        private void DrawDeltaSetup()
        {
            PungentEnvironmentSimulationEditorGUI.Section("Core Setup", UtilityWindowTheme.Blue, () =>
            {
                PungentEnvironmentSimulationEditorGUI.DrawProperties(serializedObject, "claimSingleton", "applyUnityTimeScale", "fixedDeltaTimeBase");
            });
        }

        private void DrawDeltaProfile()
        {
            PungentEnvironmentSimulationEditorGUI.Section("Profiles", UtilityWindowTheme.Purple, () =>
            {
                PungentEnvironmentSimulationEditorGUI.DrawObjectPropertyWithCreate<PungentDeltaTimeProfile>(serializedObject, "profile", "Delta Time Profile", "Pungent Delta Time Profile");
            });
        }

        private void DrawDeltaActions(PungentDeltaTimeController delta)
        {
            PungentEnvironmentSimulationEditorGUI.Section("Time Control", UtilityWindowTheme.Green, () =>
            {
                _gameplayScale = EditorGUILayout.Slider(new GUIContent("Gameplay Scale", "Set the Gameplay channel scale for slow motion or speed-up."), _gameplayScale, 0f, 4f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Pause", "Push a Global pause request.")))
                        delta.PushPause(PungentTimeChannel.Global);
                    if (GUILayout.Button(new GUIContent("Resume", "Pop one Global pause request.")))
                        delta.PopPause(PungentTimeChannel.Global);
                    if (GUILayout.Button(new GUIContent("Apply Scale", "Set Gameplay scale.")))
                        delta.SetScale(PungentTimeChannel.Gameplay, _gameplayScale);
                    if (GUILayout.Button(new GUIContent("Restore", "Clear pauses and restore profile defaults.")))
                        delta.ResetAll();
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Open Utility", "Open the focused Delta Time utility.")))
                        PungentDeltaTimeUtilityWindow.Open();
                    if (GUILayout.Button(new GUIContent("Hub", "Open the Environment Simulation hub."), GUILayout.Width(52f)))
                        PungentEnvironmentSimulationWindow.Open();
                }
            });
        }

        private void DrawDeltaOptional()
        {
            _showEvents = EditorGUILayout.Foldout(_showEvents, "Events", true);
            if (_showEvents)
            {
                PungentEnvironmentSimulationEditorGUI.Section("Events", UtilityWindowTheme.Neutral, () =>
                {
                    PungentEnvironmentSimulationEditorGUI.DrawProperties(serializedObject, "onChannelScaleChanged", "onPauseStackChanged");
                });
            }

            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Advanced", true);
            if (_showAdvanced)
            {
                PungentEnvironmentSimulationEditorGUI.Section("Advanced", UtilityWindowTheme.Neutral, () =>
                {
                    UnityEngine.Object profile = PungentEnvironmentSimulationEditorGUI.ObjectValue(target, "profile");
                    if (profile == null)
                    {
                        EditorGUILayout.LabelField("Assign a Delta Time Profile to edit channel definitions.", UtilityWindowTheme.MutedMiniLabelStyle);
                        return;
                    }

                    SerializedObject serializedProfile = new SerializedObject(profile);
                    PungentEnvironmentSimulationEditorGUI.DrawProperties(serializedProfile, "channels");
                });
            }
        }
    }

    [CustomEditor(typeof(PungentEnvironmentCalendarProfile))]
    public sealed class PungentEnvironmentCalendarProfileEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            UtilityWindowTheme.EnsureStyles();
            PungentEnvironmentCalendarProfile profile = (PungentEnvironmentCalendarProfile)target;
            PungentEnvironmentSimulationEditorGUI.Section("Calendar", UtilityWindowTheme.Cyan, () =>
            {
                EditorGUILayout.LabelField("Months: " + profile.MonthCount + " | Days in year: " + profile.DaysInYear, UtilityWindowTheme.CardLabelStyle);
                PungentEnvironmentSimulationEditorGUI.DrawProperties(serializedObject, "firstDayOfYear", "months");
            });
        }
    }

    [CustomEditor(typeof(PungentWeatherProfile))]
    public sealed class PungentWeatherProfileEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            UtilityWindowTheme.EnsureStyles();
            PungentWeatherProfile profile = (PungentWeatherProfile)target;
            int count = profile.Presets == null ? 0 : profile.Presets.Length;
            PungentEnvironmentSimulationEditorGUI.Section("Weather Profile", UtilityWindowTheme.Teal, () =>
            {
                EditorGUILayout.LabelField("Weather presets: " + count, UtilityWindowTheme.CardLabelStyle);
                PungentEnvironmentSimulationEditorGUI.DrawProperties(serializedObject, "seed", "seasonRules", "presets");
            });
        }
    }

    [CustomEditor(typeof(PungentWeatherVolumeAdapter))]
    public sealed class PungentWeatherVolumeAdapterEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            UtilityWindowTheme.EnsureStyles();
            PungentEnvironmentSimulationEditorGUI.Section("Weather Volume Adapter", UtilityWindowTheme.Teal, () =>
            {
                EditorGUILayout.LabelField("Maps weather metrics into Volume-style profile parameters by component and parameter name.", UtilityWindowTheme.MutedMiniLabelStyle);
                PungentEnvironmentSimulationEditorGUI.DrawProperties(serializedObject, "weather", "volume", "applyContinuously", "floatBindings");
            });

            PungentEnvironmentSimulationEditorGUI.Section("Open Tools", UtilityWindowTheme.Neutral, () =>
            {
                if (GUILayout.Button(new GUIContent("Open Weather Utility", "Open the focused Weather utility.")))
                    PungentWeatherUtilityWindow.Open();
                if (GUILayout.Button(new GUIContent("Open Environment Simulation Hub", "Open the Environment Simulation workbench.")))
                    PungentEnvironmentSimulationWindow.Open();
            });
        }
    }

    [CustomEditor(typeof(PungentTimelineClockBridge))]
    public sealed class PungentTimelineClockBridgeEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            UtilityWindowTheme.EnsureStyles();
            PungentEnvironmentSimulationEditorGUI.Section("Timeline Clock Bridge", UtilityWindowTheme.Cyan, () =>
            {
                EditorGUILayout.LabelField("Maps PlayableDirector progress to the clock hour for Timeline-style previews.", UtilityWindowTheme.MutedMiniLabelStyle);
                PungentEnvironmentSimulationEditorGUI.DrawProperties(serializedObject, "director", "clock", "driveInEditMode", "onlyWhileDirectorPlaying", "startHour", "endHour");
            });

            PungentTimelineClockBridge bridge = (PungentTimelineClockBridge)target;
            PungentEnvironmentSimulationEditorGUI.Section("Preview", UtilityWindowTheme.Green, () =>
            {
                if (GUILayout.Button(new GUIContent("Apply Director Time", "Apply the current PlayableDirector time to the clock once.")))
                    bridge.ApplyDirectorTime();
                if (GUILayout.Button(new GUIContent("Open Calendar Clock", "Open the focused Calendar Clock utility.")))
                    PungentCalendarClockWindow.Open();
            });
        }
    }

    [CustomEditor(typeof(PungentDeltaTimePlayableDirectorAdapter))]
    public sealed class PungentDeltaTimePlayableDirectorAdapterEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            UtilityWindowTheme.EnsureStyles();
            PungentEnvironmentSimulationEditorGUI.Section("Delta Time PlayableDirector Adapter", UtilityWindowTheme.Amber, () =>
            {
                EditorGUILayout.LabelField("Applies a delta-time channel scale to a PlayableDirector graph.", UtilityWindowTheme.MutedMiniLabelStyle);
                PungentEnvironmentSimulationEditorGUI.DrawProperties(serializedObject, "deltaTime", "director", "channel", "customChannel", "setRootPlayableSpeed", "pauseDirectorWhenScaleIsZero", "resumeDirectorWhenScaleRestored");
            });

            PungentDeltaTimePlayableDirectorAdapter adapter = (PungentDeltaTimePlayableDirectorAdapter)target;
            PungentEnvironmentSimulationEditorGUI.Section("Preview", UtilityWindowTheme.Green, () =>
            {
                if (GUILayout.Button(new GUIContent("Apply Scale Now", "Apply the selected delta-time channel scale once.")))
                    adapter.Apply();
                if (GUILayout.Button(new GUIContent("Open Delta Time", "Open the focused Delta Time utility.")))
                    PungentDeltaTimeUtilityWindow.Open();
            });
        }
    }

    [CustomEditor(typeof(PungentWeatherWindAdapterBase), true)]
    public sealed class PungentWeatherWindAdapterEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            UtilityWindowTheme.EnsureStyles();
            PungentEnvironmentSimulationEditorGUI.Section("Weather Wind Adapter", UtilityWindowTheme.Amber, () =>
            {
                EditorGUILayout.LabelField("Generic PFU weather wind routing. This adapter does not reference project-specific wind controllers.", UtilityWindowTheme.MutedMiniLabelStyle);
                serializedObject.Update();
                DrawKnownProperty("weather");
                DrawKnownProperty("applyContinuously");
                DrawKnownProperty("speedMultiplier");
                DrawKnownProperty("gustMultiplier");
                DrawRemainingProperties();
                serializedObject.ApplyModifiedProperties();
            });

            PungentEnvironmentSimulationEditorGUI.Section("Open Tools", UtilityWindowTheme.Neutral, () =>
            {
                if (GUILayout.Button(new GUIContent("Open Weather Utility", "Open the focused Weather utility.")))
                    PungentWeatherUtilityWindow.Open();
                if (GUILayout.Button(new GUIContent("Open Environment Simulation Hub", "Open the Environment Simulation workbench.")))
                    PungentEnvironmentSimulationWindow.Open();
            });
        }

        private void DrawKnownProperty(string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                EditorGUILayout.PropertyField(property, true);
        }

        private void DrawRemainingProperties()
        {
            SerializedProperty property = serializedObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.propertyPath == "m_Script" ||
                    property.propertyPath == "weather" ||
                    property.propertyPath == "applyContinuously" ||
                    property.propertyPath == "speedMultiplier" ||
                    property.propertyPath == "gustMultiplier")
                    continue;

                EditorGUILayout.PropertyField(property, true);
            }
        }
    }

    [CustomEditor(typeof(PungentDeltaTimeAdapterBase), true)]
    public sealed class PungentDeltaTimeAdapterEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            UtilityWindowTheme.EnsureStyles();
            PungentEnvironmentSimulationEditorGUI.Section("Delta Time Adapter", UtilityWindowTheme.Amber, () =>
            {
                EditorGUILayout.LabelField("Applies a selected delta-time channel to a Unity system that consumes explicit scale.", UtilityWindowTheme.MutedMiniLabelStyle);
                serializedObject.Update();
                DrawKnownProperty("deltaTime");
                DrawKnownProperty("channel");
                DrawKnownProperty("customChannel");
                DrawKnownProperty("applyContinuously");
                DrawRemainingProperties();
                serializedObject.ApplyModifiedProperties();
            });

            PungentEnvironmentSimulationEditorGUI.Section("Open Tools", UtilityWindowTheme.Neutral, () =>
            {
                if (GUILayout.Button(new GUIContent("Open Delta Time", "Open the focused Delta Time utility.")))
                    PungentDeltaTimeUtilityWindow.Open();
                if (GUILayout.Button(new GUIContent("Open Environment Simulation Hub", "Open the Environment Simulation workbench.")))
                    PungentEnvironmentSimulationWindow.Open();
            });
        }

        private void DrawKnownProperty(string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                EditorGUILayout.PropertyField(property, true);
        }

        private void DrawRemainingProperties()
        {
            SerializedProperty property = serializedObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.propertyPath == "m_Script" ||
                    property.propertyPath == "deltaTime" ||
                    property.propertyPath == "channel" ||
                    property.propertyPath == "customChannel" ||
                    property.propertyPath == "applyContinuously")
                    continue;

                EditorGUILayout.PropertyField(property, true);
            }
        }
    }
#endif
}
