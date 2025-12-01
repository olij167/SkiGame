#pragma once

struct SparkleInput
{
    half3 normalWS;
    float3 positionWS;
    half3 viewDirWS;
    float2 normalizedScreenSpaceUV;
    half3 mainLightColor;
    half3 mainLightDir;
    half3 additionalLightsColor;
    half surfaceSmoothness;
    float screenSpaceTiling;
    float localSpaceTiling;
    float amount;
    float brightness; 
    float expansion;
    half mask;
};

struct SSSInput
{
    half3 normalWS;
    half3 viewDirWS;
    half3 mainLightColor;
    half3 mainLightDir;
    half intensity;
    half mask;
};

float AdjustBrightnessContrast(float value, float brightness, float contrast)
{
    return value = saturate((value - 0.5) * max(contrast, 0.0) + 0.5 + brightness); //adjust mask brightness/contrast
}

#if defined(_SPARKLE_ON)
half3 CalcSparkle(Texture2D sparkleTex, SamplerState samplerState, float2 texelSize, half amount, half brightness, half lsTiling, 
half ssTiling, half expansion, half highlightBrightness, float2 normSSUV, float3 positionWS, half3 normalWS, half3 viewDirWS, 
half3 mainLightDir, half3 mainLightColor, half3 additionalLightsColor, half surfSm, float distBlendMask)
{
    float2 ssUV = normSSUV * _ScreenParams.xy * texelSize;

    //Single sample triplanar (fast)
    half localSparkleTex = FastTriplanar_RGBA(sparkleTex, samplerState, positionWS, normalWS, lsTiling).r;

    half highlightMask = 1;

    highlightMask = dot(normalize(mainLightDir + viewDirWS), normalWS);
    highlightMask = pow(max(highlightMask, 0.0001), (0.999 - expansion) * 128);

    ssUV += mainLightDir.xz; // make sparkle uv depend on main light direction

    #if UNITY_SINGLE_PASS_STEREO
        float4 scaleOffset = unity_StereoScaleOffset[unity_StereoEyeIndex];
        ssUV = (ssUV - scaleOffset.zw) / scaleOffset.xy;
    #endif

    ssUV *= ssTiling;

    half screenSpaceSparkle = SAMPLE_TEXTURE2D(sparkleTex, samplerState, ssUV).r;
    //half mix = lerp(localSparkleTexFar * 0.6, localSparkleTex, distBlendMask) * screenSpaceSparkle;
    half mix = localSparkleTex * screenSpaceSparkle;
    half ampSm = pow(surfSm, 4) * brightness;
    
    half3 finalSparkle = 0;
    half sparkleMask = step(1 - amount, mix) * brightness; //add sparkles
    sparkleMask += ampSm * highlightMask * amount * highlightBrightness; //increase local space highlight 
    
    finalSparkle += mainLightColor * sparkleMask.xxx * highlightMask; //main light sparkle
    finalSparkle += mainLightColor * sparkleMask.xxx * 0.2;//add overall sparkles
    finalSparkle += additionalLightsColor * sparkleMask.xxx;
    return finalSparkle;
}

