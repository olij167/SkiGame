#pragma once
/*
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

    inputData.positionWS
    inputData.tangentToWorld
    inputData.normalWS
    inputData.viewDirectionWS
    inputData.shadowCoord
    inputData.fogCoord
    inputData.vertexLighting
    inputData.bakedGI
    inputData.normalizedScreenSpaceUV
    inputData.shadowMask
    inputData.dynamicLightmapUV
    inputData.staticLightmapUV
    inputData.vertexSH
*/
//sampler2D _CoverageTex0;

uniform float _depthCamFarHeight;

float BrightnessContrast(float value, float brightness, float contrast)
{
    return value = saturate((value - 0.5) * max(contrast, 0.0) + 0.5 + brightness); //adjust mask brightness/contrast
}

void AddDetails(Texture2D tex, SamplerState ss, float3 positionWS, half3 normalWS, half tiling, half maxDistance, half2 detailRemap, half normalScale, half dither, out half detailMask, out half3 detailNormals)
{
    half distGradient = GetDistanceGradient(positionWS, maxDistance);
    half4 detailTex = FastTriplanarSoft_Normals_MaskZ(tex, ss, positionWS, normalWS, 32, tiling, normalScale * distGradient, dither);

    //half3 detailTex = SAMPLE_TEXTURE2D(tex, ss, positionWS.xz * tiling).xyz;
    detailTex.w = (detailTex.w - 0.5) * 2; //remap to -1, 1 range.
    detailTex.w = Remap(detailRemap.x, detailRemap.y, detailTex.w);
    
    detailTex.w *= distGradient;
    detailMask = detailTex.w;
    detailNormals = detailTex.xyz;
}

float3 GetWeightedMasks(float3 sourceMask)
{
    float3 splatMasks = sourceMask; 
    float weight = dot(splatMasks, float3(1, 1, 1)) + 0.001;
    splatMasks /= weight;
    return splatMasks;
}

