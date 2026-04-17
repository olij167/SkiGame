using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace SkiGame.Audio
{
    [DisallowMultipleComponent]
    public sealed class GameAudioDirector : MonoBehaviour
    {
        private struct VoiceRequest
        {
            public GameAudioCueId cueId;
            public Vector3 position;
            public Transform followTarget;
            public bool isUi;
            public float volumeScale;
        }

        private sealed class ActiveVoice
        {
            public AudioSource source;
            public Transform followTarget;
        }

        public static GameAudioDirector Instance { get; private set; }

        [Header("Catalog")]
        [SerializeField] private GameAudioCatalogSO catalog;
        [SerializeField] private string resourcesCatalogPath = "GameAudioCatalog";

        [Header("Pooling")]
        [SerializeField] private int initialPoolSize = 12;
        [SerializeField] private AudioMixerGroup defaultUiMixerGroup;
        [SerializeField] private AudioMixerGroup defaultWorldMixerGroup;

        private readonly Queue<AudioSource> _availableSources = new Queue<AudioSource>(16);
        private readonly List<ActiveVoice> _activeVoices = new List<ActiveVoice>(16);
        private readonly Dictionary<GameAudioCueId, float> _nextAllowedTimes = new Dictionary<GameAudioCueId, float>();
        private readonly HashSet<GameAudioCueId> _warnedMissingCues = new HashSet<GameAudioCueId>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureInstance()
        {
            if (Instance != null)
                return;

#if UNITY_2023_1_OR_NEWER
            GameAudioDirector existing = FindAnyObjectByType<GameAudioDirector>();
#else
            GameAudioDirector existing = FindObjectOfType<GameAudioDirector>();
#endif
            if (existing != null)
            {
                Instance = existing;
                return;
            }

            GameObject root = new GameObject("[GameAudioDirector]");
            Instance = root.AddComponent<GameAudioDirector>();
            DontDestroyOnLoad(root);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            ResolveCatalog();
            WarmPool();
        }

        private void Update()
        {
            for (int i = _activeVoices.Count - 1; i >= 0; i--)
            {
                ActiveVoice voice = _activeVoices[i];
                if (voice == null || voice.source == null)
                {
                    _activeVoices.RemoveAt(i);
                    continue;
                }

                if (voice.followTarget != null)
                    voice.source.transform.position = voice.followTarget.position;

                if (voice.source.isPlaying)
                    continue;

                Recycle(voice.source);
                _activeVoices.RemoveAt(i);
            }
        }

        public static bool TryPlayUi(GameAudioCueId cueId, float volumeScale = 1f)
        {
            return Instance != null && Instance.Play(new VoiceRequest
            {
                cueId = cueId,
                isUi = true,
                volumeScale = volumeScale,
                position = Vector3.zero,
                followTarget = null
            });
        }

        public static bool TryPlayWorld(GameAudioCueId cueId, Vector3 position, float volumeScale = 1f)
        {
            return Instance != null && Instance.Play(new VoiceRequest
            {
                cueId = cueId,
                isUi = false,
                volumeScale = volumeScale,
                position = position,
                followTarget = null
            });
        }

        public static bool TryPlayAttached(GameAudioCueId cueId, Transform target, float volumeScale = 1f)
        {
            return Instance != null && Instance.Play(new VoiceRequest
            {
                cueId = cueId,
                isUi = false,
                volumeScale = volumeScale,
                position = target != null ? target.position : Vector3.zero,
                followTarget = target
            });
        }

        private bool Play(VoiceRequest request)
        {
            ResolveCatalog();

            if (!TryResolveCue(request.cueId, out GameAudioCatalogSO.CueDefinition cue))
                return false;

            if (!CanPlay(cue))
                return false;

            AudioClip clip = ChooseClip(cue);
            if (clip == null)
                return false;

            AudioSource source = GetSource();
            ConfigureSource(source, request, cue, clip);
            source.Play();

            _activeVoices.Add(new ActiveVoice
            {
                source = source,
                followTarget = request.followTarget
            });

            if (cue.cooldownSeconds > 0f)
                _nextAllowedTimes[request.cueId] = Time.unscaledTime + cue.cooldownSeconds;

            return true;
        }

        private bool TryResolveCue(GameAudioCueId cueId, out GameAudioCatalogSO.CueDefinition cue)
        {
            cue = null;

            if (cueId == GameAudioCueId.None)
                return false;

            if (catalog == null || !catalog.TryGet(cueId, out cue) || cue == null || cue.clips == null || cue.clips.Length == 0)
            {
                if (_warnedMissingCues.Add(cueId))
                    Debug.Log($"[GameAudioDirector] Cue '{cueId}' is hooked but has no configured clips yet.");

                return false;
            }

            return true;
        }

        private void ResolveCatalog()
        {
            if (catalog != null)
                return;

            if (!string.IsNullOrWhiteSpace(resourcesCatalogPath))
                catalog = Resources.Load<GameAudioCatalogSO>(resourcesCatalogPath.Trim());
        }

        private bool CanPlay(GameAudioCatalogSO.CueDefinition cue)
        {
            if (cue == null || cue.cooldownSeconds <= 0f)
                return true;

            return !_nextAllowedTimes.TryGetValue(cue.id, out float nextAllowed) || Time.unscaledTime >= nextAllowed;
        }

        private AudioClip ChooseClip(GameAudioCatalogSO.CueDefinition cue)
        {
            if (cue == null || cue.clips == null || cue.clips.Length == 0)
                return null;

            int index = cue.clips.Length == 1 ? 0 : Random.Range(0, cue.clips.Length);
            return cue.clips[index];
        }

        private void ConfigureSource(AudioSource source, VoiceRequest request, GameAudioCatalogSO.CueDefinition cue, AudioClip clip)
        {
            source.transform.position = request.followTarget != null ? request.followTarget.position : request.position;
            source.clip = clip;
            source.volume = Mathf.Max(0f, cue.volume * Mathf.Max(0f, request.volumeScale));
            source.pitch = Random.Range(
                Mathf.Min(cue.pitchRange.x, cue.pitchRange.y),
                Mathf.Max(cue.pitchRange.x, cue.pitchRange.y));
            source.spatialBlend = request.isUi ? 0f : cue.spatialBlend;
            source.minDistance = cue.minDistance;
            source.maxDistance = Mathf.Max(cue.minDistance, cue.maxDistance);
            source.outputAudioMixerGroup = cue.mixerGroup != null
                ? cue.mixerGroup
                : (request.isUi ? defaultUiMixerGroup : defaultWorldMixerGroup);
            source.loop = false;
            source.playOnAwake = false;
        }

        private void WarmPool()
        {
            int targetCount = Mathf.Max(1, initialPoolSize);
            while (_availableSources.Count < targetCount)
                _availableSources.Enqueue(CreateSource());
        }

        private AudioSource GetSource()
        {
            return _availableSources.Count > 0 ? _availableSources.Dequeue() : CreateSource();
        }

        private void Recycle(AudioSource source)
        {
            if (source == null)
                return;

            source.Stop();
            source.clip = null;
            source.transform.SetParent(transform, false);
            _availableSources.Enqueue(source);
        }

        private AudioSource CreateSource()
        {
            GameObject go = new GameObject("AudioVoice");
            go.transform.SetParent(transform, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.rolloffMode = AudioRolloffMode.Linear;
            return source;
        }
    }
}
