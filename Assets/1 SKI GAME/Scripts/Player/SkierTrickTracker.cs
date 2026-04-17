using System;
using System.Collections.Generic;
using System.Text;
using SkiGame.Map;
using SkiGame.Tricks;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SkiController))]
public sealed class SkierTrickTracker : MonoBehaviour
{
    public enum SpinDirection
    {
        None = 0,
        Clockwise = 1,
        CounterClockwise = -1
    }

    public enum SegmentState
    {
        Building = 0,
        Pending = 1,
        Locked = 2,
        Failed = 3
    }

    [Serializable]
    public struct TrickComboSegment
    {
        public int id;
        public string text;
        public int progressionTier;
        public float severity01;
        public bool usedTuck;
        public bool usedGrind;
        public bool usedStyle;
        public SegmentState state;

        public int adjectiveTextSeed;
        public int adjectiveFontSeed;
        public int adjectiveColorSeed;
    }

    [Serializable]
    public struct TrickLiveState
    {
        public bool active;
        public string displayName;
        public int spinDegrees;
        public SpinDirection spinDirection;
        public int flipCount;
        public bool backflip;
        public bool frontflip;
        public bool usedGrind;
        public bool usedSlide;
        public bool usedTuck;
        public bool usedStyle;
        public bool usedValidPose;
        public bool poseOnly;
        public bool poseRotationCombo;
        public string primaryPoseLabel;
        public SkiController.AerialPoseFamily primaryPoseFamily;
        public SkiController.AerialPoseShape primaryPoseShape;
        public SkiController.AerialOrientationModifier primaryOrientationModifier;
        public int comboVariety;
        public int estimatedComboScore;
        public float airtime;
        public float severity01;
        public int progressionTier;

        public bool awaitingLandingValidation;
        public int focusSegmentId;
        public TrickComboSegment[] comboSegments;

        public bool useAdjective;
        public string adjective;
        public Color adjectiveColor;
        public int adjectiveFontTierOverride;
    }

    [Serializable]
    public struct TrickResult
    {
        public bool success;
        public string displayName;
        public string attemptedDisplayName;

        public string failureOverlayText;
        public bool failedByStack;
        public bool failedLandingAttempt;

        public int spinDegrees;
        public SpinDirection spinDirection;
        public int flipCount;
        public bool backflip;
        public bool frontflip;
        public bool usedGrind;
        public bool usedSlide;
        public bool usedTuck;
        public bool usedStyle;
        public bool usedValidPose;
        public bool poseOnly;
        public bool poseRotationCombo;

        public string primaryPoseLabel;
        public SkiController.AerialPoseFamily primaryPoseFamily;
        public SkiController.AerialPoseShape primaryPoseShape;
        public SkiController.AerialOrientationModifier primaryOrientationModifier;
        public string[] poseLabels;
        public SkiController.AerialPoseFamily[] poseFamilies;
        public SkiController.AerialPoseShape[] poseShapes;
        public SkiController.AerialOrientationModifier[] orientationModifiers;

        public bool switchLanding;
        public bool noseLanding;
        public bool tailLanding;
        public bool hadBounceFollowup;
        public bool hadTipBounceFollowup;
        public int comboVariety;
        public int rawComboScore;
        public int comboScore;
        public float repetitionPenalty01;
        public string canonicalSignature;
        public string zoneId;

        public float airtime;
        public float peakHeight;
        public float severity01;
        public int progressionTier;

        public int focusSegmentId;
        public TrickComboSegment[] comboSegments;

        public bool useAdjective;
        public string adjective;
        public Color adjectiveColor;
        public int adjectiveFontTierOverride;
    }

    public event Action<TrickLiveState> OnLiveTrickUpdated;
    public event Action<TrickResult> OnTrickResolved;

    [Header("References")]
    [SerializeField] private SkiController skiController;
    [SerializeField] private PlayerMapRegionTracker playerMapRegionTracker;

    [Header("Segment Thresholds")]
    [SerializeField] private float directionReverseDeadzoneDegrees = 20f;
    [SerializeField] private float minSegmentAirTime = 0.12f;
    [SerializeField] private float motionPauseTime = 0.16f;

    [Header("Unlimited Thresholding")]
    [SerializeField] private int spinStepDegrees = 180;
    [SerializeField] private int spinLeniencyDegrees = 30;
    [SerializeField] private int flipStepDegrees = 360;
    [SerializeField] private int flipLeniencyDegrees = 60;

    [Header("Run Gating")]
    [SerializeField] private float minAirtimeForTrick = 0.18f;
    [SerializeField] private float minAngleDeltaToCount = 2f;
    [SerializeField, Range(0f, 1f)] private float tuckLabelThreshold = 0.55f;

    [Header("Grounded Combo Grace")]
    [SerializeField] private float groundedComboGraceTime = 0.9f;
    [SerializeField] private Vector2 bounceTouchTime = new Vector2(0.05f, 0.15f);

    [Header("Landing Trick Gating")]
    [SerializeField] private float minLandingOnlyPeakHeight = 0.45f;
    [SerializeField] private float minLandingOnlyDownwardSpeed = 3.0f;
    [SerializeField] private float minLandingOnlyPlanarSpeed = 4.0f;
    [SerializeField] private float minLandingOnlyAirTime = 0.18f;
    [SerializeField] private float minLandingOnlyHorizontalTravel = 1.2f;

    [Header("End Contact Reliability")]
    [SerializeField] private float minEndContactBaseAlignment = 0.82f;
    [SerializeField] private float maxOpposingEndContactBaseAlignment = 0.92f;
    [SerializeField] private bool allowSingleSkiEndContactForLandingTags = true;

    [Header("Tip Bounce")]
    [SerializeField] private bool enableTipBounces = true;
    [SerializeField] private float minTipBouncePlanarSpeed = 3.5f;
    [SerializeField] private float maxTipBounceBaseAlignment = 0.82f;
    [SerializeField] private float minTipBounceDownwardSpeed = 1.75f;

    [Header("Slide Tracking Advanced")]
    [SerializeField] private float slideStartMaxBaseAlignment = 0.88f;
    [SerializeField] private float slideKeepMaxBaseAlignment = 0.94f;
    [SerializeField] private float slideCoyoteTime = 0.08f;

    [Header("Pose Gating")]
    [SerializeField] private float minPoseHoldTime = 0.20f;
    [SerializeField] private float poseRetriggerCooldown = 0.20f;
    [SerializeField] private float minAirTimeForPoseOnlySegment = 0.30f;
    [SerializeField] private float minGroundedTimeToStartNewCombo = 0.18f;
    [SerializeField] private bool requireRotationOrOrientationForPoseOnlySegment = true;

    [Header("Slide Tracking")]
    [SerializeField] private float minSlideSpeed = 3.0f;
    [SerializeField] private float minSlideDistance = 1.5f;
    [SerializeField] private float maxSlideBaseAlignment = 0.72f;

    [Header("Grind Tracking")]
    [SerializeField] private float minGrindDistance = 1.5f;
    [SerializeField] private float minNoseTailGrindDistance = 0.8f;
    [SerializeField] private float grindSwitchAngle = 135f;

    [Header("Recovery")]
    [SerializeField] private float recoveryDisplayMinDelay = 0.05f;

    private sealed class RuntimeSegment
    {
        public int id;
        public string text;
        public int progressionTier;
        public float severity01;
        public bool usedTuck;
        public bool usedGrind;
        public bool usedStyle;
        public SegmentState state;

        public int adjectiveTextSeed;
        public int adjectiveFontSeed;
        public int adjectiveColorSeed;
    }

    private readonly List<RuntimeSegment> _segments = new List<RuntimeSegment>();

    private bool _runActive;
    private bool _runFailed;
    private bool _wasMobilityActiveLastFrame;
    private bool _touchPhaseActive;
    private bool _bounceAddedThisTouch;
    private bool _landingTagAddedThisTouch;

    private Quaternion _lastRotation;
    private float _runStartTime;
    private float _peakY;
    private Vector3 _runStartPosition;

    private bool _usedGrindDuringRun;
    private bool _usedSlideDuringRun;
    private bool _usedTuckDuringRun;
    private bool _usedStyleDuringRun;
    private bool _usedValidPoseDuringRun;
    private bool _poseOnlyDuringRun;
    private bool _poseRotationComboDuringRun;
    private bool _hadBounceDuringRun;
    private bool _hadTipBounceDuringRun;
    private string _primaryPoseLabelDuringRun = string.Empty;
    private SkiController.AerialPoseFamily _primaryPoseFamilyDuringRun;
    private SkiController.AerialPoseShape _primaryPoseShapeDuringRun;
    private SkiController.AerialOrientationModifier _primaryOrientationDuringRun;
    private readonly HashSet<string> _poseLabelsDuringRun = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<SkiController.AerialPoseFamily> _poseFamiliesDuringRun = new HashSet<SkiController.AerialPoseFamily>();
    private readonly HashSet<SkiController.AerialPoseShape> _poseShapesDuringRun = new HashSet<SkiController.AerialPoseShape>();
    private readonly HashSet<SkiController.AerialOrientationModifier> _orientationModifiersDuringRun = new HashSet<SkiController.AerialOrientationModifier>();
    private readonly TrickScoreHistory _scoreHistory = new TrickScoreHistory();

    private string _currentDisplayName = string.Empty;
    private string _lastBroadcastDisplayName = string.Empty;
    private string _attemptedNameAtFailure = string.Empty;
    private bool _stackedDuringRun;
    private string _lastFailureOverlayText = string.Empty;

