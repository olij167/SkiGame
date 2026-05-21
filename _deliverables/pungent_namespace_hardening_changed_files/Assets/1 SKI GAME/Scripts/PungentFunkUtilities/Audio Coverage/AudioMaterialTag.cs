using UnityEngine;

namespace PungentFunk.Utilities.Audio
{
    public enum AudioMaterialRole
    {
        Default = 0,
        Surface = 1,
        Structure = 2,
        Character = 3,
        Prop = 4,
        ToolTip = 5,
        ContactPatch = 6,
        ContactEdge = 7
    }

    [DisallowMultipleComponent]
    public sealed class AudioMaterialTag : MonoBehaviour
    {
        [Header("Assignment")]
        [SerializeField] private AudioSurfaceMaterialSO surfaceMaterial;
        [SerializeField] private AudioMaterialRole role = AudioMaterialRole.Surface;
        [SerializeField] private bool applyToChildren;
        [SerializeField] private bool includeInactiveChildren;
        [SerializeField] private AudioSurfaceMaterialSO colliderSpecificOverride;

        public AudioSurfaceMaterialSO SurfaceMaterial => surfaceMaterial;
        public AudioMaterialRole Role => role;
        public bool ApplyToChildrenEnabled => applyToChildren;
        public bool IncludeInactiveChildren => includeInactiveChildren;
        public AudioSurfaceMaterialSO ColliderSpecificOverride => colliderSpecificOverride;

        public AudioSurfaceMaterialSO GetAssignedMaterial()
        {
            return colliderSpecificOverride != null ? colliderSpecificOverride : surfaceMaterial;
        }

        public void ApplyToChildColliders()
        {
            if (!applyToChildren)
                return;

            Collider[] colliders = GetComponentsInChildren<Collider>(includeInactiveChildren);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || collider.gameObject == gameObject)
                    continue;

                AudioMaterialTag tag = collider.GetComponent<AudioMaterialTag>();
                if (tag == null)
                    tag = collider.gameObject.AddComponent<AudioMaterialTag>();

                tag.surfaceMaterial = surfaceMaterial;
                tag.role = role;
                tag.colliderSpecificOverride = colliderSpecificOverride;
                tag.applyToChildren = false;
                tag.includeInactiveChildren = includeInactiveChildren;
            }
        }

        public bool TryGetMaterial(out AudioSurfaceMaterialSO material)
        {
            material = GetAssignedMaterial();
            return material != null;
        }
    }

}