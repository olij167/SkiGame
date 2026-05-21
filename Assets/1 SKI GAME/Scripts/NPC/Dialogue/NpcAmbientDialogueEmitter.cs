using UnityEngine;

[DisallowMultipleComponent]
public sealed class NpcAmbientDialogueEmitter : MonoBehaviour
{
    [SerializeField] private NpcDialogueAgent dialogueAgent;
    [SerializeField] private NpcTalkInteractable talkInteractable;
    [SerializeField] private NpcQuestGiver questGiver;
    [SerializeField] private NpcSocialActor socialActor;
    [SerializeField] private LiftAttendantSpeaker liftAttendantContext;
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
    [SerializeField] private bool fallbackToGenericAmbientWhenQuestAmbientFails = true;
    [SerializeField] private bool logAmbientDebug = false;
    [SerializeField] private bool includeInactivePlayersInResolution = false;
    [SerializeField, Min(0f)] private float maxResolvedPlayerDistanceFromCamera = 5000f;

    private Transform _player;
    private float _nextSpeakTime;

    private void Awake()
    {
        if (dialogueAgent == null)
            dialogueAgent = GetComponent<NpcDialogueAgent>();
        if (talkInteractable == null)
            talkInteractable = GetComponent<NpcTalkInteractable>();
        if (questGiver == null)
            questGiver = GetComponent<NpcQuestGiver>();
        if (socialActor == null)
            socialActor = GetComponent<NpcSocialActor>();
        if (liftAttendantContext == null)
            liftAttendantContext = GetComponent<LiftAttendantSpeaker>();

        ScheduleNext();
    }

    private void Update()
    {
        if (!playAmbientAutomatically)
            return;
        if (dialogueAgent == null)
        {
            LogAmbient("Suppressed: no dialogue agent.");
            return;
        }
        if (Time.unscaledTime < _nextSpeakTime)
            return;

        if (!TryResolvePlayer(out var player))
        {
            LogAmbient("Suppressed: player not found.");
            return;
        }

        if (requirePlayerInTrigger && (talkInteractable == null || !talkInteractable.IsPromptAvailable))
        {
            LogAmbient("Suppressed: require player in trigger is enabled and prompt is unavailable.");
            return;
        }

        if (Vector3.Distance(transform.position, player.position) > activationRadius)
        {
            LogAmbient("Suppressed: player outside activation radius.");
            return;
        }

        bool persistentDialogueVisible = dialogueAgent.Presenter != null && dialogueAgent.Presenter.IsShowingPersistentContent;
        if (suppressDuringQuestOffer && (persistentDialogueVisible || dialogueAgent.IsShowingQuestOffer || (questGiver != null && questGiver.HasActiveOfferSession)))
        {
            LogAmbient("Suppressed: persistent dialogue or quest offer session is active.");
            return;
        }

        if (suppressDuringInteraction && talkInteractable != null && (talkInteractable.IsActivelyInteracting || (!playOnPlayerNearby && talkInteractable.IsPromptAvailable)))
        {
            LogAmbient("Suppressed: NPC is actively interacting.");
            return;
        }

        if (socialActor != null && socialActor.IsInSocialExchange)
        {
            LogAmbient("Suppressed: NPC is participating in a social exchange.");
            return;
        }

        if (!NpcDialogueDirector.Instance.CanShowAmbientBubble())
        {
            LogAmbient($"Suppressed: dialogue budget full. {NpcDialogueDirector.Instance.GetAmbientBudgetDebugString()}");
            return;
        }

        if (TryEmitQuestAwareAmbient(out bool questRequestFound))
        {
            ScheduleNext();
            return;
        }

        if (questRequestFound && !fallbackToGenericAmbientWhenQuestAmbientFails)
        {
            LogAmbient("Quest-aware ambient request failed and generic fallback is disabled.");
            ScheduleNext();
            return;
        }

        if (TryEmitGenericAmbient())
            ScheduleNext();
        else
            ScheduleRetry();
    }

    private bool TryEmitGenericAmbient()
    {
        if (dialogueAgent == null)
        {
            LogAmbient("Cannot emit generic ambient: no dialogue agent.");
            return false;
        }

        string topic = ambientTopics != null && ambientTopics.Length > 0
            ? ambientTopics[Random.Range(0, ambientTopics.Length)]
            : string.Empty;

        NpcDialogueTrigger trigger = playOnPlayerNearby
            ? NpcDialogueTrigger.PlayerNearby
            : NpcDialogueTrigger.Ambient;

        DialogueContext context;

        if (liftAttendantContext != null &&
            liftAttendantContext.TryBuildAmbientDialogueContext(topic, trigger, audience, out var attendantContext))
        {
            context = attendantContext;
            LogAmbient(
                $"Generic attendant ambient topic='{topic}' lift='{context.liftName}' requiredPass='{context.requiredPassName}' region='{context.regionName}'.");
        }
        else
        {
            context = new DialogueContext
            {
                trigger = trigger,
                audience = audience,
                topicId = topic
            };
        }

        bool success = dialogueAgent.SayContextual(context);
        LogAmbient($"Generic ambient topic='{topic}' trigger={context.trigger} success={success}.");
        return success;
    }

