using System.Collections.Generic;
using UnityEngine;
using SkiGame.Runs;

namespace SkiGame.Progression
{
    /// <summary>
    /// Tracks entering/committing/abandoning/completing SkiRunLine corridors,
    /// and records per-completion attempt stats into PlayerStatsProfile.runRecords.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunProgressTracker : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SkiController skiController;
        [SerializeField] private Rigidbody rb;

        [Header("Detection")]
        [Tooltip("How often we scan runs to find an initial run to start tracking.")]
        [SerializeField] private float scanIntervalSeconds = 0.2f;

        [Tooltip("Extra leeway added to corridor width when first entering a run.")]
        [SerializeField] private float enterClearanceMeters = 2.0f;

        [Tooltip("Vertical tolerance when considering corridor membership (helps bridges/overpasses).")]
        [SerializeField] private float verticalToleranceMeters = 25f;

        [Header("Commit / Lock")]
        [Tooltip("Once inside a run for at least this time, we consider the player committed (locked) to this run attempt.")]
        [SerializeField] private float commitAfterSecondsInside = 0.35f;

        [Tooltip("Or commit once they advance at least this far along the run from their entry point.")]
        [SerializeField] private float commitAfterAdvanceMeters = 8f;

        [Header("Abandon")]
        [Tooltip("If the player is this much beyond the corridor half-width, we start an abandon timer.")]
        [SerializeField] private float abandonExtraDistanceMeters = 6f;

        [Tooltip("How long they must remain beyond abandon distance before the run is abandoned.")]
        [SerializeField] private float abandonAfterSeconds = 2f;

        [Header("Completion")]
        [Tooltip("Required completion fraction, computed over the remaining run (from entry distance to end).")]
        [Range(0.5f, 1f)]
        [SerializeField] private float requiredCompletionFraction = 0.90f;

        [Tooltip("Must also get within this distance (XZ) of the run end to count as completed.")]
        [SerializeField] private float endProximityMeters = 12f;

        [Header("Attempt Stats")]
        [Tooltip("Minimum speed required to count time into average-speed integration.")]
        [SerializeField] private float minActiveSpeedMps = 0.25f;

        [Tooltip("If false, stacked frames do not contribute to avg-speed integration or distance splits.")]
        [SerializeField] private bool includeWhileStacked = false;

        [Tooltip("Max number of stored attempts per run record (older attempts are dropped).")]
        [SerializeField] private int maxAttemptsPerRun = 20;

        [Header("Visits")]
        [SerializeField] private bool recordSessionVisits = true;
        [SerializeField] private bool recordLifetimeVisits = true;

        private float _nextScanTime;

        // Active attempt state
        private SkiRunLine _run;
        private bool _committed;
        private float _enteredAtDist;
        private float _maxDistAlong;
        private float _timeEntered;
        private float _timeInside;
        private float _timeBeyondAbandon;

        // Attempt metrics
        private float _attemptStartTime;
        private float _speedIntegral;
        private float _activeTime;
        private float _topSpeed;
        private float _airTime;
        private float _airDistance;

        private float _onRouteDistance;
        private float _offRouteDistance;

        private bool _wasGrounded;
        private bool _inAir;
        private float _airStartTime;
        private Vector3 _airStartPos;

        private Vector3 _prevPos;
        private bool _hasPrev;

        private int _stacks;
        private readonly List<StackEventEntry> _stackEvents = new List<StackEventEntry>(8);

        private void Reset()
        {
            if (skiController == null) skiController = GetComponent<SkiController>();
            if (rb == null) rb = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            if (skiController == null) skiController = GetComponent<SkiController>();
            if (rb == null) rb = GetComponent<Rigidbody>();

            if (skiController != null)
                skiController.OnStacked += OnPlayerStacked;

            ClearAttemptState();
            _nextScanTime = Time.time + 0.05f;
        }

        private void OnDisable()
        {
            if (skiController != null)
                skiController.OnStacked -= OnPlayerStacked;
        }

        private void Update()
        {
            if (_run != null)
                return; // while we have an active attempt, we do not scan for a new run (locking logic)

            if (Time.time < _nextScanTime) return;
            _nextScanTime = Time.time + Mathf.Max(0.05f, scanIntervalSeconds);

            TryStartRunIfInsideAny();
        }

