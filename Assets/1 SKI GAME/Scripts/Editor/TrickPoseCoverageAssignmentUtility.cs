using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class TrickPoseCoverageEntryCandidate
{
    public TrickPoseEntry entry;
    public int entryIndex;
    public int failCount;
    public int specificity;
}

public static class TrickPoseCoverageAssignmentUtility
{
    private const string PlaceholderPrefix = "[Placeholder] ";

    public static bool IsPlaceholderEntry(TrickPoseEntry entry)
    {
        return entry != null && entry.isCoveragePlaceholder;
    }

    public static void ApplySlotConditionsToEntry(TrickPoseCoverageSlot slot, TrickPoseEntry entry)
    {
        if (slot == null || entry == null)
            return;

        entry.requireAirborne = slot.airborne ? TrickPoseBoolRequirement.True : TrickPoseBoolRequirement.False;
        entry.requirePoseButtonHeld = slot.poseHeld ? TrickPoseBoolRequirement.True : TrickPoseBoolRequirement.False;
        entry.requiredPoseFamily = slot.poseFamily;
        entry.requiredPoseShape = slot.poseShape;
        entry.requiredVerticalOrientation = slot.verticalOrientation;
        entry.requiredHorizontalOrientation = slot.horizontalOrientation;
        entry.requiredTravelFacing = slot.travelFacing;
        entry.requiredMotionState = slot.motionState;
        entry.requiredOrientationModifier = SkiController.AerialOrientationModifier.None;
        entry.useAdvancedModifierConditions = false;
        entry.requiredSpinDirection = TrickPoseSpinDirectionRequirement.Any;
        entry.requiredFlipDirection = TrickPoseFlipDirectionRequirement.Any;
        entry.yawAngularVelocityRange = default;
        entry.pitchAngularVelocityRange = default;
        entry.rollAngularVelocityRange = default;
        entry.totalAngularSpeedRange = default;
        entry.entryPitchAngleRange = default;
        entry.entryYawAngleRange = default;
        entry.entryRollAngleRange = default;
        entry.requiredPoseName = slot.poseFamily != SkiController.AerialPoseFamily.None && slot.poseShape != SkiController.AerialPoseShape.None
            ? TrickPoseEditorPreviewUtility.ResolvePoseName(slot.poseFamily, slot.poseShape)
            : string.Empty;
        entry.coverageSlotId = slot.slotId;
    }

    public static TrickPoseEntry CreatePlaceholderEntry(TrickPoseCoverageSlot slot)
    {
        TrickPoseEntry entry = new TrickPoseEntry
        {
            isCoveragePlaceholder = true,
            displayName = $"{PlaceholderPrefix}{slot.shortLabel}",
            overridePoseLabel = "Coverage Placeholder"
        };

        ApplySlotConditionsToEntry(slot, entry);
        return entry;
    }

    public static int CreateEntryFromSlot(TrickPoseProfileSO profile, TrickPoseCoverageSlot slot, bool placeholder)
    {
        if (profile == null || profile.entries == null || slot == null)
            return -1;

        Undo.RecordObject(profile, placeholder ? "Create Coverage Placeholder" : "Create Trick Pose From Slot");
        TrickPoseEntry entry = placeholder ? CreatePlaceholderEntry(slot) : CreateAuthoredEntryFromSlot(slot);
        profile.entries.Add(entry);
        EditorUtility.SetDirty(profile);
        return profile.entries.Count - 1;
    }

    public static int DuplicateEntryForSlot(TrickPoseProfileSO profile, TrickPoseCoverageSlot slot, int sourceEntryIndex)
    {
        if (profile == null || profile.entries == null || slot == null || sourceEntryIndex < 0 || sourceEntryIndex >= profile.entries.Count)
            return -1;

        TrickPoseEntry source = profile.entries[sourceEntryIndex];
        if (source == null)
            return -1;

        Undo.RecordObject(profile, "Clone Entry For Coverage Slot");
        TrickPoseEntry copy = JsonUtility.FromJson<TrickPoseEntry>(JsonUtility.ToJson(source));
        copy.displayName = $"{source.displayName} -> {slot.shortLabel}";
        copy.isCoveragePlaceholder = false;
        copy.coverageSlotId = string.Empty;
        ApplySlotConditionsToEntry(slot, copy);
        profile.entries.Add(copy);
        EditorUtility.SetDirty(profile);
        return profile.entries.Count - 1;
    }

