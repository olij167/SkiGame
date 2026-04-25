using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct WeightedAudioMaterial
{
    public AudioSurfaceMaterialSO material;
    [Range(0f, 1f)] public float weight;
}

[Serializable]
public struct TerrainLayerAudioBinding
{
    public TerrainLayer layer;
    public AudioSurfaceMaterialSO material;
}

[CreateAssetMenu(
    fileName = "TerrainAudioMaterialProfile",
    menuName = "SkiGame/Audio/Terrain Material Profile")]
public sealed class TerrainAudioMaterialProfileSO : ScriptableObject
{
    [SerializeField] private List<TerrainLayerAudioBinding> layerBindings = new List<TerrainLayerAudioBinding>();
    [SerializeField] private AudioSurfaceMaterialSO fallbackMaterial;
    [SerializeField, Range(0f, 1f)] private float blendHysteresis = 0.1f;

    public IReadOnlyList<TerrainLayerAudioBinding> LayerBindings => layerBindings;
    public AudioSurfaceMaterialSO FallbackMaterial => fallbackMaterial;
    public float BlendHysteresis => blendHysteresis;

    public AudioSurfaceMaterialSO GetMaterialForLayer(TerrainLayer layer)
    {
        return TryGetMaterialForLayer(layer, out AudioSurfaceMaterialSO material)
            ? material
            : fallbackMaterial;
    }

    public bool TryGetMaterialForLayer(TerrainLayer layer, out AudioSurfaceMaterialSO material)
    {
        material = null;
        if (layer == null)
            return false;

        for (int i = 0; i < layerBindings.Count; i++)
        {
            TerrainLayerAudioBinding binding = layerBindings[i];
            if (binding.layer == layer)
            {
                material = binding.material != null ? binding.material : fallbackMaterial;
                return material != null;
            }
        }

        return false;
    }

    public AudioSurfaceMaterialSO ResolveDominantMaterial(Terrain terrain, Vector3 worldPos)
    {
        WeightedAudioMaterial[] weighted = ResolveWeightedMaterials(terrain, worldPos);
        if (weighted == null || weighted.Length == 0)
            return fallbackMaterial;

        AudioSurfaceMaterialSO bestMaterial = fallbackMaterial;
        float bestWeight = -1f;

        for (int i = 0; i < weighted.Length; i++)
        {
            if (weighted[i].material == null)
                continue;

            if (weighted[i].weight > bestWeight + blendHysteresis)
            {
                bestWeight = weighted[i].weight;
                bestMaterial = weighted[i].material;
            }
        }

        return bestMaterial != null ? bestMaterial : fallbackMaterial;
    }

    public WeightedAudioMaterial[] ResolveWeightedMaterials(Terrain terrain, Vector3 worldPos)
    {
        if (terrain == null || terrain.terrainData == null)
            return fallbackMaterial != null
                ? new[] { new WeightedAudioMaterial { material = fallbackMaterial, weight = 1f } }
                : Array.Empty<WeightedAudioMaterial>();

        TerrainData data = terrain.terrainData;
        Vector3 terrainLocal = worldPos - terrain.transform.position;
        Vector3 size = data.size;
        if (size.x <= 0.001f || size.z <= 0.001f)
            return fallbackMaterial != null
                ? new[] { new WeightedAudioMaterial { material = fallbackMaterial, weight = 1f } }
                : Array.Empty<WeightedAudioMaterial>();

        float normX = Mathf.Clamp01(terrainLocal.x / size.x);
        float normZ = Mathf.Clamp01(terrainLocal.z / size.z);

        int mapX = Mathf.Clamp(Mathf.RoundToInt(normX * (data.alphamapWidth - 1)), 0, data.alphamapWidth - 1);
        int mapZ = Mathf.Clamp(Mathf.RoundToInt(normZ * (data.alphamapHeight - 1)), 0, data.alphamapHeight - 1);

        float[,,] weights = data.GetAlphamaps(mapX, mapZ, 1, 1);
        TerrainLayer[] layers = data.terrainLayers;
        List<WeightedAudioMaterial> resolved = new List<WeightedAudioMaterial>(layers.Length);

        for (int i = 0; i < layers.Length; i++)
        {
            float weight = weights[0, 0, i];
            if (weight <= 0.001f)
                continue;

            AudioSurfaceMaterialSO material = GetMaterialForLayer(layers[i]);
            if (material == null)
                continue;

            resolved.Add(new WeightedAudioMaterial
            {
                material = material,
                weight = weight
            });
        }

        if (resolved.Count == 0 && fallbackMaterial != null)
        {
            resolved.Add(new WeightedAudioMaterial
            {
                material = fallbackMaterial,
                weight = 1f
            });
        }

        return resolved.ToArray();
    }
}
