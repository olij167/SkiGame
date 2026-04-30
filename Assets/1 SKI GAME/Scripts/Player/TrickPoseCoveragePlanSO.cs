using System;
using System.Collections.Generic;
using UnityEngine;

public enum TrickPoseCoverageAxis
{
    None = 0,
    AirborneState = 1,
    PoseHeldState = 2,
    PoseFamily = 3,
    PoseShape = 4,
    VerticalOrientation = 5,
    HorizontalOrientation = 6,
    TravelFacing = 7,
    MotionState = 8,
    Orientation = 9,
    SpinDirection = 10,
    FlipDirection = 11,
    YawBucket = 12,
    PitchBucket = 13,
    RollBucket = 14,
    TotalSpeedBucket = 15
}

[Serializable]
public sealed class TrickPoseCoverageAngularBucket
{
    public string id = "Bucket";
    public Vector2 range = new Vector2(-120f, 120f);

    public Vector2 GetSortedRange()
    {
        return range.x <= range.y ? range : new Vector2(range.y, range.x);
    }
}

[Serializable]
public sealed class TrickPoseCoverageExclusionRule
{
    public string label = "Excluded Combination";
    public bool matchAirborneState;
    public bool airborne = true;
    public bool matchPoseHeldState;
    public bool poseHeld = true;
    public SkiController.AerialPoseFamily poseFamily = SkiController.AerialPoseFamily.None;
    public SkiController.AerialPoseShape poseShape = SkiController.AerialPoseShape.None;
    public TrickPoseVerticalOrientationRequirement verticalOrientation = TrickPoseVerticalOrientationRequirement.Any;
    public TrickPoseHorizontalOrientationRequirement horizontalOrientation = TrickPoseHorizontalOrientationRequirement.Any;
    public TrickPoseTravelFacingRequirement travelFacing = TrickPoseTravelFacingRequirement.Any;
  
    public TrickPoseMotionStateRequirement motionState = TrickPoseMotionStateRequirement.Any;
    public SkiController.AerialOrientationModifier orientationModifier = SkiController.AerialOrientationModifier.None;
    public TrickPoseSpinDirectionRequirement spinDirection = TrickPoseSpinDirectionRequirement.Any;
    public TrickPoseFlipDirectionRequirement flipDirection = TrickPoseFlipDirectionRequirement.Any;
    public string yawBucketId;
    public string pitchBucketId;
    public string rollBucketId;
    public string totalSpeedBucketId;
}

[CreateAssetMenu(fileName = "TrickPoseCoveragePlan", menuName = "SkiGame/Trick Pose Coverage Plan")]
public sealed class TrickPoseCoveragePlanSO : ScriptableObject
{
    [Header("Coverage Participation")]
    public bool includeAirborne = true;
    public bool includeGrounded;
    public bool includePoseHeld = true;
    public bool includePoseReleased;
    public bool usePoseFamily = true;
    public bool usePoseShape = true;
    public bool useVerticalOrientation = true;
    public bool useHorizontalOrientation = true;
    public bool useTravelFacing = true;

    public bool useMotionState;
    public bool useOrientation;
    public bool useSpinDirection;
    public bool useFlipDirection;
    public bool useYawBuckets;
    public bool usePitchBuckets;
    public bool useRollBuckets;
    public bool useTotalSpeedBuckets;

    [Header("Allowed Values")]
    public List<SkiController.AerialPoseFamily> poseFamilies = new List<SkiController.AerialPoseFamily>
    {
        SkiController.AerialPoseFamily.Neutral,
        SkiController.AerialPoseFamily.Left,
        SkiController.AerialPoseFamily.Right,
        SkiController.AerialPoseFamily.Spread
    };
    public List<SkiController.AerialPoseShape> poseShapes = new List<SkiController.AerialPoseShape>
    {
        SkiController.AerialPoseShape.Neutral,
        SkiController.AerialPoseShape.Compact,
        SkiController.AerialPoseShape.Driving,
        SkiController.AerialPoseShape.LaidOut
    };
    public List<SkiController.AerialOrientationModifier> orientationModifiers = new List<SkiController.AerialOrientationModifier>
    {
        SkiController.AerialOrientationModifier.None,
        SkiController.AerialOrientationModifier.Switch,
        SkiController.AerialOrientationModifier.Inverted,
        SkiController.AerialOrientationModifier.OnSide,
        SkiController.AerialOrientationModifier.ChestDown,
        SkiController.AerialOrientationModifier.ChestUp
    };
    public List<TrickPoseVerticalOrientationRequirement> verticalOrientations = new List<TrickPoseVerticalOrientationRequirement>
    {
        TrickPoseVerticalOrientationRequirement.Upright,
        TrickPoseVerticalOrientationRequirement.ChestDown,
        TrickPoseVerticalOrientationRequirement.Inverted,
        TrickPoseVerticalOrientationRequirement.ChestUp
    };
    public List<TrickPoseHorizontalOrientationRequirement> horizontalOrientations = new List<TrickPoseHorizontalOrientationRequirement>
    {
        TrickPoseHorizontalOrientationRequirement.Upright,
        TrickPoseHorizontalOrientationRequirement.LeftSide,
        TrickPoseHorizontalOrientationRequirement.RightSide
    };
    public List<TrickPoseTravelFacingRequirement> travelFacings = new List<TrickPoseTravelFacingRequirement>
    {
        TrickPoseTravelFacingRequirement.Forward,
        TrickPoseTravelFacingRequirement.Backward,
        TrickPoseTravelFacingRequirement.Left,
        TrickPoseTravelFacingRequirement.Right
    };
    
    public List<TrickPoseMotionStateRequirement> motionStates = new List<TrickPoseMotionStateRequirement>
    {
        TrickPoseMotionStateRequirement.Any,
        TrickPoseMotionStateRequirement.Rising,
        TrickPoseMotionStateRequirement.Diving
    };
    public List<TrickPoseSpinDirectionRequirement> spinDirections = new List<TrickPoseSpinDirectionRequirement>
    {
        TrickPoseSpinDirectionRequirement.Clockwise,
        TrickPoseSpinDirectionRequirement.CounterClockwise
    };
    public List<TrickPoseFlipDirectionRequirement> flipDirections = new List<TrickPoseFlipDirectionRequirement>
    {
        TrickPoseFlipDirectionRequirement.Frontflip,
        TrickPoseFlipDirectionRequirement.Backflip
    };

    [Header("Angular Buckets")]
    public List<TrickPoseCoverageAngularBucket> yawBuckets = new List<TrickPoseCoverageAngularBucket>();
    public List<TrickPoseCoverageAngularBucket> pitchBuckets = new List<TrickPoseCoverageAngularBucket>();
    public List<TrickPoseCoverageAngularBucket> rollBuckets = new List<TrickPoseCoverageAngularBucket>();
    public List<TrickPoseCoverageAngularBucket> totalSpeedBuckets = new List<TrickPoseCoverageAngularBucket>();

    [Header("Exclusions")]
    public List<TrickPoseCoverageExclusionRule> exclusions = new List<TrickPoseCoverageExclusionRule>();

    [Header("Matrix Layout")]
    public TrickPoseCoverageAxis rowAxis = TrickPoseCoverageAxis.PoseFamily;
    public TrickPoseCoverageAxis columnAxis = TrickPoseCoverageAxis.PoseShape;
    public TrickPoseCoverageAxis pageAxis = TrickPoseCoverageAxis.VerticalOrientation;
}
