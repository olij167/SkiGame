using System;
using UnityEngine;
using UnityEngine.Events;

namespace PungentFunk.Utilities.EnvironmentSimulation
{
    [Serializable] public sealed class PungentClockSnapshotEvent : UnityEvent<PungentClockSnapshot> { }
    [Serializable] public sealed class PungentSeasonEvent : UnityEvent<string> { }

    [DisallowMultipleComponent]
    [AddComponentMenu("PungentFunk/Environment Simulation/Pungent Clock Controller")]
    public sealed class PungentClockController : MonoBehaviour
    {
        [Header("Calendar")]
        [SerializeField] private PungentEnvironmentCalendarProfile calendarProfile;
        [SerializeField] private int startYear = 1;
        [SerializeField] private int startMonthIndex;
        [SerializeField] private int startDayOfMonth = 1;
        [SerializeField, Range(0f, 24f)] private float startHour = 8f;

        [Header("Progression")]
        [SerializeField] private bool runOnStart = true;
        [SerializeField, Min(0.001f)] private float secondsPerInGameMinute = 0.5f;
        [SerializeField] private bool useDeltaTimeController = true;
        [SerializeField] private PungentDeltaTimeController deltaTimeController;
        [SerializeField] private bool fireMissedSequenceEventsWhenSkipping = true;

        [Header("Outputs")]
        [SerializeField] private Transform sunTransform;
        [SerializeField] private Transform moonTransform;
        [SerializeField] private Transform seasonalTiltTransform;
        [SerializeField] private Light sunLight;
        [SerializeField] private Light moonLight;
        [SerializeField] private PungentClockSkyProfile skyProfile;

        [Header("Events")]
        [SerializeField] private PungentClockSnapshotEvent onTimeChanged = new PungentClockSnapshotEvent();
        [SerializeField] private PungentClockSnapshotEvent onHourChanged = new PungentClockSnapshotEvent();
        [SerializeField] private PungentClockSnapshotEvent onDayChanged = new PungentClockSnapshotEvent();
        [SerializeField] private PungentClockSnapshotEvent onMonthChanged = new PungentClockSnapshotEvent();
        [SerializeField] private PungentSeasonEvent onSeasonChanged = new PungentSeasonEvent();
        [SerializeField] private PungentClockSnapshotEvent onYearChanged = new PungentClockSnapshotEvent();

        private PungentCalendarDate _date;
        private float _hour;
        private bool _running;
        private PungentClockSnapshot _snapshot;
        private bool _lastAdvanceFiredMissedSequenceEvents = true;

        public event Action<PungentClockSnapshot> TimeChanged;
        public event Action<PungentClockSnapshot> HourChanged;
        public event Action<PungentClockSnapshot> DayChanged;
        public event Action<PungentClockSnapshot> MonthChanged;
        public event Action<string> SeasonChanged;
        public event Action<PungentClockSnapshot> YearChanged;

        public PungentEnvironmentCalendarProfile CalendarProfile => calendarProfile;
        public PungentCalendarDate Date => _date;
        public float Hour24 => _hour;
        public PungentClockSnapshot Snapshot => _snapshot;
        public bool IsRunning { get => _running; set => _running = value; }
        public bool FireMissedSequenceEventsWhenSkipping => fireMissedSequenceEventsWhenSkipping;
        public bool LastAdvanceFiredMissedSequenceEvents => _lastAdvanceFiredMissedSequenceEvents;

        public PungentClockSnapshotEvent OnTimeChanged => onTimeChanged;
        public PungentClockSnapshotEvent OnHourChanged => onHourChanged;
        public PungentClockSnapshotEvent OnDayChanged => onDayChanged;
        public PungentClockSnapshotEvent OnMonthChanged => onMonthChanged;
        public PungentSeasonEvent OnSeasonChanged => onSeasonChanged;
        public PungentClockSnapshotEvent OnYearChanged => onYearChanged;

        private void Awake()
        {
            ResetToStart();
            _running = runOnStart;
        }

