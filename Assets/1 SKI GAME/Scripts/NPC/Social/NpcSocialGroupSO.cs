using System;
using System.Collections.Generic;
using SkiGame.POI;
using UnityEngine;

[Serializable]
public sealed class NpcSocialSpeakerSlot
{
    public string speakerRole = "A";
    public string[] requiredTags;
}

[Serializable]
public sealed class NpcSocialDialogueEntry
{
    public string entryId;
    public NpcDialogueSequenceSO sequence;
    public NpcDialogueBankSO fallbackBank;
    public string[] fallbackTopics;
    public NpcSocialAnchorType[] allowedAnchorTypes;
    public string[] requiredAnchorTags;
    public DialogueRequirement[] requirements;
    [Min(0f)] public float weight = 1f;
    [Min(0f)] public float cooldownSeconds = 30f;
    public bool avoidImmediateRepeat = true;
    public string[] requiredContextKeys;

    public string SafeId => string.IsNullOrWhiteSpace(entryId)
        ? (sequence != null ? sequence.SafeId : "entry")
        : entryId.Trim();

    public bool HasPlayableContent =>
        sequence != null ||
        (fallbackTopics != null && fallbackTopics.Length > 0);
}

[CreateAssetMenu(fileName = "NpcSocialGroup", menuName = "SkiGame/NPC/Social Group")]
public sealed class NpcSocialGroupSO : ScriptableObject
{
    [SerializeField] private string groupId;
    [SerializeField] private string displayName;
    [SerializeField] private NpcSocialAnchorType[] allowedAnchorTypes;
    [SerializeField] private string[] requiredAnchorTags;
    [SerializeField] private string[] requiredActorTags;
    [SerializeField] private NpcSocialSpeakerSlot[] speakerSlots;
    [SerializeField, Min(1)] private int minSpeakers = 1;
    [SerializeField, Min(1)] private int maxSpeakers = 2;
    [SerializeField] private bool requiresMultipleSpeakers;
    [SerializeField, Min(0f)] private float weight = 1f;
    [SerializeField, Min(0f)] private float cooldownSeconds = 30f;
    [SerializeField, Min(0f)] private float globalCooldownSeconds = 10f;
    [SerializeField] private NpcDialogueSequenceSO sequence;
    [SerializeField] private NpcDialogueBankSO fallbackBank;
    [SerializeField] private string[] fallbackTopics;
    [SerializeField] private NpcDialogueAudience audience = NpcDialogueAudience.Group;
    [SerializeField] private DialogueRequirement[] requirements;
    [SerializeField] private bool canRepeat = true;
    [SerializeField] private bool avoidImmediateRepeat = true;
    [Header("Dialogue Entries")]
    [SerializeField] private NpcSocialDialogueEntry[] dialogueEntries;
    [Header("Identity / Population Metadata")]
    [SerializeField] private string[] socialTags;
    [SerializeField] private NpcSkierProfile.SkierArchetype[] preferredArchetypes;
    [SerializeField] private Vector2 skillRange = new Vector2(0f, 1f);
    [SerializeField] private string[] allowedRegionIds;
    [SerializeField] private POICategory[] preferredPoiCategories;
    [SerializeField] private NpcSocialAnchorType[] preferredAnchorTypes;
    [Header("Appearance Metadata")]
    [SerializeField, Tooltip("Optional data-driven profile used by runtime NPC social appearance generation.")]
    private NpcSocialAppearanceProfileSO appearanceProfile;
    [SerializeField] private NpcAppearancePresetSO[] appearancePresets;
    [SerializeField] private Color[] palette;
    [SerializeField] private bool applySharedPalette;
    [SerializeField, Range(0f, 1f)] private float paletteVariation = 0.15f;
    [Header("Dialogue Metadata")]
    [SerializeField] private NpcDialogueBankSO[] dialogueBanks;
    [SerializeField] private NpcDialogueSequenceSO[] commonSequences;
    [Header("Activity Preferences")]
    [SerializeField] private bool canSkiRuns = true;
    [SerializeField] private bool canRideLifts = true;
    [SerializeField] private bool canLoiterAtPoi = true;
    [SerializeField] private bool canSpectateRace = true;
    [SerializeField] private bool canVisitLandmarks = true;
    [SerializeField] private bool canPerformTricks;
    [SerializeField, Min(0f)] private float loiterWeight = 1f;
    [SerializeField, Min(0f)] private float runWeight = 1f;
    [SerializeField, Min(0f)] private float liftWeight = 1f;
    [SerializeField, Min(0f)] private float landmarkWeight = 0.5f;
    [SerializeField, Min(0f)] private float trickWeight = 0.1f;

