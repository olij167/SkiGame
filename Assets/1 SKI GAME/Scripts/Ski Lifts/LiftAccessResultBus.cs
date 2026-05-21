using System;
using UnityEngine;

public static class LiftAccessResultBus
{
    public static bool LogEvents;
    public static event Action<LiftAccessResultEvent> AccessResult;

    public static void Raise(in LiftAccessResultEvent evt)
    {
        if (LogEvents)
        {
            string liftName = string.IsNullOrWhiteSpace(evt.liftName) ? "lift" : evt.liftName;
            Debug.Log($"[{nameof(LiftAccessResultBus)}] {liftName}: {evt.reason} phase={evt.phase} allowed={evt.allowed} gate={(evt.gate != null ? evt.gate.name + "#" + evt.gate.GetInstanceID() : "none")}");
        }

        AccessResult?.Invoke(evt);
    }
}
