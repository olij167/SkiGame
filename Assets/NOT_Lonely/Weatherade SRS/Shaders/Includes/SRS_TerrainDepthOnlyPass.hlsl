
#ifndef SRS_TERRAIN_DEPTH_ONLY_INCLUDED
#define SRS_TERRAIN_DEPTH_ONLY_INCLUDED

#include "SRS_CoverageCommon.hlsl" //SRS: common functions
#include "SRS_Lighting.hlsl" //SRS: use this include instead of standard
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GBufferOutput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"


struct Attributes
{
    float4 positionOS     : POSITION;
    float3 normalOS       : NORMAL;
    float3 texcoord     : TEXCOORD0; //SRS: use float3 to store the tess mask in Z
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 clipPos      : SV_POSITION;
    float2 texcoord     : TEXCOORD0;
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

Varyings DepthOnlyVertex(Attributes v)
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

    //SRS: do displacement in world space and then convert from world to clip space (default URP shader converts from local to clip directly).
    //Also make sure that _TERRAIN_INSTANCED_PERPIXEL_NORMAL added to the DepthOnly pass
    float3 posWS = (float3)0;
    #if defined(_COVERAGE_ON) && defined(_DISPLACEMENT_ON)
        posWS = Displace(v.normalOS, v.positionOS.xyz, v.texcoord.xy, 0);
    #else
        posWS = TransformObjectToWorld(v.positionOS.xyz);
    #endif
    o.clipPos = TransformWorldToHClip(posWS);
    //
    o.texcoord = v.texcoord.xy;
    return o;
}

half4 DepthOnlyFragment(Varyings IN) : SV_TARGET
{
#ifdef _ALPHATEST_ON
    ClipHoles(IN.texcoord);
#endif
#ifdef SCENESELECTIONPASS
    // We use depth prepass for scene selection in the editor, this code allow to output the outline correctly
    return half4(_ObjectId, _PassValue, 1.0, 1.0);
#endif
    return IN.clipPos.z;
}

#endif
