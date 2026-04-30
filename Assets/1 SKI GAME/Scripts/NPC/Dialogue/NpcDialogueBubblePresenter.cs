using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class NpcDialogueBubblePresenter : MonoBehaviour
{
    public sealed class NpcQuestOfferBubbleViewData
    {
        public string npcName;
        public string badgeText;
        public string questTitle;
        public string stateText;
        public string bodyText;
        public string indexText;
        public bool canCycle;
        public bool canConfirm;
        public string previousBindingText;
        public string nextBindingText;
        public string confirmBindingText;
        public string confirmVerb;
        public string confirmText;
        public Color? accentColor;
        public NpcDialogueBubbleStyleSO styleOverride;
    }

    private enum PresentationMode
    {
        Dialogue,
        QuestOffer
    }

    private const float FadeDuration = 0.18f;

    [Header("Anchor")]
    [SerializeField] private Transform bubbleAnchor;
    [SerializeField] private Vector3 worldOffset = new(0f, 1.8f, 0f);

    [Header("Styles")]
    [SerializeField] private NpcDialogueBubbleStyleSO ambientStyle;
    [SerializeField] private NpcDialogueBubbleStyleSO interactionStyle;
    [SerializeField] private NpcDialogueBubbleStyleSO questStyle;
    [SerializeField] private NpcDialogueBubbleStyleSO tutorialStyle;
    [SerializeField] private NpcDialogueBubbleStyleSO criticalStyle;

    private Canvas _canvas;
    private CanvasGroup _canvasGroup;
    private RectTransform _rootRect;
    private Image _backgroundImage;
    private Outline _backgroundOutline;
    private Image _accentBar;

    private GameObject _dialogueRoot;
    private TextMeshProUGUI _speakerLabel;
    private TextMeshProUGUI _bodyLabel;

    private GameObject _questRoot;
    private RectTransform _questHeaderRow;
    private RectTransform _questSelectorRow;
    private RectTransform _questActionRow;
    private TextMeshProUGUI _questNpcNameLabel;
    private TextMeshProUGUI _questBadgeLabel;
    private TextMeshProUGUI _questStateLabel;
    private TextMeshProUGUI _questTitleLabel;
    private TextMeshProUGUI _questBodyLabel;
    private TextMeshProUGUI _questIndexLabel;
    private TextMeshProUGUI _questPreviousLabel;
    private TextMeshProUGUI _questNextLabel;
    private TextMeshProUGUI _questConfirmChipLabel;
    private TextMeshProUGUI _questConfirmTextLabel;
    private GameObject _questPreviousCard;
    private GameObject _questNextCard;
    private GameObject _questIndexChip;
    private GameObject _questConfirmChip;
    private Image _questTitleCardImage;
    private Image _questPreviousCardImage;
    private Image _questNextCardImage;
    private Image _questConfirmChipImage;
    private Image _questBadgeChipImage;
    private Image _questStateChipImage;

    private Camera _activeCamera;
    private bool _visible;
    private bool _fadingOut;
    private float _visibleUntil;
    private int _currentPriority = int.MinValue;
    private bool _currentAmbient;
    private PresentationMode _mode;
    private NpcDialogueBubbleStyleSO _activeStyle;

    public bool IsShowing => _visible;
    public bool IsShowingQuestOffer => _visible && _mode == PresentationMode.QuestOffer;
    public int CurrentPriority => _currentPriority;
    public bool IsAmbientVisible => _visible && _mode == PresentationMode.Dialogue && _currentAmbient;

    public void SetAnchor(Transform anchor)
    {
        bubbleAnchor = anchor;
    }

    public bool TryPresent(string speakerName, string resolvedText, NpcDialogueLine line, bool forceInterrupt)
    {
        if (line == null || string.IsNullOrWhiteSpace(resolvedText))
            return false;

        if (IsShowingQuestOffer && !forceInterrupt)
            return false;

        if (_visible && !forceInterrupt && line.priority < _currentPriority)
            return false;

        EnsureVisuals();
        ShowDialogueVisuals();

        _speakerLabel.richText = true;
        _bodyLabel.richText = true;

        bool showSpeaker = line.showSpeakerName && !string.IsNullOrWhiteSpace(speakerName);
        _speakerLabel.gameObject.SetActive(showSpeaker);
        _speakerLabel.text = showSpeaker ? speakerName : string.Empty;
        _bodyLabel.text = resolvedText.Trim();

        _activeStyle = ResolveStyle(line);
        ApplyDialogueStyle(_activeStyle, line);
        AutoSizeDialogue(_activeStyle, showSpeaker);

        _currentPriority = line.priority;
        _currentAmbient = line.IsAmbientLike && line.importance == NpcDialogueImportance.Ambient && !line.preferImportantStyle;
        _mode = PresentationMode.Dialogue;
        _visible = true;
        _fadingOut = false;
        _visibleUntil = Time.unscaledTime + Mathf.Max(0.25f, line.duration);
        _canvasGroup.alpha = 1f;
        _canvas.enabled = true;
        UpdateVisualTransform(true);
        return true;
    }

    public void ShowQuestOffer(NpcQuestOfferBubbleViewData data)
    {
        if (data == null)
            return;

        EnsureVisuals();
        ShowQuestVisuals();

        _activeStyle = data.styleOverride != null ? data.styleOverride : questStyle != null ? questStyle : interactionStyle != null ? interactionStyle : ambientStyle;

        _questNpcNameLabel.richText = true;
        _questBadgeLabel.richText = true;
        _questStateLabel.richText = true;
        _questTitleLabel.richText = true;
        _questBodyLabel.richText = true;
        _questIndexLabel.richText = true;
        _questPreviousLabel.richText = true;
        _questNextLabel.richText = true;
        _questConfirmChipLabel.richText = true;
        _questConfirmTextLabel.richText = true;

        _questNpcNameLabel.text = data.npcName ?? string.Empty;
        _questBadgeLabel.text = data.badgeText ?? string.Empty;
        _questStateLabel.text = data.stateText ?? string.Empty;
        _questTitleLabel.text = data.questTitle ?? string.Empty;
        _questBodyLabel.text = data.bodyText ?? string.Empty;
        _questIndexLabel.text = data.indexText ?? string.Empty;
        _questPreviousLabel.text = data.previousBindingText ?? string.Empty;
        _questNextLabel.text = data.nextBindingText ?? string.Empty;
        _questConfirmChipLabel.text = data.confirmBindingText ?? string.Empty;
        _questConfirmTextLabel.text = !string.IsNullOrWhiteSpace(data.confirmText) ? data.confirmText : data.confirmVerb ?? string.Empty;

        _questBadgeChipImage.gameObject.SetActive(!string.IsNullOrWhiteSpace(data.badgeText));
        _questStateChipImage.gameObject.SetActive(!string.IsNullOrWhiteSpace(data.stateText));
        _questIndexChip.SetActive(!string.IsNullOrWhiteSpace(data.indexText));
        _questPreviousCard.SetActive(data.canCycle);
        _questNextCard.SetActive(data.canCycle);
        _questConfirmChip.SetActive(data.canConfirm && !string.IsNullOrWhiteSpace(data.confirmBindingText));
        _questConfirmTextLabel.gameObject.SetActive(data.canConfirm && !string.IsNullOrWhiteSpace(_questConfirmTextLabel.text));

        ApplyQuestOfferStyle(_activeStyle, data);
        LayoutQuestOfferCard(_activeStyle, data);

        _currentPriority = 1000;
        _currentAmbient = false;
        _mode = PresentationMode.QuestOffer;
        _visible = true;
        _fadingOut = false;
        _visibleUntil = float.PositiveInfinity;
        _canvasGroup.alpha = 1f;
        _canvas.enabled = true;
        UpdateVisualTransform(true);
    }

    public void UpdateQuestOffer(NpcQuestOfferBubbleViewData data)
    {
        ShowQuestOffer(data);
    }

    public void HideQuestOffer(bool immediate = false)
    {
        if (!IsShowingQuestOffer)
            return;

        if (immediate)
        {
            HideImmediate();
            return;
        }

        _visibleUntil = Time.unscaledTime;
        _fadingOut = true;
    }

    public void HideImmediate()
    {
        if (_canvas != null)
            _canvas.enabled = false;
        if (_canvasGroup != null)
            _canvasGroup.alpha = 0f;

        _visible = false;
        _fadingOut = false;
        _currentPriority = int.MinValue;
        _currentAmbient = false;
    }

    private void LateUpdate()
    {
        if (!_visible)
            return;

        if (Time.unscaledTime >= _visibleUntil)
            _fadingOut = true;

        if (_fadingOut)
        {
            _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, 0f, Time.unscaledDeltaTime / FadeDuration);
            if (_canvasGroup.alpha <= 0.001f)
            {
                HideImmediate();
                return;
            }
        }

        UpdateVisualTransform(false);
    }

    private void EnsureVisuals()
    {
        if (_canvas != null)
            return;

        var root = new GameObject("DialogueBubble", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        root.transform.SetParent(transform, false);
        _rootRect = root.GetComponent<RectTransform>();
        _canvas = root.GetComponent<Canvas>();
        _canvasGroup = root.GetComponent<CanvasGroup>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.enabled = false;
        _canvasGroup.alpha = 0f;

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(Outline));
        panel.transform.SetParent(root.transform, false);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        _backgroundImage = panel.GetComponent<Image>();
        _backgroundOutline = panel.GetComponent<Outline>();
        _backgroundOutline.effectDistance = new Vector2(1f, -1f);

        _accentBar = CreateImage("AccentBar", panel.transform);
        Stretch(_accentBar.rectTransform, 0f, 0f, 0f, 0f);
        _accentBar.rectTransform.anchorMin = new Vector2(0f, 1f);
        _accentBar.rectTransform.anchorMax = new Vector2(1f, 1f);
        _accentBar.rectTransform.pivot = new Vector2(0.5f, 1f);
        _accentBar.rectTransform.sizeDelta = new Vector2(0f, 4f);

        BuildDialogueVisuals(panel.transform);
        BuildQuestVisuals(panel.transform);
        ShowDialogueVisuals();
    }

    private void BuildDialogueVisuals(Transform parent)
    {
        _dialogueRoot = CreateContainer("DialogueRoot", parent).gameObject;
        _speakerLabel = CreateLabel("Speaker", _dialogueRoot.transform, 32f, TextAlignmentOptions.Center, FontStyles.Bold);
        _speakerLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        _speakerLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        _speakerLabel.rectTransform.pivot = new Vector2(0.5f, 1f);

        _bodyLabel = CreateLabel("Body", _dialogueRoot.transform, 18f, TextAlignmentOptions.Center, FontStyles.Normal);
        _bodyLabel.enableWordWrapping = true;
    }

    private void BuildQuestVisuals(Transform parent)
    {
        _questRoot = CreateContainer("QuestOfferRoot", parent).gameObject;
        _questHeaderRow = CreateContainer("HeaderRow", _questRoot.transform);
        _questSelectorRow = CreateContainer("SelectorRow", _questRoot.transform);
        _questActionRow = CreateContainer("ActionRow", _questRoot.transform);

        _questNpcNameLabel = CreateLabel("NpcName", _questHeaderRow, 16f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        _questBadgeChipImage = CreateCard("BadgeChip", _questHeaderRow);
        _questBadgeLabel = CreateLabel("BadgeLabel", _questBadgeChipImage.transform, 13f, TextAlignmentOptions.Center, FontStyles.Bold);
        _questStateChipImage = CreateCard("StateChip", _questHeaderRow);
        _questStateLabel = CreateLabel("StateLabel", _questStateChipImage.transform, 12f, TextAlignmentOptions.Center, FontStyles.Bold);

        _questPreviousCard = CreateCard("PreviousCard", _questSelectorRow).gameObject;
        _questPreviousCardImage = _questPreviousCard.GetComponent<Image>();
        _questPreviousLabel = CreateLabel("PreviousLabel", _questPreviousCard.transform, 14f, TextAlignmentOptions.Center, FontStyles.Bold);

        _questTitleCardImage = CreateCard("TitleCard", _questSelectorRow);
        _questTitleLabel = CreateLabel("QuestTitle", _questTitleCardImage.transform, 24f, TextAlignmentOptions.Center, FontStyles.Bold);
        _questTitleLabel.enableWordWrapping = true;

        _questNextCard = CreateCard("NextCard", _questSelectorRow).gameObject;
        _questNextCardImage = _questNextCard.GetComponent<Image>();
        _questNextLabel = CreateLabel("NextLabel", _questNextCard.transform, 14f, TextAlignmentOptions.Center, FontStyles.Bold);

        _questIndexChip = CreateCard("IndexChip", _questRoot.transform).gameObject;
        _questIndexLabel = CreateLabel("IndexLabel", _questIndexChip.transform, 12f, TextAlignmentOptions.Center, FontStyles.Bold);

        _questBodyLabel = CreateLabel("QuestBody", _questRoot.transform, 16f, TextAlignmentOptions.TopLeft, FontStyles.Normal);
        _questBodyLabel.enableWordWrapping = true;

        _questConfirmChip = CreateCard("ConfirmChip", _questActionRow).gameObject;
        _questConfirmChipImage = _questConfirmChip.GetComponent<Image>();
        _questConfirmChipLabel = CreateLabel("ConfirmBinding", _questConfirmChip.transform, 13f, TextAlignmentOptions.Center, FontStyles.Bold);
        _questConfirmTextLabel = CreateLabel("ConfirmText", _questActionRow, 14f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
    }

    private void ShowDialogueVisuals()
    {
        _dialogueRoot.SetActive(true);
        _questRoot.SetActive(false);
    }

    private void ShowQuestVisuals()
    {
        _dialogueRoot.SetActive(false);
        _questRoot.SetActive(true);
    }

    private void ApplyDialogueStyle(NpcDialogueBubbleStyleSO style, NpcDialogueLine line)
    {
        style ??= ambientStyle;
        ApplySharedStyle(style, false, null);

        _speakerLabel.font = style != null && style.speakerFont != null ? style.speakerFont : TMP_Settings.defaultFontAsset;
        _bodyLabel.font = style != null && style.bodyFont != null ? style.bodyFont : TMP_Settings.defaultFontAsset;
        _speakerLabel.fontSize = style != null ? style.speakerFontSize : 32f;
        _bodyLabel.fontSize = line.importance == NpcDialogueImportance.Ambient && !line.preferImportantStyle
            ? style != null ? style.bodyFontSize : 18f
            : style != null ? style.importantBodyFontSize : 20f;
        _speakerLabel.color = style != null ? style.speakerColor : Color.white;
        _bodyLabel.color = style != null ? style.textColor : Color.white;
    }

    private void ApplyQuestOfferStyle(NpcDialogueBubbleStyleSO style, NpcQuestOfferBubbleViewData data)
    {
        ApplySharedStyle(style, true, data.accentColor);

        TMP_FontAsset speakerFont = style != null && style.speakerFont != null ? style.speakerFont : TMP_Settings.defaultFontAsset;
        TMP_FontAsset bodyFont = style != null && style.bodyFont != null ? style.bodyFont : TMP_Settings.defaultFontAsset;
        TMP_FontAsset titleFont = style != null && style.titleFont != null ? style.titleFont : bodyFont;
        TMP_FontAsset chipFont = style != null && style.chipFont != null ? style.chipFont : bodyFont;

        _questNpcNameLabel.font = speakerFont;
        _questBadgeLabel.font = chipFont;
        _questStateLabel.font = chipFont;
        _questTitleLabel.font = titleFont;
        _questBodyLabel.font = bodyFont;
        _questIndexLabel.font = chipFont;
        _questPreviousLabel.font = chipFont;
        _questNextLabel.font = chipFont;
        _questConfirmChipLabel.font = chipFont;
        _questConfirmTextLabel.font = bodyFont;

        _questNpcNameLabel.fontSize = style != null ? style.speakerFontSize * 0.56f : 16f;
        _questBadgeLabel.fontSize = style != null ? style.chipFontSize : 13f;
        _questStateLabel.fontSize = style != null ? style.chipFontSize : 12f;
        _questTitleLabel.fontSize = style != null ? style.titleFontSize : 24f;
        _questBodyLabel.fontSize = style != null ? style.questBodyFontSize : 16f;
        _questIndexLabel.fontSize = style != null ? style.chipFontSize : 12f;
        _questPreviousLabel.fontSize = style != null ? style.chipFontSize : 14f;
        _questNextLabel.fontSize = style != null ? style.chipFontSize : 14f;
        _questConfirmChipLabel.fontSize = style != null ? style.chipFontSize : 13f;
        _questConfirmTextLabel.fontSize = style != null ? style.bodyFontSize : 14f;

        Color chipText = style != null ? style.chipTextColor : Color.white;
        _questNpcNameLabel.color = style != null ? style.speakerColor : Color.white;
        _questBadgeLabel.color = chipText;
        _questStateLabel.color = chipText;
        _questTitleLabel.color = style != null ? style.titleColor : Color.white;
        _questBodyLabel.color = style != null ? style.textColor : Color.white;
        _questIndexLabel.color = chipText;
        _questPreviousLabel.color = chipText;
        _questNextLabel.color = chipText;
        _questConfirmChipLabel.color = chipText;
        _questConfirmTextLabel.color = style != null ? style.textColor : Color.white;

        Color innerColor = style != null ? style.innerCardBackgroundColor : new Color(0.11f, 0.17f, 0.29f, 0.94f);
        Color chipColor = style != null ? style.inputChipBackgroundColor : new Color(0.19f, 0.29f, 0.42f, 0.98f);
        StyleCard(_questTitleCardImage, style != null ? style.innerCardSprite : null, innerColor, style != null && style.useSlicedBackground);
        StyleCard(_questPreviousCardImage, style != null ? style.inputChipSprite : null, chipColor, style != null && style.useSlicedBackground);
        StyleCard(_questNextCardImage, style != null ? style.inputChipSprite : null, chipColor, style != null && style.useSlicedBackground);
        StyleCard(_questConfirmChipImage, style != null ? style.inputChipSprite : null, chipColor, style != null && style.useSlicedBackground);
        StyleCard(_questBadgeChipImage, style != null ? style.inputChipSprite : null, chipColor, style != null && style.useSlicedBackground);
        StyleCard(_questStateChipImage, style != null ? style.inputChipSprite : null, chipColor, style != null && style.useSlicedBackground);
        if (_questIndexChip.TryGetComponent<Image>(out var indexImage))
            StyleCard(indexImage, style != null ? style.inputChipSprite : null, chipColor, style != null && style.useSlicedBackground);
    }

    private void ApplySharedStyle(NpcDialogueBubbleStyleSO style, bool questOffer, Color? accentOverride)
    {
        StyleCard(_backgroundImage, style != null ? style.backgroundSprite : null, style != null ? style.backgroundColor : new Color(0.08f, 0.12f, 0.16f, 0.82f), style != null && style.useSlicedBackground);
        _backgroundImage.material = style != null ? style.backgroundMaterial : null;
        _backgroundOutline.effectColor = style != null ? style.borderColor : new Color(1f, 1f, 1f, 0.08f);
        _accentBar.color = accentOverride ?? (style != null ? style.accentColor : new Color(0.45f, 0.7f, 0.95f, 1f));
        _accentBar.gameObject.SetActive(questOffer);
    }

    private void AutoSizeDialogue(NpcDialogueBubbleStyleSO style, bool showSpeaker)
    {
        style ??= ambientStyle;
        float maxWidth = style != null && style.autoSizeDialoguePanel ? style.dialogueMaxSize.x - (style.dialogueHorizontalPadding * 2f) : 280f;
        float speakerHeight = 0f;
        if (showSpeaker)
        {
            _speakerLabel.ForceMeshUpdate();
            speakerHeight = _speakerLabel.GetPreferredValues(_speakerLabel.text, maxWidth, 0f).y + 6f;
        }

        _bodyLabel.ForceMeshUpdate();
        Vector2 bodyPreferred = _bodyLabel.GetPreferredValues(_bodyLabel.text, maxWidth, 0f);
        float width = style != null && style.autoSizeDialoguePanel ? Mathf.Clamp(Mathf.Max(bodyPreferred.x, showSpeaker ? _speakerLabel.preferredWidth : 0f) + (style.dialogueHorizontalPadding * 2f), style.dialogueMinSize.x, style.dialogueMaxSize.x) : style != null ? style.panelSize.x : 280f;
        float height = style != null && style.autoSizeDialoguePanel ? Mathf.Clamp(bodyPreferred.y + speakerHeight + (style.dialogueVerticalPadding * 2f) + 6f, style.dialogueMinSize.y, style.dialogueMaxSize.y) : style != null ? style.panelSize.y : 120f;
        _rootRect.sizeDelta = new Vector2(width, height);

        float top = style != null ? style.dialogueVerticalPadding : 12f;
        float horizontal = style != null ? style.dialogueHorizontalPadding : 16f;
        if (showSpeaker)
        {
            _speakerLabel.rectTransform.anchoredPosition = new Vector2(0f, -top);
            _speakerLabel.rectTransform.sizeDelta = new Vector2(0f, speakerHeight);
            _bodyLabel.rectTransform.offsetMin = new Vector2(horizontal, style != null ? style.dialogueVerticalPadding : 12f);
            _bodyLabel.rectTransform.offsetMax = new Vector2(-horizontal, -(top + speakerHeight));
        }
        else
        {
            _bodyLabel.rectTransform.offsetMin = new Vector2(horizontal, style != null ? style.dialogueVerticalPadding : 12f);
            _bodyLabel.rectTransform.offsetMax = new Vector2(-horizontal, -(style != null ? style.dialogueVerticalPadding : 12f));
        }
    }

    private void LayoutQuestOfferCard(NpcDialogueBubbleStyleSO style, NpcQuestOfferBubbleViewData data)
    {
        style ??= questStyle;
        float padX = style != null ? style.questHorizontalPadding : 16f;
        float padY = style != null ? style.questVerticalPadding : 14f;
        float gap = style != null ? style.cardGap : 8f;
        float headerHeight = style != null ? style.headerHeight : 26f;
        float selectorHeight = style != null ? style.selectorRowHeight : 52f;
        float actionHeight = style != null ? style.actionRowHeight : 28f;
        float maxWidth = style != null ? style.questMaxSize.x : 460f;
        float minWidth = style != null ? style.questMinSize.x : 340f;
        float maxBodyWidth = maxWidth - (padX * 2f);

        float selectorChipWidth = data.canCycle ? 74f : 0f;
        float selectorGapWidth = data.canCycle ? gap * 2f : 0f;
        float titleWidth = data.canCycle ? maxBodyWidth - selectorChipWidth * 2f - selectorGapWidth : maxBodyWidth;

        _questTitleLabel.ForceMeshUpdate();
        _questBodyLabel.ForceMeshUpdate();
        Vector2 titlePreferred = _questTitleLabel.GetPreferredValues(_questTitleLabel.text, titleWidth - 24f, 0f);
        Vector2 bodyPreferred = _questBodyLabel.GetPreferredValues(_questBodyLabel.text, maxBodyWidth, 0f);
        float titlePreferredHeight = Mathf.Max(selectorHeight, titlePreferred.y + 20f);
        float bodyPreferredHeight = bodyPreferred.y;
        float indexHeight = _questIndexChip.activeSelf ? 22f : 0f;
        float bodyHeight = Mathf.Max(28f, bodyPreferredHeight);

        float preferredWidth = Mathf.Max(
            minWidth,
            Mathf.Max(160f, titlePreferred.x + 24f) + padX * 2f + selectorChipWidth * 2f + selectorGapWidth,
            Mathf.Max(160f, bodyPreferred.x) + padX * 2f);

        float contentHeight = headerHeight + gap + titlePreferredHeight + (indexHeight > 0f ? gap + indexHeight : 0f) + gap + bodyHeight + gap + actionHeight;
        float finalHeight = style != null && style.autoSizeQuestPanel
            ? Mathf.Clamp(contentHeight + padY * 2f + 4f, style.questMinSize.y, style.questMaxSize.y)
            : style != null ? style.questPanelSize.y : 220f;
        float finalWidth = style != null && style.autoSizeQuestPanel
            ? Mathf.Clamp(preferredWidth, minWidth, maxWidth)
            : style != null ? style.questPanelSize.x : 390f;
        _rootRect.sizeDelta = new Vector2(finalWidth, finalHeight);

        float y = padY + 4f;

        Stretch(_questHeaderRow, padX, y, -padX, -(finalHeight - y - headerHeight));
        LayoutHeaderRow(headerHeight, finalWidth, padX, data);
        y += headerHeight + gap;

        Stretch(_questSelectorRow, padX, y, -padX, -(finalHeight - y - titlePreferredHeight));
        LayoutSelectorRow(titlePreferredHeight, finalWidth, padX, gap, data);
        y += titlePreferredHeight;

        if (_questIndexChip.activeSelf)
        {
            y += gap;
            Stretch(_questIndexChip.GetComponent<RectTransform>(), (finalWidth * 0.5f) - 32f, y, -((finalWidth * 0.5f) - 32f), -(finalHeight - y - indexHeight));
            Stretch(_questIndexLabel.rectTransform, 8f, 4f, -8f, -4f);
            y += indexHeight;
        }

        y += gap;
        Stretch(_questBodyLabel.rectTransform, padX, y, -padX, -(finalHeight - y - bodyHeight));
        y += bodyHeight + gap;

        Stretch(_questActionRow, padX, y, -padX, -(finalHeight - y - actionHeight));
        LayoutActionRow(actionHeight, finalWidth, padX, gap);
    }

    private void LayoutHeaderRow(float height, float totalWidth, float padX, NpcQuestOfferBubbleViewData data)
    {
        float availableWidth = totalWidth - padX * 2f;
        float rightWidth = 0f;
        if (_questStateChipImage.gameObject.activeSelf)
            rightWidth += 88f;
        if (_questBadgeChipImage.gameObject.activeSelf)
            rightWidth += (_questStateChipImage.gameObject.activeSelf ? 6f : 0f) + 92f;

        Stretch(_questNpcNameLabel.rectTransform, 0f, 0f, -(rightWidth + 4f), 0f);
        if (_questBadgeChipImage.gameObject.activeSelf)
        {
            float badgeRight = _questStateChipImage.gameObject.activeSelf ? 94f : 0f;
            Stretch(_questBadgeChipImage.rectTransform, availableWidth - rightWidth, 0f, -badgeRight, 0f);
            Stretch(_questBadgeLabel.rectTransform, 8f, 4f, -8f, -4f);
        }

        if (_questStateChipImage.gameObject.activeSelf)
        {
            Stretch(_questStateChipImage.rectTransform, availableWidth - 88f, 0f, 0f, 0f);
            Stretch(_questStateLabel.rectTransform, 8f, 4f, -8f, -4f);
        }
    }

    private void LayoutSelectorRow(float height, float totalWidth, float padX, float gap, NpcQuestOfferBubbleViewData data)
    {
        float availableWidth = totalWidth - padX * 2f;
        float chipWidth = data.canCycle ? 74f : 0f;
        if (data.canCycle)
        {
            Stretch(_questPreviousCard.GetComponent<RectTransform>(), 0f, 0f, -(availableWidth - chipWidth), 0f);
            Stretch(_questPreviousLabel.rectTransform, 8f, 6f, -8f, -6f);
            Stretch(_questNextCard.GetComponent<RectTransform>(), availableWidth - chipWidth, 0f, 0f, 0f);
            Stretch(_questNextLabel.rectTransform, 8f, 6f, -8f, -6f);
            Stretch(_questTitleCardImage.rectTransform, chipWidth + gap, 0f, -(chipWidth + gap), 0f);
        }
        else
        {
            Stretch(_questTitleCardImage.rectTransform, 0f, 0f, 0f, 0f);
        }

        Stretch(_questTitleLabel.rectTransform, 12f, 10f, -12f, -10f);
    }

    private void LayoutActionRow(float height, float totalWidth, float padX, float gap)
    {
        float availableWidth = totalWidth - padX * 2f;
        if (_questConfirmChip.activeSelf)
        {
            Stretch(_questConfirmChip.GetComponent<RectTransform>(), 0f, 0f, -(availableWidth - 112f), 0f);
            Stretch(_questConfirmChipLabel.rectTransform, 8f, 4f, -8f, -4f);
            Stretch(_questConfirmTextLabel.rectTransform, 126f, 0f, 0f, 0f);
        }
        else
        {
            Stretch(_questConfirmTextLabel.rectTransform, 0f, 0f, 0f, 0f);
        }
    }

    private void UpdateVisualTransform(bool forceInstant)
    {
        Transform anchor = bubbleAnchor != null ? bubbleAnchor : transform;
        _activeCamera = ResolveCamera();
        if (_activeCamera == null)
            return;

        Vector3 worldPosition = anchor.position + worldOffset;
        _rootRect.position = worldPosition;

        Vector3 toCamera = _activeCamera.transform.position - worldPosition;
        if (toCamera.sqrMagnitude > 0.0001f)
            _rootRect.rotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);

        float distance = toCamera.magnitude;
        var style = _activeStyle != null ? _activeStyle : ambientStyle;
        float minWorldScale = style != null ? style.minWorldScale : 0.0075f;
        float maxWorldScale = style != null ? style.maxWorldScale : 0.011f;
        float maxVisibleDistance = style != null ? style.maxVisibleDistance : 35f;
        float importantReadableDistance = style != null ? style.importantReadableDistance : maxVisibleDistance;
        float questReadableDistance = style != null ? style.questReadableDistance : Mathf.Max(maxVisibleDistance, importantReadableDistance);
        bool constantScale = style != null && style.scaleMode == NpcDialogueScaleMode.ConstantScreenSize && (_mode != PresentationMode.Dialogue || !_currentAmbient);
        float scale = constantScale ? maxWorldScale : Mathf.Lerp(minWorldScale, maxWorldScale, Mathf.InverseLerp(maxVisibleDistance, 2f, distance));
        _rootRect.localScale = Vector3.one * scale;

        float visibilityDistance = _mode == PresentationMode.QuestOffer ? questReadableDistance : _currentAmbient ? maxVisibleDistance : Mathf.Max(maxVisibleDistance, importantReadableDistance);
        if (!_fadingOut)
        {
            float targetAlpha = distance <= visibilityDistance ? 1f : 0f;
            _canvasGroup.alpha = forceInstant ? targetAlpha : Mathf.MoveTowards(_canvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime / FadeDuration);
        }
    }

    private NpcDialogueBubbleStyleSO ResolveStyle(NpcDialogueLine line)
    {
        if (line.styleOverride != null)
            return line.styleOverride;

        return line.importance switch
        {
            NpcDialogueImportance.Interaction => interactionStyle != null ? interactionStyle : ambientStyle,
            NpcDialogueImportance.Quest => questStyle != null ? questStyle : interactionStyle != null ? interactionStyle : ambientStyle,
            NpcDialogueImportance.Tutorial => tutorialStyle != null ? tutorialStyle : questStyle != null ? questStyle : ambientStyle,
            NpcDialogueImportance.Critical => criticalStyle != null ? criticalStyle : tutorialStyle != null ? tutorialStyle : ambientStyle,
            _ => ambientStyle
        };
    }

    private static void StyleCard(Image image, Sprite sprite, Color color, bool sliced)
    {
        if (image == null)
            return;

        image.color = color;
        image.sprite = sprite;
        image.type = sliced && sprite != null ? Image.Type.Sliced : Image.Type.Simple;
    }

    private static RectTransform CreateContainer(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static Image CreateCard(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.type = Image.Type.Simple;
        return image;
    }

    private static Image CreateImage(string name, Transform parent)
    {
        return CreateCard(name, parent);
    }

    private static TextMeshProUGUI CreateLabel(string name, Transform parent, float fontSize, TextAlignmentOptions alignment, FontStyles fontStyle)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var label = go.GetComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.fontStyle = fontStyle;
        label.text = string.Empty;
        label.raycastTarget = false;
        label.richText = true;
        return label;
    }

    private static Camera ResolveCamera()
    {
        if (Camera.main != null)
            return Camera.main;
        return Camera.current;
    }

    private static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(right, -top);
    }
}
