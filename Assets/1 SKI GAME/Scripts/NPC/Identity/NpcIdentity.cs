using UnityEngine;

[DisallowMultipleComponent]
public sealed class NpcIdentity : MonoBehaviour, INpcDialogueNameSource
{
    [SerializeField] private NpcCharacterDefinitionSO characterDefinition;
    [SerializeField] private bool preserveAuthoredName = true;
    [SerializeField] private bool preserveAuthoredProfile = true;
    [SerializeField] private bool preserveAuthoredAppearance = true;
    [SerializeField] private bool applyDialogueDefaultsOnAwake = true;

    public NpcCharacterDefinitionSO CharacterDefinition => characterDefinition;
    public bool IsAuthored => characterDefinition != null;
    public string IdentityId => characterDefinition != null ? characterDefinition.IdentityId : gameObject.name;
    public string DisplayName => characterDefinition != null ? characterDefinition.DisplayName : gameObject.name;
    public NpcCharacterRole Role => characterDefinition != null ? characterDefinition.Role : NpcCharacterRole.GenericSkier;
    public string DialogueDisplayName => DisplayName;
    public Color NameplateColor => characterDefinition != null ? characterDefinition.NameplateColor : Color.white;
    public NpcDialogueBankSO DefaultDialogueBank => characterDefinition != null ? characterDefinition.DefaultDialogueBank : null;
    public NpcAppearancePresetSO AppearancePreset => characterDefinition != null ? characterDefinition.AppearancePreset : null;
    public bool PreserveAuthoredName => IsAuthored && preserveAuthoredName;
    public bool PreserveAuthoredProfile => IsAuthored && preserveAuthoredProfile;
    public bool PreserveAuthoredAppearance => IsAuthored && preserveAuthoredAppearance;

    private void Awake()
    {
        if (applyDialogueDefaultsOnAwake)
            ApplyRuntimeDefaults();
    }

    [ContextMenu("Apply Runtime Defaults")]
    public void ApplyRuntimeDefaults()
    {
        if (TryGetComponent(out NpcDialogueAgent dialogueAgent))
        {
            dialogueAgent.ConfigureSpeakerNameSource(this);
            if (DefaultDialogueBank != null)
                dialogueAgent.SetDialogueBank(DefaultDialogueBank, onlyIfMissing: true);
        }

        if (TryGetComponent(out NpcAppearancePresetApplier presetApplier) && AppearancePreset != null)
            presetApplier.SetPreset(AppearancePreset, onlyIfMissing: true);
    }
}
