using UnityEngine;
using SkiGame.Activities;
using SkiGame.Progression;

namespace SkiGame.UI
{
    public enum ActivityHudMode
    {
        None = 0,
        RacePreStart = 10,
        RaceActive = 20,
        RescueActive = 30,
        Result = 40
    }

    public enum ActivityHudResultType
    {
        None = 0,
        Success = 10,
        Failure = 20,
        Cancelled = 30
    }

    public sealed class ActivityHudSnapshot
    {
        public ActivityHudMode mode = ActivityHudMode.None;
        public ActivityHudResultType resultType = ActivityHudResultType.None;

        public MountainActivityKind activityKind = MountainActivityKind.None;
        public Object source;

        public string title;
        public string subtitle;
        public string statusText;
        public string objectiveText;
        public string progressText;
        public float progress01 = -1f;

        public string statLineA;
        public string statLineB;
        public string statLineC;

        public string actionLineA;
        public string actionLineB;

        public bool suppressQuestList;
        public bool allowQuestListExpansion = true;
        public bool displaceTrackedQuestToSecondary;
        public bool showProgressBar;

        public Color accentColor = new Color(0.36f, 0.78f, 1f, 1f);

        public ActivityHudSnapshot Clone()
        {
            return new ActivityHudSnapshot
            {
                mode = mode,
                resultType = resultType,
                activityKind = activityKind,
                source = source,
                title = title,
                subtitle = subtitle,
                statusText = statusText,
                objectiveText = objectiveText,
                progressText = progressText,
                progress01 = progress01,
                statLineA = statLineA,
                statLineB = statLineB,
                statLineC = statLineC,
                actionLineA = actionLineA,
                actionLineB = actionLineB,
                suppressQuestList = suppressQuestList,
                allowQuestListExpansion = allowQuestListExpansion,
                displaceTrackedQuestToSecondary = displaceTrackedQuestToSecondary,
                showProgressBar = showProgressBar,
                accentColor = accentColor
            };
        }
    }

    [DisallowMultipleComponent]
    public sealed class ActivityHudPresenter : MonoBehaviour
    {
        [Header("Result Display")]
        [SerializeField, Min(0.5f)] private float resultDisplaySeconds = 3.25f;

        [Header("Optional Direct References")]
        [SerializeField] private MountainActivityManager activityManager;
        [SerializeField] private RescueService rescueService;

        private MountainActivityManager _boundActivityManager;
        private ActivityHudSnapshot _resultSnapshot;
        private float _resultUntil = -1f;

        private RaceCourseLine _pendingRaceFailureRace;
        private string _pendingRaceFailureReason;

        private static readonly Color RaceAccent = new Color(0.98f, 0.83f, 0.28f, 1f);
        private static readonly Color RescueAccent = new Color(0.42f, 0.88f, 0.74f, 1f);
        private static readonly Color SuccessAccent = new Color(0.36f, 0.90f, 0.52f, 1f);
        private static readonly Color FailureAccent = new Color(1f, 0.56f, 0.48f, 1f);
        private static readonly Color CancelledAccent = new Color(1f, 0.80f, 0.42f, 1f);

        private void Reset()
        {
            if (activityManager == null)
                activityManager = MountainActivityManager.Instance != null
                    ? MountainActivityManager.Instance
                    : FindObjectOfType<MountainActivityManager>();

            if (rescueService == null)
                rescueService = RescueService.Instance != null
                    ? RescueService.Instance
                    : FindObjectOfType<RescueService>();
        }

        private void OnEnable()
        {
            TryBindActivityManager();
        }

        private void OnDisable()
        {
            UnbindActivityManager();
        }

        private void Update()
        {
            TryBindActivityManager();
        }

        public bool TryGetCurrentSnapshot(out ActivityHudSnapshot snapshot)
        {
            snapshot = null;

            if (_resultSnapshot != null && Time.unscaledTime < _resultUntil)
            {
                snapshot = _resultSnapshot.Clone();
                return true;
            }

            var mgr = ResolveActivityManager();
            if (mgr != null && mgr.HasActiveActivity)
            {
                if (mgr.ActiveKind == MountainActivityKind.Race && mgr.ActiveSource is RaceCourseLine activeRace)
                {
                    snapshot = BuildRaceActiveSnapshot(activeRace);
                    return true;
                }

                if (mgr.ActiveKind == MountainActivityKind.Rescue)
                {
                    var rescue = ResolveRescueService();
                    if (rescue != null && rescue.HasActiveMission)
                    {
                        snapshot = BuildRescueActiveSnapshot(rescue);
                        return true;
                    }
                }
            }

            RaceCourseLine selectableRace = FindSelectableRace();
            if (selectableRace != null)
            {
                snapshot = BuildRacePreStartSnapshot(selectableRace);
                return true;
            }

            return false;
        }

