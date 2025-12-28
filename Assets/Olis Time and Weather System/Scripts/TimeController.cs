using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
//using UnityEditor;

namespace TimeWeather
{

#if UNITY_EDITOR
    [RequireComponent(typeof(TimeDisplay))]
#endif
    public class TimeController : MonoBehaviour
    {
        public static TimeController instance;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        [System.Serializable] public enum Day { Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday }

        [System.Serializable]
        public class MonthData
        {
            [Tooltip("The name of the month")]
            public string month;
            [Tooltip("The name of the season")]
            public string season;
            [Tooltip("The number of days in the month")]
            public int daysInMonth;
        }

        [Header("Time")]
        [Tooltip("How many real-world seconds it takes for an in-game minute to pass")]
        [Range(0.001f, 60f)] public float secondsPerMinuteInGame = 0.5f; //length of the day in minutes
        [HideInInspector] public float timeScale = 100f;
        [Tooltip("The current in-game time")]
        [Range(0, 24)] public float timeOfDay;
        [HideInInspector] public int timeHours;
        [HideInInspector] public float timeMinutes;
        [HideInInspector] public float timeSeconds;

        [HideInInspector, Range(0, 1)] public float timePercent;

        [Tooltip("The current hours progression as a value between 0 and 1")]
        [Range(0, 1)] public float hourlyTimePercent;

        [Space(10)]

        [Tooltip("The time the sun will rise. Requires a 'WeatherController' in the scene to change")]
        [field: ReadOnlyField] public float sunriseTime;
        [HideInInspector, Range(0, 1)] public float sunriseTimePercent;

        [Tooltip("The time the sun will rise. Requires a 'WeatherController' in the scene to change")]
        [field: ReadOnlyField] public float sunsetTime;
        [HideInInspector, Range(0, 1)] public float sunsetTimePercent;

        [Header("Day")]
        [Tooltip("The current day")]
        public Day currentDay = Day.Sunday;
        [Tooltip("The current date")]
        public int dayOfMonth = 1;

        [Header("Month")]
        [Tooltip("The current month's variables")]
        public MonthData currentMonthData;

        [Tooltip("An array of each month in the year. Set the months of the year here. In-game year length is based on the sum of each element's 'daysInMonth' variable")]
        public MonthData[] monthPresets;

        [Tooltip("The transform responsible for tilting the sun throughout the year")]
        public Transform seasonalRotation;

        [Tooltip("The range the sun will tilt throughout the year")]
        [Range(-45f, 45f)] public float maxSeasonalTilt;

        [Tooltip("The current year")]
        public int currentYear;
        private int daysInYear;

        [Tooltip("The total number of days which have passed in the current year")]
        [Range(0, 365)] public int dayCount = 0;

        [Header("Sky Objects")]
        [Tooltip("The variables which controll the skybox throughout the day")]
        public SkyData skyData;
        [Tooltip("The sun's light component")]
        public Light sunLight;
        [Tooltip("The moon's light component")]
        [SerializeField] private Light moonLight;
        [Tooltip("The moon object")]
        [SerializeField] private GameObject moon;

        private WeatherController weatherController;


        [Header("UI")]
        [Tooltip("Toggle whether to show time UI elements")]
        public bool toggleTimeUI;
        [Tooltip("Toggle whether to show seconds on the UI")]
        public bool showSeconds = true;
        [Tooltip("Toggle whether to show 12 or 24 hour time")]
        public bool twelveHourTime;
        private bool isNewDay;

        [Tooltip("Text for displaying the in-game clock")]
        public TextMeshProUGUI timeText;
        [Tooltip("Text for displaying the day, month, season, and year")]
        public TextMeshProUGUI dayText;
        [HideInInspector] public string timeString;

