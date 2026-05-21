using UnityEngine;

namespace PungentFunk.Utilities.SceneTools
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Utilities/Scene Tools/Pungent Trigger Sensor Gizmo")]
    public sealed class PungentTriggerSensorGizmo : MonoBehaviour, IPungentTriggerSensorProvider, IPungentSceneGizmoPerformanceProvider
    {
        public bool drawInScene = true;
        public bool drawWhenSelectedOnly = true;
        public bool drawTriggerBounds = true;
        public bool drawOverlapLinks = true;
        public bool drawOverlapLabels = false;
        public bool drawRecentExits = true;
        [Min(0.05f)] public float recentExitLifetime = 1.5f;
        [Min(1)] public int maxOverlaps = 32;
        [Tooltip("Maximum SceneView camera distance for drawing this sensor. Zero means unlimited.")]
        [Min(0f)] public float maxDrawDistance = 0f;
        public Color idleColor = new Color(0.35f, 0.75f, 1f, 0.65f);
        public Color activeColor = new Color(0.4f, 1f, 0.6f, 0.9f);
        public Color overlapColor = new Color(1f, 0.85f, 0.25f, 0.9f);
        public Color recentExitColor = new Color(1f, 0.45f, 0.25f, 0.65f);
        public Collider observedTrigger;

        private PungentTriggerOverlapSnapshot[] _overlaps = new PungentTriggerOverlapSnapshot[32];
        private PungentTriggerOverlapSnapshot[] _recentExits = new PungentTriggerOverlapSnapshot[32];
        private int[] _overlapIds = new int[32];
        private int[] _recentExitIds = new int[32];
        private int _overlapCount;
        private int _recentExitCount;

        private void OnValidate()
        {
            recentExitLifetime = Mathf.Max(0.05f, recentExitLifetime);
            maxOverlaps = Mathf.Clamp(maxOverlaps, 1, 256);
            maxDrawDistance = Mathf.Max(0f, maxDrawDistance);
            EnsureBuffers();
        }

        public void ClearHistory()
        {
            _overlapCount = 0;
            _recentExitCount = 0;
        }

        private void OnTriggerEnter(Collider other)
        {
            UpsertOverlap(other, false);
        }

        private void OnTriggerStay(Collider other)
        {
            UpsertOverlap(other, false);
        }

        private void OnTriggerExit(Collider other)
        {
            RemoveOverlap(other);
            UpsertRecentExit(other);
        }

        public bool TryGetTriggerSensorSnapshot(out PungentTriggerSensorSnapshot snapshot)
        {
            EnsureBuffers();
            ExpireRecentExits();
            Collider collider = ResolveTrigger();
            bool hasCollider = collider != null;
            bool isTrigger = hasCollider && collider.isTrigger;
            snapshot = new PungentTriggerSensorSnapshot
            {
                owner = this,
                observedTrigger = collider,
                drawInScene = drawInScene,
                drawWhenSelectedOnly = drawWhenSelectedOnly,
                drawTriggerBounds = drawTriggerBounds,
                drawOverlapLinks = drawOverlapLinks,
                drawOverlapLabels = drawOverlapLabels,
                drawRecentExits = drawRecentExits,
                hasCollider = hasCollider,
                colliderIsTrigger = isTrigger,
                triggerBounds = hasCollider ? collider.bounds : new Bounds(transform.position, Vector3.one),
                maxDrawDistance = Mathf.Max(0f, maxDrawDistance),
                overlapCount = _overlapCount,
                recentExitCount = _recentExitCount,
                maxOverlaps = Mathf.Max(1, maxOverlaps),
                idleColor = idleColor,
                activeColor = activeColor,
                overlapColor = overlapColor,
                recentExitColor = recentExitColor,
                warning = !hasCollider ? "Trigger sensor has no collider." : (!isTrigger ? "Observed collider is not marked as trigger." : string.Empty),
                overlaps = _overlaps,
                recentExits = _recentExits
            };

            return drawInScene && isActiveAndEnabled;
        }

        public bool TryGetPerformanceEstimate(out PungentSceneGizmoPerformanceEstimate estimate)
        {
            estimate = new PungentSceneGizmoPerformanceEstimate
            {
                owner = this,
                active = isActiveAndEnabled,
                drawInScene = drawInScene,
                selectedOnly = drawWhenSelectedOnly,
                hasLabels = drawOverlapLabels,
                alwaysVisible = !drawWhenSelectedOnly,
                estimatedDrawOperations = drawInScene && isActiveAndEnabled ? ((drawTriggerBounds ? 1 : 0) + _overlapCount * ((drawOverlapLinks ? 1 : 0) + (drawOverlapLabels ? 1 : 0)) + (drawRecentExits ? _recentExitCount : 0)) : 0,
                estimatedLabels = drawInScene && isActiveAndEnabled && drawOverlapLabels ? _overlapCount : 0,
                estimatedTrajectorySamples = 0,
                priority = _overlapCount > 0 ? 15 : 0,
                providerCategory = "Trigger Sensor",
                warning = BuildWarning()
            };
            return true;
        }

        private void UpsertOverlap(Collider other, bool recentExit)
        {
            if (other == null)
                return;

            EnsureBuffers();
            int id = other.GetInstanceID();
            int index = FindId(_overlapIds, _overlapCount, id);
            if (index < 0)
            {
                if (_overlapCount >= _overlaps.Length)
                    index = FindOldest(_overlaps, _overlapCount);
                else
                    index = _overlapCount++;
                _overlapIds[index] = id;
            }

            _overlaps[index] = BuildOverlap(other, recentExit);
        }

        private void UpsertRecentExit(Collider other)
        {
            if (other == null || !drawRecentExits)
                return;

            EnsureBuffers();
            int id = other.GetInstanceID();
            int index = FindId(_recentExitIds, _recentExitCount, id);
            if (index < 0)
            {
                if (_recentExitCount >= _recentExits.Length)
                    index = FindOldest(_recentExits, _recentExitCount);
                else
                    index = _recentExitCount++;
                _recentExitIds[index] = id;
            }

            _recentExitIds[index] = id;
            _recentExits[index] = BuildOverlap(other, true);
        }

        private void RemoveOverlap(Collider other)
        {
            if (other == null)
                return;

            int id = other.GetInstanceID();
            int index = FindId(_overlapIds, _overlapCount, id);
            if (index < 0)
                return;

            for (int i = index; i < _overlapCount - 1; i++)
            {
                _overlaps[i] = _overlaps[i + 1];
                _overlapIds[i] = _overlapIds[i + 1];
            }
            _overlapCount = Mathf.Max(0, _overlapCount - 1);
        }

        private void ExpireRecentExits()
        {
            float now = Time.time;
            float lifetime = Mathf.Max(0.05f, recentExitLifetime);
            int write = 0;
            for (int i = 0; i < _recentExitCount; i++)
            {
                if (now - _recentExits[i].time > lifetime)
                    continue;

                _recentExits[write] = _recentExits[i];
                _recentExitIds[write] = _recentExitIds[i];
                write++;
            }
            _recentExitCount = write;
        }

        private PungentTriggerOverlapSnapshot BuildOverlap(Collider other, bool recentExit)
        {
            Bounds bounds = other != null ? other.bounds : new Bounds(transform.position, Vector3.one);
            return new PungentTriggerOverlapSnapshot
            {
                position = bounds.center,
                bounds = bounds,
                objectName = other != null ? other.name : string.Empty,
                gameObject = other != null ? other.gameObject : null,
                time = Time.time,
                recentExit = recentExit
            };
        }

        private Collider ResolveTrigger()
        {
            return observedTrigger != null ? observedTrigger : GetComponent<Collider>();
        }

        private string BuildWarning()
        {
            Collider collider = ResolveTrigger();
            if (collider == null)
                return "No collider is available.";
            if (!collider.isTrigger)
                return "Observed collider is not marked as trigger.";
            return string.Empty;
        }

        private void EnsureBuffers()
        {
            int capped = Mathf.Clamp(maxOverlaps, 1, 256);
            if (_overlaps == null || _overlaps.Length != capped)
            {
                _overlaps = Resize(_overlaps, capped, ref _overlapCount);
                _recentExits = Resize(_recentExits, capped, ref _recentExitCount);
                _overlapIds = ResizeIds(_overlapIds, capped);
                _recentExitIds = ResizeIds(_recentExitIds, capped);
            }
        }

        private static PungentTriggerOverlapSnapshot[] Resize(PungentTriggerOverlapSnapshot[] source, int size, ref int count)
        {
            PungentTriggerOverlapSnapshot[] next = new PungentTriggerOverlapSnapshot[size];
            if (source != null)
            {
                int copy = Mathf.Min(count, size);
                for (int i = 0; i < copy; i++)
                    next[i] = source[i];
                count = copy;
            }
            return next;
        }

        private static int[] ResizeIds(int[] source, int size)
        {
            int[] next = new int[size];
            if (source != null)
            {
                int copy = Mathf.Min(source.Length, size);
                for (int i = 0; i < copy; i++)
                    next[i] = source[i];
            }
            return next;
        }

        private static int FindId(int[] ids, int count, int id)
        {
            if (ids == null)
                return -1;

            for (int i = 0; i < count; i++)
            {
                if (ids[i] == id)
                    return i;
            }
            return -1;
        }

        private static int FindOldest(PungentTriggerOverlapSnapshot[] snapshots, int count)
        {
            int oldest = 0;
            float oldestTime = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (snapshots[i].time < oldestTime)
                {
                    oldest = i;
                    oldestTime = snapshots[i].time;
                }
            }
            return oldest;
        }
    }
}
