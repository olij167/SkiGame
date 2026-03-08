using System.Collections.Generic;
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

    [Header("Direct Payload (preferred)")]
    public Texture skinPatternTexture;      // SkinPattern
    public Sprite eyeSprite;                // EyeIcon (also used for UI icon if you want)
    public GameObject hatPrefab;            // Hat
    public GameObject jacketPrefab;          // Jacket
    public int customizerIndex = -1;          // for SkinPattern / EyeIcon / Hat / Cloak (maps to CharacterCustomizer arrays)
    public SkiGearProfileSO gearProfile;      // for Skis / Poles

    [Header("Gear Default Pattern (optional)")]
    public CustomizationOptionSO defaultPatternOption;   // preferred authoring reference
    public string defaultPatternId;                      // legacy / fallback

    [Header("Unlocks On Purchase")]
    public List<CustomizationOptionSO> unlockPatternOptionsOnPurchase; // preferred
    public List<string> unlockPatternIdsOnPurchase;                   // legacy / fallback

    public Vector2 defaultPatternTiling = Vector2.one;
    public Vector2 defaultPatternOffset = Vector2.zero;
    public float defaultPatternRotation = 0f;

    [SerializeField] private bool hasTint = true;
    [SerializeField] private Color defaultTint = new Color(0.12f, 0.12f, 0.12f, 1f);

    public bool HasTint => hasTint;
    public Color DefaultTint => defaultTint;

    [Header("Shop Rules")]
    [Tooltip("If true, this item is always shown in the shop until purchased. Once owned, it is removed to free a slot.")]
    public bool alwaysInStoreUntilOwned = false;

    [Tooltip("If true, this item is never picked for daily rotation offers (can still be persistent if alwaysInStoreUntilOwned is true).")]
    public bool excludeFromDailyRotation = false;

    public string DefaultPatternIdResolved
    {
        get
        {
            if (defaultPatternOption != null) return defaultPatternOption.id;
            return defaultPatternId;
        }
    }

    public IEnumerable<string> UnlockPatternIdsResolved()
    {
        if (unlockPatternOptionsOnPurchase != null)
            foreach (var o in unlockPatternOptionsOnPurchase)
                if (o != null && !string.IsNullOrEmpty(o.id))
                    yield return o.id;

        if (unlockPatternIdsOnPurchase != null)
            foreach (var id in unlockPatternIdsOnPurchase)
                if (!string.IsNullOrEmpty(id))
                    yield return id;
    }

}
