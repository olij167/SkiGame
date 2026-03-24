using System.Collections.Generic;
using UnityEngine;
using SkiGame.Runs;

[DisallowMultipleComponent]
public class NpcSkierRunFollower : MonoBehaviour, ISkiInputSource
{
    public enum SkiMode
    {
        None,
        FreeSki,
        FollowRun
    }

    [Header("References")]
    [SerializeField] private SkiController skiController;
    [SerializeField] private NpcSkierProfile profile;

    [Header("Mode")]
    [SerializeField] private bool inputEnabled = false;
    [SerializeField] private SkiMode skiMode = SkiMode.None;
    [SerializeField] private SkiRunLine currentRun;

    [Header("Scene Context")]
    [SerializeField] private List<SkiRunLine> knownRuns = new();

    [Header("Intent")]
    [SerializeField] private Vector3 broadTargetPoint;
    [SerializeField] private bool hasBroadTarget = false;

    [Header("Free Ski")]
    [SerializeField] private float nearbyRunSearchRadius = 28f;
    [SerializeField] private float broadTargetInfluence = 0.28f;
    [SerializeField] private float downhillBias = 0.85f;
    [SerializeField] private float nearbyRunBias = 0.55f;

    [Header("Run Follow")]
    [SerializeField] private float minLookaheadMeters = 8f;
    [SerializeField] private float maxLookaheadMeters = 24f;
    [SerializeField] private float speedToLookahead = 0.65f;

    [Header("Steering")]
    [SerializeField] private float turnDeadZone = 0.08f;
    [SerializeField] private float turnSensitivity = 2f;
    [SerializeField] private float brakeLeanStrength = 0.95f;
    [SerializeField] private float neutralLeanForwardBias = 0.15f;

    [Header("Skiability")]
    [SerializeField] private float minSkiSlopeDeg = 6f;
    [SerializeField] private float walkRecoverySlopeDeg = 3f;
    [SerializeField] private float minAllowedDownhillDot = 0.02f;
    [SerializeField] private float minCoastSpeedMps = 2.25f;
    [SerializeField] private float uphillWalkDelay = 1.2f;

    [Header("Recovery")]
    [SerializeField] private float severeOffRunRespawnDistance = 45f;
    [SerializeField] private float progressStallSpeedThreshold = 0.9f;
    [SerializeField] private float progressStallTime = 5f;
    [SerializeField] private float noGroundRespawnTime = 4f;

    [Header("Completion")]
    [SerializeField] private float finishDistanceRemainingMeters = 10f;
    [SerializeField] private float finishCenterlineToleranceMeters = 14f;

    [Header("Avoidance")]
    [SerializeField] private LayerMask avoidanceMask = ~0;
    [SerializeField] private float avoidanceProbeForward = 4f;
    [SerializeField] private float avoidanceRadius = 3.5f;
    [SerializeField] private float avoidanceStrength = 1.15f;

    [Header("Poles")]
    [SerializeField] private float polesMaxSlopeDeg = 14f;
    [SerializeField] private float polesMaxSpeedMps = 8f;

    [Header("Personality Polish")]
    [SerializeField] private float steeringNoiseFrequency = 0.22f;
    [SerializeField] private float steeringNoiseStrength = 0.22f;
    [SerializeField] private float scenicPauseSlopeMaxDeg = 14f;
    [SerializeField] private float scenicPauseSpeedMax = 6f;
    [SerializeField] private float steepHesitationSlopeMinDeg = 20f;

    private float _distanceAlongMeters;
    private float _distanceToCenterXZ;
    private float _halfWidthMeters;
    private float _completion01;
    private float _distanceRemainingMeters;

    private float _lastProgressMeters;
    private float _lastMeaningfulProgressTime;
    private float _lastGroundedTime;
    private float _uphillIntentStartTime = -999f;

    private bool _wantsWalkingRecovery;
    private bool _wantsRespawn;
    private Vector3 _suggestedRecoveryPoint;
    private Vector3 _suggestedRespawnPoint;
    private Quaternion _suggestedRespawnRotation;

    private float _noiseSeed;
    private float _pauseUntil;

    public SkiRunLine CurrentRun => currentRun;
    public SkiMode CurrentMode => skiMode;
    public bool InputEnabled => inputEnabled;
    public bool WantsWalkingRecovery => _wantsWalkingRecovery;
    public bool WantsRespawn => _wantsRespawn;
    public Vector3 SuggestedRecoveryPoint => _suggestedRecoveryPoint;
    public Vector3 SuggestedRespawnPoint => _suggestedRespawnPoint;
    public Quaternion SuggestedRespawnRotation => _suggestedRespawnRotation;

