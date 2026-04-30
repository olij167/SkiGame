using UnityEngine;

public enum TrickPosePreviewMode
{
    Sequence = 0,
    Influence = 1,
    Coverage = 2
}

public enum TrickPosePreviewContextSource
{
    None = 0,
    MatchPreview = 1,
    SequencePreview = 2,
    InfluencePreview = 3,
    CoveragePreview = 4
}

[System.Serializable]
public sealed class TrickPoseInfluencePreviewState
{
    public bool autoInfluence;
    public bool poseInputHeld = true;
    public bool tuckInput;
    public bool leftInput;
    public bool rightInput;
    public bool airborne = true;
    public float leanInput;
    public bool rising;
    public bool diving;
    public bool manualRotationActive;
    public Vector3 manualRotationEuler;
    public float yawAngularVelocity;
    public float pitchAngularVelocity;
    public float rollAngularVelocity;
}

[System.Serializable]
public sealed class TrickPoseEditorPreviewContext
{
    public TrickPosePreviewContextSource source;
    public TrickPoseEntry sourceEntry;
    public bool poseInputHeld;
    public bool tuckInput;
    public bool leftInput;
    public bool rightInput;
    public bool airborne;
    public bool grounded;
    public float leanInput;
    public bool rising;
    public bool diving;
    public Vector3 entryEulerAngles;
    public Vector3 presentationRotationEuler;
    public bool hasPresentationRotation;
    public float yawAngularVelocity;
    public float pitchAngularVelocity;
    public float rollAngularVelocity;
    public float totalAngularSpeed;
    public int spinDirectionSign;
    public int flipDirectionSign;
    public SkiController.AerialPoseFamily poseFamily;
    public SkiController.AerialPoseShape poseShape;
    public SkiController.AerialOrientationModifier orientationModifier;
    public TrickPoseVerticalOrientationRequirement verticalOrientation;
    public TrickPoseHorizontalOrientationRequirement horizontalOrientation;
    public TrickPoseTravelFacingRequirement travelFacing;
    
    public TrickPoseMotionStateRequirement motionState;
    public string poseName;

    public bool HasEntryConditions => sourceEntry != null;
}

public sealed class TrickPoseEntryConditionStatus
{
    public string label;
    public string status;
}

public static class TrickPoseEditorPreviewUtility
{
    private const float DirectionThreshold = 1f;

    public static TrickPoseEditorPreviewContext BuildFromInfluences(SkiController controller, TrickPoseInfluencePreviewState state)
    {
        if (state == null)
            return null;

        TrickPoseEditorPreviewContext context = new TrickPoseEditorPreviewContext
        {
            source = TrickPosePreviewContextSource.InfluencePreview,
            poseInputHeld = state.poseInputHeld,
            tuckInput = state.tuckInput,
            leftInput = state.leftInput,
            rightInput = state.rightInput,
            airborne = state.airborne,
            grounded = !state.airborne,
            leanInput = state.leanInput,
            rising = state.rising && state.airborne,
            diving = state.diving && state.airborne,
            entryEulerAngles = NormalizeEulerAngles(state.manualRotationEuler),
            presentationRotationEuler = state.manualRotationEuler,
            hasPresentationRotation = state.manualRotationActive,
            yawAngularVelocity = state.yawAngularVelocity,
            pitchAngularVelocity = state.pitchAngularVelocity,
            rollAngularVelocity = state.rollAngularVelocity
        };

        FinalizeContext(controller, context, null, false);
        return context;
    }

