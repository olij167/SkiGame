#ifndef SRS_DEPTH_ONLY_PASS_INCLUDED
#define SRS_DEPTH_ONLY_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "SRS_CoverageCommon.hlsl"

struct Attributes
{
    float4 positionOS     : POSITION;
    float3 normalOS     : NORMAL;
    //SRS
    float4 color : COLOR0; // SRS: vertex color needed for the Paintable Coverage feature
    #if defined(_USE_AVERAGED_NORMALS)
        half3 unifiedNormal : TEXCOORD3;
    #endif
    //
    float3 texcoord     : TEXCOORD0; //SRS: change float2 to float3 to store a tess mask in Z
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 uv           : TEXCOORD0;
    float4 positionCS   : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

#if defined(SRS_SNOW_COVERAGE_SHADER)
    #include "../Includes/SRS_SnowCoverage.hlsl"
#else
    #include "../CGIncludes/SRS_RainCoverage.cginc"
#endif

Varyings DepthOnlyVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    output.uv = TRANSFORM_TEX(input.texcoord.xy, _BaseMap); //SRS: use only XY since Z is used for the tess mask
    //SRS: do displacement in world space and then convert from world to clip space (default URP shader converts from local to clip directly)
    float3 posWS = (float3)0;
    #if defined(_COVERAGE_ON) && defined(_DISPLACEMENT_ON)
        
        half3 n = 0;
        #if defined (_USE_AVERAGED_NORMALS)
            n = input.unifiedNormal;
        #else
            n = input.normalOS;
        #endif

        posWS = Displace(n, input.positionOS.xyz, 0, input.color);
    #else
        posWS = TransformObjectToWorld(input.positionOS.xyz);
    #endif
    //
    output.positionCS = TransformWorldToHClip(posWS);
    return output;
}

half4 DepthOnlyFragment(Varyings input) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
    return 0;
}
#endif