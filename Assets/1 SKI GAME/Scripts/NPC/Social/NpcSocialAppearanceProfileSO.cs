using System;
using System.Collections.Generic;
using UnityEngine;

public enum NpcAppearanceElement
{
    Skin,
    Eyes,
    Hat,
    Jacket,
    Gloves,
    Boots,
    Accessory,
    Skis,
    Poles
}

[Flags]
public enum NpcAppearanceElementMask
{
    None = 0,
    Skin = 1 << 0,
    Eyes = 1 << 1,
    Hat = 1 << 2,
    Jacket = 1 << 3,
    Gloves = 1 << 4,
    Boots = 1 << 5,
    Accessory = 1 << 6,
    Skis = 1 << 7,
    Poles = 1 << 8,

    SkinPattern = Skin,
    EyeIcon = Eyes,

    Wearables = Hat | Jacket | Gloves | Boots | Accessory,
    Gear = Skis | Poles,
    ClothingAndGear = Wearables | Gear,
    All = Skin | Eyes | Hat | Jacket | Gloves | Boots | Accessory | Skis | Poles
}

public enum NpcAppearanceColourTarget
{
    Primary,
    Secondary,
    Eye,
    Skin
}

public enum NpcColourHarmonyMode
{
    None,
    Monochromatic,
    Analogous,
    Complementary,
    SplitComplementary,
    Triadic,
    Tetradic,
    Square
}

[Serializable]
public sealed class NpcAppearanceOptionPoolRule
{
    public NpcAppearanceElementMask elements;
    public CustomizationOptionSO[] allowedOptions;
    [Range(0f, 1f)] public float equipChance = 1f;
    public bool includeCatalogueOptionsOfMatchingType = false;
}

[Serializable]
public struct NpcHSVRange
{
    [Range(0f, 1f)] public float hueMin;
    [Range(0f, 1f)] public float hueMax;
    [Range(0f, 1f)] public float saturationMin;
    [Range(0f, 1f)] public float saturationMax;
    [Range(0f, 1f)] public float valueMin;
    [Range(0f, 1f)] public float valueMax;
}

[Serializable]
public sealed class NpcAppearanceColourRule
{
    public string label;
    public NpcAppearanceElementMask elements;
    public NpcAppearanceColourTarget target = NpcAppearanceColourTarget.Primary;
    public string channelId = "secondary";
    public CustomizationOptionSO[] specificOptions;
    public NpcHSVRange[] ranges;
    public bool useHarmonyOffset;
    public int harmonySlot;
    [Range(0f, 1f)] public float weight = 1f;
}

[Serializable]
public sealed class NpcColourTargetRange
{
    public string label;
    public NpcHSVRange[] anchorRanges;
    [Range(0f, 1f)] public float rangeWeight = 1f;
}

[Serializable]
public sealed class NpcHarmonySlotRule
{
    [Tooltip("Elements that should use this harmony slot by default.")]
    public NpcAppearanceElementMask elements;
    public int harmonySlot;
    [Range(0f, 2f)] public float saturationScale = 1f;
    [Range(0f, 2f)] public float valueScale = 1f;
    [Range(0f, 1f)] public float hueJitter = 0.02f;
}

[Serializable]
public sealed class NpcElementColourOverride
{
    public NpcAppearanceElementMask elements;

    [Header("Primary")]
    public bool overridePrimary;
    public NpcHSVRange[] primaryRanges;
    public int primaryHarmonySlot;
    [Range(0f, 1f)] public float primaryHueJitter = 0.02f;

    [Header("Secondary")]
    public bool overrideSecondary;
    public string secondaryChannelId = "secondary";
    public NpcHSVRange[] secondaryRanges;
    public int secondaryHarmonySlot;
    [Range(0f, 1f)] public float secondaryHueJitter = 0.02f;
}

[Serializable]
public sealed class NpcSocialEquipmentChances
{
    [Tooltip("When enabled, the generator component's equipment chances are used.")]
    public bool useGeneratorChances = true;
    [Range(0f, 1f)] public float hatChance = 0.75f;
    [Range(0f, 1f)] public float jacketChance = 0.9f;
    [Range(0f, 1f)] public float glovesChance = 0.8f;
    [Range(0f, 1f)] public float bootsChance = 0.9f;
    [Range(0f, 1f)] public float accessoryChance = 0.45f;
}

[Serializable]
public sealed class NpcSocialColourPlan
{
    [Header("Harmony")]
    public NpcColourHarmonyMode harmonyMode = NpcColourHarmonyMode.Analogous;
    public bool randomizeHarmonyMode;
    public NpcColourHarmonyMode[] allowedHarmonyModes;

