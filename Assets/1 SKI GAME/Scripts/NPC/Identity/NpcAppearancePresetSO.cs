using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NpcAppearancePreset", menuName = "SkiGame/NPC/Appearance Preset")]
public sealed class NpcAppearancePresetSO : ScriptableObject
{
    [Serializable]
    public struct ChannelColorOverride
    {
        public string channelId;
        public Color color;
    }

    [Header("Body")]
    public CustomizationOptionSO skinPatternOption;
    public Color skinColor = Color.white;
    public CustomizationOptionSO eyeOption;
    public Color eyeColor = Color.white;
    public Color eyeOutlineColor = new(0f, 0f, 0f, 0f);

    [Header("Wearables")]
    public CustomizationOptionSO hatOption;
    public Color hatColor = Color.white;
    public CustomizationOptionSO hatPatternOption;
    public Texture2D legacyHatPattern;
    public List<ChannelColorOverride> hatChannelColors = new();

    public CustomizationOptionSO jacketOption;
    public Color jacketColor = Color.white;
    public CustomizationOptionSO jacketPatternOption;
    public Texture2D legacyJacketPattern;
    public List<ChannelColorOverride> jacketChannelColors = new();

    public CustomizationOptionSO glovesOption;
    public Color glovesColor = Color.white;
    public CustomizationOptionSO glovesPatternOption;
    public Texture2D legacyGlovesPattern;
    public List<ChannelColorOverride> gloveChannelColors = new();

    public CustomizationOptionSO bootsOption;
    public Color bootsColor = Color.white;
    public CustomizationOptionSO bootsPatternOption;
    public Texture2D legacyBootsPattern;
    public List<ChannelColorOverride> bootChannelColors = new();

    public CustomizationOptionSO accessoryOption;
    public Color accessoryColor = Color.white;
    public CustomizationOptionSO accessoryPatternOption;
    public Texture2D legacyAccessoryPattern;
    public List<ChannelColorOverride> accessoryChannelColors = new();

    [Header("Gear")]
    public CustomizationOptionSO skisOption;
    public SkiGearProfileSO skisProfileOverride;
    public Color skisColor = Color.white;
    public CustomizationOptionSO skisPatternOption;
    public Texture2D legacySkisPattern;

    public CustomizationOptionSO polesOption;
    public SkiGearProfileSO polesProfileOverride;
    public Color polesColor = Color.white;
    public CustomizationOptionSO polesPatternOption;
    public Texture2D legacyPolesPattern;
}
