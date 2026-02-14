using System;
using System.Collections.Generic;
using UnityEngine;
using SkiGame.Progression;

public class CustomizationUIController : MonoBehaviour
{
    public enum RootTab { Shop, Inventory }
    public enum SubTab { Cosmetics, Gear }

    public enum PatternTarget { Skis, Poles, Hat, Jacket }

    [Header("Runtime (read-only)")]
    [SerializeField] private GameObject playerRoot;
    [SerializeField] private CharacterCustomizer customizer;
    [SerializeField] private PlayerCustomizationApplier applier;
    [SerializeField] private SkiController skiController;

    [Header("Shop Rotation")]
    [SerializeField] private int dailySkinPatternOffers = 6;
    [SerializeField] private int dailyEyeIconOffers = 6;
    [SerializeField] private int cosmeticStockPerOffer = 25;
    [SerializeField] private int dailySkisOffers = 6;
    [SerializeField] private int dailyPolesOffers = 6;
    [SerializeField] private int dailyHatOffers = 4;
    [SerializeField] private int dailyJacketOffers = 4;

    private CustomizationCatalogSO _catalog;
    private PlayerStatsProfile _profile;
    private Action<bool> _onRequestExit;

    private RootTab _rootTab = RootTab.Shop;
    private SubTab _subTab = SubTab.Cosmetics;

    private CustomizationOptionType _activeCategory = CustomizationOptionType.SkinPattern;
    private PatternTarget _patternTarget = PatternTarget.Skis;

    private readonly List<CustomizationOptionSO> _visible = new();
    private readonly List<CustomizationOptionSO> _owned = new();

    private CustomizationOptionSO _selected;
    private int _todayKey;

    // Preview ids (unowned only)
    private string _previewEyeId;
    private string _previewSkisId;
    private string _previewPolesId;
    private string _previewHatId;
    private string _previewJacketId;

    private string _previewSkisPatternId;
    private string _previewPolesPatternId;
    private string _previewHatPatternId;
    private string _previewJacketPatternId;

    public void Open(GameObject playerRoot, GameObject previewRoot, CustomizationCatalogSO catalog, PlayerStatsProfile profile, Action<bool> onRequestExit)
    {
        this.playerRoot = playerRoot;
        _catalog = catalog;
        _profile = profile;
        _onRequestExit = onRequestExit;

        customizer = playerRoot != null ? playerRoot.GetComponentInChildren<CharacterCustomizer>(true) : null;
        applier = playerRoot != null ? playerRoot.GetComponent<PlayerCustomizationApplier>() : null;
        skiController = playerRoot != null ? playerRoot.GetComponent<SkiController>() : null;

        _todayKey = DailyShopService.GetUtcDayKey();

        DailyShopService.EnsureDailyOffers(
            _catalog, _todayKey,
            dailySkinPatternOffers,
            dailyEyeIconOffers,
            dailySkisOffers,
            dailyPolesOffers,
            dailyHatOffers,
            dailyJacketOffers,
            cosmeticStockPerOffer,
            isOwnedId: (id) => _profile != null && _profile.customization != null && _profile.customization.IsUnlocked(id)
        );

        _rootTab = RootTab.Shop;
        _subTab = SubTab.Cosmetics;
        _activeCategory = CustomizationOptionType.SkinPattern;
        _patternTarget = PatternTarget.Skis;

        // Preview starts “none” (we only preview unowned)
        _previewEyeId = null;
        _previewSkisId = null;
        _previewPolesId = null;
        _previewHatId = null;
        _previewJacketId = null;

        _previewSkisPatternId = null;
        _previewPolesPatternId = null;
        _previewHatPatternId = null;
        _previewJacketPatternId = null;

        RebuildLists();
    }

    // ---------------- Queries for UI ----------------

    public RootTab GetRootTab() => _rootTab;
    public SubTab GetSubTab() => _subTab;

    public int GetCurrency() => _profile != null ? _profile.currency : 0;

    public CustomizationOptionType GetActiveCategory() => _activeCategory;

    public PatternTarget GetPatternTarget() => _patternTarget;
    public void SetPatternTarget(PatternTarget t) => _patternTarget = t;

    public IReadOnlyList<CustomizationOptionSO> GetVisibleList() => _visible;
    public IReadOnlyList<CustomizationOptionSO> GetOwnedList() => _owned;

    public CustomizationOptionSO GetSelected() => _selected;

