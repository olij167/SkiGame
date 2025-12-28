using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace TimeWeather
{

    [CustomEditor(typeof(TimeDisplay))]
    public class TimeDisplayEditor : Editor
    {

        //TIME DISPLAY
        //set to a specific date
        // calculate day of week



        TimeController timeController;
        string timeString;
        float timeScale;
        //string twelveHourTimeString;

        int dayOfMonth;
        TimeController.MonthData month;
        int year;

        int daysToPass;
        int monthsToPass;

        bool showSeconds;
        bool twelveHourTime;


        // bool progressDay, regressDay;

        // graph with points representing sunrise and sunset times

        public override void OnInspectorGUI()
        {
            TimeDisplay timeDisplay = (TimeDisplay)target;
            if (timeDisplay == null) return;
            if (timeController == null) timeController = timeDisplay.transform.GetComponent<TimeController>();

            float timeOfDay = timeController.timeOfDay;
            timeScale = timeController.secondsPerMinuteInGame;

            twelveHourTime = timeController.twelveHourTime;
            showSeconds = timeController.showSeconds;

            timeString = SetTimeString(timeOfDay);

            EditorGUILayout.LabelField(timeString + ", " + timeController.currentDay.ToString() + ", " + timeController.currentMonthData.month + " " +
            timeController.dayOfMonth.ToString() + ", " + timeController.currentMonthData.season + ", " + timeController.currentYear, EditorStyles.boldLabel);

            DrawUILine(new Color(1f, 1f, 1f, 0.25f));
            EditorGUILayout.Space(5);

            GUILayout.Label("Change Time ");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Current Time ");
            if (timeController.toggleUITimeControls) timeController.timeOfDaySlider.value = EditorGUILayout.Slider(timeOfDay, 0f, 23.99f);
            else timeController.timeOfDay = EditorGUILayout.Slider(timeOfDay, 0f, 23.99f);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Time Scale ");
            if (timeController.toggleUITimeControls) timeController.timeScaleSlider.value = EditorGUILayout.Slider(timeScale, 0.001f, 60f);
            else timeController.secondsPerMinuteInGame = EditorGUILayout.Slider(timeScale, 0.001f, 60f);
            GUILayout.EndHorizontal();

            //if (timeController.timeOfDay < 0.01f)
            //{
            //    timeController.RegressDays();
            //    timeController.timeOfDay = 23.99f;
            //}
            //else if (timeController.timeOfDay > 23.99f)
            //{
            //    timeController.ProgressDays();
            //    timeController.timeOfDay = 0.01f;
            //}

            GUILayout.BeginHorizontal();

            GUILayout.Label("Display Twelve Hour Time ");
            twelveHourTime = EditorGUILayout.Toggle(twelveHourTime);
            timeController.twelveHourTime = twelveHourTime;
            GUILayout.Label("Display Seconds ");
            showSeconds = EditorGUILayout.Toggle(showSeconds);
            timeController.showSeconds = showSeconds;

            EditorGUILayout.EndHorizontal();

            DrawUILine(new Color(1f, 1f, 1f, 0.25f));
            EditorGUILayout.Space(5);

            GUILayout.Label("Change Day & Month");

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Progress Days"))
            {
                timeController.ProgressDays(daysToPass);
            }

            if (daysToPass == 0) daysToPass = 1;
            daysToPass = EditorGUILayout.IntField(daysToPass);

            if (GUILayout.Button("Regress Days"))
            {
                timeController.RegressDays(daysToPass);
            }

            EditorGUILayout.EndHorizontal();


            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Progress Months"))
            {
                timeController.ProgressMonth(monthsToPass);
            }

            if (monthsToPass == 0) monthsToPass = 1;
            monthsToPass = EditorGUILayout.IntField(monthsToPass);

            if (GUILayout.Button("Regress Months"))
            {
                timeController.RegressMonth(monthsToPass);
            }

            EditorGUILayout.EndHorizontal();
            DrawUILine(new Color(1f, 1f, 1f, 0.25f));
            EditorGUILayout.Space(5);

            GUILayout.Label("Set Specific Date");

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Date ");
            if (dayOfMonth > 0)
                dayOfMonth = EditorGUILayout.IntField(dayOfMonth);
            else
                dayOfMonth = timeController.dayOfMonth;

            GUILayout.Label("Month ");
            if (month != null)
                month.month = EditorGUILayout.TextField(month.month);
            else
            {
                month = new TimeController.MonthData();
                month.month = timeController.currentMonthData.month;
                month.season = timeController.currentMonthData.season;
                month.daysInMonth = timeController.currentMonthData.daysInMonth;
            }
            GUILayout.Label("Year ");
            if (year > 0 || timeController.currentYear == 0)
                year = EditorGUILayout.IntField(year);
            else year = timeController.currentYear;

            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Set Date"))
            {
                if (timeController.currentYear != year)
                {
                    timeController.currentYear = year;
                }

                if (month == null) Debug.LogError("Please enter a month to change to from 'MonthPresets' in the 'TimeController' script");
                else if (month.month != timeController.currentMonthData.month)
                {
                    for (int i = 0; i < timeController.monthPresets.Length; i++)
                    {
                        if (month.month == timeController.monthPresets[i].month)
                        {
                            timeController.currentMonthData = timeController.monthPresets[i];
                            break;
                        }

                        else if (i + 1 >= timeController.monthPresets.Length)
                        {
                            Debug.LogError("Please enter a month to change to from 'MonthPresets' in the 'TimeController' script");
                        }
                    }
                }

                if (timeController.dayOfMonth != dayOfMonth)
                {
                    if (timeController.dayOfMonth < dayOfMonth)
                    {
                        if (timeController.dayOfMonth + (dayOfMonth - timeController.dayOfMonth) > timeController.currentMonthData.daysInMonth)
                        {
                            timeController.ProgressDays(timeController.currentMonthData.daysInMonth - timeController.dayOfMonth);
                        }
                        else
                        {
                            timeController.ProgressDays(dayOfMonth - timeController.dayOfMonth);
                        }
                    }
                    else
                    {
                        timeController.RegressDays(timeController.dayOfMonth - dayOfMonth);
                    }
                }
            }

        }

        string SetTimeString(float timeOfDay)
        {
            float timeHours = (int)timeOfDay;
            float timeMinutes = Mathf.Clamp((timeOfDay - timeHours) * 60, 0f, 59.49f);
            float timeSeconds = Mathf.Clamp((timeMinutes - (int)timeMinutes) * 60, 0f, 59.49f);

            if (twelveHourTime)
            {
                string twelveHourTimeString;

                if (timeOfDay < 12)
                {
                    if (showSeconds)
                        timeString = timeHours.ToString("00") + ":" + timeMinutes.ToString("00") + ":" + timeSeconds.ToString("00");
                    else
                        timeString = timeHours.ToString("00") + ":" + timeMinutes.ToString("00");

                    return twelveHourTimeString = timeString + " am";

                }
                else if (timeOfDay > 12 && timeOfDay < 13)
                {
                    if (showSeconds)
                        timeString = timeHours.ToString("00") + ":" + timeMinutes.ToString("00") + ":" + timeSeconds.ToString("00");
                    else
                        timeString = timeHours.ToString("00") + ":" + timeMinutes.ToString("00");

                    return twelveHourTimeString = timeString + " pm";
                }
                else
                {
                    if (showSeconds)
                        timeString = (timeHours - 12).ToString("00") + ":" + timeMinutes.ToString("00") + ":" + timeSeconds.ToString("00");
                    else
                        timeString = (timeHours - 12).ToString("00") + ":" + timeMinutes.ToString("00");

                    return twelveHourTimeString = timeString + " pm";

                }
            }
            else
            {
                if (showSeconds)
                    return timeString = timeHours.ToString("00") + ":" + timeMinutes.ToString("00") + ":" + timeSeconds.ToString("00");
                else
                    return timeString = timeHours.ToString("00") + ":" + timeMinutes.ToString("00");
            }
        }

        public static void DrawUILine(Color color, int thickness = 2, int padding = 10)
        {
            Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(padding + thickness));
            r.height = thickness;
            r.y += padding / 2;
            r.x -= 2;
            r.width += 6;
            EditorGUI.DrawRect(r, color);
        }
    }
}