    public static TrickPoseEditorPreviewContext BuildFromEntry(SkiController controller, TrickPoseEntry entry, TrickPosePreviewContextSource source, bool forceMatched)
    {
        if (entry == null)
            return null;

        bool useAdvancedModifiers = entry.useAdvancedModifierConditions;
        float currentYawVelocity = controller != null ? controller.CurrentYawAngularVelocity : 0f;
        float currentPitchVelocity = controller != null ? controller.CurrentPitchAngularVelocity : 0f;
        float currentRollVelocity = controller != null ? controller.CurrentRollAngularVelocity : 0f;
        Vector3 controllerEntryEuler = controller != null ? controller.EntryEulerAngles : Vector3.zero;
        bool hasAuthoredEntryRotation =
            entry.entryPitchAngleRange.enabled ||
            entry.entryYawAngleRange.enabled ||
            entry.entryRollAngleRange.enabled;
        Vector3 authoredOrientationEuler = Vector3.zero;
        bool hasAuthoredOrientationRotation = forceMatched && TrickPoseOrientationUtility.TryBuildPresentationEuler(
            entry.requiredVerticalOrientation,
            entry.requiredHorizontalOrientation,
            entry.requiredTravelFacing,
            out authoredOrientationEuler);
        Vector3 representativeFallback = forceMatched ? Vector3.zero : controllerEntryEuler;
        Vector3 entryEuler = forceMatched
            ? new Vector3(
                entry.entryPitchAngleRange.GetRepresentativeValue(representativeFallback.x),
                entry.entryYawAngleRange.GetRepresentativeValue(representativeFallback.y),
                entry.entryRollAngleRange.GetRepresentativeValue(representativeFallback.z))
            : controllerEntryEuler;
        Vector3 presentationEuler = hasAuthoredOrientationRotation
            ? authoredOrientationEuler
            : entryEuler;
        TrickPoseEditorPreviewContext context = new TrickPoseEditorPreviewContext
        {
            source = source,
            sourceEntry = entry,
            poseInputHeld = forceMatched
                ? RequiredOrDefault(entry.requirePoseButtonHeld, true)
                : controller != null && controller.IsPoseButtonHeld,
            tuckInput = false,
            leftInput = false,
            rightInput = false,
            airborne = forceMatched
                ? RequiredOrDefault(entry.requireAirborne, true)
                : controller != null && controller.IsAuthoredPoseAirborne,
            grounded = forceMatched
                ? !RequiredOrDefault(entry.requireAirborne, true)
                : controller != null && !controller.IsAuthoredPoseAirborne,
            rising = false,
            diving = false,
            entryEulerAngles = entryEuler,
            presentationRotationEuler = presentationEuler,
            hasPresentationRotation = forceMatched && (hasAuthoredEntryRotation || hasAuthoredOrientationRotation),
            yawAngularVelocity = useAdvancedModifiers
                ? GetRepresentativeAxisValue(entry.yawAngularVelocityRange, currentYawVelocity, entry.requiredSpinDirection == TrickPoseSpinDirectionRequirement.Any ? 0 : (int)entry.requiredSpinDirection)
                : currentYawVelocity,
            pitchAngularVelocity = useAdvancedModifiers
                ? GetRepresentativeAxisValue(entry.pitchAngularVelocityRange, currentPitchVelocity, entry.requiredFlipDirection == TrickPoseFlipDirectionRequirement.Any ? 0 : (int)entry.requiredFlipDirection)
                : currentPitchVelocity,
            rollAngularVelocity = useAdvancedModifiers
                ? GetRepresentativeAxisValue(entry.rollAngularVelocityRange, currentRollVelocity, 0)
                : currentRollVelocity
        };

        if (forceMatched)
        {
            context.poseFamily = entry.requiredPoseFamily != SkiController.AerialPoseFamily.None ? entry.requiredPoseFamily : GetCurrentPoseFamily(controller);
            context.poseShape = entry.requiredPoseShape != SkiController.AerialPoseShape.None ? entry.requiredPoseShape : GetCurrentPoseShape(controller);
            context.verticalOrientation = entry.requiredVerticalOrientation != TrickPoseVerticalOrientationRequirement.Any
                ? entry.requiredVerticalOrientation
                : GetCurrentVerticalOrientation(controller);
            context.horizontalOrientation = entry.requiredHorizontalOrientation != TrickPoseHorizontalOrientationRequirement.Any
                ? entry.requiredHorizontalOrientation
                : GetCurrentHorizontalOrientation(controller);
            context.travelFacing = entry.requiredTravelFacing != TrickPoseTravelFacingRequirement.Any
                ? entry.requiredTravelFacing
                : GetCurrentTravelFacing(controller);
            
            context.motionState = entry.requiredMotionState != TrickPoseMotionStateRequirement.Any
                ? entry.requiredMotionState
                : GetCurrentMotionState(controller);
            context.orientationModifier = entry.requiredOrientationModifier != SkiController.AerialOrientationModifier.None
                ? entry.requiredOrientationModifier
                : TrickPoseOrientationUtility.ToLegacyOrientationModifier(
                    context.verticalOrientation,
                    context.horizontalOrientation,
                    context.motionState);
            context.leftInput = context.poseFamily == SkiController.AerialPoseFamily.Left || context.poseFamily == SkiController.AerialPoseFamily.Spread;
            context.rightInput = context.poseFamily == SkiController.AerialPoseFamily.Right || context.poseFamily == SkiController.AerialPoseFamily.Spread;
            context.tuckInput = context.poseShape == SkiController.AerialPoseShape.Compact;
            context.leanInput = context.poseShape == SkiController.AerialPoseShape.Driving
                ? 0.65f
                : context.poseShape == SkiController.AerialPoseShape.LaidOut ? -0.65f : 0f;
            context.rising = context.motionState == TrickPoseMotionStateRequirement.Rising;
            context.diving = context.motionState == TrickPoseMotionStateRequirement.Diving;
        }

        FinalizeContext(controller, context, entry, forceMatched);
        return context;
    }

    public static TrickPoseEntry EvaluateBestMatchingEntry(TrickPoseProfileSO profile, TrickPoseEditorPreviewContext context)
    {
        if (profile == null || profile.entries == null || context == null)
            return null;

        TrickPoseEntry best = null;
        int bestPriority = int.MinValue;
        int bestSpecificity = int.MinValue;

        for (int i = 0; i < profile.entries.Count; i++)
        {
            TrickPoseEntry entry = profile.entries[i];
            if (entry == null || !entry.enabled || !Matches(entry, context))
                continue;

            int priority = entry.priority;
            int specificity = entry.GetSpecificityScore();
            if (priority > bestPriority || (priority == bestPriority && specificity > bestSpecificity))
            {
                best = entry;
                bestPriority = priority;
                bestSpecificity = specificity;
            }
        }

        return best;
    }

    public static TrickPoseEntry SelectBestMatchingEntry(System.Collections.Generic.IList<TrickPoseEntry> matches)
    {
        if (matches == null || matches.Count == 0)
            return null;

        TrickPoseEntry best = null;
        int bestPriority = int.MinValue;
        int bestSpecificity = int.MinValue;
        for (int i = 0; i < matches.Count; i++)
        {
            TrickPoseEntry entry = matches[i];
            if (entry == null)
                continue;

            int priority = entry.priority;
            int specificity = entry.GetSpecificityScore();
            if (priority > bestPriority || (priority == bestPriority && specificity > bestSpecificity))
            {
                best = entry;
                bestPriority = priority;
                bestSpecificity = specificity;
            }
        }

        return best;
    }

    public static System.Collections.Generic.List<TrickPoseEntry> EvaluateMatchingEntries(TrickPoseProfileSO profile, TrickPoseEditorPreviewContext context)
    {
        System.Collections.Generic.List<TrickPoseEntry> matches = new System.Collections.Generic.List<TrickPoseEntry>();
        if (profile == null || profile.entries == null || context == null)
            return matches;

        for (int i = 0; i < profile.entries.Count; i++)
        {
            TrickPoseEntry entry = profile.entries[i];
            if (entry != null && entry.enabled && Matches(entry, context))
                matches.Add(entry);
        }

        return matches;
    }

