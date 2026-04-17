using System.Collections.Generic;
using UnityEngine;
using SkiGame.POI;
using SkiGame.Progression;
using SkiGame.Map;

namespace SkiGame.Progression
{
    [DisallowMultipleComponent]
    public sealed class POIVisitTracker : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField] private float visitRadiusMeters = 8f;

        [Tooltip("How often to check for nearby POIs. Keep modest to stay cheap.")]
        [SerializeField] private float checkIntervalSeconds = 0.25f;

        [Tooltip("If true, visiting a POI is recorded in the session list.")]
        [SerializeField] private bool recordSessionVisits = true;

        [Tooltip("If true, visiting a POI is recorded in the lifetime list.")]
        [SerializeField] private bool recordLifetimeVisits = true;

        [Header("Progression")]
        [SerializeField] private PlayerMapRegionTracker regionTracker;
        [SerializeField] private RegionReputationManager regionReputationManager;
        [SerializeField] private QuestSignalBus questSignalBus;

        [Header("Anti-Spam")]
        [Tooltip("Prevents rapid re-processing of the same POI while hovering near it.")]
        [SerializeField] private float perPoiCooldownSeconds = 3f;

        private float _nextCheckTime;

        // POI id -> next allowed time
        private readonly Dictionary<string, float> _cooldowns = new Dictionary<string, float>(64);

        private void Update()
        {
            if (Time.unscaledTime < _nextCheckTime) return;
            _nextCheckTime = Time.unscaledTime + Mathf.Max(0.05f, checkIntervalSeconds);

            ResolveReferences();

            var reg = PointOfInterestRegistry.Instance;
            if (reg == null) return;

            var mgr = PlayerStatsManager.Instance;
            var profile = mgr != null ? mgr.Profile : null;
            if (profile == null) return;

            if (!reg.TryFindNearest(transform.position, visitRadiusMeters, out POIInfo nearest))
                return;

            if (!nearest.IsValid)
                return;

            // Cooldown gate
            if (_cooldowns.TryGetValue(nearest.id, out float until) && Time.unscaledTime < until)
                return;

            _cooldowns[nearest.id] = Time.unscaledTime + Mathf.Max(0f, perPoiCooldownSeconds);

            bool changed = false;
            bool grantedDiscoveryReputation = false;

            if (recordSessionVisits)
            {
                changed |= profile.TryAddVisitedLandmarkThisSession(nearest.id);
                profile.IncrementLandmarkVisitCount(nearest.id, session: true);
            }

            if (recordLifetimeVisits)
            {
                changed |= profile.TryAddVisitedLandmark(nearest.id);
                profile.IncrementLandmarkVisitCount(nearest.id, session: false);
            }

            if (TryResolveRegionId(nearest, out string regionId) &&
                profile.TryMarkPoiDiscoveryRewarded(nearest.id) &&
                regionReputationManager != null &&
                regionReputationManager.TryGetDiscoveryReward(regionId, out int reputationReward) &&
                reputationReward > 0)
            {
                regionReputationManager.AwardReputation(regionId, reputationReward, $"poi:{nearest.id}");
                changed = true;
                grantedDiscoveryReputation = true;
            }

            if ((changed || grantedDiscoveryReputation) && questSignalBus != null)
            {
                QuestSignalData data = QuestSignalData.Create()
                    .WithTag("poiId", nearest.id)
                    .WithTag("category", nearest.category.ToString().ToLowerInvariant());

                if (TryResolveRegionId(nearest, out string signalRegionId))
                    data.WithTag("regionId", signalRegionId);

                questSignalBus.RaiseEvent("poi.discovered", data);
            }

            if (changed)
                mgr.Save();
        }

        private void ResolveReferences()
        {
            if (regionTracker == null)
                regionTracker = FindObjectOfType<PlayerMapRegionTracker>();

            if (regionReputationManager == null)
                regionReputationManager = RegionReputationManager.Instance != null
                    ? RegionReputationManager.Instance
                    : RegionReputationManager.EnsureInstance();

            if (questSignalBus == null)
                questSignalBus = FindObjectOfType<QuestSignalBus>();
        }

        private bool TryResolveRegionId(POIInfo poi, out string regionId)
        {
            regionId = string.Empty;

            if (poi.source is SkiResortZone resortZone &&
                !string.IsNullOrWhiteSpace(resortZone.ResortId))
            {
                SkiResortAccessManager resortAccess = SkiResortAccessManager.Instance != null
                    ? SkiResortAccessManager.Instance
                    : SkiResortAccessManager.EnsureInstance();

                if (resortAccess != null && resortAccess.TryGetRegionId(resortZone.ResortId, out regionId))
                    return !string.IsNullOrWhiteSpace(regionId);
            }

            if (regionTracker != null && !string.IsNullOrWhiteSpace(regionTracker.CurrentRegionId))
            {
                regionId = regionTracker.CurrentRegionId.Trim();
                return true;
            }

            return false;
        }
    }
}
