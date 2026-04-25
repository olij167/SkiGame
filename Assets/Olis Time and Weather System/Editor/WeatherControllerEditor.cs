using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TimeWeather
{
    [CustomEditor(typeof(WeatherController))]
    public class WeatherControllerEditor : Editor
    {
        private readonly Dictionary<string, bool> _foldoutStates = new Dictionary<string, bool>();
        private SerializedProperty _weatherDataPresets;
        private WeatherController _controller;

        private void OnEnable()
        {
            _controller = (WeatherController)target;
            _weatherDataPresets = serializedObject.FindProperty("weatherDataPresets");
            _controller.EnsureWeatherPresetAuthoringData();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            _controller.EnsureWeatherPresetAuthoringData();
            serializedObject.UpdateIfRequiredOrScript();

            DrawPropertiesExcluding(serializedObject, "m_Script", "weatherDataPresets");

            EditorGUILayout.Space(8f);
            DrawPresetLibrary();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawPresetLibrary()
        {
            EditorGUILayout.LabelField("Weather Presets", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Simple Authoring describes visible scene outcomes. The cloud controls resolve as one response across skybox clouds, scene cloud planes, terrain shadows, and volumetric clouds. Enable Advanced Overrides only when you need shader-facing control.",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            int newSize = Mathf.Max(0, EditorGUILayout.IntField("Preset Count", _weatherDataPresets.arraySize));
            if (newSize != _weatherDataPresets.arraySize)
            {
                _weatherDataPresets.arraySize = newSize;
            }

            if (GUILayout.Button("Add Preset", GUILayout.Width(110f)))
            {
                int index = _weatherDataPresets.arraySize;
                _weatherDataPresets.arraySize++;
                SerializedProperty newPreset = _weatherDataPresets.GetArrayElementAtIndex(index);
                newPreset.FindPropertyRelative("weatherCondition").stringValue = "New Weather";
                newPreset.FindPropertyRelative("weatherGlyph").stringValue = "◌";
                newPreset.FindPropertyRelative("overallWeatherTint").colorValue = Color.white;
            }

            if (GUILayout.Button("Open Severity Tuner", GUILayout.Width(150f)))
            {
                WeatherPresetTuningWindow.Open(_controller);
            }
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < _weatherDataPresets.arraySize; i++)
            {
                DrawPreset(_weatherDataPresets.GetArrayElementAtIndex(i), i);
                EditorGUILayout.Space(6f);
            }
        }

        private void DrawPreset(SerializedProperty presetProp, int index)
        {
            SerializedProperty weatherCondition = presetProp.FindPropertyRelative("weatherCondition");
            SerializedProperty weatherGlyph = presetProp.FindPropertyRelative("weatherGlyph");
            SerializedProperty useAdvancedOverrides = presetProp.FindPropertyRelative("useAdvancedOverrides");

            string presetKey = presetProp.propertyPath;
            bool expanded = GetState(presetKey, false);
            string title = string.IsNullOrWhiteSpace(weatherCondition.stringValue)
                ? $"Preset {index + 1}"
                : $"{weatherGlyph.stringValue} {weatherCondition.stringValue}";

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            expanded = EditorGUILayout.Foldout(expanded, title, true);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(useAdvancedOverrides.boolValue ? "Advanced" : "Simple", GUILayout.Width(70f));

            if (GUILayout.Button("Preview", GUILayout.Width(65f)))
            {
                serializedObject.ApplyModifiedProperties();
                _controller.ApplyWeatherPresetPreviewInEditor(index);
                serializedObject.Update();
            }

            if (GUILayout.Button("Duplicate", GUILayout.Width(75f)))
            {
                serializedObject.ApplyModifiedProperties();
                _weatherDataPresets.InsertArrayElementAtIndex(index);
                serializedObject.Update();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }

            if (GUILayout.Button("Remove", GUILayout.Width(70f)))
            {
                _weatherDataPresets.DeleteArrayElementAtIndex(index);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUILayout.EndHorizontal();
            SetState(presetKey, expanded);

            if (expanded)
            {
                DrawSection("Core Weather", presetKey + "/core", () =>
                {
                    EditorGUILayout.PropertyField(weatherCondition);
                    EditorGUILayout.PropertyField(weatherGlyph);
                    EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("tempRange"));
                    EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("rainRange"));
                    EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("isRaining"));
                    EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("activeClouds"), true);
                    EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("wetness"));
                    EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("snowiness"));
                    EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("weatherParticles"), true);
                    EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("clips"), true);
                    EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("pitch"));
                    EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("volume"));
                });

                DrawSimpleAuthoring(presetProp, index);

                EditorGUILayout.Space(4f);
                EditorGUILayout.PropertyField(useAdvancedOverrides, new GUIContent("Use Advanced Overrides"));

                bool showAdvanced = GetState(presetKey + "/advanced", false);
                showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Advanced Overrides", true);
                SetState(presetKey + "/advanced", showAdvanced);

                if (showAdvanced)
                {
                    EditorGUI.indentLevel++;
                    EditorGUI.BeginDisabledGroup(!useAdvancedOverrides.boolValue);

                    DrawSection("Wind", presetKey + "/wind", () =>
                    {
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("windSpeedRange"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("windDirectionRange"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("gustStrengthRange"));
                    });

                    DrawSection("Skybox Clouds", presetKey + "/sky", () =>
                    {
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("cloudPowerRange"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("cloudAlphaRange"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("skyCloudAlphaRange"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("skyCloudPowerRange"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("cloudSoftness"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("cloudCoverageBias"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("cloudTurbulence"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("cloudWarpStrength"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("cloudTint"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("cloudShadowStrength"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("cloudGreyStrength"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("cloudSunLightStrength"));
                    });

                    DrawSection("Scene Cloud Layer", presetKey + "/scene", () =>
                    {
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("sceneCloudAlphaRange"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("sceneCloudPowerRange"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("sceneCloudSoftness"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("sceneCloudCoverageBias"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("sceneCloudTurbulence"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("sceneCloudWarpStrength"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("sceneCloudTint"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("sceneCloudShadowTint"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("planeEdgeFade"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("planeRadialFade"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("planeHeightFade"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("sceneCloudFresnelFade"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("sceneCloudMacroRoundness"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("sceneCloudDetailStrength"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("sceneCloudDensityContrast"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("sceneCloudErosionStrength"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("sceneCloudSecondLayerStrength"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("sceneCloudSilverLiningStrength"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("sceneCloudBottomDarkening"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("sceneCloudDriftMultiplier"));
                    });

                    DrawSection("Terrain Shadows", presetKey + "/terrain", () =>
                    {
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("terrainCloudShadowStrength"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("terrainCloudShadowScale"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("terrainCloudShadowSoftness"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("terrainCloudShadowBias"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("terrainCloudShadowDriftMultiplier"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("terrainCloudShadowTint"));
                    });

                    DrawSection("Volumetric Clouds", presetKey + "/volume", () =>
                    {
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("volumeDensity"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("volumeAbsorption"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("volumeLightingStrength"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("volumeAlphaMultiplier"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("volumeClusterScale"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("volumeClusterDensity"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("volumeDetailStrength"));
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("volumeEdgeFade"));
                    });

                    DrawSection("Fog / Skybox Interaction", presetKey + "/fog", () =>
                    {
                        EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("fogStrength"));
                        EditorGUILayout.HelpBox(
                            "Sky fog visual response thresholds stay on the main WeatherController and are applied to the resolved fog strength from this preset.",
                            MessageType.None);
                    });

                    EditorGUI.EndDisabledGroup();
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSimpleAuthoring(SerializedProperty presetProp, int index)
        {
            DrawSection("Simple Authoring", presetProp.propertyPath + "/simple", () =>
            {
                EditorGUILayout.HelpBox(
                    "Tune visible scene outcomes first. These controls resolve into the detailed skybox, cloud plane, terrain-shadow, volumetric-cloud, fog, and sky-atmosphere values below.",
                    MessageType.None);

                EditorGUILayout.LabelField("Cloud Read", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("simpleCloudCoverage"), new GUIContent("Cloud Amount", "How much of the sky and scene should read as cloudy."));
                EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("simpleCloudHeaviness"), new GUIContent("Cloud Density", "How solid, dark, shadow-casting, and volumetric the cloud mass should feel."));
                EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("simpleCloudSoftness"), new GUIContent("Cloud Edge Softness", "How soft and feathered the cloud edges should look."));

                EditorGUILayout.Space(3f);
                EditorGUILayout.LabelField("Atmosphere & Visibility", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("simpleFogginess"), new GUIContent("Visibility Fog", "How much distance visibility is reduced. Also drives the resolved sky fog blend so the sky and horizon fade with the scene."));
                EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("overallWeatherTint"), new GUIContent("Atmosphere Tint", "Overall colour cast applied to the cloud, fog-facing sky response, and cloud-shadow tints."));
                EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("simpleGloom"), new GUIContent("Sunlight Blocked", "How much the preset dims sunlight, greys the sky/clouds, and darkens cloud undersides."));

                EditorGUILayout.Space(3f);
                EditorGUILayout.LabelField("Motion & Weather Energy", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("simpleStorminess"), new GUIContent("Weather Intensity", "How turbulent, gusty, and storm-shaped the weather response should be."));
                EditorGUILayout.PropertyField(presetProp.FindPropertyRelative("simpleWindiness"), new GUIContent("Wind Motion", "How strongly clouds and gust-driven weather should move."));

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Seed From Archetype", EditorStyles.miniBoldLabel);
                EditorGUILayout.BeginHorizontal();
                DrawSeedButton(index, "Clear", WeatherPresetSeedType.Clear);
                DrawSeedButton(index, "Partly Cloudy", WeatherPresetSeedType.PartlyCloudy);
                DrawSeedButton(index, "Overcast", WeatherPresetSeedType.Overcast);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                DrawSeedButton(index, "Rain", WeatherPresetSeedType.Rain);
                DrawSeedButton(index, "Snow", WeatherPresetSeedType.Snow);
                DrawSeedButton(index, "Blizzard", WeatherPresetSeedType.Blizzard);
                DrawSeedButton(index, "Storm", WeatherPresetSeedType.Storm);
                EditorGUILayout.EndHorizontal();

                WeatherController.WeatherData runtimePreset = GetRuntimePreset(index);
                if (runtimePreset != null)
                {
                    WeatherPresetAuthoringUtility.DerivedWeatherPreview preview =
                        WeatherPresetAuthoringUtility.BuildPreview(
                            runtimePreset,
                            _controller.skyFogVisualStart,
                            _controller.skyFogVisualEnd);

                    EditorGUILayout.Space(4f);
                    EditorGUILayout.LabelField("Derived Preview", EditorStyles.miniBoldLabel);
                    EditorGUILayout.HelpBox(
                        $"Clouds - sky {preview.skyCloudAlpha:0.00}/{preview.skyCloudPower:0.00}, scene {preview.sceneCloudAlpha:0.00}/{preview.sceneCloudPower:0.00}\n" +
                        $"Visibility fog {preview.fogStrength:0.00}, sky atmosphere blend {preview.skyFogInfluence:0.00}\n" +
                        $"Ground shadow {preview.terrainShadowStrength:0.00}, volumetric density {preview.volumetricDensity:0.00}, total shadow read {preview.shadowIntensity:0.00}",
                        MessageType.None);
                }
            });
        }

        private void DrawSeedButton(int index, string label, WeatherPresetSeedType seedType)
        {
            if (!GUILayout.Button(label))
                return;

            serializedObject.ApplyModifiedProperties();
            Undo.RecordObject(_controller, $"Seed {label} Weather Preset");
            WeatherController.WeatherData runtimePreset = GetRuntimePreset(index);
            WeatherPresetAuthoringUtility.ApplySeed(runtimePreset, seedType);
            EditorUtility.SetDirty(_controller);
            _controller.EnsureWeatherPresetAuthoringData();
            serializedObject.Update();
        }

        private WeatherController.WeatherData GetRuntimePreset(int index)
        {
            if (_controller == null || _controller.weatherDataPresets == null)
                return null;

            if (index < 0 || index >= _controller.weatherDataPresets.Length)
                return null;

            return _controller.weatherDataPresets[index];
        }

        private void DrawSection(string title, string key, System.Action drawer)
        {
            bool expanded = GetState(key, true);
            expanded = EditorGUILayout.Foldout(expanded, title, true);
            SetState(key, expanded);

            if (!expanded)
                return;

            EditorGUI.indentLevel++;
            drawer?.Invoke();
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(3f);
        }

        private bool GetState(string key, bool defaultValue)
        {
            if (_foldoutStates.TryGetValue(key, out bool value))
                return value;

            _foldoutStates[key] = defaultValue;
            return defaultValue;
        }

        private void SetState(string key, bool value)
        {
            _foldoutStates[key] = value;
        }
    }

    public class WeatherPresetTuningWindow : EditorWindow
    {
        private readonly List<int> _orderedPresetIndexes = new List<int>();
        private readonly Dictionary<int, bool> _selectedPresets = new Dictionary<int, bool>();
        private WeatherController _controller;
        private WeatherController _lastController;
        private Vector2 _listScroll;
        private Vector2 _editorScroll;
        private int _focusedPresetIndex = -1;
        private bool _autoApply = true;
        private bool _writeResolvedAdvancedVisuals = true;
        private bool _previewFocusedAfterApply = true;
        private AnimationCurve _severityCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        private readonly Color _focusedTint = new Color(0.78f, 0.90f, 1f, 1f);

        private float _cloudAmountLow = 0.05f;
        private float _cloudAmountHigh = 1f;
        private float _cloudThicknessLow = 0.05f;
        private float _cloudThicknessHigh = 1f;
        private float _cloudSoftnessLow = 0.45f;
        private float _cloudSoftnessHigh = 0.2f;
        private float _weatherIntensityLow = 0f;
        private float _weatherIntensityHigh = 1f;
        private float _distanceHazeLow = 0f;
        private float _distanceHazeHigh = 0.7f;
        private float _sunlightBlockedLow = 0f;
        private float _sunlightBlockedHigh = 0.9f;
        private float _windMotionLow = 0.15f;
        private float _windMotionHigh = 1f;
        private float _wetnessLow = 0f;
        private float _wetnessHigh = 4f;
        private float _snowinessLow = 0f;
        private float _snowinessHigh = 0f;
        private Color _tintLow = Color.white;
        private Color _tintHigh = new Color(0.85f, 0.92f, 1f, 1f);

        public static void Open(WeatherController controller)
        {
            WeatherPresetTuningWindow window = GetWindow<WeatherPresetTuningWindow>("Weather Severity Tuner");
            window.minSize = new Vector2(860f, 620f);
            window.SetController(controller);
            window.Show();
        }

        private void OnSelectionChange()
        {
            if (_controller == null && Selection.activeGameObject != null)
            {
                WeatherController selectedController = Selection.activeGameObject.GetComponent<WeatherController>();
                if (selectedController != null)
                    SetController(selectedController);
            }

            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("Weather Severity Tuner", EditorStyles.boldLabel, GUILayout.Width(190f));
            _controller = (WeatherController)EditorGUILayout.ObjectField(_controller, typeof(WeatherController), true, GUILayout.MinWidth(220f));
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"{GetSelectedCount()} active", EditorStyles.miniLabel, GUILayout.Width(70f));
            EditorGUILayout.EndHorizontal();

            if (_controller != _lastController)
            {
                _lastController = _controller;
                SyncPresetList(true);
                CaptureEndpointsFromSelection();
            }

            if (_controller == null)
            {
                EditorGUILayout.HelpBox("Assign a WeatherController to tune its presets.", MessageType.Info);
                return;
            }

            _controller.EnsureWeatherPresetAuthoringData();
            SyncPresetList();

            DrawWorkspace();
        }

        private void DrawWorkspace()
        {
            float topHeight = Mathf.Clamp(position.height * 0.38f, 240f, 330f);

            EditorGUILayout.BeginHorizontal(GUILayout.Height(topHeight));
            DrawRankedPresetList(topHeight);
            DrawBalancingControls();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4f);
            DrawInlinePresetEditor();
        }

        private void DrawRankedPresetList(float height)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(Mathf.Clamp(position.width * 0.34f, 315f, 430f)), GUILayout.Height(height));
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("1. Select & Rank", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("mild -> severe", EditorStyles.miniLabel, GUILayout.Width(85f));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("All", EditorStyles.miniButtonLeft, GUILayout.Width(42f)))
            {
                SetAllSelected(true);
                AutoApplyBalancing();
            }
            if (GUILayout.Button("None", EditorStyles.miniButtonMid, GUILayout.Width(52f)))
            {
                SetAllSelected(false);
                AutoApplyBalancing();
            }
            if (GUILayout.Button("Use Endpoints", EditorStyles.miniButtonRight, GUILayout.Width(105f)))
                CaptureEndpointsFromSelection();
            EditorGUILayout.EndHorizontal();

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            for (int order = 0; order < _orderedPresetIndexes.Count; order++)
            {
                DrawPresetRow(order);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawBalancingControls()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("2. Shape Shared Response", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("protected fields are skipped", EditorStyles.miniLabel, GUILayout.Width(145f));
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            _autoApply = GUILayout.Toggle(_autoApply, new GUIContent("Auto Apply", "Immediately rewrite active selected presets when balancing controls change."), EditorStyles.toolbarButton, GUILayout.Width(82f));
            _writeResolvedAdvancedVisuals = GUILayout.Toggle(_writeResolvedAdvancedVisuals, new GUIContent("Sync Advanced", "Also write resolved cloud/fog visual values into advanced visual fields so Advanced Overrides remain aligned. Protected metadata and references are still skipped."), EditorStyles.toolbarButton, GUILayout.Width(102f));
            _previewFocusedAfterApply = GUILayout.Toggle(_previewFocusedAfterApply, new GUIContent("Preview Focus", "Re-apply the focused preset to the scene view after shared balancing changes."), EditorStyles.toolbarButton, GUILayout.Width(105f));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            _severityCurve = EditorGUILayout.CurveField(new GUIContent("Severity Curve", "X is preset rank from mild to severe. Y is the shaped value used for interpolation."), _severityCurve, Color.cyan, new Rect(0f, 0f, 1f, 1f));

            DrawBalanceRange("Cloud Amount", ref _cloudAmountLow, ref _cloudAmountHigh, 0f, 1f);
            DrawBalanceRange("Cloud Density", ref _cloudThicknessLow, ref _cloudThicknessHigh, 0f, 1f);
            DrawBalanceRange("Edge Softness", ref _cloudSoftnessLow, ref _cloudSoftnessHigh, 0f, 1f);
            DrawBalanceRange("Weather Intensity", ref _weatherIntensityLow, ref _weatherIntensityHigh, 0f, 1f);
            DrawBalanceRange("Visibility Fog", ref _distanceHazeLow, ref _distanceHazeHigh, 0f, 1f);
            DrawBalanceRange("Sunlight Blocked", ref _sunlightBlockedLow, ref _sunlightBlockedHigh, 0f, 1f);
            DrawBalanceRange("Wind Motion", ref _windMotionLow, ref _windMotionHigh, 0f, 1f);
            DrawBalanceRange("Wetness", ref _wetnessLow, ref _wetnessHigh, 0f, 5f);
            DrawBalanceRange("Snowiness", ref _snowinessLow, ref _snowinessHigh, 0f, 1f);
            _tintLow = EditorGUILayout.ColorField("Mild Atmosphere Tint", _tintLow);
            _tintHigh = EditorGUILayout.ColorField("Severe Atmosphere Tint", _tintHigh);

            bool changed = EditorGUI.EndChangeCheck();
            if (changed)
                AutoApplyBalancing();

            EditorGUILayout.Space(5f);
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(GetSelectedCount() < 2);
            if (GUILayout.Button("Apply Ranked Balance", GUILayout.Height(24f)))
                ApplyRankedBalancing();
            EditorGUI.EndDisabledGroup();
            if (GUILayout.Button("Refresh", GUILayout.Width(90f), GUILayout.Height(24f)))
                SyncPresetList(true);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void SetController(WeatherController controller)
        {
            _controller = controller;
            _lastController = controller;
            SyncPresetList(true);
        }

        private void SyncPresetList(bool rebuild = false)
        {
            if (_controller == null || _controller.weatherDataPresets == null)
                return;

            if (rebuild)
            {
                _orderedPresetIndexes.Clear();
                _selectedPresets.Clear();
            }

            for (int i = 0; i < _controller.weatherDataPresets.Length; i++)
            {
                if (!_orderedPresetIndexes.Contains(i))
                    _orderedPresetIndexes.Add(i);

                if (!_selectedPresets.ContainsKey(i))
                    _selectedPresets[i] = true;
            }

            for (int i = _orderedPresetIndexes.Count - 1; i >= 0; i--)
            {
                int presetIndex = _orderedPresetIndexes[i];
                if (presetIndex < 0 || presetIndex >= _controller.weatherDataPresets.Length)
                    _orderedPresetIndexes.RemoveAt(i);
            }
        }

        private void DrawPresetRow(int order)
        {
            int presetIndex = _orderedPresetIndexes[order];
            WeatherController.WeatherData preset = _controller.weatherDataPresets[presetIndex];
            string label = preset == null || string.IsNullOrWhiteSpace(preset.weatherCondition)
                ? $"Preset {presetIndex + 1}"
                : $"{preset.weatherGlyph} {preset.weatherCondition}";

            Color previousColor = GUI.backgroundColor;
            if (presetIndex == _focusedPresetIndex)
                GUI.backgroundColor = _focusedTint;

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUI.backgroundColor = previousColor;
            EditorGUI.BeginChangeCheck();
            _selectedPresets[presetIndex] = EditorGUILayout.Toggle(_selectedPresets[presetIndex], GUILayout.Width(20f));
            if (EditorGUI.EndChangeCheck())
                AutoApplyBalancing();
            EditorGUILayout.LabelField($"{order + 1}", EditorStyles.miniBoldLabel, GUILayout.Width(22f));
            if (GUILayout.Button(label, EditorStyles.label))
                FocusPreset(presetIndex);

            EditorGUI.BeginDisabledGroup(order == 0);
            if (GUILayout.Button("Up", EditorStyles.miniButtonLeft, GUILayout.Width(32f)))
            {
                Move(order, -1);
                AutoApplyBalancing();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(order >= _orderedPresetIndexes.Count - 1);
            if (GUILayout.Button("Dn", EditorStyles.miniButtonMid, GUILayout.Width(30f)))
            {
                Move(order, 1);
                AutoApplyBalancing();
            }
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("Preview", EditorStyles.miniButtonRight, GUILayout.Width(58f)))
            {
                FocusPreset(presetIndex);
                _controller.ApplyWeatherPresetPreviewInEditor(presetIndex);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void FocusPreset(int presetIndex)
        {
            _focusedPresetIndex = presetIndex;

            if (_selectedPresets.TryGetValue(presetIndex, out bool selected) && !selected)
                return;

            Repaint();
        }

        private void Move(int order, int direction)
        {
            int newOrder = Mathf.Clamp(order + direction, 0, _orderedPresetIndexes.Count - 1);
            if (newOrder == order)
                return;

            int value = _orderedPresetIndexes[order];
            _orderedPresetIndexes.RemoveAt(order);
            _orderedPresetIndexes.Insert(newOrder, value);
        }

        private void AutoApplyBalancing()
        {
            if (_autoApply)
                ApplyRankedBalancing();
        }

        private void ApplyRankedBalancing()
        {
            List<int> selectedInOrder = GetSelectedPresetIndexes();
            if (selectedInOrder.Count < 2)
                return;

            Undo.RecordObject(_controller, "Apply Weather Ranked Balance");

            for (int i = 0; i < selectedInOrder.Count; i++)
            {
                float t = EvaluateSeverity(i, selectedInOrder.Count);
                WeatherController.WeatherData target = _controller.weatherDataPresets[selectedInOrder[i]];
                ApplyBalancedVisualFields(target, t);
            }

            EditorUtility.SetDirty(_controller);
            _controller.EnsureWeatherPresetAuthoringData();

            if (_previewFocusedAfterApply && _focusedPresetIndex >= 0 && _selectedPresets.TryGetValue(_focusedPresetIndex, out bool focusedSelected) && focusedSelected)
                _controller.ApplyWeatherPresetPreviewInEditor(_focusedPresetIndex);
        }

        private void ApplyBalancedVisualFields(WeatherController.WeatherData target, float t)
        {
            if (target == null)
                return;

            target.simpleCloudCoverage = Mathf.Lerp(_cloudAmountLow, _cloudAmountHigh, t);
            target.simpleCloudHeaviness = Mathf.Lerp(_cloudThicknessLow, _cloudThicknessHigh, t);
            target.simpleCloudSoftness = Mathf.Lerp(_cloudSoftnessLow, _cloudSoftnessHigh, t);
            target.simpleStorminess = Mathf.Lerp(_weatherIntensityLow, _weatherIntensityHigh, t);
            target.simpleFogginess = Mathf.Lerp(_distanceHazeLow, _distanceHazeHigh, t);
            target.simpleGloom = Mathf.Lerp(_sunlightBlockedLow, _sunlightBlockedHigh, t);
            target.simpleWindiness = Mathf.Lerp(_windMotionLow, _windMotionHigh, t);
            target.overallWeatherTint = Color.Lerp(_tintLow, _tintHigh, t);
            target.wetness = Mathf.Lerp(_wetnessLow, _wetnessHigh, t);
            target.snowiness = Mathf.Lerp(_snowinessLow, _snowinessHigh, t);

            if (_writeResolvedAdvancedVisuals)
                WriteResolvedAdvancedVisuals(target);
        }

        private void DrawInlinePresetEditor()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("3. Compare & Fine Tune Selected Presets", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (_focusedPresetIndex >= 0 && GUILayout.Button("Preview Focus", EditorStyles.miniButton, GUILayout.Width(105f)))
                _controller.ApplyWeatherPresetPreviewInEditor(_focusedPresetIndex);
            EditorGUILayout.EndHorizontal();

            List<int> selectedInOrder = GetSelectedPresetIndexes();
            if (selectedInOrder.Count == 0)
            {
                EditorGUILayout.HelpBox("Select presets in the ranked list to compare and tune them here.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            _editorScroll = EditorGUILayout.BeginScrollView(_editorScroll);
            DrawWrappedPresetCards(selectedInOrder);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawWrappedPresetCards(List<int> selectedInOrder)
        {
            float cardWidth = 245f;
            float availableWidth = Mathf.Max(cardWidth, position.width - 38f);
            int cardsPerRow = Mathf.Max(1, Mathf.FloorToInt(availableWidth / cardWidth));

            for (int i = 0; i < selectedInOrder.Count; i++)
            {
                if (i % cardsPerRow == 0)
                    EditorGUILayout.BeginHorizontal();

                DrawPresetCard(selectedInOrder[i], i, selectedInOrder.Count, cardWidth - 8f);

                if (i % cardsPerRow == cardsPerRow - 1 || i == selectedInOrder.Count - 1)
                    EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawPresetCard(int presetIndex, int selectedOrder, int selectedCount, float width)
        {
            WeatherController.WeatherData preset = _controller.weatherDataPresets[presetIndex];
            if (preset == null)
                return;

            float severity = EvaluateSeverity(selectedOrder, selectedCount);
            string label = string.IsNullOrWhiteSpace(preset.weatherCondition)
                ? $"Preset {presetIndex + 1}"
                : $"{preset.weatherGlyph} {preset.weatherCondition}";

            Color previousColor = GUI.backgroundColor;
            if (presetIndex == _focusedPresetIndex)
                GUI.backgroundColor = _focusedTint;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(width));
            GUI.backgroundColor = previousColor;
            EditorGUILayout.BeginHorizontal();
            GUIStyle titleStyle = presetIndex == _focusedPresetIndex ? EditorStyles.boldLabel : EditorStyles.label;
            if (GUILayout.Button(label, titleStyle))
                FocusPreset(presetIndex);
            if (presetIndex == _focusedPresetIndex)
                EditorGUILayout.LabelField("FOCUS", EditorStyles.miniBoldLabel, GUILayout.Width(45f));
            if (GUILayout.Button("Preview", EditorStyles.miniButton, GUILayout.Width(58f)))
            {
                FocusPreset(presetIndex);
                _controller.ApplyWeatherPresetPreviewInEditor(presetIndex);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField($"Rank {selectedOrder + 1}    severity {severity:0.00}", EditorStyles.miniLabel);
            EditorGUI.BeginChangeCheck();
            float cloudAmount = EditorGUILayout.Slider("Cloud Amount", preset.simpleCloudCoverage, 0f, 1f);
            float cloudThickness = EditorGUILayout.Slider("Cloud Density", preset.simpleCloudHeaviness, 0f, 1f);
            float cloudSoftness = EditorGUILayout.Slider("Edge Softness", preset.simpleCloudSoftness, 0f, 1f);
            float intensity = EditorGUILayout.Slider("Intensity", preset.simpleStorminess, 0f, 1f);
            float haze = EditorGUILayout.Slider("Visibility Fog", preset.simpleFogginess, 0f, 1f);
            float blocked = EditorGUILayout.Slider("Sun Blocked", preset.simpleGloom, 0f, 1f);
            float wind = EditorGUILayout.Slider("Wind Motion", preset.simpleWindiness, 0f, 1f);
            float wetness = EditorGUILayout.Slider("Wetness", preset.wetness, 0f, 5f);
            float snowiness = EditorGUILayout.Slider("Snowiness", preset.snowiness, 0f, 1f);
            Color tint = EditorGUILayout.ColorField("Atmosphere", preset.overallWeatherTint);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_controller, "Edit Weather Preset Balance Card");
                preset.simpleCloudCoverage = cloudAmount;
                preset.simpleCloudHeaviness = cloudThickness;
                preset.simpleCloudSoftness = cloudSoftness;
                preset.simpleStorminess = intensity;
                preset.simpleFogginess = haze;
                preset.simpleGloom = blocked;
                preset.simpleWindiness = wind;
                preset.wetness = wetness;
                preset.snowiness = snowiness;
                preset.overallWeatherTint = tint;

                if (_writeResolvedAdvancedVisuals)
                    WriteResolvedAdvancedVisuals(preset);

                PushFocusedValuesIntoBalancingEndpoints(preset, selectedOrder, selectedCount);
                EditorUtility.SetDirty(_controller);

                bool endpointChanged = selectedOrder == 0 || selectedOrder == selectedCount - 1;
                if (_autoApply && endpointChanged)
                    ApplyRankedBalancing();
            }

            WeatherPresetAuthoringUtility.DerivedWeatherPreview preview =
                WeatherPresetAuthoringUtility.BuildPreview(preset, _controller.skyFogVisualStart, _controller.skyFogVisualEnd);
            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField($"Sky {preview.skyCloudAlpha:0.00}/{preview.skyCloudPower:0.00}  Scene {preview.sceneCloudAlpha:0.00}/{preview.sceneCloudPower:0.00}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Shadow {preview.shadowIntensity:0.00}  Fog {preview.fogStrength:0.00}  Volume {preview.volumetricDensity:0.00}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private void PushFocusedValuesIntoBalancingEndpoints(WeatherController.WeatherData preset, int selectedOrder, int selectedCount)
        {
            if (selectedOrder == 0)
            {
                _cloudAmountLow = preset.simpleCloudCoverage;
                _cloudThicknessLow = preset.simpleCloudHeaviness;
                _cloudSoftnessLow = preset.simpleCloudSoftness;
                _weatherIntensityLow = preset.simpleStorminess;
                _distanceHazeLow = preset.simpleFogginess;
                _sunlightBlockedLow = preset.simpleGloom;
                _windMotionLow = preset.simpleWindiness;
                _wetnessLow = preset.wetness;
                _snowinessLow = preset.snowiness;
                _tintLow = preset.overallWeatherTint;
            }
            else if (selectedOrder == selectedCount - 1)
            {
                _cloudAmountHigh = preset.simpleCloudCoverage;
                _cloudThicknessHigh = preset.simpleCloudHeaviness;
                _cloudSoftnessHigh = preset.simpleCloudSoftness;
                _weatherIntensityHigh = preset.simpleStorminess;
                _distanceHazeHigh = preset.simpleFogginess;
                _sunlightBlockedHigh = preset.simpleGloom;
                _windMotionHigh = preset.simpleWindiness;
                _wetnessHigh = preset.wetness;
                _snowinessHigh = preset.snowiness;
                _tintHigh = preset.overallWeatherTint;
            }
        }

        private void CaptureEndpointsFromSelection()
        {
            List<int> selectedInOrder = GetSelectedPresetIndexes();
            if (_controller == null || _controller.weatherDataPresets == null || selectedInOrder.Count < 2)
                return;

            WeatherController.WeatherData first = _controller.weatherDataPresets[selectedInOrder[0]];
            WeatherController.WeatherData last = _controller.weatherDataPresets[selectedInOrder[selectedInOrder.Count - 1]];
            if (first == null || last == null)
                return;

            _cloudAmountLow = first.simpleCloudCoverage;
            _cloudAmountHigh = last.simpleCloudCoverage;
            _cloudThicknessLow = first.simpleCloudHeaviness;
            _cloudThicknessHigh = last.simpleCloudHeaviness;
            _cloudSoftnessLow = first.simpleCloudSoftness;
            _cloudSoftnessHigh = last.simpleCloudSoftness;
            _weatherIntensityLow = first.simpleStorminess;
            _weatherIntensityHigh = last.simpleStorminess;
            _distanceHazeLow = first.simpleFogginess;
            _distanceHazeHigh = last.simpleFogginess;
            _sunlightBlockedLow = first.simpleGloom;
            _sunlightBlockedHigh = last.simpleGloom;
            _windMotionLow = first.simpleWindiness;
            _windMotionHigh = last.simpleWindiness;
            _wetnessLow = first.wetness;
            _wetnessHigh = last.wetness;
            _snowinessLow = first.snowiness;
            _snowinessHigh = last.snowiness;
            _tintLow = first.overallWeatherTint;
            _tintHigh = last.overallWeatherTint;
        }

        private void DrawBalanceRange(string label, ref float low, ref float high, float min, float max)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(140f));
            EditorGUILayout.LabelField("Mild", GUILayout.Width(30f));
            low = EditorGUILayout.Slider(low, min, max);
            EditorGUILayout.LabelField("Severe", GUILayout.Width(45f));
            high = EditorGUILayout.Slider(high, min, max);
            EditorGUILayout.EndHorizontal();

            low = Mathf.Clamp(low, min, max);
            high = Mathf.Clamp(high, min, max);
        }

        private float EvaluateSeverity(int selectedOrder, int selectedCount)
        {
            if (selectedCount <= 1)
                return 0f;

            float linear = selectedOrder / (float)(selectedCount - 1);
            if (_severityCurve == null)
                return linear;

            return Mathf.Clamp01(_severityCurve.Evaluate(linear));
        }

        private void WriteResolvedAdvancedVisuals(WeatherController.WeatherData target)
        {
            bool previousOverride = target.useAdvancedOverrides;
            target.useAdvancedOverrides = false;
            WeatherPresetAuthoringUtility.ResolvedWeatherProfile resolved =
                WeatherPresetAuthoringUtility.BuildResolvedProfile(target);
            target.useAdvancedOverrides = previousOverride;

            target.cloudPowerRange = resolved.cloudPowerRange;
            target.cloudAlphaRange = resolved.cloudAlphaRange;
            target.windSpeedRange = resolved.windSpeedRange;
            target.windDirectionRange = resolved.windDirectionRange;
            target.gustStrengthRange = resolved.gustStrengthRange;
            target.skyCloudAlphaRange = resolved.skyCloudAlphaRange;
            target.skyCloudPowerRange = resolved.skyCloudPowerRange;
            target.cloudSoftness = resolved.cloudSoftness;
            target.cloudCoverageBias = resolved.cloudCoverageBias;
            target.cloudTurbulence = resolved.cloudTurbulence;
            target.cloudWarpStrength = resolved.cloudWarpStrength;
            target.cloudTint = resolved.cloudTint;
            target.cloudShadowStrength = resolved.cloudShadowStrength;
            target.cloudGreyStrength = resolved.cloudGreyStrength;
            target.cloudSunLightStrength = resolved.cloudSunLightStrength;
            target.sceneCloudAlphaRange = resolved.sceneCloudAlphaRange;
            target.sceneCloudPowerRange = resolved.sceneCloudPowerRange;
            target.sceneCloudSoftness = resolved.sceneCloudSoftness;
            target.sceneCloudCoverageBias = resolved.sceneCloudCoverageBias;
            target.sceneCloudTurbulence = resolved.sceneCloudTurbulence;
            target.sceneCloudWarpStrength = resolved.sceneCloudWarpStrength;
            target.sceneCloudTint = resolved.sceneCloudTint;
            target.sceneCloudShadowTint = resolved.sceneCloudShadowTint;
            target.planeEdgeFade = resolved.planeEdgeFade;
            target.planeRadialFade = resolved.planeRadialFade;
            target.planeHeightFade = resolved.planeHeightFade;
            target.sceneCloudFresnelFade = resolved.sceneCloudFresnelFade;
            target.sceneCloudMacroRoundness = resolved.sceneCloudMacroRoundness;
            target.sceneCloudDetailStrength = resolved.sceneCloudDetailStrength;
            target.sceneCloudDensityContrast = resolved.sceneCloudDensityContrast;
            target.sceneCloudErosionStrength = resolved.sceneCloudErosionStrength;
            target.sceneCloudSecondLayerStrength = resolved.sceneCloudSecondLayerStrength;
            target.sceneCloudSilverLiningStrength = resolved.sceneCloudSilverLiningStrength;
            target.sceneCloudBottomDarkening = resolved.sceneCloudBottomDarkening;
            target.sceneCloudDriftMultiplier = resolved.sceneCloudDriftMultiplier;
            target.terrainCloudShadowStrength = resolved.terrainCloudShadowStrength;
            target.terrainCloudShadowScale = resolved.terrainCloudShadowScale;
            target.terrainCloudShadowSoftness = resolved.terrainCloudShadowSoftness;
            target.terrainCloudShadowBias = resolved.terrainCloudShadowBias;
            target.terrainCloudShadowDriftMultiplier = resolved.terrainCloudShadowDriftMultiplier;
            target.terrainCloudShadowTint = resolved.terrainCloudShadowTint;
            target.volumeDensity = resolved.volumeDensity;
            target.volumeAbsorption = resolved.volumeAbsorption;
            target.volumeLightingStrength = resolved.volumeLightingStrength;
            target.volumeAlphaMultiplier = resolved.volumeAlphaMultiplier;
            target.volumeClusterScale = resolved.volumeClusterScale;
            target.volumeClusterDensity = resolved.volumeClusterDensity;
            target.volumeDetailStrength = resolved.volumeDetailStrength;
            target.volumeEdgeFade = resolved.volumeEdgeFade;
            target.fogStrength = resolved.fogStrength;
        }

        private List<int> GetSelectedPresetIndexes()
        {
            List<int> selected = new List<int>();
            for (int i = 0; i < _orderedPresetIndexes.Count; i++)
            {
                int presetIndex = _orderedPresetIndexes[i];
                if (_selectedPresets.TryGetValue(presetIndex, out bool isSelected) && isSelected)
                    selected.Add(presetIndex);
            }

            return selected;
        }

        private int GetSelectedCount()
        {
            int count = 0;
            for (int i = 0; i < _orderedPresetIndexes.Count; i++)
            {
                int presetIndex = _orderedPresetIndexes[i];
                if (_selectedPresets.TryGetValue(presetIndex, out bool isSelected) && isSelected)
                    count++;
            }

            return count;
        }

        private void SetAllSelected(bool selected)
        {
            for (int i = 0; i < _orderedPresetIndexes.Count; i++)
            {
                _selectedPresets[_orderedPresetIndexes[i]] = selected;
            }
        }
    }
}