    public static System.Collections.Generic.List<TrickPoseEntryConditionStatus> BuildConditionStatuses(TrickPoseEntry entry, TrickPoseEditorPreviewContext context, bool includeLegacyAdvanced = false)
    {
        System.Collections.Generic.List<TrickPoseEntryConditionStatus> statuses = new System.Collections.Generic.List<TrickPoseEntryConditionStatus>();
        if (entry == null || context == null)
            return statuses;

        AddStatus(statuses, "Family", DescribeEnumCondition(entry.requiredPoseFamily, context.poseFamily, SkiController.AerialPoseFamily.None));
        AddStatus(statuses, "Shape", DescribeEnumCondition(entry.requiredPoseShape, context.poseShape, SkiController.AerialPoseShape.None));
        AddStatus(statuses, "Vertical Orientation", DescribeEnumCondition(entry.requiredVerticalOrientation, context.verticalOrientation, TrickPoseVerticalOrientationRequirement.Any));
        AddStatus(statuses, "Horizontal Orientation", DescribeEnumCondition(entry.requiredHorizontalOrientation, context.horizontalOrientation, TrickPoseHorizontalOrientationRequirement.Any));
        AddStatus(statuses, "Travel Facing", DescribeEnumCondition(entry.requiredTravelFacing, context.travelFacing, TrickPoseTravelFacingRequirement.Any));
        
        AddStatus(statuses, "Motion State", DescribeEnumCondition(entry.requiredMotionState, context.motionState, TrickPoseMotionStateRequirement.Any));
        AddStatus(statuses, "Airborne", DescribeBoolCondition(entry.requireAirborne, context.airborne));
        AddStatus(statuses, "Pose Input", DescribeBoolCondition(entry.requirePoseButtonHeld, context.poseInputHeld));

        if (includeLegacyAdvanced)
        {
            AddStatus(statuses, "Legacy Orientation", DescribeEnumCondition(entry.requiredOrientationModifier, context.orientationModifier, SkiController.AerialOrientationModifier.None));
            AddStatus(statuses, "Required Pose Name", string.IsNullOrWhiteSpace(entry.requiredPoseName)
                ? $"Ignore (current {context.poseName})"
                : string.Equals(entry.requiredPoseName, context.poseName, System.StringComparison.OrdinalIgnoreCase)
                    ? $"Match ({context.poseName})"
                    : $"Mismatch (need {entry.requiredPoseName}, current {context.poseName})");
            AddStatus(statuses, "Advanced Modifier Gates", entry.useAdvancedModifierConditions ? "Enabled" : "Disabled (ignored for base pose matching)");
            AddStatus(statuses, "Spin Direction", entry.useAdvancedModifierConditions ? DescribeSpinCondition(entry.requiredSpinDirection, context.spinDirectionSign) : "Advanced gate disabled");
            AddStatus(statuses, "Flip Direction", entry.useAdvancedModifierConditions ? DescribeFlipCondition(entry.requiredFlipDirection, context.flipDirectionSign) : "Advanced gate disabled");
            AddStatus(statuses, "Yaw Range", entry.useAdvancedModifierConditions ? DescribeRangeCondition(entry.yawAngularVelocityRange, context.yawAngularVelocity) : "Advanced gate disabled");
            AddStatus(statuses, "Pitch Range", entry.useAdvancedModifierConditions ? DescribeRangeCondition(entry.pitchAngularVelocityRange, context.pitchAngularVelocity) : "Advanced gate disabled");
            AddStatus(statuses, "Roll Range", entry.useAdvancedModifierConditions ? DescribeRangeCondition(entry.rollAngularVelocityRange, context.rollAngularVelocity) : "Advanced gate disabled");
            AddStatus(statuses, "Total Speed", entry.useAdvancedModifierConditions ? DescribeRangeCondition(entry.totalAngularSpeedRange, context.totalAngularSpeed) : "Advanced gate disabled");
            AddStatus(statuses, "Entry Pitch", DescribeAngleCondition(entry.entryPitchAngleRange, context.entryEulerAngles.x));
            AddStatus(statuses, "Entry Yaw", DescribeAngleCondition(entry.entryYawAngleRange, context.entryEulerAngles.y));
            AddStatus(statuses, "Entry Roll", DescribeAngleCondition(entry.entryRollAngleRange, context.entryEulerAngles.z));
        }

        return statuses;
    }

    public static System.Collections.Generic.List<string> BuildFailReasons(TrickPoseEntry entry, TrickPoseEditorPreviewContext context, int maxCount = int.MaxValue)
    {
        System.Collections.Generic.List<string> reasons = new System.Collections.Generic.List<string>();
        System.Collections.Generic.List<TrickPoseEntryConditionStatus> statuses = BuildConditionStatuses(entry, context, includeLegacyAdvanced: false);
        for (int i = 0; i < statuses.Count; i++)
        {
            TrickPoseEntryConditionStatus status = statuses[i];
            if (!IsFailStatus(status))
                continue;

            reasons.Add($"{status.label}: {status.status}");
            if (reasons.Count >= maxCount)
                break;
        }

        return reasons;
    }

    public static bool IsFailStatus(TrickPoseEntryConditionStatus status)
    {
        if (status == null || string.IsNullOrWhiteSpace(status.status))
            return false;

        return status.status.StartsWith("Mismatch", System.StringComparison.Ordinal) ||
               status.status.StartsWith("Below", System.StringComparison.Ordinal) ||
               status.status.StartsWith("Above", System.StringComparison.Ordinal);
    }

