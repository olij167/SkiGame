using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;

namespace PungentFunk.Utilities.Editor.PreviewExport
{
    #if UNITY_EDITOR
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Small generic front-end for exporting selected scene/generated objects as prefabs with embedded mesh assets.
    /// </summary>
    public sealed class PrefabAssetExporterWindow : EditorWindow
    {
        private const string PrefPrefix = "GenericUtility.PrefabAssetExporter.";
        private const string PrefSaveMeshes = PrefPrefix + "SaveMeshes";
        private const string PrefOnlyGeneratedMeshes = PrefPrefix + "OnlyGeneratedMeshes";
        private const string PrefIncludeDisabled = PrefPrefix + "IncludeDisabled";
        private const string PrefIncludeSkinned = PrefPrefix + "IncludeSkinned";
        private const string PrefConnect = PrefPrefix + "Connect";

        private bool _saveMeshes;
        private bool _onlyGeneratedMeshes;
        private bool _includeDisabled;
        private bool _includeSkinned;
        private bool _connect;
        private Vector2 _scroll;

        [MenuItem("Tools/Utilities/Prefabs/Prefab Asset Exporter", priority = 999)]
        public static void Open()
        {
            PrefabAssetExporterWindow window = GetWindow<PrefabAssetExporterWindow>("Prefab Exporter");
            window.minSize = new Vector2(400f, 320f);
            window.Show();
        }

        private void OnEnable()
        {
            _saveMeshes = UtilityWindowPrefs.GetBool(PrefSaveMeshes, true);
            _onlyGeneratedMeshes = UtilityWindowPrefs.GetBool(PrefOnlyGeneratedMeshes, true);
            _includeDisabled = UtilityWindowPrefs.GetBool(PrefIncludeDisabled, true);
            _includeSkinned = UtilityWindowPrefs.GetBool(PrefIncludeSkinned, true);
            _connect = UtilityWindowPrefs.GetBool(PrefConnect, false);
        }

        private void OnGUI()
        {
            UtilityWindowTheme.Header(
                "Prefab Asset Exporter",
                "Save a selected scene/generated object as a prefab and optionally duplicate generated meshes into sibling asset files.",
                Selection.activeGameObject != null ? Selection.activeGameObject.name : "no selection");

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                UtilityWindowTheme.SectionTitle("Selection", UtilityWindowTheme.Blue, Selection.activeGameObject != null ? "ready" : "missing");
                EditorGUILayout.ObjectField("Selected Root", Selection.activeGameObject, typeof(GameObject), true);
                EditorGUILayout.LabelField("Use this for generated props, combined scene objects, modular buildings, path-built objects, or any object whose runtime meshes should be made portable with the prefab.", UtilityWindowTheme.MutedMiniLabelStyle);
            }

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan)))
            {
                UtilityWindowTheme.SectionTitle("Export Options", UtilityWindowTheme.Cyan, "persistent");
                EditorGUI.BeginChangeCheck();
                _saveMeshes = EditorGUILayout.ToggleLeft(new GUIContent("Save Meshes As Assets", "Duplicate eligible meshes into a sibling _Meshes folder and connect the exported prefab to those saved assets."), _saveMeshes);
                using (new EditorGUI.DisabledScope(!_saveMeshes))
                {
                    _onlyGeneratedMeshes = EditorGUILayout.ToggleLeft(new GUIContent("Only Duplicate Non-Persistent Meshes", "Reuse meshes that already live as project assets. This avoids unnecessary duplicates for authored meshes."), _onlyGeneratedMeshes);
                    _includeSkinned = EditorGUILayout.ToggleLeft(new GUIContent("Include Skinned Mesh Renderers", "Also duplicate/reuse meshes assigned to SkinnedMeshRenderer components."), _includeSkinned);
                }
                _includeDisabled = EditorGUILayout.ToggleLeft(new GUIContent("Include Disabled Children", "Include renderers under inactive child objects."), _includeDisabled);
                _connect = EditorGUILayout.ToggleLeft(new GUIContent("Connect Selected Scene Object", "After saving, connect the currently selected scene object to the newly saved prefab. Leave off when you only want an exported copy."), _connect);
                if (EditorGUI.EndChangeCheck())
                    SavePrefs();
            }

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Green)))
            {
                UtilityWindowTheme.SectionTitle("Actions", UtilityWindowTheme.Green, "export");
                using (new EditorGUI.DisabledScope(Selection.activeGameObject == null))
                {
                    if (UtilityWindowTheme.TintedButton("Save Selected As Prefab", UtilityWindowTheme.Green, GUILayout.Height(30f)))
                        PrefabAssetExporter.SaveSelectedWithOptions(BuildOptions());
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private PrefabAssetExporter.ExportOptions BuildOptions()
        {
            return new PrefabAssetExporter.ExportOptions
            {
                SaveMeshesAsAssets = _saveMeshes,
                OnlyDuplicateNonPersistentMeshes = _onlyGeneratedMeshes,
                IncludeDisabledChildren = _includeDisabled,
                IncludeSkinnedMeshes = _includeSkinned,
                ConnectSelectedSceneObjectToPrefab = _connect
            };
        }

        private void SavePrefs()
        {
            UtilityWindowPrefs.SetBool(PrefSaveMeshes, _saveMeshes);
            UtilityWindowPrefs.SetBool(PrefOnlyGeneratedMeshes, _onlyGeneratedMeshes);
            UtilityWindowPrefs.SetBool(PrefIncludeDisabled, _includeDisabled);
            UtilityWindowPrefs.SetBool(PrefIncludeSkinned, _includeSkinned);
            UtilityWindowPrefs.SetBool(PrefConnect, _connect);
        }
    }
    #endif

}