    private static TrickPoseEntry CreateAuthoredEntryFromSlot(TrickPoseCoverageSlot slot)
    {
        TrickPoseEntry entry = new TrickPoseEntry
        {
            enabled = true,
            displayName = slot.shortLabel,
            overallWeight = 1f,
            blendInSpeed = 8f,
            blendOutSpeed = 8f,
            snapOnPreview = true
        };

        ApplySlotConditionsToEntry(slot, entry);
        return entry;
    }

    public static void CreatePlaceholdersForGaps(TrickPoseProfileSO profile, List<TrickPoseCoverageSlot> slots)
    {
        if (profile == null || slots == null)
            return;

        Undo.RecordObject(profile, "Create Coverage Placeholders");
        for (int i = 0; i < slots.Count; i++)
        {
            TrickPoseCoverageSlot slot = slots[i];
            if (slot == null || slot.excluded || slot.validationStatus != TrickPoseCoverageSlotValidationStatus.Gap || slot.assignedEntry != null)
                continue;
            profile.entries.Add(CreatePlaceholderEntry(slot));
        }
        EditorUtility.SetDirty(profile);
    }

    public static bool AssignEntryToSlot(TrickPoseProfileSO profile, TrickPoseCoverageSlot slot, int entryIndex)
    {
        if (profile == null || profile.entries == null || slot == null || entryIndex < 0 || entryIndex >= profile.entries.Count)
            return false;

        TrickPoseEntry entry = profile.entries[entryIndex];
        if (entry == null)
            return false;

        Undo.RecordObject(profile, "Assign Entry To Coverage Slot");
        entry.isCoveragePlaceholder = false;
        ApplySlotConditionsToEntry(slot, entry);
        if (!string.IsNullOrWhiteSpace(entry.displayName) && entry.displayName.StartsWith(PlaceholderPrefix))
            entry.displayName = entry.displayName.Substring(PlaceholderPrefix.Length);
        EditorUtility.SetDirty(profile);
        return true;
    }

    public static bool ReplacePlaceholderWithEntry(TrickPoseProfileSO profile, TrickPoseCoverageSlot slot, int entryIndex)
    {
        if (profile == null || profile.entries == null || slot == null || slot.assignedEntry == null || !slot.assignedEntry.isCoveragePlaceholder)
            return false;
        if (entryIndex < 0 || entryIndex >= profile.entries.Count || profile.entries[entryIndex] == null)
            return false;

        TrickPoseEntry replacement = profile.entries[entryIndex];
        int placeholderIndex = profile.entries.IndexOf(slot.assignedEntry);
        if (placeholderIndex < 0)
            return false;
        if (replacement == slot.assignedEntry)
            return false;

        Undo.RecordObject(profile, "Replace Coverage Placeholder");
        replacement.isCoveragePlaceholder = false;
        ApplySlotConditionsToEntry(slot, replacement);
        profile.entries.RemoveAt(placeholderIndex);
        EditorUtility.SetDirty(profile);
        return true;
    }

    public static List<TrickPoseCoverageEntryCandidate> ScoreCandidates(TrickPoseProfileSO profile, TrickPoseCoverageSlot slot)
    {
        List<TrickPoseCoverageEntryCandidate> candidates = new List<TrickPoseCoverageEntryCandidate>();
        if (profile == null || profile.entries == null || slot == null || slot.representativeContext == null)
            return candidates;

        for (int i = 0; i < profile.entries.Count; i++)
        {
            TrickPoseEntry entry = profile.entries[i];
            if (entry == null || !entry.enabled)
                continue;

            candidates.Add(new TrickPoseCoverageEntryCandidate
            {
                entry = entry,
                entryIndex = i,
                failCount = TrickPoseEditorPreviewUtility.BuildFailReasons(entry, slot.representativeContext, 6).Count,
                specificity = entry.GetSpecificityScore()
            });
        }

        candidates.Sort((a, b) =>
        {
            int failCompare = a.failCount.CompareTo(b.failCount);
            if (failCompare != 0)
                return failCompare;
            return b.specificity.CompareTo(a.specificity);
        });
        return candidates;
    }
}