        [Tooltip("Toggle the ability to control time in-game")]
        public bool toggleUITimeControls;
        private GameObject timeControlsParent;
        [Tooltip("A slider for controlling the speed that time passes during runtime")]
        public UnityEngine.UI.Slider timeScaleSlider;
        [Tooltip("A slider for controlling the time of day during runtime")]
        public UnityEngine.UI.Slider timeOfDaySlider;

        [Tooltip("Toggle whether to show sunrise and set time UI")]
        public bool toggleSunTimeUI;
        [Tooltip("Text for displaying the current day's sunrise time")]
        public TextMeshProUGUI sunriseText;
        [Tooltip("Text for displaying the current day's sunrise time")]
        public TextMeshProUGUI sunsetText;

        public bool showDebugLogs;


        [Header("Debug")]
        private bool useSmoothLerp = true;
        [Range(0, 10)] public float smoothLerp = 0.5f;
        private float GetSmoothLerp { get { if (Application.isPlaying && useSmoothLerp) return smoothLerp * Time.unscaledDeltaTime; else return 1; } }

        [Range(0, 1)] private float dayLightTime;
        [Range(0, 1)] private float nightLightTime;
        [Range(0, 1)] private float fadeToNight;
        [Range(0, 1)] private float smoothFadeNight;

        //public Calendar calendar;

        [System.Serializable]
        public struct SkyData
        {
            //[Tooltip("The skybox material")]
            //public Material skyBoxMat;
            [Tooltip("The sun's intesity")]
            public float lightIntensity;// = 1.3f;
            [Tooltip("The moon's intensity")]
            public float moonLightIntensity;// = 1;
            [Space()]
            [Tooltip("A curve representing the sun's scale throughout the day")]
            public AnimationCurve sunSizeOverTime;
            [Tooltip("A curve representing the sun's light intensity throughout the day")]
            public AnimationCurve sunIntensityCurve;
            [Space()]
            [Tooltip("A gradient for the colour of the top of the skybox throughout the day")]
            public Gradient dayTopColorOverTime;
            [Tooltip("A gradient for the colour of the middle of the skybox throughout the day")]
            public Gradient dayMidColorOverTime;

            [Space()]
            [Tooltip("A gradient for the colour of the top of the skybox throughout the night")]
            public Gradient nightTopColorOverTime;
            [Tooltip("A gradient for the colour of the middle of the skybox throughout the night")]
            public Gradient nightMidColorOverTime;

            [Tooltip("A gradient for the ambient atmospheric colour throughout the day")]
            public Gradient ambientColour; // atmospheric colour - set based on weather
            [Tooltip("A gradient for the fog colour throughout the day")]
            public Gradient fogColour; // set based on weather
            [Tooltip("Fog transition smoothing")]
            [Range(0, 1)] public float fogToDayColor;

        }


        void Start()
        {
            if (WeatherController.instance != null) weatherController = WeatherController.instance;

            if (dayText != null)
                dayText.text = currentDay.ToString() + ", " + currentMonthData.month + " " + dayOfMonth + ", \n" + currentMonthData.season + ", " + currentYear;

            if (currentMonthData.month == string.Empty)
                ProgressMonth();



            for (int i = 0; i < monthPresets.Length; i++)
            {
                daysInYear += monthPresets[i].daysInMonth;
            }
            if (showDebugLogs) Debug.Log("Days in year " + daysInYear);

            //SetCalendar();


            if (timeScaleSlider != null)
            {
                timeControlsParent = timeScaleSlider.transform.parent.parent.gameObject;

                timeScaleSlider.minValue = 0.001f;
                timeScaleSlider.maxValue = 60f;

                timeScaleSlider.value = secondsPerMinuteInGame;
            }

            if (timeOfDaySlider != null)
            {
                timeOfDaySlider.minValue = 0f;
                timeOfDaySlider.maxValue = 24f;

                timeOfDaySlider.value = timeOfDay;
            }


            ToggleUI();
        }

