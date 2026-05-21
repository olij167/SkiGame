using System;
using UnityEngine;

namespace PungentFunk.Utilities.EnvironmentSimulation
{
    public enum PungentTimeChannel
    {
        Global,
        Gameplay,
        UI,
        Physics,
        Animation,
        Audio,
        Particles,
        Clock,
        Weather,
        Custom
    }

    public enum PungentTimeUpdateMode
    {
        Scaled,
        Unscaled
    }

    [Serializable]
    public sealed class PungentDeltaTimeChannelSettings
    {
        public PungentTimeChannel channel = PungentTimeChannel.Gameplay;
        public string customChannel = string.Empty;
        [Min(0f)] public float defaultScale = 1f;
        [Min(0f)] public float minimumScale = 0f;
        [Min(0f)] public float maximumScale = 8f;
        [Min(0f)] public float blendDuration = 0.1f;
        public int priority;
        public PungentTimeUpdateMode blendUpdateMode = PungentTimeUpdateMode.Unscaled;
        public bool applyToUnityTimeScale;

        public string Key => PungentDeltaTimeController.BuildChannelKey(channel, customChannel);
    }

    [CreateAssetMenu(menuName = "PungentFunk/Environment Simulation/Delta Time Profile", fileName = "Pungent Delta Time Profile")]
    public sealed class PungentDeltaTimeProfile : ScriptableObject
    {
        [SerializeField] private PungentDeltaTimeChannelSettings[] channels =
        {
            new PungentDeltaTimeChannelSettings { channel = PungentTimeChannel.Global, defaultScale = 1f, blendDuration = 0.08f, applyToUnityTimeScale = true },
            new PungentDeltaTimeChannelSettings { channel = PungentTimeChannel.Gameplay, defaultScale = 1f, blendDuration = 0.08f },
            new PungentDeltaTimeChannelSettings { channel = PungentTimeChannel.UI, defaultScale = 1f, blendDuration = 0.02f },
            new PungentDeltaTimeChannelSettings { channel = PungentTimeChannel.Physics, defaultScale = 1f, blendDuration = 0.08f },
            new PungentDeltaTimeChannelSettings { channel = PungentTimeChannel.Animation, defaultScale = 1f, blendDuration = 0.08f },
            new PungentDeltaTimeChannelSettings { channel = PungentTimeChannel.Audio, defaultScale = 1f, blendDuration = 0.08f },
            new PungentDeltaTimeChannelSettings { channel = PungentTimeChannel.Particles, defaultScale = 1f, blendDuration = 0.08f },
            new PungentDeltaTimeChannelSettings { channel = PungentTimeChannel.Clock, defaultScale = 1f, blendDuration = 0.08f },
            new PungentDeltaTimeChannelSettings { channel = PungentTimeChannel.Weather, defaultScale = 1f, blendDuration = 0.08f }
        };

        public PungentDeltaTimeChannelSettings[] Channels => channels;

        public PungentDeltaTimeChannelSettings Find(string key)
        {
            if (channels == null)
                return null;

            for (int i = 0; i < channels.Length; i++)
            {
                PungentDeltaTimeChannelSettings settings = channels[i];
                if (settings != null && string.Equals(settings.Key, key, StringComparison.OrdinalIgnoreCase))
                    return settings;
            }

            return null;
        }
    }
}
