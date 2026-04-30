using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-50)]
public sealed class NpcDialogueDirector : MonoBehaviour
{
    private sealed class AgentCooldownState
    {
        public float nextAmbientTime;
        public float nextInteractionTime;
    }

    private readonly struct ActivePresentation
    {
        public readonly NpcDialogueBubblePresenter Presenter;
        public readonly bool Ambient;

        public ActivePresentation(NpcDialogueBubblePresenter presenter, bool ambient)
        {
            Presenter = presenter;
            Ambient = ambient;
        }
    }

    private static NpcDialogueDirector _instance;

    [SerializeField] private InputActionAsset inputActions;
    [SerializeField, Min(1)] private int maxVisibleAmbientBubbles = 3;
    [SerializeField, Min(1)] private int maxVisibleBubbles = 6;

    private readonly Dictionary<string, float> _lineCooldowns = new();
    private readonly Dictionary<int, AgentCooldownState> _agentCooldowns = new();
    private readonly List<ActivePresentation> _activePresentations = new();
    private readonly List<NpcDialogueLine> _candidateBuffer = new();
    private readonly List<NpcDialogueLine> _repeatFallbackBuffer = new();

    public static NpcDialogueDirector Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindFirstObjectByType<NpcDialogueDirector>() ?? CreateRuntimeInstance();

