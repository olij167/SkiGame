using UnityEngine;

public enum CustomizationOptionType
{
    SkinPattern,
    EyeIcon,
    Hat,
    Jacket,
    Skis,
    Poles,
}

[CreateAssetMenu(menuName = "SkiGame/Customization/Option", fileName = "CustomizationOption")]
public class CustomizationOptionSO : ScriptableObject
{
    [Header("Identity")]
    public string id;                 // must be unique
    public CustomizationOptionType type;
    public string displayName;
    [TextArea] public string description;

    [Header("UI")]
    public Sprite icon;

    [Header("Economy")]
    public int cost;

    [Header("Payload (pick what applies for this type)")]
    public int customizerIndex = -1;          // for SkinPattern / EyeIcon / Hat / Cloak (maps to CharacterCustomizer arrays)
    public SkiGearProfileSO gearProfile;      // for Skis / Poles

    [Header("Shop Rules")]
    [Tooltip("If true, this item is always shown in the shop until purchased. Once owned, it is removed to free a slot.")]
    public bool alwaysInStoreUntilOwned = false;

    [Tooltip("If true, this item is never picked for daily rotation offers (can still be persistent if alwaysInStoreUntilOwned is true).")]
    public bool excludeFromDailyRotation = false;

}
