using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public enum TrickPoseRigConformMode
{
    PreserveEndpointAdjustJoint = 0,
    PreserveJointAdjustEndpoint = 1,
    PreserveRootScaleChainDirection = 2
}

[Serializable]
public enum TrickPoseHeadAssistMode
{
    Off = 0,
    PreserveAuthoredHeadOffset = 1,
    StrictLockToAnchor = 2
}

[Serializable]
public enum TrickPoseRigEditablePoint
{
    Body = 0,
    Head = 1,
    LeftSki = 2,
    RightSki = 3,
    LeftPole = 4,
    RightPole = 5,
    LeftElbow = 6,
    RightElbow = 7,
    LeftKnee = 8,
    RightKnee = 9
}

[Serializable]
public struct TrickPoseRigSegmentLengthSet
{
    public bool captured;
    [Min(0f)] public float leftUpperArm;
    [Min(0f)] public float leftLowerArm;
    [Min(0f)] public float rightUpperArm;
    [Min(0f)] public float rightLowerArm;
    [Min(0f)] public float leftUpperLeg;
    [Min(0f)] public float leftLowerLeg;
    [Min(0f)] public float rightUpperLeg;
    [Min(0f)] public float rightLowerLeg;
}

[Serializable]
public class TrickPoseRigAssistSettings
{
    public TrickPoseHeadAssistMode headAssistMode = TrickPoseHeadAssistMode.PreserveAuthoredHeadOffset;
    [Min(0f)] public float headAnchorTolerance = 0.15f;
    [Obsolete("Use headAssistMode instead.")]
    public bool constrainHeadToNeckAnchor;
    public bool enablePreviewSolve = true;
    public bool enableRuntimeHeadSolve;
    public bool enableSceneAuthoringConstraints;
    public bool lockHeadPositionInSceneAuthoring;
    public bool enforceLimbLengthsInSceneAuthoring;
    public bool solveLimbsAfterBodyEdit;
    public bool preserveLimbLengthsInPreview = true;
    public bool showLimbLengthGuidesInScene = true;
    [Min(0f)] public float segmentLengthTolerance = 0.08f;
    public TrickPoseRigConformMode conformMode = TrickPoseRigConformMode.PreserveEndpointAdjustJoint;
    public bool hasCapturedNeckAnchor;
    public Vector3 neckAnchorBodyLocal = new Vector3(0f, 0.2f, 0f);
    public TrickPoseRigSegmentLengthSet restSegments;
}

public struct TrickPoseRigAssistLimbGuide
{
    public string chainName;
    public Vector3 root;
    public Vector3 currentJoint;
    public Vector3 currentEndpoint;
    public Vector3 guideJoint;
    public Vector3 guideEndpoint;
    public float currentUpperLength;
    public float targetUpperLength;
    public float upperDelta;
    public float currentLowerLength;
    public float targetLowerLength;
    public float lowerDelta;
    public bool valid;
    public string warning;
}

public struct TrickPoseRigAssistReferenceData
{
    public bool hasBody;
    public bool hasHead;
    public bool hasNeckAnchor;
    public Vector3 neckAnchorBodyLocal;
    public Vector3 defaultHeadBodyLocal;
    public Quaternion defaultHeadBodyRotation;
    public bool hasLeftShoulder;
    public Vector3 leftShoulderBodyLocal;
    public bool hasRightShoulder;
    public Vector3 rightShoulderBodyLocal;
    public bool hasLeftHip;
    public Vector3 leftHipBodyLocal;
    public bool hasRightHip;
    public Vector3 rightHipBodyLocal;
}

public struct TrickPoseRigAssistValidationMessage
{
    public readonly string chainName;
    public readonly float currentLength;
    public readonly float expectedLength;
    public readonly float delta;
    public readonly bool unreachable;

    public TrickPoseRigAssistValidationMessage(string chainName, float currentLength, float expectedLength, float delta, bool unreachable)
    {
        this.chainName = chainName;
        this.currentLength = currentLength;
        this.expectedLength = expectedLength;
        this.delta = delta;
        this.unreachable = unreachable;
    }

    public string ToDisplayString(float tolerance)
    {
        if (unreachable)
            return $"{chainName}: endpoint is unreachable with captured rest lengths.";

        if (expectedLength <= 0f)
            return $"{chainName}: distance {currentLength:0.###}m exceeds tolerance {tolerance:0.###}m.";

        return $"{chainName}: current {currentLength:0.###}m, expected {expectedLength:0.###}m, delta {delta:0.###}m (tol {tolerance:0.###}m).";
    }
}

public static class TrickPoseRigAssistUtility
{
    private enum ChainId
    {
        LeftArm,
        RightArm,
        LeftLeg,
        RightLeg
    }

    private struct ChainDefinition
    {
        public readonly ChainId id;
        public readonly string name;
        public readonly bool arm;
        public readonly bool left;
        public readonly Vector3 fallbackAxis;
        public readonly float upperLength;
        public readonly float lowerLength;