        private void FixedUpdate()
        {
            if (_run == null || skiController == null || rb == null)
                return;

            Vector3 pos = transform.position;

            // Update closest point / corridor relation
            if (!_run.TryGetClosestPointOnCenterlineXZ(
                    pos,
                    out float distAlong,
                    out float distToCenterXZ,
                    out float halfWidthMeters,
                    out Vector3 closest))
            {
                // If the run becomes invalid, abandon cleanly
                ClearAttemptState();
                return;
            }

            // Track max progression (distance-along, monotonic)
            if (distAlong > _maxDistAlong)
                _maxDistAlong = distAlong;

            // Corridor membership / abandon gating
            float abandonThreshold = halfWidthMeters + Mathf.Max(0f, abandonExtraDistanceMeters);
            bool beyondAbandon = distToCenterXZ > abandonThreshold;

            if (beyondAbandon)
                _timeBeyondAbandon += Time.fixedDeltaTime;
            else
                _timeBeyondAbandon = 0f;

            if (_timeBeyondAbandon >= abandonAfterSeconds)
            {
                // Run abandoned (uncommitted or committed)
                ClearAttemptState();
                return;
            }

            // Commit logic: once inside for a bit OR advanced enough, lock the attempt
            _timeInside += Time.fixedDeltaTime;
            float advanced = Mathf.Max(0f, _maxDistAlong - _enteredAtDist);
            if (!_committed)
            {
                if (_timeInside >= commitAfterSecondsInside || advanced >= commitAfterAdvanceMeters)
                    _committed = true;
            }

            // Attempt movement deltas
            float dt = Time.fixedDeltaTime;
            if (dt <= 0f) return;

            float speed = rb.linearVelocity.magnitude;
            bool stacked = skiController.IsStacked;

            // Air tracking within the run attempt
            bool grounded = skiController.IsRiderGrounded;
            if (_wasGrounded && !grounded)
            {
                _inAir = true;
                _airStartTime = Time.time;
                _airStartPos = pos;
            }
            else if (!_wasGrounded && grounded && _inAir)
            {
                float segAir = Mathf.Max(0f, Time.time - _airStartTime);
                float segDist = Vector3.Distance(_airStartPos, pos);
                _airTime += segAir;
                _airDistance += segDist;
                _inAir = false;
            }
            _wasGrounded = grounded;

            // Distance splits: on-route vs off-route (based on current dist-to-center vs halfWidth)
            if (!_hasPrev)
            {
                _prevPos = pos;
                _hasPrev = true;
            }
            else
            {
                float deltaDist = Vector3.Distance(_prevPos, pos);

                bool eligible = includeWhileStacked || !stacked;
                if (eligible)
                {
                    bool onRouteNow = distToCenterXZ <= halfWidthMeters;
                    if (onRouteNow) _onRouteDistance += deltaDist;
                    else _offRouteDistance += deltaDist;
                }

                _prevPos = pos;
            }

            // Speed metrics (avg/top) for the attempt
            if (speed > _topSpeed)
                _topSpeed = speed;

            bool active = (speed >= minActiveSpeedMps) && (includeWhileStacked || !stacked);
            if (active)
            {
                _speedIntegral += speed * dt;
                _activeTime += dt;
            }

            // Completion check:
            // completion fraction is computed from entry distance to end (so starting mid-run doesn't credit prior segments).
            float runLen = _run.GetTotalLengthMeters();
            float denom = Mathf.Max(0.001f, runLen - _enteredAtDist);
            float completionFrac = Mathf.Clamp01((_maxDistAlong - _enteredAtDist) / denom);

            // Must also get near the endpoint (to avoid weird “near-end but not actually finishing” cases)
            Vector3 end = _run.GetEndPointWorld();
            float endDistXZ = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(end.x, end.z));

