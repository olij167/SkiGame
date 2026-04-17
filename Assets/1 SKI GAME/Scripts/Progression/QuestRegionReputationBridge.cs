using System;
using SkiGame.Progression;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class QuestRegionReputationBridge : MonoBehaviour
{
    public static QuestRegionReputationBridge Instance { get; private set; }

    [SerializeField] private QuestSignalBus signalBus;
    [SerializeField] private RegionReputationManager reputationManager;

    public static QuestRegionReputationBridge EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        QuestRegionReputationBridge existing = FindObjectOfType<QuestRegionReputationBridge>();
        if (existing != null)
            return existing;

        GameObject go = new GameObject("QuestRegionReputationBridge");
        return go.AddComponent<QuestRegionReputationBridge>();
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
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (signalBus != null)
            signalBus.OnEventRaised += HandleSignalRaised;
    }

    private void OnDisable()
    {
        if (signalBus != null)
            signalBus.OnEventRaised -= HandleSignalRaised;
    }

    private void HandleSignalRaised(QuestSignalRecord record)
    {
        if (!string.Equals(record.Key, "quest.completed", StringComparison.OrdinalIgnoreCase))
            return;

        ResolveReferences();
        if (reputationManager == null)
            return;

        string questId = record.Data != null ? record.Data.GetTag("questId") : string.Empty;
        if (!string.IsNullOrWhiteSpace(questId))
            reputationManager.TryAwardQuestCompletionReward(questId.Trim());
    }

    private void ResolveReferences()
    {
        if (signalBus == null)
            signalBus = FindObjectOfType<QuestSignalBus>();

        if (reputationManager == null)
            reputationManager = RegionReputationManager.Instance != null
                ? RegionReputationManager.Instance
                : RegionReputationManager.EnsureInstance();
    }
}
