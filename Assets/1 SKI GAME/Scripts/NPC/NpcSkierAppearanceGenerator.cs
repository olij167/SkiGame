using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class NpcSkierAppearanceGenerator : MonoBehaviour
{
    public enum HarmonyMode
    {
        Monochromatic,
        Analogous,
        Complementary,
        SplitComplementary,
        Triadic
    }

    private struct SocialColourContext
    {
        public float anchorHue;
        public float anchorSaturation;
        public float anchorValue;
        public NpcColourHarmonyMode harmonyMode;
    }

    [Header("Catalog")]
    [SerializeField] private CustomizationCatalogSO catalog;

    [Header("References")]
    [SerializeField] private CharacterCustomizer customizer;
    [SerializeField] private SkiGearLoadout gearLoadout;
    [SerializeField] private SkiController skiController;
    [SerializeField] private NpcAppearancePresetApplier appearanceApplier;

    [Header("Selection Chances")]
    [Range(0f, 1f)][SerializeField] private float hatChance = 0.75f;
    [Range(0f, 1f)][SerializeField] private float jacketChance = 0.9f;
    [Range(0f, 1f)][SerializeField] private float glovesChance = 0.8f;
    [Range(0f, 1f)][SerializeField] private float bootsChance = 0.9f;
    [Range(0f, 1f)][SerializeField] private float accessoryChance = 0.45f;

    [Header("Palette")]
    [SerializeField] private HarmonyMode harmonyMode = HarmonyMode.Analogous;
    [SerializeField] private bool randomizeHarmonyModeOnApply = true;
    [Range(0f, 1f)][SerializeField] private float colourVariance = 0.18f;
    [Range(0f, 1f)][SerializeField] private float saturationBias = 0.8f;
    [Range(0f, 1f)][SerializeField] private float valueBias = 0.82f;
    [Range(0f, 1f)][SerializeField] private float useDefaultTintBias = 0.2f;

    [Header("Skin")]
    [Range(0f, 1f)][SerializeField] private float skinToneWarmthBias = 0.5f;

    private readonly List<CustomizationOptionSO> _eyeIcons = new();
    private readonly List<CustomizationOptionSO> _hats = new();
    private readonly List<CustomizationOptionSO> _jackets = new();
    private readonly List<CustomizationOptionSO> _gloves = new();
    private readonly List<CustomizationOptionSO> _boots = new();
    private readonly List<CustomizationOptionSO> _accessories = new();
    private readonly List<CustomizationOptionSO> _skis = new();
    private readonly List<CustomizationOptionSO> _poles = new();
    private static readonly HashSet<string> WarnedProfileIssues = new();

    public void ApplyRandomAppearance(NpcSkierProfile profile = null)
    {
        ApplyRandomAppearance(profile, null);
    }

    public void ApplyRandomAppearance(NpcSkierProfile profile, NpcSocialAppearanceProfileSO socialAppearance)
    {
        CacheRefs();

        CustomizationCatalogSO activeCatalog = ResolveCatalog(socialAppearance);
        RebuildPools(activeCatalog);

        if (activeCatalog == null || customizer == null)
        {
            Debug.LogWarning($"[{nameof(NpcSkierAppearanceGenerator)}] Missing catalog or customizer on '{name}'.", this);
            return;
        }

        if (appearanceApplier == null)
            appearanceApplier = GetComponent<NpcAppearancePresetApplier>() ?? gameObject.AddComponent<NpcAppearancePresetApplier>();

        if (socialAppearance != null)
            ApplySocialAppearance(profile, socialAppearance);
        else
            ApplyLegacyRandomAppearance(profile);
    }

    private void ApplySocialAppearance(NpcSkierProfile profile, NpcSocialAppearanceProfileSO socialAppearance)
    {
        NpcAppearancePresetSO seedPreset = PickPreset(socialAppearance);
        if (seedPreset != null && socialAppearance.UsePresetAsFullAppearance)
        {
            appearanceApplier.ApplyData(NpcAppearancePresetApplier.BuildDataFromPreset(seedPreset));
            return;
        }

        var data = seedPreset != null
            ? NpcAppearancePresetApplier.BuildDataFromPreset(seedPreset)
            : new NpcAppearancePresetApplier.AppearanceSelectionData();

        data.skinPatternOption = null;

        bool fillMissing = seedPreset == null || socialAppearance.FillMissingPresetSlotsFromCatalogue;
        if (fillMissing)
            FillMissingOptionsFromCatalogue(data, socialAppearance);

        SocialColourContext context = ResolveSocialColourContext(socialAppearance);
        NpcSocialColourPlan colourPlan = socialAppearance.ColourPlan;

        data.skinColor = ResolveSkinColour(colourPlan, context);
        data.eyeColor = ResolveEyeColour(colourPlan, context, data.skinColor);
        data.eyeOutlineColor = new Color(0f, 0f, 0f, 0f);

        data.hatColor = ResolveElementPrimaryColour(colourPlan, NpcAppearanceElementMask.Hat, context);
        data.jacketColor = ResolveElementPrimaryColour(colourPlan, NpcAppearanceElementMask.Jacket, context);
        data.glovesColor = ResolveElementPrimaryColour(colourPlan, NpcAppearanceElementMask.Gloves, context);
        data.bootsColor = ResolveElementPrimaryColour(colourPlan, NpcAppearanceElementMask.Boots, context);
        data.accessoryColor = ResolveElementPrimaryColour(colourPlan, NpcAppearanceElementMask.Accessory, context);
        data.skisColor = ResolveElementPrimaryColour(colourPlan, NpcAppearanceElementMask.Skis, context);
        data.polesColor = ResolveElementPrimaryColour(colourPlan, NpcAppearanceElementMask.Poles, context);

        FillMissingPatterns(data);
        ApplySecondaryChannelOverride(socialAppearance, colourPlan, NpcAppearanceElementMask.Hat, data.hatOption, data.hatChannelColors, context);
        ApplySecondaryChannelOverride(socialAppearance, colourPlan, NpcAppearanceElementMask.Jacket, data.jacketOption, data.jacketChannelColors, context);
        ApplySecondaryChannelOverride(socialAppearance, colourPlan, NpcAppearanceElementMask.Gloves, data.glovesOption, data.gloveChannelColors, context);
        ApplySecondaryChannelOverride(socialAppearance, colourPlan, NpcAppearanceElementMask.Boots, data.bootsOption, data.bootChannelColors, context);
        ApplySecondaryChannelOverride(socialAppearance, colourPlan, NpcAppearanceElementMask.Accessory, data.accessoryOption, data.accessoryChannelColors, context);

        appearanceApplier.ApplyData(data);
    }

    private void ApplyLegacyRandomAppearance(NpcSkierProfile profile)
    {
        HarmonyMode effectiveHarmony = randomizeHarmonyModeOnApply
            ? (HarmonyMode)Random.Range(0, System.Enum.GetValues(typeof(HarmonyMode)).Length)
            : harmonyMode;

        Color skinColor = GenerateSkinColor();
        Color eyeColor = GenerateEyeColor(skinColor);
        BuildPalette(effectiveHarmony, profile, out Color hatCol, out Color jacketCol, out Color glovesCol, out Color bootsCol, out Color accessoryCol, out Color skisCol, out Color polesCol);

        var data = new NpcAppearancePresetApplier.AppearanceSelectionData
        {
            skinPatternOption = null,
            skinColor = skinColor,
            eyeOption = PickRandom(_eyeIcons),
            eyeColor = eyeColor,
            eyeOutlineColor = new Color(0f, 0f, 0f, 0f),
            hatOption = Random.value <= hatChance ? PickRandom(_hats) : null,
            jacketOption = Random.value <= jacketChance ? PickRandom(_jackets) : null,
            glovesOption = Random.value <= glovesChance ? PickRandom(_gloves) : null,
            bootsOption = Random.value <= bootsChance ? PickRandom(_boots) : null,
            accessoryOption = Random.value <= accessoryChance ? PickRandom(_accessories) : null,
            skisOption = PickRandom(_skis),
            polesOption = PickRandom(_poles)
        };

        data.hatColor = ResolveTint(data.hatOption, hatCol);
        data.jacketColor = ResolveTint(data.jacketOption, jacketCol);
        data.glovesColor = ResolveTint(data.glovesOption, glovesCol);
        data.bootsColor = ResolveTint(data.bootsOption, bootsCol);
        data.accessoryColor = ResolveTint(data.accessoryOption, accessoryCol);
        data.skisColor = ResolveTint(data.skisOption, skisCol);
        data.polesColor = ResolveTint(data.polesOption, polesCol);

        FillMissingPatterns(data);
        ApplyLegacySecondaryChannelOverride(data.jacketOption, data.jacketColor, data.jacketChannelColors);
        ApplyLegacySecondaryChannelOverride(data.hatOption, data.hatColor, data.hatChannelColors);
        ApplyLegacySecondaryChannelOverride(data.glovesOption, data.glovesColor, data.gloveChannelColors);
        ApplyLegacySecondaryChannelOverride(data.bootsOption, data.bootsColor, data.bootChannelColors);
        ApplyLegacySecondaryChannelOverride(data.accessoryOption, data.accessoryColor, data.accessoryChannelColors);

        appearanceApplier.ApplyData(data);
    }

    private void CacheRefs()
    {
        if (customizer == null) customizer = GetComponentInChildren<CharacterCustomizer>(true);
        if (gearLoadout == null) gearLoadout = GetComponentInChildren<SkiGearLoadout>(true);
        if (skiController == null) skiController = GetComponent<SkiController>();
        if (appearanceApplier == null) appearanceApplier = GetComponent<NpcAppearancePresetApplier>();
    }

    private CustomizationCatalogSO ResolveCatalog(NpcSocialAppearanceProfileSO socialAppearance)
    {
        return socialAppearance != null && socialAppearance.CatalogOverride != null ? socialAppearance.CatalogOverride : catalog;
    }

    private void RebuildPools(CustomizationCatalogSO activeCatalog)
    {
        _eyeIcons.Clear();
        _hats.Clear();
        _jackets.Clear();
        _gloves.Clear();
        _boots.Clear();
        _accessories.Clear();
        _skis.Clear();
        _poles.Clear();

        if (activeCatalog == null || activeCatalog.options == null)
            return;

        for (int i = 0; i < activeCatalog.options.Count; i++)
        {
            var option = activeCatalog.options[i];
            if (option == null || option.type == CustomizationOptionType.SkinPattern)
                continue;

            GetPool(option.type)?.Add(option);
        }
    }

    private void FillMissingOptionsFromCatalogue(NpcAppearancePresetApplier.AppearanceSelectionData data, NpcSocialAppearanceProfileSO socialAppearance)
    {
        data.skinPatternOption = null;

        NpcSocialEquipmentChances chances = socialAppearance != null ? socialAppearance.EquipmentChances : null;
        data.eyeOption ??= PickRandom(_eyeIcons);
        data.hatOption ??= Random.value <= GetEquipChance(chances, NpcAppearanceElementMask.Hat) ? PickRandom(_hats) : null;
        data.jacketOption ??= Random.value <= GetEquipChance(chances, NpcAppearanceElementMask.Jacket) ? PickRandom(_jackets) : null;
        data.glovesOption ??= Random.value <= GetEquipChance(chances, NpcAppearanceElementMask.Gloves) ? PickRandom(_gloves) : null;
        data.bootsOption ??= Random.value <= GetEquipChance(chances, NpcAppearanceElementMask.Boots) ? PickRandom(_boots) : null;
        data.accessoryOption ??= Random.value <= GetEquipChance(chances, NpcAppearanceElementMask.Accessory) ? PickRandom(_accessories) : null;
        data.skisOption ??= PickRandom(_skis);
        data.polesOption ??= PickRandom(_poles);
    }

    private float GetEquipChance(NpcSocialEquipmentChances chances, NpcAppearanceElementMask element)
    {
        if (chances == null || chances.useGeneratorChances)
        {
            return element switch
            {
                NpcAppearanceElementMask.Hat => hatChance,
                NpcAppearanceElementMask.Jacket => jacketChance,
                NpcAppearanceElementMask.Gloves => glovesChance,
                NpcAppearanceElementMask.Boots => bootsChance,
                NpcAppearanceElementMask.Accessory => accessoryChance,
                _ => 1f
            };
        }

        return element switch
        {
            NpcAppearanceElementMask.Hat => chances.hatChance,
            NpcAppearanceElementMask.Jacket => chances.jacketChance,
            NpcAppearanceElementMask.Gloves => chances.glovesChance,
            NpcAppearanceElementMask.Boots => chances.bootsChance,
            NpcAppearanceElementMask.Accessory => chances.accessoryChance,
            _ => 1f
        };
    }

    private void FillMissingPatterns(NpcAppearancePresetApplier.AppearanceSelectionData data)
    {
        data.hatPatternOption ??= PickPatternOption(data.hatOption);
        data.jacketPatternOption ??= PickPatternOption(data.jacketOption);
        data.glovesPatternOption ??= PickPatternOption(data.glovesOption);
        data.bootsPatternOption ??= PickPatternOption(data.bootsOption);
        data.accessoryPatternOption ??= PickPatternOption(data.accessoryOption);
        data.skisPatternOption ??= PickPatternOption(data.skisOption);
        data.polesPatternOption ??= PickPatternOption(data.polesOption);
    }

    private SocialColourContext ResolveSocialColourContext(NpcSocialAppearanceProfileSO socialAppearance)
    {
        NpcSocialColourPlan plan = socialAppearance != null ? socialAppearance.ColourPlan : null;
        NpcColourHarmonyMode mode = ResolveProfileHarmonyMode(plan);
        Color anchor = SampleRange(plan != null ? plan.anchorRanges : null, Color.HSVToRGB(Random.value, 0.75f, 0.8f));
        Color.RGBToHSV(anchor, out float h, out float s, out float v);

        return new SocialColourContext
        {
            anchorHue = h,
            anchorSaturation = s,
            anchorValue = v,
            harmonyMode = mode
        };
    }

    private NpcColourHarmonyMode ResolveProfileHarmonyMode(NpcSocialColourPlan plan)
    {
        if (plan == null)
            return NpcColourHarmonyMode.Analogous;

        if (plan.randomizeHarmonyMode && plan.allowedHarmonyModes != null && plan.allowedHarmonyModes.Length > 0)
            return plan.allowedHarmonyModes[Random.Range(0, plan.allowedHarmonyModes.Length)];

        if (plan.randomizeHarmonyMode)
        {
            var values = (NpcColourHarmonyMode[])System.Enum.GetValues(typeof(NpcColourHarmonyMode));
            return values[Random.Range(1, values.Length)];
        }

        return plan.harmonyMode;
    }

    private Color ResolveSkinColour(NpcSocialColourPlan plan, SocialColourContext context)
    {
        return SampleRange(plan != null ? plan.skinRanges : null, GenerateSkinColor());
    }

    private Color ResolveEyeColour(NpcSocialColourPlan plan, SocialColourContext context, Color skinColor)
    {
        return SampleRange(plan != null ? plan.eyeRanges : null, GenerateEyeColor(skinColor));
    }

    private Color ResolveElementPrimaryColour(NpcSocialColourPlan plan, NpcAppearanceElementMask element, SocialColourContext context)
    {
        NpcElementColourOverride elementOverride = GetElementOverride(plan, element);
        NpcHSVRange[] ranges = elementOverride != null && elementOverride.overridePrimary
            ? elementOverride.primaryRanges
            : plan != null ? plan.primaryRanges : null;
        int slot = elementOverride != null && elementOverride.overridePrimary
            ? elementOverride.primaryHarmonySlot
            : GetProfileHarmonySlotForElement(plan, element, out _);

        bool hasSlotRule = GetProfileHarmonySlotForElement(plan, element, out float configuredSaturation, out float configuredValue, out float configuredJitter);
        float hueJitter = elementOverride != null && elementOverride.overridePrimary
            ? elementOverride.primaryHueJitter
            : hasSlotRule ? configuredJitter : 0.02f;
        float saturationScale = hasSlotRule ? configuredSaturation : 1f;
        float valueScale = hasSlotRule ? configuredValue : 1f;

        return ResolveHarmonizedColour(ranges, context, slot, hueJitter, saturationScale, valueScale);
    }

    private bool ResolveElementSecondaryColour(
        NpcSocialColourPlan plan,
        NpcAppearanceElementMask element,
        SocialColourContext context,
        out Color colour,
        out string channelId)
    {
        NpcElementColourOverride elementOverride = GetElementOverride(plan, element);
        NpcHSVRange[] ranges = elementOverride != null && elementOverride.overrideSecondary
            ? elementOverride.secondaryRanges
            : plan != null ? plan.secondaryRanges : null;
        int slot = elementOverride != null && elementOverride.overrideSecondary
            ? elementOverride.secondaryHarmonySlot
            : GetDefaultSecondaryHarmonySlotForElement(element);
        float hueJitter = elementOverride != null && elementOverride.overrideSecondary
            ? elementOverride.secondaryHueJitter
            : 0.02f;

        channelId = elementOverride != null && elementOverride.overrideSecondary
            ? elementOverride.secondaryChannelId
            : "secondary";
        colour = ResolveHarmonizedColour(ranges, context, slot, hueJitter, 1f, 1f);
        return ranges != null && ranges.Length > 0 || elementOverride != null && elementOverride.overrideSecondary;
    }

    private Color ResolveHarmonizedColour(NpcHSVRange[] ranges, SocialColourContext context, int harmonySlot, float hueJitter, float saturationScale, float valueScale)
    {
        Color baseColour = SampleRange(ranges, Color.HSVToRGB(context.anchorHue, context.anchorSaturation, context.anchorValue));
        Color.RGBToHSV(baseColour, out _, out float s, out float v);

        float hue = Wrap01(context.anchorHue + GetHarmonyOffset01(context.harmonyMode, harmonySlot) + Random.Range(-hueJitter, hueJitter));
        return Color.HSVToRGB(
            hue,
            Mathf.Clamp01(s * saturationScale),
            Mathf.Clamp01(v * valueScale));
    }

    private Color SampleRange(NpcHSVRange[] ranges, Color fallback)
    {
        if (ranges == null || ranges.Length == 0)
            return fallback;

        NpcHSVRange range = ranges[Random.Range(0, ranges.Length)];
        float hue = RandomRangeWrapped(range.hueMin, range.hueMax);
        float saturation = Random.Range(Mathf.Min(range.saturationMin, range.saturationMax), Mathf.Max(range.saturationMin, range.saturationMax));
        float value = Random.Range(Mathf.Min(range.valueMin, range.valueMax), Mathf.Max(range.valueMin, range.valueMax));
        return Color.HSVToRGB(Wrap01(hue), Mathf.Clamp01(saturation), Mathf.Clamp01(value));
    }

    private int GetDefaultHarmonySlotForElement(NpcAppearanceElementMask element)
    {
        return element switch
        {
            NpcAppearanceElementMask.Hat => 0,
            NpcAppearanceElementMask.Jacket => 1,
            NpcAppearanceElementMask.Gloves => 2,
            NpcAppearanceElementMask.Boots => 2,
            NpcAppearanceElementMask.Accessory => 3,
            NpcAppearanceElementMask.Skis => 2,
            NpcAppearanceElementMask.Poles => 3,
            _ => 0
        };
    }

    private int GetDefaultSecondaryHarmonySlotForElement(NpcAppearanceElementMask element)
    {
        return element switch
        {
            NpcAppearanceElementMask.Jacket => 2,
            NpcAppearanceElementMask.Hat => 1,
            NpcAppearanceElementMask.Gloves => 1,
            NpcAppearanceElementMask.Boots => 0,
            NpcAppearanceElementMask.Accessory => 2,
            _ => GetDefaultHarmonySlotForElement(element)
        };
    }

    private int GetProfileHarmonySlotForElement(NpcSocialColourPlan plan, NpcAppearanceElementMask element, out bool matched)
    {
        matched = GetProfileHarmonySlotForElement(plan, element, out _, out _, out _);
        return matched ? GetMatchingHarmonySlot(plan, element).harmonySlot : GetDefaultHarmonySlotForElement(element);
    }

    private bool GetProfileHarmonySlotForElement(NpcSocialColourPlan plan, NpcAppearanceElementMask element, out float saturationScale, out float valueScale, out float hueJitter)
    {
        saturationScale = 1f;
        valueScale = 1f;
        hueJitter = 0.02f;

        var rule = GetMatchingHarmonySlot(plan, element);
        if (rule == null)
            return false;

        saturationScale = rule.saturationScale;
        valueScale = rule.valueScale;
        hueJitter = rule.hueJitter;
        return true;
    }

    private NpcHarmonySlotRule GetMatchingHarmonySlot(NpcSocialColourPlan plan, NpcAppearanceElementMask element)
    {
        if (plan == null || plan.harmonySlots == null)
            return null;

        for (int i = 0; i < plan.harmonySlots.Length; i++)
        {
            var rule = plan.harmonySlots[i];
            if (rule != null && (rule.elements & element) != 0)
                return rule;
        }

        return null;
    }

    private NpcElementColourOverride GetElementOverride(NpcSocialColourPlan plan, NpcAppearanceElementMask element)
    {
        if (plan == null || plan.elementOverrides == null)
            return null;

        for (int i = 0; i < plan.elementOverrides.Length; i++)
        {
            var rule = plan.elementOverrides[i];
            if (rule != null && (rule.elements & element) != 0)
                return rule;
        }

        return null;
    }

    private void ApplySecondaryChannelOverride(
        NpcSocialAppearanceProfileSO socialAppearance,
        NpcSocialColourPlan plan,
        NpcAppearanceElementMask element,
        CustomizationOptionSO option,
        List<NpcAppearancePresetSO.ChannelColorOverride> targetList,
        SocialColourContext context)
    {
        if (option == null || targetList == null)
            return;

        if (!ResolveElementSecondaryColour(plan, element, context, out Color colour, out string requestedChannelId))
            return;

        string channelId = ResolveSupportedSecondaryChannel(option, requestedChannelId);
        if (string.IsNullOrWhiteSpace(channelId))
        {
            WarnOnce(socialAppearance, $"Option '{option.name}' does not expose secondary channel '{requestedChannelId}' for {element}.");
            return;
        }

        UpsertChannelColor(targetList, channelId, colour);
    }

    private void ApplyLegacySecondaryChannelOverride(CustomizationOptionSO option, Color primaryColor, List<NpcAppearancePresetSO.ChannelColorOverride> targetList)
    {
        if (option == null || targetList == null)
            return;

        string channelId = ResolveSupportedSecondaryChannel(option, "secondary");
        if (!string.IsNullOrWhiteSpace(channelId))
            UpsertChannelColor(targetList, channelId, ResolveTint(option, ShiftValue(primaryColor)));
    }

    private string ResolveSupportedSecondaryChannel(CustomizationOptionSO option, string requestedChannelId)
    {
        if (option == null)
            return null;

        string fallbackChannelId = string.IsNullOrWhiteSpace(requestedChannelId) ? "secondary" : requestedChannelId.Trim();
        string channelId = option.UsesLimbSecondaryColour()
            ? option.GetResolvedLimbSecondaryChannelId(fallbackChannelId)
            : fallbackChannelId;

        GameObject prefab = option.ResolveWearablePrefab(customizer);
        WearableAttachment attachment = prefab != null ? prefab.GetComponent<WearableAttachment>() : null;
        if (attachment == null)
            return option.UsesLimbSecondaryColour() ? channelId : null;

        var channels = attachment.GetExtraChannels();
        if (channels == null || channels.Count == 0)
            return option.UsesLimbSecondaryColour() ? channelId : null;

        for (int i = 0; i < channels.Count; i++)
        {
            var channel = channels[i];
            if (channel != null && string.Equals(channel.id, channelId, System.StringComparison.Ordinal))
                return channelId;
        }

        return null;
    }

    private float GetHarmonyOffset01(NpcColourHarmonyMode mode, int slot)
    {
        slot = Mathf.Max(0, slot);
        return mode switch
        {
            NpcColourHarmonyMode.None => 0f,
            NpcColourHarmonyMode.Monochromatic => 0f,
            NpcColourHarmonyMode.Analogous => slot switch
            {
                1 => -18f / 360f,
                2 => 18f / 360f,
                3 => 8f / 360f,
                _ => 0f
            },
            NpcColourHarmonyMode.Complementary => slot switch
            {
                1 => 180f / 360f,
                2 => 0f,
                3 => 165f / 360f,
                _ => 0f
            },
            NpcColourHarmonyMode.SplitComplementary => slot switch
            {
                1 => 150f / 360f,
                2 => 210f / 360f,
                3 => 0f,
                _ => 0f
            },
            NpcColourHarmonyMode.Triadic => slot switch
            {
                1 => 120f / 360f,
                2 => 240f / 360f,
                3 => 0f,
                _ => 0f
            },
            NpcColourHarmonyMode.Tetradic => slot switch
            {
                1 => 60f / 360f,
                2 => 180f / 360f,
                3 => 240f / 360f,
                _ => 0f
            },
            NpcColourHarmonyMode.Square => slot switch
            {
                1 => 90f / 360f,
                2 => 180f / 360f,
                3 => 270f / 360f,
                _ => 0f
            },
            _ => 0f
        };
    }

    private void BuildPalette(HarmonyMode mode, NpcSkierProfile profile, out Color hat, out Color jacket, out Color gloves, out Color boots, out Color accessory, out Color skis, out Color poles)
    {
        float anchorHue = Random.value;
        float sat = Mathf.Lerp(0.35f, 1f, saturationBias);
        float val = Mathf.Lerp(0.45f, 1f, valueBias);

        float h0 = Wrap01(anchorHue + Random.Range(-colourVariance, colourVariance) * 0.06f);
        float h1 = Wrap01(anchorHue + HarmonyOffset01(mode, 1) + Random.Range(-colourVariance, colourVariance) * 0.04f);
        float h2 = Wrap01(anchorHue + HarmonyOffset01(mode, 2) + Random.Range(-colourVariance, colourVariance) * 0.05f);
        float h3 = Wrap01(anchorHue + HarmonyOffset01(mode, 3) + Random.Range(-colourVariance, colourVariance) * 0.05f);
        float h4 = Wrap01(anchorHue + HarmonyOffset01(mode, 1) + Random.Range(-colourVariance, colourVariance) * 0.08f);
        float h5 = Wrap01(anchorHue + HarmonyOffset01(mode, 2) + Random.Range(-colourVariance, colourVariance) * 0.08f);
        float h6 = Wrap01(anchorHue + HarmonyOffset01(mode, 3) + Random.Range(-colourVariance, colourVariance) * 0.08f);

        float confidence = profile != null ? profile.Confidence01 : 0.5f;
        float caution = profile != null ? profile.Caution01 : 0.5f;

        hat = Color.HSVToRGB(h0, Mathf.Clamp01(sat * 0.82f), Mathf.Clamp01(val * 0.92f));
        jacket = Color.HSVToRGB(h1, Mathf.Clamp01(sat * Mathf.Lerp(0.60f, 0.95f, confidence)), Mathf.Clamp01(val * 0.88f));
        gloves = Color.HSVToRGB(h4, Mathf.Clamp01(sat * 0.72f), Mathf.Clamp01(val * 0.78f));
        boots = Color.HSVToRGB(h5, Mathf.Clamp01(sat * 0.68f), Mathf.Clamp01(val * 0.72f));
        accessory = Color.HSVToRGB(h6, Mathf.Clamp01(sat * 0.7f), Mathf.Clamp01(val * 0.86f));
        skis = Color.HSVToRGB(h2, Mathf.Clamp01(sat), Mathf.Clamp01(Mathf.Lerp(0.55f, 1f, 1f - caution)));
        poles = Color.HSVToRGB(h3, Mathf.Clamp01(sat * 0.72f), Mathf.Clamp01(val * 0.82f));
    }

    private float HarmonyOffset01(HarmonyMode mode, int slot)
    {
        return mode switch
        {
            HarmonyMode.Monochromatic => 0f,
            HarmonyMode.Analogous => slot switch
            {
                1 => -18f / 360f,
                2 => 18f / 360f,
                3 => 8f / 360f,
                _ => 0f
            },
            HarmonyMode.Complementary => slot switch
            {
                1 => 180f / 360f,
                2 => 0f,
                3 => 165f / 360f,
                _ => 0f
            },
            HarmonyMode.SplitComplementary => slot switch
            {
                1 => 150f / 360f,
                2 => 210f / 360f,
                3 => 0f,
                _ => 0f
            },
            HarmonyMode.Triadic => slot switch
            {
                1 => 120f / 360f,
                2 => 240f / 360f,
                3 => 0f,
                _ => 0f
            },
            _ => 0f
        };
    }

    private Color GenerateSkinColor()
    {
        float h = Mathf.Lerp(0.05f, 0.12f, skinToneWarmthBias) + Random.Range(-0.025f, 0.025f);
        float s = Random.Range(0.18f, 0.55f);
        float v = Random.Range(0.35f, 0.95f);
        return Color.HSVToRGB(Wrap01(h), s, v);
    }

    private Color GenerateEyeColor(Color skinColor)
    {
        Color.RGBToHSV(skinColor, out _, out _, out float skinV);
        float h = Random.value;
        float s = Random.Range(0.12f, 0.9f);
        float v = skinV > 0.65f ? Random.Range(0.1f, 0.45f) : Random.Range(0.35f, 0.9f);
        return Color.HSVToRGB(h, s, v);
    }

    private Color ResolveTint(CustomizationOptionSO option, Color fallback)
    {
        if (option == null)
            return fallback;

        if (option.HasTint && Random.value < useDefaultTintBias)
            return option.DefaultTint;

        return fallback;
    }

    private CustomizationOptionSO PickPatternOption(CustomizationOptionSO option)
    {
        if (option == null)
            return null;

        var options = new List<CustomizationOptionSO>();
        if (option.defaultPatternOption != null)
            options.Add(option.defaultPatternOption);

        if (option.unlockPatternOptionsOnPurchase != null)
        {
            for (int i = 0; i < option.unlockPatternOptionsOnPurchase.Count; i++)
            {
                var candidate = option.unlockPatternOptionsOnPurchase[i];
                if (candidate != null)
                    options.Add(candidate);
            }
        }

        return options.Count > 0 ? options[Random.Range(0, options.Count)] : null;
    }

    private List<CustomizationOptionSO> GetPool(CustomizationOptionType type)
    {
        return type switch
        {
            CustomizationOptionType.EyeIcon => _eyeIcons,
            CustomizationOptionType.Hat => _hats,
            CustomizationOptionType.Jacket => _jackets,
            CustomizationOptionType.Gloves => _gloves,
            CustomizationOptionType.Boots => _boots,
            CustomizationOptionType.Accessory => _accessories,
            CustomizationOptionType.Skis => _skis,
            CustomizationOptionType.Poles => _poles,
            _ => null
        };
    }

    private NpcAppearancePresetSO PickPreset(NpcSocialAppearanceProfileSO socialAppearance)
    {
        if (socialAppearance == null || socialAppearance.BasePresets == null || socialAppearance.BasePresets.Length == 0)
            return null;

        var valid = new List<NpcAppearancePresetSO>();
        for (int i = 0; i < socialAppearance.BasePresets.Length; i++)
        {
            if (socialAppearance.BasePresets[i] != null)
                valid.Add(socialAppearance.BasePresets[i]);
        }

        return valid.Count > 0 ? valid[Random.Range(0, valid.Count)] : null;
    }

    private static float RandomRangeWrapped(float min, float max)
    {
        min = Wrap01(min);
        max = Wrap01(max);
        if (min <= max)
            return Random.Range(min, max);

        float span = 1f - min + max;
        return Wrap01(min + Random.value * span);
    }

    private static T PickRandom<T>(List<T> list) where T : class
    {
        if (list == null || list.Count == 0)
            return null;

        return list[Random.Range(0, list.Count)];
    }

    private static void UpsertChannelColor(List<NpcAppearancePresetSO.ChannelColorOverride> targetList, string channelId, Color color)
    {
        if (targetList == null || string.IsNullOrWhiteSpace(channelId))
            return;

        for (int i = 0; i < targetList.Count; i++)
        {
            if (!string.Equals(targetList[i].channelId, channelId, System.StringComparison.Ordinal))
                continue;

            targetList[i] = new NpcAppearancePresetSO.ChannelColorOverride { channelId = channelId, color = color };
            return;
        }

        targetList.Add(new NpcAppearancePresetSO.ChannelColorOverride { channelId = channelId, color = color });
    }

    private static float Wrap01(float v)
    {
        v %= 1f;
        if (v < 0f) v += 1f;
        return v;
    }

    private static Color ShiftValue(Color color)
    {
        Color.RGBToHSV(color, out float h, out float s, out float v);
        return Color.HSVToRGB(h, s, Mathf.Clamp01(v * 0.75f));
    }

    private void WarnOnce(NpcSocialAppearanceProfileSO profile, string message)
    {
        if (profile == null || string.IsNullOrWhiteSpace(message))
            return;

        string key = $"{profile.GetInstanceID()}:{message}";
        if (!WarnedProfileIssues.Add(key))
            return;

        Debug.LogWarning($"[{nameof(NpcSkierAppearanceGenerator)}] {profile.name}: {message}", profile);
    }
}
