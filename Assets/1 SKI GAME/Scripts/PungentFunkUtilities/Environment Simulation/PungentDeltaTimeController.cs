using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace PungentFunk.Utilities.EnvironmentSimulation
{
    [Serializable] public sealed class PungentTimeScaleEvent : UnityEvent<string, float> { }
    [Serializable] public sealed class PungentPauseStackEvent : UnityEvent<string, int> { }

    [DisallowMultipleComponent]
    [AddComponentMenu("PungentFunk/Environment Simulation/Pungent Delta Time Controller")]
    public sealed class PungentDeltaTimeController : MonoBehaviour
    {
        private sealed class ChannelState
        {
            public PungentDeltaTimeChannelSettings settings;
            public float targetScale = 1f;
            public float currentScale = 1f;
            public int pauseStack;
            public float velocity;
            public readonly Dictionary<string, int> pauseOwners = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, ScaleRequest> scaleOwners = new Dictionary<string, ScaleRequest>(StringComparer.OrdinalIgnoreCase);
        }

        private struct ScaleRequest
        {
            public float scale;
            public int priority;
            public int sequence;
        }

        public static PungentDeltaTimeController Instance { get; private set; }

        [SerializeField] private PungentDeltaTimeProfile profile;
        [SerializeField] private bool claimSingleton = true;
        [SerializeField] private bool applyUnityTimeScale = true;
        [SerializeField, Min(0f)] private float fixedDeltaTimeBase = 0.02f;
        [SerializeField] private PungentTimeScaleEvent onChannelScaleChanged = new PungentTimeScaleEvent();
        [SerializeField] private PungentPauseStackEvent onPauseStackChanged = new PungentPauseStackEvent();

        private readonly Dictionary<string, ChannelState> _channels = new Dictionary<string, ChannelState>(StringComparer.OrdinalIgnoreCase);
        private bool _initialized;
        private bool _capturedUnityTime;
        private bool _appliedUnityTime;
        private float _originalTimeScale = 1f;
        private float _originalFixedDeltaTime = 0.02f;
        private int _requestSequence;

        public event Action<string, float> ChannelScaleChanged;
        public event Action<string, int> PauseStackChanged;

        public PungentTimeScaleEvent OnChannelScaleChanged => onChannelScaleChanged;
        public PungentPauseStackEvent OnPauseStackChanged => onPauseStackChanged;

        public static string BuildChannelKey(PungentTimeChannel channel, string customChannel)
        {
            if (channel == PungentTimeChannel.Custom && !string.IsNullOrWhiteSpace(customChannel))
                return customChannel.Trim();
            return channel.ToString();
        }

        private void Awake()
        {
            if (claimSingleton)
            {
                if (Instance != null && Instance != this)
                {
                    Destroy(this);
                    return;
                }

                Instance = this;
            }

            CaptureUnityTimeSettings();
            fixedDeltaTimeBase = Time.fixedDeltaTime;
            RebuildChannels();
        }

        private void OnEnable()
        {
            CaptureUnityTimeSettings();
            RebuildChannels();
        }

        private void OnDisable()
        {
            RestoreUnityTimeSettings();
        }

        private void OnDestroy()
        {
            RestoreUnityTimeSettings();
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            Tick(Time.unscaledDeltaTime);
        }

        public void RebuildChannels()
        {
            _channels.Clear();
            if (profile != null && profile.Channels != null)
            {
                for (int i = 0; i < profile.Channels.Length; i++)
                    Register(profile.Channels[i]);
            }

            EnsureDefault(PungentTimeChannel.Global, true);
            EnsureDefault(PungentTimeChannel.Gameplay, false);
            EnsureDefault(PungentTimeChannel.Clock, false);
            EnsureDefault(PungentTimeChannel.Weather, false);
            _initialized = true;
        }

        public float GetScale(PungentTimeChannel channel, string customChannel = null)
        {
            ChannelState state = GetOrCreate(BuildChannelKey(channel, customChannel), channel, customChannel);
            return state.pauseStack > 0 ? 0f : Mathf.Max(0f, state.currentScale);
        }

        public int GetPauseStack(PungentTimeChannel channel, string customChannel = null)
        {
            return GetOrCreate(BuildChannelKey(channel, customChannel), channel, customChannel).pauseStack;
        }

        public string GetPauseOwnerSummary(PungentTimeChannel channel, string customChannel = null)
        {
            ChannelState state = GetOrCreate(BuildChannelKey(channel, customChannel), channel, customChannel);
            return FormatOwnerSummary(state.pauseOwners);
        }

        public string GetScaleOwnerSummary(PungentTimeChannel channel, string customChannel = null)
        {
            ChannelState state = GetOrCreate(BuildChannelKey(channel, customChannel), channel, customChannel);
            if (state.scaleOwners.Count == 0)
                return "No owner scale requests.";

            List<string> owners = new List<string>();
            foreach (KeyValuePair<string, ScaleRequest> pair in state.scaleOwners)
                owners.Add(pair.Key + " " + pair.Value.scale.ToString("0.00") + "x");
            owners.Sort(StringComparer.OrdinalIgnoreCase);
            return string.Join(", ", owners);
        }

        public void SetScale(PungentTimeChannel channel, float scale) => SetScale(channel, null, scale);

        public void SetScale(PungentTimeChannel channel, string customChannel, float scale)
        {
            if (channel != PungentTimeChannel.Custom && !string.IsNullOrWhiteSpace(customChannel))
            {
                SetScale(channel, null, customChannel, scale);
                return;
            }

            ChannelState state = GetOrCreate(BuildChannelKey(channel, customChannel), channel, customChannel);
            state.targetScale = Mathf.Clamp(scale, state.settings.minimumScale, Mathf.Max(state.settings.minimumScale, state.settings.maximumScale));
        }

        public void SetScale(PungentTimeChannel channel, string customChannel, string owner, float scale)
        {
            ChannelState state = GetOrCreate(BuildChannelKey(channel, customChannel), channel, customChannel);
            string key = OwnerKey(owner);
            state.scaleOwners[key] = new ScaleRequest
            {
                scale = Mathf.Clamp(scale, state.settings.minimumScale, Mathf.Max(state.settings.minimumScale, state.settings.maximumScale)),
                priority = state.settings.priority,
                sequence = ++_requestSequence
            };
        }

        public void ClearScale(PungentTimeChannel channel, string owner) => ClearScale(channel, null, owner);

        public void ClearScale(PungentTimeChannel channel, string customChannel, string owner)
        {
            ChannelState state = GetOrCreate(BuildChannelKey(channel, customChannel), channel, customChannel);
            state.scaleOwners.Remove(OwnerKey(owner));
        }

        public void PushPause(PungentTimeChannel channel) => PushPause(channel, null);

        public void PushPause(PungentTimeChannel channel, string customChannel)
        {
            if (channel != PungentTimeChannel.Custom && !string.IsNullOrWhiteSpace(customChannel))
            {
                PushPause(channel, null, customChannel);
                return;
            }

            ChannelState state = GetOrCreate(BuildChannelKey(channel, customChannel), channel, customChannel);
            state.pauseStack++;
            RaisePauseChanged(state.settings.Key, state.pauseStack);
        }

        public void PushPause(PungentTimeChannel channel, string customChannel, string owner)
        {
            ChannelState state = GetOrCreate(BuildChannelKey(channel, customChannel), channel, customChannel);
            string key = OwnerKey(owner);
            state.pauseOwners.TryGetValue(key, out int count);
            state.pauseOwners[key] = count + 1;
            state.pauseStack++;
            RaisePauseChanged(state.settings.Key, state.pauseStack);
        }

        public void PopPause(PungentTimeChannel channel) => PopPause(channel, null);

        public void PopPause(PungentTimeChannel channel, string customChannel)
        {
            if (channel != PungentTimeChannel.Custom && !string.IsNullOrWhiteSpace(customChannel))
            {
                PopPause(channel, null, customChannel);
                return;
            }

            ChannelState state = GetOrCreate(BuildChannelKey(channel, customChannel), channel, customChannel);
            state.pauseStack = Mathf.Max(0, state.pauseStack - 1);
            RaisePauseChanged(state.settings.Key, state.pauseStack);
        }

        public void PopPause(PungentTimeChannel channel, string customChannel, string owner)
        {
            ChannelState state = GetOrCreate(BuildChannelKey(channel, customChannel), channel, customChannel);
            string key = OwnerKey(owner);
            if (state.pauseOwners.TryGetValue(key, out int count))
            {
                count--;
                if (count <= 0)
                    state.pauseOwners.Remove(key);
                else
                    state.pauseOwners[key] = count;
                state.pauseStack = Mathf.Max(0, state.pauseStack - 1);
                RaisePauseChanged(state.settings.Key, state.pauseStack);
            }
        }

        public void ClearPause(PungentTimeChannel channel, string customChannel = null)
        {
            if (channel != PungentTimeChannel.Custom && !string.IsNullOrWhiteSpace(customChannel))
            {
                ClearPause(channel, null, customChannel);
                return;
            }

            ChannelState state = GetOrCreate(BuildChannelKey(channel, customChannel), channel, customChannel);
            if (state.pauseStack == 0)
                return;

            state.pauseStack = 0;
            state.pauseOwners.Clear();
            RaisePauseChanged(state.settings.Key, 0);
        }

        public void ClearPause(PungentTimeChannel channel, string customChannel, string owner)
        {
            ChannelState state = GetOrCreate(BuildChannelKey(channel, customChannel), channel, customChannel);
            string key = OwnerKey(owner);
            if (!state.pauseOwners.TryGetValue(key, out int count))
                return;

            state.pauseOwners.Remove(key);
            state.pauseStack = Mathf.Max(0, state.pauseStack - count);
            RaisePauseChanged(state.settings.Key, state.pauseStack);
        }

        public void ResetAll()
        {
            foreach (ChannelState state in _channels.Values)
            {
                state.pauseStack = 0;
                state.pauseOwners.Clear();
                state.scaleOwners.Clear();
                state.targetScale = Mathf.Clamp(state.settings.defaultScale, state.settings.minimumScale, state.settings.maximumScale);
                state.currentScale = state.targetScale;
                RaisePauseChanged(state.settings.Key, 0);
                RaiseScaleChanged(state.settings.Key, state.currentScale);
            }

            ApplyUnityScale();
        }

        private void Tick(float unscaledDeltaTime)
        {
            if (!_initialized)
                RebuildChannels();

            foreach (ChannelState state in _channels.Values)
            {
                float previous = state.currentScale;
                float target = state.pauseStack > 0 ? 0f : ResolveTargetScale(state);
                float blend = Mathf.Max(0f, state.settings.blendDuration);
                float deltaTime = state.settings.blendUpdateMode == PungentTimeUpdateMode.Scaled ? Time.deltaTime : unscaledDeltaTime;
                if (blend <= 0f)
                    state.currentScale = target;
                else
                    state.currentScale = Mathf.SmoothDamp(state.currentScale, target, ref state.velocity, blend, float.PositiveInfinity, deltaTime);

                if (Mathf.Abs(previous - state.currentScale) > 0.0005f)
                    RaiseScaleChanged(state.settings.Key, state.currentScale);
            }

            ApplyUnityScale();
        }

        private void ApplyUnityScale()
        {
            if (!applyUnityTimeScale)
                return;

            float scale = 1f;
            foreach (ChannelState state in _channels.Values)
            {
                if (state.settings.applyToUnityTimeScale)
                    scale = Mathf.Min(scale, Mathf.Max(0f, state.pauseStack > 0 ? 0f : state.currentScale));
            }

            Time.timeScale = scale;
            Time.fixedDeltaTime = fixedDeltaTimeBase * Mathf.Max(0.0001f, scale);
            _appliedUnityTime = true;
        }

        private float ResolveTargetScale(ChannelState state)
        {
            float scale = state.targetScale;
            int bestPriority = int.MinValue;
            int bestSequence = int.MinValue;
            foreach (ScaleRequest request in state.scaleOwners.Values)
            {
                if (request.priority < bestPriority || (request.priority == bestPriority && request.sequence < bestSequence))
                    continue;

                bestPriority = request.priority;
                bestSequence = request.sequence;
                scale = request.scale;
            }

            return Mathf.Clamp(scale, state.settings.minimumScale, Mathf.Max(state.settings.minimumScale, state.settings.maximumScale));
        }

        private void Register(PungentDeltaTimeChannelSettings settings)
        {
            if (settings == null)
                return;

            string key = settings.Key;
            if (string.IsNullOrWhiteSpace(key))
                return;

            _channels[key] = new ChannelState
            {
                settings = settings,
                currentScale = Mathf.Clamp(settings.defaultScale, settings.minimumScale, settings.maximumScale),
                targetScale = Mathf.Clamp(settings.defaultScale, settings.minimumScale, settings.maximumScale)
            };
        }

        private void EnsureDefault(PungentTimeChannel channel, bool applyToUnity)
        {
            string key = BuildChannelKey(channel, null);
            if (_channels.ContainsKey(key))
                return;

            Register(new PungentDeltaTimeChannelSettings { channel = channel, defaultScale = 1f, maximumScale = 8f, blendDuration = 0.08f, applyToUnityTimeScale = applyToUnity });
        }

        private ChannelState GetOrCreate(string key, PungentTimeChannel channel, string customChannel)
        {
            if (!_channels.TryGetValue(key, out ChannelState state))
            {
                PungentDeltaTimeChannelSettings settings = new PungentDeltaTimeChannelSettings
                {
                    channel = channel,
                    customChannel = customChannel ?? string.Empty,
                    defaultScale = 1f,
                    maximumScale = 8f,
                    blendDuration = 0.08f
                };
                Register(settings);
                state = _channels[key];
            }

            return state;
        }

        private void RaiseScaleChanged(string key, float scale)
        {
            ChannelScaleChanged?.Invoke(key, scale);
            onChannelScaleChanged.Invoke(key, scale);
        }

        private void RaisePauseChanged(string key, int stack)
        {
            PauseStackChanged?.Invoke(key, stack);
            onPauseStackChanged.Invoke(key, stack);
        }

        private void CaptureUnityTimeSettings()
        {
            if (_capturedUnityTime)
                return;

            _originalTimeScale = Time.timeScale;
            _originalFixedDeltaTime = Time.fixedDeltaTime;
            _capturedUnityTime = true;
        }

        private void RestoreUnityTimeSettings()
        {
            if (!applyUnityTimeScale || !_capturedUnityTime || !_appliedUnityTime)
                return;

            Time.timeScale = _originalTimeScale;
            Time.fixedDeltaTime = _originalFixedDeltaTime;
            _appliedUnityTime = false;
        }

        private static string OwnerKey(string owner)
        {
            return string.IsNullOrWhiteSpace(owner) ? "Anonymous" : owner.Trim();
        }

        private static string FormatOwnerSummary(Dictionary<string, int> owners)
        {
            if (owners == null || owners.Count == 0)
                return "No pause owners.";

            List<string> labels = new List<string>();
            foreach (KeyValuePair<string, int> pair in owners)
            {
                if (pair.Value > 0)
                    labels.Add(pair.Key + " x" + pair.Value);
            }

            labels.Sort(StringComparer.OrdinalIgnoreCase);
            return labels.Count == 0 ? "No pause owners." : string.Join(", ", labels);
        }
    }
}
