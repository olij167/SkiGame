using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.EnvironmentSimulation;

namespace PungentFunk.Utilities.Editor.EnvironmentSimulation
{
#if UNITY_EDITOR
    using System;
    using UnityEditor;
    using UnityEngine;

    internal sealed class PungentEnvironmentSimulationEditorState
    {
        public PungentClockController clock;
        public PungentWeatherController weather;
        public PungentDeltaTimeController deltaTime;
        public PungentEnvironmentSimulationValidationReport validation = new PungentEnvironmentSimulationValidationReport();

        public bool HasClock => clock != null;
        public bool HasWeather => weather != null;
        public bool HasDeltaTime => deltaTime != null;

        public void Refresh()
        {
            clock = PungentEnvironmentSimulationEditorGUI.FindSceneObject<PungentClockController>();
            weather = PungentEnvironmentSimulationEditorGUI.FindSceneObject<PungentWeatherController>();
            deltaTime = PungentEnvironmentSimulationEditorGUI.FindSceneObject<PungentDeltaTimeController>();
            validation.Refresh(clock, weather, deltaTime);
        }

        public string Summary()
        {
            return "Clock " + (HasClock ? "found" : "missing") +
                   " | Weather " + (HasWeather ? "found" : "missing") +
                   " | Delta " + (HasDeltaTime ? "found" : "missing");
        }
    }

    internal sealed class PungentEnvironmentSimulationValidationReport
    {
        public bool hasClock;
        public bool hasWeather;
        public bool hasDeltaTime;
        public bool hasCalendarProfile;
        public bool hasSkyProfile;
        public bool hasClockOutput;
        public bool hasWeatherProfile;
        public bool hasWeatherOutput;
        public bool hasForecast;
        public bool hasDeltaProfile;
        public bool hasUnityTimeWriter;
        public bool hasTimelineClockBridge;
        public bool hasWeatherVolumeAdapter;
        public bool hasWeatherWindAdapter;
        public bool hasPlayableDirectorAdapter;
        public int unityTimeWriterCount;
        public int configuredSystems;
        public int readySystems;

        public PackageOpportunity timeline = new PackageOpportunity("Timeline", "com.unity.timeline", "Time-event tracks, cinematic time overrides, and authored sequence bridges.");
        public PackageOpportunity urp = new PackageOpportunity("URP", "com.unity.render-pipelines.universal", "Volume, renderer feature, fog, sky, and weather-output adapters.");
        public PackageOpportunity shaderGraph = new PackageOpportunity("Shader Graph", "com.unity.shadergraph", "Wetness, snow, cloud, tint, and surface-state shader globals.");
        public PackageOpportunity terrainTools = new PackageOpportunity("Terrain Tools", "com.unity.terrain-tools", "Terrain wetness, snow masks, and weather-aware terrain authoring.");
        public PackageOpportunity inputSystem = new PackageOpportunity("Input System", "com.unity.inputsystem", "Runtime debug bindings for pause, slow motion, forecast preview, and time scrub controls.");
        public PackageOpportunity testFramework = new PackageOpportunity("Test Framework", "com.unity.test-framework", "EditMode/PlayMode validation for rollover, forecasts, pause stacks, and adapters.");

