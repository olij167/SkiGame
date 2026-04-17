using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "AudioMaterialLibrary",
    menuName = "Ski Game/Audio/Material Library")]
public sealed class AudioMaterialLibrarySO : ScriptableObject
{
    [SerializeField] private List<AudioSurfaceMaterialSO> materials = new List<AudioSurfaceMaterialSO>();

    public AudioSurfaceMaterialSO FindById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        for (int i = 0; i < materials.Count; i++)
        {
            AudioSurfaceMaterialSO material = materials[i];
            if (material != null && string.Equals(material.MaterialId, id, System.StringComparison.OrdinalIgnoreCase))
                return material;
        }

        return null;
    }

    public bool Contains(AudioSurfaceMaterialSO material)
    {
        return material != null && materials.Contains(material);
    }

    public IReadOnlyList<AudioSurfaceMaterialSO> GetAll()
    {
        return materials;
    }
}
