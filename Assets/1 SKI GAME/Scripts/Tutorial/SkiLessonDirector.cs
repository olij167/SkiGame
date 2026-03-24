using SkiGame.Runs;
using UnityEngine;

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
            HudAndTasks,
            CustomisationShop,
            Explore,
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

        [Header("Activation")]
        [SerializeField] private bool forceRunEvenIfCompleted = false;

        [Header("Thresholds")]
        [SerializeField] private float moveDistanceRequired = 12f;
        [SerializeField] private float turnSpeedRequired = 4.5f;
        [SerializeField] private float quickStopPrimeSpeed = 5.25f;
        [SerializeField] private float quickStopExitSpeed = 3.4f;
        [SerializeField] private float sorenessRequired = 0.035f;
        [SerializeField] private float exploreTravelDistanceRequired = 200f;
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
        private bool _shopBought;
        private bool _shopEquipped;
        private int _shopUnlockedBaseline;
        private string _shopEquippedSignatureBaseline;

        public bool IsLessonActive => _active;
        public bool CanOfferLessons => playerStatsManager != null && playerStatsManager.Profile?.tutorial != null && playerStatsManager.Profile.tutorial.CanOfferSkiLessons;
        public bool LessonsCompleted => playerStatsManager != null && playerStatsManager.Profile?.tutorial != null && playerStatsManager.Profile.tutorial.skiLessonsCompleted;
        public int CurrentStepIndex => (int)_currentStep;
        public int TotalStepCount => (int)LessonStep.Explore + 1;
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
                        if (!IsPlayerInResort() && skiResortInteractor != null)
                            return skiResortInteractor.transform;
                        break;

                    case LessonStep.CustomisationShop:
                        if (!_shopOpened && customizationPortal != null)
                            return customizationPortal.transform;
                        break;
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
                }

                return string.Empty;
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



            if (progressionDirector == null)
                progressionDirector = ProgressionDirector.Instance != null
                    ? ProgressionDirector.Instance
                    : FindObjectOfType<ProgressionDirector>();

            if (customizationPortal == null)
                customizationPortal = FindObjectOfType<CustomizationPortal>();
        }

        public void BeginLessons()
        {
            if (playerStatsManager?.Profile?.tutorial == null)
                return;

            playerStatsManager.Profile.tutorial.AcceptLessons();

            int rawIndex = playerStatsManager.Profile.tutorial.skiLessonStepIndex;
            _currentStep = (LessonStep)Mathf.Clamp(rawIndex, 0, (int)LessonStep.Explore);

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
            _shopBought = false;
            _shopEquipped = false;
            _shopUnlockedBaseline = 0;
            _shopEquippedSignatureBaseline = string.Empty;
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
                _shopUnlockedBaseline = GetUnlockedCustomizationCount();
                _shopEquippedSignatureBaseline = GetEquippedCustomizationSignature();
            }

            if (_currentStep == LessonStep.Explore)
            {
                _exploreStartDistanceMeters = GetSessionDistanceMeters();
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
                        if (skiController.IsRiderGrounded && skiController.ForwardLeanInput > 0.05f)
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
                                _quickStopBestSpeedDrop = 0f;
                                _quickStopBrakeWindowTimer = 0f;
                            }
                        }

                        break;
                    }

                case LessonStep.JumpAndAir:
                    {
                        if (!skiController.IsRiderGrounded && skiController.Velocity.y > 1f)
                            _jumpStarted = true;

                        if (_jumpStarted && !skiController.IsRiderGrounded)
                        {
                            if (Mathf.Abs(skiController.RightLegInput - skiController.LeftLegInput) >= 0.2f ||
                                Mathf.Abs(skiController.ForwardLeanInput) >= 0.2f)
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
                        if (skiController.CurrentPolePhase == SkiController.PoleStrokePhase.Entry ||
                            skiController.CurrentPolePhase == SkiController.PoleStrokePhase.FollowThrough)
                        {
                            _sawPolePush = true;
                        }

                        if (skiController.CurrentPolePhase == SkiController.PoleStrokePhase.Drag && speed > 2f)
                            _sawPoleDrag = true;

                        if (_sawPolePush && _sawPoleDrag)
                            AdvanceStep();

                        break;
                    }

                case LessonStep.Soreness:
                    {
                        bool hitConfirmed = WasHitForSoreness();

                        if (hitConfirmed)
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

                        // First, require the player to actually enter the resort.
                        if (!_enteredResortAfterSoreness)
                            break;

                        // Then let them leave once they have seen the resort and had time to recover, even a little.
                        if (!inResort && (_resortRecoveryExplained || sorenessReduced || fullyRecovered))
                        {
                            AdvanceStep();
                        }

                        break;
                    }

                case LessonStep.HudAndTasks:
                    {
                        bool overlayOpen = mountainHudOverlay != null && mountainHudOverlay.IsOpen;
                        bool visitedStats = mountainHudOverlay != null && mountainHudOverlay.TutorialVisitedStats;
                        bool visitedTasks = mountainHudOverlay != null && mountainHudOverlay.TutorialVisitedTasks;
                        bool visitedMap = mountainHudOverlay != null && mountainHudOverlay.TutorialVisitedMap;
                        bool claimedTask = progressionDirector != null && progressionDirector.CountClaimedDailyTiers() > _hudClaimedTierBaseline;

                        if (overlayOpen && visitedStats && visitedTasks && visitedMap && claimedTask)
                            AdvanceStep();

                        break;
                    }

                case LessonStep.CustomisationShop:
                    {
                        if (CustomizationShopRuntime.IsOpen)
                            _shopOpened = true;

                        if (GetUnlockedCustomizationCount() > _shopUnlockedBaseline)
                            _shopBought = true;

                        if (GetEquippedCustomizationSignature() != _shopEquippedSignatureBaseline)
                            _shopEquipped = true;

                        if (_shopOpened && _shopBought && _shopEquipped)
                            AdvanceStep();

                        break;
                    }

                case LessonStep.Explore:
                    {
                        var profile = playerStatsManager.Profile;
                        int currentRuns = profile.session != null ? profile.session.runsCompleted : 0;
                        int currentLifts = profile.session != null ? profile.session.liftsUsed : 0;
                        float traveled = GetSessionDistanceMeters() - _exploreStartDistanceMeters;

                        if (_loggedRunAttempt ||
                            currentRuns > _baselineRunsCompleted ||
                            currentLifts > _baselineLiftsUsed ||
                            traveled >= exploreTravelDistanceRequired)
                        {
                            CompleteLessons();
                        }

                        break;
                    }
            }
        }

        private float GetSessionDistanceMeters()
        {
            return playerStatsManager?.Profile?.session != null ? playerStatsManager.Profile.session.distanceMeters : 0f;
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
                    return Mathf.Clamp01(_turnAccumulatedTime / 1f);

                case LessonStep.Skate:
                    return Mathf.Clamp01(_skateAlternations / 4f);

                case LessonStep.QuickStop:
                    {
                        if (!_quickStopPrimed)
                            return Mathf.Clamp01(GetPlanarSpeed() / Mathf.Max(0.01f, quickStopPrimeSpeed));

                        if (!_quickStopBrakeStarted)
                            return 0.35f;

                        float stopProgress = Mathf.Clamp01(_quickStopBestSpeedDrop / 1.9f);
                        float finishProgress = Mathf.Clamp01(1f - (GetPlanarSpeed() / Mathf.Max(0.01f, quickStopPrimeSpeed)));
                        return Mathf.Clamp01(stopProgress * 0.75f + finishProgress * 0.25f);
                    }

                case LessonStep.JumpAndAir:
                    {
                        float p = 0f;
                        if (_jumpStarted) p += 0.34f;
                        if (_airAdjusted) p += 0.33f;
                        if (_jumpStarted && _airAdjusted && skiController.IsRiderGrounded) p = 1f;
                        return p;
                    }

                case LessonStep.Poles:
                    return ((_sawPolePush ? 1f : 0f) + (_sawPoleDrag ? 1f : 0f)) / 2f;

                case LessonStep.Soreness:
                    {
                        if (!_sorenessImpactConfirmed)
                            return sorenessMeter != null ? Mathf.Clamp01(sorenessMeter.Soreness01 / Mathf.Max(0.001f, sorenessRequired)) : 0f;

                        return 1f;
                    }

                case LessonStep.ResortRecovery:
                    {
                        if (!IsPlayerInResort())
                            return 1f;

                        if (sorenessMeter == null)
                            return 0.5f;

                        float current = sorenessMeter.Soreness01;
                        float recovered = Mathf.Max(0f, _resortRecoveryStartSoreness - current);
                        float target = Mathf.Max(0.02f, _resortRecoveryStartSoreness);
                        return Mathf.Clamp01(recovered / target);
                    }

                case LessonStep.HudAndTasks:
                    {
                        float p = 0f;
                        if (mountainHudOverlay != null && mountainHudOverlay.TutorialVisitedStats) p += 0.25f;
                        if (mountainHudOverlay != null && mountainHudOverlay.TutorialVisitedTasks) p += 0.25f;
                        if (mountainHudOverlay != null && mountainHudOverlay.TutorialVisitedMap) p += 0.25f;
                        if (progressionDirector != null && progressionDirector.CountClaimedDailyTiers() > _hudClaimedTierBaseline) p += 0.25f;
                        return p;
                    }

                case LessonStep.CustomisationShop:
                    {
                        float p = 0f;
                        if (_shopOpened) p += 0.34f;
                        if (_shopBought) p += 0.33f;
                        if (_shopEquipped) p += 0.33f;
                        return p;
                    }

                case LessonStep.Explore:
                    {
                        float traveled = GetSessionDistanceMeters() - _exploreStartDistanceMeters;
                        return Mathf.Clamp01(traveled / Mathf.Max(0.01f, exploreTravelDistanceRequired));
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

                case LessonStep.HudAndTasks:
                    {
                        int count = 0;
                        if (mountainHudOverlay != null && mountainHudOverlay.TutorialVisitedStats) count++;
                        if (mountainHudOverlay != null && mountainHudOverlay.TutorialVisitedTasks) count++;
                        if (mountainHudOverlay != null && mountainHudOverlay.TutorialVisitedMap) count++;
                        if (progressionDirector != null && progressionDirector.CountClaimedDailyTiers() > _hudClaimedTierBaseline) count++;
                        return $"{count}/4";
                    }

                case LessonStep.CustomisationShop:
                    {
                        int count = 0;
                        if (_shopOpened) count++;
                        if (_shopBought) count++;
                        if (_shopEquipped) count++;
                        return $"{count}/3";
                    }

                case LessonStep.Explore:
                    return $"{Mathf.Max(0f, GetSessionDistanceMeters() - _exploreStartDistanceMeters):0}/{exploreTravelDistanceRequired:0} m";
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
                case LessonStep.HudAndTasks: return "Mini HUD and Overlay";
                case LessonStep.CustomisationShop: return "Customisation Shop";
                case LessonStep.Explore: return "Explore";
                default: return "Ski Lessons";
            }
        }

        private string GetStepBody(LessonStep step)
        {
            switch (step)
            {
                case LessonStep.Move:
                    return "Press your movement inputs and move away from the start.";

                case LessonStep.Lean:
                    return "Press forward lean to gain speed. Press backward lean to slow down.";

                case LessonStep.Turn:
                    return "Press your ski steering inputs to turn and carve.";

                case LessonStep.Skate:
                    return "Lean forward and alternate: left, right, left, right.";

                case LessonStep.QuickStop:
                    return "Build some speed, then lean back and turn sharply to scrub speed fast.";

                case LessonStep.JumpAndAir:
                    return "Press and hold jump to pop. Use lean and ski inputs in the air.";

                case LessonStep.Poles:
                    return "Press poles to push on flats and drag them to control speed.";

                case LessonStep.Soreness:
                    return _sorenessImpactConfirmed
                        ? "Soreness has built up from the crash."
                        : "Crashes and hard efforts build soreness.";

                case LessonStep.ResortRecovery:
                    return "Go to the ski resort to recover. Soreness heals inside, time moves forward, and you can choose when to leave.";

                case LessonStep.HudAndTasks:
                    return "The Mini HUD shows live mountain info. Open the Mountain Overlay, check Stats, open Tasks, view the Map, and claim the ready task.";

                case LessonStep.CustomisationShop:
                    return "Go to the customisation shop. Buy one item and equip one item.";

                case LessonStep.Explore:
                    return "Ride a lift, attempt a run, or travel 200 m to finish the lesson.";
            }

            return string.Empty;
        }

        private string GetStepHint(LessonStep step)
        {
            switch (step)
            {
                case LessonStep.Move:
                    return "Push off, lean, or skate to start moving.";
                case LessonStep.Lean: return "Forward = faster. Backward = slower.";
                case LessonStep.Turn: return "Keep moving while you turn.";
                case LessonStep.Skate: return "Left, right, left, right.";
                case LessonStep.QuickStop: return "A strong speed drop counts, even if the stop is not perfect.";
                case LessonStep.JumpAndAir: return "Holding sprint in the air gives you more movement.";
                case LessonStep.Poles: return "Push for speed. Drag for braking.";
                case LessonStep.Soreness:
                    return _sorenessImpactConfirmed
                        ? "Now head to the resort to recover."
                        : "Soreness reduces your condition until you recover.";
                case LessonStep.ResortRecovery:
                    return "Enter the resort, recover for a moment, then use the exit options when ready.";
                case LessonStep.HudAndTasks:
                    return "Use the overlay to review your progress and navigate the mountain.";

                case LessonStep.CustomisationShop:
                    return "Use the shop to personalise your skier.";
                case LessonStep.Explore: return "You do not need to complete a full run.";
            }

            return string.Empty;
        }
    }
}