using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using SkiGame.UI;

namespace SkiGame.Progression
{
    public sealed class PauseMenuController : MonoBehaviour
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument document;

        [Header("Input (optional)")]
        [Tooltip("Optional: bind to a Pause/Menu action. If null, we fall back to Keyboard Escape.")]
        [SerializeField] private InputActionReference pauseAction;

        [Header("Scenes")]
        [SerializeField] private string mainMenuSceneName = "Menu";

        [Header("Rebinding")]
        [SerializeField] private InputActionAsset rebindActionsAsset;

        [Header("Availability Gate")]
        [SerializeField] private bool requireProfileLoaded = true;

        private bool _profileLoaded;

        [Header("Behaviour")]
        [SerializeField] private MonoBehaviour phoneHudControllerToSuppress;

        private VisualElement _root;
        private VisualElement _pauseRoot;

        private VisualElement _panelMain;
        private VisualElement _panelSettings;
        private VisualElement _panelControls;

        private Button _btnResume;
        private Button _btnOpenSettings;
        private Button _btnOpenControls;
        private Button _btnQuitToMenu;
        private Button _btnQuitToDesktop;

        private Button _btnBackFromSettings;
        private Button _btnBackFromControls;

        // Settings controls (subset shown; keep wiring if you need it)
        private Button _btnSettingsReset;

        // =====================================================================
        // Settings (ported from PhoneHUD so dropdowns/labels behave identically)
        // =====================================================================
        private Slider _sMaster, _sMusic, _sSfx;
        private Label _lblMaster, _lblMusic, _lblSfx;

        private Toggle _tInvertY;
        private Slider _sLookSens;
        private Label _lblLookSens;

        private Slider _sUiScale;
        private Label _lblUiScale;
        private Toggle _tReduceMotion;

        private Toggle _tShowTutorialOverlay;
        private Toggle _tShowMovementInputs;
        private Toggle _tShowWorldPrompts;

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

        private Label _lblSettingsNote;

        private bool _settingsDirty;
        private float _settingsNextSaveTime;

        // =====================================================================
        // Controls UI (ported from PhoneHUD for intuitive list + listening state)
        // =====================================================================
        private Label _lblControlsNote;
        private readonly List<Button> _controlsRebindButtons = new();
        private InputAction _activeRebindAction;
        private int _activeRebindBindingIndex = -1;
        private VisualElement _activeRebindRow;
        private Label _activeRebindBindingLabel;

        private bool _settingsBound;
        private bool _controlsBound;
        // Controls page
        private Label _lblControlsStatus;
        private VisualElement _controlsRebindList;
        private Button _btnControlsResetBindings;

        private InputActionRebindingExtensions.RebindingOperation _activeRebind;

        private bool _isOpen;
        private bool _bound;

        private void Awake()
        {
            // Be tolerant of scene setup: controller can be on a sibling of the UIDocument
            if (document == null)
                document = GetComponent<UIDocument>() ?? GetComponentInChildren<UIDocument>(true);

            if (document == null)
                document = FindObjectOfType<UIDocument>(includeInactive: true);

            // Hard-hide immediately so it never flashes on scene load
            var r = document != null ? document.rootVisualElement : null;
            if (r != null) r.style.display = DisplayStyle.Flex; // keep document alive for layout

            // We hide PauseRoot (not the document) once the tree exists; if tree doesn't exist yet, BindNextFrame handles it.
        }
        private void OnEnable()
        {
            // Bind after UI Toolkit has created the visual tree
            // (in practice Awake/OnEnable is often fine, but this avoids “null Q” races)
            StartCoroutine(BindNextFrame());

            if (pauseAction != null && pauseAction.action != null)
            {
                pauseAction.action.Enable();
                pauseAction.action.started += OnPauseAction;
                pauseAction.action.performed += OnPauseAction;
            }
        }

        private void OnDisable()
        {
            if (pauseAction != null && pauseAction.action != null)
            {
                pauseAction.action.started -= OnPauseAction;
                pauseAction.action.performed -= OnPauseAction;
            }

            CancelActiveRebind();
            GameCursorService.Release(this);
        }

        private System.Collections.IEnumerator BindNextFrame()
        {
            yield return null; // wait one frame for visual tree
            BindUI();
            SetOpen(false, force: true);
        }

        public void NotifyProfileLoaded()
        {
            _profileLoaded = true;
        }

