using System.Collections.Generic;
using UnityEngine;
using SkiGame.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class LiftBoardGate : MonoBehaviour
{
    [Header("Lift")]
    public LiftLine line;

    [Header("Boarding")]
    [Tooltip("Where the first rider should stand to board.")]
    public Transform boardingPoint;

    [Tooltip("Optional queue slot transforms behind the boarding point.")]
    public Transform[] queueSlots;

    [Tooltip("How close a carrier attach point must be to the boarding point before the gate attempts boarding.")]
    public float carrierCatchRadius = 2.5f;

    [Header("Rules")]
    [Tooltip("If true, the player must join the queue through this gate instead of free-boarding nearby carriers.")]
    public bool requireGateForPlayers = true;

    [Tooltip("NPCs are allowed through this gate. Their movement into queue slots must still be handled by their locomotion scripts.")]
    public bool allowNpcQueueing = true;

    [Tooltip("Optional NPC entry target. Use this when queue slots or boarding point sit near the edge of the trigger.")]
    [SerializeField] private Transform npcEntryPoint;

    [Tooltip("NPCs may join by proximity to the queue/boarding area even if their trigger callback is unreliable.")]
    [SerializeField, Min(0.25f)] private float npcQueueJoinRadius = 2.5f;

    [Tooltip("Queued NPCs that briefly leave the trigger are kept if they remain near the queue/boarding area.")]
    [SerializeField, Min(0f)] private float npcTriggerExitQueueGraceRadius = 4f;

    [Tooltip("If true, riders leave the queue automatically if they walk out of the trigger.")]
    public bool removeRiderWhenLeavingTrigger = true;

    private readonly List<LiftRider> _queue = new List<LiftRider>();
    private readonly HashSet<LiftRider> _insideTrigger = new HashSet<LiftRider>();

    private Collider _trigger;

    public int QueueCount => _queue.Count;
    public Transform NpcEntryPoint => npcEntryPoint;
    public float NpcQueueJoinRadius => npcQueueJoinRadius;

    private void Awake()
    {
        _trigger = GetComponent<Collider>();
        _trigger.isTrigger = true;
    }

    private void FixedUpdate()
    {
        CleanupQueue();
        TryBoardHeadRider();
    }

    private void OnTriggerEnter(Collider other)
    {
        LiftRider rider = other.GetComponentInParent<LiftRider>();
        if (rider == null)
            return;

        _insideTrigger.Add(rider);
        rider.SetNearbyBoardGate(this);
    }

    private void OnTriggerExit(Collider other)
    {
        LiftRider rider = other.GetComponentInParent<LiftRider>();
        if (rider == null)
            return;

        _insideTrigger.Remove(rider);

        if (removeRiderWhenLeavingTrigger)
        {
            if (rider.IsNpcRider() && IsQueued(rider) && IsNpcCloseEnoughToRemainQueued(rider))
            {
                if (rider.NearbyBoardGate == this)
                    rider.SetNearbyBoardGate(this);
                return;
            }

            LeaveQueue(rider);
        }

        if (rider.NearbyBoardGate == this)
            rider.SetNearbyBoardGate(null);
    }

    public bool IsQueued(LiftRider rider)
    {
        return rider != null && _queue.Contains(rider);
    }

    public bool IsHeadOfQueue(LiftRider rider)
    {
        return rider != null && _queue.Count > 0 && _queue[0] == rider;
    }

    public int GetQueueIndex(LiftRider rider)
    {
        return rider == null ? -1 : _queue.IndexOf(rider);
    }

    public Vector3 GetQueueSlotPosition(int queueIndex)
    {
        if (queueIndex <= 0 && boardingPoint != null)
            return boardingPoint.position;

        int slotIndex = queueIndex - 1;
        if (queueSlots != null && slotIndex >= 0 && slotIndex < queueSlots.Length && queueSlots[slotIndex] != null)
            return queueSlots[slotIndex].position;

        if (boardingPoint != null)
            return boardingPoint.position - transform.forward * (1.5f * queueIndex);

        return transform.position;
    }

    public Vector3 GetQueueTargetPosition(LiftRider rider)
    {
        if (rider == null)
            return GetQueueSlotPosition(_queue.Count);

        int queueIndex = GetQueueIndex(rider);
        if (queueIndex >= 0)
            return GetQueueSlotPosition(queueIndex);

        return GetQueueSlotPosition(_queue.Count);
    }

    public Vector3 GetNpcEntryPosition()
    {
        if (npcEntryPoint != null)
            return npcEntryPoint.position;

        if (boardingPoint != null)
            return boardingPoint.position;

        if (_trigger != null)
            return _trigger.bounds.center;

        return transform.position;
    }

    public bool IsRiderInsideTrigger(LiftRider rider)
    {
        return rider != null && _insideTrigger.Contains(rider);
    }

    public bool TryJoinQueue(LiftRider rider)
    {
        if (rider == null)
            return false;

        if (IsQueued(rider))
            return true;

        bool isNpc = rider.IsNpcRider();
        if (isNpc && !allowNpcQueueing)
            return false;

        bool insideTrigger = _insideTrigger.Contains(rider);
        bool npcCloseEnough = isNpc && IsNpcCloseEnoughToJoin(rider);
        if (!insideTrigger && !npcCloseEnough)
            return false;

        if (!CanRiderUseGate(rider, showFeedback: true))
            return false;

        _queue.Add(rider);
        RaiseAccessResult(rider, true, LiftAccessResultReason.Allowed, null, LiftAccessResultPhase.QueueJoined);
        rider.OnJoinedLiftQueue(this);
        return true;
    }

    public void LeaveQueue(LiftRider rider)
    {
        if (rider == null)
            return;

        if (_queue.Remove(rider))
            rider.OnLeftLiftQueue(this);
    }

    public bool ShouldInterceptPlayerAttach(LiftRider rider)
    {
        if (!requireGateForPlayers || rider == null)
            return false;

        bool isPlayer = rider.IsPlayerControlled();
        return isPlayer && _insideTrigger.Contains(rider);
    }

    private bool CanRiderUseGate(LiftRider rider, bool showFeedback)
    {
        if (rider == null)
            return false;

        bool isPlayer = rider.IsPlayerControlled();
        if (!isPlayer)
            return true;

        SkiPassManager passMgr = SkiPassManager.Instance;

        if (passMgr != null && line != null && !passMgr.CanUseLift(line))
        {
            if (showFeedback)
            {
                string requiredName = line.GetRequiredPassDisplayName();
                LiftAccessPopupBus.RaiseDenied(requiredName);
                RaiseAccessResult(rider, false, LiftAccessResultUtility.DetermineDeniedReason(passMgr, line), null, LiftAccessResultPhase.AccessChecked);
            }
            return false;
        }

        return true;
    }

    private void RaiseAccessResult(LiftRider rider, bool allowed, LiftAccessResultReason reason, LiftCarrier carrier, LiftAccessResultPhase phase)
    {
        var evt = LiftAccessResultUtility.Build(rider, this, carrier, line, allowed, reason, phase);
        LiftAccessResultBus.Raise(evt);
    }

    public bool HasCarrierReadyFor(LiftRider rider)
    {
        return FindBestBoardingCarrier(rider) != null;
    }

    private void TryBoardHeadRider()
    {
        if (_queue.Count == 0 || line == null)
            return;

        LiftRider head = _queue[0];
        if (head == null)
        {
            _queue.RemoveAt(0);
            return;
        }

        if (!CanRiderUseGate(head, showFeedback: false))
            return;

        LiftCarrier carrier = FindBestBoardingCarrier(head);
        if (carrier == null)
            return;

        head.GrantBoardAuthorization(this);

        bool attached = carrier.AttachRider(head, this);
        if (attached)
        {
            _queue.RemoveAt(0);
            head.OnLeftLiftQueue(this);
        }
    }

    private LiftCarrier FindBestBoardingCarrier(LiftRider rider)
    {
        if (line == null || line.carriers == null || boardingPoint == null)
            return null;

        LiftCarrier best = null;
        float bestSq = float.PositiveInfinity;
        Vector3 boardPos = boardingPoint.position;

        for (int i = 0; i < line.carriers.Count; i++)
        {
            LiftCarrier carrier = line.carriers[i];
            if (carrier == null || carrier.attachPoint == null)
                continue;

            if (!carrier.CanAttach(rider))
                continue;

            float sq = (carrier.attachPoint.position - boardPos).sqrMagnitude;
            if (sq > carrierCatchRadius * carrierCatchRadius)
                continue;

            if (sq < bestSq)
            {
                bestSq = sq;
                best = carrier;
            }
        }

        return best;
    }

    private bool IsNpcCloseEnoughToJoin(LiftRider rider)
    {
        if (rider == null || !rider.IsNpcRider() || !allowNpcQueueing)
            return false;

        return DistanceToBoardingOrQueueTarget(rider) <= npcQueueJoinRadius;
    }

    private bool IsNpcCloseEnoughToRemainQueued(LiftRider rider)
    {
        if (rider == null || !rider.IsNpcRider())
            return false;

        return DistanceToBoardingOrQueueTarget(rider) <= Mathf.Max(npcQueueJoinRadius, npcTriggerExitQueueGraceRadius);
    }

    public float DistanceToBoardingOrQueueTarget(LiftRider rider)
    {
        if (rider == null)
            return float.PositiveInfinity;

        Vector3 riderPos = rider.transform.position;
        float best = Vector3.Distance(riderPos, GetQueueTargetPosition(rider));

        if (boardingPoint != null)
            best = Mathf.Min(best, Vector3.Distance(riderPos, boardingPoint.position));

        if (npcEntryPoint != null)
            best = Mathf.Min(best, Vector3.Distance(riderPos, npcEntryPoint.position));

        return best;
    }

    [ContextMenu("Print Queue Debug State")]
    public void PrintQueueDebugState()
    {
        Debug.Log(
            $"[LiftBoardGate] {name}\n" +
            $"line={(line != null ? line.name : "none")} queue={_queue.Count} insideTrigger={_insideTrigger.Count} requireGateForPlayers={requireGateForPlayers} allowNpcQueueing={allowNpcQueueing}\n" +
            $"boardingPoint={(boardingPoint != null ? boardingPoint.position.ToString("F2") : "none")} npcEntry={GetNpcEntryPosition().ToString("F2")} npcJoinRadius={npcQueueJoinRadius:0.00} catchRadius={carrierCatchRadius:0.00}",
            this);

        for (int i = 0; i < _queue.Count; i++)
        {
            LiftRider rider = _queue[i];
            Debug.Log(
                $"[LiftBoardGate] queue[{i}]={(rider != null ? rider.name : "null")} npc={(rider != null && rider.IsNpcRider())} inside={(rider != null && IsRiderInsideTrigger(rider))} dist={(rider != null ? DistanceToBoardingOrQueueTarget(rider).ToString("0.00") : "n/a")} carrierReady={(rider != null && HasCarrierReadyFor(rider))}",
                rider != null ? rider : this);
        }
    }

    [ContextMenu("Print Nearby Riders In Trigger")]
    public void PrintNearbyRidersInTrigger()
    {
        Debug.Log($"[LiftBoardGate] {name}: ridersInTrigger={_insideTrigger.Count}", this);
        foreach (LiftRider rider in _insideTrigger)
        {
            if (rider == null)
                continue;

            Debug.Log($"[LiftBoardGate] trigger rider={rider.name} npc={rider.IsNpcRider()} queued={IsQueued(rider)} dist={DistanceToBoardingOrQueueTarget(rider):0.00}", rider);
        }
    }

    [ContextMenu("Force Board Head Rider If Carrier Available")]
    public void ForceBoardHeadRiderIfCarrierAvailable()
    {
        TryBoardHeadRider();
    }

    private void CleanupQueue()
    {
        for (int i = _queue.Count - 1; i >= 0; i--)
        {
            LiftRider rider = _queue[i];
            if (rider == null || rider.CurrentCarrier != null)
            {
                if (rider != null)
                    rider.OnLeftLiftQueue(this);

                _queue.RemoveAt(i);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (boardingPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(boardingPoint.position, carrierCatchRadius);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(boardingPoint.position, npcQueueJoinRadius);
        }

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(GetNpcEntryPosition(), 0.35f);

        if (queueSlots != null)
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < queueSlots.Length; i++)
            {
                if (queueSlots[i] == null) continue;
                Gizmos.DrawWireSphere(queueSlots[i].position, 0.3f);
            }
        }
    }
}
