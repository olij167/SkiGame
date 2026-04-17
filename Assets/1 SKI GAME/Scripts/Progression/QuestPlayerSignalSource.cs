using UnityEngine;

namespace SkiGame.Progression
{
    [DisallowMultipleComponent]
    public sealed class QuestPlayerSignalSource : MonoBehaviour
    {
        [SerializeField] private QuestSignalBus signalBus;
        [SerializeField] private SkiController skiController;
        [SerializeField] private WalkingController walkingController;
        [SerializeField] private SorenessMeter sorenessMeter;
        [SerializeField] private SkiResortHoldInteractor skiResortInteractor;
        [SerializeField] private LiftRider liftRider;
        [SerializeField] private SkiPassManager skiPassManager;

        [Header("Input Thresholds")]
        [SerializeField] private float tuckThreshold = 0.35f;
        [SerializeField] private float leanThreshold = 0.35f;
        [SerializeField] private float moveInputThreshold = 0.18f;

        [Header("Movement Detection")]
        [SerializeField] private float minimumDistanceDelta = 0.005f;
        [SerializeField] private float quickStopPrimeSpeed = 5.25f;
        [SerializeField] private float quickStopExitSpeed = 3.4f;

        private Vector3 _lastPosition;
        private bool _hasLastPosition;
        private bool _wasOnLift;
        private bool _wasInResort;
        private bool _wereSkisEquipped;
        private bool _hadDefaultPass;
        private bool _wasStacked;
        private bool _hasEverStacked;
        private bool _wasTucking;
        private bool _wasLeanForward;
        private bool _wasLeanBackward;
        private bool _wasAirPoseActive;
        private bool _wasWedging;
        private bool _wasMeaningfulMoveInput;
        private bool _jumpStarted;
        private bool _jumpAdjusted;
        private int _lastDominantSkateSide;
        private SkiController.PoleStrokePhase _lastPolePhase;
        private bool _initializedStates;

        private bool _quickStopPrimed;
        private bool _quickStopBrakeStarted;
        private float _quickStopBrakeStartSpeed;
        private float _quickStopBestSpeedDrop;
        private float _quickStopBrakeWindowTimer;

        private void Awake()
        {
            ResolveReferences();
        }

        private void Update()
        {
            ResolveReferences();
            if (signalBus == null || skiController == null)
                return;

            Vector3 normal = skiController.GroundNormal.sqrMagnitude > 0.0001f ? skiController.GroundNormal : Vector3.up;
            float planarSpeed = Vector3.ProjectOnPlane(skiController.Velocity, normal).magnitude;

            UpdateStatsAndStates(normal, planarSpeed);
            RaiseDiscreteStateEvents(planarSpeed);
            DetectInputEvents();
            DetectMovementSkillEvents(planarSpeed);
        }

        private void UpdateStatsAndStates(Vector3 normal, float planarSpeed)
        {
            signalBus.SetStat("player.speed", planarSpeed);
            signalBus.SetState("player.airborne", skiController.IsAirborne);
            signalBus.SetState("player.grounded", skiController.IsRiderGrounded);
            signalBus.SetState("player.grinding", skiController.IsGrinding);
            signalBus.SetState("player.air_pose_active", skiController.IsAirPoseActive);

            bool inResort = skiResortInteractor != null && skiResortInteractor.IsPlayerInsideResort;
            bool onLift = liftRider != null && liftRider.IsAttached;
            bool skisEquipped = walkingController != null ? walkingController.SkisOn : skiController.enabled;
            bool hasDefaultPass = skiPassManager != null && skiPassManager.HasClaimedDefaultPass;

            signalBus.SetState("player.in_resort", inResort);
            signalBus.SetState("player.on_lift", onLift);
            signalBus.SetState("player.skis_equipped", skisEquipped);
            signalBus.SetState("player.has_default_pass", hasDefaultPass);
            signalBus.SetState("player.stacked", skiController.IsStacked);
            signalBus.SetState("player.ever_stacked", _hasEverStacked || skiController.IsStacked);
            signalBus.SetStat("player.soreness", sorenessMeter != null ? sorenessMeter.Soreness01 : 0f);

            if (_hasLastPosition)
            {
                float planarDistance = Vector3.ProjectOnPlane(skiController.transform.position - _lastPosition, normal).magnitude;
                if (planarDistance >= minimumDistanceDelta)
                {
                if (skiController.IsAirborne)
                        signalBus.AddStat("player.air_distance", planarDistance);
                    else if (skiController.IsRiderGrounded)
                        signalBus.AddStat("player.ground_distance", planarDistance);
                }
            }

            _lastPosition = skiController.transform.position;
            _hasLastPosition = true;

            if (skiController.IsAirborne)
                signalBus.AddStat("player.air_time", Time.deltaTime);

            if (skiController.ForwardLeanInput >= tuckThreshold)
                signalBus.AddStat("player.tuck_time", Time.deltaTime);

            if (skiController.IsRiderGrounded &&
                planarSpeed >= 4.5f &&
                Mathf.Abs(skiController.RightLegInput - skiController.LeftLegInput) >= 0.35f)
            {
                signalBus.AddStat("player.turn_time", Time.deltaTime);
            }
        }

