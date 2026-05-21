using UnityEngine;

namespace PungentFunk.Utilities.Audio
{
    [System.Serializable]
    public struct ContactRawInput
    {
        public AudioSurfaceMaterialSO materialA;
        public AudioSurfaceMaterialSO materialB;
        public Vector3 point;
        public Vector3 normal;
        public float normalSpeed;
        public float tangentialSpeed;
        public float duration;
        public bool isEnter;
        public bool isStay;
        public bool isExit;
        public bool isBounce;
        public GameObject sourceObject;
        public Collider otherCollider;
        public int contactId;
    }

    [DisallowMultipleComponent]
    public sealed class ContactEventClassifier : MonoBehaviour
    {
        [Header("Thresholds")]
        [SerializeField] private float lightImpactSpeed = 1.5f;
        [SerializeField] private float mediumImpactSpeed = 4f;
        [SerializeField] private float heavyImpactSpeed = 8f;
        [SerializeField] private float scrapeTangentialSpeed = 2f;
        [SerializeField] private float dragTangentialSpeed = 0.8f;
        [SerializeField] private float sustainedDuration = 0.15f;

        public ContactAudioEvent ClassifyEnter(ContactRawInput raw)
        {
            return BuildEvent(raw, DetermineType(raw));
        }

        public ContactAudioEvent ClassifyStay(ContactRawInput raw)
        {
            return BuildEvent(raw, DetermineType(raw));
        }

        public ContactAudioEvent ClassifyExit(ContactRawInput raw)
        {
            ContactAudioEventType type = raw.isBounce ? ContactAudioEventType.Bounce : ContactAudioEventType.Release;
            return BuildEvent(raw, type);
        }

        public ContactAudioEventType DetermineType(ContactRawInput raw)
        {
            if (raw.isExit)
                return raw.isBounce ? ContactAudioEventType.Bounce : ContactAudioEventType.Release;

            if (raw.isBounce)
                return ContactAudioEventType.Bounce;

            if (raw.isEnter)
            {
                if (raw.normalSpeed >= heavyImpactSpeed)
                    return ContactAudioEventType.HeavyImpact;
                if (raw.normalSpeed >= mediumImpactSpeed)
                    return ContactAudioEventType.MediumImpact;
                if (raw.normalSpeed >= lightImpactSpeed)
                    return ContactAudioEventType.LightImpact;
            }

            if (raw.tangentialSpeed >= scrapeTangentialSpeed && raw.duration >= sustainedDuration)
                return ContactAudioEventType.Scrape;

            if (raw.tangentialSpeed >= dragTangentialSpeed && raw.duration >= sustainedDuration * 0.5f)
                return ContactAudioEventType.Drag;

            if (raw.isStay && raw.tangentialSpeed > 0.1f)
                return ContactAudioEventType.Slide;

            return raw.isStay ? ContactAudioEventType.Brush : ContactAudioEventType.None;
        }

        public float ComputeIntensity01(ContactRawInput raw)
        {
            float impact01 = Mathf.InverseLerp(lightImpactSpeed, heavyImpactSpeed, raw.normalSpeed);
            float tangential01 = Mathf.InverseLerp(dragTangentialSpeed, scrapeTangentialSpeed * 2f, raw.tangentialSpeed);
            return Mathf.Clamp01(Mathf.Max(impact01, tangential01));
        }

        private ContactAudioEvent BuildEvent(ContactRawInput raw, ContactAudioEventType type)
        {
            return new ContactAudioEvent
            {
                type = type,
                materialA = raw.materialA,
                materialB = raw.materialB,
                point = raw.point,
                normal = raw.normal.sqrMagnitude > 0.0001f ? raw.normal.normalized : Vector3.up,
                normalSpeed = raw.normalSpeed,
                tangentialSpeed = raw.tangentialSpeed,
                intensity01 = ComputeIntensity01(raw),
                duration = raw.duration,
                sourceObject = raw.sourceObject,
                otherCollider = raw.otherCollider,
                isSustained = type == ContactAudioEventType.Scrape ||
                              type == ContactAudioEventType.Drag ||
                              type == ContactAudioEventType.Slide,
                contactId = raw.contactId
            };
        }
    }

}