        public void ToggleUI()
        {
            if (!toggleTimeUI)
            {
                if (timeText != null) timeText.gameObject.SetActive(false);
                if (dayText != null) dayText.gameObject.SetActive(false);

                toggleUITimeControls = false;
                toggleSunTimeUI = false;
            }
            else
            {
                if (timeText != null) timeText.gameObject.SetActive(true);
                if (dayText != null) dayText.gameObject.SetActive(true);
            }

            if (toggleUITimeControls)
            {
                if (timeScaleSlider != null) timeScaleSlider.value = secondsPerMinuteInGame;
                
                if (timeOfDaySlider != null)
                {
                    timeOfDaySlider.value = timeOfDay;
                    timeOfDaySlider.gameObject.SetActive(true);
                }
                if (timeControlsParent != null) timeControlsParent.SetActive(true);

            }
            else
            {
                if (timeControlsParent != null) timeControlsParent.SetActive(false);
                //if (timeScaleSlider != null) timeScaleSlider.gameObject.SetActive(false);
                if (timeOfDaySlider != null) timeOfDaySlider.gameObject.SetActive(false);
            }

            if (!toggleSunTimeUI)
            {
                if (sunriseText != null) sunriseText.gameObject.SetActive(false);
                if (sunsetText != null) sunsetText.gameObject.SetActive(false);
            }
            else
            {
                if (sunriseText != null) sunriseText.gameObject.SetActive(true);
                if (sunsetText != null) sunsetText.gameObject.SetActive(true);
            }
        }

        void Update()
        {
            if (Application.isPlaying)
            {

                if (toggleUITimeControls)
                {
                    if (timeScaleSlider != null)
                        secondsPerMinuteInGame = timeScaleSlider.value;

                    //if (timeOfDay <= 23.9f)
                    if (timeOfDaySlider != null && timeOfDaySlider.value > 0 && timeOfDaySlider.value < 24)
                    {
                        if (timeOfDaySlider.value <= 23.99f && timeOfDay <= 23.99f)
                            timeOfDay = timeOfDaySlider.value;
                        else
                            timeOfDaySlider.value = timeOfDay;
                    }
                }

                if (timeOfDaySlider != null) timeOfDaySlider.value = timeOfDay;

                timeScale = 24 / (secondsPerMinuteInGame / 60);

                timeOfDay += Time.deltaTime * timeScale / 86400; // seconds in a day

                timePercent = (timeOfDay %= 24f) / 24f;
                UpdateLighting();

                timeHours = (int)timeOfDay;
                timeMinutes = Mathf.Clamp((timeOfDay - timeHours) * 60, 0f, 59.49f);
                timeSeconds = Mathf.Clamp((timeMinutes - (int)timeMinutes) * 60, 0f, 59.49f);

                hourlyTimePercent = (timeMinutes %= 60f) / 60f;

                if (toggleTimeUI)
                {
                    if (twelveHourTime)
                    {
                        if (timeOfDay < 12)
                        {
                            if (showSeconds)
                                timeString = timeHours.ToString("00") + ":" + timeMinutes.ToString("00") + ":" + timeSeconds.ToString("00");
                            else
                                timeString = timeHours.ToString("00") + ":" + timeMinutes.ToString("00");

                            if (timeText != null)
                                timeText.text = timeString + " am";
                        }
                        else if (timeOfDay > 12 && timeOfDay < 13)
                        {
                            if (showSeconds)
                                timeString = timeHours.ToString("00") + ":" + timeMinutes.ToString("00") + ":" + timeSeconds.ToString("00");
                            else
                                timeString = timeHours.ToString("00") + ":" + timeMinutes.ToString("00");

                            if (timeText != null)
                                timeText.text = timeString + " pm";
                        }
                        else
                        {
                            if (showSeconds)
                                timeString = (timeHours - 12).ToString("00") + ":" + timeMinutes.ToString("00") + ":" + timeSeconds.ToString("00");
                            else
                                timeString = (timeHours - 12).ToString("00") + ":" + timeMinutes.ToString("00");

                            if (timeText != null)
                                timeText.text = timeString + " pm";


                            if (timeOfDay >= 23.9f)
                            {
                                isNewDay = true;
                                //timeOfDaySlider.value = 0f;
                            }
                        }
                    }
                    else
                    {
                        if (showSeconds)
                            timeString = timeHours.ToString("00") + ":" + timeMinutes.ToString("00") + ":" + timeSeconds.ToString("00");
                        else
                            timeString = timeHours.ToString("00") + ":" + timeMinutes.ToString("00");

                        if (timeText != null)
                            timeText.text = timeString;
                    }
                }

                if (isNewDay && timeOfDay < 1f)
                {
                    ProgressDays(1);
                }

                ToggleUI();
            }
        }

