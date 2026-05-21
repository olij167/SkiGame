using System;
using System.Collections.Generic;
using System.IO;
using SkiGame.Progression;
using SkiGame.Tricks;
using UnityEditor;
using UnityEngine;

namespace SkiGame.EditorTools
{
    [CustomEditor(typeof(QuestDefinitionSO))]
    public sealed class QuestDefinitionSOEditor : Editor
    {
        private enum HelpTopic
        {
            Identity,
            Flow,
            TurnIn,
            StageStructure,
            TemplateSelection,
            Filters,
            ChildConditions,
            AdvancedKeys,
            Dialogue,
            Recipes
        }

        private struct ValidationMessage
        {
            public MessageType type;
            public string text;

            public ValidationMessage(MessageType type, string text)
            {
                this.type = type;
                this.text = text;
            }
        }

        private readonly Dictionary<object, bool> _foldouts = new Dictionary<object, bool>();
        private readonly Dictionary<HelpTopic, bool> _helpFoldouts = new Dictionary<HelpTopic, bool>();
        private readonly List<QuestDialogueProfileSO> _foundProfiles = new List<QuestDialogueProfileSO>();
        private bool _dialogueFoldout;

        private static readonly GUIContent QuestIdLabel = new GUIContent("Quest Id", "Stable ID used by save data, quest lookups, dialogue bindings, and debugging. Avoid renaming after release.");
        private static readonly GUIContent TitleLabel = new GUIContent("Title", "Player-facing quest title shown in UI.");
        private static readonly GUIContent DescriptionLabel = new GUIContent("Description", "Player-facing quest summary shown in quest UI and dialogue token formatting.");
        private static readonly GUIContent AutoAcceptLabel = new GUIContent("Auto Accept", "If enabled, the quest starts immediately when offered instead of waiting for explicit player acceptance.");
        private static readonly GUIContent TutorialQuestLabel = new GUIContent("Tutorial Quest", "Marks this quest as tutorial content for systems that filter or present lessons differently.");
        private static readonly GUIContent CompletionModeLabel = new GUIContent("Completion Mode", "Controls whether the quest completes immediately after objectives finish or waits for a turn-in interaction.");
        private static readonly GUIContent TurnInTargetTypeLabel = new GUIContent("Turn-In Target", "Where the player must return after objectives are complete.");
        private static readonly GUIContent TurnInNpcLabel = new GUIContent("NPC Identity Id", "Specific NPC identity required for turn-in when using the SpecificNpcIdentity target.");
        private static readonly GUIContent TurnInArchetypeLabel = new GUIContent("NPC Archetype Id", "Generic NPC archetype that can accept turn-in when using the GenericNpcArchetype target.");
        private static readonly GUIContent TurnInLocationLabel = new GUIContent("Location Id", "Scene marker or authored location key used for location-based turn-ins.");
        private static readonly GUIContent TurnInOriginalLabel = new GUIContent("Original Quest Giver", "Allows the NPC who accepted/offered this quest to receive the turn-in.");

        private bool GetFoldoutState(object key, bool defaultExpanded = false)
        {
            if (key == null)
                return defaultExpanded;

            if (!_foldouts.TryGetValue(key, out bool expanded))
            {
                expanded = defaultExpanded;
                _foldouts[key] = expanded;
            }

            return expanded;
        }

        private void SetFoldoutState(object key, bool expanded)
        {
            if (key != null)
                _foldouts[key] = expanded;
        }

        private void RemoveFoldoutState(object key)
        {
            if (key != null)
                _foldouts.Remove(key);
        }

        private bool DrawHelpFoldout(HelpTopic topic, string label, string text)
        {
            _helpFoldouts.TryGetValue(topic, out bool expanded);
            expanded = EditorGUILayout.Foldout(expanded, new GUIContent(label, "Show contextual quest authoring guidance."), true);
            _helpFoldouts[topic] = expanded;
            if (expanded)
                EditorGUILayout.HelpBox(text, MessageType.Info);
            return expanded;
        }

        private static string GetStageFoldoutLabel(QuestStageDefinition stage, int index)
        {
            if (stage == null)
                return $"Stage {index + 1}";

            string title = string.IsNullOrWhiteSpace(stage.title) ? $"Stage {index + 1}" : stage.title.Trim();
            return string.IsNullOrWhiteSpace(stage.id) ? title : $"{title}  ({stage.id.Trim()})";
        }

        private static string GetObjectiveFoldoutLabel(QuestObjectiveDefinition objective, int index)
        {
            if (objective == null)
                return $"Objective {index + 1}";

            string label = string.IsNullOrWhiteSpace(objective.title) ? $"Objective {index + 1}" : objective.title.Trim();
            return $"{label}  [{objective.template}]";
        }

