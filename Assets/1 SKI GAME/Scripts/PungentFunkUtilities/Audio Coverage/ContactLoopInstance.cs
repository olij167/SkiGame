using UnityEngine;

namespace PungentFunk.Utilities.Audio
{
    [System.Serializable]
    public sealed class ContactLoopInstance
    {
        public int contactId;
        public AudioSource AudioSource;
        public AudioInteractionProfileSO profile;
        public ContactAudioEventType type;
        public float currentVolume;
        public float targetVolume;
        public float currentPitch = 1f;
        public float targetPitch = 1f;
        public bool isActive;
        public float lastUpdateTime;

        public void Initialize(int newContactId, AudioSource source, AudioInteractionProfileSO newProfile, ContactAudioEventType newType)
        {
            contactId = newContactId;
            AudioSource = source;
            profile = newProfile;
            type = newType;
            currentVolume = 0f;
            targetVolume = 0f;
            currentPitch = AudioSource != null ? AudioSource.pitch : 1f;
            targetPitch = currentPitch;
            isActive = true;
            lastUpdateTime = Time.time;
        }

        public void UpdateFromEvent(ContactAudioEvent audioEvent, ContactEventAudioRule rule)
        {
            targetVolume = Mathf.Clamp01(audioEvent.intensity01) * Mathf.Max(0.01f, rule.volumeMultiplier);
            targetPitch = Mathf.Lerp(0.9f, 1.1f, Mathf.Clamp01(audioEvent.tangentialSpeed / 12f)) * Mathf.Max(0.5f, rule.pitchMultiplier);
            lastUpdateTime = Time.time;
            isActive = true;

            if (AudioSource != null)
                AudioSource.transform.position = audioEvent.point;
        }

        public void FadeOut(float fadeSpeed)
        {
            targetVolume = Mathf.MoveTowards(targetVolume, 0f, fadeSpeed * Time.deltaTime);
            if (AudioSource != null)
                AudioSource.volume = Mathf.MoveTowards(AudioSource.volume, targetVolume, fadeSpeed * Time.deltaTime);
        }

        public bool IsExpired(float timeNow, float timeout)
        {
            return !isActive || (timeNow - lastUpdateTime) > timeout;
        }

        public void StopAndRelease()
        {
            isActive = false;
            if (AudioSource == null)
                return;

            AudioSource.Stop();
            AudioSource.clip = null;
        }
    }

}