    public static TrickPoseRigSnapshot ApplySyntheticPosture(TrickPoseRigSnapshot snapshot, TrickPoseEditorPreviewContext context)
    {
        if (snapshot == null || context == null)
            return snapshot;

        TrickPoseRigSnapshot adjusted = snapshot.Clone();
        float strength = context.source == TrickPosePreviewContextSource.InfluencePreview ? 1.4f : 1f;

        switch (context.poseFamily)
        {
            case SkiController.AerialPoseFamily.Left:
                OffsetFamily(ref adjusted.leftSki, -0.11f * strength, 0f);
                OffsetFamily(ref adjusted.leftPole, -0.08f * strength, -12f * strength);
                OffsetFamily(ref adjusted.rightSki, 0.04f * strength, 0f);
                OffsetFamily(ref adjusted.body, -0.025f * strength, -4f * strength);
                break;

            case SkiController.AerialPoseFamily.Right:
                OffsetFamily(ref adjusted.rightSki, 0.11f * strength, 0f);
                OffsetFamily(ref adjusted.rightPole, 0.08f * strength, 12f * strength);
                OffsetFamily(ref adjusted.leftSki, -0.04f * strength, 0f);
                OffsetFamily(ref adjusted.body, 0.025f * strength, 4f * strength);
                break;

            case SkiController.AerialPoseFamily.Spread:
                OffsetFamily(ref adjusted.leftSki, -0.15f * strength, 0f);
                OffsetFamily(ref adjusted.rightSki, 0.15f * strength, 0f);
                OffsetFamily(ref adjusted.leftPole, -0.11f * strength, -16f * strength);
                OffsetFamily(ref adjusted.rightPole, 0.11f * strength, 16f * strength);
                OffsetFamily(ref adjusted.body, 0f, 0f);
                break;
        }

        switch (context.poseShape)
        {
            case SkiController.AerialPoseShape.Compact:
                OffsetState(ref adjusted.body, new Vector3(0f, -0.09f * strength, 0.05f * strength), new Vector3(14f * strength, 0f, 0f));
                OffsetState(ref adjusted.head, new Vector3(0f, -0.07f * strength, 0.10f * strength), new Vector3(4f * strength, 0f, 0f));
                OffsetState(ref adjusted.leftSki, new Vector3(0f, 0.04f * strength, -0.04f * strength), new Vector3(8f * strength, 0f, 0f));
                OffsetState(ref adjusted.rightSki, new Vector3(0f, 0.04f * strength, -0.04f * strength), new Vector3(8f * strength, 0f, 0f));
                break;

            case SkiController.AerialPoseShape.Driving:
                OffsetState(ref adjusted.body, new Vector3(0f, 0f, 0.07f * strength), new Vector3(-12f * strength, 0f, 0f));
                OffsetState(ref adjusted.head, new Vector3(0f, 0.01f * strength, 0.10f * strength), new Vector3(-6f * strength, 0f, 0f));
                break;

            case SkiController.AerialPoseShape.LaidOut:
                OffsetState(ref adjusted.body, new Vector3(0f, 0.05f * strength, -0.06f * strength), new Vector3(14f * strength, 0f, 0f));
                OffsetState(ref adjusted.head, new Vector3(0f, 0.05f * strength, -0.08f * strength), new Vector3(7f * strength, 0f, 0f));
                break;
        }

        return adjusted;
    }

    public static Quaternion ComposePreviewRotation(Quaternion baseRotation, Vector3 pitchAxis, Vector3 rollAxis, TrickPoseEditorPreviewContext context, Vector3 dynamicAngles)
    {
        Quaternion modifierRotation = Quaternion.identity;

        if (context != null)
        {
            float strength = context.source == TrickPosePreviewContextSource.InfluencePreview ? 1.35f : 1f;
            bool suppressLegacyOrientation = TrickPoseOrientationUtility.ShouldSuppressLegacyPreviewModifier(
                context.hasPresentationRotation,
                context.verticalOrientation,
                context.horizontalOrientation,
                context.travelFacing);

            modifierRotation = suppressLegacyOrientation ? Quaternion.identity : context.orientationModifier switch
            {
                SkiController.AerialOrientationModifier.Switch =>
                    Quaternion.AngleAxis(180f, Vector3.up),

                SkiController.AerialOrientationModifier.Inverted =>
                    Quaternion.AngleAxis(180f, pitchAxis),

                SkiController.AerialOrientationModifier.OnSide =>
                    Quaternion.AngleAxis(90f * strength, rollAxis),

                SkiController.AerialOrientationModifier.ChestDown =>
                    Quaternion.AngleAxis(90f * strength, pitchAxis),

                SkiController.AerialOrientationModifier.ChestUp =>
                    Quaternion.AngleAxis(-90f * strength, pitchAxis),

                SkiController.AerialOrientationModifier.Sideways =>
                    Quaternion.AngleAxis(90f * strength, rollAxis),

                SkiController.AerialOrientationModifier.Rising =>
                    Quaternion.AngleAxis(-30f * strength, pitchAxis),

                SkiController.AerialOrientationModifier.Diving =>
                    Quaternion.AngleAxis(30f * strength, pitchAxis),

                _ => Quaternion.identity
            };
        }

        Quaternion entryRotation = context != null && context.hasPresentationRotation
            ? Quaternion.Euler(context.presentationRotationEuler)
            : Quaternion.identity;

        return
            Quaternion.AngleAxis(dynamicAngles.y, Vector3.up) *
            Quaternion.AngleAxis(dynamicAngles.x, pitchAxis) *
            Quaternion.AngleAxis(dynamicAngles.z, rollAxis) *
            entryRotation *
            modifierRotation *
            baseRotation;
    }

