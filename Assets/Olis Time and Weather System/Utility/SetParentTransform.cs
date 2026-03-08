using UnityEngine;

/// <summary>
/// Parents this object to a persistent global anchor (DontDestroyOnLoad),
/// and makes that anchor follow the most appropriate active camera.
/// This prevents cross-scene parenting (which can move objects into the menu scene and destroy them on unload).
/// </summary>
[DisallowMultipleComponent]
public class SetParentTransform : MonoBehaviour
{
    [Header("Optional Override")]
    [Tooltip("If assigned, this transform will be followed instead of auto-resolving a camera.")]
    public Transform parentTransform;

    [Header("Follow Settings")]
    [Tooltip("If true, this object will be parented to a persistent global anchor instead of directly to the camera.")]
    public bool usePersistentAnchor = true;

    [Tooltip("Re-evaluate the target camera each frame (recommended for menu->game transitions).")]
    public bool autoRefreshTarget = true;

    [Tooltip("Follow camera position.")]
    public bool followPosition = true;

    [Tooltip("Follow camera rotation.")]
    public bool followRotation = true;

    [Tooltip("If true, resets local position/rotation when parenting.")]
    public bool snapToTarget = true;

    private static Transform s_anchor;
    private Transform _target;

    private void OnEnable()
    {
        RefreshTarget(force: true);
        ApplyParenting(force: true);
    }

    private void Start()
    {
        RefreshTarget(force: true);
        ApplyParenting(force: true);
    }

    private void LateUpdate()
    {
        if (autoRefreshTarget)
            RefreshTarget(force: false);

        if (_target == null)
            return;

        // Anchor follows target; this object can be a child of anchor.
        if (usePersistentAnchor)
        {
            EnsureAnchor();

            if (followPosition)
                s_anchor.position = _target.position;
            if (followRotation)
                s_anchor.rotation = _target.rotation;
        }
        else
        {
            // Direct-follow mode (no parenting)
            if (followPosition)
                transform.position = _target.position;
            if (followRotation)
                transform.rotation = _target.rotation;
        }
    }

    /// <summary>Manual call if something external changes which camera should be followed.</summary>
    public void SetParent(Transform newParent)
    {
        parentTransform = newParent;
        RefreshTarget(force: true);
        ApplyParenting(force: true);
    }

    private void RefreshTarget(bool force)
    {
        var desired = parentTransform != null ? parentTransform : ResolveBestCameraTransform();
        if (!force && desired == _target) return;

        _target = desired;
    }

    private void ApplyParenting(bool force)
    {
        if (_target == null) return;

        if (usePersistentAnchor)
        {
            EnsureAnchor();

            // Parent to persistent anchor (prevents cross-scene parenting)
            if (force || transform.parent != s_anchor)
                transform.SetParent(s_anchor, worldPositionStays: !snapToTarget);

            if (snapToTarget)
            {
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }

            // Immediately place anchor on the current target
            if (followPosition)
                s_anchor.position = _target.position;
            if (followRotation)
                s_anchor.rotation = _target.rotation;
        }
        else
        {
            // No parenting; direct-follow in LateUpdate.
            if (force || transform.parent != null)
                transform.SetParent(null, worldPositionStays: true);
        }
    }

    private static void EnsureAnchor()
    {
        if (s_anchor != null) return;

        var go = GameObject.Find("__WeatherFollowAnchor");
        if (go == null)
            go = new GameObject("__WeatherFollowAnchor");

        DontDestroyOnLoad(go);
        s_anchor = go.transform;
    }

    /// <summary>
    /// Finds the most appropriate camera to follow:
    /// 1) enabled + activeInHierarchy camera tagged MainCamera
    /// 2) any enabled + activeInHierarchy camera
    /// 3) any camera
    /// </summary>
    private static Transform ResolveBestCameraTransform()
    {
        Camera best = null;

        var cams = Camera.allCameras;
        if (cams != null && cams.Length > 0)
        {
            // Prefer an enabled MainCamera
            for (int i = 0; i < cams.Length; i++)
            {
                var c = cams[i];
                if (c == null) continue;
                if (!c.enabled) continue;
                if (!c.gameObject.activeInHierarchy) continue;
                if (c.CompareTag("MainCamera"))
                    return c.transform;
            }

            // Otherwise any enabled camera
            for (int i = 0; i < cams.Length; i++)
            {
                var c = cams[i];
                if (c == null) continue;
                if (!c.enabled) continue;
                if (!c.gameObject.activeInHierarchy) continue;
                best = c;
                break;
            }

            if (best != null)
                return best.transform;

            // Fallback: any camera at all
            for (int i = 0; i < cams.Length; i++)
            {
                var c = cams[i];
                if (c == null) continue;
                return c.transform;
            }
        }

        // As a last resort, Camera.main (may be disabled, but gives something)
        return Camera.main != null ? Camera.main.transform : null;
    }
}