    public string GroupId => string.IsNullOrWhiteSpace(groupId) ? name : groupId.Trim();
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim();
    public IReadOnlyList<NpcSocialAnchorType> AllowedAnchorTypes => allowedAnchorTypes;
    public IReadOnlyList<string> RequiredAnchorTags => requiredAnchorTags;
    public IReadOnlyList<string> RequiredActorTags => requiredActorTags;
    public IReadOnlyList<NpcSocialSpeakerSlot> SpeakerSlots => speakerSlots;
    public int MinSpeakers => requiresMultipleSpeakers ? Mathf.Max(2, minSpeakers) : Mathf.Max(1, minSpeakers);
    public int MaxSpeakers => Mathf.Max(MinSpeakers, maxSpeakers);
    public bool RequiresMultipleSpeakers => requiresMultipleSpeakers;
    public float Weight => weight;
    public float CooldownSeconds => cooldownSeconds;
    public float GlobalCooldownSeconds => globalCooldownSeconds;
    public NpcDialogueSequenceSO Sequence => sequence;
    public NpcDialogueBankSO FallbackBank => fallbackBank;
    public IReadOnlyList<string> FallbackTopics => fallbackTopics;
    public NpcDialogueAudience Audience => audience;
    public DialogueRequirement[] Requirements => requirements;
    public bool CanRepeat => canRepeat;
    public bool AvoidImmediateRepeat => avoidImmediateRepeat;
    public IReadOnlyList<NpcSocialDialogueEntry> DialogueEntries => dialogueEntries;
    public IReadOnlyList<string> SocialTags => socialTags;
    public IReadOnlyList<NpcSkierProfile.SkierArchetype> PreferredArchetypes => preferredArchetypes;
    public Vector2 SkillRange => skillRange;
    public IReadOnlyList<string> AllowedRegionIds => allowedRegionIds;
    public IReadOnlyList<POICategory> PreferredPoiCategories => preferredPoiCategories;
    public IReadOnlyList<NpcSocialAnchorType> PreferredAnchorTypes => preferredAnchorTypes;
    public NpcSocialAppearanceProfileSO AppearanceProfile => appearanceProfile;
    public IReadOnlyList<NpcAppearancePresetSO> AppearancePresets => appearancePresets;
    public IReadOnlyList<Color> Palette => palette;
    public bool ApplySharedPalette => applySharedPalette;
    public float PaletteVariation => paletteVariation;
    public IReadOnlyList<NpcDialogueBankSO> DialogueBanks => dialogueBanks;
    public IReadOnlyList<NpcDialogueSequenceSO> CommonSequences => commonSequences;
    public bool CanSkiRuns => canSkiRuns;
    public bool CanRideLifts => canRideLifts;
    public bool CanLoiterAtPoi => canLoiterAtPoi;
    public bool CanSpectateRace => canSpectateRace;
    public bool CanVisitLandmarks => canVisitLandmarks;
    public bool CanPerformTricks => canPerformTricks;
    public float LoiterWeight => loiterWeight;
    public float RunWeight => runWeight;
    public float LiftWeight => liftWeight;
    public float LandmarkWeight => landmarkWeight;
    public float TrickWeight => trickWeight;

    public bool AllowsAnchor(NpcSocialAnchor anchor)
    {
        if (anchor == null)
            return false;

        if (allowedAnchorTypes != null && allowedAnchorTypes.Length > 0)
        {
            bool typeMatched = false;
            for (int i = 0; i < allowedAnchorTypes.Length; i++)
            {
                if (allowedAnchorTypes[i] == anchor.AnchorType)
                {
                    typeMatched = true;
                    break;
                }
            }

            if (!typeMatched)
                return false;
        }

        if (requiredAnchorTags != null)
        {
            for (int i = 0; i < requiredAnchorTags.Length; i++)
            {
                if (!anchor.HasContextTag(requiredAnchorTags[i]))
                    return false;
            }
        }

        return true;
    }

    public bool AreRequirementsMet(DialogueContext context)
    {
        return DialogueRequirementEvaluator.AreMet(requirements, context);
    }

    public bool HasDialogueEntries => dialogueEntries != null && dialogueEntries.Length > 0;

