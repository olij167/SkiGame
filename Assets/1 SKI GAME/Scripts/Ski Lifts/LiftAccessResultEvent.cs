using UnityEngine;

public enum LiftAccessResultReason
{
    Allowed,
    NoPass,
    PassExpired,
    WrongPassTier,
    WrongPassRegion,
    LiftClosed,
    AlreadyBoarding,
    AlreadyRiding,
    TooFar,
    UnknownDenied
}

public enum LiftAccessResultPhase
{
    AccessChecked,
    QueueJoined,
    CarrierAttached,
    DirectCarrierAttach
}

public struct LiftAccessResultEvent
{
    public GameObject player;
    public GameObject rider;
    public LiftBoardGate gate;
    public LiftCarrier carrier;
    public Component lift;
    public Transform station;
    public bool allowed;
    public LiftAccessResultReason reason;
    public LiftAccessResultPhase phase;

    public string liftId;
    public string liftName;
    public string stationName;
    public string regionName;

    public string requiredPassId;
    public string requiredPassName;
    public int requiredPassTier;

    public string currentPassId;
    public string currentPassName;
    public int currentPassTier;
    public float passExpirySeconds;
    public string passExpiryText;

    public string kioskHint;
    public string upgradeHint;
}
