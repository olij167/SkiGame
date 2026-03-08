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

    private CustomizationOptionSO _noneHat;
    private CustomizationOptionSO _noneJacket;

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

    public event Action OnChanged;


    public bool IsOpen => _catalog != null && _profile != null && playerRoot != null;

    private void NotifyChanged()
    {
        try { OnChanged?.Invoke(); }
        catch (Exception e) { Debug.LogException(e); }
    }

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
        NotifyChanged();
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

        // IMPORTANT: Do NOT clear previews here.
        // Previews must persist across Shop/Inventory swaps so the user can preview multiple slots.

        RebuildLists();
        NotifyChanged();
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
        if (_activeCategory == t) return;

        _activeCategory = t;
        _selected = null;

        // IMPORTANT: Do NOT clear previews here.
        // Users must be able to switch to patterns (or any category) while keeping previews applied.

        RebuildLists();
        NotifyChanged();
    }

    // ---------------- Selection / preview / equip ----------------

    public void Select(CustomizationOptionSO opt)
    {
        _selected = opt;
        if (_selected == null) return;

        // Owned items are equipped immediately (this becomes the authoritative selection for that slot).
        // To avoid “effective” preview overriding the equip, clear preview ONLY for that slot/type.
        if (IsOwned(_selected))
        {
            ClearPreviewForType(_selected.type);
            EquipSelected(); // EquipSelected will call ReapplyEquippedThenPreviews()
            return;
        }

        // Unowned items are previewed (previews persist across tabs).
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
                if (customizer != null) customizer.SetJacket(_selected.customizerIndex);
                if (_profile?.customization != null) customizer?.SetJacketColor(_profile.customization.jacketColor);
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

        var tex = ResolvePatternTexture(patternOpt);

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
                customizer?.SetJacketPatternTexture(tex);
                break;
        }
    }

    private CustomizationOptionSO GetNoneOption(CustomizationOptionType type)
    {
        if (type == CustomizationOptionType.Hat)
        {
            if (_noneHat == null)
            {
                _noneHat = ScriptableObject.CreateInstance<CustomizationOptionSO>();
                _noneHat.hideFlags = HideFlags.HideAndDontSave;
                _noneHat.id = ""; // empty means none
                _noneHat.type = CustomizationOptionType.Hat;
                _noneHat.displayName = "None";
                _noneHat.description = "No hat equipped.";
                _noneHat.cost = 0;
                _noneHat.customizerIndex = -1;
            }
            return _noneHat;
        }

        if (type == CustomizationOptionType.Jacket)
        {
            if (_noneJacket == null)
            {
                _noneJacket = ScriptableObject.CreateInstance<CustomizationOptionSO>();
                _noneJacket.hideFlags = HideFlags.HideAndDontSave;
                _noneJacket.id = "";
                _noneJacket.type = CustomizationOptionType.Jacket;
                _noneJacket.displayName = "None";
                _noneJacket.description = "No jacket equipped.";
                _noneJacket.cost = 0;
                _noneJacket.customizerIndex = -1;
            }
            return _noneJacket;
        }

        return null;
    }

    // ---------------- Preview clearing (UI "X" buttons) ----------------

    public void ClearPreviewEye()
    {
        _previewEyeId = null;
        ReapplyEquippedThenPreviews();
        NotifyChanged();
    }

    public void ClearPreviewSkis()
    {
        _previewSkisId = null;
        ReapplyEquippedThenPreviews();
        NotifyChanged();
    }

    public void ClearPreviewPoles()
    {
        _previewPolesId = null;
        ReapplyEquippedThenPreviews();
        NotifyChanged();
    }

    public void ClearPreviewHat()
    {
        _previewHatId = null;
        ReapplyEquippedThenPreviews();
        NotifyChanged();
    }

    public void ClearPreviewJacket()
    {
        _previewJacketId = null;
        ReapplyEquippedThenPreviews();
        NotifyChanged();
    }

    // Optional (if you later add preview badges for patterns too)
    public void ClearPreviewPattern(PatternTarget t)
    {
        switch (t)
        {
            case PatternTarget.Skis: _previewSkisPatternId = null; break;
            case PatternTarget.Poles: _previewPolesPatternId = null; break;
            case PatternTarget.Hat: _previewHatPatternId = null; break;
            case PatternTarget.Jacket: _previewJacketPatternId = null; break;
        }

        ReapplyEquippedThenPreviews();
        NotifyChanged();
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

        // Unlock any patterns granted by this purchase (preferred SO refs, fallback ids)
        if (_selected.unlockPatternOptionsOnPurchase != null)
        {
            foreach (var pat in _selected.unlockPatternOptionsOnPurchase)
                if (pat != null && !string.IsNullOrEmpty(pat.id))
                    _profile.customization.Unlock(pat.id);
        }
        if (_selected.unlockPatternIdsOnPurchase != null)
        {
            foreach (var pid in _selected.unlockPatternIdsOnPurchase)
                if (!string.IsNullOrEmpty(pid))
                    _profile.customization.Unlock(pid);
        }

        RebuildLists();

    }

    public void EquipSelected()
    {
        if (_selected == null) return;
        if (_profile == null || _profile.customization == null) return;
        if (!IsOwned(_selected)) return;

        // Safety: if called directly, ensure the slot preview is cleared
        ClearPreviewForType(_selected.type);

        var s = _profile.customization;

        switch (_selected.type)
        {
            case CustomizationOptionType.EyeIcon:
                s.equippedEyeIconId = _selected.id;
                break;

            case CustomizationOptionType.Skis:
                s.equippedSkisId = _selected.id;
                break;

            case CustomizationOptionType.Poles:
                s.equippedPolesId = _selected.id;
                break;

            case CustomizationOptionType.Hat:
                // "" = None
                s.equippedHatId = _selected.id;
                break;

            case CustomizationOptionType.Jacket:
                // "" = None
                s.equippedJacketId = _selected.id;
                break;

            case CustomizationOptionType.SkinPattern:
                // Equip pattern to current target. Clear preview for that target first so equip wins.
                ClearPreviewForType(CustomizationOptionType.SkinPattern);
                EquipPatternToTarget(_selected);
                break;
        }

        // IMPORTANT: apply equipped baseline, then re-apply remaining previews for OTHER slots
        ReapplyEquippedThenPreviews();
    }

    public void ResetSkisColorToDefault()
    {
        if (_profile?.customization == null) return;
        var s = _profile.customization;
        var opt = ResolveById(s.equippedSkisId);
        var c = (opt != null && opt.HasTint) ? opt.DefaultTint : Color.white;

        s.skisColor = c;
        s.hasSetSkisColor = false;
        applier?.ApplySkisColorToLoadout(_profile, c);
    }
    public void ResetPolesColorToDefault()
    {
        if (_profile?.customization == null) return;
        var s = _profile.customization;
        var opt = ResolveById(s.equippedPolesId);
        var c = (opt != null && opt.HasTint) ? opt.DefaultTint : Color.white;

        s.polesColor = c;
        s.hasSetPolesColor = false;
        applier?.ApplyPolesColorToLoadout(_profile, c);
    }

    public void ResetHatColorToDefault()
    {
        if (_profile?.customization == null) return;
        var s = _profile.customization;
        var opt = ResolveById(s.equippedHatId);
        var c = (opt != null && opt.HasTint) ? opt.DefaultTint : Color.white;

        s.hatColor = c;
        s.hasSetHatColor = false;
        customizer?.SetHatColor(c);
    }

    public void ResetJacketColorToDefault()
    {
        if (_profile?.customization == null) return;
        var s = _profile.customization;
        var opt = ResolveById(s.equippedJacketId);
        var c = (opt != null && opt.HasTint) ? opt.DefaultTint : Color.white;

        s.jacketColor = c;
        s.hasSetJacketColor = false;
        customizer?.SetJacketColor(c);
    }

    public void ResetPatternToDefault(PatternTarget t)
    {
        if (_profile?.customization == null) return;
        var s = _profile.customization;

        // Empty means “use equipped gear’s defaultPatternId”
        switch (t)
        {
            case PatternTarget.Skis: s.equippedSkisPatternId = ""; break;
            case PatternTarget.Poles: s.equippedPolesPatternId = ""; break;
            case PatternTarget.Hat: s.equippedHatPatternId = ""; break;
            case PatternTarget.Jacket: s.equippedJacketPatternId = ""; break;
        }

        applier?.ApplyFromProfile(_profile);
    }

    private void EquipPatternToTarget(CustomizationOptionSO patternOpt)
    {
        if (patternOpt == null || patternOpt.type != CustomizationOptionType.SkinPattern) return;
        if (_profile?.customization == null) return;

        var s = _profile.customization;
        var tex = ResolvePatternTexture(patternOpt) as Texture2D;

        switch (_patternTarget)
        {
            case PatternTarget.Skis:
                s.equippedSkisPatternId = patternOpt.id;
                s.skisUseDefaultPattern = false; // switch to custom
                skiController?.SetSkisPatternTexture(tex);
                break;

            case PatternTarget.Poles:
                s.equippedPolesPatternId = patternOpt.id;
                s.polesUseDefaultPattern = false;
                skiController?.SetPolesPatternTexture(tex);
                break;

            case PatternTarget.Hat:
                s.equippedHatPatternId = patternOpt.id;
                s.hatUseDefaultPattern = false;
                customizer?.SetHatPatternTexture(tex);
                break;

            case PatternTarget.Jacket:
                s.equippedJacketPatternId = patternOpt.id;
                s.jacketUseDefaultPattern = false;
                customizer?.SetJacketPatternTexture(tex);
                break;
        }

        // Keep runtime visuals coherent
        applier?.ApplyFromProfile(_profile);
    }

    // ---------------- Colors ----------------

    public void SetSkinColor(Color c)
    {
        if (_profile?.customization == null) return;
        _profile.customization.skinColor = c;
        _profile.customization.hasSetSkinColor = true;

        customizer?.SetSkinColor(c);
    }

    public void SetEyeColor(Color c)
    {
        if (_profile?.customization == null) return;
        _profile.customization.eyeColor = c;
        _profile.customization.hasSetEyeColor = true;

        customizer?.SetEyeColor(c);
    }

    public void SetSkisColor(Color c)
    {
        if (_profile?.customization == null) return;
        var s = _profile.customization;

        s.skisColor = c;              // store custom selection
        s.hasSetSkisColor = true;
        s.skisUseDefaultColor = false;

        applier?.ApplyFromProfile(_profile);
    }

    public void SetPolesColor(Color c)
    {
        if (_profile?.customization == null) return;
        var s = _profile.customization;

        s.polesColor = c;
        s.hasSetPolesColor = true;
        s.polesUseDefaultColor = false;

        applier?.ApplyFromProfile(_profile);
    }

    public void SetHatColor(Color c)
    {
        if (_profile?.customization == null) return;
        var s = _profile.customization;

        s.hatColor = c;
        s.hasSetHatColor = true;
        s.hatUseDefaultColor = false;

        applier?.ApplyFromProfile(_profile);
    }

    public void SetJacketColor(Color c)
    {
        if (_profile?.customization == null) return;
        var s = _profile.customization;

        s.jacketColor = c;
        s.hasSetJacketColor = true;
        s.jacketUseDefaultColor = false;

        applier?.ApplyFromProfile(_profile);
    }

    public Color GetSkinColor() => _profile?.customization != null ? _profile.customization.skinColor : Color.white;
    public Color GetEyeColor() => _profile?.customization != null ? _profile.customization.eyeColor : Color.white;
    public Color GetSkisColor() => _profile?.customization != null ? _profile.customization.skisColor : Color.white;
    public Color GetPolesColor() => _profile?.customization != null ? _profile.customization.polesColor : Color.white;
    public Color GetHatColor() => _profile?.customization != null ? _profile.customization.hatColor : Color.white;
    public Color GetJacketColor() => _profile?.customization != null ? _profile.customization.jacketColor : Color.white;

    // ---------------- Reset toggles (default <-> custom) ----------------

    public void ToggleSkisColorDefault()
    {
        if (_profile?.customization == null) return;
        _profile.customization.skisUseDefaultColor = !_profile.customization.skisUseDefaultColor;
        applier?.ApplyFromProfile(_profile);
    }

    public void TogglePolesColorDefault()
    {
        if (_profile?.customization == null) return;
        _profile.customization.polesUseDefaultColor = !_profile.customization.polesUseDefaultColor;
        applier?.ApplyFromProfile(_profile);
    }

    public void ToggleHatColorDefault()
    {
        if (_profile?.customization == null) return;
        _profile.customization.hatUseDefaultColor = !_profile.customization.hatUseDefaultColor;
        applier?.ApplyFromProfile(_profile);
    }

    public void ToggleJacketColorDefault()
    {
        if (_profile?.customization == null) return;
        _profile.customization.jacketUseDefaultColor = !_profile.customization.jacketUseDefaultColor;
        applier?.ApplyFromProfile(_profile);
    }

    public void TogglePatternDefault(PatternTarget t)
    {
        if (_profile?.customization == null) return;
        var s = _profile.customization;

        switch (t)
        {
            case PatternTarget.Skis: s.skisUseDefaultPattern = !s.skisUseDefaultPattern; break;
            case PatternTarget.Poles: s.polesUseDefaultPattern = !s.polesUseDefaultPattern; break;
            case PatternTarget.Hat: s.hatUseDefaultPattern = !s.hatUseDefaultPattern; break;
            case PatternTarget.Jacket: s.jacketUseDefaultPattern = !s.jacketUseDefaultPattern; break;
        }

        applier?.ApplyFromProfile(_profile);
    }

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
            if (_activeCategory == CustomizationOptionType.Hat || _activeCategory == CustomizationOptionType.Jacket)
{
                _owned.Insert(0, GetNoneOption(_activeCategory));
            }

            foreach (var o in _catalog.GetByType(_activeCategory))
            {
                if (o == null) continue;
                if (IsOwned(o)) _owned.Add(o);
            }
        }
    }

    public CustomizationOptionSO ResolveById(string id)
    {
        if (string.IsNullOrEmpty(id) || _catalog == null) return null;
        return _catalog.FindById(id);
    }

    private Texture ResolvePatternTexture(CustomizationOptionSO opt)
    {
        if (opt == null || opt.type != CustomizationOptionType.SkinPattern) return null;

        // Preferred: direct payload on the option
        if (opt.skinPatternTexture != null)
            return opt.skinPatternTexture;

        // Back-compat: index into CharacterCustomizer arrays
        if (customizer != null && opt.customizerIndex >= 0)
            return customizer.GetSkinPatternTexture2D(opt.customizerIndex);

        return null;
    }

    // Public for UI (shop/inventory icon slots)
    public Texture GetPatternTexture(CustomizationOptionSO opt) => ResolvePatternTexture(opt);

    public CustomizationOptionSO GetEquippedEyeOption() => ResolveById(_profile?.customization?.equippedEyeIconId);
    public CustomizationOptionSO GetEquippedSkisOption() => ResolveById(_profile?.customization?.equippedSkisId);
    public CustomizationOptionSO GetEquippedPolesOption() => ResolveById(_profile?.customization?.equippedPolesId);
    public CustomizationOptionSO GetEquippedHatOption() => ResolveById(_profile?.customization?.equippedHatId);
    public CustomizationOptionSO GetEquippedJacketOption() => ResolveById(_profile?.customization?.equippedJacketId);

    public string GetEquippedPatternId(PatternTarget t)
    {
        var s = _profile?.customization;
        if (s == null) return null;

        return t switch
        {
            PatternTarget.Skis => s.equippedSkisPatternId,
            PatternTarget.Poles => s.equippedPolesPatternId,
            PatternTarget.Hat => s.equippedHatPatternId,
            PatternTarget.Jacket => s.equippedJacketPatternId,
            _ => null
        };
    }

    public Texture2D GetEquippedPatternTexture(PatternTarget t)
    {
        var id = GetEquippedPatternId(t);
        var opt = ResolveById(id);
        if (opt == null || opt.type != CustomizationOptionType.SkinPattern) return null;
        return ResolvePatternTexture(opt) as Texture2D;
    }

    public CustomizationOptionSO GetEquippedPatternOption(PatternTarget t)
    {
        var id = GetEquippedPatternId(t);
        var opt = ResolveById(id);
        return (opt != null && opt.type == CustomizationOptionType.SkinPattern) ? opt : null;
    }

    // ---------------- Preview + Effective getters (for UI) ----------------

    public bool IsPreviewingEye() => !string.IsNullOrEmpty(_previewEyeId);
    public bool IsPreviewingSkis() => !string.IsNullOrEmpty(_previewSkisId);
    public bool IsPreviewingPoles() => !string.IsNullOrEmpty(_previewPolesId);
    public bool IsPreviewingHat() => !string.IsNullOrEmpty(_previewHatId);
    public bool IsPreviewingJacket() => !string.IsNullOrEmpty(_previewJacketId);

    public CustomizationOptionSO GetPreviewEyeOption() => ResolveById(_previewEyeId);
    public CustomizationOptionSO GetPreviewSkisOption() => ResolveById(_previewSkisId);
    public CustomizationOptionSO GetPreviewPolesOption() => ResolveById(_previewPolesId);
    public CustomizationOptionSO GetPreviewHatOption() => ResolveById(_previewHatId);
    public CustomizationOptionSO GetPreviewJacketOption() => ResolveById(_previewJacketId);

    public CustomizationOptionSO GetEffectiveEyeOption() => GetPreviewEyeOption() ?? GetEquippedEyeOption();
    public CustomizationOptionSO GetEffectiveSkisOption() => GetPreviewSkisOption() ?? GetEquippedSkisOption();
    public CustomizationOptionSO GetEffectivePolesOption() => GetPreviewPolesOption() ?? GetEquippedPolesOption();
    public CustomizationOptionSO GetEffectiveHatOption() => GetPreviewHatOption() ?? GetEquippedHatOption();
    public CustomizationOptionSO GetEffectiveJacketOption() => GetPreviewJacketOption() ?? GetEquippedJacketOption();

    // Patterns
    public string GetPreviewPatternId(PatternTarget t)
    {
        return t switch
        {
            PatternTarget.Skis => _previewSkisPatternId,
            PatternTarget.Poles => _previewPolesPatternId,
            PatternTarget.Hat => _previewHatPatternId,
            PatternTarget.Jacket => _previewJacketPatternId,
            _ => null
        };
    }

    public bool IsPreviewingPattern(PatternTarget t) => !string.IsNullOrEmpty(GetPreviewPatternId(t));

    public CustomizationOptionSO GetPreviewPatternOption(PatternTarget t)
    {
        var id = GetPreviewPatternId(t);
        var opt = ResolveById(id);
        return (opt != null && opt.type == CustomizationOptionType.SkinPattern) ? opt : null;
    }

    public Texture2D GetPreviewPatternTexture(PatternTarget t)
    {
        var opt = GetPreviewPatternOption(t);
        if (opt == null) return null;
        return ResolvePatternTexture(opt) as Texture2D;
    }

    public CustomizationOptionSO GetEffectivePatternOption(PatternTarget t) => GetPreviewPatternOption(t) ?? GetEquippedPatternOption(t);
    public Texture2D GetEffectivePatternTexture(PatternTarget t) => GetPreviewPatternTexture(t) ?? GetEquippedPatternTexture(t);

    public Texture2D GetPatternTexture2D(CustomizationOptionSO opt)
    {
        if (opt == null || opt.type != CustomizationOptionType.SkinPattern) return null;

        // Preferred: direct payload (only if it's actually a Texture2D)
        if (opt.skinPatternTexture is Texture2D t2d)
            return t2d;

        // Back-compat: index into CharacterCustomizer arrays
        if (customizer != null && opt.customizerIndex >= 0)
            return customizer.GetSkinPatternTexture2D(opt.customizerIndex);

        return null;
    }

    // Clears ONLY the preview state for the given option type (and for patterns: only current pattern target).
    private void ClearPreviewForType(CustomizationOptionType t)
    {
        switch (t)
        {
            case CustomizationOptionType.EyeIcon: _previewEyeId = null; break;
            case CustomizationOptionType.Skis: _previewSkisId = null; break;
            case CustomizationOptionType.Poles: _previewPolesId = null; break;
            case CustomizationOptionType.Hat: _previewHatId = null; break;
            case CustomizationOptionType.Jacket: _previewJacketId = null; break;

            case CustomizationOptionType.SkinPattern:
                switch (_patternTarget)
                {
                    case PatternTarget.Skis: _previewSkisPatternId = null; break;
                    case PatternTarget.Poles: _previewPolesPatternId = null; break;
                    case PatternTarget.Hat: _previewHatPatternId = null; break;
                    case PatternTarget.Jacket: _previewJacketPatternId = null; break;
                }
                break;
        }
    }

    // Rebuilds the player visuals as: equipped baseline -> then all active previews as per-slot overrides.
    // This preserves previews across tab swaps and across equips in other slots.
    private void ReapplyEquippedThenPreviews()
    {
        if (_profile == null) return;

        // 1) Apply the currently equipped baseline.
        applier?.ApplyFromProfile(_profile);

        // 2) Re-apply preview overrides per slot (only if a preview exists for that slot).
        if (_catalog == null) return;

        // Eye preview
        if (!string.IsNullOrEmpty(_previewEyeId))
        {
            var opt = _catalog.FindById(_previewEyeId);
            if (opt != null && opt.type == CustomizationOptionType.EyeIcon)
                customizer?.SetEyeStyle(opt.customizerIndex);
        }

        // Gear previews (skis/poles use applier preview so they swap prefabs properly)
        if (!string.IsNullOrEmpty(_previewSkisId))
            applier?.PreviewGearFromOption(_profile, _previewSkisId);

        if (!string.IsNullOrEmpty(_previewPolesId))
            applier?.PreviewGearFromOption(_profile, _previewPolesId);

        // Hat / Jacket previews
        if (!string.IsNullOrEmpty(_previewHatId))
        {
            var opt = _catalog.FindById(_previewHatId);
            if (opt != null && opt.type == CustomizationOptionType.Hat)
            {
                customizer?.SetHat(opt.customizerIndex);
                if (_profile.customization != null) customizer?.SetHatColor(_profile.customization.hatColor);
            }
            else if (_previewHatId == "")
            {
                customizer?.ClearHat();
            }
        }

        if (!string.IsNullOrEmpty(_previewJacketId))
        {
            var opt = _catalog.FindById(_previewJacketId);
            if (opt != null && opt.type == CustomizationOptionType.Jacket)
            {
                customizer?.SetJacket(opt.customizerIndex);
                if (_profile.customization != null) customizer?.SetJacketColor(_profile.customization.jacketColor);
            }
            else if (_previewJacketId == "")
            {
                customizer?.ClearJacket();
            }
        }

        // Pattern previews (apply by id if present)
        if (!string.IsNullOrEmpty(_previewSkisPatternId))
        {
            var opt = _catalog.FindById(_previewSkisPatternId);
            if (opt != null && opt.type == CustomizationOptionType.SkinPattern)
                skiController?.SetSkisPatternTexture(ResolvePatternTexture(opt));
        }

        if (!string.IsNullOrEmpty(_previewPolesPatternId))
        {
            var opt = _catalog.FindById(_previewPolesPatternId);
            if (opt != null && opt.type == CustomizationOptionType.SkinPattern)
                skiController?.SetPolesPatternTexture(ResolvePatternTexture(opt));
        }

        if (!string.IsNullOrEmpty(_previewHatPatternId))
        {
            var opt = _catalog.FindById(_previewHatPatternId);
            if (opt != null && opt.type == CustomizationOptionType.SkinPattern)
                customizer?.SetHatPatternTexture(ResolvePatternTexture(opt));
        }

        if (!string.IsNullOrEmpty(_previewJacketPatternId))
        {
            var opt = _catalog.FindById(_previewJacketPatternId);
            if (opt != null && opt.type == CustomizationOptionType.SkinPattern)
                customizer?.SetJacketPatternTexture(ResolvePatternTexture(opt));
        }
    }

    private void ClearAllPreviews()
    {
        _previewEyeId = null;
        _previewSkisId = null;
        _previewPolesId = null;
        _previewHatId = null;
        _previewJacketId = null;

        _previewSkisPatternId = null;
        _previewPolesPatternId = null;
        _previewHatPatternId = null;
        _previewJacketPatternId = null;
    }

}