        public void ProgressDays(int numToProgress = 1)
        {
            for (int n = 0; n < numToProgress; n++)
            {
                if (currentDay < Day.Sunday)
                    currentDay++;
                else
                    currentDay = Day.Monday;

                dayCount++;

                if (dayOfMonth < currentMonthData.daysInMonth)
                {
                    dayOfMonth++;
                }
                else
                {
                    ProgressMonth(1);
                    dayOfMonth = 1;
                }

                if (toggleTimeUI && dayText != null)
                    dayText.text = currentDay.ToString() + ", " + currentMonthData.month + " " + dayOfMonth + ", \n" + currentMonthData.season + ", " + currentYear;
            }

            if (weatherController != null) weatherController.SetDailyConditions();

            sunriseTimePercent = (sunriseTime %= 24) / 24f;
            sunsetTimePercent = (sunsetTime %= 24) / 24f;

            isNewDay = false;
        }

        public void RegressDays(int numToProgress = 1)
        {
            for (int n = 0; n < numToProgress; n++)
            {
                if (currentDay > Day.Monday)
                    currentDay--;
                else
                    currentDay = Day.Sunday;

                dayCount--;

                if (dayOfMonth - 1 > 0)
                {
                    dayOfMonth--;
                }
                else
                {
                    RegressMonth(1);
                    dayOfMonth = currentMonthData.daysInMonth;
                }

                if (toggleTimeUI && dayText != null)
                    dayText.text = currentDay.ToString() + ", " + currentMonthData.month + " " + dayOfMonth + ", \n" + currentMonthData.season + ", " + currentYear;
            }

            if (weatherController != null) weatherController.SetDailyConditions();

            sunriseTimePercent = (sunriseTime %= 24) / 24f;
            sunsetTimePercent = (sunsetTime %= 24) / 24f;

            //isNewDay = false;

        }

