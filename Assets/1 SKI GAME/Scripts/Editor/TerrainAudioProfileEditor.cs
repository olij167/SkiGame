#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TerrainAudioMaterialProfileSO))]
public sealed class TerrainAudioProfileEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("layerBindings"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("fallbackMaterial"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("blendHysteresis"));

        TerrainAudioMaterialProfileSO profile = (TerrainAudioMaterialProfileSO)target;
        HashSet<TerrainLayer> seen = new HashSet<TerrainLayer>();
        bool hasDuplicate = false;
        bool hasMissing = false;

        foreach (TerrainLayerAudioBinding binding in profile.LayerBindings)
        {
            if (binding.layer == null || binding.material == null)
                hasMissing = true;

            if (binding.layer != null && !seen.Add(binding.layer))
                hasDuplicate = true;
        }

        if (hasMissing)
            EditorGUILayout.HelpBox("One or more terrain layer bindings are incomplete.", MessageType.Warning);

        if (hasDuplicate)
            EditorGUILayout.HelpBox("Duplicate terrain layer entries found. Only the first matching binding will resolve.", MessageType.Warning);

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