        public ChainDefinition(ChainId id, string name, bool arm, bool left, Vector3 fallbackAxis, float upperLength, float lowerLength)
        {
            this.id = id;
            this.name = name;
            this.arm = arm;
            this.left = left;
            this.fallbackAxis = fallbackAxis;
            this.upperLength = upperLength;
            this.lowerLength = lowerLength;
        }
    }

    public static TrickPoseRigAssistReferenceData BuildReferenceData(
        TrickPoseRigSnapshot defaultSnapshot,
        TrickPoseRigAssistSettings settings,
        SkierLimbLineVisual limbVisual)
    {
        return BuildReferenceData(defaultSnapshot, settings, limbVisual, useSettingsAnchor: true);
    }

    public static TrickPoseRigAssistReferenceData BuildCaptureReferenceData(
        TrickPoseRigSnapshot snapshot,
        SkierLimbLineVisual limbVisual)
    {
        return BuildReferenceData(snapshot, null, limbVisual, useSettingsAnchor: false);
    }

    private static TrickPoseRigAssistReferenceData BuildReferenceData(
        TrickPoseRigSnapshot defaultSnapshot,
        TrickPoseRigAssistSettings settings,
        SkierLimbLineVisual limbVisual,
        bool useSettingsAnchor)
    {
        TrickPoseRigAssistReferenceData data = new TrickPoseRigAssistReferenceData
        {
            hasBody = defaultSnapshot != null && defaultSnapshot.body.hasValue,
            hasHead = defaultSnapshot != null && defaultSnapshot.head.hasValue
        };

        if (data.hasBody && data.hasHead)
        {
            data.defaultHeadBodyLocal = ToBodyLocal(defaultSnapshot.body, defaultSnapshot.head.localPosition);
            data.defaultHeadBodyRotation = Quaternion.Inverse(defaultSnapshot.body.localRotation) * defaultSnapshot.head.localRotation;
        }

        if (useSettingsAnchor && settings != null)
        {
            data.hasNeckAnchor = true;
            data.neckAnchorBodyLocal = settings.neckAnchorBodyLocal;
        }
        else if (data.hasBody && data.hasHead)
        {
            data.hasNeckAnchor = true;
            data.neckAnchorBodyLocal = data.defaultHeadBodyLocal;
        }

        if (limbVisual != null)
        {
            data.hasLeftShoulder = limbVisual.TryGetBodyLocalAnchor(true, true, out data.leftShoulderBodyLocal);
            data.hasRightShoulder = limbVisual.TryGetBodyLocalAnchor(false, true, out data.rightShoulderBodyLocal);
            data.hasLeftHip = limbVisual.TryGetBodyLocalAnchor(true, false, out data.leftHipBodyLocal);
            data.hasRightHip = limbVisual.TryGetBodyLocalAnchor(false, false, out data.rightHipBodyLocal);
        }

        return data;
    }

    public static void CaptureRestPose(
        TrickPoseRigSnapshot snapshot,
        TrickPoseRigAssistReferenceData referenceData,
        TrickPoseRigAssistSettings settings)
    {
        if (snapshot == null || settings == null)
            return;

        AssignCapturedLengths(snapshot, referenceData, ref settings.restSegments);
    }

    public static void CaptureHeadAnchor(
        TrickPoseRigSnapshot snapshot,
        TrickPoseRigAssistSettings settings)
    {
        if (snapshot == null || settings == null || !snapshot.body.hasValue || !snapshot.head.hasValue)
            return;

        settings.hasCapturedNeckAnchor = true;
        settings.neckAnchorBodyLocal = ToBodyLocal(snapshot.body, snapshot.head.localPosition);
    }

    public static void ResetHeadAnchorToDefault(
        TrickPoseRigSnapshot defaultSnapshot,
        TrickPoseRigAssistSettings settings)
    {
        CaptureHeadAnchor(defaultSnapshot, settings);
    }

    public static List<TrickPoseRigAssistValidationMessage> ValidateSnapshot(
        TrickPoseRigSnapshot snapshot,
        TrickPoseRigAssistReferenceData referenceData,
        TrickPoseRigAssistSettings settings)
    {
        List<TrickPoseRigAssistValidationMessage> messages = new List<TrickPoseRigAssistValidationMessage>();
        if (snapshot == null || settings == null)
            return messages;

        if (!settings.restSegments.captured)
            return messages;

        foreach (ChainDefinition chain in EnumerateChains(settings.restSegments))
        {
            if (!TryGetChainPositions(snapshot, referenceData, chain.id, out Vector3 root, out Vector3 joint, out Vector3 end))
                continue;

            float currentUpper = Vector3.Distance(root, joint);
            float currentLower = Vector3.Distance(joint, end);
            float upperDelta = Mathf.Abs(currentUpper - chain.upperLength);
            float lowerDelta = Mathf.Abs(currentLower - chain.lowerLength);
            bool unreachable = !IsReachable(root, end, chain.upperLength, chain.lowerLength);

            if (upperDelta > settings.segmentLengthTolerance)
                messages.Add(new TrickPoseRigAssistValidationMessage($"{chain.name} upper", currentUpper, chain.upperLength, upperDelta, unreachable));

            if (lowerDelta > settings.segmentLengthTolerance)
                messages.Add(new TrickPoseRigAssistValidationMessage($"{chain.name} lower", currentLower, chain.lowerLength, lowerDelta, unreachable));
        }

        return messages;
    }

