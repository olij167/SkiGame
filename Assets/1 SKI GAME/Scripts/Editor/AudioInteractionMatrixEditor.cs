#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AudioInteractionMatrixSO))]
public sealed class AudioInteractionMatrixEditor : Editor
{
    private AudioMaterialLibrarySO _library;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("exactProfiles"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("categoryFallbacks"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultSoftProfile"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultHardProfile"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultScrapeProfile"));

        EditorGUILayout.Space();
        _library = (AudioMaterialLibrarySO)EditorGUILayout.ObjectField("Coverage Library", _library, typeof(AudioMaterialLibrarySO), false);
        if (_library == null)
            _library = TryFindLibrary();

        if (_library != null)
            DrawCoverage((AudioInteractionMatrixSO)target, _library);
        else
            EditorGUILayout.HelpBox("Assign an AudioMaterialLibrarySO to inspect pair coverage gaps.", MessageType.Info);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawCoverage(AudioInteractionMatrixSO matrix, AudioMaterialLibrarySO library)
    {
        IReadOnlyList<AudioSurfaceMaterialSO> materials = library.GetAll();
        List<string> missing = new List<string>();

        EditorGUILayout.LabelField("Coverage", EditorStyles.boldLabel);
        for (int i = 0; i < matrix.ExactProfiles.Count; i++)
        {
            AudioInteractionProfileSO profile = matrix.ExactProfiles[i];
            if (profile == null)
                continue;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"{profile.MaterialA?.name ?? "?"} x {profile.MaterialB?.name ?? "?"}");
                if (GUILayout.Button("Open", GUILayout.Width(60f)))
                    Selection.activeObject = profile;
            }
        }

        for (int i = 0; i < materials.Count; i++)
        {
            AudioSurfaceMaterialSO a = materials[i];
            if (a == null)
                continue;

            for (int j = i; j < materials.Count; j++)
            {
                AudioSurfaceMaterialSO b = materials[j];
                if (b == null)
                    continue;

                if (matrix.FindExact(a, b) != null || matrix.FindCategoryFallback(a, b) != null)
                    continue;

                missing.Add($"{a.name} x {b.name}");
            }
        }

        if (missing.Count > 0)
        {
            EditorGUILayout.HelpBox($"Missing pair coverage: {missing.Count}", MessageType.Warning);
            for (int i = 0; i < Mathf.Min(missing.Count, 20); i++)
                EditorGUILayout.LabelField($"• {missing[i]}");
        }
        else
        {
            EditorGUILayout.HelpBox("No pair coverage gaps found against the selected library.", MessageType.Info);
        }
    }

    private static AudioMaterialLibrarySO TryFindLibrary()
    {
        string[] guids = AssetDatabase.FindAssets("t:AudioMaterialLibrarySO");
        if (guids.Length == 0)
            return null;

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<AudioMaterialLibrarySO>(path);
    }
}
#endif
