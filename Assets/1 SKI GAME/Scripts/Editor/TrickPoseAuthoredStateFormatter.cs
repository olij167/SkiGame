public static class TrickPoseAuthoredStateFormatter
{
    public static string Format(
        SkiController.AerialPoseFamily family,
        SkiController.AerialPoseShape shape,
        TrickPoseVerticalOrientationRequirement vertical,
        TrickPoseHorizontalOrientationRequirement horizontal,
        TrickPoseTravelFacingRequirement travelFacing,
        TrickPoseMotionStateRequirement motion)
    {
        return $"{family} / {shape} | Pitch/Chest: {vertical} | Roll/Side: {horizontal} | Travel: {travelFacing} | Motion: {motion}";
    }

    public static string Format(TrickPoseEntry entry)
    {
        if (entry == null)
            return "(no entry)";

        return Format(
            entry.GetEffectiveRequiredPoseFamily(),
            entry.GetEffectiveRequiredPoseShape(),
            entry.requiredVerticalOrientation,
            entry.requiredHorizontalOrientation,
            entry.requiredTravelFacing,
            entry.requiredMotionState);
    }

    public static string Format(TrickPoseCoverageSlot slot)
    {
        if (slot == null)
            return "(no slot)";

        return Format(
            slot.poseFamily,
            slot.poseShape,
            slot.verticalOrientation,
            slot.horizontalOrientation,
            slot.travelFacing,
            slot.motionState);
    }

    public static string Format(TrickPoseEditorPreviewContext context)
    {
        if (context == null)
            return "(no context)";

        return Format(
            context.poseFamily,
            context.poseShape,
            context.verticalOrientation,
            context.horizontalOrientation,
            context.travelFacing,
            context.motionState);
    }
}