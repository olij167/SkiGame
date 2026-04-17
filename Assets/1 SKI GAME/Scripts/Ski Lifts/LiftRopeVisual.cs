using UnityEngine;

/// <summary>
/// Purely visual rope that follows the LiftLine's analytic band exactly,
/// using a LineRenderer instead of RopeToolkit. This should visually match
/// the cyan gizmo path drawn by LiftLine at edit time and runtime.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(LiftLine))]
public class LiftRopeVisual : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Lift line whose analytic band path we should follow.")]
    public LiftLine line;

    [Tooltip("LineRenderer used to draw the rope. If null, one will be created as a child.")]
    public LineRenderer lineRenderer;

    [Header("Visual Settings")]
    [Tooltip("Approximate max distance between visual rope points in world units.")]
    public float maxSegmentLength = 2f;

    [Tooltip("Treat the path as a closed loop.")]
    public bool loop = true;

    [Tooltip("Base width of the rope.")]
    public float ropeWidth = 0.05f;

    [Tooltip("Material to use for the rope line.")]
    public Material ropeMaterial;

    [Header("Texture Scroll (optional)")]
    [Tooltip("Tiling of the rope texture along its length.")]
    public float textureTiling = 1f;

    [Tooltip("Scroll speed of the rope texture along the band (for motion illusion).")]
    public float textureScrollSpeed = 0.3f;

    [Header("Editor Rebuild (Performance)")]
    [SerializeField, Range(0.02f, 1f)]
    private float editorRebuildIntervalSeconds = 0.2f;

#if UNITY_EDITOR
    private double _nextEditorRebuildTime;
    private int _lastEditorHash;
    private bool _editorForceRebuild;
#endif

    private bool _configDirty = true;

    private bool _lastLoop;
    private float _lastRopeWidth;
    private Material _lastRopeMaterial;
    // Cached config to avoid dirtying the inspector every frame
    private bool _lrConfigInitialized;

    private static readonly int MainTexST = Shader.PropertyToID("_MainTex_ST");
    private static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");

    private MaterialPropertyBlock _mpb; // created in Awake/OnEnable (ExecuteAlways-safe)
    private int _stPropertyId = -1;     // resolved to _BaseMap_ST or _MainTex_ST
    private float _tilingX = 1f;        // cached tiling so Update can preserve it
    private float _texOffsetX = 0f;     // cached offset

    private static readonly AnimationCurve ConstantWidthCurve = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(1f, 1f)
    );

    private void EnsureMPB()
    {
        if (_mpb == null)
            _mpb = new MaterialPropertyBlock();
    }

    private void Awake()
    {
        EnsureMPB();
    }

    private void Reset()
    {
        // Try to auto-wire references when the component is first added
        line = GetComponent<LiftLine>();
        lineRenderer = GetComponentInChildren<LineRenderer>();
    }

    private void OnEnable()
    {
        EnsureSetup();
        EnsureMPB();

        _configDirty = true;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            _editorForceRebuild = true;
            return;
        }
#endif

        ApplyLineRendererConfigIfNeeded(force: true);
        RebuildRopeFromLiftLine();
    }

    private void OnValidate()
    {
        EnsureSetup();
        _configDirty = true;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            _editorForceRebuild = true;
            return;
        }
#endif

        ApplyLineRendererConfigIfNeeded(force: true);
        RebuildRopeFromLiftLine();
    }

    private void Update()
    {
        if (!EnsureSetup())
            return;

        if (!Application.isPlaying && !runInEditMode) return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            double now = UnityEditor.EditorApplication.timeSinceStartup;
            if (!_editorForceRebuild && now < _nextEditorRebuildTime)
                return;

            int hash = ComputeEditorHash();
            if (_editorForceRebuild || hash != _lastEditorHash)
            {
                _lastEditorHash = hash;
                _editorForceRebuild = false;
                _nextEditorRebuildTime = now + editorRebuildIntervalSeconds;

                ApplyLineRendererConfigIfNeeded(force: true);
                RebuildRopeFromLiftLine();
            }
            return;
        }
