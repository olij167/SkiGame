using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SkiGame.Progression;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public sealed class MainMenuController : MonoBehaviour
{
    private enum ViewState
    {
        Home,
        Play,
        Settings,
        Controls
    }

    [Header("Scene Names")]
    [SerializeField] private string menuSceneName = "MainMenu";

    [Header("UI Toolkit")]
    [SerializeField] private UIDocument document;

    [Header("Background Loader")]
    [SerializeField] private MenuBackgroundLoader backgroundLoader;

    [Header("Rebinding")]
    [Tooltip("Assign the same InputActionAsset used by gameplay / pause menu rebinding.")]
    [SerializeField] private InputActionAsset rebindActionsAsset;

    private const int SlotCount = GameSaveSystem.MaxSlots;

    private ViewState _state = ViewState.Home;
    private int _selectedSlot;
    private bool _muteSettingsCallbacks;

    // Root panels
    private VisualElement _panelHome;
    private VisualElement _panelPlay;
    private VisualElement _panelSettings;
    private VisualElement _panelControls;

    // Simple delete modal
    private VisualElement _modalDeleteConfirm;
    private Button _btnConfirmDelete;
    private Button _btnCancelDelete;

    // Home
    private Button _btnPlay;
    private Button _btnSettings;
    private Button _btnControls;
    private Button _btnQuit;

    // Play
    private Button _btnBackFromPlay;
    private Button _btnPrimarySlotAction;
    private Button _btnDeleteSlot;

    private Label _playHeader;
    private Label _previewSlotName;
    private Label _previewSlotState;
    private Label _previewPlaythrough;
    private Label _previewCalendar;
    private Label _previewStats;

    // Settings
    private Button _btnBackFromSettings;
    private Button _btnResetSettings;
    private Button _btnSaveSettings;

    private Slider _sMaster;
    private Slider _sMusic;
    private Slider _sSfx;
    private Label _lblMaster;
    private Label _lblMusic;
    private Label _lblSfx;

    private Toggle _tInvertY;
    private Slider _sLookSens;
    private Label _lblLookSens;

    private Slider _sUiScale;
    private Label _lblUiScale;
    private Toggle _tReduceMotion;

    private DropdownField _ddColourMode;

    private DropdownField _ddQuality;
    private Toggle _tVSync;
    private DropdownField _ddFpsCap;

    private DropdownField _ddResolution;
    private DropdownField _ddWindowMode;
    private DropdownField _ddDisplay;

    private Slider _sFov;
    private Label _lblFov;

    private DropdownField _ddUnits;

    // Controls
    private Button _btnBackFromControls;
    private Button _btnControlsResetBindings;
    private Label _lblControlsStatus;
    private Label _lblControlsNote;
    private VisualElement _controlsRebindList;

    private readonly List<Button> _slotButtons = new();
    private readonly List<Button> _controlsRebindButtons = new();

    private InputActionRebindingExtensions.RebindingOperation _activeRebind;
    private InputAction _activeRebindAction;
    private int _activeRebindBindingIndex = -1;
    private VisualElement _activeRebindRow;
    private Label _activeRebindBindingLabel;

    private void Reset()
    {
        document = GetComponent<UIDocument>();
    }

    private void Awake()
    {
        if (document == null)
            document = GetComponent<UIDocument>();

        if (backgroundLoader == null)
            backgroundLoader = FindObjectOfType<MenuBackgroundLoader>();

        if (document == null)
        {
            Debug.LogError("[MainMenuController] Missing UIDocument.");
            enabled = false;
            return;
        }

        Bind(document.rootVisualElement);
        Wire();

        SetView(ViewState.Home);
        HideDeleteModal();
    }

    private void Start()
    {
        GameSettingsService.EnsureLoaded();
        RefreshSettingsUI(GameSettingsService.Current);

        _selectedSlot = Mathf.Clamp(GameSaveSystem.ActiveSlotId, 0, SlotCount - 1);

        RefreshSlotCards();
        RefreshSelectedSlotPreview();

        document.rootVisualElement.schedule.Execute(() =>
        {
            _btnPlay?.Focus();
        }).ExecuteLater(10);
    }

    private void OnEnable()
    {
        GameSettingsService.OnChanged += OnSettingsChanged;
    }

    private void OnDisable()
    {
        GameSettingsService.OnChanged -= OnSettingsChanged;
        CancelActiveRebind();
    }

    private void OnSettingsChanged(GameSettingsProfile profile)
    {
        RefreshSettingsUI(profile);
    }

    private void Bind(VisualElement root)
    {
        _panelHome = root.Q<VisualElement>("Panel_Home");
        _panelPlay = root.Q<VisualElement>("Panel_Play");
        _panelSettings = root.Q<VisualElement>("Panel_Settings");
        _panelControls = root.Q<VisualElement>("Panel_Controls");

        _modalDeleteConfirm = root.Q<VisualElement>("Modal_DeleteConfirm");
        _btnConfirmDelete = root.Q<Button>("Button_ConfirmDelete");
        _btnCancelDelete = root.Q<Button>("Button_CancelDelete");

        // Home
        _btnPlay = root.Q<Button>("Button_Play");
        _btnSettings = root.Q<Button>("Button_OpenSettings");
        _btnControls = root.Q<Button>("Button_OpenControls");
        _btnQuit = root.Q<Button>("Button_Quit");

        // Play
        _btnBackFromPlay = root.Q<Button>("Button_BackFromPlay");
        _btnPrimarySlotAction = root.Q<Button>("Button_PrimarySlotAction");
        _btnDeleteSlot = root.Q<Button>("Button_DeleteSlot");

        _playHeader = root.Q<Label>("Label_PlayHeader");
        _previewSlotName = root.Q<Label>("Label_PreviewSlotName");
        _previewSlotState = root.Q<Label>("Label_PreviewSlotState");
        _previewPlaythrough = root.Q<Label>("Label_PreviewPlaythrough");
        _previewCalendar = root.Q<Label>("Label_PreviewCalendar");
        _previewStats = root.Q<Label>("Label_PreviewStats");

        _slotButtons.Clear();
        for (int i = 0; i < SlotCount; i++)
        {
            var button = root.Q<Button>($"Button_Slot_{i}");
            if (button != null)
                _slotButtons.Add(button);
        }

        // Settings
        _btnBackFromSettings = root.Q<Button>("Button_BackFromSettings");
        _btnResetSettings = root.Q<Button>("Button_ResetSettings");
        _btnSaveSettings = root.Q<Button>("Button_SaveSettings");

        _sMaster = root.Q<Slider>("Slider_MasterVolume");
        _sMusic = root.Q<Slider>("Slider_MusicVolume");
        _sSfx = root.Q<Slider>("Slider_SfxVolume");
        _lblMaster = root.Q<Label>("Label_MasterVolume");
        _lblMusic = root.Q<Label>("Label_MusicVolume");
        _lblSfx = root.Q<Label>("Label_SfxVolume");

        _tInvertY = root.Q<Toggle>("Toggle_InvertLookY");
        _sLookSens = root.Q<Slider>("Slider_LookSensitivity");
        _lblLookSens = root.Q<Label>("Label_LookSensitivity");

        _sUiScale = root.Q<Slider>("Slider_UiScale");
        _lblUiScale = root.Q<Label>("Label_UiScale");
        _tReduceMotion = root.Q<Toggle>("Toggle_ReduceMotion");

        _ddColourMode = root.Q<DropdownField>("Dropdown_ColorAccessibility");

        _ddQuality = root.Q<DropdownField>("Dropdown_Quality");
        _tVSync = root.Q<Toggle>("Toggle_VSync");
        _ddFpsCap = root.Q<DropdownField>("Dropdown_FpsCap");

        _ddResolution = root.Q<DropdownField>("Dropdown_Resolution");
        _ddWindowMode = root.Q<DropdownField>("Dropdown_WindowMode");
        _ddDisplay = root.Q<DropdownField>("Dropdown_Display");

        _sFov = root.Q<Slider>("Slider_Fov");
        _lblFov = root.Q<Label>("Label_Fov");

        _ddUnits = root.Q<DropdownField>("Dropdown_Units");

        // Controls
        _btnBackFromControls = root.Q<Button>("Button_BackFromControls");
        _btnControlsResetBindings = root.Q<Button>("Button_ControlsResetBindings");
        _lblControlsStatus = root.Q<Label>("Label_ControlsStatus");
        _lblControlsNote = root.Q<Label>("Label_ControlsNote");
        _controlsRebindList = root.Q<VisualElement>("ControlsRebindList");

        PopulateSettingsChoices();
    }

    private void Wire()
    {
        if (_btnPlay != null)
        {
            _btnPlay.clicked += () =>
            {
                SetView(ViewState.Play);
                RefreshSlotCards();
                RefreshSelectedSlotPreview();
            };
        }

        if (_btnSettings != null)
        {
            _btnSettings.clicked += () =>
            {
                SetView(ViewState.Settings);
                RefreshSettingsUI(GameSettingsService.Current);
            };
        }

        if (_btnControls != null)
        {
            _btnControls.clicked += () =>
            {
                SetView(ViewState.Controls);
                RebuildControlsList();
            };
        }

        if (_btnQuit != null)
            _btnQuit.clicked += Quit;

        if (_btnBackFromPlay != null)
            _btnBackFromPlay.clicked += () => SetView(ViewState.Home);

        if (_btnPrimarySlotAction != null)
            _btnPrimarySlotAction.clicked += OnClickPrimarySlotAction;

        if (_btnDeleteSlot != null)
            _btnDeleteSlot.clicked += ShowDeleteModal;

        for (int i = 0; i < _slotButtons.Count; i++)
        {
            int slot = i;
            if (_slotButtons[i] != null)
                _slotButtons[i].clicked += () => SelectSlot(slot);
        }

        if (_btnConfirmDelete != null)
            _btnConfirmDelete.clicked += ConfirmDeleteSlot;

        if (_btnCancelDelete != null)
            _btnCancelDelete.clicked += HideDeleteModal;

        if (_btnBackFromSettings != null)
            _btnBackFromSettings.clicked += () => SetView(ViewState.Home);

        if (_btnResetSettings != null)
        {
            _btnResetSettings.clicked += () =>
            {
                GameSettingsService.ResetToDefaults();
                GameSettingsService.Save();
                RefreshSettingsUI(GameSettingsService.Current);
            };
        }

        if (_btnSaveSettings != null)
            _btnSaveSettings.clicked += () => GameSettingsService.Save();

        if (_btnBackFromControls != null)
            _btnBackFromControls.clicked += () => SetView(ViewState.Home);

        if (_btnControlsResetBindings != null)
            _btnControlsResetBindings.clicked += OnClickResetBindings;

        WireSettingsInputs();
    }

    private void SetView(ViewState state)
    {
        _state = state;

        SetVisible(_panelHome, state == ViewState.Home);
        SetVisible(_panelPlay, state == ViewState.Play);
        SetVisible(_panelSettings, state == ViewState.Settings);
        SetVisible(_panelControls, state == ViewState.Controls);

        HideDeleteModal();

        if (state == ViewState.Controls)
            RebuildControlsList();
        else
            CancelActiveRebind();
    }

    private static void SetVisible(VisualElement element, bool visible)
    {
        if (element == null) return;
        element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void ShowDeleteModal()
    {
        if (_modalDeleteConfirm == null) return;
        _modalDeleteConfirm.style.display = DisplayStyle.Flex;
    }

    private void HideDeleteModal()
    {
        if (_modalDeleteConfirm == null) return;
        _modalDeleteConfirm.style.display = DisplayStyle.None;
    }

    private void SelectSlot(int slot)
    {
        _selectedSlot = Mathf.Clamp(slot, 0, SlotCount - 1);
        GameSaveSystem.ActiveSlotId = _selectedSlot;

        RefreshSlotCards();
        RefreshSelectedSlotPreview();
    }

    private void RefreshSlotCards()
    {
        var manifest = GameSaveSystem.LoadOrCreateManifest();

        for (int i = 0; i < SlotCount; i++)
        {
            var button = document.rootVisualElement.Q<Button>($"Button_Slot_{i}");
            var title = document.rootVisualElement.Q<Label>($"Label_SlotTitle_{i}");
            var sub = document.rootVisualElement.Q<Label>($"Label_SlotSub_{i}");

            var meta = GameSaveSystem.GetSlotMeta(manifest, i);
            bool hasData = meta != null && meta.hasData;

            if (title != null)
                title.text = meta?.displayName ?? $"Save {i + 1}";

            if (sub != null)
                sub.text = hasData ? "Continue existing save" : "Start a new game";

            if (button != null)
                button.EnableInClassList("slot-selected", i == _selectedSlot);
        }

        if (_playHeader != null)
            _playHeader.text = $"Save Slot {_selectedSlot + 1}";
    }

    private void RefreshSelectedSlotPreview()
    {
        bool hasData = File.Exists(GameSaveSystem.GetProfilePath(_selectedSlot));

        if (_previewSlotName != null)
            _previewSlotName.text = $"Save {_selectedSlot + 1}";

        if (_previewSlotState != null)
            _previewSlotState.text = hasData ? "Existing career" : "Empty slot";

        if (!hasData)
        {
            if (_previewPlaythrough != null) _previewPlaythrough.text = "-";
            if (_previewCalendar != null) _previewCalendar.text = "-";
            if (_previewStats != null) _previewStats.text = "-";

            if (_btnPrimarySlotAction != null)
                _btnPrimarySlotAction.text = "New Game";

            if (_btnDeleteSlot != null)
                _btnDeleteSlot.SetEnabled(false);

            return;
        }

        if (TryLoadProfile(_selectedSlot, out var profile))
        {
            if (_previewPlaythrough != null)
            {
                int playthroughId = 0;
                if (profile.playthrough != null)
                    playthroughId = profile.playthrough.playthroughId;

                _previewPlaythrough.text = $"Playthrough {Mathf.Max(1, playthroughId + 1)}";
            }

            if (_previewCalendar != null)
                _previewCalendar.text = FormatCalendar(profile);

            if (_previewStats != null)
                _previewStats.text = FormatStats(profile);
        }
        else
        {
            if (_previewPlaythrough != null) _previewPlaythrough.text = "Unavailable";
            if (_previewCalendar != null) _previewCalendar.text = "Unavailable";
            if (_previewStats != null) _previewStats.text = "Unavailable";
        }

        if (_btnPrimarySlotAction != null)
            _btnPrimarySlotAction.text = "Continue";

        if (_btnDeleteSlot != null)
            _btnDeleteSlot.SetEnabled(true);
    }

    private void OnClickPrimarySlotAction()
    {
        bool hasData = File.Exists(GameSaveSystem.GetProfilePath(_selectedSlot));

        if (hasData)
            GameStartBootstrap.RequestLoadGame(_selectedSlot);
        else
            GameStartBootstrap.RequestNewGame(_selectedSlot);

        StartCoroutine(EnterGameRoutine());
    }

    private void ConfirmDeleteSlot()
    {
        HideDeleteModal();

        GameSaveSystem.DeleteSlot(_selectedSlot);
        RefreshSlotCards();
        RefreshSelectedSlotPreview();
    }

    private IEnumerator EnterGameRoutine()
    {
        if (backgroundLoader == null)
        {
            Debug.LogError("[MainMenuController] Missing MenuBackgroundLoader reference.");
            yield break;
        }

        yield return backgroundLoader.EnterGameplayAndUnloadMenu(menuSceneName);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private bool TryLoadProfile(int slotId, out PlayerStatsProfile profile)
    {
        profile = null;

        string path = GameSaveSystem.GetProfilePath(slotId);
        if (!File.Exists(path))
            return false;

        try
        {
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
                return false;

            profile = JsonUtility.FromJson<PlayerStatsProfile>(json);
            if (profile == null)
                return false;

            profile.Sanitize();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string FormatCalendar(PlayerStatsProfile profile)
    {
        if (profile == null)
            return "-";

        int dayKey = profile.dailyTasks != null ? profile.dailyTasks.dayKey : -1;
        if (!TryParseDayKey(dayKey, out var currentDate))
            return "-";

        bool hasStart =
            profile.playthrough != null &&
            profile.playthrough.calendarStartYear > 0 &&
            profile.playthrough.calendarStartDayOfYear > 0;

        string datePart = currentDate.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

        if (!hasStart)
            return datePart;

        var startDate = new DateTime(profile.playthrough.calendarStartYear, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddDays(profile.playthrough.calendarStartDayOfYear - 1);

        int daysSinceStart = Mathf.Max(0, (int)(currentDate.Date - startDate.Date).TotalDays);
        int week = (daysSinceStart / 7) + 1;
        int day = (daysSinceStart % 7) + 1;

        return $"Week {week}, Day {day}";
    }

    private static bool TryParseDayKey(int dayKey, out DateTime dateUtc)
    {
        dateUtc = default;

        if (dayKey <= 0)
            return false;

        string raw = dayKey.ToString(CultureInfo.InvariantCulture);
        if (raw.Length != 8)
            return false;

        return DateTime.TryParseExact(
            raw,
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out dateUtc
        );
    }

    private static string FormatStats(PlayerStatsProfile profile)
    {
        if (profile?.lifetime == null)
            return "-";

        int runs = profile.lifetime.totalRunsCompleted;
        float km = profile.lifetime.totalDistanceMeters / 1000f;
        float vert = profile.lifetime.totalVerticalDescentMeters;

        if (runs <= 0 && km <= 0.01f && vert <= 0.01f)
            return "-";

        return $"{runs} runs • {km:0.#} km • {vert:0} m vert";
    }

    // =========================================================
    // Settings
    // =========================================================

    private void PopulateSettingsChoices()
    {
        if (_ddColourMode != null)
            _ddColourMode.choices = new List<string> { "None", "Protanopia", "Deuteranopia", "Tritanopia" };

        if (_ddQuality != null)
        {
            _ddQuality.choices = new List<string>();
            var names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++)
                _ddQuality.choices.Add(names[i]);
        }

        if (_ddFpsCap != null)
            _ddFpsCap.choices = new List<string> { "Unlimited", "30", "60", "90", "120", "144", "165", "240" };

        if (_ddWindowMode != null)
            _ddWindowMode.choices = new List<string> { "Windowed", "Borderless", "Fullscreen" };

        if (_ddDisplay != null)
        {
            _ddDisplay.choices = new List<string>();
            int count = Mathf.Max(1, Display.displays != null ? Display.displays.Length : 1);
            for (int i = 0; i < count; i++)
                _ddDisplay.choices.Add($"Display {i + 1}");
        }

        if (_ddResolution != null)
        {
            _ddResolution.choices = new List<string>();
            var resolutions = Screen.resolutions;

            if (resolutions != null && resolutions.Length > 0)
            {
                for (int i = 0; i < resolutions.Length; i++)
                {
#if UNITY_2022_2_OR_NEWER
                    int hz = Mathf.RoundToInt((float)resolutions[i].refreshRateRatio.value);
#else
                    int hz = resolutions[i].refreshRate;
#endif
                    _ddResolution.choices.Add($"{resolutions[i].width}x{resolutions[i].height} @ {hz}Hz");
                }
            }
            else
            {
                _ddResolution.choices.Add("Current");
            }
        }

        if (_ddUnits != null)
            _ddUnits.choices = new List<string> { "km/h", "mph" };
    }

    private void WireSettingsInputs()
    {
        if (_sMaster != null)
            _sMaster.RegisterValueChangedCallback(evt =>
            {
                if (_muteSettingsCallbacks) return;
                MutateSettings(p => p.masterVolume = evt.newValue);
            });

        if (_sMusic != null)
            _sMusic.RegisterValueChangedCallback(evt =>
            {
                if (_muteSettingsCallbacks) return;
                MutateSettings(p => p.musicVolume = evt.newValue);
            });

        if (_sSfx != null)
            _sSfx.RegisterValueChangedCallback(evt =>
            {
                if (_muteSettingsCallbacks) return;
                MutateSettings(p => p.sfxVolume = evt.newValue);
            });

        if (_tInvertY != null)
            _tInvertY.RegisterValueChangedCallback(evt =>
            {
                if (_muteSettingsCallbacks) return;
                MutateSettings(p => p.invertLookY = evt.newValue);
            });

        if (_sLookSens != null)
            _sLookSens.RegisterValueChangedCallback(evt =>
            {
                if (_muteSettingsCallbacks) return;
                MutateSettings(p => p.lookSensitivity = evt.newValue);
            });

        if (_sUiScale != null)
            _sUiScale.RegisterValueChangedCallback(evt =>
            {
                if (_muteSettingsCallbacks) return;
                MutateSettings(p => p.uiScale = evt.newValue);
            });

        if (_tReduceMotion != null)
            _tReduceMotion.RegisterValueChangedCallback(evt =>
            {
                if (_muteSettingsCallbacks) return;
                MutateSettings(p => p.reduceMotion = evt.newValue);
            });

        if (_ddColourMode != null)
            _ddColourMode.RegisterValueChangedCallback(evt =>
            {
                if (_muteSettingsCallbacks) return;
                int idx = Mathf.Clamp(_ddColourMode.choices.IndexOf(evt.newValue), 0, 3);
                MutateSettings(p => p.colorAccessibilityMode = idx);
            });

        if (_ddQuality != null)
            _ddQuality.RegisterValueChangedCallback(evt =>
            {
                if (_muteSettingsCallbacks) return;
                int idx = (_ddQuality.choices != null) ? _ddQuality.choices.IndexOf(evt.newValue) : -1;
                MutateSettings(p => p.qualityLevel = idx);
            });

        if (_tVSync != null)
            _tVSync.RegisterValueChangedCallback(evt =>
            {
                if (_muteSettingsCallbacks) return;
                MutateSettings(p => p.vSync = evt.newValue);
            });

        if (_ddFpsCap != null)
            _ddFpsCap.RegisterValueChangedCallback(evt =>
            {
                if (_muteSettingsCallbacks) return;

                int fps = 0;
                if (int.TryParse(evt.newValue, out int parsed))
                    fps = parsed;

                MutateSettings(p => p.targetFps = fps);
            });

        if (_ddWindowMode != null)
            _ddWindowMode.RegisterValueChangedCallback(evt =>
            {
                if (_muteSettingsCallbacks) return;

                int mode = evt.newValue switch
                {
                    "Windowed" => (int)FullScreenMode.Windowed,
                    "Borderless" => (int)FullScreenMode.FullScreenWindow,
                    "Fullscreen" => (int)FullScreenMode.ExclusiveFullScreen,
                    _ => (int)FullScreenMode.FullScreenWindow
                };

                MutateSettings(p => p.fullScreenMode = mode);
            });

        if (_ddDisplay != null)
            _ddDisplay.RegisterValueChangedCallback(evt =>
            {
                if (_muteSettingsCallbacks) return;
                int idx = Mathf.Max(0, _ddDisplay.choices.IndexOf(evt.newValue));
                MutateSettings(p => p.displayIndex = idx);
            });

        if (_ddResolution != null)
            _ddResolution.RegisterValueChangedCallback(evt =>
            {
                if (_muteSettingsCallbacks) return;

                int idx = _ddResolution.choices.IndexOf(evt.newValue);
                var resolutions = Screen.resolutions;

                if (resolutions == null || idx < 0 || idx >= resolutions.Length)
                    return;

                var res = resolutions[idx];
#if UNITY_2022_2_OR_NEWER
                int hz = Mathf.RoundToInt((float)res.refreshRateRatio.value);
#else
                int hz = res.refreshRate;
#endif

                MutateSettings(p =>
                {
                    p.resolutionWidth = res.width;
                    p.resolutionHeight = res.height;
                    p.resolutionRefreshHz = hz;
                });
            });

        if (_sFov != null)
            _sFov.RegisterValueChangedCallback(evt =>
            {
                if (_muteSettingsCallbacks) return;
                MutateSettings(p => p.cameraFov = evt.newValue);
            });

        if (_ddUnits != null)
            _ddUnits.RegisterValueChangedCallback(evt =>
            {
                if (_muteSettingsCallbacks) return;
                int idx = Mathf.Max(0, _ddUnits.choices.IndexOf(evt.newValue));
                MutateSettings(p => p.speedUnitMode = idx);
            });
    }

    private void MutateSettings(Action<GameSettingsProfile> mutator)
    {
        var current = GameSettingsService.Current;
        var next = JsonUtility.FromJson<GameSettingsProfile>(JsonUtility.ToJson(current));

        mutator?.Invoke(next);
        next.Sanitize();

        GameSettingsService.Set(next, applyMinimal: true);
        GameSettingsService.Save();
    }

    private void RefreshSettingsUI(GameSettingsProfile profile)
    {
        if (profile == null)
            return;

        _muteSettingsCallbacks = true;

        if (_sMaster != null) _sMaster.SetValueWithoutNotify(profile.masterVolume);
        if (_sMusic != null) _sMusic.SetValueWithoutNotify(profile.musicVolume);
        if (_sSfx != null) _sSfx.SetValueWithoutNotify(profile.sfxVolume);

        if (_tInvertY != null) _tInvertY.SetValueWithoutNotify(profile.invertLookY);
        if (_sLookSens != null) _sLookSens.SetValueWithoutNotify(profile.lookSensitivity);

        if (_sUiScale != null) _sUiScale.SetValueWithoutNotify(profile.uiScale);
        if (_tReduceMotion != null) _tReduceMotion.SetValueWithoutNotify(profile.reduceMotion);

        if (_ddColourMode != null && _ddColourMode.choices != null && _ddColourMode.choices.Count > 0)
        {
            int idx = Mathf.Clamp(profile.colorAccessibilityMode, 0, _ddColourMode.choices.Count - 1);
            _ddColourMode.SetValueWithoutNotify(_ddColourMode.choices[idx]);
        }

        if (_lblMaster != null) _lblMaster.text = $"{Mathf.RoundToInt(profile.masterVolume * 100f)}%";
        if (_lblMusic != null) _lblMusic.text = $"{Mathf.RoundToInt(profile.musicVolume * 100f)}%";
        if (_lblSfx != null) _lblSfx.text = $"{Mathf.RoundToInt(profile.sfxVolume * 100f)}%";

        if (_lblLookSens != null) _lblLookSens.text = $"{profile.lookSensitivity:0.00}x";
        if (_lblUiScale != null) _lblUiScale.text = $"{Mathf.RoundToInt(profile.uiScale * 100f)}%";

        if (_ddQuality != null && _ddQuality.choices != null && _ddQuality.choices.Count > 0)
        {
            int q = profile.qualityLevel;
            if (q < 0 || q >= _ddQuality.choices.Count)
                q = Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, _ddQuality.choices.Count - 1);

            _ddQuality.SetValueWithoutNotify(_ddQuality.choices[q]);
        }

        if (_tVSync != null)
            _tVSync.SetValueWithoutNotify(profile.vSync);

        if (_ddFpsCap != null && _ddFpsCap.choices != null && _ddFpsCap.choices.Count > 0)
        {
            string v = (profile.targetFps <= 0) ? "Unlimited" : profile.targetFps.ToString();
            _ddFpsCap.SetValueWithoutNotify(_ddFpsCap.choices.Contains(v) ? v : "Unlimited");
        }

        if (_ddWindowMode != null && _ddWindowMode.choices != null && _ddWindowMode.choices.Count > 0)
        {
            string mode = ((FullScreenMode)profile.fullScreenMode) switch
            {
                FullScreenMode.Windowed => "Windowed",
                FullScreenMode.ExclusiveFullScreen => "Fullscreen",
                _ => "Borderless"
            };

            if (_ddWindowMode.choices.Contains(mode))
                _ddWindowMode.SetValueWithoutNotify(mode);
        }

        if (_ddDisplay != null && _ddDisplay.choices != null && _ddDisplay.choices.Count > 0)
        {
            int idx = Mathf.Clamp(profile.displayIndex, 0, _ddDisplay.choices.Count - 1);
            _ddDisplay.SetValueWithoutNotify(_ddDisplay.choices[idx]);
        }

        if (_ddResolution != null && _ddResolution.choices != null && _ddResolution.choices.Count > 0)
        {
            string best = null;
            var resolutions = Screen.resolutions;

            if (profile.resolutionWidth > 0 && profile.resolutionHeight > 0 && resolutions != null)
            {
                for (int i = 0; i < resolutions.Length && i < _ddResolution.choices.Count; i++)
                {
#if UNITY_2022_2_OR_NEWER
                    int hz = Mathf.RoundToInt((float)resolutions[i].refreshRateRatio.value);
#else
                    int hz = resolutions[i].refreshRate;
#endif
                    if (resolutions[i].width == profile.resolutionWidth &&
                        resolutions[i].height == profile.resolutionHeight &&
                        (profile.resolutionRefreshHz <= 0 || hz == profile.resolutionRefreshHz))
                    {
                        best = _ddResolution.choices[i];
                        break;
                    }
                }
            }

            if (best == null && _ddResolution.choices.Count > 0)
                best = _ddResolution.choices[_ddResolution.choices.Count - 1];

            _ddResolution.SetValueWithoutNotify(best);
        }

        if (_sFov != null) _sFov.SetValueWithoutNotify(profile.cameraFov);
        if (_lblFov != null) _lblFov.text = $"{Mathf.RoundToInt(profile.cameraFov)}";

        if (_ddUnits != null && _ddUnits.choices != null && _ddUnits.choices.Count > 0)
        {
            int idx = Mathf.Clamp(profile.speedUnitMode, 0, _ddUnits.choices.Count - 1);
            _ddUnits.SetValueWithoutNotify(_ddUnits.choices[idx]);
        }

        _muteSettingsCallbacks = false;
    }

    // =========================================================
    // Controls Rebinding
    // =========================================================

    private sealed class RebindRow
    {
        public string displayName;
        public InputAction action;
        public int bindingIndex;
    }

    private void RebuildControlsList()
    {
        if (_controlsRebindList == null)
            return;

        _controlsRebindList.Clear();
        _controlsRebindButtons.Clear();

        if (_lblControlsStatus != null)
            _lblControlsStatus.text = "Select an action to rebind.";

        if (_lblControlsNote != null)
            _lblControlsNote.text = "Changes are saved automatically.";

        var rows = CollectRebindRowsFromAsset();
        if (rows.Count == 0)
        {
            _controlsRebindList.Add(new Label("No rebindable actions found. Assign a rebindActionsAsset on MainMenuController."));
            return;
        }

        InputBindingOverridesStorage.ApplySavedOverrides(rebindActionsAsset);

        foreach (var r in rows)
        {
            var row = new VisualElement();
            row.AddToClassList("controls-rebind-row");

            var lblAction = new Label(r.displayName);
            lblAction.AddToClassList("controls-rebind-action");

            var lblBinding = new Label(GetReadableBinding(r.action, r.bindingIndex));
            lblBinding.AddToClassList("controls-rebind-binding");

            var btn = new Button { text = "Rebind" };
            btn.AddToClassList("controls-rebind-btn");

            var capturedRow = row;
            var capturedLabel = lblBinding;
            var capturedAction = r.action;
            int capturedBindingIndex = r.bindingIndex;

            btn.clicked += () => StartRebind(capturedAction, capturedBindingIndex, capturedLabel, capturedRow);

            _controlsRebindButtons.Add(btn);

            row.Add(lblAction);
            row.Add(lblBinding);
            row.Add(btn);

            _controlsRebindList.Add(row);
        }
    }

    private List<RebindRow> CollectRebindRowsFromAsset()
    {
        var rows = new List<RebindRow>();

        if (rebindActionsAsset == null)
            return rows;

        AddSimpleRow(rows, "Interact", FindActionByAliases("interact", "use", "liftinput", "resortaction", "exithold"));
        AddLeanRows(rows, FindActionByAliases("lean"));
        AddSimpleRow(rows, "Left Ski", FindActionByAliases("leftski"));
        AddSimpleRow(rows, "Right Ski", FindActionByAliases("rightski"));
        AddSimpleRow(rows, "Poles", FindActionByAliases("poles"));
        AddSimpleRow(rows, "Jump", FindActionByAliases("jump"));
        AddSimpleRow(rows, "Equip Skis", FindActionByAliases("equipskis", "equipski"));
        AddSimpleRow(rows, "Shop Orbit", FindActionByAliases("shoporbitpress", "shoporbit"));

        return rows;
    }

    private void AddSimpleRow(List<RebindRow> rows, string displayName, InputAction action)
    {
        if (action == null)
            return;

        rows.Add(new RebindRow
        {
            displayName = displayName,
            action = action,
            bindingIndex = FindPrimaryBindingIndex(action)
        });
    }

    private void AddLeanRows(List<RebindRow> rows, InputAction action)
    {
        if (action == null)
            return;

        int forward = FindCompositePartBindingIndex(action, "positive", "up", "forward");
        int backward = FindCompositePartBindingIndex(action, "negative", "down", "back", "backward");

        if (forward >= 0)
        {
            rows.Add(new RebindRow
            {
                displayName = "Lean Forward",
                action = action,
                bindingIndex = forward
            });
        }

        if (backward >= 0)
        {
            rows.Add(new RebindRow
            {
                displayName = "Lean Backward",
                action = action,
                bindingIndex = backward
            });
        }

        if (forward < 0 && backward < 0)
        {
            rows.Add(new RebindRow
            {
                displayName = "Lean",
                action = action,
                bindingIndex = FindPrimaryBindingIndex(action)
            });
        }
    }

    private InputAction FindActionByAliases(params string[] aliases)
    {
        if (rebindActionsAsset == null || aliases == null)
            return null;

        for (int i = 0; i < rebindActionsAsset.actionMaps.Count; i++)
        {
            var map = rebindActionsAsset.actionMaps[i];
            if (map == null) continue;

            for (int a = 0; a < map.actions.Count; a++)
            {
                var action = map.actions[a];
                if (action == null) continue;

                string normalized = Normalize(action.name);

                for (int j = 0; j < aliases.Length; j++)
                {
                    string alias = Normalize(aliases[j]);
                    if (normalized == alias || normalized.Contains(alias))
                        return action;
                }
            }
        }

        return null;
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .ToLowerInvariant();
    }

    private static int FindPrimaryBindingIndex(InputAction action)
    {
        if (action == null)
            return 0;

        for (int i = 0; i < action.bindings.Count; i++)
        {
            var b = action.bindings[i];
            if (b.isComposite || b.isPartOfComposite) continue;
            if (!string.IsNullOrEmpty(b.path)) return i;
        }

        return 0;
    }

    private static int FindCompositePartBindingIndex(InputAction action, params string[] partNames)
    {
        if (action == null)
            return -1;

        for (int i = 0; i < action.bindings.Count; i++)
        {
            var b = action.bindings[i];
            if (!b.isPartOfComposite) continue;

            for (int p = 0; p < partNames.Length; p++)
            {
                if (string.Equals(b.name, partNames[p], StringComparison.OrdinalIgnoreCase))
                    return i;
            }
        }

        return -1;
    }

    private static string GetReadableBinding(InputAction action, int bindingIndex)
    {
        if (action == null)
            return "-";

        if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
            bindingIndex = 0;

        return action.GetBindingDisplayString(bindingIndex, InputBinding.DisplayStringOptions.DontUseShortDisplayNames);
    }

    private void StartRebind(InputAction action, int bindingIndex, Label bindingLabel, VisualElement rowElement)
    {
        CancelActiveRebind();

        if (action == null)
            return;

        _activeRebindAction = action;
        _activeRebindBindingIndex = bindingIndex;
        _activeRebindRow = rowElement;
        _activeRebindBindingLabel = bindingLabel;

        if (_lblControlsStatus != null)
            _lblControlsStatus.text = $"Listening… press a key or button for {action.name}. Esc cancels.";

        if (_activeRebindRow != null)
            _activeRebindRow.AddToClassList("is-listening");

        if (_activeRebindBindingLabel != null)
        {
            _activeRebindBindingLabel.text = "…";
            _activeRebindBindingLabel.AddToClassList("is-listening");
        }

        for (int i = 0; i < _controlsRebindButtons.Count; i++)
            _controlsRebindButtons[i].SetEnabled(false);

        action.Disable();

        _activeRebind = action.PerformInteractiveRebinding(bindingIndex)
            .WithCancelingThrough("<Keyboard>/escape")
            .OnMatchWaitForAnother(0.1f)
            .OnCancel(op =>
            {
                action.Enable();
                op.Dispose();
                _activeRebind = null;

                EndListeningUI();

                if (_lblControlsNote != null)
                    _lblControlsNote.text = "Rebind cancelled.";

                if (_activeRebindBindingLabel != null)
                    _activeRebindBindingLabel.text = GetReadableBinding(action, bindingIndex);

                ClearActiveRebindRefs();
            })
            .OnComplete(op =>
            {
                action.Enable();
                op.Dispose();
                _activeRebind = null;

                if (rebindActionsAsset != null)
                    InputBindingOverridesStorage.SaveOverrides(rebindActionsAsset);

                EndListeningUI();

                if (_lblControlsNote != null)
                    _lblControlsNote.text = "Binding saved.";

                if (_activeRebindBindingLabel != null)
                    _activeRebindBindingLabel.text = GetReadableBinding(action, bindingIndex);

                ClearActiveRebindRefs();
            });

        _activeRebind.Start();
    }

    private void EndListeningUI()
    {
        if (_lblControlsStatus != null)
            _lblControlsStatus.text = "Select an action to rebind.";

        if (_activeRebindRow != null)
            _activeRebindRow.RemoveFromClassList("is-listening");

        if (_activeRebindBindingLabel != null)
            _activeRebindBindingLabel.RemoveFromClassList("is-listening");

        for (int i = 0; i < _controlsRebindButtons.Count; i++)
            _controlsRebindButtons[i].SetEnabled(true);
    }

    private void ClearActiveRebindRefs()
    {
        _activeRebindAction = null;
        _activeRebindBindingIndex = -1;
        _activeRebindRow = null;
        _activeRebindBindingLabel = null;
    }

    private void CancelActiveRebind()
    {
        if (_activeRebind == null)
            return;

        try { _activeRebind.Cancel(); }
        catch { }

        _activeRebind = null;
    }

    private void OnClickResetBindings()
    {
        CancelActiveRebind();
        EndListeningUI();
        ClearActiveRebindRefs();

        if (rebindActionsAsset != null)
        {
            rebindActionsAsset.RemoveAllBindingOverrides();
            InputBindingOverridesStorage.ClearOverrides();
        }

        if (_lblControlsNote != null)
            _lblControlsNote.text = "Bindings reset.";

        RebuildControlsList();
    }
}