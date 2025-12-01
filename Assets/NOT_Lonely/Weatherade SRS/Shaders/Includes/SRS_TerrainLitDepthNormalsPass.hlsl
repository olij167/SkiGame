#ifndef SRS_DEPTH_NORMALS_PASS_INCLUDED
#define SRS_DEPTH_NORMALS_PASS_INCLUDED

#include "SRS_CoverageCommon.hlsl" //SRS: common functions
#if defined(SRS_SNOW_COVERAGE_SHADER)
    #include "SRS_Lighting.hlsl" //SRS: use this include instead of standard for snow shaders
#elif defined(SRS_RAIN_COVERAGE_SHADER)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#endif
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"

// DepthNormal pass
struct Attributes
{
    float4 positionOS : POSITION;
    half3 normalOS : NORMAL;
    float3 texcoord : TEXCOORD0; //SRS: use float3 to store the tess mask in Z
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    //SRS: add requred interpolator for the SnowCoverage function
    #if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    float3 positionWS               : TEXCOORD6;
    #endif
    //

    float4 uvMainAndLM              : TEXCOORD0; // xy: control, zw: lightmap
    #ifndef TERRAIN_SPLAT_BASEPASS
        float4 uvSplat01                : TEXCOORD1; // xy: splat0, zw: splat1
        float4 uvSplat23                : TEXCOORD2; // xy: splat2, zw: splat3
    #endif

    #if defined(_NORMALMAP) && !defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
        half4 normal                   : TEXCOORD3;    // xyz: normal, w: viewDir.x
        half4 tangent                  : TEXCOORD4;    // xyz: tangent, w: viewDir.y
        half4 bitangent                : TEXCOORD5;    // xyz: bitangent, w: viewDir.z
    #else
        half3 normal                   : TEXCOORD3;
    #endif

    float4 clipPos                  : SV_POSITION;
    UNITY_VERTEX_OUTPUT_STEREO

    //SRS: add instanceID to support tessellation
    #if defined(_TESSELLATION_ON)
        UNITY_VERTEX_INPUT_INSTANCE_ID
    #endif
};

//SRS includes

#if defined(SRS_SNOW_COVERAGE_SHADER)
    #include "SRS_SnowCoverage.hlsl"
#elif defined(SRS_RAIN_COVERAGE_SHADER)
    #include "SRS_RainCoverage.hlsl"
#endif

Varyings DepthNormalsVertex(Attributes v)
{
    Varyings o = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(v);
    #if defined(_TESSELLATION_ON)
        UNITY_TRANSFER_INSTANCE_ID(v, o);
    #endif
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    //SRS: do instancing in this shader stage only if it's not a tessellation shader
    #if !defined(_TESSELLATION_ON)
        TerrainInstancing(v.positionOS, v.normalOS, v.texcoord.xy);
    #endif
    
    //SRS: replace standard GetVertexPositionInputs function. The Displace function is inside it.
    #if defined(SRS_SNOW_COVERAGE_SHADER)
        VertexPositionInputs attributes = NL_GetVertexPositionInputs(v);
    #elif defined(SRS_RAIN_COVERAGE_SHADER)
        VertexPositionInputs attributes = GetVertexPositionInputs(v.positionOS.xyz);
    #endif
    //
    //SRS: interpolate positionWS as it's needed for the SnowCoverage function
    #if defined(_COVERAGE_ON)
        o.positionWS = attributes.positionWS;
    #endif

    o.uvMainAndLM.xy = v.texcoord.xy;
    o.uvMainAndLM.zw = v.texcoord.xy * unity_LightmapST.xy + unity_LightmapST.zw;
    #ifndef TERRAIN_SPLAT_BASEPASS
        o.uvSplat01.xy = TRANSFORM_TEX(v.texcoord.xy, _Splat0);
        o.uvSplat01.zw = TRANSFORM_TEX(v.texcoord.xy, _Splat1);
        o.uvSplat23.xy = TRANSFORM_TEX(v.texcoord.xy, _Splat2);
        o.uvSplat23.zw = TRANSFORM_TEX(v.texcoord.xy, _Splat3);
    #endif

    #if defined(_NORMALMAP) && !defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
        half3 viewDirWS = GetWorldSpaceNormalizeViewDir(attributes.positionWS);
        float4 vertexTangent = float4(cross(float3(0, 0, 1), v.normalOS), 1.0);
        VertexNormalInputs normalInput = GetVertexNormalInputs(v.normalOS, vertexTangent);

        o.normal = half4(normalInput.normalWS, viewDirWS.x);
        o.tangent = half4(normalInput.tangentWS, viewDirWS.y);
        o.bitangent = half4(normalInput.bitangentWS, viewDirWS.z);
    #else
        o.normal = TransformObjectToWorldNormal(v.normalOS);
    #endif

    o.clipPos = attributes.positionCS;
    return o;
}

