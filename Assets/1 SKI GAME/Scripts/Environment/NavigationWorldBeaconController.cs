using UnityEngine;

namespace SkiGame.Navigation
{
    [DisallowMultipleComponent]
    public sealed class NavigationWorldBeaconController : MonoBehaviour
    {
        [Header("View")]
        [SerializeField] private Camera targetCamera;

        [Header("Colour Target")]
        [Tooltip("Optional child object whose renderers should receive the marker colour. If left empty, this object is used.")]
        [SerializeField] private Transform colourTargetRoot;

        [Header("Colour Application")]
        [Tooltip("Shader colour properties to try, in order.")]
        [SerializeField] private string[] colourPropertyNames = { "_BaseColor", "_Color", "_TintColor", "_EmissionColor" };
        [SerializeField] private bool alsoWriteMaterialColor = true;
        [SerializeField] private bool updateSharedMaterialsInEditor = false;

        [Header("Transparency")]
        [Range(0f, 1f)]
        [SerializeField] private float beaconAlpha = 0.55f;
        [SerializeField] private bool forceTransparentMaterials = true;

        [Header("Terrain Snap")]
        [SerializeField] private bool snapAnchorToTerrain = true;
        [SerializeField] private LayerMask terrainLayers = ~0;
        [SerializeField] private float terrainRayStartHeight = 200f;
        [SerializeField] private float terrainRayDistance = 500f;
        [SerializeField] private float terrainSurfaceOffset = 0.05f;

        private Renderer[] _targetRenderers;
        private Vector3 _anchorPosition;
        private Color _accentColor = Color.cyan;
        private Quaternion _initialRotation;

        public Vector3 AnchorPosition => _anchorPosition;

        private void Awake()
        {
            _initialRotation = transform.rotation;
            CacheTargetRenderers();
            EnsureSemiTransparentMaterials();
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                CacheTargetRenderers();
            }
        }

        public void Configure(Vector3 anchorPosition, string displayName, Color accentColor)
        {
            _anchorPosition = ResolveAnchorPosition(anchorPosition);
            _accentColor = accentColor.a > 0f ? accentColor : Color.cyan;
            _accentColor.a = beaconAlpha;

            transform.rotation = _initialRotation;
            transform.position = _anchorPosition;

            if (_targetRenderers == null || _targetRenderers.Length == 0)
                CacheTargetRenderers();

            EnsureSemiTransparentMaterials();
            ApplyAccentColor();
        }

        private void CacheTargetRenderers()
        {
            Transform target = colourTargetRoot != null ? colourTargetRoot : transform;
            _targetRenderers = target != null
                ? target.GetComponentsInChildren<Renderer>(true)
                : System.Array.Empty<Renderer>();
        }

        private Vector3 ResolveAnchorPosition(Vector3 requestedAnchor)
        {
            if (!snapAnchorToTerrain)
                return requestedAnchor;

            if (TryRaycastTerrain(requestedAnchor, out Vector3 grounded))
                return grounded;

            if (TrySampleTerrain(requestedAnchor, out grounded))
                return grounded;

            return requestedAnchor;
        }

