using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-40)]
[DisallowMultipleComponent]
public sealed class NpcSocialDirector : MonoBehaviour
{
    private sealed class SocialExchange
    {
        public NpcSocialGroupSO group;
        public readonly List<NpcSocialActor> actors = new();
    }

    private static NpcSocialDirector _instance;
    private static readonly List<NpcSocialActor> _registeredActors = new();
    private static readonly List<NpcSocialAnchor> _registeredAnchors = new();

    [SerializeField] private bool playAutomatically = true;
    [SerializeField, Min(0.25f)] private float tickInterval = 2f;
    [SerializeField, Min(0f)] private float minDelayBetweenSocialEvents = 12f;
    [SerializeField, Min(0f)] private float maxDelayBetweenSocialEvents = 25f;
    [SerializeField, Min(1f)] private float playerSearchRadius = 50f;
    [SerializeField, Min(1)] private int maxConcurrentSocialExchanges = 1;
    [SerializeField] private bool useRegisteredActors = true;
    [SerializeField] private bool scanSceneForActorsFallback = true;
    [SerializeField] private bool useRegisteredAnchors = true;
    [SerializeField] private bool scanSceneForAnchorsFallback = true;
    [SerializeField] private NpcSocialGroupSO[] globalGroups;
    [SerializeField] private bool logDebug;

    private readonly List<NpcSocialAnchor> _anchorBuffer = new();
    private readonly List<NpcSocialActor> _actorBuffer = new();
    private readonly List<NpcSocialGroupSO> _groupBuffer = new();
    private readonly List<NpcSocialGroupSO> _weightedGroupBuffer = new();
    private readonly List<NpcSocialDialogueEntry> _entryBuffer = new();
    private readonly List<SocialExchange> _activeExchanges = new();
    private readonly Dictionary<string, float> _groupCooldownUntil = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, float> _entryCooldownUntil = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, float> _playedOnce = new(StringComparer.OrdinalIgnoreCase);
    private Transform _player;
    private float _nextTickTime;
    private float _nextSocialEventTime;
    private string _lastGroupId;
    private string _lastEntryKey;

    public static NpcSocialDirector Instance => _instance;
    public static IReadOnlyList<NpcSocialActor> RegisteredActors => _registeredActors;
    public static IReadOnlyList<NpcSocialAnchor> RegisteredAnchors => _registeredAnchors;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        ScheduleNextSocialEvent();
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void Update()
    {
        CleanupActiveExchanges();

        if (!playAutomatically || Time.unscaledTime < _nextTickTime)
            return;

        _nextTickTime = Time.unscaledTime + tickInterval;
        if (Time.unscaledTime < _nextSocialEventTime)
            return;

        if (TryTriggerSocialDialogue())
            ScheduleNextSocialEvent();
        else
            _nextSocialEventTime = Time.unscaledTime + Mathf.Clamp(tickInterval, 1f, 5f);
    }

    public static void RegisterActor(NpcSocialActor actor)
    {
        if (actor != null && !_registeredActors.Contains(actor))
            _registeredActors.Add(actor);

        if (_instance != null)
            _instance.Log($"Registered actor '{actor?.name}'.");
    }

    public static void UnregisterActor(NpcSocialActor actor)
    {
        _registeredActors.Remove(actor);
    }

    public static void RegisterAnchor(NpcSocialAnchor anchor)
    {
        if (anchor != null && !_registeredAnchors.Contains(anchor))
            _registeredAnchors.Add(anchor);

        if (_instance != null)
            _instance.Log($"Registered anchor '{anchor?.name}'.");
    }

    public static void UnregisterAnchor(NpcSocialAnchor anchor)
    {
        _registeredAnchors.Remove(anchor);
    }

    public bool TryTriggerSocialDialogue()
    {
        return TryTriggerSocialDialogueAtAnchor(null, bypassTiming: false);
    }

    public bool TryTriggerSocialDialogueAtAnchor(NpcSocialAnchor requestedAnchor)
    {
        return TryTriggerSocialDialogueAtAnchor(requestedAnchor, bypassTiming: false);
    }