half3 CalcSparkle(SparkleInput sparkleInput, Texture2D sparkleTex, SamplerState samplerState, float distBlendMask = 1)
{
    float3 absGeomNormal = abs(sparkleInput.normalWS);
	half3 worldMasks = saturate(absGeomNormal - (0.3).xxx);

    //TODO: optimize sparkle triplanar sampling by using a single sample trinplanar technique

    float2 ssUV = sparkleInput.normalizedScreenSpaceUV;
    float ratio = _ScreenParams.x / _ScreenParams.y;
    ssUV.x = ssUV.x * ratio;

    //Single sample triplanar (fast)
    half localSparkleTex = FastTriplanar_RGBA(sparkleTex, samplerState, sparkleInput.positionWS, sparkleInput.normalWS, _LocalSparkleTiling).r;
    half localSparkleTexFar = FastTriplanar_RGBA(sparkleTex, samplerState, sparkleInput.positionWS, sparkleInput.normalWS, _LocalSparkleTiling * 0.1).r;
    
    /*
    //True triplanar (slow)
    half extraSparkleX = SAMPLE_TEXTURE2D(sparkleTex, samplerState, sparkleInput.positionWS.zy * _LocalSparkleTiling).r;
    half extraSparkleY = SAMPLE_TEXTURE2D(sparkleTex, samplerState, sparkleInput.positionWS.xz * _LocalSparkleTiling).r;
    half extraSparkleZ = SAMPLE_TEXTURE2D(sparkleTex, samplerState, sparkleInput.positionWS.xy * _LocalSparkleTiling).r;
    half localSparkleTex = saturate(extraSparkleY * worldMasks.y + extraSparkleX * worldMasks.x + extraSparkleZ * worldMasks.z);
    */

    half highlightMask = 1;

    highlightMask = dot(normalize(sparkleInput.mainLightDir + sparkleInput.viewDirWS), sparkleInput.normalWS);
    highlightMask = pow(max(highlightMask, 0.0001), (0.999 - sparkleInput.expansion) * 128);

    ssUV += sparkleInput.mainLightDir.xz; // make sparkle uv depend on main light direction

    #if UNITY_SINGLE_PASS_STEREO
        float4 scaleOffset = unity_StereoScaleOffset[unity_StereoEyeIndex];
        ssUV = (ssUV - scaleOffset.zw) / scaleOffset.xy;
    #endif

    ssUV *= sparkleInput.screenSpaceTiling;

    half screenSpaceSparkle = SAMPLE_TEXTURE2D(sparkleTex, samplerState, ssUV).r;
    half mix = lerp(localSparkleTexFar, localSparkleTex, distBlendMask) * screenSpaceSparkle;

    half ampSm = pow(sparkleInput.surfaceSmoothness, 2) * sparkleInput.brightness * 0.3;
    
    half3 finalSparkle = 0;
    half sparkleMask = step(1 - sparkleInput.amount, mix) * sparkleInput.brightness; //add sparkles
    //sparkleMask += step(1 - sparkleInput.amount * 0.9, mix) * sparkleInput.brightness * 0.05; //add overall sparkles
    sparkleMask += ampSm * pow(highlightMask, 2) * sparkleInput.amount * 2; //increase local space highlight 
    
    finalSparkle += sparkleInput.mainLightColor * sparkleMask.xxx * highlightMask; //main light sparkle
    finalSparkle += sparkleInput.mainLightColor * sparkleMask.xxx * 0.2;
    finalSparkle += sparkleInput.additionalLightsColor * sparkleMask.xxx;
    return finalSparkle * sparkleInput.mask;
}
#endif

#if defined(_SSS_ON)
float3 SSS(half3 normalWS, half3 viewDirWS, half3 mainLightDir, half3 mainLightColor, half intensity, half mask)    
{
    float3 finalSSS = 0;
    float sssMask = saturate(dot(-normalize(normalWS + mainLightDir * 2), viewDirWS)) * mask;
    sssMask = pow(sssMask, 2) * intensity;
    finalSSS += mainLightColor;
    return finalSSS * sssMask;
}

float3 SSS(SSSInput sssInput)    
{
    float3 finalSSS = 0;
    float sssMask = saturate(dot(-normalize(sssInput.normalWS + sssInput.mainLightDir * 2), sssInput.viewDirWS)) * sssInput.mask;
    sssMask = pow(sssMask, 2) * sssInput.intensity;
    finalSSS += sssInput.mainLightColor;
    //finalSSS += sssInput.additionalLightsColor;
    return finalSSS * sssMask;
}
#endif

float3 EnhanceColor(half3 mainLightColor, half3 additionalLightsColor, half surf, half mask, half intensity)
{
    return (mainLightColor + additionalLightsColor) * surf * mask * mask * intensity;
}

half GetLightmapMask(float3 bakedGI)
{
    half lightmapMask = 1;
    #if defined(_SPARKLE_ON) || defined(_SSS_ON)
        
        #if defined(LIGHTMAP_ON)
            #if defined(LIGHTMAP_SHADOW_MIXING) && !defined(SHADOWS_SHADOWMASK)
                lightmapMask = saturate((bakedGI.r + bakedGI.g + bakedGI.b) * 0.333);
                lightmapMask = AdjustBrightnessContrast(lightmapMask, -1, 10);
            #endif
        #else
            lightmapMask = 1;
        #endif
    #endif
    return lightmapMask;
}