    [Header("Anchor")]
    [Tooltip("Base hue/saturation/value ranges used to seed clothing and gear harmony.")]
    public NpcHSVRange[] anchorRanges;

    [Header("Global Target Ranges")]
    public NpcHSVRange[] skinRanges;
    public NpcHSVRange[] eyeRanges;
    public NpcHSVRange[] primaryRanges;
    public NpcHSVRange[] secondaryRanges;

    [Header("Default Harmony Slots")]
    public NpcHarmonySlotRule[] harmonySlots;

    [Header("Per-Element Overrides")]
    public NpcElementColourOverride[] elementOverrides;
}

[CreateAssetMenu(fileName = "NpcSocialAppearanceProfile", menuName = "SkiGame/NPC/Social Appearance Profile")]
public sealed class NpcSocialAppearanceProfileSO : ScriptableObject
{
    [Header("Catalogue")]
    [Tooltip("Catalogue used as this group's complete option pool. If empty, the generator's default catalogue is used.")]
    [SerializeField] private CustomizationCatalogSO catalogOverride;

    [Header("Preset Seeds")]
    [Tooltip("Optional preset pool used as complete appearances or seeds. Skin patterns from presets are ignored by NPC appearance application.")]
    [SerializeField] private NpcAppearancePresetSO[] basePresets;
    [SerializeField] private bool usePresetAsFullAppearance;
    [SerializeField] private bool fillMissingPresetSlotsFromCatalogue = true;

    [Header("Equipment Chances")]
    [SerializeField] private NpcSocialEquipmentChances equipmentChances = new();

    [Header("Colour Plan")]
    [SerializeField] private NpcSocialColourPlan colourPlan = new();

    [SerializeField, HideInInspector] private NpcAppearanceOptionPoolRule[] optionPoolRules;
    [SerializeField, HideInInspector] private NpcAppearanceColourRule[] colourRules;
    [SerializeField, HideInInspector] private bool useHarmonyFallback = true;
    [SerializeField, HideInInspector] private NpcSkierAppearanceGenerator.HarmonyMode[] allowedHarmonyModes;
    [SerializeField, HideInInspector, Range(0f, 1f)] private float colourVariance = 0.18f;
    [SerializeField, HideInInspector, Range(0f, 1f)] private float paletteVariation = 0.15f;

    public CustomizationCatalogSO CatalogOverride => catalogOverride;
    public NpcAppearancePresetSO[] BasePresets => basePresets;
    public bool UsePresetAsFullAppearance => usePresetAsFullAppearance;
    public bool FillMissingPresetSlotsFromPools => fillMissingPresetSlotsFromCatalogue;
    public bool FillMissingPresetSlotsFromCatalogue => fillMissingPresetSlotsFromCatalogue;
    public NpcSocialEquipmentChances EquipmentChances => equipmentChances;
    public NpcSocialColourPlan ColourPlan => colourPlan;

    [Obsolete("Social appearance profiles now use CatalogOverride as the option pool. This legacy data is not used at runtime.")]
    public NpcAppearanceOptionPoolRule[] OptionPoolRules => optionPoolRules;

    [Obsolete("Use ColourPlan instead. This legacy data is not used at runtime.")]
    public NpcAppearanceColourRule[] ColourRules => colourRules;

    [Obsolete("Harmony is now part of ColourPlan.")]
    public bool UseHarmonyFallback => useHarmonyFallback;

    [Obsolete("Use ColourPlan.allowedHarmonyModes instead.")]
    public NpcSkierAppearanceGenerator.HarmonyMode[] AllowedHarmonyModes => allowedHarmonyModes;

    [Obsolete("Use ColourPlan ranges and jitter settings instead.")]
    public float ColourVariance => colourVariance;

    [Obsolete("Use ColourPlan ranges and jitter settings instead.")]
    public float PaletteVariation => paletteVariation;

    [ContextMenu("Validate Profile")]
    public void ValidateProfile()
    {
        foreach (string warning in CollectValidationWarnings())
            Debug.LogWarning($"[{name}] {warning}", this);
    }

