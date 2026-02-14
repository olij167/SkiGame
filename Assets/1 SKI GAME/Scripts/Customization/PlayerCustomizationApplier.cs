using UnityEngine;
using SkiGame.Progression;

public class PlayerCustomizationApplier : MonoBehaviour
{
    [Header("References (auto-found if null)")]
    [SerializeField] private CharacterCustomizer characterCustomizer;
    [SerializeField] private SkiGearLoadout gearLoadout;
    [SerializeField] private SkiController skiController;

    [Header("Catalog")]
    [SerializeField] private CustomizationCatalogSO catalog;

    private void Awake()
    {
        if (characterCustomizer == null) characterCustomizer = GetComponentInChildren<CharacterCustomizer>(true);
        if (gearLoadout == null) gearLoadout = GetComponent<SkiGearLoadout>();
        if (skiController == null) skiController = GetComponent<SkiController>();
    }

    public void ApplyFromProfile(PlayerStatsProfile profile)
    {
        if (profile == null || profile.customization == null) return;
        var state = profile.customization;

        // Defaults
        if (catalog != null)
        {
            if (string.IsNullOrEmpty(state.equippedEyeIconId))
                state.equippedEyeIconId = GetDefaultId(CustomizationOptionType.EyeIcon);

            if (string.IsNullOrEmpty(state.equippedSkisId))
                state.equippedSkisId = GetDefaultGearId(CustomizationOptionType.Skis);

            if (string.IsNullOrEmpty(state.equippedPolesId))
                state.equippedPolesId = GetDefaultGearId(CustomizationOptionType.Poles);

            if (string.IsNullOrEmpty(state.equippedHatId))
                state.equippedHatId = GetDefaultId(CustomizationOptionType.Hat);

            if (string.IsNullOrEmpty(state.equippedCloakId))
                state.equippedCloakId = GetDefaultId(CustomizationOptionType.Jacket);
        }

        // Apply continuous values
        if (characterCustomizer != null)
        {
            characterCustomizer.SetSkinColor(state.skinColor);
            characterCustomizer.SetEyeColor(state.eyeColor);

            ApplyCosmetic(state.equippedEyeIconId);
            ApplyCosmetic(state.equippedHatId);
            ApplyCosmetic(state.equippedCloakId);

            characterCustomizer.SetHatColor(state.hatColor);
            characterCustomizer.SetCloakColor(state.jacketColor);
        }

        ApplyGear(state.equippedSkisId, isSkis: true);
        ApplyGear(state.equippedPolesId, isSkis: false);

        if (gearLoadout != null)
        {
            gearLoadout.SetSkisColor(state.skisColor);
            gearLoadout.SetPolesColor(state.polesColor);
        }

        // Apply patterns (textures)
        ApplyGearPatternsFromState(state);
    }

    public void PreviewGearFromOption(PlayerStatsProfile profile, string optionId)
    {
        if (profile == null || profile.customization == null) return;
        if (string.IsNullOrEmpty(optionId) || catalog == null) return;

        var opt = catalog.FindById(optionId);
        if (opt == null) return;

        // Preview is just “apply now” without writing equipped ids.
        if (opt.type == CustomizationOptionType.Skis)
        {
            if (gearLoadout != null && opt.gearProfile != null) gearLoadout.EquipSkis(opt.gearProfile);
            gearLoadout?.SetSkisColor(profile.customization.skisColor);
        }
        else if (opt.type == CustomizationOptionType.Poles)
        {
            if (gearLoadout != null && opt.gearProfile != null) gearLoadout.EquipPoles(opt.gearProfile);
            gearLoadout?.SetPolesColor(profile.customization.polesColor);
        }
    }

    public void ApplySkisColorToLoadout(PlayerStatsProfile profile, Color c)
    {
        if (profile?.customization == null) return;
        if (gearLoadout != null) gearLoadout.SetSkisColor(c);
    }

    public void ApplyPolesColorToLoadout(PlayerStatsProfile profile, Color c)
    {
        if (profile?.customization == null) return;
        if (gearLoadout != null) gearLoadout.SetPolesColor(c);
    }

    private void ApplyGearPatternsFromState(PlayerStatsProfile.CustomizationState state)
    {
        if (state == null || catalog == null) return;

        // Patterns are SkinPattern options; their customizerIndex maps to CharacterCustomizer texture list.
        Texture2D ResolvePattern(string patternId)
        {
            if (string.IsNullOrEmpty(patternId) || characterCustomizer == null) return null;
            var opt = catalog.FindById(patternId);
            if (opt == null || opt.type != CustomizationOptionType.SkinPattern) return null;
            return characterCustomizer.GetSkinPatternTexture2D(opt.customizerIndex);
        }

        var skisTex = ResolvePattern(state.equippedSkisPatternId);
        var polesTex = ResolvePattern(state.equippedPolesPatternId);
        var hatTex = ResolvePattern(state.equippedHatPatternId);
        var jacketTex = ResolvePattern(state.equippedJacketPatternId);

        skiController?.SetSkisPatternTexture(skisTex);
        skiController?.SetPolesPatternTexture(polesTex);

        characterCustomizer?.SetHatPatternTexture(hatTex);
        characterCustomizer?.SetCloakPatternTexture(jacketTex);
    }

    private string GetDefaultId(CustomizationOptionType type)
    {
        if (catalog == null) return null;

        foreach (var o in catalog.GetByType(type))
            if (o != null && o.customizerIndex == 0)
                return o.id;

        foreach (var o in catalog.GetByType(type))
            if (o != null)
                return o.id;

        return null;
    }

    private string GetDefaultGearId(CustomizationOptionType type)
    {
        if (catalog == null) return null;

        foreach (var o in catalog.GetByType(type))
            if (o != null && o.gearProfile != null)
                return o.id;

        return null;
    }

    private void ApplyCosmetic(string optionId)
    {
        if (string.IsNullOrEmpty(optionId) || catalog == null || characterCustomizer == null) return;

        var opt = catalog.FindById(optionId);
        if (opt == null) return;

        switch (opt.type)
        {
            case CustomizationOptionType.EyeIcon:
                characterCustomizer.SetEyeStyle(opt.customizerIndex);
                break;
            case CustomizationOptionType.Hat:
                characterCustomizer.SetHat(opt.customizerIndex);
                break;
            case CustomizationOptionType.Jacket:
                characterCustomizer.SetCloak(opt.customizerIndex);
                break;
        }
    }

    private void ApplyGear(string optionId, bool isSkis)
    {
        if (string.IsNullOrEmpty(optionId) || catalog == null || gearLoadout == null) return;

        var opt = catalog.FindById(optionId);
        if (opt == null || opt.gearProfile == null) return;

        if (isSkis) gearLoadout.EquipSkis(opt.gearProfile);
        else gearLoadout.EquipPoles(opt.gearProfile);
    }
}
