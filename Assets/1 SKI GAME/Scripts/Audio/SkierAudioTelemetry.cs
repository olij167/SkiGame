using UnityEngine;

[System.Serializable]
public struct SkierAudioTelemetry
{
    public bool groundedState;
    public bool stackedState;
    public Vector3 velocity;
    public Vector3 planarVelocity;
    public Vector3 groundNormal;
    public Vector3 skiForwardOnPlane;
    public float planarSpeed;
    public float verticalSpeed;
    public float normalizedSpeed;
    public float carveAngle;
    public float carve01;
    public float signedCarve;
    public float landingSeverity;
    public bool didLandThisFrame;
    public bool didStackThisFrame;
    public SkiController.PoleStrokePhase polePhase;
    public bool leftPoleContact;
    public bool rightPoleContact;
    public float poleDragAmount;

    public void ReadFrom(SkiController controller, SkierAudioConfigSO config)
    {
        if (controller == null)
            return;

        groundNormal = controller.GroundNormal.sqrMagnitude > 0.0001f
            ? controller.GroundNormal.normalized
            : Vector3.up;

        velocity = controller.Velocity;
        planarVelocity = Vector3.ProjectOnPlane(velocity, groundNormal);
        planarSpeed = GetPlanarSpeed(velocity, groundNormal);
        verticalSpeed = Vector3.Dot(velocity, groundNormal);
        normalizedSpeed = config != null && config.MaxSpeedForAudio > 0f
            ? Mathf.Clamp01(planarSpeed / config.MaxSpeedForAudio)
            : 0f;

        groundedState = controller.IsRiderGrounded;
        stackedState = controller.IsStacked;
        skiForwardOnPlane = controller.SkiForwardOnPlane;
        carveAngle = GetCarveAngle(planarVelocity, skiForwardOnPlane, groundNormal);
        signedCarve = GetSignedCarve(planarVelocity, skiForwardOnPlane, groundNormal);
        float maxCarveAngle = config != null ? Mathf.Max(1f, config.MaxCarveAngleForAudio) : 45f;
        carve01 = Mathf.InverseLerp(0f, maxCarveAngle, Mathf.Abs(carveAngle));

        polePhase = controller.CurrentPolePhase;
        leftPoleContact = controller.LeftPoleContact != null && controller.LeftPoleContact.IsInContact;
        rightPoleContact = controller.RightPoleContact != null && controller.RightPoleContact.IsInContact;
        bool anyPoleContact = leftPoleContact || rightPoleContact;
        poleDragAmount = groundedState &&
                         anyPoleContact &&
                         polePhase == SkiController.PoleStrokePhase.Drag
            ? normalizedSpeed
            : 0f;
    }

    public static float GetPlanarSpeed(Vector3 velocity, Vector3 groundNormal)
    {
        return Vector3.ProjectOnPlane(velocity, groundNormal).magnitude;
    }

    public static float GetCarveAngle(Vector3 planarVelocity, Vector3 skiForwardOnPlane, Vector3 groundNormal)
    {
        if (planarVelocity.sqrMagnitude < 0.01f || skiForwardOnPlane.sqrMagnitude < 0.01f)
            return 0f;

        return Mathf.Abs(Vector3.SignedAngle(
            skiForwardOnPlane.normalized,
            planarVelocity.normalized,
            groundNormal));
    }

    public static float GetSignedCarve(Vector3 planarVelocity, Vector3 skiForwardOnPlane, Vector3 groundNormal)
    {
        if (planarVelocity.sqrMagnitude < 0.01f || skiForwardOnPlane.sqrMagnitude < 0.01f)
            return 0f;

        return Vector3.SignedAngle(
            skiForwardOnPlane.normalized,
            planarVelocity.normalized,
            groundNormal);
    }
}
