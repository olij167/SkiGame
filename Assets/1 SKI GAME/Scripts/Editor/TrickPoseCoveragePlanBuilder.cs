using System.Collections.Generic;
using UnityEngine;

public sealed class TrickPoseCoverageBuildResult
{
    public readonly List<TrickPoseCoverageSlot> slots = new List<TrickPoseCoverageSlot>();
    public int excludedCount;
}

public static class TrickPoseCoveragePlanBuilder
{
    public static TrickPoseCoverageBuildResult BuildSlots(TrickPoseCoveragePlanSO plan, SkiController controller)
    {
        TrickPoseCoverageBuildResult result = new TrickPoseCoverageBuildResult();
        if (plan == null)
            return result;

        List<bool> airborneValues = BuildBoolOptions(plan.includeAirborne, plan.includeGrounded, true);
        List<bool> poseHeldValues = BuildBoolOptions(plan.includePoseHeld, plan.includePoseReleased, true);
        List<SkiController.AerialPoseFamily> poseFamilies = plan.usePoseFamily && plan.poseFamilies != null && plan.poseFamilies.Count > 0
            ? plan.poseFamilies
            : new List<SkiController.AerialPoseFamily> { SkiController.AerialPoseFamily.None };
        List<SkiController.AerialPoseShape> poseShapes = plan.usePoseShape && plan.poseShapes != null && plan.poseShapes.Count > 0
            ? plan.poseShapes
            : new List<SkiController.AerialPoseShape> { SkiController.AerialPoseShape.None };
        List<TrickPoseVerticalOrientationRequirement> verticalOrientations = plan.useVerticalOrientation && plan.verticalOrientations != null && plan.verticalOrientations.Count > 0
            ? plan.verticalOrientations
            : new List<TrickPoseVerticalOrientationRequirement> { TrickPoseVerticalOrientationRequirement.Any };
        List<TrickPoseHorizontalOrientationRequirement> horizontalOrientations = plan.useHorizontalOrientation && plan.horizontalOrientations != null && plan.horizontalOrientations.Count > 0
            ? plan.horizontalOrientations
            : new List<TrickPoseHorizontalOrientationRequirement> { TrickPoseHorizontalOrientationRequirement.Any };
        List<TrickPoseTravelFacingRequirement> travelFacings = plan.useTravelFacing && plan.travelFacings != null && plan.travelFacings.Count > 0
            ? plan.travelFacings
            : new List<TrickPoseTravelFacingRequirement> { TrickPoseTravelFacingRequirement.Any };
        List<TrickPoseMotionStateRequirement> motionStates = plan.useMotionState && plan.motionStates != null && plan.motionStates.Count > 0
            ? plan.motionStates
            : new List<TrickPoseMotionStateRequirement> { TrickPoseMotionStateRequirement.Any };

        foreach (bool airborne in airborneValues)
        {
            foreach (bool poseHeld in poseHeldValues)
            {
                foreach (SkiController.AerialPoseFamily poseFamily in poseFamilies)
                {
                    foreach (SkiController.AerialPoseShape poseShape in poseShapes)
                    {
                        foreach (TrickPoseVerticalOrientationRequirement vertical in verticalOrientations)
                        {
                            foreach (TrickPoseHorizontalOrientationRequirement horizontal in horizontalOrientations)
                            {
                                foreach (TrickPoseTravelFacingRequirement travelFacing in travelFacings)
                                {
                                    foreach (TrickPoseMotionStateRequirement motion in motionStates)
                                    {
                                        TrickPoseCoverageSlot slot = CreateSlot(
                                            controller,
                                            airborne,
                                            poseHeld,
                                            poseFamily,
                                            poseShape,
                                            vertical,
                                            horizontal,
                                            travelFacing,
                                            motion);

                                        if (TryFindExclusion(plan, slot, out TrickPoseCoverageExclusionRule exclusion))
                                        {
                                            slot.excluded = true;
                                            slot.exclusionReason = exclusion != null ? exclusion.label : "Excluded";
                                            slot.validationStatus = TrickPoseCoverageSlotValidationStatus.Excluded;
                                            result.excludedCount++;
                                        }

                                        result.slots.Add(slot);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        return result;
    }

    public static void RefreshSlotAssignments(TrickPoseProfileSO profile, List<TrickPoseCoverageSlot> slots, int nearestEntryCount)
    {
        if (slots == null)
            return;

        for (int i = 0; i < slots.Count; i++)
        {
            TrickPoseCoverageSlot slot = slots[i];
            if (slot == null)
                continue;

            if (slot.excluded)
            {
                slot.validationStatus = TrickPoseCoverageSlotValidationStatus.Excluded;
                slot.summary = string.IsNullOrWhiteSpace(slot.exclusionReason) ? "Excluded from intended coverage." : slot.exclusionReason;
                continue;
            }

            if (slot.representativeContext == null)
            {
                slot.summary = "Representative context is missing. Rebuild or repair the slot atlas.";
                continue;
            }

            slot.assignedEntry = null;
            slot.assignedEntryIndex = -1;
            slot.assignmentState = TrickPoseCoverageAssignmentState.Unassigned;
            slot.candidateEntryIndices.Clear();
            slot.ambiguousEntryIndices.Clear();
            slot.suppressedEntryIndices.Clear();
            slot.candidateEntryLabels.Clear();

            TrickPoseCoverageContextEvaluation evaluation = TrickPoseCoverageAnalyzer.EvaluateContext(profile, slot.representativeContext, nearestEntryCount);
            slot.summary = evaluation != null ? evaluation.Summary : "No evaluation available.";
            if (evaluation == null)
            {
                slot.validationStatus = TrickPoseCoverageSlotValidationStatus.Gap;
                continue;
            }

            slot.validationStatus = evaluation.classification switch
            {
                TrickPoseCoverageClassification.Covered => TrickPoseCoverageSlotValidationStatus.Covered,
                TrickPoseCoverageClassification.Ambiguous => TrickPoseCoverageSlotValidationStatus.Ambiguous,
                TrickPoseCoverageClassification.Suppressed => TrickPoseCoverageSlotValidationStatus.Suppressed,
                _ => TrickPoseCoverageSlotValidationStatus.Gap
            };

            AppendEntryIndices(profile, evaluation.matches, slot.candidateEntryIndices, slot.candidateEntryLabels);
            AppendEntryIndices(profile, evaluation.ambiguousEntries, slot.ambiguousEntryIndices, null);
            AppendEntryIndices(profile, evaluation.suppressedEntries, slot.suppressedEntryIndices, null);

            if (evaluation.bestEntry == null)
                continue;

            slot.assignedEntry = evaluation.bestEntry;
            slot.assignedEntryIndex = profile != null && profile.entries != null ? profile.entries.IndexOf(evaluation.bestEntry) : -1;
            slot.assignmentState = TrickPoseCoverageAssignmentUtility.IsPlaceholderEntry(evaluation.bestEntry)
                ? TrickPoseCoverageAssignmentState.AssignedToPlaceholder
                : TrickPoseCoverageAssignmentState.AssignedToExistingEntry;
        }
    }

    public static bool RefreshSlotRepresentativeContexts(TrickPoseCoveragePlanSO plan, SkiController controller, List<TrickPoseCoverageSlot> slots)
    {
        if (slots == null)
            return false;

        bool rebuiltAny = false;
        for (int i = 0; i < slots.Count; i++)
        {
            TrickPoseCoverageSlot slot = slots[i];
            if (slot == null)
                continue;

            RebuildDerivedSlotState(plan, controller, slot, out bool rebuiltSlot);
            rebuiltAny |= rebuiltSlot;
        }

        return rebuiltAny;
    }

    public static List<TrickPoseCoverageSlot> CollectSlotsForEntry(List<TrickPoseCoverageSlot> slots, TrickPoseEntry entry, bool primaryOnly)
    {
        List<TrickPoseCoverageSlot> result = new List<TrickPoseCoverageSlot>();
        if (slots == null || entry == null)
            return result;

        TrickPoseProfileSO tempProfile = ScriptableObject.CreateInstance<TrickPoseProfileSO>();
        tempProfile.entries.Add(entry);

        for (int i = 0; i < slots.Count; i++)
        {
            TrickPoseCoverageSlot slot = slots[i];
            if (slot == null || slot.excluded || slot.representativeContext == null)
                continue;

            if (primaryOnly && slot.assignedEntry != entry)
                continue;

            if (TrickPoseEditorPreviewUtility.EvaluateMatchingEntries(tempProfile, slot.representativeContext).Count > 0)
                result.Add(slot);
        }

        Object.DestroyImmediate(tempProfile);
        return result;
    }

    public static TrickPoseEditorPreviewContext BuildRepresentativeContext(SkiController controller, TrickPoseCoverageSlot slot)
    {
        if (slot == null)
            return null;

        bool hasOrientationRotation = TrickPoseOrientationUtility.TryBuildPresentationEuler(
            slot.verticalOrientation,
            slot.horizontalOrientation,
            slot.travelFacing,
            out Vector3 orientationEuler);

        TrickPoseInfluencePreviewState state = new TrickPoseInfluencePreviewState
        {
            poseInputHeld = slot.poseHeld,
            airborne = slot.airborne,
            leftInput = slot.poseFamily == SkiController.AerialPoseFamily.Left || slot.poseFamily == SkiController.AerialPoseFamily.Spread,
            rightInput = slot.poseFamily == SkiController.AerialPoseFamily.Right || slot.poseFamily == SkiController.AerialPoseFamily.Spread,
            tuckInput = slot.poseShape == SkiController.AerialPoseShape.Compact,
            leanInput = slot.poseShape == SkiController.AerialPoseShape.Driving
                ? 0.65f
                : slot.poseShape == SkiController.AerialPoseShape.LaidOut ? -0.65f : 0f,
            rising = slot.motionState == TrickPoseMotionStateRequirement.Rising,
            diving = slot.motionState == TrickPoseMotionStateRequirement.Diving,
            manualRotationActive = hasOrientationRotation,
            manualRotationEuler = orientationEuler,
            yawAngularVelocity = 0f,
            pitchAngularVelocity = slot.motionState == TrickPoseMotionStateRequirement.Diving ? 120f : slot.motionState == TrickPoseMotionStateRequirement.Rising ? -90f : 0f,
            rollAngularVelocity = 0f
        };

        TrickPoseEditorPreviewContext context = TrickPoseEditorPreviewUtility.BuildFromInfluences(controller, state) ?? new TrickPoseEditorPreviewContext();
        context.source = TrickPosePreviewContextSource.CoveragePreview;
        context.poseInputHeld = slot.poseHeld;
        context.airborne = slot.airborne;
        context.grounded = !slot.airborne;
        context.poseFamily = slot.poseFamily;
        context.poseShape = slot.poseShape;
        context.verticalOrientation = slot.verticalOrientation;
        context.horizontalOrientation = slot.horizontalOrientation;
        context.travelFacing = slot.travelFacing;
        context.motionState = slot.motionState;
        context.orientationModifier = slot.orientationModifier;
        context.spinDirectionSign = SignFromValue(context.yawAngularVelocity);
        context.flipDirectionSign = SignFromValue(context.pitchAngularVelocity);
        context.poseName = TrickPoseEditorPreviewUtility.ResolvePoseName(slot.poseFamily, slot.poseShape);
        context.totalAngularSpeed = new Vector3(context.pitchAngularVelocity, context.yawAngularVelocity, context.rollAngularVelocity).magnitude;
        return context;
    }

    public static void RebuildDerivedSlotState(TrickPoseCoveragePlanSO plan, SkiController controller, TrickPoseCoverageSlot slot, out bool rebuilt)
    {
        rebuilt = false;
        if (slot == null)
            return;

        string slotId = BuildSlotId(
            slot.airborne,
            slot.poseHeld,
            slot.poseFamily,
            slot.poseShape,
            slot.verticalOrientation,
            slot.horizontalOrientation,
            slot.travelFacing,
            slot.motionState);
        if (!string.Equals(slot.slotId, slotId, System.StringComparison.Ordinal))
        {
            slot.slotId = slotId;
            rebuilt = true;
        }

        string shortLabel = TrickPoseAuthoredStateFormatter.Format(slot);
        if (!string.Equals(slot.shortLabel, shortLabel, System.StringComparison.Ordinal))
        {
            slot.shortLabel = shortLabel;
            rebuilt = true;
        }

        SkiController.AerialOrientationModifier orientationModifier = TrickPoseOrientationUtility.ToLegacyOrientationModifier(slot.verticalOrientation, slot.horizontalOrientation, slot.motionState);
        if (slot.orientationModifier != orientationModifier)
        {
            slot.orientationModifier = orientationModifier;
            rebuilt = true;
        }

        TrickPoseEditorPreviewContext representativeContext = BuildRepresentativeContext(controller, slot);
        if (slot.representativeContext == null || representativeContext == null || !MatchesRepresentativeContext(slot.representativeContext, representativeContext))
        {
            slot.representativeContext = representativeContext;
            rebuilt = true;
        }

        bool isExcluded = TryFindExclusion(plan, slot, out TrickPoseCoverageExclusionRule exclusion);
        if (slot.excluded != isExcluded)
        {
            slot.excluded = isExcluded;
            rebuilt = true;
        }

        string exclusionReason = isExcluded ? exclusion != null ? exclusion.label : "Excluded" : string.Empty;
        if (!string.Equals(slot.exclusionReason, exclusionReason, System.StringComparison.Ordinal))
        {
            slot.exclusionReason = exclusionReason;
            rebuilt = true;
        }

        if (slot.excluded)
        {
            slot.validationStatus = TrickPoseCoverageSlotValidationStatus.Excluded;
            if (string.IsNullOrWhiteSpace(slot.summary))
                slot.summary = string.IsNullOrWhiteSpace(slot.exclusionReason) ? "Excluded from intended coverage." : slot.exclusionReason;
        }
    }

    private static float GetPitchValue(TrickPoseCoverageSlot slot)
    {
        float fallback = slot.orientationModifier switch
        {
            SkiController.AerialOrientationModifier.Inverted => -220f,
            SkiController.AerialOrientationModifier.ChestDown => 120f,
            SkiController.AerialOrientationModifier.ChestUp => -120f,
            SkiController.AerialOrientationModifier.Diving => 120f,
            SkiController.AerialOrientationModifier.Rising => -90f,
            _ => 0f
        };

        if (slot.flipDirection == TrickPoseFlipDirectionRequirement.Frontflip)
            fallback = Mathf.Max(fallback, 220f);
        else if (slot.flipDirection == TrickPoseFlipDirectionRequirement.Backflip)
            fallback = Mathf.Min(fallback, -220f);

        return GetAxisValue(slot.pitchAngularVelocityRange, slot.flipDirection == TrickPoseFlipDirectionRequirement.Any ? 0 : (int)slot.flipDirection, fallback);
    }

    private static float GetAxisValue(TrickPoseAngularVelocityRange range, int forcedSign, float fallback)
    {
        if (range.enabled)
        {
            float mid = Average(range.GetSortedRange());
            if (forcedSign != 0 && Mathf.Abs(mid) > 0.001f)
                mid = Mathf.Abs(mid) * forcedSign;
            return mid;
        }

        if (forcedSign != 0 && Mathf.Abs(fallback) < 0.001f)
            return 220f * forcedSign;
        return fallback;
    }

    private static float Average(Vector2 range)
    {
        return (range.x + range.y) * 0.5f;
    }

    private static int SignFromValue(float value)
    {
        return value > 1f ? 1 : value < -1f ? -1 : 0;
    }

    private static void AppendEntryIndices(TrickPoseProfileSO profile, List<TrickPoseEntry> entries, List<int> indices, List<string> labels)
    {
        if (profile == null || profile.entries == null || entries == null)
            return;

        for (int i = 0; i < entries.Count; i++)
        {
            int index = profile.entries.IndexOf(entries[i]);
            if (index < 0)
                continue;
            indices.Add(index);
            labels?.Add(entries[i].GetSummary());
        }
    }

    private static TrickPoseCoverageSlot CreateSlot(
    SkiController controller,
    bool airborne,
    bool poseHeld,
    SkiController.AerialPoseFamily poseFamily,
    SkiController.AerialPoseShape poseShape,
    TrickPoseVerticalOrientationRequirement vertical,
    TrickPoseHorizontalOrientationRequirement horizontal,
    TrickPoseTravelFacingRequirement travelFacing,
    TrickPoseMotionStateRequirement motion)
    {
        TrickPoseCoverageSlot slot = new TrickPoseCoverageSlot
        {
            airborne = airborne,
            poseHeld = poseHeld,
            poseFamily = poseFamily,
            poseShape = poseShape,
            verticalOrientation = vertical,
            horizontalOrientation = horizontal,
            travelFacing = travelFacing,
            motionState = motion,
            orientationModifier = TrickPoseOrientationUtility.ToLegacyOrientationModifier(vertical, horizontal, motion)
        };

        slot.slotId = BuildSlotId(airborne, poseHeld, poseFamily, poseShape, vertical, horizontal, travelFacing, motion);
        slot.shortLabel = TrickPoseAuthoredStateFormatter.Format(slot);
        slot.representativeContext = BuildRepresentativeContext(controller, slot);
        slot.summary = "Unassigned coverage slot.";
        return slot;
    }

    private static string BuildSlotId(
    bool airborne,
    bool poseHeld,
    SkiController.AerialPoseFamily poseFamily,
    SkiController.AerialPoseShape poseShape,
    TrickPoseVerticalOrientationRequirement vertical,
    TrickPoseHorizontalOrientationRequirement horizontal,
    TrickPoseTravelFacingRequirement travelFacing,
    TrickPoseMotionStateRequirement motion)
    {
        return string.Join("|",
            airborne ? "Air" : "Ground",
            poseHeld ? "PoseHeld" : "PoseOff",
            poseFamily,
            poseShape,
            vertical,
            horizontal,
            travelFacing,
            motion);
    }

    private static bool MatchesRepresentativeContext(TrickPoseEditorPreviewContext a, TrickPoseEditorPreviewContext b)
    {
        if (a == null || b == null)
            return a == b;

        return a.poseInputHeld == b.poseInputHeld &&
               a.airborne == b.airborne &&
               a.poseFamily == b.poseFamily &&
               a.poseShape == b.poseShape &&
               a.verticalOrientation == b.verticalOrientation &&
               a.horizontalOrientation == b.horizontalOrientation &&
               a.travelFacing == b.travelFacing &&
               
               a.motionState == b.motionState &&
               a.orientationModifier == b.orientationModifier &&
               a.entryEulerAngles == b.entryEulerAngles &&
               a.presentationRotationEuler == b.presentationRotationEuler;
    }

    private static string FormatOrientation(SkiController.AerialOrientationModifier orientation)
    {
        string label = SkiController.GetAerialOrientationModifierLabel(orientation);
        return string.IsNullOrWhiteSpace(label) ? "None" : label;
    }

    private static TrickPoseAngularVelocityRange ToRange(TrickPoseCoverageAngularBucket bucket)
    {
        if (bucket == null || string.IsNullOrWhiteSpace(bucket.id))
            return default;

        return new TrickPoseAngularVelocityRange
        {
            enabled = true,
            range = bucket.GetSortedRange()
        };
    }

    private static bool TryFindExclusion(TrickPoseCoveragePlanSO plan, TrickPoseCoverageSlot slot, out TrickPoseCoverageExclusionRule exclusion)
    {
        exclusion = null;
        if (plan == null || plan.exclusions == null)
            return false;

        for (int i = 0; i < plan.exclusions.Count; i++)
        {
            TrickPoseCoverageExclusionRule rule = plan.exclusions[i];
            if (rule == null)
                continue;

            if (rule.matchAirborneState && rule.airborne != slot.airborne)
                continue;
            if (rule.matchPoseHeldState && rule.poseHeld != slot.poseHeld)
                continue;
            if (rule.poseFamily != SkiController.AerialPoseFamily.None && rule.poseFamily != slot.poseFamily)
                continue;
            if (rule.poseShape != SkiController.AerialPoseShape.None && rule.poseShape != slot.poseShape)
                continue;
            if (rule.verticalOrientation != TrickPoseVerticalOrientationRequirement.Any && rule.verticalOrientation != slot.verticalOrientation)
                continue;
            if (rule.horizontalOrientation != TrickPoseHorizontalOrientationRequirement.Any && rule.horizontalOrientation != slot.horizontalOrientation)
                continue;
            if (rule.travelFacing != TrickPoseTravelFacingRequirement.Any && rule.travelFacing != slot.travelFacing)
                continue;
          
            if (rule.motionState != TrickPoseMotionStateRequirement.Any && rule.motionState != slot.motionState)
                continue;
            if (rule.orientationModifier != SkiController.AerialOrientationModifier.None && rule.orientationModifier != slot.orientationModifier)
                continue;
            if (rule.spinDirection != TrickPoseSpinDirectionRequirement.Any && rule.spinDirection != slot.spinDirection)
                continue;
            if (rule.flipDirection != TrickPoseFlipDirectionRequirement.Any && rule.flipDirection != slot.flipDirection)
                continue;
            if (!string.IsNullOrWhiteSpace(rule.yawBucketId) && rule.yawBucketId != slot.yawBucketId)
                continue;
            if (!string.IsNullOrWhiteSpace(rule.pitchBucketId) && rule.pitchBucketId != slot.pitchBucketId)
                continue;
            if (!string.IsNullOrWhiteSpace(rule.rollBucketId) && rule.rollBucketId != slot.rollBucketId)
                continue;
            if (!string.IsNullOrWhiteSpace(rule.totalSpeedBucketId) && rule.totalSpeedBucketId != slot.totalSpeedBucketId)
                continue;

            exclusion = rule;
            return true;
        }

        return false;
    }

    private static List<bool> BuildBoolOptions(bool first, bool second, bool defaultValue)
    {
        List<bool> values = new List<bool>();
        if (first)
            values.Add(defaultValue);
        if (second)
            values.Add(!defaultValue);
        if (values.Count == 0)
            values.Add(defaultValue);
        return values;
    }

    private static List<TrickPoseCoverageAngularBucket> BuildBucketOptions(bool enabled, List<TrickPoseCoverageAngularBucket> source)
    {
        if (!enabled || source == null || source.Count == 0)
            return new List<TrickPoseCoverageAngularBucket> { null };
        return source;
    }

    
}
