using System;
using System.Collections.Generic;
using UnityEngine;

public enum NpcSocialAnchorType
{
    Generic,
    Lodge,
    LiftQueue,
    LiftStation,
    Kiosk,
    RaceStart,
    MedicTent,
    RunOverlook,
    RunJunction,
    TerrainPark,
    ResortEntry,
    Viewpoint
}

[Serializable]
public sealed class SocialAnchorTokenOverride
{
    public string key;
    public string value;
}

[Serializable]
public sealed class SocialAnchorResolvedContext
{
    public string regionId;
    public string regionName;
    public string nearbyRunId;
    public string nearbyRunName;
    public string nearbyRunDifficulty;
    public string nearbyLiftId;
    public string nearbyLiftName;
    public string nearbyPoiId;
    public string nearbyPoiName;
    public string nearbyPoiCategory;
    public string raceId;
    public string raceName;
    public string kioskName;
    public DialogueContextValue[] tokenValues;
}

[DisallowMultipleComponent]
public sealed class NpcSocialAnchor : MonoBehaviour
{
    [SerializeField] private string anchorId;
    [SerializeField] private string displayName;
    [SerializeField] private NpcSocialAnchorType anchorType = NpcSocialAnchorType.Generic;
    [SerializeField] private Transform center;
    [SerializeField, Min(0.1f)] private float activationRadius = 25f;
    [SerializeField, Min(0.1f)] private float actorSearchRadius = 12f;
    [SerializeField, Min(0.1f)] private float playerRequiredRadius = 30f;
    [SerializeField] private bool requirePlayerNearby = true;
    [SerializeField] private bool requireLineOfSightToPlayer;
    [SerializeField] private bool allowSoloBarks = true;
    [SerializeField] private bool allowNpcToNpcExchanges = true;
    [SerializeField] private bool allowPopulationRequests = true;
    [SerializeField] private string[] contextTags;
    [SerializeField] private bool autoResolveContext = true;
    [SerializeField, Min(1f)] private float contextSearchRadius = 150f;
    [SerializeField] private bool refreshContextOnEnable = true;
    [SerializeField] private bool refreshContextAtRuntime = false;
    [SerializeField] private SocialAnchorTokenOverride[] customTokenOverrides;
    [Header("Occupancy")]
    [SerializeField] private int desiredMinActorsOverride = -1;
    [SerializeField] private int desiredMaxActorsOverride = -1;
    [SerializeField, Min(1)] private int hardMaxActors = 8;
    [SerializeField, Min(0f)] private float crowdingPenalty = 2f;
    [SerializeField, Min(0f)] private float revisitCooldownSeconds = 120f;
    [SerializeField] private bool allowOvercrowding;
    [SerializeField] private bool isCrowdLocation;
    [Header("Explicit Context Overrides")]
    [SerializeField] private string regionId;
    [SerializeField] private string regionName;
    [SerializeField] private string nearbyRunId;
    [SerializeField] private string nearbyRunName;
    [SerializeField] private string nearbyRunDifficulty;
    [SerializeField] private string nearbyLiftId;
    [SerializeField] private string nearbyLiftName;
    [SerializeField] private string nearbyPoiId;
    [SerializeField] private string nearbyPoiName;
    [SerializeField] private string raceId;
    [SerializeField] private string raceName;
    [SerializeField] private string kioskName;
    [SerializeField] private NpcSocialGroupSO[] allowedGroups;
    [SerializeField] private bool logDebug;
    [SerializeField, HideInInspector] private SocialAnchorResolvedContext resolvedContext = new();

    public string AnchorId => string.IsNullOrWhiteSpace(anchorId) ? name : anchorId.Trim();
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim();
    public NpcSocialAnchorType AnchorType => anchorType;
    public Transform Center => center != null ? center : transform;
    public float ActivationRadius => activationRadius;
    public float ActorSearchRadius => actorSearchRadius;
    public float PlayerRequiredRadius => playerRequiredRadius;
    public bool RequirePlayerNearby => requirePlayerNearby;
    public bool AllowSoloBarks => allowSoloBarks;
    public bool AllowNpcToNpcExchanges => allowNpcToNpcExchanges;
    public bool AllowPopulationRequests => allowPopulationRequests;
    public IReadOnlyList<NpcSocialGroupSO> AllowedGroups => allowedGroups;
    public bool LogDebug => logDebug;
    public bool AutoResolveContext => autoResolveContext;
    public float ContextSearchRadius => contextSearchRadius;
    public string RegionIdOverride => regionId;
    public string RegionNameOverride => regionName;
    public string NearbyRunIdOverride => nearbyRunId;
    public string NearbyRunNameOverride => nearbyRunName;
    public string NearbyRunDifficultyOverride => nearbyRunDifficulty;
    public string NearbyLiftIdOverride => nearbyLiftId;
    public string NearbyLiftNameOverride => nearbyLiftName;
    public string NearbyPoiIdOverride => nearbyPoiId;
    public string NearbyPoiNameOverride => nearbyPoiName;
    public string RaceIdOverride => raceId;
    public string RaceNameOverride => raceName;
    public string KioskNameOverride => kioskName;
    public IReadOnlyList<string> ContextTags => contextTags;
    public IReadOnlyList<SocialAnchorTokenOverride> CustomTokenOverrides => customTokenOverrides;
    public SocialAnchorResolvedContext ResolvedContext => resolvedContext;
    public int DesiredMinActorsOverride => desiredMinActorsOverride;
    public int DesiredMaxActorsOverride => desiredMaxActorsOverride;
    public int HardMaxActors => Mathf.Max(1, hardMaxActors);
    public float CrowdingPenalty => crowdingPenalty;
    public float RevisitCooldownSeconds => revisitCooldownSeconds;
    public bool AllowOvercrowding => allowOvercrowding;
    public bool IsCrowdLocation => isCrowdLocation;

