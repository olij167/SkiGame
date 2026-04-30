using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;
using Font = UnityEngine.Font;

[RequireComponent(typeof(UIDocument))]
[DisallowMultipleComponent]
public sealed class TrickOverlayUI : MonoBehaviour
{
    private enum LabelAnimStyle
    {
        None = 0,
        Pop = 1,
        Drop = 2,
        Wiggle = 3
    }

    private sealed class TrickEntry
    {
        public VisualElement root;
        public VisualElement scoreRow;
        public Label comboScoreLabel;
        public Label comboIncrementLabel;
        public VisualElement comboRow;
        public VisualElement strikeLine;
        public Label adjectiveLabel;
        public Label failedOverlayLabel;

        public float severity01;
        public bool landed;
        public bool failed;
        public float spawnTime;
        public float lastImpactTime;
        public float holdUntil;
        public float opacity;
        public float targetOpacity;
        public float animSeed;
        public bool isLiveEntry;

        public int focusSegmentId;
        public SkierTrickTracker.TrickComboSegment[] comboSegments = Array.Empty<SkierTrickTracker.TrickComboSegment>();

        public string adjective;
        public UnityEngine.Object adjectiveFont;
        public Color adjectiveColor = Color.white;
        public int comboScore;
        public int comboIncrement;

        public int visualHash;
        public float lastVisualChangeTime;
    }

    private struct SegmentStyle
    {
        public string adjective;
        public UnityEngine.Object adjectiveFont;
        public Color adjectiveColor;

        public UnityEngine.Object mainFont;
        public Color mainColor;
        public float mainFontSize;
    }

    [Header("References")]
    [SerializeField] private UIDocument document;
    [SerializeField] private SkierTrickTracker trickTracker;

    [Header("Display")]
    [SerializeField] private int documentSortOrder = 875;
    [SerializeField] private float fadeInSpeed = 12f;
    [SerializeField] private float fadeOutSpeed = 5f;
    [SerializeField] private float landedDisplayDuration = 1.55f;
    [SerializeField] private float failedDisplayDuration = 0.95f;
    [SerializeField] private float liveEntryGraceDuration = 0.25f;
    [SerializeField] private int maxVisibleEntries = 4;

    [Header("Progression Tiers")]
    [SerializeField] private List<TrickUiProgressionTier> progressionTiers = new List<TrickUiProgressionTier>();

    [Header("Adjectives")]
    [SerializeField] private bool shareFocusedTrickFontWithAdjective = false;
    [SerializeField] private bool shareFocusedTrickColorWithAdjective = false;
    [SerializeField] private bool shareAdjectiveFontWithFocusedTrick = false;
    [SerializeField] private bool shareAdjectiveColorWithFocusedTrick = false;
    [SerializeField] private bool keepHistoricalSegmentStyling = true;
    [SerializeField] private bool plusSignsUseFocusedSegmentStyle = false;

    [SerializeField] private bool randomizeAdjectiveText = false;
    [SerializeField] private bool randomizeAdjectiveFont = false;
    [SerializeField] private bool randomizeAdjectiveColor = false;

    [SerializeField] private float adjectiveFontSizeScale = 0.68f;
    [SerializeField] private float adjectiveMinFontSize = 18f;
    [SerializeField] private TrickAdjectiveDefinition[] adjectives;

    [Header("Animation")]
    [SerializeField] private LabelAnimStyle adjectiveAnimStyle = LabelAnimStyle.Drop;
    [SerializeField] private LabelAnimStyle trickAnimStyle = LabelAnimStyle.Pop;
    [SerializeField] private float adjectiveAnimDuration = 0.18f;
    [SerializeField] private float adjectiveDropDistance = 18f;
    [SerializeField] private float adjectivePopScale = 1.22f;
    [SerializeField] private float trickAnimDuration = 0.14f;
    [SerializeField] private float trickPopScale = 1.12f;
    [Tooltip("When enabled, trick pulse animation avoids horizontal root scaling so long labels do not overflow off the side of the screen.")]
    [SerializeField] private bool preventHorizontalPulseOverflow = true;

    [Tooltip("Horizontal scale used during trick pulses when overflow prevention is enabled. Keep at 1 to avoid labels growing off-screen.")]
    [SerializeField, Range(1f, 1.2f)] private float safePulseHorizontalScale = 1f;

    [Tooltip("Vertical scale used during trick pulses when overflow prevention is enabled. This preserves the punchy pulse without widening the label.")]
    [SerializeField, Range(1f, 1.35f)] private float safePulseVerticalScaleMultiplier = 1f;

