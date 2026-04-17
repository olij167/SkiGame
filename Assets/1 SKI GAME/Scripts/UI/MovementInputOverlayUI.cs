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
        [SerializeField] private MountainHudOverlayController overlayController;
        [SerializeField] private QuestDirector questDirector;
        [SerializeField] private SkiController skiController;
        [SerializeField] private WalkingController walkingController;
        [SerializeField] private SkiResortHoldInteractor skiResortInteractor;
        [SerializeField] private InputPromptIconLibrary iconLibrary;

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
        private readonly List<QuestRuntimeState> _questStateBuffer = new();

        private float _nextRefreshTime = 0f;
        private string _lastSignature = string.Empty;
        private string _lastTitle = string.Empty;
        private string _resolvedTitle = "Movement Inputs";

        private struct InputRow
        {
            public string Key;
            public string Label;
            public InputPromptToken[] PromptTokens;
            public bool IsHighlighted;
            public bool IsPrimary;
        }

        private sealed class RowView
        {
            public VisualElement Root;
            public Label Name;
            public VisualElement Prompt;
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

            if (inputActions == null)
                inputActions = new InputSystem_Actions().asset;

            if (skiController == null)
                skiController = FindObjectOfType<SkiController>();

            if (walkingController == null)
                walkingController = FindObjectOfType<WalkingController>();

            if (skiResortInteractor == null)
                skiResortInteractor = FindObjectOfType<SkiResortHoldInteractor>();

            if (questDirector == null)
                questDirector = QuestDirector.Instance != null ? QuestDirector.Instance : FindObjectOfType<QuestDirector>();

            if (iconLibrary == null)
                iconLibrary = InputPromptResolver.LoadDefaultLibrary();

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
            return string.IsNullOrWhiteSpace(_resolvedTitle) ? "Movement Inputs" : _resolvedTitle;
        }

        private void BuildRows()
        {
            _buffer.Clear();

            bool grounded = skiController == null || skiController.IsRiderGrounded;
            bool showPreAirRows = grounded && ShouldPreviewAerialRows();

            _resolvedTitle = grounded
                ? (showPreAirRows ? "Aerial" : "Grounded")
                : "Aerial";

            if (showPreAirRows)
            {
                AddAerialRows();
                return;
            }

            if (grounded)
            {
                AddGroundedRows();
                return;
            }

            AddAerialRows();
        }

        private bool ShouldPreviewAerialRows()
        {
            bool skisEquipped = walkingController != null
                ? walkingController.SkisOn
                : skiController != null && skiController.enabled;

            if (!skisEquipped)
                return false;

            if (skiResortInteractor != null && skiResortInteractor.IsPlayerInsideResort)
                return false;

            return GetLatched("pre_air_preview", IsActionPressed("Jump"));
        }

        private void AddGroundedRows()
        {
            bool skisEquipped = walkingController != null ? walkingController.SkisOn : skiController != null && skiController.enabled;
            bool inResort = skiResortInteractor != null && skiResortInteractor.IsPlayerInsideResort;

            if (!skisEquipped)
            {
                AddRow("equip", "Skis", HoldBinding("EquipSkis"), GetLatched("equip", IsActionPressed("EquipSkis")), isPrimary: true);
                if (inResort)
                    AddRow("interact", "Use", Binding("Interact"), GetLatched("interact", IsActionPressed("Interact")));
                return;
            }

            AddRow("turn", "Turn", CombineBindings("LeftSki", "RightSki"), GetLatched("turn", IsTurningActive()), isPrimary: true);
            AddRow("lean", "Lean", LeanAxisText(), GetLatched("lean", IsLeanActive()));
            AddRow("pole_push", "Pole Push", Binding("Poles"), GetLatched("pole_push", IsPolePushActive()));
            AddRow("pole_drag", "Pole Drag", HoldBinding("Poles"), GetLatched("pole_drag", IsPoleDragActive()));
            AddRow("tuck", "Tuck", HoldBinding("Sprint"), GetLatched("tuck", IsActionPressed("Sprint")));
            AddRow("jump", "Jump", Binding("Jump"), GetLatched("jump", IsJumpActive()));

            if (inResort)
                AddRow("interact", "Use", Binding("Interact"), GetLatched("interact", IsActionPressed("Interact")));
        }

        private void AddAerialRows()
        {
            AddRow("air_turn", "Turn", CombineBindings("LeftSki", "RightSki"), GetLatched("air_turn", IsTurningActive()), isPrimary: true);
            AddRow("air_pitch", "Pitch", LeanAxisText(), GetLatched("air_pitch", IsLeanActive()));
            AddRow("air_pose", "Pose", Binding("Poles"), GetLatched("air_pose", IsPolesActive()));
            AddRow("air_boost", "Boost", HoldBinding("Sprint"), GetLatched("air_boost", IsAirBoostActive()));
            AddRow("air_jump", "Jump", Binding("Jump"), GetLatched("air_jump", IsJumpActive()));
        }

        private bool TryBuildQuestRows()
        {
            if (!TryGetFocusedQuestObjective(out var questDefinition, out var objectiveDefinition, out var objectiveState))
                return false;

            _resolvedTitle = string.IsNullOrWhiteSpace(questDefinition.title) ? "Tutorial Controls" : questDefinition.title.Trim();
            AddRowsForObjective(objectiveDefinition, objectiveState);
            return _buffer.Count > 0;
        }

        private bool TryGetFocusedQuestObjective(
            out QuestDefinitionSO questDefinition,
            out QuestObjectiveDefinition objectiveDefinition,
            out QuestObjectiveRuntimeState objectiveState)
        {
            questDefinition = null;
            objectiveDefinition = null;
            objectiveState = null;

            if (questDirector == null)
                questDirector = QuestDirector.Instance != null ? QuestDirector.Instance : FindObjectOfType<QuestDirector>();

            if (questDirector == null)
                return false;

            questDirector.GetTrackedQuestStates(_questStateBuffer);
            if (TryFindFocusedQuestObjective(_questStateBuffer, tutorialOnly: true, out questDefinition, out objectiveDefinition, out objectiveState) ||
                TryFindFocusedQuestObjective(_questStateBuffer, tutorialOnly: false, out questDefinition, out objectiveDefinition, out objectiveState))
                return true;

            questDirector.GetActiveQuestStates(_questStateBuffer);
            return
                TryFindFocusedQuestObjective(_questStateBuffer, tutorialOnly: true, out questDefinition, out objectiveDefinition, out objectiveState) ||
                TryFindFocusedQuestObjective(_questStateBuffer, tutorialOnly: false, out questDefinition, out objectiveDefinition, out objectiveState);
        }

        private bool TryFindFocusedQuestObjective(
            List<QuestRuntimeState> questStates,
            bool tutorialOnly,
            out QuestDefinitionSO questDefinition,
            out QuestObjectiveDefinition objectiveDefinition,
            out QuestObjectiveRuntimeState objectiveState)
        {
            questDefinition = null;
            objectiveDefinition = null;
            objectiveState = null;

            if (questStates == null || questDirector == null)
                return false;

            for (int i = 0; i < questStates.Count; i++)
            {
                var state = questStates[i];
                if (state == null || state.completed)
                    continue;

                var definition = questDirector.GetQuestDefinition(state.questId);
                if (definition == null)
                    continue;

                if (tutorialOnly && !definition.tutorialQuest)
                    continue;

                var stage = definition.GetStage(state.currentStageIndex);
                if (stage?.objectives == null || state.objectiveStates == null)
                    continue;

                for (int j = 0; j < state.objectiveStates.Count; j++)
                {
                    var candidateState = state.objectiveStates[j];
                    if (candidateState == null || candidateState.completed)
                        continue;

                    var candidateDefinition = FindObjectiveDefinition(stage, candidateState.objectiveId);
                    if (candidateDefinition == null)
                        continue;

                    questDefinition = definition;
                    objectiveDefinition = candidateDefinition;
                    objectiveState = candidateState;
                    return true;
                }
            }

            return false;
        }

        private void AddRowsForObjective(QuestObjectiveDefinition objectiveDefinition, QuestObjectiveRuntimeState objectiveState)
        {
            if (objectiveDefinition == null)
                return;

            if (objectiveDefinition.template == QuestObjectiveTemplate.Compound && objectiveDefinition.compoundObjectives != null)
            {
                for (int i = 0; i < objectiveDefinition.compoundObjectives.Count; i++)
                {
                    var child = objectiveDefinition.compoundObjectives[i];
                    if (child == null)
                        continue;

                    if (objectiveState != null && IsConditionPathCompleted(objectiveState, $"0/{i}"))
                        continue;

                    AddRowsForObjective(child, null);
                    return;
                }
            }

            AddRow(
                "objective_primary",
                string.IsNullOrWhiteSpace(objectiveDefinition.title) ? objectiveDefinition.BuildAuthoringSummary() : objectiveDefinition.title.Trim(),
                BuildDetailedPromptTokens(objectiveDefinition),
                IsObjectiveHighlighted(objectiveDefinition),
                isPrimary: true);

            AddSupportRows(objectiveDefinition);
        }

        private void AddSupportRows(QuestObjectiveDefinition objectiveDefinition)
        {
            if (objectiveDefinition == null)
                return;

            switch (objectiveDefinition.template)
            {
                case QuestObjectiveTemplate.PerformMovementSkill:
                    switch (objectiveDefinition.movementSkill)
                    {
                        case QuestMovementSkillType.JumpAdjust:
                            AddRow("jump_support", "Jump", $"Hold {Binding("Jump")}", GetLatched("jump_support", IsJumpActive()));
                            AddRow("air_adjust_support", "Air adjust", $"{Binding("Lean")} + {CombineBindings("LeftSki", "RightSki")}", GetLatched("air_adjust_support", IsAirControlActive()));
                            return;

                        case QuestMovementSkillType.SkateSwitch:
                            AddRow("lean_fwd_support", "Lean fwd", Binding("Lean", "positive", "up", "forward"), GetLatched("lean_fwd_support", IsLeanForwardActive()));
                            AddRow("skate_support", "Alt skis", $"{Binding("LeftSki")} / {Binding("RightSki")}", GetLatched("skate_support", IsSkatingActive()));
                            return;

                        case QuestMovementSkillType.QuickStop:
                            AddRow("lean_back_support", "Lean back", Binding("Lean", "negative", "down", "back", "backward"), GetLatched("lean_back_support", IsLeanBackwardActive()));
                            AddRow("turn_support", "Turn", CombineBindings("LeftSki", "RightSki"), GetLatched("turn_support", IsTurningActive()));
                            return;
                    }
                    break;

                case QuestObjectiveTemplate.UseInput:
                    switch (objectiveDefinition.inputAction)
                    {
                        case QuestInputActionType.AirPose:
                            AddRow("jump_setup", "Get airborne", Binding("Jump"), GetLatched("jump_setup", IsJumpActive()));
                            return;

                        case QuestInputActionType.PolePush:
                            AddRow("pole_push_tip", "Best use", "Flat / low speed", false);
                            return;

                        case QuestInputActionType.PoleDrag:
                            AddRow("pole_drag_tip", "Best use", "While moving", false);
                            return;
                    }
                    break;

                case QuestObjectiveTemplate.OpenUiScreen:
                    if (objectiveDefinition.uiTarget == QuestUiScreenTargetType.TasksViewed ||
                        objectiveDefinition.uiTarget == QuestUiScreenTargetType.QuestsViewed ||
                        objectiveDefinition.uiTarget == QuestUiScreenTargetType.MapViewed ||
                        objectiveDefinition.uiTarget == QuestUiScreenTargetType.StatsViewed)
                    {
                        AddRow("overlay_support", "Open overlay", GetOverlayBindingDisplay(), GetLatched("overlay_support", IsActionPressed("Toggle")));
                        return;
                    }

                    if (objectiveDefinition.uiTarget == QuestUiScreenTargetType.ShopOpened ||
                        objectiveDefinition.uiTarget == QuestUiScreenTargetType.KioskOpened ||
                        objectiveDefinition.uiTarget == QuestUiScreenTargetType.RaceKioskOpened)
                    {
                        AddRow("interact_support", "Interact", Binding("Interact"), GetLatched("interact_support", IsActionPressed("Interact")));
                        return;
                    }
                    break;

                case QuestObjectiveTemplate.Interaction:
                    AddRow("interact_row", "Interact", Binding("Interact"), GetLatched("interact_row", IsActionPressed("Interact")));
                    switch (objectiveDefinition.interactionTarget)
                    {
                        case QuestInteractionTargetType.Stacked:
                            AddRow("stack_tip", "Next", $"Hold {Binding("EquipSkis")} -> walk", false);
                            break;

                        case QuestInteractionTargetType.UnequipSkis:
                            AddRow("walk_tip", "Goal", "Switch to walk mode.", false);
                            break;

                        case QuestInteractionTargetType.EquipSkis:
                            AddRow("ski_tip", "Goal", "Put skis back on.", false);
                            break;

                        case QuestInteractionTargetType.EnterResort:
                            AddRow("resort_tip", "Goal", "Go inside the resort.", false);
                            break;

                        case QuestInteractionTargetType.ExitResort:
                            AddRow("leave_tip", "Goal", "Head back outside.", false);
                            break;
                    }
                    return;
            }

            if (!string.IsNullOrWhiteSpace(objectiveDefinition.description))
                AddRow("objective_hint", "Tip", objectiveDefinition.description.Trim(), false);
        }

        private void AddRow(string key, string label, string bindingText, bool isHighlighted, bool isPrimary = false)
        {
            AddRow(key, label, InputPromptTokens.Text(bindingText), isHighlighted, isPrimary);
        }

        private void AddRow(string key, string label, InputPromptToken[] promptTokens, bool isHighlighted, bool isPrimary = false)
        {
            _buffer.Add(new InputRow
            {
                Key = key,
                Label = label,
                PromptTokens = promptTokens == null || promptTokens.Length == 0 ? InputPromptTokens.Text("-") : promptTokens,
                IsHighlighted = isHighlighted,
                IsPrimary = isPrimary
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
                if (rowData.IsPrimary)
                    row.AddToClassList("input-row-primary");

                var lblName = new Label(rowData.Label);
                lblName.AddToClassList("input-row-name");
                if (rowData.IsPrimary)
                    lblName.AddToClassList("input-row-name-primary");

                var prompt = InputPromptVisualBuilder.CreatePrompt(rowData.PromptTokens, "input-row-binding");
                if (rowData.IsPrimary)
                    prompt.AddToClassList("input-row-binding-primary");

                ApplyHighlightClasses(row, lblName, prompt, rowData.IsHighlighted);

                row.Add(prompt);
                row.Add(lblName);
                _list.Add(row);

                _rowViews.Add(new RowView
                {
                    Root = row,
                    Name = lblName,
                    Prompt = prompt,
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

                ApplyHighlightClasses(rowView.Root, rowView.Name, rowView.Prompt, rowData.IsHighlighted);
            }
        }

        private static void ApplyHighlightClasses(VisualElement row, Label name, VisualElement binding, bool highlighted)
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
                  .Append(InputPromptResolver.BuildSignature(rows[i].PromptTokens)).Append('|')
                  .Append(rows[i].IsPrimary ? 'P' : 'S').Append('|')
                  .Append(rows[i].IsHighlighted ? '1' : '0')
                  .Append(';');
            }
            return sb.ToString();
        }

        private InputPromptToken[] BuildDetailedPromptTokens(QuestObjectiveDefinition objectiveDefinition)
        {
            if (objectiveDefinition == null)
                return InputPromptTokens.Text("-");

            switch (objectiveDefinition.template)
            {
                case QuestObjectiveTemplate.UseInput:
                    switch (objectiveDefinition.inputAction)
                    {
                        case QuestInputActionType.LeanForward:
                            return Action("Lean", "positive", "up", "forward");
                        case QuestInputActionType.LeanBackward:
                            return Action("Lean", "negative", "down", "back", "backward");
                        case QuestInputActionType.Tuck:
                            return HoldAction("Sprint");
                        case QuestInputActionType.PolePush:
                            return Join(Action("Poles"), "on flats");
                        case QuestInputActionType.PoleDrag:
                            return Join(Action("Poles"), "while moving");
                        case QuestInputActionType.AirPose:
                            return Join(Action("Jump"), "+", Action("Poles"));
                    }
                    break;

                case QuestObjectiveTemplate.PerformMovementSkill:
                    switch (objectiveDefinition.movementSkill)
                    {
                        case QuestMovementSkillType.Wedge:
                            return Join(Action("LeftSki"), "+", Action("RightSki"));
                        case QuestMovementSkillType.SkateSwitch:
                            return Join(Action("Lean", "positive", "up", "forward"), "+", Join(Action("LeftSki"), "/", Action("RightSki")));
                        case QuestMovementSkillType.QuickStop:
                            return Join(Action("Lean", "negative", "down", "back", "backward"), "+", Join(Action("LeftSki"), "/", Action("RightSki")));
                        case QuestMovementSkillType.JumpAdjust:
                            return Join(Action("Jump"), "+", Join(LeanAxis(), "+", TurnAxis()));
                    }
                    break;

                case QuestObjectiveTemplate.OpenUiScreen:
                    switch (objectiveDefinition.uiTarget)
                    {
                        case QuestUiScreenTargetType.OverlayOpened:
                            return Action("Toggle");
                        case QuestUiScreenTargetType.MapViewed:
                            return Join(Action("Toggle"), "-> Map");
                        case QuestUiScreenTargetType.StatsViewed:
                            return Join(Action("Toggle"), "-> Stats");
                        case QuestUiScreenTargetType.TasksViewed:
                            return Join(Action("Toggle"), "-> Goals -> Tasks");
                        case QuestUiScreenTargetType.QuestsViewed:
                            return Join(Action("Toggle"), "-> Goals -> Quests");
                        case QuestUiScreenTargetType.TaskRewardClaimed:
                            return Join(Action("Toggle"), "-> Goals -> Tasks -> Claim");
                        case QuestUiScreenTargetType.ShopOpened:
                        case QuestUiScreenTargetType.KioskOpened:
                        case QuestUiScreenTargetType.RaceKioskOpened:
                            return Join(InputPromptTokens.Text("Beacon"), "+", Action("Interact"));
                    }
                    break;

                case QuestObjectiveTemplate.Interaction:
                    switch (objectiveDefinition.interactionTarget)
                    {
                        case QuestInteractionTargetType.Stacked:
                            return InputPromptTokens.Text("Stack / topple");
                        case QuestInteractionTargetType.EquipSkis:
                        case QuestInteractionTargetType.UnequipSkis:
                            return HoldAction("EquipSkis");
                        case QuestInteractionTargetType.ClaimDefaultPass:
                            return Join(Action("Interact"), "+", InputPromptTokens.Text("Claim"));
                        case QuestInteractionTargetType.ExitResort:
                            return InputPromptTokens.Text("Leave resort");
                        default:
                            return Join(InputPromptTokens.Text("Beacon"), "+", Action("Interact"));
                    }

                case QuestObjectiveTemplate.ReachThreshold:
                    if (objectiveDefinition.thresholdStat == QuestThresholdStatType.Soreness)
                    {
                        return objectiveDefinition.comparison == QuestComparisonOp.LessOrEqual
                            ? InputPromptTokens.Text("Stay in resort")
                            : InputPromptTokens.Text("Crash / rough land");
                    }
                    break;

                case QuestObjectiveTemplate.TravelDistance:
                    return InputPromptTokens.Text("Keep moving");

                case QuestObjectiveTemplate.Activity:
                    switch (objectiveDefinition.activityTarget)
                    {
                        case QuestActivityTargetType.RaceStarted:
                            return InputPromptTokens.Text("Kiosk -> Start gate");
                        case QuestActivityTargetType.RaceCompleted:
                            return InputPromptTokens.Text("Finish the race");
                        case QuestActivityTargetType.RescueStarted:
                            return InputPromptTokens.Text("Medic tent -> Accept");
                        case QuestActivityTargetType.RescueCompleted:
                            return InputPromptTokens.Text("Finish rescue");
                    }
                    break;
            }

            return InputPromptTokens.Text(objectiveDefinition.BuildPromptLabel());
        }

        private string BuildDetailedPrompt(QuestObjectiveDefinition objectiveDefinition)
        {
            if (objectiveDefinition == null)
                return "-";

            switch (objectiveDefinition.template)
            {
                case QuestObjectiveTemplate.UseInput:
                    switch (objectiveDefinition.inputAction)
                    {
                        case QuestInputActionType.LeanForward:
                            return Binding("Lean", "positive", "up", "forward");
                        case QuestInputActionType.LeanBackward:
                            return Binding("Lean", "negative", "down", "back", "backward");
                        case QuestInputActionType.Tuck:
                            return $"Hold {Binding("Sprint")}";
                        case QuestInputActionType.PolePush:
                            return $"{Binding("Poles")} on flats";
                        case QuestInputActionType.PoleDrag:
                            return $"{Binding("Poles")} while moving";
                        case QuestInputActionType.AirPose:
                            return $"{Binding("Jump")} + {Binding("Poles")}";
                    }
                    break;

                case QuestObjectiveTemplate.PerformMovementSkill:
                    switch (objectiveDefinition.movementSkill)
                    {
                        case QuestMovementSkillType.Wedge:
                            return $"{Binding("LeftSki")} + {Binding("RightSki")}";
                        case QuestMovementSkillType.SkateSwitch:
                            return $"{Binding("Lean", "positive", "up", "forward")} + {Binding("LeftSki")} / {Binding("RightSki")}";
                        case QuestMovementSkillType.QuickStop:
                            return $"{Binding("Lean", "negative", "down", "back", "backward")} + {Binding("LeftSki")} / {Binding("RightSki")}";
                        case QuestMovementSkillType.JumpAdjust:
                            return $"{Binding("Jump")} + {Binding("Lean")} + {CombineBindings("LeftSki", "RightSki")}";
                    }
                    break;

                case QuestObjectiveTemplate.OpenUiScreen:
                    switch (objectiveDefinition.uiTarget)
                    {
                        case QuestUiScreenTargetType.OverlayOpened:
                            return GetOverlayBindingDisplay();
                        case QuestUiScreenTargetType.MapViewed:
                            return $"{GetOverlayBindingDisplay()} -> Map";
                        case QuestUiScreenTargetType.StatsViewed:
                            return $"{GetOverlayBindingDisplay()} -> Stats";
                        case QuestUiScreenTargetType.TasksViewed:
                            return $"{GetOverlayBindingDisplay()} -> Goals -> Tasks";
                        case QuestUiScreenTargetType.QuestsViewed:
                            return $"{GetOverlayBindingDisplay()} -> Goals -> Quests";
                        case QuestUiScreenTargetType.TaskRewardClaimed:
                            return $"{GetOverlayBindingDisplay()} -> Goals -> Tasks -> Claim";
                        case QuestUiScreenTargetType.ShopOpened:
                        case QuestUiScreenTargetType.KioskOpened:
                        case QuestUiScreenTargetType.RaceKioskOpened:
                            return $"Beacon + {Binding("Interact")}";
                    }
                    break;

                case QuestObjectiveTemplate.Interaction:
                    switch (objectiveDefinition.interactionTarget)
                    {
                        case QuestInteractionTargetType.Stacked:
                            return "Stack / topple";
                        case QuestInteractionTargetType.EquipSkis:
                        case QuestInteractionTargetType.UnequipSkis:
                            return $"Hold {Binding("EquipSkis")}";
                        case QuestInteractionTargetType.ClaimDefaultPass:
                            return $"{Binding("Interact")} + Claim";
                        case QuestInteractionTargetType.ExitResort:
                            return "Leave resort";
                        default:
                            return $"Beacon + {Binding("Interact")}";
                    }

                case QuestObjectiveTemplate.ReachThreshold:
                    if (objectiveDefinition.thresholdStat == QuestThresholdStatType.Soreness)
                    {
                        return objectiveDefinition.comparison == QuestComparisonOp.LessOrEqual
                            ? "Stay in resort"
                            : "Crash / rough land";
                    }
                    break;

                case QuestObjectiveTemplate.TravelDistance:
                    return "Keep moving";

                case QuestObjectiveTemplate.Activity:
                    switch (objectiveDefinition.activityTarget)
                    {
                        case QuestActivityTargetType.RaceStarted:
                            return "Kiosk -> Start gate";
                        case QuestActivityTargetType.RaceCompleted:
                            return "Finish the race";
                        case QuestActivityTargetType.RescueStarted:
                            return "Medic tent -> Accept";
                        case QuestActivityTargetType.RescueCompleted:
                            return "Finish rescue";
                    }
                    break;
            }

            return objectiveDefinition.BuildPromptLabel();
        }

        private bool IsObjectiveHighlighted(QuestObjectiveDefinition objectiveDefinition)
        {
            if (objectiveDefinition == null)
                return false;

            switch (objectiveDefinition.template)
            {
                case QuestObjectiveTemplate.UseInput:
                    switch (objectiveDefinition.inputAction)
                    {
                        case QuestInputActionType.LeanForward: return GetLatched("objective_lean_fwd", IsLeanForwardActive());
                        case QuestInputActionType.LeanBackward: return GetLatched("objective_lean_back", IsLeanBackwardActive());
                        case QuestInputActionType.Tuck: return GetLatched("objective_tuck", IsActionPressed("Sprint"));
                        case QuestInputActionType.PolePush: return GetLatched("objective_pole_push", IsPolePushActive());
                        case QuestInputActionType.PoleDrag: return GetLatched("objective_pole_drag", IsPoleDragActive());
                        case QuestInputActionType.AirPose: return GetLatched("objective_air_pose", !IsGrounded() && IsActionPressed("Poles"));
                    }
                    break;

                case QuestObjectiveTemplate.PerformMovementSkill:
                    switch (objectiveDefinition.movementSkill)
                    {
                        case QuestMovementSkillType.Wedge: return GetLatched("objective_wedge", IsWedgeActive());
                        case QuestMovementSkillType.SkateSwitch: return GetLatched("objective_skate", IsSkatingActive());
                        case QuestMovementSkillType.QuickStop: return GetLatched("objective_quick_stop", IsQuickStopActive());
                        case QuestMovementSkillType.JumpAdjust: return GetLatched("objective_jump_adjust", IsJumpActive() || IsAirControlActive());
                    }
                    break;

                case QuestObjectiveTemplate.OpenUiScreen:
                    return objectiveDefinition.uiTarget == QuestUiScreenTargetType.OverlayOpened && GetLatched("objective_overlay", IsActionPressed("Toggle"));

                case QuestObjectiveTemplate.Interaction:
                    switch (objectiveDefinition.interactionTarget)
                    {
                        case QuestInteractionTargetType.Stacked:
                            return GetLatched("objective_stacked", skiController != null && skiController.IsStacked);
                        case QuestInteractionTargetType.EquipSkis:
                        case QuestInteractionTargetType.UnequipSkis:
                            return GetLatched("objective_equip", IsActionPressed("EquipSkis"));
                        default:
                            return GetLatched("objective_interact", IsActionPressed("Interact"));
                    }
            }

            return false;
        }

        private bool IsConditionPathCompleted(QuestObjectiveRuntimeState objectiveState, string path)
        {
            if (objectiveState?.conditionStates == null || string.IsNullOrWhiteSpace(path))
                return false;

            for (int i = 0; i < objectiveState.conditionStates.Count; i++)
            {
                var state = objectiveState.conditionStates[i];
                if (state != null && string.Equals(state.path, path, StringComparison.Ordinal))
                    return state.completed;
            }

            return false;
        }

        private static QuestObjectiveDefinition FindObjectiveDefinition(QuestStageDefinition stage, string objectiveId)
        {
            if (stage?.objectives == null || string.IsNullOrWhiteSpace(objectiveId))
                return null;

            for (int i = 0; i < stage.objectives.Count; i++)
            {
                var objective = stage.objectives[i];
                if (objective != null && string.Equals(objective.id, objectiveId, StringComparison.OrdinalIgnoreCase))
                    return objective;
            }

            return null;
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

        private string GetShiftLikeBinding()
        {
            string sprint = Binding("Sprint");
            if (!string.IsNullOrWhiteSpace(sprint) && sprint != "-")
                return sprint;

            return "Shift";
        }

        private InputPromptToken[] Action(string actionName, params string[] compositePartNames)
        {
            return InputPromptResolver.ResolveActionTokens(inputActions, actionName, iconLibrary, compositePartNames);
        }

        private InputPromptToken[] HoldAction(string actionName, params string[] compositePartNames)
        {
            return InputPromptTokens.Phrase("Hold", Action(actionName, compositePartNames));
        }

        private InputPromptToken[] LeanAxis()
        {
            return Join(Action("Lean", "positive", "up", "forward"), "/", Action("Lean", "negative", "down", "back", "backward"));
        }

        private InputPromptToken[] TurnAxis()
        {
            return Join(Action("LeftSki"), "/", Action("RightSki"));
        }

        private static InputPromptToken[] Join(IReadOnlyList<InputPromptToken> left, string separatorOrText, IReadOnlyList<InputPromptToken> right)
        {
            return InputPromptTokens.Concat(left, new[] { InputPromptToken.Separator(separatorOrText) }, right);
        }

        private static InputPromptToken[] Join(IReadOnlyList<InputPromptToken> left, string trailingText)
        {
            return InputPromptTokens.Concat(left, InputPromptTokens.Text(trailingText));
        }

        private string Binding(string actionName, params string[] compositePartNames)
        {
            return InputPromptResolver.GetBindingDisplay(inputActions, actionName, compositePartNames);
        }

        private string HoldBinding(string actionName, params string[] compositePartNames)
        {
            return $"Hold {Binding(actionName, compositePartNames)}";
        }

        private string GetOverlayBindingDisplay()
        {
            return overlayController != null
                ? overlayController.GetOverlayToggleBindingDisplay(inputActions)
                : InputPromptResolver.GetBindingDisplay(inputActions, "Toggle");
        }

        private string LeanAxisText()
        {
            return $"{Binding("Lean", "positive", "up", "forward")} / {Binding("Lean", "negative", "down", "back", "backward")}";
        }

        private string CombineBindings(params string[] actionNames)
        {
            return InputPromptResolver.CombineBindings(inputActions, actionNames);
        }

        private bool IsActionPressed(string actionName)
        {
            var action = InputPromptResolver.FindActionByFriendlyName(inputActions, actionName);
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

        private bool IsWedgeActive()
        {
            if (skiController == null)
                return false;

            return Mathf.Abs(skiController.LeftLegInput) >= turnEnterThreshold &&
                   Mathf.Abs(skiController.RightLegInput) >= turnEnterThreshold &&
                   Mathf.Sign(skiController.LeftLegInput) != Mathf.Sign(skiController.RightLegInput);
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

        private bool IsGrounded()
        {
            return skiController != null && skiController.IsRiderGrounded;
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
