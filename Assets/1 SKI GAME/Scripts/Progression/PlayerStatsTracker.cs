using UnityEngine;

namespace SkiGame.Progression
{
    /// <summary>
    /// Phase 2: Measures core stats from SkiController + Rigidbody and writes them into PlayerStatsManager.Profile.
    /// No allocations in FixedUpdate; no Input System dependency.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerStatsTracker : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SkiController skiController;
        [SerializeField] private Rigidbody rb;

        [Header("Tracking Gates")]
        [Tooltip("If false, stacked time/movement will not contribute to distance/speed/air stats.")]
        [SerializeField] private bool includeWhileStacked = false;

        [Tooltip("Minimum speed required to count time as 'active' for average speed and distance tracking.")]
        [SerializeField] private float minActiveSpeedMps = 0.25f;

        [Tooltip("If true, airborne segments are tracked (airtime + air distance).")]
        [SerializeField] private bool trackAir = true;

        [Tooltip("If position delta in a single FixedUpdate exceeds this, treat it as a teleport and do not count it.")]
        [SerializeField] private float teleportDistanceThresholdMeters = 25f;

        [Header("Optional Autosave")]
        [Tooltip("If > 0, periodically saves the profile during play to reduce loss on crash.")]
        [SerializeField] private float autosaveIntervalSeconds = 30f;

        private Vector3 _prevPos;
        private bool _hasPrev;
        private bool _wasGrounded;
        private bool _wasStacked;

        // Air segment state
        private bool _inAir;
        private float _airStartTime;
        private Vector3 _airStartPos;

        // Autosave
        private float _nextAutosaveTime;

        private void Reset()
        {
            // Attempt to auto-wire references in editor.
            if (skiController == null) skiController = GetComponent<SkiController>();
            if (rb == null) rb = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            if (skiController == null) skiController = GetComponent<SkiController>();
            if (rb == null) rb = GetComponent<Rigidbody>();

            ResetBaseline();

            _nextAutosaveTime = (autosaveIntervalSeconds > 0f)
                ? Time.time + autosaveIntervalSeconds
                : float.PositiveInfinity;
        }

        /// <summary>
        /// Call this if you respawn/teleport the player and want to avoid a one-frame distance spike.
        /// </summary>
        public void ResetBaseline()
        {
            _prevPos = transform.position;
            _hasPrev = true;

            _wasGrounded = (skiController != null) ? skiController.IsRiderGrounded : true;
            _wasStacked = (skiController != null) ? skiController.IsStacked : false;

            _inAir = false;
            _airStartTime = 0f;
            _airStartPos = Vector3.zero;
        }

        private void FixedUpdate()
        {
            var mgr = PlayerStatsManager.Instance;
            if (mgr == null) return;

            var profile = mgr.Profile;
            if (profile == null)
            {
                // If for any reason it's not loaded, load/create now.
                mgr.Load();
                profile = mgr.Profile;
                if (profile == null) return;
            }

            if (skiController == null || rb == null)
                return;

            bool grounded = skiController.IsRiderGrounded;
            bool stacked = skiController.IsStacked;

            // Stack rising edge => count a stack.
            if (!_wasStacked && stacked)
            {
                profile.lifetime.totalStacks++;
                profile.session.stacks++;
            }
            _wasStacked = stacked;

            // Air transitions (grounded edge detection).
            if (trackAir)
            {
                if (_wasGrounded && !grounded)
                {
                    // Takeoff
                    _inAir = true;
                    _airStartTime = Time.time;
                    _airStartPos = transform.position;
                }
                else if (!_wasGrounded && grounded && _inAir)
                {
                    // Landing
                    float airTime = Mathf.Max(0f, Time.time - _airStartTime);
                    float airDist = Vector3.Distance(_airStartPos, transform.position);

                    // If not including stacked time, only count if we didn't land stacked.
                    if (includeWhileStacked || !stacked)
                    {
                        profile.lifetime.totalAirTimeSeconds += airTime;
                        profile.lifetime.totalAirDistanceMeters += airDist;

                        profile.session.airTimeSeconds += airTime;
                        profile.session.airDistanceMeters += airDist;
                    }

                    _inAir = false;
                }
            }

            _wasGrounded = grounded;

            // Compute dt safely
            float dt = Time.fixedDeltaTime;
            if (dt <= 0f) return;

            // Speed
            float speed = rb.linearVelocity.magnitude;

            // Determine whether we count this frame as "active"
            bool eligible = includeWhileStacked || !stacked;
            bool active = eligible && (speed >= minActiveSpeedMps);

            // Distance & vertical travel
            Vector3 pos = transform.position;

            if (_hasPrev)
            {
                Vector3 delta = pos - _prevPos;
                float dist = delta.magnitude;

                // Teleport / big correction guard
                if (dist <= teleportDistanceThresholdMeters)
                {
                    if (active)
                    {
                        profile.lifetime.totalDistanceMeters += dist;
                        profile.session.distanceMeters += dist;

                        float dy = delta.y;
                        if (dy > 0f)
                        {
                            profile.lifetime.totalVerticalAscentMeters += dy;
                            profile.session.verticalAscentMeters += dy;
                        }
                        else if (dy < 0f)
                        {
                            float descent = -dy;
                            profile.lifetime.totalVerticalDescentMeters += descent;
                            profile.session.verticalDescentMeters += descent;
                        }
                    }
                }
                else
                {
                    // Teleport detected: reset baseline only (do not count distance)
                    _prevPos = pos;
                    return;
                }
            }

            _prevPos = pos;
            _hasPrev = true;

            // Top speed (count regardless of active gating, but respect includeWhileStacked)
            if (eligible)
            {
                if (speed > profile.lifetime.topSpeedMps) profile.lifetime.topSpeedMps = speed;
                if (speed > profile.session.topSpeedMps) profile.session.topSpeedMps = speed;
            }

            // Average speed (time-weighted over active time)
            if (active)
            {
                profile.lifetime.speedIntegral += speed * dt;
                profile.lifetime.activeTimeSeconds += dt;

                profile.session.speedIntegral += speed * dt;
                profile.session.activeTimeSeconds += dt;
            }

            // Optional periodic autosave
            if (autosaveIntervalSeconds > 0f && Time.time >= _nextAutosaveTime)
            {
                mgr.Save();
                _nextAutosaveTime = Time.time + autosaveIntervalSeconds;
            }
        }
    }
}
