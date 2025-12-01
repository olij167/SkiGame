Shader "Hidden/NOT_Lonely/Weatherade/Rain Coverage (Terrain-Bake)"
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
        ///SRS rain shader properties///
        ////////////////////////////////
        [Toggle(_USE_AVERAGED_NORMALS)] _UseAveragedNormals("_UseAveragedNormals", Float) = 0 _UseAveragedNormalsOverride("UseAveragedNormalsOverride", Float) = 0
		[Toggle(_COVERAGE_ON)] _Coverage("Coverage", Float) = 1 _CoverageOverride("CoverageOverride", Float) = 0
		[Toggle(_PAINTABLE_COVERAGE_ON)] _PaintableCoverage("PaintableCoverage", Float) = 0 _PaintableCoverageOverride("PaintableCoverageOverride", Float) = 0
        [Toggle(_STOCHASTIC_ON)] _Stochastic("Stochastic", Float) = 0 _StochasticOverride("StochasticOverride", Float) = 0
        [Toggle(_DRIPS_ON)] _Drips("Drips", Float) = 1 _DripsOverride("DripsOverride", Float) = 0
        [Toggle(_RIPPLES_ON)] _Ripples("Ripples", Float) = 1 _RipplesOverride("RipplesOverride", Float) = 0

        //Wetness
        _WetnessAmount("WetnessAmount", Range( 0 , 1)) = 1 _WetnessAmountOverride("WetnessAmountOverride", Float) = 0
		[NoAlpha]_WetColor("WetColor", Color) = (0.8349056,0.9156185,1,1) _WetColorOverride("WetColorOverride", Float) = 0

        //Puddles
        _PuddlesAmount("PuddlesAmount", Range( 0 , 1)) = 0.5 _PuddlesAmountOverride("PuddlesAmountOverride", Float) = 0
        _PuddlesMult("PuddlesMult", Range(0, 1)) = 0.8 _PuddlesMultOverride("PuddlesMultOverride", Float) = 0
        _PuddlesRange("PuddlesRange", Vector) = (0,1,0,0) _PuddlesRangeOverride("PuddlesRangeOverride", Float) = 0
		_PuddlesTiling("PuddlesTiling", Float) = 1 _PuddlesTilingOverride("PuddlesTilingOverride", Float) = 0
		_PuddlesSlope("PuddlesSlope", Range( 0 , 1)) = 0.05 _PuddlesSlopeOverride("PuddlesSlopeOverride", Float) = 0

        //Ripples and Spots
		_RipplesAmount("RipplesAmount", Range(0, 15)) = 3 _RipplesAmountOverride("RipplesAmountOverride", Float) = 0
		_RipplesIntensity("RipplesIntensity", Float) = 0.5 _RipplesIntensityOverride("RipplesIntensityOverride", Float) = 0
        _RipplesFPS("RipplesFPS", Range( 0 , 120)) = 30 _RipplesFPSOverride("RipplesFPSOverride", Float) = 0
        _RipplesTiling("RipplesTiling", Float) = 1 _RipplesTilingOverride("RipplesTilingOverride", Float) = 0
        _SpotsIntensity("SpotsIntensity", Range( 0 , 5)) = 1 _SpotsIntensityOverride("SpotsIntensityOverride", Float) = 0
		_SpotsAmount("SpotsAmount", Range( 0 , 1)) = 0.85 _SpotsAmountOverride("SpotsAmountOverride", Float) = 0
		_RipplesFramesCount("RipplesFramesCount", Range( 0 , 64)) = 64 _RipplesFramesCountOverride("RipplesFramesCountOverride", Float) = 0

        //Drips
        _DripsIntensity("DripsIntensity", Range( 0 , 5)) = 1 _DripsIntensityOverride("DripsIntensityOverride", Float) = 0
        _DripsSpeed("DripsSpeed", Float) = 0.2 _DripsSpeedOverride("DripsSpeedOverride", Float) = 0
        _DripsTiling("DripsTiling", Vector) = (2.5,1,0,0) _DripsTilingOverride("DripsTilingOverride", Float) = 0
        _DistortionAmount("DistortionAmount", Float) = 0.002 _DistortionAmountOverride("DistortionAmountOverride", Float) = 0
        _DistortionTiling("DistortionTiling", Float) = 2 _DistortionTilingOverride("DistortionTilingOverride", Float) = 0
        _DripsTriBlendContrast("DripsTriBlendContrast", Range(2, 128)) = 12 _DripsTriBlendContrastOverride("DripsTriBlendContrastOverride", Float) = 0

        //Area mask
        _CoverageAreaMaskRange("CoverageAreaMaskRange", Range(0, 1)) = 1 _CoverageAreaMaskRangeOverride("CoverageAreaMaskRangeOverride", Float) = 0
        _CoverageAreaBias("Coverage Area Bias", Range( 0.001 , 0.3)) = 0.001 _CoverageAreaBiasOverride("CoverageAreaBiasOverride", Float) = 0
        _CoverageLeakReduction("CoverageLeakReduction", Range( 0.0 , 0.99)) = 0.0 _CoverageLeakReductionOverride("CoverageLeakReductionOverride", Float) = 0
        _PrecipitationDirOffset("PrecipitationDirOffset", Range( -1 , 1)) = 0 _PrecipitationDirOffsetOverride("PrecipitationDirOffsetOverride", Float) = 0
        _PrecipitationDirRange("PrecipitationDirRange", Vector) = (0,1,0,0) _PrecipitationDirRangeOverride("PrecipitationDirRangeOverride", Float) = 0
        
        //Blend by normals
		_BlendByNormalsStrength("Blend By Normals Strength", Float) = 2 _BlendByNormalsStrengthOverride("BlendByNormalsStrengthOverride", Float) = 0
		_BlendByNormalsPower("Blend By Normals Power", Float) = 5 _BlendByNormalsPowerOverride("BlendByNormalsPowerOverride", Float) = 0

        //Distance fade
        _DistanceFadeStart("DistanceFadeStart", Float) = 150 _DistanceFadeStartOverride("DistanceFadeStartOverride", Float) = 0
		_DistanceFadeFalloff("DistanceFadeFalloff", Float) = 1 _DistanceFadeFalloffOverride("DistanceFadeFalloffOverride", Float) = 0

        //Other
        _CoverageAreaFalloffHardness("CoverageAreaFalloffHardness", Range( 0 , 1)) = 0.5
		_PaintedMask("PaintedMask", 2D) = "gray" {}
		_AlbedoLOD("AlbedoLOD", 2D) = "white" {}
		_NormalLOD("NormalLOD", 2D) = "bump" {}
		_MapID("MapID", float) = 0 //needed to set a particular map when baking the distant map
		[HideInInspector] _TilingMultiplier("TilingMultiplier", Range(0 , 1)) = 1 // needed to set adjust the splats tiling when baking the distant map
		[HideInInspector] _Mode ("__mode", Float) = 0.0
        //-----------------------------------------------  
    }

    HLSLINCLUDE

    #pragma multi_compile_fragment __ _ALPHATEST_ON
    #define SRS_RAIN_COVERAGE_SHADER //define this shader as a rain coverage shader
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
            #pragma target 4.0

            #pragma vertex SplatmapVert
            #pragma fragment SplatmapFragment

            #define _METALLICSPECGLOSSMAP 1
            #define _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A 1

            // -------------------------------------
            //Weatherade Keywords
            #pragma shader_feature_local _COVERAGE_ON
			#pragma shader_feature_local_fragment _PAINTABLE_COVERAGE_ON
			#pragma shader_feature_local_fragment _STOCHASTIC_ON

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

            #include "../../Includes/SRS_TerrainLitInputRain.hlsl"
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

            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap

            #include "Packages/com.unity.render-pipelines.universal/Shaders/Terrain/TerrainLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Terrain/TerrainLitPasses.hlsl"
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
			#pragma shader_feature_local_fragment _PAINTABLE_COVERAGE_ON
			#pragma shader_feature_local_fragment _STOCHASTIC_ON

            #pragma shader_feature_local _NORMALMAP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap

            #include "../../Includes/SRS_TerrainLitInputRain.hlsl"
            #include "../../Includes/SRS_TerrainBake.hlsl"
            #include "../../Includes/SRS_TerrainLitDepthNormalsPass.hlsl"
            ENDHLSL
        }
        
        UsePass "Hidden/Nature/Terrain/Utilities/PICKING"
    }
    Dependency "AddPassShader" = "Hidden/NOT_Lonely/Weatherade/RainTerrainBakeAddPass"
    Dependency "BaseMapShader" = "Hidden/Universal Render Pipeline/Terrain/Lit (Base Pass)"
    Dependency "BaseMapGenShader" = "Hidden/Universal Render Pipeline/Terrain/Lit (Basemap Gen)"

    CustomEditor "UnityEditor.Rendering.Universal.TerrainLitShaderGUI"

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
