using UnityEngine;

/// <summary>
/// SkiFlagReactive
/// - Planted flags should NOT slide down the mountain.
/// - On hit, they should tilt based on hit direction/speed.
/// - On big hits, they can uproot and fly.
/// 
/// This implementation uses a planted ConfigurableJoint (recommended) rather than FreezePosition
/// so gravity + impulses create natural tilt around a base pivot without downhill sliding.
/// </summary>
[DisallowMultipleComponent]
public class SkiFlagReactive : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody rb;

    [Header("Hit Detection")]
    [Tooltip("Tags that can trigger hits (optional). If empty, any collider can trigger hits.")]
    [SerializeField] private string[] hitterTags;

    [Tooltip("Layers that can trigger hits (optional). If 0, all layers allowed.")]
    [SerializeField] private LayerMask hitterLayers = ~0;

    [Tooltip("Minimum relative speed to react.")]
    [SerializeField] private float minHitSpeed = 1.0f;

    [Tooltip("Clamp relative speed used for impulse calculation.")]
    [SerializeField] private float maxHitSpeed = 28.0f;

    [Header("Impulse Tuning")]
    [Tooltip("Scales the computed impulse magnitude. Think of this as 'how floppy' the pole is.")]
    [SerializeField] private float impulseScale = 0.55f;

    [Tooltip("Optional extra upward impulse fraction when uprooted.")]
    [SerializeField] private float uprootUpFraction = 0.25f;

    [Tooltip("How much spin to add when uprooted.")]
    [SerializeField] private float uprootSpinScale = 0.9f;

    [Tooltip("If true, also apply a small torque assist when planted (helps snappy tilt).")]
    [SerializeField] private bool useTorqueAssist = true;

    [Tooltip("Torque assist scale when planted.")]
    [SerializeField] private float plantedTorqueScale = 0.12f;

    [Header("Planted Behaviour")]
    [Tooltip("Time after a hit (while planted) before we re-freeze to a stable planted state.")]
    [SerializeField] private float freezeDelay = 0.35f;

    [Tooltip("Angular damping while planted (higher = settles faster).")]
    [SerializeField] private float plantedAngularDamping = 7.0f;

    [Tooltip("Angular damping while flying.")]
    [SerializeField] private float flyingAngularDamping = 0.6f;

    [Tooltip("Linear damping while flying.")]
    [SerializeField] private float flyingLinearDamping = 0.05f;

    [Header("Uproot Threshold")]
    [Tooltip("If computed impulse magnitude exceeds this, uproot and let the flag fly.")]
    [SerializeField] private float uprootImpulseThreshold = 11.0f;

    [Tooltip("Cooldown before we allow another uproot (prevents repeated triggers).")]
    [SerializeField] private float uprootCooldown = 0.75f;

    [Header("Planted Joint (Recommended)")]
    [SerializeField] private bool usePlantedJoint = true;

    [Tooltip("Optional: supply an existing anchor transform. If null, one will be created at runtime.")]
    [SerializeField] private Transform plantedAnchor;

    [Tooltip("Where the pole is planted, in RB local space. This MUST be at the base of the pole for correct tilt.")]
    [SerializeField] private Vector3 localBaseAnchor = Vector3.zero;

    [Tooltip("Max tilt left/right (degrees).")]
    [SerializeField] private float tiltLimitDegrees = 25f;

    [Tooltip("Max tilt forward/back (degrees).")]
    [SerializeField] private float swingLimitDegrees = 25f;

    [Tooltip("Joint damping (prevents long wobble tails).")]
    [SerializeField] private float jointAngularDamping = 12f;

    [Header("Debug")]
    [SerializeField] private bool drawDebug = false;
    [SerializeField] private Color debugAnchorColor = new Color(1f, 0.75f, 0.1f, 1f);

    // Internal state
    private ConfigurableJoint _joint;
    private Rigidbody _anchorRb;

    private bool _uprooted;
    private float _freezeAtTime;
    private float _lastUprootTime;

    private void Reset()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (rb == null)
        {
            Debug.LogError($"[{nameof(SkiFlagReactive)}] Missing Rigidbody on {name}.", this);
            enabled = false;
            return;
        }

        // IMPORTANT: Do not force centerOfMass to Vector3.zero.
        // That often makes tipping feel wrong unless your pivot/origin is exactly at the desired COM.
        // If you need custom COM, expose a field and apply an offset intentionally.

        rb.interpolation = RigidbodyInterpolation.None;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

        // Start planted.
        if (usePlantedJoint)
            EnsurePlantedJoint();

        SetPlanted(true);
    }

    private void FixedUpdate()
    {
        // If planted and we’ve allowed dynamics briefly for the hit response, re-stabilize after delay.
        if (!_uprooted && rb != null && !rb.isKinematic && _freezeAtTime > 0f && Time.time >= _freezeAtTime)
        {
            // Replant: kinematic again, joint keeps it pinned, angular velocity cleared for stability.
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;

            _freezeAtTime = 0f;
        }

        // Keep anchor positioned at the planted point even if parent moves slightly (optional robustness).
        if (!_uprooted && usePlantedJoint && _joint != null && plantedAnchor != null)
        {
            plantedAnchor.position = transform.TransformPoint(localBaseAnchor);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!enabled || rb == null) return;
        if (_uprooted) return;

        if (!IsValidHitter(collision.collider))
            return;

        // Relative velocity of the collision is the best signal.
        Vector3 relVel = collision.relativeVelocity;
        float speed = relVel.magnitude;
        if (speed < minHitSpeed) return;

        speed = Mathf.Min(speed, maxHitSpeed);

        // Use first contact point if available.
        Vector3 contactPoint = transform.position;
        Vector3 contactNormal = Vector3.up;

        if (collision.contactCount > 0)
        {
            var cp = collision.GetContact(0);
            contactPoint = cp.point;
            contactNormal = cp.normal;
        }

        HandleHit(relVel, speed, contactPoint, contactNormal);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Optional trigger-based reaction (if your flag uses triggers).
        if (!enabled || rb == null) return;
        if (_uprooted) return;

        if (!IsValidHitter(other))
            return;

        // With triggers we don’t have relative velocity, so approximate from hitter RB if present.
        Rigidbody otherRb = other.attachedRigidbody;
        if (otherRb == null) return;

        Vector3 relVel = otherRb.linearVelocity - rb.linearVelocity;
        float speed = relVel.magnitude;
        if (speed < minHitSpeed) return;

        speed = Mathf.Min(speed, maxHitSpeed);

        Vector3 contactPoint = other.ClosestPoint(transform.position);
        Vector3 contactNormal = (transform.position - contactPoint).sqrMagnitude > 1e-6f
            ? (transform.position - contactPoint).normalized
            : Vector3.up;

        HandleHit(relVel, speed, contactPoint, contactNormal);
    }

    private void HandleHit(Vector3 relVel, float speed, Vector3 contactPoint, Vector3 contactNormal)
    {
        // Direction: along the incoming velocity.
        Vector3 dir = relVel.normalized;

        // Compute an impulse magnitude.
        // We intentionally keep it simple: scale with speed and rb.mass.
        float Jmag = speed * rb.mass * impulseScale;

        Vector3 J = dir * Jmag;

        // Big hit => uproot.
        if (Time.time - _lastUprootTime >= uprootCooldown && Jmag >= uprootImpulseThreshold)
        {
            UprootAndFly(J, contactPoint);
            return;
        }

        // Planted hit: allow brief dynamics so physics + joint can tilt, then re-freeze.
        WakeDynamicPlanted();

        // Physically-correct: apply impulse at the contact point -> creates a moment around planted pivot.
        rb.AddForceAtPosition(J, contactPoint, ForceMode.Impulse);

        // Optional torque assist (kept small; joint does most work)
        if (useTorqueAssist)
        {
            Vector3 torqueAxis = Vector3.Cross(transform.up, dir);
            if (torqueAxis.sqrMagnitude < 1e-6f)
                torqueAxis = transform.right;

            float torqueMag = Jmag * plantedTorqueScale;
            rb.AddTorque(torqueAxis.normalized * torqueMag, ForceMode.Impulse);
        }

        // Schedule re-freeze.
        _freezeAtTime = Time.time + freezeDelay;
    }

    private void UprootAndFly(Vector3 impulse, Vector3 contactPoint)
    {
        _lastUprootTime = Time.time;
        _uprooted = true;

        // Detach joint so it can fly.
        if (_joint != null)
        {
            Destroy(_joint);
            _joint = null;
        }

        // Ensure dynamic
        rb.isKinematic = false;
        rb.constraints = RigidbodyConstraints.None;

        rb.linearDamping = flyingLinearDamping;
        rb.angularDamping = flyingAngularDamping;

        // Add uplift + impulse and spin.
        Vector3 up = Vector3.up;
        Vector3 flyImpulse = impulse + up * (impulse.magnitude * uprootUpFraction);

        rb.AddForceAtPosition(flyImpulse, contactPoint, ForceMode.Impulse);

        Vector3 spinAxis = Vector3.Cross(up, impulse.normalized);
        if (spinAxis.sqrMagnitude < 1e-6f) spinAxis = Random.onUnitSphere;
        rb.AddTorque(spinAxis.normalized * (impulse.magnitude * uprootSpinScale), ForceMode.Impulse);
    }

    private void SetPlanted(bool planted)
    {
        _uprooted = !planted;
        _freezeAtTime = 0f;

        if (planted)
        {
            if (usePlantedJoint)
                EnsurePlantedJoint();

            // Planted: kinematic until hit (we temporarily un-kinematic for reactions).
            rb.isKinematic = true;
            rb.constraints = RigidbodyConstraints.None; // joint handles translation lock

            rb.linearDamping = 0.2f;
            rb.angularDamping = plantedAngularDamping;

            // Ensure anchor is placed at the base point.
            if (usePlantedJoint && plantedAnchor != null)
                plantedAnchor.position = transform.TransformPoint(localBaseAnchor);
        }
        else
        {
            rb.isKinematic = false;
            rb.constraints = RigidbodyConstraints.None;

            rb.linearDamping = flyingLinearDamping;
            rb.angularDamping = flyingAngularDamping;
        }
    }

    private void WakeDynamicPlanted()
    {
        if (_uprooted) return;

        rb.isKinematic = false;
        rb.constraints = RigidbodyConstraints.None; // joint handles translation lock

        rb.linearDamping = 0.2f;
        rb.angularDamping = plantedAngularDamping;

        rb.WakeUp();
    }

    private void EnsurePlantedJoint()
    {
        if (!usePlantedJoint) return;

        if (plantedAnchor == null)
        {
            var go = new GameObject($"{name}_FlagAnchor");
            go.transform.SetPositionAndRotation(transform.TransformPoint(localBaseAnchor), Quaternion.identity);
            plantedAnchor = go.transform;
        }

        _anchorRb = plantedAnchor.GetComponent<Rigidbody>();
        if (_anchorRb == null) _anchorRb = plantedAnchor.gameObject.AddComponent<Rigidbody>();
        _anchorRb.isKinematic = true;
        _anchorRb.useGravity = false;

        _joint = GetComponent<ConfigurableJoint>();
        if (_joint == null) _joint = gameObject.AddComponent<ConfigurableJoint>();

        _joint.connectedBody = _anchorRb;

        // IMPORTANT: anchor pivot at the base of the pole (RB local space).
        _joint.anchor = localBaseAnchor;
        _joint.connectedAnchor = Vector3.zero;

        // No translation => no downhill sliding.
        _joint.xMotion = ConfigurableJointMotion.Locked;
        _joint.yMotion = ConfigurableJointMotion.Locked;
        _joint.zMotion = ConfigurableJointMotion.Locked;

        // Tilt only.
        _joint.angularYMotion = ConfigurableJointMotion.Locked;
        _joint.angularXMotion = ConfigurableJointMotion.Limited;
        _joint.angularZMotion = ConfigurableJointMotion.Limited;

        // Angular limits (degrees)
        var lowX = _joint.lowAngularXLimit;
        lowX.limit = -tiltLimitDegrees;
        _joint.lowAngularXLimit = lowX;

        var highX = _joint.highAngularXLimit;
        highX.limit = tiltLimitDegrees;
        _joint.highAngularXLimit = highX;

        var z = _joint.angularZLimit;
        z.limit = swingLimitDegrees;
        _joint.angularZLimit = z;

        // Damping only (no spring-back).
        var drive = _joint.angularXDrive;
        drive.positionSpring = 0f;
        drive.positionDamper = jointAngularDamping;
        drive.maximumForce = Mathf.Infinity;
        _joint.angularXDrive = drive;
        _joint.angularYZDrive = drive;

        _joint.rotationDriveMode = RotationDriveMode.XYAndZ;

        // Stability helpers
        _joint.projectionMode = JointProjectionMode.PositionAndRotation;
        _joint.enablePreprocessing = false;
    }

    private bool IsValidHitter(Collider col)
    {
        if (col == null) return false;

        if ((hitterLayers.value & (1 << col.gameObject.layer)) == 0)
            return false;

        if (hitterTags != null && hitterTags.Length > 0)
        {
            bool match = false;
            for (int i = 0; i < hitterTags.Length; i++)
            {
                var t = hitterTags[i];
                if (!string.IsNullOrEmpty(t) && col.CompareTag(t))
                {
                    match = true;
                    break;
                }
            }
            if (!match) return false;
        }

        return true;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawDebug) return;

        Rigidbody r = rb != null ? rb : GetComponent<Rigidbody>();
        if (r == null) return;

        Gizmos.color = debugAnchorColor;
        Vector3 worldAnchor = transform.TransformPoint(localBaseAnchor);
        Gizmos.DrawSphere(worldAnchor, 0.07f);
        Gizmos.DrawLine(worldAnchor, worldAnchor + Vector3.up * 0.5f);
    }
#endif
}
