using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkiGame.Navigation
{
    [Serializable]
    public struct MapWaypointRecord
    {
        public string id;
        public string sourceKey;
        public string displayName;
        public Vector3 worldPosition;
        public Color color;
        public NavigationTargetKind kind;
    }

    [DisallowMultipleComponent]
    public sealed class MapWaypointManager : MonoBehaviour
    {
        public static MapWaypointManager Instance { get; private set; }

        [Header("Beacon")]
        [SerializeField] private NavigationWorldBeaconController beaconPrefab;

        [Header("Guidance")]
        [SerializeField] private int navigationPriority = 25;
        [SerializeField] private float activeWaypointArriveDistance = 8f;

        [Header("Colours")]
        [SerializeField]
        private Color[] waypointColours =
        {
            new Color(0.27f, 0.76f, 1.00f, 1f), // blue
            new Color(0.98f, 0.35f, 0.82f, 1f), // pink
            new Color(1.00f, 0.85f, 0.18f, 1f), // yellow
            new Color(0.38f, 0.92f, 0.46f, 1f), // green
            new Color(1.00f, 0.49f, 0.20f, 1f), // orange
        };

        private readonly List<MapWaypointRecord> _waypoints = new();
        private readonly Dictionary<string, NavigationWorldBeaconController> _beaconInstances = new();

        private int _selectedColourIndex;
        private int _customSequence;
        private string _activeWaypointId;
        private string _selectedWaypointId;
        private int _version;

        public int Version => _version;
        public int SelectedColourIndex => _selectedColourIndex;
        public Color SelectedColour => GetColour(_selectedColourIndex);
        public IReadOnlyList<MapWaypointRecord> Waypoints => _waypoints;
        public string SelectedWaypointId => _selectedWaypointId;
        public string ActiveWaypointId => _activeWaypointId;

        public static MapWaypointManager EnsureInstance()
        {
            if (Instance != null)
                return Instance;

            var existing = FindObjectOfType<MapWaypointManager>();
            if (existing != null)
                return existing;

            var go = new GameObject("MapWaypointManager");
            DontDestroyOnLoad(go);
            return go.AddComponent<MapWaypointManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void SelectWaypoint(string waypointId, bool setActive = true)
        {
            if (string.IsNullOrWhiteSpace(waypointId))
                return;

            if (FindById(waypointId) < 0)
                return;

            _selectedWaypointId = waypointId;

            MarkChanged();

            if (setActive)
                SetActiveWaypoint(waypointId);
        }

        public void ClearSelectedWaypoint()
        {
            _selectedWaypointId = null;
            MarkChanged();
        }

        public bool IsWaypointSelected(string waypointId)
        {
            return !string.IsNullOrWhiteSpace(waypointId) &&
                   string.Equals(_selectedWaypointId, waypointId, StringComparison.Ordinal);
        }

        public bool TryGetWaypoint(string waypointId, out MapWaypointRecord record)
        {
            int index = FindById(waypointId);
            if (index >= 0)
            {
                record = _waypoints[index];
                return true;
            }

            record = default;
            return false;
        }

        public bool TryGetWaypointBySourceKey(string sourceKey, out MapWaypointRecord record)
        {
            int index = FindBySourceKey(sourceKey);
            if (index >= 0)
            {
                record = _waypoints[index];
                return true;
            }

            record = default;
            return false;
        }

        public bool HasWaypointForSource(string sourceKey)
        {
            return FindBySourceKey(sourceKey) >= 0;
        }

        public void ToggleOrCycleSourceWaypoint(string sourceKey, string displayName, Vector3 worldPosition, NavigationTargetKind kind, Color sourceColour, bool setActive = true)
        {
            if (string.IsNullOrWhiteSpace(sourceKey))
                return;

            int index = FindBySourceKey(sourceKey);
            if (index >= 0)
            {
                string id = _waypoints[index].id;
                SelectWaypoint(id, setActive);
                CycleWaypointColour(id);
                return;
            }

            ToggleSourceWaypoint(sourceKey, displayName, worldPosition, kind, sourceColour, setActive);
        }

        public void RemoveWaypointBySourceKey(string sourceKey)
        {
            int index = FindBySourceKey(sourceKey);
            if (index < 0)
                return;

            RemoveWaypoint(_waypoints[index].id);
        }

        public void SelectNextColour()
        {
            if (waypointColours == null || waypointColours.Length == 0)
                return;

            _selectedColourIndex = (_selectedColourIndex + 1) % waypointColours.Length;
        }

        public void SelectPreviousColour()
        {
            if (waypointColours == null || waypointColours.Length == 0)
                return;

            _selectedColourIndex--;
            if (_selectedColourIndex < 0)
                _selectedColourIndex = waypointColours.Length - 1;
        }

        public Color GetColour(int index)
        {
            if (waypointColours == null || waypointColours.Length == 0)
                return new Color(0.27f, 0.76f, 1.00f, 1f);

            index = Mathf.Clamp(index, 0, waypointColours.Length - 1);
            return waypointColours[index];
        }

        public string AddCustomWaypoint(Vector3 worldPosition, string displayName = null, bool setActive = true)
        {
            string id = $"custom-waypoint-{_customSequence++}";
            string label = string.IsNullOrWhiteSpace(displayName) ? "Custom Waypoint" : displayName;

            var record = new MapWaypointRecord
            {
                id = id,
                sourceKey = string.Empty,
                displayName = label,
                worldPosition = worldPosition,
                color = SelectedColour,
                kind = NavigationTargetKind.Waypoint
            };

            _waypoints.Add(record);
            SpawnBeacon(record);
            MarkChanged();

            _selectedWaypointId = id;

            if (setActive)
                SetActiveWaypoint(id);

            return id;
        }

        public void ToggleSourceWaypoint(string sourceKey, string displayName, Vector3 worldPosition, NavigationTargetKind kind, Color sourceColour, bool setActive = true)
        {
            if (string.IsNullOrWhiteSpace(sourceKey))
                return;

            int existingIndex = FindBySourceKey(sourceKey);
            if (existingIndex >= 0)
            {
                string existingId = _waypoints[existingIndex].id;
                RemoveWaypoint(existingId);
                return;
            }

            var record = new MapWaypointRecord
            {
                id = sourceKey,
                sourceKey = sourceKey,
                displayName = displayName,
                worldPosition = worldPosition,
                color = sourceColour,
                kind = kind
            };

            _waypoints.Add(record);
            SpawnBeacon(record);
            MarkChanged();

            _selectedWaypointId = record.id;

            if (setActive)
                SetActiveWaypoint(record.id);
        }

        public void CycleWaypointColour(string waypointId)
        {
            int index = FindById(waypointId);
            if (index < 0 || waypointColours == null || waypointColours.Length == 0)
                return;

            Color current = _waypoints[index].color;
            int currentIndex = FindNearestPresetIndex(current);
            int nextIndex = (currentIndex + 1) % waypointColours.Length;

            SetWaypointColour(waypointId, waypointColours[nextIndex]);
            _selectedColourIndex = nextIndex;
        }

        public void SetWaypointColour(string waypointId, Color colour)
        {
            int index = FindById(waypointId);
            if (index < 0)
                return;

            var record = _waypoints[index];
            record.color = colour;
            _waypoints[index] = record;
            MarkChanged();

            if (_beaconInstances.TryGetValue(waypointId, out var beacon) && beacon != null)
                beacon.Configure(record.worldPosition, record.displayName, record.color);

            if (_activeWaypointId == waypointId)
                PublishActiveWaypoint();
        }

        public void RemoveWaypoint(string waypointId)
        {
            if (string.IsNullOrWhiteSpace(waypointId))
                return;

            int index = FindById(waypointId);
            if (index < 0)
                return;

            string removedId = _waypoints[index].id;
            _waypoints.RemoveAt(index);

            if (_beaconInstances.TryGetValue(removedId, out var beacon))
            {
                if (beacon != null)
                    Destroy(beacon.gameObject);

                _beaconInstances.Remove(removedId);
            }

            if (_selectedWaypointId == removedId)
                _selectedWaypointId = null;

            if (_activeWaypointId == removedId)
            {
                _activeWaypointId = _waypoints.Count > 0 ? _waypoints[_waypoints.Count - 1].id : null;
                PublishActiveWaypoint();
            }

            MarkChanged();
        }

        public void ClearAllWaypoints()
        {
            for (int i = 0; i < _waypoints.Count; i++)
            {
                string id = _waypoints[i].id;
                if (_beaconInstances.TryGetValue(id, out var beacon) && beacon != null)
                    Destroy(beacon.gameObject);
            }

            _waypoints.Clear();
            _beaconInstances.Clear();
            _activeWaypointId = null;
            _selectedWaypointId = null;
            NavigationTargetController.ClearTarget(this);

            MarkChanged();
        }

        public void SetActiveWaypoint(string waypointId)
        {
            if (string.IsNullOrWhiteSpace(waypointId))
                return;

            if (FindById(waypointId) < 0)
                return;

            _activeWaypointId = waypointId;
            PublishActiveWaypoint();
        }

        private void PublishActiveWaypoint()
        {
            if (string.IsNullOrWhiteSpace(_activeWaypointId))
            {
                NavigationTargetController.ClearTarget(this);
                return;
            }

            int index = FindById(_activeWaypointId);
            if (index < 0)
            {
                NavigationTargetController.ClearTarget(this);
                return;
            }

            MapWaypointRecord record = _waypoints[index];

            NavigationTargetController.SetTarget(this, new NavigationTargetRequest
            {
                owner = this,
                id = record.id,
                displayName = record.displayName,
                kind = record.kind,
                targetTransform = null,
                worldPosition = record.worldPosition,
                worldOffset = Vector3.zero,
                priority = navigationPriority,
                showHud = true,
                showWorldBeacon = true,
                clearWhenReached = false,
                arriveDistance = activeWaypointArriveDistance,
                accentColor = record.color,
                preferMiniMapIndicator = true,
            });
        }

        public void RenameWaypoint(string waypointId, string newDisplayName)
        {
            int index = FindById(waypointId);
            if (index < 0)
                return;

            string trimmed = string.IsNullOrWhiteSpace(newDisplayName)
                ? "Custom Waypoint"
                : newDisplayName.Trim();

            var record = _waypoints[index];
            record.displayName = trimmed;
            _waypoints[index] = record;

            if (_beaconInstances.TryGetValue(waypointId, out var beacon) && beacon != null)
                beacon.Configure(record.worldPosition, record.displayName, record.color);

            MarkChanged();

            if (_activeWaypointId == waypointId)
                PublishActiveWaypoint();
        }

        private void SpawnBeacon(MapWaypointRecord record)
        {
            if (beaconPrefab == null)
                return;

            var beacon = Instantiate(beaconPrefab, record.worldPosition, beaconPrefab.transform.rotation);
            beacon.name = $"Waypoint_{record.displayName}";
            beacon.Configure(record.worldPosition, record.displayName, record.color);
            _beaconInstances[record.id] = beacon;
        }

        private int FindNearestPresetIndex(Color colour)
        {
            if (waypointColours == null || waypointColours.Length == 0)
                return 0;

            int bestIndex = 0;
            float bestDist = float.MaxValue;

            for (int i = 0; i < waypointColours.Length; i++)
            {
                Color c = waypointColours[i];
                float d =
                    (c.r - colour.r) * (c.r - colour.r) +
                    (c.g - colour.g) * (c.g - colour.g) +
                    (c.b - colour.b) * (c.b - colour.b) +
                    (c.a - colour.a) * (c.a - colour.a);

                if (d < bestDist)
                {
                    bestDist = d;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private int FindById(string waypointId)
        {
            for (int i = 0; i < _waypoints.Count; i++)
            {
                if (string.Equals(_waypoints[i].id, waypointId, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private int FindBySourceKey(string sourceKey)
        {
            for (int i = 0; i < _waypoints.Count; i++)
            {
                if (string.Equals(_waypoints[i].sourceKey, sourceKey, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }
        private void MarkChanged()
        {
            _version++;
        }

    }
}