using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//Code base sourced from user 'losingisfun' in this thread https://forum.unity.com/threads/audio-crossfade-how.144606/

namespace TimeWeather
{

    [RequireComponent(typeof(AudioSource))]
    public class CrossFadeAudio : MonoBehaviour
    {

        //We create an array with 2 audio sources that we will swap between for transitions
        private AudioSource[] aud = new AudioSource[2];

        private bool activeMusicSource;
        [Range(0f, 1f)] public float maxVolume;
        [Tooltip("10 is equivalent to a 1 second transition")]
        [Range(0, 60)] public int transitionDuration = 25;
        IEnumerator musicTransition;

        [Tooltip("0 = first audio source, 1 = second audio source")]
        [field: ReadOnlyField, SerializeField] private int currentSource;
        private int nextSource;

        private AudioSource audioSource;
        private AudioSource fadeOutSource;

        void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            fadeOutSource = gameObject.AddComponent<AudioSource>();
            fadeOutSource.loop = true;
            aud[0] = audioSource;
            aud[1] = fadeOutSource;
        }

        public void newSoundtrack(AudioClip clip, float volume)
        {
            nextSource = !activeMusicSource ? 0 : 1;
            currentSource = activeMusicSource ? 0 : 1;

            if (aud[currentSource].clip == null)
            {
                aud[currentSource].clip = clip;
                //aud[nextSource].pitch = pitch;
                aud[currentSource].volume = volume;

                aud[currentSource].Play();

                return;
            }

            //If the clip is already being played on the current audio source, we will end now and prevent the transition
            if (clip == aud[currentSource].clip)
                return;

            //If a transition is already happening, we stop it here to prevent our new Coroutine from competing
            if (musicTransition != null)
                StopCoroutine(musicTransition);

            aud[nextSource].clip = clip;

            if (!aud[nextSource].isPlaying)
                aud[nextSource].Play();

            musicTransition = transition(transitionDuration, volume); //20 is the equivalent to 2 seconds (More than 3 seconds begins to overlap for a bit too long)
            StartCoroutine(musicTransition);
        }

        //  'transitionDuration' is how many tenths of a second it will take, eg, 10 would be equal to 1 second
        IEnumerator transition(int transitionDuration, float volume)
        {
            for (int i = 0; i < transitionDuration + 1; i++)
            {
                aud[0].volume = !activeMusicSource ? (transitionDuration - i) * (volume / transitionDuration) : i * (volume / transitionDuration);
                aud[1].volume = activeMusicSource ? (transitionDuration - i) * (volume / transitionDuration) : i * (volume / transitionDuration);

                aud[0].volume *= maxVolume;
                aud[1].volume *= maxVolume;

                yield return new WaitForSecondsRealtime(0.1f);
                //use realtime otherwise if you pause the game you could pause the transition half way
            }

            //finish by stopping the audio clip on the now silent audio source
            aud[activeMusicSource ? 0 : 1].Stop();
            activeMusicSource = !activeMusicSource;

            musicTransition = null;
        }

        IEnumerator SmoothStopTransition(int stopDuration)
        {
            for (int i = 0; i < transitionDuration + 1; i++)
            {
                aud[currentSource].volume = Mathf.Lerp(aud[currentSource].volume, 0f, stopDuration);

                yield return new WaitForSecondsRealtime(0.1f);
            }

            aud[currentSource].Stop();
            activeMusicSource = !activeMusicSource;

            StopCoroutine(musicTransition);
        }
    }
}
