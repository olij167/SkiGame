using UnityEngine;
using System.Collections;
using UnityEditor;
namespace Artngame.SKYMASTER
{
    [CustomEditor(typeof(WeatherScript))]
    public class WeatherRendererEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            WeatherScript weatherRenderer = (WeatherScript)target;
            if (GUILayout.Button("Generate new weather texture"))
            {
                weatherRenderer.GenerateAndChangeWeatherTexture();
            }
        }
    }
}