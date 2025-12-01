Shader "NOT_Lonely/Weatherade/Rain Coverage"
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

        //Other
        _CoverageAreaFalloffHardness("CoverageAreaFalloffHardness", Range( 0 , 1)) = 0.5
		//_PaintedMask("PaintedMask", 2D) = "gray" {}
		//_AlbedoLOD("AlbedoLOD", 2D) = "white" {}
		//_NormalLOD("NormalLOD", 2D) = "bump" {}
		//_MapID("MapID", float) = 0 //needed to set a particular map when baking the distant map
		//[HideInInspector] _TilingMultiplier("TilingMultiplier", Range(0 , 1)) = 1 // needed to set adjust the splats tiling when baking the distant map
		[HideInInspector] _Mode ("__mode", Float) = 0.0
        //-----------------------------------------------   
    }

    HLSLINCLUDE
        #define SRS_RAIN_COVERAGE_SHADER //define this shader as a rain coverage shader
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
            #pragma shader_feature_local_fragment _DRIPS_ON
			#pragma shader_feature_local_fragment _STOCHASTIC_ON
			#pragma shader_feature_local _RIPPLES_ON

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

            #include "../../Includes/SRS_LitInputRain.hlsl"
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

            #include "../../Includes/SRS_LitInputRain.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"

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
            #pragma shader_feature_local_fragment _DRIPS_ON
			#pragma shader_feature_local_fragment _STOCHASTIC_ON
			#pragma shader_feature_local _RIPPLES_ON

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

            #include "../../Includes/SRS_LitInputRain.hlsl"
            #include "../../Includes/SRS_LitGBufferPass.hlsl"

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

            #include "../../Includes/SRS_LitInputRain.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
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
            #pragma shader_feature_local_fragment _DRIPS_ON
			#pragma shader_feature_local_fragment _STOCHASTIC_ON
			#pragma shader_feature_local _RIPPLES_ON
            
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
            #include "../../Includes/SRS_LitInputRain.hlsl"
            #include "../../Includes/SRS_LitDepthNormalsPass.hlsl"

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
            #include "../../Includes/SRS_LitInputRain.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitMetaPass.hlsl"

            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "NOT_Lonely.Weatherade.ShaderGUI.NL_SRS_RainCoverage_GUI"
}
