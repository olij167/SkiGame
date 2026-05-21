using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.SceneTools;

namespace PungentFunk.Utilities.Editor.SceneTools
{
    #if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using UnityEditor;
    using UnityEngine;

    public sealed class PungentSceneNavigationWindow : EditorWindow
    {
        private enum TrackingOperator
        {
            Always,
            IsActive,
            ComponentEnabled,
            Equals,
            NotEquals,
            Contains,
            GreaterThan,
            LessThan,
            IsNull,
            IsNotNull
        }

        private enum TrackingMode
        {
            ManualOnly,
            SoftFollow,
            RefocusWhenOutsideMargin
        }

        [Serializable]
        private sealed class Waypoint
        {
            public string name;
            public string group = "Default";
            public Color color = new Color(0.25f, 0.8f, 1f, 1f);
            public Vector3 pivot;
            public Quaternion rotation = Quaternion.identity;
            public float size = 10f;
        }

        [Serializable]
        private sealed class WaypointStore
        {
            public List<Waypoint> waypoints = new List<Waypoint>();
        }

        [Serializable]
        private sealed class TrackingRule
        {
            public string name = "Tracking Rule";
            public bool enabled = true;
            public int priority = 0;
            public GameObject target;
            public Component component;
            public string fieldOrProperty = string.Empty;
            public TrackingOperator op = TrackingOperator.IsActive;
            public string expectedValue = "true";
            public float focusSize = 8f;
            public float margin = 2f;
        }

        private const string PrefWaypoints = "PungentFunkUtilities.SceneNavigation.Waypoints";
        private const string PrefFollow = "PungentFunkUtilities.SceneNavigation.Follow";
        private const string PrefOverlay = "PungentFunkUtilities.SceneNavigation.Overlay";
        private const double FollowInterval = 0.25d;

        private readonly List<Waypoint> _waypoints = new List<Waypoint>();
        private readonly List<TrackingRule> _trackingRules = new List<TrackingRule>();
        private Vector2 _scroll;
        private string _newWaypointName = "Waypoint";
        private string _newWaypointGroup = "Default";
        private string _activeGroup = "All";
        private bool _followEnabled;
        private bool _drawOverlay = true;
        private TrackingMode _trackingMode = TrackingMode.RefocusWhenOutsideMargin;
        private double _nextFollowTime;
        private string _status = "Ready.";

        [MenuItem("Tools/Utilities/Scene/Scene Navigation", priority = 930)]
        public static void Open()
        {
            PungentSceneNavigationWindow window = GetWindow<PungentSceneNavigationWindow>("Scene Navigation");
            window.minSize = new Vector2(620f, 420f);
            window.Show();
        }

        [MenuItem("GameObject/PungentFunk Utilities/Fast Travel SceneView To Selection", false, 4)]
        private static void FastTravelToSelectionMenu()
        {
            FrameSelection(Selection.transforms);
        }

        [MenuItem("GameObject/PungentFunk Utilities/Fast Travel SceneView To Selection", true)]
        private static bool ValidateFastTravelToSelectionMenu() => Selection.transforms != null && Selection.transforms.Length > 0;

        private void OnEnable()
        {
            LoadWaypoints();
            _followEnabled = EditorPrefs.GetBool(PrefFollow, false);
            _drawOverlay = EditorPrefs.GetBool(PrefOverlay, true);
            EditorApplication.update -= EditorUpdate;
            EditorApplication.update += EditorUpdate;
            SceneView.duringSceneGui -= DuringSceneGUI;
            SceneView.duringSceneGui += DuringSceneGUI;
        }

        private void OnDisable()
        {
            SaveWaypoints();
            EditorPrefs.SetBool(PrefFollow, _followEnabled);
            EditorPrefs.SetBool(PrefOverlay, _drawOverlay);
            EditorApplication.update -= EditorUpdate;
            SceneView.duringSceneGui -= DuringSceneGUI;
        }

        private void OnGUI()
        {
            UtilityWindowTheme.Header(
                "Scene Navigation",
                "Fast-travel the SceneView, save grouped camera waypoints, and focus targets with configurable tracking rules.",
                _status);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawFastTravelPanel();
            DrawWaypointPanel();
            DrawTrackingPanel();
            EditorGUILayout.EndScrollView();
        }