#if defined(_COVERAGE_ON)
void SnowCoverage(inout half3 normalWS, float3 positionWS, inout half3 albedo, inout half metallic, 
inout half smoothness, out half colorEnhanceMask, inout half occlusion, inout half alpha, 
half3 geomNormalWS, inout half finalSnowMask, float4 vertexColor, out half sssMask, out half highlightBrightness, float2 uv = 0, half dither = 0)
{
    float3 approximateSurfHeight = normalWS;

    //sample VSM (depth coverage mask)
    float3 posWS = positionWS;
    posWS.y = (posWS.y + _depthCamFarHeight) * 0.5;

    float3 relativeSurfPos = mul(_depthCamMatrix, float4(posWS, 1.0)).xyz;
    relativeSurfPos.z = 1-relativeSurfPos.z;
    float2 srsDepthUV = (0.5 + (0.5 * relativeSurfPos)).xy;

	float2 moments = SAMPLE_TEXTURE2D(_SRS_depth, sampler_SRS_depth, srsDepthUV).xy;

	float basicAreaMask = ComputeBasicAreaMask(moments, relativeSurfPos.z, _CoverageAreaBias, _CoverageAreaMaskRange, _CoverageLeakReduction); //basic occluded area mask
    
    float3 absGeomNormal = abs(geomNormalWS);
	float3 worldMasks = saturate(absGeomNormal - (0.3).xxx);

    #if defined (SRS_TERRAIN) && !defined(SRS_TERRAIN_BAKE_SHADER)
        float distanceFade = GetDistanceGradient(positionWS, _DistanceFadeFalloff + _DistanceFadeStart + 0.001, _DistanceFadeStart); 
    #else
        float distanceFade = 1;
    #endif

    float4 addMask = float4(0, 0, 0, 0);
	float4 eraseMask = float4(1, 1, 1, 1);
    float3 splatMasks = 1;
    half2 covSm = _Cov0Smoothness;
    float3 finalColor = _CoverageColor;

	#ifdef _PAINTABLE_COVERAGE_ON
        #ifdef SRS_TERRAIN
            float2 covSplatUV = (uv * (_PaintedMask_TexelSize.zw - 1.0f) + 0.5f) * _PaintedMask_TexelSize.xy;
            float4 paintedMask = SAMPLE_TEXTURE2D(_PaintedMask, sampler_SRS_depth, covSplatUV);
            
            half3 paintedMaskNormal = SAMPLE_TEXTURE2D(_PaintedMaskNormal, sampler_SRS_depth, covSplatUV).rgb * 2 - 1;
            paintedMaskNormal = paintedMaskNormal.xzy;

            #if defined (_THREE_TEX_MODE)
                splatMasks = GetWeightedMasks(paintedMask.yzw); // use G B A channels for 3 different snow textures
            #endif

		    PaintMaskRGBA(paintedMask, addMask, eraseMask);
        #else
            #if defined (_THREE_TEX_MODE)
                splatMasks = GetWeightedMasks(vertexColor.yzw); // use G B A channels for 3 different snow textures
            #endif
            PaintMaskRGBA(vertexColor, addMask, eraseMask);
        #endif
	#endif

    float covAmount = (eraseMask.r * _CoverageAmount);

    #ifdef SRS_TERRAIN
        float2 basicUV = positionWS.xz * _TilingMultiplier;//multiply UV for the terrain LOD maps baking
        
        //Use simple world space or stochastic sampling for terrains
        float2 covTex0_uv = basicUV * _CoverageTiling.xx;
        #if defined (_THREE_TEX_MODE)
            float2 covTex1_uv = basicUV * _CoverageTiling1.xx;
            float2 covTex2_uv = basicUV * _CoverageTiling2.xx;
        #endif
        
        #ifdef _STOCHASTIC_ON
            float4 coverageMasks0 = StochasticTex2D(_CoverageTex0, sampler_SRS_depth, covTex0_uv);
            #if defined (_THREE_TEX_MODE)
                float4 coverageMasks1 = StochasticTex2D(_CoverageTex1, sampler_SRS_depth, covTex1_uv);
                float4 coverageMasks2 = StochasticTex2D(_CoverageTex2, sampler_SRS_depth, covTex2_uv);
            #endif
        #else
            float4 coverageMasks0 = SAMPLE_TEXTURE2D(_CoverageTex0, sampler_SRS_depth, covTex0_uv);
            #if defined (_THREE_TEX_MODE)
                float4 coverageMasks1 = SAMPLE_TEXTURE2D(_CoverageTex1, sampler_SRS_depth, covTex1_uv);
                float4 coverageMasks2 = SAMPLE_TEXTURE2D(_CoverageTex2, sampler_SRS_depth, covTex2_uv);
            #endif
        #endif

/*
        #if defined (_THREE_TEX_MODE)
            //3 snow textures
            float2 heightAndSmoothness = SummedBlend2D(coverageMasks0.zw, coverageMasks1.zw, coverageMasks2.zw, splatMasks);
            half2 normalXY = SummedBlend2D(coverageMasks0.xy, coverageMasks1.xy, coverageMasks2.xy, splatMasks);
            half nScale = SummedBlend1D(_CoverageNormalScale0, _CoverageNormalScale1, _CoverageNormalScale2, splatMasks);
            covSm = SummedBlend2D(_Cov0Smoothness, _Cov1Smoothness, _Cov2Smoothness, splatMasks);
            half3 snowNormals = ConstructNormal(normalXY, nScale);
            finalColor = SummedBlend3D(_CoverageColor.rgb, _CoverageColor1.rgb, _CoverageColor2.rgb, splatMasks);
        #else
            //Single snow texture
            float2 heightAndSmoothness = coverageMasks0.zw;
            half3 snowNormals = ConstructNormal(coverageMasks0.rg, _CoverageNormalScale0);
        #endif
        */
    #else

/*
//True triplanar
        float2 uvX = positionWS.zy * _CoverageTiling.xx;
        float2 uvY = positionWS.xz * _CoverageTiling.xx;
        float2 uvZ = positionWS.xy * _CoverageTiling.xx;

        float4 coverageMasks0X = SAMPLE_TEXTURE2D(_CoverageTex0, sampler_SRS_depth, uvX);
	    float4 coverageMasks0Y = SAMPLE_TEXTURE2D(_CoverageTex0, sampler_SRS_depth, uvY);
	    float4 coverageMasks0Z = SAMPLE_TEXTURE2D(_CoverageTex0, sampler_SRS_depth, uvZ);

        float2 heightAndSmoothness = saturate(coverageMasks0Y.zw * worldMasks.y + coverageMasks0X.zw * worldMasks.x + coverageMasks0Z.zw * worldMasks.z);

        half3 snowNormalX = ConstructNormal(coverageMasks0X.rg, _CoverageNormalScale0);
        half3 snowNormalY = ConstructNormal(coverageMasks0Y.rg, _CoverageNormalScale0);
        half3 snowNormalZ = ConstructNormal(coverageMasks0Z.rg, _CoverageNormalScale0);
    
	    //swizzle world normals to match tangent space and apply reoriented normal mapping blend
        snowNormalX = BlendReorientedNormal(half3(geomNormalWS.zy, absGeomNormal.x), snowNormalX);
        snowNormalY = BlendReorientedNormal(half3(geomNormalWS.xz, absGeomNormal.y), snowNormalY);
        snowNormalZ = BlendReorientedNormal(half3(geomNormalWS.xy, absGeomNormal.z), snowNormalZ);
    
        //prevent return value of 0
        half3 axisSign = geomNormalWS < 0 ? -1 : 1;
    
        // apply world space sign to tangent space Z
        snowNormalX.z *= axisSign.x;
        snowNormalY.z *= axisSign.y;
        snowNormalZ.z *= axisSign.z;
    
        //swizzle tangent normals to match world normal and blend together
        half3 snowNormals = normalize(
        snowNormalX.zyx * worldMasks.x +
        snowNormalY.xzy * worldMasks.y +
        snowNormalZ.xyz * worldMasks.z
        );
*/

        //fast single sampler triplanar
        half3 triMask = GetTriBlendMask(geomNormalWS, _CovTriBlendContrast);
        half4 coverageMasks0 = FastTriplanarSoft_RGBA(_CoverageTex0, sampler_SRS_depth, positionWS, geomNormalWS, _CoverageTiling, dither, triMask);
        #if defined (_THREE_TEX_MODE)
            half4 coverageMasks1 = FastTriplanarSoft_RGBA(_CoverageTex1, sampler_SRS_depth, positionWS, geomNormalWS, _CoverageTiling1, dither, triMask);
            half4 coverageMasks2 = FastTriplanarSoft_RGBA(_CoverageTex2, sampler_SRS_depth, positionWS, geomNormalWS, _CoverageTiling2, dither, triMask);
        #endif
    #endif

    #if defined (_THREE_TEX_MODE)
        //3 snow textures
        float2 heightAndSmoothness = SummedBlend2D(coverageMasks0.zw, coverageMasks1.zw, coverageMasks2.zw, splatMasks);
        half2 normalXY = SummedBlend2D(coverageMasks0.xy, coverageMasks1.xy, coverageMasks2.xy, splatMasks);
        half nScale = SummedBlend1D(_CoverageNormalScale0, _CoverageNormalScale1, _CoverageNormalScale2, splatMasks);
        covSm = SummedBlend2D(_Cov0Smoothness, _Cov1Smoothness, _Cov2Smoothness, splatMasks);
        #if defined (SRS_TERRAIN)
            half3 snowNormals = ConstructNormal(normalXY, nScale);
        #else
            half3 snowNormals = CalcFastTriplanarNormalSoft(normalXY, geomNormalWS, triMask, nScale);
        #endif
        finalColor = SummedBlend3D(_CoverageColor.rgb, _CoverageColor1.rgb, _CoverageColor2.rgb, splatMasks);
    #else
        //Single snow texture
        float2 heightAndSmoothness = coverageMasks0.zw;
        #if defined (SRS_TERRAIN)
            half3 snowNormals = ConstructNormal(coverageMasks0.rg, _CoverageNormalScale0);
        #else
            half3 snowNormals = CalcFastTriplanarNormalSoft(coverageMasks0.xy, geomNormalWS, triMask, _CoverageNormalScale0);
        #endif
    #endif

    //Detail map
    #if defined (_USE_COVERAGE_DETAIL)
        half detailMask = 0;
        half3 detailNormals = (half3)0;

        AddDetails(_CoverageDetailTex, sampler_SRS_depth, positionWS, geomNormalWS, _DetailTiling, _DetailDistance, _DetailTexRemap, _DetailNormalScale, dither, detailMask, detailNormals);

        heightAndSmoothness.y += detailMask;
        heightAndSmoothness = saturate(heightAndSmoothness);

        #if defined (SRS_TERRAIN)
            snowNormals = BlendReorientedNormal(half3(detailNormals.xz, detailNormals.y), snowNormals);
            snowNormals = snowNormals.xzy;
        #else
            snowNormals = BlendNormalsWS(snowNormals, detailNormals, geomNormalWS);
        #endif
    #else
        #if defined (SRS_TERRAIN)
            snowNormals = BlendReorientedNormal(half3(geomNormalWS.xz, absGeomNormal.y), snowNormals);
            snowNormals = snowNormals.xzy;
        #endif
    #endif
    //

    float snowHeightMix = lerp(0, saturate(heightAndSmoothness.x + heightAndSmoothness.x + 0.2), covAmount);
    //snowHeight = heightAndSmoothness.x;

    float dirMask = DirMask(_PrecipitationDirRange, _depthCamDir, geomNormalWS, _PrecipitationDirOffset); //Old variant that uses vertex normals
	//float dirMask = DirMask(_PrecipitationDirRange, _depthCamDir, normalWS, _PrecipitationDirOffset); //precipitation direction mask
	float volumeMask = CalcVolumeMask(_CoverageAreaFalloffHardness, srsDepthUV, relativeSurfPos.z); //Weatherade volume mask
    float outsideAreaMaskVal = saturate(smoothstep(0, _CoverageAreaMaskRange * 10, 5) * 2);

    float transitionFactor = 2;
    float heightCalc = saturate(heightAndSmoothness.x * transitionFactor - (transitionFactor * 0.5));
    float transitionGradient = smoothstep(heightCalc, 1, pow(basicAreaMask, 0.5)) * covAmount;
    float snowMask = lerp(snowHeightMix * outsideAreaMaskVal, transitionGradient, volumeMask) * dirMask;

    snowMask = saturate(snowMask + addMask.r);
    snowMask = HeightBlendTriplanar(snowMask, approximateSurfHeight, worldMasks, _BlendByNormalsPower, _BlendByNormalsStrength);

	float3 covOverlayNormals = lerp(normalWS, snowNormals, _CoverageNormalsOverlay);

    #if defined(_DISPLACEMENT_ON)
        float dirMulSnowHeight = snowHeightMix * pow(saturate(dirMask * 2), 10);
        float basicMask = basicAreaMask * covAmount + addMask.r;
        float basicMaskRemaped = smoothstep(_CoverageDisplacementOffset * 0.8, _CoverageDisplacementOffset, basicMask);

        float finalDispMask = lerp(dirMulSnowHeight, basicMaskRemaped, dirMask);
        #if defined (_THREE_TEX_MODE)
            half covDisplacement = SummedBlend1D(_CoverageDisplacement, _CoverageDisplacement1, _CoverageDisplacement2, splatMasks);
        #else
            half covDisplacement = _CoverageDisplacement;
        #endif
        finalDispMask = saturate(lerp(1, finalDispMask, volumeMask * saturate(covDisplacement * covDisplacement * 100)));
        half3 overrideNormalsByDisp = lerp(covOverlayNormals, snowNormals, finalDispMask);
        overrideNormalsByDisp = lerp(normalWS, overrideNormalsByDisp, snowMask);
        half3 finalNormals = overrideNormalsByDisp;
    #else
        half3 finalNormals = lerp(normalWS, covOverlayNormals, snowMask);
    #endif

    #ifdef _TRACES_ON
        relativeSurfPos = mul(_SRS_TraceSurfCamMatrix, float4(positionWS, 1)).xyz;
    	float2 traceMaskUV = (0.5 + ( 0.5 * relativeSurfPos)).xy;
    	traceMaskUV.y = 1 - traceMaskUV.y;
        
        float4 sampledTraces = SAMPLE_TEXTURE2D(_SRS_TraceTex, sampler_SRS_depth, traceMaskUV);

        sampledTraces = lerp(half4(0.5, 0.5, 0, 0.5), sampledTraces, volumeMask);

        #if defined (_THREE_TEX_MODE)
            half traceNormalScale = SummedBlend1D(_TracesNormalScale, _TracesNormalScale1, _TracesNormalScale2, splatMasks);
        #else
            half traceNormalScale = _TracesNormalScale;
        #endif

        half3 traceNormals = ConstructNormal(sampledTraces.rg, traceNormalScale);

        //Blend t-space trace normals with the surface world normals
        traceNormals = BlendReorientedNormal(half3(finalNormals.xz, finalNormals.y), traceNormals);
        traceNormals = traceNormals.xzy;

        float traceMask = saturate(sampledTraces.z * 5);
        float3 traceDetailSampled = float3(0, 0, 1);
        #ifdef _TRACE_DETAIL
            traceDetailSampled = SAMPLE_TEXTURE2D(_TraceDetailTex, sampler_TraceDetailTex, positionWS.xz * _TraceDetailTiling).rgb;
            float3 traceDetailNormals = ConstructNormal(traceDetailSampled.rg, _TraceDetailNormalScale);
            traceDetailNormals = lerp(float3(0, 0, 1), traceDetailNormals, lerp(0, saturate(sampledTraces.a + 0.5), traceMask));
            traceNormals = BlendReorientedNormal(half3(traceNormals.xz, traceNormals.y), traceDetailNormals);
            traceNormals = traceNormals.xzy;
        #endif

        finalNormals = lerp(finalNormals, traceNormals, snowMask * pow(dirMask, 10));
        #if defined (_THREE_TEX_MODE)
            half4 tracesColor = SummedBlend4D(_TracesColor, _TracesColor1, _TracesColor2, splatMasks);
            half2 blendRange = SummedBlend2D(_TracesColorBlendRange, _TracesColorBlendRange1, _TracesColorBlendRange2, splatMasks);
            half tracesBaseBlend = SummedBlend1D(_TracesBaseBlend0, _TracesBaseBlend1, _TracesBaseBlend2, splatMasks);
        #else
            half4 tracesColor = _TracesColor;
            half2 blendRange = _TracesColorBlendRange;
            half tracesBaseBlend = _TracesBaseBlend0;
        #endif

        tracesBaseBlend = 1-tracesBaseBlend;

        blendRange = blendRange * 0.5 + 0.5;
        
        half tracesMask = smoothstep(blendRange.x, blendRange.y, saturate((1 - sampledTraces.w) - traceDetailSampled.z * 0.2));

        finalColor = lerp(finalColor, albedo, tracesMask * tracesBaseBlend);

        finalColor = lerp(finalColor, finalColor * tracesColor.rgb, tracesMask  * tracesColor.a);
    #endif

    #if defined(_PAINTABLE_COVERAGE_ON) && defined(SRS_TERRAIN)
        half3 blendWithPaintedNormals = BlendReorientedNormal(half3(finalNormals.xz, finalNormals.y), paintedMaskNormal);
        blendWithPaintedNormals = blendWithPaintedNormals.xzy;
        finalNormals = lerp(finalNormals, blendWithPaintedNormals, snowMask);
    #endif

    float snowSmoothness = heightAndSmoothness.y;
    float3 snowColor = finalColor;
    float3 blendAlbedo = lerp(albedo, snowColor, snowMask);
    float blendSmoothness = lerp(smoothness, snowSmoothness, snowMask);

    float finalSmoothness = 0;
    #if defined(SRS_TERRAIN)
		half3 distantNormal = SAMPLE_TEXTURE2D(_NormalLOD, sampler_SRS_depth, uv).rgb;
		distantNormal = distantNormal * 2 - 1;
		distantNormal = distantNormal.xzy;
		normalWS = lerp(distantNormal, finalNormals, distanceFade);
        half4 distantAlbedoSmoothness = SAMPLE_TEXTURE2D(_AlbedoLOD, sampler_SRS_depth, uv);
		albedo = lerp(distantAlbedoSmoothness.rgb, blendAlbedo, distanceFade);
        finalSmoothness = lerp(distantAlbedoSmoothness.a, blendSmoothness, distanceFade);
	#else
		normalWS = finalNormals;
		albedo = blendAlbedo;
        finalSmoothness = blendSmoothness;
	#endif
    
    half enhanceMask = finalSmoothness * distanceFade;
    half2 sssMaskRemap = _SssMaskRemap0;
    half2 enhanceRemap = _EnhanceRemap0;

    #if defined (_THREE_TEX_MODE)
        sssMaskRemap = SummedBlend2D(_SssMaskRemap0, _SssMaskRemap1, _SssMaskRemap2, splatMasks);
        enhanceRemap = SummedBlend2D(_EnhanceRemap0, _EnhanceRemap1, _EnhanceRemap2, splatMasks);
        highlightBrightness = SummedBlend1D(_HighlightBrightness0, _HighlightBrightness1, _HighlightBrightness2, splatMasks);
    #else
        highlightBrightness = _HighlightBrightness0;
    #endif

    sssMask = saturate(Remap(sssMaskRemap.x, sssMaskRemap.y, enhanceMask + 0.5));
    colorEnhanceMask = saturate(Remap(enhanceRemap.x, enhanceRemap.y, enhanceMask));

    smoothness = lerp(finalSmoothness, saturate(Remap(covSm.x, covSm.y, finalSmoothness) * 0.5), snowMask);

    #if defined(FORWARD_BASE_PASS) && defined(_DISPLACEMENT_ON)
        snowMask *= finalDispMask;
    #endif

    finalSnowMask = snowMask;

    metallic = lerp(metallic, 0, finalSnowMask);
    occlusion = lerp(occlusion, 1, finalSnowMask);

    #if defined(TERRAIN_SPLAT_ADDPASS)
        alpha *= 1-snowMask; //output black alpha on the snow surface when TERRAIN_SPLAT_ADDPASS used
    #else
        alpha = lerp(saturate(finalSnowMask + alpha), alpha, _MaskCoverageByAlpha);
    #endif
}
#endif

