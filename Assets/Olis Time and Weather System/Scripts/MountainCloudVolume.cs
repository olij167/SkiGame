using UnityEngine;

namespace TimeWeather
{
    [RequireComponent(typeof(Renderer))]
    public class MountainCloudVolume : MonoBehaviour
    {
        private static readonly int ID_CloudColour = Shader.PropertyToID("_CloudColour");
        private static readonly int ID_ShadowColour = Shader.PropertyToID("_ShadowColour");
        private static readonly int ID_Density = Shader.PropertyToID("_Density");
        private static readonly int ID_Absorption = Shader.PropertyToID("_Absorption");
        private static readonly int ID_LightingStrength = Shader.PropertyToID("_LightingStrength");
        private static readonly int ID_CloudSpeed = Shader.PropertyToID("_CloudSpeed");
        private static readonly int ID_CloudSoftness = Shader.PropertyToID("_CloudSoftness");
        private static readonly int ID_CloudCoverageBias = Shader.PropertyToID("_CloudCoverageBias");
        private static readonly int ID_CloudTurbulence = Shader.PropertyToID("_CloudTurbulence");
        private static readonly int ID_CloudWarpStrength = Shader.PropertyToID("_CloudWarpStrength");
        private static readonly int ID_AlphaMultiplier = Shader.PropertyToID("_AlphaMultiplier");
        private static readonly int ID_ClusterScale = Shader.PropertyToID("_ClusterScale");
        private static readonly int ID_ClusterDensity = Shader.PropertyToID("_ClusterDensity");
        private static readonly int ID_DetailStrength = Shader.PropertyToID("_DetailStrength");
        private static readonly int ID_EdgeFade = Shader.PropertyToID("_EdgeFade");
        private static readonly int ID_FieldOffset = Shader.PropertyToID("_FieldOffset");
        private static readonly int ID_VolumeSeed = Shader.PropertyToID("_VolumeSeed");

        public Renderer targetRenderer;

        [Header("Layout / Variation")]
        public Vector2 localFieldOffset;
        public float volumeSeed = 31.73f;

        [Header("Defaults")]
        public Color defaultCloudColour = new Color(0.95f, 0.97f, 1.0f, 1f);
        public Color defaultShadowColour = new Color(0.62f, 0.68f, 0.76f, 1f);
        public float defaultDensity = 0.65f;
        public float defaultAbsorption = 1.15f;
        public float defaultLightingStrength = 0.75f;
        public Vector4 defaultCloudSpeed = new Vector4(0.003f, 0.002f, 0f, 0f);
        public float defaultSoftness = 0.18f;
        public float defaultCoverageBias = 0f;
        public float defaultTurbulence = 0.35f;
        public float defaultWarpStrength = 0.16f;
        public float defaultAlphaMultiplier = 1.0f;
        public float defaultClusterScale = 180f;
        public float defaultClusterDensity = 0.72f;
        public float defaultDetailStrength = 0.85f;
        public float defaultEdgeFade = 0.22f;

        private MaterialPropertyBlock _mpb;
        private bool _dirty = true;

        private Color _cloudColour;
        private Color _shadowColour;
        private float _density;
        private float _absorption;
        private float _lightingStrength;
        private Vector4 _cloudSpeed;
        private float _softness;
        private float _coverageBias;
        private float _turbulence;
        private float _warpStrength;
        private float _alphaMultiplier;
        private float _clusterScale;
        private float _clusterDensity;
        private float _detailStrength;
        private float _edgeFade;
        private Vector2 _fieldOffset;
        private float _seed;

        private void Awake()
        {
            if (targetRenderer == null)
                targetRenderer = GetComponent<Renderer>();

            if (_mpb == null)
                _mpb = new MaterialPropertyBlock();

            ResetToDefaults();
            Apply();
        }

        private void LateUpdate()
        {
            if (_dirty)
                Apply();
        }

        public void ResetToDefaults()
        {
            _cloudColour = defaultCloudColour;
            _shadowColour = defaultShadowColour;
            _density = defaultDensity;
            _absorption = defaultAbsorption;
            _lightingStrength = defaultLightingStrength;
            _cloudSpeed = defaultCloudSpeed;
            _softness = defaultSoftness;
            _coverageBias = defaultCoverageBias;
            _turbulence = defaultTurbulence;
            _warpStrength = defaultWarpStrength;
            _alphaMultiplier = defaultAlphaMultiplier;
            _clusterScale = defaultClusterScale;
            _clusterDensity = defaultClusterDensity;
            _detailStrength = defaultDetailStrength;
            _edgeFade = defaultEdgeFade;
            _fieldOffset = localFieldOffset;
            _seed = volumeSeed;
            _dirty = true;
        }

        public void RefreshNow()
        {
            Apply();
        }

        public void SetFieldState(
            Color cloudColour,
            Color shadowColour,
            float density,
            float absorption,
            float lightingStrength,
            Vector4 cloudSpeed,
            float softness,
            float coverageBias,
            float turbulence,
            float warpStrength,
            float alphaMultiplier,
            float clusterScale,
            float clusterDensity,
            float detailStrength,
            float edgeFade,
            Vector2 fieldOffset,
            float seed)
        {
            _cloudColour = cloudColour;
            _shadowColour = shadowColour;
            _density = density;
            _absorption = absorption;
            _lightingStrength = lightingStrength;
            _cloudSpeed = cloudSpeed;
            _softness = softness;
            _coverageBias = coverageBias;
            _turbulence = turbulence;
            _warpStrength = warpStrength;
            _alphaMultiplier = alphaMultiplier;
            _clusterScale = clusterScale;
            _clusterDensity = clusterDensity;
            _detailStrength = detailStrength;
            _edgeFade = edgeFade;
            _fieldOffset = fieldOffset;
            _seed = seed;
            _dirty = true;
        }

        private void Apply()
        {
            if (targetRenderer == null)
                return;

            if (_mpb == null)
                _mpb = new MaterialPropertyBlock();

            targetRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(ID_CloudColour, _cloudColour);
            _mpb.SetColor(ID_ShadowColour, _shadowColour);
            _mpb.SetFloat(ID_Density, _density);
            _mpb.SetFloat(ID_Absorption, _absorption);
            _mpb.SetFloat(ID_LightingStrength, _lightingStrength);
            _mpb.SetVector(ID_CloudSpeed, _cloudSpeed);
            _mpb.SetFloat(ID_CloudSoftness, _softness);
            _mpb.SetFloat(ID_CloudCoverageBias, _coverageBias);
            _mpb.SetFloat(ID_CloudTurbulence, _turbulence);
            _mpb.SetFloat(ID_CloudWarpStrength, _warpStrength);
            _mpb.SetFloat(ID_AlphaMultiplier, _alphaMultiplier);
            _mpb.SetFloat(ID_ClusterScale, _clusterScale);
            _mpb.SetFloat(ID_ClusterDensity, _clusterDensity);
            _mpb.SetFloat(ID_DetailStrength, _detailStrength);
            _mpb.SetFloat(ID_EdgeFade, _edgeFade);
            _mpb.SetVector(ID_FieldOffset, new Vector4(_fieldOffset.x, _fieldOffset.y, 0f, 0f));
            _mpb.SetFloat(ID_VolumeSeed, _seed);
            targetRenderer.SetPropertyBlock(_mpb);

            _dirty = false;
        }
    }
}