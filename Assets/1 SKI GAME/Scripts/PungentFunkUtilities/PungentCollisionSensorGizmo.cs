using System.Collections.Generic;
using UnityEngine;

namespace PungentFunk.Utilities.SceneTools
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Utilities/Scene Tools/Pungent Collision Sensor Gizmo")]
    public sealed class PungentCollisionSensorGizmo : MonoBehaviour, IPungentCollisionSensorProvider, IPungentSceneGizmoPerformanceProvider
    {
        public bool drawInScene = true;
        public bool drawWhenSelectedOnly = true;
        public bool drawColliderBounds = true;
        public bool drawContactPoints = true;
        public bool drawContactNormals = true;
        public bool drawContactLabels = false;
        public bool keepRecentContacts = true;
        [Min(0.05f)] public float recentContactLifetime = 1.5f;
        [Min(1)] public int maxContacts = 16;
        [Tooltip("Maximum SceneView camera distance for drawing this sensor. Zero means unlimited.")]
        [Min(0f)] public float maxDrawDistance = 0f;
        [Min(0.01f)] public float contactPointSize = 0.12f;
        [Min(0.01f)] public float normalLength = 0.7f;
        public Color idleColor = new Color(0.35f, 0.7f, 1f, 0.65f);
        public Color activeColor = new Color(1f, 0.45f, 0.2f, 0.9f);
        public Color contactColor = new Color(1f, 0.9f, 0.25f, 0.95f);
        public Color normalColor = new Color(0.4f, 1f, 0.65f, 0.95f);
        public Collider observedCollider;

        private readonly List<Collider> _activeColliders = new List<Collider>(8);
        private PungentCollisionContactSnapshot[] _contacts = new PungentCollisionContactSnapshot[16];
        private ContactPoint[] _contactBuffer = new ContactPoint[16];
        private int _contactCount;
        private string _lastCollisionObjectName = string.Empty;
        private string _lastEventType = string.Empty;
        private Vector3 _lastRelativeVelocity;

        private void OnValidate()
        {
            recentContactLifetime = Mathf.Max(0.05f, recentContactLifetime);
            maxContacts = Mathf.Clamp(maxContacts, 1, 128);
            maxDrawDistance = Mathf.Max(0f, maxDrawDistance);
            contactPointSize = Mathf.Max(0.01f, contactPointSize);
            normalLength = Mathf.Max(0.01f, normalLength);
            EnsureBuffers();
        }

        public void ClearHistory()
        {
            _activeColliders.Clear();
            _contactCount = 0;
            _lastCollisionObjectName = string.Empty;
            _lastEventType = string.Empty;
            _lastRelativeVelocity = Vector3.zero;
        }

        private void OnCollisionEnter(Collision collision)
        {
            RecordCollision(collision, "Enter");
        }

        private void OnCollisionStay(Collision collision)
        {
            RecordCollision(collision, "Stay");
        }

        private void OnCollisionExit(Collision collision)
        {
            Collider other = collision != null ? collision.collider : null;
            if (other != null)
                _activeColliders.Remove(other);

            _lastCollisionObjectName = other != null ? other.name : string.Empty;
            _lastEventType = "Exit";
        }

        public bool TryGetCollisionSensorSnapshot(out PungentCollisionSensorSnapshot snapshot)
        {
            EnsureBuffers();
            ExpireContacts();
            Collider collider = ResolveCollider();
            snapshot = new PungentCollisionSensorSnapshot
            {
                owner = this,
                observedCollider = collider,
                drawInScene = drawInScene,
                drawWhenSelectedOnly = drawWhenSelectedOnly,
                drawColliderBounds = drawColliderBounds,
                drawContactPoints = drawContactPoints,
                drawContactNormals = drawContactNormals,
                drawContactLabels = drawContactLabels,
                hasCollider = collider != null,
                colliderBounds = collider != null ? collider.bounds : new Bounds(transform.position, Vector3.one),
                maxDrawDistance = Mathf.Max(0f, maxDrawDistance),
                currentCollisionCount = CountLiveColliders(),
                contactCount = _contactCount,
                maxContacts = Mathf.Max(1, maxContacts),
                contactPointSize = Mathf.Max(0.01f, contactPointSize),
                normalLength = Mathf.Max(0.01f, normalLength),
                idleColor = idleColor,
                activeColor = activeColor,
                contactColor = contactColor,
                normalColor = normalColor,
                lastCollisionObjectName = _lastCollisionObjectName,
                lastEventType = _lastEventType,
                warning = collider == null ? "Collision sensor has no collider." : string.Empty,
                contacts = _contacts
            };

            return drawInScene && isActiveAndEnabled;
        }

        public bool TryGetPerformanceEstimate(out PungentSceneGizmoPerformanceEstimate estimate)
        {
            int liveContacts = Mathf.Min(_contactCount, Mathf.Max(1, maxContacts));
            estimate = new PungentSceneGizmoPerformanceEstimate
            {
                owner = this,
                active = isActiveAndEnabled,
                drawInScene = drawInScene,
                selectedOnly = drawWhenSelectedOnly,
                hasLabels = drawContactLabels,
                alwaysVisible = !drawWhenSelectedOnly,
                estimatedDrawOperations = drawInScene && isActiveAndEnabled ? ((drawColliderBounds ? 1 : 0) + liveContacts * ((drawContactPoints ? 1 : 0) + (drawContactNormals ? 1 : 0) + (drawContactLabels ? 1 : 0))) : 0,
                estimatedLabels = drawInScene && isActiveAndEnabled && drawContactLabels ? liveContacts : 0,
                estimatedTrajectorySamples = 0,
                priority = CountLiveColliders() > 0 ? 20 : 0,
                providerCategory = "Collision Sensor",
                warning = ResolveCollider() == null ? "No collider is available." : string.Empty
            };
            return true;
        }

        private void RecordCollision(Collision collision, string eventType)
        {
            if (collision == null)
                return;

            EnsureBuffers();
            Collider other = collision.collider;
            if (other != null && !_activeColliders.Contains(other))
                _activeColliders.Add(other);

            _lastCollisionObjectName = other != null ? other.name : string.Empty;
            _lastEventType = eventType;
            _lastRelativeVelocity = collision.relativeVelocity;

            int count = collision.GetContacts(_contactBuffer);
            for (int i = 0; i < count; i++)
                AddContact(_contactBuffer[i], other, collision.relativeVelocity);
        }

        private void AddContact(ContactPoint point, Collider other, Vector3 relativeVelocity)
        {
            EnsureBuffers();
            int index = _contactCount < _contacts.Length ? _contactCount++ : FindOldestContactIndex();
            _contacts[index] = new PungentCollisionContactSnapshot
            {
                point = point.point,
                normal = point.normal,
                relativeVelocity = relativeVelocity,
                otherObjectName = other != null ? other.name : string.Empty,
                otherObject = other != null ? other.gameObject : null,
                time = Time.time,
                recent = true
            };
        }

        private void ExpireContacts()
        {
            if (!keepRecentContacts)
            {
                _contactCount = 0;
                return;
            }

            float now = Time.time;
            float lifetime = Mathf.Max(0.05f, recentContactLifetime);
            int write = 0;
            for (int i = 0; i < _contactCount; i++)
            {
                PungentCollisionContactSnapshot contact = _contacts[i];
                if (now - contact.time > lifetime)
                    continue;

                contact.recent = true;
                _contacts[write++] = contact;
            }
            _contactCount = write;
        }

        private int CountLiveColliders()
        {
            for (int i = _activeColliders.Count - 1; i >= 0; i--)
            {
                if (_activeColliders[i] == null)
                    _activeColliders.RemoveAt(i);
            }
            return _activeColliders.Count;
        }

        private int FindOldestContactIndex()
        {
            int oldest = 0;
            float oldestTime = float.MaxValue;
            for (int i = 0; i < _contacts.Length; i++)
            {
                if (_contacts[i].time < oldestTime)
                {
                    oldest = i;
                    oldestTime = _contacts[i].time;
                }
            }
            return oldest;
        }

        private Collider ResolveCollider()
        {
            return observedCollider != null ? observedCollider : GetComponent<Collider>();
        }

        private void EnsureBuffers()
        {
            int capped = Mathf.Clamp(maxContacts, 1, 128);
            if (_contacts == null || _contacts.Length != capped)
            {
                PungentCollisionContactSnapshot[] next = new PungentCollisionContactSnapshot[capped];
                if (_contacts != null)
                {
                    int copy = Mathf.Min(_contactCount, next.Length);
                    for (int i = 0; i < copy; i++)
                        next[i] = _contacts[i];
                    _contactCount = copy;
                }
                _contacts = next;
            }

            if (_contactBuffer == null || _contactBuffer.Length != capped)
                _contactBuffer = new ContactPoint[capped];
        }
    }
}
