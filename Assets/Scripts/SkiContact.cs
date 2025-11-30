using UnityEngine;

/// <summary>
/// Per-ski helper used by SkiController to reason about each ski's direction
/// and stance. This does NOT have its own Rigidbody; it just lives on the
/// left/right ski transforms.
/// </summary>
[DisallowMultipleComponent]
public class SkiContact : MonoBehaviour
{
    [Tooltip("True if this is the left ski; false for right. Only for clarity / debugging.")]
    public bool isLeftSki = true;

    [SerializeField, Range(0f, 1f)]
    private float stanceOut;

    /// <summary>
    /// Current normalized stance for this ski (0 = under body, 1 = fully out).
    /// Set by SkiController every frame.
    /// </summary>
    public float StanceOut
    {
        get => stanceOut;
        set => stanceOut = Mathf.Clamp01(value);
    }

    /// <summary>
    /// Returns this ski's forward direction projected onto the given ground plane.
    /// Falls back to the parent transform's forward if this transform's forward
    /// is degenerate relative to the plane.
    /// </summary>
    public Vector3 GetForwardOnPlane(Vector3 groundNormal)
    {
        Vector3 f = transform.forward;
        f = Vector3.ProjectOnPlane(f, groundNormal);

        if (f.sqrMagnitude < 0.0001f)
        {
            Transform parent = transform.parent;
            if (parent != null)
            {
                f = Vector3.ProjectOnPlane(parent.forward, groundNormal);
            }
            else
            {
                f = Vector3.ProjectOnPlane(Vector3.forward, groundNormal);
            }
        }

        return f.normalized;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Simple debug gizmo: show ski forward on assumed up-plane for quick checks.
        Gizmos.color = isLeftSki ? Color.cyan : Color.magenta;
        Vector3 up = Vector3.up;
        Vector3 f = Vector3.ProjectOnPlane(transform.forward, up).normalized;
        Gizmos.DrawRay(transform.position, f * 0.5f);
    }
#endif
}