void DepthNormalOnlyFragment(
    Varyings IN
    , out half4 outNormalWS : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out float4 outRenderingLayers : SV_Target1
#endif
    )
{
    #ifdef _ALPHATEST_ON
        ClipHoles(IN.uvMainAndLM.xy);
    #endif

    float2 splatUV = (IN.uvMainAndLM.xy * (_Control_TexelSize.zw - 1.0f) + 0.5f) * _Control_TexelSize.xy;
    half4 splatControl = SAMPLE_TEXTURE2D(_Control, sampler_Control, splatUV);

    half3 normalTS = half3(0.0h, 0.0h, 1.0h);
    NormalMapMix(IN.uvSplat01, IN.uvSplat23, splatControl, normalTS);

    #if defined(_NORMALMAP) && !defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
        half3 normalWS = TransformTangentToWorld(normalTS, half3x3(-IN.tangent.xyz, IN.bitangent.xyz, IN.normal.xyz));
    #elif defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
        float2 sampleCoords = (IN.uvMainAndLM.xy / _TerrainHeightmapRecipSize.zw + 0.5f) * _TerrainHeightmapRecipSize.xy;
        half3 normalWS = TransformObjectToWorldNormal(normalize(SAMPLE_TEXTURE2D(_TerrainNormalmapTexture, sampler_TerrainNormalmapTexture, sampleCoords).rgb * 2 - 1));
        half3 tangentWS = cross(GetObjectToWorldMatrix()._13_23_33, normalWS);
        normalWS = TransformTangentToWorld(normalTS, half3x3(-tangentWS, cross(normalWS, tangentWS), normalWS));
    #else
        half3 normalWS = IN.normal;
    #endif

    normalWS = NormalizeNormalPerPixel(normalWS);

    //SRS: call SnowCoverage here to ensure that proper normals will be used
    #if defined(_COVERAGE_ON)
        half3 albedo = 0;
        half smoothness = 0;
        #if defined(SRS_SNOW_COVERAGE_SHADER)
            half snowMask = 0;
            half rawSmoothness = 0;
            half metallic = 0;
            half occlusion = 1;
            half alpha = 1;
            half sssMask = 0;
            half highlightBrightness = 0;
            half dither = DitherAnimated(GetNormalizedScreenSpaceUV(IN.clipPos));
            SnowCoverage(normalWS, IN.positionWS, albedo, metallic, smoothness, rawSmoothness, occlusion, alpha, IN.normal.xyz, snowMask, half4(0,0,0,0), sssMask, highlightBrightness, IN.uvMainAndLM.xy, dither);
        #elif defined(SRS_RAIN_COVERAGE_SHADER)
            half4 tangent = half4(1, 0, 0, 1);
            RainCoverage(normalWS, IN.positionWS, albedo, smoothness, IN.uvMainAndLM.xy, 0, splatUV, IN.normal.xyz, 0, tangent);
        #endif
    #endif

    outNormalWS = half4(normalWS, 0.0);

    #ifdef _WRITE_RENDERING_LAYERS
        uint renderingLayers = GetMeshRenderingLayer();
        outRenderingLayers = float4(EncodeMeshRenderingLayer(renderingLayers), 0, 0, 0);
    #endif
}

#endif
