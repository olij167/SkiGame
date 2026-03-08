using UnityEngine;
using UnityEngine.InputSystem;

namespace SkiGame.Progression
{
    public static class InputBindingOverridesStorage
    {
        private const string Key = "InputBindingOverridesJson";

        public static void SaveOverrides(InputActionAsset asset)
        {
            if (asset == null) return;
            string json = asset.SaveBindingOverridesAsJson();
            PlayerPrefs.SetString(Key, json);
            PlayerPrefs.Save();
        }

        public static void ApplySavedOverrides(InputActionAsset asset)
        {
            if (asset == null) return;
            if (!PlayerPrefs.HasKey(Key)) return;

            string json = PlayerPrefs.GetString(Key, string.Empty);
            if (string.IsNullOrEmpty(json)) return;

            asset.LoadBindingOverridesFromJson(json);
        }

        public static void ClearOverrides()
        {
            if (PlayerPrefs.HasKey(Key))
            {
                PlayerPrefs.DeleteKey(Key);
                PlayerPrefs.Save();
            }
        }
    }
}
