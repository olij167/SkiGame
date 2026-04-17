#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LiftLine))]
public class LiftLineEditor : Editor
{
    private LiftLine line;

    private const string PlaceHint = "Hold Shift + Left Click to place a manual support";
    private const string RemoveHint = "Hold Shift + Right Click to remove the clicked support";
    private const string AlignHint = "Use inspector buttons to align manual/all supports to terrain";

    private void OnEnable()
    {
        line = (LiftLine)target;
        SceneView.duringSceneGui += DuringSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DuringSceneGUI;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Manual Support Authoring", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            $"{PlaceHint}\n{RemoveHint}\n{AlignHint}",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Regenerate Supports"))
            {
                Undo.RegisterFullObjectHierarchyUndo(line.gameObject, "Regenerate Lift Supports");
                line.RegenerateIntermediateSupports();
                EditorUtility.SetDirty(line);
            }

            if (GUILayout.Button("Clear Generated"))
            {
                Undo.RegisterFullObjectHierarchyUndo(line.gameObject, "Clear Generated Lift Supports");
                line.ClearGeneratedIntermediateSupports();
                EditorUtility.SetDirty(line);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Align Manual To Terrain"))
            {
                Undo.RegisterFullObjectHierarchyUndo(line.gameObject, "Align Manual Supports To Terrain");
                line.AlignManualSupportsToTerrain();
                EditorUtility.SetDirty(line);
            }

            if (GUILayout.Button("Align All To Terrain"))
            {
                Undo.RegisterFullObjectHierarchyUndo(line.gameObject, "Align All Supports To Terrain");
                line.AlignAllSupportsToTerrain();
                EditorUtility.SetDirty(line);
            }
        }
    }

    private void DuringSceneGUI(SceneView sceneView)
    {
        if (line == null || Selection.activeGameObject != line.gameObject)
            return;

        Event e = Event.current;
        if (e == null)
            return;

        DrawSceneHints();

        bool shiftHeld = e.shift;
        if (!shiftHeld)
            return;

        int controlId = GUIUtility.GetControlID(FocusType.Passive);
        HandleUtility.AddDefaultControl(controlId);

        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

        // Prevent SceneView context menu when using Shift + Right Click authoring.
        if (e.type == EventType.ContextClick)
        {
            e.Use();
            return;
        }

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            if (TryGetTerrainHit(ray, out RaycastHit hit))
            {
                GUIUtility.hotControl = controlId;
                PlaceSupportAt(hit.point);
                e.Use();
            }
            return;
        }

        if (e.type == EventType.MouseDown && e.button == 1)
        {
            LiftSupportTower tower = PickSupportUnderCursor(ray);
            if (tower != null && tower.transform.IsChildOf(line.supportTowerContainer != null ? line.supportTowerContainer : line.transform))
            {
                GUIUtility.hotControl = controlId;
                RemoveSupport(tower);
                e.Use();
            }
            else
            {
                // Still consume it so the SceneView context menu does not open while authoring.
                GUIUtility.hotControl = controlId;
                e.Use();
            }
            return;
        }

        if (e.type == EventType.MouseUp && (e.button == 0 || e.button == 1))
        {
            if (GUIUtility.hotControl == controlId)
                GUIUtility.hotControl = 0;

            e.Use();
        }
    }

    private void DrawSceneHints()
    {
        Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(12f, 12f, 340f, 70f), GUI.skin.window);
        GUILayout.Label("Lift Support Authoring", EditorStyles.boldLabel);
        GUILayout.Label("Shift + Left Click: Place manual support");
        GUILayout.Label("Shift + Right Click: Remove clicked support");
        GUILayout.EndArea();
        Handles.EndGUI();
    }

    private bool TryGetTerrainHit(Ray ray, out RaycastHit hit)
    {
        return Physics.Raycast(
            ray,
            out hit,
            5000f,
            line.supportPlacementLayers,
            QueryTriggerInteraction.Ignore
        );
    }

    private void PlaceSupportAt(Vector3 point)
    {
        Undo.RegisterFullObjectHierarchyUndo(line.gameObject, "Place Lift Support");

        Vector3 forward = line.topStation != null && line.bottomStation != null
            ? (line.topStation.position - line.bottomStation.position)
            : Vector3.forward;

        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        Quaternion rot = Quaternion.LookRotation(forward.normalized, Vector3.up);
        LiftSupportTower tower = line.AddManualSupport(point + Vector3.up * line.manualTowerTerrainYOffset, rot);

        if (tower != null)
        {
            Undo.RegisterCreatedObjectUndo(tower.gameObject, "Create Lift Support");
            Selection.activeGameObject = line.gameObject;
            EditorUtility.SetDirty(line);
        }
    }

    private void RemoveSupport(LiftSupportTower tower)
    {
        if (tower == null)
            return;

        Undo.RegisterFullObjectHierarchyUndo(line.gameObject, "Remove Lift Support");
        Undo.DestroyObjectImmediate(tower.gameObject);

        line.supportTowers.Remove(tower);
        line.CacheSupportTowerEditorOnly();
        line.RebuildNow();
        EditorUtility.SetDirty(line);
    }

    private LiftSupportTower PickSupportUnderCursor(Ray ray)
    {
        float bestDistance = float.PositiveInfinity;
        LiftSupportTower best = null;

        line.CacheSupportTowerEditorOnly();

        for (int i = 0; i < line.supportTowers.Count; i++)
        {
            LiftSupportTower tower = line.supportTowers[i];
            if (tower == null)
                continue;

            Vector3 pos = tower.transform.position;
            float size = HandleUtility.GetHandleSize(pos) * 0.2f;
            float dist = HandleUtility.DistancePointLine(pos, ray.origin, ray.origin + ray.direction * 5000f);

            if (dist <= size && dist < bestDistance)
            {
                bestDistance = dist;
                best = tower;
            }
        }

        return best;
    }
}
#endif