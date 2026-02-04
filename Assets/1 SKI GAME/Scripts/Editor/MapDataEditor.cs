#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using SkiGame.Map;
using SkiGame.POI;

[CustomEditor(typeof(MapData))]
public sealed class MapDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Authoring Helpers", EditorStyles.boldLabel);

        if (GUILayout.Button("Sync Marker Colours From PointOfInterestRegistry (open scene)"))
        {
            var reg = Object.FindObjectOfType<PointOfInterestRegistry>();
            if (reg == null)
            {
                EditorUtility.DisplayDialog("No Registry Found",
                    "No PointOfInterestRegistry exists in the currently open scene.", "OK");
                return;
            }

            reg.Refresh();

            var so = serializedObject;
            var markersProp = so.FindProperty("markers");
            if (markersProp == null || !markersProp.isArray)
                return;

            Undo.RecordObject(target, "Sync Marker Colours");

            int changed = 0;

            for (int i = 0; i < markersProp.arraySize; i++)
            {
                var m = markersProp.GetArrayElementAtIndex(i);
                var idProp = m.FindPropertyRelative("id");
                var colProp = m.FindPropertyRelative("color");

                string id = idProp != null ? idProp.stringValue : null;
                if (string.IsNullOrWhiteSpace(id)) continue;

                if (reg.TryGetById(id, out var info))
                {
                    if (colProp != null && colProp.colorValue != info.color)
                    {
                        colProp.colorValue = info.color;
                        changed++;
                    }
                }
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);

            Debug.Log($"[MapDataEditor] Synced {changed} marker colours from POI registry.");
        }
    }
}
#endif
