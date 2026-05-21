using System;
using SkiGame.Progression;

[Serializable]
public struct DialogueContext
{
    public string topicId;
    public NpcDialogueTrigger trigger;
    public string npcName;
    public string playerName;
    public string passName;
    public string passExpiry;
    public float passExpirySeconds;
    public string liftId;
    public string liftName;
    public string stationName;
    public string regionName;
    public string requiredPassId;
    public string requiredPassName;
    public int requiredPassTier;
    public string currentPassId;
    public string currentPassName;
    public int currentPassTier;
    public string kioskHint;
    public string upgradeHint;
    public string raceName;
    public string anchorName;
    public string anchorType;
    public string nearbyRunName;
    public string nearbyRunDifficulty;
    public string nearbyLiftName;
    public string nearbyPoiName;
    public string weather;
    public string timeOfDay;
    public string kioskName;
    public string speakerName;
    public string listenerName;
    public string questId;
    public string questTitle;
    public string questStage;
    public string objectiveName;
    public NpcDialogueAudience audience;
    public bool preferImportantStyle;
    public int priorityOverride;
    public float durationOverride;
    public NpcDialogueImportance? importanceOverride;
    public NpcDialogueBubbleStyleSO styleOverride;
    public QuestDefinitionSO questDefinition;
    public QuestRuntimeState questState;
    public DialogueContextValueSet values;

    public static DialogueContext FromTopic(string topicId, NpcDialogueTrigger trigger = NpcDialogueTrigger.Ambient)
    {
        return new DialogueContext
        {
            topicId = topicId,
            trigger = trigger
        };
    }
}