        private bool CanOpen()
        {
            if (!requireProfileLoaded) return true;
            return _profileLoaded;
        }

        private void Update()
        {
            // Keyboard fallback ALWAYS works even if InputActionReference is misconfigured
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                // Escape should always close immediately if open; opening is gated
                if (_isOpen) SetOpen(false);
                else if (CanOpen()) SetOpen(true);
            }

            // Debounced settings save (use unscaled time since game is paused)
            if (_isOpen && _settingsDirty && Time.unscaledTime >= _settingsNextSaveTime)
            {
                _settingsDirty = false;
                GameSettingsService.Save();
                if (_lblSettingsNote != null) _lblSettingsNote.text = "";
            }
        }

        private void OnPauseAction(InputAction.CallbackContext ctx)
        {
            // If already open, always allow close
            if (_isOpen)
            {
                SetOpen(false);
                return;
            }

            // If closed, only open if allowed
            if (!CanOpen())
                return;

            SetOpen(true);
        }

        private void BindUI()
        {
            if (_bound) return;

            _root = document != null ? document.rootVisualElement : null;
            if (_root == null) return;

            _pauseRoot = _root.Q<VisualElement>("PauseRoot");
            if (_pauseRoot == null)
            {
                Debug.LogError("[PauseMenuController] Could not find VisualElement 'PauseRoot' in PauseMenu.uxml. UI will not show.");
            }
            else
            {
                // Ensure hidden by default
                _pauseRoot.style.display = DisplayStyle.None;
            }

            _panelMain = _root.Q<VisualElement>("Panel_PauseMain");
            _panelSettings = _root.Q<VisualElement>("Panel_PauseSettings");
            _panelControls = _root.Q<VisualElement>("Panel_PauseControls");

            _btnResume = _root.Q<Button>("Btn_PauseResume");
            _btnOpenSettings = _root.Q<Button>("Btn_PauseOpenSettings");
            _btnOpenControls = _root.Q<Button>("Btn_PauseOpenControls");
            _btnQuitToMenu = _root.Q<Button>("Btn_PauseQuitToMenu");
            _btnQuitToDesktop = _root.Q<Button>("Btn_PauseQuitToDesktop");

            _btnBackFromSettings = _root.Q<Button>("Btn_PauseBackFromSettings");
            _btnBackFromControls = _root.Q<Button>("Btn_PauseBackFromControls");

            _btnSettingsReset = _root.Q<Button>("Btn_SettingsReset");

            _lblControlsStatus = _root.Q<Label>("Lbl_ControlsStatus");
            _controlsRebindList = _root.Q<VisualElement>("ControlsRebindList");
            _btnControlsResetBindings = _root.Q<Button>("Btn_ControlsResetBindings");

            // --- Settings element queries ---
            _sMaster = _root.Q<Slider>("Slider_SettingsMaster");
            _sMusic = _root.Q<Slider>("Slider_SettingsMusic");
            _sSfx = _root.Q<Slider>("Slider_SettingsSfx");
            _lblMaster = _root.Q<Label>("Lbl_SettingsMaster");
            _lblMusic = _root.Q<Label>("Lbl_SettingsMusic");
            _lblSfx = _root.Q<Label>("Lbl_SettingsSfx");

            _tInvertY = _root.Q<Toggle>("Toggle_SettingsInvertY");
            _sLookSens = _root.Q<Slider>("Slider_SettingsLookSensitivity");
            _lblLookSens = _root.Q<Label>("Lbl_SettingsLookSensitivity");

            _sUiScale = _root.Q<Slider>("Slider_SettingsUiScale");
            _lblUiScale = _root.Q<Label>("Lbl_SettingsUiScale");
            _tReduceMotion = _root.Q<Toggle>("Toggle_SettingsReduceMotion");

            _tShowTutorialOverlay = _root.Q<Toggle>("Toggle_SettingsShowTutorialOverlay");
            _tShowMovementInputs = _root.Q<Toggle>("Toggle_SettingsShowMovementInputs");
            _tShowWorldPrompts = _root.Q<Toggle>("Toggle_SettingsShowWorldPrompts");

            _ddColourMode = _root.Q<DropdownField>("Dropdown_SettingsColourMode");
            _lblSettingsNote = _root.Q<Label>("Lbl_SettingsNote");

            _ddQuality = _root.Q<DropdownField>("Dropdown_SettingsQuality");
            _tVSync = _root.Q<Toggle>("Toggle_SettingsVSync");
            _ddFpsCap = _root.Q<DropdownField>("Dropdown_SettingsFpsCap");
            _ddResolution = _root.Q<DropdownField>("Dropdown_SettingsResolution");
            _ddWindowMode = _root.Q<DropdownField>("Dropdown_SettingsWindowMode");
            _ddDisplay = _root.Q<DropdownField>("Dropdown_SettingsDisplay");

            _sFov = _root.Q<Slider>("Slider_SettingsFov");
            _lblFov = _root.Q<Label>("Lbl_SettingsFov");

            _ddUnits = _root.Q<DropdownField>("Dropdown_SettingsUnits");

            // --- Controls element queries ---
            _lblControlsNote = _root.Q<Label>("Lbl_ControlsNote");

            // Wire reset (ensure only once)
            if (_btnSettingsReset != null) _btnSettingsReset.clicked += OnClickSettingsReset;
            if (_btnControlsResetBindings != null) _btnControlsResetBindings.clicked += OnClickResetBindings;
            // Wire buttons (explicit null checks; no ?.clicked)
            if (_btnResume != null) _btnResume.clicked += () => SetOpen(false);
            if (_btnOpenSettings != null) _btnOpenSettings.clicked += ShowSettings;
            if (_btnOpenControls != null) _btnOpenControls.clicked += ShowControls;
            if (_btnQuitToMenu != null) _btnQuitToMenu.clicked += QuitToMenu;
            if (_btnQuitToDesktop != null) _btnQuitToDesktop.clicked += QuitToDesktop;

            if (_btnBackFromSettings != null) _btnBackFromSettings.clicked += ShowMain;
            if (_btnBackFromControls != null) _btnBackFromControls.clicked += ShowMain;

            if (_btnControlsResetBindings != null) _btnControlsResetBindings.clicked += OnClickResetBindings;

            _bound = true;

            BindSettingsPageIfNeeded();
            BindControlsPageIfNeeded();
        }

        private void SetOpen(bool open, bool force = false)
        {
            if (!force && _isOpen == open) return;

            // Hard gate: never open before profile loaded
            if (open && !CanOpen())
                return;

            // Ensure UI is bound BEFORE we pause time
            if (!_bound)
                BindUI();

            if (open)
            {
                // If we can't show UI, do NOT pause the game (prevents "paused with no menu")
                if (_pauseRoot == null)
                {
                    Debug.LogError("[PauseMenuController] Tried to open pause menu but PauseRoot is missing. Aborting pause.");
                    return;
                }

                _isOpen = true;

                _pauseRoot.style.display = DisplayStyle.Flex;

                Time.timeScale = 0f;

                GameCursorService.Request(this, GameCursorMode.VisibleUnlocked, priority: 900);

                if (phoneHudControllerToSuppress != null)
                    phoneHudControllerToSuppress.enabled = false;

                ShowMain();

                // Ensure settings + controls pages are bound like PhoneHUD.
                BindSettingsPageIfNeeded();
                BindControlsPageIfNeeded();

                // Rebuild the curated controls list on open so it always matches current action refs.
                RebuildControlsList();
            }
            else
            {
                _isOpen = false;

                if (_pauseRoot != null)
                    _pauseRoot.style.display = DisplayStyle.None;

                CancelActiveRebind();
                Time.timeScale = 1f;

                GameCursorService.Release(this);

                if (phoneHudControllerToSuppress != null)
                    phoneHudControllerToSuppress.enabled = true;
            }
        }

        private static void SetPanel(VisualElement ve, bool show)
        {
            if (ve == null) return;
            ve.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void ShowMain()
        {
            SetPanel(_panelMain, true);
            SetPanel(_panelSettings, false);
            SetPanel(_panelControls, false);
        }

        private void ShowSettings()
        {
            BindSettingsPageIfNeeded();

            SetPanel(_panelMain, false);
            SetPanel(_panelSettings, true);
            SetPanel(_panelControls, false);
        }

        private void ShowControls()
        {
            BindControlsPageIfNeeded();
            RebuildControlsList();

            SetPanel(_panelMain, false);
            SetPanel(_panelSettings, false);
            SetPanel(_panelControls, true);
        }
        // ---------------- Controls Rebinding (minimal) ----------------

        // =====================================================================
        // Controls page (rebinding UI) - PhoneHUD style (curated, ordered, intuitive)
        // =====================================================================
        private void BindControlsPageIfNeeded()
        {
            if (_controlsBound) return;
            _controlsBound = true;

            if (_lblControlsStatus != null)
            {
                _lblControlsStatus.text = "Select an action to rebind.";
                _lblControlsStatus.RemoveFromClassList("is-listening");
            }

            RebuildControlsList();
        }

        private sealed class RebindRow
        {
            public string displayName;
            public InputAction action;
            public int bindingIndex;
        }

        private void RebuildControlsList()
        {
            if (_controlsRebindList == null) return;

            _controlsRebindList.Clear();
            _controlsRebindButtons.Clear();

            var rows = CollectRebindRows();
            if (rows.Count == 0)
            {
                _controlsRebindList.Add(new Label("No input actions found in the scene."));
                return;
            }

            // Apply saved overrides on the owning asset (if present)
            var anyAsset = rows[0].action?.actionMap?.asset;
            if (anyAsset != null)
                InputBindingOverridesStorage.ApplySavedOverrides(anyAsset);

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

                btn.clicked += () => StartRebind(r.action, r.bindingIndex, capturedLabel, capturedRow);

                _controlsRebindButtons.Add(btn);

                row.Add(lblAction);
                row.Add(lblBinding);
                row.Add(btn);

                _controlsRebindList.Add(row);
            }
        }

        private List<RebindRow> CollectRebindRows()
        {
            // NOTE: This matches PhoneHUD's approach: scan scene components for InputActionReference fields,
            // then convert to friendly names and ordered list.

            var rows = new Dictionary<string, RebindRow>(64);

            // These types exist in your project (PhoneHUD uses them)
            TryCollectFrom(FindObjectOfType<SkiController>(true), rows);
            TryCollectFrom(FindObjectOfType<CameraController>(true), rows);
            TryCollectFrom(FindObjectOfType<LiftRider>(true), rows);
            TryCollectFrom(FindObjectOfType<SkiResortHoldInteractor>(true), rows);
            TryCollectFrom(FindObjectOfType<CustomizationPortal>(true), rows);
            TryCollectFrom(FindObjectOfType<CustomizationSceneBootstrap>(true), rows);

            var list = new List<RebindRow>(rows.Values);

            int Rank(string name)
            {
                return name switch
                {
                    "Interact" => 0,
                    "Lean Forward" => 1,
                    "Lean Backward" => 2,
                    "Left Ski" => 3,
                    "Right Ski" => 4,
                    "Poles" => 5,
                    "Jump" => 6,
                    "Equip Skis" => 7,
                    "Shop Orbit" => 8,
                    _ => 100
                };
            }

            list.Sort((a, b) =>
            {
                int ra = Rank(a.displayName);
                int rb = Rank(b.displayName);
                if (ra != rb) return ra.CompareTo(rb);
                return string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase);
            });

            return list;
        }

        private void TryCollectFrom(MonoBehaviour component, Dictionary<string, RebindRow> rows)
        {
            if (component == null) return;

            var t = component.GetType();
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

            var fields = t.GetFields(flags);

            foreach (var f in fields)
            {
                if (f.FieldType != typeof(InputActionReference)) continue;
                if (!ShouldExposeForRebind(f.Name)) continue;

                var iar = f.GetValue(component) as InputActionReference;
                var action = iar != null ? iar.action : null;
                if (action == null) continue;

                string display = GetFriendlyActionName(f.Name, action.name);

                // Special case: Lean should expose Forward + Backward separately if composite
                if (display == "Lean")
                {
                    int fwd = FindCompositePartBindingIndex(action, "positive", "up", "forward");
                    int back = FindCompositePartBindingIndex(action, "negative", "down", "back");

                    if (fwd >= 0) AddRow(rows, action, fwd, "Lean Forward");
                    if (back >= 0) AddRow(rows, action, back, "Lean Backward");
                    if (fwd >= 0 || back >= 0) continue;
                }

                // Default: primary binding
                int idx = FindPrimaryBindingIndex(action);
                AddRow(rows, action, idx, display);
            }
        }

        private static bool ShouldExposeForRebind(string fieldName)
        {
            if (fieldName == "shopOrbitDeltaAction") return false;
            if (fieldName == "lookAction") return false;
            if (fieldName == "rotateDeltaAction") return false;
            if (fieldName == "rotateStickAction") return false;
            return true;
        }

        private static void AddRow(Dictionary<string, RebindRow> rows, InputAction action, int bindingIndex, string displayName)
        {
            if (action == null) return;
            if (bindingIndex < 0) bindingIndex = 0;

            string key = $"{action.id:N}:{bindingIndex}";
            if (rows.ContainsKey(key)) return;

            rows[key] = new RebindRow
            {
                displayName = displayName,
                action = action,
                bindingIndex = bindingIndex
            };
        }

        private static int FindPrimaryBindingIndex(InputAction action)
        {
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
            if (action == null) return -1;

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
            if (action == null) return "-";
            if (bindingIndex < 0 || bindingIndex >= action.bindings.Count) bindingIndex = 0;

            return action.GetBindingDisplayString(bindingIndex,
                InputBinding.DisplayStringOptions.DontUseShortDisplayNames);
        }

        private static string GetFriendlyActionName(string fieldName, string fallbackActionName)
        {
            switch (fieldName)
            {
                case "jumpAction": return "Jump";
                case "polesAction": return "Poles";
                case "leftSkiAction": return "Left Ski";
                case "rightSkiAction": return "Right Ski";

                case "equipSkisAction":
                case "equipSkiAction":
                case "equipSkisPressAction":
                    return "Equip Skis";

                case "interactAction":
                case "exitHoldAction":
                case "resortAction":
                case "liftInput":
                    return "Interact";

                case "shopOrbitPressAction":
                    return "Shop Orbit";

                case "leanAction":
                    return "Lean";
            }

            if (!string.IsNullOrEmpty(fallbackActionName))
            {
                string normalized = fallbackActionName.Replace(" ", "").Replace("_", "").ToLowerInvariant();
                if (normalized.Contains("equipskis") || normalized.Contains("equipski"))
                    return "Equip Skis";
            }

            return string.IsNullOrEmpty(fallbackActionName) ? fieldName : fallbackActionName;
        }
        private void StartRebind(InputAction action, int bindingIndex, Label bindingLabel, VisualElement rowElement)
        {
            CancelActiveRebind();

            _activeRebindAction = action;
            _activeRebindBindingIndex = bindingIndex;
            _activeRebindRow = rowElement;
            _activeRebindBindingLabel = bindingLabel;

            if (_lblControlsStatus != null)
            {
                _lblControlsStatus.text = $"Listening… press a key/button for {action.name} (Esc to cancel)";
                _lblControlsStatus.AddToClassList("is-listening");
            }

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

                    if (_lblControlsNote != null) _lblControlsNote.text = "Rebind cancelled.";
                    if (_activeRebindBindingLabel != null)
                        _activeRebindBindingLabel.text = GetReadableBinding(action, bindingIndex);

                    ClearActiveRebindRefs();
                })
                .OnComplete(op =>
                {
                    action.Enable();
                    op.Dispose();
                    _activeRebind = null;

                    var asset = action.actionMap?.asset;
                    if (asset != null)
                        InputBindingOverridesStorage.SaveOverrides(asset);

                    EndListeningUI();

                    if (_lblControlsNote != null) _lblControlsNote.text = "Binding saved.";
                    if (_activeRebindBindingLabel != null)
                        _activeRebindBindingLabel.text = GetReadableBinding(action, bindingIndex);

                    ClearActiveRebindRefs();
                });

            _activeRebind.Start();
        }

        private void EndListeningUI()
        {
            if (_lblControlsStatus != null)
            {
                _lblControlsStatus.text = "Select an action to rebind.";
                _lblControlsStatus.RemoveFromClassList("is-listening");
            }

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
            if (_activeRebind == null) return;
            try { _activeRebind.Cancel(); } catch { }
        }

        private void OnClickResetBindings()
        {
            CancelActiveRebind();
            EndListeningUI();
            ClearActiveRebindRefs();

            var rows = CollectRebindRows();
            if (rows.Count > 0)
            {
                var asset = rows[0].action?.actionMap?.asset;
                if (asset != null)
                {
                    asset.RemoveAllBindingOverrides();
                    InputBindingOverridesStorage.ClearOverrides();
                }
                else
                {
                    foreach (var r in rows)
                        r.action?.RemoveAllBindingOverrides();
                }
            }

            if (_lblControlsNote != null)
                _lblControlsNote.text = "Bindings reset.";

            RebuildControlsList();
        }
        // ---------------- Quit ----------------

        private void QuitToMenu()
        {
            CancelActiveRebind();
            Time.timeScale = 1f;
            SceneManager.LoadScene(mainMenuSceneName);
        }

        private void QuitToDesktop()
        {
            CancelActiveRebind();
            Time.timeScale = 1f;
            Application.Quit();
        }

        // =====================================================================
        // Settings binding (from PhoneHUD)
        // =====================================================================
        private void BindSettingsPageIfNeeded()
        {
            if (_settingsBound) return;
            _settingsBound = true;

            GameSettingsService.EnsureLoaded();

            // Dropdown options
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

            // Push model -> UI
            RefreshSettingsUIFromProfile(GameSettingsService.Current);

            // UI -> model
            if (_sMaster != null) _sMaster.RegisterValueChangedCallback(evt => { MutateSettings(p => p.masterVolume = evt.newValue); });
            if (_sMusic != null) _sMusic.RegisterValueChangedCallback(evt => { MutateSettings(p => p.musicVolume = evt.newValue); });
            if (_sSfx != null) _sSfx.RegisterValueChangedCallback(evt => { MutateSettings(p => p.sfxVolume = evt.newValue); });

            if (_tInvertY != null) _tInvertY.RegisterValueChangedCallback(evt => { MutateSettings(p => p.invertLookY = evt.newValue); });
            if (_sLookSens != null) _sLookSens.RegisterValueChangedCallback(evt => { MutateSettings(p => p.lookSensitivity = evt.newValue); });

            if (_sUiScale != null) _sUiScale.RegisterValueChangedCallback(evt => { MutateSettings(p => p.uiScale = evt.newValue); });
            if (_tReduceMotion != null) _tReduceMotion.RegisterValueChangedCallback(evt => { MutateSettings(p => p.reduceMotion = evt.newValue); });

            if (_tShowTutorialOverlay != null) _tShowTutorialOverlay.RegisterValueChangedCallback(evt => { MutateSettings(p => p.showTutorialOverlay = evt.newValue); });
            if (_tShowMovementInputs != null) _tShowMovementInputs.RegisterValueChangedCallback(evt => { MutateSettings(p => p.showMovementInputOverlay = evt.newValue); });
            if (_tShowWorldPrompts != null) _tShowWorldPrompts.RegisterValueChangedCallback(evt => { MutateSettings(p => p.showWorldInteractionPrompts = evt.newValue); });

            if (_ddColourMode != null)
            {
                _ddColourMode.RegisterValueChangedCallback(evt =>
                {
                    int idx = Mathf.Clamp(_ddColourMode.choices.IndexOf(evt.newValue), 0, 3);
                    MutateSettings(p => p.colorAccessibilityMode = idx);
                });
            }

            if (_ddQuality != null)
            {
                _ddQuality.RegisterValueChangedCallback(evt =>
                {
                    int idx = (_ddQuality.choices != null) ? _ddQuality.choices.IndexOf(evt.newValue) : -1;
                    MutateSettings(p => p.qualityLevel = idx);
                });
            }

            if (_tVSync != null) _tVSync.RegisterValueChangedCallback(evt => { MutateSettings(p => p.vSync = evt.newValue); });

            if (_ddFpsCap != null)
            {
                _ddFpsCap.RegisterValueChangedCallback(evt =>
                {
                    int fps = 0;
                    if (int.TryParse(evt.newValue, out int parsed)) fps = parsed;
                    MutateSettings(p => p.targetFps = fps);
                });
            }

            if (_ddWindowMode != null)
            {
                _ddWindowMode.RegisterValueChangedCallback(evt =>
                {
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
                    int idx = Mathf.Max(0, _ddDisplay.choices.IndexOf(evt.newValue));
                    MutateSettings(p => p.displayIndex = idx);
                });
            }

            if (_ddResolution != null)
            {
                _ddResolution.RegisterValueChangedCallback(evt =>
                {
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

            if (_sFov != null) _sFov.RegisterValueChangedCallback(evt => { MutateSettings(p => p.cameraFov = evt.newValue); });

            if (_ddUnits != null)
            {
                _ddUnits.RegisterValueChangedCallback(evt =>
                {
                    int idx = Mathf.Clamp(_ddUnits.choices.IndexOf(evt.newValue), 0, 1);
                    MutateSettings(p => p.speedUnitMode = idx);
                });
            }

            GameSettingsService.OnChanged -= RefreshSettingsUIFromProfile;
            GameSettingsService.OnChanged += RefreshSettingsUIFromProfile;
        }

        private void RefreshSettingsUIFromProfile(GameSettingsProfile p)
        {
            if (p == null) return;

            if (_sMaster != null) _sMaster.SetValueWithoutNotify(p.masterVolume);
            if (_sMusic != null) _sMusic.SetValueWithoutNotify(p.musicVolume);
            if (_sSfx != null) _sSfx.SetValueWithoutNotify(p.sfxVolume);

            if (_tInvertY != null) _tInvertY.SetValueWithoutNotify(p.invertLookY);
            if (_sLookSens != null) _sLookSens.SetValueWithoutNotify(p.lookSensitivity);

            if (_sUiScale != null) _sUiScale.SetValueWithoutNotify(p.uiScale);
            if (_tReduceMotion != null) _tReduceMotion.SetValueWithoutNotify(p.reduceMotion);

            if (_tShowTutorialOverlay != null) _tShowTutorialOverlay.SetValueWithoutNotify(p.showTutorialOverlay);
            if (_tShowMovementInputs != null) _tShowMovementInputs.SetValueWithoutNotify(p.showMovementInputOverlay);
            if (_tShowWorldPrompts != null) _tShowWorldPrompts.SetValueWithoutNotify(p.showWorldInteractionPrompts);

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
                if (_ddFpsCap.choices.Contains(v))
                    _ddFpsCap.SetValueWithoutNotify(v);
                else
                    _ddFpsCap.SetValueWithoutNotify("Unlimited");
            }

            if (_ddWindowMode != null && _ddWindowMode.choices?.Count > 0)
            {
                string v =
                    ((FullScreenMode)p.fullScreenMode) switch
                    {
                        FullScreenMode.Windowed => "Windowed",
                        FullScreenMode.ExclusiveFullScreen => "Fullscreen",
                        _ => "Borderless"
                    };

                if (_ddWindowMode.choices.Contains(v))
                    _ddWindowMode.SetValueWithoutNotify(v);
            }

            if (_ddDisplay != null && _ddDisplay.choices?.Count > 0)
            {
                int idx = Mathf.Clamp(p.displayIndex, 0, _ddDisplay.choices.Count - 1);
                _ddDisplay.SetValueWithoutNotify(_ddDisplay.choices[idx]);
            }

            if (_ddResolution != null && _ddResolution.choices?.Count > 0)
            {
                string best = null;
                var res = Screen.resolutions;

                if (p.resolutionWidth > 0 && p.resolutionHeight > 0 && res != null)
                {
                    for (int i = 0; i < res.Length && i < _ddResolution.choices.Count; i++)
                    {
#if UNITY_2022_2_OR_NEWER
                        int hz = (int)Mathf.Round((float)res[i].refreshRateRatio.value);
#else
                int hz = res[i].refreshRate;
#endif
                        if (res[i].width == p.resolutionWidth && res[i].height == p.resolutionHeight &&
                            (p.resolutionRefreshHz <= 0 || hz == p.resolutionRefreshHz))
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

            if (_sFov != null) _sFov.SetValueWithoutNotify(p.cameraFov);
            if (_lblFov != null) _lblFov.text = $"{Mathf.RoundToInt(p.cameraFov)}";

            if (_ddUnits != null && _ddUnits.choices != null && _ddUnits.choices.Count > 0)
            {
                int idx = Mathf.Clamp(p.speedUnitMode, 0, _ddUnits.choices.Count - 1);
                _ddUnits.SetValueWithoutNotify(_ddUnits.choices[idx]);
            }
        }

        private void MarkSettingsDirty()
        {
            _settingsDirty = true;
            _settingsNextSaveTime = Time.unscaledTime + 0.25f;
        }

        private void MutateSettings(Action<GameSettingsProfile> mutator)
        {
            var p = GameSettingsService.Current;
            var next = JsonUtility.FromJson<GameSettingsProfile>(JsonUtility.ToJson(p));
            mutator?.Invoke(next);
            next.Sanitize();

            GameSettingsService.Set(next, applyMinimal: true);
            MarkSettingsDirty();

            if (_lblSettingsNote != null)
                _lblSettingsNote.text = "Saved.";
        }

        private void OnClickSettingsReset()
        {
            GameSettingsService.ResetToDefaults();
            if (_lblSettingsNote != null)
                _lblSettingsNote.text = "Reset to defaults.";
        }
    }
}