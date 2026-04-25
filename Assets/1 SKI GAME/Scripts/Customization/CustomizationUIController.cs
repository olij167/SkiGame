using System;
using System.Collections.Generic;
using UnityEngine;
using SkiGame.Progression;

public class CustomizationUIController : MonoBehaviour
{
    public enum RootTab { Shop, Inventory }
    public enum SubTab { Cosmetics, Gear }

    public enum PatternTarget { Skis, Poles, Hat, Jacket, Gloves, Boots, Accessory }

    [Serializable]
    public sealed class GearColorChannelInfo
    {
        public string id;
        public string displayName;
        public Color defaultColor = Color.white;
        public bool isPrimary;
    }

    [Header("Runtime (read-only)")]
    [SerializeField] private GameObject playerRoot;
    [SerializeField] private CharacterCustomizer customizer;
    [SerializeField] private PlayerCustomizationApplier applier;
    [SerializeField] private SkiController skiController;
    [SerializeField] private TimeWeather.TimeController timeController;

    [Header("Shop Rotation")]
    [SerializeField] private int dailySkinPatternOffers = 6;
    [SerializeField] private int dailyEyeIconOffers = 6;
    [SerializeField] private int cosmeticStockPerOffer = 25;
    [SerializeField] private int dailySkisOffers = 6;
    [SerializeField] private int dailyPolesOffers = 6;
    [SerializeField] private int dailyHatOffers = 4;
    [SerializeField] private int dailyJacketOffers = 4;
    [SerializeField] private int dailyGlovesOffers = 4;
    [SerializeField] private int dailyBootsOffers = 4;
    [SerializeField] private int dailyAccessoryOffers = 3;

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
    private CustomizationOptionSO _noneGloves;
    private CustomizationOptionSO _noneBoots;
    private CustomizationOptionSO _noneAccessory;

    // Preview ids (unowned only)
    private string _previewEyeId;
    private string _previewSkisId;
    private string _previewPolesId;
    private string _previewHatId;
    private string _previewJacketId;
    private string _previewGlovesId;
    private string _previewBootsId;
    private string _previewAccessoryId;

    private string _previewSkisPatternId;
    private string _previewPolesPatternId;
    private string _previewHatPatternId;
    private string _previewJacketPatternId;
    private string _previewGlovesPatternId;
    private string _previewBootsPatternId;
    private string _previewAccessoryPatternId;

    public event Action OnChanged;


    public bool IsOpen => _catalog != null && _profile != null && playerRoot != null;

    private void NotifyChanged()
    {
        try { OnChanged?.Invoke(); }
        catch (Exception e) { Debug.LogException(e); }
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        int currentKey = ResolveCurrentShopDayKey();
        if (currentKey != _todayKey)
            RefreshDailyOffers();
    }

    public void Open(GameObject playerRoot, GameObject previewRoot, CustomizationCatalogSO catalog, PlayerStatsProfile profile, Action<bool> onRequestExit)
    {
        bool sameProfile = ReferenceEquals(_profile, profile);
        bool sameCatalog = ReferenceEquals(_catalog, catalog);

        this.playerRoot = playerRoot;
        _catalog = catalog;
        _profile = profile;
        _onRequestExit = onRequestExit;

        customizer = playerRoot != null ? playerRoot.GetComponentInChildren<CharacterCustomizer>(true) : null;
        applier = playerRoot != null ? playerRoot.GetComponent<PlayerCustomizationApplier>() : null;
        skiController = playerRoot != null ? playerRoot.GetComponent<SkiController>() : null;

        _todayKey = ResolveCurrentShopDayKey();

        DailyShopService.EnsureDailyOffers(
            _catalog, _todayKey,
            dailySkinPatternOffers,
            dailyEyeIconOffers,
            dailySkisOffers,
            dailyPolesOffers,
            dailyHatOffers,
            dailyJacketOffers,
            dailyGlovesOffers,
            dailyBootsOffers,
            dailyAccessoryOffers,
            cosmeticStockPerOffer,
            isOwnedId: (id) => _profile != null && _profile.customization != null && _profile.customization.IsUnlocked(id)
        );

        // Only reset previews/navigation when switching to a different profile/catalog.
        if (!sameProfile || !sameCatalog)
        {
            ResetNavigationState();
            ClearPreviewState();
        }

        RebuildLists();
        ReapplyEquippedThenPreviews();
        NotifyChanged();
    }

    private void ClearPreviewState()
    {
        _previewEyeId = null;
        _previewSkisId = null;
        _previewPolesId = null;
        _previewHatId = null;
        _previewJacketId = null;
        _previewGlovesId = null;
        _previewBootsId = null;
        _previewAccessoryId = null;

        _previewSkisPatternId = null;
        _previewPolesPatternId = null;
        _previewHatPatternId = null;
        _previewJacketPatternId = null;
        _previewGlovesPatternId = null;
        _previewBootsPatternId = null;
        _previewAccessoryPatternId = null;
    }