    private float _visibleFastForwardMultiplier = 1f;
    public bool HasCompletedRun =>
        inputEnabled &&
        currentRun != null &&
        skiMode == SkiMode.FollowRun &&
        _completion01 >= 0.98f &&
        _distanceRemainingMeters <= finishDistanceRemainingMeters &&
        _distanceToCenterXZ <= finishCenterlineToleranceMeters;

    private void Awake()
    {
        _noiseSeed = Random.Range(0f, 1000f);
    }

    public void ConfigureRuns(IEnumerable<SkiRunLine> runs)
    {
        knownRuns.Clear();
        if (runs == null) return;

        foreach (var run in runs)
        {
            if (run != null)
                knownRuns.Add(run);
        }
    }

    public void SetVisibleFastForwardMultiplier(float multiplier)
    {
        _visibleFastForwardMultiplier = Mathf.Max(1f, multiplier);
    }

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;

        if (!enabled)
        {
            skiMode = SkiMode.None;
            _wantsWalkingRecovery = false;
            _wantsRespawn = false;
            hasBroadTarget = false;
            _pauseUntil = 0f;
        }
    }

    public void ClearRecoveryRequests()
    {
        _wantsWalkingRecovery = false;
        _wantsRespawn = false;
    }

    public void SetRun(SkiRunLine run)
    {
        currentRun = run;
        skiMode = run != null ? SkiMode.FollowRun : SkiMode.None;
        inputEnabled = run != null;
        hasBroadTarget = false;
        ResetProgressState();
        BuildRespawnPoseFromRun();
    }

    public void BeginFreeSki(Vector3 broadTarget)
    {
        currentRun = null;
        skiMode = SkiMode.FreeSki;
        inputEnabled = true;
        broadTargetPoint = broadTarget;
        hasBroadTarget = true;
        ResetProgressState();
        _suggestedRespawnPoint = transform.position + Vector3.up * 0.35f;
        _suggestedRespawnRotation = transform.rotation;
    }

    public void UpdateBroadTarget(Vector3 point)
    {
        broadTargetPoint = point;
        hasBroadTarget = true;
    }

    public bool HasInput()
    {
        return inputEnabled && skiMode != SkiMode.None && skiController != null && skiController.enabled;
    }

    public SkiInputFrame GetSkiInput()
    {
        if (!HasInput())
            return SkiInputFrame.Neutral;

        if (skiController.IsRiderGrounded)
            _lastGroundedTime = Time.time;

        Vector3 groundNormal = skiController.GroundNormal.sqrMagnitude > 0.0001f
            ? skiController.GroundNormal.normalized
            : Vector3.up;

        Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, groundNormal);
        if (downhill.sqrMagnitude <= 0.0001f)
            downhill = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        downhill.Normalize();

        float slopeDeg = Vector3.Angle(groundNormal, Vector3.up);
        Vector3 planarVelocity = Vector3.ProjectOnPlane(skiController.Velocity, groundNormal);
        float speed = planarVelocity.magnitude;

        if (Time.time < _pauseUntil)
        {
            return new SkiInputFrame
            {
                leftLeg01 = 0.15f,
                rightLeg01 = 0.15f,
                lean01 = -0.15f,
                polesHeld = false
            };
        }

        MaybeTriggerAmbientPause(slopeDeg, speed);

        Vector3 desiredDirection;
        bool hasLineIntent;

        if (skiMode == SkiMode.FollowRun && currentRun != null)
            hasLineIntent = TryBuildRunFollowDirection(groundNormal, downhill, speed, out desiredDirection);
        else
            hasLineIntent = TryBuildFreeSkiDirection(groundNormal, downhill, out desiredDirection);

        if (!hasLineIntent)
        {
            _wantsRespawn = true;
            BuildRespawnPoseFromRun();
            return SkiInputFrame.Neutral;
        }

        desiredDirection = ApplyPersonalityVariation(desiredDirection, downhill, groundNormal);
        desiredDirection = ApplyLocalAvoidance(desiredDirection, groundNormal);

        UpdateRecoveryState(slopeDeg, speed, downhill, desiredDirection);

        if (_wantsRespawn || _wantsWalkingRecovery)
            return SkiInputFrame.Neutral;

        return BuildInputFromDesiredDirection(desiredDirection, downhill, groundNormal, slopeDeg, speed);
    }

    private void ResetProgressState()
    {
        _distanceAlongMeters = 0f;
        _distanceToCenterXZ = 0f;
        _halfWidthMeters = 0f;
        _completion01 = 0f;
        _distanceRemainingMeters = 0f;

        _lastProgressMeters = 0f;
        _lastMeaningfulProgressTime = Time.time;
        _lastGroundedTime = Time.time;
        _uphillIntentStartTime = -999f;

        _wantsWalkingRecovery = false;
        _wantsRespawn = false;
        _suggestedRecoveryPoint = transform.position;
    }

    private void MaybeTriggerAmbientPause(float slopeDeg, float speed)
    {
        if (profile == null)
            return;

        if (slopeDeg > scenicPauseSlopeMaxDeg || speed > scenicPauseSpeedMax)
            return;

        if (Random.value <= profile.AmbientPauseChance * Time.deltaTime)
        {
            _pauseUntil = Time.time + (Random.Range(profile.AmbientPauseDurationMin, profile.AmbientPauseDurationMax) / Mathf.Max(1f, _visibleFastForwardMultiplier));
        }
    }

    private Vector3 ApplyPersonalityVariation(Vector3 desiredDirection, Vector3 downhill, Vector3 groundNormal)
    {
        if (profile == null)
            return desiredDirection;

        Vector3 right = Vector3.Cross(groundNormal, downhill).normalized;

        float noise = Mathf.PerlinNoise(_noiseSeed, Time.time * steeringNoiseFrequency) * 2f - 1f;
        float lateral = noise * steeringNoiseStrength * profile.LineVariation01;

        Vector3 varied = desiredDirection + right * lateral;

        if (profile.HesitationOnSteeps01 > 0f)
        {
            float slopeDeg = Vector3.Angle(groundNormal, Vector3.up);
            if (slopeDeg >= steepHesitationSlopeMinDeg)
            {
                float hesitation = Mathf.Lerp(0f, 0.25f, profile.HesitationOnSteeps01);
                varied = Vector3.Slerp(varied, downhill, hesitation);
            }
        }

        varied = Vector3.ProjectOnPlane(varied, groundNormal);
        if (varied.sqrMagnitude <= 0.0001f)
            varied = downhill;

        varied.Normalize();
        return varied;
    }

    private bool TryBuildRunFollowDirection(Vector3 groundNormal, Vector3 downhill, float speed, out Vector3 desiredDirection)
    {
        desiredDirection = downhill;

        if (currentRun == null)
            return false;

        if (!currentRun.TryGetClosestPointOnCenterlineXZ(
                transform.position,
                out _distanceAlongMeters,
                out _distanceToCenterXZ,
                out _halfWidthMeters,
                out _))
        {
            return false;
        }

        float runLength = Mathf.Max(1f, currentRun.LengthMeters > 0.001f
            ? currentRun.LengthMeters
            : currentRun.GetTotalLengthMeters());

        _completion01 = Mathf.Clamp01(_distanceAlongMeters / runLength);
        _distanceRemainingMeters = Mathf.Max(0f, runLength - _distanceAlongMeters);

        float profileLookahead = profile != null ? profile.ReactionLookaheadMeters : 14f;
        float dynamicLookahead = Mathf.Lerp(
            minLookaheadMeters,
            maxLookaheadMeters,
            Mathf.Clamp01((profileLookahead - minLookaheadMeters) / Mathf.Max(0.001f, maxLookaheadMeters - minLookaheadMeters)));
        dynamicLookahead += speed * speedToLookahead;

        float lookaheadDistance = Mathf.Clamp(_distanceAlongMeters + dynamicLookahead, 0f, runLength);
        Vector3 lookaheadPoint = SamplePointAtDistance(currentRun.PointsWorld, lookaheadDistance);
        Vector3 runTangent = SampleTangentAtDistance(currentRun.PointsWorld, lookaheadDistance, groundNormal);

        Vector3 toLookahead = Vector3.ProjectOnPlane(lookaheadPoint - transform.position, groundNormal);
        if (toLookahead.sqrMagnitude > 0.0001f)
            toLookahead.Normalize();
        else
            toLookahead = skiController.SkiForwardOnPlane.normalized;

        if (Vector3.Dot(runTangent, toLookahead) < 0f)
            runTangent = -runTangent;

        desiredDirection = BlendNaturalSkiDirection(downhill, runTangent, toLookahead, hasBroadTarget ? broadTargetPoint : lookaheadPoint, groundNormal);
        _suggestedRecoveryPoint = lookaheadPoint;

        if (_distanceAlongMeters > _lastProgressMeters + 1f)
        {
            _lastProgressMeters = _distanceAlongMeters;
            _lastMeaningfulProgressTime = Time.time;
        }

        if (_distanceToCenterXZ >= severeOffRunRespawnDistance)
            _wantsRespawn = true;

        BuildRespawnPoseFromRun();
        return true;
    }

    private bool TryBuildFreeSkiDirection(Vector3 groundNormal, Vector3 downhill, out Vector3 desiredDirection)
    {
        desiredDirection = downhill;

        SkiRunLine nearbyRun = FindNearbyRun(transform.position, nearbyRunSearchRadius, out _, out float nearestAlong, out Vector3 nearestPoint);
        Vector3 nearbyRunTangent = downhill;

        if (nearbyRun != null)
        {
            nearbyRunTangent = SampleTangentAtDistance(
                nearbyRun.PointsWorld,
                nearestAlong + 4f,
                groundNormal);

            if (Vector3.Dot(nearbyRunTangent, downhill) < 0f)
                nearbyRunTangent = -nearbyRunTangent;

            _suggestedRecoveryPoint = nearestPoint;
        }
        else
        {
            _suggestedRecoveryPoint = transform.position + downhill * 8f;
        }

        Vector3 targetDir = downhill;
        if (hasBroadTarget)
        {
            Vector3 toTarget = Vector3.ProjectOnPlane(broadTargetPoint - transform.position, groundNormal);
            if (toTarget.sqrMagnitude > 0.0001f)
                targetDir = toTarget.normalized;
        }

        desiredDirection = downhill * downhillBias;

        if (nearbyRun != null)
            desiredDirection += nearbyRunTangent * nearbyRunBias;

        if (hasBroadTarget)
        {
            float downhillDot = Vector3.Dot(targetDir, downhill);
            if (downhillDot > -0.15f)
                desiredDirection += targetDir * broadTargetInfluence;
        }

        desiredDirection = Vector3.ProjectOnPlane(desiredDirection, groundNormal);
        if (desiredDirection.sqrMagnitude <= 0.0001f)
            desiredDirection = downhill;

        desiredDirection.Normalize();
        return true;
    }

    private Vector3 BlendNaturalSkiDirection(
        Vector3 downhill,
        Vector3 runTangent,
        Vector3 lineDirection,
        Vector3 targetPoint,
        Vector3 groundNormal)
    {
        Vector3 result = downhill * downhillBias;
        result += runTangent * 0.65f;
        result += lineDirection * 0.45f;

        if (hasBroadTarget)
        {
            Vector3 toTarget = Vector3.ProjectOnPlane(targetPoint - transform.position, groundNormal);
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                toTarget.Normalize();
                if (Vector3.Dot(toTarget, downhill) > -0.15f)
                    result += toTarget * broadTargetInfluence;
            }
        }

        result = Vector3.ProjectOnPlane(result, groundNormal);
        if (result.sqrMagnitude <= 0.0001f)
            result = downhill;

        result.Normalize();

        if (Vector3.Dot(result, downhill) < minAllowedDownhillDot)
        {
            result = Vector3.Slerp(result, downhill, 0.65f);
            result.Normalize();
        }

        return result;
    }

    private void UpdateRecoveryState(float slopeDeg, float speed, Vector3 downhill, Vector3 desiredDirection)
    {
        _wantsWalkingRecovery = false;
        _wantsRespawn = false;

        if (!skiController.IsRiderGrounded && (Time.time - _lastGroundedTime) >= noGroundRespawnTime)
        {
            _wantsRespawn = true;
            return;
        }

        bool skiableSlope = slopeDeg >= minSkiSlopeDeg || speed >= minCoastSpeedMps;
        bool mustWalkIfMoving = slopeDeg < walkRecoverySlopeDeg;
        bool uphillIntent = Vector3.Dot(desiredDirection, downhill) < minAllowedDownhillDot;

        if (uphillIntent)
        {
            if (_uphillIntentStartTime < -100f)
                _uphillIntentStartTime = Time.time;
        }
        else
        {
            _uphillIntentStartTime = -999f;
        }

        bool sustainedUphillIntent = (Time.time - _uphillIntentStartTime) >= uphillWalkDelay;

        bool stalled = speed <= progressStallSpeedThreshold &&
                       (Time.time - _lastMeaningfulProgressTime) >= progressStallTime &&
                       skiController.IsRiderGrounded;

        if (!skiableSlope && mustWalkIfMoving && hasBroadTarget)
        {
            _wantsWalkingRecovery = true;
            return;
        }

        if (sustainedUphillIntent && !skiableSlope)
        {
            _wantsWalkingRecovery = true;
            return;
        }

        if (stalled && !skiableSlope)
        {
            _wantsWalkingRecovery = true;
            return;
        }

        if (stalled && currentRun == null && !hasBroadTarget)
        {
            _wantsRespawn = true;
        }
    }

    private SkiInputFrame BuildInputFromDesiredDirection(Vector3 desiredDirection, Vector3 downhill, Vector3 groundNormal, float slopeDeg, float speed)
    {
        Vector3 localDesired = transform.InverseTransformDirection(desiredDirection);
        float turnSignal = Mathf.Clamp(localDesired.x * turnSensitivity, -1f, 1f);
        float forwardSignal = Mathf.Clamp(localDesired.z, -1f, 1f);

        float desiredSpeed = (profile != null ? profile.CruiseSpeedMps : 10f) * Mathf.Max(1f, _visibleFastForwardMultiplier);
        float caution = profile != null ? profile.Caution01 : 0.5f;
        float confidence = profile != null ? profile.Confidence01 : 0.5f;
        float assertiveness = profile != null ? profile.Assertiveness01 : 0.5f;

        float downhillAssist = Mathf.Lerp(0.8f, 1.15f, Mathf.Clamp01(Vector3.Dot(desiredDirection, downhill) * 0.5f + 0.5f));
        desiredSpeed *= downhillAssist;
        desiredSpeed *= Mathf.Lerp(0.9f, 1.15f, assertiveness);

        float speedError = desiredSpeed - speed;
        float lean = Mathf.Clamp((speedError / 8f) + (neutralLeanForwardBias * forwardSignal), -1f, 1f);

        float brake01 = 0f;
        if (speed > desiredSpeed)
        {
            float overspeedWindow = Mathf.Lerp(3f, 8f, 1f - caution);
            brake01 = Mathf.InverseLerp(desiredSpeed, desiredSpeed + overspeedWindow, speed);
            lean -= brake01 * brakeLeanStrength;
        }

        float leftLeg = 0f;
        float rightLeg = 0f;

        if (brake01 > 0.2f)
        {
            float wedge = Mathf.Clamp01(brake01 + (Mathf.Abs(turnSignal) * 0.25f));
            leftLeg = wedge;
            rightLeg = wedge;
        }
        else if (turnSignal > turnDeadZone)
        {
            rightLeg = Mathf.Clamp01(Mathf.Abs(turnSignal) * Mathf.Lerp(0.75f, 1.15f, assertiveness) * (profile != null ? profile.TurnAggression : 0.75f));
        }
        else if (turnSignal < -turnDeadZone)
        {
            leftLeg = Mathf.Clamp01(Mathf.Abs(turnSignal) * Mathf.Lerp(0.75f, 1.15f, assertiveness) * (profile != null ? profile.TurnAggression : 0.75f));
        }

        bool usePoles =
            slopeDeg <= polesMaxSlopeDeg &&
            speed <= polesMaxSpeedMps &&
            lean > 0.15f &&
            (profile != null ? profile.PoleUsage01 : 0.5f) > 0.25f &&
            confidence > 0.15f;

        return new SkiInputFrame
        {
            leftLeg01 = Mathf.Clamp01(leftLeg),
            rightLeg01 = Mathf.Clamp01(rightLeg),
            lean01 = Mathf.Clamp(lean, -1f, 1f),
            polesHeld = usePoles,
            jumpHeld = false,
            jumpPressedThisFrame = false,
            jumpReleasedThisFrame = false
        };
    }

    private SkiRunLine FindNearbyRun(Vector3 worldPos, float radius, out float bestDist, out float bestAlong, out Vector3 closestPoint)
    {
        bestDist = float.PositiveInfinity;
        bestAlong = 0f;
        closestPoint = worldPos;

        if (knownRuns == null || knownRuns.Count == 0)
            return null;

        SkiRunLine bestRun = null;

        for (int i = 0; i < knownRuns.Count; i++)
        {
            SkiRunLine run = knownRuns[i];
            if (run == null) continue;

            if (!run.TryGetClosestPointOnCenterlineXZ(worldPos, out float along, out float distXZ, out _, out Vector3 cp))
                continue;

            if (distXZ > radius)
                continue;

            if (distXZ < bestDist)
            {
                bestDist = distXZ;
                bestAlong = along;
                closestPoint = cp;
                bestRun = run;
            }
        }

        return bestRun;
    }

    private void BuildRespawnPoseFromRun()
    {
        if (currentRun == null || currentRun.PointsWorld == null || currentRun.PointsWorld.Count < 2)
        {
            _suggestedRespawnPoint = transform.position + Vector3.up * 0.35f;
            _suggestedRespawnRotation = transform.rotation;
            return;
        }

        float runLength = Mathf.Max(1f, currentRun.LengthMeters > 0.001f
            ? currentRun.LengthMeters
            : currentRun.GetTotalLengthMeters());

        float respawnDistance = Mathf.Clamp(_distanceAlongMeters + 6f, 0f, runLength);
        Vector3 p = SamplePointAtDistance(currentRun.PointsWorld, respawnDistance);
        Vector3 t = SampleTangentAtDistance(currentRun.PointsWorld, respawnDistance, Vector3.up);

        _suggestedRespawnPoint = p + Vector3.up * 0.35f;
        _suggestedRespawnRotation = Quaternion.LookRotation(t.sqrMagnitude > 0.0001f ? t : transform.forward, Vector3.up);
    }

    private Vector3 ApplyLocalAvoidance(Vector3 desiredDirection, Vector3 groundNormal)
    {
        Vector3 origin = transform.position + (skiController.SkiForwardOnPlane * avoidanceProbeForward);
        Collider[] hits = Physics.OverlapSphere(origin, avoidanceRadius, avoidanceMask, QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
            return desiredDirection;

        Rigidbody ownRb = GetComponent<Rigidbody>();
        Vector3 avoidance = Vector3.zero;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null) continue;
            if (hit.transform.IsChildOf(transform)) continue;

            Rigidbody hitRb = hit.attachedRigidbody;
            if (hitRb != null && ownRb != null && hitRb == ownRb)
                continue;

            Vector3 toOther = hit.ClosestPoint(transform.position) - transform.position;
            Vector3 planar = Vector3.ProjectOnPlane(toOther, groundNormal);
            float dist = planar.magnitude;
            if (dist < 0.001f || dist > avoidanceRadius)
                continue;

            Vector3 away = -planar.normalized;
            float weight = 1f - Mathf.Clamp01(dist / avoidanceRadius);
            avoidance += away * weight;
        }

        if (avoidance.sqrMagnitude <= 0.0001f)
            return desiredDirection;

        Vector3 blended = desiredDirection + (avoidance.normalized * avoidanceStrength);
        blended = Vector3.ProjectOnPlane(blended, groundNormal);
        if (blended.sqrMagnitude <= 0.0001f)
            return desiredDirection;

        return blended.normalized;
    }

    private static Vector3 SamplePointAtDistance(IReadOnlyList<Vector3> points, float distanceMeters)
    {
        if (points == null || points.Count == 0)
            return Vector3.zero;

        if (points.Count == 1)
            return points[0];

        float remaining = Mathf.Max(0f, distanceMeters);

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector3 a = points[i];
            Vector3 b = points[i + 1];
            float segLen = Vector3.Distance(a, b);

            if (segLen <= 0.0001f)
                continue;

            if (remaining <= segLen)
                return Vector3.LerpUnclamped(a, b, remaining / segLen);

            remaining -= segLen;
        }

        return points[points.Count - 1];
    }

    private static Vector3 SampleTangentAtDistance(IReadOnlyList<Vector3> points, float distanceMeters, Vector3 planeNormal)
    {
        const float delta = 2f;
        Vector3 a = SamplePointAtDistance(points, Mathf.Max(0f, distanceMeters - delta));
        Vector3 b = SamplePointAtDistance(points, distanceMeters + delta);
        Vector3 tangent = Vector3.ProjectOnPlane(b - a, planeNormal);
        if (tangent.sqrMagnitude < 0.0001f)
            tangent = b - a;

        return tangent.sqrMagnitude > 0.0001f ? tangent.normalized : Vector3.forward;
    }
}