using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public class SorenessBruiseDecals : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private SorenessMeter sorenessMeter;

    [Tooltip("Optional but recommended: used to ignore ski-collider contacts & reuse ground layer filtering.")]
    [SerializeField] private SkiController skiController;

    [Tooltip("DecalProjector prefab with a bruise material. Must include a DecalProjector component.")]
    [SerializeField] private DecalProjector decalPrefab;

    [Header("Target surfaces (Player Only)")]
    [Tooltip("Only these renderers will receive bruise decals (assign your Head + Body mesh renderers).")]
    [SerializeField] private Renderer[] bruiseReceivers;

    [Tooltip("Optional: if assigned, bruises will only spawn from collisions involving these colliders (Head + Body colliders).")]
    [SerializeField] private Collider[] bruiseColliders;

    [Tooltip("Optional: renderers that must NOT receive bruises (terrain, props). If assigned, the bruise rendering layer bit will be removed from them at runtime.")]
    [SerializeField] private Renderer[] forceExcludeReceivers;

    [Tooltip("Rendering Layer index for bruises (0..31). Example: 7 means mask = 1<<7 (128).")]
    [SerializeField, Range(0, 31)] private int bruiseRenderingLayerIndex = 7;

    private uint BruiseMask => 1u << bruiseRenderingLayerIndex;


    [Tooltip("If true, this script will OR 'bruiseRenderingLayerMask' into each receiver renderer's Rendering Layer Mask at runtime.")]
    [SerializeField] private bool autoApplyRenderingLayerToReceivers = true;

    [Tooltip("If true, this script will REMOVE the bruise rendering-layer bit from every other Renderer under this character (including skis), ensuring decals never project onto them.")]
    [SerializeField] private bool autoStripBruiseLayerFromOtherChildRenderers = true;

    [Tooltip("Include inactive child renderers when stripping the bruise rendering-layer bit.")]
    [SerializeField] private bool stripInactiveChildRenderers = true;

    [Header("Projection volume shaping")]
    [Tooltip("Fixed projection depth (meters). Keep small to avoid hitting nearby surfaces.")]
    [SerializeField] private float projectionDepth = 0.08f;

    [Tooltip("How much of the projector depth is allowed to extend OUTSIDE the player's surface (meters). Smaller = less chance of projecting onto terrain/props.")]
    [SerializeField] private float outwardSurfacePadding = 0.004f;

    [Tooltip("How far to push the decal projector inward (toward the target collider center) so it projects onto the player, not the environment.")]
    [SerializeField] private float inwardOffset = 0.025f;

    [Header("Bruise Receivers (Head/Body Only)")]
    [Tooltip("Assign the player's HEAD root transform. Bruises will only spawn on colliders under this transform.")]
    [SerializeField] private Transform headRoot;

    [Tooltip("Assign the player's BODY root transform. Bruises will only spawn on colliders under this transform.")]
    [SerializeField] private Transform bodyRoot;

    [Tooltip("If true, bruises will NOT spawn unless the impact contact is on a collider under HeadRoot or BodyRoot.")]
    [SerializeField] private bool requireHeadOrBody = true;

    private readonly HashSet<Collider> _allowedBruiseColliders = new HashSet<Collider>(64);

    [Header("Impact filtering")]
    [Tooltip("If SkiController is not provided, use this layer mask to decide what counts as an impact.")]
    [SerializeField] private LayerMask impactLayers = ~0;

    [Tooltip("Ignore impacts below this relative speed (m/s).")]
    [SerializeField] private float minImpactSpeed = 3f;

    [Tooltip("Relative speed (m/s) treated as full severity.")]
    [SerializeField] private float maxImpactSpeed = 12f;

    [Tooltip("Shape the mapped severity (x:0..1 => y:0..1).")]
    [SerializeField]
    private AnimationCurve severityBySpeed = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(1f, 1f)
    );

    [Header("Decal appearance")]
    [Tooltip("World-space size range for the decal projector.")]
    [SerializeField] private float minDecalSize = 0.12f;

    [SerializeField] private float maxDecalSize = 0.30f;

    [Tooltip("How far to push the projector away from the contact point along the surface normal.")]
    [SerializeField] private float normalOffset = 0.015f;

    [Tooltip("Euler offset applied after aligning to the contact normal (use if your decal projects on a different axis).")]
    [SerializeField] private Vector3 projectorEulerOffset = Vector3.zero;

    [Tooltip("Maximum projector fadeFactor at full severity (0..1).")]
    [SerializeField, Range(0f, 1f)] private float maxFadeFactor = 1f;

    [Header("Pooling")]
    [SerializeField] private int poolSize = 24;

    private readonly List<DecalInstance> _pool = new List<DecalInstance>();
    private int _poolCursor;

    private readonly HashSet<Collider> _allowedColliders = new HashSet<Collider>();

    private struct DecalInstance
    {
        public DecalProjector projector;
        public float dieTime;          // no longer used for expiry (kept for minimal disruption)
        public float life;             // no longer used for expiry (kept for minimal disruption)
        public float baseFadeFactor;
        public float spawnSoreness01;  // soreness level when the bruise was created
    }

    private void Awake()
    {
        if (skiController == null)
            skiController = GetComponent<SkiController>();

        // Build allowed collider set (Head + Body only) if provided.
        _allowedColliders.Clear();
        if (bruiseColliders != null)
        {
            for (int i = 0; i < bruiseColliders.Length; i++)
            {
                var c = bruiseColliders[i];
                if (c != null) _allowedColliders.Add(c);
            }
        }

        BuildAllowedBruiseColliders();
        ApplyRenderingLayerMasks();


        if (decalPrefab == null)
            return;

        _pool.Clear();
        _pool.Capacity = Mathf.Max(1, poolSize);

        for (int i = 0; i < poolSize; i++)
        {
            DecalProjector p = Instantiate(decalPrefab, transform);
            p.gameObject.SetActive(false);

            _pool.Add(new DecalInstance
            {
                projector = p,
                dieTime = 0f,
                life = 0f,
                baseFadeFactor = 0f,
                spawnSoreness01 = 0f
            });
        }

        _poolCursor = 0;
    }

    private void BuildAllowedBruiseColliders()
    {
        _allowedBruiseColliders.Clear();

        if (headRoot != null)
        {
            var cols = headRoot.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                Collider c = cols[i];
                if (c == null) continue;
                if (skiController != null && skiController.IsSkiCollider(c)) continue;
                _allowedBruiseColliders.Add(c);
            }
        }

        if (bodyRoot != null)
        {
            var cols = bodyRoot.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                Collider c = cols[i];
                if (c == null) continue;
                if (skiController != null && skiController.IsSkiCollider(c)) continue;
                _allowedBruiseColliders.Add(c);
            }
        }
    }

    private void ApplyRenderingLayerMasks()
    {
        // IMPORTANT: Rendering Layers only work if your URP Decal Renderer Feature has
        // "Use Rendering Layers" enabled. If it's disabled, decals will project onto everything.

        uint inv = ~BruiseMask;

        // 1) Ensure receivers include the bruise bit.
        if (autoApplyRenderingLayerToReceivers && bruiseReceivers != null)
        {
            for (int i = 0; i < bruiseReceivers.Length; i++)
            {
                var r = bruiseReceivers[i];
                if (r == null) continue;
                r.renderingLayerMask |= BruiseMask;
            }
        }

        // 2) Ensure forced excludes remove the bruise bit.
        if (forceExcludeReceivers != null)
        {
            for (int i = 0; i < forceExcludeReceivers.Length; i++)
            {
                var r = forceExcludeReceivers[i];
                if (r == null) continue;
                r.renderingLayerMask &= inv;
            }
        }

        // 3) Optionally strip the bruise bit from every other child renderer (skis, props, etc.).
        if (!autoStripBruiseLayerFromOtherChildRenderers)
            return;

        var receiversSet = new HashSet<Renderer>();
        if (bruiseReceivers != null)
        {
            for (int i = 0; i < bruiseReceivers.Length; i++)
            {
                var r = bruiseReceivers[i];
                if (r != null) receiversSet.Add(r);
            }
        }

        var all = GetComponentsInChildren<Renderer>(stripInactiveChildRenderers);
        for (int i = 0; i < all.Length; i++)
        {
            var r = all[i];
            if (r == null) continue;
            if (receiversSet.Contains(r)) continue;
            r.renderingLayerMask &= inv;
        }
    }

    private Vector3 GetStableUp(Vector3 forward)
    {
        // Avoid LookRotation instability when forward is close to world up.
        float d = Mathf.Abs(Vector3.Dot(forward, Vector3.up));
        return (d > 0.95f) ? Vector3.forward : Vector3.up;
    }

    private void OnTransformChildrenChanged()
    {
        // Handles runtime ski visual swaps / hierarchy changes that would otherwise leave masks out-of-sync.
        if (!Application.isPlaying) return;
        BuildAllowedBruiseColliders();
        ApplyRenderingLayerMasks();
    }

    private void Update()
    {
        // Bruises persist and fade out only as soreness recovers.
        float s = (sorenessMeter != null) ? Mathf.Clamp01(sorenessMeter.Soreness01) : 1f;

        for (int i = 0; i < _pool.Count; i++)
        {
            var inst = _pool[i];
            var p = inst.projector;
            if (p == null || !p.gameObject.activeSelf)
                continue;

            // If we don't know spawn soreness, keep full intensity.
            float denom = Mathf.Max(0.0001f, inst.spawnSoreness01);

            // As soreness decreases, bruise fades proportionally.
            // - at s == spawnSoreness01 -> t = 1 (full bruise)
            // - at s -> 0 -> t -> 0 (fully recovered -> bruise gone)
            float t = Mathf.Clamp01(s / denom);

            p.fadeFactor = Mathf.Clamp01(inst.baseFadeFactor * t);

            // When fully recovered (or close), disable the bruise permanently.
            if (s <= 0.01f || t <= 0.01f)
            {
                p.gameObject.SetActive(false);
                inst.spawnSoreness01 = 0f;
                _pool[i] = inst;
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (decalPrefab == null || _pool.Count == 0)
            return;

        // NOTE: We do per-contact layer filtering below using cp.otherCollider.
        // collision.gameObject.layer is unreliable for compound colliders.

        if (collision.contactCount <= 0)
            return;

        // Severity from relative speed
        float v = collision.relativeVelocity.magnitude;
        float sev01 = Mathf.InverseLerp(minImpactSpeed, maxImpactSpeed, v);
        sev01 = Mathf.Clamp01(sev01);
        sev01 = severityBySpeed != null ? Mathf.Clamp01(severityBySpeed.Evaluate(sev01)) : sev01;

        if (sev01 <= 0.0001f)
            return;

        // Choose best non-ski, head/body contact.
        ContactPoint best = collision.GetContact(0);
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint cp = collision.GetContact(i);

            // Per-contact layer filtering (compound colliders can differ from collision.gameObject.layer)
            int otherLayer = (cp.otherCollider != null) ? cp.otherCollider.gameObject.layer : collision.gameObject.layer;
            if (skiController != null)
            {
                if ((skiController.GroundLayers.value & (1 << otherLayer)) == 0)
                    continue;
            }
            else
            {
                if ((impactLayers.value & (1 << otherLayer)) == 0)
                    continue;
            }

            // Ignore ski contacts
            if (skiController != null && cp.thisCollider != null && skiController.IsSkiCollider(cp.thisCollider))
                continue;

            // Restrict bruises to head/body colliders only.
            if (requireHeadOrBody)
            {
                if (cp.thisCollider == null || _allowedBruiseColliders.Count == 0 || !_allowedBruiseColliders.Contains(cp.thisCollider))
                    continue;
            }

            // Only spawn bruises for the specified player colliders (head/body) if provided.
            if (_allowedColliders.Count > 0 && (cp.thisCollider == null || !_allowedColliders.Contains(cp.thisCollider)))
                continue;

            // Prefer contacts whose normal points most "into" the player
            Vector3 toCenter = (cp.thisCollider != null) ? (cp.thisCollider.bounds.center - cp.point) : (transform.position - cp.point);
            float intoPlayer = Vector3.Dot(cp.normal.normalized, toCenter.sqrMagnitude > 0.000001f ? toCenter.normalized : Vector3.up);

            if (intoPlayer > bestScore)
            {
                bestScore = intoPlayer;
                best = cp;
            }
        }

        if (requireHeadOrBody && bestScore == float.NegativeInfinity)
            return;

        SpawnDecal(best.point, best.normal, sev01, best.thisCollider);
    }

    private void SpawnDecal(Vector3 point, Vector3 normal, float severity01, Collider targetCollider)
    {
        // Pull a projector from the pool (overwrite oldest)
        int idx = _poolCursor;
        _poolCursor = (_poolCursor + 1) % _pool.Count;

        var inst = _pool[idx];
        var p = inst.projector;
        if (p == null)
            return;

        // Ensure decals only affect the player receivers.
        p.renderingLayerMask = BruiseMask;

        // Parent to target collider so it follows the player (optional).
        if (targetCollider != null)
            p.transform.SetParent(targetCollider.transform, true);
        else
            p.transform.SetParent(transform, true);

        // Position/orient
        Vector3 inwardDir;
        if (targetCollider != null)
        {
            Vector3 toCenter = (targetCollider.bounds.center - point);
            inwardDir = (toCenter.sqrMagnitude > 0.000001f) ? toCenter.normalized : (normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up);
        }
        else
        {
            inwardDir = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
        }

        // Find a reliable surface point ON the player collider, then position the projector mostly inside the body.
        Vector3 surfacePoint = point;
        if (targetCollider != null)
            surfacePoint = targetCollider.ClosestPoint(point + inwardDir * 0.05f);
        else
            surfacePoint = point + inwardDir * Mathf.Max(0f, normalOffset);

        float depth = Mathf.Clamp(projectionDepth, 0.01f, 1f);
        float outward = Mathf.Clamp(outwardSurfacePadding, 0f, depth * 0.49f);

        // Place the projector so only a small portion of its depth extends outside the player.
        Vector3 pos = surfacePoint + inwardDir * ((depth * 0.5f) - outward);

        // Additional inward bias
        pos += inwardDir * Mathf.Max(0f, inwardOffset);

        // URP DecalProjector projects along its -Z axis. We aim -Z into the player.
        Quaternion rot = Quaternion.LookRotation(-inwardDir, GetStableUp(inwardDir)) * Quaternion.Euler(projectorEulerOffset);
        p.transform.SetPositionAndRotation(pos, rot);

        float size = Mathf.Lerp(minDecalSize, maxDecalSize, severity01);

        // Projection volume
        p.size = new Vector3(size, size, depth);

        // intensity via fadeFactor (0..1)
        float baseFade = Mathf.Clamp01(maxFadeFactor * severity01);
        p.fadeFactor = baseFade;
        inst.baseFadeFactor = baseFade;

        // lifetime
        // Persist until soreness recovers (no time expiry).
        inst.life = 0f;
        inst.dieTime = float.PositiveInfinity;

        // Record soreness at time of bruise creation.
        // If we happen to be at 0, clamp so the ratio math is stable.
        float s0 = (sorenessMeter != null) ? Mathf.Clamp01(sorenessMeter.Soreness01) : 1f;
        inst.spawnSoreness01 = Mathf.Max(0.05f, s0);

        _pool[idx] = inst;

        if (!p.gameObject.activeSelf)
            p.gameObject.SetActive(true);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        poolSize = Mathf.Clamp(poolSize, 1, 256);
        minImpactSpeed = Mathf.Max(0f, minImpactSpeed);
        maxImpactSpeed = Mathf.Max(minImpactSpeed + 0.01f, maxImpactSpeed);
        minDecalSize = Mathf.Max(0.01f, minDecalSize);
        maxDecalSize = Mathf.Max(minDecalSize, maxDecalSize);
        projectionDepth = Mathf.Clamp(projectionDepth, 0.01f, 1f);
        outwardSurfacePadding = Mathf.Clamp(outwardSurfacePadding, 0f, projectionDepth * 0.49f);

        if (!Application.isPlaying)
            ApplyRenderingLayerMasks();

    }
#endif
}