    public bool TryTriggerSocialDialogueAtAnchor(NpcSocialAnchor requestedAnchor, bool bypassTiming)
    {
        CleanupActiveExchanges();

        if (!bypassTiming && _activeExchanges.Count >= maxConcurrentSocialExchanges)
        {
            Log("Rejected: max concurrent social exchanges reached.");
            return false;
        }

        if (!TryResolvePlayer(out Transform player))
        {
            Log("Rejected: no player found.");
            return false;
        }

        NpcSocialAnchor anchor = requestedAnchor != null ? requestedAnchor : ChooseNearestActiveAnchor(player);
        if (anchor == null)
        {
            Log("Rejected: no active anchor.");
            return false;
        }

        if (!anchor.IsPlayerInRange(player))
        {
            Log($"Rejected: player outside anchor '{anchor.name}' range.");
            return false;
        }

        if (!NpcDialogueDirector.Instance.CanShowAmbientBubble())
        {
            Log($"Rejected: dialogue budget full. {NpcDialogueDirector.Instance.GetAmbientBudgetDebugString()}");
            return false;
        }

        DialogueContext context = anchor.BuildDialogueContext(string.Empty, NpcDialogueTrigger.SocialReply, NpcDialogueAudience.Group);
        CollectEligibleActors(anchor, _actorBuffer);
        if (_actorBuffer.Count == 0)
        {
            Log($"Rejected: no eligible actors near anchor '{anchor.name}'.");
            return false;
        }

        CollectValidGroups(anchor, context, _actorBuffer, _groupBuffer);
        if (_groupBuffer.Count == 0)
        {
            Log($"Rejected: no valid groups for anchor '{anchor.name}'.");
            return false;
        }

        NpcSocialGroupSO group = ChooseWeightedGroup(_groupBuffer);
        if (group == null)
        {
            Log("Rejected: weighted group selection returned none.");
            return false;
        }

        NpcSocialDialogueEntry entry = ChooseDialogueEntry(group, anchor, context);
        if (group.HasDialogueEntries && entry == null)
        {
            Log($"Rejected: group '{group.name}' has no valid dialogue entry for anchor '{anchor.name}'.");
            return false;
        }

        List<NpcSocialActor> speakers = SelectSpeakers(group, _actorBuffer);
        if (speakers.Count < group.MinSpeakers)
        {
            Log($"Rejected: group '{group.name}' needs {group.MinSpeakers} speakers, found {speakers.Count}.");
            return false;
        }

        bool success = GetSequence(group, entry) != null
            ? TryPlaySequence(anchor, group, entry, speakers, context)
            : TryPlayFallbackLine(anchor, group, entry, speakers, context);

        if (success)
        {
            StampCooldown(group, entry);
            _lastGroupId = group.GroupId;
            _lastEntryKey = BuildEntryCooldownKey(group, entry);
            ScheduleNextSocialEvent();
        }

        return success;
    }

    [ContextMenu("Print Social Debug State")]
    private void PrintSocialDebugState()
    {
        TryResolvePlayer(out var player);
        Debug.Log(
            $"[NpcSocialDirector] {name}\n" +
            $"player={(player != null ? player.name : "none")} actors={_registeredActors.Count} anchors={_registeredAnchors.Count} active={_activeExchanges.Count}/{maxConcurrentSocialExchanges}\n" +
            $"nextEventIn={Mathf.Max(0f, _nextSocialEventTime - Time.unscaledTime):0.0}s globalGroups={(globalGroups != null ? globalGroups.Length : 0)}",
            this);
    }

    [ContextMenu("Trigger Random Social Dialogue")]
    private void TriggerRandomSocialDialogue()
    {
        TryTriggerSocialDialogueAtAnchor(null, bypassTiming: true);
    }

    [ContextMenu("Trigger Social Dialogue At Nearest Anchor")]
    private void TriggerSocialDialogueAtNearestAnchor()
    {
        TryResolvePlayer(out var player);
        TryTriggerSocialDialogueAtAnchor(ChooseNearestActiveAnchor(player), bypassTiming: true);
    }