    private float _lastStackTime = -999f;
    private float _touchPhaseStartTime;
    private float _stableLandingTimer;
    private float _lastMeaningfulMotionTime;
    private float _groundedNoTrickTimer;

    private int _nextSegmentId = 1;
    private int _focusSegmentId;
    private string _currentPoseSignature = string.Empty;

    private float _segmentYaw;
    private float _segmentPitch;
    private float _segmentStartTime;
    private bool _segmentHasYaw;
    private bool _segmentHasPitch;
    private int _segmentYawDir;
    private int _segmentPitchDir;

    private bool _slideActive;
    private int _slideSign;
    private float _slideDistance;
    private Vector3 _lastSlidePosition;
    private int _slideSegmentId;

    private float _lastSlideValidTime;
    private int _lastTouchEndContactSign;

    private bool _grindTrackActive;
    private float _lastGrindControllerDistance;
    private float _grindNoseDistance;
    private float _grindTailDistance;

    private float _poseHeldStartTime = -999f;
    private float _lastPoseSegmentTime = -999f;
    private float _lastGroundedComboEndTime = -999f;
    private int _resolvedSpinDegreesDuringRun;
    private SpinDirection _resolvedSpinDirectionDuringRun;
    private int _resolvedFlipCountDuringRun;
    private bool _resolvedBackflipDuringRun;
    private bool _resolvedFrontflipDuringRun;

    private void Reset()
    {
        skiController = GetComponent<SkiController>();
        if (playerMapRegionTracker == null)
            playerMapRegionTracker = GetComponent<PlayerMapRegionTracker>();
    }

    private void Awake()
    {
        if (skiController == null)
            skiController = GetComponent<SkiController>();

        if (playerMapRegionTracker == null)
            playerMapRegionTracker = GetComponent<PlayerMapRegionTracker>();
    }

    private void OnEnable()
    {
        if (skiController != null)
        {
            skiController.OnStacked += HandleStacked;
            skiController.OnRecoveredFromStack += HandleRecoveredFromStack;
        }
    }

    private void OnDisable()
    {
        if (skiController != null)
        {
            skiController.OnStacked -= HandleStacked;
            skiController.OnRecoveredFromStack -= HandleRecoveredFromStack;
        }
    }

    private void LateUpdate()
    {
        if (skiController == null)
            return;

        bool mobilityActiveNow = IsTrickMobilityActive();

        if (!_runActive && mobilityActiveNow)
        {
            if ((Time.time - _lastGroundedComboEndTime) >= minGroundedTimeToStartNewCombo)
            {
                BeginRun();
            }

            _wasMobilityActiveLastFrame = true;
            return;
        }

        if (!_runActive)
        {
            _wasMobilityActiveLastFrame = mobilityActiveNow;
            return;
        }

        if (mobilityActiveNow)
        {
            if (!_wasMobilityActiveLastFrame)
                ResumeFromTouchPhase();

            UpdateRun();
        }
        else
        {
            UpdateTouchPhase();
        }

        _wasMobilityActiveLastFrame = mobilityActiveNow;
    }

    private bool IsTrickMobilityActive()
    {
        return skiController.IsAirborne || skiController.IsGrinding;
    }

    private void BeginRun()
    {
        _runActive = true;
        _runFailed = false;
        _touchPhaseActive = false;
        _bounceAddedThisTouch = false;
        _landingTagAddedThisTouch = false;
        _groundedNoTrickTimer = 0f;

        _runStartTime = Time.time;
        _peakY = transform.position.y;
        _runStartPosition = transform.position;
        _lastRotation = transform.rotation;
        _lastMeaningfulMotionTime = Time.time;

        _usedGrindDuringRun = skiController.IsGrinding;
        _usedSlideDuringRun = false;
        _usedTuckDuringRun = skiController.Tuck01 >= tuckLabelThreshold;
        _usedStyleDuringRun = skiController.IsAirStyleActive;
        _usedValidPoseDuringRun = false;
        _poseOnlyDuringRun = false;
        _poseRotationComboDuringRun = false;
        _hadBounceDuringRun = false;
        _hadTipBounceDuringRun = false;
        _primaryPoseLabelDuringRun = string.Empty;
        _primaryPoseFamilyDuringRun = SkiController.AerialPoseFamily.None;
        _primaryPoseShapeDuringRun = SkiController.AerialPoseShape.None;
        _primaryOrientationDuringRun = SkiController.AerialOrientationModifier.None;
        _poseLabelsDuringRun.Clear();
        _poseFamiliesDuringRun.Clear();
        _poseShapesDuringRun.Clear();
        _orientationModifiersDuringRun.Clear();
        _resolvedSpinDegreesDuringRun = 0;
        _resolvedSpinDirectionDuringRun = SpinDirection.None;
        _resolvedFlipCountDuringRun = 0;
        _resolvedBackflipDuringRun = false;
        _resolvedFrontflipDuringRun = false;

        _currentDisplayName = string.Empty;
        _lastBroadcastDisplayName = string.Empty;
        _attemptedNameAtFailure = string.Empty;
        _stackedDuringRun = false;
        _lastFailureOverlayText = string.Empty;
        _currentPoseSignature = string.Empty;

        _poseHeldStartTime = -999f;

        _segments.Clear();
        _focusSegmentId = 0;
        _nextSegmentId = 1;

        ResetCurrentSegment();
        ResetSlideTracking();
        ResetGrindTracking();

        BroadcastLiveState(force: true);
    }

    private void UpdateRun()
    {
        _peakY = Mathf.Max(_peakY, transform.position.y);
        _usedGrindDuringRun |= skiController.IsGrinding;
        _usedTuckDuringRun |= skiController.Tuck01 >= tuckLabelThreshold;
        _usedStyleDuringRun |= skiController.IsAirStyleActive;
        CapturePoseUsage();
        UpdateGrindTracking();

        Quaternion delta = Quaternion.Inverse(_lastRotation) * transform.rotation;
        Vector3 euler = NormalizeSignedEuler(delta.eulerAngles);

        float yawDelta = Mathf.Abs(euler.y) >= minAngleDeltaToCount ? euler.y : 0f;
        float pitchDelta = Mathf.Abs(euler.x) >= minAngleDeltaToCount ? euler.x : 0f;

        if (Mathf.Abs(yawDelta) > 0.0001f || Mathf.Abs(pitchDelta) > 0.0001f)
            _lastMeaningfulMotionTime = Time.time;

        UpdateDirectionalSegment(ref _segmentYaw, ref _segmentHasYaw, ref _segmentYawDir, yawDelta);
        UpdateDirectionalSegment(ref _segmentPitch, ref _segmentHasPitch, ref _segmentPitchDir, pitchDelta);

        UpdatePoseSegmentState();

        _lastRotation = transform.rotation;

        if (!string.IsNullOrWhiteSpace(BuildSegmentDisplayName()) &&
            (Time.time - _lastMeaningfulMotionTime) >= motionPauseTime)
        {
            FinalizeCurrentSegmentIfValid();
        }

        _currentDisplayName = BuildComboString(includeBuildingSegment: true);
        BroadcastLiveState(force: false);
    }

    private void UpdateDirectionalSegment(ref float segmentValue, ref bool hasAxis, ref int dir, float delta)
    {
        if (Mathf.Abs(delta) < minAngleDeltaToCount)
            return;

        int newDir = delta > 0f ? 1 : -1;

        if (!hasAxis)
        {
            hasAxis = true;
            dir = newDir;
            segmentValue = delta;
            _segmentStartTime = Time.time;
            _focusSegmentId = _nextSegmentId;
            return;
        }

        if (dir != 0 && newDir != dir && Mathf.Abs(segmentValue) >= directionReverseDeadzoneDegrees)
        {
            FinalizeCurrentSegmentIfValid();
            BeginNewSegmentAfterReset();
            hasAxis = true;
            dir = newDir;
            segmentValue = delta;
            _focusSegmentId = _nextSegmentId;
            return;
        }

        dir = newDir;
        segmentValue += delta;
    }

    private void UpdatePoseSegmentState()
    {
        string poseSignature = BuildPoseSignature();

        if (string.IsNullOrWhiteSpace(poseSignature))
        {
            if (!string.IsNullOrWhiteSpace(_currentPoseSignature))
            {
                FinalizeCurrentSegmentIfValid();
                _currentPoseSignature = string.Empty;
                _poseHeldStartTime = -999f;
                _lastMeaningfulMotionTime = Time.time;
            }

            return;
        }

        _usedStyleDuringRun |= true;
        CapturePoseUsage();

        if (string.IsNullOrWhiteSpace(_currentPoseSignature))
        {
            _currentPoseSignature = poseSignature;
            _poseHeldStartTime = Time.time;
            _segmentStartTime = Time.time;
            _lastMeaningfulMotionTime = Time.time;
            _focusSegmentId = _nextSegmentId;
            return;
        }

        if (!string.Equals(_currentPoseSignature, poseSignature, StringComparison.Ordinal))
        {
            float poseHeldTime = (_poseHeldStartTime > 0f) ? (Time.time - _poseHeldStartTime) : 0f;
            bool canRetrigger = (Time.time - _lastPoseSegmentTime) >= poseRetriggerCooldown;

            if (poseHeldTime >= minPoseHoldTime && canRetrigger)
                FinalizeCurrentSegmentIfValid();

            BeginNewSegmentAfterReset();
            _currentPoseSignature = poseSignature;
            _poseHeldStartTime = Time.time;
            _segmentStartTime = Time.time;
            _lastMeaningfulMotionTime = Time.time;
            _focusSegmentId = _nextSegmentId;
            return;
        }

        _lastMeaningfulMotionTime = Time.time;
    }