    public static Vector3 GetInstantDynamicAngles(TrickPoseEditorPreviewContext context)
    {
        if (context == null)
            return Vector3.zero;

        return new Vector3(
            Mathf.Clamp(context.pitchAngularVelocity * 0.12f, -45f, 45f),
            Mathf.Clamp(context.yawAngularVelocity * 0.12f, -70f, 70f),
            Mathf.Clamp(context.rollAngularVelocity * 0.12f, -45f, 45f));
    }

    public static string ResolvePoseName(SkiController.AerialPoseFamily family, SkiController.AerialPoseShape shape)
    {
        return (family, shape) switch
        {
            (SkiController.AerialPoseFamily.Left, SkiController.AerialPoseShape.Compact) => "Mantis",
            (SkiController.AerialPoseFamily.Left, SkiController.AerialPoseShape.Driving) => "Harpoon",
            (SkiController.AerialPoseFamily.Left, SkiController.AerialPoseShape.LaidOut) => "High Hook",
            (SkiController.AerialPoseFamily.Left, SkiController.AerialPoseShape.Neutral) => "Scarecrow",
            (SkiController.AerialPoseFamily.Right, SkiController.AerialPoseShape.Compact) => "Crane",
            (SkiController.AerialPoseFamily.Right, SkiController.AerialPoseShape.Driving) => "Stinger",
            (SkiController.AerialPoseFamily.Right, SkiController.AerialPoseShape.LaidOut) => "Sidearm",
            (SkiController.AerialPoseFamily.Right, SkiController.AerialPoseShape.Neutral) => "Crowbar",
            (SkiController.AerialPoseFamily.Spread, SkiController.AerialPoseShape.Compact) => "Foldover",
            (SkiController.AerialPoseFamily.Spread, SkiController.AerialPoseShape.Driving) => "Cathedral",
            (SkiController.AerialPoseFamily.Spread, SkiController.AerialPoseShape.LaidOut) => "High Wire",
            (SkiController.AerialPoseFamily.Spread, SkiController.AerialPoseShape.Neutral) => "Split Rail",
            (SkiController.AerialPoseFamily.Neutral, SkiController.AerialPoseShape.Compact) => "Deadbolt",
            (SkiController.AerialPoseFamily.Neutral, SkiController.AerialPoseShape.Driving) => "Dart",
            (SkiController.AerialPoseFamily.Neutral, SkiController.AerialPoseShape.LaidOut) => "Dead Sail",
            (SkiController.AerialPoseFamily.Neutral, SkiController.AerialPoseShape.Neutral) => "Drifter",
            _ => string.Empty
        };
    }

    private static void FinalizeContext(SkiController controller, TrickPoseEditorPreviewContext context, TrickPoseEntry entry, bool forceMatched)
    {
        if (context == null)
            return;

        context.totalAngularSpeed = new Vector3(context.pitchAngularVelocity, context.yawAngularVelocity, context.rollAngularVelocity).magnitude;
        context.spinDirectionSign = SignFromAngularVelocity(context.yawAngularVelocity);
        context.flipDirectionSign = SignFromAngularVelocity(context.pitchAngularVelocity);

        if (context.source != TrickPosePreviewContextSource.InfluencePreview)
        {
            if (context.poseFamily == SkiController.AerialPoseFamily.None)
                context.poseFamily = forceMatched && entry != null && entry.requiredPoseFamily != SkiController.AerialPoseFamily.None
                    ? entry.requiredPoseFamily
                    : GetCurrentPoseFamily(controller);

            if (context.poseShape == SkiController.AerialPoseShape.None)
                context.poseShape = forceMatched && entry != null && entry.requiredPoseShape != SkiController.AerialPoseShape.None
                    ? entry.requiredPoseShape
                    : GetCurrentPoseShape(controller);

            if (context.orientationModifier == SkiController.AerialOrientationModifier.None)
                context.orientationModifier = forceMatched
                    ? entry != null && entry.requiredOrientationModifier != SkiController.AerialOrientationModifier.None
                        ? entry.requiredOrientationModifier
                        : TrickPoseOrientationUtility.ToLegacyOrientationModifier(
                            context.verticalOrientation,
                            context.horizontalOrientation,
                            context.motionState)
                    : GetCurrentOrientation(controller);

            if (!forceMatched &&
                context.orientationModifier == SkiController.AerialOrientationModifier.None &&
                context.hasPresentationRotation)
                context.orientationModifier = DeriveOrientationModifier(context);

            if (context.verticalOrientation == TrickPoseVerticalOrientationRequirement.Any)
                context.verticalOrientation = forceMatched && entry != null && entry.requiredVerticalOrientation != TrickPoseVerticalOrientationRequirement.Any
                    ? entry.requiredVerticalOrientation
                    : GetCurrentVerticalOrientation(controller);

            if (context.horizontalOrientation == TrickPoseHorizontalOrientationRequirement.Any)
                context.horizontalOrientation = forceMatched && entry != null && entry.requiredHorizontalOrientation != TrickPoseHorizontalOrientationRequirement.Any
                    ? entry.requiredHorizontalOrientation
                    : GetCurrentHorizontalOrientation(controller);

            if (context.travelFacing == TrickPoseTravelFacingRequirement.Any)
                context.travelFacing = forceMatched && entry != null && entry.requiredTravelFacing != TrickPoseTravelFacingRequirement.Any
                    ? entry.requiredTravelFacing
                    : GetCurrentTravelFacing(controller);

            

            if (context.motionState == TrickPoseMotionStateRequirement.Any)
                context.motionState = forceMatched && entry != null && entry.requiredMotionState != TrickPoseMotionStateRequirement.Any
                    ? entry.requiredMotionState
                    : GetCurrentMotionState(controller);
        }
        else
        {
            context.poseFamily = DerivePoseFamily(context);
            context.poseShape = DerivePoseShape(context);
            context.verticalOrientation = DeriveVerticalOrientation(context);
            context.horizontalOrientation = DeriveHorizontalOrientation(context);
            context.travelFacing = DeriveTravelFacing(context);
            
            context.motionState = DeriveMotionState(context);
            context.orientationModifier = DeriveOrientationModifier(context);
        }

        context.poseName = ResolvePoseName(context.poseFamily, context.poseShape);
    }

