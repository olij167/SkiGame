using System;
using System.Collections;
using System.Collections.Generic;
using SkiGame.Progression;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class NpcQuestGiver : MonoBehaviour
{
    public enum QuestInteractionState
    {
        None,
        Available,
        InProgress,
        ReadyToTurnIn,
        Completed,
        ReplayableCompleted,
        Blocked
    }

    public enum NpcQuestConfirmResult
    {
        None,
        Accepted,
        Tracked,
        TurnedIn,
        Replayed,
        CompletedNoAction,
        FailedNoQuestDirector,
        FailedQuestNotInCatalog,
        FailedQuestAlreadyActive,
        FailedQuestAlreadyCompleted,
        FailedInvalidTurnInTarget,
        FailedUnknown
    }

    public enum NpcQuestIndicatorState
    {
        None,
        Available,
        InProgress,
        ReadyToTurnIn,
        CompletedReplayable,
        Blocked
    }

    [Serializable]
    public sealed class NpcQuestOfferRuntimeState
    {
        public NpcQuestOfferSO offer;
        public QuestInteractionState state;
        public int inspectorIndex;
        public string npcName;
        public string questTitle;
        public string bodyText;
        public string badgeText;
        public string stateText;
        public string confirmVerb;
        public string confirmText;
    }

    [Serializable]
    public sealed class NpcQuestOfferViewData
    {
        public string npcName;
        public string badgeText;
        public string questTitle;
        public string stateText;
        public string bodyText;
        public string confirmVerb;
        public string confirmText;
        public QuestInteractionState state;
        public int selectedIndex;
        public int totalOffers;
        public bool canCycle;
        public bool canConfirm;
        public string errorText;
        public float errorUntil;
    }

    public sealed class NpcQuestAmbientDialogueRequest
    {
        public DialogueContext context;
        public NpcDialogueBankSO bankOverride;
        public float weight;
        public float cooldown;
    }

    [SerializeField] private NpcDialogueAgent dialogueAgent;
    [SerializeField] private NpcDialogueSequencePlayer sequencePlayer;
    [SerializeField] private NpcIdentity identity;
    [SerializeField] private NpcConversationPanel conversationPanel;
    [SerializeField] private List<NpcQuestOfferSO> questOffers = new();
    [SerializeField] private NpcQuestOfferOrderMode offerOrderMode = NpcQuestOfferOrderMode.InspectorOrder;
    [SerializeField] private NpcQuestActiveBlockingMode activeBlockingMode = NpcQuestActiveBlockingMode.None;
    [SerializeField] private bool closeOfferSessionAfterConfirm = true;
    [SerializeField] private bool closeOfferSessionAfterFailedConfirm = false;
    [SerializeField] private bool useConversationPanelAsDebugFallback = false;
    [SerializeField] private bool logQuestOfferDebug = false;

    private readonly List<NpcQuestOfferRuntimeState> _displayableOffers = new();
    private int _selectedOfferIndex;
    private bool _hasActiveOfferSession;
    private string _inlineErrorText;
    private float _inlineErrorUntil;
    private float _nextQuestAmbientAllowedTime;

    public bool HasOffers => questOffers.Count > 0;
    public bool HasActiveOfferSession => _hasActiveOfferSession;
    public int CurrentSelectedOfferIndex => _selectedOfferIndex;
    public bool HasDisplayableOffers
    {
        get
        {
            RefreshDisplayableOffers();
            return _displayableOffers.Count > 0;
        }
    }

    public bool HasAnyAvailableQuest
    {
        get
        {
            RefreshDisplayableOffers();
            return HasAnyState(QuestInteractionState.Available);
        }
    }

    public bool HasAnyReadyToTurnInQuest
    {
        get
        {
            RefreshDisplayableOffers();
            return HasAnyState(QuestInteractionState.ReadyToTurnIn);
        }
    }

    public bool HasAnyReplayableCompletedQuest
    {
        get
        {
            RefreshDisplayableOffers();
            return HasAnyState(QuestInteractionState.ReplayableCompleted);
        }
    }

    private void Awake()
    {
        if (dialogueAgent == null)
            dialogueAgent = GetComponent<NpcDialogueAgent>();
        if (sequencePlayer == null)
            sequencePlayer = GetComponent<NpcDialogueSequencePlayer>();
        if (identity == null)
            identity = GetComponent<NpcIdentity>();
    }

    private void OnEnable()
    {
        if (sequencePlayer != null)
            sequencePlayer.OnActionRequested += HandleSequenceActionRequested;
    }

    private void OnDisable()
    {
        if (sequencePlayer != null)
            sequencePlayer.OnActionRequested -= HandleSequenceActionRequested;
    }

    public IReadOnlyList<NpcQuestOfferRuntimeState> GetDisplayableOffers()
    {
        RefreshDisplayableOffers();
        return _displayableOffers;
    }

    public bool BeginOfferSession()
    {
        RefreshDisplayableOffers();
        if (_displayableOffers.Count == 0)
        {
            CancelOfferSession();
            return false;
        }

        _hasActiveOfferSession = true;
        _selectedOfferIndex = Mathf.Clamp(_selectedOfferIndex, 0, _displayableOffers.Count - 1);
        return true;
    }

    public void CancelOfferSession()
    {
        _hasActiveOfferSession = false;
        ClearInlineError();

        if (conversationPanel != null && conversationPanel.IsOpen)
            conversationPanel.Hide();
    }

    public void CycleOffer(int direction)
    {
        RefreshDisplayableOffers();
        if (!_hasActiveOfferSession || _displayableOffers.Count <= 1)
            return;

        _selectedOfferIndex = WrapIndex(_selectedOfferIndex + Math.Sign(direction), _displayableOffers.Count);
        ClearInlineError();
    }

    public bool SetSelectedOfferIndex(int index)
    {
        RefreshDisplayableOffers();
        if (_displayableOffers.Count == 0)
            return false;

        _selectedOfferIndex = Mathf.Clamp(index, 0, _displayableOffers.Count - 1);
        return true;
    }

    public NpcQuestConfirmResult ConfirmOfferAtIndex(int index)
    {
        if (!SetSelectedOfferIndex(index))
            return NpcQuestConfirmResult.None;

        return ConfirmSelectedOffer();
    }

    public NpcQuestConfirmResult ConfirmSelectedOffer()
    {
        RefreshDisplayableOffers();
        if (!_hasActiveOfferSession || _displayableOffers.Count == 0)
            return NpcQuestConfirmResult.None;

        var runtime = _displayableOffers[Mathf.Clamp(_selectedOfferIndex, 0, _displayableOffers.Count - 1)];
        string speakerName = ResolveSpeakerName();
        ClearInlineError();

        NpcQuestConfirmResult result = runtime.state switch
        {
            QuestInteractionState.Available => AcceptOffer(runtime.offer, speakerName),
            QuestInteractionState.InProgress => TrackOffer(runtime.offer, speakerName),
            QuestInteractionState.ReadyToTurnIn => TurnInOffer(runtime.offer, speakerName),
            QuestInteractionState.ReplayableCompleted => ReplayOffer(runtime.offer, speakerName),
            QuestInteractionState.Completed => NpcQuestConfirmResult.CompletedNoAction,
            _ => NpcQuestConfirmResult.FailedUnknown
        };

        if (IsSuccess(result))
        {
            if (closeOfferSessionAfterConfirm)
                CancelOfferSession();
            else
                BeginOfferSession();
        }
        else
        {
            SetInlineError(ResolveFailureFeedback(result));
            if (closeOfferSessionAfterFailedConfirm)
                CancelOfferSession();
            else
                BeginOfferSession();
        }

        if (logQuestOfferDebug)
            Debug.Log($"[NpcQuestGiver] Confirm result on '{name}' -> {result}", this);

        return result;
    }

    public NpcQuestOfferViewData GetCurrentOfferViewData()
    {
        RefreshDisplayableOffers();
        if (!_hasActiveOfferSession || _displayableOffers.Count == 0)
            return null;

        var runtime = _displayableOffers[Mathf.Clamp(_selectedOfferIndex, 0, _displayableOffers.Count - 1)];
        return BuildViewData(runtime, _selectedOfferIndex, _displayableOffers.Count);
    }

    public bool HasSequenceForCurrentOffer()
    {
        RefreshDisplayableOffers();
        if (_displayableOffers.Count == 0)
            return false;

        var runtime = _displayableOffers[Mathf.Clamp(_selectedOfferIndex, 0, _displayableOffers.Count - 1)];
        return ResolveSequenceForState(runtime.offer, runtime.state) != null;
    }

    public bool TryPlayCurrentOfferSequence()
    {
        RefreshDisplayableOffers();
        if (_displayableOffers.Count == 0)
            return false;

        var runtime = _displayableOffers[Mathf.Clamp(_selectedOfferIndex, 0, _displayableOffers.Count - 1)];
        return TryPlaySequenceForRuntime(runtime);
    }

    public bool TryPlayOfferSequenceAtIndex(int index)
    {
        RefreshDisplayableOffers();
        if (_displayableOffers.Count == 0)
            return false;

        _selectedOfferIndex = Mathf.Clamp(index, 0, _displayableOffers.Count - 1);
        return TryPlaySequenceForRuntime(_displayableOffers[_selectedOfferIndex]);
    }

    public string GetPromptDescription()
    {
        string name = ResolveSpeakerName();
        return HasAvailableQuestOffer() ? $"Ask {name} about quests" : $"Talk to {name}";
    }

    public bool HasAvailableQuestOffer()
    {
        RefreshDisplayableOffers();
        return HasAnyState(QuestInteractionState.Available) || HasAnyState(QuestInteractionState.ReplayableCompleted);
    }

    public NpcQuestOfferRuntimeState GetBestAmbientQuestState()
    {
        RefreshDisplayableOffers();
        NpcQuestOfferRuntimeState best = null;
        int bestRank = int.MaxValue;

        for (int i = 0; i < _displayableOffers.Count; i++)
        {
            int rank = GetAmbientPriority(_displayableOffers[i].state);
            if (rank < bestRank)
            {
                best = _displayableOffers[i];
                bestRank = rank;
            }
        }

        if (best != null)
            return best;

        for (int i = 0; i < questOffers.Count; i++)
        {
            var offer = questOffers[i];
            if (offer == null || offer.QuestDefinition == null)
                continue;

            if (IsBlocked(offer) || !MeetsRequirements(offer) || !HasAnyAmbientTopic(offer))
                continue;

            if (HasBlockingActiveQuestForOffer(offer))
            {
                return CreateRuntimeState(offer, QuestInteractionState.Blocked, i);
            }
        }

        return null;
    }

    public bool TryGetQuestAmbientDialogueRequest(out DialogueContext context, out NpcDialogueBankSO bankOverride, out float weight)
    {
        context = default;
        bankOverride = null;
        weight = 0f;

        if (Time.unscaledTime < _nextQuestAmbientAllowedTime)
            return false;

        var runtime = GetBestAmbientQuestState();
        if (runtime == null || runtime.offer == null)
            return false;

        string topic = ResolveAmbientTopic(runtime.offer, runtime.state);
        if (string.IsNullOrWhiteSpace(topic))
            return false;

        context = new DialogueContext
        {
            topicId = topic,
            trigger = NpcDialogueTrigger.PlayerNearby,
            audience = NpcDialogueAudience.Player,
            npcName = ResolveSpeakerName(),
            importanceOverride = NpcDialogueImportance.Ambient
        };

        bankOverride = runtime.offer.AmbientDialogueBankOverride;
        weight = Mathf.Max(0f, runtime.offer.AmbientWeight);
        return true;
    }

    public void NotifyQuestAmbientDialogueResult(bool spoke, float cooldown)
    {
        if (spoke)
            _nextQuestAmbientAllowedTime = Time.unscaledTime + Mathf.Max(0f, cooldown);
    }

    public NpcQuestIndicatorState GetQuestIndicatorState()
    {
        RefreshDisplayableOffers();

        if (HasAnyState(QuestInteractionState.ReadyToTurnIn))
            return NpcQuestIndicatorState.ReadyToTurnIn;
        if (HasAnyState(QuestInteractionState.Available))
            return NpcQuestIndicatorState.Available;
        if (HasAnyState(QuestInteractionState.InProgress))
            return NpcQuestIndicatorState.InProgress;
        if (HasAnyState(QuestInteractionState.ReplayableCompleted))
            return NpcQuestIndicatorState.CompletedReplayable;

        for (int i = 0; i < questOffers.Count; i++)
        {
            var offer = questOffers[i];
            if (offer == null || offer.QuestDefinition == null)
                continue;

            if (HasBlockingActiveQuestForOffer(offer))
                return NpcQuestIndicatorState.Blocked;
        }

        return NpcQuestIndicatorState.None;
    }

    private void RefreshDisplayableOffers()
    {
        _displayableOffers.Clear();

        for (int i = 0; i < questOffers.Count; i++)
        {
            var offer = questOffers[i];
            if (offer == null || offer.QuestDefinition == null)
                continue;

            QuestInteractionState state = ResolveOfferState(offer);
            if (state == QuestInteractionState.None)
                continue;

            _displayableOffers.Add(CreateRuntimeState(offer, state, i));
        }

        _displayableOffers.Sort(CompareRuntimeStates);
        _selectedOfferIndex = _displayableOffers.Count == 0 ? 0 : Mathf.Clamp(_selectedOfferIndex, 0, _displayableOffers.Count - 1);
    }

    private NpcQuestOfferRuntimeState CreateRuntimeState(NpcQuestOfferSO offer, QuestInteractionState state, int inspectorIndex)
    {
        string speakerName = ResolveSpeakerName();
        return new NpcQuestOfferRuntimeState
        {
            offer = offer,
            state = state,
            inspectorIndex = inspectorIndex,
            npcName = speakerName,
            questTitle = ResolveFormattedTitle(offer),
            bodyText = ResolveDialogueText(offer, state),
            badgeText = ResolveBadgeText(offer, state),
            stateText = ResolveStateLabel(state),
            confirmVerb = ResolveConfirmVerb(state),
            confirmText = ResolveConfirmText(state)
        };
    }

    private NpcQuestOfferViewData BuildViewData(NpcQuestOfferRuntimeState runtime, int selectedIndex, int totalOffers)
    {
        return new NpcQuestOfferViewData
        {
            npcName = runtime.npcName,
            badgeText = runtime.badgeText,
            questTitle = runtime.questTitle,
            stateText = runtime.stateText,
            bodyText = runtime.bodyText,
            confirmVerb = runtime.confirmVerb,
            confirmText = runtime.confirmText,
            state = runtime.state,
            selectedIndex = selectedIndex,
            totalOffers = totalOffers,
            canCycle = totalOffers > 1,
            canConfirm = runtime.state != QuestInteractionState.Completed && runtime.state != QuestInteractionState.Blocked,
            errorText = _inlineErrorUntil > Time.unscaledTime ? _inlineErrorText : string.Empty,
            errorUntil = _inlineErrorUntil
        };
    }

    private QuestInteractionState ResolveOfferState(NpcQuestOfferSO offer)
    {
        if (offer == null || offer.QuestDefinition == null || QuestDirector.Instance == null)
            return QuestInteractionState.None;
        if (IsBlocked(offer) || !MeetsRequirements(offer))
            return QuestInteractionState.None;

        string questId = offer.QuestDefinition.SafeId;
        bool blockingActive = HasBlockingActiveQuestForOffer(offer);

        if (QuestDirector.Instance.IsQuestReadyToTurnIn(questId))
            return offer.ShowWhenReadyToTurnIn ? QuestInteractionState.ReadyToTurnIn : QuestInteractionState.None;
        if (QuestDirector.Instance.IsQuestCompleted(questId))
            return offer.Replayable ? QuestInteractionState.ReplayableCompleted : (offer.ShowWhenCompleted && !offer.HideIfQuestCompleted ? QuestInteractionState.Completed : QuestInteractionState.None);
        if (QuestDirector.Instance.IsQuestAccepted(questId))
            return offer.ShowWhenInProgress ? QuestInteractionState.InProgress : QuestInteractionState.None;
        if (blockingActive)
            return QuestInteractionState.Blocked;
        return offer.ShowWhenAvailable ? QuestInteractionState.Available : QuestInteractionState.None;
    }

    private NpcQuestConfirmResult AcceptOffer(NpcQuestOfferSO offer, string speakerName)
    {
        if (QuestDirector.Instance == null)
            return NpcQuestConfirmResult.FailedNoQuestDirector;
        if (offer == null || offer.QuestDefinition == null || QuestDirector.Instance.GetQuestDefinition(offer.QuestDefinition.SafeId) == null)
            return NpcQuestConfirmResult.FailedQuestNotInCatalog;
        if (QuestDirector.Instance.IsQuestCompleted(offer.QuestDefinition.SafeId) && !offer.Replayable)
            return NpcQuestConfirmResult.FailedQuestAlreadyCompleted;
        if (QuestDirector.Instance.IsQuestAccepted(offer.QuestDefinition.SafeId) && !QuestDirector.Instance.IsQuestReadyToTurnIn(offer.QuestDefinition.SafeId))
            return NpcQuestConfirmResult.FailedQuestAlreadyActive;

        if (!QuestDirector.Instance.TryAcceptQuest(offer.QuestDefinition.SafeId, identity != null ? identity.IdentityId : null))
            return NpcQuestConfirmResult.FailedUnknown;

        if (offer.AutoTrackOnAccept || offer.ShowNavigationTarget)
            QuestDirector.Instance.SetQuestTracked(offer.QuestDefinition.SafeId, true);

        if (TryPlayQuestSequence(offer.AcceptedSequence, offer, QuestInteractionState.InProgress))
            return NpcQuestConfirmResult.Accepted;

        string acceptedText = string.IsNullOrWhiteSpace(offer.AcceptedDialogue)
            ? $"You've got it. {offer.OfferTitle} is now active."
            : offer.AcceptedDialogue;
        PresentDialogueBubble(acceptedText, QuestInteractionState.InProgress, speakerName, true);
        return NpcQuestConfirmResult.Accepted;
    }

    private NpcQuestConfirmResult TrackOffer(NpcQuestOfferSO offer, string speakerName)
    {
        if (QuestDirector.Instance == null)
            return NpcQuestConfirmResult.FailedNoQuestDirector;
        if (offer == null || offer.QuestDefinition == null || QuestDirector.Instance.GetQuestDefinition(offer.QuestDefinition.SafeId) == null)
            return NpcQuestConfirmResult.FailedQuestNotInCatalog;
        if (!QuestDirector.Instance.IsQuestAccepted(offer.QuestDefinition.SafeId))
            return NpcQuestConfirmResult.FailedQuestAlreadyCompleted;

        QuestDirector.Instance.SetQuestTracked(offer.QuestDefinition.SafeId, true);
        if (TryPlayQuestSequence(offer.InProgressSequence, offer, QuestInteractionState.InProgress))
            return NpcQuestConfirmResult.Tracked;

        string trackText = string.IsNullOrWhiteSpace(offer.InProgressDialogue)
            ? $"I'll mark {offer.OfferTitle} for you."
            : offer.InProgressDialogue;
        PresentDialogueBubble(trackText, QuestInteractionState.InProgress, speakerName, true);
        return NpcQuestConfirmResult.Tracked;
    }

    private NpcQuestConfirmResult TurnInOffer(NpcQuestOfferSO offer, string speakerName)
    {
        if (QuestDirector.Instance == null)
            return NpcQuestConfirmResult.FailedNoQuestDirector;
        if (offer == null || offer.QuestDefinition == null || QuestDirector.Instance.GetQuestDefinition(offer.QuestDefinition.SafeId) == null)
            return NpcQuestConfirmResult.FailedQuestNotInCatalog;

        string giverIdentityId = identity != null ? identity.IdentityId : string.Empty;
        if (!QuestDirector.Instance.TryTurnInQuest(offer.QuestDefinition.SafeId, giverIdentityId))
            return NpcQuestConfirmResult.FailedInvalidTurnInTarget;

        if (TryPlayQuestSequence(offer.CompletedSequence, offer, QuestInteractionState.Completed))
            return NpcQuestConfirmResult.TurnedIn;

        string completedText = string.IsNullOrWhiteSpace(offer.CompletedDialogue) ? offer.TurnInDialogue : offer.CompletedDialogue;
        if (string.IsNullOrWhiteSpace(completedText))
            completedText = $"{offer.OfferTitle} is complete.";
        PresentDialogueBubble(completedText, QuestInteractionState.Completed, speakerName, true);
        return NpcQuestConfirmResult.TurnedIn;
    }

    private NpcQuestConfirmResult ReplayOffer(NpcQuestOfferSO offer, string speakerName)
    {
        if (QuestDirector.Instance == null)
            return NpcQuestConfirmResult.FailedNoQuestDirector;
        if (offer == null || offer.QuestDefinition == null || QuestDirector.Instance.GetQuestDefinition(offer.QuestDefinition.SafeId) == null)
            return NpcQuestConfirmResult.FailedQuestNotInCatalog;

        if (!QuestDirector.Instance.TryRestartQuest(offer.QuestDefinition.SafeId, identity != null ? identity.IdentityId : null))
            return NpcQuestConfirmResult.FailedUnknown;

        if (offer.AutoTrackOnAccept || offer.ShowNavigationTarget)
            QuestDirector.Instance.SetQuestTracked(offer.QuestDefinition.SafeId, true);

        if (TryPlayQuestSequence(offer.ReplaySequence, offer, QuestInteractionState.InProgress))
            return NpcQuestConfirmResult.Replayed;

        string replayText = string.IsNullOrWhiteSpace(offer.ReplayDialogue) ? offer.AcceptedDialogue : offer.ReplayDialogue;
        if (string.IsNullOrWhiteSpace(replayText))
            replayText = $"Let's run {offer.OfferTitle} again.";
        PresentDialogueBubble(replayText, QuestInteractionState.InProgress, speakerName, true);
        return NpcQuestConfirmResult.Replayed;
    }

    private static string ResolveFailureFeedback(NpcQuestConfirmResult result)
    {
        return result switch
        {
            NpcQuestConfirmResult.FailedNoQuestDirector => "That quest isn't available right now.",
            NpcQuestConfirmResult.FailedQuestNotInCatalog => "That quest isn't available right now.",
            NpcQuestConfirmResult.FailedQuestAlreadyActive => "That lesson is already active.",
            NpcQuestConfirmResult.FailedQuestAlreadyCompleted => "You've already finished that one.",
            NpcQuestConfirmResult.FailedInvalidTurnInTarget => "Come back when you're ready to turn it in here.",
            _ => "I can't start that quest right now."
        };
    }

    private bool MeetsRequirements(NpcQuestOfferSO offer)
    {
        if (QuestDirector.Instance == null)
            return false;

        var requirements = offer.RequiredCompletedQuests;
        for (int i = 0; i < requirements.Count; i++)
        {
            var quest = requirements[i];
            if (quest != null && !QuestDirector.Instance.IsQuestCompleted(quest.SafeId))
                return false;
        }

        return true;
    }

    private bool IsBlocked(NpcQuestOfferSO offer)
    {
        if (QuestDirector.Instance == null)
            return true;

        var blocked = offer.BlockedByCompletedQuests;
        for (int i = 0; i < blocked.Count; i++)
        {
            var quest = blocked[i];
            if (quest != null && QuestDirector.Instance.IsQuestCompleted(quest.SafeId))
                return true;
        }

        return false;
    }

    private bool HasBlockingActiveQuestForOffer(NpcQuestOfferSO targetOffer)
    {
        if (QuestDirector.Instance == null || activeBlockingMode == NpcQuestActiveBlockingMode.None)
            return false;

        for (int i = 0; i < questOffers.Count; i++)
        {
            var offer = questOffers[i];
            if (offer == null || offer.QuestDefinition == null)
                continue;

            string questId = offer.QuestDefinition.SafeId;
            if (!QuestDirector.Instance.IsQuestAccepted(questId) || QuestDirector.Instance.IsQuestCompleted(questId))
                continue;

            switch (activeBlockingMode)
            {
                case NpcQuestActiveBlockingMode.BlockIfAnyOfferedQuestActive:
                case NpcQuestActiveBlockingMode.BlockIfAnyQuestFromThisGiverActive:
                    return true;
                case NpcQuestActiveBlockingMode.BlockIfAnyQuestInSameQuestLineActive:
                    if (!string.IsNullOrWhiteSpace(targetOffer?.QuestLineId) &&
                        string.Equals(offer.QuestLineId, targetOffer.QuestLineId, StringComparison.OrdinalIgnoreCase))
                        return true;
                    break;
            }
        }

        return false;
    }

    private string ResolveDialogueText(NpcQuestOfferSO offer, QuestInteractionState state)
    {
        string source = state switch
        {
            QuestInteractionState.Available => offer.OfferDialogue,
            QuestInteractionState.InProgress => offer.InProgressDialogue,
            QuestInteractionState.ReadyToTurnIn => string.IsNullOrWhiteSpace(offer.TurnInDialogue) ? offer.CompletedDialogue : offer.TurnInDialogue,
            QuestInteractionState.Completed => offer.CompletedDialogue,
            QuestInteractionState.ReplayableCompleted => string.IsNullOrWhiteSpace(offer.ReplayDialogue) ? offer.CompletedDialogue : offer.ReplayDialogue,
            QuestInteractionState.Blocked => string.IsNullOrWhiteSpace(offer.OfferDialogue) ? $"Finish your current lesson before starting {offer.OfferTitle}." : offer.OfferDialogue,
            _ => string.Empty
        };

        if (!string.IsNullOrWhiteSpace(source))
            return source;

        return state switch
        {
            QuestInteractionState.Available => $"I've got something for you: {offer.OfferTitle}.",
            QuestInteractionState.InProgress => $"You're still working on {offer.OfferTitle}.",
            QuestInteractionState.ReadyToTurnIn => $"That should do it for {offer.OfferTitle}.",
            QuestInteractionState.ReplayableCompleted => $"You can replay {offer.OfferTitle} any time.",
            QuestInteractionState.Completed => $"You've already wrapped up {offer.OfferTitle}.",
            QuestInteractionState.Blocked => $"You can't start {offer.OfferTitle} just yet.",
            _ => "Hello."
        };
    }

    private bool TryPlaySequenceForRuntime(NpcQuestOfferRuntimeState runtime)
    {
        if (runtime == null)
            return false;

        var sequence = ResolveSequenceForState(runtime.offer, runtime.state);
        if (!TryPlayQuestSequence(sequence, runtime.offer, runtime.state))
            return false;

        _hasActiveOfferSession = true;
        return true;
    }

    private bool TryPlayQuestSequence(NpcDialogueSequenceSO sequence, NpcQuestOfferSO offer, QuestInteractionState state)
    {
        if (sequence == null || offer == null || sequencePlayer == null)
            return false;

        var context = BuildQuestDialogueContext(offer, state);
        return sequencePlayer.Play(sequence, context, dialogueAgent);
    }

    private NpcDialogueSequenceSO ResolveSequenceForState(NpcQuestOfferSO offer, QuestInteractionState state)
    {
        if (offer == null)
            return null;

        return state switch
        {
            QuestInteractionState.Available => offer.OfferSequence,
            QuestInteractionState.InProgress => offer.InProgressSequence,
            QuestInteractionState.ReadyToTurnIn => offer.ReadyToTurnInSequence,
            QuestInteractionState.Completed => offer.CompletedSequence,
            QuestInteractionState.ReplayableCompleted => offer.ReplaySequence,
            _ => null
        };
    }

    private DialogueContext BuildQuestDialogueContext(NpcQuestOfferSO offer, QuestInteractionState state)
    {
        var definition = offer != null ? offer.QuestDefinition : null;
        string questId = definition != null ? definition.SafeId : string.Empty;
        var questState = QuestDirector.Instance != null ? QuestDirector.Instance.GetQuestState(questId) : null;
        var stage = definition != null && questState != null ? definition.GetStage(questState.currentStageIndex) : null;

        return new DialogueContext
        {
            topicId = ResolveAmbientTopic(offer, state),
            trigger = NpcDialogueTrigger.InteractionStarted,
            audience = NpcDialogueAudience.Player,
            npcName = ResolveSpeakerName(),
            questId = questId,
            questTitle = offer != null ? offer.OfferTitle : definition != null ? definition.title : string.Empty,
            questStage = stage != null ? string.IsNullOrWhiteSpace(stage.title) ? stage.id : stage.title : string.Empty,
            questDefinition = definition,
            questState = questState,
            importanceOverride = NpcDialogueImportance.Quest,
            preferImportantStyle = true
        };
    }

    private void HandleSequenceActionRequested(NpcDialogueSequenceAction action, DialogueContext context)
    {
        if (action == null || action.actionType != NpcDialogueSequenceActionType.AcceptQuest)
            return;

        for (int i = 0; i < questOffers.Count; i++)
        {
            var offer = questOffers[i];
            if (offer == null || offer.AcceptedSequence == null || offer.QuestDefinition == null)
                continue;

            string questId = offer.QuestDefinition.SafeId;
            if (!string.Equals(questId, context.questId, StringComparison.OrdinalIgnoreCase))
                continue;

            StartCoroutine(PlayAcceptedSequenceNextFrame(offer));
            return;
        }
    }

    private IEnumerator PlayAcceptedSequenceNextFrame(NpcQuestOfferSO offer)
    {
        yield return null;
        if (offer != null && offer.AcceptedSequence != null && QuestDirector.Instance != null &&
            QuestDirector.Instance.IsQuestAccepted(offer.QuestDefinition.SafeId))
            TryPlayQuestSequence(offer.AcceptedSequence, offer, QuestInteractionState.InProgress);
    }

    private static string ResolveFormattedTitle(NpcQuestOfferSO offer)
    {
        return offer != null ? offer.OfferTitle : string.Empty;
    }

    private static string ResolveBadgeText(NpcQuestOfferSO offer, QuestInteractionState state)
    {
        string title = offer != null ? offer.OfferTitle : string.Empty;
        if (!string.IsNullOrWhiteSpace(title) && title.IndexOf("lesson", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Lesson";

        return state switch
        {
            QuestInteractionState.Available => "Quest",
            QuestInteractionState.InProgress => "Reminder",
            QuestInteractionState.ReadyToTurnIn => "Turn In",
            QuestInteractionState.ReplayableCompleted => "Replay",
            QuestInteractionState.Blocked => "Locked",
            _ => "Quest"
        };
    }

    private static string ResolveStateLabel(QuestInteractionState state)
    {
        return state switch
        {
            QuestInteractionState.Available => "Available",
            QuestInteractionState.InProgress => "Active",
            QuestInteractionState.ReadyToTurnIn => "Ready",
            QuestInteractionState.ReplayableCompleted => "Completed",
            QuestInteractionState.Completed => "Done",
            QuestInteractionState.Blocked => "Blocked",
            _ => string.Empty
        };
    }

    private static string ResolveConfirmVerb(QuestInteractionState state)
    {
        return state switch
        {
            QuestInteractionState.Available => "Accept",
            QuestInteractionState.InProgress => "Track",
            QuestInteractionState.ReadyToTurnIn => "Turn In",
            QuestInteractionState.ReplayableCompleted => "Replay",
            _ => "Confirm"
        };
    }

    private static string ResolveConfirmText(QuestInteractionState state)
    {
        return state switch
        {
            QuestInteractionState.Available => "Hold to Accept",
            QuestInteractionState.InProgress => "Hold to Track",
            QuestInteractionState.ReadyToTurnIn => "Hold to Turn In",
            QuestInteractionState.ReplayableCompleted => "Hold to Replay",
            _ => string.Empty
        };
    }

    private string ResolveSpeakerName()
    {
        if (identity != null && identity.IsAuthored)
            return identity.DisplayName;

        return dialogueAgent != null ? dialogueAgent.ResolveSpeakerName(default) : gameObject.name;
    }

    private void PresentDialogueBubble(string text, QuestInteractionState state, string speakerName, bool forceInterrupt)
    {
        if (dialogueAgent == null || string.IsNullOrWhiteSpace(text))
            return;

        var line = new NpcDialogueLine
        {
            id = $"quest_{state}",
            text = text,
            priority = state == QuestInteractionState.Available ? 50 : 45,
            duration = 4f,
            preferImportantStyle = true,
            importance = NpcDialogueImportance.Quest,
            showSpeakerName = true,
            trigger = NpcDialogueTrigger.InteractionStarted,
            audience = NpcDialogueAudience.Player
        };

        if (forceInterrupt)
            dialogueAgent.InterruptAndSay(line);
        else
            dialogueAgent.Say(line);
    }

    private int CompareRuntimeStates(NpcQuestOfferRuntimeState a, NpcQuestOfferRuntimeState b)
    {
        if (offerOrderMode == NpcQuestOfferOrderMode.InspectorOrder)
            return a.inspectorIndex.CompareTo(b.inspectorIndex);

        int priorityCompare = offerOrderMode == NpcQuestOfferOrderMode.PriorityThenInspector
            ? b.offer.OfferPriority.CompareTo(a.offer.OfferPriority)
            : GetStateRank(a.state).CompareTo(GetStateRank(b.state));
        return priorityCompare != 0 ? priorityCompare : a.inspectorIndex.CompareTo(b.inspectorIndex);
    }

    private bool HasAnyState(QuestInteractionState state)
    {
        for (int i = 0; i < _displayableOffers.Count; i++)
        {
            if (_displayableOffers[i].state == state)
                return true;
        }

        return false;
    }

    private static int GetStateRank(QuestInteractionState state)
    {
        return state switch
        {
            QuestInteractionState.ReadyToTurnIn => 0,
            QuestInteractionState.Available => 1,
            QuestInteractionState.InProgress => 2,
            QuestInteractionState.ReplayableCompleted => 3,
            QuestInteractionState.Completed => 4,
            QuestInteractionState.Blocked => 5,
            _ => 99
        };
    }

    private static int GetAmbientPriority(QuestInteractionState state)
    {
        return state switch
        {
            QuestInteractionState.ReadyToTurnIn => 0,
            QuestInteractionState.InProgress => 1,
            QuestInteractionState.Available => 2,
            QuestInteractionState.ReplayableCompleted => 3,
            QuestInteractionState.Completed => 4,
            QuestInteractionState.Blocked => 5,
            _ => 99
        };
    }

    private static bool HasAnyAmbientTopic(NpcQuestOfferSO offer)
    {
        return !string.IsNullOrWhiteSpace(offer.AvailableAmbientTopic) ||
               !string.IsNullOrWhiteSpace(offer.InProgressAmbientTopic) ||
               !string.IsNullOrWhiteSpace(offer.ReadyToTurnInAmbientTopic) ||
               !string.IsNullOrWhiteSpace(offer.CompletedAmbientTopic) ||
               !string.IsNullOrWhiteSpace(offer.ReplayAmbientTopic) ||
               !string.IsNullOrWhiteSpace(offer.BlockedAmbientTopic);
    }

    private static string ResolveAmbientTopic(NpcQuestOfferSO offer, QuestInteractionState state)
    {
        if (offer == null)
            return string.Empty;

        return state switch
        {
            QuestInteractionState.ReadyToTurnIn => offer.ReadyToTurnInAmbientTopic,
            QuestInteractionState.InProgress => offer.InProgressAmbientTopic,
            QuestInteractionState.Available => offer.AvailableAmbientTopic,
            QuestInteractionState.ReplayableCompleted => string.IsNullOrWhiteSpace(offer.ReplayAmbientTopic) ? offer.CompletedAmbientTopic : offer.ReplayAmbientTopic,
            QuestInteractionState.Completed => offer.CompletedAmbientTopic,
            QuestInteractionState.Blocked => offer.BlockedAmbientTopic,
            _ => string.Empty
        };
    }

    private void SetInlineError(string text)
    {
        _inlineErrorText = text;
        _inlineErrorUntil = Time.unscaledTime + 2.5f;
    }

    private void ClearInlineError()
    {
        _inlineErrorText = string.Empty;
        _inlineErrorUntil = 0f;
    }

    private static int WrapIndex(int value, int count)
    {
        if (count <= 0)
            return 0;

        value %= count;
        if (value < 0)
            value += count;
        return value;
    }

    private static bool IsSuccess(NpcQuestConfirmResult result)
    {
        return result == NpcQuestConfirmResult.Accepted ||
               result == NpcQuestConfirmResult.Tracked ||
               result == NpcQuestConfirmResult.TurnedIn ||
               result == NpcQuestConfirmResult.Replayed ||
               result == NpcQuestConfirmResult.CompletedNoAction;
    }
}
