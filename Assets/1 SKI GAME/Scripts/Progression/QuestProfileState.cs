using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkiGame.Progression
{
    [Serializable]
    public sealed class QuestLogState
    {
        public List<QuestRuntimeState> questStates = new List<QuestRuntimeState>();
        public List<string> completedQuestIds = new List<string>();
        public List<string> trackedQuestIds = new List<string>();
        public string pinnedQuestId;
        public string pinnedObjectiveId;
    }

    [Serializable]
    public sealed class QuestRuntimeState
    {
        public string questId;
        public bool accepted;
        public bool completed;
        public int currentStageIndex;
        public string pinnedObjectiveId;
        public List<QuestObjectiveRuntimeState> objectiveStates = new List<QuestObjectiveRuntimeState>();
    }

    [Serializable]
    public sealed class QuestObjectiveRuntimeState
    {
        public string objectiveId;
        public bool available = true;
        public bool completed;
        public float progress01;
        public float progressValue;
        public string progressText;
        public List<QuestConditionRuntimeState> conditionStates = new List<QuestConditionRuntimeState>();
    }

    [Serializable]
    public sealed class QuestConditionRuntimeState
    {
        public string path;
        public bool initialized;
        public bool completed;
        public float accumulatedValue;
        public float lastSeenStatValue;
        public int lastSeenEventSequence;
        public int sequenceChildIndex;
    }
}