    public static List<TrickPoseRigAssistLimbGuide> BuildLimbLengthGuides(
        TrickPoseRigSnapshot snapshot,
        TrickPoseRigAssistReferenceData referenceData,
        TrickPoseRigAssistSettings settings)
    {
        List<TrickPoseRigAssistLimbGuide> guides = new List<TrickPoseRigAssistLimbGuide>();
        if (snapshot == null || settings == null || !settings.restSegments.captured)
            return guides;

        foreach (ChainDefinition chain in EnumerateChains(settings.restSegments))
        {
            TrickPoseRigAssistLimbGuide guide = new TrickPoseRigAssistLimbGuide
            {
                chainName = chain.name,
                targetUpperLength = chain.upperLength,
                targetLowerLength = chain.lowerLength
            };

            if (!TryGetChainPositions(snapshot, referenceData, chain.id, out guide.root, out guide.currentJoint, out guide.currentEndpoint))
            {
                guide.valid = false;
                guide.warning = $"{chain.name}: missing root, joint, or endpoint data.";
                guides.Add(guide);
                continue;
            }

            guide.currentUpperLength = Vector3.Distance(guide.root, guide.currentJoint);
            guide.currentLowerLength = Vector3.Distance(guide.currentJoint, guide.currentEndpoint);
            guide.upperDelta = Mathf.Abs(guide.currentUpperLength - guide.targetUpperLength);
            guide.lowerDelta = Mathf.Abs(guide.currentLowerLength - guide.targetLowerLength);

            Vector3 upperDirection = SafeDirection(guide.currentJoint - guide.root, guide.currentEndpoint - guide.root);
            if (upperDirection.sqrMagnitude < 0.0001f)
                upperDirection = SafeDirection(chain.fallbackAxis, Vector3.forward);

            Vector3 lowerDirection = SafeDirection(guide.currentEndpoint - guide.currentJoint, guide.currentEndpoint - guide.root);
            if (lowerDirection.sqrMagnitude < 0.0001f)
                lowerDirection = upperDirection;

            guide.guideJoint = guide.root + upperDirection * guide.targetUpperLength;
            guide.guideEndpoint = guide.guideJoint + lowerDirection * guide.targetLowerLength;
            guide.valid = true;
            guides.Add(guide);
        }

        return guides;
    }

    public static TrickPoseRigSnapshot SolveSnapshot(
        TrickPoseRigSnapshot rawSnapshot,
        TrickPoseRigSnapshot defaultSnapshot,
        TrickPoseRigAssistReferenceData referenceData,
        TrickPoseRigAssistSettings settings,
        bool applyHeadConstraint,
        bool applyLimbLengths)
    {
        if (rawSnapshot == null)
            return null;

        TrickPoseRigSnapshot solved = rawSnapshot.Clone();

        if (settings == null)
            return solved;

        if (applyLimbLengths && settings.preserveLimbLengthsInPreview && settings.restSegments.captured)
            SolveLimbLengths(solved, referenceData, settings.restSegments, TrickPoseRigConformMode.PreserveEndpointAdjustJoint);

        return solved;
    }

    public static bool SolveSceneEdit(
        TrickPoseRigSnapshot rawSnapshot,
        TrickPoseRigSnapshot previousValidSnapshot,
        TrickPoseRigSnapshot defaultSnapshot,
        TrickPoseRigAssistReferenceData referenceData,
        TrickPoseRigAssistSettings settings,
        TrickPoseRigEditablePoint editedPoint,
        out TrickPoseRigSnapshot solvedSnapshot,
        out string warning)
    {
        warning = null;
        solvedSnapshot = rawSnapshot?.Clone();
        if (solvedSnapshot == null || settings == null)
            return false;

        bool changed = false;
        return changed;
    }

    public static bool TryConformSnapshot(
        TrickPoseRigSnapshot snapshot,
        TrickPoseRigAssistReferenceData referenceData,
        TrickPoseRigAssistSettings settings,
        TrickPoseRigConformMode conformMode,
        out List<string> warnings)
    {
        warnings = new List<string>();
        if (snapshot == null || settings == null || !settings.restSegments.captured)
            return false;

        bool changed = false;
        foreach (ChainDefinition chain in EnumerateChains(settings.restSegments))
        {
                if (!TrySolveChain(snapshot, referenceData, chain, TrickPoseRigConformMode.PreserveRootScaleChainDirection, out string warning))
            {
                if (!string.IsNullOrWhiteSpace(warning))
                    warnings.Add(warning);
                continue;
            }

            changed = true;
        }

        return changed;
    }