    private bool TryPlaySequence(NpcSocialAnchor anchor, NpcSocialGroupSO group, NpcSocialDialogueEntry entry, List<NpcSocialActor> speakers, DialogueContext context)
    {
        NpcSocialActor primary = speakers[0];
        context.audience = group.Audience;
        context.trigger = NpcDialogueTrigger.SocialReply;
        NpcDialogueSequenceSO sequence = GetSequence(group, entry);
        var player = primary.DialogueAgent.GetComponent<NpcDialogueSequencePlayer>();
        if (player == null)
            player = primary.DialogueAgent.gameObject.AddComponent<NpcDialogueSequencePlayer>();

        MarkActorsStarted(speakers);
        Action<NpcDialogueSequenceSO> completed = null;
        completed = _ =>
        {
            player.OnSequenceCompleted -= completed;
            EndExchange(anchor, group, speakers);
        };

        player.OnSequenceCompleted += completed;
        bool started = player.Play(sequence, context, primary.DialogueAgent, node => ResolveSequenceSpeaker(node, speakers));
        if (!started)
        {
            player.OnSequenceCompleted -= completed;
            EndExchange(anchor, group, speakers);
            Log($"Sequence failed for group '{group.name}'.");
            return false;
        }

        var exchange = new SocialExchange { group = group };
        exchange.actors.AddRange(speakers);
        _activeExchanges.Add(exchange);
        Log($"Started sequence '{sequence.name}' entry='{entry?.SafeId ?? "legacy"}' at anchor '{anchor.name}' with {speakers.Count} speakers.");
        return true;
    }

    private bool TryPlayFallbackLine(NpcSocialAnchor anchor, NpcSocialGroupSO group, NpcSocialDialogueEntry entry, List<NpcSocialActor> speakers, DialogueContext context)
    {
        IReadOnlyList<string> topics = entry != null && entry.fallbackTopics != null && entry.fallbackTopics.Length > 0
            ? entry.fallbackTopics
            : group.FallbackTopics;

        if (topics == null || topics.Count == 0)
        {
            Log($"Fallback failed: group '{group.name}' entry='{entry?.SafeId ?? "legacy"}' has no fallback topics.");
            return false;
        }

        string topic = topics[UnityEngine.Random.Range(0, topics.Count)];
        context.topicId = topic;
        context.audience = group.Audience;
        context.trigger = NpcDialogueTrigger.SocialReply;

        NpcSocialActor speaker = speakers[0];
        NpcDialogueBankSO originalBank = speaker.DialogueAgent.DialogueBank;
        NpcDialogueBankSO bankOverride = entry != null && entry.fallbackBank != null ? entry.fallbackBank : group.FallbackBank;
        bool success = false;
        try
        {
            if (bankOverride != null)
                speaker.DialogueAgent.SetDialogueBank(bankOverride);

            MarkActorsStarted(speakers);
            success = speaker.DialogueAgent.SayContextual(context);
        }
        finally
        {
            if (bankOverride != null)
                speaker.DialogueAgent.SetDialogueBank(originalBank);
        }

        if (!success)
        {
            EndExchange(anchor, group, speakers);
            Log($"Fallback line failed for group '{group.name}' topic='{topic}'.");
            return false;
        }

        StartCoroutine(EndExchangeAfter(anchor, group, speakers, 3.5f));
        Log($"Started fallback topic '{topic}' entry='{entry?.SafeId ?? "legacy"}' at anchor '{anchor.name}'.");
        return true;
    }

    private NpcDialogueAgent ResolveSequenceSpeaker(NpcDialogueSequenceNode node, List<NpcSocialActor> speakers)
    {
        if (speakers == null || speakers.Count == 0)
            return null;

        string role = node != null ? node.speakerRole : string.Empty;
        if (string.IsNullOrWhiteSpace(role))
            return speakers[0].DialogueAgent;

        role = role.Trim();
        if (role.Length == 1 && role[0] >= 'A' && role[0] <= 'Z')
        {
            int index = role[0] - 'A';
            if (index >= 0 && index < speakers.Count)
                return speakers[index].DialogueAgent;
        }

        for (int i = 0; i < speakers.Count; i++)
        {
            if (speakers[i].HasTag(role) || string.Equals(speakers[i].SocialRole, role, StringComparison.OrdinalIgnoreCase))
                return speakers[i].DialogueAgent;
        }

        Log($"Speaker binding failed for role '{role}'. Falling back to first speaker.");
        return speakers[0].DialogueAgent;
    }

    private void MarkActorsStarted(List<NpcSocialActor> speakers)
    {
        for (int i = 0; i < speakers.Count; i++)
        {
            Transform lookTarget = speakers.Count > 1 ? speakers[(i + 1) % speakers.Count].transform : null;
            speakers[i].MarkSocialDialogueStarted(lookTarget);
        }
    }

