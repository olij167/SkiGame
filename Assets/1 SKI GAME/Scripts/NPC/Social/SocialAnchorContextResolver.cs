using System;
using System.Collections.Generic;
using SkiGame.Map;
using SkiGame.POI;
using SkiGame.Runs;
using UnityEngine;

public static class SocialAnchorContextResolver
{
    public static SocialAnchorResolvedContext Resolve(NpcSocialAnchor anchor)
    {
        var result = new SocialAnchorResolvedContext();
        if (anchor == null)
            return result;

        Vector3 position = anchor.Center != null ? anchor.Center.position : anchor.transform.position;
        float radius = Mathf.Max(1f, anchor.ContextSearchRadius);

        ApplyExplicitOverrides(anchor, result);
        ResolveAttachedContext(anchor, result);
        ResolveNearestPoi(position, radius, result);
        ResolveNearestRun(position, radius, result);
        ResolveNearestLift(position, radius, result);
        ResolveNearestRace(position, radius, result);
        ResolveNearestKiosk(position, radius, result);

        // Region should be resolved late so attached POI/run/lift/race data can provide hints,
        // but before fallbacks turn it into "this area".
        ResolveRegion(position, result);

        ApplyFallbacks(anchor, result);
        return result;
    }

    private static void ApplyExplicitOverrides(NpcSocialAnchor anchor, SocialAnchorResolvedContext result)
    {
        result.regionId = FirstNonEmpty(anchor.RegionIdOverride, result.regionId);
        result.regionName = FirstNonEmpty(anchor.RegionNameOverride, result.regionName);
        result.nearbyRunId = FirstNonEmpty(anchor.NearbyRunIdOverride, result.nearbyRunId);
        result.nearbyRunName = FirstNonEmpty(anchor.NearbyRunNameOverride, result.nearbyRunName);
        result.nearbyRunDifficulty = FirstNonEmpty(anchor.NearbyRunDifficultyOverride, result.nearbyRunDifficulty);
        result.nearbyLiftId = FirstNonEmpty(anchor.NearbyLiftIdOverride, result.nearbyLiftId);
        result.nearbyLiftName = FirstNonEmpty(anchor.NearbyLiftNameOverride, result.nearbyLiftName);
        result.nearbyPoiId = FirstNonEmpty(anchor.NearbyPoiIdOverride, result.nearbyPoiId);
        result.nearbyPoiName = FirstNonEmpty(anchor.NearbyPoiNameOverride, result.nearbyPoiName);
        result.raceId = FirstNonEmpty(anchor.RaceIdOverride, result.raceId);
        result.raceName = FirstNonEmpty(anchor.RaceNameOverride, result.raceName);
        result.kioskName = FirstNonEmpty(anchor.KioskNameOverride, result.kioskName);
    }

    private static void ResolveAttachedContext(NpcSocialAnchor anchor, SocialAnchorResolvedContext result)
    {
        if (anchor.TryGetComponent(out SkiRunLine run) || (run = anchor.GetComponentInParent<SkiRunLine>()) != null)
            ApplyRun(run, result);

        if (anchor.TryGetComponent(out LiftLine lift) || (lift = anchor.GetComponentInParent<LiftLine>()) != null)
            ApplyLift(lift, result);

        if (anchor.TryGetComponent(out RaceCourseLine race) || (race = anchor.GetComponentInParent<RaceCourseLine>()) != null)
            ApplyRace(race, result);

        if (anchor.TryGetComponent(out MedicTentActivityHub medic) || (medic = anchor.GetComponentInParent<MedicTentActivityHub>()) != null)
        {
            if (!string.IsNullOrWhiteSpace(medic.AssignedRegionId))
                result.regionId = FirstNonEmpty(result.regionId, medic.AssignedRegionId);
        }
    }

    private static void ResolveNearestPoi(Vector3 position, float radius, SocialAnchorResolvedContext result)
    {
        PointOfInterestRegistry registry = PointOfInterestRegistry.Instance;
        if (registry == null)
            return;

        registry.Refresh();
        if (!registry.TryFindNearest(position, radius, out POIInfo poi) || !poi.IsValid)
            return;

        result.nearbyPoiId = FirstNonEmpty(result.nearbyPoiId, poi.id);
        result.nearbyPoiName = FirstNonEmpty(result.nearbyPoiName, poi.displayName);
        result.nearbyPoiCategory = FirstNonEmpty(result.nearbyPoiCategory, poi.category.ToString(), poi.type.ToString());

        if (poi.source is SkiRunLine run)
            ApplyRun(run, result);
        else if (poi.source is LiftLine lift)
            ApplyLift(lift, result);
        else if (poi.source is RaceCourseLine race)
            ApplyRace(race, result);
        else if (poi.source is SkiPassKiosk kiosk)
            result.kioskName = FirstNonEmpty(result.kioskName, poi.displayName, kiosk.name);
        else if (poi.category == POICategory.Kiosk)
            result.kioskName = FirstNonEmpty(result.kioskName, poi.displayName);
    }