    private static SkiController.AerialPoseFamily DerivePoseFamily(TrickPoseEditorPreviewContext context)
    {
        if (context == null || !context.airborne || !context.poseInputHeld)
            return SkiController.AerialPoseFamily.None;

        if (context.leftInput && context.rightInput)
            return SkiController.AerialPoseFamily.Spread;
        if (context.leftInput)
            return SkiController.AerialPoseFamily.Left;
        if (context.rightInput)
            return SkiController.AerialPoseFamily.Right;

        return SkiController.AerialPoseFamily.Neutral;
    }

    private static SkiController.AerialPoseShape DerivePoseShape(TrickPoseEditorPreviewContext context)
    {
        if (context == null || !context.airborne || !context.poseInputHeld)
            return SkiController.AerialPoseShape.None;

        if (context.tuckInput)
            return SkiController.AerialPoseShape.Compact;

        if (context.leanInput >= 0.35f)
            return SkiController.AerialPoseShape.Driving;

        if (context.leanInput <= -0.35f)
            return SkiController.AerialPoseShape.LaidOut;

        return SkiController.AerialPoseShape.Neutral;
    }

    private static SkiController.AerialOrientationModifier DeriveOrientationModifier(TrickPoseEditorPreviewContext context)
    {
        if (context == null || !context.airborne || !context.poseInputHeld)
            return SkiController.AerialOrientationModifier.None;

        if (context.hasPresentationRotation)
        {
            Quaternion rotation = Quaternion.Euler(context.presentationRotationEuler);
            TrickPoseVerticalOrientationRequirement derivedVertical = TrickPoseOrientationUtility.DerivePreviewVertical(rotation, true);
            TrickPoseHorizontalOrientationRequirement derivedHorizontal = TrickPoseOrientationUtility.DerivePreviewHorizontal(rotation, true);

            if (derivedVertical == TrickPoseVerticalOrientationRequirement.Inverted)
                return SkiController.AerialOrientationModifier.Inverted;
            if (derivedVertical == TrickPoseVerticalOrientationRequirement.ChestDown)
                return SkiController.AerialOrientationModifier.ChestDown;
            if (derivedVertical == TrickPoseVerticalOrientationRequirement.ChestUp)
                return SkiController.AerialOrientationModifier.ChestUp;
            if (derivedHorizontal == TrickPoseHorizontalOrientationRequirement.LeftSide ||
                derivedHorizontal == TrickPoseHorizontalOrientationRequirement.RightSide)
                return SkiController.AerialOrientationModifier.OnSide;

            float yaw = Mathf.Abs(TrickPoseEulerAngleRange.NormalizeSignedAngle(context.presentationRotationEuler.y));
            float roll = Mathf.Abs(TrickPoseEulerAngleRange.NormalizeSignedAngle(context.presentationRotationEuler.z));
            if (yaw > 135f)
                return SkiController.AerialOrientationModifier.Switch;
            if (roll >= 55f && roll <= 125f)
                return SkiController.AerialOrientationModifier.Sideways;
        }

        if (context.rising)
            return SkiController.AerialOrientationModifier.Rising;

        if (context.diving)
            return SkiController.AerialOrientationModifier.Diving;

        if (Mathf.Abs(context.rollAngularVelocity) >= 120f)
            return SkiController.AerialOrientationModifier.Sideways;

        if (context.pitchAngularVelocity <= -180f)
            return SkiController.AerialOrientationModifier.Inverted;

        if (Mathf.Abs(context.yawAngularVelocity) >= 220f && context.spinDirectionSign != 0)
            return SkiController.AerialOrientationModifier.Switch;

        return SkiController.AerialOrientationModifier.None;
    }

    private static TrickPoseVerticalOrientationRequirement DeriveVerticalOrientation(TrickPoseEditorPreviewContext context)
    {
        if (context == null || !context.airborne || !context.poseInputHeld)
            return TrickPoseVerticalOrientationRequirement.Any;

        if (context.hasPresentationRotation)
        {
            return TrickPoseOrientationUtility.DerivePreviewVertical(Quaternion.Euler(context.presentationRotationEuler), true);
        }

        return TrickPoseVerticalOrientationRequirement.Upright;
    }

    private static TrickPoseHorizontalOrientationRequirement DeriveHorizontalOrientation(TrickPoseEditorPreviewContext context)
    {
        if (context == null || !context.airborne || !context.poseInputHeld)
            return TrickPoseHorizontalOrientationRequirement.Any;

        if (context.hasPresentationRotation)
        {
            return TrickPoseOrientationUtility.DerivePreviewHorizontal(Quaternion.Euler(context.presentationRotationEuler), true);
        }

        return TrickPoseHorizontalOrientationRequirement.Upright;
    }

    private static TrickPoseTravelFacingRequirement DeriveTravelFacing(TrickPoseEditorPreviewContext context)
    {
        if (context == null || !context.airborne || !context.poseInputHeld)
            return TrickPoseTravelFacingRequirement.Any;

        if (context.hasPresentationRotation)
            return TrickPoseOrientationUtility.DerivePreviewTravelFacing(Quaternion.Euler(context.presentationRotationEuler), true);

        return TrickPoseTravelFacingRequirement.Forward;
    } 