    public List<string> CollectValidationWarnings()
    {
        var warnings = new List<string>();

        if (catalogOverride == null)
            warnings.Add("No catalogue assigned; NPCs will use the generator fallback catalogue.");

        if (colourPlan == null)
        {
            warnings.Add("No colour plan assigned.");
            return warnings;
        }

        if (IsEmpty(colourPlan.anchorRanges))
            warnings.Add("Colour plan has no anchor ranges; a default anchor colour will be used.");
        if (IsEmpty(colourPlan.skinRanges))
            warnings.Add("Colour plan has no skin ranges; generator skin colour fallback will be used.");
        if (IsEmpty(colourPlan.eyeRanges))
            warnings.Add("Colour plan has no eye ranges; generator eye colour fallback will be used.");
        if (IsEmpty(colourPlan.primaryRanges))
            warnings.Add("Colour plan has no primary ranges; anchor colour fallback will be used for clothing/gear.");

        if (colourPlan.harmonySlots != null)
        {
            for (int i = 0; i < colourPlan.harmonySlots.Length; i++)
            {
                var rule = colourPlan.harmonySlots[i];
                if (rule != null && rule.elements == NpcAppearanceElementMask.None)
                    warnings.Add($"Harmony slot rule {i} targets no elements.");
            }
        }

        if (colourPlan.elementOverrides != null)
        {
            for (int i = 0; i < colourPlan.elementOverrides.Length; i++)
            {
                var rule = colourPlan.elementOverrides[i];
                if (rule == null)
                    continue;

                if (rule.elements == NpcAppearanceElementMask.None)
                    warnings.Add($"Element override {i} targets no elements.");
                if (rule.overridePrimary && IsEmpty(rule.primaryRanges))
                    warnings.Add($"Element override {i} overrides primary but has no primary ranges.");
                if (rule.overrideSecondary && string.IsNullOrWhiteSpace(rule.secondaryChannelId))
                    warnings.Add($"Element override {i} overrides secondary but has a blank channel id.");
            }
        }

        return warnings;
    }

    [ContextMenu("Create Basic Racer Colour Plan")]
    public void CreateBasicRacerColourPlan()
    {
        colourPlan = new NpcSocialColourPlan
        {
            harmonyMode = NpcColourHarmonyMode.Complementary,
            anchorRanges = new[] { Range(0.55f, 0.68f, 0.65f, 1f, 0.55f, 0.95f) },
            skinRanges = DefaultSkinRanges(),
            eyeRanges = DefaultEyeRanges(),
            primaryRanges = new[] { Range(0.55f, 0.68f, 0.55f, 1f, 0.45f, 0.95f) },
            secondaryRanges = new[] { Range(0.0f, 1f, 0.2f, 0.8f, 0.35f, 0.95f) }
        };
    }

    [ContextMenu("Create Warm Tourist Colour Plan")]
    public void CreateWarmTouristColourPlan()
    {
        colourPlan = new NpcSocialColourPlan
        {
            harmonyMode = NpcColourHarmonyMode.Analogous,
            anchorRanges = new[] { Range(0.02f, 0.14f, 0.35f, 0.85f, 0.55f, 0.95f) },
            skinRanges = DefaultSkinRanges(),
            eyeRanges = DefaultEyeRanges(),
            primaryRanges = new[] { Range(0.02f, 0.18f, 0.3f, 0.8f, 0.45f, 0.95f) },
            secondaryRanges = new[] { Range(0.08f, 0.22f, 0.2f, 0.7f, 0.5f, 1f) }
        };
    }

    [ContextMenu("Create Lift Attendant Colour Plan")]
    public void CreateLiftAttendantColourPlan()
    {
        colourPlan = new NpcSocialColourPlan
        {
            harmonyMode = NpcColourHarmonyMode.SplitComplementary,
            anchorRanges = new[] { Range(0.57f, 0.64f, 0.45f, 0.9f, 0.35f, 0.8f) },
            skinRanges = DefaultSkinRanges(),
            eyeRanges = DefaultEyeRanges(),
            primaryRanges = new[] { Range(0.57f, 0.64f, 0.35f, 0.85f, 0.3f, 0.8f) },
            secondaryRanges = new[] { Range(0.10f, 0.16f, 0.45f, 0.9f, 0.55f, 1f) }
        };
    }

    private static bool IsEmpty(NpcHSVRange[] ranges) => ranges == null || ranges.Length == 0;

    private static NpcHSVRange[] DefaultSkinRanges()
    {
        return new[] { Range(0.05f, 0.12f, 0.18f, 0.55f, 0.35f, 0.95f) };
    }

    private static NpcHSVRange[] DefaultEyeRanges()
    {
        return new[] { Range(0f, 1f, 0.12f, 0.75f, 0.18f, 0.85f) };
    }

    private static NpcHSVRange Range(float hMin, float hMax, float sMin, float sMax, float vMin, float vMax)
    {
        return new NpcHSVRange
        {
            hueMin = hMin,
            hueMax = hMax,
            saturationMin = sMin,
            saturationMax = sMax,
            valueMin = vMin,
            valueMax = vMax
        };
    }
}
