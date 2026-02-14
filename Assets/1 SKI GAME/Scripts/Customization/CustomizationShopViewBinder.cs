using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class CustomizationShopViewBinder : MonoBehaviour
{
    [Header("UXML/UIDocument")]
    [SerializeField] private UIDocument document;

    [Header("Controller")]
    [SerializeField] private CustomizationUIController controller;

    private VisualElement _root;

    // Root tabs
    private Button _tabShop;
    private Button _tabInventory;

    // Category tabs
    private Button _tabEyes;
    private Button _tabPatterns;
    private Button _tabSkis;
    private Button _tabPoles;
    private Button _tabHats;
    private Button _tabJackets;

    // Header
    private Label _lblCurrency;
    private Button _btnExit;

    // Shop content
    private Label _lblListTitle;
    private ListView _listOptions;

    // Details
    private VisualElement _iconSelected;
    private Label _lblSelectedName;
    private Label _lblSelectedDesc;
    private Label _lblSelectedCost;
    private Label _lblSelectedOwned;

    private Button _btnBuy;
    private Button _btnEquip;

    // Loadout
    private VisualElement _swatchSkin;
    private Button _slotEyeIcon;
    private VisualElement _swatchEye;

    private Button _slotSkisGear;
    private VisualElement _swatchSkisColor;
    private Button _slotSkisPattern;

    private Button _slotPolesGear;
    private VisualElement _swatchPolesColor;
    private Button _slotPolesPattern;

    private Button _slotHatGear;
    private VisualElement _swatchHatColor;
    private Button _slotHatPattern;

    private Button _slotJacketGear;
    private VisualElement _swatchJacketColor;
    private Button _slotJacketPattern;

    // Optional labels in loadout
    private Label _lblSkisGearName, _lblPolesGearName, _lblHatGearName, _lblJacketGearName;
    private Label _lblSkisPatternName, _lblPolesPatternName, _lblHatPatternName, _lblJacketPatternName;

    // Color popover
    private VisualElement _colorOverlay;
    private VisualElement _colorPreview;
    private Button _btnCloseColor;
    private Slider _sldH, _sldS, _sldV;

    // Stable buffer so ListView never points at controller's live lists (prevents mid-enumeration edits)
    private readonly List<CustomizationOptionSO> _listBuffer = new();
    private bool _suppressSelectionCallback;

    private enum ColorTarget { None, Skin, Eye, Skis, Poles, Hat, Jacket }
    private ColorTarget _colorTarget = ColorTarget.None;

    // Cached sources for ListView (we swap between these without allocating per-frame)
    private IReadOnlyList<CustomizationOptionSO> _currentSource;

    private void Awake()
    {
        if (document == null) document = GetComponent<UIDocument>();
        _root = document != null ? document.rootVisualElement : null;

        CacheElements();
        SetupList();
        HookEvents();
        RefreshAll();
    }

    private void OnEnable() => RefreshAll();

    private void CacheElements()
    {
        if (_root == null) return;

        // Header
        _lblCurrency = _root.Q<Label>("Lbl_Currency");
        _btnExit = _root.Q<Button>("Btn_Exit");

        // Root tabs
        _tabShop = _root.Q<Button>("Tab_Shop");
        _tabInventory = _root.Q<Button>("Tab_Inventory");

        // Category tabs
        _tabEyes = _root.Q<Button>("Tab_Eyes");
        _tabPatterns = _root.Q<Button>("Tab_Patterns");
        _tabSkis = _root.Q<Button>("Tab_Skis");
        _tabPoles = _root.Q<Button>("Tab_Poles");
        _tabHats = _root.Q<Button>("Tab_Hats");
        _tabJackets = _root.Q<Button>("Tab_Jackets");

        // List + title
        _lblListTitle = _root.Q<Label>("Lbl_ListTitle");
        _listOptions = _root.Q<ListView>("List_Options");

        // Details
        _iconSelected = _root.Q<VisualElement>("Icon_Selected");
        _lblSelectedName = _root.Q<Label>("Lbl_SelectedName");
        _lblSelectedDesc = _root.Q<Label>("Lbl_SelectedDesc");
        _lblSelectedCost = _root.Q<Label>("Lbl_SelectedCost");
        _lblSelectedOwned = _root.Q<Label>("Lbl_SelectedOwned");
        _btnBuy = _root.Q<Button>("Btn_Buy");
        _btnEquip = _root.Q<Button>("Btn_Equip");

        // Loadout
        _swatchSkin = _root.Q<VisualElement>("Swatch_SkinColor");
        _slotEyeIcon = _root.Q<Button>("Slot_EyeIcon");
        _swatchEye = _root.Q<VisualElement>("Swatch_EyeColor");

        _slotSkisGear = _root.Q<Button>("Slot_SkisGear");
        _swatchSkisColor = _root.Q<VisualElement>("Swatch_SkisColor");
        _slotSkisPattern = _root.Q<Button>("Slot_SkisPattern");

        _slotPolesGear = _root.Q<Button>("Slot_PolesGear");
        _swatchPolesColor = _root.Q<VisualElement>("Swatch_PolesColor");
        _slotPolesPattern = _root.Q<Button>("Slot_PolesPattern");

        _slotHatGear = _root.Q<Button>("Slot_HatGear");
        _swatchHatColor = _root.Q<VisualElement>("Swatch_HatColor");
        _slotHatPattern = _root.Q<Button>("Slot_HatPattern");

        _slotJacketGear = _root.Q<Button>("Slot_JacketGear");
        _swatchJacketColor = _root.Q<VisualElement>("Swatch_JacketColor");
        _slotJacketPattern = _root.Q<Button>("Slot_JacketPattern");

        _lblSkisGearName = _root.Q<Label>("Lbl_SkisGearName");
        _lblPolesGearName = _root.Q<Label>("Lbl_PolesGearName");
        _lblHatGearName = _root.Q<Label>("Lbl_HatGearName");
        _lblJacketGearName = _root.Q<Label>("Lbl_JacketGearName");

        _lblSkisPatternName = _root.Q<Label>("Lbl_SkisPatternName");
        _lblPolesPatternName = _root.Q<Label>("Lbl_PolesPatternName");
        _lblHatPatternName = _root.Q<Label>("Lbl_HatPatternName");
        _lblJacketPatternName = _root.Q<Label>("Lbl_JacketPatternName");

        // Color popover
        _colorOverlay = _root.Q<VisualElement>("ColorOverlay");
        _colorPreview = _root.Q<VisualElement>("Swatch_ColorPreview");
        _btnCloseColor = _root.Q<Button>("Btn_CloseColor");
        _sldH = _root.Q<Slider>("Sld_H");
        _sldS = _root.Q<Slider>("Sld_S");
        _sldV = _root.Q<Slider>("Sld_V");
    }

    private void SetupList()
    {
        if (_listOptions == null) return;

        _listOptions.selectionType = SelectionType.Single;
        _listOptions.fixedItemHeight = 48f;

        _listOptions.makeItem = () =>
        {
            var row = new VisualElement();
            row.AddToClassList("option-row");

            var icon = new VisualElement { name = "icon" };
            icon.AddToClassList("option-icon");

            var mid = new VisualElement { name = "mid" };
            mid.AddToClassList("option-mid");

            var name = new Label { name = "name" };
            name.AddToClassList("option-name");

            var meta = new Label { name = "meta" };
            meta.AddToClassList("option-meta");

            mid.Add(name);
            mid.Add(meta);

            var right = new VisualElement { name = "right" };
            right.AddToClassList("option-right");

            var price = new Label { name = "price" };
            price.AddToClassList("option-price");

            var badge = new Label { name = "badge" };
            badge.AddToClassList("option-badge");

            right.Add(price);
            right.Add(badge);

            row.Add(icon);
            row.Add(mid);
            row.Add(right);

            return row;
        };

        _listOptions.bindItem = (ve, i) =>
        {
            if (_currentSource == null || i < 0 || i >= _currentSource.Count) return;
            var opt = _currentSource[i];
            if (opt == null) return;

            var icon = ve.Q<VisualElement>("icon");
            var name = ve.Q<Label>("name");
            var meta = ve.Q<Label>("meta");
            var price = ve.Q<Label>("price");
            var badge = ve.Q<Label>("badge");

            if (name != null) name.text = string.IsNullOrEmpty(opt.displayName) ? opt.name : opt.displayName;
            if (meta != null) meta.text = GetMetaLine(opt);

            // icon
            if (icon != null)
            {
                if (opt.icon != null && opt.icon.texture != null)
                    icon.style.backgroundImage = new StyleBackground(opt.icon.texture);
                else
                    icon.style.backgroundImage = StyleKeyword.None;
            }

            bool owned = controller != null && controller.IsOwned(opt);

            // Right side: shop shows price, inventory hides price
            bool isShop = controller != null && controller.GetRootTab() == CustomizationUIController.RootTab.Shop;
            if (price != null)
                price.text = isShop && !owned ? opt.cost.ToString() : "";

            if (badge != null)
            {
                badge.RemoveFromClassList("is-owned");
                badge.RemoveFromClassList("is-equipped");
                badge.RemoveFromClassList("is-preview");

                if (owned)
                {
                    badge.text = "OWNED";
                    badge.AddToClassList("is-owned");
                }
                else if (!isShop)
                {
                    badge.text = ""; // inventory should only contain owned anyway
                }
                else
                {
                    badge.text = ""; // no badge by default in shop
                }
            }
        };

        _listOptions.onSelectionChange += _ =>
        {
            if (_suppressSelectionCallback) return;
            if (controller == null || _listOptions == null) return;

            int idx = _listOptions.selectedIndex;
            if (idx < 0 || idx >= _listBuffer.Count) return;

            var opt = _listBuffer[idx];
            if (opt == null) return;

            // IMPORTANT: do not Rebuild/Refresh synchronously inside selection notification.
            controller.Select(opt);

            // Defer UI refresh to next tick to avoid modifying collections mid-enumeration.
            _root?.schedule.Execute(RefreshAll);
        };

    }

    private void HookEvents()
    {
        if (controller == null) return;

        // Root tabs
        if (_tabShop != null) _tabShop.clicked += () => { controller.SetRootTab(CustomizationUIController.RootTab.Shop); RefreshAll(); };
        if (_tabInventory != null) _tabInventory.clicked += () => { controller.SetRootTab(CustomizationUIController.RootTab.Inventory); RefreshAll(); };

        // Category tabs
        if (_tabEyes != null) _tabEyes.clicked += () => { controller.SetCategory(CustomizationOptionType.EyeIcon); RefreshAll(); };
        if (_tabPatterns != null) _tabPatterns.clicked += () => { controller.SetCategory(CustomizationOptionType.SkinPattern); RefreshAll(); };
        if (_tabSkis != null) _tabSkis.clicked += () => { controller.SetCategory(CustomizationOptionType.Skis); RefreshAll(); };
        if (_tabPoles != null) _tabPoles.clicked += () => { controller.SetCategory(CustomizationOptionType.Poles); RefreshAll(); };
        if (_tabHats != null) _tabHats.clicked += () => { controller.SetCategory(CustomizationOptionType.Hat); RefreshAll(); };
        if (_tabJackets != null) _tabJackets.clicked += () => { controller.SetCategory(CustomizationOptionType.Jacket); RefreshAll(); };

        // Details actions
        if (_btnBuy != null) _btnBuy.clicked += () => { controller.TryBuySelected(); RefreshAll(); };
        if (_btnEquip != null) _btnEquip.clicked += () => { controller.EquipSelected(); RefreshAll(); };

        // Loadout: gear selection
        if (_slotSkisGear != null) _slotSkisGear.clicked += () => { controller.SetCategory(CustomizationOptionType.Skis); RefreshAll(); };
        if (_slotPolesGear != null) _slotPolesGear.clicked += () => { controller.SetCategory(CustomizationOptionType.Poles); RefreshAll(); };
        if (_slotHatGear != null) _slotHatGear.clicked += () => { controller.SetCategory(CustomizationOptionType.Hat); RefreshAll(); };
        if (_slotJacketGear != null) _slotJacketGear.clicked += () => { controller.SetCategory(CustomizationOptionType.Jacket); RefreshAll(); };

        // Loadout: patterns (also selects pattern target)
        if (_slotSkisPattern != null) _slotSkisPattern.clicked += () =>
        {
            controller.SetPatternTarget(CustomizationUIController.PatternTarget.Skis);
            controller.SetCategory(CustomizationOptionType.SkinPattern);
            RefreshAll();
        };

        if (_slotPolesPattern != null) _slotPolesPattern.clicked += () =>
        {
            controller.SetPatternTarget(CustomizationUIController.PatternTarget.Poles);
            controller.SetCategory(CustomizationOptionType.SkinPattern);
            RefreshAll();
        };

        if (_slotHatPattern != null) _slotHatPattern.clicked += () =>
        {
            controller.SetPatternTarget(CustomizationUIController.PatternTarget.Hat);
            controller.SetCategory(CustomizationOptionType.SkinPattern);
            RefreshAll();
        };

        if (_slotJacketPattern != null) _slotJacketPattern.clicked += () =>
        {
            controller.SetPatternTarget(CustomizationUIController.PatternTarget.Jacket);
            controller.SetCategory(CustomizationOptionType.SkinPattern);
            RefreshAll();
        };

        // Color swatches open popover
        MakeClickable(_swatchSkin, () => OpenColor(ColorTarget.Skin, controller.GetSkinColor()));
        MakeClickable(_swatchEye, () => OpenColor(ColorTarget.Eye, controller.GetEyeColor()));
        MakeClickable(_swatchSkisColor, () => OpenColor(ColorTarget.Skis, controller.GetSkisColor()));
        MakeClickable(_swatchPolesColor, () => OpenColor(ColorTarget.Poles, controller.GetPolesColor()));
        MakeClickable(_swatchHatColor, () => OpenColor(ColorTarget.Hat, controller.GetHatColor()));
        MakeClickable(_swatchJacketColor, () => OpenColor(ColorTarget.Jacket, controller.GetJacketColor()));

        if (_btnCloseColor != null) _btnCloseColor.clicked += CloseColor;

        if (_sldH != null) _sldH.RegisterValueChangedCallback(_ => OnColorSlidersChanged());
        if (_sldS != null) _sldS.RegisterValueChangedCallback(_ => OnColorSlidersChanged());
        if (_sldV != null) _sldV.RegisterValueChangedCallback(_ => OnColorSlidersChanged());

        if (_btnExit != null) _btnExit.clicked += () => controller.Exit(apply: true);
    }

    private static void MakeClickable(VisualElement ve, Action onClick)
    {
        if (ve == null || onClick == null) return;
        ve.AddManipulator(new Clickable(onClick));
    }

    private void RefreshAll()
    {
        if (controller == null) return;

        // Header
        if (_lblCurrency != null) _lblCurrency.text = controller.GetCurrency().ToString();

        // List title
        if (_lblListTitle != null)
        {
            _lblListTitle.text = controller.GetRootTab() == CustomizationUIController.RootTab.Shop
                ? "Today's Offers"
                : "Inventory";
        }

        // List source swap
        _currentSource = controller.GetRootTab() == CustomizationUIController.RootTab.Shop
    ? controller.GetVisibleList()
    : controller.GetOwnedList();

        if (_listOptions != null)
        {
            _listBuffer.Clear();
            if (_currentSource != null)
            {
                for (int i = 0; i < _currentSource.Count; i++)
                    _listBuffer.Add(_currentSource[i]);
            }

            _suppressSelectionCallback = true;
            _listOptions.itemsSource = _listBuffer;
            _listOptions.Rebuild();
            _suppressSelectionCallback = false;
        }


        // Details
        RefreshDetails();

        // Loadout swatches
        if (_swatchSkin != null) _swatchSkin.style.backgroundColor = controller.GetSkinColor();
        if (_swatchEye != null) _swatchEye.style.backgroundColor = controller.GetEyeColor();
        if (_swatchSkisColor != null) _swatchSkisColor.style.backgroundColor = controller.GetSkisColor();
        if (_swatchPolesColor != null) _swatchPolesColor.style.backgroundColor = controller.GetPolesColor();
        if (_swatchHatColor != null) _swatchHatColor.style.backgroundColor = controller.GetHatColor();
        if (_swatchJacketColor != null) _swatchJacketColor.style.backgroundColor = controller.GetJacketColor();

        UpdatePatternTargetHighlight();
    }

    private void RefreshDetails()
    {
        var sel = controller.GetSelected();

        if (sel == null)
        {
            if (_lblSelectedName != null) _lblSelectedName.text = "Select an item";
            if (_lblSelectedDesc != null) _lblSelectedDesc.text = "";
            if (_lblSelectedCost != null) _lblSelectedCost.text = "";
            if (_lblSelectedOwned != null) _lblSelectedOwned.text = "";

            if (_btnBuy != null) _btnBuy.AddToClassList("is-hidden");
            if (_btnEquip != null) _btnEquip.AddToClassList("is-hidden");
            if (_iconSelected != null) _iconSelected.style.backgroundImage = StyleKeyword.None;
            return;
        }

        bool owned = controller.IsOwned(sel);
        bool isShop = controller.GetRootTab() == CustomizationUIController.RootTab.Shop;

        if (_lblSelectedName != null) _lblSelectedName.text = string.IsNullOrEmpty(sel.displayName) ? sel.name : sel.displayName;
        if (_lblSelectedDesc != null) _lblSelectedDesc.text = sel.description;
        if (_lblSelectedOwned != null) _lblSelectedOwned.text = owned ? "Owned" : "Not owned";

        if (_lblSelectedCost != null)
            _lblSelectedCost.text = isShop && !owned ? $"Cost: {sel.cost}" : "";

        if (_iconSelected != null)
        {
            if (sel.icon != null && sel.icon.texture != null)
                _iconSelected.style.backgroundImage = new StyleBackground(sel.icon.texture);
            else
                _iconSelected.style.backgroundImage = StyleKeyword.None;
        }

        // Actions
        if (_btnBuy != null)
        {
            if (isShop && !owned)
                _btnBuy.RemoveFromClassList("is-hidden");
            else
                _btnBuy.AddToClassList("is-hidden");
        }

        if (_btnEquip != null)
        {
            // Equip button is mostly redundant now (owned auto equips on select),
            // but keep it visible in inventory for clarity.
            if (!isShop && owned)
                _btnEquip.RemoveFromClassList("is-hidden");
            else
                _btnEquip.AddToClassList("is-hidden");
        }
    }

    private string GetMetaLine(CustomizationOptionSO opt)
    {
        if (opt == null) return "";
        switch (opt.type)
        {
            case CustomizationOptionType.EyeIcon: return "Eyes";
            case CustomizationOptionType.SkinPattern: return "Pattern";
            case CustomizationOptionType.Skis: return "Skis";
            case CustomizationOptionType.Poles: return "Poles";
            case CustomizationOptionType.Hat: return "Hat";
            case CustomizationOptionType.Jacket: return "Jacket";
            default: return opt.type.ToString();
        }
    }

    private void UpdatePatternTargetHighlight()
    {
        ClearTarget(_slotSkisPattern);
        ClearTarget(_slotPolesPattern);
        ClearTarget(_slotHatPattern);
        ClearTarget(_slotJacketPattern);

        if (controller.GetActiveCategory() != CustomizationOptionType.SkinPattern) return;

        switch (controller.GetPatternTarget())
        {
            case CustomizationUIController.PatternTarget.Skis: SetTarget(_slotSkisPattern); break;
            case CustomizationUIController.PatternTarget.Poles: SetTarget(_slotPolesPattern); break;
            case CustomizationUIController.PatternTarget.Hat: SetTarget(_slotHatPattern); break;
            case CustomizationUIController.PatternTarget.Jacket: SetTarget(_slotJacketPattern); break;
        }
    }

    private static void SetTarget(VisualElement ve) { if (ve != null) ve.AddToClassList("is-pattern-target"); }
    private static void ClearTarget(VisualElement ve) { if (ve != null) ve.RemoveFromClassList("is-pattern-target"); }

    private void OpenColor(ColorTarget target, Color current)
    {
        _colorTarget = target;
        if (_colorOverlay != null) _colorOverlay.RemoveFromClassList("is-hidden");

        Color.RGBToHSV(current, out float h, out float s, out float v);
        if (_sldH != null) _sldH.SetValueWithoutNotify(h);
        if (_sldS != null) _sldS.SetValueWithoutNotify(s);
        if (_sldV != null) _sldV.SetValueWithoutNotify(v);

        if (_colorPreview != null) _colorPreview.style.backgroundColor = current;
    }

    private void CloseColor()
    {
        _colorTarget = ColorTarget.None;
        if (_colorOverlay != null) _colorOverlay.AddToClassList("is-hidden");
    }

    private void OnColorSlidersChanged()
    {
        if (controller == null || _colorTarget == ColorTarget.None) return;

        float h = _sldH != null ? _sldH.value : 0f;
        float s = _sldS != null ? _sldS.value : 0f;
        float v = _sldV != null ? _sldV.value : 0f;

        Color c = Color.HSVToRGB(h, s, v);
        if (_colorPreview != null) _colorPreview.style.backgroundColor = c;

        switch (_colorTarget)
        {
            case ColorTarget.Skin: controller.SetSkinColor(c); break;
            case ColorTarget.Eye: controller.SetEyeColor(c); break;
            case ColorTarget.Skis: controller.SetSkisColor(c); break;
            case ColorTarget.Poles: controller.SetPolesColor(c); break;
            case ColorTarget.Hat: controller.SetHatColor(c); break;
            case ColorTarget.Jacket: controller.SetJacketColor(c); break;
        }

        RefreshAll();
    }
}
