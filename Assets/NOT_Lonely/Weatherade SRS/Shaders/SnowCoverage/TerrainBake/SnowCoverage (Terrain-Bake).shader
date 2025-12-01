Shader "Hidden/NOT_Lonely/Weatherade/Snow Coverage (Terrain-Bake)"
{
    Properties
    {
        [HideInInspector] [ToggleUI] _EnableHeightBlend("EnableHeightBlend", Float) = 0.0
        _HeightTransition("Height Transition", Range(0, 1.0)) = 0.0
        // Layer count is passed down to guide height-blend enable/disable, due
        // to the fact that heigh-based blend will be broken with multipass.
        [HideInInspector] [PerRendererData] _NumLayersCount ("Total Layer Count", Float) = 1.0

        // set by terrain engine
        [HideInInspector] _Control("Control (RGBA)", 2D) = "red" {}
        [HideInInspector] _Splat3("Layer 3 (A)", 2D) = "grey" {}
        [HideInInspector] _Splat2("Layer 2 (B)", 2D) = "grey" {}
        [HideInInspector] _Splat1("Layer 1 (G)", 2D) = "grey" {}
        [HideInInspector] _Splat0("Layer 0 (R)", 2D) = "grey" {}
        [HideInInspector] _Normal3("Normal 3 (A)", 2D) = "bump" {}
        [HideInInspector] _Normal2("Normal 2 (B)", 2D) = "bump" {}
        [HideInInspector] _Normal1("Normal 1 (G)", 2D) = "bump" {}
        [HideInInspector] _Normal0("Normal 0 (R)", 2D) = "bump" {}
        [HideInInspector] _Mask3("Mask 3 (A)", 2D) = "grey" {}
        [HideInInspector] _Mask2("Mask 2 (B)", 2D) = "grey" {}
        [HideInInspector] _Mask1("Mask 1 (G)", 2D) = "grey" {}
        [HideInInspector] _Mask0("Mask 0 (R)", 2D) = "grey" {}
        [HideInInspector][Gamma] _Metallic0("Metallic 0", Range(0.0, 1.0)) = 0.0
        [HideInInspector][Gamma] _Metallic1("Metallic 1", Range(0.0, 1.0)) = 0.0
        [HideInInspector][Gamma] _Metallic2("Metallic 2", Range(0.0, 1.0)) = 0.0
        [HideInInspector][Gamma] _Metallic3("Metallic 3", Range(0.0, 1.0)) = 0.0
        [HideInInspector] _Smoothness0("Smoothness 0", Range(0.0, 1.0)) = 0.5
        [HideInInspector] _Smoothness1("Smoothness 1", Range(0.0, 1.0)) = 0.5
        [HideInInspector] _Smoothness2("Smoothness 2", Range(0.0, 1.0)) = 0.5
        [HideInInspector] _Smoothness3("Smoothness 3", Range(0.0, 1.0)) = 0.5

        // used in fallback on old cards & base map
        [HideInInspector] _MainTex("BaseMap (RGB)", 2D) = "grey" {}
        [HideInInspector] _BaseColor("Main Color", Color) = (1,1,1,1)

        [HideInInspector] _TerrainHolesTexture("Holes Map (RGB)", 2D) = "white" {}

        [ToggleUI] _EnableInstancedPerPixelNormal("Enable Instanced per-pixel normal", Float) = 1.0

		////////////////////////////////
        ///SRS snow shader properties///
        ////////////////////////////////
		[Toggle(_COVERAGE_ON)] _Coverage("Coverage", Float) = 1 _CoverageOverride("CoverageOverride", Float) = 0
		[Toggle(_PAINTABLE_COVERAGE_ON)] _PaintableCoverage("PaintableCoverage", Float) = 0 _PaintableCoverageOverride("PaintableCoverageOverride", Float) = 0
        [Toggle(_THREE_TEX_MODE)] _ThreeTexMode("ThreeTexMode", Float) = 0 _ThreeTexModeOverride("ThreeTexModeOverride", Float) = 0
        [Toggle(_USE_COVERAGE_DETAIL)] _UseCoverageDetail("UseCoverageDetail", Float) = 0 _UseCoverageDetailOverride("UseCoverageDetailOverride", Float) = 0
		[Toggle(_SPARKLE_ON)] _Sparkle("Sparkle", Float) = 0 _SparkleOverride("SparkleOverride", Float) = 0
		[Toggle(_SSS_ON)] _Sss("Sparkle", Float) = 0 _SssOverride("SssOverride", Float) = 0
		[Toggle(_SPARKLE_TEX_SS)] _SparkleTexSS("Sparkle Tex SS", Float) = 0 _SparkleTexSSOverride("SparkleTexSSOverride", Float) = 0
		[Toggle(_SPARKLE_TEX_LS)] _SparkleTexLS("Sparkle Tex LS", Float) = 0 _SparkleTexLSOverride("SparkleTexLSOverride", Float) = 0
		[Toggle(_DISPLACEMENT_ON)] _Displacement("Displacement", Float) = 0 _DisplacementOverride("DisplacementOverride", Float) = 0 
		[Toggle(_STOCHASTIC_ON)] _Stochastic("Stochastic", Float) = 0 _StochasticOverride("StochasticOverride", Float) = 0
        [Toggle(_TRACES_ON)] _Traces("Traces", Float) = 0  _TracesOverride("TracesOverride", Float) = 0
		[Toggle(_TRACE_DETAIL)] _TraceDetail("TraceDetail", Float) = 0 _TraceDetailOverride("TraceDetailOverride", Float) = 0

        //Basic coverage settings
        _CoverageAmount("CoverageAmount", Range( 0 , 1)) = 1 _CoverageAmountOverride("CoverageAmountOverride", Float) = 0

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
		
        //Distance fade
        _DistanceFadeStart("DistanceFadeStart", Float) = 150 _DistanceFadeStartOverride("DistanceFadeStartOverride", Float) = 0
		_DistanceFadeFalloff("DistanceFadeFalloff", Float) = 1 _DistanceFadeFalloffOverride("DistanceFadeFalloffOverride", Float) = 0

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
		//_AlbedoLOD("AlbedoLOD", 2D) = "white" {}
		//_NormalLOD("NormalLOD", 2D) = "bump" {}
		_MapID("MapID", float) = 0 //needed to set a particular map when baking the distant map
		[HideInInspector] _TilingMultiplier("TilingMultiplier", Range(0 , 1)) = 1 // needed to set adjust the splats tiling when baking the distant map
		[HideInInspector] _Mode ("__mode", Float) = 0.0
        //-----------------------------------------------
    }

    HLSLINCLUDE

    #pragma multi_compile_fragment __ _ALPHATEST_ON
    #define SRS_SNOW_COVERAGE_SHADER //define this shader as a snow coverage shader
    #define SRS_TERRAIN //define this shader as a terrain shader
    #define SRS_TERRAIN_BAKE_SHADER

    ENDHLSL

    SubShader
    {
        Tags { "Queue" = "Geometry-100" "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "UniversalMaterialType" = "Lit" "IgnoreProjector" = "False" "TerrainCompatible" = "True"}

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0

            #pragma vertex SplatmapVert
            #pragma fragment SplatmapFragment

            #define _METALLICSPECGLOSSMAP 1
            #define _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A 1

            // -------------------------------------
            //Weatherade Keywords
            #pragma shader_feature_local _COVERAGE_ON
            #pragma shader_feature_local _PAINTABLE_COVERAGE_ON
            #pragma shader_feature_local _DISPLACEMENT_ON 
            #pragma shader_feature_local _STOCHASTIC_ON
            #pragma shader_feature_local _THREE_TEX_MODE

            // -------------------------------------
            // Universal Pipeline keywords

            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap

            #pragma shader_feature_local_fragment _TERRAIN_BLEND_HEIGHT
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _MASKMAP
            // Sample normal in pixel shader when doing instancing
            #pragma shader_feature_local _TERRAIN_INSTANCED_PERPIXEL_NORMAL

            #include "../../Includes/SRS_TerrainLitInput.hlsl"
            #include "../../Includes/SRS_TerrainBake.hlsl"
            #include "../../Includes/SRS_TerrainLitPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags{"LightMode" = "DepthOnly"}

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 2.0

            #pragma shader_feature_local _TERRAIN_INSTANCED_PERPIXEL_NORMAL //SRS: add this keyword to the DepthOnly pass to make the shader render the depth with the correct displacement

            //Weatherade Keywords
            #pragma shader_feature_local _COVERAGE_ON
            #pragma shader_feature_local _PAINTABLE_COVERAGE_ON
            #pragma shader_feature_local _DISPLACEMENT_ON 
            #pragma shader_feature_local _STOCHASTIC_ON
            #pragma shader_feature_local _THREE_TEX_MODE

            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap

            #include "../../Includes/SRS_TerrainLitInput.hlsl"
            #include "../../Includes/SRS_TerrainBake.hlsl"
            #include "../../Includes/SRS_TerrainDepthOnlyPass.hlsl"
            ENDHLSL
        }

		 // This pass is used when drawing to a _CameraNormalsTexture texture
        Pass
        {
            Name "DepthNormals"
            Tags{"LightMode" = "DepthNormals"}

            ZWrite On

            HLSLPROGRAM
            #pragma target 2.0
            #pragma shader_feature_local_vertex _TERRAIN_INSTANCED_PERPIXEL_NORMAL //SRS: add this keyword to the DepthNormals pass to make the shader render the DepthNormals with the correct displacement

            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalOnlyFragment

            // -------------------------------------
            //Weatherade Keywords
            #pragma shader_feature_local _COVERAGE_ON
            #pragma shader_feature_local _PAINTABLE_COVERAGE_ON
            #pragma shader_feature_local _DISPLACEMENT_ON 
            #pragma shader_feature_local _STOCHASTIC_ON
            #pragma shader_feature_local _THREE_TEX_MODE
            //

            #pragma shader_feature_local _NORMALMAP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap

            #include "../../Includes/SRS_TerrainLitInput.hlsl"
			#include "../../Includes/SRS_TerrainBake.hlsl"
            #include "../../Includes/SRS_TerrainLitDepthNormalsPass.hlsl"
            ENDHLSL
        }

        UsePass "Hidden/Nature/Terrain/Utilities/PICKING"
    }
    Dependency "AddPassShader" = "Hidden/NOT_Lonely/Weatherade/SnowCoverageTerrain (AddPass)"
    Dependency "BaseMapShader" = "Hidden/Universal Render Pipeline/Terrain/Lit (Base Pass)"
    Dependency "BaseMapGenShader" = "Hidden/Universal Render Pipeline/Terrain/Lit (Basemap Gen)"

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
