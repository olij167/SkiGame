using System.Collections;
using UnityEngine;

// Code base sourced from user 'losingisfun' in this thread https://forum.unity.com/threads/audio-crossfade-how.144606/
// Hardened for scene transitions / destroyed AudioSource references.

namespace TimeWeather
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public class CrossFadeAudio : MonoBehaviour
    {
        // We create an array with 2 audio sources that we will swap between for transitions
        private readonly AudioSource[] aud = new AudioSource[2];

        private bool activeMusicSource; // false => aud[0] is active, true => aud[1] is active
        [Range(0f, 1f)] public float maxVolume = 1f;

        [Tooltip("10 is equivalent to a 1 second transition")]
        [Range(0, 60)] public int transitionDuration = 25;

        private IEnumerator musicTransition;

        [Tooltip("0 = first audio source, 1 = second audio source")]
        [field: ReadOnlyField, SerializeField] private int currentSource;
        private int nextSource;

        private AudioSource audioSource;
        private AudioSource fadeOutSource;

        private static bool IsAlive(Object o) => o != null;

        private void Awake()
        {
            EnsureSources();
        }

        private void OnEnable()
        {
            EnsureSources();
        }

        /// <summary>
        /// Ensures aud[0] and aud[1] always reference valid AudioSources.
        /// This is critical because scene transitions / root disabling / singleton cleanup can destroy components.
        /// </summary>
        private void EnsureSources()
        {
            // Primary source (required)
            if (!IsAlive(audioSource))
                audioSource = GetComponent<AudioSource>();

            if (!IsAlive(audioSource))
                audioSource = gameObject.AddComponent<AudioSource>();

            // Secondary source (for crossfade)
            if (!IsAlive(fadeOutSource))
            {
                // Try to reuse an existing extra AudioSource on this GO first
                var sources = GetComponents<AudioSource>();
                for (int i = 0; i < sources.Length; i++)
                {
                    if (!IsAlive(sources[i])) continue;
                    if (sources[i] == audioSource) continue;

                    fadeOutSource = sources[i];
                    break;
                }

                if (!IsAlive(fadeOutSource))
                    fadeOutSource = gameObject.AddComponent<AudioSource>();
            }

            // Copy baseline settings so the two sources behave identically
            CopySettings(audioSource, fadeOutSource);

            // Ensure safe defaults
            audioSource.playOnAwake = false;
            fadeOutSource.playOnAwake = false;

            // These are used for music/ambience crossfade
            audioSource.loop = true;
            fadeOutSource.loop = true;

            // Cache into array
            aud[0] = audioSource;
            aud[1] = fadeOutSource;

            // Keep indices consistent with activeMusicSource meaning
            currentSource = activeMusicSource ? 1 : 0;
            nextSource = activeMusicSource ? 0 : 1;
        }

        private static void CopySettings(AudioSource src, AudioSource dst)
        {
            if (!IsAlive(src) || !IsAlive(dst)) return;

            // Keep the most relevant settings in sync.
            dst.outputAudioMixerGroup = src.outputAudioMixerGroup;
            dst.mute = src.mute;
            dst.bypassEffects = src.bypassEffects;
            dst.bypassListenerEffects = src.bypassListenerEffects;
            dst.bypassReverbZones = src.bypassReverbZones;

            dst.priority = src.priority;
            dst.volume = src.volume;
            dst.pitch = src.pitch;
            dst.panStereo = src.panStereo;
            dst.spatialBlend = src.spatialBlend;
            dst.reverbZoneMix = src.reverbZoneMix;

            dst.dopplerLevel = src.dopplerLevel;
            dst.spread = src.spread;
            dst.rolloffMode = src.rolloffMode;
            dst.minDistance = src.minDistance;
            dst.maxDistance = src.maxDistance;

            dst.loop = src.loop;
        }

        public void newSoundtrack(AudioClip clip, float volume)
        {
            EnsureSources();

            // If our sources are still invalid for any reason, fail gracefully.
            if (!IsAlive(aud[0]) || !IsAlive(aud[1]))
                return;

            // Null clip => stop both gracefully
            if (clip == null)
            {
                StopAllTransitions();

                if (IsAlive(aud[0])) aud[0].Stop();
                if (IsAlive(aud[1])) aud[1].Stop();

                if (IsAlive(aud[0])) aud[0].clip = null;
                if (IsAlive(aud[1])) aud[1].clip = null;

                return;
            }

            // Determine which is currently active
            currentSource = activeMusicSource ? 1 : 0;
            nextSource = activeMusicSource ? 0 : 1;

            // If the current has no clip, just start it there (no fade needed)
            if (aud[currentSource].clip == null)
            {
                aud[currentSource].clip = clip;
                aud[currentSource].volume = Mathf.Clamp01(volume) * maxVolume;
                if (!aud[currentSource].isPlaying)
                    aud[currentSource].Play();
                return;
            }

            // If the clip is already playing on the current audio source, prevent transition
            if (aud[currentSource].clip == clip)
                return;

            // Stop competing transition
            StopAllTransitions();

            // Setup next source
            aud[nextSource].clip = clip;
            aud[nextSource].volume = 0f;

            if (!aud[nextSource].isPlaying)
                aud[nextSource].Play();

            musicTransition = transition(transitionDuration, Mathf.Clamp01(volume));
            StartCoroutine(musicTransition);
        }

        private void StopAllTransitions()
        {
            if (musicTransition != null)
            {
                StopCoroutine(musicTransition);
                musicTransition = null;
            }
        }

        // 'transitionDuration' is how many tenths of a second it will take, eg, 10 would be equal to 1 second
        private IEnumerator transition(int durationTicks, float volume)
        {
            EnsureSources();

            // Cache local refs and revalidate each step
            for (int i = 0; i < durationTicks + 1; i++)
            {
                // If sources got destroyed mid-transition, abort safely
                if (!IsAlive(aud[0]) || !IsAlive(aud[1]))
                {
                    musicTransition = null;
                    yield break;
                }

                // When activeMusicSource is false, aud[0] is current (fading out) and aud[1] fades in.
                // When true, aud[1] is current and aud[0] fades in.
                float t = (durationTicks <= 0) ? 1f : (float)i / durationTicks;

                int fadeOut = activeMusicSource ? 1 : 0;
                int fadeIn = activeMusicSource ? 0 : 1;

                aud[fadeOut].volume = Mathf.Lerp(volume, 0f, t) * maxVolume;
                aud[fadeIn].volume = Mathf.Lerp(0f, volume, t) * maxVolume;

                yield return new WaitForSecondsRealtime(0.1f);
            }

            // Stop the now-silent source
            int stopped = activeMusicSource ? 1 : 0;
            if (IsAlive(aud[stopped]))
                aud[stopped].Stop();

            // Flip which one is active
            activeMusicSource = !activeMusicSource;
            musicTransition = null;
        }

        // Optional: Smooth stop for current track (not used by WeatherController in your trace)
        private IEnumerator SmoothStopTransition(int stopDurationTicks)
        {
            EnsureSources();

            currentSource = activeMusicSource ? 1 : 0;

            for (int i = 0; i < stopDurationTicks + 1; i++)
            {
                if (!IsAlive(aud[currentSource]))
                    yield break;

                aud[currentSource].volume = Mathf.Lerp(aud[currentSource].volume, 0f, 0.25f);
                yield return new WaitForSecondsRealtime(0.1f);
            }

            if (IsAlive(aud[currentSource]))
                aud[currentSource].Stop();

            musicTransition = null;
        }
    }
}