        public override void OnInspectorGUI()
        {
            var quest = (QuestDefinitionSO)target;
            if (quest == null)
                return;

            serializedObject.Update();
            Undo.RecordObject(quest, "Edit Quest Definition");

            DrawValidationSummary(quest);
            EditorGUILayout.Space(4f);

            DrawSectionHeader("Identity", "Core quest IDs and player-facing text.");
            DrawHelpFoldout(HelpTopic.Identity, "Quest identity/setup help", "Use a stable quest ID before wiring dialogue, saves, or catalog entries. The title and description are player-facing; leave implementation notes in comments or asset names instead.");
            quest.id = EditorGUILayout.TextField(QuestIdLabel, quest.id);
            quest.title = EditorGUILayout.TextField(TitleLabel, quest.title);
            EditorGUILayout.LabelField(DescriptionLabel);
            quest.description = EditorGUILayout.TextArea(quest.description ?? string.Empty, GUILayout.MinHeight(48f));

            EditorGUILayout.Space(6f);
            DrawSectionHeader("Flow", "Acceptance, tutorial tagging, completion, and turn-in behavior.");
            DrawHelpFoldout(HelpTopic.Flow, "Quest flow and completion mode help", "Auto-complete is best for lightweight goals that should finish immediately. Require turn-in when the quest needs a final NPC/location interaction, reward beat, or closing dialogue.");
            quest.autoAccept = EditorGUILayout.Toggle(AutoAcceptLabel, quest.autoAccept);
            quest.tutorialQuest = EditorGUILayout.Toggle(TutorialQuestLabel, quest.tutorialQuest);
            quest.completionMode = (QuestCompletionMode)EditorGUILayout.EnumPopup(CompletionModeLabel, quest.completionMode);

            if (quest.completionMode == QuestCompletionMode.RequireTurnIn)
            {
                DrawHelpFoldout(HelpTopic.TurnIn, "Turn-in behavior help", "Choose OriginalQuestGiver for NPC-offered quests, SpecificNpcIdentity for a named NPC, GenericNpcArchetype for any matching NPC type, or SceneMarker for location-based delivery.");
                quest.turnInTargetType = (QuestTurnInTargetType)EditorGUILayout.EnumPopup(TurnInTargetTypeLabel, quest.turnInTargetType);
                quest.turnInToOriginalQuestGiver = EditorGUILayout.Toggle(TurnInOriginalLabel, quest.turnInToOriginalQuestGiver);

                if (quest.turnInTargetType == QuestTurnInTargetType.SpecificNpcIdentity)
                    quest.turnInNpcIdentityId = EditorGUILayout.TextField(TurnInNpcLabel, quest.turnInNpcIdentityId);
                if (quest.turnInTargetType == QuestTurnInTargetType.GenericNpcArchetype)
                    quest.turnInGenericArchetypeId = EditorGUILayout.TextField(TurnInArchetypeLabel, quest.turnInGenericArchetypeId);
                if (quest.turnInTargetType == QuestTurnInTargetType.SceneMarker)
                    quest.turnInLocationId = EditorGUILayout.TextField(TurnInLocationLabel, quest.turnInLocationId);
            }

            EditorGUILayout.Space(8f);
            DrawDialogueIntegration(quest);

            EditorGUILayout.Space(8f);
            DrawSectionHeader("Stages", "Ordered quest stages and their objectives.");
            DrawHelpFoldout(HelpTopic.StageStructure, "Stage structure help", "Each stage should have a stable ID and at least one objective. Dialogue profiles can bind to these stage IDs, so keep them unique within the quest.");

            quest.stages ??= new List<QuestStageDefinition>();
            for (int i = 0; i < quest.stages.Count; i++)
            {
                var stage = quest.stages[i] ?? new QuestStageDefinition();
                quest.stages[i] = stage;

                bool expanded = GetFoldoutState(stage);
                expanded = EditorGUILayout.BeginFoldoutHeaderGroup(expanded, new GUIContent(GetStageFoldoutLabel(stage, i), "Stable foldout state is tied to this stage object, not its mutable ID or list index."));
                SetFoldoutState(stage, expanded);

                if (expanded)
                {
                    EditorGUILayout.BeginVertical("box");
                    stage.id = EditorGUILayout.TextField(new GUIContent("Stage Id", "Stable stage key used by dialogue bindings and progression checks. Keep unique within this quest."), stage.id);
                    stage.title = EditorGUILayout.TextField(new GUIContent("Stage Title", "Player-facing stage title. If empty, UI can fall back to the stage number."), stage.title);
                    EditorGUILayout.LabelField(new GUIContent("Stage Description", "Player-facing stage description or instruction text."));
                    stage.description = EditorGUILayout.TextArea(stage.description ?? string.Empty, GUILayout.MinHeight(36f));

                    DrawStageToolbar(quest, i);
                    DrawObjectives(stage);
                    EditorGUILayout.EndVertical();
                }

                EditorGUILayout.EndFoldoutHeaderGroup();
                EditorGUILayout.Space(4f);
            }

            if (GUILayout.Button(new GUIContent("Add Stage", "Adds a new quest stage and expands it for editing.")))
            {
                var newStage = new QuestStageDefinition();
                quest.stages.Add(newStage);
                SetFoldoutState(newStage, true);
            }

            EditorGUILayout.Space(6f);
            DrawHelpFoldout(HelpTopic.Recipes, "Common quest setup recipes/tutorials", "Simple collect/visit: one stage, one objective, then choose auto-complete or turn-in.\n\nMulti-stage: give each stage a stable ID, add objectives per stage, then sync a dialogue profile for stage bindings.\n\nCompound objective: make a parent objective with the Compound template, then add child objectives for the real requirements.\n\nFiltered objective: use Advanced conditions and filters to constrain credit to a region, NPC, activity, item, or authored key.\n\nDialogue-linked quest: create/link a QuestDialogueProfileSO, sync stage bindings, then create NPCDialogueSequenceSO assets for accepted, ready, completed, and stage events.");

            if (GUI.changed)
                EditorUtility.SetDirty(quest);

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawSectionHeader(string label, string tooltip)
        {
            EditorGUILayout.LabelField(new GUIContent(label, tooltip), EditorStyles.boldLabel);
        }

        private void DrawObjectives(QuestStageDefinition stage)
        {
            stage.objectives ??= new List<QuestObjectiveDefinition>();
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(new GUIContent("Objectives", "Requirements that must be met while this stage is active."), EditorStyles.boldLabel);

            for (int objectiveIndex = 0; objectiveIndex < stage.objectives.Count; objectiveIndex++)
            {
                stage.objectives[objectiveIndex] ??= new QuestObjectiveDefinition();
                DrawObjective(stage.objectives, objectiveIndex, 0);
            }

            if (GUILayout.Button(new GUIContent("Add Objective", "Adds a new objective to this stage.")))
            {
                var newObjective = new QuestObjectiveDefinition();
                stage.objectives.Add(newObjective);
                SetFoldoutState(newObjective, true);
            }
        }

        private void DrawStageToolbar(QuestDefinitionSO quest, int stageIndex)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Move Up", "Moves this stage earlier without changing its stable foldout key.")) && stageIndex > 0)
            {
                Swap(quest.stages, stageIndex, stageIndex - 1);
                GUIUtility.ExitGUI();
            }

            if (GUILayout.Button(new GUIContent("Move Down", "Moves this stage later without changing its stable foldout key.")) && stageIndex < quest.stages.Count - 1)
            {
                Swap(quest.stages, stageIndex, stageIndex + 1);
                GUIUtility.ExitGUI();
            }

            GUI.backgroundColor = new Color(1f, 0.78f, 0.78f);
            if (GUILayout.Button(new GUIContent("Remove Stage", "Removes this stage from the quest. Existing dialogue bindings are not automatically deleted.")))
            {
                RemoveFoldoutState(quest.stages[stageIndex]);
                quest.stages.RemoveAt(stageIndex);
                GUI.backgroundColor = Color.white;
                GUIUtility.ExitGUI();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawObjective(List<QuestObjectiveDefinition> objectives, int objectiveIndex, int depth)
        {
            var objective = objectives[objectiveIndex];
            string label = GetObjectiveFoldoutLabel(objective, objectiveIndex);
            bool expanded = GetFoldoutState(objective);
            expanded = EditorGUILayout.Foldout(expanded, new GUIContent(label, "Stable foldout state is tied to this objective object, not its mutable ID, template, title, or list index."), true);
            SetFoldoutState(objective, expanded);

            if (expanded)
            {
                EditorGUILayout.BeginVertical("box");
                objective.id = EditorGUILayout.TextField(new GUIContent("Objective Id", "Stable objective key used by runtime progress, dialogue bindings, and debugging. Keep unique within this quest."), objective.id);
                objective.title = EditorGUILayout.TextField(new GUIContent("Title", "Player-facing objective title. If empty, the editor summary can be used as a fallback."), objective.title);
                EditorGUILayout.LabelField(new GUIContent("Description", "Player-facing objective description or instruction text."));
                objective.description = EditorGUILayout.TextArea(objective.description ?? string.Empty, GUILayout.MinHeight(32f));

                DrawHelpFoldout(HelpTopic.TemplateSelection, "Objective template selection help", "Templates fill in common condition keys for you. Changing the template changes which fields below are meaningful; Advanced is for authored keys that are not covered by presets.");
                objective.template = (QuestObjectiveTemplate)EditorGUILayout.EnumPopup(new GUIContent("Template", "Preset objective behavior. Changing this changes which fields are meaningful below."), objective.template);
                objective.optional = EditorGUILayout.Toggle(new GUIContent("Optional", "If enabled, this objective can be shown without blocking stage completion."), objective.optional);
                objective.hiddenUntilAvailable = EditorGUILayout.Toggle(new GUIContent("Hidden Until Available", "Keeps the objective hidden until progression logic considers it available."), objective.hiddenUntilAvailable);
                objective.autoPinWhenAvailable = EditorGUILayout.Toggle(new GUIContent("Auto Track Hint", "Suggests this objective for tracking/pinning when it becomes available."), objective.autoPinWhenAvailable);

                EditorGUILayout.Space(3f);
                DrawTemplateFields(objective, depth + 1);
                EditorGUILayout.HelpBox(objective.BuildAuthoringSummary(), MessageType.None);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(new GUIContent("Duplicate", "Clones this objective and inserts the copy after it.")))
                {
                    var clone = CloneObjective(objective);
                    objectives.Insert(objectiveIndex + 1, clone);
                    SetFoldoutState(clone, true);
                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button(new GUIContent("Move Up", "Moves this objective earlier without changing its stable foldout key.")) && objectiveIndex > 0)
                {
                    Swap(objectives, objectiveIndex, objectiveIndex - 1);
                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button(new GUIContent("Move Down", "Moves this objective later without changing its stable foldout key.")) && objectiveIndex < objectives.Count - 1)
                {
                    Swap(objectives, objectiveIndex, objectiveIndex + 1);
                    GUIUtility.ExitGUI();
                }

                GUI.backgroundColor = new Color(1f, 0.78f, 0.78f);
                if (GUILayout.Button(new GUIContent("Remove", "Removes this objective from the quest.")))
                {
                    RemoveFoldoutState(objective);
                    objectives.RemoveAt(objectiveIndex);
                    GUI.backgroundColor = Color.white;
                    GUIUtility.ExitGUI();
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(3f);
        }

        private void DrawTemplateFields(QuestObjectiveDefinition objective, int depth)
        {
            switch (objective.template)
            {
                case QuestObjectiveTemplate.UseInput:
                    objective.inputAction = (QuestInputActionType)EditorGUILayout.EnumPopup(new GUIContent("Input", "Input action that grants objective credit."), objective.inputAction);
                    objective.requiredCount = EditorGUILayout.IntField(new GUIContent("Count", "Number of matching input events required."), Mathf.Max(1, objective.requiredCount));
                    break;
                case QuestObjectiveTemplate.PerformMovementSkill:
                    objective.movementSkill = (QuestMovementSkillType)EditorGUILayout.EnumPopup(new GUIContent("Movement Skill", "Movement skill event that grants objective credit."), objective.movementSkill);
                    objective.requiredCount = EditorGUILayout.IntField(new GUIContent("Count", "Number of matching movement events required."), Mathf.Max(1, objective.requiredCount));
                    break;
                case QuestObjectiveTemplate.TravelDistance:
                    objective.distanceStat = (QuestDistanceStatType)EditorGUILayout.EnumPopup(new GUIContent("Metric", "Accumulating distance/time stat to track."), objective.distanceStat);
                    objective.comparison = (QuestComparisonOp)EditorGUILayout.EnumPopup(new GUIContent("Comparison", "How accumulated progress is compared to the target."), objective.comparison);
                    objective.targetValue = EditorGUILayout.FloatField(new GUIContent("Target", "Required accumulated value."), objective.targetValue);
                    break;
                case QuestObjectiveTemplate.ReachThreshold:
                    objective.thresholdStat = (QuestThresholdStatType)EditorGUILayout.EnumPopup(new GUIContent("Metric", "Live stat to compare against the target."), objective.thresholdStat);
                    objective.comparison = (QuestComparisonOp)EditorGUILayout.EnumPopup(new GUIContent("Comparison", "How the live stat is compared to the target."), objective.comparison);
                    objective.targetValue = EditorGUILayout.FloatField(new GUIContent("Target", "Threshold value to reach."), objective.targetValue);
                    break;
                case QuestObjectiveTemplate.ReachState:
                    objective.stateTarget = (QuestStateTargetType)EditorGUILayout.EnumPopup(new GUIContent("State", "Boolean player/world state that must become true."), objective.stateTarget);
                    break;
                case QuestObjectiveTemplate.Activity:
                    objective.activityTarget = (QuestActivityTargetType)EditorGUILayout.EnumPopup(new GUIContent("Activity", "Activity event that grants objective credit."), objective.activityTarget);
                    objective.requiredCount = EditorGUILayout.IntField(new GUIContent("Count", "Number of matching activity events required."), Mathf.Max(1, objective.requiredCount));
                    break;
                case QuestObjectiveTemplate.Trick:
                    objective.trickTarget = (QuestTrickTargetType)EditorGUILayout.EnumPopup(new GUIContent("Trick Target", "Trick event or rule used to award objective credit."), objective.trickTarget);
                    if (objective.trickTarget == QuestTrickTargetType.NamedLanding)
                        objective.namedTrick = EditorGUILayout.TextField(new GUIContent("Named Trick", "Named trick ID or pose label expected from trick telemetry."), objective.namedTrick);
                    if (objective.trickTarget == QuestTrickTargetType.RuleLanding || objective.trickRequirement != null)
                    {
                        EditorGUILayout.Space(3f);
                        DrawTrickRequirementEditor(objective.trickRequirement ??= new TrickRequirementDefinition());
                    }
                    objective.requiredCount = EditorGUILayout.IntField(new GUIContent("Count", "Number of matching trick events required."), Mathf.Max(1, objective.requiredCount));
                    break;
                case QuestObjectiveTemplate.Interaction:
                    objective.interactionTarget = (QuestInteractionTargetType)EditorGUILayout.EnumPopup(new GUIContent("Interaction", "Interaction event or state that grants objective credit."), objective.interactionTarget);
                    objective.requiredCount = EditorGUILayout.IntField(new GUIContent("Count", "Number of matching interaction events required."), Mathf.Max(1, objective.requiredCount));
                    break;
                case QuestObjectiveTemplate.OpenUiScreen:
                    objective.uiTarget = (QuestUiScreenTargetType)EditorGUILayout.EnumPopup(new GUIContent("Screen", "UI event that grants objective credit."), objective.uiTarget);
                    objective.requiredCount = EditorGUILayout.IntField(new GUIContent("Count", "Number of matching UI events required."), Mathf.Max(1, objective.requiredCount));
                    break;
                case QuestObjectiveTemplate.Compound:
                    DrawHelpFoldout(HelpTopic.ChildConditions, "Child/compound conditions help", "Compound objectives group child objectives. Use AllOf for every child, AnyOf for alternatives, and Sequence when order matters.");
                    objective.compoundMode = (QuestConditionKind)EditorGUILayout.EnumPopup(new GUIContent("Group Mode", "How child objective conditions are combined."), objective.compoundMode);
                    objective.compoundObjectives ??= new List<QuestObjectiveDefinition>();
                    EditorGUILayout.LabelField(new GUIContent("Sub Objectives", "Child objectives that define the real requirements."), EditorStyles.boldLabel);
                    for (int i = 0; i < objective.compoundObjectives.Count; i++)
                    {
                        objective.compoundObjectives[i] ??= new QuestObjectiveDefinition();
                        DrawObjective(objective.compoundObjectives, i, depth + 1);
                    }
                    if (GUILayout.Button(new GUIContent("Add Sub Objective", "Adds a child requirement to this compound objective.")))
                    {
                        var newSubObjective = new QuestObjectiveDefinition();
                        objective.compoundObjectives.Add(newSubObjective);
                        SetFoldoutState(newSubObjective, true);
                    }
                    break;
                case QuestObjectiveTemplate.Advanced:
                    DrawHelpFoldout(HelpTopic.AdvancedKeys, "Advanced condition keys help", "Advanced conditions are evaluated by raw keys. Use them only when built-in templates are not specific enough, and keep keys aligned with QuestSignalCatalog or telemetry emitters.");
                    DrawConditionEditor(objective.advancedCondition ??= new QuestObjectiveCondition(), depth + 1);
                    break;
            }
        }

        private void DrawConditionEditor(QuestObjectiveCondition condition, int depth)
        {
            condition.kind = (QuestConditionKind)EditorGUILayout.EnumPopup(new GUIContent("Condition Kind", "Runtime condition evaluator type."), condition.kind);
            condition.key = EditorGUILayout.TextField(new GUIContent("Key", "Signal, state, or stat key evaluated by this condition."), condition.key);
            condition.comparison = (QuestComparisonOp)EditorGUILayout.EnumPopup(new GUIContent("Comparison", "How the actual value is compared to the target."), condition.comparison);
            condition.targetCount = EditorGUILayout.IntField(new GUIContent("Target Count", "Required event count for event-based conditions."), condition.targetCount);
            condition.targetValue = EditorGUILayout.FloatField(new GUIContent("Target Value", "Required numeric value for stat-based conditions."), condition.targetValue);
            condition.stateValueKind = (QuestValueKind)EditorGUILayout.EnumPopup(new GUIContent("State Value Kind", "Whether this state comparison expects a bool or string value."), condition.stateValueKind);

            if (condition.stateValueKind == QuestValueKind.Bool)
                condition.boolValue = EditorGUILayout.Toggle(new GUIContent("Bool Value", "Expected boolean state value."), condition.boolValue);
            else
                condition.stringValue = EditorGUILayout.TextField(new GUIContent("String Value", "Expected string state value."), condition.stringValue);

            condition.filters ??= new List<QuestConditionFilter>();
            DrawHelpFoldout(HelpTopic.Filters, "Filters help", "Filters constrain objective credit to events carrying matching key/value metadata, such as region, NPC, activity, item, or authored telemetry keys.");
            EditorGUILayout.LabelField(new GUIContent("Filters", "Optional event metadata filters. Empty keys cannot match safely."), EditorStyles.boldLabel);
            for (int i = 0; i < condition.filters.Count; i++)
            {
                var filter = condition.filters[i] ?? new QuestConditionFilter();
                condition.filters[i] = filter;
                EditorGUILayout.BeginHorizontal();
                filter.key = EditorGUILayout.TextField(new GUIContent("Key", "Event metadata key that must be present."), filter.key);
                filter.value = EditorGUILayout.TextField(new GUIContent("Value", "Expected event metadata value."), filter.value);
                if (GUILayout.Button(new GUIContent("X", "Remove this filter."), GUILayout.Width(24f)))
                {
                    condition.filters.RemoveAt(i);
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button(new GUIContent("Add Filter", "Adds a metadata filter to this condition.")))
                condition.filters.Add(new QuestConditionFilter());

            if (condition.kind == QuestConditionKind.TrickRuleEventCount)
            {
                EditorGUILayout.Space(4f);
                DrawTrickRequirementEditor(condition.trickRequirement ??= new TrickRequirementDefinition());
            }

            condition.children ??= new List<QuestObjectiveCondition>();
            EditorGUILayout.LabelField(new GUIContent("Child Conditions", "Nested conditions for AllOf, AnyOf, or Sequence conditions."), EditorStyles.boldLabel);
            for (int i = 0; i < condition.children.Count; i++)
            {
                condition.children[i] ??= new QuestObjectiveCondition();
                EditorGUILayout.BeginVertical("box");
                DrawConditionEditor(condition.children[i], depth + 1);
                GUI.backgroundColor = new Color(1f, 0.78f, 0.78f);
                if (GUILayout.Button(new GUIContent("Remove Child Condition", "Removes this nested condition.")))
                {
                    condition.children.RemoveAt(i);
                    GUI.backgroundColor = Color.white;
                    GUIUtility.ExitGUI();
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button(new GUIContent("Add Child Condition", "Adds a nested condition.")))
                condition.children.Add(new QuestObjectiveCondition());
        }

        private void DrawValidationSummary(QuestDefinitionSO quest)
        {
            var messages = CollectValidationMessages(quest);
            EditorGUILayout.LabelField(new GUIContent("Validation", "Non-destructive authoring checks for common quest and dialogue setup mistakes."), EditorStyles.boldLabel);
            if (messages.Count == 0)
            {
                EditorGUILayout.HelpBox("No quest authoring issues detected.", MessageType.Info);
                return;
            }

            for (int i = 0; i < messages.Count; i++)
                EditorGUILayout.HelpBox(messages[i].text, messages[i].type);
        }

        private List<ValidationMessage> CollectValidationMessages(QuestDefinitionSO quest)
        {
            var messages = new List<ValidationMessage>();
            var stageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var objectiveIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(quest.id))
                messages.Add(new ValidationMessage(MessageType.Error, "Quest ID is empty. Save data, catalog lookup, and dialogue bindings need a stable key."));
            if (string.IsNullOrWhiteSpace(quest.title))
                messages.Add(new ValidationMessage(MessageType.Warning, "Quest title is empty. Players may see a fallback ID/name."));
            if (quest.stages == null || quest.stages.Count == 0)
                messages.Add(new ValidationMessage(MessageType.Error, "Quest has no stages."));

            if (quest.stages != null)
            {
                for (int i = 0; i < quest.stages.Count; i++)
                {
                    var stage = quest.stages[i];
                    if (stage == null)
                        continue;

                    if (string.IsNullOrWhiteSpace(stage.id))
                        messages.Add(new ValidationMessage(MessageType.Warning, $"Stage {i + 1} has an empty ID."));
                    else if (!stageIds.Add(stage.id.Trim()))
                        messages.Add(new ValidationMessage(MessageType.Error, $"Duplicate stage ID '{stage.id}'."));

                    if (stage.objectives == null || stage.objectives.Count == 0)
                        messages.Add(new ValidationMessage(MessageType.Warning, $"Stage '{DisplayStage(stage, i)}' has no objectives."));
                    else
                        CollectObjectiveValidation(stage.objectives, messages, objectiveIds, $"Stage '{DisplayStage(stage, i)}'");
                }
            }

            if (quest.completionMode == QuestCompletionMode.RequireTurnIn)
            {
                bool hasAnyTarget = quest.turnInTargetType != QuestTurnInTargetType.None ||
                                    quest.turnInToOriginalQuestGiver ||
                                    !string.IsNullOrWhiteSpace(quest.turnInNpcIdentityId) ||
                                    !string.IsNullOrWhiteSpace(quest.turnInGenericArchetypeId) ||
                                    !string.IsNullOrWhiteSpace(quest.turnInLocationId);
                if (!hasAnyTarget)
                    messages.Add(new ValidationMessage(MessageType.Error, "RequireTurnIn quest is missing a turn-in target."));
                if (quest.turnInTargetType == QuestTurnInTargetType.SpecificNpcIdentity && string.IsNullOrWhiteSpace(quest.turnInNpcIdentityId))
                    messages.Add(new ValidationMessage(MessageType.Error, "RequireTurnIn uses SpecificNpcIdentity but NPC identity ID is empty."));
            }

            FindProfiles(quest);
            if (_foundProfiles.Count > 0)
            {
                for (int i = 0; i < _foundProfiles.Count; i++)
                    CollectProfileValidation(quest, _foundProfiles[i], messages);
            }

            CollectNpcQuestOfferDialogueValidation(quest, messages);

            return messages;
        }

        private void CollectObjectiveValidation(List<QuestObjectiveDefinition> objectives, List<ValidationMessage> messages, HashSet<string> objectiveIds, string path)
        {
            for (int i = 0; i < objectives.Count; i++)
            {
                var objective = objectives[i];
                if (objective == null)
                    continue;

                string label = string.IsNullOrWhiteSpace(objective.id) ? $"{path} objective {i + 1}" : $"Objective '{objective.id}'";
                if (string.IsNullOrWhiteSpace(objective.id))
                    messages.Add(new ValidationMessage(MessageType.Warning, $"{path} objective {i + 1} has an empty ID."));
                else if (!objectiveIds.Add(objective.id.Trim()))
                    messages.Add(new ValidationMessage(MessageType.Error, $"Duplicate objective ID '{objective.id}' within this quest."));

                if (objective.template == QuestObjectiveTemplate.Compound)
                {
                    if (objective.compoundObjectives == null || objective.compoundObjectives.Count == 0)
                        messages.Add(new ValidationMessage(MessageType.Warning, $"{label} is Compound but has no child objectives."));
                    else
                        CollectObjectiveValidation(objective.compoundObjectives, messages, objectiveIds, label);
                }

                if (objective.template == QuestObjectiveTemplate.Trick && objective.trickTarget == QuestTrickTargetType.NamedLanding && string.IsNullOrWhiteSpace(objective.namedTrick))
                    messages.Add(new ValidationMessage(MessageType.Warning, $"{label} targets a named trick but Named Trick is empty."));

                if (objective.template == QuestObjectiveTemplate.Advanced)
                    CollectConditionValidation(objective.advancedCondition, messages, label);
            }
        }

        private void CollectConditionValidation(QuestObjectiveCondition condition, List<ValidationMessage> messages, string label)
        {
            if (condition == null)
            {
                messages.Add(new ValidationMessage(MessageType.Warning, $"{label} has no advanced condition."));
                return;
            }

            if (RequiresConditionKey(condition.kind) && string.IsNullOrWhiteSpace(condition.key))
                messages.Add(new ValidationMessage(MessageType.Warning, $"{label} has an advanced condition with an empty key."));

            if (condition.filters != null)
            {
                for (int i = 0; i < condition.filters.Count; i++)
                {
                    var filter = condition.filters[i];
                    if (filter != null && string.IsNullOrWhiteSpace(filter.key))
                        messages.Add(new ValidationMessage(MessageType.Warning, $"{label} filter {i + 1} has an empty expected key."));
                }
            }

            if ((condition.kind == QuestConditionKind.AllOf || condition.kind == QuestConditionKind.AnyOf || condition.kind == QuestConditionKind.Sequence) &&
                (condition.children == null || condition.children.Count == 0))
                messages.Add(new ValidationMessage(MessageType.Warning, $"{label} uses {condition.kind} but has no child conditions."));

            if (condition.children != null)
            {
                for (int i = 0; i < condition.children.Count; i++)
                    CollectConditionValidation(condition.children[i], messages, $"{label} child condition {i + 1}");
            }
        }

        private static bool RequiresConditionKey(QuestConditionKind kind)
        {
            return kind == QuestConditionKind.EventCount ||
                   kind == QuestConditionKind.StateEquals ||
                   kind == QuestConditionKind.StatThreshold ||
                   kind == QuestConditionKind.StatAccumulate ||
                   kind == QuestConditionKind.TrickRuleEventCount;
        }

        private void CollectProfileValidation(QuestDefinitionSO quest, QuestDialogueProfileSO profile, List<ValidationMessage> messages)
        {
            if (profile == null)
                return;

            var stageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (quest.stages != null)
            {
                for (int i = 0; i < quest.stages.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(quest.stages[i]?.id))
                        stageIds.Add(quest.stages[i].id.Trim());
                }
            }

            var boundStageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (profile.stageBindings != null)
            {
                for (int i = 0; i < profile.stageBindings.Length; i++)
                {
                    var binding = profile.stageBindings[i];
                    if (binding == null)
                        continue;

                    if (!string.IsNullOrWhiteSpace(binding.stageId))
                    {
                        string trimmed = binding.stageId.Trim();
                        boundStageIds.Add(trimmed);
                        if (!stageIds.Contains(trimmed))
                            messages.Add(new ValidationMessage(MessageType.Warning, $"Dialogue profile '{profile.name}' has a stage binding for missing stage ID '{binding.stageId}'."));
                    }
                }
            }

            foreach (string stageId in stageIds)
            {
                if (!boundStageIds.Contains(stageId))
                    messages.Add(new ValidationMessage(MessageType.Info, $"Quest stage '{stageId}' has no dialogue profile stage binding in '{profile.name}'."));
            }

            if (profile.objectiveBindings != null && profile.objectiveBindings.Length > 0)
                messages.Add(new ValidationMessage(MessageType.Info, $"Dialogue profile '{profile.name}' has objective bindings, but QuestDialogueBridge does not currently consume objective binding events."));
        }

        private void CollectNpcQuestOfferDialogueValidation(QuestDefinitionSO quest, List<ValidationMessage> messages)
        {
            string[] guids = AssetDatabase.FindAssets("t:NpcQuestOfferSO");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var offer = AssetDatabase.LoadAssetAtPath<NpcQuestOfferSO>(path);
                if (offer == null || offer.QuestDefinition != quest)
                    continue;

                var offerObject = new SerializedObject(offer);
                bool hasProfile = offerObject.FindProperty("dialogueProfile")?.objectReferenceValue != null;
                bool hasDirectSequences =
                    offerObject.FindProperty("offerSequence")?.objectReferenceValue != null ||
                    offerObject.FindProperty("acceptedSequence")?.objectReferenceValue != null ||
                    offerObject.FindProperty("inProgressSequence")?.objectReferenceValue != null ||
                    offerObject.FindProperty("readyToTurnInSequence")?.objectReferenceValue != null ||
                    offerObject.FindProperty("completedSequence")?.objectReferenceValue != null ||
                    offerObject.FindProperty("replaySequence")?.objectReferenceValue != null ||
                    offerObject.FindProperty("failedSequence")?.objectReferenceValue != null;

                if (hasProfile && hasDirectSequences)
                    messages.Add(new ValidationMessage(MessageType.Info, $"NpcQuestOfferSO '{offer.name}' has both direct sequence fields and a dialogue profile. Direct offer sequences are authoritative for NPC quest-giver interactions; QuestDialogueBridge profile events are separate lifecycle hooks."));
            }
        }

        private void DrawDialogueIntegration(QuestDefinitionSO quest)
        {
            _dialogueFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_dialogueFoldout, new GUIContent("Dialogue Integration", "Find, create, sync, and inspect quest dialogue profiles associated with this quest."));
            if (_dialogueFoldout)
            {
                EditorGUILayout.BeginVertical("box");
                DrawHelpFoldout(HelpTopic.Dialogue, "Dialogue integration help", "NPC quest offers currently use their direct sequence fields during offer/accept/turn-in interactions. QuestDialogueBridge separately consumes profile onAccepted, onReadyToTurnIn, onCompleted, and stage started/reminder fields. Other profile slots are authoring placeholders until runtime events consume them.");
                FindProfiles(quest);

                if (_foundProfiles.Count == 0)
                    EditorGUILayout.HelpBox("No QuestDialogueProfileSO directly references this quest. Create one here, or assign/link one from a profile asset.", MessageType.Warning);
                else
                {
                    for (int i = 0; i < _foundProfiles.Count; i++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.ObjectField(new GUIContent($"Profile {i + 1}", "Quest dialogue profile referencing this quest."), _foundProfiles[i], typeof(QuestDialogueProfileSO), false);
                        if (GUILayout.Button(new GUIContent("Select", "Select this profile in the Project window."), GUILayout.Width(56f)))
                            Selection.activeObject = _foundProfiles[i];
                        if (GUILayout.Button(new GUIContent("Ping", "Ping this profile in the Project window."), GUILayout.Width(48f)))
                            EditorGUIUtility.PingObject(_foundProfiles[i]);
                        EditorGUILayout.EndHorizontal();
                    }
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(new GUIContent("Create Quest Dialogue Profile", "Creates a profile asset next to this quest and links it to the quest.")))
                    CreateProfileForQuest(quest);
                if (GUILayout.Button(new GUIContent("Find Existing Profiles", "Searches AssetDatabase for profiles referencing this quest.")))
                    FindProfiles(quest);
                EditorGUILayout.EndHorizontal();

                using (new EditorGUI.DisabledScope(_foundProfiles.Count == 0))
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button(new GUIContent("Sync Stage Bindings", "Adds missing stage bindings to the first found profile without deleting existing bindings.")))
                        SyncStageBindings(_foundProfiles[0], quest);
                    if (GUILayout.Button(new GUIContent("Select/Ping Dialogue Profile", "Selects and pings the first found profile.")))
                    {
                        Selection.activeObject = _foundProfiles[0];
                        EditorGUIUtility.PingObject(_foundProfiles[0]);
                    }
                    EditorGUILayout.EndHorizontal();

                    if (GUILayout.Button(new GUIContent("Create Missing Stage Sequences", "Creates missing stage started/completed sequence assets on the first found profile.")))
                        CreateMissingStageSequences(_foundProfiles[0], quest);
                }

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void FindProfiles(QuestDefinitionSO quest)
        {
            _foundProfiles.Clear();
            if (quest == null)
                return;

            string[] guids = AssetDatabase.FindAssets("t:QuestDialogueProfileSO");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var profile = AssetDatabase.LoadAssetAtPath<QuestDialogueProfileSO>(path);
                if (profile != null && profile.questDefinition == quest)
                    _foundProfiles.Add(profile);
            }
        }