    public static void ApplySolvedChainBackToEntry(
        TrickPoseEntry entry,
        TrickPoseRigSnapshot solvedSnapshot,
        TrickPoseRigSnapshot defaultSnapshot,
        TrickPoseRigConformMode conformMode)
    {
        if (entry == null || solvedSnapshot == null || defaultSnapshot == null)
            return;

        switch (conformMode)
        {
            case TrickPoseRigConformMode.PreserveEndpointAdjustJoint:
                WriteJointPose(ref entry.leftElbowPose, solvedSnapshot.leftElbow, defaultSnapshot.leftElbow);
                WriteJointPose(ref entry.rightElbowPose, solvedSnapshot.rightElbow, defaultSnapshot.rightElbow);
                WriteJointPose(ref entry.leftKneePose, solvedSnapshot.leftKnee, defaultSnapshot.leftKnee);
                WriteJointPose(ref entry.rightKneePose, solvedSnapshot.rightKnee, defaultSnapshot.rightKnee);
                break;

            case TrickPoseRigConformMode.PreserveJointAdjustEndpoint:
                WritePartPose(ref entry.leftPolePose, solvedSnapshot.leftPole, defaultSnapshot.leftPole);
                WritePartPose(ref entry.rightPolePose, solvedSnapshot.rightPole, defaultSnapshot.rightPole);
                WritePartPose(ref entry.leftSkiPose, solvedSnapshot.leftSki, defaultSnapshot.leftSki);
                WritePartPose(ref entry.rightSkiPose, solvedSnapshot.rightSki, defaultSnapshot.rightSki);
                break;

            case TrickPoseRigConformMode.PreserveRootScaleChainDirection:
                WriteJointPose(ref entry.leftElbowPose, solvedSnapshot.leftElbow, defaultSnapshot.leftElbow);
                WriteJointPose(ref entry.rightElbowPose, solvedSnapshot.rightElbow, defaultSnapshot.rightElbow);
                WriteJointPose(ref entry.leftKneePose, solvedSnapshot.leftKnee, defaultSnapshot.leftKnee);
                WriteJointPose(ref entry.rightKneePose, solvedSnapshot.rightKnee, defaultSnapshot.rightKnee);
                WritePartPose(ref entry.leftPolePose, solvedSnapshot.leftPole, defaultSnapshot.leftPole);
                WritePartPose(ref entry.rightPolePose, solvedSnapshot.rightPole, defaultSnapshot.rightPole);
                WritePartPose(ref entry.leftSkiPose, solvedSnapshot.leftSki, defaultSnapshot.leftSki);
                WritePartPose(ref entry.rightSkiPose, solvedSnapshot.rightSki, defaultSnapshot.rightSki);
                break;
        }
    }

    public static void ApplySolvedSceneEditBackToEntry(
        TrickPoseEntry entry,
        TrickPoseRigSnapshot solvedSnapshot,
        TrickPoseRigSnapshot defaultSnapshot,
        TrickPoseRigEditablePoint editedPoint,
        bool includeAffectedChain,
        bool includeHead,
        bool includeBody)
    {
        ApplySolvedSceneEditBackToEntry(entry, solvedSnapshot, defaultSnapshot, editedPoint, includeAffectedChain, includeHead, includeBody, false);
    }

