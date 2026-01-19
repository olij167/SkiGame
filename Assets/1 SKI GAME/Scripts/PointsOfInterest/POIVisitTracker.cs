using System.Collections.Generic;
using UnityEngine;
using SkiGame.POI;
using SkiGame.Progression;

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

            if (recordSessionVisits)
                changed |= profile.TryAddVisitedLandmarkThisSession(nearest.id);

            if (recordLifetimeVisits)
                changed |= profile.TryAddVisitedLandmark(nearest.id);

            // Optional: if you want instant persistence on visit, uncomment:
            // if (changed) mgr.Save();
        }
    }
}
