using System;
using UnityEngine;

namespace PungentFunk.Utilities.EnvironmentSimulation
{
    public enum PungentWeekday
    {
        Monday,
        Tuesday,
        Wednesday,
        Thursday,
        Friday,
        Saturday,
        Sunday
    }

    [Serializable]
    public struct PungentCalendarDate : IEquatable<PungentCalendarDate>
    {
        public int year;
        public int monthIndex;
        public int dayOfMonth;
        public PungentWeekday weekday;
        public int dayOfYear;

        public PungentCalendarDate(int year, int monthIndex, int dayOfMonth, PungentWeekday weekday, int dayOfYear)
        {
            this.year = year;
            this.monthIndex = Mathf.Max(0, monthIndex);
            this.dayOfMonth = Mathf.Max(1, dayOfMonth);
            this.weekday = weekday;
            this.dayOfYear = Mathf.Max(0, dayOfYear);
        }

        public bool Equals(PungentCalendarDate other)
        {
            return year == other.year &&
                   monthIndex == other.monthIndex &&
                   dayOfMonth == other.dayOfMonth &&
                   weekday == other.weekday &&
                   dayOfYear == other.dayOfYear;
        }

        public override bool Equals(object obj) => obj is PungentCalendarDate other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + year;
                hash = hash * 31 + monthIndex;
                hash = hash * 31 + dayOfMonth;
                hash = hash * 31 + (int)weekday;
                hash = hash * 31 + dayOfYear;
                return hash;
            }
        }
    }

    [Serializable]
    public struct PungentClockSnapshot
    {
        public PungentCalendarDate date;
        public float hour24;
        public int hour;
        public int minute;
        public int second;
        public float dayPercent;
        public string monthName;
        public string seasonName;
        public float sunriseHour;
        public float sunsetHour;
        public float seasonPercent;

        public string TimeLabel24 => $"{hour:00}:{minute:00}:{second:00}";
        public string DateLabel => string.IsNullOrEmpty(monthName)
            ? $"{date.weekday}, Month {date.monthIndex + 1} {date.dayOfMonth}, {date.year}"
            : $"{date.weekday}, {monthName} {date.dayOfMonth}, {date.year}";
    }

    [CreateAssetMenu(menuName = "PungentFunk/Environment Simulation/Calendar Profile", fileName = "Pungent Calendar Profile")]
    public sealed class PungentEnvironmentCalendarProfile : ScriptableObject
    {
        [Serializable]
        public sealed class MonthDefinition
        {
            public string name = "January";
            [Min(1)] public int days = 31;
            public string season = "Winter";
            [Range(0f, 24f)] public float sunriseHour = 7f;
            [Range(0f, 24f)] public float sunsetHour = 18f;
            [Range(-45f, 45f)] public float seasonalSunTilt = 0f;
        }

        [SerializeField] private PungentWeekday firstDayOfYear = PungentWeekday.Monday;
        [SerializeField] private MonthDefinition[] months =
        {
            new MonthDefinition { name = "January", days = 31, season = "Winter", sunriseHour = 7.5f, sunsetHour = 17.5f, seasonalSunTilt = -20f },
            new MonthDefinition { name = "February", days = 28, season = "Winter", sunriseHour = 7f, sunsetHour = 18f, seasonalSunTilt = -14f },
            new MonthDefinition { name = "March", days = 31, season = "Spring", sunriseHour = 6.5f, sunsetHour = 18.5f, seasonalSunTilt = -4f },
            new MonthDefinition { name = "April", days = 30, season = "Spring", sunriseHour = 6f, sunsetHour = 19f, seasonalSunTilt = 6f },
            new MonthDefinition { name = "May", days = 31, season = "Spring", sunriseHour = 5.5f, sunsetHour = 19.5f, seasonalSunTilt = 14f },
            new MonthDefinition { name = "June", days = 30, season = "Summer", sunriseHour = 5f, sunsetHour = 20f, seasonalSunTilt = 22f },
            new MonthDefinition { name = "July", days = 31, season = "Summer", sunriseHour = 5.25f, sunsetHour = 20f, seasonalSunTilt = 20f },
            new MonthDefinition { name = "August", days = 31, season = "Summer", sunriseHour = 5.75f, sunsetHour = 19.25f, seasonalSunTilt = 14f },
            new MonthDefinition { name = "September", days = 30, season = "Autumn", sunriseHour = 6.25f, sunsetHour = 18.5f, seasonalSunTilt = 4f },
            new MonthDefinition { name = "October", days = 31, season = "Autumn", sunriseHour = 6.75f, sunsetHour = 17.75f, seasonalSunTilt = -6f },
            new MonthDefinition { name = "November", days = 30, season = "Autumn", sunriseHour = 7.25f, sunsetHour = 17.25f, seasonalSunTilt = -14f },
            new MonthDefinition { name = "December", days = 31, season = "Winter", sunriseHour = 7.75f, sunsetHour = 17f, seasonalSunTilt = -22f }
        };

        public PungentWeekday FirstDayOfYear => firstDayOfYear;
        public int MonthCount => months == null ? 0 : months.Length;
        public int DaysInYear
        {
            get
            {
                int total = 0;
                if (months == null)
                    return 0;

                for (int i = 0; i < months.Length; i++)
                    total += Mathf.Max(1, months[i] != null ? months[i].days : 30);
                return total;
            }
        }

        public MonthDefinition GetMonth(int monthIndex)
        {
            if (months == null || months.Length == 0)
                return null;

            return months[Mathf.Clamp(monthIndex, 0, months.Length - 1)];
        }

        public PungentCalendarDate NormalizeDate(int year, int monthIndex, int dayOfMonth)
        {
            if (months == null || months.Length == 0)
                return new PungentCalendarDate(year, 0, Mathf.Max(1, dayOfMonth), firstDayOfYear, 0);

            monthIndex = Mathf.Clamp(monthIndex, 0, months.Length - 1);
            MonthDefinition month = GetMonth(monthIndex);
            int safeDay = Mathf.Clamp(dayOfMonth, 1, Mathf.Max(1, month != null ? month.days : 30));
            int dayOfYear = GetDayOfYear(monthIndex, safeDay);
            return new PungentCalendarDate(year, monthIndex, safeDay, GetWeekday(dayOfYear), dayOfYear);
        }

        public int GetDayOfYear(int monthIndex, int dayOfMonth)
        {
            int total = 0;
            if (months == null)
                return Mathf.Max(0, dayOfMonth - 1);

            for (int i = 0; i < Mathf.Clamp(monthIndex, 0, months.Length); i++)
                total += Mathf.Max(1, months[i] != null ? months[i].days : 30);

            return Mathf.Clamp(total + Mathf.Max(1, dayOfMonth) - 1, 0, Mathf.Max(0, DaysInYear - 1));
        }

        public PungentWeekday GetWeekday(int dayOfYear)
        {
            int index = Mathf.FloorToInt(Mathf.Repeat((int)firstDayOfYear + Mathf.Max(0, dayOfYear), 7f));
            return (PungentWeekday)index;
        }

        public PungentCalendarDate AdvanceDays(PungentCalendarDate date, int deltaDays)
        {
            if (months == null || months.Length == 0)
                return new PungentCalendarDate(date.year, 0, Mathf.Max(1, date.dayOfMonth + deltaDays), GetWeekday(date.dayOfYear + deltaDays), Mathf.Max(0, date.dayOfYear + deltaDays));

            int year = date.year;
            int absoluteDay = GetDayOfYear(date.monthIndex, date.dayOfMonth) + deltaDays;
            int daysInYear = Mathf.Max(1, DaysInYear);

            while (absoluteDay < 0)
            {
                year--;
                absoluteDay += daysInYear;
            }

            while (absoluteDay >= daysInYear)
            {
                year++;
                absoluteDay -= daysInYear;
            }

            int running = 0;
            for (int i = 0; i < months.Length; i++)
            {
                int days = Mathf.Max(1, months[i] != null ? months[i].days : 30);
                if (absoluteDay < running + days)
                    return new PungentCalendarDate(year, i, absoluteDay - running + 1, GetWeekday(absoluteDay), absoluteDay);
                running += days;
            }

            return NormalizeDate(year, months.Length - 1, Mathf.Max(1, months[months.Length - 1].days));
        }
    }
}
