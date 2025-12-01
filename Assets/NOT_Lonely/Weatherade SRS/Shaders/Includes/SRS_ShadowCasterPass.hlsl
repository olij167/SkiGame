#ifndef SRS_SHADOW_CASTER_PASS_INCLUDED
#define SRS_SHADOW_CASTER_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

#include "SRS_CoverageCommon.hlsl"

// Shadow Casting Light geometric parameters. These variables are used when applying the shadow Normal Bias and are set by UnityEngine.Rendering.Universal.ShadowUtils.SetupShadowCasterConstantBuffer in com.unity.render-pipelines.universal/Runtime/ShadowUtils.cs
// For Directional lights, _LightDirection is used when applying shadow Normal Bias.
// For Spot lights and Point lights, _LightPosition is used to compute the actual light direction because it is different at each shadow caster geometry vertex.
float3 _LightDirection;
float3 _LightPosition;

struct Attributes
{
    float4 positionOS   : POSITION;
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
    #if defined(_ALPHATEST_ON)
        float2 uv       : TEXCOORD0;
    #endif
    float4 positionCS   : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

#if defined(SRS_SNOW_COVERAGE_SHADER)
    #include "../Includes/SRS_SnowCoverage.hlsl"
#else
    #include "../CGIncludes/SRS_RainCoverage.cginc"
#endif

float4 GetShadowPositionHClip(Attributes input)
{
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

    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

#if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 lightDirectionWS = normalize(_LightPosition - posWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif

    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, normalWS, lightDirectionWS));

#if UNITY_REVERSED_Z
    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#else
    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#endif

    return positionCS;
}

Varyings ShadowPassVertex(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);

    #if defined(_ALPHATEST_ON)
        output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    #endif

    output.positionCS = GetShadowPositionHClip(input);
    return output;
}

half4 ShadowPassFragment(Varyings input) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);

    #if defined(_ALPHATEST_ON)
        Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
    #endif

    #if defined(LOD_FADE_CROSSFADE)
        LODFadeCrossFade(input.positionCS);
    #endif

    return 0;
}

#endif
