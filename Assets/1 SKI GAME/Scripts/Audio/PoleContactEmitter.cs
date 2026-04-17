using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PoleContactEmitter : MonoBehaviour
{
    [SerializeField] private SkiAudioController skierAudio;
    [SerializeField] private ContactAudioRouter router;
    [SerializeField] private AudioSurfaceMaterialSO poleTipMaterial;
    [SerializeField] private Transform leftPoleTip;
    [SerializeField] private Transform rightPoleTip;
    [SerializeField, Min(0f)] private float poleProbeRadius = 0.04f;
    [SerializeField, Min(0f)] private float poleProbeDistance = 0.15f;
    [SerializeField] private LayerMask poleContactMask = ~0;

    private readonly Dictionary<int, PoleProbeState> _states = new Dictionary<int, PoleProbeState>();
    private ContactEventClassifier _classifier;
    private int _nextContactId = 1000;

    private struct PoleProbeState
    {
        public bool inContact;
        public int contactId;
        public Collider collider;
        public float startTime;
    }

    private void Awake()
    {
        _classifier = GetComponent<ContactEventClassifier>();
        if (_classifier == null)
            _classifier = gameObject.AddComponent<ContactEventClassifier>();
    }

    private void Update()
    {
        UpdatePole(0, leftPoleTip);
        UpdatePole(1, rightPoleTip);
    }

    public void UpdatePole(int key, Transform poleTip)
    {
        if (poleTip == null || router == null || _classifier == null)
            return;

        bool hitFound = ProbePoleTip(poleTip, out RaycastHit hit);
        _states.TryGetValue(key, out PoleProbeState state);

        if (hitFound)
        {
            if (!state.inContact || state.collider != hit.collider)
                HandlePoleEnter(key, hit);
            else
                HandlePoleStay(key, hit);
        }
        else if (state.inContact)
        {
            HandlePoleExit(key, state);
        }
    }

    public bool ProbePoleTip(Transform poleTip, out RaycastHit hit)
    {
        return Physics.SphereCast(
            poleTip.position,
            poleProbeRadius,
            -poleTip.up,
            out hit,
            poleProbeDistance,
            poleContactMask,
            QueryTriggerInteraction.Ignore);
    }

    public void HandlePoleEnter(int key, RaycastHit hit)
    {
        PoleProbeState state = new PoleProbeState
        {
            inContact = true,
            collider = hit.collider,
            startTime = Time.time,
            contactId = _nextContactId++
        };
        _states[key] = state;
        SendPoleEvent(hit, state, true, false, false);
    }

    public void HandlePoleStay(int key, RaycastHit hit)
    {
        PoleProbeState state = _states[key];
        SendPoleEvent(hit, state, false, true, false);
    }

    private void HandlePoleExit(int key, PoleProbeState state)
    {
        ContactRawInput raw = new ContactRawInput
        {
            materialA = poleTipMaterial != null ? poleTipMaterial : skierAudio != null ? skierAudio.PoleTipMaterial : null,
            materialB = ResolvePoleHitMaterial(state.collider),
            point = state.collider != null ? state.collider.bounds.ClosestPoint(transform.position) : transform.position,
            normal = Vector3.up,
            duration = Mathf.Max(0f, Time.time - state.startTime),
            isExit = true,
            sourceObject = gameObject,
            otherCollider = state.collider,
            contactId = state.contactId
        };

        router.HandleContactEvent(_classifier.ClassifyExit(raw));
        _states.Remove(key);
    }

    public AudioSurfaceMaterialSO ResolvePoleHitMaterial(Collider collider)
    {
        return AudioMaterialResolver.ResolveDominantSurface(
            collider,
            collider != null ? collider.bounds.center : transform.position,
            skierAudio != null ? skierAudio.TerrainProfile : null);
    }

    private void SendPoleEvent(RaycastHit hit, PoleProbeState state, bool enter, bool stay, bool exit)
    {
        ContactRawInput raw = new ContactRawInput
        {
            materialA = poleTipMaterial != null ? poleTipMaterial : skierAudio != null ? skierAudio.PoleTipMaterial : null,
            materialB = ResolvePoleHitMaterial(hit.collider),
            point = hit.point,
            normal = hit.normal,
            normalSpeed = skierAudio != null ? Mathf.Abs(skierAudio.Telemetry.verticalSpeed) : 0f,
            tangentialSpeed = skierAudio != null ? skierAudio.Telemetry.planarSpeed : 0f,
            duration = Mathf.Max(0f, Time.time - state.startTime),
            isEnter = enter,
            isStay = stay,
            isExit = exit,
            sourceObject = gameObject,
            otherCollider = hit.collider,
            contactId = state.contactId
        };

        ContactAudioEvent audioEvent = enter
            ? _classifier.ClassifyEnter(raw)
            : _classifier.ClassifyStay(raw);

        router.HandleContactEvent(audioEvent);
    }
}