        public bool TryConsumeRaceFailureModal(out RaceCourseLine race, out string reason)
        {
            race = _pendingRaceFailureRace;
            reason = _pendingRaceFailureReason;

            if (race == null)
                return false;

            _pendingRaceFailureRace = null;
            _pendingRaceFailureReason = null;
            return true;
        }

        public void ClearPendingRaceFailureModal()
        {
            _pendingRaceFailureRace = null;
            _pendingRaceFailureReason = null;
        }

        private MountainActivityManager ResolveActivityManager()
        {
            if (activityManager == null)
                activityManager = MountainActivityManager.Instance != null
                    ? MountainActivityManager.Instance
                    : FindObjectOfType<MountainActivityManager>();

            return activityManager;
        }

        private RescueService ResolveRescueService()
        {
            if (rescueService == null)
                rescueService = RescueService.Instance != null
                    ? RescueService.Instance
                    : FindObjectOfType<RescueService>();

            return rescueService;
        }

        private void TryBindActivityManager()
        {
            var mgr = ResolveActivityManager();
            if (mgr == null || _boundActivityManager == mgr)
                return;

            UnbindActivityManager();

            _boundActivityManager = mgr;
            _boundActivityManager.OnActivityCompleted += HandleActivityCompleted;
            _boundActivityManager.OnActivityFailed += HandleActivityFailed;
            _boundActivityManager.OnActivityCancelled += HandleActivityCancelled;
        }

        private void UnbindActivityManager()
        {
            if (_boundActivityManager == null)
                return;

            _boundActivityManager.OnActivityCompleted -= HandleActivityCompleted;
            _boundActivityManager.OnActivityFailed -= HandleActivityFailed;
            _boundActivityManager.OnActivityCancelled -= HandleActivityCancelled;
            _boundActivityManager = null;
        }

        private void HandleActivityCompleted(MountainActivityKind kind, MonoBehaviour source, string displayName)
        {
            switch (kind)
            {
                case MountainActivityKind.Race:
                    if (source is RaceCourseLine race)
                        SetTransientResult(BuildRaceResultSnapshot(race, ActivityHudResultType.Success, string.Empty));
                    break;

                case MountainActivityKind.Rescue:
                    SetTransientResult(BuildRescueResultSnapshot(displayName, ActivityHudResultType.Success, string.Empty));
                    break;
            }
        }

        private void HandleActivityFailed(MountainActivityKind kind, MonoBehaviour source, string displayName, string reason)
        {
            switch (kind)
            {
                case MountainActivityKind.Race:
                    if (source is RaceCourseLine race)
                    {
                        _pendingRaceFailureRace = race;
                        _pendingRaceFailureReason = string.IsNullOrWhiteSpace(reason) ? "Race failed." : reason.Trim();
                    }
                    break;

                case MountainActivityKind.Rescue:
                    SetTransientResult(BuildRescueResultSnapshot(displayName, ActivityHudResultType.Failure, reason));
                    break;
            }
        }

        private void HandleActivityCancelled(MountainActivityKind kind, MonoBehaviour source, string displayName)
        {
            switch (kind)
            {
                case MountainActivityKind.Race:
                    if (source is RaceCourseLine race)
                        SetTransientResult(BuildRaceResultSnapshot(race, ActivityHudResultType.Cancelled, "Race cancelled."));
                    break;

                case MountainActivityKind.Rescue:
                    SetTransientResult(BuildRescueResultSnapshot(displayName, ActivityHudResultType.Cancelled, "Mission cancelled."));
                    break;
            }
        }

        private void SetTransientResult(ActivityHudSnapshot snapshot)
        {
            _resultSnapshot = snapshot;
            _resultUntil = Time.unscaledTime + Mathf.Max(0.5f, resultDisplaySeconds);
        }

