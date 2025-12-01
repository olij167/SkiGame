Shader "Hidden/NOT_Lonely/Weatherade/SnowCoverageTerrain (AddPass-Tessellation)"
{
    Properties
    {
        // Layer count is passed down to guide height-blend enable/disable, due
        // to the fact that heigh-based blend will be broken with multipass.
        [HideInInspector] [PerRendererData] _NumLayersCount ("Total Layer Count", Float) = 1.0

        // set by terrain engine
        [HideInInspector] _Control("Control (RGBA)", 2D) = "red" {}
        [HideInInspector] _Splat3("Layer 3 (A)", 2D) = "white" {}
        [HideInInspector] _Splat2("Layer 2 (B)", 2D) = "white" {}
        [HideInInspector] _Splat1("Layer 1 (G)", 2D) = "white" {}
        [HideInInspector] _Splat0("Layer 0 (R)", 2D) = "white" {}
        [HideInInspector] _Normal3("Normal 3 (A)", 2D) = "bump" {}
        [HideInInspector] _Normal2("Normal 2 (B)", 2D) = "bump" {}
        [HideInInspector] _Normal1("Normal 1 (G)", 2D) = "bump" {}
        [HideInInspector] _Normal0("Normal 0 (R)", 2D) = "bump" {}
        [HideInInspector][Gamma] _Metallic0("Metallic 0", Range(0.0, 1.0)) = 0.0
        [HideInInspector][Gamma] _Metallic1("Metallic 1", Range(0.0, 1.0)) = 0.0
        [HideInInspector][Gamma] _Metallic2("Metallic 2", Range(0.0, 1.0)) = 0.0
        [HideInInspector][Gamma] _Metallic3("Metallic 3", Range(0.0, 1.0)) = 0.0
        [HideInInspector] _Mask3("Mask 3 (A)", 2D) = "grey" {}
        [HideInInspector] _Mask2("Mask 2 (B)", 2D) = "grey" {}
        [HideInInspector] _Mask1("Mask 1 (G)", 2D) = "grey" {}
        [HideInInspector] _Mask0("Mask 0 (R)", 2D) = "grey" {}
        [HideInInspector] _Smoothness0("Smoothness 0", Range(0.0, 1.0)) = 1.0
        [HideInInspector] _Smoothness1("Smoothness 1", Range(0.0, 1.0)) = 1.0
        [HideInInspector] _Smoothness2("Smoothness 2", Range(0.0, 1.0)) = 1.0
        [HideInInspector] _Smoothness3("Smoothness 3", Range(0.0, 1.0)) = 1.0

        // used in fallback on old cards & base map
        [HideInInspector] _BaseMap("BaseMap (RGB)", 2D) = "white" {}
        [HideInInspector] _BaseColor("Main Color", Color) = (1,1,1,1)

        [HideInInspector] _TerrainHolesTexture("Holes Map (RGB)", 2D) = "white" {}

        //SRS snow shader properties
		[Toggle(_COVERAGE_ON)] _Coverage("Coverage", Float) = 1
		[Toggle(_PAINTABLE_COVERAGE_ON)] _PaintableCoverage("PaintableCoverage", Float) = 0
		[Toggle(_SPARKLE_ON)] _Sparkle("Sparkle", Float) = 0
		[Toggle(_SSS_ON)] _Sss("Sparkle", Float) = 0
		[Toggle(_SPARKLE_TEX_SS)] _SparkleTexSS("Sparkle Tex SS", Float) = 0
		[Toggle(_SPARKLE_TEX_LS)] _SparkleTexLS("Sparkle Tex LS", Float) = 0

		[Toggle] _Tessellation("Tessellation", Float) = 0
		[Toggle(_DISPLACEMENT_ON)] _Displacement("Displacement", Float) = 0
		[Toggle(_STOCHASTIC_ON)] _Stochastic("Stochastic", Float) = 0

		_PrecipitationDirOffset("PrecipitationDirOffset", Range( -1 , 1)) = 0

		_CoverageTex0("CoverageTex0", 2D) = "bump" {}
        _Cov0Smoothness("Cov0Smoothness", Range(0, 5)) = 1
        _Cov0SmoothnessOverride("Cov0SmoothnessOverride", Float) = 0
		_CovMasks0_triBlendContrast("CovMasks0_triBlendContrast", Float) = 2.5
		_CoverageAmount("CoverageAmount", Range( 0 , 1)) = 1
		_HeightMap0Contrast("HeightMap0Contrast", Range(0, 1)) = 0.2
		[NoAlpha]_CoverageColor("CoverageColor", Color) = (0.8349056,0.9156185,1,1)
		_CoverageSmoothnessContrast("Coverage Smoothness Contrast", Range( 0 , 1)) = 0.1
		_CoverageMicroRelief("Coverage Micro Relief", Range( 0 , 1)) = 0.05
		_MicroReliefFadeDistance("Micro Relief Fade Distance", Float) = 2
		_BaseCoverageNormalsBlend("Base/CoverageNormalsBlend", Range( 0 , 1)) = 0.2588235
		_CoverageNormalsOverlay("Coverage Normals Overlay", Range( 0 , 1)) = 0.5080121
		_CoverageTiling("Coverage Tiling", Float) = 0.14
		_CoverageAreaBias("Coverage Area Bias", Range( 0.001 , 0.3)) = 0.001
		_CoverageLeakReduction("CoverageLeakReduction", Range( 0.0 , 0.99)) = 0.0
		_CoverageNormalScale0("Coverage Normal Scale 0", Float) = 1
		_BlendByNormalsStrength("Blend By Normals Strength", Float) = 2
		_BlendByNormalsPower("Blend By Normals Power", Float) = 5
		//Sparkle
		_SparkleTex("SparkleTex", 2D) = "black" {}
		_SparklesAmount("Sparkles Amount", Range(0, 0.999)) = 0.95
		_SparkleDistFalloff("Sparkle Distance Falloff", Float) = 50
		_LocalSparkleTiling ("Local Sparkle Tiling", Float) = 1
		_ScreenSpaceSparklesTiling("Screen Space Sparkles Tiling", Float) = 2
		_SparklesBrightness("Sparkles Brightness", Float) = 30
        _ColorEnhance("Color Enhance", Float) = 10
        _ColorEnhanceOverride("ColorEnhanceOverride", Float) = 0
		_SparkleBrightnessRT("Sparkles Brightness RT", Float) = 4
		_SparklesLightmapMaskPower("Sparkles Lightmap Mask Power", Float) = 4.5
		_SparklesHighlightMaskExpansion("Sparkles Highlight Mask Expansion", Range( 0 , 0.99)) = 0.8

		_CoverageAreaFalloffHardness("CoverageAreaFalloffHardness", Range( 0 , 1)) = 0.5
		_PaintedMask("PaintedMask", 2D) = "gray" {}
		_PaintedMaskNormal("PaintedMaskNormal", 2D) = "bump" {}
		_AlbedoLOD("AlbedoLOD", 2D) = "white" {}
		_NormalLOD("NormalLOD", 2D) = "bump" {}
		_DistanceFadeStart("DistanceFadeStart", Float) = 150
		_DistanceFadeFalloff("DistanceFadeFalloff", Float) = 1
		_CoverageDisplacementOffset("CoverageDisplacementOffset", Range( 0 , 1)) = 0.5
		_TessFactorSnow("TessFactorSnow", Range(0, 1)) = 0.5
		_TessEdgeL("TessEdgeL", Range( 5 , 100)) = 20
		_TessMaxDisp("TessMaxDisp", Float) = 0.45
		_TessSnowdriftRange("TessSnowdriftRange", Vector) = (0.5, 0.8, 0, 0)

		[Toggle(_TRACES_ON)] _Traces("Traces", Float) = 0
		[Toggle(_TRACE_DETAIL)] _TraceDetail("TraceDetail", Float) = 0
		_TracesNormalScale("TracesNormalScale", Float) = 5
		_TraceDetailTiling("TraceDetailTiling", Float) = 50
		_TraceDetailNormalScale("TraceDetailNormalScale", Float) = 1
		_TraceDetailIntensity("TraceDetailIntensity", Range(0, 1)) = 0.5
		[NoAlpha]_TracesColor("TracesColor", Color) = (0.1, 0.25, 0.4, 1)
		_TracesBlendFactor("TracesBlendFactor", Range(0, 1)) = 0.5
		_TracesColorBlendRange("TracesColorBlendRange", Vector) = (0, 0.5, 0, 0)

		_SSS_intensity("SSS_intensity", Float) = 1

		_PrecipitationDirRange("PrecipitationDirRange", Vector) = (0,1,0,0)
		_CoverageAreaMaskRange("CoverageAreaMaskRange", Range(0, 1)) = 1
		_CoverageDisplacement("CoverageDisplacement", Float) = 0.5
		_MapID("MapID", float) = 0 //needed to set a particular map when baking the distant map
		[HideInInspector] _TilingMultiplier("TilingMultiplier", Range(0 , 1)) = 1 // needed to set adjust the splats tiling when baking the distant map

		//snow shader overrides
		_CoverageOverride("CoverageOverride", Float) = 0
		_CoverageColorOverride("CoverageColorOverride", Float) = 0
		_CoverageDisplacementOverride("CoverageDisplacementOverride", Float) = 0
		
		//Traces
		_TracesOverride("TracesOverride", Float) = 0
		_TraceDetailOverride("TraceDetailOverride", Float) = 0
		_TracesNormalScaleOverride("TracesNormalScaleOverride", Float) = 0
		_TraceDetailTexOverride("TraceDetailTexOverride", Float) = 0
		_TraceDetailTilingOverride("TraceDetailTilingOverride", Float) = 0
		_TraceDetailNormalScaleOverride("TraceDetailNormalScaleOverride", Float) = 0
		_TraceDetailIntensityOverride("TraceDetailIntensityOverride", Float) = 0
		_TracesBlendFactorOverride("TracesBlendFactorOverride", Float) = 0
		_TracesColorOverride("TracesColorOverride", Float) = 0
		_TracesColorBlendRangeOverride("TracesColorBlendRangeOverride", Float) = 0
		
		_CoverageDisplacementOffsetOverride("CoverageDisplacementOffsetOverride", Float) = 0
		_DistanceFadeFalloffOverride("DistanceFadeFalloffOverride", Float) = 0
		_DistanceFadeStartOverride("DistanceFadeStartOverride", Float) = 0
		_PaintableCoverageOverride("PaintableCoverageOverride", Float) = 0
		_CoverageTex0Override("CoverageTex0Override", Float) = 0
		_CovMasks0_triBlendContrast("CovMasks0_triBlendContrastOverride", Float) = 0
		_CoverageAmountOverride("CoverageAmountOverride", Float) = 0
		_HeightMap0ContrastOverride("HeightMap0ContrastOverride", Float) = 0
		_PrecipitationDirOffsetOverride("PrecipitationDirOffsetOverride", Float) = 0

		//Sparkle
		_SparkleOverride("SparkleOverride", Float) = 0
		_SssOverride("SssOverride", Float) = 0
		_SparkleTexSSOverride("SparkleTexSSOverride", Float) = 0
		_SparkleTexLSOverride("SparkleTexLSOverride", Float) = 0
		_SparklesAmountOverride("SparklesAmountOverride", Float) = 0
		_SparkleDistFalloffOverride("SparkleDistFalloffOverride", Float) = 0
		_SparkleTexOverride("SparkleTexOverride", Float) = 0
		_LocalSparkleTilingOverride("LocalSparkleTilingOverride", Float) = 0
		_SparklesLightmapMaskPowerOverride("SparklesLightmapMaskPowerOverride", Float) = 0
		_SparklesLightmapMaskPowerOverride("SparklesLightmapMaskPowerOverride", Float) = 0
		_SparklesBrightnessOverride("SparklesBrightnessOverride", Float) = 0
		_SparkleBrightnessRTOverride("SparkleBrightnessRT", Float) = 0
		_ScreenSpaceSparklesTilingOverride("ScreenSpaceSparklesTilingOverride", Float) = 0
		_SparklesHighlightMaskExpansionOverride("SparklesHighlightMaskExpansionOverride", Float) = 0

		_BlendByNormalsPowerOverride("BlendByNormalsPowerOverride", Float) = 0
		_BlendByNormalsStrengthOverride("BlendByNormalsStrengthOverride", Float) = 0
		_CoverageNormalScale0Override("CoverageNormalScaleOverride", Float) = 0
		_PrecipitationDirRangeOverride("PrecipitationDirRangeOverride", Float) = 0
		_CoverageAreaMaskRangeOverride("CoverageAreaMaskRangeOverride", Float) = 0
		_CoverageAreaBiasOverride("CoverageAreaBiasOverride", Float) = 0
		_CoverageLeakReductionOverride("CoverageLeakReductionOverride", Float) = 0
		_CoverageTilingOverride("CoverageTilingOverride", Float) = 0
		_BaseCoverageNormalsBlendOverride("BaseCoverageNormalsBlendOverride", Float) = 0
		_CoverageNormalsOverlayOverride("CoverageNormalsOverlayOverride", Float) = 0
		_MicroReliefFadeOverride("MicroReliefFadeOverride", Float) = 0
		_CoverageMicroReliefOverride("CoverageMicroReliefOverride", Float) = 0
		_CoverageSmoothnessContrastOverride("CoverageSmoothnessContrastOverride", Float) = 0
		_DisplacementOverride("DisplacementOverride", Float) = 0
		_StochasticOverride("StochasticOverride", Float) = 0
		

		_TessellationOverride("TessellationOverride", Float) = 0
		_TessFactorSnowOverride("TessFactorSnowOverride", Float) = 0
		_TessEdgeLOverride("TessEdgeLOverride", Float) = 0
		_TessMaxDispOverride("TessMaxDispOverride", Float) = 0
		_TessSnowdriftRangeOverride("TessSnowdriftRangeOverride", Float) = 0

		_SSS_intensityOverride("SSS_intensityOverride", Float) = 0

		[HideInInspector] _Mode ("__mode", Float) = 0.0
    }

    HLSLINCLUDE

    #pragma multi_compile_fragment __ _ALPHATEST_ON
    #define SRS_SNOW_COVERAGE_SHADER //define this shader as a snow coverage shader
    #define SRS_TERRAIN //define this shader as a terrain shader
    #define _TESSELLATION_ON; //define this shader as a tessellation shader

    ENDHLSL

    SubShader
    {
        Tags { "Queue" = "Geometry-99" "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "UniversalMaterialType" = "Lit" "IgnoreProjector" = "True"}

        Pass
        {
            Name "TerrainAddLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            HLSLPROGRAM

            #pragma require tessHW

            #define SRS_TERRAIN_UNIVERSAL_FORWARD_PASS
            
            #pragma vertex TessellationVertexProgram
			#pragma hull HullProgram
			#pragma domain DomainProgram
            #pragma fragment SplatmapFragment

            // -------------------------------------
            //Weatherade Keywords
            #pragma shader_feature_local _COVERAGE_ON
            #pragma shader_feature_local _PAINTABLE_COVERAGE_ON
            #pragma shader_feature_local _TRACES_ON
            #ifdef _TRACES_ON
                #pragma shader_feature_local _TRACE_DETAIL
            #endif
            #pragma shader_feature_local _DISPLACEMENT_ON 
            #pragma shader_feature_local _STOCHASTIC_ON
            #pragma shader_feature_local_fragment _SSS_ON
            #pragma shader_feature_local_fragment _SPARKLE_ON
            #ifdef _SPARKLE_ON
                #pragma shader_feature_local_fragment _SPARKLE_TEX_SS
                #pragma shader_feature_local_fragment _SPARKLE_TEX_LS
            #endif

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap
            #pragma multi_compile_fragment _ DEBUG_DISPLAY

            #pragma shader_feature_local_fragment _TERRAIN_BLEND_HEIGHT
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _MASKMAP
            // Sample normal in pixel shader when doing instancing
            #pragma shader_feature_local _TERRAIN_INSTANCED_PERPIXEL_NORMAL
            #define TERRAIN_SPLAT_ADDPASS

            #include "../../Includes/SRS_TerrainLitInput.hlsl"
            #include "../../Includes/SRS_TerrainLitPasses.hlsl"
            #include "../../Includes/SRS_Tessellation.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "GBuffer"
            Tags{"LightMode" = "UniversalGBuffer"}

            Blend One One

            HLSLPROGRAM
            #pragma require tessHW

            #define SRS_TERRAIN_UNIVERSAL_GBUFFER_PASS

            // Deferred Rendering Path does not support the OpenGL-based graphics API:
            // Desktop OpenGL, OpenGL ES 3.0, WebGL 2.0.
            #pragma exclude_renderers gles3 glcore

            #pragma vertex TessellationVertexProgram
			#pragma hull HullProgram
			#pragma domain DomainProgram
            #pragma fragment SplatmapFragment

            // -------------------------------------
            //Weatherade Keywords
            #pragma shader_feature_local _COVERAGE_ON
            #pragma shader_feature_local _PAINTABLE_COVERAGE_ON
            #pragma shader_feature_local _TRACES_ON
            #ifdef _TRACES_ON
                #pragma shader_feature_local _TRACE_DETAIL
            #endif
            #pragma shader_feature_local _DISPLACEMENT_ON 
            #pragma shader_feature_local _STOCHASTIC_ON
            #pragma shader_feature_local_fragment _SSS_ON
            #pragma shader_feature_local_fragment _SPARKLE_ON
            #ifdef _SPARKLE_ON
                #pragma shader_feature_local_fragment _SPARKLE_TEX_SS
                #pragma shader_feature_local_fragment _SPARKLE_TEX_LS
            #endif
            
            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            //#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            //#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED

            //#pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap

            #pragma shader_feature_local _TERRAIN_BLEND_HEIGHT
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _MASKMAP
            // Sample normal in pixel shader when doing instancing
            #pragma shader_feature_local _TERRAIN_INSTANCED_PERPIXEL_NORMAL
            #define TERRAIN_SPLAT_ADDPASS 1
            #define TERRAIN_GBUFFER 1

            #include "../../Includes/SRS_TerrainLitInput.hlsl"
            #include "../../Includes/SRS_TerrainLitPasses.hlsl"
            #include "../../Includes/SRS_Tessellation.hlsl"
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
