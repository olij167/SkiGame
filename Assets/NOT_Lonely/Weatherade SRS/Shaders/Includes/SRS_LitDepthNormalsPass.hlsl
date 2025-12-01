#ifndef SRS_DEPTH_NORMALS_PASS_INCLUDED
#define SRS_DEPTH_NORMALS_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"
#include "../Includes/SRS_CoverageCommon.hlsl"

#if defined(_DETAIL_MULX2) || defined(_DETAIL_SCALED)
#define _DETAIL
#endif

// GLES2 has limited amount of interpolators
#if defined(_PARALLAXMAP) && !defined(SHADER_API_GLES)
#define REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR
#endif

#if (defined(_NORMALMAP) || (defined(_PARALLAXMAP) && !defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR))) || defined(_DETAIL) || defined(_DRIPS_ON) //SRS: define if drips feature enabled
#define REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR
#endif

#if defined(_ALPHATEST_ON) || defined(_PARALLAXMAP) || defined(_NORMALMAP) || defined(_DETAIL) || defined(SRS_RAIN_COVERAGE_SHADER)
#define REQUIRES_UV_INTERPOLATOR
#endif

struct Attributes
{
    float4 positionOS     : POSITION;
    float4 tangentOS      : TANGENT;
    //SRS: use float3 to store the tess mask in Z
    #if defined (_TESSELLATION_ON)
        float3 texcoord     : TEXCOORD0; //SRS: use float3 to store the tess mask in Z
    #else
        float2 texcoord     : TEXCOORD0;
    #endif
    float3 normalOS       : NORMAL; //SRS: changed variable name from normal to normalOS
    //SRS
    float4 color : COLOR0; // SRS: vertex color needed for the Paintable Coverage feature
    #if defined(_USE_AVERAGED_NORMALS)
        half3 unifiedNormal : TEXCOORD3;
    #endif
    //
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    //SRS
    #if defined (_COVERAGE_ON)
        float4 color : COLOR0; // SRS: vertex color needed for the Paintable Coverage feature
        #if defined(_USE_AVERAGED_NORMALS)
                half3 unifiedWorldNormal : COLOR1;
        #endif 
    #endif
    //
    //SRS: add requred interpolator for the SnowCoverage function
    #if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    float3 positionWS               : TEXCOORD3;
    #endif
    //

    float4 positionCS   : SV_POSITION;
    #if defined(REQUIRES_UV_INTERPOLATOR)
        float2 uv       : TEXCOORD1;
    #endif
    float3 normalWS     : TEXCOORD2;

    #if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
    half4 tangentWS    : TEXCOORD4;    // xyz: tangent, w: sign
    #endif

    half3 viewDirWS    : TEXCOORD5;

    #if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    half3 viewDirTS    : TEXCOORD8;
    #endif

    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

#if defined(SRS_SNOW_COVERAGE_SHADER)
    #include "SRS_SnowCoverage.hlsl"
#elif defined(SRS_RAIN_COVERAGE_SHADER)
    #include "SRS_RainCoverage.hlsl"
#endif

Varyings DepthNormalsVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    #if defined(REQUIRES_UV_INTERPOLATOR)
        output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    #endif

    //SRS
    #if defined(_COVERAGE_ON)
        #if defined(_USE_AVERAGED_NORMALS)
            VertexNormalInputs normalInput = NL_GetVertexNormalInputs(input.normalOS, input.unifiedNormal, input.tangentOS);
            output.unifiedWorldNormal = TransformObjectToWorldNormal(input.unifiedNormal);
        #else
            VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
        #endif

        #if defined (_PAINTABLE_COVERAGE_ON)
            output.color = input.color;
        #endif
    #else
        VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    #endif
    #if defined(SRS_SNOW_COVERAGE_SHADER)
        VertexPositionInputs vertexInput = NL_GetVertexPositionInputs(input);
    #elif defined(SRS_RAIN_COVERAGE_SHADER)
        VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    #endif
    
    #if defined(_COVERAGE_ON)
    output.positionWS = vertexInput.positionWS;
    #endif
    //

    output.positionCS = TransformWorldToHClip(vertexInput.positionWS);
    output.normalWS = normalInput.normalWS;

