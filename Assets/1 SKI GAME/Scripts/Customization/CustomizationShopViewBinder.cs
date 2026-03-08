using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static CustomizationUIController;

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
    private VisualElement _slotEyeIcon;
    private VisualElement _swatchEye;

    private VisualElement _slotSkisGear;
    private Button _slotSkisPattern;
    
    private VisualElement _swatchSkisColor;
    //private Button _slotSkisPattern;


    private VisualElement _slotPolesGear;
    private Button _slotPolesPattern;
    private VisualElement _swatchPolesColor;
    //private Button _slotPolesPattern;

    private VisualElement _slotHatGear;
    private Button _slotHatPattern;
    private VisualElement _swatchHatColor;
    //private Button _slotHatPattern;

    private VisualElement _slotJacketGear;
    private Button _slotJacketPattern;
    private VisualElement _swatchJacketColor;
    //private Button _slotJacketPattern;

    // Optional: make entire row clickable (covers empty space/color blocks)
    private VisualElement _rowSkis, _rowPoles, _rowHat, _rowJacket, _rowEyes;
    // Optional labels in loadout
    private Label _lblSkisGearName, _lblPolesGearName, _lblHatGearName, _lblJacketGearName;
    private Label _lblSkisPatternName, _lblPolesPatternName, _lblHatPatternName, _lblJacketPatternName;

    private Label _lblEyeName;
    private Image _imgEyeIcon;

    private Button _iconSkisPattern, _iconPolesPattern, _iconHatPattern, _iconJacketPattern;

    // Loadout state badges
    private Label _lblEyeState, _lblSkisState, _lblPolesState, _lblHatState, _lblJacketState;

    private Button _btnResetSkisColor, _btnResetPolesColor, _btnResetHatColor, _btnResetJacketColor;
    private Button _btnResetSkisPattern, _btnResetPolesPattern, _btnResetHatPattern, _btnResetJacketPattern;

    private Button _btnClearEyePreview;
    private Button _btnClearSkisPreview;
    private Button _btnClearPolesPreview;
    private Button _btnClearHatPreview;
    private Button _btnClearJacketPreview;

    // Color popover
    private VisualElement _colorOverlay;
    private VisualElement _colorPreview;
    private Button _btnCloseColor;
    private Slider _sldH, _sldS, _sldV;

    private VisualElement _hTrack, _sTrack, _vTrack;
    private VisualElement _hDragger, _sDragger, _vDragger;

    private Texture2D _texHue, _texSat, _texVal;
    private Color32[] _bufHue, _bufSat, _bufVal;

    private const int GradW = 256;
    private const int GradH = 1;
    // NEW: preset row
    private VisualElement _rowColorPresets;

    private static readonly Color LoadoutEquippedBorder = new Color(0f, 1f, 160f / 255f, 0.65f);
    private static readonly Color LoadoutPreviewBorder = new Color(1f, 210f / 255f, 80f / 255f, 0.65f);

    [Header("Colour Presets")]
    [SerializeField]
    private Color[] colourPresets = new Color[]
{
    // --- Neutrals (always useful) ---
    new Color(1.00f, 1.00f, 1.00f, 1f), // White
    new Color(0.92f, 0.92f, 0.92f, 1f), // Light Grey
    new Color(0.75f, 0.75f, 0.75f, 1f), // Mid Grey
    new Color(0.55f, 0.55f, 0.55f, 1f), // Dark Grey
    new Color(0.25f, 0.25f, 0.25f, 1f), // Charcoal
    new Color(0.08f, 0.08f, 0.08f, 1f), // Near-Black

    // --- Saturated “wheel” anchors ---
    new Color(0.92f, 0.18f, 0.18f, 1f), // Red
    new Color(0.98f, 0.45f, 0.10f, 1f), // Orange
    new Color(0.98f, 0.82f, 0.14f, 1f), // Yellow
    new Color(0.20f, 0.80f, 0.30f, 1f), // Green
    new Color(0.10f, 0.78f, 0.78f, 1f), // Cyan
    new Color(0.18f, 0.45f, 0.98f, 1f), // Blue
    new Color(0.45f, 0.20f, 0.95f, 1f), // Violet
    new Color(0.92f, 0.20f, 0.70f, 1f), // Magenta

    // --- Muted / stylish accents (less “neon”) ---
    new Color(0.72f, 0.24f, 0.24f, 1f), // Muted Red
    new Color(0.78f, 0.52f, 0.18f, 1f), // Muted Orange
    new Color(0.62f, 0.62f, 0.22f, 1f), // Olive Yellow
    new Color(0.24f, 0.55f, 0.38f, 1f), // Muted Green
    new Color(0.22f, 0.52f, 0.58f, 1f), // Muted Cyan
    new Color(0.26f, 0.36f, 0.62f, 1f), // Muted Blue
    new Color(0.44f, 0.32f, 0.62f, 1f), // Muted Purple
    new Color(0.68f, 0.32f, 0.52f, 1f), // Muted Magenta

    // --- Earth tones (great for gear + realism) ---
    new Color(0.48f, 0.34f, 0.22f, 1f), // Brown
    new Color(0.72f, 0.56f, 0.38f, 1f), // Tan
    new Color(0.30f, 0.40f, 0.26f, 1f), // Forest / Moss
    new Color(0.18f, 0.26f, 0.22f, 1f), // Deep Teal-ish

    // --- Pastels (nice for cosmetics / UI-friendly) ---
    new Color(0.98f, 0.74f, 0.74f, 1f), // Pastel Pink
    new Color(0.98f, 0.84f, 0.70f, 1f), // Pastel Peach
    new Color(0.92f, 0.92f, 0.70f, 1f), // Pastel Yellow
    new Color(0.74f, 0.92f, 0.78f, 1f), // Pastel Green
    new Color(0.72f, 0.88f, 0.98f, 1f), // Pastel Blue
    new Color(0.84f, 0.78f, 0.98f, 1f), // Pastel Lavender
};

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
        SetupColorGradientUI();

        SetupList();
        HookEvents();

        if (controller != null)
            controller.OnChanged += HandleControllerChanged;

        BuildColourPresets();

        RefreshAll();
    }

    private void OnEnable() => RefreshAll();

    private void OnDestroy()
    {
        if (controller != null)
            controller.OnChanged -= HandleControllerChanged;
    }

    private void HandleControllerChanged()
    {
        // Ensure this runs after controller finishes its updates
        _root?.schedule.Execute(() => RefreshAll()).ExecuteLater(0);
    }

    private T QAny<T>(VisualElement root, params string[] names) where T : VisualElement
    {
        if (root == null) return null;
        foreach (var n in names)
        {
            var ve = root.Q<T>(n);
            if (ve != null) return ve;
        }
        return null;
    }

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

        // UXML currently names the eyes row "Slot_EyeIcon" (and it is a VisualElement).
        // Older iterations used Row_Eyes/Row_Eye/etc. Support all of them.
        _rowEyes = QAny<VisualElement>(_root, "Slot_EyeIcon", "Row_Eyes", "Row_Eye", "Row_EyeIcon");
        _slotEyeIcon = QAny<VisualElement>(_root, "Slot_EyeIcon");

        // Make sure children don't steal the click.
        if (_rowEyes != null)
        {
            foreach (var child in _rowEyes.Children())
                child.pickingMode = PickingMode.Ignore;
        }

        _swatchEye = _root.Q<VisualElement>("Swatch_EyeColor");

        _rowSkis = _root.Q<VisualElement>("Row_Skis");
        _rowPoles = _root.Q<VisualElement>("Row_Poles");
        _rowHat = _root.Q<VisualElement>("Row_Hat");
        _rowJacket = _root.Q<VisualElement>("Row_Jacket");

        _slotSkisGear = _root.Q<VisualElement>("Slot_SkisGear");
        _slotSkisPattern = _root.Q<Button>("Slot_SkisPattern");
        _swatchSkisColor = _root.Q<VisualElement>("Swatch_SkisColor");
        //_slotSkisPattern = _root.Q<Button>("Slot_SkisPattern");

        _slotPolesGear = _root.Q<VisualElement>("Slot_PolesGear");
        _slotPolesPattern = _root.Q<Button>("Slot_PolesPattern");
        _swatchPolesColor = _root.Q<VisualElement>("Swatch_PolesColor");
        //_slotPolesPattern = _root.Q<Button>("Slot_PolesPattern");

        _slotHatGear = _root.Q<VisualElement>("Slot_HatGear");
        _slotHatPattern = _root.Q<Button>("Slot_HatPattern");
        _swatchHatColor = _root.Q<VisualElement>("Swatch_HatColor");
        //_slotHatPattern = _root.Q<Button>("Slot_HatPattern");

        _slotJacketGear = _root.Q<VisualElement>("Slot_JacketGear");
        _slotJacketPattern = _root.Q<Button>("Slot_JacketPattern");
        _swatchJacketColor = _root.Q<VisualElement>("Swatch_JacketColor");
        //_slotJacketPattern = _root.Q<Button>("Slot_JacketPattern");

        _lblSkisGearName = _root.Q<Label>("Lbl_SkisGearName");
        _lblPolesGearName = _root.Q<Label>("Lbl_PolesGearName");
        _lblHatGearName = _root.Q<Label>("Lbl_HatGearName");
        _lblJacketGearName = _root.Q<Label>("Lbl_JacketGearName");

        _lblSkisPatternName = _root.Q<Label>("Lbl_SkisPatternName");
        _lblPolesPatternName = _root.Q<Label>("Lbl_PolesPatternName");
        _lblHatPatternName = _root.Q<Label>("Lbl_HatPatternName");
        _lblJacketPatternName = _root.Q<Label>("Lbl_JacketPatternName");

        _lblEyeName = _root.Q<Label>("Lbl_EyeName");
        _imgEyeIcon = _root.Q<Image>("Icon_EyeIcon");

        _iconSkisPattern = _root.Q<Button>("Icon_SkisPattern");
        _iconPolesPattern = _root.Q<Button>("Icon_PolesPattern");
        _iconHatPattern = _root.Q<Button>("Icon_HatPattern");
        _iconJacketPattern = _root.Q<Button>("Icon_JacketPattern");

        _lblEyeState = _root.Q<Label>("Lbl_EyeState");
        _lblSkisState = _root.Q<Label>("Lbl_SkisState");
        _lblPolesState = _root.Q<Label>("Lbl_PolesState");
        _lblHatState = _root.Q<Label>("Lbl_HatState");
        _lblJacketState = _root.Q<Label>("Lbl_JacketState");

        _btnResetSkisColor = _root.Q<Button>("Btn_ResetSkisColor");
        _btnResetPolesColor = _root.Q<Button>("Btn_ResetPolesColor");
        _btnResetHatColor = _root.Q<Button>("Btn_ResetHatColor");
        _btnResetJacketColor = _root.Q<Button>("Btn_ResetJacketColor");

        _btnResetSkisPattern = _root.Q<Button>("Btn_ResetSkisPattern");
        _btnResetPolesPattern = _root.Q<Button>("Btn_ResetPolesPattern");
        _btnResetHatPattern = _root.Q<Button>("Btn_ResetHatPattern");
        _btnResetJacketPattern = _root.Q<Button>("Btn_ResetJacketPattern");

        _btnClearEyePreview = _root.Q<Button>("Btn_ClearEyePreview");
        _btnClearSkisPreview = _root.Q<Button>("Btn_ClearSkisPreview");
        _btnClearPolesPreview = _root.Q<Button>("Btn_ClearPolesPreview");
        _btnClearHatPreview = _root.Q<Button>("Btn_ClearHatPreview");
        _btnClearJacketPreview = _root.Q<Button>("Btn_ClearJacketPreview");

        // Color popover
        _colorOverlay = _root.Q<VisualElement>("ColorOverlay");
        _colorPreview = _root.Q<VisualElement>("Swatch_ColorPreview");
        _btnCloseColor = _root.Q<Button>("Btn_CloseColor");
        _sldH = _root.Q<Slider>("Sld_H");
        _sldS = _root.Q<Slider>("Sld_S");
        _sldV = _root.Q<Slider>("Sld_V");
        _rowColorPresets = _root.Q<VisualElement>("Row_ColorPresets");

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

            row.AddManipulator(new Clickable(() =>
            {
                if (controller == null || _listOptions == null) return;

                if (row.userData is not int idx) return;
                if (idx < 0 || idx >= _listBuffer.Count) return;

                var opt = _listBuffer[idx];
                if (opt == null) return;

                // Ensure selection highlight updates even if same index
                _suppressSelectionCallback = true;
                _listOptions.SetSelection(idx);
                _suppressSelectionCallback = false;

                controller.Select(opt);
                _root?.schedule.Execute(RefreshAll);
            }));

            return row;
        };

        _listOptions.bindItem = (ve, i) =>
        {
            if (_currentSource == null || i < 0 || i >= _currentSource.Count) return;
            var opt = _currentSource[i];
            if (opt == null) return;

            ve.userData = i;

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
                var tex = GetOptionIconTexture2D(opt);
                if (tex != null)
                    icon.style.backgroundImage = new StyleBackground(tex);
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

        if (_btnResetSkisColor != null) _btnResetSkisColor.clicked += () => { controller.ResetSkisColorToDefault(); RefreshAll(); };
        if (_btnResetPolesColor != null) _btnResetPolesColor.clicked += () => { controller.ResetPolesColorToDefault(); RefreshAll(); };
        if (_btnResetHatColor != null) _btnResetHatColor.clicked += () => { controller.ResetHatColorToDefault(); RefreshAll(); };
        if (_btnResetJacketColor != null) _btnResetJacketColor.clicked += () => { controller.ResetJacketColorToDefault(); RefreshAll(); };

        if (_btnResetSkisPattern != null) _btnResetSkisPattern.clicked += () => { controller.ResetPatternToDefault(CustomizationUIController.PatternTarget.Skis); RefreshAll(); };
        if (_btnResetPolesPattern != null) _btnResetPolesPattern.clicked += () => { controller.ResetPatternToDefault(CustomizationUIController.PatternTarget.Poles); RefreshAll(); };
        if (_btnResetHatPattern != null) _btnResetHatPattern.clicked += () => { controller.ResetPatternToDefault(CustomizationUIController.PatternTarget.Hat); RefreshAll(); };
        if (_btnResetJacketPattern != null) _btnResetJacketPattern.clicked += () => { controller.ResetPatternToDefault(CustomizationUIController.PatternTarget.Jacket); RefreshAll(); };

        if (_btnClearEyePreview != null) _btnClearEyePreview.clicked += () => { controller.ClearPreviewEye(); RefreshAll(); };
        if (_btnClearSkisPreview != null) _btnClearSkisPreview.clicked += () => { controller.ClearPreviewSkis(); RefreshAll(); };
        if (_btnClearPolesPreview != null) _btnClearPolesPreview.clicked += () => { controller.ClearPreviewPoles(); RefreshAll(); };
        if (_btnClearHatPreview != null) _btnClearHatPreview.clicked += () => { controller.ClearPreviewHat(); RefreshAll(); };
        if (_btnClearJacketPreview != null) _btnClearJacketPreview.clicked += () => { controller.ClearPreviewJacket(); RefreshAll(); };

        // Loadout: gear selection
        // Eyes: click anywhere on the row opens Eyes category
        MakeClickable(_rowEyes, () => { controller.SetCategory(CustomizationOptionType.EyeIcon); RefreshAll(); });
        MakeClickable(_rowSkis, () => { controller.SetCategory(CustomizationOptionType.Skis); RefreshAll(); });
        MakeClickable(_rowPoles, () => { controller.SetCategory(CustomizationOptionType.Poles); RefreshAll(); });
        MakeClickable(_rowHat, () => { controller.SetCategory(CustomizationOptionType.Hat); RefreshAll(); });
        MakeClickable(_rowJacket, () => { controller.SetCategory(CustomizationOptionType.Jacket); RefreshAll(); });

        // Also make just the “top line” elements clickable (if you prefer narrower hit area)
        MakeClickable(_slotSkisGear, () => { controller.SetCategory(CustomizationOptionType.Skis); RefreshAll(); });
        MakeClickable(_slotPolesGear, () => { controller.SetCategory(CustomizationOptionType.Poles); RefreshAll(); });
        MakeClickable(_slotHatGear, () => { controller.SetCategory(CustomizationOptionType.Hat); RefreshAll(); });
        MakeClickable(_slotJacketGear, () => { controller.SetCategory(CustomizationOptionType.Jacket); RefreshAll(); });


        // Loadout: patterns (also selects pattern target)
        if (_iconSkisPattern != null) _iconSkisPattern.clicked += () =>
        {
            controller.SetPatternTarget(CustomizationUIController.PatternTarget.Skis);
            controller.SetCategory(CustomizationOptionType.SkinPattern);
            RefreshAll();
        };

        if (_iconPolesPattern != null) _iconPolesPattern.clicked += () =>
        {
            controller.SetPatternTarget(CustomizationUIController.PatternTarget.Poles);
            controller.SetCategory(CustomizationOptionType.SkinPattern);
            RefreshAll();
        };

        if (_iconHatPattern != null) _iconHatPattern.clicked += () =>
        {
            controller.SetPatternTarget(CustomizationUIController.PatternTarget.Hat);
            controller.SetCategory(CustomizationOptionType.SkinPattern);
            RefreshAll();
        };

        if (_iconJacketPattern != null) _iconJacketPattern.clicked += () =>
        {
            controller.SetPatternTarget(CustomizationUIController.PatternTarget.Jacket);
            controller.SetCategory(CustomizationOptionType.SkinPattern);
            RefreshAll();
        };

        // Loadout: patterns - clicking anywhere on the pattern slot should open pattern list
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

    private void MakeClickable(VisualElement ve, Action onClick)
    {
        if (ve == null || onClick == null) return;

        // Avoid duplicate manipulators if CacheElements/HookEvents gets called again
        ve.pickingMode = PickingMode.Position;

        ve.AddManipulator(new Clickable(() =>
        {
            if (controller == null) return;
            onClick.Invoke();
        }));
    }

    private void RefreshAll()
    {
        if (_root == null) return;

        // Always refresh loadout swatches even if controller isn't open yet,
        // but don't touch lists/details until controller is open.
        bool controllerReady = controller != null && controller.IsOpen;

        // This prevents "blank until selection" by ensuring we refresh again
        // automatically when Open() completes via controller.OnChanged.

        if (controllerReady)
        {
            // Header
            if (_lblCurrency != null) _lblCurrency.text = controller.GetCurrency().ToString();

            // List title
            if (_lblListTitle != null)
            {
                _lblListTitle.text = controller.GetRootTab() == CustomizationUIController.RootTab.Shop
                    ? "Today's Offers (Buy / Preview)"
                    : "Owned Items (Equip / Preview)";
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
                AutoSelectEquippedRow();

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

        if (controller != null)
            RefreshLoadoutEquipped();

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
            var tex = GetOptionIconTexture2D(sel);
            if (tex != null)
                _iconSelected.style.backgroundImage = new StyleBackground(tex);
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

    private Texture2D GetOptionIconTexture2D(CustomizationOptionSO opt)
    {
        if (opt == null) return null;

        // Prefer explicit sprite icon
        if (opt.icon != null && opt.icon.texture != null)
            return opt.icon.texture;

        // Skin patterns: use direct texture payload if it's Texture2D, else controller resolver
        if (opt.type == CustomizationOptionType.SkinPattern)
        {
            if (opt.skinPatternTexture is Texture2D t2d)
                return t2d;

            if (controller != null)
                return controller.GetPatternTexture2D(opt);
        }

        // Eyes: allow eyeSprite as fallback
        if (opt.type == CustomizationOptionType.EyeIcon && opt.eyeSprite != null && opt.eyeSprite.texture != null)
            return opt.eyeSprite.texture;

        return null;
    }

    private void UpdatePatternTargetHighlight()
    {
        ClearTarget(_iconSkisPattern);
        ClearTarget(_iconPolesPattern);
        ClearTarget(_iconHatPattern);
        ClearTarget(_iconJacketPattern);

        if (controller.GetActiveCategory() != CustomizationOptionType.SkinPattern) return;

        switch (controller.GetPatternTarget())
        {
            case CustomizationUIController.PatternTarget.Skis: SetTarget(_iconSkisPattern); break;
            case CustomizationUIController.PatternTarget.Poles: SetTarget(_iconPolesPattern); break;
            case CustomizationUIController.PatternTarget.Hat: SetTarget(_iconHatPattern); break;
            case CustomizationUIController.PatternTarget.Jacket: SetTarget(_iconJacketPattern); break;
        }
    }

    private static void SetTarget(VisualElement ve) { if (ve != null) ve.AddToClassList("is-pattern-target"); }
    private static void ClearTarget(VisualElement ve) { if (ve != null) ve.RemoveFromClassList("is-pattern-target"); }

    private void SetLoadoutBadge(Label badge, bool isPreview)
    {
        if (badge == null) return;

        badge.text = isPreview ? "PREVIEW" : "EQUIPPED";

        badge.RemoveFromClassList("is-equipped");
        badge.RemoveFromClassList("is-preview");
        badge.AddToClassList(isPreview ? "is-preview" : "is-equipped");

        // Ensure it shows even if a prior state hid it
        badge.style.display = DisplayStyle.Flex;
    }

    private void SetPatternThumbBorder(VisualElement thumb, bool isPreview)
    {
        if (thumb == null) return;

        var c = isPreview ? LoadoutPreviewBorder : LoadoutEquippedBorder;

        thumb.style.borderTopWidth = 2;
        thumb.style.borderRightWidth = 2;
        thumb.style.borderBottomWidth = 2;
        thumb.style.borderLeftWidth = 2;

        thumb.style.borderTopColor = c;
        thumb.style.borderRightColor = c;
        thumb.style.borderBottomColor = c;
        thumb.style.borderLeftColor = c;
    }

    private void RefreshLoadoutEquipped()
    {
        if (controller == null) return;

        // --- EYES ---
        bool eyePrev = controller.IsPreviewingEye();
        var eye = controller.GetEffectiveEyeOption();

        if (_lblEyeName != null)
            _lblEyeName.text = eye != null ? (string.IsNullOrEmpty(eye.displayName) ? eye.name : eye.displayName) : "Default";

        // Eye icon: prefer icon, then eyeSprite (your option SO supports both)
        if (_imgEyeIcon != null)
            _imgEyeIcon.sprite = eye != null ? (eye.icon != null ? eye.icon : eye.eyeSprite) : null;

        SetLoadoutBadge(_lblEyeState, eyePrev);
        if (_btnClearEyePreview != null)
            _btnClearEyePreview.style.display = eyePrev ? DisplayStyle.Flex : DisplayStyle.None;

        // --- SKIS / POLES / HAT / JACKET (names + badges) ---
        bool skisPrev = controller.IsPreviewingSkis();
        var skis = controller.GetEffectiveSkisOption();
        if (_lblSkisGearName != null)
            _lblSkisGearName.text = skis != null ? (string.IsNullOrEmpty(skis.displayName) ? skis.name : skis.displayName) : "Default";
        SetLoadoutBadge(_lblSkisState, skisPrev);
        if (_btnClearSkisPreview != null)
            _btnClearSkisPreview.style.display = skisPrev ? DisplayStyle.Flex : DisplayStyle.None;

        bool polesPrev = controller.IsPreviewingPoles();
        var poles = controller.GetEffectivePolesOption();
        if (_lblPolesGearName != null)
            _lblPolesGearName.text = poles != null ? (string.IsNullOrEmpty(poles.displayName) ? poles.name : poles.displayName) : "Default";
        SetLoadoutBadge(_lblPolesState, polesPrev);
        if (_btnClearPolesPreview != null)
            _btnClearPolesPreview.style.display = polesPrev ? DisplayStyle.Flex : DisplayStyle.None;


        bool hatPrev = controller.IsPreviewingHat();
        var hat = controller.GetEffectiveHatOption();
        if (_lblHatGearName != null)
            _lblHatGearName.text = hat != null ? (string.IsNullOrEmpty(hat.displayName) ? hat.name : hat.displayName) : "None";
        SetLoadoutBadge(_lblHatState, hatPrev);
        if (_btnClearHatPreview != null)
            _btnClearHatPreview.style.display = hatPrev ? DisplayStyle.Flex : DisplayStyle.None;

        bool jacketPrev = controller.IsPreviewingJacket();
        var jacket = controller.GetEffectiveJacketOption();
        if (_lblJacketGearName != null)
            _lblJacketGearName.text = jacket != null ? (string.IsNullOrEmpty(jacket.displayName) ? jacket.name : jacket.displayName) : "None";
        SetLoadoutBadge(_lblJacketState, jacketPrev);
        if (_btnClearJacketPreview != null)
            _btnClearJacketPreview.style.display = jacketPrev ? DisplayStyle.Flex : DisplayStyle.None;

        // --- PATTERNS (texture + name + border state) ---
        void ApplyPattern(CustomizationUIController.PatternTarget t, VisualElement iconVe, Label nameLbl)
        {
            bool patPrev = controller.IsPreviewingPattern(t);
            var opt = controller.GetEffectivePatternOption(t);
            var tex = controller.GetEffectivePatternTexture(t);

            if (iconVe != null)
            {
                if (tex != null) iconVe.style.backgroundImage = new StyleBackground(tex);
                else iconVe.style.backgroundImage = StyleKeyword.None;

                // Border matches Equipped vs Preview badge color
                SetPatternThumbBorder(iconVe, patPrev);
            }

            if (nameLbl != null)
                nameLbl.text = opt != null ? (string.IsNullOrEmpty(opt.displayName) ? opt.name : opt.displayName) : "Default";
        }

        ApplyPattern(CustomizationUIController.PatternTarget.Skis, _iconSkisPattern, _lblSkisPatternName);
        ApplyPattern(CustomizationUIController.PatternTarget.Poles, _iconPolesPattern, _lblPolesPatternName);
        ApplyPattern(CustomizationUIController.PatternTarget.Hat, _iconHatPattern, _lblHatPatternName);
        ApplyPattern(CustomizationUIController.PatternTarget.Jacket, _iconJacketPattern, _lblJacketPatternName);
    }

    private void AutoSelectEquippedRow()
    {
        if (controller == null || _listOptions == null) return;
        if (_listBuffer == null || _listBuffer.Count == 0) return;

        // Don’t override an explicit user selection
        if (controller.GetSelected() != null) return;

        string desiredId = null;
        var cat = controller.GetActiveCategory();

        // Only do this in Inventory so Shop doesn’t jump selection unexpectedly
        if (controller.GetRootTab() != CustomizationUIController.RootTab.Inventory)
            return;

        switch (cat)
        {
            case CustomizationOptionType.EyeIcon:
                desiredId = controller.GetEquippedEyeOption()?.id;
                break;
            case CustomizationOptionType.Skis:
                desiredId = controller.GetEquippedSkisOption()?.id;
                break;
            case CustomizationOptionType.Poles:
                desiredId = controller.GetEquippedPolesOption()?.id;
                break;
            case CustomizationOptionType.Hat:
                desiredId = controller.GetEquippedHatOption()?.id; // null if "None"
                if (string.IsNullOrEmpty(desiredId)) desiredId = ""; // ensure None matches
                break;
            case CustomizationOptionType.Jacket:
                desiredId = controller.GetEquippedJacketOption()?.id;
                if (string.IsNullOrEmpty(desiredId)) desiredId = "";
                break;
        }

        int idx = -1;
        for (int i = 0; i < _listBuffer.Count; i++)
        {
            var o = _listBuffer[i];
            if (o == null) continue;
            if (o.id == desiredId)
            {
                idx = i;
                break;
            }
        }

        if (idx < 0) idx = 0; // fall back to first item (often None for hat/jacket)

        _suppressSelectionCallback = true;
        _listOptions.SetSelection(idx);
        _suppressSelectionCallback = false;

        controller.Select(_listBuffer[idx]);
    }

    private void OpenColor(ColorTarget target, Color current)
    {
        _colorTarget = target;
        if (_colorOverlay != null) _colorOverlay.RemoveFromClassList("is-hidden");

        
        Color.RGBToHSV(current, out float h, out float s, out float v);
        _sldH?.SetValueWithoutNotify(h);
        _sldS?.SetValueWithoutNotify(s);
        _sldV?.SetValueWithoutNotify(v);

        if (_colorPreview != null) _colorPreview.style.backgroundColor = current;

        RefreshSVGradients();
        RefreshDraggerColors(current);
    }

    private void CloseColor()
    {
        _colorTarget = ColorTarget.None;
        if (_colorOverlay != null) _colorOverlay.AddToClassList("is-hidden");
    }

    private void SetupColorGradientUI()
    {
        if (_sldH == null || _sldS == null || _sldV == null) return;

        // Cache tracker + dragger elements from the built-in Slider hierarchy
        _hTrack = _sldH.Q<VisualElement>("unity-tracker");
        _sTrack = _sldS.Q<VisualElement>("unity-tracker");
        _vTrack = _sldV.Q<VisualElement>("unity-tracker");

        _hDragger = _sldH.Q<VisualElement>("unity-dragger");
        _sDragger = _sldS.Q<VisualElement>("unity-dragger");
        _vDragger = _sldV.Q<VisualElement>("unity-dragger");

        // Build textures once
        _texHue = MakeGradientTex(ref _bufHue);
        _texSat = MakeGradientTex(ref _bufSat);
        _texVal = MakeGradientTex(ref _bufVal);

        // Hue is constant
        FillHueGradient(_bufHue);
        ApplyGradient(_texHue, _bufHue);
        if (_hTrack != null) _hTrack.style.backgroundImage = new StyleBackground(_texHue);

        // Sat/Val depend on current HSV, filled on open + on change
        RefreshSVGradients();
    }

    private Texture2D MakeGradientTex(ref Color32[] buf)
    {
        buf = new Color32[GradW * GradH];
        var t = new Texture2D(GradW, GradH, TextureFormat.RGBA32, mipChain: false, linear: true);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        return t;
    }

    private static void ApplyGradient(Texture2D tex, Color32[] buf)
    {
        if (tex == null || buf == null) return;
        tex.SetPixels32(buf);
        tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);
    }

    private static void FillHueGradient(Color32[] buf)
    {
        for (int x = 0; x < GradW; x++)
        {
            float h = x / (GradW - 1f);
            Color c = Color.HSVToRGB(h, 1f, 1f);
            buf[x] = (Color32)c;
        }
    }

    private void RefreshSVGradients()
    {
        if (_texSat == null || _texVal == null) return;

        float h = _sldH != null ? _sldH.value : 0f;
        float s = _sldS != null ? _sldS.value : 0f;
        float v = _sldV != null ? _sldV.value : 0f;

        // Saturation: vary S 0..1 for current H,V
        for (int x = 0; x < GradW; x++)
        {
            float ss = x / (GradW - 1f);
            _bufSat[x] = (Color32)Color.HSVToRGB(h, ss, v);
        }

        // Value: vary V 0..1 for current H,S
        for (int x = 0; x < GradW; x++)
        {
            float vv = x / (GradW - 1f);
            _bufVal[x] = (Color32)Color.HSVToRGB(h, s, vv);
        }

        ApplyGradient(_texSat, _bufSat);
        ApplyGradient(_texVal, _bufVal);

        if (_sTrack != null) _sTrack.style.backgroundImage = new StyleBackground(_texSat);
        if (_vTrack != null) _vTrack.style.backgroundImage = new StyleBackground(_texVal);
    }

    private void RefreshDraggerColors(Color c)
    {
        // Make knobs reflect the resulting colour at their current values
        if (_hDragger != null) _hDragger.style.backgroundColor = Color.HSVToRGB(_sldH.value, 1f, 1f);
        if (_sDragger != null) _sDragger.style.backgroundColor = Color.HSVToRGB(_sldH.value, _sldS.value, _sldV.value);
        if (_vDragger != null) _vDragger.style.backgroundColor = Color.HSVToRGB(_sldH.value, _sldS.value, _sldV.value);
    }

    private void OnColorSlidersChanged()
    {
        if (controller == null || _colorTarget == ColorTarget.None) return;

        float h = _sldH != null ? _sldH.value : 0f;
        float s = _sldS != null ? _sldS.value : 0f;
        float v = _sldV != null ? _sldV.value : 0f;

        Color c = Color.HSVToRGB(h, s, v);
        if (_colorPreview != null) _colorPreview.style.backgroundColor = c;

        // NEW: keep UI intuitive
        RefreshSVGradients();
        RefreshDraggerColors(c);

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

    private void BuildColourPresets()
    {
        if (_rowColorPresets == null) return;

        _rowColorPresets.Clear();

        if (colourPresets == null || colourPresets.Length == 0)
            return;

        for (int i = 0; i < colourPresets.Length; i++)
        {
            Color c = colourPresets[i];

            var sw = new VisualElement();
            sw.AddToClassList("color-preset-swatch");
            sw.style.backgroundColor = c;

            sw.AddManipulator(new Clickable(() =>
            {
                // Apply preset to current active target (only if overlay is open / target chosen)
                if (controller == null || _colorTarget == ColorTarget.None)
                    return;

                // Update sliders + preview
                Color.RGBToHSV(c, out float h, out float s, out float v);
                _sldH?.SetValueWithoutNotify(h);
                _sldS?.SetValueWithoutNotify(s);
                _sldV?.SetValueWithoutNotify(v);
                if (_colorPreview != null) _colorPreview.style.backgroundColor = c;

                // Apply to controller
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
            }));

            _rowColorPresets.Add(sw);
        }
    }

}
