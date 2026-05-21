using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkiGame.Progression
{
    [Serializable]
    public sealed class StageDialogueBinding
    {
        public string stageId;
        public int stageIndex = -1;
        public NpcDialogueSequenceSO onStageStarted;
        public string stageReminderTopic;
        public NpcDialogueSequenceSO onStageCompleted;
    }

    [Serializable]
    public sealed class ObjectiveDialogueBinding
    {
        public string objectiveId;
        public NpcDialogueSequenceSO onObjectiveStarted;
        public NpcDialogueSequenceSO onObjectiveCompleted;
    }

    [CreateAssetMenu(fileName = "QuestDialogueProfile", menuName = "SkiGame/Quests/Quest Dialogue Profile")]
    public sealed class QuestDialogueProfileSO : ScriptableObject
    {
        public QuestDefinitionSO questDefinition;
        public string defaultSpeakerIdentityId;
        public NpcDialogueSequenceSO onAvailableSequence;
        public NpcDialogueSequenceSO onOfferedSequence;
        public NpcDialogueSequenceSO onAcceptedSequence;
        public string reminderTopic;
        public NpcDialogueSequenceSO onObjectivesCompleteSequence;
        public NpcDialogueSequenceSO onReadyToTurnInSequence;
        public NpcDialogueSequenceSO onTurnedInSequence;
        public NpcDialogueSequenceSO onCompletedSequence;
        public StageDialogueBinding[] stageBindings;
        public ObjectiveDialogueBinding[] objectiveBindings;
        public string availableAmbientTopic;
        public string inProgressAmbientTopic;
        public string readyToTurnInAmbientTopic;
        public string completedAmbientTopic;

        [ContextMenu("Validate Profile")]
        public void ValidateProfile()
        {
            foreach (string warning in CollectValidationWarnings())
                Debug.LogWarning($"[{name}] {warning}", this);
        }

        public List<string> CollectValidationWarnings()
        {
            var warnings = new List<string>();
            if (questDefinition == null)
                warnings.Add("Missing quest definition.");

            if (stageBindings != null && questDefinition != null)
            {
                for (int i = 0; i < stageBindings.Length; i++)
                {
                    var binding = stageBindings[i];
                    if (binding == null)
                        continue;

                    bool hasValidIndex = binding.stageIndex >= 0 && questDefinition.GetStage(binding.stageIndex) != null;
                    bool hasValidId = !string.IsNullOrWhiteSpace(binding.stageId) && FindStageById(binding.stageId) != null;
                    if (!hasValidIndex && !hasValidId)
                        warnings.Add($"Stage binding {i} does not reference a valid stage index/id.");
                }
            }

            WarnMissingSequence(onAcceptedSequence, "onAcceptedSequence", warnings);
            WarnMissingSequence(onReadyToTurnInSequence, "onReadyToTurnInSequence", warnings);
            WarnMissingSequence(onCompletedSequence, "onCompletedSequence", warnings);
            return warnings;
        }

        public StageDialogueBinding FindStageBinding(QuestRuntimeState state)
        {
            if (stageBindings == null || state == null)
                return null;

            var stage = questDefinition != null ? questDefinition.GetStage(state.currentStageIndex) : null;
            for (int i = 0; i < stageBindings.Length; i++)
            {
                var binding = stageBindings[i];
                if (binding == null)
                    continue;

                if (binding.stageIndex == state.currentStageIndex)
                    return binding;

                if (stage != null && !string.IsNullOrWhiteSpace(binding.stageId) &&
                    string.Equals(stage.id, binding.stageId, StringComparison.OrdinalIgnoreCase))
                    return binding;
            }

            return null;
        }

        private QuestStageDefinition FindStageById(string stageId)
        {
            if (questDefinition?.stages == null || string.IsNullOrWhiteSpace(stageId))
                return null;

            for (int i = 0; i < questDefinition.stages.Count; i++)
            {
                var stage = questDefinition.stages[i];
                if (stage != null && string.Equals(stage.id, stageId, StringComparison.OrdinalIgnoreCase))
                    return stage;
            }

            return null;
        }

        private static void WarnMissingSequence(NpcDialogueSequenceSO sequence, string fieldName, List<string> warnings)
        {
            if (sequence == null)
                return;

            if (sequence.nodes == null || sequence.nodes.Length == 0)
                warnings.Add($"{fieldName} references a sequence with no nodes.");
        }
    }
}
