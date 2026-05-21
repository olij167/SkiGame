using System;
using UnityEngine;

public static class LiftAccessResultUtility
{
    public static LiftAccessResultEvent Build(
        LiftRider rider,
        LiftBoardGate gate,
        LiftCarrier carrier,
        LiftLine line,
        bool allowed,
        LiftAccessResultReason reason,
        LiftAccessResultPhase phase = LiftAccessResultPhase.AccessChecked,
        Transform stationOverride = null)
    {
        var passMgr = SkiPassManager.Instance;
        var config = passMgr != null ? passMgr.Config : null;
        bool hasOwnedPass = HasAnyOwnedPass(passMgr);
        string requiredPassId = ResolveRequiredPassId(line, config);
        string currentPassId = passMgr != null && hasOwnedPass ? passMgr.GetCurrentPassId() : string.Empty;
        int currentPassTier = config != null && !string.IsNullOrWhiteSpace(currentPassId)
            ? config.GetLevelIndexByPassId(currentPassId)
            : (passMgr != null && hasOwnedPass ? passMgr.CurrentLevel : -1);

        Transform station = stationOverride != null
            ? stationOverride
            : ResolveStation(gate, carrier, line);

        return new LiftAccessResultEvent
        {
            player = IsPlayerRider(rider) ? rider.gameObject : null,
            rider = rider != null ? rider.gameObject : null,
            gate = gate,
            carrier = carrier,
            lift = line,
            station = station,
            allowed = allowed,
            reason = reason,
            phase = phase,
            liftId = line != null ? line.name : string.Empty,
            liftName = line != null ? line.name : "this lift",
            stationName = station != null ? station.name : "this station",
            regionName = ResolveRegionName(config, requiredPassId),
            requiredPassId = requiredPassId,
            requiredPassName = ResolveRequiredPassName(line, config, requiredPassId),
            requiredPassTier = ResolveRequiredPassTier(line, config, requiredPassId),
            currentPassId = currentPassId,
            currentPassName = passMgr != null && hasOwnedPass ? passMgr.GetCurrentPassDisplayName() : "your pass",
            currentPassTier = currentPassTier,
            passExpirySeconds = hasOwnedPass ? ResolvePassExpirySeconds(passMgr) : 0f,
            passExpiryText = passMgr != null && hasOwnedPass ? passMgr.GetRemainingTimeString() : "soon",
            kioskHint = "Head to the kiosk near the lodge.",
            upgradeHint = "Upgrade your pass at the kiosk."
        };
    }

    public static LiftAccessResultReason DetermineDeniedReason(SkiPassManager passMgr, LiftLine line)
    {
        if (passMgr == null || line == null)
            return LiftAccessResultReason.UnknownDenied;

        if (passMgr.HasTimedPass && passMgr.GetRemainingHours() <= 0.0)
            return LiftAccessResultReason.PassExpired;

        if (!HasAnyOwnedPass(passMgr))
            return LiftAccessResultReason.NoPass;

        var config = passMgr.Config;
        string requiredPassId = config != null ? line.GetResolvedRequiredPassId(config) : string.Empty;
        string currentPassId = passMgr.GetCurrentPassId();
        if (config != null && HasDifferentAuthoredRegion(config, currentPassId, requiredPassId))
            return LiftAccessResultReason.WrongPassRegion;

        return LiftAccessResultReason.WrongPassTier;
    }

    private static Transform ResolveStation(LiftBoardGate gate, LiftCarrier carrier, LiftLine line)
    {
        if (gate != null)
            return ResolveNearestLineStation(line, gate.boardingPoint != null ? gate.boardingPoint.position : gate.transform.position);

        if (carrier != null)
            return ResolveNearestLineStation(line, carrier.attachPoint != null ? carrier.attachPoint.position : carrier.transform.position);

        return line != null ? line.bottomStation : null;
    }

    private static string ResolveRequiredPassId(LiftLine line, SkiPassConfigSO config)
    {
        if (line == null)
            return string.Empty;

        if (config != null)
        {
            string resolved = line.GetResolvedRequiredPassId(config);
            if (!string.IsNullOrWhiteSpace(resolved))
                return resolved.Trim();
        }

        if (!string.IsNullOrWhiteSpace(line.RequiredPassId))
            return line.RequiredPassId.Trim();

        return config != null
            ? config.GetPassIdForLevel(Mathf.Max(0, line.RequiredPassLevel))
            : string.Empty;
    }

    private static string ResolveRequiredPassName(LiftLine line, SkiPassConfigSO config, string requiredPassId)
    {
        if (config != null && !string.IsNullOrWhiteSpace(requiredPassId))
        {
            var pass = config.GetByPassId(requiredPassId);
            if (pass != null && !string.IsNullOrWhiteSpace(pass.displayName))
                return pass.displayName.Trim();
        }

        if (line != null)
        {
            string lineDisplay = line.GetRequiredPassDisplayName();
            if (!string.IsNullOrWhiteSpace(lineDisplay) &&
                !string.Equals(lineDisplay, "Ski pass required", StringComparison.OrdinalIgnoreCase))
            {
                return lineDisplay.Trim();
            }
        }

        if (!string.IsNullOrWhiteSpace(requiredPassId))
            return requiredPassId.Trim();

        return "the right pass";
    }

    private static Transform ResolveNearestLineStation(LiftLine line, Vector3 reference)
    {
        if (line == null)
            return null;

        Transform bottom = line.bottomStation;
        Transform top = line.topStation;
        if (bottom == null)
            return top;
        if (top == null)
            return bottom;

        float bottomSq = (reference - bottom.position).sqrMagnitude;
        float topSq = (reference - top.position).sqrMagnitude;
        return bottomSq <= topSq ? bottom : top;
    }

    private static int ResolveRequiredPassTier(LiftLine line, SkiPassConfigSO config, string requiredPassId)
    {
        if (config != null && !string.IsNullOrWhiteSpace(requiredPassId))
        {
            int level = config.GetLevelIndexByPassId(requiredPassId);
            if (level >= 0)
                return level;
        }

        return line != null ? line.RequiredPassLevel : -1;
    }

    private static float ResolvePassExpirySeconds(SkiPassManager passMgr)
    {
        if (passMgr == null)
            return 0f;

        double remainingHours = passMgr.GetRemainingHours();
        if (double.IsInfinity(remainingHours))
            return float.PositiveInfinity;

        return Mathf.Max(0f, (float)(remainingHours * 3600.0));
    }

    private static string ResolveRegionName(SkiPassConfigSO config, string requiredPassId)
    {
        var pass = config != null ? config.GetByPassId(requiredPassId) : null;
        return pass != null && !string.IsNullOrWhiteSpace(pass.regionId)
            ? pass.regionId.Trim()
            : "this area";
    }

    private static bool HasDifferentAuthoredRegion(SkiPassConfigSO config, string currentPassId, string requiredPassId)
    {
        var current = config != null ? config.GetByPassId(currentPassId) : null;
        var required = config != null ? config.GetByPassId(requiredPassId) : null;
        if (current == null || required == null)
            return false;

        if (string.IsNullOrWhiteSpace(current.regionId) || string.IsNullOrWhiteSpace(required.regionId))
            return false;

        return !string.Equals(current.regionId.Trim(), required.regionId.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPlayerRider(LiftRider rider)
    {
        return rider != null && rider.IsPlayerControlled();
    }

    private static bool HasAnyOwnedPass(SkiPassManager passMgr)
    {
        return passMgr != null &&
               (passMgr.HasClaimedDefaultPass || passMgr.HasTimedPass || passMgr.GetHighestPermanentUnlockedLevel() >= 0);
    }
}
