#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class AudioMaterialAssignmentWindow : EditorWindow
{
    private sealed class SuggestionRow
    {
        public Collider collider;
        public AudioSurfaceMaterialSO suggestion;
    }

    private readonly List<SuggestionRow> _rows = new List<SuggestionRow>();
    private AudioMaterialLibrarySO _library;
    private Vector2 _scroll;

    [MenuItem("Tools/Ski Game/Audio Material Assignment")]
    public static void Open()
    {
        GetWindow<AudioMaterialAssignmentWindow>("Audio Material Assignment");
    }

    private void OnGUI()
    {
        _library = (AudioMaterialLibrarySO)EditorGUILayout.ObjectField("Material Library", _library, typeof(AudioMaterialLibrarySO), false);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Scan Scene"))
                Scan(false);
            if (GUILayout.Button("Scan Selection"))
                Scan(true);
        }

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        foreach (SuggestionRow row in _rows)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.ObjectField(row.collider, typeof(Collider), true);
                row.suggestion = (AudioSurfaceMaterialSO)EditorGUILayout.ObjectField(row.suggestion, typeof(AudioSurfaceMaterialSO), false);
                if (GUILayout.Button("Apply", GUILayout.Width(70f)))
                    ApplySuggestion(row);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void Scan(bool selectionOnly)
    {
        _rows.Clear();
        Collider[] colliders = selectionOnly
            ? GetSelectionColliders()
            : Object.FindObjectsOfType<Collider>(true);

        foreach (Collider collider in colliders)
        {
            if (collider == null || collider.GetComponent<AudioMaterialTag>() != null)
                continue;

            _rows.Add(new SuggestionRow
            {
                collider = collider,
                suggestion = SuggestMaterial(collider)
            });
        }
    }

    private Collider[] GetSelectionColliders()
    {
        List<Collider> colliders = new List<Collider>();
        foreach (GameObject go in Selection.gameObjects)
            colliders.AddRange(go.GetComponentsInChildren<Collider>(true));
        return colliders.ToArray();
    }

    private AudioSurfaceMaterialSO SuggestMaterial(Collider collider)
    {
        if (_library == null)
            return null;

        string haystack = $"{collider.name} {collider.gameObject.tag} {LayerMask.LayerToName(collider.gameObject.layer)}".ToLowerInvariant();
        foreach (AudioSurfaceMaterialSO material in _library.GetAll())
        {
            if (material == null)
                continue;

            string nameLower = material.name.ToLowerInvariant();
            if (haystack.Contains(nameLower.Replace(" ", "")) || haystack.Contains(nameLower))
                return material;
        }

        return null;
    }

    private void ApplySuggestion(SuggestionRow row)
    {
        if (row.collider == null || row.suggestion == null)
            return;

        Undo.RegisterFullObjectHierarchyUndo(row.collider.gameObject, "Assign Audio Material Tag");
        AudioMaterialTag tag = row.collider.GetComponent<AudioMaterialTag>();
        if (tag == null)
            tag = row.collider.gameObject.AddComponent<AudioMaterialTag>();

        SerializedObject so = new SerializedObject(tag);
        so.FindProperty("surfaceMaterial").objectReferenceValue = row.suggestion;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(tag);
    }
}
#endif
