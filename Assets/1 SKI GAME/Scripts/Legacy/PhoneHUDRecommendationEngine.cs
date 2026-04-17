using System;
using UnityEngine;
using SkiGame.POI;

namespace SkiGame.Progression
{
    public enum PhoneHUDRecommendationType
    {
        None = 0,
        Run = 10,
        Lift = 20,
        POI = 30,
    }

    public readonly struct PhoneHUDRecommendation
    {
        public readonly PhoneHUDRecommendationType type;
        public readonly string targetId;
        public readonly string title;
        public readonly string subtitle;
        public readonly Vector3 worldPosition;
        public readonly bool isValid;

        public PhoneHUDRecommendation(
            PhoneHUDRecommendationType type,
            string targetId,
            string title,
            string subtitle,
            Vector3 worldPosition,
            bool isValid)
        {
            this.type = type;
            this.targetId = targetId ?? string.Empty;
            this.title = title ?? string.Empty;
            this.subtitle = subtitle ?? string.Empty;
            this.worldPosition = worldPosition;
            this.isValid = isValid;
        }

        public static PhoneHUDRecommendation None =>
            new PhoneHUDRecommendation(
                PhoneHUDRecommendationType.None,
                string.Empty,
                "Map",
                "Explore the mountain",
                Vector3.zero,
                false);
    }

    public static class PhoneHUDRecommendationEngine
    {
        public static PhoneHUDRecommendation Build(
            PlayerStatsProfile profile,
            PointOfInterestRegistry registry,
            SkiPassManager skiPassMgr,
            Transform playerTransform)
        {
            if (registry == null || registry.Current == null || registry.Current.Count == 0)
                return PhoneHUDRecommendation.None;

            Vector3 playerPos = playerTransform != null ? playerTransform.position : Vector3.zero;
            bool havePlayer = playerTransform != null;

            POIInfo? nearestIncompleteRun = null;
            float nearestIncompleteRunDistSq = float.MaxValue;

            POIInfo? nearestUndiscoveredRun = null;
            float nearestUndiscoveredRunDistSq = float.MaxValue;

            POIInfo? nearestUndiscoveredPoi = null;
            float nearestUndiscoveredPoiDistSq = float.MaxValue;

            POIInfo? nearestLockedLift = null;
            float nearestLockedLiftDistSq = float.MaxValue;

            for (int i = 0; i < registry.Current.Count; i++)
            {
                var poi = registry.Current[i];
                if (!poi.IsValid) continue;

                float distSq = havePlayer
                    ? (new Vector2(playerPos.x - poi.position.x, playerPos.z - poi.position.z)).sqrMagnitude
                    : i; // stable fallback if no player

                switch (poi.type)
                {
                    case POIType.SkiRun:
                        {
                            bool discovered = profile != null && profile.HasVisitedRun(poi.id);
                            bool completed = profile != null && profile.HasCompletedRunEver(poi.id);

                            if (!completed)
                            {
                                if (discovered)
                                {
                                    if (distSq < nearestIncompleteRunDistSq)
                                    {
                                        nearestIncompleteRun = poi;
                                        nearestIncompleteRunDistSq = distSq;
                                    }
                                }
                                else
                                {
                                    if (distSq < nearestUndiscoveredRunDistSq)
                                    {
                                        nearestUndiscoveredRun = poi;
                                        nearestUndiscoveredRunDistSq = distSq;
                                    }
                                }
                            }

                            break;
                        }

                    case POIType.Custom:
                        {
                            bool discovered = profile != null && profile.HasVisitedLandmark(poi.id);
                            if (!discovered && distSq < nearestUndiscoveredPoiDistSq)
                            {
                                nearestUndiscoveredPoi = poi;
                                nearestUndiscoveredPoiDistSq = distSq;
                            }

                            break;
                        }

                    case POIType.SkiLift:
                        {
                            bool locked = false;
                            if (poi.source is LiftLine lift && skiPassMgr != null)
                                locked = !skiPassMgr.CanUseLift(lift);

                            if (locked && distSq < nearestLockedLiftDistSq)
                            {
                                nearestLockedLift = poi;
                                nearestLockedLiftDistSq = distSq;
                            }

                            break;
                        }
                }
            }

            if (nearestIncompleteRun.HasValue)
            {
                var poi = nearestIncompleteRun.Value;
                return new PhoneHUDRecommendation(
                    PhoneHUDRecommendationType.Run,
                    poi.id,
                    "Next Run",
                    $"Finish {SafeName(poi.displayName)}",
                    poi.position,
                    true);
            }

            if (nearestUndiscoveredRun.HasValue)
            {
                var poi = nearestUndiscoveredRun.Value;
                return new PhoneHUDRecommendation(
                    PhoneHUDRecommendationType.Run,
                    poi.id,
                    "Explore Next",
                    $"Find {SafeName(poi.displayName)}",
                    poi.position,
                    true);
            }

            if (nearestUndiscoveredPoi.HasValue)
            {
                var poi = nearestUndiscoveredPoi.Value;
                return new PhoneHUDRecommendation(
                    PhoneHUDRecommendationType.POI,
                    poi.id,
                    "New Landmark",
                    $"Visit {SafeName(poi.displayName)}",
                    poi.position,
                    true);
            }

            if (nearestLockedLift.HasValue)
            {
                var poi = nearestLockedLift.Value;
                return new PhoneHUDRecommendation(
                    PhoneHUDRecommendationType.Lift,
                    poi.id,
                    "Upgrade Route",
                    $"{SafeName(poi.displayName)} needs a higher pass",
                    poi.position,
                    true);
            }

            return new PhoneHUDRecommendation(
                PhoneHUDRecommendationType.None,
                string.Empty,
                "Map",
                "Mountain checklist advancing",
                Vector3.zero,
                false);
        }

        private static string SafeName(string s)
        {
            return string.IsNullOrWhiteSpace(s) ? "this location" : s.Trim();
        }
    }
}