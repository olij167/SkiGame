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

    [Tooltip("If true, riders leave the queue automatically if they walk out of the trigger.")]
    public bool removeRiderWhenLeavingTrigger = true;

    private readonly List<LiftRider> _queue = new List<LiftRider>();
    private readonly HashSet<LiftRider> _insideTrigger = new HashSet<LiftRider>();

    private Collider _trigger;

    public int QueueCount => _queue.Count;

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
            LeaveQueue(rider);

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

    public bool TryJoinQueue(LiftRider rider)
    {
        if (rider == null)
            return false;

        if (IsQueued(rider))
            return true;

        bool isNpc = rider.CompareTag("NPC") || rider.transform.root.CompareTag("NPC");
        if (isNpc && !allowNpcQueueing)
            return false;

        if (!_insideTrigger.Contains(rider))
            return false;

        if (!CanRiderUseGate(rider, showFeedback: true))
            return false;

        _queue.Add(rider);
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

        bool isPlayer = !rider.CompareTag("NPC") && !rider.transform.root.CompareTag("NPC");
        return isPlayer && _insideTrigger.Contains(rider);
    }

    private bool CanRiderUseGate(LiftRider rider, bool showFeedback)
    {
        if (rider == null)
            return false;

        bool isPlayer = !rider.CompareTag("NPC") && !rider.transform.root.CompareTag("NPC");
        if (!isPlayer)
            return true;

        SkiPassManager passMgr = SkiPassManager.Instance;

        if (passMgr != null && line != null && !passMgr.CanUseLift(line))
        {
            if (showFeedback)
            {
                string requiredName = line.GetRequiredPassDisplayName();
                LiftAccessPopupBus.RaiseDenied(requiredName);
            }
            return false;
        }

        return true;
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

        bool attached = carrier.AttachRider(head);
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
        }

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
