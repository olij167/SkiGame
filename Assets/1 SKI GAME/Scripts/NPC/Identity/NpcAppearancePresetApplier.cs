using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class NpcAppearancePresetApplier : MonoBehaviour
{
    [Serializable]
    public sealed class AppearanceSelectionData
    {
        public CustomizationOptionSO skinPatternOption;
        public Color skinColor = Color.white;
        public CustomizationOptionSO eyeOption;
        public Color eyeColor = Color.white;
        public Color eyeOutlineColor = new(0f, 0f, 0f, 0f);

        public CustomizationOptionSO hatOption;
        public Color hatColor = Color.white;
        public CustomizationOptionSO hatPatternOption;
        public Texture2D legacyHatPattern;
        public List<NpcAppearancePresetSO.ChannelColorOverride> hatChannelColors = new();

        public CustomizationOptionSO jacketOption;
        public Color jacketColor = Color.white;
        public CustomizationOptionSO jacketPatternOption;
        public Texture2D legacyJacketPattern;
        public List<NpcAppearancePresetSO.ChannelColorOverride> jacketChannelColors = new();

        public CustomizationOptionSO glovesOption;
        public Color glovesColor = Color.white;
        public CustomizationOptionSO glovesPatternOption;
        public Texture2D legacyGlovesPattern;
        public List<NpcAppearancePresetSO.ChannelColorOverride> gloveChannelColors = new();

        public CustomizationOptionSO bootsOption;
        public Color bootsColor = Color.white;
        public CustomizationOptionSO bootsPatternOption;
        public Texture2D legacyBootsPattern;
        public List<NpcAppearancePresetSO.ChannelColorOverride> bootChannelColors = new();

        public CustomizationOptionSO accessoryOption;
        public Color accessoryColor = Color.white;
        public CustomizationOptionSO accessoryPatternOption;
        public Texture2D legacyAccessoryPattern;
        public List<NpcAppearancePresetSO.ChannelColorOverride> accessoryChannelColors = new();

        public CustomizationOptionSO skisOption;
        public SkiGearProfileSO skisProfileOverride;
        public Color skisColor = Color.white;
        public CustomizationOptionSO skisPatternOption;
        public Texture2D legacySkisPattern;

        public CustomizationOptionSO polesOption;
        public SkiGearProfileSO polesProfileOverride;
        public Color polesColor = Color.white;
        public CustomizationOptionSO polesPatternOption;
        public Texture2D legacyPolesPattern;
    }

    [SerializeField] private NpcAppearancePresetSO preset;
    [SerializeField] private CharacterCustomizer customizer;
    [SerializeField] private SkiGearLoadout gearLoadout;
    [SerializeField] private SkiController skiController;
    [SerializeField] private bool runOnStart = true;
    [SerializeField] private bool preferIdentityPreset = true;

    private IEnumerator Start()
    {
        if (!runOnStart)
            yield break;

        yield return null;
        ApplyPreset();
    }

    public void SetPreset(NpcAppearancePresetSO value, bool onlyIfMissing = false)
    {
        if (onlyIfMissing && preset != null)
            return;

        preset = value;
    }

    [ContextMenu("Apply Preset")]
    public void ApplyPreset()
    {
        CacheRefs();

        var activePreset = ResolvePreset();
        if (activePreset == null)
            return;

        ApplyData(BuildDataFromPreset(activePreset));
    }

    public void ApplyData(AppearanceSelectionData data)
    {
        if (data == null)
            return;

        CacheRefs();
        if (customizer != null)
            ApplyCustomizerData(data);
        if (gearLoadout != null)
            ApplyGearData(data);
    }

    public static AppearanceSelectionData BuildDataFromPreset(NpcAppearancePresetSO activePreset)
    {
        if (activePreset == null)
            return null;

        return new AppearanceSelectionData
        {
            skinPatternOption = null,
            skinColor = activePreset.skinColor,
            eyeOption = activePreset.eyeOption,
            eyeColor = activePreset.eyeColor,
            eyeOutlineColor = activePreset.eyeOutlineColor,
            hatOption = activePreset.hatOption,
            hatColor = activePreset.hatColor,
            hatPatternOption = activePreset.hatPatternOption,
            legacyHatPattern = activePreset.legacyHatPattern,
            hatChannelColors = CloneChannels(activePreset.hatChannelColors),
            jacketOption = activePreset.jacketOption,
            jacketColor = activePreset.jacketColor,
            jacketPatternOption = activePreset.jacketPatternOption,
            legacyJacketPattern = activePreset.legacyJacketPattern,
            jacketChannelColors = CloneChannels(activePreset.jacketChannelColors),
            glovesOption = activePreset.glovesOption,
            glovesColor = activePreset.glovesColor,
            glovesPatternOption = activePreset.glovesPatternOption,
            legacyGlovesPattern = activePreset.legacyGlovesPattern,
            gloveChannelColors = CloneChannels(activePreset.gloveChannelColors),
            bootsOption = activePreset.bootsOption,
            bootsColor = activePreset.bootsColor,
            bootsPatternOption = activePreset.bootsPatternOption,
            legacyBootsPattern = activePreset.legacyBootsPattern,
            bootChannelColors = CloneChannels(activePreset.bootChannelColors),
            accessoryOption = activePreset.accessoryOption,
            accessoryColor = activePreset.accessoryColor,
            accessoryPatternOption = activePreset.accessoryPatternOption,
            legacyAccessoryPattern = activePreset.legacyAccessoryPattern,
            accessoryChannelColors = CloneChannels(activePreset.accessoryChannelColors),
            skisOption = activePreset.skisOption,
            skisProfileOverride = activePreset.skisProfileOverride,
            skisColor = activePreset.skisColor,
            skisPatternOption = activePreset.skisPatternOption,
            legacySkisPattern = activePreset.legacySkisPattern,
            polesOption = activePreset.polesOption,
            polesProfileOverride = activePreset.polesProfileOverride,
            polesColor = activePreset.polesColor,
            polesPatternOption = activePreset.polesPatternOption,
            legacyPolesPattern = activePreset.legacyPolesPattern
        };
    }

    private void CacheRefs()
    {
        if (customizer == null)
            customizer = GetComponentInChildren<CharacterCustomizer>(true);
        if (gearLoadout == null)
            gearLoadout = GetComponentInChildren<SkiGearLoadout>(true);
        if (skiController == null)
            skiController = GetComponent<SkiController>();
    }

    private NpcAppearancePresetSO ResolvePreset()
    {
        if (preferIdentityPreset && TryGetComponent(out NpcIdentity identity) && identity.AppearancePreset != null)
            return identity.AppearancePreset;

        return preset;
    }

    private void ApplyCustomizerData(AppearanceSelectionData data)
    {
        ApplySkin(data);
        ApplyEyes(data);
        ApplyHat(data);
        ApplyJacket(data);
        ApplyGloves(data);
        ApplyBoots(data);
        ApplyAccessory(data);
    }

    private void ApplySkin(AppearanceSelectionData data)
    {
        // NPC appearance presets and generated NPC data should only apply skin colour.
        // Skin pattern/texture is intentionally left as the character prefab/default.
        customizer.SetSkinColor(data.skinColor);
    }

    private void ApplyEyes(AppearanceSelectionData data)
    {
        if (data.eyeOption != null)
        {
            if (data.eyeOption.eyeSprite != null)
                customizer.SetEyeSprite(data.eyeOption.eyeSprite);
            else if (data.eyeOption.customizerIndex >= 0)
                customizer.SetEyeStyle(data.eyeOption.customizerIndex);
        }

        customizer.SetEyeColor(data.eyeColor);
        customizer.SetEyeOutlineColor(data.eyeOutlineColor);
    }

    private void ApplyHat(AppearanceSelectionData data)
    {
        ApplyWearableOption(data.hatOption, CustomizationOptionType.Hat, customizer.ClearHat, customizer.SetHat, customizer.SetHatPrefab);
        customizer.SetHatColor(data.hatColor);
        customizer.SetHatPatternTexture(ResolvePatternTexture(data.hatPatternOption, data.legacyHatPattern));
        ApplyChannelColors(data.hatChannelColors, customizer.SetHatChannelColor);
    }

    private void ApplyJacket(AppearanceSelectionData data)
    {
        ApplyWearableOption(data.jacketOption, CustomizationOptionType.Jacket, customizer.ClearJacket, customizer.SetJacket, customizer.SetJacketPrefab);
        customizer.SetCurrentJacketOption(data.jacketOption);
        customizer.SetJacketColor(data.jacketColor);
        customizer.SetJacketPatternTexture(ResolvePatternTexture(data.jacketPatternOption, data.legacyJacketPattern));
        ApplyChannelColors(data.jacketChannelColors, customizer.SetJacketChannelColor);
    }

    private void ApplyGloves(AppearanceSelectionData data)
    {
        ApplyWearableOption(data.glovesOption, CustomizationOptionType.Gloves, customizer.ClearGloves, customizer.SetGloves, customizer.SetGlovesPrefab);
        customizer.SetGlovesColor(data.glovesColor);
        customizer.SetGlovesPatternTexture(ResolvePatternTexture(data.glovesPatternOption, data.legacyGlovesPattern));
        ApplyChannelColors(data.gloveChannelColors, customizer.SetGlovesChannelColor);
    }

    private void ApplyBoots(AppearanceSelectionData data)
    {
        ApplyWearableOption(data.bootsOption, CustomizationOptionType.Boots, customizer.ClearBoots, customizer.SetBoots, customizer.SetBootsPrefab);
        customizer.SetBootsColor(data.bootsColor);
        customizer.SetBootsPatternTexture(ResolvePatternTexture(data.bootsPatternOption, data.legacyBootsPattern));
        ApplyChannelColors(data.bootChannelColors, customizer.SetBootsChannelColor);
    }

    private void ApplyAccessory(AppearanceSelectionData data)
    {
        ApplyWearableOption(data.accessoryOption, CustomizationOptionType.Accessory, customizer.ClearAccessory, customizer.SetAccessory, customizer.SetAccessoryPrefab);
        customizer.SetCurrentAccessoryOption(data.accessoryOption);
        customizer.SetAccessoryColor(data.accessoryColor);
        customizer.SetAccessoryPatternTexture(ResolvePatternTexture(data.accessoryPatternOption, data.legacyAccessoryPattern));
        ApplyChannelColors(data.accessoryChannelColors, customizer.SetAccessoryChannelColor);
    }

    private void ApplyGearData(AppearanceSelectionData data)
    {
        var skisProfile = data.skisProfileOverride != null ? data.skisProfileOverride : data.skisOption != null ? data.skisOption.gearProfile : null;
        if (skisProfile != null)
            gearLoadout.EquipSkis(skisProfile);
        gearLoadout.SetSkisColor(data.skisColor);
        var skisPattern = ResolvePatternTexture(data.skisPatternOption, data.legacySkisPattern);
        gearLoadout.SetSkisPattern(skisPattern);
        skiController?.SetSkisPatternTexture(skisPattern);

        var polesProfile = data.polesProfileOverride != null ? data.polesProfileOverride : data.polesOption != null ? data.polesOption.gearProfile : null;
        if (polesProfile != null)
            gearLoadout.EquipPoles(polesProfile);
        gearLoadout.SetPolesColor(data.polesColor);
        var polesPattern = ResolvePatternTexture(data.polesPatternOption, data.legacyPolesPattern);
        gearLoadout.SetPolesPattern(polesPattern);
        skiController?.SetPolesPatternTexture(polesPattern);
    }

    private Texture2D ResolvePatternTexture(CustomizationOptionSO patternOption, Texture2D legacyTexture)
    {
        if (patternOption != null)
        {
            var resolved = patternOption.ResolvePatternTexture(customizer);
            if (resolved != null)
                return resolved;
        }

        return legacyTexture;
    }

    private static void ApplyChannelColors(List<NpcAppearancePresetSO.ChannelColorOverride> overrides, Action<string, Color> setter)
    {
        if (overrides == null || setter == null)
            return;

        for (int i = 0; i < overrides.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(overrides[i].channelId))
                setter(overrides[i].channelId, overrides[i].color);
        }
    }

    private void ApplyWearableOption(
        CustomizationOptionSO option,
        CustomizationOptionType expectedType,
        Action clearAction,
        Action<int> indexAction,
        Action<GameObject> prefabAction)
    {
        if (option == null)
        {
            clearAction?.Invoke();
            return;
        }

        if (option.type != expectedType)
            Debug.LogWarning($"[{nameof(NpcAppearancePresetApplier)}] Expected option type '{expectedType}' but received '{option.type}' on '{option.name}'.");

        if (option.customizerIndex >= 0)
            indexAction?.Invoke(option.customizerIndex);
        else
            prefabAction?.Invoke(option.ResolveWearablePrefab(customizer));
    }

    private static List<NpcAppearancePresetSO.ChannelColorOverride> CloneChannels(List<NpcAppearancePresetSO.ChannelColorOverride> source)
    {
        return source != null ? new List<NpcAppearancePresetSO.ChannelColorOverride>(source) : new List<NpcAppearancePresetSO.ChannelColorOverride>();
    }
}
