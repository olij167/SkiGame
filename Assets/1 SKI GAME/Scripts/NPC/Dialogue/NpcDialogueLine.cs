using System;
using UnityEngine;

public enum NpcDialogueTrigger
{
    Ambient,
    PlayerNearby,
    InteractionPrompt,
    InteractionStarted,
    QuestAvailable,
    QuestAccepted,
    QuestCompleted,
    QuestReminder,
    LiftAccessAllowed,
    LiftAccessDenied,
    PassExpired,
    RaceHint,
    TutorialHint,
    SocialReply
}

public enum NpcDialogueImportance
{
    Ambient,
    Interaction,
    Quest,
    Tutorial,
    Critical
}

public enum NpcDialogueAudience
{
    Player,
    NearbyNpc,
    Group,
    Any
}

[Serializable]
public sealed class NpcDialogueLine
{
    public string id;
    public string topic;
    public NpcDialogueTrigger trigger = NpcDialogueTrigger.Ambient;
    public NpcDialogueAudience audience = NpcDialogueAudience.Any;
    public int priority;
    [Min(1)] public int weight = 1;
    [TextArea(2, 4)] public string text;
    [Min(0.25f)] public float duration = 2.75f;
    [Min(0f)] public float cooldown;
    public bool avoidImmediateRepeat = true;
    public bool showSpeakerName;
    public bool preferImportantStyle;
    public NpcDialogueImportance importance = NpcDialogueImportance.Ambient;
    public NpcDialogueBubbleStyleSO styleOverride;
    public string responseGroupId;
    public int sequenceIndex;
    public string speakerRole;
    public string socialTopicId;
    public DialogueRequirement[] requirements;

    public bool IsAmbientLike =>
        trigger == NpcDialogueTrigger.Ambient ||
        trigger == NpcDialogueTrigger.PlayerNearby ||
        trigger == NpcDialogueTrigger.SocialReply;

    public NpcDialogueLine Clone()
    {
        return new NpcDialogueLine
        {
            id = id,
            topic = topic,
            trigger = trigger,
            audience = audience,
            priority = priority,
            weight = weight,
            text = text,
            duration = duration,
            cooldown = cooldown,
            avoidImmediateRepeat = avoidImmediateRepeat,
            showSpeakerName = showSpeakerName,
            preferImportantStyle = preferImportantStyle,
            importance = importance,
            styleOverride = styleOverride,
            responseGroupId = responseGroupId,
            sequenceIndex = sequenceIndex,
            speakerRole = speakerRole,
            socialTopicId = socialTopicId,
            requirements = requirements
        };
    }
}
