using UnityEngine;

namespace PungentFunk.Utilities.Audio
{
    public enum AudioMaterialCategory
    {
        Unknown = 0,
        Powder = 1,
        Ice = 2,
        Rock = 3,
        Wood = 4,
        Metal = 5,
        Fabric = 6,
        Body = 7,
        Vegetation = 8,
        Synthetic = 9
    }

    [CreateAssetMenu(
        fileName = "AudioSurfaceMaterial",
        menuName = "PungentFunk Utilities/Audio/Surface Material")]
    public sealed class AudioSurfaceMaterialSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string materialId = "surface.material";
        [SerializeField] private string displayName = "Surface Material";
        [SerializeField] private AudioMaterialCategory category = AudioMaterialCategory.Unknown;

        [Header("Response")]
        [SerializeField, Range(0f, 1f)] private float resonance = 0.5f;
        [SerializeField, Range(0f, 1f)] private float damping = 0.5f;
        [SerializeField, Range(0f, 1f)] private float brightness = 0.5f;
        [SerializeField, Range(0f, 1f)] private float roughness = 0.5f;
        [SerializeField, Range(0f, 1f)] private float defaultReverbSend = 0.15f;

        [Header("Traits")]
        [SerializeField] private bool isSoftSurface = true;
        [SerializeField] private bool isGroundLike = true;
        [SerializeField] private bool isMetallic;
        [SerializeField] private bool isPowderySurface;

        [Header("Editor")]
        [SerializeField] private Color editorColor = new Color(0.75f, 0.85f, 1f);

        public string MaterialId => materialId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public AudioMaterialCategory Category => category;
        public float Resonance => resonance;
        public float Damping => damping;
        public float Brightness => brightness;
        public float Roughness => roughness;
        public float DefaultReverbSend => defaultReverbSend;
        public bool IsSoftSurfaceFlag => isSoftSurface;
        public bool IsGroundLike => isGroundLike;
        public bool IsMetallic => isMetallic;
        public bool IsPowderySurface => isPowderySurface;
        public Color EditorColor => editorColor;

        public string GetDebugLabel()
        {
            return $"{DisplayName} [{materialId}]";
        }

        public bool IsHardSurface()
        {
            return !isSoftSurface && !IsSoftOrPowdery();
        }

        public bool IsSoftOrPowdery()
        {
            return isSoftSurface || isPowderySurface || damping >= 0.6f;
        }
    }

}