        private ActivityHudSnapshot BuildRacePreStartSnapshot(RaceCourseLine race)
        {
            int selectedLeague = race.SelectedLeagueNumber;
            bool isChampionship = race.IsRegionalChampionship;
            bool unlocked = isChampionship
                ? race.IsChampionshipUnlocked()
                : race.IsLeagueUnlocked(selectedLeague);
            bool completed = isChampionship
                ? RaceRescueProgression.IsRaceChampionshipCompleted(race.RaceId)
                : race.HasCompletedLeague(selectedLeague);

            int bestPlacement = race.GetBestPlacement(selectedLeague);
            float bestTime = race.GetBestTimeSeconds(selectedLeague);

            string statusText;
            string objectiveText = string.Empty;

            if (unlocked)
            {
                statusText = completed ? "Completed" : "Ready";
                objectiveText = race.BuildLeagueObjectiveSummary(selectedLeague);
            }
            else
            {
                statusText = "Locked";
                objectiveText = isChampionship
                    ? race.BuildChampionshipLockReason()
                    : "Clear the required race progression first.";
            }

            string subtitle = isChampionship
                ? "Championship"
                : race.GetLeagueDisplayName(selectedLeague);

            string statA = bestPlacement > 0
                ? $"Best placement: {bestPlacement}"
                : "Best placement: --";

            string statB = bestTime >= 0f
                ? $"Best time: {bestTime:0.00}s"
                : "Best time: --";

            string statC = isChampionship
                ? CompactChampionshipSummary(race)
                : $"Reward: ${race.CompletionReward}";

            return new ActivityHudSnapshot
            {
                mode = ActivityHudMode.RacePreStart,
                activityKind = MountainActivityKind.Race,
                source = race,
                title = race.RaceName,
                subtitle = subtitle,
                statusText = statusText,
                objectiveText = objectiveText,
                progressText = string.Empty,
                progress01 = -1f,
                statLineA = statA,
                statLineB = statB,
                statLineC = statC,
                actionLineA = string.Empty,
                actionLineB = string.Empty,
                suppressQuestList = true,
                allowQuestListExpansion = false,
                displaceTrackedQuestToSecondary = false,
                showProgressBar = false,
                accentColor = RaceAccent
            };
        }

        private ActivityHudSnapshot BuildRaceActiveSnapshot(RaceCourseLine race)
        {
            string stateText;
            if (race.IsSkisOffWarningActive)
                stateText = $"Skis off {Mathf.CeilToInt(race.SkisOffGraceSecondsRemaining)}";
            else if (race.IsOffCourseWarningActive)
                stateText = $"Return {Mathf.CeilToInt(race.OffCourseGraceSecondsRemaining)}";
            else if (race.IsCountdownActive)
                stateText = $"Starts in {Mathf.CeilToInt(race.CountdownRemainingSeconds)}";
            else
                stateText = "Racing";

            float progress01 = race.CheckpointCount > 0
                ? Mathf.Clamp01((float)race.CurrentCheckpointIndex / race.CheckpointCount)
                : 0f;

            string progressText = $"{Mathf.Min(race.CurrentCheckpointIndex + 1, Mathf.Max(1, race.CheckpointCount))} / {Mathf.Max(1, race.CheckpointCount)} checkpoints";

            string objective = race.IsOffCourseWarningActive
                ? "Return to the course before the grace timer expires."
                : race.IsSkisOffWarningActive
                    ? "Get your skis back on and continue the run."
                    : race.IsCountdownActive
                        ? "Get set at the start gate."
                        : race.BuildLeagueObjectiveSummary(race.ActiveLeagueNumber);

            string statA = race.IsCountdownActive
                ? $"Start in: {Mathf.CeilToInt(race.CountdownRemainingSeconds)}"
                : $"Time: {race.AttemptTimeSeconds:0.00}s";

            string statB = RaceActivityService.Instance != null
                ? $"NPCs finished: {RaceActivityService.Instance.FinishedNpcCount}/{RaceActivityService.Instance.ActiveNpcCount}"
                : "NPCs finished: --";

            string statC = race.IsRegionalChampionship
                ? "Championship rules active"
                : $"League: {race.GetLeagueDisplayName(race.ActiveLeagueNumber)}";

            return new ActivityHudSnapshot
            {
                mode = ActivityHudMode.RaceActive,
                activityKind = MountainActivityKind.Race,
                source = race,
                title = race.RaceName,
                subtitle = race.GetLeagueDisplayName(race.ActiveLeagueNumber),
                statusText = stateText,
                objectiveText = objective,
                progressText = progressText,
                progress01 = progress01,
                statLineA = statA,
                statLineB = statB,
                statLineC = statC,
                actionLineA = string.Empty,
                actionLineB = string.Empty,
                suppressQuestList = true,
                allowQuestListExpansion = false,
                displaceTrackedQuestToSecondary = false,
                showProgressBar = true,
                accentColor = RaceAccent
            };
        }

