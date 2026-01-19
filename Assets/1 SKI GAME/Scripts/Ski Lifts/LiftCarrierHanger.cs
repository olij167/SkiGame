using UnityEngine;

/// <summary>
/// Deterministic, non-physics hanger for lift carriers.
/// Smoothly keeps a hanger (chair/t-bar assembly) below the cable anchor,
/// eliminating rope-physics spikes and rider jerk.
/// </summary>
public class LiftCarrierHanger : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The moving cable anchor. If null, uses this transform's parent (recommended with LiftLine anchors).")]
    public Transform cableAnchor;

    [Tooltip("The transform that should hang below the cable (seat root / t-bar root).")]
    public Transform hanger;

    [Header("Hang Shape")]
    [Tooltip("Vertical distance below the cable anchor (metres).")]
    public float hangLength = 2.0f;

    [Tooltip("Optional lateral offset in anchor-space (metres). Useful if your model pivot is not centered.")]
    public Vector3 localOffset = Vector3.zero;

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

    private void Reset()
    {
        cableAnchor = transform.parent;
        hanger = transform;
    }

    private void LateUpdate()
    {
        if (!hanger) return;
        if (!cableAnchor) cableAnchor = transform.parent;

        // Target position: directly under the cable (world-down), plus optional anchor-space offset.
        Vector3 anchorPos = cableAnchor ? cableAnchor.position : transform.position;
        Vector3 offsetWorld = (cableAnchor ? cableAnchor.TransformVector(localOffset) : localOffset);

        Vector3 targetPos = anchorPos + Vector3.down * hangLength + offsetWorld;

        // Smooth position
        hanger.position = Vector3.SmoothDamp(
            hanger.position,
            targetPos,
            ref _posVel,
            Mathf.Max(0.0001f, positionSmoothTime),
            Mathf.Max(0.01f, maxCatchupSpeed),
            Time.deltaTime
        );

        // Smooth rotation
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
}