    [SerializeField] private float wiggleSpeed = 10f;
    [SerializeField] private float wigglePixels = 3f;
    [SerializeField] private float landedFlashDuration = 0.32f;
    [SerializeField] private float landedFlashSpeed = 9f;
    [SerializeField] private float landedConfirmScale = 1.08f;
    [SerializeField] private float landedConfirmLiftPixels = 8f;
    [SerializeField] private float landedSettleDuration = 0.38f;
    [SerializeField] private float landedAdjectiveBoost = 1.14f;

    [Header("Failure Styling")]
    [SerializeField] private UnityEngine.Object failedFontAsset;
    [SerializeField] private Color failedColor = new Color(1f, 0.18f, 0.18f, 1f);
    [SerializeField] private Color failedOverlayColor = new Color(1f, 0.95f, 0.95f, 1f);
    [SerializeField] private float stackedLabelFontSize = 36f;

    [Header("Score Styling")]
    [SerializeField] private UnityEngine.Object comboScoreFontAsset;
    [SerializeField] private UnityEngine.Object comboIncrementFontAsset;
    [SerializeField] private Color comboScoreColor = new Color(1f, 0.97f, 0.84f, 1f);
    [SerializeField] private Color comboIncrementColor = new Color(1f, 0.77f, 0.25f, 1f);
    [SerializeField] private float comboScoreFontSize = 34f;
    [SerializeField] private float comboIncrementFontSize = 22f;
    [SerializeField] private float comboScorePulseScale = 1.18f;

    private VisualElement _root;
    private VisualElement _listRoot;

    private readonly List<TrickEntry> _entries = new List<TrickEntry>();
    private TrickEntry _liveEntry;
    private bool _isBound;

    private void Reset()
    {
        document = GetComponent<UIDocument>();
        if (trickTracker == null)
            trickTracker = FindFirstObjectByType<SkierTrickTracker>();
    }

    private void OnEnable()
    {
        if (document == null)
            document = GetComponent<UIDocument>();

        if (document == null)
        {
            Debug.LogError($"[{nameof(TrickOverlayUI)}] No UIDocument found on '{name}'.", this);
            enabled = false;
            return;
        }

        document.sortingOrder = documentSortOrder;
        _root = document.rootVisualElement;

        if (_root == null)
        {
            Debug.LogError($"[{nameof(TrickOverlayUI)}] UIDocument rootVisualElement is null on '{name}'.", this);
            enabled = false;
            return;
        }

        _listRoot = _root.Q<VisualElement>("TrickList");
        if (_listRoot == null)
        {
            Debug.LogError($"[{nameof(TrickOverlayUI)}] Failed to bind TrickList on '{name}'.", this);
            enabled = false;
            return;
        }

        if (trickTracker == null)
            trickTracker = FindFirstObjectByType<SkierTrickTracker>();

        if (trickTracker != null)
        {
            trickTracker.OnLiveTrickUpdated -= HandleLiveTrickUpdated;
            trickTracker.OnTrickResolved -= HandleTrickResolved;
            trickTracker.OnLiveTrickUpdated += HandleLiveTrickUpdated;
            trickTracker.OnTrickResolved += HandleTrickResolved;
            _isBound = true;
        }

        _entries.Clear();
        _liveEntry = null;
        _listRoot.Clear();
    }

    private void OnDisable()
    {
        if (_isBound && trickTracker != null)
        {
            trickTracker.OnLiveTrickUpdated -= HandleLiveTrickUpdated;
            trickTracker.OnTrickResolved -= HandleTrickResolved;
        }

        _isBound = false;
        _entries.Clear();
        _liveEntry = null;
    }

    private void Update()
    {
        if (_listRoot == null)
            return;

        float now = Time.unscaledTime;

        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            TrickEntry entry = _entries[i];
            if (entry == null || entry.root == null)
            {
                _entries.RemoveAt(i);
                continue;
            }

            if (now > entry.holdUntil)
                entry.targetOpacity = 0f;

            float speed = entry.targetOpacity > entry.opacity ? fadeInSpeed : fadeOutSpeed;
            entry.opacity = Mathf.MoveTowards(entry.opacity, entry.targetOpacity, speed * Time.unscaledDeltaTime);
            entry.root.style.opacity = entry.opacity;

            AnimateEntry(entry, now);

            if (entry.targetOpacity <= 0f && entry.opacity <= 0.001f)
            {
                if (_liveEntry == entry)
                    _liveEntry = null;

                entry.root.RemoveFromHierarchy();
                _entries.RemoveAt(i);
            }
        }

