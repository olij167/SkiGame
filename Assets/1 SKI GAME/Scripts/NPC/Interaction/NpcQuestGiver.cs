using System;
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
        ReplayableCompleted
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
    }

    private sealed class OfferSelection
    {
        public NpcQuestOfferSO Offer;
        public QuestInteractionState State;
        public int InspectorIndex;
    }

    [SerializeField] private NpcDialogueAgent dialogueAgent;
    [SerializeField] private NpcIdentity identity;
    [SerializeField] private NpcConversationPanel conversationPanel;
    [SerializeField] private List<NpcQuestOfferSO> questOffers = new();
    [SerializeField] private NpcQuestOfferOrderMode offerOrderMode = NpcQuestOfferOrderMode.InspectorOrder;
    [SerializeField] private NpcQuestActiveBlockingMode activeBlockingMode = NpcQuestActiveBlockingMode.None;
    [SerializeField] private bool mirrorOfferInDialogueBubble = false;
    [SerializeField] private bool closeOfferSessionAfterConfirm = true;
    [SerializeField] private bool closeOfferSessionAfterFailedConfirm = false;
    [SerializeField] private bool useConversationPanelAsDebugFallback = false;
    [SerializeField] private bool logQuestOfferDebug = false;

    private readonly List<OfferSelection> _sessionOffers = new();
    private int _selectedOfferIndex;
    private bool _hasActiveOfferSession;

    public bool HasOffers => questOffers.Count > 0;
    public bool HasDisplayableOffers
    {
        get
        {
            BuildEligibleOfferSession();
            return _sessionOffers.Count > 0;
        }
    }

    public bool HasActiveOfferSession => _hasActiveOfferSession;

    private void Awake()
    {
        if (dialogueAgent == null)
            dialogueAgent = GetComponent<NpcDialogueAgent>();
        if (identity == null)
            identity = GetComponent<NpcIdentity>();
    }

    public bool BeginOfferSession()
    {
        BuildEligibleOfferSession();
        if (_sessionOffers.Count == 0)
        {
            CancelOfferSession();
            return false;
        }

        _hasActiveOfferSession = true;
        _selectedOfferIndex = Mathf.Clamp(_selectedOfferIndex, 0, _sessionOffers.Count - 1);
        return true;
    }

    public void CycleOffer(int direction)
    {
        if (!_hasActiveOfferSession || _sessionOffers.Count <= 1)
            return;

        _selectedOfferIndex = WrapIndex(_selectedOfferIndex + Math.Sign(direction), _sessionOffers.Count);
    }

    public NpcQuestConfirmResult ConfirmSelectedOffer()
    {
        if (!_hasActiveOfferSession || _sessionOffers.Count == 0)
            return NpcQuestConfirmResult.None;

        var selection = _sessionOffers[_selectedOfferIndex];
        string speakerName = ResolveSpeakerName();
        NpcQuestConfirmResult result = selection.State switch
        {
            QuestInteractionState.Available => AcceptOffer(selection.Offer, speakerName),
            QuestInteractionState.InProgress => TrackOffer(selection.Offer, speakerName),
            QuestInteractionState.ReadyToTurnIn => TurnInOffer(selection.Offer, speakerName),
            QuestInteractionState.ReplayableCompleted => ReplayOffer(selection.Offer, speakerName),
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
            PresentFailureFeedback(result, selection.Offer, speakerName);
            if (closeOfferSessionAfterFailedConfirm)
                CancelOfferSession();
            else
                BeginOfferSession();
        }

        if (logQuestOfferDebug)
            Debug.Log($"[NpcQuestGiver] Confirm result on '{name}' -> {result}", this);

        return result;
    }

    public void CancelOfferSession()
    {
        _hasActiveOfferSession = false;
        _sessionOffers.Clear();

        if (conversationPanel != null && conversationPanel.IsOpen)
            conversationPanel.Hide();
    }

    public NpcQuestOfferViewData GetCurrentOfferViewData()
    {
        if (!_hasActiveOfferSession || _sessionOffers.Count == 0)
            return null;

        var selection = _sessionOffers[Mathf.Clamp(_selectedOfferIndex, 0, _sessionOffers.Count - 1)];
        string speakerName = ResolveSpeakerName();
        return new NpcQuestOfferViewData
        {
            npcName = speakerName,
            badgeText = ResolveBadgeText(selection),
            questTitle = ResolveFormattedTitle(selection.Offer, speakerName),
            stateText = ResolveStateLabel(selection.State),
            bodyText = ResolveDialogueText(selection.Offer, selection.State, speakerName),
            confirmVerb = ResolveConfirmVerb(selection.State),
            confirmText = ResolveConfirmText(selection),
            state = selection.State,
            selectedIndex = _selectedOfferIndex,
            totalOffers = _sessionOffers.Count,
            canCycle = _sessionOffers.Count > 1,
            canConfirm = selection.State != QuestInteractionState.Completed
        };
    }

    public string GetPromptDescription()
    {
        string name = ResolveSpeakerName();
        return HasAvailableQuestOffer() ? $"Ask {name} about quests" : $"Talk to {name}";
    }

    public bool HasAvailableQuestOffer()
    {
        BuildEligibleOfferSession();
        for (int i = 0; i < _sessionOffers.Count; i++)
        {
            if (_sessionOffers[i].State == QuestInteractionState.Available || _sessionOffers[i].State == QuestInteractionState.ReplayableCompleted)
                return true;
        }

        return false;
    }

    private void BuildEligibleOfferSession()
    {
        _sessionOffers.Clear();
        bool blockingQuestActive = IsBlockingQuestAlreadyActive();

        for (int i = 0; i < questOffers.Count; i++)
        {
            var offer = questOffers[i];
            if (offer == null || offer.QuestDefinition == null)
                continue;

            QuestInteractionState state = ResolveState(offer);
            if (state == QuestInteractionState.None)
                continue;

            if (blockingQuestActive && state == QuestInteractionState.Available)
                continue;

            if (!ShouldDisplayState(offer, state))
                continue;

            _sessionOffers.Add(new OfferSelection
            {
                Offer = offer,
                State = state,
                InspectorIndex = i
            });
        }

        _sessionOffers.Sort(CompareSelections);
        _selectedOfferIndex = _sessionOffers.Count == 0 ? 0 : Mathf.Clamp(_selectedOfferIndex, 0, _sessionOffers.Count - 1);
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

        string acceptedText = ResolveTokenizedText(offer.AcceptedDialogue, speakerName);
        if (string.IsNullOrWhiteSpace(acceptedText))
            acceptedText = $"You've got it. {offer.OfferTitle} is now active.";
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
        string trackText = ResolveTokenizedText(offer.InProgressDialogue, speakerName);
        if (string.IsNullOrWhiteSpace(trackText))
            trackText = $"I'll mark {offer.OfferTitle} for you.";
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

        string completedText = ResolveTokenizedText(string.IsNullOrWhiteSpace(offer.CompletedDialogue) ? offer.TurnInDialogue : offer.CompletedDialogue, speakerName);
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

        string replayText = ResolveTokenizedText(string.IsNullOrWhiteSpace(offer.ReplayDialogue) ? offer.AcceptedDialogue : offer.ReplayDialogue, speakerName);
        if (string.IsNullOrWhiteSpace(replayText))
            replayText = $"Let's run {offer.OfferTitle} again.";
        PresentDialogueBubble(replayText, QuestInteractionState.InProgress, speakerName, true);
        return NpcQuestConfirmResult.Replayed;
    }

    private void PresentFailureFeedback(NpcQuestConfirmResult result, NpcQuestOfferSO offer, string speakerName)
    {
        string text = result switch
        {
            NpcQuestConfirmResult.FailedNoQuestDirector => "I can't start that quest right now.",
            NpcQuestConfirmResult.FailedQuestNotInCatalog => "That quest isn't available in the active catalog.",
            NpcQuestConfirmResult.FailedQuestAlreadyActive => "That lesson is already active.",
            NpcQuestConfirmResult.FailedQuestAlreadyCompleted => "You've already finished that one.",
            NpcQuestConfirmResult.FailedInvalidTurnInTarget => "Come back when you're ready to turn it in here.",
            _ => "I can't start that quest right now."
        };
        PresentDialogueBubble(text, QuestInteractionState.InProgress, speakerName, true);
    }

    private QuestInteractionState ResolveState(NpcQuestOfferSO offer)
    {
        if (offer == null || offer.QuestDefinition == null || QuestDirector.Instance == null)
            return QuestInteractionState.None;
        if (IsBlocked(offer) || !MeetsRequirements(offer))
            return QuestInteractionState.None;

        string questId = offer.QuestDefinition.SafeId;
        if (QuestDirector.Instance.IsQuestReadyToTurnIn(questId))
            return QuestInteractionState.ReadyToTurnIn;
        if (QuestDirector.Instance.IsQuestCompleted(questId))
            return offer.Replayable ? QuestInteractionState.ReplayableCompleted : QuestInteractionState.Completed;
        if (QuestDirector.Instance.IsQuestAccepted(questId))
            return QuestInteractionState.InProgress;
        return QuestInteractionState.Available;
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

    private bool IsBlockingQuestAlreadyActive()
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
                    if (!string.IsNullOrWhiteSpace(offer.QuestLineId))
                        return true;
                    break;
            }
        }
        return false;
    }

    private bool ShouldDisplayState(NpcQuestOfferSO offer, QuestInteractionState state)
    {
        return state switch
        {
            QuestInteractionState.Available => offer.ShowWhenAvailable,
            QuestInteractionState.InProgress => offer.ShowWhenInProgress,
            QuestInteractionState.ReadyToTurnIn => offer.ShowWhenReadyToTurnIn,
            QuestInteractionState.Completed => offer.ShowWhenCompleted && !offer.HideIfQuestCompleted,
            QuestInteractionState.ReplayableCompleted => offer.Replayable,
            _ => false
        };
    }

    private string ResolveDialogueText(NpcQuestOfferSO offer, QuestInteractionState state, string speakerName)
    {
        string source = state switch
        {
            QuestInteractionState.Available => offer.OfferDialogue,
            QuestInteractionState.InProgress => offer.InProgressDialogue,
            QuestInteractionState.ReadyToTurnIn => string.IsNullOrWhiteSpace(offer.TurnInDialogue) ? offer.CompletedDialogue : offer.TurnInDialogue,
            QuestInteractionState.Completed => offer.CompletedDialogue,
            QuestInteractionState.ReplayableCompleted => string.IsNullOrWhiteSpace(offer.ReplayDialogue) ? offer.CompletedDialogue : offer.ReplayDialogue,
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(source))
        {
            source = state switch
            {
                QuestInteractionState.Available => $"I've got something for you: {offer.OfferTitle}.",
                QuestInteractionState.InProgress => $"You're still working on {offer.OfferTitle}.",
                QuestInteractionState.ReadyToTurnIn => $"That should do it for {offer.OfferTitle}.",
                QuestInteractionState.ReplayableCompleted => $"You can replay {offer.OfferTitle} any time.",
                QuestInteractionState.Completed => $"You've already wrapped up {offer.OfferTitle}.",
                _ => "Hello."
            };
        }

        return ResolveTokenizedText(source, speakerName);
    }

    private string ResolveFormattedTitle(NpcQuestOfferSO offer, string speakerName)
    {
        return ResolveTokenizedText(offer != null ? offer.OfferTitle : string.Empty, speakerName);
    }

    private string ResolveBadgeText(OfferSelection selection)
    {
        if (selection == null || selection.Offer == null)
            return string.Empty;

        return selection.State switch
        {
            QuestInteractionState.Available => "Quest",
            QuestInteractionState.InProgress => "Reminder",
            QuestInteractionState.ReadyToTurnIn => "Turn In",
            QuestInteractionState.ReplayableCompleted => "Replay",
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
            _ => string.Empty
        };
    }

    private string ResolveConfirmVerb(QuestInteractionState state)
    {
        return state switch
        {
            QuestInteractionState.Available => "Accept",
            QuestInteractionState.InProgress => "Track",
            QuestInteractionState.ReadyToTurnIn => "Turn In",
            QuestInteractionState.ReplayableCompleted => "Replay",
            QuestInteractionState.Completed => "Close",
            _ => "Confirm"
        };
    }

    private string ResolveConfirmText(OfferSelection selection)
    {
        if (selection == null)
            return string.Empty;
        return selection.State switch
        {
            QuestInteractionState.Available => $"Hold to accept {selection.Offer.OfferTitle}",
            QuestInteractionState.InProgress => $"Hold to track {selection.Offer.OfferTitle}",
            QuestInteractionState.ReadyToTurnIn => $"Hold to turn in {selection.Offer.OfferTitle}",
            QuestInteractionState.ReplayableCompleted => $"Hold to replay {selection.Offer.OfferTitle}",
            _ => string.Empty
        };
    }

    private string ResolveSpeakerName()
    {
        if (identity != null && identity.IsAuthored)
            return identity.DisplayName;
        return dialogueAgent != null ? dialogueAgent.ResolveSpeakerName(default) : gameObject.name;
    }

    private string ResolveTokenizedText(string text, string speakerName)
    {
        var context = new DialogueContext
        {
            npcName = speakerName,
            importanceOverride = NpcDialogueImportance.Quest,
            audience = NpcDialogueAudience.Player
        };
        return NpcDialogueTextFormatter.Format(
            text,
            context,
            speakerName,
            NpcDialogueDirector.Instance != null ? NpcDialogueDirector.Instance.InputActions : null,
            null);
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

    private int CompareSelections(OfferSelection a, OfferSelection b)
    {
        if (offerOrderMode == NpcQuestOfferOrderMode.InspectorOrder)
            return a.InspectorIndex.CompareTo(b.InspectorIndex);

        int priorityCompare = offerOrderMode == NpcQuestOfferOrderMode.PriorityThenInspector
            ? b.Offer.OfferPriority.CompareTo(a.Offer.OfferPriority)
            : GetStateRank(a.State).CompareTo(GetStateRank(b.State));
        return priorityCompare != 0 ? priorityCompare : a.InspectorIndex.CompareTo(b.InspectorIndex);
    }

    private static int GetStateRank(QuestInteractionState state)
    {
        return state switch
        {
            QuestInteractionState.Available => 0,
            QuestInteractionState.ReadyToTurnIn => 1,
            QuestInteractionState.InProgress => 2,
            QuestInteractionState.ReplayableCompleted => 3,
            QuestInteractionState.Completed => 4,
            _ => 99
        };
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
