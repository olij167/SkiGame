using System.Collections.Generic;
using UnityEngine;
using SkiGame.Runs;
using TimeWeather;
using System;

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

        [Tooltip("To count as a full completion, entry must be within the first X fraction of the run (0..1).")]
        [Range(0f, 0.5f)]
        [SerializeField] private float maxEntryFractionForCompletion = 0.10f;

        [Header("Partial Segments")]
        [SerializeField] private bool recordPartialSegments = true;

        [Tooltip("Minimum time inside a run attempt before we record a partial segment on exit.")]
        [SerializeField] private float minPartialTimeSeconds = 2.0f;

        [Tooltip("Minimum along-distance coverage (meters) before we record a partial segment.")]
        [SerializeField] private float minPartialCoverageMeters = 12.0f;

        [Tooltip("If true, leaving one run corridor and entering another will immediately switch runs (records a partial segment).")]
        [SerializeField] private bool allowRunSwitching = true;

        [Tooltip("Partial segments below this coverage fraction are not recorded in history (0..1).")]
        [Range(0f, 1f)]
        [SerializeField] private float minCoverageFractionToLog = 0.25f;

        // Read-only: threshold used to decide whether a partial attempt is logged.
        public float MinCoverageFractionToLog01 => Mathf.Clamp01(minCoverageFractionToLog);

        // Last attempt snapshot (persists until the next attempt starts).
        private bool _hasLastAttempt;
        private LastAttemptSummary _lastAttempt;

        public bool TryGetLastAttemptSummary(out LastAttemptSummary s)
        {
            if (_hasLastAttempt)
            {
                s = _lastAttempt;
                return true;
            }

            s = default;
            return false;
        }

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

        [Header("Progress Stability")]
        [Tooltip("Meters behind the current best distance-along we will still consider when finding the closest point. Helps prevent snapping to earlier overlapping segments.")]
        [SerializeField] private float snapWindowBackMeters = 25f;

        [Tooltip("Meters ahead of the current best distance-along to search when finding the closest point.")]
        [SerializeField] private float snapWindowForwardMeters = 90f;

        [Tooltip("How far backwards (meters) we allow the closest-point solver to jump when committed. Larger values tolerate switchbacks, smaller values prevent snapping to earlier overlaps.")]
        [SerializeField] private float allowBacktrackMeters = 6f;

        [Tooltip("When we have a last segment index, constrain the closest-point search to this many segments behind it.")]
        [SerializeField] private int continuityBackSegments = 12;

        [Tooltip("When we have a last segment index, constrain the closest-point search to this many segments ahead of it.")]
        [SerializeField] private int continuityForwardSegments = 30;

        [Tooltip("Reject progress samples that would advance more than this minimum in a single FixedUpdate tick (meters).")]
        [SerializeField] private float maxAdvanceStepMinMeters = 2.0f;

        [Tooltip("Reject progress samples that would advance more than speed*dt*this factor (meters).")]
        [SerializeField] private float maxAdvanceStepSpeedMultiplier = 2.2f;

        [Tooltip("Extra slack added to max advance step (meters).")]
        [SerializeField] private float maxAdvanceStepExtraMeters = 1.5f;

        [Tooltip("Only allow increasing maxDistAlong when within (halfWidth + this) meters of the centerline.")]
        [SerializeField] private float progressAdvanceLeewayMeters = 1.5f;

        [Tooltip("Meters of along-distance drift from entry required before we decide run direction (forward vs reverse).")]
        [SerializeField] private float directionDetectMeters = 4f;

        [SerializeField] private float alongBiasWeight = 0.0025f; // start small

        [Header("Tracking Robustness")]
        [SerializeField, Tooltip("How many meters per second the along-parameter is allowed to move (prevents snapping).")]
        private float alongFollowSpeedMps = 35f;

        [SerializeField, Tooltip("Extra meters per FixedUpdate we allow on top of alongFollowSpeedMps*dt (helps high-speed).")]
        private float alongFollowExtraMeters = 2.0f;

        // Internal smoothing state
        private bool _hasTrackedAlong;
        private float _trackedAlong;

        [SerializeField, Tooltip("How long we tolerate closest-point failures before abandoning/recording the attempt.")]
        private float lostTrackingGraceSeconds = 0.75f;

        [SerializeField, Tooltip("Minimum horizontal speed before we trust velocity-based direction detection.")]
        private float directionMinSpeedMps = 1.0f;

        [SerializeField, Tooltip("Dot threshold vs segment direction before direction is considered stable.")]
        private float directionDotThreshold = 0.25f;

        // Internal
        private float _lostTrackingTime;

        [Header("Completion Spam Guard")]
        [SerializeField, Tooltip("After completing a run, we must leave its corridor before it can be completed again.")]
        private bool requireExitAfterCompletion = true;

        [SerializeField, Tooltip("Extra distance beyond (halfWidth+enterClearance) required to consider the run 'exited' after completion.")]
        private float exitClearanceExtraMeters = 4f;

        [Header("Run Cache")]
        [SerializeField, Tooltip("How often we refresh the cached list of SkiRunLine objects (seconds).")]
        private float runsCacheRefreshSeconds = 2.0f;

        private readonly List<SkiRunLine> _cachedRuns = new List<SkiRunLine>(128);
        private float _nextRunsCacheRefreshTime;

        [Header("Run Switching")]
        [SerializeField, Tooltip("If true, we can switch attempts to a different run corridor without waiting for abandon.")]
        private bool enableRunSwitching = true;

        [SerializeField, Tooltip("Candidate run must remain best for this long before we switch (seconds).")]
        private float switchConfirmSeconds = 0.25f;

        [SerializeField, Tooltip("If the new run is closer than current by at least this many meters, we prefer switching.")]
        private float switchPreferMarginMeters = 2.0f;

        private string _pendingSwitchRunId;
        private float _pendingSwitchTime;

        [Header("Debug")]
        [SerializeField, Tooltip("Enable verbose logs to diagnose run progress resets, solver failures, corridor exits, and attempt finalization.")]
        private bool debugRunTracking = false;

        [SerializeField, Tooltip("Minimum seconds between throttled debug logs to avoid Console spam while still capturing state transitions.")]
        private float debugLogMinIntervalSeconds = 0.35f;

        private float _debugNextLogTime;
        private int _debugSeq;


        // Stability cache for along-distance queries (mirrors SkiRunLine distance cache but per-attempt).
        private readonly List<float> _runCumDist = new List<float>(256);
        private float _runTotalLenMeters;
        private int _lastSegIndex = -1;
        private float _lastSegT;

        // Continuity anchor
        private int _trackSegIndex = -1;
        private float _trackSegT = 0f;


        private float _nextScanTime;

        // Active attempt state
        private SkiRunLine _run;
        private bool _committed;
        private float _enteredAtDist;
        private float _maxDistAlong;
        private float _minDistAlong;
        private int _directionSign; // 0 unknown, +1 forward (toward end), -1 reverse (toward start)

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

        private float _lastInsideDistAlong;
        private bool _hasLastInside;

        private bool _wasGrounded;
        private bool _inAir;
        private float _airStartTime;
        private Vector3 _airStartPos;

        private Vector3 _prevPos;
        private bool _hasPrev;

        private int _stacks;
        private readonly List<StackEventEntry> _stackEvents = new List<StackEventEntry>(8);

        private string _blockedRunIdAfterCompletion;
        private bool _waitingForExitAfterCompletion;

        private void Reset()
        {
            if (skiController == null) skiController = GetComponent<SkiController>();
            if (rb == null) rb = GetComponent<Rigidbody>();
        }

        private void DebugLog(string message, bool force = false)
        {
            if (!debugRunTracking)
                return;

            float now = Time.unscaledTime;
            if (!force && now < _debugNextLogTime)
                return;

            _debugNextLogTime = now + Mathf.Max(0.05f, debugLogMinIntervalSeconds);
            _debugSeq++;

            string runId = _run != null ? _run.RunId : "(none)";
            Debug.Log(
                $"[RunProgressTracker #{_debugSeq}] run={runId} committed={_committed} dir={_directionSign} " +
                $"entered={_enteredAtDist:F1} max={_maxDistAlong:F1} min={_minDistAlong:F1} " +
                $"tInside={_timeInside:F2} tBeyond={_timeBeyondAbandon:F2} | {message}",
                this
            );
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

            if (_waitingForExitAfterCompletion)
            {
                DebugLog($"Waiting for exit after completion... blockedRun={_blockedRunIdAfterCompletion}");

                // Clear the block once we're clearly outside the completed run corridor.
                if (!IsInsideRunCorridor(_blockedRunIdAfterCompletion))
                {
                    _waitingForExitAfterCompletion = false;
                    _blockedRunIdAfterCompletion = null;
                }
                // IMPORTANT: do NOT return here.
                // We still want to start OTHER runs even if we're still inside the completed run corridor.
            }

            if (Time.time < _nextScanTime) return;
            _nextScanTime = Time.time + Mathf.Max(0.05f, scanIntervalSeconds);

            // Ignore the blocked run while waiting-for-exit, but allow others.
            string ignore = _waitingForExitAfterCompletion ? _blockedRunIdAfterCompletion : null;

            if (TryFindBestRunAtPosition(transform.position, ignore, out var best, out var along, out var distXZ, out var segIdx, out var segT))
            {
                StartAttempt(best, along, segIdx, segT, distXZ);
            }
        }

        private void FixedUpdate()
        {
            if (_run == null || skiController == null || rb == null)
                return;

            Vector3 pos = transform.position;

            float dt = Time.fixedDeltaTime;
            if (dt <= 0f) return;

            float speed = rb.linearVelocity.magnitude;

            // Maximum plausible along-advance for this tick (snap protection)
            float maxAdvance = Mathf.Max(
    maxAdvanceStepMinMeters,
    (alongFollowSpeedMps * dt) + alongFollowExtraMeters
);

            float maxAllowedAhead = Mathf.Max(15f, maxAdvance * 6f);

            float distAlong, distToCenterXZ, halfWidthMeters;
            Vector3 closest;
            int segIdx;
            float segT;

            int segMax = (_run.PointsWorld != null) ? (_run.PointsWorld.Count - 2) : 0;
            int segStart = 0;
            int segEnd = segMax;

            if (_trackSegIndex >= 0 && segMax > 0)
            {
                segStart = Mathf.Clamp(_trackSegIndex - Mathf.Max(1, continuityBackSegments), 0, segMax);
                segEnd = Mathf.Clamp(_trackSegIndex + Mathf.Max(1, continuityForwardSegments), 0, segMax);
            }

            float runLen = (_runTotalLenMeters > 0.001f) ? _runTotalLenMeters : _run.GetTotalLengthMeters();
            if (runLen <= 0.001f)
            {
                // If the run has no valid length, we cannot track reliably.
                ClearAttemptState();
                return;
            }

            // Anchor for solver bias (keep it simple and stable)
            float anchorAlong =
                (_directionSign < 0) ? _minDistAlong :
                (_directionSign > 0) ? _maxDistAlong :
                (_committed ? _maxDistAlong : _enteredAtDist);

            float minAllowedAlong;
            float maxAllowedAlong;

            if (_directionSign < 0)
            {
                minAllowedAlong = Mathf.Max(0f, anchorAlong - maxAllowedAhead);
                maxAllowedAlong = Mathf.Min(runLen, anchorAlong + Mathf.Max(0f, allowBacktrackMeters));
            }
            else
            {
                minAllowedAlong = _committed
                    ? Mathf.Max(0f, anchorAlong - Mathf.Max(0f, allowBacktrackMeters))
                    : 0f;

                maxAllowedAlong = Mathf.Min(runLen, anchorAlong + maxAllowedAhead);
            }

            bool ok = TryGetClosestPointForTracking(
                pos,
                segStart, segEnd,
                minAllowedAlong,
                maxAllowedAlong,
                anchorAlong,
                out distAlong, out distToCenterXZ, out halfWidthMeters, out closest,
                out segIdx, out segT);

            // Relaxation pass: broaden window if constraints were too tight
            if (!ok)
            {
                float relaxedMin = Mathf.Max(0f, anchorAlong - Mathf.Max(10f, snapWindowBackMeters));
                float relaxedMax = Mathf.Min(runLen, anchorAlong + Mathf.Max(10f, snapWindowForwardMeters));

                ok = TryGetClosestPointForTracking(
                    pos,
                    0, segMax,
                    relaxedMin,
                    relaxedMax,
                    anchorAlong,
                    out distAlong, out distToCenterXZ, out halfWidthMeters, out closest,
                    out segIdx, out segT);
            }

            if (!ok)
            {
                ok = TryGetClosestPointForTracking(
                    pos,
                    0, segMax,
                    0f,
                    runLen,
                    anchorAlong,
                    out distAlong, out distToCenterXZ, out halfWidthMeters, out closest,
                    out segIdx, out segT);
            }

            if (!ok)
            {
                // DO NOT instantly reset attempts; tolerate brief solver failures.
                float prevLost = _lostTrackingTime;
                _lostTrackingTime += dt;

                if (prevLost <= 0f)
                {
                    DebugLog(
                        $"Lost tracking started: segWindow={segStart}-{segEnd} segMax={segMax} " +
                        $"allowedAlong=[{minAllowedAlong:F1},{maxAllowedAlong:F1}] anchor={anchorAlong:F1} runLen={runLen:F1} " +
                        $"trackSeg={_trackSegIndex} pos=({pos.x:F1},{pos.y:F1},{pos.z:F1})",
                        force: true
                    );
                }

                if (_lostTrackingTime < Mathf.Max(0.05f, lostTrackingGraceSeconds))
                    return;

                DebugLog($"Lost tracking expired after {_lostTrackingTime:F2}s (grace={lostTrackingGraceSeconds:F2}s)", force: true);

                if (_committed)
                    RecordPartialAndClear("Lost tracking");
                else
                    ClearAttemptState("Lost tracking (uncommitted)");

                return;
            }

            // We have a valid sample again
            _lostTrackingTime = 0f;


            float abandonThreshold = halfWidthMeters + Mathf.Max(0f, abandonExtraDistanceMeters);
            bool insideCorridor = distToCenterXZ <= abandonThreshold;

            // Declare usedAlong early so it can be referenced below.
            float usedAlong = distAlong;

            // --- Run switching (corridor-to-corridor) ---
            if (recordPartialSegments && enableRunSwitching && _run != null)
            {
                // If we just completed a run and are waiting-for-exit, we still allow switching
                // because Update() won't start a new attempt while _run != null.
                string ignoreBlocked = (_waitingForExitAfterCompletion ? _blockedRunIdAfterCompletion : null);

                if (TryFindBestRunAtPosition(pos, ignoreBlocked, out var cand, out var candAlong, out var candDistXZ, out var candSegIdx, out var candSegT))
                {
                    if (cand != null && cand != _run)
                    {
                        // Heuristic: switch if we're beyond abandon on current, or candidate is meaningfully closer.
                        bool beyondCurrent = distToCenterXZ > abandonThreshold;
                        bool candidateMuchCloser = (candDistXZ + Mathf.Max(0f, switchPreferMarginMeters)) < distToCenterXZ;

                        if (beyondCurrent || candidateMuchCloser)
                        {
                            string candId = cand.RunId;

                            if (_pendingSwitchRunId != candId)
                            {
                                _pendingSwitchRunId = candId;
                                _pendingSwitchTime = 0f;
                            }
                            else
                            {
                                _pendingSwitchTime += dt;

                                if (_pendingSwitchTime >= Mathf.Max(0.05f, switchConfirmSeconds))
                                {
                                    DebugLog($"Switching run: {_run.RunId} -> {candId} (beyondCurrent={beyondCurrent} candCloser={candidateMuchCloser})", force: true);

                                    // Finalize current attempt (partial if it meets thresholds)
                                    if (_committed) RecordPartialAndClear($"Switched to {candId}");
                                    else ClearAttemptState($"Switched to {candId} (uncommitted)");

                                    // Start new attempt immediately
                                    StartAttempt(cand, candAlong, candSegIdx, candSegT, candDistXZ);

                                    _pendingSwitchRunId = null;
                                    _pendingSwitchTime = 0f;

                                    return; // we restarted state; avoid continuing with old variables this tick
                                }
                            }
                        }
                        else
                        {
                            _pendingSwitchRunId = null;
                            _pendingSwitchTime = 0f;
                        }
                    }
                    else
                    {
                        _pendingSwitchRunId = null;
                        _pendingSwitchTime = 0f;
                    }
                }
            }

            // Track last "inside-ish" sample for exit snapshots
            if (insideCorridor)
            {
                _lastInsideDistAlong = usedAlong;
                _hasLastInside = true;
            }

            // Vertical reject (stacked overlaps): treat as "beyond abandon" but don't instantly clear.
            if (verticalToleranceMeters > 0f && Mathf.Abs(pos.y - closest.y) > verticalToleranceMeters)
            {
                float dy = Mathf.Abs(pos.y - closest.y);

                if (distToCenterXZ > abandonThreshold)
                {
                    if (_timeBeyondAbandon <= 0f)
                    {
                        DebugLog(
                            $"Vertical mismatch started: dy={dy:F1}m tol={verticalToleranceMeters:F1}m distXZ={distToCenterXZ:F1}m thresh={abandonThreshold:F1}m",
                            force: true
                        );
                    }

                    _timeBeyondAbandon += dt;
                    DebugLog($"Vertical mismatch (timer): t={_timeBeyondAbandon:F2}/{abandonAfterSeconds:F2}");

                    if (_timeBeyondAbandon >= abandonAfterSeconds)
                    {
                        DebugLog("Vertical mismatch expired -> finalize", force: true);

                        if (_committed) RecordPartialAndClear("Left corridor (vertical)");
                        else ClearAttemptState("Left corridor (vertical, uncommitted)");
                    }

                    return;
                }

                // If XZ is inside the corridor, ignore vertical mismatch and continue tracking.
            }

            // --- Along smoothing (replaces snap rejection) ---
            // Clamp solver snaps so tracking can't deadlock.
            usedAlong = distAlong;

            if (insideCorridor)
            {
                if (!_hasTrackedAlong)
                {
                    _trackedAlong = distAlong;
                    _hasTrackedAlong = true;
                    DebugLog($"TrackedAlong init: {_trackedAlong:F1} (solver={distAlong:F1})", force: true);
                }
                else
                {
                    float prev = _trackedAlong;
                    _trackedAlong = Mathf.MoveTowards(_trackedAlong, distAlong, maxAdvance);
                    usedAlong = _trackedAlong;

                    // Log only when solver jumps significantly and gets clamped
                    if (Mathf.Abs(distAlong - prev) > maxAdvance * 2f)
                        DebugLog($"Along snap clamped: solver={distAlong:F1} prev={prev:F1} used={usedAlong:F1} maxStep={maxAdvance:F1}");
                }
            }
            else
            {
                // Outside corridor: don't update trackedAlong (avoid drifting to wrong branch)
                usedAlong = _hasTrackedAlong ? _trackedAlong : distAlong;
            }

            if (insideCorridor && _directionSign < 0 && usedAlong > distAlong + 5f)
            {
                DebugLog($"Inside corridor but solver behind: used={usedAlong:F1} solver={distAlong:F1}");
            }

            // Corridor membership / abandon gating
            // IMPORTANT: rejected samples should not drive abandon timers
            bool beyondAbandon = (distToCenterXZ > abandonThreshold);

            float prevBeyondTime = _timeBeyondAbandon;

            if (beyondAbandon)
            {
                if (prevBeyondTime <= 0f)
                    DebugLog($"Beyond abandon started: distXZ={distToCenterXZ:F1} thresh={abandonThreshold:F1}", force: true);

                _timeBeyondAbandon += dt;
                DebugLog($"Beyond abandon (timer): t={_timeBeyondAbandon:F2}/{abandonAfterSeconds:F2}");
            }
            else
            {
                if (prevBeyondTime > 0f)
                    DebugLog("Back inside corridor (abandon timer reset)", force: true);

                _timeBeyondAbandon = 0f;
            }

            // Update progress bounds when inside the corridor (NOT the narrower leeway threshold)
            if (insideCorridor)
            {
                if (usedAlong > _maxDistAlong) _maxDistAlong = usedAlong;
                if (usedAlong < _minDistAlong) _minDistAlong = usedAlong;

                _trackSegIndex = segIdx;
                _trackSegT = segT;

                // Direction detection:
                // 1) Prefer along-drift from the entry point (stable even if segment vectors are odd).
                // 2) Fall back to velocity vs segment direction only if still ambiguous.
                if (_directionSign == 0)
                {
                    float fwd = Mathf.Max(0f, _maxDistAlong - _enteredAtDist);
                    float rev = Mathf.Max(0f, _enteredAtDist - _minDistAlong);
                    float th = Mathf.Max(0.1f, directionDetectMeters);

                    if (fwd >= th || rev >= th)
                    {
                        // Pick the dominant drift direction.
                        _directionSign = (fwd >= rev) ? 1 : -1;
                    }
                    else if (speed >= Mathf.Max(0.01f, directionMinSpeedMps))
                    {
                        // Fall back: velocity projected onto the current segment direction.
                        var pts = _run.PointsWorld;
                        if (pts != null && segIdx >= 0 && segIdx + 1 < pts.Count)
                        {
                            Vector3 segDir = pts[segIdx + 1] - pts[segIdx];
                            Vector3 segDirXZ = new Vector3(segDir.x, 0f, segDir.z);

                            Vector3 vel = rb.linearVelocity;
                            Vector3 velXZ = new Vector3(vel.x, 0f, vel.z);

                            if (segDirXZ.sqrMagnitude > 1e-6f && velXZ.sqrMagnitude > 1e-6f)
                            {
                                float dot = Vector3.Dot(velXZ.normalized, segDirXZ.normalized);
                                float dotTh = Mathf.Clamp(directionDotThreshold, 0.05f, 0.95f);

                                if (dot >= dotTh) _directionSign = 1;
                                else if (dot <= -dotTh) _directionSign = -1;
                            }
                        }
                    }
                }
            }

            // ---- Direction-aware completion fraction + end proximity ----
            bool withinCompletionCorridor = insideCorridor;

            float completionFrac;
            bool nearEnd = false;

            var pts2 = _run.PointsWorld;

            bool NearPointXZ(Vector3 point)
            {
                float dXZ = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(point.x, point.z));
                return dXZ <= endProximityMeters;
            }

            if (_directionSign < 0)
            {
                float denom = Mathf.Max(0.001f, _enteredAtDist);
                completionFrac = Mathf.Clamp01((_enteredAtDist - _minDistAlong) / denom);

                bool nearStartAlong = _minDistAlong <= endProximityMeters;
                bool nearStartXZ = (pts2 != null && pts2.Count >= 1) ? NearPointXZ(pts2[0]) : false;
                nearEnd = nearStartAlong || nearStartXZ;
            }
            else if (_directionSign > 0)
            {
                float denom = Mathf.Max(0.001f, runLen - _enteredAtDist);
                completionFrac = Mathf.Clamp01((_maxDistAlong - _enteredAtDist) / denom);

                float remainingToEnd = Mathf.Max(0f, runLen - _maxDistAlong);
                bool nearEndAlong = remainingToEnd <= endProximityMeters;
                bool nearEndXZ = (pts2 != null && pts2.Count >= 2) ? NearPointXZ(pts2[pts2.Count - 1]) : false;
                nearEnd = nearEndAlong || nearEndXZ;
            }
            else
            {
                float forwardDenom = Mathf.Max(0.001f, runLen - _enteredAtDist);
                float forwardFrac = Mathf.Clamp01((_maxDistAlong - _enteredAtDist) / forwardDenom);
                float forwardRemain = Mathf.Max(0f, runLen - _maxDistAlong);
                bool forwardNear = (forwardRemain <= endProximityMeters) ||
                                   (pts2 != null && pts2.Count >= 2 && NearPointXZ(pts2[pts2.Count - 1]));

                float reverseDenom = Mathf.Max(0.001f, _enteredAtDist);
                float reverseFrac = Mathf.Clamp01((_enteredAtDist - _minDistAlong) / reverseDenom);
                bool reverseNear = (_minDistAlong <= endProximityMeters) ||
                                   (pts2 != null && pts2.Count >= 1 && NearPointXZ(pts2[0]));

                if (reverseFrac > forwardFrac)
                {
                    completionFrac = reverseFrac;
                    nearEnd = reverseNear;
                }
                else
                {
                    completionFrac = forwardFrac;
                    nearEnd = forwardNear;
                }
            }

            float entryFrac01 = (runLen > 0.001f) ? Mathf.Clamp01(_enteredAtDist / runLen) : 0f;
            bool validStartForCompletion = entryFrac01 <= maxEntryFractionForCompletion;

            if (_committed && validStartForCompletion && completionFrac >= requiredCompletionFraction && nearEnd && withinCompletionCorridor)
            {
                CompleteRun(completionFrac);
                ClearAttemptState();
                return;
            }

            if (_committed && !validStartForCompletion && nearEnd && withinCompletionCorridor)
            {
                RecordPartialAndClear("Reached end (partial)");
                return;
            }

            if (_timeBeyondAbandon >= abandonAfterSeconds)
            {
                RecordPartialAndClear("Abandoned");
                return;
            }

            // Commit logic
            if (insideCorridor)
                _timeInside += dt;
            else
                _timeInside = 0f; // optional but recommended: must be continuously inside to commit

            float advanced;
            if (_directionSign < 0) advanced = Mathf.Max(0f, _enteredAtDist - _minDistAlong);
            else if (_directionSign > 0) advanced = Mathf.Max(0f, _maxDistAlong - _enteredAtDist);
            else advanced = Mathf.Max(
                Mathf.Max(0f, _maxDistAlong - _enteredAtDist),
                Mathf.Max(0f, _enteredAtDist - _minDistAlong)
            );

            if (!_committed)
            {
                if (_timeInside >= commitAfterSecondsInside || advanced >= commitAfterAdvanceMeters)
                    _committed = true;
            }

            bool stacked = skiController.IsStacked;

            // Air tracking
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

            // Distance splits: on-route vs off-route
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

            // Speed metrics (avg/top)
            if (speed > _topSpeed)
                _topSpeed = speed;

            bool active = (speed >= minActiveSpeedMps) && (includeWhileStacked || !stacked);
            if (active)
            {
                _speedIntegral += speed * dt;
                _activeTime += dt;
            }
        }

        private bool TryFindBestRunAtPosition(
    Vector3 pos,
    string ignoreRunId,
    out SkiRunLine best,
    out float bestAlong,
    out float bestDistXZ,
    out int bestSegIdx,
    out float bestSegT)
        {
            best = null;
            bestAlong = 0f;
            bestDistXZ = float.PositiveInfinity;
            bestSegIdx = -1;
            bestSegT = 0f;

            RefreshRunsCacheIfNeeded();
            if (_cachedRuns.Count == 0)
                return false;

            for (int i = 0; i < _cachedRuns.Count; i++)
            {
                var r = _cachedRuns[i];
                if (r == null) continue;

                if (!string.IsNullOrEmpty(ignoreRunId) &&
                    string.Equals(r.RunId, ignoreRunId, StringComparison.Ordinal))
                    continue;

                if (!r.TryGetClosestPointOnCenterlineXZ_Detailed(
                        pos,
                        out float along,
                        out float distXZ,
                        out float halfW,
                        out Vector3 closest,
                        out int segIdx,
                        out float segT))
                    continue;

                // Reject stacked overlaps (bridges / switchbacks) by height
                if (verticalToleranceMeters > 0f && Mathf.Abs(pos.y - closest.y) > verticalToleranceMeters)
                    continue;

                if (distXZ <= (halfW + Mathf.Max(0f, enterClearanceMeters)))
                {
                    if (distXZ < bestDistXZ)
                    {
                        best = r;
                        bestDistXZ = distXZ;
                        bestAlong = along;
                        bestSegIdx = segIdx;
                        bestSegT = segT;
                    }
                }
            }

            return best != null;
        }

        private void StartAttempt(SkiRunLine run, float enteredAlong, int segIdx, float segT, float distXZForDebug)
        {
            var mgr = PlayerStatsManager.Instance;
            var profile = mgr != null ? mgr.Profile : null;
            if (profile == null || run == null) return;

            _run = run;
            _hasLastAttempt = false;

            _committed = false;
            _enteredAtDist = enteredAlong;
            _maxDistAlong = enteredAlong;
            _minDistAlong = enteredAlong;
            _lastInsideDistAlong = enteredAlong;
            _hasLastInside = true;
            _directionSign = 0;

            _hasTrackedAlong = false;
            _trackedAlong = enteredAlong;

            _trackSegIndex = segIdx;
            _trackSegT = segT;

            _timeEntered = Time.time;
            _timeInside = 0f;
            _timeBeyondAbandon = 0f;
            _lostTrackingTime = 0f;

            BuildRunDistanceCache(run);

            _lastSegIndex = -1;
            _lastSegT = 0f;

            ResetAttemptMetrics();

            // Visits
            if (recordSessionVisits)
            {
                profile.TryAddVisitedRunThisSession(run.RunId);
                profile.IncrementRunVisitCount(run.RunId, session: true);
            }

            if (recordLifetimeVisits)
            {
                profile.TryAddVisitedRun(run.RunId);
                profile.IncrementRunVisitCount(run.RunId, session: false);
            }

            DebugLog(
                $"Start attempt: run={run.RunId} name='{run.RunName}' enteredAlong={enteredAlong:F1}m distXZ={distXZForDebug:F1}m seg={segIdx} t={segT:F2}",
                force: true
            );
        }

        private void TryStartRunIfInsideAny()
        {
            var mgr = PlayerStatsManager.Instance;
            var profile = mgr != null ? mgr.Profile : null;
            if (profile == null) return;

#if UNITY_2023_1_OR_NEWER
            var runs = UnityEngine.Object.FindObjectsByType<SkiRunLine>(FindObjectsSortMode.None);
#else
            var runs = Object.FindObjectsOfType<SkiRunLine>();
#endif
            if (runs == null || runs.Length == 0)
                return;

            Vector3 pos = transform.position;

            SkiRunLine best = null;
            float bestDist = float.PositiveInfinity;
            float bestAlong = 0f;
            int bestSegIdx = -1;
            float bestSegT = 0f;

            for (int i = 0; i < runs.Length; i++)
            {
                var r = runs[i];
                if (r == null) continue;

                if (!r.TryGetClosestPointOnCenterlineXZ_Detailed(
     pos,
     out float along,
     out float distXZ,
     out float halfW,
     out Vector3 closest,
     out int segIdx,
     out float segT))
                    continue;

                // Reject stacked overlaps (bridges / switchbacks) by height
                if (verticalToleranceMeters > 0f && Mathf.Abs(pos.y - closest.y) > verticalToleranceMeters)
                    continue;

                if (distXZ <= (halfW + Mathf.Max(0f, enterClearanceMeters)))
                {
                    if (distXZ < bestDist)
                    {
                        best = r;
                        bestDist = distXZ;
                        bestAlong = along;
                        bestSegIdx = segIdx;
                        bestSegT = segT;

                    }
                }
            }

            if (best == null)
                return;

            // Start attempt
            _run = best;
            _hasLastAttempt = false;

            _committed = false;
            _enteredAtDist = bestAlong;
            _maxDistAlong = bestAlong;
            _minDistAlong = bestAlong;
            _lastInsideDistAlong = bestAlong;
            _hasLastInside = true;
            _directionSign = 0;
            _hasTrackedAlong = false;
            _trackedAlong = bestAlong;

            _trackSegIndex = bestSegIdx;
            _trackSegT = bestSegT;

            _timeEntered = Time.time;
            _timeInside = 0f;
            _timeBeyondAbandon = 0f;

            BuildRunDistanceCache(best);
            _lastSegIndex = -1;
            _lastSegT = 0f;

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

            DebugLog(
    $"Start attempt: run={best.RunId} name='{best.RunName}' enteredAlong={bestAlong:F1}m distXZ={bestDist:F1}m seg={bestSegIdx} t={bestSegT:F2} " +
    $"halfW={best.GetHalfWidthMetersAtSample(bestSegIdx, bestSegT):F1}m pos=({pos.x:F1},{pos.y:F1},{pos.z:F1})",
    force: true
);

        }

        private bool IsInsideRunCorridor(string runId)
        {
            if (string.IsNullOrEmpty(runId)) return false;

#if UNITY_2023_1_OR_NEWER
            var runs = UnityEngine.Object.FindObjectsByType<SkiRunLine>(FindObjectsSortMode.None);
#else
    var runs = Object.FindObjectsOfType<SkiRunLine>();
#endif
            if (runs == null) return false;

            Vector3 pos = transform.position;

            for (int i = 0; i < runs.Length; i++)
            {
                var r = runs[i];
                if (r == null) continue;
                if (!string.Equals(r.RunId, runId, StringComparison.Ordinal)) continue;

                if (!r.TryGetClosestPointOnCenterlineXZ_Detailed(
                    pos,
                    out float along,
                    out float distXZ,
                    out float halfW,
                    out Vector3 closest,
                    out int segIdx,
                    out float segT))
                    return false;

                if (verticalToleranceMeters > 0f && Mathf.Abs(pos.y - closest.y) > verticalToleranceMeters)
                    return false;

                float threshold = halfW + Mathf.Max(0f, enterClearanceMeters) + Mathf.Max(0f, exitClearanceExtraMeters);
                return distXZ <= threshold;
            }

            return false;
        }

        private void BuildRunDistanceCache(SkiRunLine run)
        {
            _runCumDist.Clear();
            _runTotalLenMeters = 0f;

            if (run == null) return;

            var pts = run.PointsWorld;
            if (pts == null || pts.Count < 2) return;

            _runCumDist.Add(0f);
            for (int i = 0; i < pts.Count - 1; i++)
            {
                _runTotalLenMeters += Vector3.Distance(pts[i], pts[i + 1]);
                _runCumDist.Add(_runTotalLenMeters);
            }
        }

        private bool TryGetClosestPointForTracking(
     Vector3 worldPos,
     int segStart,
     int segEnd,
     float minAllowedAlong,
     float maxAllowedAlong,
     float anchorAlong,
     out float distanceAlongMeters,
     out float distToCenterXZ,
     out float halfWidthMeters,
     out Vector3 closest,
     out int segIndex,
     out float segT)
        {
            distanceAlongMeters = 0f;
            distToCenterXZ = 0f;
            halfWidthMeters = 0f;
            closest = default;
            segIndex = -1;
            segT = 0f;

            if (_run == null) return false;

            var pts = _run.PointsWorld;
            if (pts == null || pts.Count < 2) return false;

            // Prefer our per-attempt cache (already built on run start)
            if (_runCumDist == null || _runCumDist.Count < 2)
                BuildRunDistanceCache(_run);

            if (_runCumDist == null || _runCumDist.Count < 2)
                return false;

            int segMax = pts.Count - 2;
            segStart = Mathf.Clamp(segStart, 0, segMax);
            segEnd = Mathf.Clamp(segEnd, 0, segMax);
            if (segEnd < segStart) (segStart, segEnd) = (segEnd, segStart);

            Vector2 p = new Vector2(worldPos.x, worldPos.z);

            float bestScore = float.PositiveInfinity;
            float bestXzD2 = float.PositiveInfinity;

            int bestSeg = -1;
            float bestT = 0f;
            Vector3 bestClosest = default;
            float bestAlong = 0f;

            float w = Mathf.Max(0f, alongBiasWeight);

            for (int i = segStart; i <= segEnd; i++)
            {
                Vector3 a3 = pts[i];
                Vector3 b3 = pts[i + 1];

                Vector2 a = new Vector2(a3.x, a3.z);
                Vector2 b = new Vector2(b3.x, b3.z);
                Vector2 ab = b - a;

                float abLen2 = ab.sqrMagnitude;
                float t = 0f;
                if (abLen2 > 1e-6f)
                    t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / abLen2);

                Vector3 c = Vector3.LerpUnclamped(a3, b3, t);

                // Vertical tolerance rejects stacked overlaps (bridge/switchback)
                if (verticalToleranceMeters > 0f && Mathf.Abs(worldPos.y - c.y) > verticalToleranceMeters)
                    continue;

                // IMPORTANT: keep along-distance consistent with _runCumDist (3D meters)
                float segLen3D = Vector3.Distance(a3, b3);
                float baseDist3D = _runCumDist[i];
                float along = baseDist3D + (t * segLen3D);

                if (along < minAllowedAlong || along > maxAllowedAlong)
                    continue;

                Vector2 c2 = a + ab * t;
                float xzD2 = (p - c2).sqrMagnitude;

                float alongDelta = along - anchorAlong;
                float score = xzD2 + (alongDelta * alongDelta * w);

                if (score < bestScore)
                {
                    bestScore = score;
                    bestXzD2 = xzD2;

                    bestSeg = i;
                    bestT = t;
                    bestClosest = c;
                    bestAlong = along;
                }
            }

            if (bestSeg < 0)
                return false;

            distanceAlongMeters = bestAlong;
            closest = bestClosest;
            distToCenterXZ = Mathf.Sqrt(bestXzD2); // correct distance-to-center
            halfWidthMeters = _run.GetHalfWidthMetersAtSample(bestSeg, bestT);
            segIndex = bestSeg;
            segT = bestT;
            return true;
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

        private void ClearAttemptState(string reason = null)
        {
            if (!string.IsNullOrEmpty(reason))
                DebugLog($"ClearAttemptState: {reason}", force: true);

            _run = null;
            _committed = false;
            _enteredAtDist = 0f;
            _maxDistAlong = 0f;
            _minDistAlong = 0f;
            _directionSign = 0;

            _hasTrackedAlong = false;
            _trackedAlong = 0f;

            _timeEntered = 0f;
            _timeInside = 0f;
            _timeBeyondAbandon = 0f;

            _lostTrackingTime = 0f;

            _runCumDist.Clear();
            _runTotalLenMeters = 0f;
            _lastSegIndex = -1;
            _lastSegT = 0f;
            _trackSegIndex = -1;
            _trackSegT = 0f;

            _hasPrev = false;
            _stackEvents.Clear();
        }

        private void RecordPartialAndClear(string reason)
        {
            DebugLog($"RecordPartialAndClear: {reason}", force: true);

            if (!recordPartialSegments || _run == null)
            {
                ClearAttemptState();
                return;
            }

            float timeSeconds = Mathf.Max(0f, Time.time - _attemptStartTime);
            float coveredMeters = Mathf.Max(0f, _maxDistAlong - _minDistAlong);

            float runLen = (_runTotalLenMeters > 0.001f) ? _runTotalLenMeters : _run.GetTotalLengthMeters();
            float coveredFrac01 = (runLen > 0.001f) ? Mathf.Clamp01(coveredMeters / runLen) : 0f;

            // Avoid spammy micro-attempts
            if (!_committed || timeSeconds < minPartialTimeSeconds || coveredMeters < minPartialCoverageMeters)
            {
                ClearAttemptState();
                ClearAttemptState($"Partial ignored: committed={_committed} time={timeSeconds:F2}s covered={coveredMeters:F1}m ... | {reason}");

                return;
            }

            WriteAttempt(isCompletion: false, completionFractionRemaining: 0f);
            ClearAttemptState();
        }

        private void WriteAttempt(bool isCompletion, float completionFractionRemaining)
        {
            var mgr = PlayerStatsManager.Instance;
            var profile = mgr != null ? mgr.Profile : null;
            if (profile == null || _run == null)
                return;

            float runLen = (_runTotalLenMeters > 0.001f) ? _runTotalLenMeters : _run.GetTotalLengthMeters();
            if (runLen <= 0.001f)
                return; // refuse to log broken 0-length attempts

            float timeSeconds = Mathf.Max(0f, Time.time - _attemptStartTime);
            float avgSpeed = (_activeTime > 0.0001f) ? (_speedIntegral / _activeTime) : 0f;

            // In-game time snapshot (calendar-safe)
            int gy = 0, gmi = 0, gdom = 1, gdow = 0;
            float gtod = 0f;

            var tc = TimeController.instance != null
                ? TimeController.instance
                : UnityEngine.Object.FindFirstObjectByType<TimeController>();

            if (tc != null)
            {
                gy = tc.currentYear;
                gdow = (int)tc.currentDay;
                gtod = tc.timeOfDay;

                // IMPORTANT: derive month/day from dayCount so it matches calendar conversion
                if (!TryConvertDayOfYear(tc, tc.dayCount, out gmi, out gdom))
                {
                    // Fallback: still fill something sane
                    gmi = Mathf.Clamp(tc.currentMonthIndex, 0, (tc.monthPresets != null ? tc.monthPresets.Length - 1 : 11));
                    gdom = Mathf.Max(1, tc.dayOfMonth);
                }
            }

            float exitAlong;
            if (_hasLastInside)
            {
                exitAlong = _lastInsideDistAlong;
            }
            else
            {
                // If travelling reverse, our "furthest progress" is minDistAlong (toward start).
                exitAlong = (_directionSign < 0) ? _minDistAlong : _maxDistAlong;
            }

            float entryFrac01 = Mathf.Clamp01(_enteredAtDist / runLen);
            float exitFrac01 = Mathf.Clamp01(exitAlong / runLen);

            float minFrac01 = Mathf.Clamp01(_minDistAlong / runLen);
            float maxFrac01 = Mathf.Clamp01(_maxDistAlong / runLen);

            float coveredFrac01 = Mathf.Clamp01((_maxDistAlong - _minDistAlong) / runLen);

            DebugLog(
                $"WriteAttempt: completion={isCompletion} time={timeSeconds:F2}s avg={avgSpeed:F2}m/s top={_topSpeed:F2}m/s stacks={_stacks} " +
                $"entry={entryFrac01:P0} exit={exitFrac01:P0} min={minFrac01:P0} max={maxFrac01:P0} covered={coveredFrac01:P0} " +
                $"onRoute={_onRouteDistance:F1}m offRoute={_offRouteDistance:F1}m air={_airTime:F1}s/{_airDistance:F1}m",
                force: true
            );

            var attempt = new RunAttemptEntry
            {
                completedUtc = DateTimeUtc.Now(),
                gameYear = gy,
                gameMonthIndex = gmi,
                gameDayOfMonth = gdom,
                gameDayOfWeek = gdow,
                gameTimeOfDay = gtod,

                timeSeconds = timeSeconds,
                averageSpeedMps = avgSpeed,
                topSpeedMps = _topSpeed,
                airTimeSeconds = _airTime,
                airDistanceMeters = _airDistance,
                onRouteDistanceMeters = _onRouteDistance,
                offRouteDistanceMeters = _offRouteDistance,
                stacks = _stacks,
                stackEvents = new List<StackEventEntry>(_stackEvents),

                // legacy fields (keep filled)
                startDistanceMeters = _enteredAtDist,
                completionFraction = completionFractionRemaining,

                // new v6 fields
                isCompletion = isCompletion,
                runLengthMeters = runLen,
                entryDistanceMeters = _enteredAtDist,
                exitDistanceMeters = exitAlong,
                minDistanceMeters = _minDistAlong,
                maxDistanceMeters = _maxDistAlong,
                entryFraction01 = entryFrac01,
                exitFraction01 = exitFrac01,
                coveredMinFraction01 = minFrac01,
                coveredMaxFraction01 = maxFrac01,
                coveredFraction01 = coveredFrac01
            };

            var record = profile.GetOrCreateRunRecord(_run.RunId);
            record.RegisterAttempt(attempt, maxKeep: Mathf.Max(1, maxAttemptsPerRun));

            // UI wants entry->exit range. If completion, force to the terminal end in the direction travelled.
            float exitFracUi01;
            if (isCompletion)
            {
                // reverse completions terminate at 0, forward at 1
                exitFracUi01 = (_directionSign < 0) ? 0f : 1f;
            }
            else
            {
                exitFracUi01 = attempt.exitFraction01;
            }

            OnAttemptLogged?.Invoke(new AttemptLoggedInfo(
                runId: _run.RunId,
                runName: string.IsNullOrWhiteSpace(_run.RunName) ? _run.RunId : _run.RunName,
                isCompletion: isCompletion,
                entryFraction01: attempt.entryFraction01,
                exitFraction01: exitFracUi01,
                timeSeconds: timeSeconds,
                completedUtc: attempt.completedUtc
            ));

            _lastAttempt = new LastAttemptSummary(
                runId: _run.RunId,
                runName: string.IsNullOrWhiteSpace(_run.RunName) ? _run.RunId : _run.RunName,
                isCompletion: isCompletion,
                entryFraction01: entryFrac01,
                exitFraction01: exitFracUi01,
                elapsedSeconds: timeSeconds,
                topSpeedMps: _topSpeed,
                stacks: _stacks,
                completedUtc: attempt.completedUtc
            );

            _hasLastAttempt = true;

            if (isCompletion)
            {
                profile.lifetime.totalRunsCompleted++;
                profile.session.runsCompleted++;
                profile.TryAddCompletedRunThisSession(_run.RunId);

                if (_stacks == 0)
                {
                    profile.lifetime.totalRunsCompletedClean++;
                    profile.session.runsCompletedClean++;
                }

                if (_topSpeed > profile.lifetime.topRunSpeedMps)
                    profile.lifetime.topRunSpeedMps = _topSpeed;

                if (_topSpeed > profile.session.topRunSpeedMps)
                    profile.session.topRunSpeedMps = _topSpeed;
            }

            // keep the existing completion event for popups/tasks
            if (isCompletion)
            {
                OnRunCompleted?.Invoke(new RunCompletedInfo(
                    _run.RunId,
                    string.IsNullOrWhiteSpace(_run.RunName) ? _run.RunId : _run.RunName,
                    timeSeconds,
                    _topSpeed,
                    _stacks
                ));
            }
        }

        private void CompleteRun(float completionFraction)
        {
            string completedRunId = _run != null ? _run.RunId : null;

            WriteAttempt(isCompletion: true, completionFractionRemaining: completionFraction);

            if (requireExitAfterCompletion && !string.IsNullOrEmpty(completedRunId))
            {
                _blockedRunIdAfterCompletion = completedRunId;
                _waitingForExitAfterCompletion = true;
            }
        }

        // -------------------------
        // Public read-only snapshot for UI
        // -------------------------
        public readonly struct ActiveRunProgress
        {
            public readonly string runId;
            public readonly string runName;

            // Legacy: progress since entry relative to remaining distance (kept for existing UI).
            public readonly float completion01;

            // NEW: absolute fractions along the full run.
            public readonly float entryFraction01;
            public readonly float currentFraction01;

            public readonly float elapsedSeconds;
            public readonly float topSpeedMps;
            public readonly int stacks;
            public readonly bool committed;

            // Back-compat constructor (existing call sites still compile if you accidentally use it somewhere)
            public ActiveRunProgress(
                string runId,
                string runName,
                float completion01,
                float elapsedSeconds,
                float topSpeedMps,
                int stacks,
                bool committed)
            {
                this.runId = runId;
                this.runName = runName;
                this.completion01 = completion01;

                this.entryFraction01 = 0f;
                this.currentFraction01 = 0f;

                this.elapsedSeconds = elapsedSeconds;
                this.topSpeedMps = topSpeedMps;
                this.stacks = stacks;
                this.committed = committed;
            }

            // Preferred constructor (new fields included)
            public ActiveRunProgress(
                string runId,
                string runName,
                float completion01,
                float entryFraction01,
                float currentFraction01,
                float elapsedSeconds,
                float topSpeedMps,
                int stacks,
                bool committed)
            {
                this.runId = runId;
                this.runName = runName;
                this.completion01 = completion01;
                this.entryFraction01 = entryFraction01;
                this.currentFraction01 = currentFraction01;
                this.elapsedSeconds = elapsedSeconds;
                this.topSpeedMps = topSpeedMps;
                this.stacks = stacks;
                this.committed = committed;
            }
        }

        // -------------------------
        // Last attempt snapshot for UI (completion or logged partial)
        // -------------------------
        public readonly struct LastAttemptSummary
        {
            public readonly string runId;
            public readonly string runName;

            // Note: a "completion" may still start mid-run; UI colouring treats completion as 100%.
            public readonly bool isCompletion;

            // UI wants entry -> exit, not min/max.
            public readonly float entryFraction01;
            public readonly float exitFraction01;

            public readonly float elapsedSeconds;
            public readonly float topSpeedMps;
            public readonly int stacks;

            public readonly DateTimeUtc completedUtc;

            public float CoveredFraction01 => isCompletion ? 1f : UnityEngine.Mathf.Abs(exitFraction01 - entryFraction01);

            public LastAttemptSummary(
                string runId,
                string runName,
                bool isCompletion,
                float entryFraction01,
                float exitFraction01,
                float elapsedSeconds,
                float topSpeedMps,
                int stacks,
                DateTimeUtc completedUtc)
            {
                this.runId = runId;
                this.runName = runName;
                this.isCompletion = isCompletion;
                this.entryFraction01 = UnityEngine.Mathf.Clamp01(entryFraction01);
                this.exitFraction01 = UnityEngine.Mathf.Clamp01(exitFraction01);
                this.elapsedSeconds = elapsedSeconds;
                this.topSpeedMps = topSpeedMps;
                this.stacks = stacks;
                this.completedUtc = completedUtc;
            }
        }

        public bool TryGetActiveProgress(out ActiveRunProgress progress)
        {
            if (_run == null)
            {
                progress = default;
                return false;
            }

            float runLen = Mathf.Max(0.001f, _run.GetTotalLengthMeters());

            // Completion since entry relative to remaining distance (direction-aware).
            // IMPORTANT: when direction is unknown (0), choose the best of forward/reverse
            // so the displayed progress cannot "flip" and appear to go backwards later.
            float completionFrac;

            float forwardDenom = Mathf.Max(0.001f, runLen - _enteredAtDist);
            float forwardFrac = Mathf.Clamp01((_maxDistAlong - _enteredAtDist) / forwardDenom);

            float reverseDenom = Mathf.Max(0.001f, _enteredAtDist);
            float reverseFrac = Mathf.Clamp01((_enteredAtDist - _minDistAlong) / reverseDenom);

            if (_directionSign < 0) completionFrac = reverseFrac;
            else if (_directionSign > 0) completionFrac = forwardFrac;
            else completionFrac = Mathf.Max(forwardFrac, reverseFrac);

            // NEW: entry→current absolute fractions along the run
            float entryFrac01 = Mathf.Clamp01(_enteredAtDist / runLen);

            float currentAlong;
            if (_hasLastInside)
            {
                currentAlong = _lastInsideDistAlong;
            }
            else
            {
                // Direction-aware current "furthest" distance
                currentAlong = (_directionSign < 0) ? _minDistAlong : _maxDistAlong;
            }

            float currentFrac01 = Mathf.Clamp01(currentAlong / runLen);

            string id = _run.RunId;
            string name = string.IsNullOrWhiteSpace(_run.RunName) ? id : _run.RunName;

            float elapsed = Mathf.Max(0f, Time.time - _attemptStartTime);

            progress = new ActiveRunProgress(
                id,
                name,
                completionFrac,
                entryFrac01,
                currentFrac01,
                elapsed,
                _topSpeed,
                _stacks,
                _committed
            );

            return true;
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

        [ContextMenu("Reset Run Tracker (Clear Current Attempt State)")]
        public void ResetTrackingState()
        {
            ResetAttemptMetrics();
            ClearAttemptState();
            Debug.Log("[RunProgressTracker] ResetTrackingState() - cleared current attempt state.");
        }

        private void RefreshRunsCacheIfNeeded()
        {
            if (Time.time < _nextRunsCacheRefreshTime && _cachedRuns.Count > 0)
                return;

            _nextRunsCacheRefreshTime = Time.time + Mathf.Max(0.25f, runsCacheRefreshSeconds);

#if UNITY_2023_1_OR_NEWER
            var runs = UnityEngine.Object.FindObjectsByType<SkiRunLine>(FindObjectsSortMode.None);
#else
    var runs = UnityEngine.Object.FindObjectsOfType<SkiRunLine>();
#endif

            _cachedRuns.Clear();
            if (runs == null) return;

            for (int i = 0; i < runs.Length; i++)
                if (runs[i] != null)
                    _cachedRuns.Add(runs[i]);
        }

        public readonly struct RunCompletedInfo
        {
            public readonly string runId;
            public readonly string runName;
            public readonly float timeSeconds;
            public readonly float topSpeedMps;
            public readonly int stacks;

            public RunCompletedInfo(string runId, string runName, float timeSeconds, float topSpeedMps, int stacks)
            {
                this.runId = runId;
                this.runName = runName;
                this.timeSeconds = timeSeconds;
                this.topSpeedMps = topSpeedMps;
                this.stacks = stacks;
            }
        }

        public readonly struct AttemptLoggedInfo
        {
            public readonly string runId;
            public readonly string runName;
            public readonly bool isCompletion;
            public readonly float entryFraction01;
            public readonly float exitFraction01;
            public readonly float timeSeconds;
            public readonly DateTimeUtc completedUtc;

            public AttemptLoggedInfo(
                string runId,
                string runName,
                bool isCompletion,
                float entryFraction01,
                float exitFraction01,
                float timeSeconds,
                DateTimeUtc completedUtc)
            {
                this.runId = runId;
                this.runName = runName;
                this.isCompletion = isCompletion;
                this.entryFraction01 = entryFraction01;
                this.exitFraction01 = exitFraction01;
                this.timeSeconds = timeSeconds;
                this.completedUtc = completedUtc;
            }
        }

        public event Action<AttemptLoggedInfo> OnAttemptLogged;

        public event Action<RunCompletedInfo> OnRunCompleted;

        private static bool TryConvertDayOfYear(TimeController tc, int dayOfYear, out int monthIndex, out int dayOfMonth)
        {
            monthIndex = 0;
            dayOfMonth = 1;

            if (tc == null || tc.monthPresets == null || tc.monthPresets.Length == 0)
                return false;

            int remaining = Mathf.Max(0, dayOfYear);

            for (int i = 0; i < tc.monthPresets.Length; i++)
            {
                int dim = Mathf.Max(1, tc.monthPresets[i].daysInMonth);
                if (remaining < dim)
                {
                    monthIndex = i;
                    dayOfMonth = remaining + 1; // 0-based -> 1-based
                    return true;
                }
                remaining -= dim;
            }

            // Clamp to last month if out of range
            monthIndex = tc.monthPresets.Length - 1;
            int lastDim = Mathf.Max(1, tc.monthPresets[monthIndex].daysInMonth);
            dayOfMonth = Mathf.Clamp(remaining + 1, 1, lastDim);
            return true;
        }

        public static void ClearAllPersistentDataForSlot(int slotId)
        {
            // If your run history is in PlayerPrefs
            // (adjust keys to match your implementation)
            PlayerPrefs.DeleteKey($"RunHistory_{slotId}");
            PlayerPrefs.DeleteKey($"RunCalendar_{slotId}");

            // If you write JSON files, delete them here too.
            // You MUST replace these with your real paths used by RunProgressTracker.
            try
            {
                var dir = Application.persistentDataPath;
                var pathA = System.IO.Path.Combine(dir, $"run_history_slot_{slotId}.json");
                var pathB = System.IO.Path.Combine(dir, $"run_calendar_slot_{slotId}.json");

                if (System.IO.File.Exists(pathA)) System.IO.File.Delete(pathA);
                if (System.IO.File.Exists(pathB)) System.IO.File.Delete(pathB);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[RunProgressTracker] Failed clearing slot {slotId} run data: {e.Message}");
            }
        }

    }
}