        public void ProgressMonth(int numToProgress = 1)
        {
            for (int n = 0; n < numToProgress; n++)
            {
                for (int i = 0; i < monthPresets.Length; i++)
                {
                    if (currentMonthData.month == string.Empty)
                    {
                        currentMonthData = monthPresets[i];
                        if (weatherController != null)
                        {
                            weatherController.SetSeasonalConditions();
                            weatherController.SetDailyConditions();
                        }
                    }
                    else
                    if (currentMonthData.month == monthPresets[i].month)
                    {
                        if (i + 1 < monthPresets.Length)
                        {
                            bool changeSeason = false;
                            if (monthPresets[i + 1].season != currentMonthData.season) changeSeason = true;
                            currentMonthData = monthPresets[i + 1];
                            if (weatherController != null && changeSeason) weatherController.SetSeasonalConditions();
                            break;
                        }
                        else
                        {
                            bool changeSeason = false;
                            if (monthPresets[0].season != currentMonthData.season) changeSeason = true;
                            currentMonthData = monthPresets[0];
                            currentYear += 1;
                            dayCount = 0;
                            if (weatherController != null && changeSeason) weatherController.SetSeasonalConditions();
                            break;
                        }
                    }
                }
            }

            if (dayOfMonth > currentMonthData.daysInMonth)
                dayOfMonth = currentMonthData.daysInMonth;

            ProgressDays(0);

            if (toggleTimeUI && dayText != null)
                dayText.text = currentDay.ToString() + ", " + currentMonthData.month + " " + dayOfMonth + ", \n" + currentMonthData.season + ", " + currentYear;
        }
        public void RegressMonth(int numToProgress = 1)
        {

            for (int n = 0; n < numToProgress; n++)
            {
                for (int i = 0; i < monthPresets.Length; i++)
                {
                    dayCount += monthPresets[i].daysInMonth;

                    if (currentMonthData.month == string.Empty)
                    {
                        currentMonthData = monthPresets[i];
                        if (weatherController != null)
                        {
                            weatherController.SetSeasonalConditions();
                            weatherController.SetDailyConditions();
                        }

                        break;
                    }
                    else
                    if (currentMonthData.month == monthPresets[i].month)
                    {
                        if (i - 1 > -1)
                        {
                            bool changeSeason = false;
                            if (monthPresets[i - 1].season != currentMonthData.season) changeSeason = true;
                            currentMonthData = monthPresets[i - 1];
                            if (weatherController != null && changeSeason) weatherController.SetSeasonalConditions();
                            break;
                        }
                        else
                        {
                            bool changeSeason = false;
                            if (monthPresets[^1].season != currentMonthData.season) changeSeason = true;

                            currentMonthData = monthPresets[^1];
                            currentYear -= 1;

                            if (weatherController != null && changeSeason) weatherController.SetSeasonalConditions();

                            break;

                        }
                    }

                }
            }

            if (dayOfMonth > currentMonthData.daysInMonth)
                dayOfMonth = currentMonthData.daysInMonth;

            RegressDays(0);

            if (toggleTimeUI && dayText != null)
                dayText.text = currentDay.ToString() + ", " + currentMonthData.month + " " + dayOfMonth + ", \n" + currentMonthData.season + ", " + currentYear;
        }

        // FINISH IMPLEMENTING CALENDAR EVENTS

        //[System.Serializable]
        //public class Calendar
        //{
        //    public List<CalendarMonth> months;
        //}

        //[System.Serializable]
        //public class CalendarMonth
        //{
        //    public MonthData month;
        //    public List<CalendarDay> days;
        //}

        //[System.Serializable]
        //public class CalendarDay
        //{
        //    public Day day;
        //    public int date;
        //    // public event info triggered at specific time on a specific day
        //    //Debug.log("Day!");
        //}

        //public Calendar SetCalendar()
        //{
        //    calendar = new Calendar();
        //    calendar.months = new List<CalendarMonth>();
        //    for (int i = 0; i < monthPresets.Length; i++)
        //    {
        //        CalendarMonth newMonth = new CalendarMonth();
        //        newMonth.month = monthPresets[i];
        //        calendar.months.Add(newMonth);
        //        newMonth.days = new List<CalendarDay>();
        //        for (int d = 0; d < newMonth.month.daysInMonth; d++)
        //        {
        //            CalendarDay newDay = new CalendarDay();
        //            newDay.date = d + 1;
        //            newDay.date = d + 1;
        //            newMonth.days.Add(newDay);
        //        }
        //    }

        //    return calendar;
        //}

