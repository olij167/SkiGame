using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Editor.ProjectAudit;
using PungentFunk.Utilities.Audio;

namespace PungentFunk.Utilities.Editor.Audio
{
    #if UNITY_EDITOR
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(AudioMaterialTag))]
    [CanEditMultipleObjects]
    public sealed class AudioMaterialTagEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            AudioMaterialTag tag = target as AudioMaterialTag;

            DrawHeader(tag);
            DrawAssignmentProperties();
            DrawDiagnostics(tag);
            DrawActions(tag);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawHeader(AudioMaterialTag tag)
        {
            AudioSurfaceMaterialSO resolved = tag != null ? tag.GetAssignedMaterial() : null;
            Color tint = resolved != null ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint)))
            {
                UtilityWindowTheme.SectionTitle("Audio Material Tag", tint, resolved != null ? resolved.name : "Unassigned");
                EditorGUILayout.LabelField(
                    "Assigns a reusable audio surface material to colliders so contact, terrain, and interaction routing can resolve the correct sound response.",
                    UtilityWindowTheme.MutedMiniLabelStyle);

                if (resolved != null)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Resolved Material", UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(110f));
                        EditorGUILayout.ObjectField(resolved, typeof(AudioSurfaceMaterialSO), false);
                    }
                }
            }
        }

        private void DrawAssignmentProperties()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                UtilityWindowTheme.SectionTitle("Assignment", UtilityWindowTheme.Blue);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("surfaceMaterial"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("role"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("applyToChildren"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("includeInactiveChildren"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("colliderSpecificOverride"));
            }
        }

        private void DrawDiagnostics(AudioMaterialTag tag)
        {
            if (tag == null)
                return;

            bool hasCollider = tag.GetComponent<Collider>() != null;
            int childColliderCount = tag.GetComponentsInChildren<Collider>(tag.IncludeInactiveChildren).Length;
            bool hasResolvedMaterial = tag.GetAssignedMaterial() != null;

            Color tint = hasResolvedMaterial ? UtilityWindowTheme.Teal : UtilityWindowTheme.Amber;
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint)))
            {
                UtilityWindowTheme.SectionTitle("Diagnostics", tint, hasResolvedMaterial ? "ready" : "warning");

                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill(hasCollider ? "Collider" : "No Collider", hasCollider ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber);
                    UtilityWindowTheme.CountPill($"Child Colliders {Mathf.Max(0, childColliderCount - (hasCollider ? 1 : 0))}", UtilityWindowTheme.Blue);
                    UtilityWindowTheme.CountPill(hasResolvedMaterial ? "Material Assigned" : "Material Missing", hasResolvedMaterial ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber);
                }

                if (!hasResolvedMaterial)
                    EditorGUILayout.HelpBox("No audio material is assigned yet. Assign Surface Material or Collider Specific Override.", MessageType.Warning);

                if (!hasCollider && !tag.ApplyToChildrenEnabled)
                    EditorGUILayout.HelpBox("This object has no collider. Enable child propagation if this component is meant to author child colliders.", MessageType.Info);

                if (tag.ColliderSpecificOverride != null)
                    EditorGUILayout.HelpBox("Collider Specific Override is active and will be used instead of Surface Material.", MessageType.Info);
            }
        }

        private void DrawActions(AudioMaterialTag tag)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple)))
            {
                UtilityWindowTheme.SectionTitle("Coverage Tools", UtilityWindowTheme.Purple);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.TintedButton("Open Reference Scanner", UtilityWindowTheme.Blue, GUILayout.Height(22f)))
                        ReferenceAssignmentScannerWindow.Open();

                    if (UtilityWindowTheme.TintedButton("Open Setup Coverage", UtilityWindowTheme.Teal, GUILayout.Height(22f)))
                        AudioCoverageContextWindow.Open();
                }

                using (new EditorGUI.DisabledScope(tag == null || !tag.ApplyToChildrenEnabled))
                {
                    if (UtilityWindowTheme.TintedButton("Apply To Child Colliders", UtilityWindowTheme.Green, GUILayout.Height(24f)))
                        ApplyToSelectedTags();
                }

                if (tag != null && !tag.ApplyToChildrenEnabled)
                    EditorGUILayout.LabelField("Enable Apply To Children to propagate this tag onto child collider objects.", UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void ApplyToSelectedTags()
        {
            serializedObject.ApplyModifiedProperties();

            for (int i = 0; i < targets.Length; i++)
            {
                AudioMaterialTag tag = targets[i] as AudioMaterialTag;
                if (tag == null || !tag.ApplyToChildrenEnabled)
                    continue;

                Undo.RegisterFullObjectHierarchyUndo(tag.gameObject, "Apply Audio Material To Child Colliders");
                tag.ApplyToChildColliders();
                MarkTagHierarchyDirty(tag);
            }

            serializedObject.Update();
        }

        private static void MarkTagHierarchyDirty(AudioMaterialTag root)
        {
            if (root == null)
                return;

            AudioMaterialTag[] tags = root.GetComponentsInChildren<AudioMaterialTag>(true);
            for (int i = 0; i < tags.Length; i++)
            {
                if (tags[i] != null)
                    EditorUtility.SetDirty(tags[i]);
            }

            EditorUtility.SetDirty(root.gameObject);
        }
    }
    #endif

}