        TrimOldestEntries();
    }

    private void HandleLiveTrickUpdated(SkierTrickTracker.TrickLiveState state)
    {
        if (!state.active || state.comboSegments == null || state.comboSegments.Length == 0)
        {
            if (_liveEntry != null)
            {
                _liveEntry.isLiveEntry = false;
                _liveEntry.targetOpacity = 0f;
                _liveEntry.holdUntil = Time.unscaledTime;
                _liveEntry = null;
            }
            return;
        }

        if (_liveEntry == null)
        {
            _liveEntry = CreateEntry();
            _liveEntry.isLiveEntry = true;
            _entries.Insert(0, _liveEntry);
            _listRoot.Insert(0, _liveEntry.root);
        }

        _liveEntry.comboSegments = state.comboSegments;
        _liveEntry.focusSegmentId = state.focusSegmentId;
        _liveEntry.failed = false;
        _liveEntry.landed = false;
        _liveEntry.severity01 = ComputeMaxSeverity(state.comboSegments);
        _liveEntry.comboScore = state.estimatedComboScore;
        _liveEntry.comboIncrement = 0;
        _liveEntry.targetOpacity = 1f;
        _liveEntry.holdUntil = float.PositiveInfinity;

        ApplyEntryVisualState(_liveEntry, string.Empty);
    }

    private void HandleTrickResolved(SkierTrickTracker.TrickResult result)
    {
        TrickEntry entry = CreateEntry();
        entry.isLiveEntry = false;
        entry.comboSegments = result.comboSegments ?? Array.Empty<SkierTrickTracker.TrickComboSegment>();
        entry.focusSegmentId = result.focusSegmentId;
        entry.failed = !result.success;
        entry.landed = result.success;
        entry.severity01 = ComputeMaxSeverity(entry.comboSegments);
        entry.comboScore = result.comboScore;
        entry.comboIncrement = result.success ? result.comboScore : 0;
        entry.targetOpacity = 1f;
        entry.holdUntil = Time.unscaledTime + (result.success ? landedDisplayDuration : failedDisplayDuration);
        entry.lastImpactTime = Time.unscaledTime;

        ApplyEntryVisualState(entry, result.failureOverlayText);

        _entries.Insert(0, entry);
        _listRoot.Insert(0, entry.root);

        if (_liveEntry != null)
        {
            _liveEntry.isLiveEntry = false;
            _liveEntry.holdUntil = Time.unscaledTime + liveEntryGraceDuration;
            _liveEntry.targetOpacity = 0f;
            _liveEntry = null;
        }
    }

    private TrickEntry CreateEntry()
    {
        TrickEntry entry = new TrickEntry
        {
            spawnTime = Time.unscaledTime,
            lastVisualChangeTime = Time.unscaledTime,
            opacity = 0f,
            targetOpacity = 0f,
            animSeed = UnityEngine.Random.value * 100f
        };

        entry.root = new VisualElement();
        entry.root.name = "TrickEntry";
        entry.root.AddToClassList("trick-entry");

        entry.root.style.transformOrigin = new TransformOrigin(
    new Length(0f, LengthUnit.Percent),
    new Length(100f, LengthUnit.Percent),
    0f);

        entry.adjectiveLabel = new Label();
        entry.adjectiveLabel.name = "Lbl_TrickAdjective";
        entry.adjectiveLabel.AddToClassList("trick-adjective");

        entry.adjectiveLabel.style.transformOrigin = new TransformOrigin(
    new Length(0f, LengthUnit.Percent),
    new Length(100f, LengthUnit.Percent),
    0f);

        entry.scoreRow = new VisualElement();
        entry.scoreRow.name = "ScoreRow";
        entry.scoreRow.AddToClassList("trick-score-row");

        entry.comboScoreLabel = new Label();
        entry.comboScoreLabel.name = "Lbl_ComboScore";
        entry.comboScoreLabel.AddToClassList("trick-combo-score");

        entry.comboIncrementLabel = new Label();
        entry.comboIncrementLabel.name = "Lbl_ComboIncrement";
        entry.comboIncrementLabel.AddToClassList("trick-combo-increment");

        entry.scoreRow.Add(entry.comboScoreLabel);
        entry.scoreRow.Add(entry.comboIncrementLabel);

        entry.comboRow = new VisualElement();
        entry.comboRow.name = "ComboRow";
        entry.comboRow.AddToClassList("trick-combo-row");

        entry.comboRow.style.transformOrigin = new TransformOrigin(
    new Length(0f, LengthUnit.Percent),
    new Length(100f, LengthUnit.Percent),
    0f);

        entry.strikeLine = new VisualElement();
        entry.strikeLine.name = "StrikeLine";
        entry.strikeLine.AddToClassList("trick-strike");

        entry.failedOverlayLabel = new Label();
        entry.failedOverlayLabel.name = "Lbl_FailedOverlay";
        entry.failedOverlayLabel.AddToClassList("trick-failed");

        entry.failedOverlayLabel.style.transformOrigin = new TransformOrigin(
    new Length(0f, LengthUnit.Percent),
    new Length(100f, LengthUnit.Percent),
    0f);

        entry.root.Add(entry.scoreRow);
        entry.root.Add(entry.adjectiveLabel);
        entry.root.Add(entry.comboRow);
        entry.root.Add(entry.strikeLine);
        entry.root.Add(entry.failedOverlayLabel);
        entry.root.style.opacity = 0f;

        return entry;
    }

    private void ApplyEntryVisualState(TrickEntry entry, string failureOverlayText)
    {
        if (entry == null)
            return;

        int newHash = ComputeEntryVisualHash(entry.comboSegments, entry.focusSegmentId, failureOverlayText);
        if (newHash != entry.visualHash)
        {
            entry.visualHash = newHash;
            entry.lastVisualChangeTime = Time.unscaledTime;
        }

        entry.comboRow.Clear();

        if (entry.comboScoreLabel != null)
        {
            bool showScore = entry.comboScore > 0;
            entry.comboScoreLabel.style.display = showScore ? DisplayStyle.Flex : DisplayStyle.None;
            entry.comboScoreLabel.text = showScore ? entry.comboScore.ToString() : string.Empty;
            ApplyFontToLabel(entry.comboScoreLabel, comboScoreFontAsset);
            entry.comboScoreLabel.style.fontSize = comboScoreFontSize;
            entry.comboScoreLabel.style.color = new StyleColor(entry.failed ? failedColor : comboScoreColor);
        }

        if (entry.comboIncrementLabel != null)
        {
            bool showIncrement = entry.comboIncrement > 0;
            entry.comboIncrementLabel.style.display = showIncrement ? DisplayStyle.Flex : DisplayStyle.None;
            entry.comboIncrementLabel.text = showIncrement ? $"+{entry.comboIncrement}" : string.Empty;
            ApplyFontToLabel(entry.comboIncrementLabel, comboIncrementFontAsset);
            entry.comboIncrementLabel.style.fontSize = comboIncrementFontSize;
            entry.comboIncrementLabel.style.color = new StyleColor(comboIncrementColor);
        }

        if (entry.scoreRow != null)
            entry.scoreRow.style.display = entry.comboScore > 0 || entry.comboIncrement > 0 ? DisplayStyle.Flex : DisplayStyle.None;

        SegmentStyle focusedStyle = default;
        SegmentStyle lastVisibleStyle = default;
        bool foundFocus = false;
        bool hasAnyVisibleSegment = false;

        for (int i = 0; i < entry.comboSegments.Length; i++)
        {
            SkierTrickTracker.TrickComboSegment seg = entry.comboSegments[i];
            SegmentStyle style = ResolveStyleForSegment(seg);

            lastVisibleStyle = style;
            hasAnyVisibleSegment = true;

            if (seg.id == entry.focusSegmentId)
            {
                focusedStyle = style;
                foundFocus = true;
            }

            if (!keepHistoricalSegmentStyling && foundFocus)
                style = focusedStyle;

            Label segLabel = new Label(seg.text);
            segLabel.AddToClassList("trick-segment");

            segLabel.style.whiteSpace = WhiteSpace.Normal;
            segLabel.style.maxWidth = Length.Percent(100f);
            segLabel.style.flexShrink = 1f;
            segLabel.style.overflow = Overflow.Hidden;

            ApplyFontToLabel(segLabel, style.mainFont);
            segLabel.style.fontSize = style.mainFontSize;

            Color segColor = style.mainColor;
            if (entry.failed)
                segColor = failedColor;

            segLabel.style.color = new StyleColor(segColor);

            switch (seg.state)
            {
                case SkierTrickTracker.SegmentState.Building:
                    segLabel.style.opacity = 1f;
                    break;
                case SkierTrickTracker.SegmentState.Pending:
                    segLabel.style.opacity = 0.88f;
                    break;
                case SkierTrickTracker.SegmentState.Locked:
                    segLabel.style.opacity = 1f;
                    break;
                case SkierTrickTracker.SegmentState.Failed:
                    segLabel.style.opacity = 0.72f;
                    break;
            }

            segLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            entry.comboRow.Add(segLabel);

            if (i < entry.comboSegments.Length - 1)
            {
                Label plus = new Label("+");
                plus.AddToClassList("trick-plus");

                plus.style.whiteSpace = WhiteSpace.NoWrap;
                plus.style.flexShrink = 0f;

                SegmentStyle plusStyle = plusSignsUseFocusedSegmentStyle && foundFocus ? focusedStyle : style;
                ApplyFontToLabel(plus, plusStyle.mainFont);
                plus.style.fontSize = Mathf.Max(plusStyle.mainFontSize * 0.58f, 16f);
                plus.style.color = new StyleColor(entry.failed ? failedColor : new Color(1f, 1f, 1f, 0.78f));

                entry.comboRow.Add(plus);
            }
        }

        // Fallback: if focus segment is temporarily missing, use the last visible segment
        if (!foundFocus && hasAnyVisibleSegment)
        {
            focusedStyle = lastVisibleStyle;
            foundFocus = true;
        }

        if (entry.adjectiveLabel != null)
        {
            if (foundFocus && !string.IsNullOrWhiteSpace(focusedStyle.adjective))
            {
                entry.adjective = focusedStyle.adjective;
                entry.adjectiveFont = focusedStyle.adjectiveFont;
                entry.adjectiveColor = focusedStyle.adjectiveColor;

                entry.adjectiveLabel.text = entry.adjective.ToUpperInvariant();
                entry.adjectiveLabel.style.display = DisplayStyle.Flex;

                entry.adjectiveLabel.style.whiteSpace = WhiteSpace.Normal;
                entry.adjectiveLabel.style.maxWidth = Length.Percent(100f);
                entry.adjectiveLabel.style.flexShrink = 1f;
                entry.adjectiveLabel.style.overflow = Overflow.Hidden;

                ApplyFontToLabel(
                    entry.adjectiveLabel,
                    shareFocusedTrickFontWithAdjective && focusedStyle.mainFont != null
                        ? focusedStyle.mainFont
                        : (entry.adjectiveFont != null ? entry.adjectiveFont : focusedStyle.mainFont));

                Color adjectiveColor = shareFocusedTrickColorWithAdjective
                    ? focusedStyle.mainColor
                    : entry.adjectiveColor;

                if (entry.failed)
                    adjectiveColor = failedColor;

                entry.adjectiveLabel.style.color = new StyleColor(adjectiveColor);
                entry.adjectiveLabel.style.fontSize = Mathf.Max(focusedStyle.mainFontSize * adjectiveFontSizeScale, adjectiveMinFontSize);
            }
            else
            {
                entry.adjectiveLabel.text = string.Empty;
                entry.adjectiveLabel.style.display = DisplayStyle.None;
            }
        }

        bool isStackedFailure = string.Equals(failureOverlayText, "STACKED", StringComparison.OrdinalIgnoreCase);
        bool hasComboSegments = entry.comboSegments != null && entry.comboSegments.Length > 0;
        bool stackOnlyEntry = isStackedFailure && !hasComboSegments;

        if (entry.failedOverlayLabel != null)
        {
            entry.failedOverlayLabel.text = failureOverlayText;
            entry.failedOverlayLabel.style.display = string.IsNullOrWhiteSpace(failureOverlayText) ? DisplayStyle.None : DisplayStyle.Flex;

            if (failedFontAsset != null)
                ApplyFontToLabel(entry.failedOverlayLabel, failedFontAsset);

            entry.failedOverlayLabel.style.color = new StyleColor(failedOverlayColor);
            entry.failedOverlayLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

            if (isStackedFailure)
            {
                entry.failedOverlayLabel.style.fontSize = stackedLabelFontSize;
            }
            else
            {
                entry.failedOverlayLabel.style.fontSize = 28f;
            }

            if (stackOnlyEntry)
            {
                // Let STACKED participate in layout when there is no combo row under it.
                entry.failedOverlayLabel.style.position = Position.Relative;
                entry.failedOverlayLabel.style.left = StyleKeyword.Auto;
                entry.failedOverlayLabel.style.right = StyleKeyword.Auto;
                entry.failedOverlayLabel.style.top = StyleKeyword.Auto;
                entry.failedOverlayLabel.style.bottom = StyleKeyword.Auto;
                entry.failedOverlayLabel.style.translate = new Translate(0f, 0f);
            }
            else
            {
                // Keep the normal overlay behaviour for failed trick rows with combo content.
                entry.failedOverlayLabel.style.position = Position.Absolute;
                entry.failedOverlayLabel.style.left = 0f;
                entry.failedOverlayLabel.style.right = 0f;
                entry.failedOverlayLabel.style.top = -4f;
                entry.failedOverlayLabel.style.bottom = StyleKeyword.Auto;
            }
        }

        if (entry.strikeLine != null)
        {
            entry.strikeLine.style.display = (entry.failed && !isStackedFailure) ? DisplayStyle.Flex : DisplayStyle.None;
            entry.strikeLine.style.backgroundColor = new StyleColor(failedOverlayColor);
        }

        if (entry.comboRow != null)
        {
            if (stackOnlyEntry)
            {
                entry.comboRow.style.display = DisplayStyle.None;
                entry.comboRow.style.opacity = 1f;
            }
            else
            {
                entry.comboRow.style.display = DisplayStyle.Flex;
                if (entry.failed)
                    entry.comboRow.style.opacity = isStackedFailure ? 0.45f : 1f;
                else
                    entry.comboRow.style.opacity = 1f;
            }
        }

        if (entry.adjectiveLabel != null)
        {
            if (stackOnlyEntry)
            {
                entry.adjectiveLabel.style.display = DisplayStyle.None;
            }
            else if (entry.failed && isStackedFailure)
            {
                entry.adjectiveLabel.style.display = DisplayStyle.None;
            }
        }

        if (entry.root != null)
        {
            if (stackOnlyEntry)
            {
                entry.root.style.minHeight = stackedLabelFontSize * 1.25f;
                entry.root.style.justifyContent = Justify.Center;
            }
            else
            {
                entry.root.style.minHeight = StyleKeyword.Null;
                entry.root.style.justifyContent = Justify.Center;
            }
        }

        if (shareAdjectiveFontWithFocusedTrick && foundFocus && entry.adjectiveFont != null && !entry.failed)
        {
            for (int i = 0; i < entry.comboRow.childCount; i++)
            {
                if (entry.comboRow[i] is Label lbl && lbl.ClassListContains("trick-segment"))
                    ApplyFontToLabel(lbl, entry.adjectiveFont);
            }
        }

        if (shareAdjectiveColorWithFocusedTrick && foundFocus && !entry.failed)
        {
            for (int i = 0; i < entry.comboRow.childCount; i++)
            {
                if (entry.comboRow[i] is Label lbl && lbl.ClassListContains("trick-segment"))
                    lbl.style.color = new StyleColor(entry.adjectiveColor);
            }
        }
    }

    private SegmentStyle ResolveStyleForSegment(SkierTrickTracker.TrickComboSegment segment)
    {
        SegmentStyle style = new SegmentStyle();

        TrickUiProgressionTier tier = ResolveTier(segment.progressionTier);
        style.mainFont = tier != null ? tier.fontAsset : null;
        style.mainColor = tier != null ? tier.color : Color.white;
        style.mainFontSize = tier != null && tier.fontSize > 0f ? tier.fontSize : 28f;

        if (adjectives != null && adjectives.Length > 0)
        {
            int textIndex = Mathf.Abs(segment.adjectiveTextSeed) % adjectives.Length;
            int fontIndex = randomizeAdjectiveFont
                ? Mathf.Abs(segment.adjectiveFontSeed) % adjectives.Length
                : textIndex;
            int colorIndex = randomizeAdjectiveColor
                ? Mathf.Abs(segment.adjectiveColorSeed) % adjectives.Length
                : textIndex;

            if (randomizeAdjectiveText)
                textIndex = Mathf.Abs(segment.adjectiveTextSeed) % adjectives.Length;

            TrickAdjectiveDefinition textAdj = adjectives[textIndex];
            TrickAdjectiveDefinition fontAdj = adjectives[fontIndex];
            TrickAdjectiveDefinition colorAdj = adjectives[colorIndex];

            style.adjective = textAdj != null ? textAdj.adjective : string.Empty;
            style.adjectiveFont = fontAdj != null ? fontAdj.fontAsset : null;
            style.adjectiveColor = colorAdj != null ? colorAdj.color : Color.white;
        }

        return style;
    }

    private TrickUiProgressionTier ResolveTier(int progressionTier)
    {
        if (progressionTiers == null || progressionTiers.Count == 0)
            return null;

        int index = Mathf.Clamp(Mathf.Max(1, progressionTier) - 1, 0, progressionTiers.Count - 1);
        return progressionTiers[index];
    }

    private float ComputeMaxSeverity(SkierTrickTracker.TrickComboSegment[] comboSegments)
    {
        float max = 0.2f;
        if (comboSegments == null)
            return max;

        for (int i = 0; i < comboSegments.Length; i++)
            max = Mathf.Max(max, comboSegments[i].severity01);

        return max;
    }

    private int ComputeEntryVisualHash(SkierTrickTracker.TrickComboSegment[] segments, int focusSegmentId, string failureText)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + focusSegmentId;
            hash = hash * 31 + (failureText?.GetHashCode() ?? 0);

            if (segments != null)
            {
                for (int i = 0; i < segments.Length; i++)
                {
                    hash = hash * 31 + segments[i].id;
                    hash = hash * 31 + (segments[i].text?.GetHashCode() ?? 0);
                    hash = hash * 31 + segments[i].progressionTier;
                    hash = hash * 31 + Mathf.RoundToInt(segments[i].severity01 * 1000f);
                    hash = hash * 31 + (int)segments[i].state;
                }
            }

            return hash;
        }
    }

    private void AnimateEntry(TrickEntry entry, float now)
    {
        if (entry == null || entry.root == null)
            return;

        if (entry.opacity <= 0.001f)
        {
            entry.root.style.translate = new Translate(0f, 0f);
            entry.root.style.scale = new Scale(Vector3.one);

            if (entry.comboRow != null)
                entry.comboRow.style.scale = new Scale(Vector3.one);

            if (entry.adjectiveLabel != null)
                entry.adjectiveLabel.style.scale = new Scale(Vector3.one);

            return;
        }

        float ageSinceChange = Mathf.Max(0f, now - entry.lastVisualChangeTime);

        float rootScale = 1f;
        float rootLift = 0f;

        if (trickAnimStyle == LabelAnimStyle.Pop)
        {
            float t = 1f - Mathf.Clamp01(ageSinceChange / Mathf.Max(0.01f, trickAnimDuration));
            rootScale = Mathf.Lerp(1f, trickPopScale, t);
        }
        else if (trickAnimStyle == LabelAnimStyle.Wiggle)
        {
            rootScale = 1f + Mathf.Sin((now + entry.animSeed) * wiggleSpeed) * 0.03f;
        }

        if (entry.adjectiveLabel != null && entry.adjectiveLabel.style.display.value != DisplayStyle.None)
        {
            if (adjectiveAnimStyle == LabelAnimStyle.Drop)
            {
                float t = 1f - Mathf.Clamp01(ageSinceChange / Mathf.Max(0.01f, adjectiveAnimDuration));
                float translateY = Mathf.Lerp(0f, -adjectiveDropDistance, t);
                float adjectiveScale = Mathf.Lerp(1f, adjectivePopScale, t);

                entry.adjectiveLabel.style.scale = new Scale(new Vector3(adjectiveScale, adjectiveScale, 1f));
                entry.adjectiveLabel.style.translate = new Translate(
                    new Length(0f, LengthUnit.Pixel),
                    new Length(translateY, LengthUnit.Pixel));
            }
            else if (adjectiveAnimStyle == LabelAnimStyle.Pop)
            {
                float t = 1f - Mathf.Clamp01(ageSinceChange / Mathf.Max(0.01f, adjectiveAnimDuration));
                float adjectiveScale = Mathf.Lerp(1f, adjectivePopScale, t);
                entry.adjectiveLabel.style.scale = new Scale(new Vector3(adjectiveScale, adjectiveScale, 1f));
            }
            else if (adjectiveAnimStyle == LabelAnimStyle.Wiggle)
            {
                float wiggle = Mathf.Sin((now + entry.animSeed) * wiggleSpeed) * wigglePixels;
                entry.adjectiveLabel.style.translate = new Translate(
                    new Length(0f, LengthUnit.Pixel),
                    new Length(wiggle, LengthUnit.Pixel));
            }
        }

        bool isStackedFailure =
            entry.failed &&
            entry.failedOverlayLabel != null &&
            string.Equals(entry.failedOverlayLabel.text, "STACKED", StringComparison.OrdinalIgnoreCase);

        if (entry.landed)
        {
            float landedAge = Mathf.Max(0f, now - entry.lastImpactTime);
            float landedT = 1f - Mathf.Clamp01(landedAge / Mathf.Max(0.01f, landedSettleDuration));

            rootScale *= Mathf.Lerp(1f, landedConfirmScale, landedT);
            rootLift = Mathf.Lerp(0f, -landedConfirmLiftPixels, landedT);

            if (entry.adjectiveLabel != null && entry.adjectiveLabel.style.display.value != DisplayStyle.None)
            {
                float adjectiveBoost = Mathf.Lerp(1f, landedAdjectiveBoost, landedT);
                entry.adjectiveLabel.style.scale = new Scale(new Vector3(adjectiveBoost, adjectiveBoost, 1f));
            }

            if (landedAge <= landedFlashDuration)
            {
                float flashT = Mathf.PingPong(landedAge * landedFlashSpeed, 1f);

                if (entry.comboScoreLabel != null && entry.comboScoreLabel.style.display.value != DisplayStyle.None)
                {
                    float scoreScale = Mathf.Lerp(1f, comboScorePulseScale, flashT);
                    entry.comboScoreLabel.style.scale = new Scale(new Vector3(scoreScale, scoreScale, 1f));
                }

                if (entry.comboIncrementLabel != null && entry.comboIncrementLabel.style.display.value != DisplayStyle.None)
                {
                    float incrementLift = Mathf.Lerp(0f, -10f, flashT);
                    entry.comboIncrementLabel.style.translate = new Translate(
                        new Length(0f, LengthUnit.Pixel),
                        new Length(incrementLift, LengthUnit.Pixel));
                }

                for (int i = 0; i < entry.comboRow.childCount; i++)
                {
                    if (entry.comboRow[i] is Label lbl && lbl.ClassListContains("trick-segment"))
                    {
                        Color baseColor = lbl.resolvedStyle.color;
                        lbl.style.color = new StyleColor(Color.Lerp(Color.white, baseColor, flashT));
                    }

                    if (entry.comboRow[i] is Label plus && plus.ClassListContains("trick-plus"))
                    {
                        Color baseColor = plus.resolvedStyle.color;
                        plus.style.color = new StyleColor(Color.Lerp(Color.white, baseColor, flashT));
                    }
                }
            }
        }

        if (entry.comboScoreLabel != null && (!entry.landed || (now - entry.lastImpactTime) > landedFlashDuration))
            entry.comboScoreLabel.style.scale = new Scale(Vector3.one);

        if (entry.comboIncrementLabel != null && (!entry.landed || (now - entry.lastImpactTime) > landedFlashDuration))
            entry.comboIncrementLabel.style.translate = new Translate(
                new Length(0f, LengthUnit.Pixel),
                new Length(0f, LengthUnit.Pixel));
        else if (isStackedFailure)
        {
            float failAge = Mathf.Max(0f, now - entry.lastImpactTime);
            float failT = 1f - Mathf.Clamp01(failAge / 0.28f);

            rootScale *= Mathf.Lerp(1f, 1.12f, failT);
            rootLift = Mathf.Lerp(0f, -10f, failT);

            if (entry.failedOverlayLabel != null)
            {
                float stackScale = Mathf.Lerp(1f, 1.22f, failT);
                entry.failedOverlayLabel.style.scale = new Scale(new Vector3(stackScale, stackScale, 1f));
                entry.failedOverlayLabel.style.translate = new Translate(
                    new Length(0f, LengthUnit.Pixel),
                    new Length(Mathf.Lerp(0f, -8f, failT), LengthUnit.Pixel));
            }
        }

        entry.root.style.translate = new Translate(
    new Length(0f, LengthUnit.Pixel),
    new Length(rootLift, LengthUnit.Pixel));

        if (preventHorizontalPulseOverflow)
        {
            float xScale = Mathf.Max(1f, safePulseHorizontalScale);
            float yScale = Mathf.Max(1f, rootScale * safePulseVerticalScaleMultiplier);
            entry.root.style.scale = new Scale(new Vector3(xScale, yScale, 1f));
        }
        else
        {
            entry.root.style.scale = new Scale(new Vector3(rootScale, rootScale, 1f));
        }
    }

    private void TrimOldestEntries()
    {
        while (_entries.Count > maxVisibleEntries)
        {
            TrickEntry oldest = _entries[_entries.Count - 1];
            if (oldest == null)
            {
                _entries.RemoveAt(_entries.Count - 1);
                continue;
            }

            oldest.targetOpacity = 0f;

            if (oldest.opacity > 0.05f)
                break;

            if (_liveEntry == oldest)
                _liveEntry = null;

            oldest.root?.RemoveFromHierarchy();
            _entries.RemoveAt(_entries.Count - 1);
        }
    }

    private void ApplyFontToLabel(Label label, UnityEngine.Object fontObject)
    {
        if (label == null || fontObject == null)
            return;

        if (TryBuildFontDefinition(fontObject, out FontDefinition def))
            label.style.unityFontDefinition = def;
    }

    private bool TryBuildFontDefinition(UnityEngine.Object fontObject, out FontDefinition def)
    {
        if (fontObject is Font unityFont)
        {
            def = FontDefinition.FromFont(unityFont);
            return true;
        }

        if (fontObject is FontAsset textCoreFont)
        {
            def = FontDefinition.FromSDFFont(textCoreFont);
            return true;
        }

        Type objType = fontObject.GetType();
        if (objType != null && objType.Name == "TMP_FontAsset")
        {
            PropertyInfo sourceFontFileProp = objType.GetProperty("sourceFontFile", BindingFlags.Public | BindingFlags.Instance);
            if (sourceFontFileProp != null)
            {
                object sourceFont = sourceFontFileProp.GetValue(fontObject, null);
                if (sourceFont is Font fallbackFont)
                {
                    def = FontDefinition.FromFont(fallbackFont);
                    return true;
                }
            }

            Debug.LogWarning($"[{nameof(TrickOverlayUI)}] TMP font asset '{fontObject.name}' could not provide a source font fallback.", this);
        }

        def = default;
        Debug.LogWarning($"[{nameof(TrickOverlayUI)}] Unsupported font asset type '{fontObject.GetType().Name}' on '{fontObject.name}'.", this);
        return false;
    }
}

[Serializable]
public class TrickAdjectiveDefinition
{
    public string adjective;
    public UnityEngine.Object fontAsset;
    public Color color = Color.white;
}

[Serializable]
public class TrickUiProgressionTier
{
    public string name;
    public UnityEngine.Object fontAsset;
    public Color color = Color.white;
    public float fontSize = 28f;
}