#endif

        // Play mode: only apply config when something changed (not every frame).
        if (_configDirty)
            ApplyLineRendererConfigIfNeeded(force: false);

        // Texture scrolling only (keep this lightweight)
        if (lineRenderer != null && Mathf.Abs(textureScrollSpeed) > 0.0001f)
        {
            if (textureScrollSpeed != 0f && lineRenderer != null && lineRenderer.sharedMaterial != null)
            {
                _texOffsetX += textureScrollSpeed * Time.deltaTime;
                ApplyST(lineRenderer, Mathf.Max(0.0001f, _tilingX), _texOffsetX);
            }

        }
    }

    private bool EnsureSetup()
    {
        // --- LiftLine ---
        if (line == null)
            line = GetComponent<LiftLine>();

        if (line == null)
        {
            Debug.LogError($"[{nameof(LiftRopeVisual)}] No LiftLine found on this GameObject. Disabling.", this);
            enabled = false;
            return false;
        }

        // --- LineRenderer ---
        if (lineRenderer == null)
        {
            lineRenderer = GetComponentInChildren<LineRenderer>();

            if (lineRenderer == null)
            {
                GameObject lrGO = new GameObject("RopeLineRenderer");
                lrGO.transform.SetParent(transform, worldPositionStays: false);
                lrGO.transform.localPosition = Vector3.zero;
                lrGO.transform.localRotation = Quaternion.identity;
                lrGO.transform.localScale = Vector3.one;
                lrGO.layer = gameObject.layer;

                lineRenderer = lrGO.AddComponent<LineRenderer>();
            }
        }

        if (lineRenderer == null)
            return false;

        // Only apply config when it actually changed
        if (!_lrConfigInitialized ||
    _lastLoop != loop ||
    !Mathf.Approximately(_lastRopeWidth, ropeWidth) ||
    _lastRopeMaterial != ropeMaterial)
        {
            lineRenderer.enabled = true;
            lineRenderer.useWorldSpace = true;
            lineRenderer.loop = loop;
            lineRenderer.widthMultiplier = Mathf.Max(0.0001f, ropeWidth);

            if (ropeMaterial != null && lineRenderer.sharedMaterial != ropeMaterial)
                lineRenderer.sharedMaterial = ropeMaterial;

            if (lineRenderer.widthCurve == null || lineRenderer.widthCurve.length != 2)
                lineRenderer.widthCurve = ConstantWidthCurve;

            _lastLoop = loop;
            _lastRopeWidth = ropeWidth;
            _lastRopeMaterial = ropeMaterial;
            _lrConfigInitialized = true;
        }

        return true;
    }

    /// <summary>
    /// Rebuilds the LineRenderer positions so it exactly follows the LiftLine's analytic band.
    /// </summary>
    public void RebuildRopeFromLiftLine()
    {
        if (!EnsureSetup())
            return;

        if (!IsValidSceneInstance())
            return;

        if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
            return;

        // Ensure LiftLine's analytic path is up to date
        line.RebuildNow(refreshRopeVisuals: false);
        float totalLength = line.BandLength;

        if (totalLength <= 0f)
        {
            Debug.LogWarning($"[{nameof(LiftRopeVisual)}] LiftLine band length is zero. Check station & collider setup.", this);
            lineRenderer.positionCount = 0;
            return;
        }

        float maxSeg = Mathf.Max(0.25f, maxSegmentLength);
        int segmentCount = Mathf.Max(4, Mathf.CeilToInt(totalLength / maxSeg));

        // We add one extra point at the end equal to the first point if looping,
        // to make sure the loop closes cleanly.
        int positionCount = loop ? segmentCount + 1 : segmentCount;
        lineRenderer.positionCount = positionCount;

        // Sample positions along the analytic band
        for (int i = 0; i < segmentCount; i++)
        {
            float t = (float)i / segmentCount;
            float distance = t * totalLength;
            Vector3 worldPos = line.GetBandPosition(distance);
            lineRenderer.SetPosition(i, worldPos);
        }

        if (loop)
        {
            // Close loop: last position == first
            Vector3 first = lineRenderer.GetPosition(0);
            lineRenderer.SetPosition(positionCount - 1, first);
        }

        if (lineRenderer != null && lineRenderer.sharedMaterial != null && textureTiling > 0f)
        {
            _tilingX = totalLength * textureTiling;
            ApplyST(lineRenderer, _tilingX, _texOffsetX);
        }

    }

    private int ComputeEditorHash()
    {
        unchecked
        {
            int h = 17;

            h = h * 31 + (line ? line.GetInstanceID() : 0);
            h = h * 31 + (lineRenderer ? lineRenderer.GetInstanceID() : 0);

            // RopeVisual parameters that affect output
            h = h * 31 + loop.GetHashCode();
            h = h * 31 + Mathf.RoundToInt(maxSegmentLength * 1000f);
            h = h * 31 + Mathf.RoundToInt(ropeWidth * 1000f);
            h = h * 31 + Mathf.RoundToInt(textureTiling * 1000f);
            h = h * 31 + (ropeMaterial ? ropeMaterial.GetInstanceID() : 0);

            if (line != null)
            {
                // Station positions affect the analytic loop
                var a = line.bottomStation ? line.bottomStation.position : Vector3.zero;
                var b = line.topStation ? line.topStation.position : Vector3.zero;
                h = h * 31 + a.GetHashCode();
                h = h * 31 + b.GetHashCode();

                // LiftLine fields that affect analytic loop geometry
                h = h * 31 + Mathf.RoundToInt(line.horizontalSeparation * 1000f);
                h = h * 31 + Mathf.RoundToInt(line.verticalOffset * 1000f);
                h = h * 31 + Mathf.RoundToInt(line.ropeClearance * 1000f);
                h = h * 31 + Mathf.RoundToInt(line.sideMaxSegmentLength * 1000f);
                h = h * 31 + Mathf.RoundToInt(line.sideSagFraction * 1000f);
                h = h * 31 + line.arcSamplesPerStation;

                // Terrain clearance affects generated points (raycasts)
                h = h * 31 + (line.terrainClearanceEnabled ? 1 : 0);
                h = h * 31 + (line.terrainClearanceInEditMode ? 1 : 0);
                h = h * 31 + line.terrainLayers.value;
                h = h * 31 + Mathf.RoundToInt(line.terrainClearanceHeight * 1000f);
                h = h * 31 + Mathf.RoundToInt(line.terrainRaycastStartHeight * 1000f);
                h = h * 31 + Mathf.RoundToInt(line.terrainRaycastMaxDistance * 1000f);
            }

            return h;
        }
    }

    private void ApplyLineRendererConfigIfNeeded(bool force)
    {
        if (lineRenderer == null) return;

        if (!force &&
            _lastLoop == loop &&
            Mathf.Approximately(_lastRopeWidth, ropeWidth) &&
            _lastRopeMaterial == ropeMaterial)
        {
            return;
        }

        lineRenderer.enabled = true;
        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = loop;
        lineRenderer.widthMultiplier = Mathf.Max(0.0001f, ropeWidth);

        if (ropeMaterial != null && lineRenderer.sharedMaterial != ropeMaterial)
            lineRenderer.sharedMaterial = ropeMaterial;

        // Do NOT allocate a new curve every frame.
        // Assign once (or reassign if someone changed it externally).
        if (lineRenderer.widthCurve == null || lineRenderer.widthCurve.length != 2)
            lineRenderer.widthCurve = ConstantWidthCurve;

        _lastLoop = loop;
        _lastRopeWidth = ropeWidth;
        _lastRopeMaterial = ropeMaterial;
        _configDirty = false;
    }

    private bool IsValidSceneInstance()
    {
        // Prefab assets and some editor contexts will have an invalid scene
        return gameObject.scene.IsValid() && gameObject.scene.isLoaded;
    }

    private int ResolveSTPropertyId(Renderer r)
    {
        if (r == null) return -1;

        var mat = r.sharedMaterial;
        if (mat == null) return -1;

        // Prefer URP name if present, otherwise fall back to MainTex
        if (mat.HasProperty(BaseMapST)) return BaseMapST;
        if (mat.HasProperty(MainTexST)) return MainTexST;

        return -1;
    }

    private void ApplyST(Renderer r, float scaleX, float offsetX)
    {
        if (r == null) return;

        // In ExecuteAlways, Awake/OnEnable should run, but be defensive.
        EnsureMPB();
        if (_mpb == null) return;

        if (_stPropertyId < 0)
            _stPropertyId = ResolveSTPropertyId(r);

        if (_stPropertyId < 0)
            return;

        r.GetPropertyBlock(_mpb);

        // (scaleX, scaleY, offsetX, offsetY)
        _mpb.SetVector(_stPropertyId, new Vector4(scaleX, 1f, offsetX, 0f));

        r.SetPropertyBlock(_mpb);
    }

}
