using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public class TerrainAlignTool : MonoBehaviour
{
    // Optional runtime marker component.
    // You do not need to attach this to anything for the editor tool to work.
}

#if UNITY_EDITOR
public class TerrainAlignToolWindow : EditorWindow
{
    private bool autoAlign = true;
    private bool alignRotationToNormal = true;
    private bool preserveYaw = true;
    private float heightOffset = 0f;
    private LayerMask raycastMask = ~0;
    private float rayStartHeight = 1000f;
    private float rayDistance = 5000f;

    [MenuItem("Tools/Ski Game/Terrain Align Tool")]
    public static void ShowWindow()
    {
        GetWindow<TerrainAlignToolWindow>("Terrain Align");
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        Undo.undoRedoPerformed += Repaint;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        Undo.undoRedoPerformed -= Repaint;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Selection Terrain Alignment", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        autoAlign = EditorGUILayout.Toggle("Auto Align While Moving", autoAlign);
        alignRotationToNormal = EditorGUILayout.Toggle("Align Rotation To Normal", alignRotationToNormal);
        preserveYaw = EditorGUILayout.Toggle("Preserve Yaw", preserveYaw);
        heightOffset = EditorGUILayout.FloatField("Height Offset", heightOffset);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Raycast Settings", EditorStyles.boldLabel);
        raycastMask = LayerMaskField("Raycast Mask", raycastMask);
        rayStartHeight = EditorGUILayout.FloatField("Ray Start Height", rayStartHeight);
        rayDistance = EditorGUILayout.FloatField("Ray Distance", rayDistance);

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(Selection.transforms == null || Selection.transforms.Length == 0))
        {
            if (GUILayout.Button("Align Selected Now"))
            {
                AlignSelectionNow();
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Select one or more scene objects. Move them in the Scene view and they will snap to the terrain or any collider in the raycast mask.",
            MessageType.Info
        );
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!autoAlign)
            return;

        Transform[] selected = Selection.transforms;
        if (selected == null || selected.Length == 0)
            return;

        Event e = Event.current;
        if (e == null)
            return;

        // Only react while dragging scene handles / moving objects.
        bool shouldUpdate =
            e.type == EventType.MouseDrag ||
            e.type == EventType.KeyUp ||
            e.type == EventType.MouseUp;

        if (!shouldUpdate)
            return;

        bool changedAny = false;

        foreach (Transform t in selected)
        {
            if (t == null)
                continue;

            if (TryAlignTransform(t))
                changedAny = true;
        }

        if (changedAny)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            SceneView.RepaintAll();
        }
    }

    private void AlignSelectionNow()
    {
        Transform[] selected = Selection.transforms;
        if (selected == null || selected.Length == 0)
            return;

        Undo.RecordObjects(selected, "Align To Terrain");

        bool changedAny = false;
        foreach (Transform t in selected)
        {
            if (t == null)
                continue;

            if (TryAlignTransform(t))
                changedAny = true;
        }

        if (changedAny)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
    }

    private bool TryAlignTransform(Transform t)
    {
        Vector3 start = t.position + Vector3.up * rayStartHeight;
        Ray ray = new Ray(start, Vector3.down);

        if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance, raycastMask, QueryTriggerInteraction.Ignore))
            return false;

        Undo.RecordObject(t, "Align To Terrain");

        Vector3 newPos = hit.point + hit.normal * heightOffset;
        t.position = newPos;

        if (alignRotationToNormal)
        {
            Quaternion targetRotation;

            if (preserveYaw)
            {
                Vector3 projectedForward = Vector3.ProjectOnPlane(t.forward, hit.normal);
                if (projectedForward.sqrMagnitude < 0.0001f)
                {
                    projectedForward = Vector3.ProjectOnPlane(Vector3.forward, hit.normal);
                }

                targetRotation = Quaternion.LookRotation(projectedForward.normalized, hit.normal);
            }
            else
            {
                targetRotation = Quaternion.FromToRotation(t.up, hit.normal) * t.rotation;
            }

            t.rotation = targetRotation;
        }

        EditorUtility.SetDirty(t);
        return true;
    }

    private LayerMask LayerMaskField(string label, LayerMask selected)
    {
        var layers = UnityEditorInternal.InternalEditorUtility.layers;
        int[] layerNumbers = new int[layers.Length];

        for (int i = 0; i < layers.Length; i++)
            layerNumbers[i] = LayerMask.NameToLayer(layers[i]);

        int maskWithoutEmpty = 0;
        for (int i = 0; i < layerNumbers.Length; i++)
        {
            if (((1 << layerNumbers[i]) & selected.value) > 0)
                maskWithoutEmpty |= 1 << i;
        }

        maskWithoutEmpty = EditorGUILayout.MaskField(label, maskWithoutEmpty, layers);

        int mask = 0;
        for (int i = 0; i < layerNumbers.Length; i++)
        {
            if ((maskWithoutEmpty & (1 << i)) > 0)
                mask |= 1 << layerNumbers[i];
        }

        selected.value = mask;
        return selected;
    }
}
#endif