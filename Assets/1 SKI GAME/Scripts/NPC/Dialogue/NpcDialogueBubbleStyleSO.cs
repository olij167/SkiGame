using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum NpcDialogueScaleMode
{
    DistanceScaled,
    ConstantScreenSize
}

[Serializable]
public struct NpcDialogueInlineTextStyle
{
    public string id;
    public Color color;
    public bool bold;
    public bool italic;
    [Min(0.1f)] public float sizePercent;
}

[CreateAssetMenu(fileName = "NpcDialogueBubbleStyle", menuName = "SkiGame/NPC/Dialogue Bubble Style")]
public sealed class NpcDialogueBubbleStyleSO : ScriptableObject
{
    [Header("Fonts")]
    public TMP_FontAsset speakerFont;
    public TMP_FontAsset titleFont;
    public TMP_FontAsset bodyFont;
    public TMP_FontAsset metaFont;
    public TMP_FontAsset controlsFont;
    public TMP_FontAsset errorFont;
    public float speakerFontSize = 28f;
    public float titleFontSize = 24f;
    public float bodyFontSize = 18f;
    public float importantBodyFontSize = 20f;
    public float metaFontSize = 14f;
    public float controlsFontSize = 14f;
    public float errorFontSize = 14f;

    [Header("Colors")]
    public Color textColor = Color.white;
    public Color speakerColor = Color.white;
    public Color titleColor = Color.white;
    public Color metaColor = new(0.78f, 0.84f, 0.9f, 1f);
    public Color controlsColor = new(0.78f, 0.9f, 1f, 1f);
    public Color errorColor = new(1f, 0.83f, 0.45f, 1f);
    public Color backgroundColor = new(0.08f, 0.12f, 0.16f, 0.85f);
    public Color borderColor = new(0.45f, 0.7f, 0.95f, 0.35f);
    public Color accentColor = new(0.45f, 0.7f, 0.95f, 1f);

    [Header("Background")]
    public Sprite backgroundSprite;
    public Material backgroundMaterial;
    public bool useSlicedBackground = true;

    [Header("Layout")]
    public Vector2 panelSize = new(320f, 140f);
    public bool autoSizePanel = true;
    public Vector2 panelMinSize = new(220f, 90f);
    public Vector2 panelMaxSize = new(460f, 320f);
    public Vector4 padding = new(16f, 12f, 16f, 12f);
    public float sectionGap = 8f;
    public bool showAccentBar = true;
    public float accentBarHeight = 4f;

    [Header("Alignment")]
    public TextAlignmentOptions bodyAlignment = TextAlignmentOptions.TopLeft;
    public TextAlignmentOptions titleAlignment = TextAlignmentOptions.TopLeft;
    public TextAlignmentOptions controlsAlignment = TextAlignmentOptions.TopLeft;

    [Header("Inline Text")]
    public List<NpcDialogueInlineTextStyle> inlineTextStyles = new();

    [Header("Distance")]
    public float readableDistance = 52f;
    public float maxReadableDistance = 64f;
    public float fadeStartDistance = 48f;
    public float fadeEndDistance = 64f;
    public float followSmoothing = 14f;
    public float rotationSmoothing = 16f;
    public bool useStabilizedFollow = true;
    public bool useConstantScreenSize = false;
    public bool importantKeepsReadableScale = true;
    public bool clampNearScreenEdge = false;
    public float minScreenMargin = 0.03f;
    public float minWorldScale = 0.0075f;
    public float maxWorldScale = 0.011f;
    public NpcDialogueScaleMode scaleMode = NpcDialogueScaleMode.DistanceScaled;

    [Header("Legacy Serialized Fields")]
    [SerializeField, HideInInspector] private TMP_FontAsset chipFont;
    [SerializeField, HideInInspector] private float chipFontSize = 14f;
    [SerializeField, HideInInspector] private Color chipTextColor = new(0.88f, 0.94f, 1f, 1f);
    [SerializeField, HideInInspector] private Color chipBackgroundColor = new(0.19f, 0.29f, 0.42f, 0.98f);
    [SerializeField, HideInInspector] private Color cardBackgroundColor = new(0.08f, 0.12f, 0.16f, 0.85f);
    [SerializeField, HideInInspector] private Color innerCardBackgroundColor = new(0.11f, 0.17f, 0.29f, 0.94f);
    [SerializeField, HideInInspector] private Sprite innerCardSprite;
    [SerializeField, HideInInspector] private Sprite chipSprite;
    [SerializeField, HideInInspector] private float headerRowHeight = 26f;
    [SerializeField, HideInInspector] private float selectorRowHeight = 52f;
    [SerializeField, HideInInspector] private float actionRowHeight = 28f;
    [SerializeField, HideInInspector] private float questBodyFontSize = 16f;
    [SerializeField, HideInInspector] private Vector2 questPanelSize = new(390f, 220f);
    [SerializeField, HideInInspector] private Vector4 questPanelPadding = new(16f, 14f, 16f, 14f);
    [SerializeField, HideInInspector] private bool autoSizeQuestPanel = true;
    [SerializeField, HideInInspector] private Vector2 questMinSize = new(340f, 180f);
    [SerializeField, HideInInspector] private Vector2 questMaxSize = new(460f, 320f);
    [SerializeField, HideInInspector] private float questReadableDistance = 58f;
    [SerializeField, HideInInspector] private float maxVisibleDistance = 35f;
    [SerializeField, HideInInspector] private float importantReadableDistance = 52f;

    private void OnEnable()
    {
        MigrateLegacyFields();
    }

    private void OnValidate()
    {
        MigrateLegacyFields();
    }

    private void MigrateLegacyFields()
    {
        if (metaFont == null && chipFont != null)
            metaFont = chipFont;
        if (controlsFont == null && chipFont != null)
            controlsFont = chipFont;

        if (metaFontSize <= 0f)
            metaFontSize = chipFontSize > 0f ? chipFontSize : 14f;
        if (controlsFontSize <= 0f)
            controlsFontSize = chipFontSize > 0f ? chipFontSize : 14f;

        if (metaColor.a <= 0f && chipTextColor.a > 0f)
            metaColor = chipTextColor;
        if (controlsColor.a <= 0f && chipTextColor.a > 0f)
            controlsColor = chipTextColor;

        if (panelSize.x < 1f || panelSize.y < 1f)
            panelSize = questPanelSize;
        if (panelMinSize.x < 1f || panelMinSize.y < 1f)
            panelMinSize = questMinSize;
        if (panelMaxSize.x < panelMinSize.x || panelMaxSize.y < panelMinSize.y)
            panelMaxSize = questMaxSize;

        if (padding == Vector4.zero && questPanelPadding != Vector4.zero)
            padding = questPanelPadding;

        if (bodyFontSize <= 0f && questBodyFontSize > 0f)
            bodyFontSize = questBodyFontSize;

        if (readableDistance <= 0f)
        {
            readableDistance = questReadableDistance > 0f
                ? questReadableDistance
                : Mathf.Max(maxVisibleDistance, importantReadableDistance);
        }

        if (!autoSizePanel && autoSizeQuestPanel)
            autoSizePanel = true;

        if (showAccentBar == false && accentBarHeight <= 0f)
            accentBarHeight = 4f;
    }
}
