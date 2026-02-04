using System.Collections.Generic;
using UnityEngine;

public class LiftCarrier : MonoBehaviour
{
    [Header("Setup")]
    public LiftCarrierMode mode = LiftCarrierMode.Chair;
    public Transform attachPoint;     // seat or T-bar handle
    public float attachRadius = 2f;   // how close rider must be to attach

    [HideInInspector] public LiftLine line;
    [HideInInspector] public float distanceAlong;

    private List<LiftRider> riders = new List<LiftRider>();

    public bool CanAttach(LiftRider rider)
    {
        if (!rider || riders.Contains(rider))
            return false;

        // For chairs, you may want a maximum seat count
        if (mode == LiftCarrierMode.Chair && riders.Count >= 2) // e.g. 2 seat chair
            return false;

        // Range check
        float dist = Vector3.Distance(rider.transform.position, attachPoint.position);
        return dist <= attachRadius;
    }

    public bool AttachRider(LiftRider rider)
    {
        if (!CanAttach(rider))
            return false;

        // --- Ski Pass gate (authoritative) ---
        var passMgr = SkiPassManager.Instance;
        if (passMgr != null)
        {
            int req = line != null ? line.RequiredPassLevel : 0;

            if (!passMgr.CanUseLift(req))
            {
                // Watch flash (deny)
                string requiredName = GetPassDisplayNameForLevel(req);
                SkiPassWatchFeedbackBus.RaiseDenied(requiredName);
                return false;
            }

            // Watch flash (allow)
            SkiPassWatchFeedbackBus.RaiseAllowed(passMgr.GetCurrentPassDisplayName());
        }

        riders.Add(rider);
        rider.OnAttachedToCarrier(this);
        return true;
    }

    private string GetPassDisplayNameForLevel(int level)
    {
        var mgr = SkiPassManager.Instance;
        var cfg = mgr != null ? mgr.Config : null;

        if (cfg != null)
        {
            var p = cfg.Get(level);
            if (p != null) return $"{p.displayName} (L{level})";
        }

        return $"Pass Level {level}";
    }

    public void DetachRider(LiftRider rider)
    {
        if (!rider) return;
        if (!riders.Contains(rider)) return;

        riders.Remove(rider);
        rider.OnDetachedFromCarrier(this);
    }

    private void OnDrawGizmosSelected()
    {
        if (attachPoint)
        {
            Gizmos.color = mode == LiftCarrierMode.Chair ? Color.cyan : Color.yellow;
            Gizmos.DrawWireSphere(attachPoint.position, attachRadius);
        }
    }
}