            if (completionFrac >= requiredCompletionFraction && endDistXZ <= endProximityMeters)
            {
                CompleteRun(completionFrac);
                ClearAttemptState();
            }
        }

        private void TryStartRunIfInsideAny()
        {
            var mgr = PlayerStatsManager.Instance;
            var profile = mgr != null ? mgr.Profile : null;
            if (profile == null) return;

#if UNITY_2023_1_OR_NEWER
            var runs = Object.FindObjectsByType<SkiRunLine>(FindObjectsSortMode.None);
#else
            var runs = Object.FindObjectsOfType<SkiRunLine>();
#endif
            if (runs == null || runs.Length == 0)
                return;

            Vector3 pos = transform.position;

            SkiRunLine best = null;
            float bestDist = float.PositiveInfinity;
            float bestAlong = 0f;

            for (int i = 0; i < runs.Length; i++)
            {
                var r = runs[i];
                if (r == null) continue;

                if (!r.TryGetClosestPointOnCenterlineXZ(pos, out float along, out float distXZ, out float halfW, out _))
                    continue;

                if (distXZ <= (halfW + Mathf.Max(0f, enterClearanceMeters)))
                {
                    if (distXZ < bestDist)
                    {
                        best = r;
                        bestDist = distXZ;
                        bestAlong = along;
                    }
                }
            }

            if (best == null)
                return;

            // Start attempt
            _run = best;
            _committed = false;
            _enteredAtDist = bestAlong;
            _maxDistAlong = bestAlong;
            _timeEntered = Time.time;
            _timeInside = 0f;
            _timeBeyondAbandon = 0f;

            ResetAttemptMetrics();

            // Record visit (session/lifetime) + increment counts (anti-spam is handled by enter detection)
            if (recordSessionVisits)
            {
                profile.TryAddVisitedRunThisSession(best.RunId);
                profile.IncrementRunVisitCount(best.RunId, session: true);
            }

            if (recordLifetimeVisits)
            {
                profile.TryAddVisitedRun(best.RunId);
                profile.IncrementRunVisitCount(best.RunId, session: false);
            }

        }

        private void ResetAttemptMetrics()
        {
            _attemptStartTime = Time.time;
            _speedIntegral = 0f;
            _activeTime = 0f;
            _topSpeed = 0f;
            _airTime = 0f;
            _airDistance = 0f;
            _onRouteDistance = 0f;
            _offRouteDistance = 0f;

            _hasPrev = false;
            _prevPos = transform.position;

            _wasGrounded = (skiController != null) ? skiController.IsRiderGrounded : true;
            _inAir = false;
            _airStartTime = 0f;
            _airStartPos = Vector3.zero;

            _stacks = 0;
            _stackEvents.Clear();
        }

        private void ClearAttemptState()
        {
            _run = null;
            _committed = false;
            _enteredAtDist = 0f;
            _maxDistAlong = 0f;
            _timeEntered = 0f;
            _timeInside = 0f;
            _timeBeyondAbandon = 0f;

            _hasPrev = false;
            _stackEvents.Clear();
        }

        private void CompleteRun(float completionFraction)
        {
            var mgr = PlayerStatsManager.Instance;
            var profile = mgr != null ? mgr.Profile : null;
            if (profile == null || _run == null)
                return;

            float timeSeconds = Mathf.Max(0f, Time.time - _attemptStartTime);
            float avgSpeed = (_activeTime > 0.0001f) ? (_speedIntegral / _activeTime) : 0f;

            // Write attempt entry
            var attempt = new RunAttemptEntry
            {
                completedUtc = DateTimeUtc.Now(),
                timeSeconds = timeSeconds,
                averageSpeedMps = avgSpeed,
                topSpeedMps = _topSpeed,
                airTimeSeconds = _airTime,
                airDistanceMeters = _airDistance,
                onRouteDistanceMeters = _onRouteDistance,
                offRouteDistanceMeters = _offRouteDistance,
                stacks = _stacks,
                startDistanceMeters = _enteredAtDist,
                completionFraction = completionFraction,
                stackEvents = new List<StackEventEntry>(_stackEvents)
            };

            // Update per-run record
            var record = profile.GetOrCreateRunRecord(_run.RunId);
            record.RegisterAttempt(attempt, maxKeep: Mathf.Max(1, maxAttemptsPerRun));

            // Update global counters
            profile.lifetime.totalRunsCompleted++;
            profile.session.runsCompleted++;
            profile.TryAddCompletedRunThisSession(_run.RunId);

            // Clean run aggregates (still counts as completed regardless of stacks, but we track “clean”)
            if (_stacks == 0)
            {
                profile.lifetime.totalRunsCompletedClean++;
                profile.session.runsCompletedClean++;
            }

            // Best “top speed during a completed run”
            if (_topSpeed > profile.lifetime.topRunSpeedMps)
                profile.lifetime.topRunSpeedMps = _topSpeed;

            if (_topSpeed > profile.session.topRunSpeedMps)
                profile.session.topRunSpeedMps = _topSpeed;

            // (Optional) persist now
            // mgr.Save();
        }

        private void OnPlayerStacked(SkiController.StackEventInfo info)
        {
            // Only count stacks if currently in an attempt
            if (_run == null)
                return;

            _stacks++;

            // Record a small stack event history for UI/debug (cap it to avoid huge saves)
            if (_stackEvents.Count < 12)
            {
                _stackEvents.Add(new StackEventEntry
                {
                    timeSinceRunStartSeconds = Mathf.Max(0f, Time.time - _attemptStartTime),
                    positionWorld = info.position,
                    impactSpeedMps = info.velocity.magnitude,
                    severity01 = info.severity01,
                    reason = info.reason
                });
            }
        }
    }
}
