using System;
using UnityEngine;

namespace SkiGame.Activities
{
    public enum MountainActivityKind
    {
        None = 0,
        Race = 10,
        Rescue = 20,
    }

    [DisallowMultipleComponent]
    public sealed class MountainActivityManager : MonoBehaviour
    {
        public static MountainActivityManager Instance { get; private set; }

        public event Action<MountainActivityKind, MonoBehaviour, string, int> OnActivityStarted;
        public event Action<MountainActivityKind, MonoBehaviour, string> OnActivityCompleted;
        public event Action<MountainActivityKind, MonoBehaviour, string, string> OnActivityFailed;
        public event Action<MountainActivityKind, MonoBehaviour, string> OnActivityCancelled;

        [SerializeField] private bool allowOnlyOneActiveActivity = true;

        [NonSerialized] private MountainActivityKind _activeKind = MountainActivityKind.None;
        [NonSerialized] private MonoBehaviour _activeSource;
        [NonSerialized] private string _activeDisplayName;
        [NonSerialized] private int _activeVariantNumber;

        public MountainActivityKind ActiveKind => _activeKind;
        public MonoBehaviour ActiveSource => _activeSource;
        public string ActiveDisplayName => _activeDisplayName;
        public int ActiveVariantNumber => _activeVariantNumber;
        public bool HasActiveActivity => _activeKind != MountainActivityKind.None && _activeSource != null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public bool CanStart(MountainActivityKind kind, MonoBehaviour source)
        {
            if (kind == MountainActivityKind.None)
                return false;

            if (source == null)
                return false;

            if (!allowOnlyOneActiveActivity)
                return true;

            return !HasActiveActivity;
        }

        public bool TryStart(MountainActivityKind kind, MonoBehaviour source, string displayName, int variantNumber = 1)
        {
            if (!CanStart(kind, source))
                return false;

            _activeKind = kind;
            _activeSource = source;
            _activeDisplayName = string.IsNullOrWhiteSpace(displayName) ? kind.ToString() : displayName.Trim();
            _activeVariantNumber = Mathf.Max(1, variantNumber);

            OnActivityStarted?.Invoke(_activeKind, _activeSource, _activeDisplayName, _activeVariantNumber);
            return true;
        }

        public bool IsActive(MountainActivityKind kind, MonoBehaviour source)
        {
            return _activeKind == kind && _activeSource == source && _activeSource != null;
        }

        public void Complete(MountainActivityKind kind, MonoBehaviour source, string result = null)
        {
            if (!IsActive(kind, source))
                return;

            var kindCopy = _activeKind;
            var sourceCopy = _activeSource;
            var nameCopy = _activeDisplayName;

            ClearActive();
            OnActivityCompleted?.Invoke(kindCopy, sourceCopy, nameCopy);
        }

        public void Fail(MountainActivityKind kind, MonoBehaviour source, string reason)
        {
            if (!IsActive(kind, source))
                return;

            var kindCopy = _activeKind;
            var sourceCopy = _activeSource;
            var nameCopy = _activeDisplayName;

            ClearActive();
            OnActivityFailed?.Invoke(kindCopy, sourceCopy, nameCopy, reason ?? string.Empty);
        }

        public void Cancel(MountainActivityKind kind, MonoBehaviour source)
        {
            if (!IsActive(kind, source))
                return;

            var kindCopy = _activeKind;
            var sourceCopy = _activeSource;
            var nameCopy = _activeDisplayName;

            ClearActive();
            OnActivityCancelled?.Invoke(kindCopy, sourceCopy, nameCopy);
        }

        private void ClearActive()
        {
            _activeKind = MountainActivityKind.None;
            _activeSource = null;
            _activeDisplayName = null;
            _activeVariantNumber = 0;
        }
    }
}