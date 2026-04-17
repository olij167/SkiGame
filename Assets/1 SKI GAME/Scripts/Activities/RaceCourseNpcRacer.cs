using UnityEngine;
using SkiGame.Activities;

[DisallowMultipleComponent]
[RequireComponent(typeof(SkiController))]
public sealed class RaceCourseNpcRacer : MonoBehaviour, ISkiInputSource
{
    private enum CrowdMode
    {
        None = 0,
        Idle = 1,
        Dispersing = 2
    }

    private enum RacerArchetype
    {
        Technician,
        Aggressor,
        Sprinter,
        SteadyFinisher,
        WildCard,
        Defender
    }

    private enum RaceSegmentType
    {
        Glide,
        MediumTurn,
        TechnicalTurn,
        Steep,
        Finish
    }

    private enum TacticalMode
    {
        HoldLine,
        SetupPass,
        PassInside,
        PassOutside,
        DefendLane,
        Stabilize
    }

    private enum MistakeType
    {
        None,
        EarlyBrake,
        LateTurnIn,
        WashedWide,
        NervousStraight,
        UnstableCorrection
    }

    [Header("References")]
    [SerializeField] private SkiController skiController;
    [SerializeField] private WalkingController walkingController;
    [SerializeField] private Rigidbody body;

    [Header("Tuning")]
    [SerializeField] private float baseCruiseSpeed = 12f;
    [SerializeField] private float minLookaheadMeters = 8f;
    [SerializeField] private float maxLookaheadMeters = 24f;
    [SerializeField] private float speedToLookahead = 0.65f;
    [SerializeField] private float turnSensitivity = 2f;
    [SerializeField] private float turnDeadZone = 0.08f;
    [SerializeField] private float neutralLeanForwardBias = 0.18f;
    [SerializeField] private float brakeLeanStrength = 0.95f;
    [SerializeField] private float finishDistanceRemainingMeters = 8f;
    [SerializeField] private float finishCenterlineToleranceMeters = 14f;

    [Header("Racecraft")]
    [SerializeField] private float tacticalStateMinSeconds = 1.4f;
    [SerializeField] private float tacticalStateMaxSeconds = 3.4f;
    [SerializeField] private float overtakeTriggerGapMeters = 10f;
    [SerializeField] private float defendTriggerGapMeters = 7f;

    [Header("Crowd")]
    [SerializeField] private float crowdHopHeight = 0.28f;
    [SerializeField] private float crowdHopFrequency = 1.75f;
    [SerializeField] private float crowdSwayDegrees = 10f;
    [SerializeField] private float crowdDisperseSpeed = 1.65f;

    private RaceCourseLine _course;
    private int _leagueNumber;
    private bool _armed;
    private bool _finished;
    private float _elapsed;
    private float _distanceAlong;
    private float _distanceToCenter;
    private float _finishTime;
    private Vector3 _startHoldPosition;
    private Quaternion _startHoldRotation = Quaternion.identity;
    private bool _spectatorMode;
    private CrowdMode _crowdMode;
    private Vector3 _crowdAnchorPosition;
    private Quaternion _crowdAnchorRotation;
    private Vector3 _crowdDisperseDirection;
    private float _crowdPhase;
    private float _crowdModeUntil;
    private float _laneBiasNormalized;
    private float _mistakeUntil;
    private float _nextMistakeDecisionAt;
    private float _mistakeLaneBias;
    private float _aggressionBias;
    private RacerArchetype _archetype;
    private TacticalMode _tacticalMode;
    private float _tacticalModeUntil;
    private MistakeType _mistakeType;
    private RaceSegmentType _segmentType;
    private float _segmentStrength;
    private float _reactionDelayUntil;
    private float _launchBoostUntil;
    private float _launchStrength;
    private float _stabilityUntil;

    public bool IsFinished => _finished;
    public float FinishTimeSeconds => _finishTime;
    public float DistanceAlong => _distanceAlong;

