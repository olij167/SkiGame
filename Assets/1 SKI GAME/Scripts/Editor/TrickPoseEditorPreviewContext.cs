using UnityEngine;

public enum TrickPosePreviewMode
{
    Sequence = 0,
    Influence = 1
}

public enum TrickPosePreviewContextSource
{
    None = 0,
    MatchPreview = 1,
    SequencePreview = 2,
    InfluencePreview = 3
}

[System.Serializable]
public sealed class TrickPoseInfluencePreviewState
{
    public bool poseInputHeld = true;
    public bool tuckInput;
    public bool leftInput;
    public bool rightInput;
    public bool airborne = true;
    public bool rising;
    public bool diving;
    public float yawAngularVelocity;
    public float pitchAngularVelocity;
    public float rollAngularVelocity;
}

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
    public bool rising;
    public bool diving;
    public float yawAngularVelocity;
    public float pitchAngularVelocity;
    public float rollAngularVelocity;
    public float totalAngularSpeed;
    public int spinDirectionSign;
    public int flipDirectionSign;
    public SkiController.AerialPoseFamily poseFamily;
    public SkiController.AerialPoseShape poseShape;
    public SkiController.AerialOrientationModifier orientationModifier;
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
            rising = state.rising && state.airborne,
            diving = state.diving && state.airborne,
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
            yawAngularVelocity = GetRepresentativeAxisValue(entry.yawAngularVelocityRange, controller != null ? controller.CurrentYawAngularVelocity : 0f, entry.requiredSpinDirection == TrickPoseSpinDirectionRequirement.Any ? 0 : (int)entry.requiredSpinDirection),
            pitchAngularVelocity = GetRepresentativeAxisValue(entry.pitchAngularVelocityRange, controller != null ? controller.CurrentPitchAngularVelocity : 0f, entry.requiredFlipDirection == TrickPoseFlipDirectionRequirement.Any ? 0 : (int)entry.requiredFlipDirection),
            rollAngularVelocity = GetRepresentativeAxisValue(entry.rollAngularVelocityRange, controller != null ? controller.CurrentRollAngularVelocity : 0f, 0)
        };

        if (forceMatched)
        {
            context.poseFamily = entry.requiredPoseFamily != SkiController.AerialPoseFamily.None ? entry.requiredPoseFamily : GetCurrentPoseFamily(controller);
            context.poseShape = entry.requiredPoseShape != SkiController.AerialPoseShape.None ? entry.requiredPoseShape : GetCurrentPoseShape(controller);
            context.orientationModifier = entry.requiredOrientationModifier != SkiController.AerialOrientationModifier.None
                ? entry.requiredOrientationModifier
                : GetCurrentOrientation(controller);
            context.leftInput = context.poseFamily == SkiController.AerialPoseFamily.Left || context.poseFamily == SkiController.AerialPoseFamily.Spread;
            context.rightInput = context.poseFamily == SkiController.AerialPoseFamily.Right || context.poseFamily == SkiController.AerialPoseFamily.Spread;
            context.tuckInput = context.poseShape == SkiController.AerialPoseShape.Compact;
            context.rising = context.orientationModifier == SkiController.AerialOrientationModifier.Rising;
            context.diving = context.orientationModifier == SkiController.AerialOrientationModifier.Diving;
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

    public static System.Collections.Generic.List<TrickPoseEntryConditionStatus> BuildConditionStatuses(TrickPoseEntry entry, TrickPoseEditorPreviewContext context)
    {
        System.Collections.Generic.List<TrickPoseEntryConditionStatus> statuses = new System.Collections.Generic.List<TrickPoseEntryConditionStatus>();
        if (entry == null || context == null)
            return statuses;

        AddStatus(statuses, "Family", DescribeEnumCondition(entry.requiredPoseFamily, context.poseFamily, SkiController.AerialPoseFamily.None));
        AddStatus(statuses, "Shape", DescribeEnumCondition(entry.requiredPoseShape, context.poseShape, SkiController.AerialPoseShape.None));
        AddStatus(statuses, "Orientation", DescribeEnumCondition(entry.requiredOrientationModifier, context.orientationModifier, SkiController.AerialOrientationModifier.None));
        AddStatus(statuses, "Airborne", DescribeBoolCondition(entry.requireAirborne, context.airborne));
        AddStatus(statuses, "Pose Input", DescribeBoolCondition(entry.requirePoseButtonHeld, context.poseInputHeld));
        AddStatus(statuses, "Spin Direction", DescribeSpinCondition(entry.requiredSpinDirection, context.spinDirectionSign));
        AddStatus(statuses, "Flip Direction", DescribeFlipCondition(entry.requiredFlipDirection, context.flipDirectionSign));
        AddStatus(statuses, "Yaw Range", DescribeRangeCondition(entry.yawAngularVelocityRange, context.yawAngularVelocity));
        AddStatus(statuses, "Pitch Range", DescribeRangeCondition(entry.pitchAngularVelocityRange, context.pitchAngularVelocity));
        AddStatus(statuses, "Roll Range", DescribeRangeCondition(entry.rollAngularVelocityRange, context.rollAngularVelocity));
        AddStatus(statuses, "Total Speed", DescribeRangeCondition(entry.totalAngularSpeedRange, context.totalAngularSpeed));
        return statuses;
    }

    public static TrickPoseRigSnapshot ApplySyntheticPosture(TrickPoseRigSnapshot snapshot, TrickPoseEditorPreviewContext context)
    {
        if (snapshot == null || context == null)
            return snapshot;

        TrickPoseRigSnapshot adjusted = snapshot.Clone();

        switch (context.poseFamily)
        {
            case SkiController.AerialPoseFamily.Left:
                OffsetFamily(ref adjusted.leftSki, -0.08f, 0f);
                OffsetFamily(ref adjusted.leftPole, -0.05f, -8f);
                OffsetFamily(ref adjusted.rightSki, 0.03f, 0f);
                break;
            case SkiController.AerialPoseFamily.Right:
                OffsetFamily(ref adjusted.rightSki, 0.08f, 0f);
                OffsetFamily(ref adjusted.rightPole, 0.05f, 8f);
                OffsetFamily(ref adjusted.leftSki, -0.03f, 0f);
                break;
            case SkiController.AerialPoseFamily.Spread:
                OffsetFamily(ref adjusted.leftSki, -0.11f, 0f);
                OffsetFamily(ref adjusted.rightSki, 0.11f, 0f);
                OffsetFamily(ref adjusted.leftPole, -0.08f, -10f);
                OffsetFamily(ref adjusted.rightPole, 0.08f, 10f);
                break;
        }

        switch (context.poseShape)
        {
            case SkiController.AerialPoseShape.Compact:
                OffsetState(ref adjusted.body, new Vector3(0f, -0.07f, 0.03f), new Vector3(10f, 0f, 0f));
                OffsetState(ref adjusted.head, new Vector3(0f, -0.06f, 0.08f), Vector3.zero);
                OffsetState(ref adjusted.leftSki, new Vector3(0f, 0.03f, -0.03f), new Vector3(6f, 0f, 0f));
                OffsetState(ref adjusted.rightSki, new Vector3(0f, 0.03f, -0.03f), new Vector3(6f, 0f, 0f));
                break;
            case SkiController.AerialPoseShape.Driving:
                OffsetState(ref adjusted.body, new Vector3(0f, 0f, 0.05f), new Vector3(-8f, 0f, 0f));
                OffsetState(ref adjusted.head, new Vector3(0f, 0.01f, 0.08f), new Vector3(-4f, 0f, 0f));
                break;
            case SkiController.AerialPoseShape.LaidOut:
                OffsetState(ref adjusted.body, new Vector3(0f, 0.03f, -0.04f), new Vector3(9f, 0f, 0f));
                OffsetState(ref adjusted.head, new Vector3(0f, 0.04f, -0.06f), new Vector3(4f, 0f, 0f));
                break;
        }

        return adjusted;
    }

    public static Quaternion ComposePreviewRotation(Quaternion baseRotation, Vector3 pitchAxis, Vector3 rollAxis, TrickPoseEditorPreviewContext context, Vector3 dynamicAngles)
    {
        Quaternion modifierRotation = Quaternion.identity;
        if (context != null)
        {
            modifierRotation = context.orientationModifier switch
            {
                SkiController.AerialOrientationModifier.Switch => Quaternion.AngleAxis(180f, Vector3.up),
                SkiController.AerialOrientationModifier.Inverted => Quaternion.AngleAxis(180f, pitchAxis),
                SkiController.AerialOrientationModifier.Sideways => Quaternion.AngleAxis(90f, Vector3.up),
                SkiController.AerialOrientationModifier.Rising => Quaternion.AngleAxis(-18f, pitchAxis),
                SkiController.AerialOrientationModifier.Diving => Quaternion.AngleAxis(18f, pitchAxis),
                _ => Quaternion.identity
            };
        }

        return
            Quaternion.AngleAxis(dynamicAngles.y, Vector3.up) *
            Quaternion.AngleAxis(dynamicAngles.x, pitchAxis) *
            Quaternion.AngleAxis(dynamicAngles.z, rollAxis) *
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
                context.orientationModifier = forceMatched && entry != null && entry.requiredOrientationModifier != SkiController.AerialOrientationModifier.None
                    ? entry.requiredOrientationModifier
                    : GetCurrentOrientation(controller);
        }
        else
        {
            context.poseFamily = DerivePoseFamily(context);
            context.poseShape = DerivePoseShape(context);
            context.orientationModifier = DeriveOrientationModifier(context);
        }

        context.totalAngularSpeed = new Vector3(context.pitchAngularVelocity, context.yawAngularVelocity, context.rollAngularVelocity).magnitude;
        context.spinDirectionSign = SignFromAngularVelocity(context.yawAngularVelocity);
        context.flipDirectionSign = SignFromAngularVelocity(context.pitchAngularVelocity);
        context.poseName = ResolvePoseName(context.poseFamily, context.poseShape);
    }

    private static SkiController.AerialPoseFamily DerivePoseFamily(TrickPoseEditorPreviewContext context)
    {
        if (!context.airborne || !context.poseInputHeld)
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
        if (!context.airborne || !context.poseInputHeld)
            return SkiController.AerialPoseShape.None;

        if (context.tuckInput)
            return SkiController.AerialPoseShape.Compact;
        if (context.diving)
            return SkiController.AerialPoseShape.Driving;
        if (context.rising)
            return SkiController.AerialPoseShape.LaidOut;
        return SkiController.AerialPoseShape.Neutral;
    }

    private static SkiController.AerialOrientationModifier DeriveOrientationModifier(TrickPoseEditorPreviewContext context)
    {
        if (!context.airborne || !context.poseInputHeld)
            return SkiController.AerialOrientationModifier.None;

        if (context.rising)
            return SkiController.AerialOrientationModifier.Rising;
        if (context.diving)
            return SkiController.AerialOrientationModifier.Diving;
        if (Mathf.Abs(context.rollAngularVelocity) >= 120f)
            return SkiController.AerialOrientationModifier.Sideways;
        if (context.pitchAngularVelocity <= -180f)
            return SkiController.AerialOrientationModifier.Inverted;
        if (context.spinDirectionSign != 0 && Mathf.Abs(context.yawAngularVelocity) >= 220f)
            return SkiController.AerialOrientationModifier.Switch;
        return SkiController.AerialOrientationModifier.None;
    }

    private static bool Matches(TrickPoseEntry entry, TrickPoseEditorPreviewContext context)
    {
        if (entry.requiredPoseFamily != SkiController.AerialPoseFamily.None && context.poseFamily != entry.requiredPoseFamily)
            return false;
        if (entry.requiredPoseShape != SkiController.AerialPoseShape.None && context.poseShape != entry.requiredPoseShape)
            return false;
        if (entry.requiredOrientationModifier != SkiController.AerialOrientationModifier.None && context.orientationModifier != entry.requiredOrientationModifier)
            return false;
        if (!string.IsNullOrWhiteSpace(entry.requiredPoseName) && !string.Equals(context.poseName, entry.requiredPoseName, System.StringComparison.OrdinalIgnoreCase))
            return false;
        if (!MatchesBool(entry.requireAirborne, context.airborne))
            return false;
        if (!MatchesBool(entry.requirePoseButtonHeld, context.poseInputHeld))
            return false;
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
