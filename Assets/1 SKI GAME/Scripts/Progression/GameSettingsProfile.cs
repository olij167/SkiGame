using System;
using UnityEngine;

namespace SkiGame.Progression
{
    [Serializable]
    public sealed class GameSettingsProfile
    {
        public const int CurrentVersion = 3;

        [Header("Versioning")]
        public int version = CurrentVersion;

        [Header("Audio (0..1)")]
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float musicVolume = 1f;
        [Range(0f, 1f)] public float sfxVolume = 1f;

        [Header("Display / Graphics")]
        public int qualityLevel = -1;       // -1 = keep current
        public bool vSync = true;
        public int targetFps = 0;           // 0 = Unlimited

        // Resolution selection stored as explicit values (more stable than an index).
        // 0 = keep current.
        public int resolutionWidth = 0;
        public int resolutionHeight = 0;
        public int resolutionRefreshHz = 0;

        // FullScreenMode stored as int for serialization safety.
        // Matches UnityEngine.FullScreenMode enum values.
        public int fullScreenMode = (int)FullScreenMode.FullScreenWindow; // Borderless by default on PC

        // Which display/monitor to use (best-effort; platform dependent).
        public int displayIndex = 0;

        [Header("Controls")]
        public bool invertLookY = false;
        public bool invertLookX = false;
        [Range(0.25f, 3f)] public float lookSensitivity = 1f;
        [Range(0f, 0.5f)] public float stickDeadzone = 0.15f;

        [Header("Gameplay / Camera")]
        [Range(60f, 110f)] public float cameraFov = 85f;

        [Header("UI / Accessibility")]
        [Range(0.8f, 1.3f)] public float uiScale = 1f;
        public bool reduceMotion = false;

        // Keep as int (migration-safe)
        public int colorAccessibilityMode = 0; // 0=None
        public int speedUnitMode = 0;          // 0=km/h, 1=mph

        public void Sanitize()
        {
            version = Mathf.Max(version, 1);

            masterVolume = Mathf.Clamp01(masterVolume);
            musicVolume = Mathf.Clamp01(musicVolume);
            sfxVolume = Mathf.Clamp01(sfxVolume);

            qualityLevel = Mathf.Clamp(qualityLevel, -1, 50);
            targetFps = Mathf.Clamp(targetFps, 0, 360);

            // Display
            resolutionWidth = Mathf.Clamp(resolutionWidth, 0, 16384);
            resolutionHeight = Mathf.Clamp(resolutionHeight, 0, 16384);
            resolutionRefreshHz = Mathf.Clamp(resolutionRefreshHz, 0, 1000);

            // FullscreenMode enum is small; clamp to safe range (0..5)
            fullScreenMode = Mathf.Clamp(fullScreenMode, 0, 5);
            displayIndex = Mathf.Clamp(displayIndex, 0, 16);

            lookSensitivity = Mathf.Clamp(lookSensitivity, 0.25f, 3f);
            stickDeadzone = Mathf.Clamp(stickDeadzone, 0f, 0.5f);

            cameraFov = Mathf.Clamp(cameraFov, 60f, 110f);

            uiScale = Mathf.Clamp(uiScale, 0.8f, 1.3f);
            colorAccessibilityMode = Mathf.Clamp(colorAccessibilityMode, 0, 3);
            speedUnitMode = Mathf.Clamp(speedUnitMode, 0, 1);
        }

        public static GameSettingsProfile Defaults()
        {
            var p = new GameSettingsProfile
            {
                version = CurrentVersion,

                // Reasonable PC defaults
                vSync = true,
                targetFps = 0,
                fullScreenMode = (int)FullScreenMode.FullScreenWindow,
                displayIndex = 0,

                uiScale = 1f,
                cameraFov = 85f
            };

            p.Sanitize();
            return p;
        }
    }
}
