using System.Collections.Generic;
using UnityEngine;
using SkiGame.UI;

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
        if (!rider || attachPoint == null || riders.Contains(rider))
            return false;

        // For chairs, you may want a maximum seat count
        if (mode == LiftCarrierMode.Chair && riders.Count >= 2) // e.g. 2 seat chair
            return false;

        // Range check
        float dist = Vector3.Distance(rider.transform.position, attachPoint.position);
        return dist <= attachRadius;
    }

    public bool AttachRider(LiftRider rider, LiftBoardGate sourceGate = null)
    {
        if (!CanAttach(rider))
            return false;

        if (line != null && rider != null && rider.RequiresBoardAuthorization(line))
        {
            if (!rider.HasBoardAuthorizationFor(line))
                return false;
        }

        // --- Ski Pass gate (authoritative) ---
        var passMgr = SkiPassManager.Instance;
        bool raisedAccessResult = false;
        bool playerControlled = rider != null && rider.IsPlayerControlled();
        if (playerControlled && passMgr != null && line != null)
        {
            if (!passMgr.CanUseLift(line))
            {
                string requiredName = line.GetRequiredPassDisplayName();
                LiftAccessPopupBus.RaiseDenied(requiredName);
                LiftAccessResultBus.Raise(LiftAccessResultUtility.Build(
                    rider,
                    sourceGate,
                    this,
                    line,
                    false,
                    LiftAccessResultUtility.DetermineDeniedReason(passMgr, line),
                    LiftAccessResultPhase.AccessChecked));
                return false;
            }

            LiftAccessPopupBus.RaiseAllowed(passMgr.GetCurrentPassDisplayName());
            LiftAccessResultBus.Raise(LiftAccessResultUtility.Build(
                rider,
                sourceGate,
                this,
                line,
                true,
                LiftAccessResultReason.Allowed,
                sourceGate != null ? LiftAccessResultPhase.CarrierAttached : LiftAccessResultPhase.DirectCarrierAttach));
            raisedAccessResult = true;
        }

        riders.Add(rider);

        if (line != null)
            rider.ConsumeBoardAuthorization(line);

        rider.OnAttachedToCarrier(this);
        if (!raisedAccessResult)
        {
            LiftAccessResultBus.Raise(LiftAccessResultUtility.Build(
                rider,
                sourceGate,
                this,
                line,
                true,
                LiftAccessResultReason.Allowed,
                sourceGate != null ? LiftAccessResultPhase.CarrierAttached : LiftAccessResultPhase.DirectCarrierAttach));
        }

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

    private void OnValidate()
    {
        if (attachPoint == null)
        {
            Debug.LogWarning($"[{nameof(LiftCarrier)}] {name} has no attach point assigned.", this);
            return;
        }

        if (!attachPoint.IsChildOf(transform))
            Debug.LogWarning($"[{nameof(LiftCarrier)}] {name} attach point '{attachPoint.name}' is not under this carrier hierarchy.", this);

        var hanger = GetComponentInChildren<LiftCarrierHanger>(true);
        if (hanger != null && hanger.hanger != null && !attachPoint.IsChildOf(hanger.hanger))
            Debug.LogWarning($"[{nameof(LiftCarrier)}] {name} attach point '{attachPoint.name}' is not under hanger '{hanger.hanger.name}'. This is allowed for custom rigs, but chair follow may look wrong.", this);
    }
}
