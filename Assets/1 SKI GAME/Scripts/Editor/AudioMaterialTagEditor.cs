#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AudioMaterialTag))]
public sealed class AudioMaterialTagEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("surfaceMaterial"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("role"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("applyToChildren"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("includeInactiveChildren"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("colliderSpecificOverride"));

        AudioMaterialTag tag = (AudioMaterialTag)target;
        if (tag.SurfaceMaterial == null && tag.ColliderSpecificOverride == null)
            EditorGUILayout.HelpBox("No audio material is assigned yet.", MessageType.Warning);

        if (tag.GetComponent<Collider>() == null && !tag.ApplyToChildrenEnabled)
            EditorGUILayout.HelpBox("This object has no collider. Enable child propagation if this is meant to author child colliders.", MessageType.Info);

        if (tag.ApplyToChildrenEnabled)
        {
            EditorGUILayout.Space();
            if (GUILayout.Button("Apply To Child Colliders"))
            {
                Undo.RegisterFullObjectHierarchyUndo(tag.gameObject, "Apply Audio Material To Child Colliders");
                tag.ApplyToChildColliders();
                EditorUtility.SetDirty(tag.gameObject);
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