        private void OnEnable()
        {
            if (_snapshot.date.dayOfMonth <= 0)
                ResetToStart();
        }

        private void Update()
        {
            if (!_running)
                return;

            float channelScale = 1f;
            if (useDeltaTimeController)
            {
                if (deltaTimeController == null)
                    deltaTimeController = PungentDeltaTimeController.Instance;

                if (deltaTimeController != null)
                    channelScale = deltaTimeController.GetScale(PungentTimeChannel.Clock);
            }

            if (channelScale <= 0f)
                return;

            float gameMinutes = Time.unscaledDeltaTime * channelScale / Mathf.Max(0.001f, secondsPerInGameMinute);
            AdvanceHours(gameMinutes / 60f, fireMissedSequenceEventsWhenSkipping);
        }

        private void OnValidate()
        {
            secondsPerInGameMinute = Mathf.Max(0.001f, secondsPerInGameMinute);
            startDayOfMonth = Mathf.Max(1, startDayOfMonth);
            if (!Application.isPlaying)
            {
                ResetToStart();
                ApplySkyOutputs();
            }
        }

        public void ResetToStart()
        {
            _date = calendarProfile != null
                ? calendarProfile.NormalizeDate(startYear, startMonthIndex, startDayOfMonth)
                : new PungentCalendarDate(startYear, startMonthIndex, startDayOfMonth, PungentWeekday.Monday, 0);
            _hour = Mathf.Repeat(startHour, 24f);
            _snapshot = BuildSnapshot();
        }

        public void SetDateTime(int year, int monthIndex, int dayOfMonth, float hour24)
        {
            PungentClockSnapshot previous = _snapshot;
            _lastAdvanceFiredMissedSequenceEvents = fireMissedSequenceEventsWhenSkipping;
            _date = calendarProfile != null
                ? calendarProfile.NormalizeDate(year, monthIndex, dayOfMonth)
                : new PungentCalendarDate(year, monthIndex, dayOfMonth, previous.date.weekday, previous.date.dayOfYear);
            _hour = Mathf.Repeat(hour24, 24f);
            CommitSnapshot(previous);
        }

        public void SetHour(float hour24)
        {
            PungentClockSnapshot previous = _snapshot;
            _lastAdvanceFiredMissedSequenceEvents = fireMissedSequenceEventsWhenSkipping;
            _hour = Mathf.Repeat(hour24, 24f);
            CommitSnapshot(previous);
        }

        public void AdvanceDays(int days)
        {
            PungentClockSnapshot previous = _snapshot;
            _lastAdvanceFiredMissedSequenceEvents = fireMissedSequenceEventsWhenSkipping;
            _date = calendarProfile != null
                ? calendarProfile.AdvanceDays(_date, days)
                : new PungentCalendarDate(_date.year, _date.monthIndex, Mathf.Max(1, _date.dayOfMonth + days), _date.weekday, Mathf.Max(0, _date.dayOfYear + days));
            CommitSnapshot(previous);
        }

        public void AdvanceHours(float hours, bool fireMissedEvents)
        {
            PungentClockSnapshot previous = _snapshot;
            _lastAdvanceFiredMissedSequenceEvents = fireMissedEvents;
            float newHour = _hour + hours;
            int dayDelta = Mathf.FloorToInt(newHour / 24f);
            _hour = Mathf.Repeat(newHour, 24f);
            if (dayDelta != 0)
            {
                _date = calendarProfile != null
                    ? calendarProfile.AdvanceDays(_date, dayDelta)
                    : new PungentCalendarDate(_date.year, _date.monthIndex, Mathf.Max(1, _date.dayOfMonth + dayDelta), _date.weekday, Mathf.Max(0, _date.dayOfYear + dayDelta));
            }

            CommitSnapshot(previous);
        }

        public string GetCurrentSeason()
        {
            PungentEnvironmentCalendarProfile.MonthDefinition month = calendarProfile != null ? calendarProfile.GetMonth(_date.monthIndex) : null;
            return month == null || string.IsNullOrWhiteSpace(month.season) ? "Default" : month.season;
        }