    public bool IsOwned(CustomizationOptionSO opt)
    {
        if (opt == null) return false;
        if (opt.cost <= 0) return true;
        if (_profile == null || _profile.customization == null) return false;
        return _profile.customization.IsUnlocked(opt.id);
    }

    // ---------------- Tab controls ----------------

    public void SetRootTab(RootTab tab)
    {
        if (_rootTab == tab) return;
        _rootTab = tab;
        _selected = null;
        RebuildLists();
    }

    public void SetSubTab(SubTab tab)
    {
        if (_subTab == tab) return;
        _subTab = tab;
        _selected = null;
        RebuildLists();
    }

    public void SetCategory(CustomizationOptionType t)
    {
        _activeCategory = t;
        _selected = null;
        RebuildLists();
    }

    // ---------------- Selection / preview / equip ----------------

    public void Select(CustomizationOptionSO opt)
    {
        _selected = opt;
        if (_selected == null) return;

        // If owned, equip immediately (no preview-first)
        if (IsOwned(_selected))
        {
            EquipSelected();
            return;
        }

        // Otherwise: preview only
        PreviewSelected();
    }

    private void PreviewSelected()
    {
        if (_selected == null) return;

        switch (_selected.type)
        {
            case CustomizationOptionType.EyeIcon:
                _previewEyeId = _selected.id;
                if (customizer != null) customizer.SetEyeStyle(_selected.customizerIndex);
                break;

            case CustomizationOptionType.Skis:
                _previewSkisId = _selected.id;
                if (applier != null && _profile != null) applier.PreviewGearFromOption(_profile, _selected.id);
                break;

            case CustomizationOptionType.Poles:
                _previewPolesId = _selected.id;
                if (applier != null && _profile != null) applier.PreviewGearFromOption(_profile, _selected.id);
                break;

            case CustomizationOptionType.Hat:
                _previewHatId = _selected.id;
                if (customizer != null) customizer.SetHat(_selected.customizerIndex);
                if (_profile?.customization != null) customizer?.SetHatColor(_profile.customization.hatColor);
                break;

            case CustomizationOptionType.Jacket:
                _previewJacketId = _selected.id;
                if (customizer != null) customizer.SetCloak(_selected.customizerIndex);
                if (_profile?.customization != null) customizer?.SetCloakColor(_profile.customization.jacketColor);
                break;

            case CustomizationOptionType.SkinPattern:
                // Patterns are gear textures now. Preview applies to current target.
                PreviewPatternToTarget(_selected);
                break;
        }
    }

    private void PreviewPatternToTarget(CustomizationOptionSO patternOpt)
    {
        if (patternOpt == null || patternOpt.type != CustomizationOptionType.SkinPattern) return;
        if (customizer == null && skiController == null) return;

        var tex = customizer != null ? customizer.GetSkinPatternTexture2D(patternOpt.customizerIndex) : null;

        switch (_patternTarget)
        {
            case PatternTarget.Skis:
                _previewSkisPatternId = patternOpt.id;
                skiController?.SetSkisPatternTexture(tex);
                break;
            case PatternTarget.Poles:
                _previewPolesPatternId = patternOpt.id;
                skiController?.SetPolesPatternTexture(tex);
                break;
            case PatternTarget.Hat:
                _previewHatPatternId = patternOpt.id;
                customizer?.SetHatPatternTexture(tex);
                break;
            case PatternTarget.Jacket:
                _previewJacketPatternId = patternOpt.id;
                customizer?.SetCloakPatternTexture(tex);
                break;
        }
    }

    // ---------------- Actions ----------------

    public void TryBuySelected()
    {
        if (_selected == null) return;
        if (_rootTab != RootTab.Shop) return;

        if (IsOwned(_selected))
        {
            EquipSelected();
            return;
        }

        if (!DailyShopService.IsInTodayOffers(_selected.id, _todayKey))
            return;

        if (_profile == null || _profile.customization == null) return;
        if (_profile.currency < _selected.cost) return;

        _profile.currency -= _selected.cost;
        _profile.customization.Unlock(_selected.id);

        RebuildLists();
    }