            return _instance;
        }
    }

    public InputActionAsset InputActions
    {
        get
        {
            if (inputActions == null)
                inputActions = new InputSystem_Actions().asset;

            return inputActions;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
    }

    private void LateUpdate()
    {
        for (int i = _activePresentations.Count - 1; i >= 0; i--)
        {
            var entry = _activePresentations[i];
            if (entry.Presenter == null || !entry.Presenter.IsShowing)
                _activePresentations.RemoveAt(i);
        }
    }

    public bool TrySayById(NpcDialogueAgent agent, string lineId, bool forceInterrupt = false)
    {
        if (agent == null || agent.DialogueBank == null || string.IsNullOrWhiteSpace(lineId))
            return false;

        if (!agent.DialogueBank.TryGetLineById(lineId, out var line))
            return false;

        return TrySay(agent, line, default, forceInterrupt);
    }

    public bool TrySayFromTopic(NpcDialogueAgent agent, string topicId, DialogueContext context, bool forceInterrupt = false)
    {
        if (agent == null || agent.DialogueBank == null)
            return false;

        DialogueContext effectiveContext = context;
        if (string.IsNullOrWhiteSpace(effectiveContext.topicId))
            effectiveContext.topicId = topicId;

        if (!TrySelectCandidateLine(agent, effectiveContext.topicId, effectiveContext, out var line, forceInterrupt))
            return false;

        return TrySay(agent, line, effectiveContext, forceInterrupt);
    }

    public bool TrySay(NpcDialogueAgent agent, NpcDialogueLine line, DialogueContext context, bool forceInterrupt = false)
    {
        if (agent == null || line == null)
            return false;

        CleanupActivePresentations();

        bool ambientLike = line.IsAmbientLike && !line.preferImportantStyle && !context.preferImportantStyle;
        if (!forceInterrupt && !CanAgentSpeak(agent, ambientLike))
            return false;

        if (!forceInterrupt && IsLineCoolingDown(agent, line))
            return false;

        var presenter = agent.Presenter;
        if (presenter == null)
            return false;

        if (!forceInterrupt && _activePresentations.Count >= maxVisibleBubbles && !presenter.IsShowing)
            return false;

        int visibleAmbientCount = CountVisibleAmbientBubbles();
        if (ambientLike && visibleAmbientCount >= maxVisibleAmbientBubbles && !presenter.IsAmbientVisible)
            return false;

        var resolvedLine = line.Clone();
        ApplyContextOverrides(resolvedLine, context);
        string speakerName = agent.ResolveSpeakerName(context);
        string resolvedText = NpcDialogueTokenResolver.Resolve(resolvedLine.text, context, speakerName, InputActions);
        if (string.IsNullOrWhiteSpace(resolvedText))
            return false;

        if (!presenter.TryPresent(speakerName, resolvedText, resolvedLine, forceInterrupt))
            return false;

        StampCooldowns(agent, resolvedLine);
        agent.RememberSpokenLine(resolvedLine, context.topicId);
        RegisterPresentation(presenter, ambientLike);
        return true;
    }

    public bool CanShowAmbientBubble()
    {
        CleanupActivePresentations();
        return CountVisibleAmbientBubbles() < maxVisibleAmbientBubbles && _activePresentations.Count < maxVisibleBubbles;
    }

    private bool CanAgentSpeak(NpcDialogueAgent agent, bool ambientLike)
    {
        int id = agent.GetInstanceID();
        if (!_agentCooldowns.TryGetValue(id, out var cooldown))
        {
            cooldown = new AgentCooldownState();
            _agentCooldowns[id] = cooldown;
        }

        float now = Time.unscaledTime;
        return ambientLike ? now >= cooldown.nextAmbientTime : now >= cooldown.nextInteractionTime;
    }

    private bool IsLineCoolingDown(NpcDialogueAgent agent, NpcDialogueLine line)
    {
        if (line.cooldown <= 0f)
            return false;

        string key = BuildLineCooldownKey(agent, line);
        return _lineCooldowns.TryGetValue(key, out float until) && until > Time.unscaledTime;
    }

    private void StampCooldowns(NpcDialogueAgent agent, NpcDialogueLine line)
    {
        int id = agent.GetInstanceID();
        if (!_agentCooldowns.TryGetValue(id, out var cooldown))
        {
            cooldown = new AgentCooldownState();
            _agentCooldowns[id] = cooldown;
        }

        float now = Time.unscaledTime;
        cooldown.nextAmbientTime = now + agent.AmbientCooldownSeconds;
        cooldown.nextInteractionTime = now + agent.InteractionCooldownSeconds;

        if (line.cooldown > 0f)
            _lineCooldowns[BuildLineCooldownKey(agent, line)] = now + line.cooldown;
    }

    private void RegisterPresentation(NpcDialogueBubblePresenter presenter, bool ambientLike)
    {
        for (int i = _activePresentations.Count - 1; i >= 0; i--)
        {
            if (_activePresentations[i].Presenter == presenter)
                _activePresentations.RemoveAt(i);
        }

        _activePresentations.Add(new ActivePresentation(presenter, ambientLike));
    }

    private int CountVisibleAmbientBubbles()
    {
        int count = 0;
        for (int i = 0; i < _activePresentations.Count; i++)
        {
            if (_activePresentations[i].Presenter != null && _activePresentations[i].Presenter.IsAmbientVisible)
                count++;
        }

        return count;
    }

    private void CleanupActivePresentations()
    {
        for (int i = _activePresentations.Count - 1; i >= 0; i--)
        {
            var presenter = _activePresentations[i].Presenter;
            if (presenter == null || !presenter.IsShowing)
                _activePresentations.RemoveAt(i);
        }
    }

    private static string BuildLineCooldownKey(NpcDialogueAgent agent, NpcDialogueLine line)
    {
        string lineKey = !string.IsNullOrWhiteSpace(line.id)
            ? line.id.Trim().ToLowerInvariant()
            : line.text?.Trim().ToLowerInvariant() ?? "line";
        return $"{agent.GetInstanceID()}::{lineKey}";
    }

    private bool TrySelectCandidateLine(NpcDialogueAgent agent, string topicId, DialogueContext context, out NpcDialogueLine line, bool forceInterrupt)
    {
        _candidateBuffer.Clear();
        _repeatFallbackBuffer.Clear();
        agent.DialogueBank.GetCandidateLines(topicId, context, _candidateBuffer);
        if (_candidateBuffer.Count == 0)
        {
            line = null;
            return false;
        }

        for (int i = _candidateBuffer.Count - 1; i >= 0; i--)
        {
            var candidate = _candidateBuffer[i];
            if (candidate == null)
            {
                _candidateBuffer.RemoveAt(i);
                continue;
            }

            bool coolingDown = !forceInterrupt && IsLineCoolingDown(agent, candidate);
            bool repeating = agent.WouldImmediatelyRepeat(candidate, topicId);

            if (repeating && !coolingDown)
                _repeatFallbackBuffer.Add(candidate);

            if (coolingDown || repeating)
                _candidateBuffer.RemoveAt(i);
        }

        if (_candidateBuffer.Count > 0)
        {
            line = agent.DialogueBank.ChooseWeightedRandom(_candidateBuffer);
            return line != null;
        }

        if (_repeatFallbackBuffer.Count > 0)
        {
            line = agent.DialogueBank.ChooseWeightedRandom(_repeatFallbackBuffer);
            return line != null;
        }

        line = null;
        return false;
    }

    private static void ApplyContextOverrides(NpcDialogueLine line, DialogueContext context)
    {
        if (context.priorityOverride != 0)
            line.priority = context.priorityOverride;

        if (context.durationOverride > 0f)
            line.duration = context.durationOverride;

        if (context.preferImportantStyle)
            line.preferImportantStyle = true;

        if (context.importanceOverride.HasValue)
            line.importance = context.importanceOverride.Value;

        if (context.styleOverride != null)
            line.styleOverride = context.styleOverride;
    }

    private static NpcDialogueDirector CreateRuntimeInstance()
    {
        var go = new GameObject(nameof(NpcDialogueDirector));
        DontDestroyOnLoad(go);
        return go.AddComponent<NpcDialogueDirector>();
    }
}
