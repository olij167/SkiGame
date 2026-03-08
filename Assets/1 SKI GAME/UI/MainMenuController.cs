using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

using SkiGame.Progression;
using UnityEngine;
using UnityEngine.UIElements;

using System.Globalization;


public sealed class MainMenuController : MonoBehaviour
{
    [Header("Scene Names")]
    [SerializeField] private string menuSceneName = "MainMenu";

    [Header("UI Toolkit")]
    [SerializeField] private UIDocument document;

    [Header("Background Loader")]
    [SerializeField] private MenuBackgroundLoader backgroundLoader;

    private const int SlotCount = GameSaveSystem.MaxSlots;

    private int _selectedSlot = 0;
    private GameSaveSystem.SaveManifest _manifest;

    // Panels
    private VisualElement _panelMain;
    private VisualElement _panelSaves;      // combined load/new
    private VisualElement _panelSettings;

    // Modals
    private VisualElement _modalNewConfirm;
    private VisualElement _modalDeleteConfirm;

    // Main buttons
    private Button _btnOpenPlay;
    private Button _btnOpenSettings;
    private Button _btnQuit;

    // Save panel buttons
    private Button _btnPrimaryAction; // Load or Create
    private Button _btnDeleteSlot;
    private Button _btnBackFromSaves;

    // Settings buttons
    private Button _btnBackFromSettings;
    private Button _btnResetSettings;
    private Button _btnSaveSettings;

    // Modal buttons
    private Button _btnConfirmNewGame;
    private Button _btnCancelNewGame;
    private Button _btnConfirmDelete;
    private Button _btnCancelDelete;

    // Active slot label
    private Label _labelActiveSlot;

    // Save preview labels
    private Label _previewTitle;
    private Label _previewSub;
    private Label _previewPlaythrough;
    private Label _previewCalendar;
    private Label _previewStats;

    // Settings fields (mirror PhoneHUD settings)
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

    private bool _muteSettingsCallbacks;

    private void Reset()
    {
        document = GetComponent<UIDocument>();
    }

    private void Awake()
    {
        if (document == null) document = GetComponent<UIDocument>();
        if (backgroundLoader == null) backgroundLoader = FindObjectOfType<MenuBackgroundLoader>();

        if (document == null)
        {
            Debug.LogError("[MainMenuController] Missing UIDocument.");
            enabled = false;
            return;
        }

        Bind(document.rootVisualElement);
        Wire();

        HideModal(_modalNewConfirm);
        HideModal(_modalDeleteConfirm);

        ShowPanel(_panelMain);
    }

    private void Start()
    {
        // Ensure settings are loaded so the main menu reflects the same values as the in-game PhoneHUD.
        GameSettingsService.EnsureLoaded();
        RefreshSettingsUIFromProfile(GameSettingsService.Current);

        _manifest = GameSaveSystem.LoadOrCreateManifest();

        // Keep a stable selected slot across boots.
        _selectedSlot = Mathf.Clamp(GameSaveSystem.ActiveSlotId, 0, SlotCount - 1);

        RefreshSlotsUI();
        UpdatePreviewForSelectedSlot();
    }

    private void OnEnable()
    {
        GameSettingsService.OnChanged += OnSettingsChanged;
    }

    private void OnDisable()
    {
        GameSettingsService.OnChanged -= OnSettingsChanged;
    }

    private void OnSettingsChanged(GameSettingsProfile p)
    {
        // Keep menu UI in sync if something else applies settings.
        RefreshSettingsUIFromProfile(p);
    }

    // -------------------- Bind / Wire --------------------

