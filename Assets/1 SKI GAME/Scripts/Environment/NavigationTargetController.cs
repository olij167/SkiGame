using System.Collections.Generic;
using UnityEngine;

namespace SkiGame.Navigation
{
    public enum NavigationTargetKind
    {
        TutorialObjective = 0,
        Waypoint = 10,
        PointOfInterest = 20,
        Resort = 30,
        Lift = 40,
        Shop = 50,
        Custom = 90
    }

    public struct NavigationTargetRequest
    {
        public Object owner;
        public string id;
        public string displayName;
        public NavigationTargetKind kind;
        public Transform targetTransform;
        public Vector3 worldPosition;
        public Vector3 worldOffset;
        public int priority;
        public bool showHud;
        public bool showWorldBeacon;
        public bool clearWhenReached;
        public float arriveDistance;
        public Color accentColor;
        public bool preferMiniMapIndicator;

        public Vector3 ResolveWorldPosition()
        {
            Vector3 basePosition = targetTransform != null ? targetTransform.position : worldPosition;
            return basePosition + worldOffset;
        }
    }

    public struct NavigationActiveState
    {
        public NavigationTargetRequest request;
        public Vector3 beaconWorldPosition;
    }

    [DisallowMultipleComponent]
    public sealed class NavigationTargetController : MonoBehaviour
    {
        private struct Entry
        {
            public NavigationTargetRequest request;
            public long sequence;
        }

        public static NavigationTargetController Instance { get; private set; }

        [Header("Beacon")]
        [SerializeField] private NavigationWorldBeaconController beaconPrefab;