    private IEnumerator EndExchangeAfter(NpcSocialAnchor anchor, NpcSocialGroupSO group, List<NpcSocialActor> speakers, float seconds)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, seconds));
        EndExchange(anchor, group, speakers);
    }

    private void EndExchange(NpcSocialAnchor anchor, NpcSocialGroupSO group, List<NpcSocialActor> speakers)
    {
        for (int i = 0; i < speakers.Count; i++)
        {
            if (speakers[i] != null)
            {
                speakers[i].MarkSocialDialogueEnded();
                speakers[i].MarkSocialSequenceCompleted(anchor, group);
            }
        }

        for (int i = _activeExchanges.Count - 1; i >= 0; i--)
        {
            if (_activeExchanges[i].group == group)
                _activeExchanges.RemoveAt(i);
        }
    }

    private void CleanupActiveExchanges()
    {
        for (int i = _activeExchanges.Count - 1; i >= 0; i--)
        {
            SocialExchange exchange = _activeExchanges[i];
            bool anyActive = false;
            for (int a = 0; a < exchange.actors.Count; a++)
            {
                if (exchange.actors[a] != null && exchange.actors[a].IsInSocialExchange)
                {
                    anyActive = true;
                    break;
                }
            }

            if (!anyActive)
                _activeExchanges.RemoveAt(i);
        }
    }

    private void CollectEligibleActors(NpcSocialAnchor anchor, List<NpcSocialActor> results)
    {
        results.Clear();

        if (useRegisteredActors)
        {
            for (int i = 0; i < _registeredActors.Count; i++)
                AddIfEligible(anchor, _registeredActors[i], results);
        }

        if (results.Count == 0 && scanSceneForActorsFallback)
        {
#if UNITY_2023_1_OR_NEWER
            var actors = FindObjectsByType<NpcSocialActor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            var actors = FindObjectsOfType<NpcSocialActor>();
#endif
            for (int i = 0; i < actors.Length; i++)
                AddIfEligible(anchor, actors[i], results);
        }
    }

    private static void AddIfEligible(NpcSocialAnchor anchor, NpcSocialActor actor, List<NpcSocialActor> results)
    {
        if (anchor == null || actor == null || results.Contains(actor))
            return;

        if (!actor.IsAvailableForSocialDialogue())
            return;

        if (actor.IsCoolingDownForAnchor(anchor))
            return;

        if (Vector3.Distance(anchor.Center.position, actor.transform.position) > anchor.ActorSearchRadius)
            return;

        results.Add(actor);
    }

    private void CollectValidGroups(NpcSocialAnchor anchor, DialogueContext context, List<NpcSocialActor> actors, List<NpcSocialGroupSO> results)
    {
        results.Clear();
        AppendGroups(anchor != null ? anchor.AllowedGroups : null, results);
        AppendGroups(globalGroups, results);

        for (int i = results.Count - 1; i >= 0; i--)
        {
            var group = results[i];
            if (group == null ||
                !group.AllowsAnchor(anchor) ||
                !group.AreRequirementsMet(context) ||
                IsGroupCoolingDown(group) ||
                !HasValidDialogue(group, anchor, context) ||
                !HasRequiredActors(group, actors) ||
                (!group.CanRepeat && _playedOnce.ContainsKey(group.GroupId)) ||
                (group.AvoidImmediateRepeat && string.Equals(_lastGroupId, group.GroupId, StringComparison.OrdinalIgnoreCase)))
            {
                results.RemoveAt(i);
            }
        }
    }

    private static void AppendGroups(IReadOnlyList<NpcSocialGroupSO> source, List<NpcSocialGroupSO> results)
    {
        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] != null && !results.Contains(source[i]))
                results.Add(source[i]);
        }
    }

    private static bool HasRequiredActors(NpcSocialGroupSO group, List<NpcSocialActor> actors)
    {
        int count = 0;
        for (int i = 0; i < actors.Count; i++)
        {
            if (ActorMatchesGroup(group, actors[i]))
                count++;
        }

        return count >= group.MinSpeakers;
    }

    private List<NpcSocialActor> SelectSpeakers(NpcSocialGroupSO group, List<NpcSocialActor> actors)
    {
        var selected = new List<NpcSocialActor>();
        int targetCount = Mathf.Clamp(group.RequiresMultipleSpeakers ? Mathf.Max(2, group.MinSpeakers) : group.MinSpeakers, 1, group.MaxSpeakers);

        var slots = group.SpeakerSlots;
        if (slots != null && slots.Count > 0)
        {
            for (int s = 0; s < slots.Count && selected.Count < targetCount; s++)
            {
                var actor = FindActorForSlot(slots[s], actors, selected);
                if (actor != null)
                    selected.Add(actor);
            }
        }

        for (int i = 0; i < actors.Count && selected.Count < targetCount; i++)
        {
            if (selected.Contains(actors[i]) || !ActorMatchesGroup(group, actors[i]))
                continue;

            selected.Add(actors[i]);
        }

        return selected;
    }

    private static NpcSocialActor FindActorForSlot(NpcSocialSpeakerSlot slot, List<NpcSocialActor> actors, List<NpcSocialActor> selected)
    {
        if (slot == null)
            return null;

        for (int i = 0; i < actors.Count; i++)
        {
            var actor = actors[i];
            if (actor == null || selected.Contains(actor))
                continue;

            if (slot.requiredTags == null || slot.requiredTags.Length == 0)
                return actor;

            bool allTags = true;
            for (int t = 0; t < slot.requiredTags.Length; t++)
            {
                if (!actor.HasTag(slot.requiredTags[t]))
                {
                    allTags = false;
                    break;
                }
            }

            if (allTags)
                return actor;
        }

        return null;
    }

    private static bool ActorMatchesGroup(NpcSocialGroupSO group, NpcSocialActor actor)
    {
        if (group == null || actor == null)
            return false;

        if (group.Audience == NpcDialogueAudience.Player && !actor.AllowPlayerFacingSocialBarks)
            return false;

        if ((group.Audience == NpcDialogueAudience.Group || group.Audience == NpcDialogueAudience.NearbyNpc) && !actor.AllowNpcToNpcDialogue)
            return false;

        var tags = group.RequiredActorTags;
        if (tags == null || tags.Count == 0)
            return true;

        for (int i = 0; i < tags.Count; i++)
        {
            if (!actor.HasTag(tags[i]))
                return false;
        }

        return true;
    }

    private bool HasValidDialogue(NpcSocialGroupSO group, NpcSocialAnchor anchor, DialogueContext context)
    {
        if (group == null)
            return false;

        if (!group.HasDialogueEntries)
            return group.Sequence != null || (group.FallbackTopics != null && group.FallbackTopics.Count > 0);

        return ChooseDialogueEntry(group, anchor, context) != null;
    }

    private NpcSocialDialogueEntry ChooseDialogueEntry(NpcSocialGroupSO group, NpcSocialAnchor anchor, DialogueContext context)
    {
        _entryBuffer.Clear();
        if (group == null || !group.HasDialogueEntries)
            return null;

        var entries = group.DialogueEntries;
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null || !group.IsEntryValid(entry, anchor, context))
                continue;

            string key = BuildEntryCooldownKey(group, entry);
            if (_entryCooldownUntil.TryGetValue(key, out float until) && until > Time.unscaledTime)
                continue;

            if (entry.avoidImmediateRepeat && string.Equals(_lastEntryKey, key, StringComparison.OrdinalIgnoreCase))
                continue;

            _entryBuffer.Add(entry);
        }

        if (_entryBuffer.Count == 0)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || !group.IsEntryValid(entry, anchor, context))
                    continue;

                string key = BuildEntryCooldownKey(group, entry);
                if (_entryCooldownUntil.TryGetValue(key, out float until) && until > Time.unscaledTime)
                    continue;

                _entryBuffer.Add(entry);
            }
        }

        if (_entryBuffer.Count == 0)
            return null;

        float total = 0f;
        for (int i = 0; i < _entryBuffer.Count; i++)
            total += Mathf.Max(0.01f, _entryBuffer[i].weight);

        float roll = UnityEngine.Random.value * total;
        for (int i = 0; i < _entryBuffer.Count; i++)
        {
            roll -= Mathf.Max(0.01f, _entryBuffer[i].weight);
            if (roll <= 0f)
                return _entryBuffer[i];
        }

        return _entryBuffer[_entryBuffer.Count - 1];
    }

    private static NpcDialogueSequenceSO GetSequence(NpcSocialGroupSO group, NpcSocialDialogueEntry entry)
    {
        if (entry != null && entry.sequence != null)
            return entry.sequence;

        return group != null ? group.Sequence : null;
    }

    private NpcSocialGroupSO ChooseWeightedGroup(List<NpcSocialGroupSO> candidates)
    {
        _weightedGroupBuffer.Clear();
        int totalWeight = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            var group = candidates[i];
            if (group == null)
                continue;

            int copies = Mathf.Max(1, Mathf.RoundToInt(group.Weight));
            totalWeight += copies;
            for (int c = 0; c < copies; c++)
                _weightedGroupBuffer.Add(group);
        }

        return totalWeight > 0 && _weightedGroupBuffer.Count > 0
            ? _weightedGroupBuffer[UnityEngine.Random.Range(0, _weightedGroupBuffer.Count)]
            : null;
    }

    private NpcSocialAnchor ChooseNearestActiveAnchor(Transform player)
    {
        if (player == null)
            return null;

        CollectAnchors(_anchorBuffer);
        NpcSocialAnchor best = null;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < _anchorBuffer.Count; i++)
        {
            var anchor = _anchorBuffer[i];
            if (anchor == null)
                continue;

            float distance = Vector3.Distance(player.position, anchor.Center.position);
            if (distance > Mathf.Min(playerSearchRadius, anchor.ActivationRadius) || distance >= bestDistance)
                continue;

            best = anchor;
            bestDistance = distance;
        }

        return best;
    }

    private void CollectAnchors(List<NpcSocialAnchor> results)
    {
        results.Clear();
        if (useRegisteredAnchors)
        {
            for (int i = 0; i < _registeredAnchors.Count; i++)
            {
                if (_registeredAnchors[i] != null && !results.Contains(_registeredAnchors[i]))
                    results.Add(_registeredAnchors[i]);
            }
        }

        if (results.Count == 0 && scanSceneForAnchorsFallback)
        {
#if UNITY_2023_1_OR_NEWER
            var anchors = FindObjectsByType<NpcSocialAnchor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            var anchors = FindObjectsOfType<NpcSocialAnchor>();
#endif
            for (int i = 0; i < anchors.Length; i++)
            {
                if (anchors[i] != null && !results.Contains(anchors[i]))
                    results.Add(anchors[i]);
            }
        }
    }

    private bool TryResolvePlayer(out Transform player)
    {
        if (_player != null && _player.gameObject.activeInHierarchy)
        {
            player = _player;
            return true;
        }

        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer != null)
        {
            _player = taggedPlayer.transform.root != null ? taggedPlayer.transform.root : taggedPlayer.transform;
            player = _player;
            return true;
        }

        player = null;
        return false;
    }

    private bool IsGroupCoolingDown(NpcSocialGroupSO group)
    {
        if (group == null)
            return true;

        if (_groupCooldownUntil.TryGetValue(group.GroupId, out float until) && until > Time.unscaledTime)
            return true;

        return _groupCooldownUntil.TryGetValue("__global", out float globalUntil) && globalUntil > Time.unscaledTime;
    }

    private void StampCooldown(NpcSocialGroupSO group, NpcSocialDialogueEntry entry)
    {
        _groupCooldownUntil[group.GroupId] = Time.unscaledTime + group.CooldownSeconds;
        _groupCooldownUntil["__global"] = Time.unscaledTime + group.GlobalCooldownSeconds;
        if (entry != null)
            _entryCooldownUntil[BuildEntryCooldownKey(group, entry)] = Time.unscaledTime + entry.cooldownSeconds;
        _playedOnce[group.GroupId] = Time.unscaledTime;
    }

    private static string BuildEntryCooldownKey(NpcSocialGroupSO group, NpcSocialDialogueEntry entry)
    {
        if (group == null || entry == null)
            return string.Empty;

        return $"{group.GroupId}::{entry.SafeId}";
    }

    private void ScheduleNextSocialEvent()
    {
        float min = Mathf.Max(0.1f, minDelayBetweenSocialEvents);
        float max = Mathf.Max(min, maxDelayBetweenSocialEvents);
        _nextSocialEventTime = Time.unscaledTime + UnityEngine.Random.Range(min, max);
    }

    private void Log(string message)
    {
        if (logDebug)
            Debug.Log($"[NpcSocialDirector] {name}: {message}", this);
    }

    private void OnValidate()
    {
        maxConcurrentSocialExchanges = Mathf.Max(1, maxConcurrentSocialExchanges);
        tickInterval = Mathf.Max(0.25f, tickInterval);

        if (globalGroups == null || globalGroups.Length == 0)
            Debug.LogWarning($"[{nameof(NpcSocialDirector)}] {name} has no global groups. Anchor-specific groups can still work.", this);
    }
}