    public bool IsEntryValid(NpcSocialDialogueEntry entry, NpcSocialAnchor anchor, DialogueContext context)
    {
        if (entry == null || !entry.HasPlayableContent || anchor == null)
            return false;

        if (!AnchorTypeAllowed(entry.allowedAnchorTypes, anchor.AnchorType))
            return false;

        if (!AnchorTagsMet(entry.requiredAnchorTags, anchor))
            return false;

        if (!DialogueRequirementEvaluator.AreMet(entry.requirements, context))
            return false;

        return RequiredContextKeysMet(entry.requiredContextKeys, context);
    }

    [ContextMenu("Validate Social Group")]
    public void ValidateGroup()
    {
        foreach (string warning in CollectValidationWarnings())
            Debug.LogWarning($"[{name}] {warning}", this);
    }

    public List<string> CollectValidationWarnings()
    {
        var warnings = new List<string>();

        if (!HasDialogueEntries && sequence == null && (fallbackTopics == null || fallbackTopics.Length == 0))
            warnings.Add("No sequence and no fallback topics configured.");

        if (minSpeakers > maxSpeakers)
            warnings.Add("minSpeakers is greater than maxSpeakers.");

        if (requiresMultipleSpeakers && minSpeakers < 2)
            warnings.Add("requiresMultipleSpeakers is true but minSpeakers is less than 2.");

        if (allowedAnchorTypes == null || allowedAnchorTypes.Length == 0)
            warnings.Add("No allowed anchor types configured; this group can run at any anchor.");

        return warnings;
    }

    private static bool AnchorTypeAllowed(NpcSocialAnchorType[] allowedTypes, NpcSocialAnchorType anchorType)
    {
        if (allowedTypes == null || allowedTypes.Length == 0)
            return true;

        for (int i = 0; i < allowedTypes.Length; i++)
        {
            if (allowedTypes[i] == anchorType)
                return true;
        }

        return false;
    }

    private static bool AnchorTagsMet(string[] tags, NpcSocialAnchor anchor)
    {
        if (tags == null)
            return true;

        for (int i = 0; i < tags.Length; i++)
        {
            if (!anchor.HasContextTag(tags[i]))
                return false;
        }

        return true;
    }

    private static bool RequiredContextKeysMet(string[] keys, DialogueContext context)
    {
        if (keys == null || keys.Length == 0)
            return true;

        for (int i = 0; i < keys.Length; i++)
        {
            string key = keys[i];
            if (string.IsNullOrWhiteSpace(key))
                continue;

            if (!HasContextValue(key.Trim(), context))
                return false;
        }

        return true;
    }

    private static bool HasContextValue(string key, DialogueContext context)
    {
        if (context.values != null && context.values.TryGetValue(key, out string dynamicValue) && !string.IsNullOrWhiteSpace(dynamicValue))
            return true;

        return key switch
        {
            "regionName" => !string.IsNullOrWhiteSpace(context.regionName),
            "nearbyRunName" => !string.IsNullOrWhiteSpace(context.nearbyRunName) && !string.Equals(context.nearbyRunName, "that run", StringComparison.OrdinalIgnoreCase),
            "nearbyRunDifficulty" => !string.IsNullOrWhiteSpace(context.nearbyRunDifficulty) && !string.Equals(context.nearbyRunDifficulty, "pretty serious", StringComparison.OrdinalIgnoreCase),
            "nearbyLiftName" => !string.IsNullOrWhiteSpace(context.nearbyLiftName) && !string.Equals(context.nearbyLiftName, "that lift", StringComparison.OrdinalIgnoreCase),
            "nearbyPoiName" => !string.IsNullOrWhiteSpace(context.nearbyPoiName) && !string.Equals(context.nearbyPoiName, "over there", StringComparison.OrdinalIgnoreCase),
            "raceName" => !string.IsNullOrWhiteSpace(context.raceName) && !string.Equals(context.raceName, "the race", StringComparison.OrdinalIgnoreCase),
            "kioskName" => !string.IsNullOrWhiteSpace(context.kioskName) && !string.Equals(context.kioskName, "the kiosk", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private void OnValidate()
    {
        minSpeakers = Mathf.Max(1, minSpeakers);
        maxSpeakers = Mathf.Max(minSpeakers, maxSpeakers);
        if (requiresMultipleSpeakers)
            minSpeakers = Mathf.Max(2, minSpeakers);
    }
}
