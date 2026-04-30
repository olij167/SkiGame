using System;

[Serializable]
public struct DialogueContext
{
    public string topicId;
    public NpcDialogueTrigger trigger;
    public string npcName;
    public string playerName;
    public string passName;
    public string passExpiry;
    public string raceName;
    public NpcDialogueAudience audience;
    public bool preferImportantStyle;
    public int priorityOverride;
    public float durationOverride;
    public NpcDialogueImportance? importanceOverride;
    public NpcDialogueBubbleStyleSO styleOverride;

    public static DialogueContext FromTopic(string topicId, NpcDialogueTrigger trigger = NpcDialogueTrigger.Ambient)
    {
        return new DialogueContext
        {
            topicId = topicId,
            trigger = trigger
        };
    }
}
