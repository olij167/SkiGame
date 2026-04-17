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

        Bounds localBounds = meshFilter.sharedMesh.bounds;

        _renderer.GetPropertyBlock(_mpb);
        _mpb.SetFloat(HeightMinID, localBounds.min.y);
        _mpb.SetFloat(HeightMaxID, localBounds.max.y);
        _renderer.SetPropertyBlock(_mpb);
    }
}