    private sealed class RunContextCandidate
    {
        public SkiRunLine run;
        public string id;
        public string name;
        public string difficulty;
        public float sqrDistance;
    }

    private static void ResolveNearestRun(Vector3 position, float radius, SocialAnchorResolvedContext result)
    {
#if UNITY_2023_1_OR_NEWER
        var runs = UnityEngine.Object.FindObjectsByType<SkiRunLine>(FindObjectsSortMode.None);
#else
    var runs = UnityEngine.Object.FindObjectsOfType<SkiRunLine>();
#endif

        List<RunContextCandidate> candidates = new();
        float radiusSqr = radius * radius;

        for (int i = 0; i < runs.Length; i++)
        {
            SkiRunLine run = runs[i];
            if (run == null)
                continue;

            float sqr = DistanceSqrToRun(run, position);
            if (sqr > radiusSqr)
                continue;

            candidates.Add(new RunContextCandidate
            {
                run = run,
                id = run.RunId,
                name = run.RunName,
                difficulty = run.Difficulty.ToString(),
                sqrDistance = sqr
            });
        }

        if (candidates.Count == 0)
            return;

        candidates.Sort((a, b) => a.sqrDistance.CompareTo(b.sqrDistance));

        // Randomize among nearby candidates so repeated conversations at the same area
        // do not always mention the same run.
        int randomWindow = Mathf.Min(candidates.Count, 6);
        for (int i = 0; i < randomWindow; i++)
        {
            int swap = UnityEngine.Random.Range(i, randomWindow);
            (candidates[i], candidates[swap]) = (candidates[swap], candidates[i]);
        }

        RunContextCandidate primary = candidates[0];
        result.nearbyRunId = FirstNonEmpty(result.nearbyRunId, primary.id);
        result.nearbyRunName = FirstNonEmpty(result.nearbyRunName, primary.name);
        result.nearbyRunDifficulty = FirstNonEmpty(result.nearbyRunDifficulty, primary.difficulty);

        AddRunTokenValues(result, candidates);
    }

    private static float DistanceSqrToRun(SkiRunLine run, Vector3 position)
    {
        if (run == null)
            return float.PositiveInfinity;

        var points = run.PointsWorld;
        if (points == null || points.Count == 0)
            return (run.transform.position - position).sqrMagnitude;

        if (points.Count == 1)
            return (points[0] - position).sqrMagnitude;

        float best = float.PositiveInfinity;
        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector3 a = points[i];
            Vector3 b = points[i + 1];
            Vector3 closest = ClosestPointOnSegment(a, b, position);
            float sqr = (closest - position).sqrMagnitude;
            if (sqr < best)
                best = sqr;
        }

