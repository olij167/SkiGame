using UnityEngine;

/// <summary>
/// Deterministic, non-physics hanger for lift carriers.
/// Smoothly keeps a hanger (chair/t-bar assembly) below the cable anchor,
/// eliminating rope-physics spikes and rider jerk.
/// 
/// Now supports terrain-aware hang-length adjustment near stations so carriers
/// stay at a more reliable height for interaction.
/// </summary>
public class LiftCarrierHanger : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The moving cable anchor. If null, uses this transform's parent (recommended with LiftLine anchors).")]
    public Transform cableAnchor;

    [Tooltip("The transform that should hang below the cable (seat root / t-bar root).")]
    public Transform hanger;

    [Header("Hang Shape")]
    [Tooltip("Base vertical distance below the cable anchor (metres).")]
    public float hangLength = 2.0f;

    [Tooltip("Optional lateral offset in anchor-space (metres). Useful if your model pivot is not centered.")]
    public Vector3 localOffset = Vector3.zero;

    [Header("Terrain-Aware Hang Length")]
    [Tooltip("If enabled, adjust hang length near stations so the carrier stays closer to a desired ground clearance.")]
    public bool terrainAwareNearStations = true;

    [Tooltip("Layers considered ground for hanger clearance checks.")]
    public LayerMask terrainLayers = ~0;

    [Tooltip("Desired distance from the hanger root to the ground when station adjustment is active.")]
    public float desiredGroundClearance = 1.6f;

    [Tooltip("Downward raycast starts this far above the sample point.")]
    public float terrainRaycastStartHeight = 8f;

    [Tooltip("Maximum extra distance for the downward raycast.")]
    public float terrainRaycastMaxDistance = 25f;

    [Tooltip("Within this distance of a station center, terrain-based length adjustment begins.")]
    public float stationAdjustDistance = 14f;

    [Tooltip("Maximum additional hang length allowed beyond the base length.")]
    public float maxAdditionalHang = 2.5f;

    [Tooltip("Maximum shortening allowed below the base length.")]
    public float maxShortening = 0.75f;

    [Tooltip("How quickly the resolved hang length follows the target.")]
    public float hangLengthLerpSpeed = 8f;

    [Header("Smoothing")]
    [Tooltip("Seconds to smooth position changes (lower = snappier).")]
    public float positionSmoothTime = 0.08f;

    [Tooltip("Seconds to smooth rotation changes (lower = snappier).")]
    public float rotationSmoothTime = 0.12f;

    [Tooltip("Max speed allowed when catching up (m/s).")]
    public float maxCatchupSpeed = 25f;

    [Header("Orientation")]
    [Tooltip("If true, hanger faces cable direction but stays upright to world up.")]
    public bool faceCableUpright = true;

    private Vector3 _posVel;
    private float _currentResolvedHangLength;
    private LiftCarrier _carrier;

    private void Reset()
    {
        cableAnchor = transform.parent;
        hanger = transform;
    }

    private void Awake()
    {
        _carrier = GetComponentInParent<LiftCarrier>();
        _currentResolvedHangLength = hangLength;
    }

    private void LateUpdate()
    {
        if (!hanger) return;
        if (!cableAnchor) cableAnchor = transform.parent;
        if (_carrier == null) _carrier = GetComponentInParent<LiftCarrier>();

        Vector3 anchorPos = cableAnchor ? cableAnchor.position : transform.position;
        Vector3 offsetWorld = (cableAnchor ? cableAnchor.TransformVector(localOffset) : localOffset);

        float resolvedHangLength = ResolveHangLength(anchorPos, offsetWorld);
        _currentResolvedHangLength = Mathf.Lerp(
            _currentResolvedHangLength,
            resolvedHangLength,
            1f - Mathf.Exp(-hangLengthLerpSpeed * Time.deltaTime));

        Vector3 targetPos = anchorPos + Vector3.down * _currentResolvedHangLength + offsetWorld;

        hanger.position = Vector3.SmoothDamp(
            hanger.position,
            targetPos,
            ref _posVel,
            Mathf.Max(0.0001f, positionSmoothTime),
            Mathf.Max(0.01f, maxCatchupSpeed),
            Time.deltaTime
        );

        if (faceCableUpright && cableAnchor)
        {
            Vector3 fwd = cableAnchor.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;

            Quaternion targetRot = Quaternion.LookRotation(fwd.normalized, Vector3.up);

            float t = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.0001f, rotationSmoothTime));
            hanger.rotation = Quaternion.Slerp(hanger.rotation, targetRot, t);
        }
    }

    private float ResolveHangLength(Vector3 anchorPos, Vector3 offsetWorld)
    {
        float baseLength = Mathf.Max(0.05f, hangLength);

        if (!terrainAwareNearStations || _carrier == null || _carrier.line == null)
            return baseLength;

        LiftLine line = _carrier.line;
        if (line.bottomStation == null && line.topStation == null)
            return baseLength;

        Vector3 sampleWorld = anchorPos + offsetWorld;

        float nearestStationDist = float.PositiveInfinity;

        if (line.bottomStation != null)
            nearestStationDist = Mathf.Min(nearestStationDist, HorizontalDistance(sampleWorld, line.bottomStation.position));

        if (line.topStation != null)
            nearestStationDist = Mathf.Min(nearestStationDist, HorizontalDistance(sampleWorld, line.topStation.position));

        if (nearestStationDist > stationAdjustDistance)
            return baseLength;

        float stationWeight = 1f - Mathf.Clamp01(nearestStationDist / Mathf.Max(0.001f, stationAdjustDistance));

        Vector3 rayOrigin = sampleWorld + Vector3.up * terrainRaycastStartHeight;
        float rayDistance = terrainRaycastStartHeight + terrainRaycastMaxDistance;

        if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayDistance, terrainLayers, QueryTriggerInteraction.Ignore))
            return baseLength;

        float desiredLength = anchorPos.y + offsetWorld.y - (hit.point.y + desiredGroundClearance);

        float minLength = Mathf.Max(0.05f, baseLength - maxShortening);
        float maxLength = baseLength + Mathf.Max(0f, maxAdditionalHang);
        desiredLength = Mathf.Clamp(desiredLength, minLength, maxLength);

        return Mathf.Lerp(baseLength, desiredLength, stationWeight);
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}