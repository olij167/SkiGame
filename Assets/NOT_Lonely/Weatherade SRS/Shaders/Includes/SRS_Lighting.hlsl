#ifndef NL_LIGHTING_INCLUDED
#define NL_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "SRS_LightingCommon.hlsl"

#ifndef REQUIRES_WORLD_SPACE_POS_INTERPOLATOR
    #define REQUIRES_WORLD_SPACE_POS_INTERPOLATOR
#endif

/*
#if defined(_SPARKLE_ON)
half3 CalcSparkle(InputData inputData, LightingData lightingData, float3 mainLightDir, half sparkleTexMask, float sm, float ssTiling, float amount, float brightness, float expansion)
{
    half highlightMask = 1;

    highlightMask = dot(normalize(mainLightDir + inputData.viewDirectionWS), inputData.normalWS);
    highlightMask = pow(max(highlightMask, 0.0001), (0.999 - expansion) * 128);

    float2 ssUV = inputData.normalizedScreenSpaceUV;
    float ratio = _ScreenParams.x / _ScreenParams.y;
    ssUV.x = ssUV.x * ratio;
    ssUV += mainLightDir.xz; // make sparkle uv depend on main light direction

    #if UNITY_SINGLE_PASS_STEREO
        float4 scaleOffset = unity_StereoScaleOffset[unity_StereoEyeIndex];
        ssUV = (ssUV - scaleOffset.zw) / scaleOffset.xy;
    #endif

    ssUV *= ssTiling;

    half screenSpaceSparkle = SAMPLE_TEXTURE2D(_SparkleTex, sampler_SparkleTex, ssUV).r;
    half mix = sparkleTexMask * screenSpaceSparkle;

    half ampSm = pow(sm, 2) * _SparklesBrightness * 0.3;
    
    half3 finalSparkle = 0;
    half sparkleMask = step(1 - amount, mix) * brightness; //add sparkles
    sparkleMask += step(1 - amount * 0.9, mix) * brightness * 0.05; //add overall sparkles
    sparkleMask += ampSm * pow(highlightMask, 2) * amount * 2; //increase local space highlight 
    
    finalSparkle += lightingData.mainLightColor * sparkleMask.xxx * highlightMask; //main light sparkle
    finalSparkle += lightingData.mainLightColor * sparkleMask.xxx * 0.2;
    finalSparkle += lightingData.additionalLightsColor * sparkleMask.xxx; //additional light sparkle
    return finalSparkle;
}
#endif

#if defined(_SSS_ON)
float3 SSS(InputData inputData, LightingData lightingData, float3 mainLightDir, half snowMask, float distFadeMask, float intensity)    
{
    half effectsMask = 1;
    #if defined(LIGHTMAP_ON)
	    #if defined(LIGHTMAP_SHADOW_MIXING) && !defined(SHADOWS_SHADOWMASK)
            effectsMask = AdjustBrightnessContrast(effectsMask, 0.5, 2);
	    #endif
    #else
        effectsMask = 1;
    #endif

    float3 finalSSS = 0;
    mainLightDir *= 2;
    float sssMask = saturate(dot(-normalize(inputData.normalWS + mainLightDir), inputData.viewDirectionWS)) * effectsMask;
    sssMask = pow(sssMask, 2) * intensity * (1 - distFadeMask);
    finalSSS += lightingData.mainLightColor * sssMask;
    finalSSS += lightingData.additionalLightsColor * sssMask;
    finalSSS *= snowMask;
    return finalSSS;
}
#endif
*/

////////////////////////////////////////////////////////////////////////////////
/// PBR lighting...
////////////////////////////////////////////////////////////////////////////////
half4 NL_UniversalFragmentPBR(InputData inputData, SurfaceData surfaceData, half snowMask, half sssMask, half colorEnhanceMask, half highlightBrightness) //SRS: added snowMask, that is used for the sparkle and SSS effects
{   
    #if defined(_SPECULARHIGHLIGHTS_OFF)
    bool specularHighlightsOff = true;
    #else
    bool specularHighlightsOff = false;
    #endif
    BRDFData brdfData;

    // NOTE: can modify "surfaceData"...
    InitializeBRDFData(surfaceData, brdfData);

    #if defined(DEBUG_DISPLAY)
    half4 debugColor;

    if (CanDebugOverrideOutputColor(inputData, surfaceData, brdfData, debugColor))
    {
        return debugColor;
    }
    #endif

    // Clear-coat calculation...
    BRDFData brdfDataClearCoat = CreateClearCoatBRDFData(surfaceData, brdfData);
    half4 shadowMask = CalculateShadowMask(inputData);
    AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData, surfaceData);
    uint meshRenderingLayers = GetMeshRenderingLayer();
    Light mainLight = GetMainLight(inputData, shadowMask, aoFactor);

    // NOTE: We don't apply AO to the GI here because it's done in the lighting calculation below...
    MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI);

    LightingData lightingData = CreateLightingData(inputData, surfaceData);

    lightingData.giColor = GlobalIllumination(brdfData, brdfDataClearCoat, surfaceData.clearCoatMask,
                                              inputData.bakedGI, aoFactor.indirectAmbientOcclusion, inputData.positionWS,
                                              inputData.normalWS, inputData.viewDirectionWS, inputData.normalizedScreenSpaceUV);
#ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
#endif
    {
        lightingData.mainLightColor = LightingPhysicallyBased(brdfData, brdfDataClearCoat,
                                                              mainLight,
                                                              inputData.normalWS, inputData.viewDirectionWS,
                                                              surfaceData.clearCoatMask, specularHighlightsOff);
    }

    #if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();

    #if USE_FORWARD_PLUS
    for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK

        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);

#ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
#endif
        {
            lightingData.additionalLightsColor += LightingPhysicallyBased(brdfData, brdfDataClearCoat, light,
                                                                          inputData.normalWS, inputData.viewDirectionWS,
                                                                          surfaceData.clearCoatMask, specularHighlightsOff);
        }
    }
    #endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);

#ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
#endif
        {
            lightingData.additionalLightsColor += LightingPhysicallyBased(brdfData, brdfDataClearCoat, light,
                                                                          inputData.normalWS, inputData.viewDirectionWS,
                                                                          surfaceData.clearCoatMask, specularHighlightsOff);
        }
    LIGHT_LOOP_END
    #endif

    #if defined(_ADDITIONAL_LIGHTS_VERTEX)
    lightingData.vertexLightingColor += inputData.vertexLighting * brdfData.diffuse;
    #endif

    half4 finalColor = (half4)0; //SRS: create a variable for the final color
#if REAL_IS_HALF
    // Clamp any half.inf+ to HALF_MAX
    finalColor = min(CalculateFinalColor(lightingData, surfaceData.alpha), HALF_MAX);
#else
    finalColor = CalculateFinalColor(lightingData, surfaceData.alpha);
#endif

    //SRS: calculate sparkle and SSS, then add it to the surface emission
    #if defined(_COVERAGE_ON)

        half lightmapMask = GetLightmapMask(inputData.bakedGI);

        //float distMask = 1 - saturate((eyeDepth -_ProjectionParams.y) / _SparkleDistFalloff);
        float distMask = GetDistanceGradient(inputData.positionWS, _SparkleDistFalloff);

        half effectsMask = lightmapMask * pow(snowMask, 2);
        
        #if defined(_SPARKLE_ON)
            finalColor.rgb += CalcSparkle(_SparkleTex, sampler_SparkleTex, _SparkleTex_TexelSize.xy, _SparklesAmount, _SparklesBrightness, _LocalSparkleTiling, 
            _ScreenSpaceSparklesTiling, _SparklesHighlightMaskExpansion, highlightBrightness, inputData.normalizedScreenSpaceUV, inputData.positionWS, inputData.normalWS, inputData.viewDirectionWS, 
            mainLight.direction, mainLight.color * mainLight.shadowAttenuation, lightingData.additionalLightsColor, colorEnhanceMask, distMask * 0.5) * effectsMask * distMask;
        #endif
        #if defined(_SSS_ON)
            finalColor.rgb += SSS(inputData.normalWS, inputData.viewDirectionWS, mainLight.direction, mainLight.color * mainLight.shadowAttenuation, _SSS_intensity, effectsMask * distMask * sssMask);
        #endif
        finalColor.rgb += EnhanceColor(mainLight.color * mainLight.shadowAttenuation, lightingData.additionalLightsColor, colorEnhanceMask, effectsMask * distMask, _ColorEnhance);
    #endif
    //SRS end
    
    return finalColor;
}

// Deprecated: Use the version which takes "SurfaceData" instead of passing all of these arguments...
half4 NL_UniversalFragmentPBR(InputData inputData, half3 albedo, half metallic, half3 specular,
    half smoothness, half occlusion, half3 emission, half alpha, half snowMask, half sssMask, half colorEnhanceMask, half highlightBrightness)
{
    SurfaceData surfaceData;

    surfaceData.albedo = albedo;
    surfaceData.specular = specular;
    surfaceData.metallic = metallic;
    surfaceData.smoothness = smoothness;
    surfaceData.normalTS = half3(0, 0, 1);
    surfaceData.emission = emission;
    surfaceData.occlusion = occlusion;
    surfaceData.alpha = alpha;
    surfaceData.clearCoatMask = 0;
    surfaceData.clearCoatSmoothness = 1;

    return NL_UniversalFragmentPBR(inputData, surfaceData, snowMask, sssMask, colorEnhanceMask, highlightBrightness);
}

#endif
