using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NpcDialogueBank", menuName = "SkiGame/NPC/Dialogue Bank")]
public sealed class NpcDialogueBankSO : ScriptableObject
{
    [SerializeField] private List<NpcDialogueLine> lines = new();

    private Dictionary<string, NpcDialogueLine> _byId;

    public bool TryGetLineById(string lineId, out NpcDialogueLine line)
    {
        EnsureLookup();
        return _byId.TryGetValue(Normalize(lineId), out line);
    }

    public void GetCandidateLines(string topicId, DialogueContext context, List<NpcDialogueLine> results)
    {
        if (results == null)
            return;

        results.Clear();
        string normalizedTopic = Normalize(topicId);
        bool requireTopic = !string.IsNullOrWhiteSpace(normalizedTopic);

        for (int i = 0; i < lines.Count; i++)
        {
            var candidate = lines[i];
            if (!IsCandidateValid(candidate, context, normalizedTopic, requireTopic))
                continue;

            results.Add(candidate);
        }

        if (results.Count > 0)
            return;

        for (int i = 0; i < lines.Count; i++)
        {
            var candidate = lines[i];
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.text))
                continue;

            if (candidate.trigger != context.trigger)
                continue;

            if (!AudienceMatches(candidate.audience, context.audience))
                continue;

            results.Add(candidate);
        }
    }

    public bool TryGetRandomLine(string topicId, DialogueContext context, out NpcDialogueLine line)
    {
        var buffer = new List<NpcDialogueLine>();
        GetCandidateLines(topicId, context, buffer);
        if (buffer.Count == 0)
        {
            line = null;
            return false;
        }

        line = buffer[UnityEngine.Random.Range(0, buffer.Count)];
        return line != null;
    }

    public bool TryGetWeightedRandomLine(string topicId, DialogueContext context, out NpcDialogueLine line)
    {
        var buffer = new List<NpcDialogueLine>();
        GetCandidateLines(topicId, context, buffer);
        if (buffer.Count == 0)
        {
            line = null;
            return false;
        }

        line = ChooseWeightedRandom(buffer);
        return line != null;
    }

    public NpcDialogueLine ChooseWeightedRandom(IReadOnlyList<NpcDialogueLine> candidates)
    {
        if (candidates == null || candidates.Count == 0)
            return null;

        int totalWeight = 0;
        for (int i = 0; i < candidates.Count; i++)
            totalWeight += Mathf.Max(1, candidates[i] != null ? candidates[i].weight : 0);

        if (totalWeight <= 0)
            return candidates[UnityEngine.Random.Range(0, candidates.Count)];

        int roll = UnityEngine.Random.Range(0, totalWeight);
        for (int i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (candidate == null)
                continue;

            roll -= Mathf.Max(1, candidate.weight);
            if (roll < 0)
                return candidate;
        }

        return candidates[candidates.Count - 1];
    }

    private static bool IsCandidateValid(NpcDialogueLine candidate, DialogueContext context, string normalizedTopic, bool requireTopic)
    {
        if (candidate == null || string.IsNullOrWhiteSpace(candidate.text))
            return false;

        if (requireTopic && Normalize(candidate.topic) != normalizedTopic)
            return false;

        if (context.trigger != 0 || requireTopic)
        {
            if (candidate.trigger != context.trigger)
                return false;
        }

        return AudienceMatches(candidate.audience, context.audience);
    }

    private static bool AudienceMatches(NpcDialogueAudience candidateAudience, NpcDialogueAudience requestedAudience)
    {
        return requestedAudience == NpcDialogueAudience.Any ||
               candidateAudience == NpcDialogueAudience.Any ||
               candidateAudience == requestedAudience;
    }

    private void EnsureLookup()
    {
        if (_byId != null)
            return;

        _byId = new Dictionary<string, NpcDialogueLine>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line == null || string.IsNullOrWhiteSpace(line.id))
                continue;

            _byId[Normalize(line.id)] = line;
        }
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }
}
