using System;
using UnityEngine;
using UnityEngine.Events;

namespace PungentFunk.Utilities.EnvironmentSimulation
{
    public enum PungentTimeSequenceRepeatMode
    {
        Once,
        Repeating
    }

    [CreateAssetMenu(menuName = "PungentFunk/Environment Simulation/Time Sequence Rule", fileName = "Pungent Time Sequence Rule")]
    public sealed class PungentTimeSequenceRule : ScriptableObject
    {
        public string ruleId = "time.sequence";
        public PungentTimeSequenceRepeatMode repeatMode = PungentTimeSequenceRepeatMode.Repeating;
        [Range(0f, 24f)] public float hour = 8f;
        public bool matchWeekday;
        public PungentWeekday weekday;
        public bool matchMonth;
        public int monthIndex;
        public bool matchDayOfMonth;
        [Min(1)] public int dayOfMonth = 1;
        public bool matchSeason;
        public string season = "Winter";
        public bool matchYearRange;
        public int firstYear = 1;
        public int lastYear = 9999;
        [Min(1)] public int intervalDays = 1;

        public bool Matches(PungentClockSnapshot snapshot, int lastTriggeredDayOfYear, int lastTriggeredYear)
        {
            if (matchWeekday && snapshot.date.weekday != weekday)
                return false;
            if (matchMonth && snapshot.date.monthIndex != monthIndex)
                return false;
            if (matchDayOfMonth && snapshot.date.dayOfMonth != dayOfMonth)
                return false;
            if (matchSeason && !string.Equals(snapshot.seasonName, season, StringComparison.OrdinalIgnoreCase))
                return false;
            if (matchYearRange && (snapshot.date.year < firstYear || snapshot.date.year > lastYear))
                return false;
            if (repeatMode == PungentTimeSequenceRepeatMode.Once && lastTriggeredYear != int.MinValue)
                return false;

            if (intervalDays > 1)
            {
                if (lastTriggeredYear != int.MinValue)
                {
                    int absoluteDay = snapshot.date.year * 10000 + snapshot.date.dayOfYear;
                    int lastAbsoluteDay = lastTriggeredYear * 10000 + lastTriggeredDayOfYear;
                    if (absoluteDay - lastAbsoluteDay < intervalDays)
                        return false;
                }
            }

            return true;
        }

        public bool HasSameTriggerShape(PungentTimeSequenceRule other)
        {
            if (other == null)
                return false;

            return Mathf.Approximately(hour, other.hour) &&
                   repeatMode == other.repeatMode &&
                   matchWeekday == other.matchWeekday &&
                   (!matchWeekday || weekday == other.weekday) &&
                   matchMonth == other.matchMonth &&
                   (!matchMonth || monthIndex == other.monthIndex) &&
                   matchDayOfMonth == other.matchDayOfMonth &&
                   (!matchDayOfMonth || dayOfMonth == other.dayOfMonth) &&
                   matchSeason == other.matchSeason &&
                   (!matchSeason || string.Equals(season, other.season, StringComparison.OrdinalIgnoreCase)) &&
                   matchYearRange == other.matchYearRange &&
                   (!matchYearRange || (firstYear == other.firstYear && lastYear == other.lastYear)) &&
                   intervalDays == other.intervalDays;
        }
    }

    [Serializable] public sealed class PungentTimeSequenceUnityEvent : UnityEvent<PungentClockSnapshot> { }

    [DisallowMultipleComponent]
    [AddComponentMenu("PungentFunk/Environment Simulation/Pungent Time Sequence Listener")]
    public sealed class PungentTimeSequenceListener : MonoBehaviour
    {
        [SerializeField] private PungentClockController clock;
        [SerializeField] private PungentTimeSequenceRule rule;
        [SerializeField] private bool triggerWhenCrossingHour = true;
        [SerializeField] private bool fireMissedEventsOnSkippedTime = true;
        [SerializeField] private PungentTimeSequenceUnityEvent onTriggered = new PungentTimeSequenceUnityEvent();

        private int _lastTriggeredDayOfYear = int.MinValue;
        private int _lastTriggeredYear = int.MinValue;
        private PungentClockSnapshot _previous;
        private bool _hasPrevious;

        public event Action<PungentClockSnapshot> Triggered;
        public PungentClockController Clock => clock;
        public PungentTimeSequenceRule Rule => rule;
        public bool TriggerWhenCrossingHour => triggerWhenCrossingHour;
        public bool FireMissedEventsOnSkippedTime => fireMissedEventsOnSkippedTime;
        public bool HasTriggered => _lastTriggeredYear != int.MinValue;
        public int LastTriggeredDayOfYear => _lastTriggeredDayOfYear;
        public int LastTriggeredYear => _lastTriggeredYear;
        public PungentTimeSequenceUnityEvent OnTriggered => onTriggered;

        private void OnEnable()
        {
            if (clock == null)
                clock = FindAnyObjectByType<PungentClockController>();

            if (clock != null)
            {
                _previous = clock.Snapshot;
                _hasPrevious = true;
                clock.TimeChanged += HandleClockChanged;
            }
        }

        private void OnDisable()
        {
            if (clock != null)
                clock.TimeChanged -= HandleClockChanged;
        }

        private void HandleClockChanged(PungentClockSnapshot snapshot)
        {
            if (rule == null)
                return;

            bool allowMissedEvents = fireMissedEventsOnSkippedTime && (clock == null || clock.LastAdvanceFiredMissedSequenceEvents);
            bool crossed = !_hasPrevious || !triggerWhenCrossingHour || CrossedHour(_previous, snapshot, rule.hour, allowMissedEvents);
            _previous = snapshot;
            _hasPrevious = true;

            if (!crossed || !rule.Matches(snapshot, _lastTriggeredDayOfYear, _lastTriggeredYear))
                return;

            _lastTriggeredDayOfYear = snapshot.date.dayOfYear;
            _lastTriggeredYear = snapshot.date.year;
            Triggered?.Invoke(snapshot);
            onTriggered.Invoke(snapshot);
        }

        private static bool CrossedHour(PungentClockSnapshot previous, PungentClockSnapshot current, float targetHour, bool fireMissedEvents)
        {
            if (!previous.date.Equals(current.date))
            {
                if (fireMissedEvents)
                    return true;
                return previous.hour24 <= targetHour || current.hour24 >= targetHour;
            }

            return previous.hour24 < targetHour && current.hour24 >= targetHour;
        }
    }
}
