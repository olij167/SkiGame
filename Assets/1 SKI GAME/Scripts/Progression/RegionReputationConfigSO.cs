using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "SkiGame/Progression/Region Reputation Config", fileName = "RegionReputationConfig")]
public sealed class RegionReputationConfigSO : ScriptableObject
{
    [Serializable]
    public sealed class RegionDefinition
    {
        public string regionId = string.Empty;
        public string linkedResortId = string.Empty;
        [Min(0)] public int permanentUnlockThreshold = 100;
        [Min(0)] public int discoveryReward = 5;
        [Min(0)] public int categoryCompletionBonus = 0;
        public List<SkiGame.Progression.QuestDefinitionSO> featuredQuestDefinitions = new List<SkiGame.Progression.QuestDefinitionSO>();
    }

    [Serializable]
    public sealed class QuestReputationRewardEntry
    {
        public string questId = string.Empty;
        public string regionId = string.Empty;
        [Min(0)] public int reputationReward = 25;
    }

    [SerializeField] private List<RegionDefinition> regions = new List<RegionDefinition>();
    [SerializeField] private List<QuestReputationRewardEntry> questRewards = new List<QuestReputationRewardEntry>();

    public IReadOnlyList<RegionDefinition> Regions => regions;
    public IReadOnlyList<QuestReputationRewardEntry> QuestRewards => questRewards;

    public RegionDefinition GetRegion(string regionId)
    {
        if (string.IsNullOrWhiteSpace(regionId) || regions == null)
            return null;

        string normalized = regionId.Trim();
        for (int i = 0; i < regions.Count; i++)
        {
            RegionDefinition definition = regions[i];
            if (definition == null || string.IsNullOrWhiteSpace(definition.regionId))
                continue;

            if (string.Equals(definition.regionId.Trim(), normalized, StringComparison.OrdinalIgnoreCase))
                return definition;
        }

        return null;
    }

    public bool TryGetQuestRewards(string questId, List<QuestReputationRewardEntry> buffer)
    {
        if (buffer == null)
            return false;

        buffer.Clear();

        if (string.IsNullOrWhiteSpace(questId) || questRewards == null)
            return false;

        string normalized = questId.Trim();
        for (int i = 0; i < questRewards.Count; i++)
        {
            QuestReputationRewardEntry entry = questRewards[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.questId))
                continue;

            if (string.Equals(entry.questId.Trim(), normalized, StringComparison.OrdinalIgnoreCase))
                buffer.Add(entry);
        }

        return buffer.Count > 0;
    }
}
