using UnityEngine;

namespace SkiGame.Runs
{
    [DisallowMultipleComponent]
    public sealed class SkiFlagReactive : MonoBehaviour
    {
        [Header("References (prefer on prefab)")]
        [SerializeField] private Rigidbody rb;

        [Header("Planted State")]
        [Tooltip("While planted, we lock position but allow rotation.")]
        [SerializeField]
        private RigidbodyConstraints plantedConstraints =
            RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezePositionZ;

        [Tooltip("How strongly the pole resists being ripped out (N*s). Higher = more planted.")]
        [SerializeField] private float pullOutImpulse = 18f;

        [Tooltip("Scales how much tilt torque is applied while planted.")]
        [SerializeField] private float plantedTorqueScale = 0.55f;

        [Tooltip("Max angular velocity while planted (rad/s). Keeps things tame.")]
        [SerializeField] private float maxPlantedAngularSpeed = 8f;

        [Header("Uproot / Flying")]
        [Tooltip("Extra upward impulse when uprooted (scaled by hit impulse).")]
        [SerializeField] private float uprootUpBoost = 0.20f;

        [Tooltip("Extra spin when uprooted (scaled by hit impulse).")]
        [SerializeField] private float uprootSpinScale = 0.18f;

        [Header("Settle & Freeze (kills wobble tails)")]
        [Tooltip("Seconds after impact before we freeze the planted pole in its current tilt.")]
        [SerializeField] private float freezeDelay = 0.18f;

        [Tooltip("Angular speed below which we consider it settled (rad/s).")]
        [SerializeField] private float settleAngularSpeed = 0.45f;

        [Header("Hit rate limit")]
        [SerializeField] private float hitCooldownSeconds = 0.08f;

        private float _nextHitTime;
        private bool _uprooted;
        private float _freezeAtTime;

        private void Awake()
        {
            if (rb == null) rb = GetComponent<Rigidbody>();

            if (rb == null)
            {
                Debug.LogWarning($"[{nameof(SkiFlagReactive)}] Missing Rigidbody on {name}", this);
                enabled = false;
                return;
            }

            // Make the base pivot behave like the anchor point.
            rb.centerOfMass = Vector3.zero;

            // Start planted.
            SetPlanted(true);
        }

        private void FixedUpdate()
        {
            // If planted and we've passed the freeze time, kill motion once it's settled.
            if (!_uprooted && Time.time >= _freezeAtTime && _freezeAtTime > 0f)
            {
                if (rb.angularVelocity.magnitude <= settleAngularSpeed)
                {
                    // Freeze in current tilt (no spring-back).
                    rb.angularVelocity = Vector3.zero;
                    rb.linearVelocity = Vector3.zero;
                    rb.isKinematic = true;
                    _freezeAtTime = 0f;
                }
            }

            // Cap angular speed while planted so it doesn't keep wobbling.
            if (!_uprooted && !rb.isKinematic)
            {
                float w = rb.angularVelocity.magnitude;
                if (w > maxPlantedAngularSpeed)
                    rb.angularVelocity = rb.angularVelocity * (maxPlantedAngularSpeed / w);
            }
        }

        private void OnCollisionEnter(Collision collision) => HandleHit(collision);
        // IMPORTANT: no OnCollisionStay -> prevents repeated “pumping”.

        private void HandleHit(Collision collision)
        {
            if (Time.time < _nextHitTime) return;
            _nextHitTime = Time.time + Mathf.Max(0.01f, hitCooldownSeconds);

            Vector3 relV = collision.relativeVelocity;
            if (relV.sqrMagnitude < 0.04f) return; // ignore micro contacts

            Vector3 dir = relV.normalized;
            Vector3 contact = (collision.contactCount > 0) ? collision.GetContact(0).point : transform.position;

            // Use an impulse estimate even when kinematic.
            Vector3 J = EstimateImpulse(collision, relV);
            float Jmag = J.magnitude;
            if (Jmag <= 0.001f) return;

            // Uproot if the hit impulse overcomes "pull-out strength".
            if (!_uprooted && Jmag >= pullOutImpulse)
            {
                UprootAndFly(J, contact);
                return;
            }

            // Otherwise: planted tilt. We apply torque impulse relative to hit direction.
            if (_uprooted)
            {
                // Already flying: just add a nudge.
                WakeDynamicFlying();
                rb.AddForceAtPosition(J, contact, ForceMode.Impulse);
                return;
            }

            // Ensure dynamic for a moment so physics produces the tilt.
            WakeDynamicPlanted();

            Vector3 torqueAxisWorld = Vector3.Cross(transform.up, dir);
            if (torqueAxisWorld.sqrMagnitude < 1e-6f)
                torqueAxisWorld = transform.right;

            // Torque magnitude scales smoothly with impulse.
            float torqueMag = Jmag * plantedTorqueScale;
            rb.AddTorque(torqueAxisWorld.normalized * torqueMag, ForceMode.Impulse);

            // Schedule freeze once it settles so it doesn't wobble for ages.
            _freezeAtTime = Time.time + freezeDelay;
        }

        private Vector3 EstimateImpulse(Collision collision, Vector3 relV)
        {
            // If we're dynamic, Unity provides impulse.
            if (!rb.isKinematic && collision.impulse.sqrMagnitude > 0.0001f)
                return collision.impulse;

            // Reduced mass impulse estimate: J ~= μ * Δv
            float mSelf = Mathf.Max(0.01f, rb.mass);

            float mOther = 75f; // skier mass fallback
            if (collision.rigidbody != null)
                mOther = Mathf.Max(0.01f, collision.rigidbody.mass);

            float mu = (mSelf * mOther) / (mSelf + mOther);
            return relV * mu;
        }

        private void SetPlanted(bool planted)
        {
            _uprooted = !planted;

            rb.isKinematic = true; // “cold” until hit
            rb.constraints = planted ? plantedConstraints : RigidbodyConstraints.None;

            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rb.interpolation = RigidbodyInterpolation.None;

            // Damping to avoid oscillation tails.
            rb.linearDamping = planted ? 0.2f : 0.05f;
            rb.angularDamping = planted ? 6.0f : 0.5f;

            _freezeAtTime = 0f;
        }

        private void WakeDynamicPlanted()
        {
            rb.isKinematic = false;
            rb.constraints = plantedConstraints; // keep position locked
            rb.WakeUp();
        }

        private void WakeDynamicFlying()
        {
            rb.isKinematic = false;
            rb.constraints = RigidbodyConstraints.None;
            rb.WakeUp();
        }

        private void UprootAndFly(Vector3 J, Vector3 contact)
        {
            _uprooted = true;

            WakeDynamicFlying();

            // Add a small upward component based on impulse.
            Vector3 up = Vector3.up * (J.magnitude * uprootUpBoost);
            rb.AddForceAtPosition(J + up, contact, ForceMode.Impulse);

            // Add some spin for visual interest (scaled by impulse).
            Vector3 spinAxis = Vector3.Cross(Vector3.up, J.normalized);
            if (spinAxis.sqrMagnitude < 1e-6f) spinAxis = Random.onUnitSphere;
            rb.AddTorque(spinAxis.normalized * (J.magnitude * uprootSpinScale), ForceMode.Impulse);
        }

        // Optional public hook if you ever want to replant manually.
        public void ReplantHere()
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            SetPlanted(true);
        }
    }
}
