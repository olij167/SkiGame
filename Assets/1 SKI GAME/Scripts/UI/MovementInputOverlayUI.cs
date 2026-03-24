using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using SkiGame.Progression;

namespace SkiGame.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class MovementInputOverlayUI : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private SkiLessonDirector skiLessonDirector;
        [SerializeField] private SkiController skiController;
        [SerializeField] private bool followTutorialStep = true;

        [Header("Stability")]
        [SerializeField] private float refreshRate = 0.08f;
        [SerializeField] private float highlightHoldTime = 0.18f;
        [SerializeField] private float leanEnterThreshold = 0.30f;
        [SerializeField] private float leanExitThreshold = 0.18f;
        [SerializeField] private float turnEnterThreshold = 0.35f;
        [SerializeField] private float turnExitThreshold = 0.20f;
        [SerializeField] private float skateEnterThreshold = 0.45f;
        [SerializeField] private float skateExitThreshold = 0.25f;

        private VisualElement _root;
        private VisualElement _panel;
        private Label _title;
        private VisualElement _list;

        private readonly List<InputRow> _buffer = new();
        private readonly List<RowView> _rowViews = new();
        private readonly Dictionary<string, LatchedBool> _latched = new();

        private float _nextRefreshTime = 0f;
        private string _lastSignature = string.Empty;
        private string _lastTitle = string.Empty;

        private struct InputRow
        {
            public string Key;
            public string Label;
            public string BindingText;
            public bool IsHighlighted;
        }

        private sealed class RowView
        {
            public VisualElement Root;
            public Label Name;
            public Label Binding;
            public string Key;
        }

        private struct LatchedBool
        {
            public bool Value;
            public float HoldUntil;
        }

        private void OnEnable()
        {
            if (document == null)
                document = GetComponent<UIDocument>();

            if (skiLessonDirector == null)
                skiLessonDirector = FindObjectOfType<SkiLessonDirector>();

            if (skiController == null)
                skiController = FindObjectOfType<SkiController>();

            if (document == null || document.rootVisualElement == null)
            {
                enabled = false;
                return;
            }

            _root = document.rootVisualElement;
            _panel = _root.Q<VisualElement>("MovementInputPanel");
            _title = _root.Q<Label>("Lbl_MovementInputTitle");
            _list = _root.Q<VisualElement>("MovementInputList");

            ForceRefresh();
        }

        private void Update()
        {
            bool enabledBySettings = GameSettingsService.Current == null || GameSettingsService.Current.showMovementInputOverlay;
            if (_panel != null)
                _panel.style.display = enabledBySettings ? DisplayStyle.Flex : DisplayStyle.None;

            if (!enabledBySettings || _list == null)
                return;

            if (Time.unscaledTime < _nextRefreshTime)
                return;

            _nextRefreshTime = Time.unscaledTime + refreshRate;
            Refresh();
        }

        private void ForceRefresh()
        {
            _lastSignature = string.Empty;
            _lastTitle = string.Empty;
            _nextRefreshTime = 0f;
            Refresh();
        }

        private void Refresh()
        {
            BuildRows();

            string titleText = GetCurrentTitle();
            string signature = BuildSignature(_buffer);

            if (_title != null && _lastTitle != titleText)
            {
                _title.text = titleText;
                _lastTitle = titleText;
            }

            if (_lastSignature != signature)
            {
                RebuildList();
                _lastSignature = signature;
            }
            else
            {
                UpdateExistingRowStyles();
            }
        }

        private string GetCurrentTitle()
        {
            if (!followTutorialStep || skiLessonDirector == null || !skiLessonDirector.IsLessonActive)
                return "Movement Inputs";

            return "Lesson Inputs";
        }

        private void BuildRows()
        {
            _buffer.Clear();

            if (!followTutorialStep || skiLessonDirector == null || !skiLessonDirector.IsLessonActive)
            {
                AddGeneralRows();
                return;
            }

            switch (skiLessonDirector.CurrentStepIndex)
            {
                case 0:
                    AddRow("move", "Move / Push Off", CombineBindings("LeftSki", "RightSki"), GetLatched("move", IsSkatingActive()));
                    AddRow("equip", "Put On / Take Off Skis", GetBindingDisplay("EquipSkis"), GetLatched("equip", IsActionPressed("EquipSkis")));
                    break;

                case 1:
                    AddRow("lean_fwd", "Lean Forward", GetBindingDisplay("Lean", "positive", "up", "forward"), GetLatched("lean_fwd", IsLeanForwardActive()));
                    AddRow("lean_back", "Lean Backward", GetBindingDisplay("Lean", "negative", "down", "back", "backward"), GetLatched("lean_back", IsLeanBackwardActive()));
                    break;

                case 2:
                    AddRow("turn", "Turn / Carve", CombineBindings("LeftSki", "RightSki"), GetLatched("turn", IsTurningActive()));
                    AddRow("lean_any", "Steady Your Speed", GetBindingDisplay("Lean"), GetLatched("lean_any", IsLeanActive()));
                    break;

                case 3:
                    AddRow("skate", "Skate", $"Lean forward + {GetBindingDisplay("LeftSki")} then {GetBindingDisplay("RightSki")}", GetLatched("skate", IsSkatingActive()));
                    AddRow("lean_fwd", "Drive Forward", GetBindingDisplay("Lean", "positive", "up", "forward"), GetLatched("lean_fwd", IsLeanForwardActive()));
                    break;

                case 4:
                    bool quickStop = GetLatched("quick_stop", IsQuickStopActive());
                    AddRow("quick_stop", "Quick Stop", $"{GetBindingDisplay("Lean", "negative", "down", "back", "backward")} + {CombineBindings("LeftSki", "RightSki")}", quickStop);
                    AddRow("quick_stop_hint", "Brake Hard", "Lean back and make a sharp turn", quickStop);
                    break;

                case 5:
                    AddRow("jump", "Take Off / Jump (Hold)", $"{GetBindingDisplay("Jump")}  (hold for more pop)", GetLatched("jump", IsJumpActive()));
                    AddRow("air_control", "Air Control", $"{GetBindingDisplay("Lean")} + {CombineBindings("LeftSki", "RightSki")}", GetLatched("air_control", IsAirControlActive()));
                    AddRow("air_boost", "Air Boost", $"Hold {GetShiftLikeBinding()} in air for faster movement", GetLatched("air_boost", IsAirBoostActive()));
                    break;

                case 6:
                    AddRow("pole_push", "Pole Push", GetBindingDisplay("Poles"), GetLatched("pole_push", IsPolePushActive()));
                    AddRow("pole_drag", "Pole Drag / Speed Control", GetBindingDisplay("Poles"), GetLatched("pole_drag", IsPoleDragActive()));
                    break;

                default:
                    AddGeneralRows();
                    break;
            }
        }

        private void AddGeneralRows()
        {
            AddRow("lean_fwd", "Lean Forward", GetBindingDisplay("Lean", "positive", "up", "forward"), GetLatched("lean_fwd", IsLeanForwardActive()));
            AddRow("lean_back", "Lean Backward", GetBindingDisplay("Lean", "negative", "down", "back", "backward"), GetLatched("lean_back", IsLeanBackwardActive()));
            AddRow("turn", "Turn / Carve", CombineBindings("LeftSki", "RightSki"), GetLatched("turn", IsTurningActive()));
            AddRow("skate", "Skate", $"Lean forward + {GetBindingDisplay("LeftSki")} then {GetBindingDisplay("RightSki")}", GetLatched("skate", IsSkatingActive()));
            AddRow("quick_stop", "Quick Stop", $"{GetBindingDisplay("Lean", "negative", "down", "back", "backward")} + {CombineBindings("LeftSki", "RightSki")}", GetLatched("quick_stop", IsQuickStopActive()));
            AddRow("jump", "Take Off / Jump (Hold)", $"{GetBindingDisplay("Jump")}  (hold for more pop)", GetLatched("jump", IsJumpActive()));
            AddRow("air_control", "Air Control", $"{GetBindingDisplay("Lean")} + {CombineBindings("LeftSki", "RightSki")}", GetLatched("air_control", IsAirControlActive()));
            AddRow("air_boost", "Air Boost", $"Hold {GetShiftLikeBinding()} in air for faster movement", GetLatched("air_boost", IsAirBoostActive()));
            AddRow("poles", "Poles", GetBindingDisplay("Poles"), GetLatched("poles", IsPolesActive()));
            AddRow("equip", "Put On / Take Off Skis", GetBindingDisplay("EquipSkis"), GetLatched("equip", IsActionPressed("EquipSkis")));
            AddRow("interact", "Interact", GetBindingDisplay("Interact"), GetLatched("interact", IsActionPressed("Interact")));
        }

        private void AddRow(string key, string label, string bindingText, bool isHighlighted)
        {
            _buffer.Add(new InputRow
            {
                Key = key,
                Label = label,
                BindingText = string.IsNullOrWhiteSpace(bindingText) ? "-" : bindingText,
                IsHighlighted = isHighlighted
            });
        }

        private void RebuildList()
        {
            _list.Clear();
            _rowViews.Clear();

            for (int i = 0; i < _buffer.Count; i++)
            {
                var rowData = _buffer[i];

                var row = new VisualElement();
                row.AddToClassList("input-row");

                var lblName = new Label(rowData.Label);
                lblName.AddToClassList("input-row-name");

                var lblBinding = new Label(rowData.BindingText);
                lblBinding.AddToClassList("input-row-binding");

                ApplyHighlightClasses(row, lblName, lblBinding, rowData.IsHighlighted);

                row.Add(lblName);
                row.Add(lblBinding);
                _list.Add(row);

                _rowViews.Add(new RowView
                {
                    Root = row,
                    Name = lblName,
                    Binding = lblBinding,
                    Key = rowData.Key
                });
            }
        }

        private void UpdateExistingRowStyles()
        {
            int count = Mathf.Min(_rowViews.Count, _buffer.Count);
            for (int i = 0; i < count; i++)
            {
                var rowView = _rowViews[i];
                var rowData = _buffer[i];

                if (rowView.Name.text != rowData.Label)
                    rowView.Name.text = rowData.Label;

                if (rowView.Binding.text != rowData.BindingText)
                    rowView.Binding.text = rowData.BindingText;

                ApplyHighlightClasses(rowView.Root, rowView.Name, rowView.Binding, rowData.IsHighlighted);
            }
        }

        private static void ApplyHighlightClasses(VisualElement row, Label name, Label binding, bool highlighted)
        {
            SetClass(row, "input-row-active", highlighted);
            SetClass(name, "input-row-name-active", highlighted);
            SetClass(binding, "input-row-binding-active", highlighted);
        }

        private static void SetClass(VisualElement element, string className, bool enabled)
        {
            if (element == null)
                return;

            if (enabled)
                element.AddToClassList(className);
            else
                element.RemoveFromClassList(className);
        }

        private string BuildSignature(List<InputRow> rows)
        {
            var sb = new StringBuilder(rows.Count * 32);
            for (int i = 0; i < rows.Count; i++)
            {
                sb.Append(rows[i].Key).Append('|')
                  .Append(rows[i].Label).Append('|')
                  .Append(rows[i].BindingText).Append('|')
                  .Append(rows[i].IsHighlighted ? '1' : '0')
                  .Append(';');
            }
            return sb.ToString();
        }

        private bool GetLatched(string key, bool rawValue)
        {
            if (!_latched.TryGetValue(key, out var state))
                state = default;

            float now = Time.unscaledTime;

            if (rawValue)
            {
                state.Value = true;
                state.HoldUntil = now + highlightHoldTime;
            }
            else if (state.Value && now >= state.HoldUntil)
            {
                state.Value = false;
            }

            _latched[key] = state;
            return state.Value;
        }

        private string CombineBindings(params string[] actionNames)
        {
            if (actionNames == null || actionNames.Length == 0)
                return "-";

            var sb = new StringBuilder();

            for (int i = 0; i < actionNames.Length; i++)
            {
                if (i > 0)
                    sb.Append(" / ");

                sb.Append(GetBindingDisplay(actionNames[i]));
            }

            return sb.ToString();
        }

        private string GetShiftLikeBinding()
        {
            string sprint = GetBindingDisplay("Sprint");
            if (!string.IsNullOrWhiteSpace(sprint) && sprint != "-")
                return sprint;

            return "Shift";
        }

        private string GetBindingDisplay(string actionName, params string[] compositePartNames)
        {
            var action = FindActionByFriendlyName(actionName);
            if (action == null)
                return "-";

            int bindingIndex = (compositePartNames != null && compositePartNames.Length > 0)
                ? FindCompositePartBindingIndex(action, compositePartNames)
                : FindPrimaryBindingIndex(action);

            return action.GetBindingDisplayString(bindingIndex, InputBinding.DisplayStringOptions.DontUseShortDisplayNames);
        }

        private InputAction FindActionByFriendlyName(string actionName)
        {
            if (inputActions == null || string.IsNullOrWhiteSpace(actionName))
                return null;

            string normalizedTarget = Normalize(actionName);

            foreach (var map in inputActions.actionMaps)
            {
                for (int i = 0; i < map.actions.Count; i++)
                {
                    var action = map.actions[i];
                    if (Normalize(action.name) == normalizedTarget)
                        return action;
                }
            }

            return null;
        }

        private bool IsActionPressed(string actionName)
        {
            var action = FindActionByFriendlyName(actionName);
            if (action == null)
                return false;

            try
            {
                return action.IsPressed();
            }
            catch
            {
                return false;
            }
        }

        private static string Normalize(string value)
        {
            return value.Replace(" ", "").Replace("_", "").ToLowerInvariant();
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
            if (action == null)
                return 0;

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

            return FindPrimaryBindingIndex(action);
        }

        private bool IsLeanActive()
        {
            if (skiController == null) return false;
            return Mathf.Abs(skiController.ForwardLeanInput) >= leanEnterThreshold;
        }

        private bool IsLeanForwardActive()
        {
            if (skiController == null) return false;
            return ReadLatchedThreshold("leanFwdRaw", skiController.ForwardLeanInput, leanEnterThreshold, leanExitThreshold, positive: true);
        }

        private bool IsLeanBackwardActive()
        {
            if (skiController == null) return false;
            return ReadLatchedThreshold("leanBackRaw", skiController.ForwardLeanInput, leanEnterThreshold, leanExitThreshold, positive: false);
        }

        private bool IsTurningActive()
        {
            if (skiController == null) return false;
            float diff = Mathf.Abs(skiController.RightLegInput - skiController.LeftLegInput);
            return ReadLatchedMagnitude("turnRaw", diff, turnEnterThreshold, turnExitThreshold);
        }

        private bool IsSkatingActive()
        {
            if (skiController == null) return false;
            float leg = Mathf.Max(Mathf.Abs(skiController.LeftLegInput), Mathf.Abs(skiController.RightLegInput));
            return ReadLatchedMagnitude("skateRaw", leg, skateEnterThreshold, skateExitThreshold);
        }

        private bool IsQuickStopActive()
        {
            return IsLeanBackwardActive() && IsTurningActive();
        }

        private bool IsJumpActive()
        {
            return IsActionPressed("Jump");
        }

        private bool IsAirControlActive()
        {
            if (skiController == null || skiController.IsRiderGrounded)
                return false;

            return IsLeanForwardActive() || IsLeanBackwardActive() || IsTurningActive();
        }

        private bool IsAirBoostActive()
        {
            if (skiController == null || skiController.IsRiderGrounded)
                return false;

            return IsActionPressed("Sprint") ||
                   Keyboard.current?.leftShiftKey.isPressed == true ||
                   Keyboard.current?.rightShiftKey.isPressed == true;
        }

        private bool IsPolesActive()
        {
            return IsActionPressed("Poles");
        }

        private bool IsPolePushActive()
        {
            if (skiController == null) return false;

            return skiController.CurrentPolePhase == SkiController.PoleStrokePhase.Entry ||
                   skiController.CurrentPolePhase == SkiController.PoleStrokePhase.FollowThrough;
        }

        private bool IsPoleDragActive()
        {
            if (skiController == null) return false;
            return skiController.CurrentPolePhase == SkiController.PoleStrokePhase.Drag;
        }

        private readonly Dictionary<string, bool> _thresholdStates = new();

        private bool ReadLatchedMagnitude(string key, float value, float enterThreshold, float exitThreshold)
        {
            bool current = _thresholdStates.TryGetValue(key, out bool active) && active;

            if (!current && value >= enterThreshold)
                current = true;
            else if (current && value <= exitThreshold)
                current = false;

            _thresholdStates[key] = current;
            return current;
        }

        private bool ReadLatchedThreshold(string key, float value, float enterThreshold, float exitThreshold, bool positive)
        {
            bool current = _thresholdStates.TryGetValue(key, out bool active) && active;

            if (positive)
            {
                if (!current && value >= enterThreshold)
                    current = true;
                else if (current && value <= exitThreshold)
                    current = false;
            }
            else
            {
                if (!current && value <= -enterThreshold)
                    current = true;
                else if (current && value >= -exitThreshold)
                    current = false;
            }

            _thresholdStates[key] = current;
            return current;
        }
    }
}