        private void CreateProfileForQuest(QuestDefinitionSO quest)
        {
            string questPath = AssetDatabase.GetAssetPath(quest);
            string directory = string.IsNullOrWhiteSpace(questPath) ? "Assets" : Path.GetDirectoryName(questPath)?.Replace("\\", "/");
            if (string.IsNullOrWhiteSpace(directory))
                directory = "Assets";

            string path = AssetDatabase.GenerateUniqueAssetPath($"{directory}/{quest.name}_DialogueProfile.asset");
            var profile = CreateInstance<QuestDialogueProfileSO>();
            profile.questDefinition = quest;
            profile.stageBindings = Array.Empty<StageDialogueBinding>();
            AssetDatabase.CreateAsset(profile, path);
            SyncStageBindings(profile, quest);
            AssetDatabase.SaveAssets();
            Selection.activeObject = profile;
            EditorGUIUtility.PingObject(profile);
            FindProfiles(quest);
        }

        internal static void SyncStageBindings(QuestDialogueProfileSO profile, QuestDefinitionSO quest)
        {
            if (profile == null || quest == null)
                return;

            Undo.RecordObject(profile, "Sync Quest Dialogue Stage Bindings");
            profile.questDefinition = quest;
            var bindings = new List<StageDialogueBinding>();
            if (profile.stageBindings != null)
                bindings.AddRange(profile.stageBindings);

            if (quest.stages == null)
                quest.stages = new List<QuestStageDefinition>();

            for (int i = 0; i < quest.stages.Count; i++)
            {
                var stage = quest.stages[i];
                if (stage == null)
                    continue;

                string stageId = stage.id ?? string.Empty;
                bool exists = false;
                for (int b = 0; b < bindings.Count; b++)
                {
                    var binding = bindings[b];
                    if (binding == null)
                        continue;

                    if ((!string.IsNullOrWhiteSpace(stageId) && string.Equals(binding.stageId, stageId, StringComparison.OrdinalIgnoreCase)) ||
                        binding.stageIndex == i)
                    {
                        if (string.IsNullOrWhiteSpace(binding.stageId))
                            binding.stageId = stageId;
                        if (binding.stageIndex < 0)
                            binding.stageIndex = i;
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                    bindings.Add(new StageDialogueBinding { stageId = stageId, stageIndex = i });
            }

            profile.stageBindings = bindings.ToArray();
            EditorUtility.SetDirty(profile);
        }

        private static void CreateMissingStageSequences(QuestDialogueProfileSO profile, QuestDefinitionSO quest)
        {
            if (profile == null || quest == null)
                return;

            SyncStageBindings(profile, quest);
            string profilePath = AssetDatabase.GetAssetPath(profile);
            string directory = string.IsNullOrWhiteSpace(profilePath) ? "Assets" : Path.GetDirectoryName(profilePath)?.Replace("\\", "/");
            Undo.RecordObject(profile, "Create Missing Stage Dialogue Sequences");

            for (int i = 0; i < profile.stageBindings.Length; i++)
            {
                var binding = profile.stageBindings[i];
                if (binding == null)
                    continue;

                string stageKey = SanitizeAssetToken(string.IsNullOrWhiteSpace(binding.stageId) ? $"Stage{i + 1}" : binding.stageId);
                if (binding.onStageStarted == null)
                    binding.onStageStarted = CreateSequenceAsset(directory, $"{quest.name}_{stageKey}_StartedSequence");
                if (binding.onStageCompleted == null)
                    binding.onStageCompleted = CreateSequenceAsset(directory, $"{quest.name}_{stageKey}_CompletedSequence");
            }

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        internal static NpcDialogueSequenceSO CreateSequenceAsset(string directory, string assetName)
        {
            if (string.IsNullOrWhiteSpace(directory))
                directory = "Assets";

            string path = AssetDatabase.GenerateUniqueAssetPath($"{directory}/{SanitizeAssetToken(assetName)}.asset");
            var sequence = CreateInstance<NpcDialogueSequenceSO>();
            sequence.sequenceId = SanitizeAssetToken(assetName);
            sequence.displayName = ObjectNames.NicifyVariableName(assetName);
            sequence.startNodeId = "line_01";
            sequence.nodes = new[]
            {
                new NpcDialogueSequenceNode
                {
                    nodeId = "line_01",
                    nodeType = NpcDialogueSequenceNodeType.Line,
                    text = string.Empty,
                    waitForPlayerContinue = true,
                    nextNodeId = "end",
                    importance = NpcDialogueImportance.Quest
                },
                new NpcDialogueSequenceNode
                {
                    nodeId = "end",
                    nodeType = NpcDialogueSequenceNodeType.End
                }
            };
            AssetDatabase.CreateAsset(sequence, path);
            EditorUtility.SetDirty(sequence);
            return sequence;
        }

        internal static string SanitizeAssetToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "DialogueSequence";

            foreach (char invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');

            return value.Replace(' ', '_');
        }

        private static string DisplayStage(QuestStageDefinition stage, int index)
        {
            if (stage == null)
                return $"Stage {index + 1}";

            return string.IsNullOrWhiteSpace(stage.id) ? $"Stage {index + 1}" : stage.id.Trim();
        }

        private static QuestObjectiveDefinition CloneObjective(QuestObjectiveDefinition source)
        {
            var clone = new QuestObjectiveDefinition
            {
                id = source.id,
                title = source.title,
                description = source.description,
                optional = source.optional,
                hiddenUntilAvailable = source.hiddenUntilAvailable,
                autoPinWhenAvailable = source.autoPinWhenAvailable,
                template = source.template,
                requiredCount = source.requiredCount,
                targetValue = source.targetValue,
                comparison = source.comparison,
                inputAction = source.inputAction,
                movementSkill = source.movementSkill,
                distanceStat = source.distanceStat,
                thresholdStat = source.thresholdStat,
                stateTarget = source.stateTarget,
                activityTarget = source.activityTarget,
                trickTarget = source.trickTarget,
                interactionTarget = source.interactionTarget,
                uiTarget = source.uiTarget,
                namedTrick = source.namedTrick,
                trickRequirement = CloneTrickRequirement(source.trickRequirement),
                compoundMode = source.compoundMode,
                advancedCondition = CloneCondition(source.advancedCondition),
                compoundObjectives = new List<QuestObjectiveDefinition>()
            };

            if (source.compoundObjectives != null)
            {
                for (int i = 0; i < source.compoundObjectives.Count; i++)
                {
                    var child = source.compoundObjectives[i];
                    if (child != null)
                        clone.compoundObjectives.Add(CloneObjective(child));
                }
            }

            return clone;
        }

        private static QuestObjectiveCondition CloneCondition(QuestObjectiveCondition source)
        {
            if (source == null)
                return new QuestObjectiveCondition();

            var clone = new QuestObjectiveCondition
            {
                kind = source.kind,
                key = source.key,
                comparison = source.comparison,
                targetCount = source.targetCount,
                targetValue = source.targetValue,
                stateValueKind = source.stateValueKind,
                boolValue = source.boolValue,
                stringValue = source.stringValue,
                filters = new List<QuestConditionFilter>(),
                trickRequirement = CloneTrickRequirement(source.trickRequirement),
                children = new List<QuestObjectiveCondition>()
            };

            if (source.filters != null)
            {
                for (int i = 0; i < source.filters.Count; i++)
                {
                    var filter = source.filters[i];
                    if (filter != null)
                        clone.filters.Add(new QuestConditionFilter { key = filter.key, value = filter.value });
                }
            }

            if (source.children != null)
            {
                for (int i = 0; i < source.children.Count; i++)
                    clone.children.Add(CloneCondition(source.children[i]));
            }

            return clone;
        }

        private static void Swap<T>(List<T> list, int a, int b)
        {
            T temp = list[a];
            list[a] = list[b];
            list[b] = temp;
        }

        private static TrickRequirementDefinition CloneTrickRequirement(TrickRequirementDefinition source)
        {
            source ??= new TrickRequirementDefinition();
            return new TrickRequirementDefinition
            {
                flavor = source.flavor,
                minimumSpinDegrees = source.minimumSpinDegrees,
                requiredSpinDirection = source.requiredSpinDirection,
                minimumFlipCount = source.minimumFlipCount,
                requiredFlipDirection = source.requiredFlipDirection,
                requiresGrind = source.requiresGrind,
                requiresSlide = source.requiresSlide,
                requiresValidAuthoredPose = source.requiresValidAuthoredPose,
                requiresPoseRotationCombo = source.requiresPoseRotationCombo,
                requiredPoseFamily = source.requiredPoseFamily,
                requiredPoseShape = source.requiredPoseShape,
                requiredOrientationModifier = source.requiredOrientationModifier,
                explicitPoseLabel = source.explicitPoseLabel,
                requiresSwitchLanding = source.requiresSwitchLanding,
                requiresNoseLanding = source.requiresNoseLanding,
                requiresTailLanding = source.requiresTailLanding,
                requiredZoneId = source.requiredZoneId
            };
        }

        private static SkiController.AerialOrientationModifier DrawOrientationRequirementField(string label, SkiController.AerialOrientationModifier value)
        {
            SkiController.AerialOrientationModifier[] values =
            {
                SkiController.AerialOrientationModifier.None,
                SkiController.AerialOrientationModifier.Switch,
                SkiController.AerialOrientationModifier.Inverted,
                SkiController.AerialOrientationModifier.OnSide,
                SkiController.AerialOrientationModifier.ChestDown,
                SkiController.AerialOrientationModifier.ChestUp,
                SkiController.AerialOrientationModifier.Sideways,
                SkiController.AerialOrientationModifier.Rising,
                SkiController.AerialOrientationModifier.Diving
            };
            string[] labels =
            {
                "None",
                "Switch",
                "Inverted",
                "On Side",
                "Chest Down",
                "Chest Up",
                "Travel Sideways (Legacy)",
                "Motion Rising",
                "Motion Diving"
            };

            int index = Array.IndexOf(values, value);
            if (index < 0)
                index = 0;

            index = EditorGUILayout.Popup(new GUIContent(label, "Required aerial orientation modifier for trick credit."), index, labels);
            return values[Mathf.Clamp(index, 0, values.Length - 1)];
        }

        private static void DrawTrickRequirementEditor(TrickRequirementDefinition requirement)
        {
            if (requirement == null)
                return;

            EditorGUILayout.LabelField(new GUIContent("Trick Rule", "Optional rule constraints evaluated against trick telemetry."), EditorStyles.boldLabel);
            requirement.flavor = (TrickRequirementFlavor)EditorGUILayout.EnumPopup(new GUIContent("Flavor", "Broad trick rule flavor used by trick validation."), requirement.flavor);
            requirement.minimumSpinDegrees = Mathf.Max(0, EditorGUILayout.IntField(new GUIContent("Min Spin Degrees", "Minimum spin rotation required."), requirement.minimumSpinDegrees));
            requirement.requiredSpinDirection = (TrickSpinRequirementDirection)EditorGUILayout.EnumPopup(new GUIContent("Spin Direction", "Required spin direction, if any."), requirement.requiredSpinDirection);
            requirement.minimumFlipCount = Mathf.Max(0, EditorGUILayout.IntField(new GUIContent("Min Flip Count", "Minimum number of flips required."), requirement.minimumFlipCount));
            requirement.requiredFlipDirection = (TrickFlipRequirementDirection)EditorGUILayout.EnumPopup(new GUIContent("Flip Direction", "Required flip direction, if any."), requirement.requiredFlipDirection);
            requirement.requiresGrind = EditorGUILayout.Toggle(new GUIContent("Requires Grind", "Requires the trick to include a grind."), requirement.requiresGrind);
            requirement.requiresSlide = EditorGUILayout.Toggle(new GUIContent("Requires Slide", "Requires the trick to include a slide."), requirement.requiresSlide);
            requirement.requiresValidAuthoredPose = EditorGUILayout.Toggle(new GUIContent("Requires Authored Pose", "Requires the trick pose to match authored pose data."), requirement.requiresValidAuthoredPose);
            requirement.requiresPoseRotationCombo = EditorGUILayout.Toggle(new GUIContent("Requires Pose + Rotation", "Requires both pose and rotation criteria to be satisfied."), requirement.requiresPoseRotationCombo);
            requirement.requiredPoseFamily = (SkiController.AerialPoseFamily)EditorGUILayout.EnumPopup(new GUIContent("Pose Family", "Required aerial pose family."), requirement.requiredPoseFamily);
            requirement.requiredPoseShape = (SkiController.AerialPoseShape)EditorGUILayout.EnumPopup(new GUIContent("Pose Shape", "Required aerial pose shape."), requirement.requiredPoseShape);
            requirement.requiredOrientationModifier = DrawOrientationRequirementField("Required Orientation", requirement.requiredOrientationModifier);
            requirement.explicitPoseLabel = EditorGUILayout.TextField(new GUIContent("Explicit Pose Label", "Exact authored pose label required for trick credit."), requirement.explicitPoseLabel);
            requirement.requiresSwitchLanding = EditorGUILayout.Toggle(new GUIContent("Requires Switch Landing", "Requires landing switch."), requirement.requiresSwitchLanding);
            requirement.requiresNoseLanding = EditorGUILayout.Toggle(new GUIContent("Requires Nose Landing", "Requires landing on the nose."), requirement.requiresNoseLanding);
            requirement.requiresTailLanding = EditorGUILayout.Toggle(new GUIContent("Requires Tail Landing", "Requires landing on the tail."), requirement.requiresTailLanding);
            requirement.requiredZoneId = EditorGUILayout.TextField(new GUIContent("Zone Restriction", "Optional zone ID that constrains where trick credit can be awarded."), requirement.requiredZoneId);
        }
    }
}