        private ActivityHudSnapshot BuildRescueActiveSnapshot(RescueService rescue)
        {
            int total = Mathf.Max(1, rescue.TotalCasualties);
            int secured = Mathf.Clamp(rescue.SecuredCasualties, 0, total);
            float progress01 = Mathf.Clamp01((float)secured / total);

            bool returnToSnowmobileWarning = rescue.UsesSnowmobile && rescue.IsAwaitingReturnToSnowmobile;

            string subtitle = rescue.IsReturnPhase
                ? "Return"
                : rescue.IsSearchAreaMission
                    ? "Search"
                    : "Recover";

            string statusText = returnToSnowmobileWarning
                ? "Return Now"
                : (rescue.IsExactLocationMission ? "Exact Location" : "Search Area");

            string objectiveText = returnToSnowmobileWarning
                ? $"Return to snowmobile {Mathf.Max(0f, rescue.ReturnToSnowmobileSecondsRemaining):0}s"
                : GetRescueObjectiveText(rescue);

            string statA = FormatTimerText(rescue.TimeRemainingSeconds);
            string statB = $"Recovered: {secured}/{total}";
            string statC = returnToSnowmobileWarning
                ? string.Empty
                : (rescue.UsesSnowmobile
                    ? "Drive through targets to collect"
                    : "Move through targets to collect");

            return new ActivityHudSnapshot
            {
                mode = ActivityHudMode.RescueActive,
                activityKind = MountainActivityKind.Rescue,
                source = rescue,
                title = rescue.MissionDisplayName,
                subtitle = subtitle,
                statusText = statusText,
                objectiveText = objectiveText,
                progressText = $"Recovered {secured}/{total}",
                progress01 = progress01,
                statLineA = statA,
                statLineB = statB,
                statLineC = statC,
                actionLineA = string.Empty,
                actionLineB = string.Empty,
                suppressQuestList = true,
                allowQuestListExpansion = false,
                displaceTrackedQuestToSecondary = false,
                showProgressBar = true,
                accentColor = RescueAccent
            };
        }

        private ActivityHudSnapshot BuildRaceResultSnapshot(RaceCourseLine race, ActivityHudResultType resultType, string reason)
        {
            string statusText;
            Color accent;
            string objective;

            switch (resultType)
            {
                case ActivityHudResultType.Success:
                    statusText = race.LastResolvedPlacement == 1 ? "Victory!" : "Finished";
                    accent = SuccessAccent;
                    objective = race.LastRewardGranted > 0
                        ? $"Reward received: ${race.LastRewardGranted}"
                        : "Race complete.";
                    break;

                case ActivityHudResultType.Cancelled:
                    statusText = "Cancelled";
                    accent = CancelledAccent;
                    objective = string.IsNullOrWhiteSpace(reason) ? "Race cancelled." : reason.Trim();
                    break;

                default:
                    statusText = "Failed";
                    accent = FailureAccent;
                    objective = string.IsNullOrWhiteSpace(reason) ? "Race failed." : reason.Trim();
                    break;
            }

            string statA = race.LastCompletionTimeSeconds > 0f
                ? $"Time: {race.LastCompletionTimeSeconds:0.00}s"
                : string.Empty;

            string statB = race.LastResolvedPlacement > 0 && race.LastResolvedEntrantCount > 0
                ? $"Placement: {race.LastResolvedPlacement}/{race.LastResolvedEntrantCount}"
                : string.Empty;

            string statC = race.LastAttemptLeagueNumber > 0
                ? $"League: {race.GetLeagueDisplayName(race.LastAttemptLeagueNumber)}"
                : string.Empty;

            return new ActivityHudSnapshot
            {
                mode = ActivityHudMode.Result,
                resultType = resultType,
                activityKind = MountainActivityKind.Race,
                source = race,
                title = race.RaceName,
                subtitle = "Race Result",
                statusText = statusText,
                objectiveText = objective,
                progressText = resultType == ActivityHudResultType.Success ? "Complete" : string.Empty,
                progress01 = resultType == ActivityHudResultType.Success ? 1f : 0f,
                statLineA = statA,
                statLineB = statB,
                statLineC = statC,
                actionLineA = string.Empty,
                actionLineB = string.Empty,
                suppressQuestList = true,
                allowQuestListExpansion = false,
                displaceTrackedQuestToSecondary = false,
                showProgressBar = resultType == ActivityHudResultType.Success,
                accentColor = accent
            };
        }

