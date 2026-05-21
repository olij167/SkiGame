using SkiGame.UI;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class NpcTalkInteractable : MonoBehaviour, IWorldInteractionPromptSource
{
    [Header("Identity")]
    [SerializeField] private NpcIdentity identity;
    [SerializeField] private NpcSkierProfile skierProfile;
    [SerializeField] private NpcGenericInteractionProfileSO genericInteractionProfile;
    [SerializeField] private NpcDialogueAgent dialogueAgent;
    [SerializeField] private NpcDialogueSequencePlayer sequencePlayer;
    [SerializeField] private NpcQuestGiver questGiver;

    [Header("Input")]
    [SerializeField] private InputActionReference interactAction;
    [SerializeField] private InputActionReference previousOfferAction;
    [SerializeField] private InputActionReference nextOfferAction;

    [Header("Prompt")]
    [SerializeField] private bool requireHoldForTalk;
    [SerializeField] private float holdToConfirmSeconds = 0.2f;
    [SerializeField] private int promptPriority = 57;

    [Header("Interaction")]
    [SerializeField] private Collider interactionAreaTrigger;
    [SerializeField] private Transform questOfferAnchor;
    [SerializeField] private string defaultPromptVerb = "Talk to";
    [SerializeField] private string interactionTopicId;
    [SerializeField] private bool autoBeginQuestSessionOnEnter = true;
    [SerializeField] private bool registerOnlyWhileNearby = true;
    [SerializeField] private bool tapInteractCyclesNextOffer = true;
    [SerializeField] private float postConfirmInputLockoutSeconds = 0.25f;
    [SerializeField] private bool logQuestInteractionDebug;

    private GameObject _playerRootInTrigger;
    private bool _enterArmed;
    private bool _registered;
    private bool _genericPromptRollPassed = true;
    private float _nextInteractionAllowedTime;
    private float _postConfirmLockoutUntil;
    private bool _interactPressActive;
    private float _interactPressStartTime;
    private bool _interactConsumed;
    private bool _waitingForReleaseAfterConfirm;
    private int _selectedOfferIndexAtInteractPress;

    private void Reset()
    {
        interactionAreaTrigger = GetComponent<Collider>();
    }

    private void Awake()
    {
        if (identity == null)
            identity = GetComponent<NpcIdentity>();
        if (skierProfile == null)
            skierProfile = GetComponent<NpcSkierProfile>();
        if (dialogueAgent == null)
            dialogueAgent = GetComponent<NpcDialogueAgent>();
        if (sequencePlayer == null)
            sequencePlayer = GetComponent<NpcDialogueSequencePlayer>();
        if (questGiver == null)
            questGiver = GetComponent<NpcQuestGiver>();
        if (interactionAreaTrigger == null)
            interactionAreaTrigger = GetComponent<Collider>();

        _genericPromptRollPassed = genericInteractionProfile == null || Random.value <= genericInteractionProfile.TalkPromptChance;
        ConfigureDialogueDefaults();
        if (dialogueAgent != null && questOfferAnchor != null)
            dialogueAgent.Presenter.SetAnchor(questOfferAnchor);
    }

    private void OnEnable()
    {
        EnableAction(interactAction);
        EnableAction(previousOfferAction);
        EnableAction(nextOfferAction);

        if (!registerOnlyWhileNearby && ShouldAllowPrompt())
            RegisterPrompt();
    }

    private void OnDisable()
    {
        UnregisterPrompt();
        _postConfirmLockoutUntil = 0f;
        ResetQuestOfferInteractionState();
        HideQuestOfferBubble();
        questGiver?.CancelOfferSession();
    }

    private void Update()
    {
        if (_playerRootInTrigger == null)
            return;

        if (sequencePlayer != null && sequencePlayer.IsPlaying)
        {
            HandleDialogueSequenceInput();
            return;
        }

        if (questGiver != null && questGiver.HasActiveOfferSession)
        {
            HandleQuestOfferInput();
            return;
        }

        HandleTalkInput();
    }

    private void HandleQuestOfferInput()
    {
        if (Time.unscaledTime < _postConfirmLockoutUntil)
            return;

        bool previousPressed = previousOfferAction != null && previousOfferAction.action != null && previousOfferAction.action.WasPressedThisFrame();
        if (previousPressed)
        {
            questGiver.CycleOffer(-1);
            RefreshQuestOfferBubble();
            return;
        }

        if (interactAction == null || interactAction.action == null)
            return;

        var interact = interactAction.action;
        if (!interact.enabled)
            interact.Enable();

        bool interactPressed = IsActionHeld(interact);
        bool sameAsNext = IsSameAction(interactAction, nextOfferAction);
        bool nextPressedThisFrame = !sameAsNext && nextOfferAction != null && nextOfferAction.action != null && nextOfferAction.action.WasPressedThisFrame();
        bool interactPressedThisFrame = interact.WasPressedThisFrame();
        bool interactReleasedThisFrame = interact.WasReleasedThisFrame();

        if (!_enterArmed)
        {
            if (!interactPressed)
                _enterArmed = true;

            ResetQuestOfferInteractionState();
            return;
        }

        if (nextPressedThisFrame)
        {
            questGiver.CycleOffer(1);
            RefreshQuestOfferBubble();
            return;
        }

        if (interactPressedThisFrame)
        {
            _interactPressActive = true;
            _interactPressStartTime = Time.unscaledTime;
            _interactConsumed = false;
            _waitingForReleaseAfterConfirm = false;
            _selectedOfferIndexAtInteractPress = questGiver.CurrentSelectedOfferIndex;
            LogQuestDebug($"Interact press started on offer index {_selectedOfferIndexAtInteractPress}.");
        }

        if (_interactPressActive && interactPressed && !_interactConsumed)
        {
            float heldDuration = Time.unscaledTime - _interactPressStartTime;
            if (heldDuration >= holdToConfirmSeconds)
            {
                _interactConsumed = true;
                _waitingForReleaseAfterConfirm = true;
                LogQuestDebug($"Hold threshold reached at {heldDuration:0.000}s for offer index {_selectedOfferIndexAtInteractPress}.");
                ConfirmQuestOfferSelection(_selectedOfferIndexAtInteractPress);
            }
        }

        if (interactReleasedThisFrame)
        {
            float heldDuration = Time.unscaledTime - _interactPressStartTime;
            bool consumed = _interactConsumed || _waitingForReleaseAfterConfirm;
            LogQuestDebug($"Interact released after {heldDuration:0.000}s. Consumed={consumed}.");
            ResetQuestOfferInteractionState();

            if (consumed)
                return;

            if (heldDuration >= holdToConfirmSeconds)
            {
                LogQuestDebug($"Release path confirming stored offer index {_selectedOfferIndexAtInteractPress}.");
                ConfirmQuestOfferSelection(_selectedOfferIndexAtInteractPress);
                return;
            }

            if (tapInteractCyclesNextOffer)
            {
                LogQuestDebug("Tap detected, cycling next offer.");
                questGiver.CycleOffer(1);
                RefreshQuestOfferBubble();
            }
        }
    }

    private void HandleDialogueSequenceInput()
    {
        if (Time.unscaledTime < _postConfirmLockoutUntil || sequencePlayer == null)
            return;

        bool previousPressed = previousOfferAction != null && previousOfferAction.action != null && previousOfferAction.action.WasPressedThisFrame();
        bool nextPressed = nextOfferAction != null && nextOfferAction.action != null && nextOfferAction.action.WasPressedThisFrame();
        if (sequencePlayer.HasActiveChoice)
        {
            if (previousPressed)
            {
                sequencePlayer.CycleChoice(-1);
                return;
            }

            if (nextPressed)
            {
                sequencePlayer.CycleChoice(1);
                return;
            }
        }

        if (interactAction == null || interactAction.action == null)
            return;

        var interact = interactAction.action;
        if (!interact.enabled)
            interact.Enable();

        if (!interact.WasPressedThisFrame())
            return;

        if (sequencePlayer.HasActiveChoice)
            sequencePlayer.Choose(sequencePlayer.CurrentChoiceIndex);
        else
            sequencePlayer.Continue();
    }

    private void HandleTalkInput()
    {
        if (!ShouldAllowPrompt() || interactAction == null || interactAction.action == null)
            return;

        var action = interactAction.action;
        if (!action.enabled)
            action.Enable();

        bool pressed = action.IsPressed();
        if (!_enterArmed)
        {
            if (!pressed)
                _enterArmed = true;

            _interactPressActive = false;
            return;
        }

        if (!pressed)
        {
            _interactPressActive = false;
            return;
        }

        if (!_interactPressActive)
        {
            _interactPressActive = true;
            _interactPressStartTime = Time.unscaledTime;

            if (!requireHoldForTalk)
                TriggerTalk();

            return;
        }

        if (requireHoldForTalk)
        {
            if (Time.unscaledTime - _interactPressStartTime >= holdToConfirmSeconds)
            {
                _interactPressStartTime = float.PositiveInfinity;
                TriggerTalk();
            }
        }
    }

    private void TriggerTalk()
    {
        if (Time.unscaledTime < _nextInteractionAllowedTime)
            return;

        if (questGiver != null && questGiver.BeginOfferSession())
        {
            ResetQuestOfferInteractionState();
            RefreshQuestOfferBubble();
            StampInteractionCooldown();
            return;
        }

        if (dialogueAgent == null)
            return;

        var context = new DialogueContext
        {
            topicId = ResolveInteractionTopic(),
            trigger = NpcDialogueTrigger.InteractionStarted,
            npcName = ResolveDisplayName(),
            preferImportantStyle = true,
            importanceOverride = NpcDialogueImportance.Interaction,
            audience = NpcDialogueAudience.Player
        };

        dialogueAgent.SayContextual(context);
        StampInteractionCooldown();
    }

    private void RefreshQuestOfferBubble()
    {
        if (questGiver == null || dialogueAgent == null)
            return;

        var data = questGiver.GetCurrentOfferViewData();
        if (data == null)
        {
            dialogueAgent.Presenter.HideQuestOffer(immediate: true);
            return;
        }

        var content = new NpcDialogueBubbleContent
        {
            kind = NpcDialogueBubbleContentKind.Card,
            lifetimeMode = NpcDialogueBubbleLifetimeMode.PersistentUntilHidden,
            speakerName = data.npcName,
            titleText = data.questTitle,
            bodyText = data.bodyText,
            metaText = ComposeMetaText(data),
            controlsText = ComposeControlsText(data),
            errorText = data.errorText,
            errorUntil = data.errorUntil,
            showSpeaker = !string.IsNullOrWhiteSpace(data.npcName),
            importance = NpcDialogueImportance.Quest,
            styleOverride = null,
            context = new DialogueContext
            {
                npcName = data.npcName,
                audience = NpcDialogueAudience.Player,
                importanceOverride = NpcDialogueImportance.Quest
            },
            inputActions = NpcDialogueDirector.Instance != null ? NpcDialogueDirector.Instance.InputActions : null,
            priority = 1000
        };

        dialogueAgent.Presenter.UpdateContent(content);
    }

    private void HideQuestOfferBubble()
    {
        if (dialogueAgent != null)
            dialogueAgent.Presenter.HideQuestOffer(immediate: true);
    }

    private void OnTriggerEnter(Collider other)
    {
        var root = ResolvePlayerRoot(other);
        if (root == null)
            return;

        _playerRootInTrigger = root;
        _enterArmed = false;
        _postConfirmLockoutUntil = 0f;
        ResetQuestOfferInteractionState();

        if (registerOnlyWhileNearby && ShouldAllowPrompt())
            RegisterPrompt();

        if (autoBeginQuestSessionOnEnter && questGiver != null && !questGiver.HasSequenceForCurrentOffer() && questGiver.BeginOfferSession())
        {
            RefreshQuestOfferBubble();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var root = ResolvePlayerRoot(other);
        if (root == null || _playerRootInTrigger != root)
            return;

        _playerRootInTrigger = null;
        _enterArmed = false;
        _postConfirmLockoutUntil = 0f;
        ResetQuestOfferInteractionState();

        if (questGiver != null)
            questGiver.CancelOfferSession();

        HideQuestOfferBubble();

        if (registerOnlyWhileNearby)
            UnregisterPrompt();
    }

    private void ConfigureDialogueDefaults()
    {
        if (dialogueAgent == null)
            return;

        if (identity != null && identity.IsAuthored)
        {
            dialogueAgent.ConfigureSpeakerNameSource(identity, onlyIfMissing: true);
            return;
        }

        if (skierProfile != null)
            dialogueAgent.ConfigureSpeakerNameSource(skierProfile, onlyIfMissing: true);

        if (genericInteractionProfile != null)
        {
            if (genericInteractionProfile.DialogueBank != null)
                dialogueAgent.SetDialogueBank(genericInteractionProfile.DialogueBank, onlyIfMissing: true);

            dialogueAgent.SetFallbackSpeakerName(genericInteractionProfile.FallbackDisplayName);
        }
        else
        {
            dialogueAgent.SetFallbackSpeakerName("Skier");
        }
    }

    private bool ShouldAllowPrompt()
    {
        if (_playerRootInTrigger == null && registerOnlyWhileNearby)
            return CanTriggerBubbleInteraction();

        if (questGiver != null && questGiver.HasActiveOfferSession)
            return false;

        if (questGiver != null && questGiver.HasDisplayableOffers)
            return true;

        if (identity != null && identity.IsAuthored)
            return CanTriggerBubbleInteraction();

        if (genericInteractionProfile == null || !genericInteractionProfile.AllowInteractionPrompt)
            return false;

        return _genericPromptRollPassed && CanTriggerBubbleInteraction();
    }

    private bool CanTriggerBubbleInteraction()
    {
        if (dialogueAgent == null || dialogueAgent.IsShowingQuestOffer)
            return false;

        if (!string.IsNullOrWhiteSpace(interactionTopicId))
            return true;

        return dialogueAgent.DialogueBank != null;
    }

    private string ResolveDisplayName()
    {
        if (identity != null && identity.IsAuthored)
            return identity.DisplayName;

        if (dialogueAgent != null)
            return dialogueAgent.ResolveSpeakerName(default);

        if (skierProfile != null && !string.IsNullOrWhiteSpace(skierProfile.SkierName))
            return skierProfile.SkierName;

        if (genericInteractionProfile != null)
            return genericInteractionProfile.FallbackDisplayName;

        return "Skier";
    }

    private string ResolveInteractionTopic()
    {
        if (!string.IsNullOrWhiteSpace(interactionTopicId))
            return interactionTopicId;

        if (genericInteractionProfile != null && genericInteractionProfile.AllowedTopics.Count > 0)
        {
            int index = genericInteractionProfile.AllowedTopics.Count == 1 || Random.value > genericInteractionProfile.TipChance
                ? 0
                : Random.Range(0, genericInteractionProfile.AllowedTopics.Count);
            return genericInteractionProfile.AllowedTopics[index];
        }

        return string.Empty;
    }

    private void StampInteractionCooldown()
    {
        float cooldown = genericInteractionProfile != null
            ? genericInteractionProfile.InteractionCooldown
            : dialogueAgent != null ? dialogueAgent.InteractionCooldownSeconds : 0f;
        _nextInteractionAllowedTime = Time.unscaledTime + Mathf.Max(0f, cooldown);
    }

    private void ResetQuestOfferInteractionState()
    {
        _interactPressActive = false;
        _interactPressStartTime = 0f;
        _interactConsumed = false;
        _waitingForReleaseAfterConfirm = false;
        _selectedOfferIndexAtInteractPress = 0;
    }

    private void HandleQuestConfirmResult(NpcQuestGiver.NpcQuestConfirmResult result)
    {
        ResetQuestOfferInteractionState();

        bool success = result == NpcQuestGiver.NpcQuestConfirmResult.Accepted ||
                       result == NpcQuestGiver.NpcQuestConfirmResult.Tracked ||
                       result == NpcQuestGiver.NpcQuestConfirmResult.TurnedIn ||
                       result == NpcQuestGiver.NpcQuestConfirmResult.Replayed ||
                       result == NpcQuestGiver.NpcQuestConfirmResult.CompletedNoAction;

        if (success || questGiver == null || !questGiver.HasActiveOfferSession)
        {
            HideQuestOfferBubble();
            return;
        }

        RefreshQuestOfferBubble();
    }

    private void RegisterPrompt()
    {
        if (_registered)
            return;

        WorldInteractionPromptRegistry.Register(this);
        _registered = true;
    }

    private void UnregisterPrompt()
    {
        if (!_registered)
            return;

        WorldInteractionPromptRegistry.Unregister(this);
        _registered = false;
    }

    private static void EnableAction(InputActionReference actionReference)
    {
        if (actionReference != null && actionReference.action != null && !actionReference.action.enabled)
            actionReference.action.Enable();
    }

    private static bool ActionsShareBinding(InputActionReference a, InputActionReference b)
    {
        return a != null && b != null && a.action != null && b.action != null && a.action.id == b.action.id;
    }

    private static bool IsSameAction(InputActionReference a, InputActionReference b)
    {
        return ActionsShareBinding(a, b);
    }

    private void ConfirmQuestOfferSelection(int offerIndex)
    {
        if (questGiver == null)
        {
            LogQuestDebug("Confirm requested, but no quest giver is assigned.");
            return;
        }

        if (!questGiver.HasActiveOfferSession)
        {
            LogQuestDebug("Confirm requested, but there is no active offer session.");
            return;
        }

        float heldDuration = Time.unscaledTime - _interactPressStartTime;
        LogQuestDebug($"Confirming offer index {offerIndex} after {heldDuration:0.000}s.");
        var result = questGiver.ConfirmOfferAtIndex(offerIndex);
        LogQuestDebug($"Confirm result: {result}.");
        _postConfirmLockoutUntil = Time.unscaledTime + postConfirmInputLockoutSeconds;
        HandleQuestConfirmResult(result);
    }

    private string ComposeMetaText(NpcQuestGiver.NpcQuestOfferViewData data)
    {
        string meta = string.Empty;
        AppendSegment(ref meta, data.badgeText);
        if (data.totalOffers > 1)
            AppendSegment(ref meta, $"{data.selectedIndex + 1} / {data.totalOffers}");
        AppendSegment(ref meta, data.stateText);
        return meta;
    }

    private string ComposeControlsText(NpcQuestGiver.NpcQuestOfferViewData data)
    {
        string interactBinding = GetBindingDisplay(interactAction, "-");
        string previousBinding = GetBindingDisplay(previousOfferAction, "-");
        string nextBinding = GetBindingDisplay(nextOfferAction != null ? nextOfferAction : interactAction, "-");
        bool sameAsNext = IsSameAction(interactAction, nextOfferAction) || nextOfferAction == null;

        string controls = string.Empty;
        if (data.canConfirm && !string.IsNullOrWhiteSpace(data.confirmText))
            AppendSegment(ref controls, $"{interactBinding} {data.confirmText}", "    ");

        if (data.canCycle)
        {
            if (sameAsNext && tapInteractCyclesNextOffer)
            {
                AppendSegment(ref controls, $"Tap {interactBinding} Next", "    ");
            }
            else
            {
                AppendSegment(ref controls, $"{previousBinding} Previous", "    ");
                AppendSegment(ref controls, $"{nextBinding} Next", "    ");
            }
        }

        return controls;
    }

    private static void AppendSegment(ref string target, string value, string separator = " • ")
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        target = string.IsNullOrWhiteSpace(target)
            ? value.Trim()
            : target + separator + value.Trim();
    }

    private void LogQuestDebug(string message)
    {
        if (!logQuestInteractionDebug)
            return;

        Debug.Log($"[NpcTalkInteractable] {name}: {message}", this);
    }

    private static bool IsActionHeld(InputAction action)
    {
        if (action == null)
            return false;

        if (action.IsPressed())
            return true;

        if (action.activeControl != null)
        {
            try
            {
                return action.ReadValue<float>() > 0.5f;
            }
            catch
            {
            }
        }

        return false;
    }

    private static string GetBindingDisplay(InputActionReference actionReference, string fallback)
    {
        if (actionReference == null || actionReference.action == null)
            return fallback;

        string display = InputPromptResolver.GetBindingDisplay(actionReference.action);
        return string.IsNullOrWhiteSpace(display) ? fallback : display.Trim();
    }

    private static GameObject ResolvePlayerRoot(Collider other)
    {
        if (other == null)
            return null;

        var t = other.transform;
        while (t != null)
        {
            if (t.CompareTag("NPC"))
                return null;
            t = t.parent;
        }

        Transform playerTagged = null;
        t = other.transform;
        while (t != null)
        {
            if (t.CompareTag("Player"))
            {
                playerTagged = t;
                break;
            }
            t = t.parent;
        }

        if (playerTagged == null)
            return null;

        var playerInput = other.GetComponentInParent<PlayerInput>();
        return playerInput != null ? playerInput.gameObject : playerTagged.gameObject;
    }

    public bool IsPromptAvailable => _playerRootInTrigger != null && ShouldAllowPrompt();
    public bool IsPlayerInInteraction => _playerRootInTrigger != null;
    public bool IsActivelyInteracting => (questGiver != null && questGiver.HasActiveOfferSession) || (sequencePlayer != null && sequencePlayer.IsPlaying);
    public string PromptActionText => "Interact";

    public string PromptDescriptionText
    {
        get
        {
            if (questGiver != null && questGiver.HasDisplayableOffers)
                return questGiver.GetPromptDescription();

            string promptVerb = genericInteractionProfile != null && !string.IsNullOrWhiteSpace(genericInteractionProfile.PromptVerbOverride)
                ? genericInteractionProfile.PromptVerbOverride
                : defaultPromptVerb;
            return $"{promptVerb} {ResolveDisplayName()}";
        }
    }

    public bool PromptUsesHold => requireHoldForTalk;
    public float PromptHoldDuration => holdToConfirmSeconds;
    public Vector3 PromptWorldPosition => interactionAreaTrigger != null ? interactionAreaTrigger.bounds.center : transform.position;
    public int PromptPriority => promptPriority;

    private void OnValidate()
    {
        if (identity == null)
            identity = GetComponent<NpcIdentity>();
        if (dialogueAgent == null)
            dialogueAgent = GetComponent<NpcDialogueAgent>();
        if (sequencePlayer == null)
            sequencePlayer = GetComponent<NpcDialogueSequencePlayer>();
        if (questGiver == null)
            questGiver = GetComponent<NpcQuestGiver>();
        if (interactionAreaTrigger == null)
            interactionAreaTrigger = GetComponent<Collider>();

        if (questGiver != null && interactAction == null)
            Debug.LogWarning($"[{nameof(NpcTalkInteractable)}] {name} has a quest giver but no interact action.", this);
    }
}
