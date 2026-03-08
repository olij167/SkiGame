using System;
using UnityEngine;

namespace SkiGame.Progression
{
    /// <summary>
    /// Central settings state + persistence. UI edits call Set(...), then Save() is debounced by PhoneHUD.
    /// Other systems can subscribe to OnChanged.
    /// </summary>
    public static class GameSettingsService
    {
        public static event Action<GameSettingsProfile> OnChanged;

        private static bool _loaded;
        private static GameSettingsProfile _current;

        public static GameSettingsProfile Current
        {
            get
            {
                EnsureLoaded();
                return _current;
            }
        }

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _current = GameSettingsStorage.LoadOrCreate();
            _loaded = true;

            // Minimal default apply that doesn't require extra wiring:
            ApplyMinimal(_current);

            OnChanged?.Invoke(_current);
        }

        public static void Set(GameSettingsProfile updated, bool applyMinimal = true)
        {
            EnsureLoaded();
            _current = updated ?? GameSettingsProfile.Defaults();
            _current.Sanitize();

            if (applyMinimal)
                ApplyMinimal(_current);

            OnChanged?.Invoke(_current);
        }

        public static void Save()
        {
            EnsureLoaded();
            GameSettingsStorage.Save(_current);
        }

        public static void ResetToDefaults()
        {
            Set(GameSettingsProfile.Defaults(), applyMinimal: true);
            Save();
        }

        private static void ApplyMinimal(GameSettingsProfile p)
        {
            // This is intentionally conservative: AudioListener is always present; mixers are optional (see Applier component).
            AudioListener.volume = p.masterVolume;
        }
    }
}