    private void ResetNavigationState()
    {
        _rootTab = RootTab.Shop;
        _subTab = SubTab.Cosmetics;
        _activeCategory = CustomizationOptionType.SkinPattern;
        _patternTarget = PatternTarget.Skis;
        _selected = null;
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
        // To avoid �effective� preview overriding the equip, clear preview ONLY for that slot/type.
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
                ApplyPreviewWearable(PatternTarget.Hat, _selected);
                break;

            case CustomizationOptionType.Jacket:
                _previewJacketId = _selected.id;
                ApplyPreviewWearable(PatternTarget.Jacket, _selected);
                break;

            case CustomizationOptionType.Gloves:
                _previewGlovesId = _selected.id;
                ApplyPreviewWearable(PatternTarget.Gloves, _selected);
                break;

            case CustomizationOptionType.Boots:
                _previewBootsId = _selected.id;
                ApplyPreviewWearable(PatternTarget.Boots, _selected);
                break;

            case CustomizationOptionType.Accessory:
                _previewAccessoryId = _selected.id;
                ApplyPreviewWearable(PatternTarget.Accessory, _selected);
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
            case PatternTarget.Gloves:
                _previewGlovesPatternId = patternOpt.id;
                customizer?.SetGlovesPatternTexture(tex);
                break;
            case PatternTarget.Boots:
                _previewBootsPatternId = patternOpt.id;
                customizer?.SetBootsPatternTexture(tex);
                break;
            case PatternTarget.Accessory:
                _previewAccessoryPatternId = patternOpt.id;
                customizer?.SetAccessoryPatternTexture(tex);
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

        if (type == CustomizationOptionType.Gloves)
        {
            if (_noneGloves == null)
            {
                _noneGloves = ScriptableObject.CreateInstance<CustomizationOptionSO>();
                _noneGloves.hideFlags = HideFlags.HideAndDontSave;
                _noneGloves.id = "";
                _noneGloves.type = CustomizationOptionType.Gloves;
                _noneGloves.displayName = "None";
                _noneGloves.description = "No gloves equipped.";
                _noneGloves.cost = 0;
                _noneGloves.customizerIndex = -1;
            }
            return _noneGloves;
        }

        if (type == CustomizationOptionType.Boots)
        {
            if (_noneBoots == null)
            {
                _noneBoots = ScriptableObject.CreateInstance<CustomizationOptionSO>();
                _noneBoots.hideFlags = HideFlags.HideAndDontSave;
                _noneBoots.id = "";
                _noneBoots.type = CustomizationOptionType.Boots;
                _noneBoots.displayName = "None";
                _noneBoots.description = "No boots equipped.";
                _noneBoots.cost = 0;
                _noneBoots.customizerIndex = -1;
            }
            return _noneBoots;
        }

        if (type == CustomizationOptionType.Accessory)
        {
            if (_noneAccessory == null)
            {
                _noneAccessory = ScriptableObject.CreateInstance<CustomizationOptionSO>();
                _noneAccessory.hideFlags = HideFlags.HideAndDontSave;
                _noneAccessory.id = "";
                _noneAccessory.type = CustomizationOptionType.Accessory;
                _noneAccessory.displayName = "None";
                _noneAccessory.description = "No accessory equipped.";
                _noneAccessory.cost = 0;
                _noneAccessory.customizerIndex = -1;
            }
            return _noneAccessory;
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

    public void ClearPreviewGloves()
    {
        _previewGlovesId = null;
        ReapplyEquippedThenPreviews();
        NotifyChanged();
    }

    public void ClearPreviewBoots()
    {
        _previewBootsId = null;
        ReapplyEquippedThenPreviews();
        NotifyChanged();
    }

    public void ClearPreviewAccessory()
    {
        _previewAccessoryId = null;
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
            case PatternTarget.Gloves: _previewGlovesPatternId = null; break;
            case PatternTarget.Boots: _previewBootsPatternId = null; break;
            case PatternTarget.Accessory: _previewAccessoryPatternId = null; break;
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

        if (!ProgressionEventRecorder.TrySpendCurrency(_profile, _selected.cost))
            return;

        ProgressionEventRecorder.RecordCustomizationPurchase(_profile);
        _profile.customization.Unlock(_selected.id);

        // Unlock any patterns granted by this purchase (preferred SO refs, fallback ids)
        if (_selected.unlockPatternOptionsOnPurchase != null)
        {
            foreach (var pat in _selected.unlockPatternOptionsOnPurchase)
            {
                if (pat != null && !string.IsNullOrEmpty(pat.id))
                    _profile.customization.Unlock(pat.id);
            }
        }

        if (_selected.unlockPatternIdsOnPurchase != null)
        {
            foreach (var pid in _selected.unlockPatternIdsOnPurchase)
            {
                if (!string.IsNullOrEmpty(pid))
                    _profile.customization.Unlock(pid);
            }
        }

        // Important UX fix:
        // once the item is bought, immediately equip/apply it so the purchase
        // flow for previewed gear and patterns feels direct.
        EquipSelected();

        RebuildLists();
        NotifyChanged();
    }

    public void SetTimeController(TimeWeather.TimeController controller)
    {
        timeController = controller;
    }

    private bool IsPurchasableToday(CustomizationOptionSO opt)
    {
        if (opt == null) return false;
        if (IsOwned(opt)) return false;
        return DailyShopService.IsInTodayOffers(opt.id, _todayKey);
    }

    private bool CanAffordInternal(int cost)
    {
        return _profile != null && _profile.currency >= cost;
    }

    private void UnlockPurchasedOption(CustomizationOptionSO opt)
    {
        if (opt == null || _profile?.customization == null) return;

        _profile.customization.Unlock(opt.id);

        if (opt.unlockPatternOptionsOnPurchase != null)
        {
            foreach (var pat in opt.unlockPatternOptionsOnPurchase)
            {
                if (pat != null && !string.IsNullOrEmpty(pat.id))
                    _profile.customization.Unlock(pat.id);
            }
        }

        if (opt.unlockPatternIdsOnPurchase != null)
        {
            foreach (var pid in opt.unlockPatternIdsOnPurchase)
            {
                if (!string.IsNullOrEmpty(pid))
                    _profile.customization.Unlock(pid);
            }
        }
    }

    private void ApplyOwnedOption(CustomizationOptionSO opt, PatternTarget? patternTargetOverride = null)
    {
        if (opt == null) return;
        if (_profile?.customization == null) return;
        if (!IsOwned(opt)) return;

        var s = _profile.customization;
        var previousPatternTarget = _patternTarget;

        if (patternTargetOverride.HasValue)
            _patternTarget = patternTargetOverride.Value;

        ClearPreviewForType(opt.type);

        switch (opt.type)
        {
            case CustomizationOptionType.EyeIcon:
                s.equippedEyeIconId = opt.id;
                break;

            case CustomizationOptionType.Skis:
                s.equippedSkisId = opt.id;
                break;

            case CustomizationOptionType.Poles:
                s.equippedPolesId = opt.id;
                break;

            case CustomizationOptionType.Hat:
                s.equippedHatId = opt.id;
                break;

            case CustomizationOptionType.Jacket:
                s.equippedJacketId = opt.id;
                break;

            case CustomizationOptionType.Gloves:
                s.equippedGlovesId = opt.id;
                break;

            case CustomizationOptionType.Boots:
                s.equippedBootsId = opt.id;
                break;

            case CustomizationOptionType.Accessory:
                s.equippedAccessoryId = opt.id;
                break;

            case CustomizationOptionType.SkinPattern:
                EquipPatternToTarget(opt);
                break;
        }

        if (patternTargetOverride.HasValue)
            _patternTarget = previousPatternTarget;

        ReapplyEquippedThenPreviews();
    }

    private bool TryPurchaseOption(CustomizationOptionSO opt, PatternTarget? patternTargetOverride = null)
    {
        if (opt == null) return false;
        if (_profile?.customization == null) return false;
        if (!IsPurchasableToday(opt)) return false;
        if (!CanAffordInternal(opt.cost)) return false;

        if (!ProgressionEventRecorder.TrySpendCurrency(_profile, opt.cost)) return false;

        ProgressionEventRecorder.RecordCustomizationPurchase(_profile);
        UnlockPurchasedOption(opt);
        ApplyOwnedOption(opt, patternTargetOverride);
        return true;
    }

    public bool CanAffordCost(int cost) => CanAffordInternal(cost);

    public CustomizationOptionSO GetPreviewedPurchasableOption(CustomizationOptionType type)
    {
        string id = type switch
        {
            CustomizationOptionType.EyeIcon => _previewEyeId,
            CustomizationOptionType.Gloves => _previewGlovesId,
            CustomizationOptionType.Boots => _previewBootsId,
            CustomizationOptionType.Accessory => _previewAccessoryId,
            _ => null
        };

        var opt = ResolveById(id);
        return (opt != null && opt.type == type && IsPurchasableToday(opt)) ? opt : null;
    }

    public int GetPreviewedPurchaseCost(CustomizationOptionType type)
    {
        return GetPreviewedPurchasableOption(type)?.cost ?? 0;
    }

    public void TryBuyPreviewedOption(CustomizationOptionType type)
    {
        var opt = GetPreviewedPurchasableOption(type);
        if (!TryPurchaseOption(opt)) return;

        RebuildLists();
        NotifyChanged();
    }

    public CustomizationOptionSO GetPreviewedPurchasableEyeOption()
    {
        var opt = ResolveById(_previewEyeId);
        return (opt != null && opt.type == CustomizationOptionType.EyeIcon && IsPurchasableToday(opt)) ? opt : null;
    }

    public CustomizationOptionSO GetPreviewedPurchasableGearOption(PatternTarget target)
    {
        string id = target switch
        {
            PatternTarget.Skis => _previewSkisId,
            PatternTarget.Poles => _previewPolesId,
            PatternTarget.Hat => _previewHatId,
            PatternTarget.Jacket => _previewJacketId,
            PatternTarget.Gloves => _previewGlovesId,
            PatternTarget.Boots => _previewBootsId,
            PatternTarget.Accessory => _previewAccessoryId,
            _ => null
        };

        var expectedType = target switch
        {
            PatternTarget.Skis => CustomizationOptionType.Skis,
            PatternTarget.Poles => CustomizationOptionType.Poles,
            PatternTarget.Hat => CustomizationOptionType.Hat,
            PatternTarget.Jacket => CustomizationOptionType.Jacket,
            PatternTarget.Gloves => CustomizationOptionType.Gloves,
            PatternTarget.Boots => CustomizationOptionType.Boots,
            PatternTarget.Accessory => CustomizationOptionType.Accessory,
            _ => CustomizationOptionType.Skis
        };

        var opt = ResolveById(id);
        return (opt != null && opt.type == expectedType && IsPurchasableToday(opt)) ? opt : null;
    }

    public CustomizationOptionSO GetPreviewedPurchasablePatternOption(PatternTarget target)
    {
        string id = target switch
        {
            PatternTarget.Skis => _previewSkisPatternId,
            PatternTarget.Poles => _previewPolesPatternId,
            PatternTarget.Hat => _previewHatPatternId,
            PatternTarget.Jacket => _previewJacketPatternId,
            PatternTarget.Gloves => _previewGlovesPatternId,
            PatternTarget.Boots => _previewBootsPatternId,
            PatternTarget.Accessory => _previewAccessoryPatternId,
            _ => null
        };

        var opt = ResolveById(id);
        return (opt != null && opt.type == CustomizationOptionType.SkinPattern && IsPurchasableToday(opt)) ? opt : null;
    }

    public int GetPreviewedEyePurchaseCost()
    {
        return GetPreviewedPurchasableEyeOption()?.cost ?? 0;
    }

    public int GetPreviewedGearPurchaseCost(PatternTarget target)
    {
        return GetPreviewedPurchasableGearOption(target)?.cost ?? 0;
    }

    public int GetPreviewedPatternPurchaseCost(PatternTarget target)
    {
        return GetPreviewedPurchasablePatternOption(target)?.cost ?? 0;
    }

    public int GetPreviewedCombinedPurchaseCost(PatternTarget target)
    {
        return GetPreviewedGearPurchaseCost(target) + GetPreviewedPatternPurchaseCost(target);
    }

    public void TryBuyPreviewedEye()
    {
        var opt = GetPreviewedPurchasableEyeOption();
        if (!TryPurchaseOption(opt)) return;

        RebuildLists();
        NotifyChanged();
    }

    public void TryBuyPreviewedGear(PatternTarget target)
    {
        var opt = GetPreviewedPurchasableGearOption(target);
        if (!TryPurchaseOption(opt)) return;

        RebuildLists();
        NotifyChanged();
    }

    public void TryBuyPreviewedPattern(PatternTarget target)
    {
        var opt = GetPreviewedPurchasablePatternOption(target);
        if (!TryPurchaseOption(opt, target)) return;

        RebuildLists();
        NotifyChanged();
    }

    public void TryBuyPreviewedGearAndPattern(PatternTarget target)
    {
        var gear = GetPreviewedPurchasableGearOption(target);
        var pattern = GetPreviewedPurchasablePatternOption(target);

        if (gear == null && pattern == null)
            return;

        int total = (gear != null ? gear.cost : 0) + (pattern != null ? pattern.cost : 0);
        if (!CanAffordInternal(total))
            return;

        if (gear != null)
        {
            if (!ProgressionEventRecorder.TrySpendCurrency(_profile, gear.cost))
                return;

            ProgressionEventRecorder.RecordCustomizationPurchase(_profile);
            UnlockPurchasedOption(gear);
            ApplyOwnedOption(gear);
        }

        if (pattern != null)
        {
            if (!ProgressionEventRecorder.TrySpendCurrency(_profile, pattern.cost))
                return;

            ProgressionEventRecorder.RecordCustomizationPurchase(_profile);
            UnlockPurchasedOption(pattern);
            ApplyOwnedOption(pattern, target);
        }

        RebuildLists();
        NotifyChanged();
    }

    public void EquipSelected()
    {
        if (_selected == null) return;
        if (_profile == null || _profile.customization == null) return;
        if (!IsOwned(_selected)) return;

        ApplyOwnedOption(_selected);
        RebuildLists();
        NotifyChanged();
    }

    public void ResetSkisColorToDefault()
    {
        if (_profile?.customization == null) return;
        var s = _profile.customization;

        s.skisColor = ResolvePrimaryDefaultColorForCurrentVisual(PatternTarget.Skis);
        s.hasSetSkisColor = false;
        s.skisUseDefaultColor = true;

        ReapplyEquippedThenPreviews();
        NotifyChanged();
    }
    public void ResetPolesColorToDefault()
    {
        if (_profile?.customization == null) return;
        var s = _profile.customization;

        s.polesColor = ResolvePrimaryDefaultColorForCurrentVisual(PatternTarget.Poles);
        s.hasSetPolesColor = false;
        s.polesUseDefaultColor = true;

        ReapplyEquippedThenPreviews();
        NotifyChanged();
    }

    public void ResetHatColorToDefault()
    {
        if (_profile?.customization == null) return;
        var s = _profile.customization;

        s.hatColor = ResolvePrimaryDefaultColorForCurrentVisual(PatternTarget.Hat);
        s.hasSetHatColor = false;
        s.hatUseDefaultColor = true;

        ReapplyEquippedThenPreviews();
        NotifyChanged();
    }

    public void ResetJacketColorToDefault()
    {
        if (_profile?.customization == null) return;
        var s = _profile.customization;

        s.jacketColor = ResolvePrimaryDefaultColorForCurrentVisual(PatternTarget.Jacket);
        s.hasSetJacketColor = false;
        s.jacketUseDefaultColor = true;

        ReapplyEquippedThenPreviews();
        NotifyChanged();
    }

    public void ResetGlovesColorToDefault()
    {
        if (_profile?.customization == null) return;
        var s = _profile.customization;

        s.glovesColor = ResolvePrimaryDefaultColorForCustomizationType(CustomizationOptionType.Gloves);
        s.hasSetGlovesColor = false;
        s.glovesUseDefaultColor = true;

        ReapplyEquippedThenPreviews();
        NotifyChanged();
    }

    public void ResetBootsColorToDefault()
    {
        if (_profile?.customization == null) return;
        var s = _profile.customization;

        s.bootsColor = ResolvePrimaryDefaultColorForCustomizationType(CustomizationOptionType.Boots);
        s.hasSetBootsColor = false;
        s.bootsUseDefaultColor = true;

        ReapplyEquippedThenPreviews();
        NotifyChanged();
    }

    public void ResetAccessoryColorToDefault()
    {
        if (_profile?.customization == null) return;
        var s = _profile.customization;

        s.accessoryColor = ResolvePrimaryDefaultColorForCustomizationType(CustomizationOptionType.Accessory);
        s.hasSetAccessoryColor = false;
        s.accessoryUseDefaultColor = true;

        ReapplyEquippedThenPreviews();
        NotifyChanged();
    }

    public void ResetPatternToDefault(PatternTarget t)
    {
        if (_profile?.customization == null) return;
        var s = _profile.customization;

        // Empty means use equipped gear's defaultPatternId
        switch (t)
        {
            case PatternTarget.Skis: s.equippedSkisPatternId = ""; break;
            case PatternTarget.Poles: s.equippedPolesPatternId = ""; break;
            case PatternTarget.Hat: s.equippedHatPatternId = ""; break;
            case PatternTarget.Jacket: s.equippedJacketPatternId = ""; break;
            case PatternTarget.Gloves: s.equippedGlovesPatternId = ""; break;
            case PatternTarget.Boots: s.equippedBootsPatternId = ""; break;
            case PatternTarget.Accessory: s.equippedAccessoryPatternId = ""; break;
        }

        switch (t)
        {
            case PatternTarget.Skis:
                _previewSkisPatternId = null;
                s.skisUseDefaultPattern = true;
                break;
            case PatternTarget.Poles:
                _previewPolesPatternId = null;
                s.polesUseDefaultPattern = true;
                break;
            case PatternTarget.Hat:
                _previewHatPatternId = null;
                s.hatUseDefaultPattern = true;
                break;
            case PatternTarget.Jacket:
                _previewJacketPatternId = null;
                s.jacketUseDefaultPattern = true;
                break;
            case PatternTarget.Gloves:
                _previewGlovesPatternId = null;
                s.glovesUseDefaultPattern = true;
                break;
            case PatternTarget.Boots:
                _previewBootsPatternId = null;
                s.bootsUseDefaultPattern = true;
                break;
            case PatternTarget.Accessory:
                _previewAccessoryPatternId = null;
                s.accessoryUseDefaultPattern = true;
                break;
        }

        ReapplyEquippedThenPreviews();
        NotifyChanged();
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

            case PatternTarget.Gloves:
                s.equippedGlovesPatternId = patternOpt.id;
                s.glovesUseDefaultPattern = false;
                customizer?.SetGlovesPatternTexture(tex);
                break;

            case PatternTarget.Boots:
                s.equippedBootsPatternId = patternOpt.id;
                s.bootsUseDefaultPattern = false;
                customizer?.SetBootsPatternTexture(tex);
                break;

            case PatternTarget.Accessory:
                s.equippedAccessoryPatternId = patternOpt.id;
                s.accessoryUseDefaultPattern = false;
                customizer?.SetAccessoryPatternTexture(tex);
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

    public void SetEyeOutlineColor(Color c)
    {
        if (_profile?.customization == null) return;
        _profile.customization.eyeOutlineColor = c;
        _profile.customization.hasSetEyeOutlineColor = c.a > 0.001f;

        customizer?.SetEyeOutlineColor(c);
    }

    public void SetEyeSize(float size)
    {
        if (_profile?.customization == null) return;
        float clamped = Mathf.Clamp(Mathf.Round(size), 1f, 5f);
        _profile.customization.eyeSize = clamped;
        _profile.customization.hasSetEyeSize = !Mathf.Approximately(clamped, 3f);

        customizer?.SetEyeSize(clamped);
    }

    public void SetSkisColor(Color c)
    {
        if (_profile?.customization == null) return;
        var s = _profile.customization;

        s.skisColor = c;
        s.hasSetSkisColor = true;
        s.skisUseDefaultColor = false;

        ReapplyStatePreservingPreviews();
    }

    public void SetPolesColor(Color c)
    {
        if (_profile?.customization == null) return;
        var s = _profile.customization;

        s.polesColor = c;
        s.hasSetPolesColor = true;
        s.polesUseDefaultColor = false;

        ReapplyStatePreservingPreviews();
    }

    public void SetHatColor(Color c)
    {
        if (_profile?.customization == null) return;
        var s = _profile.customization;

        s.hatColor = c;
        s.hasSetHatColor = true;
        s.hatUseDefaultColor = false;

        ReapplyStatePreservingPreviews();
    }

    public void SetJacketColor(Color c)
    {
        if (_profile?.customization == null) return;
        var s = _profile.customization;

        s.jacketColor = c;
        s.hasSetJacketColor = true;
        s.jacketUseDefaultColor = false;

        ReapplyStatePreservingPreviews();
    }

    public void SetGlovesColor(Color c)
    {
        if (_profile?.customization == null) return;
        var s = _profile.customization;

        s.glovesColor = c;
        s.hasSetGlovesColor = true;
        s.glovesUseDefaultColor = false;

        ReapplyStatePreservingPreviews();
    }

    public void SetBootsColor(Color c)
    {
        if (_profile?.customization == null) return;
        var s = _profile.customization;

        s.bootsColor = c;
        s.hasSetBootsColor = true;
        s.bootsUseDefaultColor = false;

        ReapplyStatePreservingPreviews();
    }

    public void SetAccessoryColor(Color c)
    {
        if (_profile?.customization == null) return;
        var s = _profile.customization;

        s.accessoryColor = c;
        s.hasSetAccessoryColor = true;
        s.accessoryUseDefaultColor = false;

        ReapplyStatePreservingPreviews();
    }

    public Color GetSkinColor() => _profile?.customization != null ? _profile.customization.skinColor : Color.white;
    public Color GetEyeColor() => _profile?.customization != null ? _profile.customization.eyeColor : Color.white;
    public Color GetEyeOutlineColor() => _profile?.customization != null ? _profile.customization.eyeOutlineColor : new Color(0f, 0f, 0f, 0f);
    public float GetEyeSize() => _profile?.customization != null ? Mathf.Clamp(Mathf.Round(_profile.customization.eyeSize), 1f, 5f) : 3f;
    public Color GetSkisColor() => _profile?.customization != null ? _profile.customization.skisColor : Color.white;
    public Color GetPolesColor() => _profile?.customization != null ? _profile.customization.polesColor : Color.white;
    public Color GetHatColor() => _profile?.customization != null ? _profile.customization.hatColor : Color.white;
    public Color GetJacketColor() => _profile?.customization != null ? _profile.customization.jacketColor : Color.white;
    public Color GetGlovesColor() => _profile?.customization != null ? _profile.customization.glovesColor : Color.white;
    public Color GetBootsColor() => _profile?.customization != null ? _profile.customization.bootsColor : Color.white;
    public Color GetAccessoryColor() => _profile?.customization != null ? _profile.customization.accessoryColor : Color.white;

    private static string GetSlotKey(PatternTarget target)
    {
        switch (target)
        {
            case PatternTarget.Skis: return "Skis";
            case PatternTarget.Poles: return "Poles";
            case PatternTarget.Hat: return "Hat";
            case PatternTarget.Jacket: return "Jacket";
            case PatternTarget.Gloves: return "Gloves";
            case PatternTarget.Boots: return "Boots";
            case PatternTarget.Accessory: return "Accessory";
            default: return null;
        }
    }

    private CustomizationOptionSO GetEffectiveGearOption(PatternTarget target)
    {
        switch (target)
        {
            case PatternTarget.Skis: return GetEffectiveSkisOption();
            case PatternTarget.Poles: return GetEffectivePolesOption();
            case PatternTarget.Hat: return GetEffectiveHatOption();
            case PatternTarget.Jacket: return GetEffectiveJacketOption();
            case PatternTarget.Gloves: return GetEffectiveGlovesOption();
            case PatternTarget.Boots: return GetEffectiveBootsOption();
            case PatternTarget.Accessory: return GetEffectiveAccessoryOption();
            default: return null;
        }
    }

    private WearableAttachment GetEffectiveWearableAttachment(PatternTarget target)
    {
        var opt = GetEffectiveGearOption(target);
        if (opt == null)
            return null;

        var prefab = ResolveWearablePrefab(target, opt);
        return prefab != null ? prefab.GetComponent<WearableAttachment>() : null;
    }

    private GameObject ResolveWearablePrefab(PatternTarget target, CustomizationOptionSO opt)
    {
        if (opt == null)
            return null;

        switch (target)
        {
            case PatternTarget.Hat:
                if (opt.hatPrefab != null)
                    return opt.hatPrefab;

                return customizer != null ? customizer.GetHatPrefabAtIndex(opt.customizerIndex) : null;

            case PatternTarget.Jacket:
                if (opt.jacketPrefab != null)
                    return opt.jacketPrefab;

                return customizer != null ? customizer.GetJacketPrefabAtIndex(opt.customizerIndex) : null;

            case PatternTarget.Gloves:
                if (opt.glovePrefab != null)
                    return opt.glovePrefab;

                return customizer != null ? customizer.GetGlovePrefabAtIndex(opt.customizerIndex) : null;

            case PatternTarget.Boots:
                if (opt.bootPrefab != null)
                    return opt.bootPrefab;

                return customizer != null ? customizer.GetBootPrefabAtIndex(opt.customizerIndex) : null;

            case PatternTarget.Accessory:
                if (opt.accessoryPrefab != null)
                    return opt.accessoryPrefab;

                return customizer != null ? customizer.GetAccessoryPrefabAtIndex(opt.customizerIndex) : null;

            default:
                return null;
        }
    }

    private Color ResolveCurrentPrimaryColorForTarget(PatternTarget target)
    {
        switch (target)
        {
            case PatternTarget.Skis: return GetSkisColor();
            case PatternTarget.Poles: return GetPolesColor();
            case PatternTarget.Hat: return GetHatColor();
            case PatternTarget.Jacket: return GetJacketColor();
            case PatternTarget.Gloves: return GetGlovesColor();
            case PatternTarget.Boots: return GetBootsColor();
            case PatternTarget.Accessory: return GetAccessoryColor();
            default: return Color.white;
        }
    }

    private void ApplyCurrentExtraChannelColorsForTarget(PatternTarget target)
    {
        var channels = GetEffectiveExtraColorChannels(target);
        if (channels == null || channels.Count == 0)
            return;

        for (int i = 0; i < channels.Count; i++)
        {
            var ch = channels[i];
            if (ch == null || string.IsNullOrEmpty(ch.id))
                continue;

            var color = GetGearChannelColor(target, ch.id);
            ApplyExtraChannelColorLive(target, ch.id, color);
        }
    }

    private Color ResolveExtraChannelDefaultColor(PatternTarget target, string channelId)
    {
        var channels = GetEffectiveExtraColorChannels(target);
        for (int i = 0; i < channels.Count; i++)
        {
            var channel = channels[i];
            if (channel == null || string.IsNullOrEmpty(channel.id))
                continue;

            if (string.Equals(channel.id, channelId, StringComparison.Ordinal))
                return channel.defaultColor;
        }

        return Color.white;
    }

    private Color ResolvePrimaryDefaultColorForCurrentVisual(PatternTarget target)
    {
        var opt = GetEffectiveGearOption(target);
        return (opt != null && opt.HasTint) ? opt.DefaultTint : Color.white;
    }

    private Color ResolvePrimaryDefaultColorForCustomizationType(CustomizationOptionType type)
    {
        var opt = GetEffectiveOption(type);
        return (opt != null && opt.HasTint) ? opt.DefaultTint : Color.white;
    }

    private void ApplyPreviewWearable(PatternTarget target, CustomizationOptionSO opt)
    {
        if (opt == null || customizer == null)
            return;

        switch (target)
        {
            case PatternTarget.Hat:
                {
                    if (opt.hatPrefab != null) customizer.SetHatPrefab(opt.hatPrefab);
                    else customizer.SetHat(opt.customizerIndex);

                    customizer.SetHatColor(ResolveCurrentPrimaryColorForTarget(PatternTarget.Hat));

                    var hatPatternOpt = ResolveById(opt.DefaultPatternIdResolved);
                    customizer.SetHatPatternTexture(ResolvePatternTexture(hatPatternOpt));

                    ApplyCurrentExtraChannelColorsForTarget(PatternTarget.Hat);
                    break;
                }

            case PatternTarget.Jacket:
                {
                    if (opt.jacketPrefab != null) customizer.SetJacketPrefab(opt.jacketPrefab);
                    else customizer.SetJacket(opt.customizerIndex);

                    customizer.SetCurrentJacketOption(opt);
                    customizer.SetJacketColor(ResolveCurrentPrimaryColorForTarget(PatternTarget.Jacket));

                    var jacketPatternOpt = ResolveById(opt.DefaultPatternIdResolved);
                    customizer.SetJacketPatternTexture(ResolvePatternTexture(jacketPatternOpt));

                    ApplyCurrentExtraChannelColorsForTarget(PatternTarget.Jacket);
                    break;
                }

            case PatternTarget.Gloves:
                {
                    if (opt.glovePrefab != null) customizer.SetGlovesPrefab(opt.glovePrefab);
                    else customizer.SetGloves(opt.customizerIndex);

                    customizer.SetGlovesColor(ResolveCurrentPrimaryColorForTarget(PatternTarget.Gloves));

                    var glovesPatternOpt = ResolveById(opt.DefaultPatternIdResolved);
                    customizer.SetGlovesPatternTexture(ResolvePatternTexture(glovesPatternOpt));

                    ApplyCurrentExtraChannelColorsForTarget(PatternTarget.Gloves);
                    break;
                }

            case PatternTarget.Boots:
                {
                    if (opt.bootPrefab != null) customizer.SetBootsPrefab(opt.bootPrefab);
                    else customizer.SetBoots(opt.customizerIndex);

                    customizer.SetBootsColor(ResolveCurrentPrimaryColorForTarget(PatternTarget.Boots));

                    var bootsPatternOpt = ResolveById(opt.DefaultPatternIdResolved);
                    customizer.SetBootsPatternTexture(ResolvePatternTexture(bootsPatternOpt));

                    ApplyCurrentExtraChannelColorsForTarget(PatternTarget.Boots);
                    break;
                }

            case PatternTarget.Accessory:
                {
                    if (opt.accessoryPrefab != null) customizer.SetAccessoryPrefab(opt.accessoryPrefab);
                    else customizer.SetAccessory(opt.customizerIndex);

                    customizer.SetCurrentAccessoryOption(opt);
                    customizer.SetAccessoryColor(ResolveCurrentPrimaryColorForTarget(PatternTarget.Accessory));

                    var accessoryPatternOpt = ResolveById(opt.DefaultPatternIdResolved);
                    customizer.SetAccessoryPatternTexture(ResolvePatternTexture(accessoryPatternOpt));

                    ApplyCurrentExtraChannelColorsForTarget(PatternTarget.Accessory);
                    break;
                }
        }
    }

    private void ApplyExtraChannelColorLive(PatternTarget target, string channelId, Color color)
    {
        if (string.IsNullOrEmpty(channelId)) return;

        switch (target)
        {
            case PatternTarget.Hat:
                customizer?.SetHatChannelColor(channelId, color);
                break;

            case PatternTarget.Jacket:
                customizer?.SetJacketChannelColor(channelId, color);
                break;

            case PatternTarget.Gloves:
                customizer?.SetGlovesChannelColor(channelId, color);
                break;

            case PatternTarget.Boots:
                customizer?.SetBootsChannelColor(channelId, color);
                break;

            case PatternTarget.Accessory:
                customizer?.SetAccessoryChannelColor(channelId, color);
                break;
        }
    }

    public List<GearColorChannelInfo> GetActiveExtraColorChannels(PatternTarget target)
    {
        return GetEffectiveExtraColorChannels(target);
    }

    private List<GearColorChannelInfo> GetEffectiveExtraColorChannels(PatternTarget target)
    {
        var result = new List<GearColorChannelInfo>();

        if (target != PatternTarget.Hat &&
            target != PatternTarget.Jacket &&
            target != PatternTarget.Gloves &&
            target != PatternTarget.Boots &&
            target != PatternTarget.Accessory)
            return result;

        var option = GetEffectiveGearOption(target);
        var wearable = GetEffectiveWearableAttachment(target);
        var channels = wearable != null ? wearable.GetExtraChannels() : null;
        if (channels != null)
        {
            for (int i = 0; i < channels.Count; i++)
            {
                var ch = channels[i];
                if (ch == null || string.IsNullOrEmpty(ch.id))
                    continue;

                AddOrUpdateEffectiveChannel(
                    result,
                    ch.id,
                    string.IsNullOrEmpty(ch.displayName) ? ch.id : ch.displayName,
                    ch.defaultColor);
            }
        }

        if (target == PatternTarget.Jacket &&
            option != null &&
            option.UsesLimbSecondaryColour())
        {
            string channelId = option.GetResolvedLimbSecondaryChannelId("secondary");
            var authoredChannel = wearable != null ? wearable.GetChannel(channelId) : null;
            string displayName = authoredChannel != null && !string.IsNullOrEmpty(authoredChannel.displayName)
                ? authoredChannel.displayName
                : "Limb Secondary";
            Color defaultColor = authoredChannel != null ? authoredChannel.defaultColor : Color.white;

            AddOrUpdateEffectiveChannel(result, channelId, displayName, defaultColor);
        }

        return result;
    }

    private static void AddOrUpdateEffectiveChannel(List<GearColorChannelInfo> result, string id, string displayName, Color defaultColor)
    {
        if (result == null || string.IsNullOrEmpty(id))
            return;

        for (int i = 0; i < result.Count; i++)
        {
            var existing = result[i];
            if (existing == null || !string.Equals(existing.id, id, StringComparison.Ordinal))
                continue;

            if (!string.IsNullOrEmpty(displayName))
                existing.displayName = displayName;

            existing.defaultColor = defaultColor;
            return;
        }

        result.Add(new GearColorChannelInfo
        {
            id = id,
            displayName = string.IsNullOrEmpty(displayName) ? id : displayName,
            defaultColor = defaultColor,
            isPrimary = false
        });
    }

    public Color GetGearChannelColor(PatternTarget target, string channelId)
    {
        if (string.IsNullOrEmpty(channelId) || string.Equals(channelId, WearableAttachment.PrimaryChannelId, StringComparison.Ordinal))
        {
            switch (target)
            {
                case PatternTarget.Skis: return GetSkisColor();
                case PatternTarget.Poles: return GetPolesColor();
                case PatternTarget.Hat: return GetHatColor();
                case PatternTarget.Jacket: return GetJacketColor();
                case PatternTarget.Gloves: return GetGlovesColor();
                case PatternTarget.Boots: return GetBootsColor();
                case PatternTarget.Accessory: return GetAccessoryColor();
                default: return Color.white;
            }
        }

        if (_profile?.customization == null)
            return ResolveExtraChannelDefaultColor(target, channelId);

        var slotKey = GetSlotKey(target);
        if (_profile.customization.TryGetExtraColor(slotKey, channelId, out var saved))
            return saved;

        return ResolveExtraChannelDefaultColor(target, channelId);
    }

    public void SetGearChannelColor(PatternTarget target, string channelId, Color color)
    {
        if (string.IsNullOrEmpty(channelId) || string.Equals(channelId, WearableAttachment.PrimaryChannelId, StringComparison.Ordinal))
        {
            switch (target)
            {
                case PatternTarget.Skis: SetSkisColor(color); break;
                case PatternTarget.Poles: SetPolesColor(color); break;
                case PatternTarget.Hat: SetHatColor(color); break;
                case PatternTarget.Jacket: SetJacketColor(color); break;
                case PatternTarget.Gloves: SetGlovesColor(color); break;
                case PatternTarget.Boots: SetBootsColor(color); break;
                case PatternTarget.Accessory: SetAccessoryColor(color); break;
            }
            return;
        }

        if (_profile?.customization == null)
            return;

        var slotKey = GetSlotKey(target);
        _profile.customization.SetExtraColor(slotKey, channelId, color);

        ApplyExtraChannelColorLive(target, channelId, color);
        NotifyChanged();
    }

    public bool HasActiveExtraColorChannels(PatternTarget target)
    {
        var channels = GetActiveExtraColorChannels(target);
        return channels != null && channels.Count > 0;
    }

    public void ResetGearChannelColorToDefault(PatternTarget target, string channelId)
    {
        if (string.IsNullOrEmpty(channelId) || string.Equals(channelId, WearableAttachment.PrimaryChannelId, StringComparison.Ordinal))
        {
            switch (target)
            {
                case PatternTarget.Skis: ResetSkisColorToDefault(); break;
                case PatternTarget.Poles: ResetPolesColorToDefault(); break;
                case PatternTarget.Hat: ResetHatColorToDefault(); break;
                case PatternTarget.Jacket: ResetJacketColorToDefault(); break;
                case PatternTarget.Gloves: ResetGlovesColorToDefault(); break;
                case PatternTarget.Boots: ResetBootsColorToDefault(); break;
                case PatternTarget.Accessory: ResetAccessoryColorToDefault(); break;
            }
            return;
        }

        if (_profile?.customization == null)
            return;

        var slotKey = GetSlotKey(target);
        _profile.customization.ClearExtraColor(slotKey, channelId);

        var defaultColor = ResolveExtraChannelDefaultColor(target, channelId);
        ApplyExtraChannelColorLive(target, channelId, defaultColor);

        NotifyChanged();
    }

    public void ResetGearExtraColorsToDefault(PatternTarget target)
    {
        if (_profile?.customization == null)
            return;

        var slotKey = GetSlotKey(target);
        if (string.IsNullOrEmpty(slotKey))
            return;

        var channels = GetActiveExtraColorChannels(target);
        if (channels != null)
        {
            for (int i = 0; i < channels.Count; i++)
            {
                var channel = channels[i];
                if (channel == null || string.IsNullOrEmpty(channel.id))
                    continue;

                _profile.customization.ClearExtraColor(slotKey, channel.id);
            }
        }

        ReapplyEquippedThenPreviews();
        NotifyChanged();
    }

    // ---------------- Reset toggles (default <-> custom) ----------------

    public void ToggleSkisColorDefault()
    {
        if (_profile?.customization == null) return;
        _profile.customization.skisUseDefaultColor = !_profile.customization.skisUseDefaultColor;
        ReapplyStatePreservingPreviews();
    }

    public void TogglePolesColorDefault()
    {
        if (_profile?.customization == null) return;
        _profile.customization.polesUseDefaultColor = !_profile.customization.polesUseDefaultColor;
        ReapplyStatePreservingPreviews();
    }

    public void ToggleHatColorDefault()
    {
        if (_profile?.customization == null) return;
        _profile.customization.hatUseDefaultColor = !_profile.customization.hatUseDefaultColor;
        ReapplyStatePreservingPreviews();
    }

    public void ToggleJacketColorDefault()
    {
        if (_profile?.customization == null) return;
        _profile.customization.jacketUseDefaultColor = !_profile.customization.jacketUseDefaultColor;
        ReapplyStatePreservingPreviews();
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
            case PatternTarget.Gloves: s.glovesUseDefaultPattern = !s.glovesUseDefaultPattern; break;
            case PatternTarget.Boots: s.bootsUseDefaultPattern = !s.bootsUseDefaultPattern; break;
            case PatternTarget.Accessory: s.accessoryUseDefaultPattern = !s.accessoryUseDefaultPattern; break;
        }

        ReapplyStatePreservingPreviews();
    }

    public void Exit(bool apply) => _onRequestExit?.Invoke(apply);

    // ---------------- Internals ----------------

    private void RebuildLists()
    {
        _visible.Clear();
        _owned.Clear();

        if (_catalog == null) return;

        DailyShopService.GetTodayOffers(_catalog, _todayKey, _activeCategory, _visible);

        if (_activeCategory == CustomizationOptionType.Hat ||
            _activeCategory == CustomizationOptionType.Jacket ||
            _activeCategory == CustomizationOptionType.Gloves ||
            _activeCategory == CustomizationOptionType.Boots ||
            _activeCategory == CustomizationOptionType.Accessory)
        {
            _owned.Insert(0, GetNoneOption(_activeCategory));
        }

        foreach (var o in _catalog.GetByType(_activeCategory))
        {
            if (o == null) continue;
            if (IsOwned(o)) _owned.Add(o);
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
    public CustomizationOptionSO GetEquippedGlovesOption() => ResolveById(_profile?.customization?.equippedGlovesId);
    public CustomizationOptionSO GetEquippedBootsOption() => ResolveById(_profile?.customization?.equippedBootsId);
    public CustomizationOptionSO GetEquippedAccessoryOption() => ResolveById(_profile?.customization?.equippedAccessoryId);

    public CustomizationOptionSO GetEquippedOption(CustomizationOptionType type)
    {
        return type switch
        {
            CustomizationOptionType.EyeIcon => GetEquippedEyeOption(),
            CustomizationOptionType.Skis => GetEquippedSkisOption(),
            CustomizationOptionType.Poles => GetEquippedPolesOption(),
            CustomizationOptionType.Hat => GetEquippedHatOption(),
            CustomizationOptionType.Jacket => GetEquippedJacketOption(),
            CustomizationOptionType.Gloves => GetEquippedGlovesOption(),
            CustomizationOptionType.Boots => GetEquippedBootsOption(),
            CustomizationOptionType.Accessory => GetEquippedAccessoryOption(),
            _ => null
        };
    }

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
            PatternTarget.Gloves => s.equippedGlovesPatternId,
            PatternTarget.Boots => s.equippedBootsPatternId,
            PatternTarget.Accessory => s.equippedAccessoryPatternId,
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
    public bool IsPreviewingGloves() => !string.IsNullOrEmpty(_previewGlovesId);
    public bool IsPreviewingBoots() => !string.IsNullOrEmpty(_previewBootsId);
    public bool IsPreviewingAccessory() => !string.IsNullOrEmpty(_previewAccessoryId);

    public bool IsPreviewingOption(CustomizationOptionType type)
    {
        return type switch
        {
            CustomizationOptionType.EyeIcon => IsPreviewingEye(),
            CustomizationOptionType.Skis => IsPreviewingSkis(),
            CustomizationOptionType.Poles => IsPreviewingPoles(),
            CustomizationOptionType.Hat => IsPreviewingHat(),
            CustomizationOptionType.Jacket => IsPreviewingJacket(),
            CustomizationOptionType.Gloves => IsPreviewingGloves(),
            CustomizationOptionType.Boots => IsPreviewingBoots(),
            CustomizationOptionType.Accessory => IsPreviewingAccessory(),
            _ => false
        };
    }

    public CustomizationOptionSO GetPreviewEyeOption() => ResolveById(_previewEyeId);
    public CustomizationOptionSO GetPreviewSkisOption() => ResolveById(_previewSkisId);
    public CustomizationOptionSO GetPreviewPolesOption() => ResolveById(_previewPolesId);
    public CustomizationOptionSO GetPreviewHatOption() => ResolveById(_previewHatId);
    public CustomizationOptionSO GetPreviewJacketOption() => ResolveById(_previewJacketId);
    public CustomizationOptionSO GetPreviewGlovesOption() => ResolveById(_previewGlovesId);
    public CustomizationOptionSO GetPreviewBootsOption() => ResolveById(_previewBootsId);
    public CustomizationOptionSO GetPreviewAccessoryOption() => ResolveById(_previewAccessoryId);

    public CustomizationOptionSO GetPreviewOption(CustomizationOptionType type)
    {
        return type switch
        {
            CustomizationOptionType.EyeIcon => GetPreviewEyeOption(),
            CustomizationOptionType.Skis => GetPreviewSkisOption(),
            CustomizationOptionType.Poles => GetPreviewPolesOption(),
            CustomizationOptionType.Hat => GetPreviewHatOption(),
            CustomizationOptionType.Jacket => GetPreviewJacketOption(),
            CustomizationOptionType.Gloves => GetPreviewGlovesOption(),
            CustomizationOptionType.Boots => GetPreviewBootsOption(),
            CustomizationOptionType.Accessory => GetPreviewAccessoryOption(),
            _ => null
        };
    }

    public CustomizationOptionSO GetEffectiveEyeOption() => GetPreviewEyeOption() ?? GetEquippedEyeOption();
    public CustomizationOptionSO GetEffectiveSkisOption() => GetPreviewSkisOption() ?? GetEquippedSkisOption();
    public CustomizationOptionSO GetEffectivePolesOption() => GetPreviewPolesOption() ?? GetEquippedPolesOption();
    public CustomizationOptionSO GetEffectiveHatOption() => GetPreviewHatOption() ?? GetEquippedHatOption();
    public CustomizationOptionSO GetEffectiveJacketOption() => GetPreviewJacketOption() ?? GetEquippedJacketOption();
    public CustomizationOptionSO GetEffectiveGlovesOption() => GetPreviewGlovesOption() ?? GetEquippedGlovesOption();
    public CustomizationOptionSO GetEffectiveBootsOption() => GetPreviewBootsOption() ?? GetEquippedBootsOption();
    public CustomizationOptionSO GetEffectiveAccessoryOption() => GetPreviewAccessoryOption() ?? GetEquippedAccessoryOption();

    public CustomizationOptionSO GetEffectiveOption(CustomizationOptionType type)
    {
        return GetPreviewOption(type) ?? GetEquippedOption(type);
    }

    // Patterns
    public string GetPreviewPatternId(PatternTarget t)
    {
        return t switch
        {
            PatternTarget.Skis => _previewSkisPatternId,
            PatternTarget.Poles => _previewPolesPatternId,
            PatternTarget.Hat => _previewHatPatternId,
            PatternTarget.Jacket => _previewJacketPatternId,
            PatternTarget.Gloves => _previewGlovesPatternId,
            PatternTarget.Boots => _previewBootsPatternId,
            PatternTarget.Accessory => _previewAccessoryPatternId,
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
            case CustomizationOptionType.Gloves: _previewGlovesId = null; break;
            case CustomizationOptionType.Boots: _previewBootsId = null; break;
            case CustomizationOptionType.Accessory: _previewAccessoryId = null; break;

            case CustomizationOptionType.SkinPattern:
                switch (_patternTarget)
                {
                    case PatternTarget.Skis: _previewSkisPatternId = null; break;
                    case PatternTarget.Poles: _previewPolesPatternId = null; break;
                    case PatternTarget.Hat: _previewHatPatternId = null; break;
                    case PatternTarget.Jacket: _previewJacketPatternId = null; break;
                    case PatternTarget.Gloves: _previewGlovesPatternId = null; break;
                    case PatternTarget.Boots: _previewBootsPatternId = null; break;
                    case PatternTarget.Accessory: _previewAccessoryPatternId = null; break;
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

        if (!string.IsNullOrEmpty(_previewGlovesId))
        {
            var opt = _catalog.FindById(_previewGlovesId);
            if (opt != null && opt.type == CustomizationOptionType.Gloves)
                ApplyPreviewWearable(PatternTarget.Gloves, opt);
        }

        if (!string.IsNullOrEmpty(_previewBootsId))
        {
            var opt = _catalog.FindById(_previewBootsId);
            if (opt != null && opt.type == CustomizationOptionType.Boots)
                ApplyPreviewWearable(PatternTarget.Boots, opt);
        }

        // Hat / Jacket previews
        if (!string.IsNullOrEmpty(_previewHatId))
        {
            var opt = _catalog.FindById(_previewHatId);
            if (opt != null && opt.type == CustomizationOptionType.Hat)
            {
                ApplyPreviewWearable(PatternTarget.Hat, opt);
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
                ApplyPreviewWearable(PatternTarget.Jacket, opt);
            }
            else if (_previewJacketId == "")
            {
                customizer?.ClearJacket();
            }
        }

        if (!string.IsNullOrEmpty(_previewAccessoryId))
        {
            var opt = _catalog.FindById(_previewAccessoryId);
            if (opt != null && opt.type == CustomizationOptionType.Accessory)
            {
                ApplyPreviewWearable(PatternTarget.Accessory, opt);
            }
            else if (_previewAccessoryId == "")
            {
                customizer?.SetCurrentAccessoryOption(null);
                customizer?.ClearAccessory();
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

        if (!string.IsNullOrEmpty(_previewGlovesPatternId))
        {
            var opt = _catalog.FindById(_previewGlovesPatternId);
            if (opt != null && opt.type == CustomizationOptionType.SkinPattern)
                customizer?.SetGlovesPatternTexture(ResolvePatternTexture(opt));
        }

        if (!string.IsNullOrEmpty(_previewBootsPatternId))
        {
            var opt = _catalog.FindById(_previewBootsPatternId);
            if (opt != null && opt.type == CustomizationOptionType.SkinPattern)
                customizer?.SetBootsPatternTexture(ResolvePatternTexture(opt));
        }

        if (!string.IsNullOrEmpty(_previewAccessoryPatternId))
        {
            var opt = _catalog.FindById(_previewAccessoryPatternId);
            if (opt != null && opt.type == CustomizationOptionType.SkinPattern)
                customizer?.SetAccessoryPatternTexture(ResolvePatternTexture(opt));
        }
    }

    private void ReapplyStatePreservingPreviews()
    {
        ReapplyEquippedThenPreviews();
        NotifyChanged();
    }

    private void ClearAllPreviews()
    {
        _previewEyeId = null;
        _previewSkisId = null;
        _previewPolesId = null;
        _previewHatId = null;
        _previewJacketId = null;
        _previewGlovesId = null;
        _previewBootsId = null;
        _previewAccessoryId = null;

        _previewSkisPatternId = null;
        _previewPolesPatternId = null;
        _previewHatPatternId = null;
        _previewJacketPatternId = null;
        _previewGlovesPatternId = null;
        _previewBootsPatternId = null;
        _previewAccessoryPatternId = null;
    }

    private int ResolveCurrentShopDayKey()
    {
        if (timeController == null)
            timeController = TimeWeather.TimeController.instance ?? FindFirstObjectByType<TimeWeather.TimeController>();

        if (timeController != null)
        {
            int year = Mathf.Max(0, timeController.currentYear);
            int month = Mathf.Max(1, timeController.currentMonthIndex + 1);
            int day = Mathf.Max(1, timeController.dayOfMonth);
            return (year * 10000) + (month * 100) + day;
        }

        // Fallback only if no in-game TimeController can be found.
        return DailyShopService.GetUtcDayKey();
    }

    public void RefreshDailyOffers()
    {
        RefreshDailyOffersInternal(forceResetCache: false);
    }

    private void RefreshDailyOffersInternal(bool forceResetCache)
    {
        if (_catalog == null)
            return;

        if (forceResetCache)
            DailyShopService.ClearSavedOffers();

        int newDayKey = ResolveCurrentShopDayKey();

        DailyShopService.EnsureDailyOffers(
            _catalog, newDayKey,
            dailySkinPatternOffers,
            dailyEyeIconOffers,
            dailySkisOffers,
            dailyPolesOffers,
            dailyHatOffers,
            dailyJacketOffers,
            dailyGlovesOffers,
            dailyBootsOffers,
            dailyAccessoryOffers,
            cosmeticStockPerOffer,
            isOwnedId: (id) => _profile != null && _profile.customization != null && _profile.customization.IsUnlocked(id)
        );

        _todayKey = newDayKey;

        if (_selected != null && !IsOwned(_selected) && !DailyShopService.IsInTodayOffers(_selected.id, _todayKey))
            _selected = null;

        RebuildLists();
        NotifyChanged();
    }

    [ContextMenu("Customization Shop/Refresh Daily Offers")]
    private void ContextRefreshDailyOffers()
    {
        RefreshDailyOffersInternal(forceResetCache: false);
        Debug.Log("[CustomizationUIController] Refreshed daily offers using the current in-game day key.", this);
    }

    [ContextMenu("Customization Shop/Reset Daily Offers")]
    private void ContextResetDailyOffers()
    {
        RefreshDailyOffersInternal(forceResetCache: true);
        Debug.Log("[CustomizationUIController] Reset cached daily offers and rebuilt them for the current in-game day.", this);
    }

}
