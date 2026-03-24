using UnityEngine;

public struct SkiInputFrame
{
    public float leftLeg01;
    public float rightLeg01;
    public float lean01;

    public bool polesHeld;

    public bool jumpHeld;
    public bool jumpPressedThisFrame;
    public bool jumpReleasedThisFrame;

    public static SkiInputFrame Neutral => default;
}

public interface ISkiInputSource
{
    bool HasInput();
    SkiInputFrame GetSkiInput();
}