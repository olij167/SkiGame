using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum NpcDialogueBubbleLifetimeMode
{
    Timed,
    PersistentUntilHidden
}

public enum NpcDialogueBubbleContentKind
{
    Simple,
    Card
}

public sealed class NpcDialogueBubbleContent
{
    public NpcDialogueBubbleContentKind kind;
    public NpcDialogueBubbleLifetimeMode lifetimeMode;
    public string speakerName;
    public string titleText;
    public string bodyText;
    public string metaText;
    public string controlsText;
    public string errorText;
    public float errorUntil;
    public bool showSpeaker;
    public NpcDialogueImportance importance;
    public NpcDialogueBubbleStyleSO styleOverride;
    public DialogueContext context;
    public InputActionAsset inputActions;
    public int priority;
    public float duration;
}

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
        public string errorText;
        public float errorUntil;
        public NpcDialogueBubbleStyleSO styleOverride;
    }

    private const float FadeDuration = 0.18f;
    private const string RootName = "DialogueBubbleRoot";
    private const string PanelName = "Panel";
    private const string AccentBarName = "AccentBar";
    private const string SpeakerLabelName = "SpeakerLabel";
    private const string TitleLabelName = "TitleLabel";
    private const string BodyLabelName = "BodyLabel";
    private const string MetaLabelName = "MetaLabel";
    private const string ControlsLabelName = "ControlsLabel";
    private const string ErrorLabelName = "ErrorLabel";

    [Header("Anchor")]
    [SerializeField] private Transform bubbleAnchor;
    [SerializeField] private Vector3 worldOffset = new(0f, 1.8f, 0f);

    [Header("Styles")]
    [SerializeField] private NpcDialogueBubbleStyleSO ambientStyle;
    [SerializeField] private NpcDialogueBubbleStyleSO interactionStyle;
    [SerializeField] private NpcDialogueBubbleStyleSO questStyle;
    [SerializeField] private NpcDialogueBubbleStyleSO tutorialStyle;
    [SerializeField] private NpcDialogueBubbleStyleSO criticalStyle;

    [Header("Template")]
    [SerializeField] private GameObject bubblePrefab;
    [SerializeField] private bool instantiatePrefabOnAwake = true;
    [SerializeField] private bool generateFallbackIfMissing = true;

    [Header("Template References")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform rootRect;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Outline backgroundOutline;
    [SerializeField] private Image accentBar;
    [SerializeField] private TextMeshProUGUI speakerLabel;
    [SerializeField] private TextMeshProUGUI titleLabel;
    [SerializeField] private TextMeshProUGUI bodyLabel;
    [SerializeField] private TextMeshProUGUI metaLabel;
    [SerializeField] private TextMeshProUGUI controlsLabel;
    [SerializeField] private TextMeshProUGUI errorLabel;

    private Camera _activeCamera;
    private bool _visible;
    private bool _fadingOut;
    private bool _currentAmbient;
    private float _visibleUntil;
    private int _currentPriority = int.MinValue;
    private NpcDialogueBubbleStyleSO _activeStyle;
    private NpcDialogueBubbleContent _currentContent;
    private GameObject _generatedRoot;
    private bool _warnedMissingTemplate;
    private Vector3 _smoothedWorldPosition;
    private bool _hasSmoothedWorldPosition;
    private Quaternion _smoothedRotation = Quaternion.identity;
    private bool _hasSmoothedRotation;

    public bool IsShowing => _visible;
    public bool IsShowingQuestOffer => IsShowingPersistentContent;
    public bool IsShowingPersistentContent => _visible && _currentContent != null && _currentContent.lifetimeMode == NpcDialogueBubbleLifetimeMode.PersistentUntilHidden;
    public int CurrentPriority => _currentPriority;
    public bool IsAmbientVisible => _visible && _currentAmbient;

    private void Awake()
    {
        if (instantiatePrefabOnAwake)
            EnsureVisuals();
    }

    public void SetAnchor(Transform anchor)
    {
        bubbleAnchor = anchor;
    }

    public bool TryPresent(string speakerName, NpcDialogueLine line, DialogueContext context, InputActionAsset inputActions, bool forceInterrupt)
    {
        if (line == null || string.IsNullOrWhiteSpace(line.text))
            return false;

        var content = new NpcDialogueBubbleContent
        {
            kind = NpcDialogueBubbleContentKind.Simple,
            lifetimeMode = NpcDialogueBubbleLifetimeMode.Timed,
            speakerName = speakerName,
            titleText = string.Empty,
            bodyText = line.text,
            showSpeaker = line.showSpeakerName && !string.IsNullOrWhiteSpace(speakerName),
            importance = line.importance,
            styleOverride = line.styleOverride,
            context = context,
            inputActions = inputActions,
            priority = line.priority,
            duration = line.duration
        };

        return ShowContent(content, forceInterrupt);
    }

    public bool ShowContent(NpcDialogueBubbleContent content, bool forceInterrupt = false)
    {
        if (content == null)
            return false;

        if (IsShowingPersistentContent && !forceInterrupt && content.lifetimeMode != NpcDialogueBubbleLifetimeMode.PersistentUntilHidden)
            return false;

        if (_visible && !forceInterrupt && content.priority < _currentPriority)
            return false;

        if (!EnsureVisuals())
            return false;

        _currentContent = content;
        _activeStyle = ResolveStyle(content);
        ApplyContent(content, _activeStyle);

        _currentPriority = content.priority;
        _currentAmbient = content.kind == NpcDialogueBubbleContentKind.Simple && content.importance == NpcDialogueImportance.Ambient;
        _visible = true;
        _fadingOut = false;
        _visibleUntil = content.lifetimeMode == NpcDialogueBubbleLifetimeMode.PersistentUntilHidden
            ? float.PositiveInfinity
            : Time.unscaledTime + Mathf.Max(0.25f, content.duration);
        canvasGroup.alpha = 1f;
        canvas.enabled = true;
        UpdateVisualTransform(true);
        return true;
    }

    public void UpdateContent(NpcDialogueBubbleContent content)
    {
        if (content == null)
            return;

        ShowContent(content, forceInterrupt: true);
    }

    public void HideContent(bool immediate = false)
    {
        if (!_visible)
            return;

        if (immediate)
        {
            HideImmediate();
            return;
        }

        _visibleUntil = Time.unscaledTime;
        _fadingOut = true;
    }

    public void ShowQuestOffer(NpcQuestOfferBubbleViewData data)
    {
        if (data == null)
            return;

        string metaText = ComposeQuestMetaText(data);
        string controlsText = ComposeQuestControlsText(data);

        ShowContent(new NpcDialogueBubbleContent
        {
            kind = NpcDialogueBubbleContentKind.Card,
            lifetimeMode = NpcDialogueBubbleLifetimeMode.PersistentUntilHidden,
            speakerName = data.npcName,
            titleText = data.questTitle,
            bodyText = data.bodyText,
            metaText = metaText,
            controlsText = controlsText,
            errorText = data.errorText,
            errorUntil = data.errorUntil,
            showSpeaker = !string.IsNullOrWhiteSpace(data.npcName),
            importance = NpcDialogueImportance.Quest,
            styleOverride = data.styleOverride ?? questStyle,
            priority = 1000
        }, forceInterrupt: true);
    }

    public void UpdateQuestOffer(NpcQuestOfferBubbleViewData data)
    {
        ShowQuestOffer(data);
    }

    public void HideQuestOffer(bool immediate = false)
    {
        HideContent(immediate);
    }

    public void HideImmediate()
    {
        if (canvas != null)
            canvas.enabled = false;
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        _visible = false;
        _fadingOut = false;
        _currentPriority = int.MinValue;
        _currentAmbient = false;
        _currentContent = null;
        _hasSmoothedWorldPosition = false;
        _hasSmoothedRotation = false;
    }

    private void LateUpdate()
    {
        if (!_visible)
            return;

        if (Time.unscaledTime >= _visibleUntil)
            _fadingOut = true;

        if (_fadingOut)
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 0f, Time.unscaledDeltaTime / FadeDuration);
            if (canvasGroup.alpha <= 0.001f)
            {
                HideImmediate();
                return;
            }
        }

        UpdateVisualTransform(false);
    }

    [ContextMenu("Generate Default Bubble Template")]
    private void GenerateDefaultBubbleTemplate()
    {
        ClearGeneratedBubbleTemplate();
        CreateDefaultTemplate();
        AutoBindBubbleTemplateReferences();
        ApplyCurrentStyleToTemplate();
    }

    [ContextMenu("Auto Bind Bubble Template References")]
    private void AutoBindBubbleTemplateReferences()
    {
        Transform searchRoot = transform.Find(RootName);
        if (searchRoot == null)
            searchRoot = transform;

        rootRect = FindRect(searchRoot, RootName) ?? rootRect;
        canvas = rootRect != null ? rootRect.GetComponent<Canvas>() : canvas;
        canvasGroup = rootRect != null ? rootRect.GetComponent<CanvasGroup>() : canvasGroup;

        backgroundImage = FindImage(searchRoot, PanelName) ?? backgroundImage;
        backgroundOutline = backgroundImage != null ? backgroundImage.GetComponent<Outline>() : backgroundOutline;
        accentBar = FindImage(searchRoot, AccentBarName) ?? accentBar;
        speakerLabel = FindLabel(searchRoot, SpeakerLabelName) ?? speakerLabel;
        titleLabel = FindLabel(searchRoot, TitleLabelName) ?? titleLabel;
        bodyLabel = FindLabel(searchRoot, BodyLabelName) ?? bodyLabel;
        metaLabel = FindLabel(searchRoot, MetaLabelName) ?? metaLabel;
        controlsLabel = FindLabel(searchRoot, ControlsLabelName) ?? controlsLabel;
        errorLabel = FindLabel(searchRoot, ErrorLabelName) ?? errorLabel;
    }

    [ContextMenu("Clear Generated Bubble Template")]
    private void ClearGeneratedBubbleTemplate()
    {
        if (_generatedRoot != null)
        {
            SafeDestroy(_generatedRoot);
            _generatedRoot = null;
        }

        var existingRoot = transform.Find(RootName);
        if (existingRoot != null && existingRoot.gameObject != gameObject && existingRoot.GetComponent<Canvas>() != null)
            SafeDestroy(existingRoot.gameObject);

        canvas = null;
        canvasGroup = null;
        rootRect = null;
        backgroundImage = null;
        backgroundOutline = null;
        accentBar = null;
        speakerLabel = null;
        titleLabel = null;
        bodyLabel = null;
        metaLabel = null;
        controlsLabel = null;
        errorLabel = null;
    }

    [ContextMenu("Apply Current Style To Template")]
    private void ApplyCurrentStyleToTemplate()
    {
        if (!EnsureVisuals())
            return;

        ApplyStyleOnly(_activeStyle != null ? _activeStyle : ambientStyle);
        RefreshLayout(_activeStyle != null ? _activeStyle : ambientStyle);
    }

    private bool EnsureVisuals()
    {
        if (HasBoundVisuals())
            return true;

        AutoBindBubbleTemplateReferences();
        if (HasBoundVisuals())
            return true;

        if (bubblePrefab != null)
        {
            InstantiateBubblePrefab();
            AutoBindBubbleTemplateReferences();
            if (HasBoundVisuals())
                return true;
        }

        if (generateFallbackIfMissing)
        {
            CreateDefaultTemplate();
            AutoBindBubbleTemplateReferences();
            if (HasBoundVisuals())
                return true;
        }

        if (!_warnedMissingTemplate)
        {
            Debug.LogWarning("NpcDialogueBubblePresenter has no bubble template or bound references, so dialogue cannot be displayed.", this);
            _warnedMissingTemplate = true;
        }

        return false;
    }

    private bool HasBoundVisuals()
    {
        return canvas != null &&
               canvasGroup != null &&
               rootRect != null &&
               backgroundImage != null &&
               backgroundOutline != null &&
               speakerLabel != null &&
               titleLabel != null &&
               bodyLabel != null &&
               metaLabel != null &&
               controlsLabel != null &&
               errorLabel != null;
    }

    private void InstantiateBubblePrefab()
    {
        if (bubblePrefab == null)
            return;

        if (_generatedRoot != null)
            SafeDestroy(_generatedRoot);

        var instance = Instantiate(bubblePrefab, transform);
        instance.name = RootName;
        _generatedRoot = instance;
    }

    private void CreateDefaultTemplate()
    {
        var root = new GameObject(RootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        root.transform.SetParent(transform, false);
        _generatedRoot = root;

        rootRect = root.GetComponent<RectTransform>();
        canvas = root.GetComponent<Canvas>();
        canvasGroup = root.GetComponent<CanvasGroup>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.enabled = false;
        canvasGroup.alpha = 0f;

        var panelObject = new GameObject(PanelName, typeof(RectTransform), typeof(Image), typeof(Outline), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panelObject.transform.SetParent(root.transform, false);
        var panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(320f, 120f);

        backgroundImage = panelObject.GetComponent<Image>();
        backgroundOutline = panelObject.GetComponent<Outline>();
        backgroundOutline.effectDistance = new Vector2(1f, -1f);

        var layout = panelObject.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = panelObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        accentBar = CreateBar(AccentBarName, panelObject.transform, 4f);
        speakerLabel = CreateLabel(SpeakerLabelName, panelObject.transform, 24f, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        titleLabel = CreateLabel(TitleLabelName, panelObject.transform, 24f, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        bodyLabel = CreateLabel(BodyLabelName, panelObject.transform, 18f, TextAlignmentOptions.TopLeft, FontStyles.Normal);
        metaLabel = CreateLabel(MetaLabelName, panelObject.transform, 14f, TextAlignmentOptions.TopLeft, FontStyles.Normal);
        controlsLabel = CreateLabel(ControlsLabelName, panelObject.transform, 14f, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        errorLabel = CreateLabel(ErrorLabelName, panelObject.transform, 14f, TextAlignmentOptions.TopLeft, FontStyles.Bold);
    }

    private void ApplyContent(NpcDialogueBubbleContent content, NpcDialogueBubbleStyleSO style)
    {
        style ??= ambientStyle;
        ApplyStyleOnly(style);
        AssignFormattedText(content, style);
        UpdateVisibility(content, style);
        RefreshLayout(style);
    }

    private void ApplyStyleOnly(NpcDialogueBubbleStyleSO style)
    {
        style ??= ambientStyle;
        if (style == null)
            return;

        backgroundImage.color = style.backgroundColor;
        backgroundImage.sprite = style.backgroundSprite;
        backgroundImage.material = style.backgroundMaterial;
        backgroundImage.type = style.useSlicedBackground && style.backgroundSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        backgroundOutline.effectColor = style.borderColor;

        if (accentBar != null)
        {
            accentBar.color = style.accentColor;
            accentBar.sprite = null;
            accentBar.type = Image.Type.Simple;
        }

        speakerLabel.font = style.speakerFont != null ? style.speakerFont : TMP_Settings.defaultFontAsset;
        titleLabel.font = style.titleFont != null ? style.titleFont : speakerLabel.font;
        bodyLabel.font = style.bodyFont != null ? style.bodyFont : speakerLabel.font;
        metaLabel.font = style.metaFont != null ? style.metaFont : bodyLabel.font;
        controlsLabel.font = style.controlsFont != null ? style.controlsFont : metaLabel.font;
        errorLabel.font = style.errorFont != null ? style.errorFont : bodyLabel.font;

        speakerLabel.fontSize = style.speakerFontSize;
        titleLabel.fontSize = style.titleFontSize;
        metaLabel.fontSize = style.metaFontSize;
        controlsLabel.fontSize = style.controlsFontSize;
        errorLabel.fontSize = style.errorFontSize;
        bodyLabel.fontSize = _currentContent != null && _currentContent.importance == NpcDialogueImportance.Ambient
            ? style.bodyFontSize
            : style.importantBodyFontSize;

        speakerLabel.color = style.speakerColor;
        titleLabel.color = style.titleColor;
        bodyLabel.color = style.textColor;
        metaLabel.color = style.metaColor;
        controlsLabel.color = style.controlsColor;
        errorLabel.color = style.errorColor;

        titleLabel.alignment = style.titleAlignment;
        bodyLabel.alignment = style.bodyAlignment;
        controlsLabel.alignment = style.controlsAlignment;
        speakerLabel.alignment = TextAlignmentOptions.TopLeft;
        metaLabel.alignment = TextAlignmentOptions.TopLeft;
        errorLabel.alignment = TextAlignmentOptions.TopLeft;

        var panelRect = backgroundImage.rectTransform;
        if (panelRect.TryGetComponent(out VerticalLayoutGroup layout))
        {
            layout.padding = new RectOffset(
                Mathf.RoundToInt(style.padding.x),
                Mathf.RoundToInt(style.padding.z),
                Mathf.RoundToInt(style.padding.y),
                Mathf.RoundToInt(style.padding.w));
            layout.spacing = style.sectionGap;
        }

        if (accentBar != null && accentBar.TryGetComponent(out LayoutElement accentLayout))
        {
            accentLayout.preferredHeight = style.accentBarHeight;
            accentLayout.minHeight = style.accentBarHeight;
        }
    }

    private void AssignFormattedText(NpcDialogueBubbleContent content, NpcDialogueBubbleStyleSO style)
    {
        string speaker = content.showSpeaker ? FormatText(content.speakerName, content, style) : string.Empty;
        string title = FormatText(content.titleText, content, style);
        string body = FormatText(content.bodyText, content, style);
        string meta = FormatText(content.metaText, content, style);
        string controls = FormatText(content.controlsText, content, style);
        string error = HasVisibleError(content) ? FormatText("{warning}" + content.errorText + "{/warning}", content, style) : string.Empty;

        speakerLabel.text = speaker;
        titleLabel.text = title;
        bodyLabel.text = body;
        metaLabel.text = meta;
        controlsLabel.text = controls;
        errorLabel.text = error;
    }

    private void UpdateVisibility(NpcDialogueBubbleContent content, NpcDialogueBubbleStyleSO style)
    {
        bool showAccent = style != null && style.showAccentBar;
        if (accentBar != null)
            accentBar.gameObject.SetActive(showAccent);

        speakerLabel.gameObject.SetActive(content.showSpeaker && !string.IsNullOrWhiteSpace(speakerLabel.text));
        titleLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(titleLabel.text));
        bodyLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(bodyLabel.text));
        metaLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(metaLabel.text));
        controlsLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(controlsLabel.text));
        errorLabel.gameObject.SetActive(HasVisibleError(content) && !string.IsNullOrWhiteSpace(errorLabel.text));
    }

    private void RefreshLayout(NpcDialogueBubbleStyleSO style)
    {
        style ??= ambientStyle;
        if (style == null || backgroundImage == null || rootRect == null)
            return;

        var panelRect = backgroundImage.rectTransform;
        float width = ResolvePreferredWidth(style);
        panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);

        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);

        float measuredHeight = panelRect.rect.height;
        float finalHeight = style.autoSizePanel
            ? Mathf.Clamp(measuredHeight, style.panelMinSize.y, style.panelMaxSize.y)
            : style.panelSize.y;

        float finalWidth = style.autoSizePanel
            ? Mathf.Clamp(width, style.panelMinSize.x, style.panelMaxSize.x)
            : style.panelSize.x;

        panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, finalWidth);
        panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, finalHeight);
        rootRect.sizeDelta = new Vector2(finalWidth, finalHeight);
    }

    private float ResolvePreferredWidth(NpcDialogueBubbleStyleSO style)
    {
        float minWidth = style.panelMinSize.x;
        float maxWidth = style.panelMaxSize.x;
        float pad = style.padding.x + style.padding.z;
        float contentMaxWidth = Mathf.Max(64f, maxWidth - pad);

        float widest = 0f;
        widest = Mathf.Max(widest, CalculatePreferredWidth(speakerLabel, contentMaxWidth));
        widest = Mathf.Max(widest, CalculatePreferredWidth(titleLabel, contentMaxWidth));
        widest = Mathf.Max(widest, CalculatePreferredWidth(bodyLabel, contentMaxWidth));
        widest = Mathf.Max(widest, CalculatePreferredWidth(metaLabel, contentMaxWidth));
        widest = Mathf.Max(widest, CalculatePreferredWidth(controlsLabel, contentMaxWidth));
        widest = Mathf.Max(widest, CalculatePreferredWidth(errorLabel, contentMaxWidth));

        float target = widest + pad;
        if (target < style.panelSize.x)
            target = style.panelSize.x;

        return style.autoSizePanel
            ? Mathf.Clamp(target, minWidth, maxWidth)
            : style.panelSize.x;
    }

    private void UpdateVisualTransform(bool forceInstant)
    {
        Transform anchor = bubbleAnchor != null ? bubbleAnchor : transform;
        _activeCamera = ResolveCamera();
        if (_activeCamera == null || rootRect == null)
            return;

        var style = _activeStyle != null ? _activeStyle : ambientStyle;
        bool importantContent = _currentContent != null && _currentContent.importance != NpcDialogueImportance.Ambient;
        Vector3 targetWorldPosition = anchor.position + worldOffset;
        if (style != null && style.useStabilizedFollow && !forceInstant)
        {
            float lerp = 1f - Mathf.Exp(-Mathf.Max(0f, style.followSmoothing) * Time.unscaledDeltaTime);
            if (!_hasSmoothedWorldPosition)
            {
                _smoothedWorldPosition = targetWorldPosition;
                _hasSmoothedWorldPosition = true;
            }
            else
            {
                _smoothedWorldPosition = Vector3.Lerp(_smoothedWorldPosition, targetWorldPosition, lerp);
            }
        }
        else
        {
            _smoothedWorldPosition = targetWorldPosition;
            _hasSmoothedWorldPosition = true;
        }

        rootRect.position = _smoothedWorldPosition;

        Vector3 toCamera = _activeCamera.transform.position - _smoothedWorldPosition;
        if (toCamera.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);
            if (style != null && style.rotationSmoothing > 0f && !forceInstant)
            {
                float rotationLerp = 1f - Mathf.Exp(-style.rotationSmoothing * Time.unscaledDeltaTime);
                if (!_hasSmoothedRotation)
                {
                    _smoothedRotation = targetRotation;
                    _hasSmoothedRotation = true;
                }
                else
                {
                    _smoothedRotation = Quaternion.Slerp(_smoothedRotation, targetRotation, rotationLerp);
                }
                rootRect.rotation = _smoothedRotation;
            }
            else
            {
                _smoothedRotation = targetRotation;
                _hasSmoothedRotation = true;
                rootRect.rotation = targetRotation;
            }
        }

        float distance = toCamera.magnitude;
        float minWorldScale = style != null ? style.minWorldScale : 0.0075f;
        float maxWorldScale = style != null ? style.maxWorldScale : 0.011f;
        float readableDistance = style != null ? style.readableDistance : 35f;
        float maxReadableDistance = style != null ? Mathf.Max(readableDistance, style.maxReadableDistance) : readableDistance;
        float fadeStartDistance = style != null ? Mathf.Max(0.1f, style.fadeStartDistance) : readableDistance;
        float fadeEndDistance = style != null ? Mathf.Max(fadeStartDistance, style.fadeEndDistance) : maxReadableDistance;
        if (importantContent)
        {
            readableDistance = Mathf.Max(readableDistance, maxReadableDistance);
            fadeStartDistance = Mathf.Max(fadeStartDistance, readableDistance * 0.85f);
            fadeEndDistance = Mathf.Max(fadeEndDistance, maxReadableDistance);
        }

        bool constantScale = style != null && (style.useConstantScreenSize || style.scaleMode == NpcDialogueScaleMode.ConstantScreenSize) && (!_currentAmbient || importantContent);
        float distanceForScale = importantContent && style != null && style.importantKeepsReadableScale
            ? Mathf.Min(distance, readableDistance)
            : distance;
        float scale = constantScale
            ? maxWorldScale
            : Mathf.Lerp(minWorldScale, maxWorldScale, Mathf.InverseLerp(readableDistance, 2f, distanceForScale));
        rootRect.localScale = Vector3.one * scale;

        if (!_fadingOut)
        {
            float targetAlpha = distance <= fadeStartDistance
                ? 1f
                : distance >= fadeEndDistance
                    ? 0f
                    : 1f - Mathf.InverseLerp(fadeStartDistance, fadeEndDistance, distance);
            canvasGroup.alpha = forceInstant ? targetAlpha : Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime / FadeDuration);
        }
    }

    private NpcDialogueBubbleStyleSO ResolveStyle(NpcDialogueBubbleContent content)
    {
        if (content != null && content.styleOverride != null)
            return content.styleOverride;

        NpcDialogueImportance importance = content != null ? content.importance : NpcDialogueImportance.Ambient;
        return importance switch
        {
            NpcDialogueImportance.Interaction => interactionStyle != null ? interactionStyle : ambientStyle,
            NpcDialogueImportance.Quest => questStyle != null ? questStyle : interactionStyle != null ? interactionStyle : ambientStyle,
            NpcDialogueImportance.Tutorial => tutorialStyle != null ? tutorialStyle : questStyle != null ? questStyle : ambientStyle,
            NpcDialogueImportance.Critical => criticalStyle != null ? criticalStyle : tutorialStyle != null ? tutorialStyle : ambientStyle,
            _ => ambientStyle
        };
    }

    private static string FormatText(string rawText, NpcDialogueBubbleContent content, NpcDialogueBubbleStyleSO style)
    {
        return NpcDialogueTextFormatter.Format(rawText, content.context, content.speakerName, content.inputActions, style);
    }

    private static bool HasVisibleError(NpcDialogueBubbleContent content)
    {
        return content != null &&
               !string.IsNullOrWhiteSpace(content.errorText) &&
               (content.errorUntil <= 0f || Time.unscaledTime <= content.errorUntil);
    }

    private static string ComposeQuestMetaText(NpcQuestOfferBubbleViewData data)
    {
        string result = string.Empty;
        AppendSegment(ref result, data.badgeText);
        AppendSegment(ref result, data.indexText);
        AppendSegment(ref result, data.stateText);
        return result;
    }

    private static string ComposeQuestControlsText(NpcQuestOfferBubbleViewData data)
    {
        string result = string.Empty;

        if (!string.IsNullOrWhiteSpace(data.confirmBindingText) && !string.IsNullOrWhiteSpace(data.confirmText))
            AppendSegment(ref result, $"{data.confirmBindingText} {data.confirmText}", "    ");

        if (data.canCycle && !string.IsNullOrWhiteSpace(data.previousBindingText))
            AppendSegment(ref result, $"{data.previousBindingText} Previous", "    ");

        if (data.canCycle && !string.IsNullOrWhiteSpace(data.nextBindingText))
            AppendSegment(ref result, $"{data.nextBindingText} Next", "    ");

        return result;
    }

    private static void AppendSegment(ref string target, string value, string separator = " • ")
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        target = string.IsNullOrWhiteSpace(target)
            ? value.Trim()
            : target + separator + value.Trim();
    }

    private static float CalculatePreferredWidth(TextMeshProUGUI label, float width)
    {
        if (label == null || !label.gameObject.activeSelf || string.IsNullOrWhiteSpace(label.text))
            return 0f;

        label.ForceMeshUpdate();
        return label.GetPreferredValues(label.text, Mathf.Max(32f, width), 0f).x;
    }

    private static Camera ResolveCamera()
    {
        if (Camera.main != null)
            return Camera.main;
        return Camera.current;
    }

    private static RectTransform FindRect(Transform root, string childName)
    {
        Transform found = FindDescendant(root, childName);
        return found != null ? found.GetComponent<RectTransform>() : null;
    }

    private static Image FindImage(Transform root, string childName)
    {
        Transform found = FindDescendant(root, childName);
        return found != null ? found.GetComponent<Image>() : null;
    }

    private static TextMeshProUGUI FindLabel(Transform root, string childName)
    {
        Transform found = FindDescendant(root, childName);
        return found != null ? found.GetComponent<TextMeshProUGUI>() : null;
    }

    private static Transform FindDescendant(Transform root, string childName)
    {
        if (root == null)
            return null;

        if (root.name == childName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDescendant(root.GetChild(i), childName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static Image CreateBar(string name, Transform parent, float height)
    {
        var image = CreateImage(name, parent);
        if (!image.TryGetComponent(out LayoutElement layout))
            layout = image.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = height;
        layout.preferredHeight = height;
        layout.flexibleHeight = 0f;
        return image;
    }

    private static TextMeshProUGUI CreateLabel(string name, Transform parent, float fontSize, TextAlignmentOptions alignment, FontStyles fontStyle)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var label = go.GetComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.fontStyle = fontStyle;
        label.text = string.Empty;
        label.raycastTarget = false;
        label.richText = true;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Overflow;

        var layout = go.GetComponent<LayoutElement>();
        layout.flexibleWidth = 1f;
        layout.flexibleHeight = 0f;
        return label;
    }

    private static Image CreateImage(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = Color.white;
        image.type = Image.Type.Simple;
        return image;
    }

    private static void SafeDestroy(Object target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }
}
