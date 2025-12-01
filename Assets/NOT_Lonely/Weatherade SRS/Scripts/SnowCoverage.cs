namespace NOT_Lonely.Weatherade
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Rendering;

#if UNITY_EDITOR
    using UnityEditor;
    using Unity.Properties;

    [ExecuteInEditMode]
#endif

    public class SnowCoverage : CoverageBase
    {
#pragma warning disable 0414
        [SerializeField] private bool basicSettingsFoldout = false;
        [SerializeField] private bool tessellationFoldout = false;
        [SerializeField] private bool displacementFoldout = false;
        [SerializeField] private bool tracesFoldout = false;
        [SerializeField] private bool sparklesFoldout = false;

        [SerializeField] private bool detailMapFoldout = false;
        [SerializeField] private bool foldout_basic_layer0 = true;
        [SerializeField] private bool foldout_basic_layer1 = false;
        [SerializeField] private bool foldout_basic_layer2 = false;
        [SerializeField] private bool foldout_displacement_layer0 = true;
        [SerializeField] private bool foldout_displacement_layer1 = false;
        [SerializeField] private bool foldout_displacement_layer2 = false;
        [SerializeField] private bool foldout_traces_layer0 = true;
        [SerializeField] private bool foldout_traces_layer1 = false;
        [SerializeField] private bool foldout_traces_layer2 = false;
        [SerializeField] private bool foldout_sss_layer0 = true;
        [SerializeField] private bool foldout_sss_layer1 = false;
        [SerializeField] private bool foldout_sss_layer2 = false;

#pragma warning restore 0414

        public enum SparkleMaskSurce
        {
            MainCoverageTexAlpha,
            SparkleMask
        }

        //Basic Settings
        [SerializeField] private Texture2D coverageTex0;
        [SerializeField] private Texture2D coverageTex1;
        [SerializeField] private Texture2D coverageTex2;

        [SerializeField] private Color coverageColor = Color.white;
        [SerializeField] private Color coverageColor1 = Color.white;
        [SerializeField] private Color coverageColor2 = Color.white;

        [SerializeField] private Vector2 cov0Smoothness = new Vector2(0, 1);
        [SerializeField] private Vector2 cov1Smoothness = new Vector2(0, 1);
        [SerializeField] private Vector2 cov2Smoothness = new Vector2(0, 1);

        [SerializeField] private float coverageNormalScale0 = 1;
        [SerializeField] private float coverageNormalScale1 = 1;
        [SerializeField] private float coverageNormalScale2 = 1;

        [SerializeField] private float coverageTiling = 0.05f;
        [SerializeField] private float coverageTiling1 = 0.05f;
        [SerializeField] private float coverageTiling2 = 0.05f;

        [Range(0, 1)][SerializeField] private float emissionMasking = 0.98f;
        [Range(1, 128)][SerializeField] private int covTriBlendContrast = 12;
        [Range(0, 1)][SerializeField] private float maskByAlpha = 1;
        [Range(0, 1)][SerializeField] private float coverageNormalsOverlay = 0.75f;
        [SerializeField] private bool useAveragedNormals;
        [Range(0, 1)]public float coverageAmount = 0.75f;

        //Detail Map
        [SerializeField] private bool useCoverageDetail = true;
        [SerializeField] private Texture2D coverageDetailTex;
        [SerializeField] private float detailTiling = 0.65f;
        [SerializeField] private Vector2 detailTexRemap = new Vector2(0.05f, 0.5f);
        [SerializeField] private float detailNormalScale = 1;
        [SerializeField] private float detailDistance = 15;

        //Tessellation
        [SerializeField] private bool tessellation;
        [SerializeField, Range(5, 100)] private float tessEdgeL = 10;
        [SerializeField, Range(0, 1)] private float tessFactorSnow = 0;
        [SerializeField] private float tessMaxDisp = 0.35f;
        [SerializeField] private Vector2 tessSnowdriftRange = new Vector2(0.63f, 0.775f);

        //Displacement
        [SerializeField] private bool displacement;
        [SerializeField] private Vector2 heightMap0Contrast = new Vector2(0, 1);
        [SerializeField] private Vector2 heightMap1Contrast = new Vector2(0, 1);
        [SerializeField] private Vector2 heightMap2Contrast = new Vector2(0, 1);

        [SerializeField] private float coverageDisplacement = 1f;
        [SerializeField] private float coverageDisplacement1 = 1f;
        [SerializeField] private float coverageDisplacement2 = 1f;

        [SerializeField, Range(0, 6)] private int heightMap0LOD = 1;
        [SerializeField, Range(0, 6)] private int heightMap1LOD = 1;
        [SerializeField, Range(0, 6)] private int heightMap2LOD = 1;

        [SerializeField][Range(0, 1)] private float coverageDisplacementOffset = 0.3f;

        //Traces
        [SerializeField] private bool traces;
        [SerializeField, Range(0, 1)] private float tracesBaseBlend0 = 1;
        [SerializeField, Range(0, 1)] private float tracesBaseBlend1 = 1;
        [SerializeField, Range(0, 1)] private float tracesBaseBlend2 = 1;

        [SerializeField] private float tracesNormalScale = 0.8f;
        [SerializeField] private float tracesNormalScale1 = 0.8f;
        [SerializeField] private float tracesNormalScale2 = 0.8f;

        [SerializeField, ColorUsage(true)] private Color tracesColor = new Color(0.1f, 0.25f, 0.4f, 0.1f);
        [SerializeField, ColorUsage(true)] private Color tracesColor1 = new Color(0.1f, 0.25f, 0.4f, 0.1f);
        [SerializeField, ColorUsage(true)] private Color tracesColor2 = new Color(0.1f, 0.25f, 0.4f, 0.1f);

        [SerializeField] private Vector2 tracesColorBlendRange = new Vector2(0, 1);
        [SerializeField] private Vector2 tracesColorBlendRange1 = new Vector2(0, 1);
        [SerializeField] private Vector2 tracesColorBlendRange2 = new Vector2(0, 1);

        [SerializeField] private bool traceDetail;
        [SerializeField] private Texture2D traceDetailTex;
        [SerializeField] private float traceDetailTiling = 1;
        [SerializeField] private float traceDetailNormalScale = 1;
        [SerializeField] private float traceDetailIntensity = 0.5f;
        
        //Sparkle
        [SerializeField] private bool sparkle = true;
        [SerializeField] private bool sss = true;
        [SerializeField] private float sss_intensity = 1;
        [SerializeField] private float colorEnhance = 0.05f;

        [SerializeField] private Vector2 enhanceRemap0 = new Vector2(0, 1);
        [SerializeField] private Vector2 enhanceRemap1 = new Vector2(0, 1);
        [SerializeField] private Vector2 enhanceRemap2 = new Vector2(0, 1);

        [SerializeField] private float highlightBrightness0 = 1;
        [SerializeField] private float highlightBrightness1 = 1;
        [SerializeField] private float highlightBrightness2 = 1;

        [SerializeField] private Vector2 sssMaskRemap0 = new Vector2(0, 1);
        [SerializeField] private Vector2 sssMaskRemap1 = new Vector2(0, 1);
        [SerializeField] private Vector2 sssMaskRemap2 = new Vector2(0, 1);

        [SerializeField] private Texture2D sparkleTex;
        [Range(0, 1)][SerializeField] private float sparklesAmount = 0.75f;
        [SerializeField] private float sparkleDistFalloff = 50;
        [SerializeField] private float localSparkleTiling = 1;
        [SerializeField] private float screenSpaceSparklesTiling = 2;
        [SerializeField] private SparkleMaskSurce sparkleTexSS = SparkleMaskSurce.SparkleMask;
        [SerializeField] private SparkleMaskSurce sparkleTexLS = SparkleMaskSurce.SparkleMask;
        [SerializeField] private float sparklesBrightness = 20;
        [SerializeField] private float sparklesLightmapMaskPower = 4.5f;
        [Range(0, 1)][SerializeField] private float sparklesHighlightMaskExpansion = 0.95f;

#if UNITY_EDITOR
        [MenuItem("GameObject/NOT_Lonely/Weatherade/Snow Coverage Instance", false, 10)]
        public static void CreateNewSnowCoverageInstance()
        {
            if (NL_Utilities.FindObjectOfType<CoverageBase>(true))
            {
                Debug.LogWarning("Only one instance of 'Weatherade Coverage' is allowed.");
                return;
            }
            SnowCoverage snowCoverageInstance = new GameObject("SRS_SnowCoverageInstance", typeof(SnowCoverage)).GetComponent<SnowCoverage>();
            Selection.activeObject = snowCoverageInstance;

            Texture2D defaultTex0 = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/NOT_Lonely/Weatherade SRS/Textures/Snow_01_n_h_sm.tif");
            Texture2D defaultDetailTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/NOT_Lonely/Weatherade SRS/Textures/SnowDetail.tif");
            Texture2D defaultSparkleTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/NOT_Lonely/Weatherade SRS/Textures/SparkleMask.tif");
            snowCoverageInstance.coverageTex0 = defaultTex0;
            snowCoverageInstance.coverageDetailTex = defaultDetailTex;
            snowCoverageInstance.sparkleTex = defaultSparkleTex;

            snowCoverageInstance.UpdateCoverageMaterials();

        }
#endif

#if UNITY_EDITOR
        public override void ValidateValues()
        {
            sss_intensity = Mathf.Max(0, sss_intensity);
            colorEnhance = Mathf.Max(0, colorEnhance);
            highlightBrightness0 = Mathf.Max(0, highlightBrightness0);
            coverageDisplacement = Mathf.Max(0, coverageDisplacement);
            sparklesBrightness = Mathf.Max(0, sparklesBrightness);
            sparkleDistFalloff = Mathf.Max(0, sparkleDistFalloff);
            screenSpaceSparklesTiling = Mathf.Max(0, screenSpaceSparklesTiling);
            sparklesLightmapMaskPower = Mathf.Max(1, sparklesLightmapMaskPower);
            coverageTiling = Mathf.Max(0.0001f, coverageTiling);
            tessMaxDisp = Mathf.Max(0, tessMaxDisp);

            detailTiling = Mathf.Max(0, detailTiling);
            detailDistance = Mathf.Max(0, detailDistance);

            base.ValidateValues();
        }
#endif
        public override void OnEnable()
        {
            base.OnEnable();
            Shader.EnableKeyword("SRS_SNOW_ON");
        }

        public override void GetCoverageShaders()
        {
            meshCoverageShaders = new Shader[2];
            terrainCoverageShaders = new Shader[2];
            meshCoverageShaders[0] = Shader.Find("NOT_Lonely/Weatherade/Snow Coverage");
            meshCoverageShaders[1] = Shader.Find("Hidden/NOT_Lonely/Weatherade/Snow Coverage (Tessellation)");
            terrainCoverageShaders[0] = Shader.Find("NOT_Lonely/Weatherade/Snow Coverage (Terrain)");
            terrainCoverageShaders[1] = Shader.Find("Hidden/NOT_Lonely/Weatherade/Snow Coverage (Terrain-Tessellation)");
        }

        public static void UpdateMtl(Material material)
        {
            if(instance != null) instance.UpdateCoverageMaterial(material);
        }

        public override void UpdateCoverageMaterial(Material material)
        {
            base.UpdateCoverageMaterial(material);

            //Global variables for the Deferred rendering path
#if UNITY_EDITOR
#if !USING_URP && !USING_HDRP
            Shader.SetKeyword(sparkleKeyword, sparkle);
            Shader.SetKeyword(sssKeyword, sss);
            SwitchCustomDeferredShading(sparkle || sss);
#endif
#endif
            if (sparkle)
            {
#if UNITY_EDITOR
#if !USING_URP && !USING_HDRP
                Shader.SetKeyword(sparkleTexLSKeyword, (float)sparkleTexLS > 0);
                Shader.SetKeyword(sparkleTexSSKeyword, (float)sparkleTexSS > 0);
                if (!Shader.IsKeywordEnabled(sparkleTexLSKeyword) || !Shader.IsKeywordEnabled(sparkleTexSSKeyword)) Shader.SetGlobalTexture("_CoverageTex0", coverageTex0);
#endif
#endif
                Shader.SetGlobalTexture("_SparkleTex", sparkleTex);
                Shader.SetGlobalFloat("_SparklesAmount", sparklesAmount);
                Shader.SetGlobalFloat("_SparkleDistFalloff", sparkleDistFalloff);
                Shader.SetGlobalFloat("_LocalSparkleTiling", localSparkleTiling);
                Shader.SetGlobalFloat("_ScreenSpaceSparklesTiling", screenSpaceSparklesTiling);
                Shader.SetGlobalFloat("_SparklesBrightness", sparklesBrightness);
                Shader.SetGlobalFloat("_SparklesHighlightMaskExpansion", sparklesHighlightMaskExpansion);
                
            }
            Shader.SetGlobalFloat("_SSS_intensity", sss_intensity);
            //
            Shader.SetGlobalTexture("_TraceDetailTex", traceDetailTex);
#if UNITY_EDITOR
            if (material.HasFloat("_TracesOverride") && material.GetFloat("_TracesOverride") == 0)
            {
                material.SetFloat("_Traces", traces ? 1 : 0);
                material.SetKeyword(new LocalKeyword(material.shader, "_TRACES_ON"), traces);
            }

            if (material.HasFloat("_DisplacementOverride") && material.GetFloat("_DisplacementOverride") == 0)
            {
                material.SetFloat("_Displacement", displacement ? 1 : 0);
                material.SetKeyword(new LocalKeyword(material.shader, "_DISPLACEMENT_ON"), displacement);
            }

            if (material.HasFloat("_TraceDetailOverride") && material.GetFloat("_TraceDetailOverride") == 0)
            {
                material.SetFloat("_TraceDetail", traceDetail ? 1 : 0);
                material.SetKeyword(new LocalKeyword(material.shader, "_TRACE_DETAIL"), traceDetail);
            }

            if (material.HasFloat("_UseAveragedNormalsOverride") && material.GetFloat("_UseAveragedNormalsOverride") == 0)
            {
                material.SetFloat("_UseAveragedNormals", useAveragedNormals ? 1 : 0);
                material.SetKeyword(new LocalKeyword(material.shader, "_USE_AVERAGED_NORMALS"), useAveragedNormals);
            }

            if (material.HasFloat("_SparkleOverride") && material.GetFloat("_SparkleOverride") == 0)
            {
                material.SetFloat("_Sparkle", sparkle ? 1 : 0);
                material.SetKeyword(new LocalKeyword(material.shader, "_SPARKLE_ON"), sparkle);
            }
            if (material.HasFloat("_SssOverride") && material.GetFloat("_SssOverride") == 0)
            {
                material.SetFloat("_Sss", sss ? 1 : 0);
                material.SetKeyword(new LocalKeyword(material.shader, "_SSS_ON"), sss);
            }

            if (material.HasFloat("_SparkleTexSSOverride") && material.GetFloat("_SparkleTexSSOverride") == 0)
            {
                material.SetFloat("_SparkleTexSS", (float)sparkleTexSS);
                material.SetKeyword(new LocalKeyword(material.shader, "_SPARKLE_TEX_SS"), (float)sparkleTexSS > 0);
            }
            if (material.HasFloat("_SparkleTexLSOverride") && material.GetFloat("_SparkleTexLSOverride") == 0)
            {
                material.SetFloat("_SparkleTexLS", (float)sparkleTexLS);
                material.SetKeyword(new LocalKeyword(material.shader, "_SPARKLE_TEX_LS"), (float)sparkleTexLS > 0);
            }

            NL_Utilities.SetKeyword(material, useCoverageDetail, "_UseCoverageDetail", "_USE_COVERAGE_DETAIL");
#endif

            if (material.HasFloat("_TessellationOverride") && material.GetFloat("_TessellationOverride") == 0)
            {
#if UNITY_EDITOR
                material.SetFloat("_Tessellation", tessellation ? 1 : 0);

                Shader simpleShader = Shader.Find("NOT_Lonely/Weatherade/Snow Coverage");
                Shader tessShader = Shader.Find("Hidden/NOT_Lonely/Weatherade/Snow Coverage (Tessellation)");
                Shader terrainSimpleShader = Shader.Find("NOT_Lonely/Weatherade/Snow Coverage (Terrain)");
                Shader terrainTessShader = Shader.Find("Hidden/NOT_Lonely/Weatherade/Snow Coverage (Terrain-Tessellation)");

                if (material.GetFloat("_Tessellation") == 1)
                {
                    if (material.shader == simpleShader) material.shader = tessShader;
                    if (material.shader == terrainSimpleShader) material.shader = terrainTessShader;
                }
                else
                {
                    if (material.shader == tessShader) material.shader = simpleShader;
                    if (material.shader == terrainTessShader) material.shader = terrainSimpleShader;
                }
#endif
            }

            //Basic Settings
            SetTexture(material, "_CoverageTex0", coverageTex0);
            SetTexture(material, "_CoverageTex1", coverageTex1);
            SetTexture(material, "_CoverageTex2", coverageTex2);

            SetColor(material, "_CoverageColor", coverageColor);
            SetColor(material, "_CoverageColor1", coverageColor1);
            SetColor(material, "_CoverageColor2", coverageColor2);

            SetVector(material, "_Cov0Smoothness", cov0Smoothness);
            SetVector(material, "_Cov1Smoothness", cov1Smoothness);
            SetVector(material, "_Cov2Smoothness", cov2Smoothness);

            SetFloat(material, "_CoverageNormalScale0", coverageNormalScale0);
            SetFloat(material, "_CoverageNormalScale1", coverageNormalScale1);
            SetFloat(material, "_CoverageNormalScale2", coverageNormalScale2);

            SetFloat(material, "_CoverageTiling", coverageTiling);
            SetFloat(material, "_CoverageTiling1", coverageTiling1);
            SetFloat(material, "_CoverageTiling2", coverageTiling2);

            SetFloat(material, "_CoverageAmount", coverageAmount);
            SetFloat(material, "_EmissionMasking", emissionMasking);
            SetFloat(material, "_MaskCoverageByAlpha", maskByAlpha);
            SetFloat(material, "_CovTriBlendContrast", covTriBlendContrast);
            SetFloat(material, "_CoverageNormalsOverlay", coverageNormalsOverlay);

            //Detail Map
            SetTexture(material, "_CoverageDetailTex", coverageDetailTex);
            SetFloat(material, "_DetailTiling", detailTiling);
            SetVector(material, "_DetailTexRemap", detailTexRemap);
            SetFloat(material, "_DetailNormalScale", detailNormalScale);
            SetFloat(material, "_DetailDistance", detailDistance);

            //Tessellation
            SetFloat(material, "_TessEdgeL", tessEdgeL);
            SetFloat(material, "_TessFactorSnow", tessFactorSnow);
            SetFloat(material, "_TessMaxDisp", tessMaxDisp);
            SetVector(material, "_TessSnowdriftRange", tessSnowdriftRange);

            //Displacement
            SetVector(material, "_HeightMap0Contrast", heightMap0Contrast);
            SetVector(material, "_HeightMap1Contrast", heightMap1Contrast);
            SetVector(material, "_HeightMap2Contrast", heightMap2Contrast);

            SetFloat(material, "_CoverageDisplacement", coverageDisplacement);
            SetFloat(material, "_CoverageDisplacement1", coverageDisplacement1);
            SetFloat(material, "_CoverageDisplacement2", coverageDisplacement2);

            SetFloat(material, "_HeightMap0LOD", heightMap0LOD);
            SetFloat(material, "_HeightMap1LOD", heightMap1LOD);
            SetFloat(material, "_HeightMap2LOD", heightMap2LOD);

            SetFloat(material, "_CoverageDisplacementOffset", coverageDisplacementOffset);

            //Traces
            SetFloat(material, "_TracesNormalScale", tracesNormalScale);
            SetFloat(material, "_TracesNormalScale1", tracesNormalScale1);
            SetFloat(material, "_TracesNormalScale2", tracesNormalScale2);

            SetFloat(material, "_TracesBaseBlend0", tracesBaseBlend0);
            SetFloat(material, "_TracesBaseBlend1", tracesBaseBlend1);
            SetFloat(material, "_TracesBaseBlend2", tracesBaseBlend2);

            SetVector(material, "_TracesColorBlendRange", tracesColorBlendRange);
            SetVector(material, "_TracesColorBlendRange1", tracesColorBlendRange1);
            SetVector(material, "_TracesColorBlendRange2", tracesColorBlendRange2);

            SetColor(material, "_TracesColor", tracesColor);
            SetColor(material, "_TracesColor1", tracesColor1);
            SetColor(material, "_TracesColor2", tracesColor2);

            SetFloat(material, "_TraceDetailTiling", traceDetailTiling);
            SetFloat(material, "_TraceDetailNormalScale", traceDetailNormalScale);
            SetFloat(material, "_TraceDetailIntensity", traceDetailIntensity);
            
            //Sparkle and SSS
            SetFloat(material, "_SSS_intensity", sss_intensity);
            SetFloat(material, "_ColorEnhance", colorEnhance);

            SetVector(material, "_EnhanceRemap0", enhanceRemap0);
            SetVector(material, "_EnhanceRemap1", enhanceRemap1);
            SetVector(material, "_EnhanceRemap2", enhanceRemap2);

            SetVector(material, "_SssMaskRemap0", sssMaskRemap0);
            SetVector(material, "_SssMaskRemap1", sssMaskRemap1);
            SetVector(material, "_SssMaskRemap2", sssMaskRemap2);

            SetFloat(material, "_HighlightBrightness0", highlightBrightness0);
            SetFloat(material, "_HighlightBrightness1", highlightBrightness1);
            SetFloat(material, "_HighlightBrightness2", highlightBrightness2);

            SetFloat(material, "_SparklesAmount", sparklesAmount);
            SetFloat(material, "_SparkleDistFalloff", sparkleDistFalloff);
            SetTexture(material, "_SparkleTex", sparkleTex);
            SetFloat(material, "_LocalSparkleTiling", localSparkleTiling);
            SetFloat(material, "_SparklesBrightness", sparklesBrightness);
            SetFloat(material, "_ScreenSpaceSparklesTiling", screenSpaceSparklesTiling);
            SetFloat(material, "_SparklesHighlightMaskExpansion", sparklesHighlightMaskExpansion);
            SetFloat(material, "_SparklesLightmapMaskPower", sparklesLightmapMaskPower);
        }
    }
}
