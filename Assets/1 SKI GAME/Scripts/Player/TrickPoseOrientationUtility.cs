using UnityEngine;

public static class TrickPoseOrientationUtility
{
    private const float InvertedUpThreshold = -0.2f;
    private const float ChestPitchThreshold = 0.45f;
    private const float SideRollThreshold = 0.55f;

    public static string VerticalAxisLabel => "Pitch / Chest Orientation";
    public static string HorizontalAxisLabel => "Roll / Side Orientation";

    public static string GetVerticalTooltip(TrickPoseVerticalOrientationRequirement requirement)
    {
        return requirement switch
        {
            TrickPoseVerticalOrientationRequirement.ChestDown => "Chest/front vector points downward.",
            TrickPoseVerticalOrientationRequirement.ChestUp => "Chest/front vector points upward.",
            TrickPoseVerticalOrientationRequirement.Inverted => "Character up vector points downward.",
            TrickPoseVerticalOrientationRequirement.Upright => "Character remains upright on the pitch axis.",
            _ => "Ignore pitch/chest orientation."
        };
    }

    public static string GetHorizontalTooltip(TrickPoseHorizontalOrientationRequirement requirement)
    {
        return requirement switch
        {
            TrickPoseHorizontalOrientationRequirement.LeftSide => "Character right vector points downward, so the left side is higher/exposed.",
            TrickPoseHorizontalOrientationRequirement.RightSide => "Character right vector points upward, so the right side is higher/exposed.",
            TrickPoseHorizontalOrientationRequirement.Upright => "Character is not rolled onto either side.",
            _ => "Ignore roll/side orientation."
        };
    }

    public static bool HasExplicitOrientationPresentation(
        TrickPoseVerticalOrientationRequirement vertical,
        TrickPoseHorizontalOrientationRequirement horizontal)
    {
        return (vertical != TrickPoseVerticalOrientationRequirement.Any &&
                vertical != TrickPoseVerticalOrientationRequirement.Upright) ||
               horizontal == TrickPoseHorizontalOrientationRequirement.LeftSide ||
               horizontal == TrickPoseHorizontalOrientationRequirement.RightSide;
    }

    public static bool TryBuildPresentationEuler(
    TrickPoseVerticalOrientationRequirement vertical,
    TrickPoseHorizontalOrientationRequirement horizontal,
    out Vector3 euler)
    {
        euler = Vector3.zero;

        // Vertical orientation = pitch/chest direction.
        // Uses X rotation so the character forward/chest axis points up/down.
        switch (vertical)
        {
            case TrickPoseVerticalOrientationRequirement.Inverted:
                euler.x = 180f;
                break;

            case TrickPoseVerticalOrientationRequirement.ChestDown:
                euler.x = 90f;
                break;

            case TrickPoseVerticalOrientationRequirement.ChestUp:
                euler.x = -90f;
                break;
        }

        // Horizontal orientation = roll/side direction.
        // Uses Z rotation so the character right axis points up/down.
        if (horizontal == TrickPoseHorizontalOrientationRequirement.LeftSide)
        {
            euler.z = -90f;
        }
        else if (horizontal == TrickPoseHorizontalOrientationRequirement.RightSide)
        {
            euler.z = 90f;
        }

        return HasExplicitOrientationPresentation(vertical, horizontal);
    }

    public static TrickPoseVerticalOrientationRequirement DeriveVertical(Quaternion rotation, bool airbornePoseActive)
    {
        if (!airbornePoseActive)
            return TrickPoseVerticalOrientationRequirement.Any;

        Vector3 up = rotation * Vector3.up;
        Vector3 forward = rotation * Vector3.forward;

        if (up.y < InvertedUpThreshold)
            return TrickPoseVerticalOrientationRequirement.Inverted;
        if (forward.y < -ChestPitchThreshold)
            return TrickPoseVerticalOrientationRequirement.ChestDown;
        if (forward.y > ChestPitchThreshold)
            return TrickPoseVerticalOrientationRequirement.ChestUp;

        return TrickPoseVerticalOrientationRequirement.Upright;
    }