    public static void ApplySolvedSceneEditBackToEntry(
        TrickPoseEntry entry,
        TrickPoseRigSnapshot solvedSnapshot,
        TrickPoseRigSnapshot defaultSnapshot,
        TrickPoseRigEditablePoint editedPoint,
        bool includeAffectedChain,
        bool includeHead,
        bool includeBody,
        bool headRotationOnly)
    {
        if (entry == null || solvedSnapshot == null || defaultSnapshot == null)
            return;

        if (includeBody || editedPoint == TrickPoseRigEditablePoint.Body)
            WritePartPose(ref entry.bodyPose, solvedSnapshot.body, defaultSnapshot.body);

        if (includeHead || editedPoint == TrickPoseRigEditablePoint.Head)
        {
            if (headRotationOnly)
                WritePartRotation(ref entry.headPose, solvedSnapshot.head, defaultSnapshot.head);
            else
                WritePartPose(ref entry.headPose, solvedSnapshot.head, defaultSnapshot.head);
        }

        if (editedPoint == TrickPoseRigEditablePoint.Body && includeAffectedChain)
        {
            WriteJointPose(ref entry.leftElbowPose, solvedSnapshot.leftElbow, defaultSnapshot.leftElbow);
            WriteJointPose(ref entry.rightElbowPose, solvedSnapshot.rightElbow, defaultSnapshot.rightElbow);
            WriteJointPose(ref entry.leftKneePose, solvedSnapshot.leftKnee, defaultSnapshot.leftKnee);
            WriteJointPose(ref entry.rightKneePose, solvedSnapshot.rightKnee, defaultSnapshot.rightKnee);
            WritePartPose(ref entry.leftPolePose, solvedSnapshot.leftPole, defaultSnapshot.leftPole);
            WritePartPose(ref entry.rightPolePose, solvedSnapshot.rightPole, defaultSnapshot.rightPole);
            WritePartPose(ref entry.leftSkiPose, solvedSnapshot.leftSki, defaultSnapshot.leftSki);
            WritePartPose(ref entry.rightSkiPose, solvedSnapshot.rightSki, defaultSnapshot.rightSki);
            return;
        }

        switch (editedPoint)
        {
            case TrickPoseRigEditablePoint.LeftSki:
                WritePartPose(ref entry.leftSkiPose, solvedSnapshot.leftSki, defaultSnapshot.leftSki);
                if (includeAffectedChain)
                    WriteJointPose(ref entry.leftKneePose, solvedSnapshot.leftKnee, defaultSnapshot.leftKnee);
                break;
            case TrickPoseRigEditablePoint.RightSki:
                WritePartPose(ref entry.rightSkiPose, solvedSnapshot.rightSki, defaultSnapshot.rightSki);
                if (includeAffectedChain)
                    WriteJointPose(ref entry.rightKneePose, solvedSnapshot.rightKnee, defaultSnapshot.rightKnee);
                break;
            case TrickPoseRigEditablePoint.LeftPole:
                WritePartPose(ref entry.leftPolePose, solvedSnapshot.leftPole, defaultSnapshot.leftPole);
                if (includeAffectedChain)
                    WriteJointPose(ref entry.leftElbowPose, solvedSnapshot.leftElbow, defaultSnapshot.leftElbow);
                break;
            case TrickPoseRigEditablePoint.RightPole:
                WritePartPose(ref entry.rightPolePose, solvedSnapshot.rightPole, defaultSnapshot.rightPole);
                if (includeAffectedChain)
                    WriteJointPose(ref entry.rightElbowPose, solvedSnapshot.rightElbow, defaultSnapshot.rightElbow);
                break;
            case TrickPoseRigEditablePoint.LeftElbow:
                WriteJointPose(ref entry.leftElbowPose, solvedSnapshot.leftElbow, defaultSnapshot.leftElbow);
                if (includeAffectedChain)
                    WritePartPose(ref entry.leftPolePose, solvedSnapshot.leftPole, defaultSnapshot.leftPole);
                break;
            case TrickPoseRigEditablePoint.RightElbow:
                WriteJointPose(ref entry.rightElbowPose, solvedSnapshot.rightElbow, defaultSnapshot.rightElbow);
                if (includeAffectedChain)
                    WritePartPose(ref entry.rightPolePose, solvedSnapshot.rightPole, defaultSnapshot.rightPole);
                break;
            case TrickPoseRigEditablePoint.LeftKnee:
                WriteJointPose(ref entry.leftKneePose, solvedSnapshot.leftKnee, defaultSnapshot.leftKnee);
                if (includeAffectedChain)
                    WritePartPose(ref entry.leftSkiPose, solvedSnapshot.leftSki, defaultSnapshot.leftSki);
                break;
            case TrickPoseRigEditablePoint.RightKnee:
                WriteJointPose(ref entry.rightKneePose, solvedSnapshot.rightKnee, defaultSnapshot.rightKnee);
                if (includeAffectedChain)
                    WritePartPose(ref entry.rightSkiPose, solvedSnapshot.rightSki, defaultSnapshot.rightSki);
                break;
        }
    }

    private static void AssignCapturedLengths(
        TrickPoseRigSnapshot snapshot,
        TrickPoseRigAssistReferenceData referenceData,
        ref TrickPoseRigSegmentLengthSet lengths)
    {
        lengths.captured = false;

        foreach (ChainDefinition chain in EnumerateChains(lengths))
        {
            if (!TryGetChainPositions(snapshot, referenceData, chain.id, out Vector3 root, out Vector3 joint, out Vector3 end))
                continue;

            SetChainLengths(ref lengths, chain.id, Vector3.Distance(root, joint), Vector3.Distance(joint, end));
            lengths.captured = true;
        }
    }

    private static bool TrySolveSceneChain(
        TrickPoseRigSnapshot snapshot,
        TrickPoseRigAssistReferenceData referenceData,
        ChainDefinition chain,
        TrickPoseRigEditablePoint editedPoint,
        out string warning)
    {
        warning = null;
        if (!TryGetChainPositions(snapshot, referenceData, chain.id, out Vector3 root, out Vector3 joint, out Vector3 end))
            return false;

        bool endpointEdit = IsEndpointPoint(editedPoint) || editedPoint == TrickPoseRigEditablePoint.Body;
        bool jointEdit = IsJointPoint(editedPoint);
        if (endpointEdit)
        {
            Vector3 requestedEnd = end;
            Vector3 clampedEnd = ClampEndpointToReachableRange(root, requestedEnd, chain.upperLength, chain.lowerLength, chain.fallbackAxis, out bool clamped);
            if (clamped)
            {
                SetEndpointLocalPosition(snapshot, chain.id, clampedEnd);
                end = clampedEnd;
                warning = $"{chain.name}: endpoint was clamped to captured segment reach.";
            }

            if (!TrySolveJointFromEndpoint(root, joint, end, chain.upperLength, chain.lowerLength, chain.fallbackAxis, out Vector3 solvedJoint))
            {
                warning = $"{chain.name}: endpoint is unreachable with captured segment lengths.";
                return false;
            }

            SetJointLocalPosition(snapshot, chain.id, snapshot.body, solvedJoint);
            return true;
        }

        if (jointEdit)
        {
            Vector3 rootToJoint = joint - root;
            Vector3 upperDirection = SafeDirection(rootToJoint, chain.fallbackAxis);
            Vector3 solvedJoint = root + upperDirection * chain.upperLength;
            Vector3 lowerDirection = SafeDirection(end - joint, solvedJoint - root);
            Vector3 solvedEnd = solvedJoint + lowerDirection * chain.lowerLength;
            SetJointLocalPosition(snapshot, chain.id, snapshot.body, solvedJoint);
            SetEndpointLocalPosition(snapshot, chain.id, solvedEnd);

            if (Vector3.Distance(root, joint) > chain.upperLength + 0.0001f)
                warning = $"{chain.name}: joint was clamped to captured upper segment length.";

            return true;
        }

        return false;
    }

