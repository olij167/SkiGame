#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using SkiGame.POI;

[CustomEditor(typeof(PointOfInterestRegistry))]
public sealed class PointOfInterestRegistryEditor : Editor
{
    private bool _placing;
    private string _newName = "POI";
    private Color _newColor = Color.yellow;
    private LayerMask _mask = ~0;

    private string _selectedCustomId;

    // Inspector UI state
    private bool _showDiscovered = true;
    private bool _groupByType = true;
    private Vector2 _discoveredScroll;

    // Editor throttling (avoid rebuilding cache every repaint)
    private double _lastInspectorRefreshTime;

    // Scene picking controls
    private float _selectNearestRadius = 25f;
    private float _raycastMaxDistance = 50000f;

    private void OnEnable()
    {
        SceneView.duringSceneGui += DuringSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DuringSceneGUI;
    }

    public override void OnInspectorGUI()
    {
        var reg = target as PointOfInterestRegistry;
        if (reg == null) return;

        // Draw all serialized fields (Discovery, Gizmos, Custom POIs, Lift POI Identity, etc.)
        DrawDefaultInspector();

        // Keep cache fresh enough that the discovered list is useful.
        // (Cache is non-serialized, so inspector must query and display it explicitly.)
        ThrottledRefresh(reg, 0.5d);

        EditorGUILayout.Space(10);
        DrawCustomPlacementTool(reg);

        EditorGUILayout.Space(12);
        DrawDiscoveredPOIs(reg);
    }

    private void DrawCustomPlacementTool(PointOfInterestRegistry reg)
    {
        EditorGUILayout.LabelField("Custom POI Tool", EditorStyles.boldLabel);

        _newName = EditorGUILayout.TextField("Name", _newName);
        _newColor = EditorGUILayout.ColorField("Color", _newColor);

        _mask = LayerMaskField("Raycast Mask", _mask);
        _selectNearestRadius = EditorGUILayout.FloatField(new GUIContent("Select Radius", "Ctrl+Click selects nearest custom POI within this radius."), _selectNearestRadius);
        _raycastMaxDistance = EditorGUILayout.FloatField(new GUIContent("Raycast Max Distance", "Max distance for raycasts used by the tool."), _raycastMaxDistance);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.backgroundColor = _placing ? new Color(0.7f, 1f, 0.7f) : Color.white;
            if (GUILayout.Button(_placing ? "Placing: ON" : "Placing: OFF", GUILayout.Height(26)))
                _placing = !_placing;
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Refresh Now", GUILayout.Height(26)))
            {
                reg.Refresh();
                EditorUtility.SetDirty(reg);
                SceneView.RepaintAll();
                Repaint();
            }
        }

