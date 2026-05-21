using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkiGame.Progression
{
    [DisallowMultipleComponent]
    public sealed class QuestDialogueBridge : MonoBehaviour
    {
        [SerializeField] private QuestDirector questDirector;
        [SerializeField] private NpcDialogueAgent dialogueAgent;
        [SerializeField] private NpcDialogueSequencePlayer sequencePlayer;
        [SerializeField] private List<QuestDialogueProfileSO> profiles = new();

        private void Awake()
        {
            if (dialogueAgent == null)
                dialogueAgent = GetComponent<NpcDialogueAgent>();
            if (sequencePlayer == null)
                sequencePlayer = GetComponent<NpcDialogueSequencePlayer>();
        }

        private void OnEnable()
        {
            questDirector = questDirector != null ? questDirector : QuestDirector.Instance;
            if (questDirector == null)
                return;

            questDirector.OnQuestAccepted += HandleQuestAccepted;
            questDirector.OnQuestStageAdvanced += HandleQuestStageAdvanced;
            questDirector.OnQuestReadyToTurnIn += HandleQuestReadyToTurnIn;
            questDirector.OnQuestCompleted += HandleQuestCompleted;
        }

        private void OnDisable()
        {
            if (questDirector == null)
                return;

            questDirector.OnQuestAccepted -= HandleQuestAccepted;
            questDirector.OnQuestStageAdvanced -= HandleQuestStageAdvanced;
            questDirector.OnQuestReadyToTurnIn -= HandleQuestReadyToTurnIn;
            questDirector.OnQuestCompleted -= HandleQuestCompleted;
        }

        private void HandleQuestAccepted(QuestDefinitionSO quest, QuestRuntimeState state)
        {
            var profile = FindProfile(quest);
            if (profile == null)
                return;

            Play(profile.onAcceptedSequence, profile, quest, state);
        }

        private void HandleQuestStageAdvanced(QuestDefinitionSO quest, QuestRuntimeState state)
        {
            var profile = FindProfile(quest);
            var binding = profile != null ? profile.FindStageBinding(state) : null;
            if (binding != null && Play(binding.onStageStarted, profile, quest, state))
                return;

            if (binding != null && !string.IsNullOrWhiteSpace(binding.stageReminderTopic))
                SayTopic(binding.stageReminderTopic, profile, quest, state);
        }

        private void HandleQuestReadyToTurnIn(QuestDefinitionSO quest, QuestRuntimeState state)
        {
            var profile = FindProfile(quest);
            if (profile == null)
                return;

            if (!Play(profile.onReadyToTurnInSequence, profile, quest, state))
                SayTopic(profile.readyToTurnInAmbientTopic, profile, quest, state);
        }

        private void HandleQuestCompleted(QuestDefinitionSO quest, QuestRuntimeState state)
        {
            var profile = FindProfile(quest);
            if (profile == null)
                return;

            if (!Play(profile.onCompletedSequence, profile, quest, state))
                SayTopic(profile.completedAmbientTopic, profile, quest, state);
        }

        private bool Play(NpcDialogueSequenceSO sequence, QuestDialogueProfileSO profile, QuestDefinitionSO quest, QuestRuntimeState state)
        {
            if (sequence == null || sequencePlayer == null)
                return false;

            return sequencePlayer.Play(sequence, BuildContext(profile, quest, state), dialogueAgent);
        }

        private void SayTopic(string topic, QuestDialogueProfileSO profile, QuestDefinitionSO quest, QuestRuntimeState state)
        {
            if (dialogueAgent == null || string.IsNullOrWhiteSpace(topic))
                return;

            var context = BuildContext(profile, quest, state);
            context.topicId = topic;
            context.trigger = NpcDialogueTrigger.QuestReminder;
            context.importanceOverride = NpcDialogueImportance.Quest;
            dialogueAgent.InterruptAndSayContextual(context);
        }

        private QuestDialogueProfileSO FindProfile(QuestDefinitionSO quest)
        {
            if (quest == null || profiles == null)
                return null;

            for (int i = 0; i < profiles.Count; i++)
            {
                var profile = profiles[i];
                if (profile != null && profile.questDefinition == quest)
                    return profile;
            }

            return null;
        }

        private DialogueContext BuildContext(QuestDialogueProfileSO profile, QuestDefinitionSO quest, QuestRuntimeState state)
        {
            var stage = quest != null && state != null ? quest.GetStage(state.currentStageIndex) : null;
            return new DialogueContext
            {
                npcName = dialogueAgent != null ? dialogueAgent.ResolveSpeakerName(default) : string.Empty,
                audience = NpcDialogueAudience.Player,
                questId = quest != null ? quest.SafeId : string.Empty,
                questTitle = quest != null ? quest.title : string.Empty,
                questStage = stage != null ? string.IsNullOrWhiteSpace(stage.title) ? stage.id : stage.title : string.Empty,
                questDefinition = quest,
                questState = state,
                importanceOverride = NpcDialogueImportance.Quest,
                preferImportantStyle = true
            };
        }
    }
}