        public void Refresh(PungentClockController clock, PungentWeatherController weather, PungentDeltaTimeController deltaTime)
        {
            PungentEnvironmentSimulationPackageCache.RefreshIfNeeded();

            hasClock = clock != null;
            hasWeather = weather != null;
            hasDeltaTime = deltaTime != null;
            hasCalendarProfile = hasClock && PungentEnvironmentSimulationEditorGUI.ObjectValue(clock, "calendarProfile") != null;
            hasSkyProfile = hasClock && PungentEnvironmentSimulationEditorGUI.ObjectValue(clock, "skyProfile") != null;
            hasClockOutput = hasClock && (PungentEnvironmentSimulationEditorGUI.ObjectValue(clock, "sunTransform") != null ||
                                          PungentEnvironmentSimulationEditorGUI.ObjectValue(clock, "moonTransform") != null ||
                                          PungentEnvironmentSimulationEditorGUI.ObjectValue(clock, "sunLight") != null ||
                                          PungentEnvironmentSimulationEditorGUI.ObjectValue(clock, "moonLight") != null);
            hasWeatherProfile = hasWeather && PungentEnvironmentSimulationEditorGUI.ObjectValue(weather, "weatherProfile") != null;
            hasWeatherOutput = hasWeather && PungentEnvironmentSimulationEditorGUI.ObjectValue(weather, "outputApplier") != null;
            hasForecast = hasWeather && PungentEnvironmentSimulationEditorGUI.ArraySize(weather, "forecast") > 0;
            hasDeltaProfile = hasDeltaTime && PungentEnvironmentSimulationEditorGUI.ObjectValue(deltaTime, "profile") != null;
            unityTimeWriterCount = CountUnityTimeWriters(deltaTime);
            hasUnityTimeWriter = unityTimeWriterCount > 0;
            hasTimelineClockBridge = hasClock && clock.GetComponent<PungentTimelineClockBridge>() != null;
            hasWeatherVolumeAdapter = hasWeather && weather.GetComponent<PungentWeatherVolumeAdapter>() != null;
            hasWeatherWindAdapter = hasWeather && weather.GetComponent<PungentWeatherWindAdapterBase>() != null;
            hasPlayableDirectorAdapter = hasDeltaTime && deltaTime.GetComponent<PungentDeltaTimePlayableDirectorAdapter>() != null;

            configuredSystems = (hasClock ? 1 : 0) + (hasWeather ? 1 : 0) + (hasDeltaTime ? 1 : 0);
            readySystems = (hasCalendarProfile ? 1 : 0) + (hasWeatherProfile ? 1 : 0) + (hasDeltaProfile ? 1 : 0);

            timeline.installed = PungentEnvironmentSimulationPackageCache.IsInstalled(timeline.packageId);
            urp.installed = PungentEnvironmentSimulationPackageCache.IsInstalled(urp.packageId);
            shaderGraph.installed = PungentEnvironmentSimulationPackageCache.IsInstalled(shaderGraph.packageId);
            terrainTools.installed = PungentEnvironmentSimulationPackageCache.IsInstalled(terrainTools.packageId);
            inputSystem.installed = PungentEnvironmentSimulationPackageCache.IsInstalled(inputSystem.packageId);
            testFramework.installed = PungentEnvironmentSimulationPackageCache.IsInstalled(testFramework.packageId);
        }

        private static int CountUnityTimeWriters(PungentDeltaTimeController deltaTime)
        {
            if (deltaTime == null)
                return 0;

            UnityEngine.Object profile = PungentEnvironmentSimulationEditorGUI.ObjectValue(deltaTime, "profile");
            if (profile == null)
                return 1;

            SerializedObject serializedProfile = new SerializedObject(profile);
            SerializedProperty channels = serializedProfile.FindProperty("channels");
            if (channels == null)
                return 0;

            int count = 0;
            for (int i = 0; i < channels.arraySize; i++)
            {
                SerializedProperty item = channels.GetArrayElementAtIndex(i);
                SerializedProperty writer = item.FindPropertyRelative("applyToUnityTimeScale");
                if (writer != null && writer.boolValue)
                    count++;
            }

            return count;
        }

        public struct PackageOpportunity
        {
            public readonly string label;
            public readonly string packageId;
            public readonly string use;
            public bool installed;

            public PackageOpportunity(string label, string packageId, string use)
            {
                this.label = label;
                this.packageId = packageId;
                this.use = use;
                installed = false;
            }
        }
    }

    internal static class PungentEnvironmentSimulationPackageCache
    {
        private static readonly System.Collections.Generic.HashSet<string> InstalledPackages = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static double _lastRefreshTime = -1000d;

        public static void RefreshIfNeeded()
        {
            double now = EditorApplication.timeSinceStartup;
            if (_lastRefreshTime > 0d && now - _lastRefreshTime < 10d)
                return;

            InstalledPackages.Clear();
            string projectRoot = System.IO.Directory.GetParent(Application.dataPath)?.FullName;
            if (!string.IsNullOrEmpty(projectRoot))
            {
                AddPackageIdsFromFile(System.IO.Path.Combine(projectRoot, "Packages", "manifest.json"));
                AddPackageIdsFromFile(System.IO.Path.Combine(projectRoot, "Packages", "packages-lock.json"));
            }

            _lastRefreshTime = now;
        }

        private static void AddPackageIdsFromFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
                return;

