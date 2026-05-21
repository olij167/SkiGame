using PungentFunk.Utilities.SceneTools;
using UnityEditor;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.SceneTools
{
#if UNITY_EDITOR
    [CustomEditor(typeof(PungentSpatialAuthoringAsset))]
    internal sealed class PungentSpatialAuthoringAssetEditor : UnityEditor.Editor
    {
        private SerializedProperty _metadata;
        private SerializedProperty _projection;
        private SerializedProperty _backgroundTexture;
        private SerializedProperty _backgroundUvMin;
        private SerializedProperty _backgroundUvMax;
        private SerializedProperty _layers;
        private SerializedProperty _paths;
        private SerializedProperty _areas;
        private SerializedProperty _regionSets;
        private SerializedProperty _markers;
        private SerializedProperty _schemaVersion;
        private SerializedProperty _lastBackgroundBake;

        private int _tab;
        private readonly string[] _tabs = { "Document", "Plan Data", "Background", "Advanced" };

        private void OnEnable()
        {
            _metadata = serializedObject.FindProperty("metadata");
            _projection = serializedObject.FindProperty("projection");
            _backgroundTexture = serializedObject.FindProperty("backgroundTexture");
            _backgroundUvMin = serializedObject.FindProperty("backgroundUvMin");
            _backgroundUvMax = serializedObject.FindProperty("backgroundUvMax");
            _layers = serializedObject.FindProperty("layers");
            _paths = serializedObject.FindProperty("paths");
            _areas = serializedObject.FindProperty("areas");
            _regionSets = serializedObject.FindProperty("regionSets");
            _markers = serializedObject.FindProperty("markers");
            _schemaVersion = serializedObject.FindProperty("schemaVersion");
            _lastBackgroundBake = serializedObject.FindProperty("lastBackgroundBake");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Spatial Authoring Asset", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Open Workbench", GUILayout.Width(130f)))
                    PungentSpatialAuthoringWindow.OpenWithAsset((PungentSpatialAuthoringAsset)target);
            }

            _tab = GUILayout.Toolbar(_tab, _tabs);
            EditorGUILayout.Space(4f);

            switch (_tab)
            {
                case 0:
                    EditorGUILayout.PropertyField(_metadata, true);
                    EditorGUILayout.PropertyField(_projection, true);
                    EditorGUILayout.PropertyField(_layers, true);
                    break;
                case 1:
                    EditorGUILayout.PropertyField(_paths, true);
                    EditorGUILayout.PropertyField(_areas, true);
                    EditorGUILayout.PropertyField(_regionSets, true);
                    EditorGUILayout.PropertyField(_markers, true);
                    break;
                case 2:
                    EditorGUILayout.PropertyField(_backgroundTexture);
                    EditorGUILayout.PropertyField(_backgroundUvMin);
                    EditorGUILayout.PropertyField(_backgroundUvMax);
                    EditorGUILayout.PropertyField(_lastBackgroundBake, true);
                    break;
                case 3:
                    EditorGUILayout.PropertyField(_schemaVersion);
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.ObjectField("Asset", target, typeof(PungentSpatialAuthoringAsset), false);
                    break;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}
