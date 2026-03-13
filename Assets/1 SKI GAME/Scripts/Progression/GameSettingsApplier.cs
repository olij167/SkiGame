using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace SkiGame.Progression
{
    /// <summary>
    /// Scene component: applies settings into global Unity knobs (Audio/Quality/FPS/Screen) + UI Toolkit scaling + Camera.
    /// Add it once to your bootstrap scene (and ideally DontDestroyOnLoad).
    /// </summary>
    public sealed class GameSettingsApplier : MonoBehaviour
    {
        [Header("Optional Audio Mixer")]
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private string masterParam = "MasterVol";
        [SerializeField] private string musicParam = "MusicVol";
        [SerializeField] private string sfxParam = "SfxVol";

        [Header("UI Scale (UI Toolkit)")]
        [Tooltip("If empty, we'll auto-find all UIDocuments once on enable and again on scene load.")]
        [SerializeField] private UIDocument[] uiDocuments;
        [SerializeField] private bool autoFindUiDocuments = true;

        [Header("Camera (optional)")]
        [Tooltip("If set, we'll apply FOV to this camera. Otherwise we try Camera.main.")]
        [SerializeField] private Camera targetCamera;

        [Header("Colour Accessibility (optional)")]
        [Tooltip("If you have a post-processing / shader based accessibility system, assign a receiver here.")]
        [SerializeField] private MonoBehaviour colorAccessibilityReceiver; // must implement IColorAccessibilityReceiver

        private bool _resolvedDocs;

        private static GameSettingsApplier _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureInstance()
        {
            // If one already exists (eg. in a bootstrap scene), don't create another.
            if (_instance != null) return;

            // Also cover cases where an applier exists but Awake hasn't run yet.
#if UNITY_2023_1_OR_NEWER
            var existing = UnityEngine.Object.FindAnyObjectByType<GameSettingsApplier>();
#else
    var existing = UnityEngine.Object.FindObjectOfType<GameSettingsApplier>();
#endif
            if (existing != null)
            {
                _instance = existing;
                return;
            }

            // Create one globally so settings always apply (menu included).
            var go = new GameObject("[GameSettingsApplier]");
            _instance = go.AddComponent<GameSettingsApplier>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            ResolveUiDocuments(force: true);

            GameSettingsService.EnsureLoaded();
            GameSettingsService.OnChanged += Apply;

            SceneManager.sceneLoaded += OnSceneLoaded;

            Apply(GameSettingsService.Current);
        }

        private void OnDisable()
        {
            GameSettingsService.OnChanged -= Apply;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // New scenes often spawn new UIDocuments and may change which camera is "main".
            ResolveUiDocuments(force: true);
            Apply(GameSettingsService.Current);
        }

        private void ResolveUiDocuments(bool force)
        {
            if (!force && _resolvedDocs) return;
            _resolvedDocs = true;

            if (autoFindUiDocuments)
            {
#if UNITY_2023_1_OR_NEWER
                uiDocuments = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
#else
                uiDocuments = FindObjectsOfType<UIDocument>();
#endif
            }
        }

        private void Apply(GameSettingsProfile s)
        {
            if (s == null) return;

            // --- Audio ---
            AudioListener.volume = s.masterVolume;

            if (mixer != null)
            {
                SetMixerLinear01(masterParam, s.masterVolume);
                SetMixerLinear01(musicParam, s.musicVolume);
                SetMixerLinear01(sfxParam, s.sfxVolume);
            }

            // --- Quality / Performance ---
            if (s.qualityLevel >= 0 && s.qualityLevel < QualitySettings.names.Length)
                QualitySettings.SetQualityLevel(s.qualityLevel, applyExpensiveChanges: true);

            QualitySettings.vSyncCount = s.vSync ? 1 : 0;

            // FPS cap: only meaningful when vSync is off in most cases.
            Application.targetFrameRate = (s.targetFps <= 0) ? -1 : s.targetFps;

            // --- Display / Window ---
            ApplyDisplaySettings(s);

            // --- Camera ---
            ApplyCameraFov(s.cameraFov);

            // --- Colour Accessibility ---
            ApplyColourAccessibility(s.colorAccessibilityMode);

            // --- UI Scale ---
            ApplyUiScale(s.uiScale);
        }

        private void ApplyDisplaySettings(GameSettingsProfile s)
        {
            // Activate additional displays if present (safe no-op if not supported).
            try
            {
                var displays = Display.displays;
                if (displays != null)
                {
                    for (int i = 1; i < displays.Length; i++)
                    {
                        if (!displays[i].active)
                            displays[i].Activate();
                    }
                }
            }
            catch { /* ignore platform issues */ }

            var mode = (FullScreenMode)Mathf.Clamp(s.fullScreenMode, 0, 5);

            // If width/height are 0, keep current resolution.
            int w = s.resolutionWidth;
            int h = s.resolutionHeight;

            // Best-effort refresh handling:
            // - If 0, Unity chooses default.
            // - If non-zero, we try to pick a matching Resolution.
            int refreshHz = s.resolutionRefreshHz;

            if (w > 0 && h > 0)
            {
                // Try to apply a matching resolution (prefer exact refresh if specified).
                Resolution chosen = default;
                bool found = false;

                var all = Screen.resolutions;
                if (all != null && all.Length > 0)
                {
                    for (int i = all.Length - 1; i >= 0; i--)
                    {
                        var r = all[i];
                        if (r.width != w || r.height != h) continue;

                        if (refreshHz > 0)
                        {
#if UNITY_2022_2_OR_NEWER
                            int hz = (int)Mathf.Round((float)r.refreshRateRatio.value);
#else
                            int hz = r.refreshRate;
#endif
                            if (hz != refreshHz) continue;
                        }

                        chosen = r;
                        found = true;
                        break;
                    }
                }

                if (found)
                {
#if UNITY_2022_2_OR_NEWER
                    Screen.SetResolution(chosen.width, chosen.height, mode, chosen.refreshRateRatio);
#else
                    Screen.SetResolution(chosen.width, chosen.height, mode, chosen.refreshRate);
#endif
                }
                else
                {
                    // Fallback: apply without refresh hint.
                    Screen.SetResolution(w, h, mode);
                }
            }
            else
            {
                // Still apply fullscreen mode even if resolution isn't explicitly chosen.
                if (Screen.fullScreenMode != mode)
                    Screen.fullScreenMode = mode;
            }

            // Display selection is platform- and Unity-version dependent.
            // We do best-effort where available.
#if UNITY_2022_2_OR_NEWER
            try
            {
                int idx = Mathf.Clamp(s.displayIndex, 0, 15);

                // Unity 2022.2+ uses the "fill list" API and returns void.
                var layout = new List<DisplayInfo>(8);
                Screen.GetDisplayLayout(layout);

                if (layout != null && layout.Count > 0 && idx < layout.Count && idx != 0)
                {
                    var di = layout[idx];

                    // Nudge inside the target display so the window is clearly "on" that screen.
                    var pos = new Vector2Int(Mathf.Max(0, di.width / 10), Mathf.Max(0, di.height / 10));
                    Screen.MoveMainWindowTo(di, pos);
                }
            }
            catch { /* ignore platform/version issues */ }
#endif

        }

        private void ApplyCameraFov(float fov)
        {
            float clamped = Mathf.Clamp(fov, 60f, 110f);

            var cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam != null)
                cam.fieldOfView = clamped;

#if CINEMACHINE
            // Optional: if you use Cinemachine, also apply to active virtual cameras.
            try
            {
                var vcams = FindObjectsOfType<Cinemachine.CinemachineVirtualCamera>(true);
                for (int i = 0; i < vcams.Length; i++)
                {
                    var v = vcams[i];
                    if (v == null) continue;
                    var lens = v.m_Lens;
                    lens.FieldOfView = clamped;
                    v.m_Lens = lens;
                }
            }
            catch { /* ignore */ }
#endif
        }

        private void ApplyColourAccessibility(int mode)
        {
            if (colorAccessibilityReceiver == null) return;

            if (colorAccessibilityReceiver is IColorAccessibilityReceiver r)
                r.ApplyColorAccessibilityMode(mode);
        }

        private void ApplyUiScale(float uiScale)
        {
            float scale = Mathf.Clamp(uiScale, 0.85f, 1.15f);

            // Always refresh list: menus often spawn their UIDocument after this applier.
            ResolveUiDocuments(force: true);

            if (uiDocuments == null || uiDocuments.Length == 0)
                return;

            for (int i = 0; i < uiDocuments.Length; i++)
            {
                var doc = uiDocuments[i];
                if (doc == null)
                    continue;

                // Preferred and layout-safe path.
                if (doc.panelSettings != null)
                    doc.panelSettings.scale = scale;

                var root = doc.rootVisualElement;
                if (root == null)
                    continue;

                // Important: do NOT also apply transform scale to the root.
                // That compounds with panel scaling and is what causes UI to drift,
                // clip, or exceed the screen bounds.
                root.transform.scale = Vector3.one;
                root.style.translate = new Translate(0, 0, 0);
            }
        }

        private void SetMixerLinear01(string param, float linear01)
        {
            if (mixer == null || string.IsNullOrEmpty(param)) return;

            // Convert 0..1 to dB. 0 => -80dB (near silent), 1 => 0dB
            float db = (linear01 <= 0.0001f) ? -80f : Mathf.Lerp(-30f, 0f, Mathf.Clamp01(linear01));
            mixer.SetFloat(param, db);
        }
    }

    /// <summary>
    /// Implement this on a component that applies colour deficiency modes (URP Volume, shader keywords, etc.).
    /// </summary>
    public interface IColorAccessibilityReceiver
    {
        void ApplyColorAccessibilityMode(int mode);
    }
}
