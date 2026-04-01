using UnityEngine;

namespace SkiGame.Map
{
    [DisallowMultipleComponent]
    public sealed class PlayerMapRegionTracker : MonoBehaviour
    {
        [SerializeField] private MapRegionSet regionSet;
        [SerializeField] private Transform player;
        [SerializeField] private float pollInterval = 0.2f;

        private string _currentRegionId;
        private float _nextPollTime;

        private bool _loggedMissingSetup;
        private bool _loggedNoRegionAtPlayer;

        public string CurrentRegionId => _currentRegionId;

        public MapRegionFace CurrentRegion
        {
            get
            {
                if (regionSet == null || string.IsNullOrWhiteSpace(_currentRegionId))
                    return null;

                return regionSet.GetFaceById(_currentRegionId);
            }
        }

        public void SetPlayer(Transform value)
        {
            player = value;
        }

        public void SetRegionSet(MapRegionSet value, MapData fallbackMapData = null)
        {
            regionSet = value;

            if (regionSet != null && regionSet.MapData == null && fallbackMapData != null)
                regionSet.SetMapData(fallbackMapData);
        }

        public void Configure(MapRegionSet set, Transform playerTransform, MapData fallbackMapData = null)
        {
            if (set != null)
            {
                regionSet = set;

                if (regionSet.MapData == null && fallbackMapData != null)
                    regionSet.SetMapData(fallbackMapData);
            }

            if (playerTransform != null)
                player = playerTransform;
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            _loggedMissingSetup = false;
            _loggedNoRegionAtPlayer = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ResolveReferences();
        }
#endif

        private void ResolveReferences()
        {
            if (player == null)
            {
                var ski = FindObjectOfType<SkiController>();
                if (ski != null)
                    player = ski.transform;
            }
        }

        private void Update()
        {
            ResolveReferences();

            if (regionSet == null || regionSet.MapData == null || player == null)
            {
                if (!_loggedMissingSetup)
                {
                    Debug.LogWarning(
                        $"[PlayerMapRegionTracker] Not ready. regionSet={(regionSet != null ? regionSet.name : "null")}, " +
                        $"mapData={(regionSet != null && regionSet.MapData != null ? regionSet.MapData.name : "null")}, " +
                        $"player={(player != null ? player.name : "null")}");
                    _loggedMissingSetup = true;
                }
                return;
            }

            _loggedMissingSetup = false;

            regionSet.EnsureInitialized();

            if (Time.unscaledTime < _nextPollTime)
                return;

            _nextPollTime = Time.unscaledTime + Mathf.Max(0.02f, pollInterval);

            Vector2 uv = regionSet.MapData.WorldToMapUV(player.position);
            string newRegionId = MapRegionUtility.ResolveRegionId(regionSet, uv);

            if (string.IsNullOrWhiteSpace(newRegionId))
            {
                if (!_loggedNoRegionAtPlayer)
                {
                    Debug.LogWarning(
                        $"[PlayerMapRegionTracker] No region found for player at world={player.position} uv={uv} using set '{regionSet.name}'.");
                    _loggedNoRegionAtPlayer = true;
                }
            }
            else
            {
                _loggedNoRegionAtPlayer = false;
            }

            if (!string.Equals(_currentRegionId, newRegionId, System.StringComparison.Ordinal))
            {
                _currentRegionId = newRegionId;

                if (!string.IsNullOrWhiteSpace(_currentRegionId))
                    Debug.Log($"[PlayerMapRegionTracker] Entered region: {_currentRegionId}");
            }
        }
    }
}