    private void Bind(VisualElement root)
    {
        _panelMain = root.Q<VisualElement>("Panel_Main");
        _panelSaves = root.Q<VisualElement>("Panel_Load"); // keep old name for minimal asset churn
        _panelSettings = root.Q<VisualElement>("Panel_Settings");

        _modalNewConfirm = root.Q<VisualElement>("Modal_NewConfirm");
        _modalDeleteConfirm = root.Q<VisualElement>("Modal_DeleteConfirm");

        // Nav buttons
        _btnOpenPlay = root.Q<Button>("Button_OpenLoad");
        _btnOpenSettings = root.Q<Button>("Button_OpenSettings");
        _btnQuit = root.Q<Button>("Button_Quit");

        // Save panel buttons
        _btnPrimaryAction = root.Q<Button>("Button_ConfirmLoad"); // now Load or Create
        _btnDeleteSlot = root.Q<Button>("Button_DeleteSlot");
        _btnBackFromSaves = root.Q<Button>("Button_BackFromLoad");

        // Settings panel buttons
        _btnBackFromSettings = root.Q<Button>("Button_BackFromSettings");
        _btnResetSettings = root.Q<Button>("Button_ResetSettings");
        _btnSaveSettings = root.Q<Button>("Button_SaveSettings");

        // Modals
        _btnConfirmNewGame = root.Q<Button>("Button_ConfirmNewGame");
        _btnCancelNewGame = root.Q<Button>("Button_CancelNewGame");
        _btnConfirmDelete = root.Q<Button>("Button_ConfirmDeleteSlot");
        _btnCancelDelete = root.Q<Button>("Button_CancelDeleteSlot");

        _labelActiveSlot = root.Q<Label>("Label_ActiveSlot");

        _previewTitle = root.Q<Label>("Load_Preview_Title");
        _previewSub = root.Q<Label>("Load_Preview_Sub");
        _previewPlaythrough = root.Q<Label>("Load_Preview_Playthrough");
        _previewCalendar = root.Q<Label>("Load_Preview_Calendar");
        _previewStats = root.Q<Label>("Load_Preview_Stats");

        // Slot selection buttons (Load_*)
        for (int i = 0; i < SlotCount; i++)
        {
            int idx = i;
            root.Q<Button>($"Load_SelectSlot_{i}")?.RegisterCallback<ClickEvent>(_ => SelectSlot(idx));
        }

        // -------- Settings fields --------
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

        PopulateSettingsDropdownChoices();
    }

    private void Wire()
    {
        _btnOpenPlay?.RegisterCallback<ClickEvent>(_ =>
        {
            ShowPanel(_panelSaves);
            RefreshSlotsUI();
            UpdatePreviewForSelectedSlot();
        });

        _btnOpenSettings?.RegisterCallback<ClickEvent>(_ => ShowPanel(_panelSettings));
        _btnQuit?.RegisterCallback<ClickEvent>(_ => Quit());

        _btnBackFromSaves?.RegisterCallback<ClickEvent>(_ => ShowPanel(_panelMain));
        _btnBackFromSettings?.RegisterCallback<ClickEvent>(_ => ShowPanel(_panelMain));

        _btnPrimaryAction?.RegisterCallback<ClickEvent>(_ => OnClickPrimarySaveAction());
        _btnDeleteSlot?.RegisterCallback<ClickEvent>(_ => ShowModal(_modalDeleteConfirm));

        // New modal
        _btnConfirmNewGame?.RegisterCallback<ClickEvent>(_ => ConfirmNewGame());
        _btnCancelNewGame?.RegisterCallback<ClickEvent>(_ => HideModal(_modalNewConfirm));

        // Delete modal
        _btnConfirmDelete?.RegisterCallback<ClickEvent>(_ => ConfirmDeleteSlot());
        _btnCancelDelete?.RegisterCallback<ClickEvent>(_ => HideModal(_modalDeleteConfirm));

        // Settings buttons
        _btnResetSettings?.RegisterCallback<ClickEvent>(_ =>
        {
            GameSettingsService.ResetToDefaults();
            GameSettingsService.Save();
        });

        _btnSaveSettings?.RegisterCallback<ClickEvent>(_ => GameSettingsService.Save());

        WireSettingsInputs();
    }

    // -------------------- UI State --------------------