    private static Vector3 ClampEndpointToReachableRange(
        Vector3 root,
        Vector3 requestedEnd,
        float upperLength,
        float lowerLength,
        Vector3 fallbackAxis,
        out bool clamped)
    {
        Vector3 rootToEnd = requestedEnd - root;
        float distance = rootToEnd.magnitude;
        float maxReach = Mathf.Max(0.0001f, upperLength + lowerLength);
        float minReach = Mathf.Max(0f, Mathf.Abs(upperLength - lowerLength));
        float clampedDistance = Mathf.Clamp(distance, minReach, maxReach);
        clamped = Mathf.Abs(clampedDistance - distance) > 0.0001f;
        Vector3 direction = SafeDirection(rootToEnd, fallbackAxis);
        return root + direction * clampedDistance;
    }

    private static bool TryGetEditedChain(TrickPoseRigSegmentLengthSet lengths, TrickPoseRigEditablePoint editedPoint, out ChainDefinition chain)
    {
        ChainId? id = editedPoint switch
        {
            TrickPoseRigEditablePoint.LeftElbow => ChainId.LeftArm,
            TrickPoseRigEditablePoint.LeftPole => ChainId.LeftArm,
            TrickPoseRigEditablePoint.RightElbow => ChainId.RightArm,
            TrickPoseRigEditablePoint.RightPole => ChainId.RightArm,
            TrickPoseRigEditablePoint.LeftKnee => ChainId.LeftLeg,
            TrickPoseRigEditablePoint.LeftSki => ChainId.LeftLeg,
            TrickPoseRigEditablePoint.RightKnee => ChainId.RightLeg,
            TrickPoseRigEditablePoint.RightSki => ChainId.RightLeg,
            _ => null
        };

        if (id.HasValue)
        {
            foreach (ChainDefinition candidate in EnumerateChains(lengths))
            {
                if (candidate.id == id.Value)
                {
                    chain = candidate;
                    return true;
                }
            }
        }

        chain = default;
        return false;
    }

    private static bool IsLimbPoint(TrickPoseRigEditablePoint point)
    {
        return IsEndpointPoint(point) || IsJointPoint(point);
    }

    private static bool IsEndpointPoint(TrickPoseRigEditablePoint point)
    {
        return point == TrickPoseRigEditablePoint.LeftSki ||
               point == TrickPoseRigEditablePoint.RightSki ||
               point == TrickPoseRigEditablePoint.LeftPole ||
               point == TrickPoseRigEditablePoint.RightPole;
    }

    private static bool IsJointPoint(TrickPoseRigEditablePoint point)
    {
        return point == TrickPoseRigEditablePoint.LeftElbow ||
               point == TrickPoseRigEditablePoint.RightElbow ||
               point == TrickPoseRigEditablePoint.LeftKnee ||
               point == TrickPoseRigEditablePoint.RightKnee;
    }

    private static bool PartPositionChanged(TrickPoseRigSnapshot.PartState before, TrickPoseRigSnapshot.PartState after)
    {
        return before.hasValue != after.hasValue ||
               (before.localPosition - after.localPosition).sqrMagnitude > 0.0000001f;
    }

    private static void SolveLimbLengths(
        TrickPoseRigSnapshot snapshot,
        TrickPoseRigAssistReferenceData referenceData,
        TrickPoseRigSegmentLengthSet lengths,
        TrickPoseRigConformMode mode)
    {
        foreach (ChainDefinition chain in EnumerateChains(lengths))
            TrySolveChain(snapshot, referenceData, chain, mode, out _);
    }

    private static bool TrySolveChain(
        TrickPoseRigSnapshot snapshot,
        TrickPoseRigAssistReferenceData referenceData,
        ChainDefinition chain,
        TrickPoseRigConformMode conformMode,
        out string warning)
    {
        warning = null;
        if (!TryGetChainPositions(snapshot, referenceData, chain.id, out Vector3 root, out Vector3 joint, out Vector3 end))
            return false;

        switch (conformMode)
        {
            case TrickPoseRigConformMode.PreserveEndpointAdjustJoint:
                if (!TrySolveJointFromEndpoint(root, joint, end, chain.upperLength, chain.lowerLength, chain.fallbackAxis, out Vector3 solvedJoint))
                {
                    warning = $"{chain.name}: endpoint is unreachable with captured segment lengths.";
                    return false;
                }

                SetJointLocalPosition(snapshot, chain.id, snapshot.body, solvedJoint);
                return true;

            case TrickPoseRigConformMode.PreserveJointAdjustEndpoint:
                if (Vector3.Distance(root, joint) > chain.upperLength + 0.0001f)
                {
                    warning = $"{chain.name}: preserved joint sits beyond the captured upper segment length.";
                    return false;
                }

                Vector3 endDirection = SafeDirection(end - joint, chain.fallbackAxis);
                SetEndpointLocalPosition(snapshot, chain.id, joint + endDirection * chain.lowerLength);
                return true;

            case TrickPoseRigConformMode.PreserveRootScaleChainDirection:
                Vector3 upperDirection = SafeDirection(joint - root, chain.fallbackAxis);
                Vector3 scaledJoint = root + upperDirection * chain.upperLength;
                Vector3 lowerDirection = SafeDirection(end - joint, end - root);
                Vector3 scaledEnd = scaledJoint + lowerDirection * chain.lowerLength;
                SetJointLocalPosition(snapshot, chain.id, snapshot.body, scaledJoint);
                SetEndpointLocalPosition(snapshot, chain.id, scaledEnd);
                return true;
        }

        return false;
    }