    private static TrickPoseMotionStateRequirement DeriveMotionState(TrickPoseEditorPreviewContext context)
    {
        if (context == null || !context.airborne || !context.poseInputHeld)
            return TrickPoseMotionStateRequirement.Any;

        if (context.rising)
            return TrickPoseMotionStateRequirement.Rising;
        if (context.diving)
            return TrickPoseMotionStateRequirement.Diving;
        return TrickPoseMotionStateRequirement.Any;
    }

    private static bool Matches(TrickPoseEntry entry, TrickPoseEditorPreviewContext context)
    {
        SkiController.AerialPoseFamily effectiveRequiredFamily = entry.GetEffectiveRequiredPoseFamily();
        SkiController.AerialPoseShape effectiveRequiredShape = entry.GetEffectiveRequiredPoseShape();

        if (effectiveRequiredFamily != SkiController.AerialPoseFamily.None && context.poseFamily != effectiveRequiredFamily)
            return false;

        if (effectiveRequiredShape != SkiController.AerialPoseShape.None && context.poseShape != effectiveRequiredShape)
            return false;
        if (entry.requiredVerticalOrientation != TrickPoseVerticalOrientationRequirement.Any && context.verticalOrientation != entry.requiredVerticalOrientation)
            return false;
        if (entry.requiredHorizontalOrientation != TrickPoseHorizontalOrientationRequirement.Any && context.horizontalOrientation != entry.requiredHorizontalOrientation)
            return false;
        if (entry.requiredTravelFacing != TrickPoseTravelFacingRequirement.Any && context.travelFacing != entry.requiredTravelFacing)
            return false;
        
        if (entry.requiredMotionState != TrickPoseMotionStateRequirement.Any && context.motionState != entry.requiredMotionState)
            return false;
        if (UsesLegacyOrientationModifier(entry) && context.orientationModifier != entry.requiredOrientationModifier)
            return false;
        if (!string.IsNullOrWhiteSpace(entry.requiredPoseName) && !string.Equals(context.poseName, entry.requiredPoseName, System.StringComparison.OrdinalIgnoreCase))
            return false;
        if (!MatchesBool(entry.requireAirborne, context.airborne))
            return false;
        if (!MatchesBool(entry.requirePoseButtonHeld, context.poseInputHeld))
            return false;
        if (entry.useAdvancedModifierConditions)
        {
            if (entry.requiredSpinDirection != TrickPoseSpinDirectionRequirement.Any && context.spinDirectionSign != (int)entry.requiredSpinDirection)
                return false;
            if (entry.requiredFlipDirection != TrickPoseFlipDirectionRequirement.Any && context.flipDirectionSign != (int)entry.requiredFlipDirection)
                return false;
            if (!entry.yawAngularVelocityRange.Contains(context.yawAngularVelocity))
                return false;
            if (!entry.pitchAngularVelocityRange.Contains(context.pitchAngularVelocity))
                return false;
            if (!entry.rollAngularVelocityRange.Contains(context.rollAngularVelocity))
                return false;
            if (!entry.totalAngularSpeedRange.Contains(context.totalAngularSpeed))
                return false;
        }
        if (!entry.entryPitchAngleRange.Contains(context.entryEulerAngles.x))
            return false;
        if (!entry.entryYawAngleRange.Contains(context.entryEulerAngles.y))
            return false;
        if (!entry.entryRollAngleRange.Contains(context.entryEulerAngles.z))
            return false;
        return true;
    }

    private static void AddStatus(System.Collections.Generic.List<TrickPoseEntryConditionStatus> statuses, string label, string status)
    {
        statuses.Add(new TrickPoseEntryConditionStatus
        {
            label = label,
            status = status
        });
    }

    private static string DescribeEnumCondition<T>(T required, T actual, T ignoreValue)
    {
        if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(required, ignoreValue))
            return $"Ignore (current {actual})";

