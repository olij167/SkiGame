using UnityEngine;

public static class TrickPoseOrientationUtility
{
    private const float InvertedUpThreshold = -0.2f;
    private const float ChestPitchThreshold = 0.45f;
    private const float SideRollThreshold = 0.55f;
    private const float TravelFacingMinPlanarSpeed = 0.75f;
    private const float TravelFacingForwardDotThreshold = 0.6f;
    private const float TravelFacingSideDotThreshold = 0.45f;

    public static string VerticalAxisLabel => "Pitch / Chest Orientation";
    public static string HorizontalAxisLabel => "Roll / Side Orientation";
    public static string TravelFacingAxisLabel => "Travel Facing";

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
            TrickPoseHorizontalOrientationRequirement.LeftSide =>
                "Skier is rolled onto their left side. Local right vector points upward.",
            TrickPoseHorizontalOrientationRequirement.RightSide =>
                "Skier is rolled onto their right side. Local right vector points downward.",
            TrickPoseHorizontalOrientationRequirement.Upright =>
                "Skier is not rolled far enough onto either side.",
            _ =>
                "Ignore roll/side orientation."
        };
    }

    public static string GetTravelFacingTooltip(TrickPoseTravelFacingRequirement requirement)
    {
        return requirement switch
        {
            TrickPoseTravelFacingRequirement.Forward => "Chest/forward vector points mostly along travel.",
            TrickPoseTravelFacingRequirement.Backward => "Chest/forward vector points mostly opposite travel.",
            TrickPoseTravelFacingRequirement.Left => "Chest/forward vector points left relative to travel.",
            TrickPoseTravelFacingRequirement.Right => "Chest/forward vector points right relative to travel.",
            _ => "Ignore facing relative to travel direction."
        };
    }

    public static bool HasExplicitOrientationPresentation(
        TrickPoseVerticalOrientationRequirement vertical,
        TrickPoseHorizontalOrientationRequirement horizontal,
        TrickPoseTravelFacingRequirement travelFacing)
    {
        return (vertical != TrickPoseVerticalOrientationRequirement.Any &&
                vertical != TrickPoseVerticalOrientationRequirement.Upright) ||
               horizontal == TrickPoseHorizontalOrientationRequirement.LeftSide ||
               horizontal == TrickPoseHorizontalOrientationRequirement.RightSide ||
               travelFacing == TrickPoseTravelFacingRequirement.Backward ||
               travelFacing == TrickPoseTravelFacingRequirement.Left ||
               travelFacing == TrickPoseTravelFacingRequirement.Right;
    }

    public static bool HasExplicitOrientationPresentation(
        TrickPoseVerticalOrientationRequirement vertical,
        TrickPoseHorizontalOrientationRequirement horizontal)
    {
        return HasExplicitOrientationPresentation(vertical, horizontal, TrickPoseTravelFacingRequirement.Any);
    }

    public static bool TryBuildPresentationEuler(
        TrickPoseVerticalOrientationRequirement vertical,
        TrickPoseHorizontalOrientationRequirement horizontal,
        TrickPoseTravelFacingRequirement travelFacing,
        out Vector3 euler)
    {
        euler = Vector3.zero;

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

        if (horizontal == TrickPoseHorizontalOrientationRequirement.LeftSide)
        {
            euler.z = 90f;
        }
        else if (horizontal == TrickPoseHorizontalOrientationRequirement.RightSide)
        {
            euler.z = -90f;
        }

        euler.y = travelFacing switch
        {
            TrickPoseTravelFacingRequirement.Backward => 180f,
            TrickPoseTravelFacingRequirement.Left => -90f,
            TrickPoseTravelFacingRequirement.Right => 90f,
            _ => 0f
        };

        return HasExplicitOrientationPresentation(vertical, horizontal, travelFacing);
    }

    public static bool TryBuildPresentationEuler(
        TrickPoseVerticalOrientationRequirement vertical,
        TrickPoseHorizontalOrientationRequirement horizontal,
        out Vector3 euler)
    {
        return TryBuildPresentationEuler(vertical, horizontal, TrickPoseTravelFacingRequirement.Any, out euler);
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
            return TrickPoseHorizontalOrientationRequirement.LeftSide;

        if (rightY < -SideRollThreshold)
            return TrickPoseHorizontalOrientationRequirement.RightSide;

        return TrickPoseHorizontalOrientationRequirement.Upright;
    }

    public static TrickPoseTravelFacingRequirement DeriveTravelFacing(
        Quaternion rotation,
        Vector3 velocity,
        Vector3 planeNormal,
        bool airbornePoseActive)
    {
        if (!airbornePoseActive)
            return TrickPoseTravelFacingRequirement.Any;

        Vector3 up = planeNormal.sqrMagnitude > 0.0001f
            ? planeNormal.normalized
            : Vector3.up;
        Vector3 travelDirection = Vector3.ProjectOnPlane(velocity, up);
        if (travelDirection.sqrMagnitude < TravelFacingMinPlanarSpeed * TravelFacingMinPlanarSpeed)
            return TrickPoseTravelFacingRequirement.Any;

        Vector3 forwardDirection = Vector3.ProjectOnPlane(rotation * Vector3.forward, up);
        if (forwardDirection.sqrMagnitude < 0.0001f)
            return TrickPoseTravelFacingRequirement.Any;

        travelDirection.Normalize();
        forwardDirection.Normalize();

        float forwardDot = Vector3.Dot(forwardDirection, travelDirection);
        if (forwardDot >= TravelFacingForwardDotThreshold)
            return TrickPoseTravelFacingRequirement.Forward;
        if (forwardDot <= -TravelFacingForwardDotThreshold)
            return TrickPoseTravelFacingRequirement.Backward;

        float sideDot = Vector3.Dot(forwardDirection, Vector3.Cross(up, travelDirection).normalized);
        if (sideDot >= TravelFacingSideDotThreshold)
            return TrickPoseTravelFacingRequirement.Right;
        if (sideDot <= -TravelFacingSideDotThreshold)
            return TrickPoseTravelFacingRequirement.Left;

        float signedAngle = Vector3.SignedAngle(travelDirection, forwardDirection, up);
        return signedAngle >= 0f
            ? TrickPoseTravelFacingRequirement.Right
            : TrickPoseTravelFacingRequirement.Left;
    }

    public static TrickPoseVerticalOrientationRequirement DerivePreviewVertical(Quaternion rotation, bool airbornePoseActive)
    {
        return DeriveVertical(rotation, airbornePoseActive);
    }

    public static TrickPoseHorizontalOrientationRequirement DerivePreviewHorizontal(Quaternion rotation, bool airbornePoseActive)
    {
        return DeriveHorizontal(rotation, airbornePoseActive);
    }

    public static TrickPoseTravelFacingRequirement DerivePreviewTravelFacing(Quaternion rotation, bool airbornePoseActive)
    {
        return DeriveTravelFacing(rotation, Vector3.forward * 10f, Vector3.up, airbornePoseActive);
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
        TrickPoseHorizontalOrientationRequirement horizontal,
        TrickPoseTravelFacingRequirement travelFacing)
    {
        return hasPresentationRotation && HasExplicitOrientationPresentation(vertical, horizontal, travelFacing);
    }

    public static bool ShouldSuppressLegacyPreviewModifier(
        bool hasPresentationRotation,
        TrickPoseVerticalOrientationRequirement vertical,
        TrickPoseHorizontalOrientationRequirement horizontal)
    {
        return ShouldSuppressLegacyPreviewModifier(hasPresentationRotation, vertical, horizontal, TrickPoseTravelFacingRequirement.Any);
    }

    public static bool ValidatePresentationMapping(
        TrickPoseVerticalOrientationRequirement requestedVertical,
        TrickPoseHorizontalOrientationRequirement requestedHorizontal,
        TrickPoseTravelFacingRequirement requestedTravelFacing,
        out TrickPoseVerticalOrientationRequirement derivedVertical,
        out TrickPoseHorizontalOrientationRequirement derivedHorizontal,
        out TrickPoseTravelFacingRequirement derivedTravelFacing)
    {
        if (!TryBuildPresentationEuler(requestedVertical, requestedHorizontal, requestedTravelFacing, out Vector3 euler))
        {
            derivedVertical = requestedVertical == TrickPoseVerticalOrientationRequirement.Any
                ? TrickPoseVerticalOrientationRequirement.Any
                : TrickPoseVerticalOrientationRequirement.Upright;
            derivedHorizontal = requestedHorizontal == TrickPoseHorizontalOrientationRequirement.Any
                ? TrickPoseHorizontalOrientationRequirement.Any
                : TrickPoseHorizontalOrientationRequirement.Upright;
            derivedTravelFacing = requestedTravelFacing == TrickPoseTravelFacingRequirement.Any
                ? TrickPoseTravelFacingRequirement.Any
                : TrickPoseTravelFacingRequirement.Forward;
            return true;
        }

        Quaternion rotation = Quaternion.Euler(euler);
        derivedVertical = DerivePreviewVertical(rotation, true);
        derivedHorizontal = DerivePreviewHorizontal(rotation, true);
        derivedTravelFacing = DerivePreviewTravelFacing(rotation, true);

        bool verticalMatches = requestedVertical == TrickPoseVerticalOrientationRequirement.Any ||
                               requestedVertical == TrickPoseVerticalOrientationRequirement.Upright ||
                               derivedVertical == requestedVertical;
        bool horizontalMatches = requestedHorizontal == TrickPoseHorizontalOrientationRequirement.Any ||
                                 requestedHorizontal == TrickPoseHorizontalOrientationRequirement.Upright ||
                                 derivedHorizontal == requestedHorizontal;
        bool travelMatches = requestedTravelFacing == TrickPoseTravelFacingRequirement.Any ||
                             requestedTravelFacing == TrickPoseTravelFacingRequirement.Forward ||
                             derivedTravelFacing == requestedTravelFacing;
        return verticalMatches && horizontalMatches && travelMatches;
    }

    public static bool ValidatePresentationMapping(
        TrickPoseVerticalOrientationRequirement requestedVertical,
        TrickPoseHorizontalOrientationRequirement requestedHorizontal,
        out TrickPoseVerticalOrientationRequirement derivedVertical,
        out TrickPoseHorizontalOrientationRequirement derivedHorizontal)
    {
        bool valid = ValidatePresentationMapping(
            requestedVertical,
            requestedHorizontal,
            TrickPoseTravelFacingRequirement.Any,
            out derivedVertical,
            out derivedHorizontal,
            out _);
        return valid;
    }
}