    private void CapturePoseUsage()
    {
        if (skiController == null || !skiController.IsAirPoseActive)
            return;

        string poseLabel = skiController.CurrentTrackedPoseName;
        if (!string.IsNullOrWhiteSpace(poseLabel))
        {
            string trimmedLabel = poseLabel.Trim();
            _usedValidPoseDuringRun = true;
            _poseLabelsDuringRun.Add(trimmedLabel);

            if (string.IsNullOrWhiteSpace(_primaryPoseLabelDuringRun))
                _primaryPoseLabelDuringRun = trimmedLabel;
        }

        SkiController.AerialPoseFamily family = skiController.CurrentPoseFamily;
        if (family != SkiController.AerialPoseFamily.None)
        {
            _poseFamiliesDuringRun.Add(family);
            if (_primaryPoseFamilyDuringRun == SkiController.AerialPoseFamily.None)
                _primaryPoseFamilyDuringRun = family;
        }

        SkiController.AerialPoseShape shape = skiController.CurrentPoseShape;
        if (shape != SkiController.AerialPoseShape.None)
        {
            _poseShapesDuringRun.Add(shape);
            if (_primaryPoseShapeDuringRun == SkiController.AerialPoseShape.None)
                _primaryPoseShapeDuringRun = shape;
        }

        SkiController.AerialOrientationModifier orientation = skiController.CurrentPoseOrientationModifier;
        if (orientation != SkiController.AerialOrientationModifier.None)
        {
            _orientationModifiersDuringRun.Add(orientation);
            if (_primaryOrientationDuringRun == SkiController.AerialOrientationModifier.None)
                _primaryOrientationDuringRun = orientation;
        }
    }

    private void BeginNewSegmentAfterReset()
    {
        _segmentYaw = 0f;
        _segmentPitch = 0f;
        _segmentHasYaw = false;
        _segmentHasPitch = false;
        _segmentYawDir = 0;
        _segmentPitchDir = 0;
        _segmentStartTime = Time.time;
        _lastMeaningfulMotionTime = Time.time;
        _poseHeldStartTime = -999f;
    }

    private void ResetCurrentSegment()
    {
        _segmentYaw = 0f;
        _segmentPitch = 0f;
        _segmentHasYaw = false;
        _segmentHasPitch = false;
        _segmentYawDir = 0;
        _segmentPitchDir = 0;
        _segmentStartTime = Time.time;
        _lastMeaningfulMotionTime = Time.time;
        _currentPoseSignature = string.Empty;
        _poseHeldStartTime = -999f;
    }

