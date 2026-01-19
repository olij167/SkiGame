using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace TimeWeather
{
    [CustomEditor(typeof(WeatherDisplay))]
    public class WeatherDisplayEditor : Editor
    {
        WeatherController weatherController;
        WeatherDisplay weatherDisplay;
        //string weatherCondition;
        float temperature;
        float chanceOfRain;
        bool isRaining;
        //float wetness;
        //float snowiness;
        //float cloudPower;
        //float windSpeed;
        //float fogStrength;

        SerializedObject getTarget;
        SerializedProperty hourlyForcast;
        //SerializedProperty hourlyClouds;
        bool changeFoldOut;
        bool forcastFoldOut;
        bool cloudFoldOut;
        int numOfHoursToDisplay;
        //WEATHER DISPLAY
        // current weather, previous weather, next weather
        //temperature, chance of rain, is raining, wetness/ snowiness, cloud power, wind speed

        // hourly forcast
        //temperature, chance of rain, is raining, wetness/ snowiness, cloud power, wind speed
        private void OnEnable()
        {
            weatherDisplay = (WeatherDisplay)target;
            if (weatherController == null)
                weatherController = weatherDisplay.GetComponent<WeatherController>();


            getTarget = new SerializedObject(weatherController);
            hourlyForcast = getTarget.FindProperty("hourlyWeather");
            //hourlyForcast.arraySize = weatherController.hourlyWeather.Length - 1;
        }
        public override void OnInspectorGUI()
        {
            //DisplayFieldType = (displayFieldType)EditorGUILayout.EnumPopup("", DisplayFieldType);

            weatherDisplay = (WeatherDisplay)target;
            if (weatherDisplay == null) return;

            if (weatherController == null)
                weatherController = weatherDisplay.GetComponent<WeatherController>();

            if (weatherController.timeController == null)
                weatherController.timeController = FindFirstObjectByType<TimeController>();

            if (weatherController.currentSeasonConditions.season == "0")
                weatherController.SetSeasonalConditions();

#if UNITY_EDITOR
            if (hourlyForcast.arraySize == 0)
            {
                weatherController.SetDailyConditions();
                //hourlyForcast = serializedObject.FindProperty("hourlyWeather");
            }
#endif
            if (weatherController.currentWeatherPreset.weatherCondition == string.Empty)
            {
                for (int i = 0; i < weatherController.weatherDataPresets.Length; i++)
                {
                    if (weatherController.hourlyWeather[weatherController.timeController.timeHours].weatherCondition == weatherController.weatherDataPresets[i].weatherCondition)
                    {
                        weatherController.currentWeatherPreset = weatherController.weatherDataPresets[i];
                        //weatherCondition = weatherController.currentWeatherPreset.weatherCondition;
                    }
                }
            }


            //temperature = weatherController.temperature;
            //chanceOfRain = weatherController.chanceOfRain;
            //isRaining = weatherController.hourlyWeather[weatherController.timeController.timeHours].isRaining;
            //cloudPower = weatherController.cloudPower;
            //windSpeed = weatherController.windSpeed;
            ////fogSpeed = weatherController.fogSpeed;
            //wetness = weatherController.wetness;
            //snowiness = weatherController.snowiness;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Current Weather ", EditorStyles.boldLabel);
            GUILayout.Label(weatherController.temperature.ToString("00") + "°");
            GUILayout.Label(weatherController.currentWeatherPreset.weatherCondition);
            GUILayout.Label(weatherController.rainChance.ToString("00") + "% Rain Chance");
            EditorGUILayout.EndHorizontal();
            DrawUILine(new Color(1f, 1f, 1f, 0.25f));

            //GUILayout.Label("Surface");
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Wetness ");
            EditorGUILayout.Slider(weatherController.wetness, 0f, 5f);
            GUILayout.Label("Snowiness ");
            EditorGUILayout.Slider(weatherController.snowiness, 0f, 1f);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            //GUILayout.Label("Cloud Power ");
            //EditorGUILayout.Slider(weatherController.cloudPower, 0f, 5f);
            GUILayout.Label("Fog Strength ");
            EditorGUILayout.Slider(weatherController.currentWeatherPreset.fogStrength, 0f, 1f);
            EditorGUILayout.EndHorizontal();


            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Wind Speed ");
            EditorGUILayout.Slider(weatherController.windSpeed, -50f, 50f);
            GUILayout.Label("Raining ");
            EditorGUILayout.Toggle(weatherController.currentWeatherPreset.isRaining);
            EditorGUILayout.EndHorizontal();


            DrawUILine(new Color(1f, 1f, 1f, 0.25f));
            EditorGUILayout.Space(5);
            changeFoldOut = EditorGUILayout.Foldout(changeFoldOut, "Set Weather for Current Hour", true);

            if (changeFoldOut)
            {
                GUILayout.Label("Change Weather", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Temperature ");
                temperature = EditorGUILayout.FloatField(temperature);
                GUILayout.Label("Rain Chance");
                chanceOfRain = EditorGUILayout.Slider(chanceOfRain, 0f, 100f);
                GUILayout.Label("%, Raining");
                isRaining = EditorGUILayout.Toggle(isRaining);
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Set Weather"))
                {
                    weatherController.SetHourlyVariables(weatherController.timeController.timeHours, temperature, chanceOfRain, isRaining);
                    weatherController.SetCurrentConditions();
                }

                WeatherController.WeatherData newWeather = new WeatherController.WeatherData();

                for (int w = 0; w < weatherController.weatherDataPresets.Length; w++)
                {
                    if (temperature >= weatherController.weatherDataPresets[w].tempRange.x && temperature <= weatherController.weatherDataPresets[w].tempRange.y)
                    {
                        if (chanceOfRain >= weatherController.weatherDataPresets[w].rainRange.x && chanceOfRain <= weatherController.weatherDataPresets[w].rainRange.y)
                        {
                            if (isRaining == weatherController.weatherDataPresets[w].isRaining)
                            {
                                newWeather = weatherController.weatherDataPresets[w];
                                //weatherCondition = weatherController.weatherDataPresets[w].weatherCondition;
                                //fogStrength = weatherController.weatherDataPresets[w].fogStrength;
                            }
                        }
                    }

                }

                DrawUILine(new Color(1f, 1f, 1f, 0.25f));
                EditorGUILayout.Space(5);

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("New Weather ", EditorStyles.boldLabel);
                GUILayout.Label(temperature + "°");
                GUILayout.Label(newWeather.weatherCondition);
                GUILayout.Label(chanceOfRain + "% Rain Chance");
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Wetness ");
                EditorGUILayout.Slider(newWeather.wetness, 0f, 5f);
                GUILayout.Label("Snowiness ");
                EditorGUILayout.Slider(newWeather.snowiness, 0f, 1f);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Fog Strength ");
                EditorGUILayout.Slider(weatherController.currentWeatherPreset.fogStrength, 0f, 1f);

                GUILayout.Label("Raining ");
                EditorGUILayout.Toggle(newWeather.isRaining);
                EditorGUILayout.EndHorizontal();
                DrawUILine(new Color(1f, 1f, 1f, 0.25f));
                EditorGUILayout.Space(5);
            }
            forcastFoldOut = EditorGUILayout.Foldout(forcastFoldOut, "Hourly Forcast", true);
            getTarget.Update();

            if (forcastFoldOut)
            {

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Hours in Forcast ");
                numOfHoursToDisplay = EditorGUILayout.IntSlider(numOfHoursToDisplay, 1, 24);
                EditorGUILayout.EndHorizontal();

                //GUILayout.Label("Hourly Forcast ", EditorStyles.boldLabel);
                int maxForwardHours = hourlyForcast.arraySize - weatherController.timeController.timeHours;
                int backwardHours = numOfHoursToDisplay - maxForwardHours;

                for (int i = 0; i < hourlyForcast.arraySize; i++)
                {
                    if ((backwardHours > 0 && i < weatherController.timeController.timeHours && i >= weatherController.timeController.timeHours - backwardHours) ||
                        (i >= weatherController.timeController.timeHours && i < weatherController.timeController.timeHours + numOfHoursToDisplay))
                    {
                        SetHourForcast(i);
                    }
                }

            }

            if (GUILayout.Button("Reset Daily Forecast"))
            {
                weatherController.SetDailyConditions();
                weatherController.SetCurrentConditions();
            }

            getTarget.ApplyModifiedProperties();

        }

        public void SetHourForcast(int i)
        {
            SerializedProperty hourProp = hourlyForcast.GetArrayElementAtIndex(i);

            SerializedProperty forcastTime = hourProp.FindPropertyRelative("forcastTime");
            SerializedProperty temp = hourProp.FindPropertyRelative("temp");
            SerializedProperty rainChance = hourProp.FindPropertyRelative("rainChance");
            SerializedProperty weatherCondition = hourProp.FindPropertyRelative("weatherCondition");
            SerializedProperty cloudPower = hourProp.FindPropertyRelative("cloudPower");
            SerializedProperty isRaining = hourProp.FindPropertyRelative("isRaining");

            // Header
            GUILayout.Label(forcastTime.intValue.ToString("00") + ":00 Forecast", EditorStyles.boldLabel);

            // Row 1: temp + condition
            EditorGUILayout.BeginHorizontal();
            temp.floatValue = EditorGUILayout.Slider(temp.floatValue, -30f, 40f);
            GUILayout.Label("°C", GUILayout.Width(30));
            EditorGUILayout.Space(6);
            GUILayout.Label(weatherCondition.stringValue, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            // Row 2: rain + raining toggle
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Rain %", GUILayout.Width(50));
            rainChance.floatValue = EditorGUILayout.Slider(rainChance.floatValue, 0f, 100f);
            EditorGUILayout.Space(6);
            GUILayout.Label("Raining", GUILayout.Width(55));
            isRaining.boolValue = EditorGUILayout.Toggle(isRaining.boolValue, GUILayout.Width(20));
            EditorGUILayout.EndHorizontal();

            // Row 3: clouds (your system uses cloudPower 0..5)
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Clouds", GUILayout.Width(50));
            cloudPower.floatValue = EditorGUILayout.Slider(cloudPower.floatValue, 0f, 5f);
            EditorGUILayout.EndHorizontal();

            DrawUILine(new Color(1f, 1f, 1f, 0.25f));
            EditorGUILayout.Space(5);
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
