using UnityEngine;

[DisallowMultipleComponent]
public sealed class NpcDialogueAgent : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NpcDialogueBankSO dialogueBank;
    [SerializeField] private NpcDialogueBubblePresenter bubblePresenter;
    [SerializeField] private Transform bubbleAnchor;
    [SerializeField] private Component speakerNameSource;

    [Header("Identity")]
    [SerializeField] private string speakerNameOverride;
    [SerializeField] private string fallbackSpeakerName = "Skier";
    [SerializeField] private bool useGameObjectNameAsFallback = true;

    [Header("Cooldowns")]
    [SerializeField, Min(0f)] private float ambientCooldownSeconds = 5f;
    [SerializeField, Min(0f)] private float interactionCooldownSeconds = 1.25f;

    [Header("Debug")]
    [SerializeField] private string debugLineId;
    [SerializeField] private string debugTopicId;

    public NpcDialogueBankSO DialogueBank => dialogueBank;
    public NpcDialogueBubblePresenter Presenter => ResolvePresenter();
    public float AmbientCooldownSeconds => ambientCooldownSeconds;
    public float InteractionCooldownSeconds => interactionCooldownSeconds;
    public bool IsShowingQuestOffer => bubblePresenter != null && bubblePresenter.IsShowingQuestOffer;

    private string _lastSpokenLineId;
    private string _lastSpokenTopic;

    private void Awake()
    {
        ResolvePresenter();
    }

    public void SetDialogueBank(NpcDialogueBankSO bank, bool onlyIfMissing = false)
    {
        if (onlyIfMissing && dialogueBank != null)
            return;

        dialogueBank = bank;
    }

    public void ConfigureSpeakerNameSource(Component source, bool onlyIfMissing = false)
    {
        if (onlyIfMissing && speakerNameSource != null)
            return;

        speakerNameSource = source;
    }

    public void SetFallbackSpeakerName(string value)
    {
        fallbackSpeakerName = value;
    }

    public bool WouldImmediatelyRepeat(NpcDialogueLine line, string topicId)
    {
        if (line == null || !line.avoidImmediateRepeat)
            return false;

        string lineKey = !string.IsNullOrWhiteSpace(line.id) ? line.id.Trim() : line.text?.Trim();
        if (string.IsNullOrWhiteSpace(lineKey))
            return false;

        return string.Equals(_lastSpokenLineId, lineKey, System.StringComparison.OrdinalIgnoreCase) &&
               string.Equals(_lastSpokenTopic, topicId ?? string.Empty, System.StringComparison.OrdinalIgnoreCase);
    }

    public void RememberSpokenLine(NpcDialogueLine line, string topicId)
    {
        if (line == null)
            return;

        _lastSpokenLineId = !string.IsNullOrWhiteSpace(line.id) ? line.id.Trim() : line.text?.Trim();
        _lastSpokenTopic = topicId ?? string.Empty;
    }

    public bool Say(string lineId)
    {
        return NpcDialogueDirector.Instance.TrySayById(this, lineId);
    }

    public bool Say(NpcDialogueLine line)
    {
        return NpcDialogueDirector.Instance.TrySay(this, line, default);
    }

    public bool SayFromTopic(string topicId)
    {
        return NpcDialogueDirector.Instance.TrySayFromTopic(this, topicId, DialogueContext.FromTopic(topicId));
    }

    public bool SayContextual(DialogueContext context)
    {
        string topicId = string.IsNullOrWhiteSpace(context.topicId) ? debugTopicId : context.topicId;
        return NpcDialogueDirector.Instance.TrySayFromTopic(this, topicId, context);
    }

    public bool InterruptAndSay(string lineId)
    {
        return NpcDialogueDirector.Instance.TrySayById(this, lineId, forceInterrupt: true);
    }

    public bool InterruptAndSay(NpcDialogueLine line)
    {
        return NpcDialogueDirector.Instance.TrySay(this, line, default, forceInterrupt: true);
    }

    public bool InterruptAndSayContextual(DialogueContext context)
    {
        string topicId = string.IsNullOrWhiteSpace(context.topicId) ? debugTopicId : context.topicId;
        return NpcDialogueDirector.Instance.TrySayFromTopic(this, topicId, context, forceInterrupt: true);
    }

    public string ResolveSpeakerName(DialogueContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.npcName))
            return context.npcName.Trim();

        if (speakerNameSource is NpcIdentity identity && identity.IsAuthored)
            return identity.DisplayName;

        if (!string.IsNullOrWhiteSpace(speakerNameOverride))
            return speakerNameOverride.Trim();

        if (speakerNameSource is INpcDialogueNameSource provider)
            return provider.DialogueDisplayName;

        if (TryGetComponent(out NpcSkierProfile skierProfile) && !string.IsNullOrWhiteSpace(skierProfile.SkierName))
            return skierProfile.SkierName.Trim();

        if (speakerNameSource != null)
        {
            var sourceType = speakerNameSource.GetType();
            var property = sourceType.GetProperty("DisplayName") ?? sourceType.GetProperty("NpcName") ?? sourceType.GetProperty("name");
            if (property != null && property.PropertyType == typeof(string))
            {
                var value = property.GetValue(speakerNameSource, null) as string;
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }
        }

        if (!string.IsNullOrWhiteSpace(fallbackSpeakerName))
            return fallbackSpeakerName.Trim();

        return useGameObjectNameAsFallback ? gameObject.name : "Skier";
    }

    [ContextMenu("Test Debug Line")]
    private void TestDebugLine()
    {
        if (!string.IsNullOrWhiteSpace(debugLineId))
            Say(debugLineId);
    }

    [ContextMenu("Test Debug Topic")]
    private void TestDebugTopic()
    {
        if (!string.IsNullOrWhiteSpace(debugTopicId))
            SayFromTopic(debugTopicId);
    }

    private NpcDialogueBubblePresenter ResolvePresenter()
    {
        if (bubblePresenter == null)
            bubblePresenter = GetComponent<NpcDialogueBubblePresenter>();

        if (bubblePresenter == null)
            bubblePresenter = gameObject.AddComponent<NpcDialogueBubblePresenter>();

        bubblePresenter.SetAnchor(bubbleAnchor != null ? bubbleAnchor : transform);
        return bubblePresenter;
    }
}

public interface INpcDialogueNameSource
{
    string DialogueDisplayName { get; }
}
