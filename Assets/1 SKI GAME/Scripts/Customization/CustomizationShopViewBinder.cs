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

    // Dual list content
    private Label _lblShopListTitle;
    private Label _lblInventoryListTitle;
    private ListView _listShopOptions;
    private ListView _listInventoryOptions;

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
    private VisualElement _swatchEyeOutline;

    private VisualElement _slotSkisGear;
    private VisualElement _slotSkisPattern;
    private VisualElement _swatchSkisColor;
    //private Button _slotSkisPattern;


    private VisualElement _slotPolesGear;
    private VisualElement _slotPolesPattern;
    private VisualElement _swatchPolesColor;
    //private Button _slotPolesPattern;

    private VisualElement _slotHatGear;
    private VisualElement _slotHatPattern;
    private VisualElement _swatchHatColor;
    //private Button _slotHatPattern;

    private VisualElement _slotJacketGear;
    private VisualElement _slotJacketPattern;
    private VisualElement _swatchJacketColor;
    //private Button _slotJacketPattern;

    // Optional: make entire row clickable (covers empty space/color blocks)
    private VisualElement _rowSkis, _rowPoles, _rowHat, _rowJacket, _rowEyes;
    // Optional labels in loadout
    private Label _lblSkisGearName, _lblPolesGearName, _lblHatGearName, _lblJacketGearName;
    private Label _lblSkisPatternName, _lblPolesPatternName, _lblHatPatternName, _lblJacketPatternName;

    private Label _lblEyeName;
    private Image _imgEyeIcon;

    private VisualElement _iconSkisPattern, _iconPolesPattern, _iconHatPattern, _iconJacketPattern;

    // Loadout state badges
    private Label _lblEyeState, _lblSkisState, _lblPolesState, _lblHatState, _lblJacketState;

    private Button _btnResetSkisColor, _btnResetPolesColor, _btnResetHatColor, _btnResetJacketColor;
    private Button _btnResetSkisSecondaryColor, _btnResetPolesSecondaryColor, _btnResetHatSecondaryColor, _btnResetJacketSecondaryColor;
    private Button _btnResetSkisPattern, _btnResetPolesPattern, _btnResetHatPattern, _btnResetJacketPattern;

    private Button _btnClearEyePreview;
    private Button _btnClearSkisPreview;
    private Button _btnClearPolesPreview;
    private Button _btnClearHatPreview;
    private Button _btnClearJacketPreview;

    private VisualElement _rowEyesPurchaseActions, _rowSkisPurchaseActions, _rowPolesPurchaseActions, _rowHatPurchaseActions, _rowJacketPurchaseActions;

    private Button _btnBuyEyePreview;

    private Button _btnBuySkisPreviewItem, _btnBuySkisPreviewPattern, _btnBuySkisPreviewBoth;
    private Button _btnBuyPolesPreviewItem, _btnBuyPolesPreviewPattern, _btnBuyPolesPreviewBoth;
    private Button _btnBuyHatPreviewItem, _btnBuyHatPreviewPattern, _btnBuyHatPreviewBoth;
    private Button _btnBuyJacketPreviewItem, _btnBuyJacketPreviewPattern, _btnBuyJacketPreviewBoth;

    private SliderInt _sldEyeSize;
    private Label _lblEyeSizeValue;

    private VisualElement _rowSkisExtraColors;
    private VisualElement _rowPolesExtraColors;
    private VisualElement _rowHatExtraColors;
    private VisualElement _rowJacketExtraColors;

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
    private readonly List<CustomizationOptionSO> _shopBuffer = new();
    private readonly List<CustomizationOptionSO> _inventoryBuffer = new();

    private IReadOnlyList<CustomizationOptionSO> _shopSource;
    private IReadOnlyList<CustomizationOptionSO> _inventorySource;

    private enum ColorTarget { None, Skin, Eye, EyeOutline, Skis, Poles, Hat, Jacket, ExtraChannel }
    private ColorTarget _colorTarget = ColorTarget.None;
    private PatternTarget _extraColorPatternTarget;
    private string _extraColorChannelId;

    private void Awake()
    {
        if (document == null) document = GetComponent<UIDocument>();
        _root = document != null ? document.rootVisualElement : null;

        CacheElements();
        SetupColorGradientUI();

        SetupLists();
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
        _lblShopListTitle = _root.Q<Label>("Lbl_ShopListTitle");
        _lblInventoryListTitle = _root.Q<Label>("Lbl_InventoryListTitle");
        _listShopOptions = _root.Q<ListView>("List_ShopOptions");
        _listInventoryOptions = _root.Q<ListView>("List_InventoryOptions");

        // Category tabs
        _tabEyes = _root.Q<Button>("Tab_Eyes");
        _tabPatterns = _root.Q<Button>("Tab_Patterns");
        _tabSkis = _root.Q<Button>("Tab_Skis");
        _tabPoles = _root.Q<Button>("Tab_Poles");
        _tabHats = _root.Q<Button>("Tab_Hats");
        _tabJackets = _root.Q<Button>("Tab_Jackets");


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

        _swatchEye = _root.Q<VisualElement>("Swatch_EyeColor");
        _swatchEyeOutline = _root.Q<VisualElement>("Swatch_EyeOutlineColor");

        _rowSkis = _root.Q<VisualElement>("Row_Skis");
        _rowPoles = _root.Q<VisualElement>("Row_Poles");
        _rowHat = _root.Q<VisualElement>("Row_Hat");
        _rowJacket = _root.Q<VisualElement>("Row_Jacket");

        _slotSkisGear = _root.Q<VisualElement>("Slot_SkisGear");
        _slotSkisPattern = _root.Q<VisualElement>("Slot_SkisPattern");
        _swatchSkisColor = _root.Q<VisualElement>("Swatch_SkisColor");
        //_slotSkisPattern = _root.Q<Button>("Slot_SkisPattern");

        _slotPolesGear = _root.Q<VisualElement>("Slot_PolesGear");
        _slotPolesPattern = _root.Q<VisualElement>("Slot_PolesPattern");
        _swatchPolesColor = _root.Q<VisualElement>("Swatch_PolesColor");
        //_slotPolesPattern = _root.Q<Button>("Slot_PolesPattern");

        _slotHatGear = _root.Q<VisualElement>("Slot_HatGear");
        _slotHatPattern = _root.Q<VisualElement>("Slot_HatPattern");
        _swatchHatColor = _root.Q<VisualElement>("Swatch_HatColor");
        //_slotHatPattern = _root.Q<Button>("Slot_HatPattern");

        _slotJacketGear = _root.Q<VisualElement>("Slot_JacketGear");
        _slotJacketPattern = _root.Q<VisualElement>("Slot_JacketPattern");
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

        _iconSkisPattern = _root.Q<VisualElement>("Icon_SkisPattern");
        _iconPolesPattern = _root.Q<VisualElement>("Icon_PolesPattern");
        _iconHatPattern = _root.Q<VisualElement>("Icon_HatPattern");
        _iconJacketPattern = _root.Q<VisualElement>("Icon_JacketPattern");

        _lblEyeState = _root.Q<Label>("Lbl_EyeState");
        _lblSkisState = _root.Q<Label>("Lbl_SkisState");
        _lblPolesState = _root.Q<Label>("Lbl_PolesState");
        _lblHatState = _root.Q<Label>("Lbl_HatState");
        _lblJacketState = _root.Q<Label>("Lbl_JacketState");

        _btnResetSkisColor = _root.Q<Button>("Btn_ResetSkisColor");
        _btnResetPolesColor = _root.Q<Button>("Btn_ResetPolesColor");
        _btnResetHatColor = _root.Q<Button>("Btn_ResetHatColor");
        _btnResetJacketColor = _root.Q<Button>("Btn_ResetJacketColor");

        _btnResetSkisSecondaryColor = _root.Q<Button>("Btn_ResetSkisSecondaryColor");
        _btnResetPolesSecondaryColor = _root.Q<Button>("Btn_ResetPolesSecondaryColor");
        _btnResetHatSecondaryColor = _root.Q<Button>("Btn_ResetHatSecondaryColor");
        _btnResetJacketSecondaryColor = _root.Q<Button>("Btn_ResetJacketSecondaryColor");

        _btnResetSkisPattern = _root.Q<Button>("Btn_ResetSkisPattern");
        _btnResetPolesPattern = _root.Q<Button>("Btn_ResetPolesPattern");
        _btnResetHatPattern = _root.Q<Button>("Btn_ResetHatPattern");
        _btnResetJacketPattern = _root.Q<Button>("Btn_ResetJacketPattern");

        _btnClearEyePreview = _root.Q<Button>("Btn_ClearEyePreview");
        _btnClearSkisPreview = _root.Q<Button>("Btn_ClearSkisPreview");
        _btnClearPolesPreview = _root.Q<Button>("Btn_ClearPolesPreview");
        _btnClearHatPreview = _root.Q<Button>("Btn_ClearHatPreview");
        _btnClearJacketPreview = _root.Q<Button>("Btn_ClearJacketPreview");

        _rowEyesPurchaseActions = _root.Q<VisualElement>("Row_EyesPurchaseActions");
        _rowSkisPurchaseActions = _root.Q<VisualElement>("Row_SkisPurchaseActions");
        _rowPolesPurchaseActions = _root.Q<VisualElement>("Row_PolesPurchaseActions");
        _rowHatPurchaseActions = _root.Q<VisualElement>("Row_HatPurchaseActions");
        _rowJacketPurchaseActions = _root.Q<VisualElement>("Row_JacketPurchaseActions");

        _btnBuyEyePreview = _root.Q<Button>("Btn_BuyEyePreview");

        _btnBuySkisPreviewItem = _root.Q<Button>("Btn_BuySkisPreviewItem");
        _btnBuySkisPreviewPattern = _root.Q<Button>("Btn_BuySkisPreviewPattern");
        _btnBuySkisPreviewBoth = _root.Q<Button>("Btn_BuySkisPreviewBoth");

        _btnBuyPolesPreviewItem = _root.Q<Button>("Btn_BuyPolesPreviewItem");
        _btnBuyPolesPreviewPattern = _root.Q<Button>("Btn_BuyPolesPreviewPattern");
        _btnBuyPolesPreviewBoth = _root.Q<Button>("Btn_BuyPolesPreviewBoth");

        _btnBuyHatPreviewItem = _root.Q<Button>("Btn_BuyHatPreviewItem");
        _btnBuyHatPreviewPattern = _root.Q<Button>("Btn_BuyHatPreviewPattern");
        _btnBuyHatPreviewBoth = _root.Q<Button>("Btn_BuyHatPreviewBoth");

        _btnBuyJacketPreviewItem = _root.Q<Button>("Btn_BuyJacketPreviewItem");
        _btnBuyJacketPreviewPattern = _root.Q<Button>("Btn_BuyJacketPreviewPattern");
        _btnBuyJacketPreviewBoth = _root.Q<Button>("Btn_BuyJacketPreviewBoth");

        _sldEyeSize = _root.Q<SliderInt>("Sld_EyeSize");
        _lblEyeSizeValue = _root.Q<Label>("Lbl_EyeSizeValue");

        _rowSkisExtraColors = _root.Q<VisualElement>("Row_SkisExtraColors");
        _rowPolesExtraColors = _root.Q<VisualElement>("Row_PolesExtraColors");
        _rowHatExtraColors = _root.Q<VisualElement>("Row_HatExtraColors");
        _rowJacketExtraColors = _root.Q<VisualElement>("Row_JacketExtraColors");

        // Color popover
        _colorOverlay = _root.Q<VisualElement>("ColorOverlay");
        _colorPreview = _root.Q<VisualElement>("Swatch_ColorPreview");
        _btnCloseColor = _root.Q<Button>("Btn_CloseColor");
        _sldH = _root.Q<Slider>("Sld_H");
        _sldS = _root.Q<Slider>("Sld_S");
        _sldV = _root.Q<Slider>("Sld_V");
        _rowColorPresets = _root.Q<VisualElement>("Row_ColorPresets");

    }

    private void SetupLists()
    {
        SetupOptionList(_listShopOptions, true);
        SetupOptionList(_listInventoryOptions, false);
    }

    private void SetupOptionList(ListView listView, bool isShopList)
    {
        if (listView == null) return;

        listView.selectionType = SelectionType.Single;
        listView.fixedItemHeight = 48f;

        listView.makeItem = () =>
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
                if (controller == null) return;
                if (row.userData is not CustomizationOptionSO opt || opt == null) return;

                controller.Select(opt);
                _root?.schedule.Execute(RefreshAll);
            }));

            return row;
        };

        listView.bindItem = (ve, i) =>
        {
            var source = isShopList ? _shopBuffer : _inventoryBuffer;
            if (i < 0 || i >= source.Count) return;

            var opt = source[i];
            if (opt == null) return;

            ve.userData = opt;

            var icon = ve.Q<VisualElement>("icon");
            var name = ve.Q<Label>("name");
            var meta = ve.Q<Label>("meta");
            var price = ve.Q<Label>("price");
            var badge = ve.Q<Label>("badge");

            if (name != null)
                name.text = string.IsNullOrEmpty(opt.displayName) ? opt.name : opt.displayName;

            if (meta != null)
                meta.text = GetMetaLine(opt);

            if (icon != null)
            {
                var tex = GetOptionIconTexture2D(opt);
                if (tex != null) icon.style.backgroundImage = new StyleBackground(tex);
                else icon.style.backgroundImage = StyleKeyword.None;
            }

            bool owned = controller != null && controller.IsOwned(opt);
            bool previewed = IsOptionPreviewed(opt);
            bool equipped = IsOptionEquipped(opt);
            bool selected = IsOptionSelected(opt);

            ve.RemoveFromClassList("is-selected");
            ve.RemoveFromClassList("is-preview");
            ve.RemoveFromClassList("is-equipped");

            if (selected) ve.AddToClassList("is-selected");
            if (previewed) ve.AddToClassList("is-preview");
            else if (equipped) ve.AddToClassList("is-equipped");

            if (price != null)
                price.text = isShopList && !owned ? $"{opt.cost}" : "";

            if (badge != null)
            {
                badge.RemoveFromClassList("is-owned");
                badge.RemoveFromClassList("is-equipped");
                badge.RemoveFromClassList("is-preview");

                if (previewed)
                {
                    badge.text = "PREVIEW";
                    badge.AddToClassList("is-preview");
                }
                else if (equipped)
                {
                    badge.text = "EQUIPPED";
                    badge.AddToClassList("is-equipped");
                }
                else if (owned)
                {
                    badge.text = "OWNED";
                    badge.AddToClassList("is-owned");
                }
                else
                {
                    badge.text = "";
                }
            }
        };

        listView.onSelectionChange += _ =>
        {
            // Selection is handled by row click to avoid fighting both lists.
        };
    }
    private void HookEvents()
    {
        if (controller == null) return;

       
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

        if (_btnResetSkisSecondaryColor != null) _btnResetSkisSecondaryColor.clicked += () => { controller.ResetGearExtraColorsToDefault(PatternTarget.Skis); RefreshAll(); };
        if (_btnResetPolesSecondaryColor != null) _btnResetPolesSecondaryColor.clicked += () => { controller.ResetGearExtraColorsToDefault(PatternTarget.Poles); RefreshAll(); };
        if (_btnResetHatSecondaryColor != null) _btnResetHatSecondaryColor.clicked += () => { controller.ResetGearExtraColorsToDefault(PatternTarget.Hat); RefreshAll(); };
        if (_btnResetJacketSecondaryColor != null) _btnResetJacketSecondaryColor.clicked += () => { controller.ResetGearExtraColorsToDefault(PatternTarget.Jacket); RefreshAll(); };

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
        MakeClickable(_iconSkisPattern, () =>
        {
            controller.SetPatternTarget(CustomizationUIController.PatternTarget.Skis);
            controller.SetCategory(CustomizationOptionType.SkinPattern);
            RefreshAll();
        });

        MakeClickable(_iconPolesPattern, () =>
        {
            controller.SetPatternTarget(CustomizationUIController.PatternTarget.Poles);
            controller.SetCategory(CustomizationOptionType.SkinPattern);
            RefreshAll();
        });

        MakeClickable(_iconHatPattern, () =>
        {
            controller.SetPatternTarget(CustomizationUIController.PatternTarget.Hat);
            controller.SetCategory(CustomizationOptionType.SkinPattern);
            RefreshAll();
        });

        MakeClickable(_iconJacketPattern, () =>
        {
            controller.SetPatternTarget(CustomizationUIController.PatternTarget.Jacket);
            controller.SetCategory(CustomizationOptionType.SkinPattern);
            RefreshAll();
        });

        MakeClickable(_slotSkisPattern, () =>
        {
            controller.SetPatternTarget(CustomizationUIController.PatternTarget.Skis);
            controller.SetCategory(CustomizationOptionType.SkinPattern);
            RefreshAll();
        });

        MakeClickable(_slotPolesPattern, () =>
        {
            controller.SetPatternTarget(CustomizationUIController.PatternTarget.Poles);
            controller.SetCategory(CustomizationOptionType.SkinPattern);
            RefreshAll();
        });

        MakeClickable(_slotHatPattern, () =>
        {
            controller.SetPatternTarget(CustomizationUIController.PatternTarget.Hat);
            controller.SetCategory(CustomizationOptionType.SkinPattern);
            RefreshAll();
        });

        MakeClickable(_slotJacketPattern, () =>
        {
            controller.SetPatternTarget(CustomizationUIController.PatternTarget.Jacket);
            controller.SetCategory(CustomizationOptionType.SkinPattern);
            RefreshAll();
        });

        if (_btnBuyEyePreview != null)
            _btnBuyEyePreview.clicked += () => { controller.TryBuyPreviewedEye(); RefreshAll(); };

        if (_btnBuySkisPreviewItem != null)
            _btnBuySkisPreviewItem.clicked += () => { controller.TryBuyPreviewedGear(PatternTarget.Skis); RefreshAll(); };
        if (_btnBuySkisPreviewPattern != null)
            _btnBuySkisPreviewPattern.clicked += () => { controller.TryBuyPreviewedPattern(PatternTarget.Skis); RefreshAll(); };
        if (_btnBuySkisPreviewBoth != null)
            _btnBuySkisPreviewBoth.clicked += () => { controller.TryBuyPreviewedGearAndPattern(PatternTarget.Skis); RefreshAll(); };

        if (_btnBuyPolesPreviewItem != null)
            _btnBuyPolesPreviewItem.clicked += () => { controller.TryBuyPreviewedGear(PatternTarget.Poles); RefreshAll(); };
        if (_btnBuyPolesPreviewPattern != null)
            _btnBuyPolesPreviewPattern.clicked += () => { controller.TryBuyPreviewedPattern(PatternTarget.Poles); RefreshAll(); };
        if (_btnBuyPolesPreviewBoth != null)
            _btnBuyPolesPreviewBoth.clicked += () => { controller.TryBuyPreviewedGearAndPattern(PatternTarget.Poles); RefreshAll(); };

        if (_btnBuyHatPreviewItem != null)
            _btnBuyHatPreviewItem.clicked += () => { controller.TryBuyPreviewedGear(PatternTarget.Hat); RefreshAll(); };
        if (_btnBuyHatPreviewPattern != null)
            _btnBuyHatPreviewPattern.clicked += () => { controller.TryBuyPreviewedPattern(PatternTarget.Hat); RefreshAll(); };
        if (_btnBuyHatPreviewBoth != null)
            _btnBuyHatPreviewBoth.clicked += () => { controller.TryBuyPreviewedGearAndPattern(PatternTarget.Hat); RefreshAll(); };

        if (_btnBuyJacketPreviewItem != null)
            _btnBuyJacketPreviewItem.clicked += () => { controller.TryBuyPreviewedGear(PatternTarget.Jacket); RefreshAll(); };
        if (_btnBuyJacketPreviewPattern != null)
            _btnBuyJacketPreviewPattern.clicked += () => { controller.TryBuyPreviewedPattern(PatternTarget.Jacket); RefreshAll(); };
        if (_btnBuyJacketPreviewBoth != null)
            _btnBuyJacketPreviewBoth.clicked += () => { controller.TryBuyPreviewedGearAndPattern(PatternTarget.Jacket); RefreshAll(); };

        // Color swatches open popover
        MakeClickable(_swatchSkin, () => OpenColor(ColorTarget.Skin, controller.GetSkinColor()));
        MakeClickable(_swatchEye, () => OpenColor(ColorTarget.Eye, controller.GetEyeColor()));
        MakeClickable(_swatchEyeOutline, () => OpenColor(ColorTarget.EyeOutline, controller.GetEyeOutlineColor()));
        MakeClickable(_swatchSkisColor, () => OpenColor(ColorTarget.Skis, controller.GetSkisColor()));
        MakeClickable(_swatchPolesColor, () => OpenColor(ColorTarget.Poles, controller.GetPolesColor()));
        MakeClickable(_swatchHatColor, () => OpenColor(ColorTarget.Hat, controller.GetHatColor()));
        MakeClickable(_swatchJacketColor, () => OpenColor(ColorTarget.Jacket, controller.GetJacketColor()));

        if (_sldEyeSize != null)
        {
            _sldEyeSize.lowValue = 1;
            _sldEyeSize.highValue = 5;
            _sldEyeSize.RegisterValueChangedCallback(evt =>
            {
                if (controller == null) return;
                controller.SetEyeSize(evt.newValue);
                RefreshAll();
            });
        }

        if (_btnCloseColor != null) _btnCloseColor.clicked += CloseColor;

        if (_colorOverlay != null)
        {
            _colorOverlay.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target == _colorOverlay)
                    CloseColor();
            });
        }

        var colorPanel = _root.Q<VisualElement>("ColorPanel");
        if (colorPanel != null)
            colorPanel.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());

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
            if (_lblCurrency != null) _lblCurrency.text = $"${controller.GetCurrency():N0}";

            if (_lblShopListTitle != null)
                _lblShopListTitle.text = "Shop Offers (Buy / Preview)";

            if (_lblInventoryListTitle != null)
                _lblInventoryListTitle.text = "Inventory (Equip / Preview)";

            _shopSource = controller.GetVisibleList();
            _inventorySource = controller.GetOwnedList();

            _shopBuffer.Clear();
            if (_shopSource != null)
            {
                for (int i = 0; i < _shopSource.Count; i++)
                    _shopBuffer.Add(_shopSource[i]);
            }

            _inventoryBuffer.Clear();
            if (_inventorySource != null)
            {
                for (int i = 0; i < _inventorySource.Count; i++)
                    _inventoryBuffer.Add(_inventorySource[i]);
            }

            if (_listShopOptions != null)
            {
                _listShopOptions.itemsSource = _shopBuffer;
                _listShopOptions.Rebuild();
            }

            if (_listInventoryOptions != null)
            {
                _listInventoryOptions.itemsSource = _inventoryBuffer;
                _listInventoryOptions.Rebuild();
                AutoSelectEquippedRow();
            }

            // Details
            RefreshDetails();

            // Loadout swatches
            if (_swatchSkin != null) _swatchSkin.style.backgroundColor = controller.GetSkinColor();
            if (_swatchEye != null) _swatchEye.style.backgroundColor = controller.GetEyeColor();
            if (_swatchEyeOutline != null) _swatchEyeOutline.style.backgroundColor = controller.GetEyeOutlineColor();
            int eyeSizeStep = Mathf.RoundToInt(controller.GetEyeSize());
            if (_sldEyeSize != null && _sldEyeSize.value != eyeSizeStep)
                _sldEyeSize.SetValueWithoutNotify(eyeSizeStep);
            if (_lblEyeSizeValue != null) _lblEyeSizeValue.text = eyeSizeStep.ToString();
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
            if (_lblSelectedDesc != null) _lblSelectedDesc.text = "Preview items from the shop, then buy them once you're happy with the look.";
            if (_lblSelectedCost != null) _lblSelectedCost.text = "";
            if (_lblSelectedOwned != null) _lblSelectedOwned.text = "";

            if (_btnBuy != null) _btnBuy.AddToClassList("is-hidden");
            if (_btnEquip != null) _btnEquip.AddToClassList("is-hidden");
            if (_iconSelected != null) _iconSelected.style.backgroundImage = StyleKeyword.None;
            return;
        }

        bool owned = controller.IsOwned(sel);
        bool previewed = IsOptionPreviewed(sel);
        bool equipped = IsOptionEquipped(sel);
        bool isPattern = sel.type == CustomizationOptionType.SkinPattern;
        string targetLabel = GetPatternTargetLabel();

        if (_lblSelectedName != null)
            _lblSelectedName.text = string.IsNullOrEmpty(sel.displayName) ? sel.name : sel.displayName;

        if (_lblSelectedDesc != null)
        {
            string desc = string.IsNullOrEmpty(sel.description) ? "No description." : sel.description;

            if (isPattern)
                desc += $"\nCurrently targeting: {targetLabel}";

            _lblSelectedDesc.text = desc;
        }

        if (_lblSelectedCost != null)
            _lblSelectedCost.text = owned ? "" : $"Price: {sel.cost}";

        if (_lblSelectedOwned != null)
        {
            if (previewed)
                _lblSelectedOwned.text = isPattern ? $"Previewing on {targetLabel}" : "Previewing";
            else if (equipped)
                _lblSelectedOwned.text = isPattern ? $"Applied to {targetLabel}" : "Equipped";
            else
                _lblSelectedOwned.text = owned ? "Owned" : "Not owned";
        }

        if (_iconSelected != null)
        {
            var tex = GetOptionIconTexture2D(sel);
            if (tex != null)
                _iconSelected.style.backgroundImage = new StyleBackground(tex);
            else
                _iconSelected.style.backgroundImage = StyleKeyword.None;
        }

        if (_btnBuy != null)
        {
            if (!owned)
            {
                _btnBuy.text = isPattern ? $"Buy & Apply to {targetLabel}" : "Buy & Equip";
                _btnBuy.RemoveFromClassList("is-hidden");
            }
            else
            {
                _btnBuy.AddToClassList("is-hidden");
            }
        }

        if (_btnEquip != null)
        {
            if (owned)
            {
                _btnEquip.text = isPattern ? $"Apply to {targetLabel}" : "Equip";
                _btnEquip.RemoveFromClassList("is-hidden");
            }
            else
            {
                _btnEquip.AddToClassList("is-hidden");
            }
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

    private static bool SameOption(CustomizationOptionSO a, CustomizationOptionSO b)
    {
        if (a == null || b == null) return false;
        return string.Equals(a.id ?? string.Empty, b.id ?? string.Empty, StringComparison.Ordinal);
    }

    private string GetPatternTargetLabel()
    {
        if (controller == null) return "Pattern";
        return controller.GetPatternTarget() switch
        {
            CustomizationUIController.PatternTarget.Skis => "Skis",
            CustomizationUIController.PatternTarget.Poles => "Poles",
            CustomizationUIController.PatternTarget.Hat => "Hat",
            CustomizationUIController.PatternTarget.Jacket => "Jacket",
            _ => "Pattern"
        };
    }

    private bool IsOptionPreviewed(CustomizationOptionSO opt)
    {
        if (controller == null || opt == null) return false;

        return opt.type switch
        {
            CustomizationOptionType.EyeIcon => SameOption(opt, controller.GetPreviewEyeOption()),
            CustomizationOptionType.Skis => SameOption(opt, controller.GetPreviewSkisOption()),
            CustomizationOptionType.Poles => SameOption(opt, controller.GetPreviewPolesOption()),
            CustomizationOptionType.Hat => SameOption(opt, controller.GetPreviewHatOption()),
            CustomizationOptionType.Jacket => SameOption(opt, controller.GetPreviewJacketOption()),
            CustomizationOptionType.SkinPattern => SameOption(opt, controller.GetPreviewPatternOption(controller.GetPatternTarget())),
            _ => false
        };
    }

    private bool IsOptionEquipped(CustomizationOptionSO opt)
    {
        if (controller == null || opt == null) return false;

        return opt.type switch
        {
            CustomizationOptionType.EyeIcon => SameOption(opt, controller.GetEquippedEyeOption()),
            CustomizationOptionType.Skis => SameOption(opt, controller.GetEquippedSkisOption()),
            CustomizationOptionType.Poles => SameOption(opt, controller.GetEquippedPolesOption()),
            CustomizationOptionType.Hat => SameOption(opt, controller.GetEquippedHatOption()),
            CustomizationOptionType.Jacket => SameOption(opt, controller.GetEquippedJacketOption()),
            CustomizationOptionType.SkinPattern => SameOption(opt, controller.GetEquippedPatternOption(controller.GetPatternTarget())),
            _ => false
        };
    }

    private bool IsOptionSelected(CustomizationOptionSO opt)
    {
        if (controller == null || opt == null) return false;
        return SameOption(opt, controller.GetSelected());
    }

    private void UpdatePatternTargetHighlight()
    {
        ClearTarget(_iconSkisPattern);
        ClearTarget(_iconPolesPattern);
        ClearTarget(_iconHatPattern);
        ClearTarget(_iconJacketPattern);

        ClearTarget(_slotSkisPattern);
        ClearTarget(_slotPolesPattern);
        ClearTarget(_slotHatPattern);
        ClearTarget(_slotJacketPattern);

        ClearTarget(_rowSkis);
        ClearTarget(_rowPoles);
        ClearTarget(_rowHat);
        ClearTarget(_rowJacket);

        if (controller.GetActiveCategory() != CustomizationOptionType.SkinPattern)
            return;

        switch (controller.GetPatternTarget())
        {
            case CustomizationUIController.PatternTarget.Skis:
                SetTarget(_iconSkisPattern);
                SetTarget(_slotSkisPattern);
                SetTarget(_rowSkis);
                break;

            case CustomizationUIController.PatternTarget.Poles:
                SetTarget(_iconPolesPattern);
                SetTarget(_slotPolesPattern);
                SetTarget(_rowPoles);
                break;

            case CustomizationUIController.PatternTarget.Hat:
                SetTarget(_iconHatPattern);
                SetTarget(_slotHatPattern);
                SetTarget(_rowHat);
                break;

            case CustomizationUIController.PatternTarget.Jacket:
                SetTarget(_iconJacketPattern);
                SetTarget(_slotJacketPattern);
                SetTarget(_rowJacket);
                break;
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

    private void SetPurchaseButtonState(Button button, bool visible, string text, bool enabled)
    {
        if (button == null) return;

        if (visible)
        {
            button.text = text;
            button.RemoveFromClassList("is-hidden");
            button.style.display = DisplayStyle.Flex;
            button.SetEnabled(enabled);
        }
        else
        {
            button.AddToClassList("is-hidden");
            button.style.display = DisplayStyle.None;
        }
    }

    private void RefreshEyePurchaseActions()
    {
        var eye = controller.GetPreviewedPurchasableEyeOption();
        bool show = eye != null;

        if (_rowEyesPurchaseActions != null)
            _rowEyesPurchaseActions.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;

        if (!show)
            return;

        bool canAfford = controller.CanAffordCost(eye.cost);
        SetPurchaseButtonState(_btnBuyEyePreview, true, $"Buy Icon · {eye.cost}", canAfford);
    }

    private void RefreshGearPurchaseActions(
        PatternTarget target,
        VisualElement row,
        Button btnItem,
        Button btnPattern,
        Button btnBoth,
        string itemLabel)
    {
        var gear = controller.GetPreviewedPurchasableGearOption(target);
        var pattern = controller.GetPreviewedPurchasablePatternOption(target);

        bool showGear = gear != null;
        bool showPattern = pattern != null;
        bool showAny = showGear || showPattern;
        bool showBoth = showGear && showPattern;

        if (row != null)
            row.style.display = showAny ? DisplayStyle.Flex : DisplayStyle.None;

        if (!showAny)
            return;

        if (showGear)
            SetPurchaseButtonState(btnItem, true, $"Buy {itemLabel} · {gear.cost}", controller.CanAffordCost(gear.cost));
        else
            SetPurchaseButtonState(btnItem, false, string.Empty, false);

        if (showPattern)
            SetPurchaseButtonState(btnPattern, true, $"Buy Pattern · {pattern.cost}", controller.CanAffordCost(pattern.cost));
        else
            SetPurchaseButtonState(btnPattern, false, string.Empty, false);

        if (showBoth)
        {
            int combined = gear.cost + pattern.cost;
            SetPurchaseButtonState(btnBoth, true, $"Buy Both · {combined}", controller.CanAffordCost(combined));
        }
        else
        {
            SetPurchaseButtonState(btnBoth, false, string.Empty, false);
        }
    }

    private void RefreshLoadoutPurchaseActions()
    {
        if (controller == null) return;

        RefreshEyePurchaseActions();

        RefreshGearPurchaseActions(
            PatternTarget.Skis,
            _rowSkisPurchaseActions,
            _btnBuySkisPreviewItem,
            _btnBuySkisPreviewPattern,
            _btnBuySkisPreviewBoth,
            "Skis");

        RefreshGearPurchaseActions(
            PatternTarget.Poles,
            _rowPolesPurchaseActions,
            _btnBuyPolesPreviewItem,
            _btnBuyPolesPreviewPattern,
            _btnBuyPolesPreviewBoth,
            "Poles");

        RefreshGearPurchaseActions(
            PatternTarget.Hat,
            _rowHatPurchaseActions,
            _btnBuyHatPreviewItem,
            _btnBuyHatPreviewPattern,
            _btnBuyHatPreviewBoth,
            "Hat");

        RefreshGearPurchaseActions(
            PatternTarget.Jacket,
            _rowJacketPurchaseActions,
            _btnBuyJacketPreviewItem,
            _btnBuyJacketPreviewPattern,
            _btnBuyJacketPreviewBoth,
            "Jacket");
    }

    private void OpenExtraChannelColor(PatternTarget target, string channelId, Color current)
    {
        _extraColorPatternTarget = target;
        _extraColorChannelId = channelId;
        OpenColor(ColorTarget.ExtraChannel, current);
    }

    private void ApplyColorToCurrentTarget(Color c)
    {
        if (controller == null || _colorTarget == ColorTarget.None)
            return;

        switch (_colorTarget)
        {
            case ColorTarget.Skin: controller.SetSkinColor(c); break;
            case ColorTarget.Eye: controller.SetEyeColor(c); break;
            case ColorTarget.EyeOutline: controller.SetEyeOutlineColor(c); break;
            case ColorTarget.Skis: controller.SetSkisColor(c); break;
            case ColorTarget.Poles: controller.SetPolesColor(c); break;
            case ColorTarget.Hat: controller.SetHatColor(c); break;
            case ColorTarget.Jacket: controller.SetJacketColor(c); break;

            case ColorTarget.ExtraChannel:
                if (!string.IsNullOrEmpty(_extraColorChannelId))
                    controller.SetGearChannelColor(_extraColorPatternTarget, _extraColorChannelId, c);
                break;
        }
    }

    private void SetExtraColorAvailability(VisualElement container, Button resetButton, bool isAvailable)
    {
        if (container != null)
        {
            container.EnableInClassList("is-unavailable", !isAvailable);
            container.pickingMode = isAvailable ? PickingMode.Position : PickingMode.Ignore;
        }

        if (resetButton != null)
        {
            resetButton.SetEnabled(isAvailable);
            resetButton.EnableInClassList("is-unavailable", !isAvailable);
            resetButton.style.opacity = isAvailable ? 1f : 0.35f;
        }
    }

    private void RebuildExtraColorSwatches(PatternTarget target, VisualElement container, Button resetButton)
    {
        if (container == null)
            return;

        container.Clear();

        if (controller == null)
        {
            SetExtraColorAvailability(container, resetButton, false);
            return;
        }

        var channels = controller.GetActiveExtraColorChannels(target);
        bool hasChannels = channels != null && channels.Count > 0;

        SetExtraColorAvailability(container, resetButton, hasChannels);

        if (!hasChannels)
            return;

        for (int i = 0; i < channels.Count; i++)
        {
            var channel = channels[i];
            if (channel == null || string.IsNullOrEmpty(channel.id))
                continue;

            var swatch = new VisualElement();
            swatch.AddToClassList("gear-mini-extra-color");
            swatch.tooltip = channel.displayName;
            swatch.style.backgroundColor = controller.GetGearChannelColor(target, channel.id);

            var localTarget = target;
            var localChannelId = channel.id;

            MakeClickable(swatch, () =>
            {
                OpenExtraChannelColor(localTarget, localChannelId, controller.GetGearChannelColor(localTarget, localChannelId));
            });

            container.Add(swatch);
        }
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

        if (_swatchEye != null) _swatchEye.style.backgroundColor = controller.GetEyeColor();
        if (_swatchEyeOutline != null) _swatchEyeOutline.style.backgroundColor = controller.GetEyeOutlineColor();
        if (_lblEyeSizeValue != null) _lblEyeSizeValue.text = $"{Mathf.RoundToInt(controller.GetEyeSize() * 100f)}%";

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

        RebuildExtraColorSwatches(PatternTarget.Skis, _rowSkisExtraColors, _btnResetSkisSecondaryColor);
        RebuildExtraColorSwatches(PatternTarget.Poles, _rowPolesExtraColors, _btnResetPolesSecondaryColor);
        RebuildExtraColorSwatches(PatternTarget.Hat, _rowHatExtraColors, _btnResetHatSecondaryColor);
        RebuildExtraColorSwatches(PatternTarget.Jacket, _rowJacketExtraColors, _btnResetJacketSecondaryColor);

        RefreshLoadoutPurchaseActions();
    }

    private void AutoSelectEquippedRow()
    {
        if (controller == null || _listInventoryOptions == null) return;
        if (_inventoryBuffer == null || _inventoryBuffer.Count == 0) return;

        // Don’t override an explicit user selection
        if (controller.GetSelected() != null) return;

        string desiredId = null;
        var cat = controller.GetActiveCategory();

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
                desiredId = controller.GetEquippedHatOption()?.id;
                if (string.IsNullOrEmpty(desiredId)) desiredId = "";
                break;

            case CustomizationOptionType.Jacket:
                desiredId = controller.GetEquippedJacketOption()?.id;
                if (string.IsNullOrEmpty(desiredId)) desiredId = "";
                break;

            case CustomizationOptionType.SkinPattern:
                desiredId = controller.GetEquippedPatternOption(controller.GetPatternTarget())?.id;
                if (string.IsNullOrEmpty(desiredId)) desiredId = "";
                break;
        }

        int idx = -1;
        for (int i = 0; i < _inventoryBuffer.Count; i++)
        {
            var o = _inventoryBuffer[i];
            if (o == null) continue;

            if ((o.id ?? "") == desiredId)
            {
                idx = i;
                break;
            }
        }

        if (idx < 0) idx = 0;

        _listInventoryOptions.SetSelectionWithoutNotify(new[] { idx });
        controller.Select(_inventoryBuffer[idx]);
    }

    private void OpenColor(ColorTarget target, Color current)
    {
        _colorTarget = target;
        if (_colorOverlay != null) _colorOverlay.RemoveFromClassList("is-hidden");

        if (target != ColorTarget.ExtraChannel)
        {
            _extraColorPatternTarget = PatternTarget.Skis;
            _extraColorChannelId = null;
        }

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
        _extraColorPatternTarget = PatternTarget.Skis;
        _extraColorChannelId = null;
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

        RefreshSVGradients();
        RefreshDraggerColors(c);

        ApplyColorToCurrentTarget(c);

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
                if (controller == null || _colorTarget == ColorTarget.None)
                    return;

                Color.RGBToHSV(c, out float h, out float s, out float v);
                _sldH?.SetValueWithoutNotify(h);
                _sldS?.SetValueWithoutNotify(s);
                _sldV?.SetValueWithoutNotify(v);
                if (_colorPreview != null) _colorPreview.style.backgroundColor = c;

                ApplyColorToCurrentTarget(c);
                RefreshAll();
            }));

            _rowColorPresets.Add(sw);
        }
    }

}
