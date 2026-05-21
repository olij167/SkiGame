using System;
using UnityEngine;

namespace PungentFunk.Utilities.Audio
{
    [Serializable]
    public struct ContactEventAudioRule
    {
        public AudioClipSetSO clips;
        [Range(0f, 2f)] public float volumeMultiplier;
        [Range(0.25f, 2f)] public float pitchMultiplier;
        public bool sustained;
    }

    [Serializable]
    public struct InteractionBlendSettings
    {
        [Range(0f, 1f)] public float intensityToVolume;
        [Range(0f, 1f)] public float speedToPitch;
        [Range(0f, 1f)] public float crossfadeSeconds;
    }

    [CreateAssetMenu(
        fileName = "AudioInteractionProfile",
        menuName = "PungentFunk Utilities/Audio/Interaction Profile")]
    public sealed class AudioInteractionProfileSO : ScriptableObject
    {
        [Header("Pair")]
        [SerializeField] private AudioSurfaceMaterialSO materialA;
        [SerializeField] private AudioSurfaceMaterialSO materialB;

        [Header("Rules")]
        [SerializeField] private ContactEventAudioRule lightImpact;
        [SerializeField] private ContactEventAudioRule mediumImpact;
        [SerializeField] private ContactEventAudioRule heavyImpact;
        [SerializeField] private ContactEventAudioRule scrape;
        [SerializeField] private ContactEventAudioRule drag;
        [SerializeField] private ContactEventAudioRule release;
        [SerializeField] private ContactEventAudioRule bounce;
        [SerializeField] private ContactEventAudioRule slide;
        [SerializeField] private InteractionBlendSettings blendSettings;

        public AudioSurfaceMaterialSO MaterialA => materialA;
        public AudioSurfaceMaterialSO MaterialB => materialB;
        public InteractionBlendSettings BlendSettings => blendSettings;

        public bool MatchesPair(AudioSurfaceMaterialSO a, AudioSurfaceMaterialSO b)
        {
            return (materialA == a && materialB == b) || (materialA == b && materialB == a);
        }

        public ContactEventAudioRule GetRule(ContactAudioEventType type)
        {
            switch (type)
            {
                case ContactAudioEventType.LightImpact: return lightImpact;
                case ContactAudioEventType.MediumImpact: return mediumImpact;
                case ContactAudioEventType.HeavyImpact: return heavyImpact;
                case ContactAudioEventType.Scrape: return scrape;
                case ContactAudioEventType.Drag: return drag;
                case ContactAudioEventType.Release: return release;
                case ContactAudioEventType.Bounce: return bounce;
                case ContactAudioEventType.Slide: return slide;
                case ContactAudioEventType.Brush: return scrape;
                default: return default;
            }
        }
    }

}