        return System.Collections.Generic.EqualityComparer<T>.Default.Equals(required, actual)
            ? $"Match ({actual})"
            : $"Mismatch (need {required}, current {actual})";
    }

    private static string DescribeBoolCondition(TrickPoseBoolRequirement requirement, bool value)
    {
        return requirement switch
        {
            TrickPoseBoolRequirement.Ignore => $"Ignore (current {(value ? "True" : "False")})",
            TrickPoseBoolRequirement.True => value ? "Match (True)" : "Mismatch (need True)",
            TrickPoseBoolRequirement.False => !value ? "Match (False)" : "Mismatch (need False)",
            _ => string.Empty
        };
    }

    private static string DescribeSpinCondition(TrickPoseSpinDirectionRequirement requirement, int actual)
    {
        if (requirement == TrickPoseSpinDirectionRequirement.Any)
            return $"Ignore (current {DescribeSpin(actual)})";

        return actual == (int)requirement
            ? $"Match ({DescribeSpin(actual)})"
            : $"Mismatch (need {DescribeSpin((int)requirement)}, current {DescribeSpin(actual)})";
    }

    private static string DescribeFlipCondition(TrickPoseFlipDirectionRequirement requirement, int actual)
    {
        if (requirement == TrickPoseFlipDirectionRequirement.Any)
            return $"Ignore (current {DescribeFlip(actual)})";

        return actual == (int)requirement
            ? $"Match ({DescribeFlip(actual)})"
            : $"Mismatch (need {DescribeFlip((int)requirement)}, current {DescribeFlip(actual)})";
    }

    private static string DescribeRangeCondition(TrickPoseAngularVelocityRange range, float value)
    {
        if (!range.enabled)
            return $"Ignore (current {value:0.#})";

        Vector2 sorted = range.GetSortedRange();
        if (value < sorted.x)
            return $"Below ({value:0.#} < {sorted.x:0.#})";
        if (value > sorted.y)
            return $"Above ({value:0.#} > {sorted.y:0.#})";
        return $"Inside ({value:0.#} in {sorted.x:0.#}..{sorted.y:0.#})";
    }

    private static string DescribeAngleCondition(TrickPoseEulerAngleRange range, float value)
    {
        if (!range.enabled)
            return $"Ignore (entry {value:0.#} deg)";

        Vector2 sorted = range.GetSortedRange();
        if (value < sorted.x)
            return $"Below ({value:0.#} < {sorted.x:0.#})";
        if (value > sorted.y)
            return $"Above ({value:0.#} > {sorted.y:0.#})";
        return $"Inside ({value:0.#} in {sorted.x:0.#}..{sorted.y:0.#})";
    }

    private static Vector3 NormalizeEulerAngles(Vector3 eulerAngles)
    {
        return new Vector3(
            TrickPoseEulerAngleRange.NormalizeSignedAngle(eulerAngles.x),
            TrickPoseEulerAngleRange.NormalizeSignedAngle(eulerAngles.y),
            TrickPoseEulerAngleRange.NormalizeSignedAngle(eulerAngles.z));
    }

    private static string DescribeSpin(int sign)
    {
        return sign > 0 ? "CW" : sign < 0 ? "CCW" : "None";
    }

    private static string DescribeFlip(int sign)
    {
        return sign > 0 ? "Front" : sign < 0 ? "Back" : "None";
    }

    private static bool MatchesBool(TrickPoseBoolRequirement requirement, bool value)
    {
        return requirement switch
        {
            TrickPoseBoolRequirement.True => value,
            TrickPoseBoolRequirement.False => !value,
            _ => true
        };
    }

    private static float GetRepresentativeAxisValue(TrickPoseAngularVelocityRange range, float fallback, int forcedSign)
    {
        if (!range.enabled)
            return fallback;

        Vector2 sorted = range.GetSortedRange();
        float value = (sorted.x + sorted.y) * 0.5f;
        if (forcedSign != 0)
            value = Mathf.Abs(value) * forcedSign;
        return value;
    }

    private static bool RequiredOrDefault(TrickPoseBoolRequirement requirement, bool defaultValue)
    {
        return requirement switch
        {
            TrickPoseBoolRequirement.True => true,
            TrickPoseBoolRequirement.False => false,
            _ => defaultValue
        };
    }

    private static int SignFromAngularVelocity(float value)
    {
        return value > DirectionThreshold ? 1 : (value < -DirectionThreshold ? -1 : 0);
    }

    private static SkiController.AerialPoseFamily GetCurrentPoseFamily(SkiController controller)
    {
        return controller != null ? controller.CurrentPoseFamily : SkiController.AerialPoseFamily.None;
    }

    private static SkiController.AerialPoseShape GetCurrentPoseShape(SkiController controller)
    {
        return controller != null ? controller.CurrentPoseShape : SkiController.AerialPoseShape.None;
    }

    private static SkiController.AerialOrientationModifier GetCurrentOrientation(SkiController controller)
    {
        return controller != null ? controller.CurrentPoseOrientationModifier : SkiController.AerialOrientationModifier.None;
    }

    private static TrickPoseVerticalOrientationRequirement GetCurrentVerticalOrientation(SkiController controller)
    {
        return controller != null ? controller.CurrentPoseVerticalOrientation : TrickPoseVerticalOrientationRequirement.Any;
    }

    private static TrickPoseHorizontalOrientationRequirement GetCurrentHorizontalOrientation(SkiController controller)
    {
        return controller != null ? controller.CurrentPoseHorizontalOrientation : TrickPoseHorizontalOrientationRequirement.Any;
    }

    private static TrickPoseTravelFacingRequirement GetCurrentTravelFacing(SkiController controller)
    {
        return controller != null ? controller.CurrentPoseTravelFacing : TrickPoseTravelFacingRequirement.Any;
    }

    private static TrickPoseMotionStateRequirement GetCurrentMotionState(SkiController controller)
    {
        return controller != null ? controller.CurrentPoseMotionState : TrickPoseMotionStateRequirement.Any;
    }

    private static bool UsesLegacyOrientationModifier(TrickPoseEntry entry)
    {
        return entry != null &&
               entry.requiredOrientationModifier != SkiController.AerialOrientationModifier.None &&
               entry.requiredVerticalOrientation == TrickPoseVerticalOrientationRequirement.Any &&
               entry.requiredHorizontalOrientation == TrickPoseHorizontalOrientationRequirement.Any &&
               entry.requiredTravelFacing == TrickPoseTravelFacingRequirement.Any &&
               entry.requiredMotionState == TrickPoseMotionStateRequirement.Any;
    }

    private static SkiController.AerialOrientationModifier ToMotionOrientationModifier(TrickPoseMotionStateRequirement motion)
    {
        return motion switch
        {
            TrickPoseMotionStateRequirement.Rising => SkiController.AerialOrientationModifier.Rising,
            TrickPoseMotionStateRequirement.Diving => SkiController.AerialOrientationModifier.Diving,
            _ => SkiController.AerialOrientationModifier.None
        };
    }

    private static void OffsetFamily(ref TrickPoseRigSnapshot.PartState state, float localX, float yaw)
    {
        OffsetState(ref state, new Vector3(localX, 0f, 0f), new Vector3(0f, yaw, 0f));
    }

    private static void OffsetState(ref TrickPoseRigSnapshot.PartState state, Vector3 localPositionOffset, Vector3 localEulerOffset)
    {
        if (!state.hasValue)
            return;

        state.localPosition += localPositionOffset;
        state.localRotation = Quaternion.Euler(localEulerOffset) * state.localRotation;
    }
}
