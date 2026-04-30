using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
public class InteractionAreaHeightNormalizer : MonoBehaviour
{
    private static readonly int HeightMinID = Shader.PropertyToID("_HeightMin");
    private static readonly int HeightMaxID = Shader.PropertyToID("_HeightMax");

    [SerializeField] private MeshFilter meshFilter;
    [SerializeField] private bool updateContinuously = true;

    private Renderer _renderer;
    private MaterialPropertyBlock _mpb;
    private Mesh _lastAppliedMesh;
    private Vector2 _lastAppliedHeightRange;
    private bool _hasAppliedHeightRange;

    private void OnEnable()
    {
        ApplyBounds();
    }

    private void OnValidate()
    {
        ApplyBounds();
    }

    private void LateUpdate()
    {
        if (updateContinuously)
            ApplyBounds();
    }

    public void SetContinuousUpdates(bool enabled)
    {
        updateContinuously = enabled;
        ApplyBounds();
    }

    private void ApplyBounds()
    {
        if (_renderer == null)
            _renderer = GetComponent<Renderer>();

        if (_mpb == null)
            _mpb = new MaterialPropertyBlock();

        if (meshFilter == null)
            meshFilter = GetComponent<MeshFilter>();

        if (meshFilter == null || meshFilter.sharedMesh == null || _renderer == null)
            return;

        Mesh sharedMesh = meshFilter.sharedMesh;
        Bounds localBounds = sharedMesh.bounds;
        Vector2 heightRange = new Vector2(localBounds.min.y, localBounds.max.y);

        if (_hasAppliedHeightRange && _lastAppliedMesh == sharedMesh && _lastAppliedHeightRange == heightRange)
            return;

        _renderer.GetPropertyBlock(_mpb);
        _mpb.SetFloat(HeightMinID, heightRange.x);
        _mpb.SetFloat(HeightMaxID, heightRange.y);
        _renderer.SetPropertyBlock(_mpb);

        _lastAppliedMesh = sharedMesh;
        _lastAppliedHeightRange = heightRange;
        _hasAppliedHeightRange = true;
    }
}
