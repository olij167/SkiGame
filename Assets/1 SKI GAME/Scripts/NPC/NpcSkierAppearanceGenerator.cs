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

    private readonly List<CustomizationOptionSO> _skinPatterns = new();
    private readonly List<CustomizationOptionSO> _eyeIcons = new();
    private readonly List<CustomizationOptionSO> _hats = new();
    private readonly List<CustomizationOptionSO> _jackets = new();
    private readonly List<CustomizationOptionSO> _gloves = new();
    private readonly List<CustomizationOptionSO> _boots = new();
    private readonly List<CustomizationOptionSO> _accessories = new();
    private readonly List<CustomizationOptionSO> _skis = new();
    private readonly List<CustomizationOptionSO> _poles = new();

    public void ApplyRandomAppearance(NpcSkierProfile profile = null)
    {
        CacheRefs();
        RebuildPools();

        if (catalog == null || customizer == null)
        {
            Debug.LogWarning($"[{nameof(NpcSkierAppearanceGenerator)}] Missing catalog or customizer on '{name}'.", this);
            return;
        }

        if (appearanceApplier == null)
            appearanceApplier = GetComponent<NpcAppearancePresetApplier>() ?? gameObject.AddComponent<NpcAppearancePresetApplier>();

        HarmonyMode effectiveHarmony = randomizeHarmonyModeOnApply
            ? (HarmonyMode)Random.Range(0, System.Enum.GetValues(typeof(HarmonyMode)).Length)
            : harmonyMode;

        Color skinColor = GenerateSkinColor();
        Color eyeColor = GenerateEyeColor(skinColor);
        BuildPalette(effectiveHarmony, profile, out Color hatCol, out Color jacketCol, out Color glovesCol, out Color bootsCol, out Color accessoryCol, out Color skisCol, out Color polesCol);

        var data = new NpcAppearancePresetApplier.AppearanceSelectionData
        {
            skinPatternOption = PickRandom(_skinPatterns),
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

        data.hatPatternOption = PickPatternOption(data.hatOption);
        data.jacketPatternOption = PickPatternOption(data.jacketOption);
        data.glovesPatternOption = PickPatternOption(data.glovesOption);
        data.bootsPatternOption = PickPatternOption(data.bootsOption);
        data.accessoryPatternOption = PickPatternOption(data.accessoryOption);
        data.skisPatternOption = PickPatternOption(data.skisOption);
        data.polesPatternOption = PickPatternOption(data.polesOption);

        if (data.jacketOption != null && data.jacketOption.UsesLimbSecondaryColour())
        {
            string channelId = data.jacketOption.GetResolvedLimbSecondaryChannelId("secondary");
            data.jacketChannelColors.Add(new NpcAppearancePresetSO.ChannelColorOverride
            {
                channelId = channelId,
                color = ResolveTint(data.jacketOption, ShiftValue(data.jacketColor))
            });
        }

        appearanceApplier.ApplyData(data);
    }

    private void CacheRefs()
    {
        if (customizer == null) customizer = GetComponentInChildren<CharacterCustomizer>(true);
        if (gearLoadout == null) gearLoadout = GetComponentInChildren<SkiGearLoadout>(true);
        if (skiController == null) skiController = GetComponent<SkiController>();
        if (appearanceApplier == null) appearanceApplier = GetComponent<NpcAppearancePresetApplier>();
    }

    private void RebuildPools()
    {
        _skinPatterns.Clear();
        _eyeIcons.Clear();
        _hats.Clear();
        _jackets.Clear();
        _gloves.Clear();
        _boots.Clear();
        _accessories.Clear();
        _skis.Clear();
        _poles.Clear();

        if (catalog == null || catalog.options == null)
            return;

        for (int i = 0; i < catalog.options.Count; i++)
        {
            var option = catalog.options[i];
            if (option == null) continue;

            switch (option.type)
            {
                case CustomizationOptionType.SkinPattern: _skinPatterns.Add(option); break;
                case CustomizationOptionType.EyeIcon: _eyeIcons.Add(option); break;
                case CustomizationOptionType.Hat: _hats.Add(option); break;
                case CustomizationOptionType.Jacket: _jackets.Add(option); break;
                case CustomizationOptionType.Gloves: _gloves.Add(option); break;
                case CustomizationOptionType.Boots: _boots.Add(option); break;
                case CustomizationOptionType.Accessory: _accessories.Add(option); break;
                case CustomizationOptionType.Skis: _skis.Add(option); break;
                case CustomizationOptionType.Poles: _poles.Add(option); break;
            }
        }
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

    private static T PickRandom<T>(List<T> list) where T : class
    {
        if (list == null || list.Count == 0)
            return null;

        return list[Random.Range(0, list.Count)];
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
}
