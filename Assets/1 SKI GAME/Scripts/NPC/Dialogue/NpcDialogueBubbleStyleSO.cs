using TMPro;
using UnityEngine;
using System;
using System.Collections.Generic;

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
    public TMP_FontAsset bodyFont;
    public TMP_FontAsset titleFont;
    public TMP_FontAsset chipFont;
    public float speakerFontSize = 28f;
    public float bodyFontSize = 18f;
    public float importantBodyFontSize = 20f;
    public float titleFontSize = 24f;
    public float chipFontSize = 14f;
    public float questBodyFontSize = 16f;

    [Header("Colors")]
    public Color textColor = Color.white;
    public Color speakerColor = Color.white;
    public Color backgroundColor = new(0.08f, 0.12f, 0.16f, 0.85f);
    public Color titleColor = Color.white;
    public Color chipTextColor = new(0.88f, 0.94f, 1f, 1f);
    public Color borderColor = new(0.45f, 0.7f, 0.95f, 0.35f);
    public Color accentColor = new(0.45f, 0.7f, 0.95f, 1f);
    public Color innerCardBackgroundColor = new(0.11f, 0.17f, 0.29f, 0.94f);
    public Color inputChipBackgroundColor = new(0.19f, 0.29f, 0.42f, 0.98f);

    [Header("Background")]
    public Sprite backgroundSprite;
    public Material backgroundMaterial;
    public bool useSlicedBackground = true;
    public Sprite innerCardSprite;
    public Sprite inputChipSprite;

    [Header("Layout")]
    public Vector2 panelSize = new(280f, 120f);
    public Vector2 questPanelSize = new(390f, 220f);
    public Vector4 padding = new(16f, 12f, 16f, 12f);
    public Vector4 questPanelPadding = new(16f, 14f, 16f, 14f);
    public float headerHeight = 26f;
    public float selectorRowHeight = 52f;
    public float actionRowHeight = 28f;
    public float cardGap = 8f;
    public bool autoSizeDialoguePanel = true;
    public Vector2 dialogueMinSize = new(160f, 70f);
    public Vector2 dialogueMaxSize = new(360f, 220f);
    public float dialogueHorizontalPadding = 16f;
    public float dialogueVerticalPadding = 12f;
    public bool autoSizeQuestPanel = true;
    public Vector2 questMinSize = new(340f, 180f);
    public Vector2 questMaxSize = new(460f, 320f);
    public float questHorizontalPadding = 16f;
    public float questVerticalPadding = 14f;

    [Header("Inline Text")]
    public List<NpcDialogueInlineTextStyle> inlineTextStyles = new();

    [Header("Distance")]
    public float minWorldScale = 0.0075f;
    public float maxWorldScale = 0.011f;
    public float maxVisibleDistance = 35f;
    public float importantReadableDistance = 52f;
    public float questReadableDistance = 58f;
    public NpcDialogueScaleMode scaleMode = NpcDialogueScaleMode.DistanceScaled;
}
