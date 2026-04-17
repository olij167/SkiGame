using UnityEngine;
public enum RescueTargetKind
{
    InjuredCasualty = 0,
    CompanionPassenger = 1,
    StrandedPassenger = 2
}

[DisallowMultipleComponent]
public sealed class RescueCasualtyTarget : MonoBehaviour
{
    private RescueService _service;
    private int _targetIndex = -1;
    private RescueTargetKind _targetKind;
    private bool _transportLocked;

    public int TargetIndex => _targetIndex;
    public RescueTargetKind TargetKind => _targetKind;
    public bool IsTransportLocked => _transportLocked;
    public RescueService Service => _service;

    public void Initialize(RescueService service, int targetIndex, RescueTargetKind targetKind)
    {
        _service = service;
        _targetIndex = targetIndex;
        _targetKind = targetKind;
    }

    public void SetTransportLocked(bool locked)
    {
        _transportLocked = locked;
    }
}
