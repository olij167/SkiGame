using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class NpcSocialGroupAssignment : MonoBehaviour
{
    [Header("Social Group")]
    [SerializeField] private NpcSocialGroupSO socialGroup;

    [Header("Apply")]
    [SerializeField] private bool applyOnStart = true;

    [Tooltip("Delays application so NpcIdentity and NpcAppearancePresetApplier Start/Awake defaults can run first. Useful for placed prefab NPCs.")]
    [SerializeField, Min(0)] private int applyDelayFrames = 2;

    [SerializeField] private bool addSocialTags = true;
    [SerializeField] private bool applyDialogueBank = true;
    [SerializeField] private bool applyAppearance = true;

    [Header("Authored NPC Handling")]
    [Tooltip("When enabled, authored NPCs with Preserve Authored Appearance will not be randomized by this assignment.")]
    [SerializeField] private bool respectPreserveAuthoredAppearance = true;

    [Header("Optional References")]
    [SerializeField] private NpcSocialActor socialActor;
    [SerializeField] private NpcDialogueAgent dialogueAgent;
    [SerializeField] private NpcSkierAppearanceGenerator appearanceGenerator;
    [SerializeField] private NpcSkierProfile skierProfile;
    [SerializeField] private NpcAppearancePresetApplier appearanceApplier;
    [SerializeField] private NpcIdentity identity;

    [Header("Debug")]
    [SerializeField] private bool logDebug;

    public NpcSocialGroupSO SocialGroup => socialGroup;

    private IEnumerator Start()
    {
        if (!applyOnStart)
            yield break;

        for (int i = 0; i < applyDelayFrames; i++)
            yield return null;

        ApplyAssignedSocialGroup();
    }

    [ContextMenu("Apply Assigned Social Group")]
    public void ApplyAssignedSocialGroup()
    {
        CacheReferences();

        if (socialGroup == null)
        {
            Log("No social group assigned.");
            return;
        }

        if (addSocialTags)
            ApplyTags();

        if (applyDialogueBank)
            ApplyDialogueBank();

        if (applyAppearance)
            ApplyAppearance();

        Log($"Applied social group '{socialGroup.DisplayName}'.");
    }

    private void ApplyTags()
    {
        if (socialActor == null)
        {
            Log("Cannot apply social tags because no NpcSocialActor was found.");
            return;
        }

        socialActor.AddSocialTags(socialGroup.SocialTags);
    }

    private void ApplyDialogueBank()
    {
        if (dialogueAgent == null || socialGroup.DialogueBanks == null || socialGroup.DialogueBanks.Count == 0)
            return;

        NpcDialogueBankSO bank = PickRandomValid(socialGroup.DialogueBanks);
        if (bank != null)
            dialogueAgent.SetDialogueBank(bank);
    }

    private void ApplyAppearance()
    {
        if (respectPreserveAuthoredAppearance &&
            identity != null &&
            identity.IsAuthored &&
            identity.PreserveAuthoredAppearance)
        {
            Log("Skipped appearance because this authored NPC preserves authored appearance.");
            return;
        }

        if (socialGroup.AppearanceProfile != null)
        {
            if (appearanceGenerator == null)
            {
                Debug.LogWarning(
                    $"[{nameof(NpcSocialGroupAssignment)}] '{name}' cannot apply social appearance because no {nameof(NpcSkierAppearanceGenerator)} was found.",
                    this);
                return;
            }

            appearanceGenerator.ApplyRandomAppearance(skierProfile, socialGroup.AppearanceProfile);
            return;
        }

        ApplyLegacyPresetFallback();
    }

    private void ApplyLegacyPresetFallback()
    {
        if (socialGroup.AppearancePresets == null || socialGroup.AppearancePresets.Count == 0)
            return;

        NpcAppearancePresetSO preset = PickRandomValid(socialGroup.AppearancePresets);
        if (preset == null)
            return;

        if (appearanceApplier == null)
            appearanceApplier = GetComponent<NpcAppearancePresetApplier>() ?? gameObject.AddComponent<NpcAppearancePresetApplier>();

        appearanceApplier.ApplyData(NpcAppearancePresetApplier.BuildDataFromPreset(preset));
    }

    private void CacheReferences()
    {
        if (socialActor == null)
            socialActor = GetComponent<NpcSocialActor>() ?? GetComponentInChildren<NpcSocialActor>(true);

        if (dialogueAgent == null)
            dialogueAgent = socialActor != null && socialActor.DialogueAgent != null
                ? socialActor.DialogueAgent
                : GetComponent<NpcDialogueAgent>() ?? GetComponentInChildren<NpcDialogueAgent>(true);

        if (appearanceGenerator == null)
            appearanceGenerator = GetComponent<NpcSkierAppearanceGenerator>() ?? GetComponentInChildren<NpcSkierAppearanceGenerator>(true);

        if (skierProfile == null)
            skierProfile = GetComponent<NpcSkierProfile>() ?? GetComponentInChildren<NpcSkierProfile>(true);

        if (appearanceApplier == null)
            appearanceApplier = GetComponent<NpcAppearancePresetApplier>() ?? GetComponentInChildren<NpcAppearancePresetApplier>(true);

        if (identity == null)
            identity = GetComponent<NpcIdentity>() ?? GetComponentInChildren<NpcIdentity>(true);
    }

    private static T PickRandomValid<T>(IReadOnlyList<T> values) where T : Object
    {
        if (values == null || values.Count == 0)
            return null;

        List<T> valid = null;
        for (int i = 0; i < values.Count; i++)
        {
            if (values[i] == null)
                continue;

            valid ??= new List<T>();
            valid.Add(values[i]);
        }

        return valid != null && valid.Count > 0
            ? valid[Random.Range(0, valid.Count)]
            : null;
    }

    private void Log(string message)
    {
        if (logDebug)
            Debug.Log($"[{nameof(NpcSocialGroupAssignment)}] {name}: {message}", this);
    }
}