    private void Reset()
    {
        if (skiController == null)
            skiController = GetComponent<SkiController>();
        if (walkingController == null)
            walkingController = GetComponent<WalkingController>();
        if (body == null)
            body = GetComponent<Rigidbody>();
    }

    private void Awake()
    {
        if (skiController == null)
            skiController = GetComponent<SkiController>();
        if (walkingController == null)
            walkingController = GetComponent<WalkingController>();
        if (body == null)
            body = GetComponent<Rigidbody>();
    }

    public void BeginRace(RaceCourseLine course, int leagueNumber)
    {
        _spectatorMode = false;
        _course = course;
        _leagueNumber = Mathf.Max(1, leagueNumber);
        _armed = true;
        _finished = false;
        _elapsed = 0f;
        _distanceAlong = 0f;
        _distanceToCenter = 0f;
        _finishTime = 0f;
        _crowdMode = CrowdMode.None;
        _laneBiasNormalized = Random.Range(-0.22f, 0.22f);
        _mistakeUntil = -1f;
        _nextMistakeDecisionAt = Time.time + Random.Range(1.5f, 3.25f);
        _mistakeLaneBias = 0f;
        _aggressionBias = Random.Range(0.88f, 1.12f);
        _archetype = PickArchetype();
        _tacticalMode = TacticalMode.HoldLine;
        _tacticalModeUntil = Time.time + Random.Range(tacticalStateMinSeconds, tacticalStateMaxSeconds);
        _mistakeType = MistakeType.None;
        _segmentType = RaceSegmentType.Glide;
        _segmentStrength = 0f;
        _stabilityUntil = -1f;

        float launchSkill = GetArchetypeLaunchBias();
        _reactionDelayUntil = Time.time + Random.Range(0.05f, 0.45f) * Mathf.Lerp(1.15f, 0.6f, launchSkill);
        _launchStrength = Mathf.Lerp(0.9f, 1.25f, launchSkill) * Random.Range(0.94f, 1.08f);
        _launchBoostUntil = _reactionDelayUntil + Mathf.Lerp(2.5f, 4.2f, launchSkill);
        _startHoldPosition = transform.position;
        _startHoldRotation = transform.rotation;

        if (skiController != null)
            skiController.SetExternalInputSource(this);
    }

    public void StopRace()
    {
        _armed = false;

        if (skiController != null)
            skiController.ClearExternalInputSource();
    }

    public void ConfigureAsSpectator()
    {
        ConfigureAsCrowd(transform.position, transform.rotation, -transform.right);
    }

    public void ConfigureAsCrowd(Vector3 anchorPosition, Quaternion anchorRotation, Vector3 disperseDirection)
    {
        _spectatorMode = true;
        _course = null;
        _armed = false;
        _finished = true;
        _elapsed = 0f;
        _distanceAlong = 0f;
        _distanceToCenter = 0f;
        _finishTime = 0f;
        _crowdMode = CrowdMode.Idle;
        _crowdAnchorPosition = anchorPosition;
        _crowdAnchorRotation = anchorRotation;
        _crowdDisperseDirection = Vector3.ProjectOnPlane(disperseDirection, Vector3.up);
        if (_crowdDisperseDirection.sqrMagnitude < 0.001f)
            _crowdDisperseDirection = anchorRotation * Vector3.right;
        _crowdDisperseDirection.Normalize();
        _crowdPhase = Random.Range(0f, Mathf.PI * 2f);
        _crowdModeUntil = -1f;

        transform.SetPositionAndRotation(_crowdAnchorPosition, _crowdAnchorRotation);

        if (walkingController != null)
            walkingController.ForceEnterWalkMode();

        if (skiController != null)
        {
            skiController.ClearExternalInputSource();
            skiController.enabled = false;
        }

        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }

