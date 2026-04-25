using System.Collections.Generic;
using SkiGame.Progression;
using SkiGame.Tricks;
using UnityEditor;
using UnityEngine;

namespace SkiGame.EditorTools
{
    [CustomEditor(typeof(QuestDefinitionSO))]
    public sealed class QuestDefinitionSOEditor : Editor
    {
        private readonly Dictionary<string, bool> _foldouts = new Dictionary<string, bool>();

        public override void OnInspectorGUI()
        {
            var quest = (QuestDefinitionSO)target;
            if (quest == null)
                return;

            serializedObject.Update();
            Undo.RecordObject(quest, "Edit Quest Definition");

            EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
            quest.id = EditorGUILayout.TextField("Id", quest.id);
            quest.title = EditorGUILayout.TextField("Title", quest.title);
            quest.description = EditorGUILayout.TextArea(quest.description ?? string.Empty, GUILayout.MinHeight(48f));

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Flow", EditorStyles.boldLabel);
            quest.autoAccept = EditorGUILayout.Toggle("Auto Accept", quest.autoAccept);
            quest.tutorialQuest = EditorGUILayout.Toggle("Tutorial Quest", quest.tutorialQuest);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Stages", EditorStyles.boldLabel);

            quest.stages ??= new List<QuestStageDefinition>();

            for (int i = 0; i < quest.stages.Count; i++)
            {
                var stage = quest.stages[i] ?? new QuestStageDefinition();
                quest.stages[i] = stage;

                string foldoutKey = $"stage:{i}:{stage.id}";
                _foldouts.TryGetValue(foldoutKey, out bool expanded);
                expanded = EditorGUILayout.BeginFoldoutHeaderGroup(expanded, string.IsNullOrWhiteSpace(stage.title) ? $"Stage {i + 1}" : stage.title);
                _foldouts[foldoutKey] = expanded;

                if (expanded)
                {
                    EditorGUILayout.BeginVertical("box");
                    stage.id = EditorGUILayout.TextField("Stage Id", stage.id);
                    stage.title = EditorGUILayout.TextField("Stage Title", stage.title);
                    EditorGUILayout.LabelField("Stage Description");
                    stage.description = EditorGUILayout.TextArea(stage.description ?? string.Empty, GUILayout.MinHeight(36f));

                    DrawStageToolbar(quest, i);

                    stage.objectives ??= new List<QuestObjectiveDefinition>();
                    EditorGUILayout.Space(4f);
                    EditorGUILayout.LabelField("Objectives", EditorStyles.boldLabel);

                    for (int objectiveIndex = 0; objectiveIndex < stage.objectives.Count; objectiveIndex++)
                    {
                        stage.objectives[objectiveIndex] ??= new QuestObjectiveDefinition();
                        DrawObjective(stage.objectives, objectiveIndex, 0);
                    }

                    if (GUILayout.Button("Add Objective"))
                        stage.objectives.Add(new QuestObjectiveDefinition());

                    EditorGUILayout.EndVertical();
                }

                EditorGUILayout.EndFoldoutHeaderGroup();
                EditorGUILayout.Space(4f);
            }

            if (GUILayout.Button("Add Stage"))
                quest.stages.Add(new QuestStageDefinition());

            if (GUI.changed)
            {
                EditorUtility.SetDirty(quest);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawStageToolbar(QuestDefinitionSO quest, int stageIndex)
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Move Up") && stageIndex > 0)
            {
                Swap(quest.stages, stageIndex, stageIndex - 1);
                GUIUtility.ExitGUI();
            }

            if (GUILayout.Button("Move Down") && stageIndex < quest.stages.Count - 1)
            {
                Swap(quest.stages, stageIndex, stageIndex + 1);
                GUIUtility.ExitGUI();
            }

            GUI.backgroundColor = new Color(1f, 0.78f, 0.78f);
            if (GUILayout.Button("Remove Stage"))
            {
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
            string label = string.IsNullOrWhiteSpace(objective.title) ? $"Objective {objectiveIndex + 1}" : objective.title;
            label = $"{label}  [{objective.template}]";
            string foldoutKey = $"objective:{depth}:{objectiveIndex}:{objective.id}:{objective.template}";
            _foldouts.TryGetValue(foldoutKey, out bool expanded);
            expanded = EditorGUILayout.Foldout(expanded, label, true);
            _foldouts[foldoutKey] = expanded;

            if (expanded)
            {
                EditorGUILayout.BeginVertical("box");

                objective.id = EditorGUILayout.TextField("Objective Id", objective.id);
                objective.title = EditorGUILayout.TextField("Title", objective.title);
                EditorGUILayout.LabelField("Description");
                objective.description = EditorGUILayout.TextArea(objective.description ?? string.Empty, GUILayout.MinHeight(32f));

                objective.template = (QuestObjectiveTemplate)EditorGUILayout.EnumPopup("Template", objective.template);
                objective.optional = EditorGUILayout.Toggle("Optional", objective.optional);
                objective.hiddenUntilAvailable = EditorGUILayout.Toggle("Hidden Until Available", objective.hiddenUntilAvailable);
                objective.autoPinWhenAvailable = EditorGUILayout.Toggle("Auto Track Hint", objective.autoPinWhenAvailable);

                EditorGUILayout.Space(3f);
                DrawTemplateFields(objective, depth + 1);

                EditorGUILayout.HelpBox(objective.BuildAuthoringSummary(), MessageType.None);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Duplicate"))
                {
                    objectives.Insert(objectiveIndex + 1, CloneObjective(objective));
                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button("Move Up") && objectiveIndex > 0)
                {
                    Swap(objectives, objectiveIndex, objectiveIndex - 1);
                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button("Move Down") && objectiveIndex < objectives.Count - 1)
                {
                    Swap(objectives, objectiveIndex, objectiveIndex + 1);
                    GUIUtility.ExitGUI();
                }

                GUI.backgroundColor = new Color(1f, 0.78f, 0.78f);
                if (GUILayout.Button("Remove"))
                {
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
                    objective.inputAction = (QuestInputActionType)EditorGUILayout.EnumPopup("Input", objective.inputAction);
                    objective.requiredCount = EditorGUILayout.IntField("Count", Mathf.Max(1, objective.requiredCount));
                    break;

                case QuestObjectiveTemplate.PerformMovementSkill:
                    objective.movementSkill = (QuestMovementSkillType)EditorGUILayout.EnumPopup("Movement Skill", objective.movementSkill);
                    objective.requiredCount = EditorGUILayout.IntField("Count", Mathf.Max(1, objective.requiredCount));
                    break;

                case QuestObjectiveTemplate.TravelDistance:
                    objective.distanceStat = (QuestDistanceStatType)EditorGUILayout.EnumPopup("Metric", objective.distanceStat);
                    objective.comparison = (QuestComparisonOp)EditorGUILayout.EnumPopup("Comparison", objective.comparison);
                    objective.targetValue = EditorGUILayout.FloatField("Target", objective.targetValue);
                    break;

                case QuestObjectiveTemplate.ReachThreshold:
                    objective.thresholdStat = (QuestThresholdStatType)EditorGUILayout.EnumPopup("Metric", objective.thresholdStat);
                    objective.comparison = (QuestComparisonOp)EditorGUILayout.EnumPopup("Comparison", objective.comparison);
                    objective.targetValue = EditorGUILayout.FloatField("Target", objective.targetValue);
                    break;

                case QuestObjectiveTemplate.ReachState:
                    objective.stateTarget = (QuestStateTargetType)EditorGUILayout.EnumPopup("State", objective.stateTarget);
                    break;

                case QuestObjectiveTemplate.Activity:
                    objective.activityTarget = (QuestActivityTargetType)EditorGUILayout.EnumPopup("Activity", objective.activityTarget);
                    objective.requiredCount = EditorGUILayout.IntField("Count", Mathf.Max(1, objective.requiredCount));
                    break;

                case QuestObjectiveTemplate.Trick:
                    objective.trickTarget = (QuestTrickTargetType)EditorGUILayout.EnumPopup("Trick Target", objective.trickTarget);
                    if (objective.trickTarget == QuestTrickTargetType.NamedLanding)
                        objective.namedTrick = EditorGUILayout.TextField("Named Trick", objective.namedTrick);
                    if (objective.trickTarget == QuestTrickTargetType.RuleLanding || objective.trickRequirement != null)
                    {
                        EditorGUILayout.Space(3f);
                        DrawTrickRequirementEditor(objective.trickRequirement ??= new TrickRequirementDefinition());
                    }
                    objective.requiredCount = EditorGUILayout.IntField("Count", Mathf.Max(1, objective.requiredCount));
                    break;

                case QuestObjectiveTemplate.Interaction:
                    objective.interactionTarget = (QuestInteractionTargetType)EditorGUILayout.EnumPopup("Interaction", objective.interactionTarget);
                    objective.requiredCount = EditorGUILayout.IntField("Count", Mathf.Max(1, objective.requiredCount));
                    break;

                case QuestObjectiveTemplate.OpenUiScreen:
                    objective.uiTarget = (QuestUiScreenTargetType)EditorGUILayout.EnumPopup("Screen", objective.uiTarget);
                    objective.requiredCount = EditorGUILayout.IntField("Count", Mathf.Max(1, objective.requiredCount));
                    break;

                case QuestObjectiveTemplate.Compound:
                    objective.compoundMode = (QuestConditionKind)EditorGUILayout.EnumPopup("Group Mode", objective.compoundMode);
                    objective.compoundObjectives ??= new List<QuestObjectiveDefinition>();

                    EditorGUILayout.LabelField("Sub Objectives", EditorStyles.boldLabel);
                    for (int i = 0; i < objective.compoundObjectives.Count; i++)
                    {
                        objective.compoundObjectives[i] ??= new QuestObjectiveDefinition();
                        DrawObjective(objective.compoundObjectives, i, depth + 1);
                    }

                    if (GUILayout.Button("Add Sub Objective"))
                        objective.compoundObjectives.Add(new QuestObjectiveDefinition());
                    break;

                case QuestObjectiveTemplate.Advanced:
                    DrawConditionEditor(objective.advancedCondition ??= new QuestObjectiveCondition(), depth + 1);
                    break;
            }
        }

        private void DrawConditionEditor(QuestObjectiveCondition condition, int depth)
        {
            condition.kind = (QuestConditionKind)EditorGUILayout.EnumPopup("Condition Kind", condition.kind);
            condition.key = EditorGUILayout.TextField("Key", condition.key);
            condition.comparison = (QuestComparisonOp)EditorGUILayout.EnumPopup("Comparison", condition.comparison);
            condition.targetCount = EditorGUILayout.IntField("Target Count", condition.targetCount);
            condition.targetValue = EditorGUILayout.FloatField("Target Value", condition.targetValue);
            condition.stateValueKind = (QuestValueKind)EditorGUILayout.EnumPopup("State Value Kind", condition.stateValueKind);

            if (condition.stateValueKind == QuestValueKind.Bool)
                condition.boolValue = EditorGUILayout.Toggle("Bool Value", condition.boolValue);
            else
                condition.stringValue = EditorGUILayout.TextField("String Value", condition.stringValue);

            condition.filters ??= new List<QuestConditionFilter>();
            EditorGUILayout.LabelField("Filters", EditorStyles.boldLabel);
            for (int i = 0; i < condition.filters.Count; i++)
            {
                var filter = condition.filters[i] ?? new QuestConditionFilter();
                condition.filters[i] = filter;

                EditorGUILayout.BeginHorizontal();
                filter.key = EditorGUILayout.TextField(filter.key);
                filter.value = EditorGUILayout.TextField(filter.value);
                if (GUILayout.Button("X", GUILayout.Width(24f)))
                {
                    condition.filters.RemoveAt(i);
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Add Filter"))
                condition.filters.Add(new QuestConditionFilter());

            if (condition.kind == QuestConditionKind.TrickRuleEventCount)
            {
                EditorGUILayout.Space(4f);
                DrawTrickRequirementEditor(condition.trickRequirement ??= new TrickRequirementDefinition());
            }

            condition.children ??= new List<QuestObjectiveCondition>();
            EditorGUILayout.LabelField("Child Conditions", EditorStyles.boldLabel);
            for (int i = 0; i < condition.children.Count; i++)
            {
                condition.children[i] ??= new QuestObjectiveCondition();
                EditorGUILayout.BeginVertical("box");
                DrawConditionEditor(condition.children[i], depth + 1);
                GUI.backgroundColor = new Color(1f, 0.78f, 0.78f);
                if (GUILayout.Button("Remove Child Condition"))
                {
                    condition.children.RemoveAt(i);
                    GUI.backgroundColor = Color.white;
                    GUIUtility.ExitGUI();
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("Add Child Condition"))
                condition.children.Add(new QuestObjectiveCondition());
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

            int index = System.Array.IndexOf(values, value);
            if (index < 0)
                index = 0;

            index = EditorGUILayout.Popup(label, index, labels);
            return values[Mathf.Clamp(index, 0, values.Length - 1)];
        }

        private static void DrawTrickRequirementEditor(TrickRequirementDefinition requirement)
        {
            if (requirement == null)
                return;

            EditorGUILayout.LabelField("Trick Rule", EditorStyles.boldLabel);
            requirement.flavor = (TrickRequirementFlavor)EditorGUILayout.EnumPopup("Flavor", requirement.flavor);
            requirement.minimumSpinDegrees = Mathf.Max(0, EditorGUILayout.IntField("Min Spin Degrees", requirement.minimumSpinDegrees));
            requirement.requiredSpinDirection = (TrickSpinRequirementDirection)EditorGUILayout.EnumPopup("Spin Direction", requirement.requiredSpinDirection);
            requirement.minimumFlipCount = Mathf.Max(0, EditorGUILayout.IntField("Min Flip Count", requirement.minimumFlipCount));
            requirement.requiredFlipDirection = (TrickFlipRequirementDirection)EditorGUILayout.EnumPopup("Flip Direction", requirement.requiredFlipDirection);
            requirement.requiresGrind = EditorGUILayout.Toggle("Requires Grind", requirement.requiresGrind);
            requirement.requiresSlide = EditorGUILayout.Toggle("Requires Slide", requirement.requiresSlide);
            requirement.requiresValidAuthoredPose = EditorGUILayout.Toggle("Requires Authored Pose", requirement.requiresValidAuthoredPose);
            requirement.requiresPoseRotationCombo = EditorGUILayout.Toggle("Requires Pose + Rotation", requirement.requiresPoseRotationCombo);
            requirement.requiredPoseFamily = (SkiController.AerialPoseFamily)EditorGUILayout.EnumPopup("Pose Family", requirement.requiredPoseFamily);
            requirement.requiredPoseShape = (SkiController.AerialPoseShape)EditorGUILayout.EnumPopup("Pose Shape", requirement.requiredPoseShape);
            requirement.requiredOrientationModifier = DrawOrientationRequirementField("Required Orientation", requirement.requiredOrientationModifier);
            requirement.explicitPoseLabel = EditorGUILayout.TextField("Explicit Pose Label", requirement.explicitPoseLabel);
            requirement.requiresSwitchLanding = EditorGUILayout.Toggle("Requires Switch Landing", requirement.requiresSwitchLanding);
            requirement.requiresNoseLanding = EditorGUILayout.Toggle("Requires Nose Landing", requirement.requiresNoseLanding);
            requirement.requiresTailLanding = EditorGUILayout.Toggle("Requires Tail Landing", requirement.requiresTailLanding);
            requirement.requiredZoneId = EditorGUILayout.TextField("Zone Restriction", requirement.requiredZoneId);
        }
    }
}