    private void ShowPanel(VisualElement panelToShow)
    {
        SetVisible(_panelMain, false);
        //SetVisible(_panelMain, panelToShow == _panelMain);
        SetVisible(_panelSaves, panelToShow == _panelSaves);
        SetVisible(_panelSettings, panelToShow == _panelSettings);

        HideModal(_modalNewConfirm);
        HideModal(_modalDeleteConfirm);
    }

    private static void SetVisible(VisualElement ve, bool visible)
    {
        if (ve == null) return;
        ve.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private static void ShowModal(VisualElement modal)
    {
        if (modal == null) return;
        modal.RemoveFromClassList("hidden");
        modal.style.display = DisplayStyle.Flex;
    }

    private static void HideModal(VisualElement modal)
    {
        if (modal == null) return;
        modal.AddToClassList("hidden");
        modal.style.display = DisplayStyle.None;
    }

    // -------------------- Slot selection + display --------------------

    private void SelectSlot(int slotId)
    {
        _selectedSlot = Mathf.Clamp(slotId, 0, SlotCount - 1);
        GameSaveSystem.ActiveSlotId = _selectedSlot; // keep this synced for paths

        RefreshSlotsUI();
        UpdatePreviewForSelectedSlot();
    }

    private void RefreshSlotsUI()
    {
        _manifest = GameSaveSystem.LoadOrCreateManifest();

        for (int i = 0; i < SlotCount; i++)
        {
            var meta = GameSaveSystem.GetSlotMeta(_manifest, i);
            bool hasData = meta != null && meta.hasData;

            SetLabel($"Load_SlotName_{i}", meta?.displayName ?? $"Save {i + 1}");
            SetLabel($"Load_SlotStatus_{i}", hasData ? "Has Data" : "Empty");

            // Avoid hard-typing the timestamp field (it may be DateTime/string/long depending on your manifest struct).
            string lastPlayed = hasData ? Convert.ToString(meta.lastPlayedUtc) : "-";
            SetLabel($"Load_SlotLastPlayed_{i}", hasData ? ("Last Played: " + lastPlayed) : "-");

            ToggleSelected(document.rootVisualElement.Q<VisualElement>($"Load_SlotCard_{i}"), i == _selectedSlot);
        }

        if (_labelActiveSlot != null)
            _labelActiveSlot.text = $"Active Save: {_selectedSlot + 1}";
    }

    // -------------------- Save preview extraction helpers --------------------

    private static object TryGetMemberValue(object obj, params string[] names)
    {
        if (obj == null) return null;
        var t = obj.GetType();

        for (int i = 0; i < names.Length; i++)
        {
            string n = names[i];
            if (string.IsNullOrWhiteSpace(n)) continue;

            // Field
            var f = t.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null) return f.GetValue(obj);

            // Property
            var p = t.GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.CanRead) return p.GetValue(obj);
        }

