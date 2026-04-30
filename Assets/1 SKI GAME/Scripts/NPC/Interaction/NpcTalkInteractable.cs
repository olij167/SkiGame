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

    private GameObject _playerRootInTrigger;
    private bool _enterArmed;
    private bool _registered;
    private bool _genericPromptRollPassed = true;
    private float _nextInteractionAllowedTime;
    private float _postConfirmLockoutUntil;
    private float _interactHeld;
    private bool _interactPressedTracking;
    private bool _confirmInputConsumed;

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

        bool interactPressed = interact.IsPressed();
        bool sameAsNext = ActionsShareBinding(interactAction, nextOfferAction);
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
            _interactPressedTracking = true;
            _interactHeld = 0f;
            _confirmInputConsumed = false;
            return;
        }

        if (_interactPressedTracking && interactPressed)
        {
            _interactHeld += Time.unscaledDeltaTime;
            if (_interactHeld >= holdToConfirmSeconds)
            {
                _confirmInputConsumed = true;
                _interactPressedTracking = false;
                _interactHeld = 0f;
                var result = questGiver.ConfirmSelectedOffer();
                _postConfirmLockoutUntil = Time.unscaledTime + postConfirmInputLockoutSeconds;
                HandleQuestConfirmResult(result);
                return;
            }
        }

        if (interactReleasedThisFrame)
        {
            bool wasTap = !_confirmInputConsumed && _interactHeld < holdToConfirmSeconds;
            ResetQuestOfferInteractionState();

            if (wasTap && tapInteractCyclesNextOffer)
            {
                questGiver.CycleOffer(1);
                RefreshQuestOfferBubble();
            }
        }
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

            _interactPressedTracking = false;
            _interactHeld = 0f;
            return;
        }

        if (!pressed)
        {
            _interactPressedTracking = false;
            _interactHeld = 0f;
            return;
        }

        if (!_interactPressedTracking)
        {
            _interactPressedTracking = true;
            _interactHeld = 0f;

            if (!requireHoldForTalk)
                TriggerTalk();

            return;
        }

        if (requireHoldForTalk)
        {
            _interactHeld += Time.unscaledDeltaTime;
            if (_interactHeld >= holdToConfirmSeconds)
            {
                _interactHeld = -999f;
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

        dialogueAgent.Presenter.UpdateQuestOffer(new NpcDialogueBubblePresenter.NpcQuestOfferBubbleViewData
        {
            npcName = data.npcName,
            badgeText = data.badgeText,
            questTitle = data.questTitle,
            stateText = data.stateText,
            bodyText = data.bodyText,
            indexText = data.totalOffers > 1 ? $"{data.selectedIndex + 1}/{data.totalOffers}" : string.Empty,
            canCycle = data.canCycle,
            canConfirm = data.canConfirm,
            previousBindingText = GetBindingDisplay(previousOfferAction, "-"),
            nextBindingText = GetBindingDisplay(nextOfferAction != null ? nextOfferAction : interactAction, "-"),
            confirmBindingText = GetBindingDisplay(interactAction, "-"),
            confirmVerb = data.confirmVerb,
            confirmText = data.confirmText
        });
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

        if (autoBeginQuestSessionOnEnter && questGiver != null && questGiver.BeginOfferSession())
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
        _interactPressedTracking = false;
        _interactHeld = 0f;
        _confirmInputConsumed = false;
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
}