    private bool TryEmitQuestAwareAmbient(out bool requestFound)
    {
        requestFound = false;
        if (questGiver == null)
            return false;

        if (!questGiver.TryGetQuestAmbientDialogueRequest(out var context, out var bankOverride, out float cooldown))
            return false;

        requestFound = true;
        var originalBank = dialogueAgent.DialogueBank;
        bool success = false;
        try
        {
            if (bankOverride != null)
                dialogueAgent.SetDialogueBank(bankOverride);

            success = dialogueAgent.SayContextual(context);
        }
        finally
        {
            if (bankOverride != null)
                dialogueAgent.SetDialogueBank(originalBank);
        }

        LogAmbient($"Quest ambient topic='{context.topicId}' trigger={context.trigger} bankOverride={(bankOverride != null)} success={success}.");
        questGiver.NotifyQuestAmbientDialogueResult(success, cooldown);
        return success;
    }

    [ContextMenu("Test Emit Generic Ambient")]
    private void TestEmitGenericAmbient()
    {
        ResolveReferencesIfNeeded();
        TryEmitGenericAmbient();
    }

    [ContextMenu("Test Emit Quest Ambient")]
    private void TestEmitQuestAmbient()
    {
        ResolveReferencesIfNeeded();
        if (!TryEmitQuestAwareAmbient(out bool requestFound))
            LogAmbient($"Quest ambient test failed. requestFound={requestFound}", force: true);
    }

    [ContextMenu("Print Ambient Debug State")]
    private void PrintAmbientDebugState()
    {
        ResolveReferencesIfNeeded();
        TryResolvePlayer(out var player, out string playerSource);
        float distance = player != null ? Vector3.Distance(transform.position, player.position) : -1f;
        bool persistentDialogueVisible = dialogueAgent != null && dialogueAgent.Presenter != null && dialogueAgent.Presenter.IsShowingPersistentContent;
        string topic = ambientTopics != null && ambientTopics.Length > 0 ? ambientTopics[0] : string.Empty;
        string budget = NpcDialogueDirector.Instance != null ? NpcDialogueDirector.Instance.GetAmbientBudgetDebugString() : "No dialogue director";

        Debug.Log(
            $"[NpcAmbientDialogueEmitter] {name}\n" +
            $"agent={(dialogueAgent != null)} bank={(dialogueAgent != null && dialogueAgent.DialogueBank != null)} questGiver={(questGiver != null)} player={(player != null)} source={playerSource} distance={distance:0.0} radius={activationRadius:0.0}\n" +
            $"playerName={(player != null ? player.name : "none")} root={(player != null ? player.root.name : "none")} tag={(player != null ? player.tag : "none")} pos={(player != null ? player.position.ToString("F1") : "none")}\n" +
            $"requirePlayerInTrigger={requirePlayerInTrigger} prompt={(talkInteractable != null && talkInteractable.IsPromptAvailable)} interacting={(talkInteractable != null && talkInteractable.IsActivelyInteracting)}\n" +
            $"persistent={persistentDialogueVisible} activeQuestSession={(questGiver != null && questGiver.HasActiveOfferSession)} nextSpeakIn={Mathf.Max(0f, _nextSpeakTime - Time.unscaledTime):0.0}s\n" +
            $"sampleTopic='{topic}' trigger={(playOnPlayerNearby ? NpcDialogueTrigger.PlayerNearby : NpcDialogueTrigger.Ambient)} audience={audience}\n" +
            budget,
            this);
    }

    [ContextMenu("Resolve Player Debug")]
    private void ResolvePlayerDebug()
    {
        _player = null;
        bool found = TryResolvePlayer(out var player, out string source);
        string details = found
            ? $"player={player.name} root={player.root.name} tag={player.tag} pos={player.position.ToString("F1")} source={source} distance={Vector3.Distance(transform.position, player.position):0.000}"
            : $"no player found source={source}";
        Debug.Log($"[NpcAmbientDialogueEmitter] {name}: {details}", this);
    }

    private void LogAmbient(string message, bool force = false)
    {
        if (!force && !logAmbientDebug)
            return;

        Debug.Log($"[NpcAmbientDialogueEmitter] {name}: {message}", this);
    }

