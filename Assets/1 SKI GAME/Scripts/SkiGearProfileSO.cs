using UnityEngine;

[CreateAssetMenu(menuName = "SkiGame/Gear/Ski Gear Profile", fileName = "SkiGearProfile")]
public class SkiGearProfileSO : ScriptableObject
{
    [Header("Display")]
    [Tooltip("Name shown to the player in UI (shop/loadout/etc).")]
    public string displayName = "Default Skis";

    [Tooltip("Short marketing-style description shown in UI (keep it brief).")]
    [TextArea] public string shortDescription;

    [Tooltip("Optional: used by shop / UI later. Currency cost to purchase/unlock this gear.")]
    public int cost = 0;

    [Header("Prefabs (optional)")]
    public GameObject skiPrefab;

    [Header("Simple Player-Facing Stats (0..10)")]
    [Tooltip("UI-only summary of how fast these skis feel overall.\n" +
             "Mechanical changes come from the 'Tuning' multipliers below.")]
    [Range(0, 10)] public int speed = 5;

    [Tooltip("UI-only summary of turning responsiveness and ease of control.\n" +
             "Mechanical changes come from the 'Tuning' multipliers below.")]
    [Range(0, 10)] public int handling = 5;

    [Tooltip("UI-only summary of how strongly these skis lock into a carve.\n" +
             "Mechanical changes come from the 'Tuning' multipliers below.")]
    [Range(0, 10)] public int carving = 5;

    [Tooltip("UI-only summary of stability and edge hold (reduced chatter/drift).\n" +
             "Mechanical changes come from the 'Tuning' multipliers below.")]
    [Range(0, 10)] public int stability = 5;

    [Tooltip("UI-only summary of grind ease and control.\n" +
             "Mechanical changes come from the 'Tuning' multipliers below.")]
    [Range(0, 10)] public int grind = 5;

    [Tooltip("UI-only summary of pole effectiveness.\n" +
             "Mechanical changes come from the 'Tuning' multipliers below.")]
    [Range(0, 10)] public int poles = 5;

    [Header("Under-the-Hood Modifiers (Multipliers)")]
    [Tooltip("These multipliers are the actual mechanical values used by the controller.\n" +
             "1 = no change. >1 increases effect. <1 decreases effect.")]
    public SkiGearTuning tuning = SkiGearTuning.Default;

    private void OnValidate()
    {
        // Guard against accidental zeroing (zero would effectively disable systems).
        ClampMin(ref tuning.downhillAccelMul);
        ClampMin(ref tuning.forwardFrictionMul);
        ClampMin(ref tuning.sideFrictionMul);

        ClampMin(ref tuning.tuckEffectMul);
        ClampMin(ref tuning.brakeEffectMul);

        ClampMin(ref tuning.turnSpeedMul);
        ClampMin(ref tuning.carveSteerMul);

        ClampMin(ref tuning.quickStopMul);
        ClampMin(ref tuning.traverseHoldMul);

        ClampMin(ref tuning.skateImpulseMul);
        ClampMin(ref tuning.poleImpulseMul);

        ClampMin(ref tuning.grindMinSpeedMul);
        ClampMin(ref tuning.grindCaptureRadiusMul);
        ClampMin(ref tuning.grindSpringMul);
        ClampMin(ref tuning.grindResponseMul);
        ClampMin(ref tuning.grindDriveMul);
    }

    private static void ClampMin(ref float v)
    {
        if (v <= 0f) v = 1f;
    }
}
