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

    [Header("Selection Chances")]
    [Range(0f, 1f)][SerializeField] private float hatChance = 0.75f;
    [Range(0f, 1f)][SerializeField] private float jacketChance = 0.9f;

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

        HarmonyMode effectiveHarmony = randomizeHarmonyModeOnApply
            ? (HarmonyMode)Random.Range(0, System.Enum.GetValues(typeof(HarmonyMode)).Length)
            : harmonyMode;

        Color skinColor = GenerateSkinColor();
        customizer.SetSkinColor(skinColor);

        var eyeOpt = PickRandom(_eyeIcons);
        if (eyeOpt != null)
        {
            if (eyeOpt.eyeSprite != null) customizer.SetEyeSprite(eyeOpt.eyeSprite);
            else if (eyeOpt.customizerIndex >= 0) customizer.SetEyeStyle(eyeOpt.customizerIndex);
        }

        customizer.SetEyeColor(GenerateEyeColor(skinColor));

        BuildPalette(effectiveHarmony, profile, out Color hatCol, out Color jacketCol, out Color skisCol, out Color polesCol);

        var hatOpt = Random.value <= hatChance ? PickRandom(_hats) : null;
        var jacketOpt = Random.value <= jacketChance ? PickRandom(_jackets) : null;
        var skisOpt = PickRandom(_skis);
        var polesOpt = PickRandom(_poles);

        ApplyWearable(hatOpt, isHat: true, ResolveTint(hatOpt, hatCol), ResolvePatternTexture(hatOpt));
        ApplyWearable(jacketOpt, isHat: false, ResolveTint(jacketOpt, jacketCol), ResolvePatternTexture(jacketOpt));

        ApplyGear(skisOpt, isSkis: true, ResolveTint(skisOpt, skisCol), ResolvePatternTexture(skisOpt));
        ApplyGear(polesOpt, isSkis: false, ResolveTint(polesOpt, polesCol), ResolvePatternTexture(polesOpt));
    }

    private void CacheRefs()
    {
        if (customizer == null) customizer = GetComponentInChildren<CharacterCustomizer>(true);
        if (gearLoadout == null) gearLoadout = GetComponentInChildren<SkiGearLoadout>(true);
        if (skiController == null) skiController = GetComponent<SkiController>();
    }

    private void RebuildPools()
    {
        _eyeIcons.Clear();
        _hats.Clear();
        _jackets.Clear();
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
                case CustomizationOptionType.EyeIcon: _eyeIcons.Add(option); break;
                case CustomizationOptionType.Hat: _hats.Add(option); break;
                case CustomizationOptionType.Jacket: _jackets.Add(option); break;
                case CustomizationOptionType.Skis: _skis.Add(option); break;
                case CustomizationOptionType.Poles: _poles.Add(option); break;
            }
        }
    }

    private void BuildPalette(HarmonyMode mode, NpcSkierProfile profile, out Color hat, out Color jacket, out Color skis, out Color poles)
    {
        float anchorHue = Random.value;
        float sat = Mathf.Lerp(0.35f, 1f, saturationBias);
        float val = Mathf.Lerp(0.45f, 1f, valueBias);

        float h0 = Wrap01(anchorHue + Random.Range(-colourVariance, colourVariance) * 0.06f);
        float h1 = Wrap01(anchorHue + HarmonyOffset01(mode, 1) + Random.Range(-colourVariance, colourVariance) * 0.04f);
        float h2 = Wrap01(anchorHue + HarmonyOffset01(mode, 2) + Random.Range(-colourVariance, colourVariance) * 0.05f);
        float h3 = Wrap01(anchorHue + HarmonyOffset01(mode, 3) + Random.Range(-colourVariance, colourVariance) * 0.05f);

        float confidence = profile != null ? profile.Confidence01 : 0.5f;
        float caution = profile != null ? profile.Caution01 : 0.5f;

        hat = Color.HSVToRGB(h0, Mathf.Clamp01(sat * 0.82f), Mathf.Clamp01(val * 0.92f));
        jacket = Color.HSVToRGB(h1, Mathf.Clamp01(sat * Mathf.Lerp(0.60f, 0.95f, confidence)), Mathf.Clamp01(val * 0.88f));
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

    private void ApplyWearable(CustomizationOptionSO option, bool isHat, Color colour, Texture2D pattern)
    {
        if (customizer == null)
            return;

        if (option == null)
        {
            if (isHat) customizer.ClearHat();
            else
            {
                customizer.SetCurrentJacketOption(null);
                customizer.ClearJacket();
            }
            return;
        }

        if (isHat)
        {
            if (option.hatPrefab != null) customizer.SetHatPrefab(option.hatPrefab);
            else if (option.customizerIndex >= 0) customizer.SetHat(option.customizerIndex);

            customizer.SetHatColor(colour);
            customizer.SetHatPatternTexture(pattern);
        }
        else
        {
            if (option.jacketPrefab != null) customizer.SetJacketPrefab(option.jacketPrefab);
            else if (option.customizerIndex >= 0) customizer.SetJacket(option.customizerIndex);

            customizer.SetCurrentJacketOption(option);
            customizer.SetJacketColor(colour);
            customizer.SetJacketPatternTexture(pattern);
        }
    }

    private void ApplyGear(CustomizationOptionSO option, bool isSkis, Color colour, Texture2D pattern)
    {
        if (option == null || gearLoadout == null)
            return;

        if (isSkis)
        {
            if (option.gearProfile != null) gearLoadout.EquipSkis(option.gearProfile);
            gearLoadout.SetSkisColor(colour);
            gearLoadout.SetSkisPattern(pattern);
            skiController?.SetSkisPatternTexture(pattern);
        }
        else
        {
            if (option.gearProfile != null) gearLoadout.EquipPoles(option.gearProfile);
            gearLoadout.SetPolesColor(colour);
            gearLoadout.SetPolesPattern(pattern);
            skiController?.SetPolesPatternTexture(pattern);
        }
    }

    private Color ResolveTint(CustomizationOptionSO option, Color fallback)
    {
        if (option == null)
            return fallback;

        if (option.HasTint && Random.value < useDefaultTintBias)
            return option.DefaultTint;

        return fallback;
    }

    private Texture2D ResolvePatternTexture(CustomizationOptionSO gearOrWearableOption)
    {
        if (gearOrWearableOption == null || catalog == null)
            return null;

        List<string> ids = new();

        if (gearOrWearableOption.defaultPatternOption != null && !string.IsNullOrEmpty(gearOrWearableOption.defaultPatternOption.id))
            ids.Add(gearOrWearableOption.defaultPatternOption.id);

        if (!string.IsNullOrEmpty(gearOrWearableOption.defaultPatternId))
            ids.Add(gearOrWearableOption.defaultPatternId);

        if (gearOrWearableOption.unlockPatternOptionsOnPurchase != null)
        {
            for (int i = 0; i < gearOrWearableOption.unlockPatternOptionsOnPurchase.Count; i++)
            {
                var p = gearOrWearableOption.unlockPatternOptionsOnPurchase[i];
                if (p != null && !string.IsNullOrEmpty(p.id))
                    ids.Add(p.id);
            }
        }

        if (gearOrWearableOption.unlockPatternIdsOnPurchase != null)
        {
            for (int i = 0; i < gearOrWearableOption.unlockPatternIdsOnPurchase.Count; i++)
            {
                string id = gearOrWearableOption.unlockPatternIdsOnPurchase[i];
                if (!string.IsNullOrEmpty(id))
                    ids.Add(id);
            }
        }

        if (ids.Count == 0)
            return null;

        string chosenId = ids[Random.Range(0, ids.Count)];
        var patternOption = catalog.FindById(chosenId);
        if (patternOption == null || patternOption.type != CustomizationOptionType.SkinPattern)
            return null;

        if (patternOption.skinPatternTexture is Texture2D tex2D)
            return tex2D;

        if (customizer != null && patternOption.customizerIndex >= 0)
            return customizer.GetSkinPatternTexture2D(patternOption.customizerIndex);

        return null;
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
}