    private void FinalizeCurrentSegmentIfValid()
    {
        string segmentName = BuildSegmentDisplayName();
        if (string.IsNullOrWhiteSpace(segmentName))
        {
            ResetCurrentSegment();
            return;
        }

        float segmentAirTime = Time.time - _segmentStartTime;
        if (segmentAirTime < minSegmentAirTime)
        {
            ResetCurrentSegment();
            return;
        }

        int spinCount = ComputeSpinCount(_segmentYaw);
        int spinDegrees = spinCount * Mathf.Max(1, spinStepDegrees);
        int flipCount = ComputeFlipCount(_segmentPitch, out bool backflip, out bool frontflip);
        bool usedStyleNow = skiController != null && skiController.IsAirStyleActive;
        CapturePoseUsage();

        bool hasRotation = spinDegrees > 0 || flipCount > 0;
        bool poseOnly = usedStyleNow && !hasRotation;
        _poseOnlyDuringRun |= poseOnly;
        _poseRotationComboDuringRun |= usedStyleNow && hasRotation;

        if (spinDegrees > _resolvedSpinDegreesDuringRun)
        {
            _resolvedSpinDegreesDuringRun = spinDegrees;
            _resolvedSpinDirectionDuringRun = spinDegrees > 0
                ? (_segmentYaw >= 0f ? SpinDirection.Clockwise : SpinDirection.CounterClockwise)
                : SpinDirection.None;
        }

        if (flipCount > _resolvedFlipCountDuringRun)
        {
            _resolvedFlipCountDuringRun = flipCount;
            _resolvedBackflipDuringRun = backflip;
            _resolvedFrontflipDuringRun = frontflip;
        }

        if (poseOnly)
        {
            float poseHeldTime = (_poseHeldStartTime > 0f) ? (Time.time - _poseHeldStartTime) : 0f;
            bool canRetrigger = (Time.time - _lastPoseSegmentTime) >= poseRetriggerCooldown;

            if (segmentAirTime < minAirTimeForPoseOnlySegment)
            {
                ResetCurrentSegment();
                return;
            }

            if (poseHeldTime < minPoseHoldTime)
            {
                ResetCurrentSegment();
                return;
            }

            if (!canRetrigger)
            {
                ResetCurrentSegment();
                return;
            }

            if (requireRotationOrOrientationForPoseOnlySegment && !HasNonTrivialPoseOrientation())
            {
                ResetCurrentSegment();
                return;
            }
        }

        RuntimeSegment seg = new RuntimeSegment
        {
            id = _nextSegmentId++,
            text = segmentName,
            progressionTier = ComputeProgressionTier(
        spinDegrees,
        flipCount,
        _usedGrindDuringRun,
        _usedTuckDuringRun,
        usedStyleNow,
        false,
        false,
        false,
        segmentName),
            severity01 = ComputeSeverity01(
        spinDegrees,
        flipCount,
        _usedTuckDuringRun,
        _usedGrindDuringRun,
        usedStyleNow,
        false,
        false,
        false),
            usedTuck = _usedTuckDuringRun,
            usedGrind = _usedGrindDuringRun,
            usedStyle = usedStyleNow,
            state = SegmentState.Pending,
            adjectiveTextSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue),
            adjectiveFontSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue),
            adjectiveColorSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue),
        };

        _segments.Add(seg);
        _focusSegmentId = seg.id;

        if (usedStyleNow)
            _lastPoseSegmentTime = Time.time;

        ResetCurrentSegment();
    }

    private void UpdateTouchPhase()
    {
        if (!_touchPhaseActive)
            BeginTouchPhase();

        if (_slideActive)
            UpdateSlideTracking();
        else
            TryStartSlideTracking();

        if (_grindTrackActive)
            FinalizeGrindTracking();

        bool grounded = AreBothSkisGrounded();
        bool performingGroundTrick = IsCurrentlyPerformingAnyTrick();

        if (!HasAnythingWorthResolving() && grounded && !performingGroundTrick)
        {
            _groundedNoTrickTimer += Time.deltaTime;

            if (_groundedNoTrickTimer >= groundedComboGraceTime)
            {
                ClearRunStateWithoutResolving();
                return;
            }
        }
        else if (grounded && !performingGroundTrick)
        {
            _groundedNoTrickTimer += Time.deltaTime;

            if (_groundedNoTrickTimer >= groundedComboGraceTime)
            {
                FinalizeTouchPhaseAndResolve(success: true, failureOverlayText: string.Empty);
                return;
            }
        }
        else
        {
            _groundedNoTrickTimer = 0f;
        }

        _currentDisplayName = BuildComboString(includeBuildingSegment: false);
        BroadcastLiveState(force: false);
    }

    private void BeginTouchPhase()
    {
        _touchPhaseActive = true;
        _touchPhaseStartTime = Time.time;
        _stableLandingTimer = 0f;
        _groundedNoTrickTimer = 0f;
        _bounceAddedThisTouch = false;
        _landingTagAddedThisTouch = false;
        _lastTouchEndContactSign = 0;

        FinalizeCurrentSegmentIfValid();
        TryAddLandingTag();
    }

    private void ResumeFromTouchPhase()
    {
        if (!_touchPhaseActive)
            return;

        float touchDuration = Time.time - _touchPhaseStartTime;

        if (_slideActive)
            FinalizeSlideTracking();

        if (_grindTrackActive)
            FinalizeGrindTracking();

        bool canAddBounce =
            HasMeaningfulResolvedTrickBeforeBounce() &&
            touchDuration >= bounceTouchTime.x &&
            touchDuration <= bounceTouchTime.y;

        if (canAddBounce && !_bounceAddedThisTouch)
        {
            Vector3 groundNormal = skiController.GroundNormal.sqrMagnitude > 0.0001f
                ? skiController.GroundNormal.normalized
                : Vector3.up;

            Vector3 vel = skiController.Velocity;
            float planarSpeed = Vector3.ProjectOnPlane(vel, groundNormal).magnitude;
            float downwardSpeed = Mathf.Max(0f, -Vector3.Dot(vel, groundNormal));

            bool tipBounceAdded = false;

            if (enableTipBounces &&
                _lastTouchEndContactSign != 0 &&
                planarSpeed >= minTipBouncePlanarSpeed &&
                downwardSpeed >= minTipBounceDownwardSpeed)
            {
                string tipBounceName = BuildTipBounceName(_lastTouchEndContactSign);
                if (!string.IsNullOrWhiteSpace(tipBounceName))
                {
                    AddSimpleSegment(tipBounceName, 2, 0.26f, false, false);
                    tipBounceAdded = true;
                    _hadTipBounceDuringRun = true;
                }
            }

            if (!tipBounceAdded)
                AddSimpleSegment("Bounce", 1, 0.18f, false, false);

            _hadBounceDuringRun = true;
            _bounceAddedThisTouch = true;
        }

        _touchPhaseActive = false;
        _stableLandingTimer = 0f;
        _groundedNoTrickTimer = 0f;
        _landingTagAddedThisTouch = false;
        _lastTouchEndContactSign = 0;
        _currentDisplayName = BuildComboString(includeBuildingSegment: true);
        BroadcastLiveState(force: true);
    }

    private void FinalizeTouchPhaseAndResolve(bool success, string failureOverlayText)
    {
        if (_slideActive)
            FinalizeSlideTracking();

        if (_grindTrackActive)
            FinalizeGrindTracking();

        string comboString = BuildComboString(includeBuildingSegment: false);

        TrickResult result = new TrickResult
        {
            success = success,
            displayName = success ? comboString : string.Empty,
            attemptedDisplayName = comboString,
            failureOverlayText = failureOverlayText,
            failedByStack = _stackedDuringRun,
            failedLandingAttempt = !success && _touchPhaseActive,
            spinDegrees = _resolvedSpinDegreesDuringRun,
            spinDirection = _resolvedSpinDirectionDuringRun,
            flipCount = _resolvedFlipCountDuringRun,
            backflip = _resolvedBackflipDuringRun,
            frontflip = _resolvedFrontflipDuringRun,
            usedGrind = _usedGrindDuringRun,
            usedSlide = _usedSlideDuringRun,
            usedTuck = _usedTuckDuringRun,
            usedStyle = _usedStyleDuringRun,
            usedValidPose = _usedValidPoseDuringRun,
            poseOnly = _poseOnlyDuringRun,
            poseRotationCombo = _poseRotationComboDuringRun,
            primaryPoseLabel = _primaryPoseLabelDuringRun,
            primaryPoseFamily = _primaryPoseFamilyDuringRun,
            primaryPoseShape = _primaryPoseShapeDuringRun,
            primaryOrientationModifier = _primaryOrientationDuringRun,
            poseLabels = ToArray(_poseLabelsDuringRun),
            poseFamilies = ToArray(_poseFamiliesDuringRun),
            poseShapes = ToArray(_poseShapesDuringRun),
            orientationModifiers = ToArray(_orientationModifiersDuringRun),
            switchLanding = false,
            noseLanding = false,
            tailLanding = false,
            hadBounceFollowup = _hadBounceDuringRun,
            hadTipBounceFollowup = _hadTipBounceDuringRun,
            comboVariety = 1,
            rawComboScore = 0,
            comboScore = 0,
            repetitionPenalty01 = 0f,
            canonicalSignature = string.Empty,
            zoneId = GetCurrentZoneId(),
            airtime = Mathf.Max(0f, Time.time - _runStartTime),
            peakHeight = Mathf.Max(0f, _peakY - transform.position.y),
            severity01 = ComputeComboSeverity(),
            progressionTier = ComputeComboProgression(),
            focusSegmentId = _focusSegmentId,
            comboSegments = BuildComboSegmentsForResult(success),
            useAdjective = false,
            adjective = string.Empty,
            adjectiveColor = Color.white,
            adjectiveFontTierOverride = 0
        };

        if (success)
        {
            EvaluateLandingStyleTags(ref result);

            if (_lastTouchEndContactSign > 0)
            {
                result.noseLanding = true;
                result.tailLanding = false;
            }
            else if (_lastTouchEndContactSign < 0)
            {
                result.noseLanding = false;
                result.tailLanding = true;
            }
        }

        result.comboVariety = ComputeComboVariety(result);
        TrickDescriptor descriptor = TrickActivityRules.CreateDescriptor(result);
        string signature = TrickActivityRules.BuildCanonicalSignature(descriptor);
        result.canonicalSignature = signature;
        descriptor = TrickActivityRules.CreateDescriptor(result);
        TrickScoreBreakdown scoreBreakdown = TrickActivityRules.Score(descriptor, _scoreHistory);
        result.rawComboScore = scoreBreakdown.rawScore;
        result.comboScore = scoreBreakdown.finalScore;
        result.repetitionPenalty01 = scoreBreakdown.repetitionPenalty01;

        if (success)
            _scoreHistory.Register(TrickActivityRules.CreateDescriptor(result));

        if (SkiGame.Progression.PlayerStatsManager.Instance != null && SkiGame.Progression.PlayerStatsManager.Instance.Profile != null)
            SkiGame.Progression.ProgressionEventRecorder.RecordTrickResolved(SkiGame.Progression.PlayerStatsManager.Instance.Profile, result);

        OnTrickResolved?.Invoke(result);

        _lastGroundedComboEndTime = Time.time;

        _runActive = false;
        _runFailed = false;
        _touchPhaseActive = false;
        _bounceAddedThisTouch = false;
        _landingTagAddedThisTouch = false;
        _groundedNoTrickTimer = 0f;
        _stackedDuringRun = false;
        _attemptedNameAtFailure = string.Empty;
        _lastFailureOverlayText = string.Empty;
        _currentDisplayName = string.Empty;
        _lastBroadcastDisplayName = string.Empty;
        _wasMobilityActiveLastFrame = false;

        _segments.Clear();
        _poseLabelsDuringRun.Clear();
        _poseFamiliesDuringRun.Clear();
        _poseShapesDuringRun.Clear();
        _orientationModifiersDuringRun.Clear();
        ResetCurrentSegment();
        ResetSlideTracking();
    }

    private void HandleStacked(SkiController.StackEventInfo info)
    {
        _lastStackTime = Time.time;

        if (!_runActive)
            return;

        _runFailed = true;
        _stackedDuringRun = true;

        FinalizeCurrentSegmentIfValid();

        if (_slideActive)
            FinalizeSlideTracking();

        if (_grindTrackActive)
            FinalizeGrindTracking();

        _attemptedNameAtFailure = BuildComboString(includeBuildingSegment: false);
        _lastFailureOverlayText = "STACKED";

        FinalizeTouchPhaseAndResolve(success: false, failureOverlayText: "STACKED");
    }

    private void HandleRecoveredFromStack(SkiController.StackRecoveryEventInfo info)
    {
        if ((Time.time - _lastStackTime) < recoveryDisplayMinDelay)
            return;

        TrickComboSegment seg = new TrickComboSegment
        {
            id = 1,
            text = BuildStackRecoveryName(),
            progressionTier = 2,
            severity01 = 0.62f,
            usedTuck = skiController.Tuck01 >= tuckLabelThreshold,
            usedGrind = false,
            usedStyle = false,
            state = SegmentState.Locked,
            adjectiveTextSeed = 101,
            adjectiveFontSeed = 202,
            adjectiveColorSeed = 303
        };

        TrickResult result = new TrickResult
        {
            success = true,
            displayName = seg.text,
            attemptedDisplayName = seg.text,
            failureOverlayText = string.Empty,
            failedByStack = false,
            failedLandingAttempt = false,
            spinDegrees = 0,
            spinDirection = SpinDirection.None,
            flipCount = 0,
            backflip = false,
            frontflip = false,
            usedGrind = false,
            usedSlide = false,
            usedTuck = skiController.Tuck01 >= tuckLabelThreshold,
            usedStyle = false,
            usedValidPose = false,
            poseOnly = false,
            poseRotationCombo = false,
            primaryPoseLabel = string.Empty,
            primaryPoseFamily = SkiController.AerialPoseFamily.None,
            primaryPoseShape = SkiController.AerialPoseShape.None,
            primaryOrientationModifier = SkiController.AerialOrientationModifier.None,
            poseLabels = Array.Empty<string>(),
            poseFamilies = Array.Empty<SkiController.AerialPoseFamily>(),
            poseShapes = Array.Empty<SkiController.AerialPoseShape>(),
            orientationModifiers = Array.Empty<SkiController.AerialOrientationModifier>(),
            switchLanding = IsSwitchStyleRecovery(),
            noseLanding = false,
            tailLanding = false,
            hadBounceFollowup = false,
            hadTipBounceFollowup = false,
            comboVariety = 1,
            rawComboScore = 0,
            comboScore = 0,
            repetitionPenalty01 = 0f,
            canonicalSignature = string.Empty,
            zoneId = GetCurrentZoneId(),
            airtime = 0f,
            peakHeight = 0f,
            severity01 = 0.62f,
            progressionTier = 2,
            focusSegmentId = 1,
            comboSegments = new[] { seg },
            useAdjective = false,
            adjective = string.Empty,
            adjectiveColor = Color.white,
            adjectiveFontTierOverride = 0
        };

        result.comboVariety = ComputeComboVariety(result);
        TrickDescriptor descriptor = TrickActivityRules.CreateDescriptor(result);
        result.canonicalSignature = TrickActivityRules.BuildCanonicalSignature(descriptor);
        TrickScoreBreakdown scoreBreakdown = TrickActivityRules.Score(TrickActivityRules.CreateDescriptor(result), _scoreHistory);
        result.rawComboScore = scoreBreakdown.rawScore;
        result.comboScore = scoreBreakdown.finalScore;
        result.repetitionPenalty01 = scoreBreakdown.repetitionPenalty01;
        _scoreHistory.Register(TrickActivityRules.CreateDescriptor(result));

        if (SkiGame.Progression.PlayerStatsManager.Instance != null && SkiGame.Progression.PlayerStatsManager.Instance.Profile != null)
            SkiGame.Progression.ProgressionEventRecorder.RecordTrickResolved(SkiGame.Progression.PlayerStatsManager.Instance.Profile, result);

        OnTrickResolved?.Invoke(result);
    }

    private bool TryGetResolvedEndContactSign(out int sign, out float strongestBaseAlignment)
    {
        sign = 0;
        strongestBaseAlignment = 1f;

        SkiContact l = skiController.LeftSkiContactRef;
        SkiContact r = skiController.RightSkiContactRef;

        bool leftValid =
            l != null &&
            l.IsGrounded &&
            l.HasTipContact &&
            l.BaseContactAlignment <= minEndContactBaseAlignment;

        bool rightValid =
            r != null &&
            r.IsGrounded &&
            r.HasTipContact &&
            r.BaseContactAlignment <= minEndContactBaseAlignment;

        if (!leftValid && !rightValid)
            return false;

        if (leftValid && rightValid)
        {
            if (l.EndContactSign == r.EndContactSign && l.EndContactSign != 0)
            {
                sign = l.EndContactSign;
                strongestBaseAlignment = Mathf.Min(l.BaseContactAlignment, r.BaseContactAlignment);
                return true;
            }

            // If both are end-contacting but opposing, reject unless one is clearly much flatter than the other.
            if (allowSingleSkiEndContactForLandingTags)
            {
                if (l.BaseContactAlignment <= r.BaseContactAlignment && r.BaseContactAlignment >= maxOpposingEndContactBaseAlignment)
                {
                    sign = l.EndContactSign;
                    strongestBaseAlignment = l.BaseContactAlignment;
                    return sign != 0;
                }

                if (r.BaseContactAlignment <= l.BaseContactAlignment && l.BaseContactAlignment >= maxOpposingEndContactBaseAlignment)
                {
                    sign = r.EndContactSign;
                    strongestBaseAlignment = r.BaseContactAlignment;
                    return sign != 0;
                }
            }

            return false;
        }

        if (allowSingleSkiEndContactForLandingTags)
        {
            if (leftValid)
            {
                sign = l.EndContactSign;
                strongestBaseAlignment = l.BaseContactAlignment;
                return sign != 0;
            }

            if (rightValid)
            {
                sign = r.EndContactSign;
                strongestBaseAlignment = r.BaseContactAlignment;
                return sign != 0;
            }
        }

        return false;
    }

    private string BuildTipBounceName(int endContactSign)
    {
        if (endContactSign > 0)
            return "Nose Bounce";

        if (endContactSign < 0)
            return "Tail Bounce";

        return string.Empty;
    }

    private void TryAddLandingTag()
    {
        if (_landingTagAddedThisTouch)
            return;

        float airtime = Time.time - _runStartTime;
        float peakHeight = Mathf.Max(0f, _peakY - transform.position.y);
        float horizontalTravel = Vector3.ProjectOnPlane(transform.position - _runStartPosition, Vector3.up).magnitude;

        Vector3 groundNormal = skiController.GroundNormal.sqrMagnitude > 0.0001f
            ? skiController.GroundNormal.normalized
            : Vector3.up;

        Vector3 vel = skiController.Velocity;
        float downwardSpeed = Mathf.Max(0f, -Vector3.Dot(vel, groundNormal));
        float planarSpeed = Vector3.ProjectOnPlane(vel, groundNormal).magnitude;

        bool landingTagGate =
            airtime >= minLandingOnlyAirTime &&
            peakHeight >= minLandingOnlyPeakHeight &&
            downwardSpeed >= minLandingOnlyDownwardSpeed &&
            planarSpeed >= minLandingOnlyPlanarSpeed &&
            horizontalTravel >= minLandingOnlyHorizontalTravel;

        if (!landingTagGate)
            return;

        if (!TryGetResolvedEndContactSign(out int endSign, out _))
            return;

        TrickResult landingProbe = new TrickResult();
        EvaluateLandingStyleTags(ref landingProbe);

        // Enforce the resolved contact sign so the tag matches the actual dominant tip/tail contact.
        landingProbe.noseLanding = endSign > 0;
        landingProbe.tailLanding = endSign < 0;

        string landingText = BuildLandingOnlyName(landingProbe);
        if (string.IsNullOrWhiteSpace(landingText))
            return;

        AddSimpleSegment(
            landingText,
            ComputeProgressionTier(
                0,
                0,
                false,
                false,
                false,
                landingProbe.switchLanding,
                landingProbe.noseLanding,
                landingProbe.tailLanding,
                landingText),
            0.24f,
            false,
            false);

        _lastTouchEndContactSign = endSign;
        _landingTagAddedThisTouch = true;
    }

    private void TryStartSlideTracking()
    {
        if (_slideActive)
            return;

        if (!TryGetResolvedEndContactSign(out int resolvedSign, out float strongestBase))
            return;

        Vector3 groundNormal = skiController.GroundNormal.sqrMagnitude > 0.0001f
            ? skiController.GroundNormal.normalized
            : Vector3.up;

        float planarSpeed = Vector3.ProjectOnPlane(skiController.Velocity, groundNormal).magnitude;
        if (planarSpeed < minSlideSpeed)
            return;

        if (strongestBase > slideStartMaxBaseAlignment)
            return;

        _slideActive = true;
        _slideSign = resolvedSign;
        _slideDistance = 0f;
        _lastSlidePosition = transform.position;
        _slideSegmentId = _nextSegmentId;
        _focusSegmentId = _slideSegmentId;
        _lastSlideValidTime = Time.time;
    }

    private void UpdateSlideTracking()
    {
        if (!_slideActive)
            return;

        bool hasResolvedSign = TryGetResolvedEndContactSign(out int resolvedSign, out float strongestBase);

        Vector3 groundNormal = skiController.GroundNormal.sqrMagnitude > 0.0001f
            ? skiController.GroundNormal.normalized
            : Vector3.up;

        float planarSpeed = Vector3.ProjectOnPlane(skiController.Velocity, groundNormal).magnitude;
        Vector3 planarDelta = Vector3.ProjectOnPlane(transform.position - _lastSlidePosition, Vector3.up);
        _slideDistance += planarDelta.magnitude;
        _lastSlidePosition = transform.position;

        bool signMatches = hasResolvedSign && resolvedSign == _slideSign;
        bool alignmentOkay = hasResolvedSign && strongestBase <= slideKeepMaxBaseAlignment;
        bool speedOkay = planarSpeed >= minSlideSpeed;
        bool stillValidNow = signMatches && alignmentOkay && speedOkay && !skiController.IsStacked;

        if (stillValidNow)
        {
            _lastSlideValidTime = Time.time;
            return;
        }

        if ((Time.time - _lastSlideValidTime) > slideCoyoteTime)
            FinalizeSlideTracking();
    }

    private void FinalizeSlideTracking()
    {
        if (!_slideActive)
            return;

        if (_slideDistance >= minSlideDistance)
        {
            _usedSlideDuringRun = true;
            string slideText = _slideSign > 0
                ? $"Noseslide {Mathf.RoundToInt(_slideDistance)}m"
                : $"Tailslide {Mathf.RoundToInt(_slideDistance)}m";

            AddSimpleSegment(
                slideText,
                Mathf.Max(1, Mathf.RoundToInt(_slideDistance / 4f)),
                Mathf.Clamp01(0.22f + (_slideDistance / 24f)),
                false,
                false);
        }

        ResetSlideTracking();
    }

    private void ResetSlideTracking()
    {
        _slideActive = false;
        _slideSign = 0;
        _slideDistance = 0f;
        _slideSegmentId = 0;
        _lastSlidePosition = transform.position;
    }

    private void UpdateGrindTracking()
    {
        if (skiController == null)
            return;

        if (!skiController.IsGrinding)
        {
            if (_grindTrackActive)
                FinalizeGrindTracking();

            return;
        }

        float controllerDistance = skiController.CurrentGrindDistance;

        if (!_grindTrackActive)
        {
            _grindTrackActive = true;
            _lastGrindControllerDistance = controllerDistance;
            _grindNoseDistance = 0f;
            _grindTailDistance = 0f;
            return;
        }

        float delta = Mathf.Max(0f, controllerDistance - _lastGrindControllerDistance);
        _lastGrindControllerDistance = controllerDistance;

        int endSign = skiController.CurrentGrindEndContactSign;
        if (endSign > 0)
            _grindNoseDistance += delta;
        else if (endSign < 0)
            _grindTailDistance += delta;
    }

    private void FinalizeGrindTracking()
    {
        if (!_grindTrackActive)
            return;

        float liveDistance = skiController != null ? skiController.CurrentGrindDistance : 0f;
        float totalDistance = Mathf.Max(_lastGrindControllerDistance, liveDistance);

        if (totalDistance >= minGrindDistance)
        {
            _usedGrindDuringRun = true;
            string baseText = $"Grind {Mathf.RoundToInt(totalDistance)}m";
            AddSimpleSegment(
                baseText,
                Mathf.Max(1, Mathf.RoundToInt(totalDistance / 4f)),
                Mathf.Clamp01(0.24f + (totalDistance / 26f)),
                false,
                true);

            if (_grindNoseDistance >= minNoseTailGrindDistance && _grindNoseDistance > _grindTailDistance)
            {
                AddSimpleSegment(
                    $"Nose Grind {Mathf.RoundToInt(_grindNoseDistance)}m",
                    Mathf.Max(1, Mathf.RoundToInt(_grindNoseDistance / 4f)),
                    Mathf.Clamp01(0.22f + (_grindNoseDistance / 24f)),
                    false,
                    true);
            }
            else if (_grindTailDistance >= minNoseTailGrindDistance && _grindTailDistance > _grindNoseDistance)
            {
                AddSimpleSegment(
                    $"Tail Grind {Mathf.RoundToInt(_grindTailDistance)}m",
                    Mathf.Max(1, Mathf.RoundToInt(_grindTailDistance / 4f)),
                    Mathf.Clamp01(0.22f + (_grindTailDistance / 24f)),
                    false,
                    true);
            }

            if (IsSwitchGrind())
            {
                AddSimpleSegment(
                    "Switch Grind",
                    Mathf.Max(1, Mathf.RoundToInt(totalDistance / 5f)),
                    Mathf.Clamp01(0.18f + (totalDistance / 30f)),
                    false,
                    true);
            }
        }

        ResetGrindTracking();
    }

    private void ResetGrindTracking()
    {
        _grindTrackActive = false;
        _lastGrindControllerDistance = 0f;
        _grindNoseDistance = 0f;
        _grindTailDistance = 0f;
    }

    private bool IsSwitchGrind()
    {
        if (skiController == null)
            return false;

        Vector3 up = skiController.GroundNormal.sqrMagnitude > 0.0001f
            ? skiController.GroundNormal.normalized
            : Vector3.up;

        Vector3 velOnPlane = Vector3.ProjectOnPlane(skiController.Velocity, up);
        Vector3 fwdOnPlane = Vector3.ProjectOnPlane(transform.forward, up);

        if (velOnPlane.sqrMagnitude < 0.01f || fwdOnPlane.sqrMagnitude < 0.01f)
            return false;

        float rawAngle = Vector3.Angle(fwdOnPlane.normalized, velOnPlane.normalized);
        return rawAngle >= grindSwitchAngle;
    }

    private int GetSharedEndContactSign()
    {
        SkiContact l = skiController.LeftSkiContactRef;
        SkiContact r = skiController.RightSkiContactRef;

        bool leftEnd = l != null && l.IsGrounded && l.HasTipContact;
        bool rightEnd = r != null && r.IsGrounded && r.HasTipContact;

        if (!leftEnd || !rightEnd)
            return 0;

        if (l.EndContactSign > 0 && r.EndContactSign > 0)
            return 1;

        if (l.EndContactSign < 0 && r.EndContactSign < 0)
            return -1;

        return 0;
    }

    private void AddSimpleSegment(string text, int progressionTier, float severity01, bool usedTuck, bool usedGrind)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        RuntimeSegment seg = new RuntimeSegment
        {
            id = _nextSegmentId++,
            text = text,
            progressionTier = Mathf.Max(1, progressionTier),
            severity01 = Mathf.Clamp01(severity01),
            usedTuck = usedTuck,
            usedGrind = usedGrind,
            usedStyle = false,
            state = SegmentState.Pending,
            adjectiveTextSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue),
            adjectiveFontSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue),
            adjectiveColorSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue),
        };

        _segments.Add(seg);
        _focusSegmentId = seg.id;
    }

    private void BroadcastLiveState(bool force)
    {
        if (!_runActive)
            return;

        int spinCount = ComputeSpinCount(_segmentYaw);
        int spinDegrees = spinCount * Mathf.Max(1, spinStepDegrees);
        int flipCount = ComputeFlipCount(_segmentPitch, out bool backflip, out bool frontflip);

        TrickComboSegment[] liveSegments = BuildComboSegmentsForLive();

        string liveDisplayName = string.Empty;
        if (liveSegments != null && liveSegments.Length > 0)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < liveSegments.Length; i++)
            {
                if (i > 0)
                    sb.Append(" + ");

                sb.Append(liveSegments[i].text);
            }

            liveDisplayName = sb.ToString();
        }

        TrickLiveState state = new TrickLiveState
        {
            active = liveSegments != null && liveSegments.Length > 0,
            displayName = liveDisplayName,
            spinDegrees = spinDegrees,
            spinDirection = spinCount > 0
                ? (_segmentYaw >= 0f ? SpinDirection.Clockwise : SpinDirection.CounterClockwise)
                : SpinDirection.None,
            flipCount = flipCount,
            backflip = backflip,
            frontflip = frontflip,
            usedGrind = _usedGrindDuringRun,
            usedSlide = _usedSlideDuringRun || _slideActive,
            usedTuck = _usedTuckDuringRun,
            usedStyle = _usedStyleDuringRun,
            usedValidPose = _usedValidPoseDuringRun,
            poseOnly = _poseOnlyDuringRun,
            poseRotationCombo = _poseRotationComboDuringRun,
            primaryPoseLabel = _primaryPoseLabelDuringRun,
            primaryPoseFamily = _primaryPoseFamilyDuringRun,
            primaryPoseShape = _primaryPoseShapeDuringRun,
            primaryOrientationModifier = _primaryOrientationDuringRun,
            comboVariety = ComputeLiveComboVariety(),
            estimatedComboScore = ComputeLiveComboScore(),
            airtime = Mathf.Max(0f, Time.time - _runStartTime),
            severity01 = ComputeLiveSeverity01(),
            progressionTier = ComputeLiveProgression(),
            awaitingLandingValidation = _touchPhaseActive,
            focusSegmentId = _focusSegmentId,
            comboSegments = liveSegments,
            useAdjective = false,
            adjective = string.Empty,
            adjectiveColor = Color.white,
            adjectiveFontTierOverride = 0
        };

        bool changed =
            force ||
            !string.Equals(state.displayName, _lastBroadcastDisplayName, StringComparison.Ordinal) ||
            state.active != (!string.IsNullOrEmpty(_lastBroadcastDisplayName));

        if (changed)
        {
            _lastBroadcastDisplayName = state.displayName ?? string.Empty;
            OnLiveTrickUpdated?.Invoke(state);
        }
    }

    private TrickComboSegment[] BuildComboSegmentsForLive()
    {
        List<TrickComboSegment> result = new List<TrickComboSegment>(_segments.Count + 1);

        for (int i = 0; i < _segments.Count; i++)
        {
            RuntimeSegment seg = _segments[i];
            result.Add(new TrickComboSegment
            {
                id = seg.id,
                text = seg.text,
                progressionTier = seg.progressionTier,
                severity01 = seg.severity01,
                usedTuck = seg.usedTuck,
                usedGrind = seg.usedGrind,
                usedStyle = seg.usedStyle,
                state = seg.state,
                adjectiveTextSeed = seg.adjectiveTextSeed,
                adjectiveFontSeed = seg.adjectiveFontSeed,
                adjectiveColorSeed = seg.adjectiveColorSeed
            });
        }

        if (CanShowCurrentBuildingSegmentLive())
        {
            string buildingText = BuildSegmentDisplayName();
            result.Add(new TrickComboSegment
            {
                id = _nextSegmentId,
                text = buildingText,
                progressionTier = ComputeLiveProgression(),
                severity01 = ComputeLiveSeverity01(),
                usedTuck = _usedTuckDuringRun,
                usedGrind = _usedGrindDuringRun,
                usedStyle = skiController != null && skiController.IsAirStyleActive,
                state = SegmentState.Building,
                adjectiveTextSeed = _nextSegmentId * 73856093,
                adjectiveFontSeed = _nextSegmentId * 19349663,
                adjectiveColorSeed = _nextSegmentId * 83492791
            });

            _focusSegmentId = _nextSegmentId;
        }

        return result.ToArray();
    }

    private TrickComboSegment[] BuildComboSegmentsForResult(bool success)
    {
        TrickComboSegment[] result = new TrickComboSegment[_segments.Count];
        for (int i = 0; i < _segments.Count; i++)
        {
            RuntimeSegment seg = _segments[i];
            result[i] = new TrickComboSegment
            {
                id = seg.id,
                text = seg.text,
                progressionTier = seg.progressionTier,
                severity01 = seg.severity01,
                usedTuck = seg.usedTuck,
                usedGrind = seg.usedGrind,
                usedStyle = seg.usedStyle,
                state = success ? SegmentState.Locked : SegmentState.Failed,
                adjectiveTextSeed = seg.adjectiveTextSeed,
                adjectiveFontSeed = seg.adjectiveFontSeed,
                adjectiveColorSeed = seg.adjectiveColorSeed
            };
        }

        return result;
    }

    private int ComputeLiveProgression()
    {
        string liveName = BuildSegmentDisplayName();
        int spinCount = ComputeSpinCount(_segmentYaw);
        int spinDegrees = spinCount * Mathf.Max(1, spinStepDegrees);
        int flipCount = ComputeFlipCount(_segmentPitch, out _, out _);

        if (string.IsNullOrWhiteSpace(liveName))
            return Mathf.Max(1, ComputeComboProgression());

        return ComputeProgressionTier(
                spinDegrees,
                flipCount,
                _usedGrindDuringRun,
                _usedTuckDuringRun,
                _usedStyleDuringRun,
                false,
                false,
                false,
                liveName);
    }

    private float ComputeLiveSeverity01()
    {
        string liveName = BuildSegmentDisplayName();
        int spinCount = ComputeSpinCount(_segmentYaw);
        int spinDegrees = spinCount * Mathf.Max(1, spinStepDegrees);
        int flipCount = ComputeFlipCount(_segmentPitch, out _, out _);

        if (string.IsNullOrWhiteSpace(liveName))
            return ComputeComboSeverity();

        return ComputeSeverity01(
            spinDegrees,
            flipCount,
            _usedTuckDuringRun,
            _usedGrindDuringRun,
            _usedStyleDuringRun,
            false,
            false,
            false);
    }

    private int ComputeComboProgression()
    {
        int max = 1;
        for (int i = 0; i < _segments.Count; i++)
            max = Mathf.Max(max, _segments[i].progressionTier);
        return max;
    }

    private float ComputeComboSeverity()
    {
        float max = 0.15f;
        for (int i = 0; i < _segments.Count; i++)
            max = Mathf.Max(max, _segments[i].severity01);
        return max;
    }

    private string BuildComboString(bool includeBuildingSegment)
    {
        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < _segments.Count; i++)
        {
            if (i > 0)
                sb.Append(" + ");

            sb.Append(_segments[i].text);
        }

        if (includeBuildingSegment)
        {
            string building = BuildSegmentDisplayName();
            if (!string.IsNullOrWhiteSpace(building))
            {
                if (sb.Length > 0)
                    sb.Append(" + ");

                sb.Append(building);
            }
            else if (_slideActive && _slideDistance > 0.01f)
            {
                if (sb.Length > 0)
                    sb.Append(" + ");

                sb.Append(_slideSign > 0
                    ? $"Noseslide {Mathf.RoundToInt(_slideDistance)}m"
                    : $"Tailslide {Mathf.RoundToInt(_slideDistance)}m");
            }
            else if (_grindTrackActive && skiController != null && skiController.CurrentGrindDistance > 0.01f)
            {
                if (sb.Length > 0)
                    sb.Append(" + ");

                sb.Append($"Grind {Mathf.RoundToInt(skiController.CurrentGrindDistance)}m");
            }
        }

        return sb.ToString().Trim();
    }

    
    private string BuildSegmentDisplayName()
    {
        int spinCount = ComputeSpinCount(_segmentYaw);
        int spinDegrees = spinCount * Mathf.Max(1, spinStepDegrees);
        int flipCount = ComputeFlipCount(_segmentPitch, out bool backflip, out bool frontflip);

        SpinDirection spinDirection = spinCount > 0
            ? (_segmentYaw >= 0f ? SpinDirection.Clockwise : SpinDirection.CounterClockwise)
            : SpinDirection.None;

        string spinName = BuildSpinName(spinDegrees, spinDirection);
        string flipName = BuildFlipName(flipCount, backflip, frontflip);
        string poseDescriptor = BuildPoseDescriptor();

        string rotationBody;
        if (!string.IsNullOrEmpty(spinName) && !string.IsNullOrEmpty(flipName))
            rotationBody = $"{spinName} {flipName}";
        else if (!string.IsNullOrEmpty(flipName))
            rotationBody = flipName;
        else
            rotationBody = spinName;

        if (!string.IsNullOrEmpty(poseDescriptor) && !string.IsNullOrEmpty(rotationBody))
            return $"{poseDescriptor} {rotationBody}";

        if (!string.IsNullOrEmpty(poseDescriptor))
            return poseDescriptor;

        if (!string.IsNullOrEmpty(rotationBody))
            return rotationBody;

        return string.Empty;
    }

    private string BuildPoseSignature()
    {
        if (skiController == null || !skiController.IsAirPoseActive)
            return string.Empty;

        string poseName = skiController.CurrentTrackedPoseName;
        if (string.IsNullOrWhiteSpace(poseName))
            return string.Empty;

        string orientation = BuildOrientationModifier();
        return string.IsNullOrWhiteSpace(orientation) ? poseName : $"{orientation}|{poseName}";
    }

    private string BuildOrientationModifier()
    {
        if (skiController == null || !skiController.IsAirPoseActive)
            return string.Empty;

        return skiController.CurrentPoseOrientationModifier switch
        {
            SkiController.AerialOrientationModifier.Switch => "Switch",
            SkiController.AerialOrientationModifier.Inverted => "Inverted",
            SkiController.AerialOrientationModifier.Sideways => "Sideways",
            SkiController.AerialOrientationModifier.Rising => "Rising",
            SkiController.AerialOrientationModifier.Diving => "Diving",
            _ => string.Empty
        };
    }

    private string BuildPoseDescriptor()
    {
        if (skiController == null || !skiController.IsAirPoseActive)
            return string.Empty;

        string poseName = skiController.CurrentTrackedPoseName;
        if (string.IsNullOrWhiteSpace(poseName))
            return string.Empty;

        string orientation = BuildOrientationModifier();
        return string.IsNullOrWhiteSpace(orientation)
            ? poseName
            : $"{orientation} {poseName}";
    }

    private void EvaluateLandingStyleTags(ref TrickResult result)
    {
        Vector3 groundNormal = skiController.GroundNormal.sqrMagnitude > 0.0001f
            ? skiController.GroundNormal.normalized
            : Vector3.up;

        Vector3 velOnPlane = Vector3.ProjectOnPlane(skiController.Velocity, groundNormal);
        Vector3 fwdOnPlane = Vector3.ProjectOnPlane(transform.forward, groundNormal);

        if (velOnPlane.sqrMagnitude > 0.01f && fwdOnPlane.sqrMagnitude > 0.01f)
        {
            float rawAngle = Vector3.Angle(fwdOnPlane.normalized, velOnPlane.normalized);
            result.switchLanding = rawAngle > 135f;
        }

        SkiContact l = skiController.LeftSkiContactRef;
        SkiContact r = skiController.RightSkiContactRef;

        bool leftEnd = l != null && l.IsGrounded && l.HasTipContact;
        bool rightEnd = r != null && r.IsGrounded && r.HasTipContact;

        if (leftEnd && rightEnd)
        {
            if (l.EndContactSign > 0 && r.EndContactSign > 0)
                result.noseLanding = true;
            else if (l.EndContactSign < 0 && r.EndContactSign < 0)
                result.tailLanding = true;
        }
    }

    private string BuildLandingOnlyName(TrickResult result)
    {
        StringBuilder sb = new StringBuilder();

        if (result.switchLanding)
            sb.Append("Switch ");

        if (result.noseLanding)
            sb.Append("Nose ");
        else if (result.tailLanding)
            sb.Append("Tail ");

        if (sb.Length == 0)
            return string.Empty;

        sb.Append("Landing");
        return sb.ToString().Trim();
    }

    private string BuildStackRecoveryName()
    {
        return skiController.Tuck01 >= tuckLabelThreshold
            ? "Tucked Stack Recovery"
            : "Stack Recovery";
    }

    private bool IsSwitchStyleRecovery()
    {
        Vector3 groundNormal = skiController.GroundNormal.sqrMagnitude > 0.0001f
            ? skiController.GroundNormal.normalized
            : Vector3.up;

        Vector3 velOnPlane = Vector3.ProjectOnPlane(skiController.Velocity, groundNormal);
        Vector3 fwdOnPlane = Vector3.ProjectOnPlane(transform.forward, groundNormal);

        if (velOnPlane.sqrMagnitude < 0.01f || fwdOnPlane.sqrMagnitude < 0.01f)
            return false;

        float rawAngle = Vector3.Angle(fwdOnPlane.normalized, velOnPlane.normalized);
        return rawAngle > 135f;
    }

    private int ComputeSpinCount(float signedYaw)
    {
        int step = Mathf.Max(1, spinStepDegrees);
        int effective = Mathf.Max(0, Mathf.RoundToInt(Mathf.Abs(signedYaw)) + Mathf.Max(0, spinLeniencyDegrees));
        return effective / step;
    }

    private int ComputeFlipCount(float pitch, out bool backflip, out bool frontflip)
    {
        int step = Mathf.Max(1, flipStepDegrees);
        int leniency = Mathf.Max(0, flipLeniencyDegrees);

        int backCount = pitch < 0f ? (Mathf.RoundToInt(Mathf.Abs(pitch)) + leniency) / step : 0;
        int frontCount = pitch > 0f ? (Mathf.RoundToInt(Mathf.Abs(pitch)) + leniency) / step : 0;

        if (backCount >= frontCount)
        {
            backflip = backCount > 0;
            frontflip = false;
            return backCount;
        }

        backflip = false;
        frontflip = frontCount > 0;
        return frontCount;
    }

    private int ComputeProgressionTier(
    int spinDegrees,
    int flipCount,
    bool usedGrind,
    bool usedTuck,
    bool usedStyle,
    bool switchLanding,
    bool noseLanding,
    bool tailLanding,
    string resolvedName)
    {
        int spinCount = Mathf.Max(0, spinStepDegrees > 0 ? spinDegrees / spinStepDegrees : 0);

        int progressionLevel =
             spinCount +
             flipCount +
             (usedGrind ? 1 : 0) +
             (usedTuck ? 1 : 0) +
             (usedStyle ? 1 : 0) +
             (switchLanding ? 1 : 0) +
             ((noseLanding || tailLanding) ? 1 : 0);

        if (!string.IsNullOrWhiteSpace(resolvedName) &&
            resolvedName.IndexOf("Recovery", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            progressionLevel = Mathf.Max(progressionLevel, 2);
        }

        return Mathf.Max(1, progressionLevel);
    }

    private static string BuildSpinName(int spin, SpinDirection direction)
    {
        if (spin <= 0)
            return string.Empty;

        string dir = direction switch
        {
            SpinDirection.Clockwise => "Clockwise",
            SpinDirection.CounterClockwise => "Counter-Clockwise",
            _ => string.Empty
        };

        return string.IsNullOrEmpty(dir) ? spin.ToString() : $"{dir} {spin}";
    }

    private static string BuildFlipName(int flipCount, bool backflip, bool frontflip)
    {
        if (flipCount <= 0)
            return string.Empty;

        string baseName = backflip ? "Backflip" : frontflip ? "Frontflip" : "Flip";

        if (flipCount == 1)
            return baseName;
        if (flipCount == 2)
            return $"Double {baseName}";
        if (flipCount == 3)
            return $"Triple {baseName}";

        return $"{flipCount}x {baseName}";
    }

    private static float ComputeSeverity01(
    int spinDegrees,
    int flipCount,
    bool usedTuck,
    bool usedGrind,
    bool usedStyle,
    bool switchLanding,
    bool noseLanding,
    bool tailLanding)
    {
        float spin01 = Mathf.Clamp01(spinDegrees / 900f);
        float flip01 = Mathf.Clamp01(flipCount / 3f);

        float severity =
                spin01 * 0.46f +
                flip01 * 0.32f +
                (usedTuck ? 0.06f : 0f) +
                (usedGrind ? 0.05f : 0f) +
                (usedStyle ? 0.08f : 0f) +
                (switchLanding ? 0.06f : 0f) +
                (noseLanding || tailLanding ? 0.08f : 0f);

        return Mathf.Clamp01(severity);
    }

    private bool AreBothSkisGrounded()
    {
        SkiContact l = skiController != null ? skiController.LeftSkiContactRef : null;
        SkiContact r = skiController != null ? skiController.RightSkiContactRef : null;

        if (l == null || r == null)
            return false;

        return l.IsGrounded && r.IsGrounded;
    }

    private bool IsCurrentlyPerformingAnyTrick()
    {
        if (!string.IsNullOrWhiteSpace(BuildSegmentDisplayName()))
            return true;

        if (_slideActive && _slideDistance > 0.01f)
            return true;

        if (_grindTrackActive && skiController != null && skiController.CurrentGrindDistance > 0.01f)
            return true;

        return false;
    }

    private int ComputeLiveComboVariety()
    {
        int liveFlipCount = ComputeFlipCount(_segmentPitch, out bool liveBackflip, out bool liveFrontflip);
        TrickResult probe = new TrickResult
        {
            success = true,
            spinDegrees = ComputeSpinCount(_segmentYaw) * Mathf.Max(1, spinStepDegrees),
            flipCount = liveFlipCount,
            backflip = liveBackflip,
            frontflip = liveFrontflip,
            usedGrind = _usedGrindDuringRun,
            usedSlide = _usedSlideDuringRun || _slideActive,
            usedTuck = _usedTuckDuringRun,
            usedStyle = _usedStyleDuringRun,
            usedValidPose = _usedValidPoseDuringRun,
            poseOnly = _poseOnlyDuringRun,
            poseRotationCombo = _poseRotationComboDuringRun,
            switchLanding = false,
            noseLanding = false,
            tailLanding = false,
            hadBounceFollowup = _hadBounceDuringRun,
            hadTipBounceFollowup = _hadTipBounceDuringRun
        };

        return ComputeComboVariety(probe);
    }

    private int ComputeLiveComboScore()
    {
        int liveSpinCount = ComputeSpinCount(_segmentYaw);
        int liveFlipCount = ComputeFlipCount(_segmentPitch, out bool liveBackflip, out bool liveFrontflip);
        TrickResult probe = new TrickResult
        {
            success = true,
            displayName = BuildComboString(includeBuildingSegment: true),
            spinDegrees = liveSpinCount * Mathf.Max(1, spinStepDegrees),
            spinDirection = liveSpinCount > 0
                ? (_segmentYaw >= 0f ? SpinDirection.Clockwise : SpinDirection.CounterClockwise)
                : SpinDirection.None,
            flipCount = liveFlipCount,
            backflip = liveBackflip,
            frontflip = liveFrontflip,
            usedGrind = _usedGrindDuringRun,
            usedSlide = _usedSlideDuringRun || _slideActive,
            usedTuck = _usedTuckDuringRun,
            usedStyle = _usedStyleDuringRun,
            usedValidPose = _usedValidPoseDuringRun,
            poseOnly = _poseOnlyDuringRun,
            poseRotationCombo = _poseRotationComboDuringRun,
            primaryPoseLabel = _primaryPoseLabelDuringRun,
            primaryPoseFamily = _primaryPoseFamilyDuringRun,
            primaryPoseShape = _primaryPoseShapeDuringRun,
            primaryOrientationModifier = _primaryOrientationDuringRun,
            poseLabels = ToArray(_poseLabelsDuringRun),
            poseFamilies = ToArray(_poseFamiliesDuringRun),
            poseShapes = ToArray(_poseShapesDuringRun),
            orientationModifiers = ToArray(_orientationModifiersDuringRun),
            switchLanding = false,
            noseLanding = false,
            tailLanding = false,
            hadBounceFollowup = _hadBounceDuringRun,
            hadTipBounceFollowup = _hadTipBounceDuringRun,
            comboVariety = ComputeLiveComboVariety(),
            zoneId = GetCurrentZoneId()
        };

        probe.canonicalSignature = TrickActivityRules.BuildCanonicalSignature(TrickActivityRules.CreateDescriptor(probe));
        return TrickActivityRules.Score(TrickActivityRules.CreateDescriptor(probe), _scoreHistory).finalScore;
    }

    private int ComputeComboVariety(TrickResult result)
    {
        int variety = 0;

        if (result.spinDegrees > 0)
            variety++;

        if (result.flipCount > 0)
            variety++;

        if (result.usedValidPose)
            variety++;

        if (result.usedGrind)
            variety++;

        if (result.usedSlide)
            variety++;

        if (result.switchLanding || result.noseLanding || result.tailLanding)
            variety++;

        if (result.hadBounceFollowup)
            variety++;

        return Mathf.Max(1, variety);
    }

    private string GetCurrentZoneId()
    {
        if (playerMapRegionTracker == null)
            playerMapRegionTracker = GetComponent<PlayerMapRegionTracker>();

        return playerMapRegionTracker != null && !string.IsNullOrWhiteSpace(playerMapRegionTracker.CurrentRegionId)
            ? playerMapRegionTracker.CurrentRegionId.Trim()
            : string.Empty;
    }

    private static string[] ToArray(HashSet<string> values)
    {
        if (values == null || values.Count == 0)
            return Array.Empty<string>();

        string[] result = new string[values.Count];
        values.CopyTo(result);
        return result;
    }

    private static TEnum[] ToArray<TEnum>(HashSet<TEnum> values) where TEnum : struct, Enum
    {
        if (values == null || values.Count == 0)
            return Array.Empty<TEnum>();

        TEnum[] result = new TEnum[values.Count];
        values.CopyTo(result);
        return result;
    }

    private static Vector3 NormalizeSignedEuler(Vector3 euler)
    {
        return new Vector3(
            NormalizeAngle(euler.x),
            NormalizeAngle(euler.y),
            NormalizeAngle(euler.z));
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    private bool HasMeaningfulResolvedTrickBeforeBounce()
    {
        for (int i = 0; i < _segments.Count; i++)
        {
            string text = _segments[i].text;
            if (string.IsNullOrWhiteSpace(text))
                continue;

            if (IsUtilitySegment(text))
                continue;

            return true;
        }

        return false;
    }

    private bool HasAnythingWorthResolving()
    {
        if (HasMeaningfulResolvedTrickBeforeBounce())
            return true;

        if (_slideActive && _slideDistance >= minSlideDistance)
            return true;

        if (_grindTrackActive && skiController != null && skiController.CurrentGrindDistance >= minGrindDistance)
            return true;

        string building = BuildSegmentDisplayName();
        return !string.IsNullOrWhiteSpace(building);
    }

    private static bool IsUtilitySegment(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return true;

        if (text.Equals("Bounce", StringComparison.OrdinalIgnoreCase))
            return true;

        if (text.EndsWith("Landing", StringComparison.OrdinalIgnoreCase))
            return true;

        if (text.StartsWith("Noseslide", StringComparison.OrdinalIgnoreCase))
            return true;

        if (text.StartsWith("Tailslide", StringComparison.OrdinalIgnoreCase))
            return true;

        if (text.StartsWith("Grind ", StringComparison.OrdinalIgnoreCase))
            return true;

        if (text.StartsWith("Nose Grind", StringComparison.OrdinalIgnoreCase))
            return true;

        if (text.StartsWith("Tail Grind", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private void ClearRunStateWithoutResolving()
    {
        _lastGroundedComboEndTime = Time.time;

        _runActive = false;
        _runFailed = false;
        _touchPhaseActive = false;
        _bounceAddedThisTouch = false;
        _landingTagAddedThisTouch = false;
        _groundedNoTrickTimer = 0f;
        _stackedDuringRun = false;

        _currentDisplayName = string.Empty;
        _lastBroadcastDisplayName = string.Empty;
        _attemptedNameAtFailure = string.Empty;
        _lastFailureOverlayText = string.Empty;
        _wasMobilityActiveLastFrame = false;

        _segments.Clear();
        ResetCurrentSegment();
        ResetSlideTracking();
        ResetGrindTracking();
    }

    private bool HasNonTrivialPoseOrientation()
    {
        if (skiController == null || !skiController.IsAirPoseActive)
            return false;

        return skiController.CurrentPoseOrientationModifier != SkiController.AerialOrientationModifier.None;
    }

    private bool CanShowCurrentBuildingSegmentLive()
    {
        string segmentName = BuildSegmentDisplayName();
        if (string.IsNullOrWhiteSpace(segmentName))
            return false;

        float segmentAirTime = Time.time - _segmentStartTime;
        if (segmentAirTime < minSegmentAirTime)
            return false;

        int spinCount = ComputeSpinCount(_segmentYaw);
        int spinDegrees = spinCount * Mathf.Max(1, spinStepDegrees);
        int flipCount = ComputeFlipCount(_segmentPitch, out _, out _);

        bool usedStyleNow = skiController != null && skiController.IsAirStyleActive;
        bool hasRotation = spinDegrees > 0 || flipCount > 0;
        bool poseOnly = usedStyleNow && !hasRotation;

        if (!poseOnly)
            return true;

        float poseHeldTime = (_poseHeldStartTime > 0f) ? (Time.time - _poseHeldStartTime) : 0f;
        if (poseHeldTime < minPoseHoldTime)
            return false;

        if (segmentAirTime < minAirTimeForPoseOnlySegment)
            return false;

        if (requireRotationOrOrientationForPoseOnlySegment && !HasNonTrivialPoseOrientation())
            return false;

        return true;
    }
}
