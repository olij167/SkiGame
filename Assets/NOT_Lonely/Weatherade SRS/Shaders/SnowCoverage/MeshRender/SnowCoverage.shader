Shader "NOT_Lonely/Weatherade/Snow Coverage"
{
    Properties
    {
        // Specular vs Metallic workflow
        _WorkflowMode("WorkflowMode", Float) = 1.0

        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)

        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _SmoothnessTextureChannel("Smoothness texture channel", Float) = 0

        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _MetallicGlossMap("Metallic", 2D) = "white" {}

        _SpecColor("Specular", Color) = (0.2, 0.2, 0.2)
        _SpecGlossMap("Specular", 2D) = "white" {}

        [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
        [ToggleOff] _EnvironmentReflections("Environment Reflections", Float) = 1.0

        _BumpScale("Scale", Float) = 1.0
        _BumpMap("Normal Map", 2D) = "bump" {}

        _Parallax("Scale", Range(0.005, 0.08)) = 0.005
        _ParallaxMap("Height Map", 2D) = "black" {}

        _OcclusionStrength("Strength", Range(0.0, 1.0)) = 1.0
        _OcclusionMap("Occlusion", 2D) = "white" {}

        [HDR] _EmissionColor("Color", Color) = (0,0,0)
        _EmissionMap("Emission", 2D) = "white" {}

        _DetailMask("Detail Mask", 2D) = "white" {}
        _DetailAlbedoMapScale("Scale", Range(0.0, 2.0)) = 1.0
        _DetailAlbedoMap("Detail Albedo x2", 2D) = "linearGrey" {}
        _DetailNormalMapScale("Scale", Range(0.0, 2.0)) = 1.0
        [Normal] _DetailNormalMap("Normal Map", 2D) = "bump" {}

        // SRP batching compatibility for Clear Coat (Not used in Lit)
        [HideInInspector] _ClearCoatMask("_ClearCoatMask", Float) = 0.0
        [HideInInspector] _ClearCoatSmoothness("_ClearCoatSmoothness", Float) = 0.0

        // Blending state
        _Surface("__surface", Float) = 0.0
        _Blend("__blend", Float) = 0.0
        _Cull("__cull", Float) = 2.0
        [ToggleUI] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0

        [ToggleUI] _ReceiveShadows("Receive Shadows", Float) = 1.0
        // Editmode props
        _QueueOffset("Queue offset", Float) = 0.0

        // ObsoleteProperties
        [HideInInspector] _MainTex("BaseMap", 2D) = "white" {}
        [HideInInspector] _Color("Base Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _GlossMapScale("Smoothness", Float) = 0.0
        [HideInInspector] _Glossiness("Smoothness", Float) = 0.0
        [HideInInspector] _GlossyReflections("EnvironmentReflections", Float) = 0.0

        [HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}

        ////////////////////////////////
        ///SRS snow shader properties///
        ////////////////////////////////
        [Toggle(_USE_AVERAGED_NORMALS)] _UseAveragedNormals("_UseAveragedNormals", Float) = 0 _UseAveragedNormalsOverride("UseAveragedNormalsOverride", Float) = 0
		[Toggle(_COVERAGE_ON)] _Coverage("Coverage", Float) = 1 _CoverageOverride("CoverageOverride", Float) = 0
		[Toggle(_PAINTABLE_COVERAGE_ON)] _PaintableCoverage("PaintableCoverage", Float) = 0 _PaintableCoverageOverride("PaintableCoverageOverride", Float) = 0
        [Toggle(_THREE_TEX_MODE)] _ThreeTexMode("ThreeTexMode", Float) = 0 _ThreeTexModeOverride("ThreeTexModeOverride", Float) = 0
        [Toggle(_USE_COVERAGE_DETAIL)] _UseCoverageDetail("UseCoverageDetail", Float) = 0 _UseCoverageDetailOverride("UseCoverageDetailOverride", Float) = 0
		[Toggle(_SPARKLE_ON)] _Sparkle("Sparkle", Float) = 0 _SparkleOverride("SparkleOverride", Float) = 0
		[Toggle(_SSS_ON)] _Sss("Sparkle", Float) = 0 _SssOverride("SssOverride", Float) = 0
		[Toggle(_SPARKLE_TEX_SS)] _SparkleTexSS("Sparkle Tex SS", Float) = 0 _SparkleTexSSOverride("SparkleTexSSOverride", Float) = 0
		[Toggle(_SPARKLE_TEX_LS)] _SparkleTexLS("Sparkle Tex LS", Float) = 0 _SparkleTexLSOverride("SparkleTexLSOverride", Float) = 0
		[Toggle(_DISPLACEMENT_ON)] _Displacement("Displacement", Float) = 0 _DisplacementOverride("DisplacementOverride", Float) = 0 
        [Toggle(_TRACES_ON)] _Traces("Traces", Float) = 0  _TracesOverride("TracesOverride", Float) = 0
		[Toggle(_TRACE_DETAIL)] _TraceDetail("TraceDetail", Float) = 0 _TraceDetailOverride("TraceDetailOverride", Float) = 0

        //Basic coverage settings
        _CoverageAmount("CoverageAmount", Range(0, 1)) = 1 _CoverageAmountOverride("CoverageAmountOverride", Float) = 0
        _CovTriBlendContrast("CovTriBlendContrast", Range(2, 128)) = 12 _CovTriBlendContrastOverride("CovTriBlendContrastOverride", Float) = 0

        _CoverageTex0("CoverageTex0", 2D) = "bump" {} _CoverageTex0Override("CoverageTex0Override", Float) = 0
        _CoverageTex1("CoverageTex1", 2D) = "bump" {} _CoverageTex1Override("CoverageTex1Override", Float) = 0
        _CoverageTex2("CoverageTex2", 2D) = "bump" {} _CoverageTex2Override("CoverageTex2Override", Float) = 0

		[NoAlpha]_CoverageColor("CoverageColor", Color) = (0.8349056,0.9156185,1,1) _CoverageColorOverride("CoverageColorOverride", Float) = 0
        [NoAlpha]_CoverageColor1("CoverageColor1", Color) = (1, 1, 1 ,1) _CoverageColor1Override("CoverageColor1Override", Float) = 0
        [NoAlpha]_CoverageColor2("CoverageColor2", Color) = (1, 1, 1, 1) _CoverageColor2Override("CoverageColor2Override", Float) = 0

        _EnhanceRemap0("Enhance Remap 0", Vector) = (0, 1, 0, 0) _EnhanceRemap0Override("EnhanceRemap0Override", Float) = 0
        _EnhanceRemap1("Enhance Remap 1", Vector) = (0, 1, 0, 0) _EnhanceRemap1Override("EnhanceRemap1Override", Float) = 0
        _EnhanceRemap2("Enhance Remap 2", Vector) = (0, 1, 0, 0) _EnhanceRemap2Override("EnhanceRemap2Override", Float) = 0

        _Cov0Smoothness("Cov0Smoothness", Vector) = (0, 1, 0, 0) _Cov0SmoothnessOverride("Cov0SmoothnessOverride", Float) = 0
        _Cov1Smoothness("Cov1Smoothness", Vector) = (0, 1, 0, 0) _Cov1SmoothnessOverride("Cov1SmoothnessOverride", Float) = 0
        _Cov2Smoothness("Cov2Smoothness", Vector) = (0, 1, 0, 0) _Cov2SmoothnessOverride("Cov2SmoothnessOverride", Float) = 0

        _CoverageNormalScale0("Coverage Normal Scale 0", Float) = 1 _CoverageNormalScale0Override("CoverageNormalScale0Override", Float) = 0
        _CoverageNormalScale1("Coverage Normal Scale 1", Float) = 1 _CoverageNormalScale1Override("CoverageNormalScale1Override", Float) = 0
        _CoverageNormalScale2("Coverage Normal Scale 2", Float) = 1 _CoverageNormalScale2Override("CoverageNormalScale2Override", Float) = 0

        _HeightMap0Contrast("HeightMap0Contrast", Vector) = (0, 1, 0, 0) _HeightMap0ContrastOverride("HeightMap0ContrastOverride", Float) = 0
        _HeightMap1Contrast("HeightMap1Contrast", Vector) = (0, 1, 0, 0) _HeightMap1ContrastOverride("HeightMap1ContrastOverride", Float) = 0
        _HeightMap2Contrast("HeightMap2Contrast", Vector) = (0, 1, 0, 0) _HeightMap2ContrastOverride("HeightMap2ContrastOverride", Float) = 0

        _CoverageTiling("Coverage Tiling", Float) = 0.15 _CoverageTilingOverride("CoverageTilingOverride", Float) = 0
        _CoverageTiling1("Coverage Tiling 1", Float) = 0.15 _CoverageTiling1Override("CoverageTiling1Override", Float) = 0
        _CoverageTiling2("Coverage Tiling 2", Float) = 0.15 _CoverageTiling2Override("CoverageTiling2Override", Float) = 0

		_CoverageNormalsOverlay("Coverage Normals Overlay", Range( 0 , 1)) = 0.5080121 _CoverageNormalsOverlayOverride("CoverageNormalsOverlayOverride", Float) = 0
        _EmissionMasking("EmissionMasking", Range(0, 1)) = 1 _EmissionMaskingOverride("EmissionMaskingOverride", Float) = 0
		_MaskCoverageByAlpha("MaskCoverageByAlpha", Range(0, 1)) = 1 _MaskCoverageByAlphaOverride("MaskCoverageByAlphaOverride", Float) = 0

        //Detail Map
        _CoverageDetailTex("CoverageDetailTex", 2D) = "bump" {} _CoverageDetailTexOverride("CoverageDetailTexOverride", Float) = 0
        _DetailTiling("Detail Tiling", Float) = 1 _DetailTilingOverride("DetailTilingOverride", Float) = 0
        _DetailDistance("Detail Distance", Float) = 10 _DetailDistanceOverride("DetailDistanceOverride", Float) = 0
        _DetailTexRemap("Detail Tex Remap", Vector) = (0, 1, 0, 0) _DetailTexRemapOverride("DetailTexRemapOverride", Float) = 0
        _DetailNormalScale("Detail Normal Scale", Float) = 1 _DetailNormalScaleOverride("DetailNormalScaleOverride", Float) = 0

        //Area mask
        _CoverageAreaMaskRange("CoverageAreaMaskRange", Range(0, 1)) = 1 _CoverageAreaMaskRangeOverride("CoverageAreaMaskRangeOverride", Float) = 0
        _CoverageAreaBias("Coverage Area Bias", Range( 0.001 , 0.3)) = 0.001 _CoverageAreaBiasOverride("CoverageAreaBiasOverride", Float) = 0
        _CoverageLeakReduction("CoverageLeakReduction", Range( 0.0 , 0.99)) = 0.0 _CoverageLeakReductionOverride("CoverageLeakReductionOverride", Float) = 0
        _PrecipitationDirOffset("PrecipitationDirOffset", Range( -1 , 1)) = 0 _PrecipitationDirOffsetOverride("PrecipitationDirOffsetOverride", Float) = 0
        _PrecipitationDirRange("PrecipitationDirRange", Vector) = (0,1,0,0) _PrecipitationDirRangeOverride("PrecipitationDirRangeOverride", Float) = 0
		
        //Tessellation
        _Tessellation("Tessellation", Float) = 0 _TessellationOverride("TessellationOverride", Float) = 0
        _TessEdgeL("TessEdgeL", Range( 5 , 100)) = 20 _TessEdgeLOverride("TessEdgeLOverride", Float) = 0
        _TessFactorSnow("TessFactorSnow", Range(0, 1)) = 0.5 _TessFactorSnowOverride("TessFactorSnowOverride", Float) = 0
		_TessSnowdriftRange("TessSnowdriftRange", Vector) = (0.5, 0.8, 0, 0) _TessSnowdriftRangeOverride("TessSnowdriftRangeOverride", Float) = 0
		_TessMaxDisp("TessMaxDisp", Float) = 0.45 _TessMaxDispOverride("TessMaxDispOverride", Float) = 0

        //Displacement
        _CoverageDisplacement("CoverageDisplacement", Float) = 0.5 _CoverageDisplacementOverride("CoverageDisplacementOverride", Float) = 0
        _CoverageDisplacement1("CoverageDisplacement1", Float) = 0.5 _CoverageDisplacement1Override("CoverageDisplacement1Override", Float) = 0
        _CoverageDisplacement2("CoverageDisplacement2", Float) = 0.5 _CoverageDisplacement2Override("CoverageDisplacement2Override", Float) = 0
		_CoverageDisplacementOffset("CoverageDisplacementOffset", Range( 0 , 1)) = 0.5 _CoverageDisplacementOffsetOverride("CoverageDisplacementOffsetOverride", Float) = 0
        _HeightMap0LOD("HeightMap0LOD", Range(0, 6)) = 1 _HeightMap0LODOverride("HeightMap0LODOverride", Float) = 0
        _HeightMap1LOD("HeightMap1LOD", Range(0, 6)) = 1 _HeightMap1LODOverride("HeightMap1LODOverride", Float) = 0
        _HeightMap2LOD("HeightMap2LOD", Range(0, 6)) = 1 _HeightMap2LODOverride("HeightMap2LODOverride", Float) = 0

        //Traces
		_TracesNormalScale("TracesNormalScale", Float) = 1 _TracesNormalScaleOverride("TracesNormalScaleOverride", Float) = 0
        _TracesNormalScale1("TracesNormalScale1", Float) = 1 _TracesNormalScale1Override("TracesNormalScale1Override", Float) = 0
        _TracesNormalScale2("TracesNormalScale2", Float) = 1 _TracesNormalScale2Override("TracesNormalScale2Override", Float) = 0
		_TraceDetailTiling("TraceDetailTiling", Float) = 50 _TraceDetailTilingOverride("TraceDetailTilingOverride", Float) = 0
		_TraceDetailNormalScale("TraceDetailNormalScale", Float) = 1 _TraceDetailNormalScaleOverride("TraceDetailNormalScaleOverride", Float) = 0
		_TraceDetailIntensity("TraceDetailIntensity", Range(0, 1)) = 0.5 _TraceDetailIntensityOverride("TraceDetailIntensityOverride", Float) = 0
		_TracesColor("TracesColor", Color) = (0.1, 0.25, 0.4, 1) _TracesColorOverride("TracesColorOverride", Float) = 0
        _TracesColor1("TracesColor1", Color) = (0.1, 0.25, 0.4, 1) _TracesColor1Override("TracesColor1Override", Float) = 0
        _TracesColor2("TracesColor2", Color) = (0.1, 0.25, 0.4, 1) _TracesColor2Override("TracesColor2Override", Float) = 0
		_TracesBaseBlend0("_TracesBaseBlend0", Range(0, 1)) = 0.5 _TracesBaseBlend0Override("_TracesBaseBlend0Override", Float) = 0
        _TracesBaseBlend1("_TracesBaseBlend1", Range(0, 1)) = 0.5 _TracesBaseBlend1Override("_TracesBaseBlend1Override", Float) = 0
        _TracesBaseBlend2("_TracesBaseBlend2", Range(0, 1)) = 0.5 _TracesBaseBlend2Override("_TracesBaseBlend2Override", Float) = 0
		_TracesColorBlendRange("TracesColorBlendRange", Vector) = (0, 0.5, 0, 0) _TracesColorBlendRangeOverride("TracesColorBlendRangeOverride", Float) = 0
        _TracesColorBlendRange1("TracesColorBlendRange1", Vector) = (0, 0.5, 0, 0) _TracesColorBlendRange1Override("TracesColorBlendRange1Override", Float) = 0
        _TracesColorBlendRange2("TracesColorBlendRange2", Vector) = (0, 0.5, 0, 0) _TracesColorBlendRange2Override("TracesColorBlendRange2Override", Float) = 0

        //Blend by normals
		_BlendByNormalsStrength("Blend By Normals Strength", Float) = 2 _BlendByNormalsStrengthOverride("BlendByNormalsStrengthOverride", Float) = 0
		_BlendByNormalsPower("Blend By Normals Power", Float) = 5 _BlendByNormalsPowerOverride("BlendByNormalsPowerOverride", Float) = 0

		//Sparkle and SSS
		_SparkleTex("SparkleTex", 2D) = "black" {} _SparkleTexOverride("SparkleTexOverride", Float) = 0
		_SparklesAmount("Sparkles Amount", Range(0, 0.999)) = 0.95 _SparklesAmountOverride("SparklesAmountOverride", Float) = 0
		_SparkleDistFalloff("Sparkle Distance Falloff", Float) = 50 _SparkleDistFalloffOverride("SparkleDistFalloffOverride", Float) = 0
		_LocalSparkleTiling ("Local Sparkle Tiling", Float) = 1 _LocalSparkleTilingOverride("LocalSparkleTilingOverride", Float) = 0
		_ScreenSpaceSparklesTiling("Screen Space Sparkles Tiling", Float) = 2 _ScreenSpaceSparklesTilingOverride("ScreenSpaceSparklesTilingOverride", Float) = 0
		_SparklesBrightness("Sparkles Brightness", Float) = 30 _SparklesBrightnessOverride("SparklesBrightnessOverride", Float) = 0
        _ColorEnhance("Color Enhance", Float) = 10 _ColorEnhanceOverride("ColorEnhanceOverride", Float) = 0
		_SparklesLightmapMaskPower("Sparkles Lightmap Mask Power", Float) = 4.5 _SparklesLightmapMaskPowerOverride("SparklesLightmapMaskPowerOverride", Float) = 0
		_SparklesHighlightMaskExpansion("Sparkles Highlight Mask Expansion", Range( 0 , 0.99)) = 0.8 _SparklesHighlightMaskExpansionOverride("SparklesHighlightMaskExpansionOverride", Float) = 0

        _SssMaskRemap0("SSS Mask Remap 0", Vector) = (0, 1, 0, 0) _SssMaskRemap0Override("SSSMaskRemap0Override", Float) = 0
        _SssMaskRemap1("SSS Mask Remap 1", Vector) = (0, 1, 0, 0) _SssMaskRemap1Override("SSSMaskRemap1Override", Float) = 0
        _SssMaskRemap2("SSS Mask Remap 2", Vector) = (0, 1, 0, 0) _SssMaskRemap2Override("SSSMaskRemap2Override", Float) = 0

        _HighlightBrightness0("Highlight Brightness 0", Float) = 1 _HighlightBrightness0Override("HighlightBrightness0Override", Float) = 0
        _HighlightBrightness1("Highlight Brightness 1", Float) = 1 _HighlightBrightness1Override("HighlightBrightness1Override", Float) = 0
        _HighlightBrightness2("Highlight Brightness 2", Float) = 1 _HighlightBrightness2Override("HighlightBrightness2Override", Float) = 0

		_SSS_intensity("SSS_intensity", Float) = 1 _SSS_intensityOverride("SSS_intensityOverride", Float) = 0

        //Other
        _CoverageAreaFalloffHardness("CoverageAreaFalloffHardness", Range( 0 , 1)) = 0.5
		_PaintedMask("PaintedMask", 2D) = "gray" {}
		_PaintedMaskNormal("PaintedMaskNormal", 2D) = "bump" {}
		_AlbedoLOD("AlbedoLOD", 2D) = "white" {}
		_NormalLOD("NormalLOD", 2D) = "bump" {}
		_MapID("MapID", float) = 0 //needed to set a particular map when baking the distant map
		[HideInInspector] _TilingMultiplier("TilingMultiplier", Range(0 , 1)) = 1 // needed to set adjust the splats tiling when baking the distant map
		[HideInInspector] _Mode ("__mode", Float) = 0.0
        //-----------------------------------------------   
    }

    HLSLINCLUDE
        #define SRS_SNOW_COVERAGE_SHADER //define this shader as a snow coverage shader
    ENDHLSL

    SubShader
    {
        // Universal Pipeline tag is required. If Universal render pipeline is not set in the graphics settings
        // this Subshader will fail. One can add a subshader below or fallback to Standard built-in to make this
        // material work with both Universal Render Pipeline and Builtin Unity Pipeline
        Tags{"RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "UniversalMaterialType" = "Lit" "IgnoreProjector" = "True" "ShaderModel"="4.5"}
        LOD 300

        // ------------------------------------------------------------------
        //  Forward pass. Shades all light in a single pass. GI + emission + Fog
        Pass
        {
            // Lightmode matches the ShaderPassName set in UniversalRenderPipeline.cs. SRPDefaultUnlit and passes with
            // no LightMode tag are also rendered by Universal Render Pipeline
            Name "ForwardLit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            // -------------------------------------
            // Render State Commands
            Blend[_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]
            ZWrite[_ZWrite]
            Cull[_Cull]
            AlphaToMask[_AlphaToMask]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

             //Weatherade Keywords
            #pragma shader_feature_local _COVERAGE_ON
            #pragma shader_feature_local _USE_AVERAGED_NORMALS
            #pragma shader_feature_local _PAINTABLE_COVERAGE_ON
            #pragma shader_feature_local _THREE_TEX_MODE
            #pragma shader_feature_local _USE_COVERAGE_DETAIL
            #pragma shader_feature_local _TRACES_ON
            #ifdef _TRACES_ON
                #pragma shader_feature_local _TRACE_DETAIL
            #endif
            #pragma shader_feature_local _DISPLACEMENT_ON 
            #pragma shader_feature_local_fragment _SSS_ON
            #pragma shader_feature_local_fragment _SPARKLE_ON
            #ifdef _SPARKLE_ON
                #pragma shader_feature_local_fragment _SPARKLE_TEX_SS
                #pragma shader_feature_local_fragment _SPARKLE_TEX_LS
            #endif

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ _ALPHAPREMULTIPLY_ON _ALPHAMODULATE_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma shader_feature_local_fragment _SPECULAR_SETUP
 
            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
 
 
            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
 
            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "../../Includes/SRS_LitInputSnow.hlsl"
            #include "../../Includes/SRS_LitForwardPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            //Weatherade Keywords
            #pragma shader_feature_local _COVERAGE_ON
            #pragma shader_feature_local _USE_AVERAGED_NORMALS
            #pragma shader_feature_local _PAINTABLE_COVERAGE_ON
            #pragma shader_feature_local _THREE_TEX_MODE
            #pragma shader_feature_local _TRACES_ON
            #ifdef _TRACES_ON
                #pragma shader_feature_local _TRACE_DETAIL
            #endif
            #pragma shader_feature_local _DISPLACEMENT_ON 
            #pragma shader_feature_local_fragment _SSS_ON

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Universal Pipeline keywords

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

            // This is used during shadow map generation to differentiate between directional and punctual light shadows, as they use different formulas to apply Normal Bias
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "../../Includes/SRS_LitInputSnow.hlsl"
            #include "../../Includes/SRS_ShadowCasterPass.hlsl"
            
            //#include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            // Lightmode matches the ShaderPassName set in UniversalRenderPipeline.cs. SRPDefaultUnlit and passes with
            // no LightMode tag are also rendered by Universal Render Pipeline
            Name "GBuffer"
            Tags
            {
                "LightMode" = "UniversalGBuffer"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite[_ZWrite]
            ZTest LEqual
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 4.5

            // Deferred Rendering Path does not support the OpenGL-based graphics API:
            // Desktop OpenGL, OpenGL ES 3.0, WebGL 2.0.
            #pragma exclude_renderers gles3 glcore

            // -------------------------------------
            // Shader Stages
            #pragma vertex LitGBufferPassVertex
            #pragma fragment LitGBufferPassFragment
            
            //Weatherade Keywords
            #pragma shader_feature_local _COVERAGE_ON
            #pragma shader_feature_local _USE_AVERAGED_NORMALS
            #pragma shader_feature_local _PAINTABLE_COVERAGE_ON
            #pragma shader_feature_local _THREE_TEX_MODE
            #pragma shader_feature_local _USE_COVERAGE_DETAIL
            #pragma shader_feature_local _TRACES_ON
            #ifdef _TRACES_ON
                #pragma shader_feature_local _TRACE_DETAIL
            #endif
            #pragma shader_feature_local _DISPLACEMENT_ON 
            #pragma shader_feature_local_fragment _SSS_ON
            #pragma shader_feature_local_fragment _SPARKLE_ON
            #ifdef _SPARKLE_ON
                #pragma shader_feature_local_fragment _SPARKLE_TEX_SS
                #pragma shader_feature_local_fragment _SPARKLE_TEX_LS
            #endif

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            //#pragma shader_feature_local_fragment _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED

            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma shader_feature_local_fragment _SPECULAR_SETUP
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            //#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            //#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "../../Includes/SRS_LitInputSnow.hlsl"
            #include "../../Includes/SRS_LitGBufferPass.hlsl"
            //#include "Packages/com.unity.render-pipelines.universal/Shaders/LitGBufferPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags
            {
                "LightMode" = "DepthOnly"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            ColorMask R
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            //Weatherade Keywords
            #pragma shader_feature_local _COVERAGE_ON
            #pragma shader_feature_local _USE_AVERAGED_NORMALS
            #pragma shader_feature_local _THREE_TEX_MODE
            #pragma shader_feature_local _PAINTABLE_COVERAGE_ON
            #pragma shader_feature_local _TRACES_ON
            #ifdef _TRACES_ON
                #pragma shader_feature_local _TRACE_DETAIL
            #endif
            #pragma shader_feature_local _DISPLACEMENT_ON 

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "../../Includes/SRS_LitInputSnow.hlsl"
            #include "../../Includes/SRS_DepthOnlyPass.hlsl"
            ENDHLSL
        }

        // This pass is used when drawing to a _CameraNormalsTexture texture
        Pass
        {
            Name "DepthNormals"
            Tags
            {
                "LightMode" = "DepthNormals"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            //Weatherade Keywords
            #pragma shader_feature_local _COVERAGE_ON
            #pragma shader_feature_local _USE_AVERAGED_NORMALS
            #pragma shader_feature_local _PAINTABLE_COVERAGE_ON
            #pragma shader_feature_local _THREE_TEX_MODE
            #pragma shader_feature_local _USE_COVERAGE_DETAIL
            #pragma shader_feature_local _TRACES_ON
            #ifdef _TRACES_ON
                #pragma shader_feature_local _TRACE_DETAIL
            #endif
            #pragma shader_feature_local _DISPLACEMENT_ON 
            
            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

            // -------------------------------------
            // Universal Pipeline keywords
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Includes
            #include "../../Includes/SRS_LitInputSnow.hlsl"
            #include "../../Includes/SRS_LitDepthNormalsPass.hlsl"
            //#include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
            ENDHLSL
        }

        // This pass it not used during regular rendering, only for lightmap baking.
        Pass
        {
            Name "Meta"
            Tags
            {
                "LightMode" = "Meta"
            }

            // -------------------------------------
            // Render State Commands
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex UniversalVertexMeta
            #pragma fragment UniversalFragmentMetaLit

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local_fragment _SPECULAR_SETUP
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local_fragment _SPECGLOSSMAP
            #pragma shader_feature EDITOR_VISUALIZATION

            // -------------------------------------
            // Includes
            #include "../../Includes/SRS_LitInputSnow.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitMetaPass.hlsl"

            ENDHLSL
        }

        Pass
        {
            Name "Universal2D"
            Tags
            {
                "LightMode" = "Universal2D"
            }

            // -------------------------------------
            // Render State Commands
            Blend[_SrcBlend][_DstBlend]
            ZWrite[_ZWrite]
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex vert
            #pragma fragment frag

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ALPHAPREMULTIPLY_ON

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Includes
            #include "../../Includes/SRS_LitInputSnow.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/Universal2D.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "NOT_Lonely.Weatherade.ShaderGUI.NL_SRS_SnowCoverage_GUI"
    //CustomEditor "UnityEditor.Rendering.Universal.ShaderGUI.LitShader"
}
