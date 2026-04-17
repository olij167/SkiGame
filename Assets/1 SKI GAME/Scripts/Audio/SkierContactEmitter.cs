using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SkierContactEmitter : MonoBehaviour
{
    [SerializeField] private SkiAudioController skierAudio;
    [SerializeField] private ContactAudioRouter router;
    [SerializeField] private AudioSurfaceMaterialSO bodyMaterial;
    [SerializeField, Min(0f)] private float minImpactVelocity = 1f;

    private readonly Dictionary<int, float> _contactStartTimes = new Dictionary<int, float>();
    private readonly Dictionary<int, int> _contactIds = new Dictionary<int, int>();
    private ContactEventClassifier _classifier;
    private int _nextContactId = 1;

    private void Awake()
    {
        _classifier = GetComponent<ContactEventClassifier>();
        if (_classifier == null)
            _classifier = gameObject.AddComponent<ContactEventClassifier>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        ProcessCollision(collision, enter: true, stay: false, exit: false);
    }

    private void OnCollisionStay(Collision collision)
    {
        ProcessCollision(collision, enter: false, stay: true, exit: false);
    }

    private void OnCollisionExit(Collision collision)
    {
        ProcessCollision(collision, enter: false, stay: false, exit: true);
        RemoveContactId(collision.collider);
    }

    private void ProcessCollision(Collision collision, bool enter, bool stay, bool exit)
    {
        if (collision == null || router == null || _classifier == null)
            return;

        ContactRawInput raw = BuildRawInput(collision, enter, stay, exit);
        if (raw.otherCollider == null)
            return;

        if (!exit && raw.normalSpeed < minImpactVelocity && raw.tangentialSpeed < minImpactVelocity)
            return;

        ContactAudioEvent audioEvent = enter
            ? _classifier.ClassifyEnter(raw)
            : exit
                ? _classifier.ClassifyExit(raw)
                : _classifier.ClassifyStay(raw);

        router.HandleContactEvent(audioEvent);
    }

    public ContactRawInput BuildRawInput(Collision collision, bool enter, bool stay, bool exit)
    {
        ContactPoint contact = collision.contactCount > 0 ? collision.GetContact(0) : default;
        Collider other = collision.collider;
        int contactId = GetOrCreateContactId(other);
        if (!_contactStartTimes.ContainsKey(contactId))
            _contactStartTimes[contactId] = Time.time;

        return new ContactRawInput
        {
            materialA = bodyMaterial != null ? bodyMaterial : skierAudio != null ? skierAudio.BodyMaterial : null,
            materialB = ResolveOtherMaterial(other),
            point = contact.point,
            normal = contact.normal.sqrMagnitude > 0.0001f ? contact.normal : Vector3.up,
            normalSpeed = ComputeNormalSpeed(collision, contact.normal),
            tangentialSpeed = ComputeTangentialSpeed(collision, contact.normal),
            duration = Mathf.Max(0f, Time.time - _contactStartTimes[contactId]),
            isEnter = enter,
            isStay = stay,
            isExit = exit,
            isBounce = false,
            sourceObject = gameObject,
            otherCollider = other,
            contactId = contactId
        };
    }

    public float ComputeNormalSpeed(Collision collision, Vector3 normal)
    {
        Vector3 relativeVelocity = collision.relativeVelocity;
        return Mathf.Abs(Vector3.Dot(relativeVelocity, normal.normalized));
    }

    public float ComputeTangentialSpeed(Collision collision, Vector3 normal)
    {
        Vector3 relativeVelocity = collision.relativeVelocity;
        return Vector3.ProjectOnPlane(relativeVelocity, normal.normalized).magnitude;
    }

    public AudioSurfaceMaterialSO ResolveOtherMaterial(Collider other)
    {
        return AudioMaterialResolver.ResolveDominantSurface(other, other != null ? other.bounds.center : transform.position, skierAudio != null ? skierAudio.TerrainProfile : null);
    }

    public int GetOrCreateContactId(Collider collider)
    {
        int key = collider != null ? collider.GetInstanceID() : 0;
        if (_contactIds.TryGetValue(key, out int contactId))
            return contactId;

        contactId = _nextContactId++;
        _contactIds[key] = contactId;
        return contactId;
    }

    public void RemoveContactId(Collider collider)
    {
        int key = collider != null ? collider.GetInstanceID() : 0;
        if (_contactIds.TryGetValue(key, out int contactId))
            _contactStartTimes.Remove(contactId);

        _contactIds.Remove(key);
    }
}