    private static bool TryGetChainPositions(
        TrickPoseRigSnapshot snapshot,
        TrickPoseRigAssistReferenceData referenceData,
        ChainId chainId,
        out Vector3 root,
        out Vector3 joint,
        out Vector3 end)
    {
        root = Vector3.zero;
        joint = Vector3.zero;
        end = Vector3.zero;

        if (snapshot == null || !snapshot.body.hasValue)
            return false;

        // All returned positions are in the controller/root-local solve space.
        // Body-authored anchors and joints are converted from body local into that
        // shared space before any lengths are measured or solved.
        switch (chainId)
        {
            case ChainId.LeftArm:
                if (!referenceData.hasLeftShoulder || !snapshot.leftElbow.hasValue || !snapshot.leftPole.hasValue)
                    return false;
                root = ToWorldLocal(snapshot.body, referenceData.leftShoulderBodyLocal);
                joint = ToWorldLocal(snapshot.body, snapshot.leftElbow.localPosition);
                end = snapshot.leftPole.localPosition;
                return true;

            case ChainId.RightArm:
                if (!referenceData.hasRightShoulder || !snapshot.rightElbow.hasValue || !snapshot.rightPole.hasValue)
                    return false;
                root = ToWorldLocal(snapshot.body, referenceData.rightShoulderBodyLocal);
                joint = ToWorldLocal(snapshot.body, snapshot.rightElbow.localPosition);
                end = snapshot.rightPole.localPosition;
                return true;

            case ChainId.LeftLeg:
                if (!referenceData.hasLeftHip || !snapshot.leftKnee.hasValue || !snapshot.leftSki.hasValue)
                    return false;
                root = ToWorldLocal(snapshot.body, referenceData.leftHipBodyLocal);
                joint = ToWorldLocal(snapshot.body, snapshot.leftKnee.localPosition);
                end = snapshot.leftSki.localPosition;
                return true;

            case ChainId.RightLeg:
                if (!referenceData.hasRightHip || !snapshot.rightKnee.hasValue || !snapshot.rightSki.hasValue)
                    return false;
                root = ToWorldLocal(snapshot.body, referenceData.rightHipBodyLocal);
                joint = ToWorldLocal(snapshot.body, snapshot.rightKnee.localPosition);
                end = snapshot.rightSki.localPosition;
                return true;
        }

        return false;
    }

    private static void SetJointLocalPosition(TrickPoseRigSnapshot snapshot, ChainId chainId, TrickPoseRigSnapshot.PartState bodyState, Vector3 jointLocalInController)
    {
        Vector3 bodyLocal = ToBodyLocal(bodyState, jointLocalInController);
        switch (chainId)
        {
            case ChainId.LeftArm:
                snapshot.leftElbow.localPosition = bodyLocal;
                break;
            case ChainId.RightArm:
                snapshot.rightElbow.localPosition = bodyLocal;
                break;
            case ChainId.LeftLeg:
                snapshot.leftKnee.localPosition = bodyLocal;
                break;
            case ChainId.RightLeg:
                snapshot.rightKnee.localPosition = bodyLocal;
                break;
        }
    }

    private static void SetEndpointLocalPosition(TrickPoseRigSnapshot snapshot, ChainId chainId, Vector3 endpointLocalInController)
    {
        switch (chainId)
        {
            case ChainId.LeftArm:
                snapshot.leftPole.localPosition = endpointLocalInController;
                break;
            case ChainId.RightArm:
                snapshot.rightPole.localPosition = endpointLocalInController;
                break;
            case ChainId.LeftLeg:
                snapshot.leftSki.localPosition = endpointLocalInController;
                break;
            case ChainId.RightLeg:
                snapshot.rightSki.localPosition = endpointLocalInController;
                break;
        }
    }

