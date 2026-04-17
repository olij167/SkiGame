using UnityEngine;
using UnityEngine.UIElements;
using TimeWeather;

namespace SkiGame.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class ResortOverlayUI : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private SkiResortStateController resortController;
        [SerializeField] private SorenessMeter sorenessMeter;
        [SerializeField] private int documentSortOrder = 2400;
        [SerializeField] private int cursorPriority = 700;
        [SerializeField] private float refreshInterval = 0.1f;

        private VisualElement _root;
        private VisualElement _panel;
        private Label _title;
        private Label _time;
        private Label _day;
        private Label _sorenessValue;
        private Label _recoveryDetail;
        private Label _scheduleStatus;
        private Label _hint;
        private VisualElement _sorenessFill;

        private Button _btnLeaveNow;
        private Button _btnLeaveRecovered;
        private Button _btnLeave0600;
        private Button _btnLeave0800;
        private Button _btnLeave1200;
        private Button _btnCancelSchedule;

        private float _nextRefreshTime;
        private bool _cursorOwned;

        private void OnEnable()
        {
            if (document == null)
                document = GetComponent<UIDocument>();

            if (resortController == null)
                resortController = SkiResortStateController.Instance;

            if (sorenessMeter == null && resortController != null)
                sorenessMeter = resortController.PlayerSoreness;

            if (document == null || document.rootVisualElement == null)
            {
                enabled = false;
                return;
            }

            document.sortingOrder = documentSortOrder;

            _root = document.rootVisualElement;
            Bind();
            HookButtons();
            RefreshImmediate();
        }

        private void OnDisable()
        {
            ReleaseCursor();
            UnhookButtons();
        }

        private void Update()
        {
            if (resortController == null)
                resortController = SkiResortStateController.Instance;

            if (sorenessMeter == null && resortController != null)
                sorenessMeter = resortController.PlayerSoreness;

            if (Time.unscaledTime < _nextRefreshTime)
                return;

            _nextRefreshTime = Time.unscaledTime + refreshInterval;
            RefreshImmediate();
        }

        private void Bind()
        {
            _panel = _root.Q<VisualElement>("ResortOverlayPanel");
            _title = _root.Q<Label>("Lbl_ResortTitle");
            _time = _root.Q<Label>("Lbl_ResortTime");
            _day = _root.Q<Label>("Lbl_ResortDay");
            _sorenessValue = _root.Q<Label>("Lbl_ResortSorenessValue");
            _recoveryDetail = _root.Q<Label>("Lbl_ResortRecoveryDetail");
            _scheduleStatus = _root.Q<Label>("Lbl_ResortScheduleStatus");
            _hint = _root.Q<Label>("Lbl_ResortHint");
            _sorenessFill = _root.Q<VisualElement>("ResortSorenessFill");

            _btnLeaveNow = _root.Q<Button>("Btn_ResortLeaveNow");
            _btnLeaveRecovered = _root.Q<Button>("Btn_ResortLeaveRecovered");
            _btnLeave0600 = _root.Q<Button>("Btn_ResortLeave0600");
            _btnLeave0800 = _root.Q<Button>("Btn_ResortLeave0800");
            _btnLeave1200 = _root.Q<Button>("Btn_ResortLeave1200");
            _btnCancelSchedule = _root.Q<Button>("Btn_ResortCancelSchedule");
        }

        private void HookButtons()
        {
            if (_btnLeaveNow != null) _btnLeaveNow.clicked += HandleLeaveNow;
            if (_btnLeaveRecovered != null) _btnLeaveRecovered.clicked += HandleLeaveRecovered;
            if (_btnLeave0600 != null) _btnLeave0600.clicked += HandleLeave0600;
            if (_btnLeave0800 != null) _btnLeave0800.clicked += HandleLeave0800;
            if (_btnLeave1200 != null) _btnLeave1200.clicked += HandleLeave1200;
            if (_btnCancelSchedule != null) _btnCancelSchedule.clicked += HandleCancelSchedule;
        }

        private void UnhookButtons()
        {
            if (_btnLeaveNow != null) _btnLeaveNow.clicked -= HandleLeaveNow;
            if (_btnLeaveRecovered != null) _btnLeaveRecovered.clicked -= HandleLeaveRecovered;
            if (_btnLeave0600 != null) _btnLeave0600.clicked -= HandleLeave0600;
            if (_btnLeave0800 != null) _btnLeave0800.clicked -= HandleLeave0800;
            if (_btnLeave1200 != null) _btnLeave1200.clicked -= HandleLeave1200;
            if (_btnCancelSchedule != null) _btnCancelSchedule.clicked -= HandleCancelSchedule;
        }

        private void RefreshImmediate()
        {
            bool show = resortController != null && resortController.State == SkiResortStateController.ResortState.InResort;

            if (_panel != null)
                _panel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;

            if (!show)
            {
                ReleaseCursor();
                return;
            }

            AcquireCursor();

            if (_title != null)
            {
                SkiResortAccessManager resortAccessManager = SkiResortAccessManager.Instance != null
                    ? SkiResortAccessManager.Instance
                    : FindObjectOfType<SkiResortAccessManager>();

                string zoneName = "Ski Resort";
                if (resortController.ActiveZone != null)
                {
                    if (resortAccessManager != null && !string.IsNullOrWhiteSpace(resortController.ActiveZone.ResortId))
                        zoneName = resortAccessManager.GetDisplayName(resortController.ActiveZone.ResortId);
                    else
                        zoneName = resortController.ActiveZone.name;
                }

                _title.text = zoneName;
            }

            if (_time != null)
                _time.text = resortController.GetCurrentClockText();

            if (_day != null)
                _day.text = resortController.GetCurrentDayText();

            float soreness01 = sorenessMeter != null ? Mathf.Clamp01(sorenessMeter.Soreness01) : 0f;
            float recovered01 = 1f - soreness01;

            if (_sorenessValue != null)
                _sorenessValue.text = $"{Mathf.RoundToInt(recovered01 * 100f)}% recovered";

            if (_sorenessFill != null)
                _sorenessFill.style.width = Length.Percent(recovered01 * 100f);

            if (_recoveryDetail != null)
                _recoveryDetail.text = BuildRecoveryDetail();

            if (_scheduleStatus != null)
                _scheduleStatus.text = resortController.GetScheduledExitSummary();

            if (_hint != null)
                _hint.text = BuildAccessHint();

            if (_btnCancelSchedule != null)
                _btnCancelSchedule.SetEnabled(resortController.HasScheduledExit);
        }

        private string BuildAccessHint()
        {
            SkiResortZone zone = resortController != null ? resortController.ActiveZone : null;
            if (zone == null || string.IsNullOrWhiteSpace(zone.ResortId))
                return "Choose when to leave so time passes intentionally rather than drifting.";

            SkiResortAccessManager resortAccessManager = SkiResortAccessManager.Instance != null
                ? SkiResortAccessManager.Instance
                : FindObjectOfType<SkiResortAccessManager>();

            if (resortAccessManager == null)
                return "Choose when to leave so time passes intentionally rather than drifting.";

            if (resortAccessManager.IsResortPermanentlyUnlocked(zone.ResortId))
                return "This resort is permanently available.";

            if (resortAccessManager.TryGetRentalTimeRemainingText(zone.ResortId, out string timeRemaining))
                return $"Rental active: {timeRemaining}.";

            return "Choose when to leave so time passes intentionally rather than drifting.";
        }

        private string BuildRecoveryDetail()
        {
            if (sorenessMeter == null)
                return "Recovery status unavailable";

            if (sorenessMeter.IsFullyRecovered(resortController != null ? resortController.RecoveredThreshold01 : 0.01f))
                return "Fully recovered";

            float etaSeconds = sorenessMeter.EstimateSecondsUntilRecovered(assumeResting: true);
            if (float.IsInfinity(etaSeconds) || etaSeconds < 0f)
                return "Recovering";

            int totalMinutes = Mathf.Max(1, Mathf.CeilToInt(etaSeconds / 60f));
            int hours = totalMinutes / 60;
            int minutes = totalMinutes % 60;

            if (hours > 0)
                return $"Full recovery in about {hours}h {minutes:00}m";

            return $"Full recovery in about {minutes}m";
        }

        private void HandleLeaveNow()
        {
            resortController?.LeaveResortNow();
        }

        private void HandleLeaveRecovered()
        {
            resortController?.ScheduleExitWhenRecovered();
            RefreshImmediate();
        }

        private void HandleLeave0600()
        {
            resortController?.ScheduleExitAtNextOccurrence(6, 0);
            RefreshImmediate();
        }

        private void HandleLeave0800()
        {
            resortController?.ScheduleExitAtNextOccurrence(8, 0);
            RefreshImmediate();
        }

        private void HandleLeave1200()
        {
            resortController?.ScheduleExitAtNextOccurrence(12, 0);
            RefreshImmediate();
        }

        private void HandleCancelSchedule()
        {
            resortController?.CancelScheduledExit();
            RefreshImmediate();
        }

        private void AcquireCursor()
        {
            if (_cursorOwned)
                return;

            _cursorOwned = true;
            GameCursorService.Request(this, GameCursorMode.VisibleUnlocked, cursorPriority);
        }

        private void ReleaseCursor()
        {
            if (!_cursorOwned)
                return;

            _cursorOwned = false;
            GameCursorService.Release(this);
        }
    }
}
