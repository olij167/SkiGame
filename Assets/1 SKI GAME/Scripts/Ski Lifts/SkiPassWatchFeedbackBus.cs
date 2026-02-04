using System;
using UnityEngine;

public static class SkiPassWatchFeedbackBus
{
    public struct Feedback
    {
        public bool allowed;          // true = green tick, false = red cross
        public string message;        // main message
        public float seconds;         // how long to display
    }

    public static event Action<Feedback> OnFeedback;

    public static void RaiseAllowed(string passName, float seconds = 1.25f)
    {
        OnFeedback?.Invoke(new Feedback
        {
            allowed = true,
            message = passName,
            seconds = seconds
        });
    }

    public static void RaiseDenied(string requiredPassName, float seconds = 1.5f)
    {
        OnFeedback?.Invoke(new Feedback
        {
            allowed = false,
            message = $"{requiredPassName} required to use this lift",
            seconds = seconds
        });
    }
}