        EditorGUILayout.HelpBox(
            "Scene Controls (when Placing is ON):\n" +
            "- Left Click: add custom POI at raycast hit\n" +
            "- Shift + Left Click: add without changing selection\n" +
            "- Ctrl + Left Click: select nearest custom POI\n" +
            "- Delete: remove selected custom POI\n" +
            "- Move tool: use position handle on selected custom POI\n",
            MessageType.Info);
    }

    private void DrawDiscoveredPOIs(PointOfInterestRegistry reg)
    {
        _showDiscovered = EditorGUILayout.Foldout(_showDiscovered, "Discovered POIs (Read Only)", true);
        if (!_showDiscovered) return;

        using (new EditorGUILayout.HorizontalScope())
        {
            _groupByType = EditorGUILayout.ToggleLeft(new GUIContent("Group By Type", "Groups POIs by category for easier scanning."), _groupByType, GUILayout.Width(140));

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Force Refresh", GUILayout.Width(110)))
            {
                reg.Refresh();
                SceneView.RepaintAll();
                Repaint();
            }
        }

        var list = reg.Current;
        int total = list != null ? list.Count : 0;

        int runs = 0, lifts = 0, customs = 0, other = 0;
        if (list != null)
        {
            for (int i = 0; i < list.Count; i++)
            {
                switch (list[i].type)
                {
                    case POIType.SkiRun: runs++; break;
                    case POIType.SkiLift: lifts++; break;
                    case POIType.Custom: customs++; break;
                    default: other++; break;
                }
            }
        }

        EditorGUILayout.LabelField($"Total: {total}   Runs: {runs}   Lifts: {lifts}   Custom: {customs}   Other: {other}");

        if (total == 0)
        {
            EditorGUILayout.HelpBox(
                "No discovered POIs in cache.\n" +
                "- Ensure the scene contains SkiRunLine and/or LiftLine objects.\n" +
                "- Ensure Discovery toggles are enabled.\n" +
                "- Click Force Refresh.",
                MessageType.Warning);
            return;
        }

        _discoveredScroll = EditorGUILayout.BeginScrollView(_discoveredScroll, GUILayout.MinHeight(160));

        if (_groupByType)
        {
            DrawPOIGroup(reg, list, POIType.SkiRun, "Ski Runs");
            DrawPOIGroup(reg, list, POIType.SkiLift, "Ski Lifts (Top/Bottom markers)");
            DrawPOIGroup(reg, list, POIType.Custom, "Custom POIs");
            DrawPOIGroup(reg, list, POIType.Unknown, "Other / Unknown", includeNonMatching: false);
        }
        else
        {
            for (int i = 0; i < list.Count; i++)
                DrawPOIRow(reg, list[i]);
        }

        EditorGUILayout.EndScrollView();
    }

    private static void DrawPOIGroup(PointOfInterestRegistry reg, IReadOnlyList<POIInfo> list, POIType type, string title, bool includeNonMatching = false)
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

        bool any = false;
        for (int i = 0; i < list.Count; i++)
        {
            bool match = list[i].type == type;
            if (!includeNonMatching && !match) continue;
            if (includeNonMatching && match) continue;

            any = true;
            DrawPOIRow(reg, list[i]);
        }

        if (!any)
            EditorGUILayout.LabelField("  (none)");
    }

    private static void DrawPOIRow(PointOfInterestRegistry reg, POIInfo poi)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            // Color chip
            Color prev = GUI.color;
            GUI.color = poi.color;
            GUILayout.Box(GUIContent.none, GUILayout.Width(14), GUILayout.Height(14));
            GUI.color = prev;

            // Main label
            EditorGUILayout.LabelField($"{poi.type}: {poi.displayName}", GUILayout.MinWidth(220));

            // Meta (difficulty, lift info, etc.)
            if (!string.IsNullOrWhiteSpace(poi.meta))
                EditorGUILayout.LabelField(poi.meta, GUILayout.MinWidth(140));

            // ID (short)
            string idShort = (poi.id != null && poi.id.Length > 10) ? poi.id.Substring(0, 10) : poi.id;
            EditorGUILayout.LabelField(idShort ?? "", GUILayout.Width(90));

            // Ping/select source object if present
            if (poi.source != null)
            {
                if (GUILayout.Button("Ping", GUILayout.Width(45)))
                    EditorGUIUtility.PingObject(poi.source);

                if (GUILayout.Button("Select", GUILayout.Width(55)))
                    Selection.activeObject = poi.source;
            }
            else
            {
                GUILayout.Space(106);
            }

            // Frame scene view on this POI
            if (GUILayout.Button("Frame", GUILayout.Width(55)))
            {
                SceneView.lastActiveSceneView?.LookAt(poi.position);
            }
        }
    }

    private void DuringSceneGUI(SceneView view)
    {
        var reg = target as PointOfInterestRegistry;
        if (reg == null) return;

        Event e = Event.current;
        if (e == null) return;

        // Ctrl+Click to select nearest custom POI
        if (e.control && e.type == EventType.MouseDown && e.button == 0)
        {
            if (TryGetHit(e.mousePosition, out var hit))
            {
                if (reg.TryFindNearest(hit.point, _selectNearestRadius, out var nearest) && nearest.type == POIType.Custom)
                {
                    _selectedCustomId = nearest.id;
                    e.Use();
                    SceneView.RepaintAll();
                }
            }
        }

        // Delete selected custom POI
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Delete)
        {
            if (!string.IsNullOrWhiteSpace(_selectedCustomId))
            {
                Undo.RecordObject(reg, "Delete Custom POI");
                reg.RemoveCustomById(_selectedCustomId);
                _selectedCustomId = null;
                EditorUtility.SetDirty(reg);
                e.Use();
            }
        }

        // Placement
        if (_placing && e.type == EventType.MouseDown && e.button == 0 && !e.control)
        {
            if (TryGetHit(e.mousePosition, out var hit))
            {
                Undo.RecordObject(reg, "Add Custom POI");
                reg.AddCustom(_newName, hit.point, _newColor);
                EditorUtility.SetDirty(reg);

                if (!e.shift)
                {
                    // Select the registry so the move handle can operate
                    Selection.activeObject = reg;
                }

                e.Use();
            }
        }

        // Draw & handle position for selected custom
        if (!string.IsNullOrWhiteSpace(_selectedCustomId))
        {
            if (reg.TryGetById(_selectedCustomId, out var info) && info.type == POIType.Custom)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 newPos = Handles.PositionHandle(info.position, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(reg, "Move Custom POI");
                    reg.TryMoveCustom(_selectedCustomId, newPos);
                    EditorUtility.SetDirty(reg);
                }

                Handles.Label(info.position + Vector3.up * 2f, $"Custom POI: {info.displayName}");
            }
        }

        if (_placing)
        {
            // Prevent scene selection while placing
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        }
    }

    private void ThrottledRefresh(PointOfInterestRegistry reg, double seconds)
    {
        double now = EditorApplication.timeSinceStartup;
        if (now - _lastInspectorRefreshTime < seconds) return;
        _lastInspectorRefreshTime = now;

        try
        {
            reg.Refresh();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    private bool TryGetHit(Vector2 mousePos, out RaycastHit hit)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);
        return Physics.Raycast(ray, out hit, Mathf.Max(1f, _raycastMaxDistance), _mask, QueryTriggerInteraction.Ignore);
    }

    private static LayerMask LayerMaskField(string label, LayerMask selected)
    {
        int mask = EditorGUILayout.MaskField(label, selected.value, GetLayerNames());
        selected.value = mask;
        return selected;
    }

    private static string[] GetLayerNames()
    {
        var layers = new List<string>();
        for (int i = 0; i < 32; i++)
        {
            string n = LayerMask.LayerToName(i);
            if (!string.IsNullOrEmpty(n))
                layers.Add(n);
        }
        return layers.ToArray();
    }
}
#endif
