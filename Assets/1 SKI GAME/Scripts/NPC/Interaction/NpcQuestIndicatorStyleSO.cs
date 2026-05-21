using UnityEngine;

[CreateAssetMenu(fileName = "NpcQuestIndicatorStyle", menuName = "SkiGame/NPC/Quest Indicator Style")]
public sealed class NpcQuestIndicatorStyleSO : ScriptableObject
{
    public Sprite availableIcon;
    public Sprite inProgressIcon;
    public Sprite readyToTurnInIcon;
    public Sprite replayableIcon;
    public Sprite blockedIcon;

    public Color availableColor = Color.white;
    public Color inProgressColor = new(0.7f, 0.85f, 1f, 1f);
    public Color readyToTurnInColor = new(1f, 0.9f, 0.45f, 1f);
    public Color replayableColor = new(0.72f, 1f, 0.75f, 1f);
    public Color blockedColor = new(0.8f, 0.8f, 0.8f, 0.9f);

    public Vector3 worldOffset = new(0f, 1.8f, 0f);
    public float minScale = 0.005f;
    public float maxScale = 0.008f;
    public float visibleDistance = 28f;
    public bool hideWhenDialogueVisible = true;
    public bool hideWhenQuestOfferVisible = true;
    public float fadeSpeed = 8f;
    public bool bobAnimation = true;
    public float bobAmplitude = 0.04f;
    public float bobSpeed = 2f;
    public bool showInProgressIndicator = true;
    public bool showBlockedIndicator = false;
}