///////////////////////
///Snow displacenent///
///////////////////////
float3 Displace(half3 normalOS, float3 positionOS, float2 texcoord0, float4 vertexColor, half dither = 0)
{
#ifdef _DISPLACEMENT_ON

    half3 worldNormal = TransformObjectToWorldNormal(normalOS);

    float3 worldPos = TransformObjectToWorld(positionOS);
	float3 worldMasks = saturate(abs(worldNormal) - (0.3).xxx);

    float4 addMask = float4(0, 0, 0, 0);
	float4 eraseMask = float4(1, 1, 1, 1);
    float3 splatMasks = 1;
    half2 heightContrast = _HeightMap0Contrast;

	#ifdef _PAINTABLE_COVERAGE_ON
        #ifdef SRS_TERRAIN
            float2 covSplatUV = (texcoord0 * (_PaintedMask_TexelSize.zw - 1.0f) + 0.5f) * _PaintedMask_TexelSize.xy;
		    float4 paintedMask = SAMPLE_TEXTURE2D_LOD(_PaintedMask, sampler_SRS_depth, covSplatUV, 0.0);
            
            #if defined (_THREE_TEX_MODE)
                splatMasks = GetWeightedMasks(paintedMask.yzw); // use G B A channels for 3 different snow textures
            #endif

		    PaintMaskRGBA(paintedMask, addMask, eraseMask);
        #else
            #if defined (_THREE_TEX_MODE)
                splatMasks = GetWeightedMasks(vertexColor.yzw); // use G B A channels for 3 different snow textures
            #endif
            PaintMaskRGBA(vertexColor, addMask, eraseMask);
        #endif
	#endif

    //sample VSM (depth coverage mask)
	//float4 relativeSurfPos = mul(_depthCamMatrix, float4(worldPos, 1));
	//float2 srsDepthUV = (0.5 + ( 0.5 * relativeSurfPos)).xy;
    float3 posWS = worldPos;
            	
    posWS.y = (posWS.y + _depthCamFarHeight) * 0.5;

    float3 relativeSurfPos = mul(_depthCamMatrix, float4(posWS, 1.0)).xyz;
    relativeSurfPos.z = 1-relativeSurfPos.z;
    float2 srsDepthUV = (0.5 + (0.5 * relativeSurfPos)).xy;

    //invert uv.y only on platforms that uses reversed depth (DirectX, Metal)
    #if defined(UNITY_REVERSED_Z)
        //srsDepthUV.y = 1 - srsDepthUV.y;
    #endif

    float2 moments = SAMPLE_TEXTURE2D_LOD(_SRS_depth, sampler_SRS_depth, srsDepthUV, 0.0).xy;
    float basicAreaMask = ComputeBasicAreaMask(moments, relativeSurfPos.z, _CoverageAreaBias, _CoverageAreaMaskRange, _CoverageLeakReduction); //basic occluded area mask
    
    #if !defined(SRS_TERRAIN)
        basicAreaMask = saturate(basicAreaMask  + addMask.r);
    #endif

    //Use simple world space or stochastic sampling for terrains
    #if defined (SRS_TERRAIN)
        float2 basicUV = worldPos.xz * _TilingMultiplier;//multiply UV for the terrain LOD maps baking
    #else
        float2 basicUV = worldPos.zx;
    #endif

    //Use simple world space or stochastic sampling for terrains
    float2 covTex0_uv = basicUV * _CoverageTiling.xx;
    #if defined (_THREE_TEX_MODE)
        float2 covTex1_uv = basicUV * _CoverageTiling1.xx;
        float2 covTex2_uv = basicUV * _CoverageTiling2.xx;
    #endif

    #ifdef _STOCHASTIC_ON
        float snowHeight0 = StochasticTex2DLod(_CoverageTex0, sampler_SRS_depth, covTex0_uv, _HeightMap0LOD).z; //use downsampled height map to prevent high frequency noise
        #if defined (_THREE_TEX_MODE)
            float snowHeight1 = StochasticTex2DLod(_CoverageTex1, sampler_SRS_depth, covTex1_uv, _HeightMap1LOD).z; //use downsampled height map to prevent high frequency noise
            float snowHeight2 = StochasticTex2DLod(_CoverageTex2, sampler_SRS_depth, covTex2_uv, _HeightMap2LOD).z; //use downsampled height map to prevent high frequency noise
        #endif
    #else
        float snowHeight0 = SAMPLE_TEXTURE2D_LOD(_CoverageTex0, sampler_SRS_depth, covTex0_uv, _HeightMap0LOD).z; //use downsampled height map to prevent high frequency noise
        #if defined (_THREE_TEX_MODE)
            float snowHeight1 = SAMPLE_TEXTURE2D_LOD(_CoverageTex1, sampler_SRS_depth, covTex1_uv, _HeightMap1LOD).z; //use downsampled height map to prevent high frequency noise
            float snowHeight2 = SAMPLE_TEXTURE2D_LOD(_CoverageTex2, sampler_SRS_depth, covTex2_uv, _HeightMap2LOD).z; //use downsampled height map to prevent high frequency noise
        #endif
    #endif

    #if defined (_THREE_TEX_MODE)
        //3 snow textures
        float snowHeightMix = SummedBlend1D(snowHeight0, snowHeight1, snowHeight2, splatMasks);
        heightContrast = SummedBlend2D(_HeightMap0Contrast, _HeightMap1Contrast, _HeightMap2Contrast, splatMasks);
    #else
        //Single snow texture
        float snowHeightMix = snowHeight0;
    #endif
	
    float dirMask = DirMask(_PrecipitationDirRange, _depthCamDir, worldNormal, _PrecipitationDirOffset); //precipitation direction mask
    //snowHeightMix = BrightnessContrast(snowHeightMix, 0, heightContrast); 
    snowHeightMix = saturate(Remap(heightContrast.x, heightContrast.y, snowHeightMix));
    snowHeightMix *= dirMask * eraseMask.r;

    float heightInsideArea = snowHeightMix * basicAreaMask;
    float heightOutsideArea = snowHeightMix;
    float volumeMask = CalcVolumeMask(_CoverageAreaFalloffHardness, srsDepthUV, relativeSurfPos.z); //Weatherade volume mask

    float insideOutsideComposed = lerp(heightOutsideArea, heightInsideArea, volumeMask);

    insideOutsideComposed = smoothstep(_CoverageDisplacementOffset, 1, insideOutsideComposed);
    
    float covAmountBiased = saturate(_CoverageAmount - 0.5);

    #if defined(SRS_TERRAIN)
        float disp = lerp(0, insideOutsideComposed + smoothstep(0, 2, saturate(addMask.r - 0.3)), covAmountBiased);
    #else
        float disp = lerp(0, insideOutsideComposed, covAmountBiased);
    #endif

    #ifdef _TRACES_ON
        relativeSurfPos = mul(_SRS_TraceSurfCamMatrix, float4(worldPos, 1)).xyz;
    	float2 traceMaskUV = (0.5 + ( 0.5 * relativeSurfPos)).xy;
    	traceMaskUV.y = 1 - traceMaskUV.y;

        float2 traceMaskSampled = SAMPLE_TEXTURE2D_LOD(_SRS_TraceTex, sampler_SRS_depth, traceMaskUV, 0.0).zw;
        float traceDispMask = traceMaskSampled.y;
        
        #ifdef _TRACE_DETAIL
            float traceDetailMask = saturate(traceMaskSampled.x * 2);
            float traceHeightDetails = SAMPLE_TEXTURE2D_LOD(_TraceDetailTex, sampler_TraceDetailTex, worldPos.xz * _TraceDetailTiling, 0.0).z;
            traceDispMask *= lerp(1, traceHeightDetails, traceDetailMask * _TraceDetailIntensity);
        #endif

        float finalDispMask = disp * traceDispMask * 4 * /* pow(dirMask, 10) */ dirMask;
    #else
        float finalDispMask = disp * 2;
    #endif

    #if defined (_THREE_TEX_MODE)
        half covDisplacement = SummedBlend1D(_CoverageDisplacement, _CoverageDisplacement1, _CoverageDisplacement2, splatMasks);
    #else
        half covDisplacement = _CoverageDisplacement;
    #endif

    float3 modifiedVPosOS = worldPos + worldNormal * finalDispMask * covDisplacement;
    return modifiedVPosOS;
#endif //Displacement end
}