        private void UpdateLighting()
        {
            RenderSettings.ambientLight = skyData.ambientColour.Evaluate(timePercent);
            sunLight.intensity = skyData.sunIntensityCurve.Evaluate(timePercent);

            if (sunLight != null)
            {
                sunLight.transform.localRotation = Quaternion.Euler(new Vector3((timePercent * 360) - 90f, 170f, 0));
            }


            float seasonalAngle = -maxSeasonalTilt * Mathf.Cos(dayCount / (float)daysInYear * 2 * Mathf.PI);
            seasonalRotation.localRotation = Quaternion.Euler(new Vector3(0f, 0f, seasonalAngle));

            float dayLength = sunsetTimePercent - sunriseTimePercent;
            float nightLength = 1 - dayLength;

            //day night cal split
            if (timePercent > sunriseTimePercent && timePercent < sunsetTimePercent)
            {
                dayLightTime = (timePercent - sunriseTimePercent) / dayLength;

                // if both dayLightTime & nightLightTime are at 0
                if (dayLightTime == 0) nightLightTime = 1;
                else nightLightTime = 0;
            }
            else
            {
                if (timeOfDay < sunriseTime) // swap direction at midnight
                {
                    nightLightTime = 1 - ((sunriseTimePercent - timePercent) / nightLength);
                }
                else if (timeOfDay > sunsetTime)
                {
                    nightLightTime = 1 - ((timePercent - sunsetTimePercent) / nightLength);
                }

                dayLightTime = 0;
            }

            SkyOnUpdate();
        }

        private void OnValidate()
        {
            if (sunLight != null)
                return;

            if (RenderSettings.sun != null)
            {
                sunLight = RenderSettings.sun;
            }
            else
            {
                Light fallbackLight = FindFirstObjectByType<Light>();

                if (fallbackLight != null)
                {
                    sunLight = fallbackLight;
                }
            }
        }

        public void SkyOnUpdate()
        {
            //visuals

            float _fadeIntensity = Mathf.Lerp(0, 1, fadeToNight);
            float _fadeIntensity_invert = Mathf.Lerp(1, 0, fadeToNight);

            //fade sun light
            bool AtDay = (fadeToNight < 1);
            sunLight.enabled = AtDay;

            float L_SunLightIntensity = Mathf.Lerp(sunLight.intensity, skyData.lightIntensity, GetSmoothLerp) * _fadeIntensity_invert;
            sunLight.intensity = L_SunLightIntensity;

            //fade moon light
            bool AtNight = (fadeToNight > 0);
            moonLight.enabled = AtNight;

            float L_MoonLightIntensity = Mathf.Lerp(moonLight.intensity, skyData.moonLightIntensity, GetSmoothLerp) * _fadeIntensity;
            moonLight.intensity = L_MoonLightIntensity;

            SkyColor();
            moon.SetActive(true);

        }