        return null;
    }

    private static bool TryGetInt(object obj, out int value, params string[] names)
    {
        value = 0;
        var v = TryGetMemberValue(obj, names);
        if (v == null) return false;

        if (v is int i) { value = i; return true; }
        if (v is long l) { value = (int)l; return true; }
        if (v is float f) { value = Mathf.RoundToInt(f); return true; }
        if (v is double d) { value = (int)Math.Round(d); return true; }

        if (int.TryParse(Convert.ToString(v), out int parsed))
        {
            value = parsed;
            return true;
        }

        return false;
    }

    private static bool TryGetFloat(object obj, out float value, params string[] names)
    {
        value = 0f;
        var v = TryGetMemberValue(obj, names);
        if (v == null) return false;

        if (v is float f) { value = f; return true; }
        if (v is double d) { value = (float)d; return true; }
        if (v is int i) { value = i; return true; }
        if (v is long l) { value = l; return true; }

        if (float.TryParse(Convert.ToString(v), out float parsed))
        {
            value = parsed;
            return true;
        }

        return false;
    }

    private static string CoerceString(object v)
    {
        if (v == null) return null;
        var s = Convert.ToString(v);
        return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    // Best-effort JSON value pulls (works for simple Unity JsonUtility-like JSON).
    private static string TryExtractJsonString(string json, params string[] keys)
    {
        if (string.IsNullOrEmpty(json)) return null;

        for (int i = 0; i < keys.Length; i++)
        {
            var key = keys[i];
            if (string.IsNullOrWhiteSpace(key)) continue;

            // "key" : "value"
            var m = Regex.Match(json, $"\"{Regex.Escape(key)}\"\\s*:\\s*\"([^\"]*)\"");
            if (m.Success)
            {
                var val = m.Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(val)) return val.Trim();
            }
        }

        return null;
    }

    private static bool TryExtractJsonInt(string json, out int value, params string[] keys)
    {
        value = 0;
        if (string.IsNullOrEmpty(json)) return false;

        for (int i = 0; i < keys.Length; i++)
        {
            var key = keys[i];
            if (string.IsNullOrWhiteSpace(key)) continue;

            // "key" : 123
            var m = Regex.Match(json, $"\"{Regex.Escape(key)}\"\\s*:\\s*(-?\\d+)");
            if (m.Success && int.TryParse(m.Groups[1].Value, out value))
                return true;
        }

        return false;
    }

    private static bool TryExtractJsonFloat(string json, out float value, params string[] keys)
    {
        value = 0f;
        if (string.IsNullOrEmpty(json)) return false;

        for (int i = 0; i < keys.Length; i++)
        {
            var key = keys[i];
            if (string.IsNullOrWhiteSpace(key)) continue;

            // "key" : 123.45
            var m = Regex.Match(json, $"\"{Regex.Escape(key)}\"\\s*:\\s*(-?\\d+(?:\\.\\d+)?)");
            if (m.Success && float.TryParse(m.Groups[1].Value, out value))
                return true;
        }

        return false;
    }

    private bool TryLoadStatsProfileForSlot(int slot, out PlayerStatsProfile profile)
    {
        profile = null;

        string path = GameSaveSystem.GetProfilePath(slot);
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
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

    private static bool TryParseDayKeyToDate(int dayKey, out DateTime dateUtc)
    {
        // dayKey is described as a "UTC day key". In this project it is typically yyyymmdd.
        // Example: 20260220.
        dateUtc = default;

        if (dayKey <= 0) return false;

        string s = dayKey.ToString(CultureInfo.InvariantCulture);
        if (s.Length != 8) return false;

        return DateTime.TryParseExact(
            s,
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out dateUtc
        );
    }

    private static string FormatCalendarFromProfile(PlayerStatsProfile p)
    {
        if (p == null) return "-";

        // If either start component is unset, we can still show date if dayKey is valid.
        bool hasStart = p.playthrough != null
                        && p.playthrough.calendarStartYear > 0
                        && p.playthrough.calendarStartDayOfYear > 0;

        int dayKey = p.dailyTasks != null ? p.dailyTasks.dayKey : -1;

        if (!TryParseDayKeyToDate(dayKey, out var currentDateUtc))
        {
            // No dayKey; still show something stable if we have a start date.
            if (hasStart)
                return $"Year {p.playthrough.calendarStartYear} • Day {p.playthrough.calendarStartDayOfYear}";
            return "-";
        }

        // Always show the real-world date for clarity
        string datePart = currentDateUtc.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

        if (!hasStart)
            return datePart;

        // Build start date from year + day-of-year
        var startDateUtc = new DateTime(p.playthrough.calendarStartYear, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddDays(p.playthrough.calendarStartDayOfYear - 1);

        int daysSinceStart = Mathf.Max(0, (int)(currentDateUtc.Date - startDateUtc.Date).TotalDays);

        int week = (daysSinceStart / 7) + 1;
        int day = (daysSinceStart % 7) + 1;

        return $"Week {week} • Day {day} • {datePart}";
    }

    private static string FormatStatsFromProfile(PlayerStatsProfile p)
    {
        if (p == null || p.lifetime == null) return "-";

        int runs = p.lifetime.totalRunsCompleted;

        float km = p.lifetime.totalDistanceMeters / 1000f;

        // Your profile tracks ascent + descent; for skiing "descent" is typically the headline stat.
        float vertM = p.lifetime.totalVerticalDescentMeters;

        // If literally nothing recorded, keep it minimal.
        if (runs <= 0 && km <= 0.01f && vertM <= 0.01f)
            return "-";

        return $"{runs} runs • {km:0.#} km • {vertM:0} m vert";
    }

    private void UpdatePreviewForSelectedSlot()
    {
        bool hasData = File.Exists(GameSaveSystem.GetProfilePath(_selectedSlot));

        if (_previewTitle != null) _previewTitle.text = hasData ? $"Save {_selectedSlot + 1}" : $"Empty Slot {_selectedSlot + 1}";
        if (_previewSub != null) _previewSub.text = hasData ? "Ready to load." : "No data found. Create a new game in this slot.";

        if (!hasData)
        {
            if (_previewPlaythrough != null) _previewPlaythrough.text = "-";
            if (_previewCalendar != null) _previewCalendar.text = "-";
            if (_previewStats != null) _previewStats.text = "-";
        }
        else
        {
            // Load the slot’s PlayerStatsProfile JSON and populate preview.
            if (TryLoadStatsProfileForSlot(_selectedSlot, out var profile))
            {
                if (_previewPlaythrough != null)
                {
                    int id = profile.playthrough != null ? profile.playthrough.playthroughId : 0;
                    _previewPlaythrough.text = $"Playthrough {id + 1}";
                }

                if (_previewCalendar != null)
                    _previewCalendar.text = FormatCalendarFromProfile(profile);

                if (_previewStats != null)
                    _previewStats.text = FormatStatsFromProfile(profile);
            }
            else
            {
                // Profile exists but couldn't be parsed as PlayerStatsProfile JSON (or is corrupt/old format)
                if (_previewPlaythrough != null) _previewPlaythrough.text = "—";
                if (_previewCalendar != null) _previewCalendar.text = "—";
                if (_previewStats != null) _previewStats.text = "—";
            }
        }

        if (_btnPrimaryAction != null)
        {
            _btnPrimaryAction.text = hasData ? "Load" : "Create";
            _btnPrimaryAction.SetEnabled(true);
        }

        if (_btnDeleteSlot != null)
            _btnDeleteSlot.SetEnabled(hasData);
    }

    private void OnClickPrimarySaveAction()
    {
        bool hasData = File.Exists(GameSaveSystem.GetProfilePath(_selectedSlot));
        if (hasData)
        {
            ConfirmLoadGame();
        }
        else
        {
            // Confirm create (even though empty, keep flow consistent)
            ShowModal(_modalNewConfirm);
        }
    }

    private void SetLabel(string name, string text)
    {
        var l = document.rootVisualElement.Q<Label>(name);
        if (l != null) l.text = text;
    }

    private static void ToggleSelected(VisualElement ve, bool selected)
    {
        if (ve == null) return;
        const string cls = "slot-selected";
        if (selected) ve.AddToClassList(cls);
        else ve.RemoveFromClassList(cls);
    }

    // -------------------- Actions --------------------

    private void ConfirmNewGame()
    {
        HideModal(_modalNewConfirm);

        GameStartBootstrap.RequestNewGame(_selectedSlot);
        StartCoroutine(EnterGameRoutine());
    }

    private void ConfirmLoadGame()
    {
        bool hasData = File.Exists(GameSaveSystem.GetProfilePath(_selectedSlot));
        if (!hasData)
        {
            Debug.LogWarning($"[MainMenuController] Tried to load empty slot {_selectedSlot}. Aborting.");
            return;
        }

        GameStartBootstrap.RequestLoadGame(_selectedSlot);
        StartCoroutine(EnterGameRoutine());
    }

    private void ConfirmDeleteSlot()
    {
        HideModal(_modalDeleteConfirm);

        GameSaveSystem.DeleteSlot(_selectedSlot);
        _manifest = GameSaveSystem.LoadOrCreateManifest();
        RefreshSlotsUI();
        UpdatePreviewForSelectedSlot();
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

    // =====================================================================
    // Settings (same model as PhoneHUD)
    // =====================================================================

    private void PopulateSettingsDropdownChoices()
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
            _ddFpsCap.choices = new List<string> { "Unlimited", "60", "120", "144", "240" };

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
            var res = Screen.resolutions;
            if (res != null && res.Length > 0)
            {
                for (int i = 0; i < res.Length; i++)
                {
#if UNITY_2022_2_OR_NEWER
                    int hz = (int)Mathf.Round((float)res[i].refreshRateRatio.value);
#else
                    int hz = res[i].refreshRate;
#endif
                    _ddResolution.choices.Add($"{res[i].width}x{res[i].height} @ {hz}Hz");
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
        if (_sMaster != null) _sMaster.RegisterValueChangedCallback(evt => { if (!_muteSettingsCallbacks) MutateSettings(p => p.masterVolume = evt.newValue); });
        if (_sMusic != null) _sMusic.RegisterValueChangedCallback(evt => { if (!_muteSettingsCallbacks) MutateSettings(p => p.musicVolume = evt.newValue); });
        if (_sSfx != null) _sSfx.RegisterValueChangedCallback(evt => { if (!_muteSettingsCallbacks) MutateSettings(p => p.sfxVolume = evt.newValue); });

        if (_tInvertY != null) _tInvertY.RegisterValueChangedCallback(evt => { if (!_muteSettingsCallbacks) MutateSettings(p => p.invertLookY = evt.newValue); });
        if (_sLookSens != null) _sLookSens.RegisterValueChangedCallback(evt => { if (!_muteSettingsCallbacks) MutateSettings(p => p.lookSensitivity = evt.newValue); });

        if (_sUiScale != null) _sUiScale.RegisterValueChangedCallback(evt => { if (!_muteSettingsCallbacks) MutateSettings(p => p.uiScale = evt.newValue); });
        if (_tReduceMotion != null) _tReduceMotion.RegisterValueChangedCallback(evt => { if (!_muteSettingsCallbacks) MutateSettings(p => p.reduceMotion = evt.newValue); });

        if (_ddColourMode != null)
        {
            _ddColourMode.RegisterValueChangedCallback(evt =>
            {
                if (_muteSettingsCallbacks) return;
                int idx = Mathf.Clamp(_ddColourMode.choices.IndexOf(evt.newValue), 0, _ddColourMode.choices.Count - 1);
                MutateSettings(p => p.colorAccessibilityMode = idx);
            });
        }

        if (_ddQuality != null)
        {
            _ddQuality.RegisterValueChangedCallback(evt =>
            {
                if (_muteSettingsCallbacks) return;
                int idx = (_ddQuality.choices != null) ? _ddQuality.choices.IndexOf(evt.newValue) : -1;
                MutateSettings(p => p.qualityLevel = idx);
            });
        }

        if (_tVSync != null) _tVSync.RegisterValueChangedCallback(evt => { if (!_muteSettingsCallbacks) MutateSettings(p => p.vSync = evt.newValue); });

        if (_ddFpsCap != null)
        {
            _ddFpsCap.RegisterValueChangedCallback(evt =>
            {
                if (_muteSettingsCallbacks) return;
                int fps = 0;
                if (int.TryParse(evt.newValue, out int parsed)) fps = parsed;
                MutateSettings(p => p.targetFps = fps);
            });
        }

        if (_ddWindowMode != null)
        {
            _ddWindowMode.RegisterValueChangedCallback(evt =>
            {
                if (_muteSettingsCallbacks) return;
                int idx = _ddWindowMode.choices.IndexOf(evt.newValue);
                int mode = idx switch
                {
                    0 => (int)FullScreenMode.Windowed,
                    1 => (int)FullScreenMode.FullScreenWindow,
                    2 => (int)FullScreenMode.ExclusiveFullScreen,
                    _ => (int)FullScreenMode.FullScreenWindow
                };
                MutateSettings(p => p.fullScreenMode = mode);
            });
        }

        if (_ddDisplay != null)
        {
            _ddDisplay.RegisterValueChangedCallback(evt =>
            {
                if (_muteSettingsCallbacks) return;
                int idx = Mathf.Max(0, _ddDisplay.choices.IndexOf(evt.newValue));
                MutateSettings(p => p.displayIndex = idx);
            });
        }

        if (_ddResolution != null)
        {
            _ddResolution.RegisterValueChangedCallback(evt =>
            {
                if (_muteSettingsCallbacks) return;
                int idx = _ddResolution.choices.IndexOf(evt.newValue);
                var res = Screen.resolutions;
                if (res != null && idx >= 0 && idx < res.Length)
                {
                    var r = res[idx];
#if UNITY_2022_2_OR_NEWER
                    int hz = (int)Mathf.Round((float)r.refreshRateRatio.value);
#else
                    int hz = r.refreshRate;
#endif
                    MutateSettings(p =>
                    {
                        p.resolutionWidth = r.width;
                        p.resolutionHeight = r.height;
                        p.resolutionRefreshHz = hz;
                    });
                }
            });
        }

        if (_sFov != null) _sFov.RegisterValueChangedCallback(evt => { if (!_muteSettingsCallbacks) MutateSettings(p => p.cameraFov = evt.newValue); });

        if (_ddUnits != null)
        {
            _ddUnits.RegisterValueChangedCallback(evt =>
            {
                if (_muteSettingsCallbacks) return;
                int idx = Mathf.Max(0, _ddUnits.choices.IndexOf(evt.newValue));
                MutateSettings(p => p.speedUnitMode = idx);
            });
        }
    }

    private void MutateSettings(Action<GameSettingsProfile> mutator)
    {
        var p = GameSettingsService.Current;

        // clone via JSON to avoid mutating shared instance
        var next = JsonUtility.FromJson<GameSettingsProfile>(JsonUtility.ToJson(p));

        mutator?.Invoke(next);
        next.Sanitize();

        GameSettingsService.Set(next, applyMinimal: true);
        GameSettingsService.Save();
    }

    private void RefreshSettingsUIFromProfile(GameSettingsProfile p)
    {
        if (p == null) return;

        _muteSettingsCallbacks = true;

        if (_sMaster != null) _sMaster.SetValueWithoutNotify(p.masterVolume);
        if (_sMusic != null) _sMusic.SetValueWithoutNotify(p.musicVolume);
        if (_sSfx != null) _sSfx.SetValueWithoutNotify(p.sfxVolume);

        if (_tInvertY != null) _tInvertY.SetValueWithoutNotify(p.invertLookY);
        if (_sLookSens != null) _sLookSens.SetValueWithoutNotify(p.lookSensitivity);

        if (_sUiScale != null) _sUiScale.SetValueWithoutNotify(p.uiScale);
        if (_tReduceMotion != null) _tReduceMotion.SetValueWithoutNotify(p.reduceMotion);

        if (_ddColourMode != null && _ddColourMode.choices != null && _ddColourMode.choices.Count > 0)
        {
            int idx = Mathf.Clamp(p.colorAccessibilityMode, 0, _ddColourMode.choices.Count - 1);
            _ddColourMode.SetValueWithoutNotify(_ddColourMode.choices[idx]);
        }

        if (_lblMaster != null) _lblMaster.text = $"{Mathf.RoundToInt(p.masterVolume * 100f)}%";
        if (_lblMusic != null) _lblMusic.text = $"{Mathf.RoundToInt(p.musicVolume * 100f)}%";
        if (_lblSfx != null) _lblSfx.text = $"{Mathf.RoundToInt(p.sfxVolume * 100f)}%";

        if (_lblLookSens != null) _lblLookSens.text = $"{p.lookSensitivity:0.00}x";
        if (_lblUiScale != null) _lblUiScale.text = $"{Mathf.RoundToInt(p.uiScale * 100f)}%";

        if (_ddQuality != null && _ddQuality.choices != null && _ddQuality.choices.Count > 0)
        {
            int q = p.qualityLevel;
            if (q < 0 || q >= _ddQuality.choices.Count) q = QualitySettings.GetQualityLevel();
            q = Mathf.Clamp(q, 0, _ddQuality.choices.Count - 1);
            _ddQuality.SetValueWithoutNotify(_ddQuality.choices[q]);
        }

        if (_tVSync != null) _tVSync.SetValueWithoutNotify(p.vSync);

        if (_ddFpsCap != null && _ddFpsCap.choices != null && _ddFpsCap.choices.Count > 0)
        {
            string v = (p.targetFps <= 0) ? "Unlimited" : p.targetFps.ToString();
            _ddFpsCap.SetValueWithoutNotify(_ddFpsCap.choices.Contains(v) ? v : "Unlimited");
        }

        if (_ddWindowMode != null && _ddWindowMode.choices != null && _ddWindowMode.choices.Count > 0)
        {
            string mode = ((FullScreenMode)p.fullScreenMode) switch
            {
                FullScreenMode.Windowed => "Windowed",
                FullScreenMode.FullScreenWindow => "Borderless",
                FullScreenMode.ExclusiveFullScreen => "Fullscreen",
                _ => "Borderless"
            };
            if (_ddWindowMode.choices.Contains(mode))
                _ddWindowMode.SetValueWithoutNotify(mode);
        }

        if (_ddDisplay != null && _ddDisplay.choices != null && _ddDisplay.choices.Count > 0)
        {
            int idx = Mathf.Clamp(p.displayIndex, 0, _ddDisplay.choices.Count - 1);
            _ddDisplay.SetValueWithoutNotify(_ddDisplay.choices[idx]);
        }

        if (_ddResolution != null && _ddResolution.choices != null && _ddResolution.choices.Count > 0)
        {
            // Find closest match (width/height + optional refresh).
            string match = null;
            var res = Screen.resolutions;

            if (res != null && res.Length > 0 && p.resolutionWidth > 0 && p.resolutionHeight > 0)
            {
                for (int i = 0; i < res.Length; i++)
                {
#if UNITY_2022_2_OR_NEWER
                    int hz = (int)Mathf.Round((float)res[i].refreshRateRatio.value);
#else
                    int hz = res[i].refreshRate;
#endif
                    if (res[i].width != p.resolutionWidth || res[i].height != p.resolutionHeight) continue;
                    if (p.resolutionRefreshHz > 0 && hz != p.resolutionRefreshHz) continue;
                    match = $"{res[i].width}x{res[i].height} @ {hz}Hz";
                    break;
                }
            }

            _ddResolution.SetValueWithoutNotify(!string.IsNullOrEmpty(match) && _ddResolution.choices.Contains(match)
                ? match
                : _ddResolution.choices[0]);
        }

        if (_sFov != null) _sFov.SetValueWithoutNotify(p.cameraFov);
        if (_lblFov != null) _lblFov.text = $"{Mathf.RoundToInt(p.cameraFov)}";

        if (_ddUnits != null && _ddUnits.choices != null && _ddUnits.choices.Count > 0)
        {
            int idx = Mathf.Clamp(p.speedUnitMode, 0, _ddUnits.choices.Count - 1);
            _ddUnits.SetValueWithoutNotify(_ddUnits.choices[idx]);
        }

        _muteSettingsCallbacks = false;
    }
}
