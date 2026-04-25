using System;
using System.Collections.Generic;

[Serializable]
public enum TrickPoseCoverageAssignmentState
{
    Unassigned = 0,
    AssignedToExistingEntry = 1,
    AssignedToPlaceholder = 2
}

[Serializable]
public enum TrickPoseCoverageSlotValidationStatus
{
    Covered = 0,
    Ambiguous = 1,
    Suppressed = 2,
    Gap = 3,
    Excluded = 4
}

[Serializable]
public sealed class TrickPoseCoverageSlot
{
    public string slotId;
    public string shortLabel;
    public bool airborne = true;
    public bool poseHeld = true;
    public SkiController.AerialPoseFamily poseFamily = SkiController.AerialPoseFamily.None;
    public SkiController.AerialPoseShape poseShape = SkiController.AerialPoseShape.None;
    public TrickPoseVerticalOrientationRequirement verticalOrientation = TrickPoseVerticalOrientationRequirement.Any;
    public TrickPoseHorizontalOrientationRequirement horizontalOrientation = TrickPoseHorizontalOrientationRequirement.Any;
    public TrickPoseMotionStateRequirement motionState = TrickPoseMotionStateRequirement.Any;
    public SkiController.AerialOrientationModifier orientationModifier = SkiController.AerialOrientationModifier.None;
    public TrickPoseSpinDirectionRequirement spinDirection = TrickPoseSpinDirectionRequirement.Any;
    public TrickPoseFlipDirectionRequirement flipDirection = TrickPoseFlipDirectionRequirement.Any;
    public string yawBucketId;
    public string pitchBucketId;
    public string rollBucketId;
    public string totalSpeedBucketId;
    public TrickPoseAngularVelocityRange yawAngularVelocityRange;
    public TrickPoseAngularVelocityRange pitchAngularVelocityRange;
    public TrickPoseAngularVelocityRange rollAngularVelocityRange;
    public TrickPoseAngularVelocityRange totalAngularSpeedRange;
    public TrickPoseCoverageAssignmentState assignmentState;
    public TrickPoseEntry assignedEntry;
    public int assignedEntryIndex = -1;
    public TrickPoseEditorPreviewContext representativeContext;
    public TrickPoseCoverageSlotValidationStatus validationStatus = TrickPoseCoverageSlotValidationStatus.Gap;
    public bool excluded;
    public string exclusionReason;
    public string summary;
    public List<int> candidateEntryIndices = new List<int>();
    public List<int> ambiguousEntryIndices = new List<int>();
    public List<int> suppressedEntryIndices = new List<int>();
    public List<string> candidateEntryLabels = new List<string>();

    public bool HasAssignment => assignmentState != TrickPoseCoverageAssignmentState.Unassigned && assignedEntry != null;
}
