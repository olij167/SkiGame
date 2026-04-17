using System;
using UnityEngine;

public enum TrickPoseBoolRequirement
{
    Ignore = 0,
    True = 1,
    False = 2
}

public enum TrickPoseSpinDirectionRequirement
{
    Any = 0,
    Clockwise = 1,
    CounterClockwise = -1
}

public enum TrickPoseFlipDirectionRequirement
{
    Any = 0,
    Frontflip = 1,
    Backflip = -1
}

[Serializable]
public struct TrickPoseAngularVelocityRange
{
    public bool enabled;
    public Vector2 range;

    public Vector2 GetSortedRange()
    {
        return range.x <= range.y
            ? range
            : new Vector2(range.y, range.x);
    }

    public bool Contains(float value)
    {
        if (!enabled)
            return true;

        Vector2 sorted = GetSortedRange();
        return value >= sorted.x && value <= sorted.y;
    }
}

[Serializable]
public class TrickPoseEntry
{
    [Header("General")]
    public bool enabled = true;
    public string displayName = "New Trick Pose";
    public int priority;
    [Range(0f, 1f)] public float overallWeight = 1f;
    public string overridePoseLabel;

    [Header("Match Conditions")]
    public SkiController.AerialPoseFamily requiredPoseFamily = SkiController.AerialPoseFamily.None;
    public SkiController.AerialPoseShape requiredPoseShape = SkiController.AerialPoseShape.None;
    public SkiController.AerialOrientationModifier requiredOrientationModifier = SkiController.AerialOrientationModifier.None;
    public string requiredPoseName;
    public TrickPoseBoolRequirement requireAirborne = TrickPoseBoolRequirement.Ignore;
    public TrickPoseBoolRequirement requirePoseButtonHeld = TrickPoseBoolRequirement.Ignore;
    public TrickPoseSpinDirectionRequirement requiredSpinDirection = TrickPoseSpinDirectionRequirement.Any;
    public TrickPoseFlipDirectionRequirement requiredFlipDirection = TrickPoseFlipDirectionRequirement.Any;
    public TrickPoseAngularVelocityRange yawAngularVelocityRange;
    public TrickPoseAngularVelocityRange pitchAngularVelocityRange;
    public TrickPoseAngularVelocityRange rollAngularVelocityRange;
    public TrickPoseAngularVelocityRange totalAngularSpeedRange;

    [Header("Per-Part Pose")]
    public PosePartTransformData bodyPose = new PosePartTransformData();
    public PosePartTransformData headPose = new PosePartTransformData();
    public PosePartTransformData leftSkiPose = new PosePartTransformData();
    public PosePartTransformData rightSkiPose = new PosePartTransformData();
    public PosePartTransformData leftPolePose = new PosePartTransformData();
    public PosePartTransformData rightPolePose = new PosePartTransformData();

    [Header("Transition")]
    public float blendInSpeed = 8f;
    public float blendOutSpeed = 8f;
    public bool snapOnPreview = true;
    public bool allowBlendWithOthers;

    public bool Matches(SkiController controller)
    {
        if (!enabled || controller == null)
            return false;

        if (requiredPoseFamily != SkiController.AerialPoseFamily.None &&
            controller.CurrentPoseFamily != requiredPoseFamily)
            return false;

        if (requiredPoseShape != SkiController.AerialPoseShape.None &&
            controller.CurrentPoseShape != requiredPoseShape)
            return false;

        if (requiredOrientationModifier != SkiController.AerialOrientationModifier.None &&
            controller.CurrentPoseOrientationModifier != requiredOrientationModifier)
            return false;

        if (!string.IsNullOrWhiteSpace(requiredPoseName) &&
            !string.Equals(controller.CurrentPoseName, requiredPoseName, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!MatchesOptionalBool(requireAirborne, controller.IsAuthoredPoseAirborne))
            return false;

        if (!MatchesOptionalBool(requirePoseButtonHeld, controller.IsPoseButtonHeld))
            return false;

        if (requiredSpinDirection != TrickPoseSpinDirectionRequirement.Any &&
            controller.CurrentSpinDirectionSign != (int)requiredSpinDirection)
            return false;

        if (requiredFlipDirection != TrickPoseFlipDirectionRequirement.Any &&
            controller.CurrentFlipDirectionSign != (int)requiredFlipDirection)
            return false;

        if (!yawAngularVelocityRange.Contains(controller.CurrentYawAngularVelocity))
            return false;

        if (!pitchAngularVelocityRange.Contains(controller.CurrentPitchAngularVelocity))
            return false;

        if (!rollAngularVelocityRange.Contains(controller.CurrentRollAngularVelocity))
            return false;

        if (!totalAngularSpeedRange.Contains(controller.CurrentTotalAngularSpeed))
            return false;

        return true;
    }

    public int GetSpecificityScore()
    {
        int score = 0;

        if (requiredPoseFamily != SkiController.AerialPoseFamily.None) score++;
        if (requiredPoseShape != SkiController.AerialPoseShape.None) score++;
        if (requiredOrientationModifier != SkiController.AerialOrientationModifier.None) score++;
        if (!string.IsNullOrWhiteSpace(requiredPoseName)) score++;
        if (requireAirborne != TrickPoseBoolRequirement.Ignore) score++;
        if (requirePoseButtonHeld != TrickPoseBoolRequirement.Ignore) score++;
        if (requiredSpinDirection != TrickPoseSpinDirectionRequirement.Any) score++;
        if (requiredFlipDirection != TrickPoseFlipDirectionRequirement.Any) score++;
        if (yawAngularVelocityRange.enabled) score++;
        if (pitchAngularVelocityRange.enabled) score++;
        if (rollAngularVelocityRange.enabled) score++;
        if (totalAngularSpeedRange.enabled) score++;

        return score;
    }

    public string GetSummary()
    {
        if (!string.IsNullOrWhiteSpace(displayName))
            return displayName;

        if (!string.IsNullOrWhiteSpace(requiredPoseName))
            return requiredPoseName;

        return "Unnamed Pose";
    }

    private static bool MatchesOptionalBool(TrickPoseBoolRequirement requirement, bool value)
    {
        return requirement switch
        {
            TrickPoseBoolRequirement.True => value,
            TrickPoseBoolRequirement.False => !value,
            _ => true
        };
    }
}
