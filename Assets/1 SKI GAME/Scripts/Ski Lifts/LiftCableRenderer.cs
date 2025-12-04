using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Renders one or two cables for a LiftPath using LineRenderers.
/// - Primary cable follows the LiftPath directly.
/// - Optional return cable is offset sideways (up/down line).
/// </summary>
[RequireComponent(typeof(LiftPath))]
[RequireComponent(typeof(LineRenderer))]
public class LiftCableRenderer : MonoBehaviour
{
    [Header("Sampling")]
    [Tooltip("If > 0, overrides LiftPath.samplesPerSegment for rendering.")]
    public int samplesPerSegmentOverride = -1;

    [Header("Visual")]
    [Tooltip("Cable radius in metres.")]
    public float cableWidth = 0.03f;
    [Tooltip("Rebuild cable every frame (useful if stations/towers move).")]
    public bool rebuildEveryFrame = false;

    [Header("Texture Animation")]
    [Tooltip("Scroll the cable texture to fake cable motion.")]
    public bool scrollTexture = true;
    public float textureScrollSpeed = 0.1f;

    [Header("Return / Parallel Cable")]
    [Tooltip("If enabled, a second cable is drawn parallel to the main one.")]
    public bool enableReturnCable = true;
    [Tooltip("Lateral offset (metres) from the main cable. Positive is to the right of travel.")]
    public float returnCableOffset = 2f;
    [Tooltip("Optional explicit LineRenderer for the return cable. If left empty, one is created as a child.")]
    public LineRenderer returnLineRenderer;

    private LiftPath path;
    private LineRenderer mainLineRenderer;
    private Material cableMaterialInstance;
    private float textureOffset;
    private readonly List<Vector3> mainPositions = new List<Vector3>();
    private readonly List<Vector3> returnPositions = new List<Vector3>();

    private void Reset()
    {
        path = GetComponent<LiftPath>();
        mainLineRenderer = GetComponent<LineRenderer>();

        if (!mainLineRenderer)
        {
            mainLineRenderer = gameObject.AddComponent<LineRenderer>();
        }

        ConfigureLineRendererDefaults(mainLineRenderer);
    }

    private void Awake()
    {
        if (!path) path = GetComponent<LiftPath>();
        if (!mainLineRenderer) mainLineRenderer = GetComponent<LineRenderer>();

        ConfigureLineRendererDefaults(mainLineRenderer);

        // Cache a material instance so we don't keep touching LineRenderer.material
        if (mainLineRenderer != null && mainLineRenderer.material != null)
        {
            cableMaterialInstance = mainLineRenderer.material;
        }

        if (enableReturnCable)
        {
            EnsureReturnLineRenderer();
        }

        RebuildCable();
    }

    private void ConfigureLineRendererDefaults(LineRenderer lr)
    {
        if (!lr) return;

        lr.useWorldSpace = true;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        lr.receiveShadows = true;

        // Width
        var widthCurve = new AnimationCurve();
        widthCurve.AddKey(0f, cableWidth);
        widthCurve.AddKey(1f, cableWidth);
        lr.widthCurve = widthCurve;

        lr.numCapVertices = 4;
        lr.numCornerVertices = 4;
    }

    private void LateUpdate()
    {
        if (rebuildEveryFrame)
        {
            RebuildCable();
        }

        if (scrollTexture && cableMaterialInstance != null)
        {
            textureOffset += textureScrollSpeed * Time.deltaTime;
            // Scroll along cable length (U axis)
            cableMaterialInstance.mainTextureOffset = new Vector2(textureOffset, 0f);

            if (returnLineRenderer != null && returnLineRenderer.material != null)
            {
                // Keep return cable texture in sync
                returnLineRenderer.material.mainTextureOffset = new Vector2(textureOffset, 0f);
            }
        }
    }

    /// <summary>
    /// Rebuilds the line renderer positions based on the LiftPath.
    /// Call this if you move stations/towers at runtime.
    /// </summary>
    public void RebuildCable()
    {
        if (path == null || mainLineRenderer == null)
            return;

        mainPositions.Clear();
        path.GetSampledPositions(mainPositions, samplesPerSegmentOverride, 0f);

        if (mainPositions.Count == 0)
        {
            mainLineRenderer.positionCount = 0;
        }
        else
        {
            mainLineRenderer.positionCount = mainPositions.Count;
            mainLineRenderer.SetPositions(mainPositions.ToArray());
        }

        if (enableReturnCable)
        {
            EnsureReturnLineRenderer();

            if (returnLineRenderer != null)
            {
                returnPositions.Clear();
                path.GetSampledPositions(returnPositions, samplesPerSegmentOverride, returnCableOffset);

                if (returnPositions.Count == 0)
                {
                    returnLineRenderer.positionCount = 0;
                }
                else
                {
                    returnLineRenderer.positionCount = returnPositions.Count;
                    returnLineRenderer.SetPositions(returnPositions.ToArray());
                }
            }
        }
        else
        {
            if (returnLineRenderer != null)
            {
                returnLineRenderer.positionCount = 0;
            }
        }
    }

    private void EnsureReturnLineRenderer()
    {
        if (returnLineRenderer != null)
            return;

        // Try to find an existing child with a LineRenderer
        foreach (Transform child in transform)
        {
            var lr = child.GetComponent<LineRenderer>();
            if (lr != null)
            {
                returnLineRenderer = lr;
                ConfigureLineRendererDefaults(returnLineRenderer);
                // Ensure it uses same material as main
                if (cableMaterialInstance != null)
                {
                    returnLineRenderer.material = cableMaterialInstance;
                }
                return;
            }
        }

        // Create a new child for the return cable
        GameObject childGO = new GameObject("ReturnCable");
        childGO.transform.SetParent(transform, false);
        returnLineRenderer = childGO.AddComponent<LineRenderer>();

        ConfigureLineRendererDefaults(returnLineRenderer);

        if (cableMaterialInstance != null)
        {
            returnLineRenderer.material = cableMaterialInstance;
        }
        else if (mainLineRenderer != null && mainLineRenderer.sharedMaterial != null)
        {
            returnLineRenderer.material = mainLineRenderer.sharedMaterial;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            if (!path) path = GetComponent<LiftPath>();
            if (!mainLineRenderer) mainLineRenderer = GetComponent<LineRenderer>();
            ConfigureLineRendererDefaults(mainLineRenderer);

            if (enableReturnCable)
            {
                EnsureReturnLineRenderer();
            }

            RebuildCable();
        }
        else
        {
            // Width might have changed
            if (mainLineRenderer != null)
            {
                ConfigureLineRendererDefaults(mainLineRenderer);
            }
            if (returnLineRenderer != null)
            {
                ConfigureLineRendererDefaults(returnLineRenderer);
            }
        }
    }
#endif
}