        Collider[] colliders = GetComponentsInChildren<Collider>(includeInactive: true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }
    }

    public void BeginCrowdDispersal(float lifetimeSeconds)
    {
        if (!_spectatorMode)
            return;

        _crowdMode = CrowdMode.Dispersing;
        _crowdModeUntil = Time.time + Mathf.Max(0.5f, lifetimeSeconds);
    }

    private void Update()
    {
        if (_spectatorMode)
        {
            UpdateCrowdMotion();
            return;
        }

        if (!_armed || _finished || _course == null)
            return;

        if (_course.IsCountdownActive)
        {
            HoldAtStartPose();
            return;
        }

        _elapsed += Time.deltaTime;

        if (_course.TryProjectPointOntoCourse(transform.position, out _distanceAlong, out _distanceToCenter, out _))
        {
            UpdateSegmentState();
            UpdateTacticalState();
            UpdateMistakeState();

            float remaining = Mathf.Max(0f, _course.TotalLengthMeters - _distanceAlong);
            if (remaining <= finishDistanceRemainingMeters && _distanceToCenter <= finishCenterlineToleranceMeters)
            {
                _finished = true;
                _finishTime = _elapsed;
                StopRace();

                if (RaceActivityService.Instance != null)
                    RaceActivityService.Instance.NotifyNpcFinished(this);
            }
        }
    }

    private void HoldAtStartPose()
    {
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        transform.SetPositionAndRotation(_startHoldPosition, _startHoldRotation);
    }

    private void UpdateCrowdMotion()
    {
        if (_crowdMode == CrowdMode.None)
            return;

        float time = Time.time + _crowdPhase;
        float hopFrequency = Mathf.Max(2.1f, crowdHopFrequency);
        float hop = Mathf.Max(0f, Mathf.Sin(time * hopFrequency)) * Mathf.Max(0.42f, crowdHopHeight);
        float sway = Mathf.Sin(time * (hopFrequency * 0.65f)) * Mathf.Max(14f, crowdSwayDegrees);

        Vector3 basePos = _crowdAnchorPosition;
        Quaternion baseRot = _crowdAnchorRotation * Quaternion.Euler(0f, sway, 0f);

        if (_crowdMode == CrowdMode.Dispersing)
        {
            _crowdAnchorPosition += _crowdDisperseDirection * (Mathf.Max(3.2f, crowdDisperseSpeed) * Time.deltaTime);
            basePos = _crowdAnchorPosition;
            Vector3 forward = _crowdDisperseDirection.sqrMagnitude > 0.001f ? _crowdDisperseDirection : transform.forward;
            baseRot = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(0f, sway * 0.5f, 0f);
        }

        transform.SetPositionAndRotation(basePos + Vector3.up * hop, baseRot);
    }

    public bool HasInput()
    {
        return _armed && !_finished && _course != null && _course.IsRaceInProgress;
    }

    public SkiInputFrame GetSkiInput()
    {
        if (!HasInput())
            return SkiInputFrame.Neutral;

        if (Time.time < _reactionDelayUntil)
            return SkiInputFrame.Neutral;

        Vector3 groundNormal = skiController != null && skiController.GroundNormal.sqrMagnitude > 0.0001f
            ? skiController.GroundNormal.normalized
            : Vector3.up;

        Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, groundNormal);
        if (downhill.sqrMagnitude <= 0.0001f)
            downhill = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        downhill.Normalize();

        Vector3 planarVelocity = skiController != null
            ? Vector3.ProjectOnPlane(skiController.Velocity, groundNormal)
            : Vector3.zero;
        float speed = planarVelocity.magnitude;

        float speedMultiplier = 1f;
        float linePrecision = 0.5f;
        float mistakeChance = 0.2f;
        float recoveryQuality = 0.5f;
        float overtakeConfidence = 0.5f;

        RaceCourseLine.RaceLeagueDefinition league = _course.GetLeague(_leagueNumber);
        if (league != null)
        {
            speedMultiplier = Mathf.Max(0.1f, league.npcSpeedMultiplier);
            linePrecision = Mathf.Clamp01(league.npcLinePrecision);
            mistakeChance = Mathf.Clamp01(league.npcMistakeChance);
            recoveryQuality = Mathf.Clamp01(league.npcRecoveryQuality);
            overtakeConfidence = Mathf.Clamp01(league.npcOvertakeConfidence);
        }

        ApplyArchetype(ref speedMultiplier, ref linePrecision, ref mistakeChance, ref recoveryQuality, ref overtakeConfidence);
        ApplySegmentModifiers(ref speedMultiplier, ref linePrecision, ref mistakeChance, ref recoveryQuality, ref overtakeConfidence);
        ApplyPressureModifiers(ref speedMultiplier, ref overtakeConfidence, ref mistakeChance);

        float tacticalSpeedMultiplier = 1f;
        float tacticalLaneBias = 0f;
        float defensiveTurnBias = 1f;
        ApplyTacticalState(ref tacticalSpeedMultiplier, ref tacticalLaneBias, ref defensiveTurnBias, overtakeConfidence);

        float recoveryStrength = Mathf.Lerp(0.7f, 1.8f, recoveryQuality);
        float aggression = Mathf.Lerp(0.85f, 1.35f, overtakeConfidence) * _aggressionBias;
        float lineWidth = Mathf.Lerp(0.10f, 0.28f, overtakeConfidence);
        float lineOffsetMeters = (_laneBiasNormalized + tacticalLaneBias) * _course.CourseWidthMeters * lineWidth;

        if (Time.time < _mistakeUntil)
            ApplyMistakeProfile(ref lineOffsetMeters, ref tacticalSpeedMultiplier, ref aggression, ref recoveryStrength);

        float lookahead = Mathf.Clamp(
            minLookaheadMeters + speed * speedToLookahead * Mathf.Lerp(0.85f, 1.35f, linePrecision),
            minLookaheadMeters,
            maxLookaheadMeters + Mathf.Lerp(0f, 10f, overtakeConfidence));

        float sampleDistance = Mathf.Clamp(_distanceAlong + lookahead, 0f, _course.TotalLengthMeters);
        Vector3 lookaheadPoint = _course.SamplePointAtDistance(sampleDistance);
        Vector3 tangent = _course.SampleTangentAtDistance(sampleDistance);
        Vector3 courseRight = Vector3.Cross(groundNormal, tangent).normalized;
        if (courseRight.sqrMagnitude < 0.0001f)
            courseRight = transform.right;

        Vector3 desiredTarget = lookaheadPoint + courseRight * lineOffsetMeters;
        Vector3 desiredDirection = Vector3.ProjectOnPlane(desiredTarget - transform.position, groundNormal);

        if (desiredDirection.sqrMagnitude <= 0.0001f)
            desiredDirection = downhill;

        desiredDirection.Normalize();

        Vector3 localDesired = transform.InverseTransformDirection(desiredDirection);
        float turnSignal = Mathf.Clamp(localDesired.x * turnSensitivity * recoveryStrength * aggression * defensiveTurnBias, -1f, 1f);
        float forwardSignal = Mathf.Clamp(localDesired.z, -1f, 1f);

        float downhillFactor = Mathf.Clamp01(Vector3.Dot(desiredDirection, downhill));
        float desiredSpeed = baseCruiseSpeed
                             * speedMultiplier
                             * tacticalSpeedMultiplier
                             * Mathf.Lerp(0.92f, 1.45f, linePrecision)
                             * aggression
                             * Mathf.Lerp(1f, 1.18f, downhillFactor);

        if (Time.time < _launchBoostUntil)
            desiredSpeed *= _launchStrength;

        float speedError = desiredSpeed - speed;
        float lean = Mathf.Clamp((speedError / 6.5f) + (neutralLeanForwardBias * Mathf.Lerp(0.8f, 1.25f, aggression) * forwardSignal), -1f, 1f);

        float brake01 = 0f;
        if (speed > desiredSpeed)
        {
            brake01 = Mathf.InverseLerp(desiredSpeed, desiredSpeed + Mathf.Lerp(7f, 4f, aggression), speed);
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
        else if (turnSignal > Mathf.Lerp(turnDeadZone, turnDeadZone * 0.35f, overtakeConfidence))
        {
            rightLeg = Mathf.Clamp01(Mathf.Abs(turnSignal));
        }
        else if (turnSignal < -Mathf.Lerp(turnDeadZone, turnDeadZone * 0.35f, overtakeConfidence))
        {
            leftLeg = Mathf.Clamp01(Mathf.Abs(turnSignal));
        }

        return new SkiInputFrame
        {
            leftLeg01 = leftLeg,
            rightLeg01 = rightLeg,
            lean01 = lean,
            polesHeld = speed < desiredSpeed * 0.75f
        };
    }

    private RacerArchetype PickArchetype()
    {
        RaceCourseLine.RaceLeagueDefinition league = _course != null ? _course.GetLeague(_leagueNumber) : null;
        float aggression = league != null ? Mathf.Clamp01(league.npcOvertakeConfidence) : 0.5f;
        float precision = league != null ? Mathf.Clamp01(league.npcLinePrecision) : 0.5f;
        float mistakes = league != null ? Mathf.Clamp01(league.npcMistakeChance) : 0.2f;

        float roll = Random.value;
        if (roll < Mathf.Lerp(0.14f, 0.24f, precision))
            return RacerArchetype.Technician;
        if (roll < Mathf.Lerp(0.28f, 0.46f, aggression))
            return RacerArchetype.Aggressor;
        if (roll < 0.56f)
            return RacerArchetype.Sprinter;
        if (roll < Mathf.Lerp(0.7f, 0.78f, 1f - mistakes))
            return RacerArchetype.SteadyFinisher;
        if (roll < 0.88f)
            return RacerArchetype.Defender;

        return RacerArchetype.WildCard;
    }

    private float GetArchetypeLaunchBias()
    {
        switch (_archetype)
        {
            case RacerArchetype.Sprinter:
                return 0.95f;
            case RacerArchetype.Aggressor:
                return 0.8f;
            case RacerArchetype.Technician:
                return 0.72f;
            case RacerArchetype.SteadyFinisher:
                return 0.58f;
            case RacerArchetype.Defender:
                return 0.48f;
            case RacerArchetype.WildCard:
                return 0.65f;
            default:
                return 0.5f;
        }
    }

    private void ApplyArchetype(ref float speedMultiplier, ref float linePrecision, ref float mistakeChance, ref float recoveryQuality, ref float overtakeConfidence)
    {
        switch (_archetype)
        {
            case RacerArchetype.Technician:
                speedMultiplier *= 0.98f;
                linePrecision = Mathf.Clamp01(linePrecision + 0.22f);
                mistakeChance = Mathf.Clamp01(mistakeChance - 0.1f);
                recoveryQuality = Mathf.Clamp01(recoveryQuality + 0.2f);
                break;

            case RacerArchetype.Aggressor:
                speedMultiplier *= 1.05f;
                linePrecision = Mathf.Clamp01(linePrecision - 0.04f);
                mistakeChance = Mathf.Clamp01(mistakeChance + 0.08f);
                overtakeConfidence = Mathf.Clamp01(overtakeConfidence + 0.22f);
                break;

            case RacerArchetype.Sprinter:
                speedMultiplier *= Time.time < _launchBoostUntil ? 1.06f : 0.98f;
                mistakeChance = Mathf.Clamp01(mistakeChance + 0.03f);
                break;

            case RacerArchetype.SteadyFinisher:
                if (_course != null)
                {
                    float remaining01 = 1f - Mathf.Clamp01(_distanceAlong / Mathf.Max(1f, _course.TotalLengthMeters));
                    speedMultiplier *= Mathf.Lerp(1.06f, 0.97f, remaining01);
                }
                mistakeChance = Mathf.Clamp01(mistakeChance - 0.05f);
                recoveryQuality = Mathf.Clamp01(recoveryQuality + 0.1f);
                break;

            case RacerArchetype.WildCard:
                speedMultiplier *= Random.Range(0.97f, 1.09f);
                linePrecision = Mathf.Clamp01(linePrecision + Random.Range(-0.1f, 0.08f));
                mistakeChance = Mathf.Clamp01(mistakeChance + 0.14f);
                overtakeConfidence = Mathf.Clamp01(overtakeConfidence + 0.08f);
                break;

            case RacerArchetype.Defender:
                linePrecision = Mathf.Clamp01(linePrecision + 0.08f);
                recoveryQuality = Mathf.Clamp01(recoveryQuality + 0.08f);
                overtakeConfidence = Mathf.Clamp01(overtakeConfidence - 0.05f);
                break;
        }
    }

    private void UpdateSegmentState()
    {
        if (_course == null)
            return;

        float aheadA = Mathf.Clamp(_distanceAlong + 8f, 0f, _course.TotalLengthMeters);
        float aheadB = Mathf.Clamp(_distanceAlong + 18f, 0f, _course.TotalLengthMeters);
        Vector3 tangentA = _course.SampleTangentAtDistance(aheadA);
        Vector3 tangentB = _course.SampleTangentAtDistance(aheadB);
        float curvature = Vector3.Angle(tangentA, tangentB);

        Vector3 pointNow = _course.SamplePointAtDistance(_distanceAlong);
        Vector3 pointAhead = _course.SamplePointAtDistance(aheadB);
        float drop = Mathf.Max(0f, pointNow.y - pointAhead.y);
        float remaining = Mathf.Max(0f, _course.TotalLengthMeters - _distanceAlong);

        if (remaining <= 28f)
        {
            _segmentType = RaceSegmentType.Finish;
            _segmentStrength = 1f - Mathf.Clamp01(remaining / 28f);
        }
        else if (drop >= 9f)
        {
            _segmentType = RaceSegmentType.Steep;
            _segmentStrength = Mathf.Clamp01(drop / 18f);
        }
        else if (curvature >= 30f)
        {
            _segmentType = RaceSegmentType.TechnicalTurn;
            _segmentStrength = Mathf.Clamp01(curvature / 55f);
        }
        else if (curvature >= 14f)
        {
            _segmentType = RaceSegmentType.MediumTurn;
            _segmentStrength = Mathf.Clamp01(curvature / 35f);
        }
        else
        {
            _segmentType = RaceSegmentType.Glide;
            _segmentStrength = Mathf.Clamp01((14f - curvature) / 14f);
        }
    }

    private void ApplySegmentModifiers(ref float speedMultiplier, ref float linePrecision, ref float mistakeChance, ref float recoveryQuality, ref float overtakeConfidence)
    {
        switch (_segmentType)
        {
            case RaceSegmentType.Glide:
                speedMultiplier *= Mathf.Lerp(1f, 1.08f, _segmentStrength);
                overtakeConfidence = Mathf.Clamp01(overtakeConfidence + 0.08f * _segmentStrength);
                break;

            case RaceSegmentType.MediumTurn:
                linePrecision = Mathf.Clamp01(linePrecision + 0.06f * _segmentStrength);
                break;

            case RaceSegmentType.TechnicalTurn:
                speedMultiplier *= Mathf.Lerp(1f, 0.92f, _segmentStrength);
                linePrecision = Mathf.Clamp01(linePrecision + 0.16f * _segmentStrength);
                mistakeChance = Mathf.Clamp01(mistakeChance + 0.06f * _segmentStrength);
                break;

            case RaceSegmentType.Steep:
                speedMultiplier *= Mathf.Lerp(1f, 1.04f, _segmentStrength);
                mistakeChance = Mathf.Clamp01(mistakeChance + 0.08f * _segmentStrength);
                recoveryQuality = Mathf.Clamp01(recoveryQuality + 0.05f * _segmentStrength);
                break;

            case RaceSegmentType.Finish:
                speedMultiplier *= Mathf.Lerp(1f, 1.12f, _segmentStrength);
                overtakeConfidence = Mathf.Clamp01(overtakeConfidence + 0.16f * _segmentStrength);
                break;
        }
    }

    private void UpdateTacticalState()
    {
        if (_course == null)
            return;

        if (Time.time < _tacticalModeUntil)
            return;

        _tacticalModeUntil = Time.time + Random.Range(tacticalStateMinSeconds, tacticalStateMaxSeconds);
        _tacticalMode = TacticalMode.HoldLine;

        float aheadGap = float.PositiveInfinity;
        float behindGap = float.PositiveInfinity;
        RaceActivityService service = RaceActivityService.Instance;
        if (service != null)
            service.TryGetNearestOpponentGap(this, _distanceAlong, out aheadGap, out behindGap);

        if (aheadGap <= overtakeTriggerGapMeters)
        {
            _tacticalMode = Random.value < 0.5f ? TacticalMode.PassInside : TacticalMode.PassOutside;
            return;
        }

        if (behindGap <= defendTriggerGapMeters)
        {
            _tacticalMode = TacticalMode.DefendLane;
            return;
        }

        if (_segmentType == RaceSegmentType.Glide && Random.value < 0.35f)
        {
            _tacticalMode = TacticalMode.SetupPass;
            return;
        }

        if (Time.time < _stabilityUntil)
            _tacticalMode = TacticalMode.Stabilize;
    }

    private void ApplyTacticalState(ref float speedMultiplier, ref float laneBias, ref float defensiveTurnBias, float overtakeConfidence)
    {
        switch (_tacticalMode)
        {
            case TacticalMode.SetupPass:
                speedMultiplier *= 1.03f;
                laneBias += Mathf.Sign(_laneBiasNormalized == 0f ? Random.Range(-1f, 1f) : _laneBiasNormalized) * 0.15f;
                break;

            case TacticalMode.PassInside:
                speedMultiplier *= 1.06f;
                laneBias -= Mathf.Lerp(0.18f, 0.32f, overtakeConfidence);
                break;

            case TacticalMode.PassOutside:
                speedMultiplier *= 1.04f;
                laneBias += Mathf.Lerp(0.18f, 0.32f, overtakeConfidence);
                break;

            case TacticalMode.DefendLane:
                speedMultiplier *= 0.98f;
                laneBias += Mathf.Sign(_laneBiasNormalized == 0f ? 1f : _laneBiasNormalized) * 0.12f;
                defensiveTurnBias = 1.08f;
                break;

            case TacticalMode.Stabilize:
                speedMultiplier *= 0.92f;
                defensiveTurnBias = 0.9f;
                break;
        }
    }

    private void ApplyPressureModifiers(ref float speedMultiplier, ref float overtakeConfidence, ref float mistakeChance)
    {
        if (_course == null)
            return;

        if (_course.TryGetActivePlayerRaceState(out float playerAlong, out _))
        {
            float delta = playerAlong - _distanceAlong;
            if (delta > 0f && delta < 12f)
            {
                if (_archetype == RacerArchetype.Aggressor || _archetype == RacerArchetype.WildCard)
                {
                    speedMultiplier *= 1.05f;
                    overtakeConfidence = Mathf.Clamp01(overtakeConfidence + 0.12f);
                    mistakeChance = Mathf.Clamp01(mistakeChance + 0.05f);
                }
                else
                {
                    speedMultiplier *= 1.02f;
                }
            }
            else if (delta < -12f)
            {
                mistakeChance = Mathf.Clamp01(mistakeChance - 0.03f);
            }
        }
    }

    private void UpdateMistakeState()
    {
        if (_course == null)
            return;

        RaceCourseLine.RaceLeagueDefinition league = _course.GetLeague(_leagueNumber);
        float mistakeChance = league != null ? Mathf.Clamp01(league.npcMistakeChance) : 0.2f;
        float linePrecision = league != null ? Mathf.Clamp01(league.npcLinePrecision) : 0.5f;

        if (Time.time < _mistakeUntil)
            return;

        if (Time.time < _nextMistakeDecisionAt)
            return;

        _nextMistakeDecisionAt = Time.time + Random.Range(1.35f, 3.1f) * Mathf.Lerp(1.2f, 0.75f, mistakeChance);
        if (Random.value > mistakeChance * GetMistakeTriggerMultiplier())
        {
            _mistakeLaneBias = 0f;
            _mistakeType = MistakeType.None;
            return;
        }

        _mistakeType = PickMistakeType();
        _mistakeLaneBias = Random.Range(-1f, 1f);
        _mistakeUntil = Time.time + Random.Range(0.45f, 1.35f) * Mathf.Lerp(1.15f, 0.7f, linePrecision);
        _stabilityUntil = _mistakeUntil + Mathf.Lerp(0.4f, 1.25f, 1f - (league != null ? Mathf.Clamp01(league.npcRecoveryQuality) : 0.5f));
        _tacticalMode = TacticalMode.Stabilize;
        _tacticalModeUntil = _stabilityUntil;
    }

    private float GetMistakeTriggerMultiplier()
    {
        float multiplier = 0.6f;

        if (_segmentType == RaceSegmentType.TechnicalTurn || _segmentType == RaceSegmentType.Steep)
            multiplier += 0.12f;

        if (_archetype == RacerArchetype.WildCard)
            multiplier += 0.18f;
        else if (_archetype == RacerArchetype.Technician)
            multiplier -= 0.1f;

        return Mathf.Clamp(multiplier, 0.25f, 0.9f);
    }

    private MistakeType PickMistakeType()
    {
        if (_segmentType == RaceSegmentType.Glide)
            return Random.value < 0.5f ? MistakeType.NervousStraight : MistakeType.EarlyBrake;

        if (_segmentType == RaceSegmentType.TechnicalTurn)
            return Random.value < 0.5f ? MistakeType.LateTurnIn : MistakeType.UnstableCorrection;

        if (_segmentType == RaceSegmentType.Steep)
            return Random.value < 0.5f ? MistakeType.WashedWide : MistakeType.EarlyBrake;

        return (MistakeType)Random.Range(1, 6);
    }

    private void ApplyMistakeProfile(ref float lineOffsetMeters, ref float speedMultiplier, ref float aggression, ref float recoveryStrength)
    {
        switch (_mistakeType)
        {
            case MistakeType.EarlyBrake:
                speedMultiplier *= 0.8f;
                aggression *= 0.9f;
                break;

            case MistakeType.LateTurnIn:
                lineOffsetMeters += _mistakeLaneBias * 3.2f;
                speedMultiplier *= 0.92f;
                break;

            case MistakeType.WashedWide:
                lineOffsetMeters += Mathf.Sign(_mistakeLaneBias == 0f ? 1f : _mistakeLaneBias) * 4.8f;
                speedMultiplier *= 0.82f;
                recoveryStrength *= 0.92f;
                break;

            case MistakeType.NervousStraight:
                speedMultiplier *= 0.86f;
                break;

            case MistakeType.UnstableCorrection:
                lineOffsetMeters += Mathf.Sin(Time.time * 8f) * 2.8f;
                speedMultiplier *= 0.88f;
                recoveryStrength *= 0.84f;
                break;
        }
    }
}