    private void OnEnable()
    {
        if (center == null)
            center = transform;

        if (refreshContextOnEnable)
            RefreshContext();

        NpcSocialDirector.RegisterAnchor(this);
    }

    private void OnDisable()
    {
        NpcSocialDirector.UnregisterAnchor(this);
    }

    public bool HasContextTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return true;

        if (contextTags == null)
            return false;

        for (int i = 0; i < contextTags.Length; i++)
        {
            if (string.Equals(contextTags[i]?.Trim(), tag.Trim(), StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public bool IsPlayerInRange(Transform player)
    {
        if (!requirePlayerNearby)
            return true;

        if (player == null)
            return false;

        if (Vector3.Distance(Center.position, player.position) > playerRequiredRadius)
            return false;

        if (!requireLineOfSightToPlayer)
            return true;

        Vector3 origin = Center.position + Vector3.up;
        Vector3 target = player.position + Vector3.up;
        return !Physics.Linecast(origin, target, out _, ~0, QueryTriggerInteraction.Ignore);
    }

    public DialogueContext BuildDialogueContext(string topicId, NpcDialogueTrigger trigger, NpcDialogueAudience audience)
    {
        if (autoResolveContext && (refreshContextAtRuntime || resolvedContext == null))
            RefreshContext();

        SocialAnchorResolvedContext contextData = resolvedContext ?? new SocialAnchorResolvedContext();
        var values = new DialogueContextValueSet();
        AddValue(values, "anchorId", AnchorId);
        AddValue(values, "anchorName", DisplayName);
        AddValue(values, "anchorType", anchorType.ToString());
        AddValue(values, "anchorTags", contextTags != null ? string.Join(",", contextTags) : string.Empty);
        AddValue(values, "regionId", contextData.regionId);
        AddValue(values, "regionName", contextData.regionName);
        AddValue(values, "region", contextData.regionName);

        AddValue(values, "nearbyRunId", contextData.nearbyRunId);
        AddValue(values, "nearbyRun", contextData.nearbyRunName);
        AddValue(values, "nearbyRunName", contextData.nearbyRunName);
        AddValue(values, "nearbyRunDifficulty", contextData.nearbyRunDifficulty);
        AddValue(values, "runDifficulty", contextData.nearbyRunDifficulty);

        AddValue(values, "nearbyLiftId", contextData.nearbyLiftId);
        AddValue(values, "nearbyLift", contextData.nearbyLiftName);
        AddValue(values, "nearbyLiftName", contextData.nearbyLiftName);

        AddValue(values, "nearbyPoiId", contextData.nearbyPoiId);
        AddValue(values, "nearbyPoi", contextData.nearbyPoiName);
        AddValue(values, "nearbyPoiName", contextData.nearbyPoiName);
        AddValue(values, "nearbyPoiCategory", contextData.nearbyPoiCategory);

        AddValue(values, "raceId", contextData.raceId);
        AddValue(values, "raceName", contextData.raceName);
        AddValue(values, "kioskName", contextData.kioskName);

        if (contextData.tokenValues != null)
        {
            for (int i = 0; i < contextData.tokenValues.Length; i++)
            {
                var token = contextData.tokenValues[i];
                if (token != null && !string.IsNullOrWhiteSpace(token.key))
                    AddValue(values, token.key, token.value);
            }
        }

        if (customTokenOverrides != null)
        {
            for (int i = 0; i < customTokenOverrides.Length; i++)
            {
                var entry = customTokenOverrides[i];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.key))
                    AddValue(values, entry.key, entry.value);
            }
        }

        return new DialogueContext
        {
            topicId = topicId,
            trigger = trigger,
            audience = audience,
            anchorName = DisplayName,
            anchorType = anchorType.ToString(),
            regionName = FirstNonEmpty(contextData.regionName, contextData.regionId, "this area"),
            nearbyRunName = FirstNonEmpty(contextData.nearbyRunName, contextData.nearbyRunId, "that run"),
            nearbyRunDifficulty = FirstNonEmpty(contextData.nearbyRunDifficulty, "pretty serious"),
            nearbyLiftName = FirstNonEmpty(contextData.nearbyLiftName, contextData.nearbyLiftId, "that lift"),
            nearbyPoiName = FirstNonEmpty(contextData.nearbyPoiName, contextData.nearbyPoiId, "over there"),
            raceName = FirstNonEmpty(contextData.raceName, contextData.raceId, "the race"),
            kioskName = FirstNonEmpty(contextData.kioskName, "the kiosk"),
            values = values
        };
    }

    public void SetGeneratedIdentity(string id, string label, NpcSocialAnchorType type, string[] tags)
    {
        if (!string.IsNullOrWhiteSpace(id))
            anchorId = id.Trim();
        if (!string.IsNullOrWhiteSpace(label))
            displayName = label.Trim();
        anchorType = type;
        contextTags = tags;
        autoResolveContext = true;
    }

    [ContextMenu("Refresh Context Now")]
    public void RefreshContext()
    {
        resolvedContext = SocialAnchorContextResolver.Resolve(this);
    }

    [ContextMenu("Print Resolved Context")]
    public void PrintResolvedContext()
    {
        if (resolvedContext == null)
            RefreshContext();

        DialogueContext context = BuildDialogueContext(string.Empty, NpcDialogueTrigger.SocialReply, NpcDialogueAudience.Group);

        string resolvedRegionId = resolvedContext != null
            ? FirstNonEmpty(resolvedContext.regionId, TryGetContextValue(context, "regionId"), "none")
            : FirstNonEmpty(TryGetContextValue(context, "regionId"), "none");

        Debug.Log(
            $"[NpcSocialAnchor] {name} resolved context\n" +
            $"anchor={DisplayName} type={AnchorType} tags={string.Join(",", ContextTags ?? Array.Empty<string>())}\n" +
            $"region={FirstNonEmpty(context.regionName, "this area")} regionId={resolvedRegionId} " +
            $"run={context.nearbyRunName} difficulty={context.nearbyRunDifficulty} lift={context.nearbyLiftName}\n" +
            $"poi={context.nearbyPoiName} race={context.raceName} kiosk={context.kioskName}",
            this);
    }

    public void CollectNearbyActors(List<NpcSocialActor> results)
    {
        if (results == null)
            return;

        results.Clear();
        var actors = NpcSocialDirector.RegisteredActors;
        for (int i = 0; i < actors.Count; i++)
        {
            var actor = actors[i];
            if (actor == null)
                continue;

            if (Vector3.Distance(Center.position, actor.transform.position) <= actorSearchRadius)
                results.Add(actor);
        }
    }

    public int CountNearbySocialActors()
    {
        var actors = NpcSocialDirector.RegisteredActors;
        int count = 0;
        for (int i = 0; i < actors.Count; i++)
        {
            var actor = actors[i];
            if (actor != null && Vector3.Distance(Center.position, actor.transform.position) <= actorSearchRadius)
                count++;
        }

        return count;
    }

    public bool HasAvailableCapacity()
    {
        return allowOvercrowding || CountNearbySocialActors() < HardMaxActors;
    }

    public float GetCrowdingScore()
    {
        int count = CountNearbySocialActors();
        if (allowOvercrowding)
            return 0f;

        int softMax = ResolveSoftMaxActors();
        if (count <= softMax)
            return 0f;

        float over = count - softMax;
        float hardSpan = Mathf.Max(1f, HardMaxActors - softMax);
        return Mathf.Clamp01(over / hardSpan) * Mathf.Max(0f, crowdingPenalty);
    }

    public float GetCandidateWeightForActor(NpcSocialActor actor, NpcSkierBrain brain)
    {
        if (!allowOvercrowding && CountNearbySocialActors() >= HardMaxActors)
            return 0f;

        float weight = isCrowdLocation ? 1.35f : 1f;
        if (brain != null && brain.PreviousGenericIntentTargetAnchor == this && Time.time < brain.PreviousGenericIntentAnchorCooldownUntil)
            weight *= 0.1f;

        float crowdScore = GetCrowdingScore();
        if (crowdScore > 0f)
            weight /= 1f + crowdScore;

        return Mathf.Max(0f, weight);
    }

    public int ResolveSoftMinActors()
    {
        if (desiredMinActorsOverride >= 0)
            return desiredMinActorsOverride;

        return isCrowdLocation ? 3 : 1;
    }

    public int ResolveSoftMaxActors()
    {
        if (desiredMaxActorsOverride >= 0)
            return Mathf.Max(ResolveSoftMinActors(), desiredMaxActorsOverride);

        return isCrowdLocation ? Mathf.Max(4, HardMaxActors - 1) : Mathf.Min(HardMaxActors, 3);
    }

    [ContextMenu("Print Nearby Actors")]
    private void PrintNearbyActors()
    {
        var actors = new List<NpcSocialActor>();
        CollectNearbyActors(actors);
        Debug.Log($"[NpcSocialAnchor] {name}: nearbyActors={actors.Count} radius={actorSearchRadius:0.0}", this);
        for (int i = 0; i < actors.Count; i++)
            Debug.Log($"[NpcSocialAnchor] {name}: actor[{i}]={actors[i].name} available={actors[i].IsAvailableForSocialDialogue()}", actors[i]);
    }

    [ContextMenu("Print Occupancy Debug State")]
    public void PrintOccupancyDebugState()
    {
        int count = CountNearbySocialActors();
        Debug.Log(
            $"[NpcSocialAnchor] {name} occupancy\n" +
            $"display={DisplayName} type={AnchorType} count={count} desiredMin={ResolveSoftMinActors()} desiredMax={ResolveSoftMaxActors()} hardMax={HardMaxActors}\n" +
            $"available={HasAvailableCapacity()} crowdScore={GetCrowdingScore():0.00} crowdLocation={isCrowdLocation} allowOvercrowding={allowOvercrowding} revisitCooldown={revisitCooldownSeconds:0.0}s",
            this);
    }

    [ContextMenu("Print Candidate Weight Debug")]
    public void PrintCandidateWeightDebug()
    {
        Debug.Log(
            $"[NpcSocialAnchor] {name} candidate weight={GetCandidateWeightForActor(null, null):0.00} count={CountNearbySocialActors()} crowdScore={GetCrowdingScore():0.00}",
            this);
    }

    [ContextMenu("Force Disperse Nearby Actors")]
    public void ForceDisperseNearbyActors()
    {
        var actors = new List<NpcSocialActor>();
        CollectNearbyActors(actors);
        for (int i = 0; i < actors.Count; i++)
        {
            if (actors[i] != null && actors[i].TryGetComponent(out NpcSkierBrain brain))
                brain.ResumeFromSocialLoiter();
        }

        Debug.Log($"[NpcSocialAnchor] {name}: forced disperse for {actors.Count} nearby actors.", this);
    }

    [ContextMenu("Trigger Social Dialogue Here")]
    private void TriggerSocialDialogueHere()
    {
        if (NpcSocialDirector.Instance != null)
            NpcSocialDirector.Instance.TryTriggerSocialDialogueAtAnchor(this, bypassTiming: true);
    }

    [ContextMenu("Request Population")]
    private void RequestPopulation()
    {
        if (NpcSkierSpawner.Instance != null)
            NpcSkierSpawner.Instance.ForcePopulateSocialAnchor(this);
        else
            Debug.LogWarning($"[NpcSocialAnchor] {name}: No NpcSkierSpawner instance found.", this);
    }

    private static void AddValue(DialogueContextValueSet set, string key, string value)
    {
        if (set == null || string.IsNullOrWhiteSpace(key))
            return;

        string normalized = key.Trim();

        if (set.values == null)
            set.values = new List<DialogueContextValue>();

        for (int i = 0; i < set.values.Count; i++)
        {
            var entry = set.values[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                continue;

            if (string.Equals(entry.key.Trim(), normalized, StringComparison.OrdinalIgnoreCase))
            {
                entry.value = value ?? string.Empty;
                return;
            }
        }

        set.values.Add(new DialogueContextValue
        {
            key = normalized,
            value = value ?? string.Empty
        });
    }

    private static string TryGetContextValue(DialogueContext context, string key)
    {
        if (string.IsNullOrWhiteSpace(key) || context.values == null)
            return string.Empty;

        if (context.values.TryGetValue(key, out string value))
            return value ?? string.Empty;

        return string.Empty;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
                return values[i].Trim();
        }

        return string.Empty;
    }

    private void OnValidate()
    {
        if (center == null)
            center = transform;

        if (activationRadius <= 0f)
            Debug.LogWarning($"[{nameof(NpcSocialAnchor)}] {name} activation radius should be greater than zero.", this);

        if (actorSearchRadius <= 0f)
            Debug.LogWarning($"[{nameof(NpcSocialAnchor)}] {name} actor search radius should be greater than zero.", this);

        hardMaxActors = Mathf.Max(1, hardMaxActors);
        desiredMaxActorsOverride = desiredMaxActorsOverride >= 0
            ? Mathf.Max(desiredMinActorsOverride, desiredMaxActorsOverride)
            : desiredMaxActorsOverride;
    }
}