            string text = System.IO.File.ReadAllText(path);
            AddIfPresent(text, "com.unity.timeline");
            AddIfPresent(text, "com.unity.render-pipelines.universal");
            AddIfPresent(text, "com.unity.shadergraph");
            AddIfPresent(text, "com.unity.terrain-tools");
            AddIfPresent(text, "com.unity.inputsystem");
            AddIfPresent(text, "com.unity.test-framework");
        }

        private static void AddIfPresent(string text, string packageId)
        {
            if (!string.IsNullOrEmpty(text) && text.IndexOf("\"" + packageId + "\"", StringComparison.OrdinalIgnoreCase) >= 0)
                InstalledPackages.Add(packageId);
        }

        public static bool IsInstalled(string packageId)
        {
            RefreshIfNeeded();
            return !string.IsNullOrEmpty(packageId) && InstalledPackages.Contains(packageId);
        }
    }

    internal static class PungentEnvironmentSimulationEditorGUI
    {
        public static T FindSceneObject<T>() where T : UnityEngine.Object
        {
            return UnityEngine.Object.FindAnyObjectByType<T>();
        }

        public static SerializedProperty Property(SerializedObject serializedObject, string propertyName)
        {
            return serializedObject == null ? null : serializedObject.FindProperty(propertyName);
        }

        public static UnityEngine.Object ObjectValue(UnityEngine.Object target, string propertyName)
        {
            if (target == null)
                return null;

            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            return property != null && property.propertyType == SerializedPropertyType.ObjectReference
                ? property.objectReferenceValue
                : null;
        }

        public static int ArraySize(UnityEngine.Object target, string propertyName)
        {
            if (target == null)
                return 0;

            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            return property != null && property.isArray ? property.arraySize : 0;
        }

        public static void Header(string title, string subtitle, string utilityId)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.HeaderTint, 0.24f, 0.12f, 8, 6)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(new GUIContent(title, subtitle), UtilityWindowTheme.TitleStyle);
                    GUILayout.FlexibleSpace();
                    PungentFunk.Utilities.Editor.Core.Help.PungentUtilityHelpButton.Draw(utilityId, "overview", "overview", "Open help for " + title + ".", title + " header");
                    PungentFunk.Utilities.Editor.Core.PungentMinimizedUtilitiesButton.DrawHeaderPill();
                    PungentFunk.Utilities.Editor.Core.PungentUtilityMinimizer.DrawHeaderMinimizeButton();
                }

                EditorGUILayout.LabelField(subtitle, UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        public static void Section(string title, Color tint, Action draw, string pill = null)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, 0.15f, 0.07f, 6, 4)))
            {
                UtilityWindowTheme.SectionTitle(title, tint, pill);
                draw?.Invoke();
            }
        }

        public static void StatusCard(string title, string detail, bool ok, Action drawActions = null)
        {
            Color tint = ok ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber;
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, ok ? 0.12f : 0.18f, 0.06f, 5, 3)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(title, UtilityWindowTheme.CardLabelStyle);
                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill(ok ? "Ready" : "Needs Setup", tint, 92f);
                }

                EditorGUILayout.LabelField(detail, UtilityWindowTheme.MutedMiniLabelStyle);
                drawActions?.Invoke();
            }
        }

        public static void OpportunityCard(PungentEnvironmentSimulationValidationReport.PackageOpportunity opportunity)
        {
            Color tint = opportunity.installed ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral;
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, opportunity.installed ? 0.11f : 0.08f, 0.05f, 5, 3)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(opportunity.label, UtilityWindowTheme.CardLabelStyle);
                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill(opportunity.installed ? "Installed" : "Optional", tint, 78f);
                }

                EditorGUILayout.LabelField(opportunity.packageId, UtilityWindowTheme.MutedMiniLabelStyle);
                EditorGUILayout.LabelField(opportunity.use, UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        public static void DrawLayoutQAStrip(float width, float height, string primaryWorkflow, string narrowFlow, string mediumFlow, string wideFlow)
        {
            string widthMode = width < 760f ? "Narrow" : width < 1120f ? "Medium" : "Wide";
            string heightMode = height < 560f ? "Short" : height > 760f ? "Tall" : "Standard";
            string activeFlow = width < 760f ? narrowFlow : width < 1120f ? mediumFlow : wideFlow;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.08f, 0.03f, 4, 2)))
            {
                AdaptiveToolbarRow(width,
                    ToolbarGroup(230f, () =>
                    {
                        Chip("Width " + widthMode, UtilityWindowTheme.Cyan, 96f);
                        Chip("Height " + heightMode, UtilityWindowTheme.Purple, 104f);
                    }),
                    ToolbarGroup(360f, () =>
                    {
                        EditorGUILayout.LabelField(new GUIContent(primaryWorkflow, "Primary workflow this window should preserve while resizing."), UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.MinWidth(330f));
                    }),
                    ToolbarGroup(360f, () =>
                    {
                        EditorGUILayout.LabelField(new GUIContent(activeFlow, "Active layout flow for the current docked/floating size."), UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.MinWidth(330f));
                    }));
            }
        }

        public static void DrawProperties(SerializedObject serializedObject, params string[] propertyNames)
        {
            if (serializedObject == null || propertyNames == null)
                return;

            serializedObject.Update();
            for (int i = 0; i < propertyNames.Length; i++)
            {
                SerializedProperty property = serializedObject.FindProperty(propertyNames[i]);
                if (property != null)
                    EditorGUILayout.PropertyField(property, true);
            }
            serializedObject.ApplyModifiedProperties();
        }

        public static void DrawObjectPropertyWithCreate<TAsset>(SerializedObject serializedObject, string propertyName, string label, string defaultName)
            where TAsset : ScriptableObject
        {
            serializedObject.Update();
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                serializedObject.ApplyModifiedProperties();
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label), true);
                if (GUILayout.Button(new GUIContent("Create", "Create and assign a new " + typeof(TAsset).Name + " asset."), GUILayout.Width(68f)))
                {
                    TAsset asset = CreateAsset<TAsset>(defaultName);
                    if (asset != null)
                    {
                        property.objectReferenceValue = asset;
                        EditorUtility.SetDirty(serializedObject.targetObject);
                    }
                }
            }
            serializedObject.ApplyModifiedProperties();
        }

        public static TAsset CreateAsset<TAsset>(string defaultName) where TAsset : ScriptableObject
        {
            string folder = "Assets";
            UnityEngine.Object active = Selection.activeObject;
            if (active != null)
            {
                string activePath = AssetDatabase.GetAssetPath(active);
                if (!string.IsNullOrEmpty(activePath))
                    folder = System.IO.Directory.Exists(activePath) ? activePath : System.IO.Path.GetDirectoryName(activePath);
            }

            if (string.IsNullOrEmpty(folder))
                folder = "Assets";

            string path = AssetDatabase.GenerateUniqueAssetPath(System.IO.Path.Combine(folder, defaultName + ".asset").Replace("\\", "/"));
            TAsset asset = ScriptableObject.CreateInstance<TAsset>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            return asset;
        }

        public static GameObject CreateController<T>(string name) where T : Component
        {
            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.AddComponent<T>();
            Selection.activeGameObject = go;
            return go;
        }

        public static void DrawTargetRow<T>(ref T target, string label, Action refresh, Action openHub) where T : UnityEngine.Object
        {
            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.10f, 0.04f, 5, 3)))
            {
                target = (T)EditorGUILayout.ObjectField(new GUIContent(label, "Scene controller configured by this utility."), target, typeof(T), true);
                if (GUILayout.Button(new GUIContent("Refresh", "Find the first matching controller in the open scene."), GUILayout.Width(76f)))
                    refresh?.Invoke();
                if (GUILayout.Button(new GUIContent("Select", "Select this controller."), GUILayout.Width(64f)) && target != null)
                    Selection.activeObject = target;
                if (GUILayout.Button(new GUIContent("Hub", "Open the Environment Simulation hub."), GUILayout.Width(52f)))
                    openHub?.Invoke();
            }
        }

        public static float GetFloat(string key, float fallback, float min, float max)
        {
            return Mathf.Clamp(UtilityWindowPrefs.GetFloat(key, fallback), min, max);
        }

        public static void SetFloat(string key, float value)
        {
            UtilityWindowPrefs.SetFloat(key, value);
        }

        public static int GetInt(string key, int fallback, int min, int max)
        {
            return Mathf.Clamp(UtilityWindowPrefs.GetInt(key, fallback), min, max);
        }

        public static void SetInt(string key, int value)
        {
            UtilityWindowPrefs.SetInt(key, value);
        }

        public static string GetString(string key, string fallback)
        {
            return UtilityWindowPrefs.GetString(key, fallback ?? string.Empty);
        }

        public static void SetString(string key, string value)
        {
            UtilityWindowPrefs.SetString(key, value ?? string.Empty);
        }

        public static bool GetBool(string key, bool fallback)
        {
            return UtilityWindowPrefs.GetBool(key, fallback);
        }

        public static void SetBool(string key, bool value)
        {
            UtilityWindowPrefs.SetBool(key, value);
        }

        public static int ToolbarButton(int selected, string[] labels, string tooltipPrefix = null)
        {
            if (labels == null || labels.Length == 0)
                return selected;

            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.10f, 0.04f, 4, 2)))
            {
                for (int i = 0; i < labels.Length; i++)
                {
                    bool active = selected == i;
                    Color tint = active ? UtilityWindowTheme.Blue : UtilityWindowTheme.Neutral;
                    string tooltip = string.IsNullOrEmpty(tooltipPrefix) ? labels[i] : tooltipPrefix + " " + labels[i];
                    if (UtilityWindowTheme.TintedButton(new GUIContent(labels[i], tooltip).text, tint, GUILayout.Height(23f)))
                        selected = i;
                }
            }

            return selected;
        }

        public static void DrawResponsiveWorkspace(
            float windowWidth,
            ref Vector2 leftScroll,
            ref Vector2 centerScroll,
            ref Vector2 rightScroll,
            float leftWidth,
            float rightWidth,
            Action drawLeft,
            Action drawCenter,
            Action drawRight)
        {
            ResponsiveWorkspaceLayout(
                windowWidth,
                null,
                null,
                ref leftScroll,
                ref centerScroll,
                ref rightScroll,
                ref leftWidth,
                ref rightWidth,
                220f,
                420f,
                390f,
                240f,
                460f,
                drawLeft,
                drawCenter,
                drawRight);
        }

        public static void ResponsiveWorkspaceLayout(
            float windowWidth,
            string leftWidthPrefKey,
            string rightWidthPrefKey,
            ref Vector2 leftScroll,
            ref Vector2 centerScroll,
            ref Vector2 rightScroll,
            ref float leftWidth,
            ref float rightWidth,
            float minLeft,
            float maxLeft,
            float minCenter,
            float minRight,
            float maxRight,
            Action drawLeft,
            Action drawCenter,
            Action drawRight)
        {
            float availableWidth = Mathf.Max(320f, windowWidth - 24f);
            bool narrow = windowWidth < 760f;
            bool medium = windowWidth >= 760f && windowWidth < 1120f;

            if (narrow)
            {
                centerScroll = EditorGUILayout.BeginScrollView(centerScroll);
                drawCenter?.Invoke();
                GUILayout.Space(6f);
                drawLeft?.Invoke();
                GUILayout.Space(6f);
                drawRight?.Invoke();
                EditorGUILayout.EndScrollView();
                return;
            }

            if (medium)
            {
                centerScroll = EditorGUILayout.BeginScrollView(centerScroll);
                drawCenter?.Invoke();
                GUILayout.Space(6f);
                if (windowWidth >= 920f)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUILayout.VerticalScope(GUILayout.Width((availableWidth - 8f) * 0.48f)))
                            drawLeft?.Invoke();
                        using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                            drawRight?.Invoke();
                    }
                }
                else
                {
                    drawLeft?.Invoke();
                    GUILayout.Space(6f);
                    drawRight?.Invoke();
                }
                EditorGUILayout.EndScrollView();
                return;
            }

            ClampRailWidths(availableWidth, ref leftWidth, ref rightWidth, minLeft, maxLeft, minCenter, minRight, maxRight);
            if (!string.IsNullOrEmpty(leftWidthPrefKey))
                SetFloat(leftWidthPrefKey, leftWidth);
            if (!string.IsNullOrEmpty(rightWidthPrefKey))
                SetFloat(rightWidthPrefKey, rightWidth);

            using (new EditorGUILayout.HorizontalScope())
            {
                leftScroll = EditorGUILayout.BeginScrollView(leftScroll, GUILayout.Width(leftWidth), GUILayout.ExpandHeight(true));
                drawLeft?.Invoke();
                EditorGUILayout.EndScrollView();

                ResizableSplitHandle(
                    "Left setup rail",
                    ref leftWidth,
                    minLeft,
                    Mathf.Max(minLeft, availableWidth - rightWidth - minCenter),
                    leftWidthPrefKey);

                centerScroll = EditorGUILayout.BeginScrollView(centerScroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                drawCenter?.Invoke();
                EditorGUILayout.EndScrollView();

                ResizableSplitHandle(
                    "Right detail rail",
                    ref rightWidth,
                    minRight,
                    Mathf.Max(minRight, availableWidth - leftWidth - minCenter),
                    rightWidthPrefKey,
                    true);

                rightScroll = EditorGUILayout.BeginScrollView(rightScroll, GUILayout.Width(rightWidth), GUILayout.ExpandHeight(true));
                drawRight?.Invoke();
                EditorGUILayout.EndScrollView();
            }
        }

        public static void ResizableRightRailLayout(
            float windowWidth,
            string rightWidthPrefKey,
            ref Vector2 centerScroll,
            ref Vector2 rightScroll,
            ref float rightWidth,
            float minCenter,
            float minRight,
            float maxRight,
            Action drawCenter,
            Action drawRight)
        {
            if (windowWidth < 920f)
            {
                centerScroll = EditorGUILayout.BeginScrollView(centerScroll);
                drawCenter?.Invoke();
                GUILayout.Space(6f);
                drawRight?.Invoke();
                EditorGUILayout.EndScrollView();
                return;
            }

            float availableWidth = Mathf.Max(320f, windowWidth - 24f);
            float maxUsableRight = Mathf.Max(minRight, Mathf.Min(maxRight, availableWidth - minCenter));
            rightWidth = Mathf.Clamp(rightWidth, minRight, maxUsableRight);
            if (!string.IsNullOrEmpty(rightWidthPrefKey))
                SetFloat(rightWidthPrefKey, rightWidth);

            using (new EditorGUILayout.HorizontalScope())
            {
                centerScroll = EditorGUILayout.BeginScrollView(centerScroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                drawCenter?.Invoke();
                EditorGUILayout.EndScrollView();

                ResizableSplitHandle(
                    "Right detail rail",
                    ref rightWidth,
                    minRight,
                    maxUsableRight,
                    rightWidthPrefKey,
                    true);

                rightScroll = EditorGUILayout.BeginScrollView(rightScroll, GUILayout.Width(rightWidth), GUILayout.ExpandHeight(true));
                drawRight?.Invoke();
                EditorGUILayout.EndScrollView();
            }
        }

        public static IDisposable StretchPanelScope(Color tint, float fillAlpha = 0.15f, float borderAlpha = 0.07f, int padding = 6, int margin = 4)
        {
            return new EditorGUILayout.VerticalScope(
                UtilityWindowTheme.PanelStyle(tint, fillAlpha, borderAlpha, padding, margin),
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));
        }

        public struct AdaptiveToolbarGroup
        {
            public float minWidth;
            public Action draw;

            public AdaptiveToolbarGroup(float minWidth, Action draw)
            {
                this.minWidth = minWidth;
                this.draw = draw;
            }
        }

        public static AdaptiveToolbarGroup ToolbarGroup(float minWidth, Action draw)
        {
            return new AdaptiveToolbarGroup(minWidth, draw);
        }

        public static void AdaptiveToolbarRow(float windowWidth, params AdaptiveToolbarGroup[] groups)
        {
            if (groups == null || groups.Length == 0)
                return;

            float availableWidth = Mathf.Max(260f, windowWidth - 42f);
            float rowWidth = 0f;
            bool rowOpen = false;

            for (int i = 0; i < groups.Length; i++)
            {
                AdaptiveToolbarGroup group = groups[i];
                float groupWidth = Mathf.Max(80f, group.minWidth);
                if (!rowOpen)
                {
                    EditorGUILayout.BeginHorizontal();
                    rowOpen = true;
                    rowWidth = 0f;
                }
                else if (rowWidth + groupWidth > availableWidth)
                {
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    rowWidth = 0f;
                }

                using (new EditorGUILayout.HorizontalScope(GUILayout.MinWidth(groupWidth)))
                    group.draw?.Invoke();
                rowWidth += groupWidth;
            }

            if (rowOpen)
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
        }

        public static void DrawButtonRow(float windowWidth, Action firstRow, Action secondRow, float wrapWidth = 760f)
        {
            if (windowWidth < wrapWidth)
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    using (new EditorGUILayout.HorizontalScope())
                        firstRow?.Invoke();
                    using (new EditorGUILayout.HorizontalScope())
                        secondRow?.Invoke();
                }
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                firstRow?.Invoke();
                secondRow?.Invoke();
            }
        }

        public static void DrawChipRow(float windowWidth, Action firstRow, Action secondRow, float wrapWidth = 760f)
        {
            if (windowWidth < wrapWidth)
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    using (new EditorGUILayout.HorizontalScope())
                        firstRow?.Invoke();
                    using (new EditorGUILayout.HorizontalScope())
                        secondRow?.Invoke();
                }
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                firstRow?.Invoke();
                GUILayout.FlexibleSpace();
                secondRow?.Invoke();
            }
        }

        public static float CurrentContentWidth(float fallback)
        {
            float viewWidth = EditorGUIUtility.currentViewWidth;
            if (viewWidth <= 1f)
                viewWidth = fallback;
            return Mathf.Max(260f, viewWidth - 44f);
        }

        private static void ClampRailWidths(
            float availableWidth,
            ref float leftWidth,
            ref float rightWidth,
            float minLeft,
            float maxLeft,
            float minCenter,
            float minRight,
            float maxRight)
        {
            const float gapBudget = 14f;

            leftWidth = Mathf.Clamp(leftWidth, minLeft, maxLeft);
            rightWidth = Mathf.Clamp(rightWidth, minRight, maxRight);

            float sideBudget = Mathf.Max(minLeft + minRight, availableWidth - minCenter - gapBudget);
            if (leftWidth + rightWidth <= sideBudget)
                return;

            float excess = leftWidth + rightWidth - sideBudget;
            float rightReduction = Mathf.Min(excess, rightWidth - minRight);
            rightWidth -= rightReduction;
            excess -= rightReduction;
            if (excess > 0f)
                leftWidth = Mathf.Max(minLeft, leftWidth - excess);
        }

        private static void ResizableSplitHandle(
            string label,
            ref float width,
            float minWidth,
            float maxWidth,
            string prefKey,
            bool rightAnchored = false)
        {
            Rect rect = GUILayoutUtility.GetRect(6f, 1f, GUILayout.Width(6f), GUILayout.ExpandHeight(true));
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);

            Color old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.14f);
            GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, 2f, Mathf.Max(1f, rect.height - 4f)), EditorGUIUtility.whiteTexture);
            GUI.color = old;

            int controlId = GUIUtility.GetControlID(label.GetHashCode(), FocusType.Passive, rect);
            Event current = Event.current;
            switch (current.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (rect.Contains(current.mousePosition) && current.button == 0)
                    {
                        GUIUtility.hotControl = controlId;
                        current.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId)
                    {
                        float delta = rightAnchored ? -current.delta.x : current.delta.x;
                        width = Mathf.Clamp(width + delta, minWidth, maxWidth);
                        if (!string.IsNullOrEmpty(prefKey))
                            SetFloat(prefKey, width);
                        current.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId)
                    {
                        GUIUtility.hotControl = 0;
                        current.Use();
                    }
                    break;
            }
        }

        public static void Chip(string label, Color tint, float width = 86f)
        {
            UtilityWindowTheme.CountPill(label, tint, width);
        }

        public static Rect TimelineRect(float height)
        {
            return GUILayoutUtility.GetRect(1f, height, GUILayout.ExpandWidth(true));
        }

        public static void DrawMarker(Rect rect, float t, Color color, string label)
        {
            t = Mathf.Clamp01(t);
            float x = Mathf.Lerp(rect.xMin, rect.xMax, t);
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(x - 1f, rect.yMin, 2f, rect.height), EditorGUIUtility.whiteTexture);
            GUI.color = old;
            if (!string.IsNullOrEmpty(label))
                GUI.Label(new Rect(Mathf.Clamp(x - 28f, rect.xMin, rect.xMax - 56f), rect.yMax - 18f, 56f, 16f), label, UtilityWindowTheme.MutedMiniLabelStyle);
        }
    }
#endif
}