        private void DrawFastTravelPanel()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                UtilityWindowTheme.SectionTitle("Fast Travel", UtilityWindowTheme.Blue);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.TintedButton("Frame Selection", UtilityWindowTheme.Blue, GUILayout.Width(120f)))
                        FrameSelection(Selection.transforms);
                    if (UtilityWindowTheme.TintedButton("Frame All Rules", UtilityWindowTheme.Purple, GUILayout.Width(122f)))
                        FrameTrackingMatches();
                    if (UtilityWindowTheme.TintedButton("Travel To Scene Pivot", UtilityWindowTheme.Teal, GUILayout.Width(142f)))
                        TravelTo(GetScenePivot(), GetSceneRotation(), GetSceneSize());
                    if (UtilityWindowTheme.TintedButton("Save View", UtilityWindowTheme.Green, GUILayout.Width(90f)))
                        AddWaypointFromSceneView();
                }

                _drawOverlay = EditorGUILayout.Toggle("SceneView Overlay", _drawOverlay);
                EditorGUILayout.LabelField("Tip: the overlay gives quick access to saved groups and follow mode without keeping this window focused.", UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void DrawWaypointPanel()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                UtilityWindowTheme.SectionTitle("Scene Waypoints", UtilityWindowTheme.Teal, _waypoints.Count + " saved");
                using (new EditorGUILayout.HorizontalScope())
                {
                    _newWaypointName = EditorGUILayout.TextField(_newWaypointName);
                    _newWaypointGroup = EditorGUILayout.TextField(_newWaypointGroup, GUILayout.Width(110f));
                    if (GUILayout.Button("Save Current View", GUILayout.Width(126f)))
                        AddWaypointFromSceneView();
                }

                string[] groups = new[] { "All" }.Concat(_waypoints.Select(w => string.IsNullOrWhiteSpace(w.group) ? "Default" : w.group).Distinct()).ToArray();
                int groupIndex = Mathf.Max(0, Array.IndexOf(groups, _activeGroup));
                _activeGroup = groups[EditorGUILayout.Popup("Group", groupIndex, groups)];

                List<Waypoint> shown = _waypoints.Where(w => _activeGroup == "All" || string.Equals(w.group, _activeGroup, StringComparison.OrdinalIgnoreCase)).ToList();
                for (int i = 0; i < shown.Count; i++)
                {
                    Waypoint waypoint = shown[i];
                    int originalIndex = _waypoints.IndexOf(waypoint);
                    using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(waypoint.color, 0.10f, 0.05f, 4, 2)))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            waypoint.name = EditorGUILayout.TextField(waypoint.name);
                            waypoint.group = EditorGUILayout.TextField(waypoint.group, GUILayout.Width(92f));
                            waypoint.color = EditorGUILayout.ColorField(GUIContent.none, waypoint.color, false, false, false, GUILayout.Width(54f));
                            if (GUILayout.Button("Go", GUILayout.Width(42f)))
                                TravelTo(waypoint.pivot, waypoint.rotation, waypoint.size);
                            if (GUILayout.Button("Set", GUILayout.Width(42f)))
                                CaptureWaypoint(waypoint);
                            if (GUILayout.Button("↑", GUILayout.Width(24f)) && originalIndex > 0)
                                SwapWaypoints(originalIndex, originalIndex - 1);
                            if (GUILayout.Button("↓", GUILayout.Width(24f)) && originalIndex < _waypoints.Count - 1)
                                SwapWaypoints(originalIndex, originalIndex + 1);
                            if (GUILayout.Button("X", GUILayout.Width(24f)))
                            {
                                _waypoints.Remove(waypoint);
                                SaveWaypoints();
                                GUIUtility.ExitGUI();
                            }
                        }
                    }
                }
            }
        }

        private void DrawTrackingPanel()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple)))
            {
                UtilityWindowTheme.SectionTitle("Conditional Object Tracking", UtilityWindowTheme.Purple, _followEnabled ? "Active" : "Manual");
                using (new EditorGUILayout.HorizontalScope())
                {
                    _followEnabled = GUILayout.Toggle(_followEnabled, "Follow Best Match", EditorStyles.toolbarButton, GUILayout.Width(132f));
                    _trackingMode = (TrackingMode)EditorGUILayout.EnumPopup(_trackingMode, GUILayout.Width(178f));
                    if (GUILayout.Button("Add Selected", GUILayout.Width(96f)))
                        AddSelectedTrackingRule();
                    if (GUILayout.Button("Focus Now", GUILayout.Width(82f)))
                        FocusBestTrackingMatch(true);
                }

                EditorGUILayout.HelpBox("Rules evaluate highest-priority first. Use operators for bools, numbers, enums, strings, object references, and GameObject/component state.", MessageType.Info);

                for (int i = 0; i < _trackingRules.Count; i++)
                    DrawTrackingRule(_trackingRules[i], i);
            }
        }

        private void DrawTrackingRule(TrackingRule rule, int index)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.12f, 0.06f, 5, 3)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    rule.enabled = GUILayout.Toggle(rule.enabled, GUIContent.none, GUILayout.Width(18f));
                    rule.name = EditorGUILayout.TextField(rule.name);
                    rule.priority = EditorGUILayout.IntField(rule.priority, GUILayout.Width(44f));
                    if (GUILayout.Button("Focus", GUILayout.Width(56f)))
                        FocusRule(rule, true);
                    if (GUILayout.Button("X", GUILayout.Width(24f)))
                    {
                        _trackingRules.RemoveAt(index);
                        GUIUtility.ExitGUI();
                    }
                }

                rule.target = (GameObject)EditorGUILayout.ObjectField("Target", rule.target, typeof(GameObject), true);
                rule.component = (Component)EditorGUILayout.ObjectField("Component", rule.component, typeof(Component), true);
                DrawMemberPicker(rule);
                rule.op = (TrackingOperator)EditorGUILayout.EnumPopup("Operator", rule.op);
                using (new EditorGUI.DisabledScope(rule.op == TrackingOperator.Always || rule.op == TrackingOperator.IsActive || rule.op == TrackingOperator.ComponentEnabled || rule.op == TrackingOperator.IsNull || rule.op == TrackingOperator.IsNotNull))
                    rule.expectedValue = EditorGUILayout.TextField("Expected", rule.expectedValue);
                rule.focusSize = EditorGUILayout.FloatField("Focus Size", Mathf.Max(0.1f, rule.focusSize));
                rule.margin = EditorGUILayout.FloatField("Refocus Margin", Mathf.Max(0f, rule.margin));
            }
        }

        private static void DrawMemberPicker(TrackingRule rule)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                rule.fieldOrProperty = EditorGUILayout.TextField("Field/Property", rule.fieldOrProperty);
                if (GUILayout.Button("Pick", GUILayout.Width(48f)))
                {
                    GenericMenu menu = new GenericMenu();
                    Component component = rule.component;
                    if (component == null && rule.target != null)
                        component = rule.target.GetComponent<Component>();
                    if (component != null)
                    {
                        Type type = component.GetType();
                        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                        foreach (FieldInfo field in type.GetFields(flags).Where(f => IsUsefulMemberType(f.FieldType)).OrderBy(f => f.Name))
                        {
                            string name = field.Name;
                            menu.AddItem(new GUIContent(name), name == rule.fieldOrProperty, () => rule.fieldOrProperty = name);
                        }
                        foreach (PropertyInfo property in type.GetProperties(flags).Where(p => p.GetIndexParameters().Length == 0 && IsUsefulMemberType(p.PropertyType)).OrderBy(p => p.Name))
                        {
                            string name = property.Name;
                            menu.AddItem(new GUIContent(name), name == rule.fieldOrProperty, () => rule.fieldOrProperty = name);
                        }
                    }
                    else
                    {
                        menu.AddDisabledItem(new GUIContent("Assign a component first"));
                    }
                    menu.ShowAsContext();
                }
            }
        }

        private static bool IsUsefulMemberType(Type type)
        {
            return type == typeof(bool) || type == typeof(int) || type == typeof(float) || type == typeof(double) || type == typeof(string) || type.IsEnum || typeof(UnityEngine.Object).IsAssignableFrom(type) || type == typeof(Vector2) || type == typeof(Vector3);
        }

        private void AddSelectedTrackingRule()
        {
            GameObject go = Selection.activeGameObject;
            if (go == null)
            {
                _status = "Select a GameObject first.";
                return;
            }

            _trackingRules.Add(new TrackingRule
            {
                name = go.name,
                target = go,
                component = go.GetComponent<Component>(),
                priority = _trackingRules.Count,
                fieldOrProperty = string.Empty,
                op = TrackingOperator.IsActive,
                expectedValue = "true"
            });
        }

        private void EditorUpdate()
        {
            if (!_followEnabled || EditorApplication.timeSinceStartup < _nextFollowTime)
                return;

            _nextFollowTime = EditorApplication.timeSinceStartup + FollowInterval;
            FocusBestTrackingMatch(false);
        }

        private void FocusBestTrackingMatch(bool force)
        {
            TrackingRule best = _trackingRules
                .Where(r => r != null && r.enabled && RuleMatches(r))
                .OrderByDescending(r => r.priority)
                .FirstOrDefault();

            if (best != null)
                FocusRule(best, force);
            else if (force)
                _status = "No tracking rule currently matches.";
        }

        private void FrameTrackingMatches()
        {
            Transform[] matches = _trackingRules.Where(r => r.enabled && r.target != null && RuleMatches(r)).Select(r => r.target.transform).Distinct().ToArray();
            FrameSelection(matches);
            _status = matches.Length == 0 ? "No matching tracked objects." : "Framed " + matches.Length + " tracked object(s).";
        }

        private void FocusRule(TrackingRule rule, bool force)
        {
            if (rule == null || rule.target == null)
                return;

            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
                return;

            Vector3 targetPivot = rule.target.transform.position;
            bool outsideMargin = Vector3.Distance(sceneView.pivot, targetPivot) > Mathf.Max(rule.margin, 0.01f);
            if (!force && _trackingMode == TrackingMode.ManualOnly)
                return;
            if (!force && _trackingMode == TrackingMode.RefocusWhenOutsideMargin && !outsideMargin)
                return;

            Vector3 pivot = _trackingMode == TrackingMode.SoftFollow && !force ? Vector3.Lerp(sceneView.pivot, targetPivot, 0.25f) : targetPivot;
            TravelTo(pivot, sceneView.rotation, Mathf.Max(0.1f, rule.focusSize));
            _status = "Tracking " + rule.target.name + ".";
        }

        private static bool RuleMatches(TrackingRule rule)
        {
            if (rule.target == null)
                return false;

            if (rule.op == TrackingOperator.Always)
                return true;
            if (rule.op == TrackingOperator.IsActive)
                return rule.target.activeInHierarchy;
            if (rule.op == TrackingOperator.ComponentEnabled)
                return rule.component is Behaviour behaviour ? behaviour.enabled : rule.component != null;

            object owner = rule.component != null ? (object)rule.component : rule.target;
            if (string.IsNullOrWhiteSpace(rule.fieldOrProperty) || !TryGetMemberValue(owner, rule.fieldOrProperty, out object value))
                return false;

            return ValueMatches(value, rule.op, rule.expectedValue);
        }

        private static bool ValueMatches(object value, TrackingOperator op, string expected)
        {
            if (op == TrackingOperator.IsNull)
                return value == null || (value is UnityEngine.Object unityObj && unityObj == null);
            if (op == TrackingOperator.IsNotNull)
                return value != null && (!(value is UnityEngine.Object unityObj) || unityObj != null);
            if (value == null)
                return false;

            string actual = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
            switch (op)
            {
                case TrackingOperator.Equals:
                    return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
                case TrackingOperator.NotEquals:
                    return !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
                case TrackingOperator.Contains:
                    return actual.IndexOf(expected ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0;
                case TrackingOperator.GreaterThan:
                    return TryFloat(actual, out float a) && TryFloat(expected, out float greaterThreshold) && a > greaterThreshold;
                case TrackingOperator.LessThan:
                    return TryFloat(actual, out float lessActual) && TryFloat(expected, out float lessThreshold) && lessActual < lessThreshold;
                default:
                    return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static bool TryFloat(string value, out float result)
        {
            return float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result);
        }

        private static bool TryGetMemberValue(object owner, string memberPath, out object value)
        {
            value = null;
            object current = owner;
            foreach (string part in memberPath.Split('.'))
            {
                if (current == null)
                    return false;
                Type type = current.GetType();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                FieldInfo field = type.GetField(part, flags);
                if (field != null)
                {
                    current = field.GetValue(current);
                    continue;
                }
                PropertyInfo property = type.GetProperty(part, flags);
                if (property != null && property.GetIndexParameters().Length == 0)
                {
                    current = property.GetValue(current, null);
                    continue;
                }
                return false;
            }
            value = current;
            return true;
        }

        private void AddWaypointFromSceneView()
        {
            Waypoint waypoint = new Waypoint
            {
                name = string.IsNullOrWhiteSpace(_newWaypointName) ? "Waypoint" : _newWaypointName,
                group = string.IsNullOrWhiteSpace(_newWaypointGroup) ? "Default" : _newWaypointGroup
            };
            CaptureWaypoint(waypoint);
            _waypoints.Add(waypoint);
            SaveWaypoints();
            _status = "Saved waypoint " + waypoint.name + ".";
        }

        private void SwapWaypoints(int a, int b)
        {
            Waypoint temp = _waypoints[a];
            _waypoints[a] = _waypoints[b];
            _waypoints[b] = temp;
            SaveWaypoints();
        }

        private static void CaptureWaypoint(Waypoint waypoint)
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null || waypoint == null)
                return;
            waypoint.pivot = sceneView.pivot;
            waypoint.rotation = sceneView.rotation;
            waypoint.size = sceneView.size;
        }

        private static void FrameSelection(Transform[] transforms)
        {
            if (transforms == null || transforms.Length == 0)
                return;

            Bounds bounds = new Bounds(transforms[0].position, Vector3.zero);
            foreach (Transform t in transforms)
            {
                if (t == null)
                    continue;
                Renderer renderer = t.GetComponentInChildren<Renderer>();
                if (renderer != null)
                    bounds.Encapsulate(renderer.bounds);
                else
                    bounds.Encapsulate(t.position);
            }

            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
                return;

            sceneView.LookAt(bounds.center, sceneView.rotation, Mathf.Max(bounds.extents.magnitude * 1.8f, 1f));
            sceneView.Repaint();
        }

        private static Vector3 GetScenePivot() => SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
        private static Quaternion GetSceneRotation() => SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.rotation : Quaternion.identity;
        private static float GetSceneSize() => SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.size : 10f;

        private static void TravelTo(Vector3 pivot, Quaternion rotation, float size)
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
                return;
            sceneView.LookAt(pivot, rotation, Mathf.Max(0.1f, size));
            sceneView.Repaint();
        }

        private void DuringSceneGUI(SceneView sceneView)
        {
            if (!_drawOverlay || sceneView == null)
                return;

            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(10f, 10f, 260f, 86f), GUI.skin.box);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Frame Sel", EditorStyles.miniButton, GUILayout.Width(68f)))
                    FrameSelection(Selection.transforms);
                if (GUILayout.Button("Save View", EditorStyles.miniButton, GUILayout.Width(72f)))
                    AddWaypointFromSceneView();
                _followEnabled = GUILayout.Toggle(_followEnabled, "Follow", EditorStyles.miniButton, GUILayout.Width(58f));
            }
            string[] groups = new[] { "All" }.Concat(_waypoints.Select(w => string.IsNullOrWhiteSpace(w.group) ? "Default" : w.group).Distinct()).ToArray();
            int index = Mathf.Max(0, Array.IndexOf(groups, _activeGroup));
            int next = EditorGUILayout.Popup(index, groups);
            _activeGroup = groups[Mathf.Clamp(next, 0, groups.Length - 1)];
            if (_waypoints.Count > 0)
            {
                Waypoint first = _waypoints.FirstOrDefault(w => _activeGroup == "All" || string.Equals(w.group, _activeGroup, StringComparison.OrdinalIgnoreCase));
                if (first != null && GUILayout.Button("Go: " + first.name, EditorStyles.miniButton))
                    TravelTo(first.pivot, first.rotation, first.size);
            }
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        private void LoadWaypoints()
        {
            _waypoints.Clear();
            string json = EditorPrefs.GetString(PrefWaypoints, string.Empty);
            if (string.IsNullOrEmpty(json))
                return;
            WaypointStore store = JsonUtility.FromJson<WaypointStore>(json);
            if (store?.waypoints != null)
                _waypoints.AddRange(store.waypoints);
        }

        private void SaveWaypoints()
        {
            EditorPrefs.SetString(PrefWaypoints, JsonUtility.ToJson(new WaypointStore { waypoints = _waypoints }));
        }
    }
    #endif

}