    public static TrickPoseHorizontalOrientationRequirement DeriveHorizontal(Quaternion rotation, bool airbornePoseActive)
    {
        if (!airbornePoseActive)
            return TrickPoseHorizontalOrientationRequirement.Any;

        float rightY = (rotation * Vector3.right).y;
        if (rightY > SideRollThreshold)
            return TrickPoseHorizontalOrientationRequirement.RightSide;
        if (rightY < -SideRollThreshold)
            return TrickPoseHorizontalOrientationRequirement.LeftSide;

        return TrickPoseHorizontalOrientationRequirement.Upright;
    }

    public static TrickPoseVerticalOrientationRequirement DerivePreviewVertical(Quaternion rotation, bool airbornePoseActive)
    {
        // Preview presentation should use the same semantic axes as runtime derivation:
        // forward/chest Y = chest up/down, up Y = inverted.
        return DeriveVertical(rotation, airbornePoseActive);
    }

    public static TrickPoseHorizontalOrientationRequirement DerivePreviewHorizontal(Quaternion rotation, bool airbornePoseActive)
    {
        // Preview presentation should use the same semantic axes as runtime derivation:
        // right Y = side/roll state.
        return DeriveHorizontal(rotation, airbornePoseActive);
    }

    public static SkiController.AerialOrientationModifier ToLegacyOrientationModifier(
        TrickPoseVerticalOrientationRequirement vertical,
        TrickPoseHorizontalOrientationRequirement horizontal,
        TrickPoseMotionStateRequirement motion)
    {
        if (vertical == TrickPoseVerticalOrientationRequirement.Inverted)
            return SkiController.AerialOrientationModifier.Inverted;
        if (vertical == TrickPoseVerticalOrientationRequirement.ChestDown)
            return SkiController.AerialOrientationModifier.ChestDown;
        if (vertical == TrickPoseVerticalOrientationRequirement.ChestUp)
            return SkiController.AerialOrientationModifier.ChestUp;
        if (horizontal == TrickPoseHorizontalOrientationRequirement.LeftSide ||
            horizontal == TrickPoseHorizontalOrientationRequirement.RightSide)
            return SkiController.AerialOrientationModifier.OnSide;
        if (motion == TrickPoseMotionStateRequirement.Rising)
            return SkiController.AerialOrientationModifier.Rising;
        if (motion == TrickPoseMotionStateRequirement.Diving)
            return SkiController.AerialOrientationModifier.Diving;

        return SkiController.AerialOrientationModifier.None;
    }

    public static bool ShouldSuppressLegacyPreviewModifier(
        bool hasPresentationRotation,
        TrickPoseVerticalOrientationRequirement vertical,
        TrickPoseHorizontalOrientationRequirement horizontal)
    {
        return hasPresentationRotation && HasExplicitOrientationPresentation(vertical, horizontal);
    }

    public static bool ValidatePresentationMapping(
        TrickPoseVerticalOrientationRequirement requestedVertical,
        TrickPoseHorizontalOrientationRequirement requestedHorizontal,
        out TrickPoseVerticalOrientationRequirement derivedVertical,
        out TrickPoseHorizontalOrientationRequirement derivedHorizontal)
    {
        if (!TryBuildPresentationEuler(requestedVertical, requestedHorizontal, out Vector3 euler))
        {
            derivedVertical = requestedVertical == TrickPoseVerticalOrientationRequirement.Any
                ? TrickPoseVerticalOrientationRequirement.Any
                : TrickPoseVerticalOrientationRequirement.Upright;
            derivedHorizontal = requestedHorizontal == TrickPoseHorizontalOrientationRequirement.Any
                ? TrickPoseHorizontalOrientationRequirement.Any
                : TrickPoseHorizontalOrientationRequirement.Upright;
            return true;
        }

        Quaternion rotation = Quaternion.Euler(euler);
        derivedVertical = DerivePreviewVertical(rotation, true);
        derivedHorizontal = DerivePreviewHorizontal(rotation, true);

        bool verticalMatches = requestedVertical == TrickPoseVerticalOrientationRequirement.Any ||
                               requestedVertical == TrickPoseVerticalOrientationRequirement.Upright ||
                               derivedVertical == requestedVertical;
        bool horizontalMatches = requestedHorizontal == TrickPoseHorizontalOrientationRequirement.Any ||
                                 requestedHorizontal == TrickPoseHorizontalOrientationRequirement.Upright ||
                                 derivedHorizontal == requestedHorizontal;
        return verticalMatches && horizontalMatches;
    }
}
