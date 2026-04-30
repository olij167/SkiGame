using UnityEngine;

[DisallowMultipleComponent]
public sealed class NpcAmbientDialogueEmitter : MonoBehaviour
{
    [SerializeField] private NpcDialogueAgent dialogueAgent;
    [SerializeField] private NpcTalkInteractable talkInteractable;
    [SerializeField] private bool playOnPlayerNearby = true;
    [SerializeField] private bool playAmbientAutomatically = true;
    [SerializeField] private float activationRadius = 18f;
    [SerializeField] private float minDelay = 7f;
    [SerializeField] private float maxDelay = 15f;
    [SerializeField] private string[] ambientTopics = { "ambient" };
    [SerializeField] private NpcDialogueAudience audience = NpcDialogueAudience.Player;
    [SerializeField] private bool suppressDuringQuestOffer = true;
    [SerializeField] private bool suppressDuringInteraction = true;
    [SerializeField] private bool requirePlayerInTrigger = false;

    private Transform _player;
    private float _nextSpeakTime;

    private void Awake()
    {
        if (dialogueAgent == null)
            dialogueAgent = GetComponent<NpcDialogueAgent>();
        if (talkInteractable == null)
            talkInteractable = GetComponent<NpcTalkInteractable>();

        ScheduleNext();
    }

    private void Update()
    {
        if (!playAmbientAutomatically || dialogueAgent == null || Time.unscaledTime < _nextSpeakTime)
            return;

        if (!TryResolvePlayer(out var player))
            return;

        if (requirePlayerInTrigger && (talkInteractable == null || !talkInteractable.IsPromptAvailable))
            return;

        if (Vector3.Distance(transform.position, player.position) > activationRadius)
            return;

        if (suppressDuringQuestOffer && dialogueAgent.IsShowingQuestOffer)
            return;

        if (suppressDuringInteraction && talkInteractable != null && talkInteractable.IsPromptAvailable && !playOnPlayerNearby)
            return;

        if (!NpcDialogueDirector.Instance.CanShowAmbientBubble())
            return;

        var context = new DialogueContext
        {
            trigger = playOnPlayerNearby ? NpcDialogueTrigger.PlayerNearby : NpcDialogueTrigger.Ambient,
            audience = audience
        };

        string topic = ambientTopics != null && ambientTopics.Length > 0
            ? ambientTopics[Random.Range(0, ambientTopics.Length)]
            : string.Empty;
        context.topicId = topic;
        dialogueAgent.SayContextual(context);
        ScheduleNext();
    }

    private void ScheduleNext()
    {
        _nextSpeakTime = Time.unscaledTime + Random.Range(Mathf.Max(0.1f, minDelay), Mathf.Max(minDelay + 0.1f, maxDelay));
    }

    private bool TryResolvePlayer(out Transform player)
    {
        if (_player != null)
        {
            player = _player;
            return true;
        }

        var skiController = FindObjectOfType<SkiController>();
        if (skiController != null)
        {
            _player = skiController.transform;
            player = _player;
            return true;
        }

        player = null;
        return false;
    }
}