VertexPositionInputs NL_GetVertexPositionInputs(Attributes IN)
{
    VertexPositionInputs input;
    input.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
    //SRS
    #if defined(_COVERAGE_ON) && defined(_DISPLACEMENT_ON)
        #if defined (SRS_TERRAIN)
            float4 vertexColor = 0;
        #else
            float4 vertexColor = IN.color;
        #endif

        half3 n = 0;
        #if defined (_USE_AVERAGED_NORMALS)
            n = IN.unifiedNormal;
        #else
            n = IN.normalOS;
        #endif

        input.positionWS.xyz = Displace(n, IN.positionOS.xyz, IN.texcoord.xy, vertexColor);
    #endif
    //
    input.positionVS = TransformWorldToView(input.positionWS);
    input.positionCS = TransformWorldToHClip(input.positionWS);
 
    float4 ndc = input.positionCS * 0.5f;
    input.positionNDC.xy = float2(ndc.x, ndc.y * _ProjectionParams.x) + ndc.w;
    input.positionNDC.zw = input.positionCS.zw;
 
    return input;
}

VertexNormalInputs NL_GetVertexNormalInputs(float3 normalOS, float3 unifiedNormal, float4 tangentOS)
{
    VertexNormalInputs tbn;
 
    // mikkts space compliant. only normalize when extracting normal at frag.
    real sign = tangentOS.w * GetOddNegativeScale();
    tbn.normalWS = TransformObjectToWorldNormal(unifiedNormal);
    tbn.tangentWS = TransformObjectToWorldDir(tangentOS.xyz);
    tbn.bitangentWS = cross(TransformObjectToWorldNormal(normalOS), tbn.tangentWS) * sign;
    return tbn;
}

//#endif
