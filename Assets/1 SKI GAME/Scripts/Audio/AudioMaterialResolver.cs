using UnityEngine;

public static class AudioMaterialResolver
{
    public static bool TryResolveFromCollider(Collider collider, out AudioSurfaceMaterialSO material)
    {
        material = null;
        if (collider == null)
            return false;

        AudioMaterialTag localTag = collider.GetComponent<AudioMaterialTag>();
        if (localTag != null && localTag.TryGetMaterial(out material))
            return true;

        AudioMaterialTag parentTag = collider.GetComponentInParent<AudioMaterialTag>();
        return parentTag != null && parentTag.TryGetMaterial(out material);
    }

    public static bool TryResolveFromHit(RaycastHit hit, out AudioSurfaceMaterialSO material)
    {
        if (TryResolveFromCollider(hit.collider, out material))
            return true;

        return TryResolveTerrainMaterial(hit.collider, hit.point, null, out material);
    }

    public static AudioSurfaceMaterialSO ResolveDominantSurface(
        Collider collider,
        Vector3 point,
        TerrainAudioMaterialProfileSO terrainProfile = null)
    {
        if (TryResolveFromCollider(collider, out AudioSurfaceMaterialSO material))
            return material;

        if (TryResolveTerrainMaterial(collider, point, terrainProfile, out material))
            return material;

        return terrainProfile != null ? terrainProfile.FallbackMaterial : null;
    }

    public static WeightedAudioMaterial[] ResolveWeightedSurface(
        Collider collider,
        Vector3 point,
        TerrainAudioMaterialProfileSO terrainProfile = null)
    {
        if (TryResolveFromCollider(collider, out AudioSurfaceMaterialSO material))
        {
            return material != null
                ? new[] { new WeightedAudioMaterial { material = material, weight = 1f } }
                : System.Array.Empty<WeightedAudioMaterial>();
        }

        Terrain terrain = FindTerrain(collider);
        if (terrain != null && terrainProfile != null)
            return terrainProfile.ResolveWeightedMaterials(terrain, point);

        if (terrainProfile != null && terrainProfile.FallbackMaterial != null)
        {
            return new[]
            {
                new WeightedAudioMaterial
                {
                    material = terrainProfile.FallbackMaterial,
                    weight = 1f
                }
            };
        }

        return System.Array.Empty<WeightedAudioMaterial>();
    }

    public static bool TryResolveTerrainMaterial(
        Collider collider,
        Vector3 point,
        TerrainAudioMaterialProfileSO terrainProfile,
        out AudioSurfaceMaterialSO material)
    {
        material = null;
        Terrain terrain = FindTerrain(collider);
        if (terrain == null || terrainProfile == null)
        {
            material = terrainProfile != null ? terrainProfile.FallbackMaterial : null;
            return material != null;
        }

        material = terrainProfile.ResolveDominantMaterial(terrain, point);
        return material != null;
    }

    private static Terrain FindTerrain(Collider collider)
    {
        if (collider == null)
            return null;

        Terrain terrain = collider.GetComponent<Terrain>();
        if (terrain != null)
            return terrain;

        terrain = collider.GetComponentInParent<Terrain>();
        if (terrain != null)
            return terrain;

        GameObject go = collider.gameObject;
        Terrain activeTerrain = Terrain.activeTerrain;
        if (activeTerrain == null || activeTerrain.terrainData == null)
            return null;

        Bounds terrainBounds = activeTerrain.terrainData.bounds;
        terrainBounds.center += activeTerrain.transform.position;
        if (terrainBounds.Contains(go.transform.position))
            return activeTerrain;

        return null;
    }
}