        private void RaiseDiscreteStateEvents(float planarSpeed)
        {
            bool onLift = liftRider != null && liftRider.IsAttached;
            bool inResort = skiResortInteractor != null && skiResortInteractor.IsPlayerInsideResort;
            bool skisEquipped = walkingController != null ? walkingController.SkisOn : skiController.enabled;
            bool hasDefaultPass = skiPassManager != null && skiPassManager.HasClaimedDefaultPass;

            if (!_initializedStates)
            {
                _wasOnLift = onLift;
                _wasInResort = inResort;
                _wereSkisEquipped = skisEquipped;
                _hadDefaultPass = hasDefaultPass;
                _wasStacked = skiController.IsStacked;
                _wasTucking = skiController.ForwardLeanInput >= tuckThreshold;
                _wasLeanForward = skiController.ForwardLeanInput >= leanThreshold;
                _wasLeanBackward = skiController.ForwardLeanInput <= -leanThreshold;
                _wasAirPoseActive = skiController.IsAirPoseActive;
                _lastPolePhase = skiController.CurrentPolePhase;
                _initializedStates = true;
                return;
            }

            if (onLift && !_wasOnLift)
            {
                signalBus.RaiseEvent(
                    "lift.mounted",
                    QuestSignalData.Create().WithTag("liftName", liftRider != null && liftRider.CurrentLiftLine != null ? liftRider.CurrentLiftLine.name : string.Empty));
            }
            else if (!onLift && _wasOnLift)
            {
                signalBus.RaiseEvent("lift.dismounted");
            }
            _wasOnLift = onLift;

            if (inResort && !_wasInResort)
                signalBus.RaiseEvent("player.entered_resort");
            else if (!inResort && _wasInResort)
                signalBus.RaiseEvent("player.exited_resort");
            _wasInResort = inResort;

            if (skisEquipped && !_wereSkisEquipped)
                signalBus.RaiseEvent("player.skis.equipped");
            else if (!skisEquipped && _wereSkisEquipped)
                signalBus.RaiseEvent("player.skis.unequipped");
            _wereSkisEquipped = skisEquipped;

            if (hasDefaultPass && !_hadDefaultPass)
                signalBus.RaiseEvent("player.default_pass.claimed");
            _hadDefaultPass = hasDefaultPass;

            bool stacked = skiController.IsStacked;
            if (stacked && !_wasStacked)
            {
                _hasEverStacked = true;
                signalBus.RaiseEvent("player.stacked");
            }
            _wasStacked = stacked;

            bool tucking = skiController.ForwardLeanInput >= tuckThreshold;
            if (tucking && !_wasTucking)
                signalBus.RaiseEvent("input.tuck.used");
            _wasTucking = tucking;

            bool leanForward = skiController.ForwardLeanInput >= leanThreshold;
            bool leanBackward = skiController.ForwardLeanInput <= -leanThreshold;

            if (leanForward && !_wasLeanForward)
                signalBus.RaiseEvent("input.lean.forward");
            if (leanBackward && !_wasLeanBackward)
                signalBus.RaiseEvent("input.lean.backward");

            _wasLeanForward = leanForward;
            _wasLeanBackward = leanBackward;

            bool airPoseActive = skiController.IsAirPoseActive;
            if (airPoseActive && !_wasAirPoseActive)
                signalBus.RaiseEvent("input.air_pose.used");
            _wasAirPoseActive = airPoseActive;

            bool meaningfulMoveInput =
                Mathf.Abs(skiController.ForwardLeanInput) >= moveInputThreshold ||
                Mathf.Abs(skiController.LeftLegInput) >= moveInputThreshold ||
                Mathf.Abs(skiController.RightLegInput) >= moveInputThreshold ||
                Mathf.Abs(skiController.RawLeanInput) >= moveInputThreshold;

            if (meaningfulMoveInput && !_wasMeaningfulMoveInput)
                signalBus.RaiseEvent("input.move.used");
            _wasMeaningfulMoveInput = meaningfulMoveInput;

            if (_lastPolePhase != skiController.CurrentPolePhase)
            {
                if (skiController.CurrentPolePhase == SkiController.PoleStrokePhase.Entry ||
                    skiController.CurrentPolePhase == SkiController.PoleStrokePhase.FollowThrough)
                {
                    signalBus.RaiseEvent("input.poles.push");
                }
                else if (skiController.CurrentPolePhase == SkiController.PoleStrokePhase.Drag)
                {
                    signalBus.RaiseEvent("input.poles.drag");
                }

                _lastPolePhase = skiController.CurrentPolePhase;
            }
        }