    private void ScheduleNext()
    {
        _nextSpeakTime = Time.unscaledTime + Random.Range(Mathf.Max(0.1f, minDelay), Mathf.Max(minDelay + 0.1f, maxDelay));
    }

    private void ScheduleRetry()
    {
        _nextSpeakTime = Time.unscaledTime + Mathf.Clamp(minDelay * 0.25f, 1f, 4f);
    }

    public void PauseForSeconds(float seconds)
    {
        _nextSpeakTime = Mathf.Max(_nextSpeakTime, Time.unscaledTime + Mathf.Max(0f, seconds));
    }

    private bool TryResolvePlayer(out Transform player)
    {
        return TryResolvePlayer(out player, out _);
    }

    private bool TryResolvePlayer(out Transform player, out string source)
    {
        if (IsValidPlayerTransform(_player))
        {
            player = _player;
            source = "Cached";
            return true;
        }

        _player = null;

        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer != null && IsValidPlayerTransform(taggedPlayer.transform))
        {
            _player = taggedPlayer.transform.root != null ? taggedPlayer.transform.root : taggedPlayer.transform;
            player = _player;
            source = "Tag";
            return true;
        }

        SkiController[] skiControllers;
#if UNITY_2023_1_OR_NEWER
        skiControllers = FindObjectsByType<SkiController>(
            includeInactivePlayersInResolution ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
#else
        skiControllers = FindObjectsOfType<SkiController>(includeInactivePlayersInResolution);
#endif
        for (int i = 0; i < skiControllers.Length; i++)
        {
            var controller = skiControllers[i];
            if (controller == null || !IsValidPlayerTransform(controller.transform))
                continue;

            _player = controller.transform.root != null ? controller.transform.root : controller.transform;
            player = _player;
            source = "SkiControllerFallback";
            return true;
        }

        player = null;
        source = "None";
        return false;
    }

    private bool IsValidPlayerTransform(Transform candidate)
    {
        if (candidate == null)
            return false;

        GameObject go = candidate.gameObject;
        if (!includeInactivePlayersInResolution && !go.activeInHierarchy)
            return false;

        Transform root = candidate.root != null ? candidate.root : candidate;
        if (candidate.CompareTag("NPC") || root.CompareTag("NPC"))
            return false;

        if (maxResolvedPlayerDistanceFromCamera > 0f && Camera.main != null)
        {
            float cameraDistance = Vector3.Distance(root.position, Camera.main.transform.position);
            if (cameraDistance > maxResolvedPlayerDistanceFromCamera)
                return false;
        }

        if (candidate.CompareTag("Player") || root.CompareTag("Player"))
            return true;

        var controller = candidate.GetComponentInParent<SkiController>();
        if (controller == null)
            return false;

        if (!includeInactivePlayersInResolution && (!controller.isActiveAndEnabled || !controller.gameObject.activeInHierarchy))
            return false;

        return !controller.CompareTag("NPC") && !controller.transform.root.CompareTag("NPC");
    }

    private void ResolveReferencesIfNeeded()
    {
        if (dialogueAgent == null)
            dialogueAgent = GetComponent<NpcDialogueAgent>();
        if (talkInteractable == null)
            talkInteractable = GetComponent<NpcTalkInteractable>();
        if (questGiver == null)
            questGiver = GetComponent<NpcQuestGiver>();
        if (socialActor == null)
            socialActor = GetComponent<NpcSocialActor>();
        if (liftAttendantContext == null)
            liftAttendantContext = GetComponent<LiftAttendantSpeaker>();
    }

    private void OnValidate()
    {
        if (dialogueAgent == null)
            dialogueAgent = GetComponent<NpcDialogueAgent>();
        if (talkInteractable == null)
            talkInteractable = GetComponent<NpcTalkInteractable>();
        if (questGiver == null)
            questGiver = GetComponent<NpcQuestGiver>();
        if (socialActor == null)
            socialActor = GetComponent<NpcSocialActor>();

        if (minDelay > maxDelay)
            maxDelay = minDelay;

        if (requirePlayerInTrigger && talkInteractable == null)
            Debug.LogWarning($"[{nameof(NpcAmbientDialogueEmitter)}] {name} requires player in trigger but has no NpcTalkInteractable.", this);

        if (dialogueAgent == null)
            Debug.LogWarning($"[{nameof(NpcAmbientDialogueEmitter)}] {name} has no dialogue agent.", this);

        if ((ambientTopics == null || ambientTopics.Length == 0) && questGiver == null)
            Debug.LogWarning($"[{nameof(NpcAmbientDialogueEmitter)}] {name} has no ambient topics and no quest giver.", this);
    }
}