        private ActivityHudSnapshot BuildRescueResultSnapshot(string displayName, ActivityHudResultType resultType, string reason)
        {
            RescueService rescue = ResolveRescueService();

            string title = string.IsNullOrWhiteSpace(displayName) ? "Rescue Mission" : displayName.Trim();
            string statusText;
            Color accent;
            string objective;
            string statA = string.Empty;
            string statB = string.Empty;
            string statC = string.Empty;
            float progress01 = 0f;

            switch (resultType)
            {
                case ActivityHudResultType.Success:
                    statusText = "Completed";
                    accent = SuccessAccent;
                    objective = "Rescue complete.";
                    progress01 = 1f;

                    if (rescue != null)
                    {
                        statA = rescue.LastMissionCompletionSeconds > 0f
                            ? $"Finished: {rescue.LastMissionCompletionSeconds:0.0}s"
                            : string.Empty;

                        statB = rescue.LastMissionRewardGranted > 0
                            ? $"Reward: ${rescue.LastMissionRewardGranted}"
                            : string.Empty;

                        statC = rescue.LastMissionRankIncreased
                            ? $"Rank increased: {Mathf.Max(1, rescue.LastMissionNewRank)}"
                            : string.Empty;
                    }
                    break;

                case ActivityHudResultType.Cancelled:
                    statusText = "Cancelled";
                    accent = CancelledAccent;
                    objective = string.IsNullOrWhiteSpace(reason) ? "The rescue mission was cancelled." : reason.Trim();
                    progress01 = 0f;
                    break;

                default:
                    statusText = "Failed";
                    accent = FailureAccent;
                    objective = string.IsNullOrWhiteSpace(reason) ? "The rescue mission failed." : reason.Trim();
                    progress01 = 0f;
                    break;
            }

            return new ActivityHudSnapshot
            {
                mode = ActivityHudMode.Result,
                resultType = resultType,
                activityKind = MountainActivityKind.Rescue,
                source = rescue,
                title = title,
                subtitle = "Rescue Result",
                statusText = statusText,
                objectiveText = objective,
                progressText = resultType == ActivityHudResultType.Success ? "Recovered" : "Mission incomplete",
                progress01 = progress01,
                statLineA = statA,
                statLineB = statB,
                statLineC = statC,
                actionLineA = string.Empty,
                actionLineB = string.Empty,
                suppressQuestList = true,
                allowQuestListExpansion = false,
                displaceTrackedQuestToSecondary = false,
                showProgressBar = true,
                accentColor = accent
            };
        }

        private RaceCourseLine FindSelectableRace()
        {
            var mgr = ResolveActivityManager();
            if (mgr != null && mgr.HasActiveActivity)
                return null;

#if UNITY_2023_1_OR_NEWER
            var races = FindObjectsByType<RaceCourseLine>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            var races = FindObjectsOfType<RaceCourseLine>();
#endif
            RaceCourseLine best = null;
            int bestPriority = int.MinValue;

            for (int i = 0; i < races.Length; i++)
            {
                RaceCourseLine race = races[i];
                if (race == null || !race.CanShowLeagueSelection)
                    continue;

                if (race.PromptPriority > bestPriority)
                {
                    best = race;
                    bestPriority = race.PromptPriority;
                }
            }

            return best;
        }

        private static string BuildLeagueLockReason(RaceCourseLine race, int selectedLeague)
        {
            int required = race.GetPreviousLeagueNumber(selectedLeague);
            return required > 0
                ? $"Locked - win {race.GetLeagueDisplayName(required)}"
                : "Locked";
        }

        private static string CompactChampionshipSummary(RaceCourseLine race)
        {
            int completedRegionalRaces = race.GetChampionshipCompletedStandardRaceCount();
            int totalRegionalRaces = Mathf.Max(1, race.GetChampionshipTotalStandardRaceCount());
            return $"Regional requirement: {completedRegionalRaces}/{totalRegionalRaces}";
        }

        private static string GetRescueObjectiveText(RescueService rescue)
        {
            if (rescue == null || !rescue.HasActiveMission)
                return string.Empty;

            if (rescue.IsReturnPhase)
                return "Return to the medic tent.";

            switch (rescue.MissionTargetMode)
            {
                case RescueService.RescueMissionTargetMode.InjuredWithCompanion:
                    return "Search the marked area for the injured skier and their companion.";

                case RescueService.RescueMissionTargetMode.StrandedHealthy:
                    return "Search the marked area for the lost skier.";

                default:
                    return "Search the marked area for the injured skier";
            }
        }

        private static string FormatTimerText(float timeRemaining)
        {
            int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(timeRemaining));
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            return $"Time left: {minutes:00}:{seconds:00}";
        }
    }
}
