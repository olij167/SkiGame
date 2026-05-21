using System.Collections.Generic;
using UnityEngine;

namespace PungentFunk.Utilities.Audio
{
    [DisallowMultipleComponent]
    public sealed class ContactAudioRouter : MonoBehaviour
    {
        [Header("Routing")]
        [SerializeField] private AudioInteractionMatrixSO interactionMatrix;
        [SerializeField] private AudioSource transientSourceTemplate;
        [SerializeField] private Transform sourceParent;
        [SerializeField, Min(1)] private int pooledLoopSourceCount = 8;
        [SerializeField, Min(0.05f)] private float loopContactTimeout = 0.25f;

        private readonly List<AudioSource> _loopPool = new List<AudioSource>();
        private readonly List<ContactLoopInstance> _activeLoops = new List<ContactLoopInstance>();
        private System.Random _rng;

        private void Awake()
        {
            InitializePool();
        }

        private void Update()
        {
            UpdateActiveLoops();
            CleanupExpiredLoops();
        }

        public void InitializePool()
        {
            _rng ??= new System.Random();
            if (_loopPool.Count >= pooledLoopSourceCount)
                return;

            for (int i = _loopPool.Count; i < pooledLoopSourceCount; i++)
            {
                AudioSource source = CreateSource($"ContactLoop_{i}");
                source.loop = true;
                _loopPool.Add(source);
            }
        }

        public void HandleContactEvent(ContactAudioEvent audioEvent)
        {
            if (audioEvent.type == ContactAudioEventType.None)
                return;

            if (audioEvent.isSustained)
                HandleSustained(audioEvent);
            else
                HandleTransient(audioEvent);
        }

        public void HandleTransient(ContactAudioEvent audioEvent)
        {
            ContactEventAudioRule rule = ResolveRule(audioEvent);
            if (rule.clips == null)
                return;

            AudioSource source = CreateSource("ContactTransient");
            ConfigureAndPlayClipSet(source, rule, audioEvent.intensity01);
            source.transform.position = audioEvent.point;
            Destroy(source.gameObject, 3f);
        }

        public void HandleSustained(ContactAudioEvent audioEvent)
        {
            ContactEventAudioRule rule = ResolveRule(audioEvent);
            if (rule.clips == null)
                return;

            ContactLoopInstance instance = null;
            for (int i = 0; i < _activeLoops.Count; i++)
            {
                if (_activeLoops[i].contactId == audioEvent.contactId)
                {
                    instance = _activeLoops[i];
                    break;
                }
            }

            if (instance == null)
            {
                AudioSource source = GetFreeLoopSource();
                if (source == null)
                    return;

                int clipIndex = rule.clips.GetNextClip(_rng, Time.time);
                if (clipIndex < 0 || clipIndex >= rule.clips.Clips.Length)
                    return;

                AudioClip clip = rule.clips.Clips[clipIndex].clip;
                if (clip == null)
                    return;

                source.clip = clip;
                source.transform.position = audioEvent.point;
                source.volume = 0f;
                source.pitch = Mathf.Max(0.1f, rule.pitchMultiplier);
                source.Play();
                rule.clips.NotifyPlayed(clipIndex, Time.time);

                instance = new ContactLoopInstance();
                instance.Initialize(audioEvent.contactId, source, ResolveProfile(audioEvent), audioEvent.type);
                _activeLoops.Add(instance);
            }

            instance.UpdateFromEvent(audioEvent, rule);
        }

        public ContactEventAudioRule ResolveRule(ContactAudioEvent audioEvent)
        {
            AudioInteractionProfileSO profile = ResolveProfile(audioEvent);
            return profile != null ? profile.GetRule(audioEvent.type) : default;
        }

        public AudioInteractionProfileSO ResolveProfile(ContactAudioEvent audioEvent)
        {
            return interactionMatrix != null
                ? interactionMatrix.ResolveProfile(audioEvent.materialA, audioEvent.materialB, audioEvent.type)
                : null;
        }

        public AudioSource GetFreeLoopSource()
        {
            for (int i = 0; i < _loopPool.Count; i++)
            {
                if (!_loopPool[i].isPlaying)
                    return _loopPool[i];
            }

            return _loopPool.Count > 0 ? _loopPool[0] : null;
        }

        public void UpdateActiveLoops()
        {
            for (int i = 0; i < _activeLoops.Count; i++)
            {
                ContactLoopInstance instance = _activeLoops[i];
                if (instance.AudioSource == null)
                    continue;

                instance.currentVolume = Mathf.MoveTowards(instance.currentVolume, instance.targetVolume, Time.deltaTime * 8f);
                instance.currentPitch = Mathf.MoveTowards(instance.currentPitch, instance.targetPitch, Time.deltaTime * 6f);
                instance.AudioSource.volume = instance.currentVolume;
                instance.AudioSource.pitch = instance.currentPitch;
            }
        }

        public void CleanupExpiredLoops()
        {
            float now = Time.time;
            for (int i = _activeLoops.Count - 1; i >= 0; i--)
            {
                ContactLoopInstance instance = _activeLoops[i];
                if (!instance.IsExpired(now, loopContactTimeout))
                    continue;

                instance.StopAndRelease();
                _activeLoops.RemoveAt(i);
            }
        }

        private void ConfigureAndPlayClipSet(AudioSource source, ContactEventAudioRule rule, float intensity01)
        {
            if (source == null || rule.clips == null)
                return;

            int clipIndex = rule.clips.GetNextClip(_rng, Time.time);
            if (clipIndex < 0 || clipIndex >= rule.clips.Clips.Length)
                return;

            AudioClip clip = rule.clips.Clips[clipIndex].clip;
            if (clip == null)
                return;

            source.clip = clip;
            source.loop = false;
            source.playOnAwake = false;
            source.volume = Mathf.Lerp(rule.clips.VolumeRange.x, rule.clips.VolumeRange.y, intensity01) * Mathf.Max(0.01f, rule.volumeMultiplier);
            source.pitch = Mathf.Lerp(rule.clips.PitchRange.x, rule.clips.PitchRange.y, intensity01) * Mathf.Max(0.1f, rule.pitchMultiplier);
            source.Play();
            rule.clips.NotifyPlayed(clipIndex, Time.time);
        }

        private AudioSource CreateSource(string objectName)
        {
            GameObject go = new GameObject(objectName);
            go.transform.SetParent(sourceParent != null ? sourceParent : transform, false);

            AudioSource template = transientSourceTemplate;
            AudioSource source = go.AddComponent<AudioSource>();
            if (template != null)
            {
                source.outputAudioMixerGroup = template.outputAudioMixerGroup;
                source.spatialBlend = template.spatialBlend;
                source.rolloffMode = template.rolloffMode;
                source.minDistance = template.minDistance;
                source.maxDistance = template.maxDistance;
                source.dopplerLevel = template.dopplerLevel;
            }

            source.playOnAwake = false;
            return source;
        }
    }

}