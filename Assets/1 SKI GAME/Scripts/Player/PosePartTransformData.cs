using System;
using UnityEngine;

[Serializable]
public class PosePartTransformData
{
    public bool enabled;
    public Vector3 localPosition;
    public Vector3 localEulerAngles;
    [Range(0f, 1f)] public float weight = 1f;

    public Quaternion LocalRotation => Quaternion.Euler(localEulerAngles);

    public void CaptureOffset(Transform current, Vector3 defaultLocalPosition, Quaternion defaultLocalRotation)
    {
        if (current == null)
            return;

        enabled = true;
        localPosition = current.localPosition - defaultLocalPosition;

        Quaternion deltaRotation = Quaternion.Inverse(defaultLocalRotation) * current.localRotation;
        localEulerAngles = deltaRotation.eulerAngles;
    }

    public Vector3 GetAbsoluteLocalPosition(Vector3 defaultLocalPosition)
    {
        return defaultLocalPosition + localPosition;
    }

    public Quaternion GetAbsoluteLocalRotation(Quaternion defaultLocalRotation)
    {
        return defaultLocalRotation * LocalRotation;
    }

    public void Reset()
    {
        enabled = false;
        localPosition = Vector3.zero;
        localEulerAngles = Vector3.zero;
        weight = 1f;
    }
}
