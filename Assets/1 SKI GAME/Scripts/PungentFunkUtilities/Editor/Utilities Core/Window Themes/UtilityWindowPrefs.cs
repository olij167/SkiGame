namespace PungentFunk.Utilities.Editor.Theme
{
    #if UNITY_EDITOR
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Small strongly-typed EditorPrefs helpers for generic utility windows.
    /// </summary>
    public static class UtilityWindowPrefs
    {
        public static bool GetBool(string key, bool defaultValue) => EditorPrefs.GetBool(key, defaultValue);
        public static void SetBool(string key, bool value) => EditorPrefs.SetBool(key, value);

        public static int GetInt(string key, int defaultValue) => EditorPrefs.GetInt(key, defaultValue);
        public static void SetInt(string key, int value) => EditorPrefs.SetInt(key, value);

        public static float GetFloat(string key, float defaultValue) => EditorPrefs.GetFloat(key, defaultValue);
        public static void SetFloat(string key, float value) => EditorPrefs.SetFloat(key, value);

        public static string GetString(string key, string defaultValue) => EditorPrefs.GetString(key, defaultValue);
        public static void SetString(string key, string value) => EditorPrefs.SetString(key, value ?? string.Empty);

        public static Color GetColor(string key, Color defaultValue)
        {
            string encoded = EditorPrefs.GetString(key, string.Empty);
            if (ColorUtility.TryParseHtmlString(encoded, out Color color))
                return color;
            return defaultValue;
        }

        public static void SetColor(string key, Color value)
        {
            EditorPrefs.SetString(key, $"#{ColorUtility.ToHtmlStringRGBA(value)}");
        }
    }
    #endif

}