        [Header("Player Tracking")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private float fallbackArriveDistance = 8f;


        private readonly Dictionary<int, Entry> _entries = new();
        private long _nextSequence = 1;

        private bool _hasActiveTarget;
        private NavigationTargetRequest _activeTarget;
        private Vector3 _activeBeaconWorldPosition;
        private string _activeSignature;

        private NavigationWorldBeaconController _activeBeaconInstance;

        public static NavigationTargetController EnsureInstance()
        {
            if (Instance != null)
                return Instance;

            var existing = FindObjectOfType<NavigationTargetController>();
            if (existing != null)
                return existing;

            var go = new GameObject("NavigationTargetController");
            DontDestroyOnLoad(go);
            return go.AddComponent<NavigationTargetController>();
        }

        public static void SetTarget(Object owner, NavigationTargetRequest request)
        {
            if (owner == null)
                return;

            EnsureInstance().SetTargetInternal(owner, request);
        }

        public static void ClearTarget(Object owner)
        {
            if (owner == null)
                return;

            if (EnsureInstance() == null)
                return;

            Instance.ClearTargetInternal(owner);
        }

        public static bool TryGetActiveState(out NavigationActiveState state)
        {
            if (EnsureInstance() != null && Instance._hasActiveTarget)
            {
                state = new NavigationActiveState
                {
                    request = Instance._activeTarget,
                    beaconWorldPosition = Instance._activeBeaconWorldPosition
                };
                return true;
            }

            state = default;
            return false;
        }

        public static bool TryGetActiveTarget(out NavigationTargetRequest request)
        {
            if (TryGetActiveState(out var state))
            {
                request = state.request;
                return true;
            }

            request = default;
            return false;
        }

        public static bool TryGetActiveBeaconWorldPosition(out Vector3 worldPosition)
        {
            if (TryGetActiveState(out var state))
            {
                worldPosition = state.beaconWorldPosition;
                return true;
            }

            worldPosition = default;
            return false;
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
            ResolveRuntimeReferences();
            RebuildActiveTarget();
        }

        private void LateUpdate()
        {
            PruneDestroyedOwners();
            ResolveRuntimeReferences();
            CheckArrival();
        }

        private void ResolveRuntimeReferences()
        {
            if (playerTransform != null)
                return;

            var ski = FindObjectOfType<SkiController>();
            if (ski != null)
            {
                playerTransform = ski.transform;
                return;
            }

            var walk = FindObjectOfType<WalkingController>();
            if (walk != null)
                playerTransform = walk.transform;
        }

        private void SetTargetInternal(Object owner, NavigationTargetRequest request)
        {
            request.owner = owner;

            if (string.IsNullOrWhiteSpace(request.displayName))
            {
                ClearTargetInternal(owner);
                return;
            }

            if (request.targetTransform == null && request.worldPosition == Vector3.zero)
            {
                ClearTargetInternal(owner);
                return;
            }

            int key = owner.GetInstanceID();
            _entries[key] = new Entry
            {
                request = request,
                sequence = _nextSequence++
            };

            RebuildActiveTarget();
        }

        private void ClearTargetInternal(Object owner)
        {
            int key = owner.GetInstanceID();
            if (_entries.Remove(key))
                RebuildActiveTarget();
        }

        private void PruneDestroyedOwners()
        {
            if (_entries.Count == 0)
                return;

            List<int> deadKeys = null;

            foreach (var pair in _entries)
            {
                if (pair.Value.request.owner == null)
                {
                    deadKeys ??= new List<int>();
                    deadKeys.Add(pair.Key);
                }
            }

            if (deadKeys == null || deadKeys.Count == 0)
                return;

            for (int i = 0; i < deadKeys.Count; i++)
                _entries.Remove(deadKeys[i]);

            RebuildActiveTarget();
        }

        private void RebuildActiveTarget()
        {
            bool found = false;
            NavigationTargetRequest bestRequest = default;
            int bestPriority = int.MinValue;
            long bestSequence = long.MinValue;

            foreach (var pair in _entries)
            {
                var entry = pair.Value;
                var request = entry.request;

                if (request.owner == null)
                    continue;

                if (string.IsNullOrWhiteSpace(request.displayName))
                    continue;

                if (request.targetTransform == null && request.worldPosition == Vector3.zero)
                    continue;

                bool better =
                    !found ||
                    request.priority > bestPriority ||
                    (request.priority == bestPriority && entry.sequence > bestSequence);

                if (!better)
                    continue;

                found = true;
                bestRequest = request;
                bestPriority = request.priority;
                bestSequence = entry.sequence;
            }

            if (!found)
            {
                _hasActiveTarget = false;
                _activeTarget = default;
                _activeSignature = string.Empty;
                DestroyActiveBeacon();
                return;
            }

            string newSignature = BuildSignature(bestRequest);

            bool sameSession =
                _hasActiveTarget &&
                !string.IsNullOrEmpty(_activeSignature) &&
                _activeSignature == newSignature;

            _hasActiveTarget = true;
            _activeTarget = bestRequest;

            if (!sameSession)
            {
                _activeSignature = newSignature;
                _activeBeaconWorldPosition = bestRequest.ResolveWorldPosition();
                RebuildBeaconInstance();
            }
            else
            {
                UpdateBeaconVisualsOnly();
            }
        }

        private string BuildSignature(NavigationTargetRequest request)
        {
            int ownerId = request.owner != null ? request.owner.GetInstanceID() : 0;
            string id = string.IsNullOrWhiteSpace(request.id) ? "nav" : request.id;
            return $"{ownerId}:{(int)request.kind}:{id}";
        }

        private void RebuildBeaconInstance()
        {
            DestroyActiveBeacon();

            if (!_activeTarget.showWorldBeacon || beaconPrefab == null)
                return;

            _activeBeaconInstance = Instantiate(beaconPrefab, _activeBeaconWorldPosition, beaconPrefab.transform.rotation);
            _activeBeaconInstance.name = $"{beaconPrefab.name}_{_activeTarget.displayName}";
            _activeBeaconInstance.Configure(_activeBeaconWorldPosition, _activeTarget.displayName, _activeTarget.accentColor);
        }

        private void UpdateBeaconVisualsOnly()
        {
            if (_activeBeaconInstance == null)
                return;

            _activeBeaconInstance.Configure(_activeBeaconWorldPosition, _activeTarget.displayName, _activeTarget.accentColor);
        }

        private void DestroyActiveBeacon()
        {
            if (_activeBeaconInstance != null)
                Destroy(_activeBeaconInstance.gameObject);

            _activeBeaconInstance = null;
        }

        private void CheckArrival()
        {
            if (!_hasActiveTarget || !_activeTarget.clearWhenReached || playerTransform == null)
                return;

            float arriveDistance = Mathf.Max(0.5f, _activeTarget.arriveDistance > 0f ? _activeTarget.arriveDistance : fallbackArriveDistance);

            Vector3 from = playerTransform.position;
            Vector3 to = _activeBeaconWorldPosition;

            from.y = 0f;
            to.y = 0f;

            if (Vector3.Distance(from, to) <= arriveDistance)
            {
                Object owner = _activeTarget.owner;
                if (owner != null)
                    ClearTargetInternal(owner);
            }
        }
    }
}