    public void EquipSelected()
    {
        if (_selected == null) return;
        if (_profile == null || _profile.customization == null) return;
        if (!IsOwned(_selected)) return;

        var s = _profile.customization;

        switch (_selected.type)
        {
            case CustomizationOptionType.EyeIcon:
                s.equippedEyeIconId = _selected.id;
                if (customizer != null) customizer.SetEyeStyle(_selected.customizerIndex);
                break;

            case CustomizationOptionType.Skis:
                s.equippedSkisId = _selected.id;
                applier?.ApplyFromProfile(_profile);
                break;

            case CustomizationOptionType.Poles:
                s.equippedPolesId = _selected.id;
                applier?.ApplyFromProfile(_profile);
                break;

            case CustomizationOptionType.Hat:
                s.equippedHatId = _selected.id;
                if (customizer != null) customizer.SetHat(_selected.customizerIndex);
                customizer?.SetHatColor(s.hatColor);
                break;

            case CustomizationOptionType.Jacket:
                s.equippedCloakId = _selected.id;
                if (customizer != null) customizer.SetCloak(_selected.customizerIndex);
                customizer?.SetCloakColor(s.jacketColor);
                break;

            case CustomizationOptionType.SkinPattern:
                // Equip pattern to current target
                EquipPatternToTarget(_selected);
                break;
        }
    }

    private void EquipPatternToTarget(CustomizationOptionSO patternOpt)
    {
        if (patternOpt == null || patternOpt.type != CustomizationOptionType.SkinPattern) return;
        var s = _profile.customization;

        var tex = customizer != null ? customizer.GetSkinPatternTexture2D(patternOpt.customizerIndex) : null;

        switch (_patternTarget)
        {
            case PatternTarget.Skis:
                s.equippedSkisPatternId = patternOpt.id;
                skiController?.SetSkisPatternTexture(tex);
                break;
            case PatternTarget.Poles:
                s.equippedPolesPatternId = patternOpt.id;
                skiController?.SetPolesPatternTexture(tex);
                break;
            case PatternTarget.Hat:
                s.equippedHatPatternId = patternOpt.id;
                customizer?.SetHatPatternTexture(tex);
                break;
            case PatternTarget.Jacket:
                s.equippedJacketPatternId = patternOpt.id;
                customizer?.SetCloakPatternTexture(tex);
                break;
        }
    }

    // ---------------- Colors ----------------

    public void SetSkinColor(Color c)
    {
        if (_profile?.customization == null) return;
        _profile.customization.skinColor = c;
        customizer?.SetSkinColor(c);
    }

    public void SetEyeColor(Color c)
    {
        if (_profile?.customization == null) return;
        _profile.customization.eyeColor = c;
        customizer?.SetEyeColor(c);
    }

    public void SetSkisColor(Color c)
    {
        if (_profile?.customization == null) return;
        _profile.customization.skisColor = c;
        // SkiController listens to gearLoadout for color changes; applier keeps that synced.
        applier?.ApplySkisColorToLoadout(_profile, c);
    }

    public void SetPolesColor(Color c)
    {
        if (_profile?.customization == null) return;
        _profile.customization.polesColor = c;
        applier?.ApplyPolesColorToLoadout(_profile, c);
    }

    public void SetHatColor(Color c)
    {
        if (_profile?.customization == null) return;
        _profile.customization.hatColor = c;
        customizer?.SetHatColor(c);
    }

    public void SetJacketColor(Color c)
    {
        if (_profile?.customization == null) return;
        _profile.customization.jacketColor = c;
        customizer?.SetCloakColor(c);
    }

    public Color GetSkinColor() => _profile?.customization != null ? _profile.customization.skinColor : Color.white;
    public Color GetEyeColor() => _profile?.customization != null ? _profile.customization.eyeColor : Color.white;
    public Color GetSkisColor() => _profile?.customization != null ? _profile.customization.skisColor : Color.white;
    public Color GetPolesColor() => _profile?.customization != null ? _profile.customization.polesColor : Color.white;
    public Color GetHatColor() => _profile?.customization != null ? _profile.customization.hatColor : Color.white;
    public Color GetJacketColor() => _profile?.customization != null ? _profile.customization.jacketColor : Color.white;

    public void Exit(bool apply) => _onRequestExit?.Invoke(apply);

    // ---------------- Internals ----------------

    private void RebuildLists()
    {
        _visible.Clear();
        _owned.Clear();

        if (_catalog == null) return;

        if (_rootTab == RootTab.Shop)
        {
            DailyShopService.GetTodayOffers(_catalog, _todayKey, _activeCategory, _visible);
        }
        else
        {
            foreach (var o in _catalog.GetByType(_activeCategory))
            {
                if (o == null) continue;
                if (IsOwned(o)) _owned.Add(o);
            }
        }
    }
}
