using UnityEngine;

public sealed class LiftAccessResultBusDebugToggle : MonoBehaviour
{
    [SerializeField] private bool logEvents;

    private void OnEnable()
    {
        LiftAccessResultBus.LogEvents = logEvents;
    }

    private void OnValidate()
    {
        LiftAccessResultBus.LogEvents = logEvents;
    }

    [ContextMenu("Enable Lift Access Result Logging")]
    private void EnableLogging()
    {
        logEvents = true;
        LiftAccessResultBus.LogEvents = true;
    }

    [ContextMenu("Disable Lift Access Result Logging")]
    private void DisableLogging()
    {
        logEvents = false;
        LiftAccessResultBus.LogEvents = false;
    }
}
