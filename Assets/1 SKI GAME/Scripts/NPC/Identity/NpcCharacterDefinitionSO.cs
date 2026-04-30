using System;
using System.Collections.Generic;
using UnityEngine;

public enum NpcCharacterRole
{
    GenericSkier,
    QuestGiver,
    TutorialInstructor,
    Rival,
    LiftAttendant,
    RaceSpectator,
    RacePromoter,
    Medic,
    Shopkeeper,
    AmbientHintNpc
}

[CreateAssetMenu(fileName = "NpcCharacterDefinition", menuName = "SkiGame/NPC/Character Definition")]
public sealed class NpcCharacterDefinitionSO : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string identityId;
    [SerializeField] private string displayName;
    [SerializeField] private NpcCharacterRole role = NpcCharacterRole.GenericSkier;
    [SerializeField] private string factionOrGroup;

    [Header("Presentation")]
    [SerializeField] private Sprite portrait;
    [SerializeField] private Color nameplateColor = Color.white;
    [SerializeField] private NpcDialogueBankSO defaultDialogueBank;
    [SerializeField] private NpcAppearancePresetSO appearancePreset;

    [Header("Tags")]
    [SerializeField] private List<string> socialTags = new();

    public string IdentityId => string.IsNullOrWhiteSpace(identityId) ? name : identityId.Trim();
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim();
    public NpcCharacterRole Role => role;
    public string FactionOrGroup => factionOrGroup;
    public Sprite Portrait => portrait;
    public Color NameplateColor => nameplateColor;
    public NpcDialogueBankSO DefaultDialogueBank => defaultDialogueBank;
    public NpcAppearancePresetSO AppearancePreset => appearancePreset;
    public IReadOnlyList<string> SocialTags => socialTags;
}