    private static bool TrySolveJointFromEndpoint(
        Vector3 root,
        Vector3 currentJoint,
        Vector3 end,
        float upperLength,
        float lowerLength,
        Vector3 fallbackAxis,
        out Vector3 solvedJoint)
    {
        solvedJoint = currentJoint;
        Vector3 rootToEnd = end - root;
        float distance = rootToEnd.magnitude;
        if (distance <= 0.0001f || !IsReachable(root, end, upperLength, lowerLength))
            return false;

        Vector3 direction = rootToEnd / distance;
        float along = ((distance * distance) + (upperLength * upperLength) - (lowerLength * lowerLength)) / (2f * distance);
        float heightSq = Mathf.Max(0f, (upperLength * upperLength) - (along * along));
        Vector3 onSegment = root + direction * along;

        Vector3 bendAxis = Vector3.ProjectOnPlane(currentJoint - onSegment, direction);
        if (bendAxis.sqrMagnitude < 0.0001f)
            bendAxis = Vector3.ProjectOnPlane(fallbackAxis, direction);
        if (bendAxis.sqrMagnitude < 0.0001f)
            bendAxis = Vector3.up;

        solvedJoint = onSegment + bendAxis.normalized * Mathf.Sqrt(heightSq);
        return true;
    }

    private static bool IsReachable(Vector3 root, Vector3 end, float upperLength, float lowerLength)
    {
        float distance = Vector3.Distance(root, end);
        return distance <= upperLength + lowerLength + 0.0001f &&
               distance >= Mathf.Abs(upperLength - lowerLength) - 0.0001f;
    }

    private static IEnumerable<ChainDefinition> EnumerateChains(TrickPoseRigSegmentLengthSet lengths)
    {
        yield return new ChainDefinition(ChainId.LeftArm, "Left arm", true, true, Vector3.left, lengths.leftUpperArm, lengths.leftLowerArm);
        yield return new ChainDefinition(ChainId.RightArm, "Right arm", true, false, Vector3.right, lengths.rightUpperArm, lengths.rightLowerArm);
        yield return new ChainDefinition(ChainId.LeftLeg, "Left leg", false, true, Vector3.forward, lengths.leftUpperLeg, lengths.leftLowerLeg);
        yield return new ChainDefinition(ChainId.RightLeg, "Right leg", false, false, Vector3.forward, lengths.rightUpperLeg, lengths.rightLowerLeg);
    }

    private static void SetChainLengths(ref TrickPoseRigSegmentLengthSet lengths, ChainId chainId, float upper, float lower)
    {
        switch (chainId)
        {
            case ChainId.LeftArm:
                lengths.leftUpperArm = upper;
                lengths.leftLowerArm = lower;
                break;
            case ChainId.RightArm:
                lengths.rightUpperArm = upper;
                lengths.rightLowerArm = lower;
                break;
            case ChainId.LeftLeg:
                lengths.leftUpperLeg = upper;
                lengths.leftLowerLeg = lower;
                break;
            case ChainId.RightLeg:
                lengths.rightUpperLeg = upper;
                lengths.rightLowerLeg = lower;
                break;
        }
    }

    private static void WriteJointPose(
        ref PosePartTransformData pose,
        TrickPoseRigSnapshot.PartState solvedState,
        TrickPoseRigSnapshot.PartState defaultState)
    {
        if (!solvedState.hasValue || !defaultState.hasValue)
            return;

        if (pose == null)
            pose = new PosePartTransformData();
        pose.enabled = true;
        pose.weight = 1f;
        pose.localPosition = solvedState.localPosition;
        pose.localEulerAngles = Vector3.zero;
    }

    private static void WritePartPose(
        ref PosePartTransformData pose,
        TrickPoseRigSnapshot.PartState solvedState,
        TrickPoseRigSnapshot.PartState defaultState)
    {
        if (!solvedState.hasValue || !defaultState.hasValue)
            return;

        if (pose == null)
            pose = new PosePartTransformData();
        pose.enabled = true;
        pose.weight = 1f;
        pose.localPosition = solvedState.localPosition - defaultState.localPosition;
        pose.localEulerAngles = (Quaternion.Inverse(defaultState.localRotation) * solvedState.localRotation).eulerAngles;
    }

    private static void WritePartRotation(
        ref PosePartTransformData pose,
        TrickPoseRigSnapshot.PartState solvedState,
        TrickPoseRigSnapshot.PartState defaultState)
    {
        if (!solvedState.hasValue || !defaultState.hasValue)
            return;

        if (pose == null)
            pose = new PosePartTransformData();
        pose.enabled = true;
        pose.weight = 1f;
        pose.localEulerAngles = (Quaternion.Inverse(defaultState.localRotation) * solvedState.localRotation).eulerAngles;
    }

    private static Vector3 ToBodyLocal(TrickPoseRigSnapshot.PartState bodyState, Vector3 worldLocalPosition)
    {
        return Quaternion.Inverse(bodyState.localRotation) * (worldLocalPosition - bodyState.localPosition);
    }

    private static Vector3 ToWorldLocal(TrickPoseRigSnapshot.PartState bodyState, Vector3 bodyLocalPosition)
    {
        return bodyState.localPosition + (bodyState.localRotation * bodyLocalPosition);
    }

    private static Vector3 SafeDirection(Vector3 vector, Vector3 fallback)
    {
        if (vector.sqrMagnitude > 0.0001f)
            return vector.normalized;
        if (fallback.sqrMagnitude > 0.0001f)
            return fallback.normalized;
        return Vector3.forward;
    }
}
