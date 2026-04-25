using System;
using UnityEngine;

[Serializable]
public class TrickPoseRigSnapshot
{
    [Serializable]
    public struct PartState
    {
        public bool hasValue;
        public Vector3 localPosition;
        public Quaternion localRotation;

        public static PartState FromTransform(Transform source)
        {
            if (source == null)
            {
                return new PartState
                {
                    hasValue = false,
                    localPosition = Vector3.zero,
                    localRotation = Quaternion.identity
                };
            }

            return new PartState
            {
                hasValue = true,
                localPosition = source.localPosition,
                localRotation = source.localRotation
            };
        }

        public static PartState Lerp(PartState from, PartState to, float t)
        {
            if (!from.hasValue)
                return to;

            if (!to.hasValue)
                return from;

            return new PartState
            {
                hasValue = true,
                localPosition = Vector3.Lerp(from.localPosition, to.localPosition, t),
                localRotation = Quaternion.Slerp(from.localRotation, to.localRotation, t)
            };
        }
    }

    public PartState body;
    public PartState head;
    public PartState leftSki;
    public PartState rightSki;
    public PartState leftPole;
    public PartState rightPole;
    public PartState leftElbow;
    public PartState rightElbow;
    public PartState leftKnee;
    public PartState rightKnee;

    public TrickPoseRigSnapshot Clone()
    {
        return new TrickPoseRigSnapshot
        {
            body = body,
            head = head,
            leftSki = leftSki,
            rightSki = rightSki,
            leftPole = leftPole,
            rightPole = rightPole,
            leftElbow = leftElbow,
            rightElbow = rightElbow,
            leftKnee = leftKnee,
            rightKnee = rightKnee
        };
    }

    public static TrickPoseRigSnapshot Lerp(TrickPoseRigSnapshot from, TrickPoseRigSnapshot to, float t)
    {
        if (from == null)
            return to?.Clone();

        if (to == null)
            return from.Clone();

        return new TrickPoseRigSnapshot
        {
            body = PartState.Lerp(from.body, to.body, t),
            head = PartState.Lerp(from.head, to.head, t),
            leftSki = PartState.Lerp(from.leftSki, to.leftSki, t),
            rightSki = PartState.Lerp(from.rightSki, to.rightSki, t),
            leftPole = PartState.Lerp(from.leftPole, to.leftPole, t),
            rightPole = PartState.Lerp(from.rightPole, to.rightPole, t),
            leftElbow = PartState.Lerp(from.leftElbow, to.leftElbow, t),
            rightElbow = PartState.Lerp(from.rightElbow, to.rightElbow, t),
            leftKnee = PartState.Lerp(from.leftKnee, to.leftKnee, t),
            rightKnee = PartState.Lerp(from.rightKnee, to.rightKnee, t)
        };
    }
}