        return best;
    }

    private static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 point)
    {
        Vector3 ab = b - a;
        float denom = Vector3.Dot(ab, ab);
        if (denom <= 0.0001f)
            return a;

        float t = Vector3.Dot(point - a, ab) / denom;
        return a + ab * Mathf.Clamp01(t);
    }

    private static void AddRunTokenValues(SocialAnchorResolvedContext result, List<RunContextCandidate> candidates)
    {
        if (result == null || candidates == null || candidates.Count == 0)
            return;

        List<DialogueContextValue> values = new();

        int count = Mathf.Min(candidates.Count, 6);
        for (int i = 0; i < count; i++)
        {
            int tokenIndex = i + 1;
            RunContextCandidate c = candidates[i];
            if (c == null)
                continue;

            values.Add(new DialogueContextValue { key = $"nearbyRun.{tokenIndex}", value = c.name });
            values.Add(new DialogueContextValue { key = $"nearbyRunName.{tokenIndex}", value = c.name });
            values.Add(new DialogueContextValue { key = $"nearbyRunId.{tokenIndex}", value = c.id });
            values.Add(new DialogueContextValue { key = $"nearbyRunDifficulty.{tokenIndex}", value = c.difficulty });
            values.Add(new DialogueContextValue { key = $"runDifficulty.{tokenIndex}", value = c.difficulty });
        }

        result.tokenValues = MergeTokenValues(result.tokenValues, values);
    }

    private static DialogueContextValue[] MergeTokenValues(DialogueContextValue[] existing, List<DialogueContextValue> added)
    {
        if ((existing == null || existing.Length == 0) && (added == null || added.Count == 0))
            return null;

        List<DialogueContextValue> merged = new();
        if (existing != null)
            merged.AddRange(existing);
        if (added != null)
            merged.AddRange(added);

        return merged.ToArray();
    }

    private static void ResolveNearestLift(Vector3 position, float radius, SocialAnchorResolvedContext result)
    {
        if (!string.IsNullOrWhiteSpace(result.nearbyLiftName))
            return;

#if UNITY_2023_1_OR_NEWER
        var lifts = UnityEngine.Object.FindObjectsByType<LiftLine>(FindObjectsSortMode.None);
#else
        var lifts = UnityEngine.Object.FindObjectsOfType<LiftLine>();
#endif
        LiftLine best = null;
        float bestSqr = radius * radius;
        for (int i = 0; i < lifts.Length; i++)
        {
            LiftLine lift = lifts[i];
            if (lift == null)
                continue;

            TryGetNearestLiftStation(lift, position, out Vector3 station);
            float sqr = (station - position).sqrMagnitude;
            if (sqr <= bestSqr)
            {
                bestSqr = sqr;
                best = lift;
            }
        }

        ApplyLift(best, result);
    }

    private static void ResolveNearestRace(Vector3 position, float radius, SocialAnchorResolvedContext result)
    {
        if (!string.IsNullOrWhiteSpace(result.raceName))
            return;

#if UNITY_2023_1_OR_NEWER
        var races = UnityEngine.Object.FindObjectsByType<RaceCourseLine>(FindObjectsSortMode.None);
#else
        var races = UnityEngine.Object.FindObjectsOfType<RaceCourseLine>();
#endif
        RaceCourseLine best = null;
        float bestSqr = radius * radius;
        for (int i = 0; i < races.Length; i++)
        {
            RaceCourseLine race = races[i];
            if (race == null)
                continue;

            float sqr = (race.StartWorldPosition - position).sqrMagnitude;
            if (sqr <= bestSqr)
            {
                bestSqr = sqr;
                best = race;
            }
        }

        ApplyRace(best, result);
    }

    private static void ResolveNearestKiosk(Vector3 position, float radius, SocialAnchorResolvedContext result)
    {
        if (!string.IsNullOrWhiteSpace(result.kioskName))
            return;

#if UNITY_2023_1_OR_NEWER
        var kiosks = UnityEngine.Object.FindObjectsByType<SkiPassKiosk>(FindObjectsSortMode.None);
#else
        var kiosks = UnityEngine.Object.FindObjectsOfType<SkiPassKiosk>();
#endif
        SkiPassKiosk best = null;
        float bestSqr = radius * radius;
        for (int i = 0; i < kiosks.Length; i++)
        {
            SkiPassKiosk kiosk = kiosks[i];
            if (kiosk == null)
                continue;

            float sqr = (kiosk.transform.position - position).sqrMagnitude;
            if (sqr <= bestSqr)
            {
                bestSqr = sqr;
                best = kiosk;
            }
        }

        if (best != null)
            result.kioskName = FirstNonEmpty(result.kioskName, best.name);
    }

    private static void ApplyRun(SkiRunLine run, SocialAnchorResolvedContext result)
    {
        if (run == null)
            return;

        result.nearbyRunId = FirstNonEmpty(result.nearbyRunId, run.RunId);
        result.nearbyRunName = FirstNonEmpty(result.nearbyRunName, run.RunName);
        result.nearbyRunDifficulty = FirstNonEmpty(result.nearbyRunDifficulty, run.Difficulty.ToString());
    }

    private static void ApplyLift(LiftLine lift, SocialAnchorResolvedContext result)
    {
        if (lift == null)
            return;

        result.nearbyLiftId = FirstNonEmpty(result.nearbyLiftId, lift.name);
        result.nearbyLiftName = FirstNonEmpty(result.nearbyLiftName, lift.name);
    }

    private static void ApplyRace(RaceCourseLine race, SocialAnchorResolvedContext result)
    {
        if (race == null)
            return;

        result.raceId = FirstNonEmpty(result.raceId, race.RaceId);
        result.raceName = FirstNonEmpty(result.raceName, race.RaceName);

        if (!string.IsNullOrWhiteSpace(race.RegionId))
            result.regionId = FirstNonEmpty(result.regionId, race.RegionId);
    }

    private static bool TryGetNearestLiftStation(LiftLine lift, Vector3 position, out Vector3 station)
    {
        station = lift != null ? lift.transform.position : position;
        if (lift == null)
            return false;

        bool hasBottom = lift.bottomStation != null;
        bool hasTop = lift.topStation != null;
        if (hasBottom && hasTop)
        {
            float bottom = (lift.bottomStation.position - position).sqrMagnitude;
            float top = (lift.topStation.position - position).sqrMagnitude;
            station = bottom <= top ? lift.bottomStation.position : lift.topStation.position;
            return true;
        }

        if (hasBottom)
        {
            station = lift.bottomStation.position;
            return true;
        }

        if (hasTop)
        {
            station = lift.topStation.position;
            return true;
        }

        return false;
    }

    private static void ResolveRegion(Vector3 worldPosition, SocialAnchorResolvedContext result)
    {
        if (result == null)
            return;

        // If a component already supplied a region id, try to upgrade it to display name.
        if (!string.IsNullOrWhiteSpace(result.regionId))
        {
            if (TryResolveRegionNameFromId(result.regionId, out string authoredName))
                result.regionName = FirstNonEmpty(result.regionName, authoredName, result.regionId);

            return;
        }

        if (!TryFindRegionSet(out MapRegionSet set))
            return;

        if (set.MapData == null && TryFindMapData(out MapData fallbackMapData))
            set.SetMapData(fallbackMapData);

        if (set.MapData == null)
            return;

        set.EnsureInitialized();

        Vector2 uv = set.MapData.WorldToMapUV(worldPosition);
        MapRegionFace face = MapRegionUtility.ResolveRegion(set, uv);
        if (face == null)
            return;

        result.regionId = FirstNonEmpty(result.regionId, face.id);
        result.regionName = FirstNonEmpty(result.regionName, face.displayName, face.id);
    }

    private static bool TryResolveRegionNameFromId(string regionId, out string regionName)
    {
        regionName = string.Empty;

        if (string.IsNullOrWhiteSpace(regionId))
            return false;

        if (!TryFindRegionSet(out MapRegionSet set))
            return false;

        set.EnsureInitialized();

        MapRegionFace face = set.GetFaceById(regionId.Trim());
        if (face == null)
            return false;

        regionName = FirstNonEmpty(face.displayName, face.id);
        return !string.IsNullOrWhiteSpace(regionName);
    }

    private static bool TryFindRegionSet(out MapRegionSet set)
    {
        set = null;

        MapRegionSet preferred = null;
        MapRegionSet fallback = null;

        var sets = Resources.FindObjectsOfTypeAll<MapRegionSet>();
        for (int i = 0; i < sets.Length; i++)
        {
            MapRegionSet candidate = sets[i];
            if (candidate == null)
                continue;

            if (fallback == null)
                fallback = candidate;

            if (candidate.MapData != null)
            {
                if (preferred == null)
                    preferred = candidate;

                if (string.Equals(candidate.name, "MapRegionSet", StringComparison.OrdinalIgnoreCase))
                {
                    preferred = candidate;
                    break;
                }
            }
        }

        set = preferred != null ? preferred : fallback;
        return set != null;
    }

    private static bool TryFindMapData(out MapData mapData)
    {
        mapData = null;

        MapData fallback = null;
        var dataAssets = Resources.FindObjectsOfTypeAll<MapData>();
        for (int i = 0; i < dataAssets.Length; i++)
        {
            MapData candidate = dataAssets[i];
            if (candidate == null)
                continue;

            if (fallback == null)
                fallback = candidate;

            if (string.Equals(candidate.name, "MapData", StringComparison.OrdinalIgnoreCase))
            {
                mapData = candidate;
                return true;
            }
        }

        mapData = fallback;
        return mapData != null;
    }

    private static void ApplyFallbacks(NpcSocialAnchor anchor, SocialAnchorResolvedContext result)
    {
        result.regionName = FirstNonEmpty(result.regionName, result.regionId, "this area");
        result.nearbyRunName = FirstNonEmpty(result.nearbyRunName, "that run");
        result.nearbyRunDifficulty = FirstNonEmpty(result.nearbyRunDifficulty, "pretty serious");
        result.nearbyLiftName = FirstNonEmpty(result.nearbyLiftName, "that lift");
        result.nearbyPoiName = FirstNonEmpty(result.nearbyPoiName, anchor != null ? anchor.DisplayName : null, "over there");
        result.raceName = FirstNonEmpty(result.raceName, "the race");
        result.kioskName = FirstNonEmpty(result.kioskName, "the kiosk");
    }

    private static string FirstNonEmpty(params string[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
                return values[i].Trim();
        }

        return string.Empty;
    }
}