    #if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR) || defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
        float sign = input.tangentOS.w * float(GetOddNegativeScale());
        half4 tangentWS = half4(normalInput.tangentWS.xyz, sign);
    #endif

    #if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
        output.tangentWS = tangentWS;
    #endif

    #if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
        half3 viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);
        half3 viewDirTS = GetViewDirectionTangentSpace(tangentWS, output.normalWS, viewDirWS);
        output.viewDirTS = viewDirTS;
    #endif

    return output;
}

void DepthNormalsFragment(
    Varyings input
    , out half4 outNormalWS : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out float4 outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    #if defined(_ALPHATEST_ON)
        Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
    #endif

    #if defined(LOD_FADE_CROSSFADE)
        LODFadeCrossFade(input.positionCS);
    #endif

    #if defined(_GBUFFER_NORMALS_OCT)
        float3 normalWS = normalize(input.normalWS);

        float2 octNormalWS = PackNormalOctQuadEncode(normalWS);           // values between [-1, +1], must use fp32 on some platforms.
        float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);   // values between [ 0,  1]
        half3 normalWS = PackFloat2To888(remappedOctNormalWS);      // values between [ 0,  1]
        //SRS: outNormalWS moved down
    #else
        #if defined(_PARALLAXMAP)
            #if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
                half3 viewDirTS = input.viewDirTS;
            #else
                half3 viewDirTS = GetViewDirectionTangentSpace(input.tangentWS, input.normalWS, input.viewDirWS);
            #endif
            ApplyPerPixelDisplacement(viewDirTS, input.uv);
        #endif

        #if defined(_NORMALMAP) || defined(_DETAIL)
            float sgn = input.tangentWS.w;      // should be either +1 or -1
            float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
            float3 normalTS = SampleNormal(input.uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);

            #if defined(_DETAIL)
                half detailMask = SAMPLE_TEXTURE2D(_DetailMask, sampler_DetailMask, input.uv).a;
                float2 detailUv = input.uv * _DetailAlbedoMap_ST.xy + _DetailAlbedoMap_ST.zw;
                normalTS = ApplyDetailNormal(detailUv, normalTS, detailMask);
            #endif

            float3 normalWS = TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz));
        #else
            float3 normalWS = input.normalWS;
        #endif
        //SRS: outNormalWS moved down
    #endif

    //SRS: call SnowCoverage here to ensure that proper normals will be used
    #if defined(_COVERAGE_ON)
        half3 albedo = 0;
        half metallic = 0;
        half smoothness = 0;
        half occlusion = 1;
        half alpha = 1;
        half dither = DitherAnimated(GetNormalizedScreenSpaceUV(input.positionCS));

        #if defined(SRS_SNOW_COVERAGE_SHADER)
            half snowMask = 0;
            half rawSmoothness = 0;
            half sssMask = 0;
            half highlightBrightness = 0;
            SnowCoverage(normalWS, input.positionWS, albedo, metallic, smoothness, rawSmoothness, occlusion, alpha, input.normalWS, snowMask, input.color, sssMask, highlightBrightness, 0, dither);
        #elif defined(SRS_RAIN_COVERAGE_SHADER)
            #if defined(_USE_AVERAGED_NORMALS)
                half3 unifiedNormal = input.unifiedWorldNormal;
            #else
                half3 unifiedNormal = 0;
            #endif 

            #if defined(_DRIPS_ON)
                half4 tangentWS = input.tangentWS;
            #else
                half4 tangentWS = 0;
            #endif
            
            RainCoverage(normalWS, input.positionWS, albedo, smoothness, 0, input.color, 0, input.normalWS, unifiedNormal, tangentWS);
        #endif
    #endif

    outNormalWS = half4(NormalizeNormalPerPixel(normalWS), 0.0); //SRS: moved this line from the #if defined(_GBUFFER_NORMALS_OCT)

    #ifdef _WRITE_RENDERING_LAYERS
        uint renderingLayers = GetMeshRenderingLayer();
        outRenderingLayers = float4(EncodeMeshRenderingLayer(renderingLayers), 0, 0, 0);
    #endif
}
#endif
