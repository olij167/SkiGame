using SkiGame.Activities;
using UnityEngine;
using UnityEngine.UIElements;

namespace SkiGame.UI
{
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public sealed class RescueMissionOverlayUI : MonoBehaviour
    {
        [Header("Document")]
        [SerializeField] private UIDocument document;
        [SerializeField] private int documentSortOrder = 840;
        [SerializeField] private float refreshInterval = 0.05f;

        [Header("Summary")]
        [SerializeField] private float summaryDuration = 6f;

        private VisualElement _root;
        private VisualElement _centerCallout;
        private Label _centerCalloutLabel;

        private VisualElement _panel;
        private Label _kicker;
        private Label _title;
        private Label _modeChip;
        private Label _targetChip;
        private Label _stageChip;

        private Label _objective;
        private Label _detail;
        private Label _timer;
        private Label _casualties;
        private Label _vehicle;
        private Label _searchRadius;

        private VisualElement _progressFill;
        private Label _progressText;

        private MountainActivityManager _boundActivityManager;
        private float _nextRefreshTime;
        private float _summaryUntil = -1f;
        private float _calloutUntil = -1f;
        private float _calloutDuration = 1.7f;
        private string _calloutMessage = string.Empty;
        private Color _calloutColor = Color.white;
        private string _summaryTitle = string.Empty;
        private string _summaryState = string.Empty;
        private string _summaryObjective = string.Empty;
        private string _summaryDetail = string.Empty;
        private string _summaryTimer = string.Empty;
        private string _summaryCasualties = string.Empty;
        private string _summaryVehicle = string.Empty;
        private string _summaryProgress = string.Empty;
        private float _summaryProgress01;

        private void OnEnable()
        {
            if (document == null)
                document = GetComponent<UIDocument>();

            if (document == null || document.rootVisualElement == null)
            {
                enabled = false;
                return;
            }

            document.sortingOrder = documentSortOrder;
            _root = document.rootVisualElement;

            Bind();
            TryBindActivityManager();
            RefreshImmediate();
        }

        private void OnDisable()
        {
            UnbindActivityManager();
        }

        private void Update()
        {
            TryBindActivityManager();
            UpdateCenterCallout();

            if (Time.unscaledTime < _nextRefreshTime)
                return;

            _nextRefreshTime = Time.unscaledTime + refreshInterval;
            RefreshImmediate();
        }

        private void Bind()
        {
            _centerCallout = _root.Q<VisualElement>("RescueCenterCallout");
            _centerCalloutLabel = _root.Q<Label>("Lbl_RescueCenterCallout");
            _panel = _root.Q<VisualElement>("RescueMissionPanel");
            _kicker = _root.Q<Label>("Lbl_RescueKicker");
            _title = _root.Q<Label>("Lbl_RescueTitle");
            _modeChip = _root.Q<Label>("Lbl_RescueModeChip");
            _targetChip = _root.Q<Label>("Lbl_RescueTargetChip");
            _stageChip = _root.Q<Label>("Lbl_RescueStageChip");

            _objective = _root.Q<Label>("Lbl_RescueObjective");
            _detail = _root.Q<Label>("Lbl_RescueDetail");
            _timer = _root.Q<Label>("Lbl_RescueTimer");
            _casualties = _root.Q<Label>("Lbl_RescueCasualties");
            _vehicle = _root.Q<Label>("Lbl_RescueVehicle");
            _searchRadius = _root.Q<Label>("Lbl_RescueSearchRadius");

            _progressFill = _root.Q<VisualElement>("RescueMissionProgressFill");
            _progressText = _root.Q<Label>("Lbl_RescueProgressText");
        }

        private void TryBindActivityManager()
        {
            var mgr = MountainActivityManager.Instance;
            if (mgr == null || _boundActivityManager == mgr)
                return;

            UnbindActivityManager();

            _boundActivityManager = mgr;
            _boundActivityManager.OnActivityCompleted += HandleCompleted;
            _boundActivityManager.OnActivityFailed += HandleFailed;
            _boundActivityManager.OnActivityCancelled += HandleCancelled;
        }

        private void UnbindActivityManager()
        {
            if (_boundActivityManager == null)
                return;

            _boundActivityManager.OnActivityCompleted -= HandleCompleted;
            _boundActivityManager.OnActivityFailed -= HandleFailed;
            _boundActivityManager.OnActivityCancelled -= HandleCancelled;
            _boundActivityManager = null;
        }

        private void HandleCompleted(MountainActivityKind kind, MonoBehaviour source, string displayName)
        {
            if (kind != MountainActivityKind.Rescue)
                return;

            RescueService rescue = RescueService.Instance;
            if (rescue == null)
            {
                ShowSummary(
                    displayName,
                    "Completed",
                    "Rescue complete.",
                    string.Empty,
                    string.Empty,
                    "All casualties secured",
                    string.Empty,
                    "Recovered",
                    1f);
                return;
            }

            string detail = rescue.LastMissionRewardGranted > 0
                ? $"Reward +${Mathf.Max(0, rescue.LastMissionRewardGranted)}."
                : "Rescue complete.";

            if (rescue.LastMissionRankIncreased)
                detail += $" Rescue rank {Mathf.Max(1, rescue.LastMissionNewRank)} reached.";

            ShowSummary(
                displayName,
                "Completed",
                "All casualties delivered to the medic tent.",
                detail,
                rescue.LastMissionCompletionSeconds > 0f ? $"Finished {rescue.LastMissionCompletionSeconds:0.0}s" : string.Empty,
                "All casualties secured",
                rescue.UsesSnowmobile ? "Transport snowmobile and stretcher returned safely" : "Return completed on foot",
                "Recovered",
                1f);
        }

        private void HandleFailed(MountainActivityKind kind, MonoBehaviour source, string displayName, string reason)
        {
            if (kind != MountainActivityKind.Rescue)
                return;

            ShowSummary(
                displayName,
                "Failed",
                string.IsNullOrWhiteSpace(reason) ? "The rescue mission failed." : reason,
                "Head back to the tent to regroup and try another dispatch.",
                string.Empty,
                "Recovery incomplete",
                string.Empty,
                "Mission failed",
                0f);

            ShowCenterCallout("Failed", new Color(1f, 0.58f, 0.50f, 1f), 1.2f);
        }

        private void HandleCancelled(MountainActivityKind kind, MonoBehaviour source, string displayName)
        {
            if (kind != MountainActivityKind.Rescue)
                return;

            ShowSummary(
                displayName,
                "Cancelled",
                "The rescue mission was cancelled.",
                "You can start again from the medic tent when ready.",
                string.Empty,
                "Recovery incomplete",
                string.Empty,
                "Mission cancelled",
                0f);

            ShowCenterCallout("Cancelled", new Color(1f, 0.82f, 0.52f, 1f), 1.2f);
        }

        private void ShowSummary(
            string title,
            string state,
            string objective,
            string detail,
            string timer,
            string casualties,
            string vehicle,
            string progressText,
            float progress01)
        {
            _summaryTitle = string.IsNullOrWhiteSpace(title) ? "Rescue Mission" : title.Trim();
            _summaryState = string.IsNullOrWhiteSpace(state) ? "Completed" : state.Trim();
            _summaryObjective = string.IsNullOrWhiteSpace(objective) ? string.Empty : objective.Trim();
            _summaryDetail = string.IsNullOrWhiteSpace(detail) ? string.Empty : detail.Trim();
            _summaryTimer = string.IsNullOrWhiteSpace(timer) ? string.Empty : timer.Trim();
            _summaryCasualties = string.IsNullOrWhiteSpace(casualties) ? string.Empty : casualties.Trim();
            _summaryVehicle = string.IsNullOrWhiteSpace(vehicle) ? string.Empty : vehicle.Trim();
            _summaryProgress = string.IsNullOrWhiteSpace(progressText) ? string.Empty : progressText.Trim();
            _summaryProgress01 = Mathf.Clamp01(progress01);
            _summaryUntil = Time.unscaledTime + Mathf.Max(0.5f, summaryDuration);
        }

        private void RefreshImmediate()
        {
            RefreshMissionPanel();
        }

        private void ShowCenterCallout(string message, Color color, float duration)
        {
            _calloutMessage = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim().ToUpperInvariant();
            _calloutColor = color;
            _calloutDuration = Mathf.Max(0.5f, duration);
            _calloutUntil = Time.unscaledTime + _calloutDuration;
        }

        private void UpdateCenterCallout()
        {
            if (_centerCallout == null || _centerCalloutLabel == null)
                return;

            if (string.IsNullOrWhiteSpace(_calloutMessage) || Time.unscaledTime >= _calloutUntil)
            {
                _centerCallout.style.display = DisplayStyle.None;
                return;
            }

            float age = _calloutDuration - (_calloutUntil - Time.unscaledTime);
            float intro = Mathf.Clamp01(age / 0.16f);
            float outro = Mathf.Clamp01((_calloutUntil - Time.unscaledTime) / 0.28f);
            float visibility = Mathf.Min(intro, outro);
            float pulse = 1f + Mathf.Sin(age * 11f) * 0.045f;
            float scale = Mathf.Lerp(1.32f, 1f, intro) * pulse;
            float y = Mathf.Lerp(18f, 0f, intro);

            _centerCallout.style.display = DisplayStyle.Flex;
            _centerCalloutLabel.text = _calloutMessage;
            _centerCalloutLabel.style.color = new StyleColor(new Color(_calloutColor.r, _calloutColor.g, _calloutColor.b, visibility));
            _centerCalloutLabel.style.opacity = visibility;
            _centerCalloutLabel.style.scale = new Scale(new Vector3(scale, scale, 1f));
            _centerCalloutLabel.style.translate = new Translate(0f, y);
        }

        private void RefreshMissionPanel()
        {
            if (_panel == null)
                return;

            RescueService rescue = RescueService.Instance;
            MountainActivityManager mgr = MountainActivityManager.Instance;

            bool showActive =
                rescue != null &&
                rescue.HasActiveMission &&
                mgr != null &&
                mgr.HasActiveActivity &&
                mgr.ActiveKind == MountainActivityKind.Rescue;

            bool showSummary = !showActive &&
                               !string.IsNullOrWhiteSpace(_summaryTitle) &&
                               Time.unscaledTime < _summaryUntil;

            bool show = showActive || showSummary;
            _panel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;

            if (!show)
                return;

            if (!showActive)
            {
                RefreshSummaryPanel();
                return;
            }

            int total = Mathf.Max(1, rescue.TotalCasualties);
            int secured = Mathf.Clamp(rescue.SecuredCasualties, 0, total);
            float progress = Mathf.Clamp01((float)secured / total);

            if (_kicker != null)
            {
                _kicker.style.display = DisplayStyle.Flex;
                _kicker.text = "Mountain Activity";
            }

            if (_title != null)
                _title.text = rescue.MissionDisplayName;

            if (_modeChip != null)
                _modeChip.text = rescue.IsExactLocationMission ? "Exact Location" : "Search Area";

            if (_targetChip != null)
                _targetChip.text = GetTargetModeText(rescue.MissionTargetMode);

            if (_stageChip != null)
                _stageChip.text = GetStageText(rescue);

            if (_objective != null)
                _objective.text = GetObjectiveText(rescue);

            if (_detail != null)
                _detail.text = GetDetailText(rescue);

            if (_timer != null)
            {
                _timer.style.display = DisplayStyle.Flex;
                _timer.text = FormatTimerText(rescue.TimeRemainingSeconds);
            }

            if (_casualties != null)
            {
                _casualties.style.display = DisplayStyle.Flex;
                _casualties.text = $"Casualties {rescue.RemainingCasualties}/{total} remaining";
            }

            if (_vehicle != null)
            {
                _vehicle.style.display = DisplayStyle.Flex;
                if (!rescue.UsesSnowmobile)
                {
                    _vehicle.text = "Transport On foot";
                }
                else
                {
                    _vehicle.text = rescue.IsAwaitingReturnToSnowmobile
                        ? "Transport Return to the snowmobile now or the mission will fail"
                        : "Transport Stay on the snowmobile and collide with the rescue target to collect";
                }
            }

            if (_searchRadius != null)
            {
                bool showRadius = rescue.IsSearchAreaMission && !rescue.IsReturnPhase;
                _searchRadius.style.display = showRadius ? DisplayStyle.Flex : DisplayStyle.None;
                _searchRadius.text = $"Search radius {Mathf.Max(0f, rescue.SearchAreaRadius):0}m";
            }

            if (_progressFill != null)
                _progressFill.style.width = Length.Percent(progress * 100f);

            if (_progressText != null)
                _progressText.text = $"Recovered {secured}/{total}";
        }

        private void RefreshSummaryPanel()
        {
            if (_kicker != null)
            {
                _kicker.style.display = DisplayStyle.Flex;
                _kicker.text = "Mountain Activity";
            }

            if (_title != null)
                _title.text = _summaryTitle;

            if (_modeChip != null)
                _modeChip.text = "Rescue";

            if (_targetChip != null)
                _targetChip.text = _summaryState;

            if (_stageChip != null)
                _stageChip.text = "Summary";

            if (_objective != null)
                _objective.text = _summaryObjective;

            if (_detail != null)
                _detail.text = _summaryDetail;

            if (_timer != null)
            {
                _timer.style.display = string.IsNullOrWhiteSpace(_summaryTimer) ? DisplayStyle.None : DisplayStyle.Flex;
                _timer.text = _summaryTimer;
            }

            if (_casualties != null)
            {
                _casualties.style.display = string.IsNullOrWhiteSpace(_summaryCasualties) ? DisplayStyle.None : DisplayStyle.Flex;
                _casualties.text = _summaryCasualties;
            }

            if (_vehicle != null)
            {
                _vehicle.style.display = string.IsNullOrWhiteSpace(_summaryVehicle) ? DisplayStyle.None : DisplayStyle.Flex;
                _vehicle.text = _summaryVehicle;
            }

            if (_searchRadius != null)
                _searchRadius.style.display = DisplayStyle.None;

            if (_progressFill != null)
                _progressFill.style.width = Length.Percent(_summaryProgress01 * 100f);

            if (_progressText != null)
                _progressText.text = _summaryProgress;
        }

        private static string GetTargetModeText(RescueService.RescueMissionTargetMode mode)
        {
            switch (mode)
            {
                case RescueService.RescueMissionTargetMode.InjuredWithCompanion:
                    return "Injured + Companion";

                case RescueService.RescueMissionTargetMode.StrandedHealthy:
                    return "Stranded Passenger";

                default:
                    return "Injured Casualty";
            }
        }

        private static string GetStageText(RescueService rescue)
        {
            if (rescue == null || !rescue.HasActiveMission)
                return "Inactive";

            if (rescue.IsReturnPhase)
                return "Return";

            return rescue.IsSearchAreaMission ? "Search" : "Recover";
        }

        private static string GetObjectiveText(RescueService rescue)
        {
            if (rescue == null || !rescue.HasActiveMission)
                return string.Empty;

            if (rescue.IsReturnPhase)
                return "Return the recovered passengers to the medic tent.";

            switch (rescue.MissionTargetMode)
            {
                case RescueService.RescueMissionTargetMode.InjuredWithCompanion:
                    return rescue.IsSearchAreaMission
                        ? "Search the marked area, secure the injured skier, and collect their companion."
                        : "Reach the casualty location, secure the injured skier, and collect their companion.";

                case RescueService.RescueMissionTargetMode.StrandedHealthy:
                    return rescue.IsSearchAreaMission
                        ? "Search the marked area and board the stranded passenger."
                        : "Reach the stranded passenger and board them onto the snowmobile.";

                default:
                    return rescue.IsSearchAreaMission
                        ? "Search the marked area and load the injured casualties onto the stretcher."
                        : "Reach the casualty location and load the injured skier onto the stretcher.";
            }
        }

        private static string GetDetailText(RescueService rescue)
        {
            if (rescue == null || !rescue.HasActiveMission)
                return string.Empty;

            if (rescue.IsReturnPhase)
                return "All targets are secured. Follow the navigation marker back to the tent.";

            if (rescue.IsSearchAreaMission)
                return rescue.UsesSnowmobile
                    ? "Enter the highlighted search zone, find the target, then collide with them using the snowmobile to collect."
                    : "Enter the highlighted search zone, reveal nearby casualties, then move in close to collect them.";

            switch (rescue.MissionTargetMode)
            {
                case RescueService.RescueMissionTargetMode.StrandedHealthy:
                    return "Drive the snowmobile into the passenger to board them. Leaving the snowmobile will fail the mission.";

                case RescueService.RescueMissionTargetMode.InjuredWithCompanion:
                    return "Collect the injured casualty with the stretcher, then collide with the companion using the snowmobile seat. Leaving the snowmobile will fail the mission.";

                default:
                    return rescue.UsesSnowmobile
                        ? "Drive the stretcher into each injured casualty to collect them. Leaving the snowmobile will fail the mission."
                        : "Bring the stretcher close enough to load each injured casualty safely.";
            }
        }

        private static string FormatTimerText(float timeRemaining)
        {
            int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(timeRemaining));
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            return $"Time left {minutes:00}:{seconds:00}";
        }
    }
}
