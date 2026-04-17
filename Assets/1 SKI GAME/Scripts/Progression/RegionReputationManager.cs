using System;
using System.Collections.Generic;
using SkiGame.Progression;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RegionReputationManager : MonoBehaviour
{
    public static RegionReputationManager Instance { get; private set; }

    [SerializeField] private RegionReputationConfigSO config;
    [SerializeField] private PlayerStatsManager playerStatsManager;
    [SerializeField] private SkiResortAccessManager resortAccessManager;
    [SerializeField] private QuestSignalBus signalBus;

    private readonly List<RegionReputationConfigSO.QuestReputationRewardEntry> _questRewardBuffer =
        new List<RegionReputationConfigSO.QuestReputationRewardEntry>(8);

    public event Action<string, int> OnRegionReputationChanged;
    public event Action<string> OnRegionPermanentUnlockReached;

    public RegionReputationConfigSO Config => config;

    public static RegionReputationManager EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        RegionReputationManager existing = FindObjectOfType<RegionReputationManager>();
        if (existing != null)
            return existing;

        GameObject go = new GameObject("RegionReputationManager");
        return go.AddComponent<RegionReputationManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        ResolveReferences();
        EnsureProfileState();
        RefreshPermanentUnlocksFromProfile();
        QuestRegionReputationBridge.EnsureInstance();
    }

    public int GetReputation(string regionId)
    {
        PlayerStatsProfile.RegionReputationState state = GetOrCreateRegionState(regionId, createIfMissing: false);
        return state != null ? Mathf.Max(0, state.reputation) : 0;
    }

    public bool AwardReputation(string regionId, int amount, string source = null)
    {
        if (string.IsNullOrWhiteSpace(regionId) || amount <= 0)
            return false;

        PlayerStatsProfile.RegionReputationState state = GetOrCreateRegionState(regionId, createIfMissing: true);
        if (state == null)
            return false;

        state.reputation = Mathf.Max(0, state.reputation + amount);
        OnRegionReputationChanged?.Invoke(regionId.Trim(), state.reputation);
        RaiseReputationSignal(regionId.Trim(), amount, source);
        EvaluatePermanentUnlock(regionId.Trim(), state.reputation);
        SaveProfile();
        return true;
    }

    public bool TryAwardQuestCompletionReward(string questId)
    {
        ResolveReferences();

        if (config == null || string.IsNullOrWhiteSpace(questId))
            return false;

        if (!config.TryGetQuestRewards(questId, _questRewardBuffer))
            return false;

        bool changed = false;
        for (int i = 0; i < _questRewardBuffer.Count; i++)
        {
            RegionReputationConfigSO.QuestReputationRewardEntry reward = _questRewardBuffer[i];
            if (reward == null || string.IsNullOrWhiteSpace(reward.regionId) || reward.reputationReward <= 0)
                continue;

            changed |= AwardReputation(reward.regionId.Trim(), reward.reputationReward, $"quest:{questId.Trim()}");
        }

        return changed;
    }

    public bool TryGetDiscoveryReward(string regionId, out int reward)
    {
        reward = 0;
        ResolveReferences();

        RegionReputationConfigSO.RegionDefinition definition = config != null ? config.GetRegion(regionId) : null;
        if (definition == null)
            return false;

        reward = Mathf.Max(0, definition.discoveryReward);
        return reward > 0;
    }

    public void RefreshPermanentUnlocksFromProfile()
    {
        ResolveReferences();
        PlayerStatsProfile profile = EnsureProfileState();
        if (profile?.regionReputations == null)
            return;

        for (int i = 0; i < profile.regionReputations.Count; i++)
        {
            PlayerStatsProfile.RegionReputationState state = profile.regionReputations[i];
            if (state == null || string.IsNullOrWhiteSpace(state.regionId))
                continue;

            EvaluatePermanentUnlock(state.regionId.Trim(), Mathf.Max(0, state.reputation));
        }
    }

    private void EvaluatePermanentUnlock(string regionId, int currentValue)
    {
        ResolveReferences();

        if (config == null || resortAccessManager == null || string.IsNullOrWhiteSpace(regionId))
            return;

        RegionReputationConfigSO.RegionDefinition definition = config.GetRegion(regionId);
        if (definition == null || string.IsNullOrWhiteSpace(definition.linkedResortId))
            return;

        int threshold = Mathf.Max(0, definition.permanentUnlockThreshold);
        if (currentValue < threshold)
            return;

        if (resortAccessManager.TryUnlockPermanentResort(definition.linkedResortId.Trim(), out bool newlyUnlocked) && newlyUnlocked)
            OnRegionPermanentUnlockReached?.Invoke(regionId);
    }

    private PlayerStatsProfile EnsureProfileState()
    {
        ResolveReferences();
        if (playerStatsManager == null)
            return null;

        if (playerStatsManager.Profile == null)
            playerStatsManager.Load();

        PlayerStatsProfile profile = playerStatsManager.Profile;
        profile?.Sanitize();
        return profile;
    }

    private PlayerStatsProfile.RegionReputationState GetOrCreateRegionState(string regionId, bool createIfMissing)
    {
        if (string.IsNullOrWhiteSpace(regionId))
            return null;

        PlayerStatsProfile profile = EnsureProfileState();
        if (profile == null)
            return null;

        profile.regionReputations ??= new List<PlayerStatsProfile.RegionReputationState>();
        string normalized = regionId.Trim();

        for (int i = 0; i < profile.regionReputations.Count; i++)
        {
            PlayerStatsProfile.RegionReputationState state = profile.regionReputations[i];
            if (state != null && string.Equals(state.regionId, normalized, StringComparison.OrdinalIgnoreCase))
                return state;
        }

        if (!createIfMissing)
            return null;

        PlayerStatsProfile.RegionReputationState created = new PlayerStatsProfile.RegionReputationState
        {
            regionId = normalized,
            reputation = 0
        };

        profile.regionReputations.Add(created);
        return created;
    }

    private void ResolveReferences()
    {
        if (playerStatsManager == null)
            playerStatsManager = PlayerStatsManager.Instance != null
                ? PlayerStatsManager.Instance
                : FindObjectOfType<PlayerStatsManager>();

        if (resortAccessManager == null)
            resortAccessManager = SkiResortAccessManager.Instance != null
                ? SkiResortAccessManager.Instance
                : SkiResortAccessManager.EnsureInstance();

        if (signalBus == null)
            signalBus = FindObjectOfType<QuestSignalBus>();

        if (config == null)
        {
            RegionReputationConfigSO[] all = Resources.FindObjectsOfTypeAll<RegionReputationConfigSO>();
            if (all != null && all.Length > 0)
                config = all[0];
        }
    }

    private void SaveProfile()
    {
        if (playerStatsManager != null)
            playerStatsManager.Save();
    }

    private void RaiseReputationSignal(string regionId, int amount, string source)
    {
        if (signalBus == null)
            return;

        QuestSignalData data = QuestSignalData.Create()
            .WithNumeric(amount)
            .WithTag("regionId", regionId);

        if (!string.IsNullOrWhiteSpace(source))
            data.WithTag("source", source.Trim());

        signalBus.RaiseEvent("progression.region_reputation_gained", data);
    }
}
