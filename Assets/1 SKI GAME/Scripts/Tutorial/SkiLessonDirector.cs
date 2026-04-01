using SkiGame.Runs;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SkiGame.Progression
{
    [DisallowMultipleComponent]
    public sealed class SkiLessonDirector : MonoBehaviour
    {
        private enum LessonStep
        {
            Move = 0,
            Lean,
            Turn,
            Skate,
            QuickStop,
            JumpAndAir,
            Poles,
            Soreness,
            ResortRecovery,
            EquipSkis,
            HudAndTasks,
            CustomisationShop,
            SkiPassKiosk,
            RideLift,
            DismountLift,
            Complete
        }

        [Header("References")]
        [SerializeField] private PlayerStatsManager playerStatsManager;
        [SerializeField] private SkiController skiController;
        [SerializeField] private SorenessMeter sorenessMeter;
        [SerializeField] private RunProgressTracker runProgressTracker;
        [SerializeField] private SkiLessonCrashCue crashCue;
        [SerializeField] private SkiResortHoldInteractor skiResortInteractor;
        [SerializeField] private ProgressionDirector progressionDirector;
        [SerializeField] private MiniMountainHudController miniMountainHud;
        [SerializeField] private MountainHudOverlayController mountainHudOverlay;
        [SerializeField] private CustomizationPortal customizationPortal;
        [SerializeField] private WalkingController walkingController;
        [SerializeField] private LiftRider liftRider;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private SkiPassKiosk skiPassKiosk;
        [SerializeField] private SkiResortZone tutorialResortZone;
        [SerializeField] private Transform tutorialResortNavigationTarget;

        [Header("Activation")]
        [SerializeField] private bool forceRunEvenIfCompleted = false;

        [Header("Thresholds")]
        [SerializeField] private float moveDistanceRequired = 12f;
        [SerializeField] private float turnSpeedRequired = 4.5f;
        [SerializeField] private float quickStopPrimeSpeed = 5.25f;
        [SerializeField] private float quickStopExitSpeed = 3.4f;
        [SerializeField] private float sorenessRequired = 0.035f;
        [SerializeField] private float moveIntroDelaySeconds = 1.4f;
        [SerializeField] private float moveInputThreshold = 0.18f;

        private bool _active;
        private LessonStep _currentStep;

        private Vector3 _stepStartPosition;
        private float _turnAccumulatedTime;
        private int _skateAlternations;
        private int _lastDominantSkateSide;
        private bool _sawForwardLean;
        private bool _sawBackwardLean;
        private bool _quickStopPrimed;
        private float _quickStopPeakSpeed;
        private bool _quickStopBrakeStarted;
        private float _quickStopBrakeStartSpeed;
        private float _quickStopBestSpeedDrop;
        private float _quickStopBrakeWindowTimer;
        private bool _jumpStarted;
        private bool _airAdjusted;
        private bool _sawPolePush;
        private bool _sawPoleDrag;
        private bool _loggedRunAttempt;
        private int _baselineRunsCompleted;
        private int _baselineLiftsUsed;
        private float _exploreStartDistanceMeters;
        private bool _sorenessCueTriggered;

        private float _stepElapsedTime;
        private bool _sorenessImpactConfirmed;

        private bool _enteredResortAfterSoreness;
        private float _resortRecoveryStartSoreness;
        private bool _resortRecoveryExplained;

        private int _hudClaimedTierBaseline;
        private string _tutorialReadyTaskLadderId;
        private int _tutorialReadyTaskTierIndex = -1;

        private bool _shopOpened;
        private bool _shopExitedAfterOpen;

        private bool _liftRideStarted;

        private bool _equipSkisPhaseEntered;
        private bool _sawSkisUnequippedAfterResort;

        [SerializeField] private float tutorialLiftSearchRadius = 2000f;

        private LiftLine _tutorialTargetLiftLine;

        private bool _kioskOpened;
        private bool _defaultPassClaimedDuringTutorial;
        private bool _hadDefaultPassBeforeKioskStep;
        public bool IsLessonActive => _active;
        public bool CanOfferLessons => playerStatsManager != null && playerStatsManager.Profile?.tutorial != null && playerStatsManager.Profile.tutorial.CanOfferSkiLessons;
        public bool LessonsCompleted => playerStatsManager != null && playerStatsManager.Profile?.tutorial != null && playerStatsManager.Profile.tutorial.skiLessonsCompleted;
        public int CurrentStepIndex => (int)_currentStep;
        public int TotalStepCount => (int)LessonStep.DismountLift + 1;
        public string CurrentStepTitle => GetStepTitle(_currentStep);
        public string CurrentStepBody => GetStepBody(_currentStep);
        public string CurrentStepHint => GetStepHint(_currentStep);
        public float CurrentStepProgress01 => GetCurrentStepProgress01();
        public string CurrentStepProgressText => GetCurrentStepProgressText();

        public Transform CurrentObjectiveTarget
        {
            get
            {
                switch (_currentStep)
                {
                    case LessonStep.ResortRecovery:
                        if (!IsPlayerInResort())
                        {
                            if (tutorialResortNavigationTarget != null)
                                return tutorialResortNavigationTarget;

                            var zone = ResolveTutorialResortZone();
                            if (zone != null)
                            {
                                if (zone.entrancePoint != null)
                                    return zone.entrancePoint;

                                return zone.transform;
                            }
                        }
                        break;

                    case LessonStep.CustomisationShop:
                        if (!_shopOpened && customizationPortal != null)
                            return customizationPortal.transform;
                        break;

                    case LessonStep.SkiPassKiosk:
                        if (skiPassKiosk != null && !_defaultPassClaimedDuringTutorial)
                            return skiPassKiosk.transform;
                        break;

                    case LessonStep.RideLift:
                        {
                            var lift = ResolveTutorialTargetLiftLine();
                            if (lift != null)
                            {
                                if (lift.bottomStation != null)
                                    return lift.bottomStation;

                                return lift.transform;
                            }
                            break;
                        }

                    case LessonStep.DismountLift:
                        {
                            var lift = liftRider != null && liftRider.CurrentLiftLine != null
                                ? liftRider.CurrentLiftLine
                                : ResolveTutorialTargetLiftLine();

                            if (lift != null)
                            {
                                if (lift.topStation != null)
                                    return lift.topStation;

                                return lift.transform;
                            }
                            break;
                        }
                }

                return null;
            }
        }

        public string CurrentObjectiveLabel
        {
            get
            {
                switch (_currentStep)
                {
                    case LessonStep.ResortRecovery:
                        return !IsPlayerInResort() ? "Go to Resort" : string.Empty;

                    case LessonStep.CustomisationShop:
                        return !_shopOpened ? "Go to Customisation Shop" : string.Empty;

                    case LessonStep.SkiPassKiosk:
                        return !_defaultPassClaimedDuringTutorial ? "Go to Ski Pass Kiosk" : string.Empty;

                    case LessonStep.RideLift:
                        return "Go to Ski Lift";

                    case LessonStep.DismountLift:
                        return "Dismount at Top Station";
                }

                return string.Empty;
            }
        }

        public bool CurrentObjectiveClearOnReach
        {
            get
            {
                switch (_currentStep)
                {
                    case LessonStep.ResortRecovery:
                        return false;

                    default:
                        return true;
                }
            }
        }

        public float CurrentObjectiveArriveDistance
        {
            get
            {
                switch (_currentStep)
                {
                    case LessonStep.ResortRecovery:
                        return 7f;

                    case LessonStep.SkiPassKiosk:
                        return 7f;

                    case LessonStep.CustomisationShop:
                        return 8f;

                    case LessonStep.RideLift:
                    case LessonStep.DismountLift:
                        return 10f;

                    default:
                        return 10f;
                }
            }
        }

        private void Start()
        {
            ResolveReferences();

            if (playerStatsManager == null)
                return;

            if (playerStatsManager.Profile == null)
                playerStatsManager.Load();

            if (playerStatsManager.Profile == null)
                return;

            var tutorial = playerStatsManager.Profile.tutorial;
            if (tutorial == null)
                return;

            bool shouldRun = forceRunEvenIfCompleted || tutorial.IsSkiLessonsActive;
            if (!shouldRun)
                return;

            BeginLessons();
        }

        private void OnEnable()
        {
            if (runProgressTracker != null)
                runProgressTracker.OnAttemptLogged += HandleAttemptLogged;
        }

        private void OnDisable()
        {
            if (runProgressTracker != null)
                runProgressTracker.OnAttemptLogged -= HandleAttemptLogged;
        }

        private void Update()
        {
            if (!_active || skiController == null || playerStatsManager == null || playerStatsManager.Profile == null)
                return;

            EvaluateCurrentStep();
        }

        private void ResolveReferences()
        {
            if (playerStatsManager == null)
                playerStatsManager = PlayerStatsManager.Instance != null
                    ? PlayerStatsManager.Instance
                    : FindObjectOfType<PlayerStatsManager>();

            if (skiController == null)
                skiController = FindObjectOfType<SkiController>();

            if (sorenessMeter == null && skiController != null)
                sorenessMeter = skiController.GetComponent<SorenessMeter>();

            if (runProgressTracker == null)
                runProgressTracker = FindObjectOfType<RunProgressTracker>();

            if (crashCue == null)
                crashCue = FindObjectOfType<SkiLessonCrashCue>();

            if (skiResortInteractor == null)
                skiResortInteractor = FindObjectOfType<SkiResortHoldInteractor>();

            if (walkingController == null)
                walkingController = FindObjectOfType<WalkingController>();

            if (progressionDirector == null)
                progressionDirector = ProgressionDirector.Instance != null
                    ? ProgressionDirector.Instance
                    : FindObjectOfType<ProgressionDirector>();

            if (customizationPortal == null)
                customizationPortal = FindObjectOfType<CustomizationPortal>();

            if (liftRider == null)
                liftRider = FindObjectOfType<LiftRider>();

            if (skiPassKiosk == null)
                skiPassKiosk = FindObjectOfType<SkiPassKiosk>();

            if (tutorialResortZone == null)
                tutorialResortZone = FindObjectOfType<SkiResortZone>();

            if (tutorialResortNavigationTarget == null && tutorialResortZone != null)
                tutorialResortNavigationTarget = tutorialResortZone.entrancePoint != null
                    ? tutorialResortZone.entrancePoint
                    : tutorialResortZone.transform;
        }

        private LiftLine ResolveTutorialTargetLiftLine()
        {
            if (_tutorialTargetLiftLine != null)
                return _tutorialTargetLiftLine;

            _tutorialTargetLiftLine = FindNearestTutorialLiftLine();
            return _tutorialTargetLiftLine;
        }

        private LiftLine FindNearestTutorialLiftLine()
        {
            if (skiController == null)
                return null;

            Vector3 playerPos = skiController.transform.position;
            float maxSqr = tutorialLiftSearchRadius * tutorialLiftSearchRadius;

            LiftLine best = null;
            float bestSqr = maxSqr;

#if UNITY_2023_1_OR_NEWER
            var lifts = FindObjectsByType<LiftLine>(FindObjectsSortMode.None);
#else
    var lifts = FindObjectsOfType<LiftLine>();
#endif

            for (int i = 0; i < lifts.Length; i++)
            {
                var lift = lifts[i];
                if (lift == null)
                    continue;

                Vector3 anchor =
                    lift.bottomStation != null ? lift.bottomStation.position :
                    lift.transform.position;

                float d2 = (anchor - playerPos).sqrMagnitude;
                if (d2 < bestSqr)
                {
                    bestSqr = d2;
                    best = lift;
                }
            }

            return best;
        }

        public void BeginLessons()
        {
            if (playerStatsManager?.Profile?.tutorial == null)
                return;

            playerStatsManager.Profile.tutorial.AcceptLessons();

            int rawIndex = playerStatsManager.Profile.tutorial.skiLessonStepIndex;
            _currentStep = (LessonStep)Mathf.Clamp(rawIndex, 0, (int)LessonStep.DismountLift);

            var profile = playerStatsManager.Profile;
            _baselineRunsCompleted = profile.session != null ? profile.session.runsCompleted : 0;
            _baselineLiftsUsed = profile.session != null ? profile.session.liftsUsed : 0;

            ResetPerStepState();
            _active = true;

            playerStatsManager.Save();
            HandleStepEntered();
        }

        public void DeferLessons()
        {
            if (playerStatsManager == null)
                return;

            if (playerStatsManager.Profile == null)
                playerStatsManager.Load();

            if (playerStatsManager.Profile?.tutorial == null)
                return;

            playerStatsManager.Profile.tutorial.DeferLessons();
            playerStatsManager.Save();
            _active = false;
        }

        private void ResetPerStepState()
        {
            _stepStartPosition = skiController != null ? skiController.transform.position : Vector3.zero;
            _turnAccumulatedTime = 0f;
            _skateAlternations = 0;
            _lastDominantSkateSide = 0;
            _sawForwardLean = false;
            _sawBackwardLean = false;
            _quickStopPrimed = false;
            _quickStopPeakSpeed = 0f;
            _quickStopBrakeStarted = false;
            _quickStopBrakeStartSpeed = 0f;
            _quickStopBestSpeedDrop = 0f;
            _quickStopBrakeWindowTimer = 0f;
            _jumpStarted = false;
            _airAdjusted = false;
            _sawPolePush = false;
            _sawPoleDrag = false;
            _sorenessCueTriggered = false;

            _stepElapsedTime = 0f;
            _sorenessImpactConfirmed = false;

            _enteredResortAfterSoreness = false;
            _resortRecoveryStartSoreness = 0f;
            _resortRecoveryExplained = false;

            _tutorialReadyTaskLadderId = null;
            _tutorialReadyTaskTierIndex = -1;

            _shopOpened = false;
            _shopExitedAfterOpen = false;

            _liftRideStarted = false;

            _equipSkisPhaseEntered = false;
            _sawSkisUnequippedAfterResort = false;

            _tutorialTargetLiftLine = null;

            _kioskOpened = false;
            _defaultPassClaimedDuringTutorial = false;
            _hadDefaultPassBeforeKioskStep = false;
        }

        private void HandleStepEntered()
        {
            if (_currentStep == LessonStep.Soreness)
            {
                _sorenessCueTriggered = crashCue != null && crashCue.TryTriggerCue();
            }

            if (_currentStep == LessonStep.ResortRecovery)
            {
                _resortRecoveryStartSoreness = sorenessMeter != null ? sorenessMeter.Soreness01 : 0f;
                _resortRecoveryExplained = true;
            }

            if (_currentStep == LessonStep.EquipSkis)
            {
                _equipSkisPhaseEntered = true;
                _sawSkisUnequippedAfterResort = false;
            }

            if (_currentStep == LessonStep.HudAndTasks)
            {
                if (progressionDirector != null)
                {
                    progressionDirector.EnsureAtLeastOneClaimableDailyTier(out _tutorialReadyTaskLadderId, out _tutorialReadyTaskTierIndex);
                    _hudClaimedTierBaseline = progressionDirector.CountClaimedDailyTiers();
                }

                if (mountainHudOverlay != null)
                    mountainHudOverlay.ResetTutorialVisitedSections();
            }

            if (_currentStep == LessonStep.CustomisationShop)
            {
                _shopOpened = false;
                _shopExitedAfterOpen = false;
            }

            if (_currentStep == LessonStep.SkiPassKiosk)
            {
                _kioskOpened = false;

                var passMgr = SkiPassManager.Instance != null ? SkiPassManager.Instance : FindObjectOfType<SkiPassManager>();
                _hadDefaultPassBeforeKioskStep = passMgr != null && passMgr.HasClaimedDefaultPass;

                // Do not auto-complete the step just because the player already has the default pass.
                // If they already have it, opening the kiosk is enough to teach the interaction.
                _defaultPassClaimedDuringTutorial = false;
            }

            if (_currentStep == LessonStep.RideLift)
            {
                _liftRideStarted = liftRider != null && liftRider.IsAttached;
                _tutorialTargetLiftLine = ResolveTutorialTargetLiftLine();
            }

            if (_currentStep == LessonStep.DismountLift)
            {
                _liftRideStarted = true;
                _tutorialTargetLiftLine = liftRider != null && liftRider.CurrentLiftLine != null
                    ? liftRider.CurrentLiftLine
                    : ResolveTutorialTargetLiftLine();
            }
        }

        private void EvaluateCurrentStep()
        {
            _stepElapsedTime += Time.deltaTime;

            float speed = GetPlanarSpeed();

            switch (_currentStep)
            {
                case LessonStep.Move:
                    {
                        float dist = Vector3.Distance(skiController.transform.position, _stepStartPosition);

                        bool enoughReadTime = _stepElapsedTime >= 1.4f;
                        bool meaningfulInput = HasMeaningfulMoveInput();
                        bool movedEnough = dist >= moveDistanceRequired;

                        if (enoughReadTime && meaningfulInput && movedEnough)
                            AdvanceStep();

                        break;
                    }

                case LessonStep.Lean:
                    {
                        if (skiController.ForwardLeanInput >= 0.35f)
                            _sawForwardLean = true;

                        if (skiController.ForwardLeanInput <= -0.35f)
                            _sawBackwardLean = true;

                        if (_sawForwardLean && _sawBackwardLean)
                            AdvanceStep();

                        break;
                    }

                case LessonStep.Turn:
                    {
                        if (skiController.IsRiderGrounded &&
                            speed >= turnSpeedRequired &&
                            Mathf.Abs(skiController.RightLegInput - skiController.LeftLegInput) >= 0.35f)
                        {
                            _turnAccumulatedTime += Time.deltaTime;
                        }

                        if (_turnAccumulatedTime >= 1.0f)
                            AdvanceStep();

                        break;
                    }

                case LessonStep.Skate:
                    {
                        if (skiController.IsRiderGrounded)
                        {
                            float diff = skiController.RightLegInput - skiController.LeftLegInput;
                            int dominant = Mathf.Abs(diff) >= 0.45f ? (diff > 0f ? 1 : -1) : 0;

                            if (dominant != 0 && _lastDominantSkateSide != 0 && dominant != _lastDominantSkateSide)
                                _skateAlternations++;

                            if (dominant != 0)
                                _lastDominantSkateSide = dominant;
                        }

                        if (_skateAlternations >= 4)
                            AdvanceStep();

                        break;
                    }

                case LessonStep.QuickStop:
                    {
                        if (speed >= quickStopPrimeSpeed)
                        {
                            _quickStopPrimed = true;
                            _quickStopPeakSpeed = Mathf.Max(_quickStopPeakSpeed, speed);
                        }

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
                            _quickStopBrakeStartSpeed = speed;
                            _quickStopBrakeWindowTimer = 0f;
                            _quickStopBestSpeedDrop = 0f;
                        }

                        if (_quickStopBrakeStarted)
                        {
                            _quickStopBrakeWindowTimer += Time.deltaTime;

                            float speedDrop = Mathf.Max(0f, _quickStopBrakeStartSpeed - speed);
                            _quickStopBestSpeedDrop = Mathf.Max(_quickStopBestSpeedDrop, speedDrop);

                            bool strongStopByOutcome =
                                _quickStopBestSpeedDrop >= 1.9f &&
                                speed <= Mathf.Max(quickStopExitSpeed, _quickStopBrakeStartSpeed * 0.72f);

                            bool strongStopByNearStandstill =
                                _quickStopBestSpeedDrop >= 1.25f &&
                                speed <= 2.35f;

                            if (strongStopByOutcome || strongStopByNearStandstill)
                            {
                                AdvanceStep();
                                break;
                            }

                            bool lostIntentTooLong =
                                _quickStopBrakeWindowTimer > 1.35f &&
                                (!brakingIntent || speed > _quickStopBrakeStartSpeed + 0.35f);

                            if (lostIntentTooLong)
                            {
                                _quickStopBrakeStarted = false;
                                _quickStopBrakeStartSpeed = 0f;
                                _quickStopBrakeWindowTimer = 0f;
                                _quickStopBestSpeedDrop = 0f;
                            }
                        }

                        break;
                    }

                case LessonStep.JumpAndAir:
                    {
                        if (!skiController.IsRiderGrounded)
                        {
                            _jumpStarted = true;

                            if (Mathf.Abs(skiController.ForwardLeanInput) >= 0.2f ||
                                Mathf.Abs(skiController.LeftLegInput) >= 0.2f ||
                                Mathf.Abs(skiController.RightLegInput) >= 0.2f)
                            {
                                _airAdjusted = true;
                            }
                        }

                        if (_jumpStarted && _airAdjusted && skiController.IsRiderGrounded)
                            AdvanceStep();

                        break;
                    }

                case LessonStep.Poles:
                    {
                        if (IsPolePushActive())
                            _sawPolePush = true;

                        if (IsPoleDragActive())
                            _sawPoleDrag = true;

                        if (_sawPolePush && _sawPoleDrag)
                            AdvanceStep();

                        break;
                    }

                case LessonStep.Soreness:
                    {
                        if (WasHitForSoreness())
                            AdvanceStep();

                        break;
                    }

                case LessonStep.ResortRecovery:
                    {
                        bool inResort = IsPlayerInResort();
                        float currentSoreness = sorenessMeter != null ? sorenessMeter.Soreness01 : 0f;

                        if (inResort)
                            _enteredResortAfterSoreness = true;

                        bool sorenessReduced = currentSoreness < _resortRecoveryStartSoreness - 0.01f;
                        bool fullyRecovered = currentSoreness <= 0.001f;

                        if (!_enteredResortAfterSoreness)
                            break;

                        if (!inResort && (_resortRecoveryExplained || sorenessReduced || fullyRecovered))
                            AdvanceStep();

                        break;
                    }

                case LessonStep.EquipSkis:
                    {
                        if (AreSkisEquippedNow())
                            AdvanceStep();

                        break;
                    }

                case LessonStep.HudAndTasks:
                    {
                        bool overlayOpen = mountainHudOverlay != null && mountainHudOverlay.IsOpen;
                        bool visitedStats = mountainHudOverlay != null && mountainHudOverlay.TutorialVisitedStats;
                        bool visitedMap = mountainHudOverlay != null && mountainHudOverlay.TutorialVisitedMap;
                        bool claimedTask = progressionDirector != null && progressionDirector.CountClaimedDailyTiers() > _hudClaimedTierBaseline;

                        // Tasks is open by default, so do not require a tasks-button click.
                        if (overlayOpen && visitedStats && visitedMap && claimedTask)
                            AdvanceStep();

                        break;
                    }

                case LessonStep.CustomisationShop:
                    {
                        if (CustomizationShopRuntime.IsOpen)
                        {
                            _shopOpened = true;
                        }
                        else if (_shopOpened)
                        {
                            _shopExitedAfterOpen = true;
                        }

                        // Completing the step now only requires visiting the shop and exiting it.
                        if (_shopOpened && _shopExitedAfterOpen)
                            AdvanceStep();

                        break;
                    }

                case LessonStep.SkiPassKiosk:
                    {
                        var passMgr = SkiPassManager.Instance != null ? SkiPassManager.Instance : FindObjectOfType<SkiPassManager>();

                        if (skiPassKiosk != null)
                        {
                            var kioskUi = skiPassKiosk.GetComponentInChildren<SkiPassKioskUI>(true);
                            if (kioskUi != null && kioskUi.IsOpen)
                                _kioskOpened = true;
                        }

                        bool hasDefaultPassNow = passMgr != null && passMgr.HasClaimedDefaultPass;

                        if (!_hadDefaultPassBeforeKioskStep && hasDefaultPassNow)
                            _defaultPassClaimedDuringTutorial = true;

                        bool stepComplete =
                            _defaultPassClaimedDuringTutorial ||
                            (_hadDefaultPassBeforeKioskStep && _kioskOpened);

                        if (stepComplete)
                            AdvanceStep();

                        break;
                    }

                case LessonStep.RideLift:
                    {
                        if (liftRider != null && liftRider.IsAttached)
                        {
                            _liftRideStarted = true;
                            if (liftRider.CurrentLiftLine != null)
                                _tutorialTargetLiftLine = liftRider.CurrentLiftLine;

                            AdvanceStep();
                        }

                        break;
                    }

                case LessonStep.DismountLift:
                    {
                        if (liftRider != null && liftRider.IsAttached)
                            _liftRideStarted = true;

                        if (_liftRideStarted && liftRider != null && !liftRider.IsAttached)
                            CompleteLessons();

                        break;
                    }
            }
        }

        private bool IsPolePushActive()
        {
            if (skiController == null)
                return false;

            return
                skiController.CurrentPolePhase == SkiController.PoleStrokePhase.Entry ||
                skiController.CurrentPolePhase == SkiController.PoleStrokePhase.FollowThrough;
        }

        private bool IsPoleDragActive()
        {
            if (skiController == null)
                return false;

            return skiController.CurrentPolePhase == SkiController.PoleStrokePhase.Drag;
        }

        private float GetPlanarSpeed()
        {
            if (skiController == null)
                return 0f;

            Vector3 n = skiController.GroundNormal.sqrMagnitude > 0.0001f ? skiController.GroundNormal : Vector3.up;
            return Vector3.ProjectOnPlane(skiController.Velocity, n).magnitude;
        }

        private bool HasMeaningfulMoveInput()
        {
            if (skiController == null)
                return false;

            return
                Mathf.Abs(skiController.ForwardLeanInput) >= 0.18f ||
                Mathf.Abs(skiController.LeftLegInput) >= 0.18f ||
                Mathf.Abs(skiController.RightLegInput) >= 0.18f ||
                Mathf.Abs(skiController.RawLeanInput) >= 0.18f;
        }

        private string GetBindingDisplay(string actionName, params string[] compositePartNames)
        {
            var action = FindActionByFriendlyName(actionName);
            if (action == null)
                return "-";

            int bindingIndex = (compositePartNames != null && compositePartNames.Length > 0)
                ? FindCompositePartBindingIndex(action, compositePartNames)
                : FindPrimaryBindingIndex(action);

            return action.GetBindingDisplayString(bindingIndex, InputBinding.DisplayStringOptions.DontUseShortDisplayNames);
        }

        private string CombineBindings(params string[] actionNames)
        {
            if (actionNames == null || actionNames.Length == 0)
                return "-";

            var sb = new System.Text.StringBuilder();

            for (int i = 0; i < actionNames.Length; i++)
            {
                if (i > 0)
                    sb.Append(" / ");

                sb.Append(GetBindingDisplay(actionNames[i]));
            }

            return sb.ToString();
        }

        private InputAction FindActionByFriendlyName(string actionName)
        {
            if (inputActions == null || string.IsNullOrWhiteSpace(actionName))
                return null;

            string normalizedTarget = Normalize(actionName);

            foreach (var map in inputActions.actionMaps)
            {
                for (int i = 0; i < map.actions.Count; i++)
                {
                    var action = map.actions[i];
                    if (Normalize(action.name) == normalizedTarget)
                        return action;
                }
            }

            return null;
        }

        private static string Normalize(string value)
        {
            return value.Replace(" ", "").Replace("_", "").ToLowerInvariant();
        }

        private static int FindPrimaryBindingIndex(InputAction action)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var b = action.bindings[i];
                if (b.isComposite || b.isPartOfComposite) continue;
                if (!string.IsNullOrEmpty(b.path)) return i;
            }

            return 0;
        }

        private static int FindCompositePartBindingIndex(InputAction action, params string[] partNames)
        {
            if (action == null)
                return 0;

            for (int i = 0; i < action.bindings.Count; i++)
            {
                var b = action.bindings[i];
                if (!b.isPartOfComposite) continue;

                for (int p = 0; p < partNames.Length; p++)
                {
                    if (string.Equals(b.name, partNames[p], System.StringComparison.OrdinalIgnoreCase))
                        return i;
                }
            }

            return FindPrimaryBindingIndex(action);
        }

        private bool WasHitForSoreness()
        {
            if (sorenessMeter == null)
                return false;

            if (_sorenessImpactConfirmed)
                return true;

            if (sorenessMeter.Soreness01 >= sorenessRequired)
            {
                _sorenessImpactConfirmed = true;
                return true;
            }

            return false;
        }

        private bool IsPlayerInResort()
        {
            return skiResortInteractor != null && skiResortInteractor.IsPlayerInsideResort;
        }

        private int GetUnlockedCustomizationCount()
        {
            return playerStatsManager?.Profile?.customization?.unlockedCustomizationIds != null
                ? playerStatsManager.Profile.customization.unlockedCustomizationIds.Count
                : 0;
        }

        private string GetEquippedCustomizationSignature()
        {
            var c = playerStatsManager?.Profile?.customization;
            if (c == null) return string.Empty;

            return $"{c.equippedEyeIconId}|{c.equippedSkisId}|{c.equippedPolesId}|{c.equippedHatId}|{c.equippedJacketId}|{c.equippedSkisPatternId}|{c.equippedPolesPatternId}|{c.equippedHatPatternId}|{c.equippedJacketPatternId}";
        }

        private bool AreSkisEquippedNow()
        {
            if (walkingController != null)
                return walkingController.SkisOn;

            if (skiController != null)
                return skiController.enabled && skiController.gameObject.activeInHierarchy;

            return false;
        }

        private bool AreSkisUnequippedNow()
        {
            return !AreSkisEquippedNow();
        }

        private void HandleAttemptLogged(RunProgressTracker.AttemptLoggedInfo info)
        {
            _loggedRunAttempt = true;
        }

        private void AdvanceStep()
        {
            _currentStep++;

            if (_currentStep >= LessonStep.Complete)
            {
                CompleteLessons();
                return;
            }

            if (playerStatsManager != null && playerStatsManager.Profile != null && playerStatsManager.Profile.tutorial != null)
            {
                playerStatsManager.Profile.tutorial.skiLessonStepIndex = (int)_currentStep;
                playerStatsManager.Save();
            }

            ResetPerStepState();
            HandleStepEntered();
        }

        public void CompleteLessons()
        {
            _active = false;

            if (playerStatsManager != null && playerStatsManager.Profile != null && playerStatsManager.Profile.tutorial != null)
            {
                playerStatsManager.Profile.tutorial.MarkCompleted();
                playerStatsManager.Save();
            }
        }

        private SkiResortZone ResolveTutorialResortZone()
        {
            if (tutorialResortZone != null)
                return tutorialResortZone;

            tutorialResortZone = FindObjectOfType<SkiResortZone>();
            return tutorialResortZone;
        }

        public void SkipLessons()
        {
            DeferLessons();
        }

        public void RestartLessons()
        {
            if (playerStatsManager == null)
                return;

            if (playerStatsManager.Profile == null)
                playerStatsManager.Load();

            if (playerStatsManager.Profile?.tutorial == null)
                return;

            playerStatsManager.Profile.tutorial.ResetForNewGame();
            playerStatsManager.Profile.tutorial.AcceptLessons();
            playerStatsManager.Profile.tutorial.skiLessonStepIndex = 0;
            playerStatsManager.Save();

            BeginLessons();
        }

        private float GetCurrentStepProgress01()
        {
            switch (_currentStep)
            {
                case LessonStep.Move:
                    return Mathf.Clamp01(Vector3.Distance(skiController.transform.position, _stepStartPosition) / Mathf.Max(0.01f, moveDistanceRequired));

                case LessonStep.Lean:
                    return ((_sawForwardLean ? 1f : 0f) + (_sawBackwardLean ? 1f : 0f)) / 2f;

                case LessonStep.Turn:
                    return Mathf.Clamp01(_turnAccumulatedTime / 1.0f);

                case LessonStep.Skate:
                    return Mathf.Clamp01(_skateAlternations / 4f);

                case LessonStep.QuickStop:
                    {
                        if (!_quickStopPrimed)
                            return Mathf.Clamp01(GetPlanarSpeed() / Mathf.Max(0.01f, quickStopPrimeSpeed));

                        if (!_quickStopBrakeStarted)
                            return 0.5f;

                        return Mathf.Clamp01(_quickStopBestSpeedDrop / 1.9f);
                    }

                case LessonStep.JumpAndAir:
                    {
                        float p = 0f;
                        if (_jumpStarted) p += 0.5f;
                        if (_airAdjusted) p += 0.5f;
                        return p;
                    }

                case LessonStep.Poles:
                    return ((_sawPolePush ? 1f : 0f) + (_sawPoleDrag ? 1f : 0f)) / 2f;

                case LessonStep.Soreness:
                    return WasHitForSoreness() ? 1f : Mathf.Clamp01((sorenessMeter != null ? sorenessMeter.Soreness01 : 0f) / Mathf.Max(0.001f, sorenessRequired));

                case LessonStep.ResortRecovery:
                    {
                        if (!IsPlayerInResort())
                            return _enteredResortAfterSoreness ? 1f : 0f;

                        float current = sorenessMeter != null ? sorenessMeter.Soreness01 : 0f;
                        float recovered = Mathf.Max(0f, _resortRecoveryStartSoreness - current);
                        float target = Mathf.Max(0.02f, _resortRecoveryStartSoreness);
                        return Mathf.Clamp01(recovered / target);
                    }

                case LessonStep.EquipSkis:
                    return AreSkisEquippedNow() ? 1f : 0f;

                case LessonStep.HudAndTasks:
                    {
                        float p = 0f;
                        if (mountainHudOverlay != null && mountainHudOverlay.TutorialVisitedStats) p += 0.34f;
                        if (mountainHudOverlay != null && mountainHudOverlay.TutorialVisitedMap) p += 0.33f;
                        if (progressionDirector != null && progressionDirector.CountClaimedDailyTiers() > _hudClaimedTierBaseline) p += 0.33f;
                        return p;
                    }

                case LessonStep.CustomisationShop:
                    {
                        float p = 0f;
                        if (_shopOpened) p += 0.5f;
                        if (_shopExitedAfterOpen) p += 0.5f;
                        return p;
                    }

                case LessonStep.SkiPassKiosk:
                    return _defaultPassClaimedDuringTutorial ? 1f : (_kioskOpened ? 0.5f : 0f);

                case LessonStep.RideLift:
                    return (liftRider != null && liftRider.IsAttached) ? 1f : 0f;

                case LessonStep.DismountLift:
                    {
                        if (!_liftRideStarted)
                            return 0f;

                        return (liftRider != null && !liftRider.IsAttached) ? 1f : 0.5f;
                    }
            }

            return 0f;
        }

        private string GetCurrentStepProgressText()
        {
            switch (_currentStep)
            {
                case LessonStep.Move:
                    return $"{Vector3.Distance(skiController.transform.position, _stepStartPosition):0}/{moveDistanceRequired:0} m";

                case LessonStep.Lean:
                    return $"{(_sawForwardLean ? 1 : 0) + (_sawBackwardLean ? 1 : 0)}/2";

                case LessonStep.Turn:
                    return $"{_turnAccumulatedTime:0.0}/1.0 s";

                case LessonStep.Skate:
                    return $"{_skateAlternations}/4 pushes";

                case LessonStep.QuickStop:
                    {
                        if (!_quickStopPrimed)
                            return $"Build to {quickStopPrimeSpeed:0.0} m/s";

                        if (!_quickStopBrakeStarted)
                            return "Lean back and turn sharply";

                        return $"Speed drop: {_quickStopBestSpeedDrop:0.0} m/s";
                    }

                case LessonStep.JumpAndAir:
                    return _airAdjusted ? "Adjust mid-air and land" : (_jumpStarted ? "Control yourself in air" : "Take off");

                case LessonStep.Poles:
                    return $"{(_sawPolePush ? 1 : 0) + (_sawPoleDrag ? 1 : 0)}/2";

                case LessonStep.Soreness:
                    return _sorenessImpactConfirmed ? "Impact confirmed" : "Build soreness";

                case LessonStep.ResortRecovery:
                    if (IsPlayerInResort())
                    {
                        float current = sorenessMeter != null ? sorenessMeter.Soreness01 : 0f;
                        return $"Soreness: {current:0.00}";
                    }
                    return "Leave the resort when ready";

                case LessonStep.EquipSkis:
                    return AreSkisEquippedNow() ? "Skis equipped" : "Put your skis on";

                case LessonStep.HudAndTasks:
                    {
                        int count = 0;
                        if (mountainHudOverlay != null && mountainHudOverlay.TutorialVisitedStats) count++;
                        if (mountainHudOverlay != null && mountainHudOverlay.TutorialVisitedMap) count++;
                        if (progressionDirector != null && progressionDirector.CountClaimedDailyTiers() > _hudClaimedTierBaseline) count++;
                        return $"{count}/3";
                    }

                case LessonStep.CustomisationShop:
                    {
                        int count = 0;
                        if (_shopOpened) count++;
                        if (_shopExitedAfterOpen) count++;
                        return $"{count}/2";
                    }

                case LessonStep.SkiPassKiosk:
                    {
                        if (_defaultPassClaimedDuringTutorial)
                            return "Pass unlocked";

                        return _kioskOpened ? "Claim the free pass" : "Visit kiosk";
                    }

                case LessonStep.RideLift:
                    return (liftRider != null && liftRider.IsAttached) ? "Lift boarded" : "Board a lift";

                case LessonStep.DismountLift:
                    return (liftRider != null && !liftRider.IsAttached) ? "Lift dismounted" : "Dismount the lift";
            }

            return string.Empty;
        }

        private string GetStepTitle(LessonStep step)
        {
            switch (step)
            {
                case LessonStep.Move: return "Get Moving";
                case LessonStep.Lean: return "Lean";
                case LessonStep.Turn: return "Turn";
                case LessonStep.Skate: return "Skate";
                case LessonStep.QuickStop: return "Quick Stop";
                case LessonStep.JumpAndAir: return "Jump";
                case LessonStep.Poles: return "Use Poles";
                case LessonStep.Soreness: return "Soreness";
                case LessonStep.ResortRecovery: return "Recover in the Resort";
                case LessonStep.EquipSkis: return "Put Your Skis On";
                case LessonStep.HudAndTasks: return "Mini HUD and Overlay";
                case LessonStep.CustomisationShop: return "Customisation Shop";
                case LessonStep.SkiPassKiosk: return "Get Your Ski Pass";
                case LessonStep.RideLift: return "Ride the Lift";
                case LessonStep.DismountLift: return "Dismount";
                default: return "Ski Lessons";
            }
        }

        private string GetStepBody(LessonStep step)
        {
            switch (step)
            {
                case LessonStep.Move:
                    return $"Press {GetBindingDisplay("Lean", "positive", "up", "forward")} and use {CombineBindings("LeftSki", "RightSki")} to move away from the start.";

                case LessonStep.Lean:
                    return $"Press {GetBindingDisplay("Lean", "positive", "up", "forward")} to gain speed and {GetBindingDisplay("Lean", "negative", "down", "back", "backward")} to slow down.";

                case LessonStep.Turn:
                    return $"Use {GetBindingDisplay("LeftSki")} and {GetBindingDisplay("RightSki")} to turn and carve.";

                case LessonStep.Skate:
                    return $"Alternate {GetBindingDisplay("LeftSki")} and {GetBindingDisplay("RightSki")} to skate forward.";

                case LessonStep.QuickStop:
                    return $"Build some speed, then press {GetBindingDisplay("Lean", "negative", "down", "back", "backward")} and turn with {CombineBindings("LeftSki", "RightSki")} to stop quickly.";

                case LessonStep.JumpAndAir:
                    return $"Press and hold {GetBindingDisplay("Jump")} to jump. Use {GetBindingDisplay("Lean")} and {CombineBindings("LeftSki", "RightSki")} in the air to control yourself.";

                case LessonStep.Poles:
                    return $"Press {GetBindingDisplay("Poles")} to push on flats and drag them to control speed.";

                case LessonStep.Soreness:
                    return _sorenessImpactConfirmed
                        ? "Soreness has built up from the crash."
                        : "Crashes and hard efforts build soreness.";

                case LessonStep.ResortRecovery:
                    return $"Go to the ski resort and use {GetBindingDisplay("Interact")} to recover. Soreness heals inside and time moves forward while you rest.";

                case LessonStep.EquipSkis:
                    return $"Press {GetBindingDisplay("EquipSkis")} to put your skis back on before heading out.";

                case LessonStep.HudAndTasks:
                    return "The Mini HUD shows live mountain info. Open the Mountain Overlay, check Stats, view the Map, and claim the ready task.";

                case LessonStep.CustomisationShop:
                    return $"Go to the customisation shop and use {GetBindingDisplay("Interact")} to enter. You can leave again whenever you are ready.";

                case LessonStep.SkiPassKiosk:
                    return $"Go to the ski pass kiosk and use {GetBindingDisplay("Interact")} to open it. Claim the default pass for free so you can use the lifts.";

                case LessonStep.RideLift:
                    return $"Head to the ski lift and press {GetBindingDisplay("Interact")} to board it.";

                case LessonStep.DismountLift:
                    return $"At the top station, press {GetBindingDisplay("Interact")} to get off the lift and finish the tutorial.";
            }

            return string.Empty;
        }

        private string GetStepHint(LessonStep step)
        {
            switch (step)
            {
                case LessonStep.Move:
                    return $"Use {GetBindingDisplay("Lean", "positive", "up", "forward")} or alternate {GetBindingDisplay("LeftSki")} and {GetBindingDisplay("RightSki")} to get moving.";

                case LessonStep.Lean:
                    return $"{GetBindingDisplay("Lean", "positive", "up", "forward")} = faster. {GetBindingDisplay("Lean", "negative", "down", "back", "backward")} = slower.";

                case LessonStep.Turn:
                    return $"Keep moving while using {GetBindingDisplay("LeftSki")} and {GetBindingDisplay("RightSki")}.";

                case LessonStep.Skate:
                    return $"You do not need to lean forward here. Just alternate {GetBindingDisplay("LeftSki")} and {GetBindingDisplay("RightSki")}.";

                case LessonStep.QuickStop:
                    return $"Use {GetBindingDisplay("Lean", "negative", "down", "back", "backward")} plus {CombineBindings("LeftSki", "RightSki")} for a strong stop.";

                case LessonStep.JumpAndAir:
                    return $"Hold {GetBindingDisplay("Jump")} for more pop.";

                case LessonStep.Poles:
                    return $"{GetBindingDisplay("Poles")} handles both push and drag.";

                case LessonStep.Soreness:
                    return _sorenessImpactConfirmed
                        ? "Now head to the resort to recover."
                        : "Soreness reduces your condition until you recover.";

                case LessonStep.ResortRecovery:
                    return $"Enter the resort with {GetBindingDisplay("Interact")}, recover for a moment, then leave when ready.";

                case LessonStep.EquipSkis:
                    return $"Press {GetBindingDisplay("EquipSkis")} once to equip your skis.";

                case LessonStep.HudAndTasks:
                    return "The tasks panel is already open by default.";

                case LessonStep.CustomisationShop:
                    return $"Use {GetBindingDisplay("Interact")} to enter and leave. You do not need to buy anything.";

                case LessonStep.SkiPassKiosk:
                    return $"The default pass only needs to be claimed once and never expires. Use {GetBindingDisplay("Interact")} at the kiosk.";

                case LessonStep.RideLift:
                    return $"Press {GetBindingDisplay("Interact")} to board the lift.";

                case LessonStep.DismountLift:
                    return $"Press {GetBindingDisplay("Interact")} again to dismount.";
            }

            return string.Empty;
        }
    }
}