        //[Header("mat properties")]
        private string _topColor = "_SkyColor1";
        private string _midColor = "_SkyColor2";
        private string _bottomColor = "_SkyColor3";
        private string _nightOpacity = "_NightOpacity";
        private string _sunScale = "_SunScale";
        private string _sunIntensity = "_SunIntensity";
        public void SkyColor()
        {
            SmoothNight();
            //change direcrtional color on light over time

            sunLight.color = Color.Lerp(sunLight.color, skyData.dayMidColorOverTime.Evaluate(dayLightTime), GetSmoothLerp);
            moonLight.color = Color.Lerp(moonLight.color, skyData.nightMidColorOverTime.Evaluate(nightLightTime), GetSmoothLerp);
            Color _timeFog;
            //Color _weatherFog;

            if (dayLightTime > 0)
            {
                //_topColor
                if (RenderSettings.skybox.HasProperty(_topColor))
                {
                    Color L_topColor = Color.Lerp(RenderSettings.skybox.GetColor(_topColor), (skyData.dayTopColorOverTime.Evaluate(dayLightTime)), GetSmoothLerp);
                    RenderSettings.skybox.SetColor(_topColor, L_topColor);
                }
                //_midColor
                if (RenderSettings.skybox.HasProperty(_midColor))
                {
                    Color L_midColor = Color.Lerp(RenderSettings.skybox.GetColor(_midColor), (skyData.dayMidColorOverTime.Evaluate(dayLightTime)), GetSmoothLerp);
                    RenderSettings.skybox.SetColor(_midColor, L_midColor);
                }

                _timeFog = Color.Lerp(skyData.fogColour.Evaluate(dayLightTime), skyData.dayMidColorOverTime.Evaluate(dayLightTime), skyData.fogToDayColor);
            }
            else
            {
                //_topColor
                if (RenderSettings.skybox.HasProperty(_topColor))
                {
                    Color L_topColor = Color.Lerp(RenderSettings.skybox.GetColor(_topColor), (skyData.nightTopColorOverTime.Evaluate(nightLightTime)), GetSmoothLerp);
                    RenderSettings.skybox.SetColor(_topColor, L_topColor);
                }
                //_midColor
                if (RenderSettings.skybox.HasProperty(_midColor))
                {
                    Color L_midColor = Color.Lerp(RenderSettings.skybox.GetColor(_midColor), (skyData.nightMidColorOverTime.Evaluate(nightLightTime)), GetSmoothLerp);
                    RenderSettings.skybox.SetColor(_midColor, L_midColor);
                }
                _timeFog = Color.Lerp(skyData.fogColour.Evaluate(dayLightTime), skyData.nightMidColorOverTime.Evaluate(nightLightTime), skyData.fogToDayColor);
                //_weatherFog = Color.Lerp(_timeFog, weatherController.currentWeather.fogColour.Evaluate(timePercent), weatherController.currentWeather.fogStrength);
            }

            if (RenderSettings.skybox.HasProperty(_bottomColor))
            {
                //_bottomColor
                RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, _timeFog, GetSmoothLerp);
                RenderSettings.skybox.SetColor(_bottomColor, RenderSettings.fogColor);
            }
            //nightsky
            if (RenderSettings.skybox.HasProperty(_nightOpacity))
            {
                float L_nightOpacity = Mathf.Lerp(RenderSettings.skybox.GetFloat(_nightOpacity), smoothFadeNight, GetSmoothLerp);
                RenderSettings.skybox.SetFloat(_nightOpacity, L_nightOpacity);
            }
            if (RenderSettings.skybox.HasProperty(_sunScale))
            {
                //sunscale
                float L_sunScale = Mathf.Lerp(RenderSettings.skybox.GetFloat(_sunScale), skyData.sunSizeOverTime.Evaluate(timePercent), GetSmoothLerp);
                RenderSettings.skybox.SetFloat(_sunScale, L_sunScale);
            }
            if (RenderSettings.skybox.HasProperty(_sunScale))
            {
                //sunintensity
                float L_sunIntensity = Mathf.Lerp(RenderSettings.skybox.GetFloat(_sunIntensity), skyData.sunIntensityCurve.Evaluate(timePercent), GetSmoothLerp);
                RenderSettings.skybox.SetFloat(_sunIntensity, L_sunIntensity);
            }

        }
        public void SmoothNight()
        {
            //smooth the day night lerp
            if (nightLightTime >= 0.95f)
            {
                fadeToNight = Mathf.Lerp(1, 0, (nightLightTime - 0.95f) / 0.05f);
            }
            else if (nightLightTime <= 0.05f)
            {
                fadeToNight = Mathf.Lerp(0, 1, nightLightTime / 0.05f);
            }
            else
            {
                fadeToNight = 1;
            }

            //smooth the day night lerp
            if (nightLightTime >= 0.6f)
            {
                smoothFadeNight = Mathf.Lerp(1, 0, (nightLightTime - 0.6f) / 0.4f);
            }
            else if (nightLightTime <= 0.4f)
            {
                smoothFadeNight = Mathf.Lerp(0, 1, nightLightTime / 0.4f);
            }
            else
            {
                smoothFadeNight = 1;
            }

        }

        public class TimeStamp
        {
            [Range(0f, 24f)] public float timeOfDay;

            public Day dayOfWeek;
            public int dayOfMonth;
            public MonthData month;
            public int year;
        }
    }

}
