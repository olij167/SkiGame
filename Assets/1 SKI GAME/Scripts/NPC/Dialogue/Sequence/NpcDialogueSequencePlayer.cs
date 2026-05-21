using System;
using System.Collections;
using System.Collections.Generic;
using SkiGame.Progression;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class NpcDialogueSequencePlayer : MonoBehaviour
{
    [SerializeField] private NpcDialogueAgent defaultSpeaker;
    [SerializeField] private NpcDialogueSequenceSO debugSequence;

    private readonly List<NpcDialogueSequenceChoice> _availableChoices = new();
    private NpcDialogueSequenceSO _sequence;
    private DialogueContext _context;
    private NpcDialogueAgent _speaker;
    private Coroutine _playRoutine;
    private NpcDialogueSequenceNode _waitingNode;
    private bool _waitingForContinue;
    private bool _executingChoiceActions;
    private int _currentChoiceIndex;
    private Func<NpcDialogueSequenceNode, NpcDialogueAgent> _speakerResolver;

    public bool IsPlaying => _sequence != null;
    public bool HasActiveChoice => IsPlaying && _availableChoices.Count > 0;
    public int CurrentChoiceIndex => _currentChoiceIndex;
    public int ChoiceCount => _availableChoices.Count;

    public event Action<NpcDialogueSequenceSO> OnSequenceStarted;
    public event Action<NpcDialogueSequenceSO> OnSequenceCompleted;
    public event Action<NpcDialogueSequenceSO, IReadOnlyList<NpcDialogueSequenceChoice>> OnChoicePresented;
    public event Action<NpcDialogueSequenceAction, DialogueContext> OnActionRequested;

    private void Awake()
    {
        if (defaultSpeaker == null)
            defaultSpeaker = GetComponent<NpcDialogueAgent>();
    }

    public bool Play(NpcDialogueSequenceSO sequence, DialogueContext context, NpcDialogueAgent speaker)
    {
        return Play(sequence, context, speaker, null);
    }

    public bool Play(NpcDialogueSequenceSO sequence, DialogueContext context, NpcDialogueAgent speaker, Func<NpcDialogueSequenceNode, NpcDialogueAgent> speakerResolver)
    {
        if (sequence == null)
            return false;

        Stop();
        _sequence = sequence;
        _context = context;
        _speaker = speaker != null ? speaker : defaultSpeaker;
        _speakerResolver = speakerResolver;
        _availableChoices.Clear();
        _currentChoiceIndex = 0;
        OnSequenceStarted?.Invoke(sequence);
        _playRoutine = StartCoroutine(PlayFromNode(sequence.GetStartNode()));
        return true;
    }

    public void Stop()
    {
        if (_playRoutine != null)
            StopCoroutine(_playRoutine);

        if (_sequence != null && _sequence.autoCloseOnEnd && ResolvePresenter() != null)
            ResolvePresenter().HideContent();

        _playRoutine = null;
        _waitingNode = null;
        _waitingForContinue = false;
        _availableChoices.Clear();
        _sequence = null;
        _speakerResolver = null;
    }

    public void Continue()
    {
        if (!_waitingForContinue)
            return;

        _waitingForContinue = false;
        if (ResolvePresenter() != null)
            ResolvePresenter().HideContent();

        string nextNodeId = _waitingNode != null ? _waitingNode.nextNodeId : string.Empty;
        _waitingNode = null;
        _playRoutine = StartCoroutine(PlayFromNode(ResolveNextNode(nextNodeId)));
    }

    public void CycleChoice(int direction)
    {
        if (_availableChoices.Count == 0)
            return;

        _currentChoiceIndex = WrapIndex(_currentChoiceIndex + Math.Sign(direction), _availableChoices.Count);
        PresentChoice(_waitingNode);
    }

    public void Choose(int choiceIndex)
    {
        if (_availableChoices.Count == 0)
            return;

        int index = Mathf.Clamp(choiceIndex, 0, _availableChoices.Count - 1);
        var choice = _availableChoices[index];
        _executingChoiceActions = true;
        try
        {
            ExecuteActions(choice.actions);
        }
        finally
        {
            _executingChoiceActions = false;
        }

        _availableChoices.Clear();
        _waitingNode = null;
        if (ResolvePresenter() != null)
            ResolvePresenter().HideContent();

        _playRoutine = StartCoroutine(PlayFromNode(ResolveNextNode(choice.nextNodeId)));
    }

    [ContextMenu("Play Debug Sequence")]
    private void PlayDebugSequence()
    {
        Play(debugSequence, default, defaultSpeaker);
    }

    private IEnumerator PlayFromNode(NpcDialogueSequenceNode node)
    {
        int safety = 0;
        while (_sequence != null && node != null && safety++ < 256)
        {
            if (!DialogueRequirementEvaluator.AreMet(node.requirements, _context))
            {
                node = ResolveNextNode(node.nextNodeId);
                continue;
            }

            switch (node.nodeType)
            {
                case NpcDialogueSequenceNodeType.Line:
                    yield return PresentLine(node);
                    node = ResolveNextNode(node.nextNodeId);
                    break;

                case NpcDialogueSequenceNodeType.Choice:
                    PresentChoice(node);
                    yield break;

                case NpcDialogueSequenceNodeType.Action:
                    ExecuteActions(node.actions);
                    node = ResolveNextNode(node.nextNodeId);
                    break;

                case NpcDialogueSequenceNodeType.Jump:
                    node = ResolveNextNode(node.targetNodeId);
                    break;

                case NpcDialogueSequenceNodeType.Wait:
                    yield return new WaitForSecondsRealtime(Mathf.Max(0f, node.waitSeconds));
                    node = ResolveNextNode(node.nextNodeId);
                    break;

                case NpcDialogueSequenceNodeType.End:
                    CompleteSequence();
                    yield break;

                default:
                    node = ResolveNextNode(node.nextNodeId);
                    break;
            }
        }

        CompleteSequence();
    }

    private IEnumerator PresentLine(NpcDialogueSequenceNode node)
    {
        var speaker = ResolveSpeaker(node);
        var presenter = speaker != null ? speaker.Presenter : ResolvePresenter();
        if (presenter == null)
            yield break;

        string speakerName = speaker != null ? speaker.ResolveSpeakerName(_context) : _context.npcName;
        DialogueContext lineContext = _context;
        lineContext.speakerName = speakerName;
        var content = new NpcDialogueBubbleContent
        {
            kind = NpcDialogueBubbleContentKind.Simple,
            lifetimeMode = node.waitForPlayerContinue ? NpcDialogueBubbleLifetimeMode.PersistentUntilHidden : NpcDialogueBubbleLifetimeMode.Timed,
            speakerName = speakerName,
            bodyText = node.text,
            controlsText = node.waitForPlayerContinue ? "Continue" : string.Empty,
            showSpeaker = !string.IsNullOrWhiteSpace(speakerName),
            importance = node.importance != default ? node.importance : _sequence.defaultImportance,
            styleOverride = node.styleOverride != null ? node.styleOverride : _sequence.styleOverride,
            context = lineContext,
            inputActions = NpcDialogueDirector.Instance != null ? NpcDialogueDirector.Instance.InputActions : null,
            priority = 1000,
            duration = node.duration
        };

        presenter.ShowContent(content, forceInterrupt: true);
        if (node.waitForPlayerContinue)
        {
            _waitingNode = node;
            _waitingForContinue = true;
            yield break;
        }

        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, node.duration));
    }

    private void PresentChoice(NpcDialogueSequenceNode node)
    {
        _waitingNode = node;
        _availableChoices.Clear();
        if (node?.choices != null)
        {
            for (int i = 0; i < node.choices.Length; i++)
            {
                var choice = node.choices[i];
                if (choice != null && DialogueRequirementEvaluator.AreMet(choice.requirements, _context))
                    _availableChoices.Add(choice);
            }
        }

        if (_availableChoices.Count == 0)
        {
            _playRoutine = StartCoroutine(PlayFromNode(ResolveNextNode(node != null ? node.nextNodeId : null)));
            return;
        }

        _currentChoiceIndex = Mathf.Clamp(_currentChoiceIndex, 0, _availableChoices.Count - 1);
        var presenter = ResolvePresenter();
        if (presenter != null)
        {
            presenter.ShowContent(new NpcDialogueBubbleContent
            {
                kind = NpcDialogueBubbleContentKind.Card,
                lifetimeMode = NpcDialogueBubbleLifetimeMode.PersistentUntilHidden,
                speakerName = _speaker != null ? _speaker.ResolveSpeakerName(_context) : _context.npcName,
                titleText = node.promptText,
                bodyText = BuildChoiceBody(),
                controlsText = "Interact Select    Previous/Next Change",
                showSpeaker = _speaker != null,
                importance = _sequence.defaultImportance,
                styleOverride = node.styleOverride != null ? node.styleOverride : _sequence.styleOverride,
                context = _context,
                inputActions = NpcDialogueDirector.Instance != null ? NpcDialogueDirector.Instance.InputActions : null,
                priority = 1000
            }, forceInterrupt: true);
        }

        OnChoicePresented?.Invoke(_sequence, _availableChoices);
    }

    private string BuildChoiceBody()
    {
        string body = string.Empty;
        for (int i = 0; i < _availableChoices.Count; i++)
        {
            string marker = i == _currentChoiceIndex ? "> " : "  ";
            body += $"{marker}{i + 1}. {_availableChoices[i].label}\n";
        }

        return body.TrimEnd();
    }

    private void ExecuteActions(NpcDialogueSequenceAction[] actions)
    {
        if (actions == null)
            return;

        for (int i = 0; i < actions.Length; i++)
            ExecuteAction(actions[i]);
    }

    private void ExecuteAction(NpcDialogueSequenceAction action)
    {
        if (action == null)
            return;

        var director = QuestDirector.Instance;
        string questId = ResolveQuestId(action);
        if (IsQuestMutatingAction(action.actionType) && action.requiresExplicitChoiceOrConfirm && !_executingChoiceActions)
        {
            Debug.Log($"Dialogue action '{action.actionType}' skipped because it requires an explicit choice/confirm.", this);
            OnActionRequested?.Invoke(action, _context);
            return;
        }

        switch (action.actionType)
        {
            case NpcDialogueSequenceActionType.AcceptQuest:
                director?.TryAcceptQuest(questId, ResolveNpcIdentityId());
                break;
            case NpcDialogueSequenceActionType.TrackQuest:
            case NpcDialogueSequenceActionType.SetQuestTracked:
                director?.SetQuestTracked(questId, action.boolValue);
                break;
            case NpcDialogueSequenceActionType.CompleteQuest:
                if (director != null)
                    director.TryTurnInQuest(questId, ResolveNpcIdentityId());
                break;
            case NpcDialogueSequenceActionType.RaiseQuestSignal:
                UnityEngine.Object.FindObjectOfType<QuestSignalBus>()?.RaiseEvent(
                    action.signalKey,
                    QuestSignalData.Create()
                        .WithText(action.signalText)
                        .WithTag("questId", questId));
                break;
            case NpcDialogueSequenceActionType.StartDialogueSequence:
                if (action.dialogueSequence != null)
                    Play(action.dialogueSequence, _context, _speaker, _speakerResolver);
                break;
            case NpcDialogueSequenceActionType.PlayTimelinePlaceholder:
                Debug.Log($"Timeline placeholder requested: {action.timelineId}", this);
                break;
            case NpcDialogueSequenceActionType.MoveNpcToMarker:
            case NpcDialogueSequenceActionType.TriggerNpcAnimation:
            case NpcDialogueSequenceActionType.PerformNpcAction:
                Debug.Log($"Dialogue action placeholder requested: {action.actionType}", this);
                break;
        }

        OnActionRequested?.Invoke(action, _context);
    }

    private static bool IsQuestMutatingAction(NpcDialogueSequenceActionType actionType)
    {
        return actionType == NpcDialogueSequenceActionType.AcceptQuest ||
               actionType == NpcDialogueSequenceActionType.TrackQuest ||
               actionType == NpcDialogueSequenceActionType.CompleteQuest ||
               actionType == NpcDialogueSequenceActionType.SetQuestTracked ||
               actionType == NpcDialogueSequenceActionType.RaiseQuestSignal;
    }

    private string ResolveQuestId(NpcDialogueSequenceAction action)
    {
        if (action.questDefinition != null)
            return action.questDefinition.SafeId;
        if (!string.IsNullOrWhiteSpace(action.questId))
            return action.questId.Trim();
        if (_context.questDefinition != null)
            return _context.questDefinition.SafeId;
        return _context.questId;
    }

    private string ResolveNpcIdentityId()
    {
        var identity = _speaker != null ? _speaker.GetComponent<NpcIdentity>() : GetComponent<NpcIdentity>();
        return identity != null ? identity.IdentityId : string.Empty;
    }

    private NpcDialogueSequenceNode ResolveNextNode(string nodeId)
    {
        if (_sequence == null || string.IsNullOrWhiteSpace(nodeId))
            return null;

        return _sequence.TryGetNode(nodeId, out var node) ? node : null;
    }

    private NpcDialogueAgent ResolveSpeaker(NpcDialogueSequenceNode node)
    {
        if (_speakerResolver != null)
        {
            var resolved = _speakerResolver(node);
            if (resolved != null)
                return resolved;
        }

        if (node != null && !string.IsNullOrWhiteSpace(node.speakerIdentityId))
        {
#if UNITY_2023_1_OR_NEWER
            var identities = FindObjectsByType<NpcIdentity>(FindObjectsSortMode.None);
#else
            var identities = FindObjectsOfType<NpcIdentity>();
#endif
            for (int i = 0; i < identities.Length; i++)
            {
                if (identities[i] != null &&
                    string.Equals(identities[i].IdentityId, node.speakerIdentityId, StringComparison.OrdinalIgnoreCase) &&
                    identities[i].TryGetComponent(out NpcDialogueAgent agent))
                    return agent;
            }
        }

        return _speaker != null ? _speaker : defaultSpeaker;
    }

    private NpcDialogueBubblePresenter ResolvePresenter()
    {
        var speaker = _speaker != null ? _speaker : defaultSpeaker;
        return speaker != null ? speaker.Presenter : GetComponent<NpcDialogueBubblePresenter>();
    }

    private void CompleteSequence()
    {
        var completed = _sequence;
        if (completed != null && completed.autoCloseOnEnd && ResolvePresenter() != null)
            ResolvePresenter().HideContent();

        _sequence = null;
        _waitingNode = null;
        _waitingForContinue = false;
        _availableChoices.Clear();
        _speakerResolver = null;
        OnSequenceCompleted?.Invoke(completed);
    }

    private static int WrapIndex(int value, int count)
    {
        if (count <= 0)
            return 0;

        value %= count;
        return value < 0 ? value + count : value;
    }
}