        private void CommitSnapshot(PungentClockSnapshot previous)
        {
            _snapshot = BuildSnapshot();
            ApplySkyOutputs();
            RaiseTimeChanged(_snapshot);

            if (previous.hour != _snapshot.hour)
                RaiseHourChanged(_snapshot);
            if (!previous.date.Equals(_snapshot.date))
                RaiseDayChanged(_snapshot);
            if (previous.date.monthIndex != _snapshot.date.monthIndex)
                RaiseMonthChanged(_snapshot);
            if (!string.Equals(previous.seasonName, _snapshot.seasonName, StringComparison.OrdinalIgnoreCase))
                RaiseSeasonChanged(_snapshot.seasonName);
            if (previous.date.year != _snapshot.date.year)
                RaiseYearChanged(_snapshot);
        }

        private PungentClockSnapshot BuildSnapshot()
        {
            int hour = Mathf.FloorToInt(_hour);
            float minuteFloat = (_hour - hour) * 60f;
            int minute = Mathf.FloorToInt(minuteFloat);
            int second = Mathf.FloorToInt((minuteFloat - minute) * 60f);
            PungentEnvironmentCalendarProfile.MonthDefinition month = calendarProfile != null ? calendarProfile.GetMonth(_date.monthIndex) : null;
            int daysInYear = calendarProfile != null ? Mathf.Max(1, calendarProfile.DaysInYear) : 365;
            return new PungentClockSnapshot
            {
                date = _date,
                hour24 = _hour,
                hour = hour,
                minute = minute,
                second = second,
                dayPercent = _hour / 24f,
                monthName = month != null ? month.name : string.Empty,
                seasonName = month != null && !string.IsNullOrWhiteSpace(month.season) ? month.season : "Default",
                sunriseHour = month != null ? month.sunriseHour : 6f,
                sunsetHour = month != null ? month.sunsetHour : 18f,
                seasonPercent = Mathf.Clamp01(_date.dayOfYear / (float)daysInYear)
            };
        }

        private void ApplySkyOutputs()
        {
            if (sunTransform != null)
                sunTransform.localRotation = Quaternion.Euler(_snapshot.dayPercent * 360f - 90f, 170f, 0f);
            if (moonTransform != null)
                moonTransform.localRotation = Quaternion.Euler(_snapshot.dayPercent * 360f + 90f, 170f, 0f);

            PungentEnvironmentCalendarProfile.MonthDefinition month = calendarProfile != null ? calendarProfile.GetMonth(_date.monthIndex) : null;
            if (seasonalTiltTransform != null && month != null)
                seasonalTiltTransform.localRotation = Quaternion.Euler(0f, 0f, month.seasonalSunTilt);

            if (skyProfile != null)
                skyProfile.Apply(_snapshot, sunLight, moonLight);
        }

        private void RaiseTimeChanged(PungentClockSnapshot snapshot)
        {
            TimeChanged?.Invoke(snapshot);
            onTimeChanged.Invoke(snapshot);
        }

        private void RaiseHourChanged(PungentClockSnapshot snapshot)
        {
            HourChanged?.Invoke(snapshot);
            onHourChanged.Invoke(snapshot);
        }

        private void RaiseDayChanged(PungentClockSnapshot snapshot)
        {
            DayChanged?.Invoke(snapshot);
            onDayChanged.Invoke(snapshot);
        }

        private void RaiseMonthChanged(PungentClockSnapshot snapshot)
        {
            MonthChanged?.Invoke(snapshot);
            onMonthChanged.Invoke(snapshot);
        }

        private void RaiseSeasonChanged(string season)
        {
            SeasonChanged?.Invoke(season);
            onSeasonChanged.Invoke(season);
        }

        private void RaiseYearChanged(PungentClockSnapshot snapshot)
        {
            YearChanged?.Invoke(snapshot);
            onYearChanged.Invoke(snapshot);
        }
    }

}
