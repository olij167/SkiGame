#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class AudioCoverageContextWindow : EditorWindow
{
    private Vector2 _scroll;
    private readonly List<string> _results = new List<string>();

    [MenuItem("Tools/Ski Game/Audio Context Coverage")]
    public static void Open()
    {
        GetWindow<AudioCoverageContextWindow>("Audio Context Coverage");
    }

    private void OnGUI()
    {
        if (GUILayout.Button("Run Scan"))
            RunScan();

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        foreach (string result in _results)
            EditorGUILayout.LabelField($"• {result}", EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndScrollView();
    }

    private void RunScan()
    {
        _results.Clear();
        ScanSceneColliders();
        ScanTerrainProfiles();
        ScanSkierPrefabs();
        ScanInteractionMatrices();
        ScanSkierConfigs();

        if (_results.Count == 0)
            _results.Add("No obvious contextual audio setup gaps were found in this pass.");
    }

    private void ScanSceneColliders()
    {
        foreach (Collider collider in Object.FindObjectsOfType<Collider>(true))
        {
            if (collider == null || collider.isTrigger)
                continue;

            Terrain terrain = collider.GetComponent<Terrain>() ?? collider.GetComponentInParent<Terrain>();
            if (terrain != null)
                continue;

            if (collider.GetComponent<AudioMaterialTag>() == null && collider.GetComponentInParent<AudioMaterialTag>() == null)
                _results.Add($"Scene collider missing AudioMaterialTag: {collider.name}");
        }
    }

    private void ScanTerrainProfiles()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:TerrainAudioMaterialProfileSO"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TerrainAudioMaterialProfileSO profile = AssetDatabase.LoadAssetAtPath<TerrainAudioMaterialProfileSO>(path);
            HashSet<TerrainLayer> seen = new HashSet<TerrainLayer>();

            foreach (TerrainLayerAudioBinding binding in profile.LayerBindings)
            {
                if (binding.layer == null || binding.material == null)
                    _results.Add($"Terrain profile has incomplete layer binding: {path}");
                else if (!seen.Add(binding.layer))
                    _results.Add($"Terrain profile has duplicate terrain layer binding: {path} ({binding.layer.name})");
            }
        }
    }

    private void ScanSkierPrefabs()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab Player t:Prefab NPC"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                continue;

            SkiAudioController audio = prefab.GetComponentInChildren<SkiAudioController>(true);
            SkiController skier = prefab.GetComponentInChildren<SkiController>(true);
            if (audio == null && skier != null)
                _results.Add($"Skier prefab is missing SkiAudioController: {path}");
            else if (audio != null && audio.GetComponent<SkiController>() == null && skier == null)
                _results.Add($"SkiAudioController prefab wiring may be incomplete: {path}");
        }
    }

    private void ScanInteractionMatrices()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:AudioInteractionMatrixSO"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AudioInteractionMatrixSO matrix = AssetDatabase.LoadAssetAtPath<AudioInteractionMatrixSO>(path);
            if (matrix.ExactProfiles.Count == 0 && matrix.CategoryFallbacks.Count == 0)
                _results.Add($"Interaction matrix has no profiles or fallbacks: {path}");
        }
    }

    private void ScanSkierConfigs()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:SkierAudioConfigSO"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            SkierAudioConfigSO config = AssetDatabase.LoadAssetAtPath<SkierAudioConfigSO>(path);
            if (config == null)
                continue;

            if (config.BaseSkiLoopClip == null)
                _results.Add($"Skier config missing base ski loop clip: {path}");
            if (config.CarveLoopClip == null)
                _results.Add($"Skier config missing carve loop clip: {path}");
            if (config.WindLoopClip == null)
                _results.Add($"Skier config missing wind loop clip: {path}");
            if (config.PoleDragLoopClip == null)
                _results.Add($"Skier config missing pole drag loop clip: {path}");
            if (config.BodyDragLoopClip == null)
                _results.Add($"Skier config missing body drag loop clip: {path}");
        }
    }
}
#endif