        private void DetectInputEvents()
        {
            bool wedging = skiController.IsRiderGrounded &&
                           skiController.LeftLegInput >= 0.35f &&
                           skiController.RightLegInput >= 0.35f;

            if (wedging && !_wasWedging)
                signalBus.RaiseEvent("movement.wedge.performed");

            _wasWedging = wedging;

            if (skiController.IsRiderGrounded)
            {
                float diff = skiController.RightLegInput - skiController.LeftLegInput;
                int dominant = Mathf.Abs(diff) >= 0.45f ? (diff > 0f ? 1 : -1) : 0;

                if (dominant != 0 && _lastDominantSkateSide != 0 && dominant != _lastDominantSkateSide)
                    signalBus.RaiseEvent("movement.skate.switch");

                if (dominant != 0)
                    _lastDominantSkateSide = dominant;
            }
        }

        private void DetectMovementSkillEvents(float planarSpeed)
        {
            if (!skiController.IsRiderGrounded)
            {
                _jumpStarted = true;

                if (Mathf.Abs(skiController.ForwardLeanInput) >= 0.2f ||
                    Mathf.Abs(skiController.LeftLegInput) >= 0.2f ||
                    Mathf.Abs(skiController.RightLegInput) >= 0.2f)
                {
                    _jumpAdjusted = true;
                }
            }
            else if (_jumpStarted)
            {
                if (_jumpAdjusted)
                    signalBus.RaiseEvent("movement.jump_adjusted");

                _jumpStarted = false;
                _jumpAdjusted = false;
            }

            if (planarSpeed >= quickStopPrimeSpeed)
                _quickStopPrimed = true;

            float turnAmount = Mathf.Abs(skiController.RightLegInput - skiController.LeftLegInput);
            float backwardLean = -skiController.ForwardLeanInput;

            bool brakingIntent =
                _quickStopPrimed &&
                skiController.IsRiderGrounded &&
                backwardLean >= 0.15f &&
                turnAmount >= 0.22f;

            if (brakingIntent && !_quickStopBrakeStarted)
            {
                _quickStopBrakeStarted = true;
                _quickStopBrakeStartSpeed = planarSpeed;
                _quickStopBrakeWindowTimer = 0f;
                _quickStopBestSpeedDrop = 0f;
            }

            if (_quickStopBrakeStarted)
            {
                _quickStopBrakeWindowTimer += Time.deltaTime;
                float speedDrop = Mathf.Max(0f, _quickStopBrakeStartSpeed - planarSpeed);
                _quickStopBestSpeedDrop = Mathf.Max(_quickStopBestSpeedDrop, speedDrop);

                bool strongStopByOutcome =
                    _quickStopBestSpeedDrop >= 1.9f &&
                    planarSpeed <= Mathf.Max(quickStopExitSpeed, _quickStopBrakeStartSpeed * 0.72f);

                bool strongStopByNearStandstill =
                    _quickStopBestSpeedDrop >= 1.25f &&
                    planarSpeed <= 2.35f;

                if (strongStopByOutcome || strongStopByNearStandstill)
                {
                    signalBus.RaiseEvent("movement.quick_stop.performed");
                    _quickStopPrimed = false;
                    _quickStopBrakeStarted = false;
                    _quickStopBestSpeedDrop = 0f;
                    _quickStopBrakeWindowTimer = 0f;
                    return;
                }

                bool lostIntentTooLong =
                    _quickStopBrakeWindowTimer > 1.35f &&
                    (!brakingIntent || planarSpeed > _quickStopBrakeStartSpeed + 0.35f);

                if (lostIntentTooLong)
                {
                    _quickStopBrakeStarted = false;
                    _quickStopBestSpeedDrop = 0f;
                    _quickStopBrakeWindowTimer = 0f;
                }
            }
        }

        private void ResolveReferences()
        {
            if (signalBus == null)
                signalBus = FindObjectOfType<QuestSignalBus>();

            if (skiController == null)
                skiController = FindObjectOfType<SkiController>();

            if (walkingController == null)
                walkingController = FindObjectOfType<WalkingController>();

            if (sorenessMeter == null && skiController != null)
                sorenessMeter = skiController.GetComponent<SorenessMeter>();

            if (skiResortInteractor == null)
                skiResortInteractor = FindObjectOfType<SkiResortHoldInteractor>();

            if (liftRider == null)
                liftRider = FindObjectOfType<LiftRider>();

            if (skiPassManager == null)
                skiPassManager = SkiPassManager.Instance != null
                    ? SkiPassManager.Instance
                    : FindObjectOfType<SkiPassManager>();
        }
    }
}