        private bool TryRaycastTerrain(Vector3 worldPosition, out Vector3 groundedPosition)
        {
            Vector3 origin = worldPosition + Vector3.up * terrainRayStartHeight;
            float distance = Mathf.Max(terrainRayDistance, terrainRayStartHeight * 2f);

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, distance, terrainLayers, QueryTriggerInteraction.Ignore))
            {
                groundedPosition = hit.point + Vector3.up * terrainSurfaceOffset;
                return true;
            }

            groundedPosition = default;
            return false;
        }

        private bool TrySampleTerrain(Vector3 worldPosition, out Vector3 groundedPosition)
        {
            Terrain terrain = FindTerrainAt(worldPosition);
            if (terrain != null && terrain.terrainData != null)
            {
                float y = terrain.SampleHeight(worldPosition) + terrain.transform.position.y + terrainSurfaceOffset;
                groundedPosition = new Vector3(worldPosition.x, y, worldPosition.z);
                return true;
            }

            groundedPosition = default;
            return false;
        }

        private static Terrain FindTerrainAt(Vector3 worldPosition)
        {
            var terrains = Terrain.activeTerrains;
            if (terrains == null || terrains.Length == 0)
                return Terrain.activeTerrain;

            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain t = terrains[i];
                if (t == null || t.terrainData == null)
                    continue;

                Vector3 p = t.transform.position;
                Vector3 s = t.terrainData.size;

                bool inside =
                    worldPosition.x >= p.x && worldPosition.x <= p.x + s.x &&
                    worldPosition.z >= p.z && worldPosition.z <= p.z + s.z;

                if (inside)
                    return t;
            }

            return Terrain.activeTerrain;
        }

        private void EnsureSemiTransparentMaterials()
        {
            if (!forceTransparentMaterials || _targetRenderers == null || _targetRenderers.Length == 0)
                return;

            for (int i = 0; i < _targetRenderers.Length; i++)
            {
                Renderer renderer = _targetRenderers[i];
                if (renderer == null)
                    continue;

                Material[] materials = Application.isPlaying
                    ? renderer.materials
                    : (updateSharedMaterialsInEditor ? renderer.sharedMaterials : renderer.materials);

                if (materials == null)
                    continue;

                for (int m = 0; m < materials.Length; m++)
                {
                    Material mat = materials[m];
                    if (mat == null)
                        continue;

                    SetMaterialTransparent(mat);
                }
            }
        }

        private static void SetMaterialTransparent(Material mat)
        {
            if (mat == null)
                return;

            // URP Lit / similar shaders using _Surface and blend factors
            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f); // Transparent
            }

            if (mat.HasProperty("_Blend"))
            {
                mat.SetFloat("_Blend", 0f); // Alpha
            }

            if (mat.HasProperty("_SrcBlend"))
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);

            if (mat.HasProperty("_DstBlend"))
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

            if (mat.HasProperty("_ZWrite"))
                mat.SetFloat("_ZWrite", 0f);

            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }

        private void ApplyAccentColor()
        {
            if (_targetRenderers == null || _targetRenderers.Length == 0)
                return;

            for (int i = 0; i < _targetRenderers.Length; i++)
            {
                Renderer renderer = _targetRenderers[i];
                if (renderer == null)
                    continue;

                ApplyAccentColor(renderer, _accentColor);
            }
        }

        private void ApplyAccentColor(Renderer renderer, Color colour)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);

            Material[] materials = Application.isPlaying
                ? renderer.materials
                : (updateSharedMaterialsInEditor ? renderer.sharedMaterials : renderer.materials);

            bool wroteAnyProperty = false;

            if (materials != null && materials.Length > 0)
            {
                for (int m = 0; m < materials.Length; m++)
                {
                    Material mat = materials[m];
                    if (mat == null)
                        continue;

                    if (forceTransparentMaterials)
                        SetMaterialTransparent(mat);

                    if (colourPropertyNames != null)
                    {
                        for (int p = 0; p < colourPropertyNames.Length; p++)
                        {
                            string prop = colourPropertyNames[p];
                            if (string.IsNullOrWhiteSpace(prop) || !mat.HasProperty(prop))
                                continue;

                            block.SetColor(prop, colour);
                            wroteAnyProperty = true;

                            if (alsoWriteMaterialColor)
                                mat.SetColor(prop, colour);
                        }
                    }

                    if (alsoWriteMaterialColor && mat.HasProperty("_EmissionColor"))
                    {
                        mat.SetColor("_EmissionColor", colour);
                    }

                    if (alsoWriteMaterialColor)
                    {
                        try
                        {
                            mat.color = colour;
                        }
                        catch
                        {
                        }
                    }
                }
            }

            if (!wroteAnyProperty && colourPropertyNames != null)
            {
                for (int p = 0; p < colourPropertyNames.Length; p++)
                {
                    string prop = colourPropertyNames[p];
                    if (string.IsNullOrWhiteSpace(prop))
                        continue;

                    block.SetColor(prop, colour);
                }
            }

            renderer.SetPropertyBlock(block);
        }
    }
}