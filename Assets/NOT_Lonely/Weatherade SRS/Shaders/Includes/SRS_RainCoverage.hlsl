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

#if defined(SHADER_API_D3D11) || defined(SHADER_API_XBOXONE) || defined(UNITY_COMPILER_HLSLCC) || defined(SHADER_API_PSSL) || (defined(SHADER_TARGET_SURFACE_ANALYSIS) && !defined(SHADER_TARGET_SURFACE_ANALYSIS_MOJOSHADER))|| defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE) || defined(SHADER_API_VULKAN) || defined(SHADER_API_METAL)
	#define TEXTURE_ARRAY_SUPPORTED 1
#endif

#if defined(_COVERAGE_ON)
void RainCoverage(inout half3 normalWS, float3 positionWS, inout half3 albedo, inout half smoothness, float2 uv, half4 color, float2 splatUV, half3 geomNormalWS, half3 unifiedWorldNormal, half4 tangentWS)
{
	float approximateSurfHeight = normalWS.y;

	//drips mask
	float3 absNormSurfNormal = abs(geomNormalWS);
	float xMask = (absNormSurfNormal.x - 0.3);
	float zMask = (absNormSurfNormal.z - 0.3);

	float4 addMask = float4(0, 0, 0, 0);
	float4 eraseMask = float4(1, 1, 1, 1);

	#ifdef _PAINTABLE_COVERAGE_ON
        #ifdef SRS_TERRAIN
            float2 covSplatUV = (uv * (_PaintedMask_TexelSize.zw - 1.0f) + 0.5f) * _PaintedMask_TexelSize.xy;
            float4 paintedMask = SAMPLE_TEXTURE2D(_PaintedMask, sampler_SRS_depth, covSplatUV); // reuse splatUV
		    PaintMaskRGBA(paintedMask, addMask, eraseMask);
        #else
            PaintMaskRGBA(color, addMask, eraseMask);
        #endif
	#endif

	//sample VSM (depth coverage mask)
    //calculate UV
	float3 posWS = positionWS;
	posWS.y = (posWS.y + _depthCamFarHeight) * 0.5;
	
	float3 relativeSurfPos = mul(_depthCamMatrix, float4(posWS, 1.0)).xyz;
	relativeSurfPos.z = 1-relativeSurfPos.z;
	float2 srsDepthUV = (0.5 + (0.5 * relativeSurfPos)).xy;

	float2 moments = SAMPLE_TEXTURE2D(_SRS_depth, sampler_SRS_depth, srsDepthUV).xy;
	float basicAreaMask = ComputeBasicAreaMask(moments, relativeSurfPos.z, _CoverageAreaBias, _CoverageAreaMaskRange, _CoverageLeakReduction); //basic occluded area mask
    
	float dirMask = DirMask(_PrecipitationDirRange, _depthCamDir, geomNormalWS, _PrecipitationDirOffset); //precipitation direction mask
	float volumeMask = CalcVolumeMask(_CoverageAreaFalloffHardness, srsDepthUV, relativeSurfPos.z);
	float2 covAreaMaskRange = _CoverageAreaMaskRange * 10;
	float outsideAreaMaskVal = saturate(smoothstep(0, _CoverageAreaMaskRange * 10, 5) * 2);
	volumeMask = lerp(outsideAreaMaskVal, basicAreaMask, volumeMask);

	//Drips
	float3 blendNormals = normalWS;
	float dripsMask = 0;
	
	#if defined (_DRIPS_ON)	
		//half3 n;
		#if defined(_USE_AVERAGED_NORMALS)
			//n = unifiedWorldNormal;
			geomNormalWS = unifiedWorldNormal;
		#else
			//n = geomNormalWS;
		#endif
		
		float3 dripsNormals;

		real sign = tangentWS.w * GetOddNegativeScale();
        half3 surfBitangent = cross(geomNormalWS, tangentWS.xyz) * sign;

		half3x3 worldToTangentMatrix = half3x3(tangentWS.xyz, surfBitangent, geomNormalWS);

		float dripsDistMask = max((xMask * SAMPLE_TEXTURE2D(_PrimaryMasks, sampler_PrimaryMasks, (positionWS.zy * _DistortionTiling)).b), (SAMPLE_TEXTURE2D(_PrimaryMasks, sampler_PrimaryMasks, (_DistortionTiling * positionWS.xy)).b * zMask));
		float dripsDistAmount = (_DistortionAmount + saturate(((geomNormalWS.y - 0.5) * 0.15)));
		float3 dripsDistNormals = PerturbNormal(positionWS, geomNormalWS, dripsDistMask, _DistortionAmount);

		float dripsSpeed = (_Time.y * _DripsSpeed);
		float2 worldToTangentDir = mul(worldToTangentMatrix, dripsDistNormals).xy;
		float2 dripsUV_distort_x = (worldToTangentDir + (_DripsTiling * positionWS.zy));
		float2 dripsUV_distort_z = (worldToTangentDir + (_DripsTiling * positionWS.xy));
		
		float2 dripsUV_x = (dripsSpeed * 0.6 * float2(0, 1) + dripsUV_distort_x);
		float2 dripsUV_z = (dripsSpeed * 0.6 * float2(0, 1) + dripsUV_distort_z);

		float dripsX = lerp(0.0, SAMPLE_TEXTURE2D(_PrimaryMasks, sampler_PrimaryMasks, dripsUV_x).g, xMask);
		float dripsZ = lerp(0.0, SAMPLE_TEXTURE2D(_PrimaryMasks, sampler_PrimaryMasks, dripsUV_z).g, zMask);

		//extra drips layer
		float2 dripsUV_distort_x_extra = (worldToTangentDir + (_DripsTiling * 0.7 * positionWS.zy + 0.3));
		float2 dripsUV_distort_z_extra = (worldToTangentDir + (_DripsTiling * 0.7 * positionWS.xy + 0.3));

		float2 dripsUV_x_extra = (dripsSpeed * float2(0, 1) + dripsUV_distort_x_extra);
		float2 dripsUV_z_extra = (dripsSpeed * float2(0, 1) + dripsUV_distort_z_extra);

		float dripsX_extra = lerp(0.0, SAMPLE_TEXTURE2D(_PrimaryMasks, sampler_PrimaryMasks, dripsUV_x_extra).g, xMask);
		float dripsZ_extra = lerp(0.0, SAMPLE_TEXTURE2D(_PrimaryMasks, sampler_PrimaryMasks, dripsUV_z_extra).g, zMask);

		dripsX = max(dripsX, dripsX_extra);
		dripsZ = max(dripsZ, dripsZ_extra);
		//

		float dripsBlend = max(dripsX, dripsZ);

		dripsMask = saturate((dripsBlend * saturate((_DripsIntensity * saturate((eraseMask.a * (addMask.a + (volumeMask * dirMask))))))));
		dripsNormals = mul(worldToTangentMatrix, PerturbNormal(positionWS, geomNormalWS, dripsMask, 1));

		blendNormals = lerp(normalWS, dripsNormals, dripsMask);
	#endif

	//half3 flatNormal = BlendReorientedNormal(half3(geomNormalWS.xz, absNormSurfNormal.y), half3(0, 0, 1)).xzy;//flat normal
	half3 flatNormal = TransformObjectToWorldNormal(half3(0, 1, 0));
	flatNormal = normalize(flatNormal);
	half3 rippleNormals = 0;
	float spots = 0;
	float ripplesAmount = 0;

	#if defined(_RIPPLES_ON)
		float2 ripplesAndSpotsUV = positionWS.xz * _RipplesTiling;
		float rippleNormalAmp = 0;
		
		#if TEXTURE_ARRAY_SUPPORTED	
			float2 seed = float2(123.456, 789.012);
			float2 rotationRange = float2(0, 360);
			float2 offsetRange = float2(-1, 1);
			float2 scaleRange = float2(1, 2);
			float3 ripplesAndSpots = 0;
			float3 sampled;
			ripplesAmount = round(_RipplesAmount);
			
			for(int i = 0; i < ripplesAmount; i++)
			{
				seed = frac(seed * 123.456);
				float curFrame = frac((((_RipplesFPS * (_Time.y + seed.y * i)) + (_RipplesFramesCount - 1.0)) / _RipplesFramesCount)) * _RipplesFramesCount;
				
				float2 randScale = lerp(scaleRange.x, scaleRange.y, seed.x);
				float2 randOffset = lerp(offsetRange.x, offsetRange.x, seed);
				float randRot = radians(lerp(rotationRange.x, rotationRange.y, seed.y));
				float2x2 rotMatrix = float2x2(cos(randRot), -sin(randRot), sin(randRot), cos(randRot));
				float2 uv = mul(rotMatrix, (ripplesAndSpotsUV * randScale) + randOffset);
				
				#if defined(_STOCHASTIC_ON)
					half3 sampled = StochasticTex2DArray(_RipplesTex, sampler_RipplesTex, uv, curFrame).rgb;
				#else
					float3 sampled = SAMPLE_TEXTURE2D_ARRAY(_RipplesTex, sampler_RipplesTex, uv, curFrame).rgb;
				#endif
				
				ripplesAndSpots += sampled;
			}			
			ripplesAndSpots /= ripplesAmount;
			rippleNormalAmp = ripplesAmount;
		#else
			float3 ripplesAndSpots = float3(0, 0, 0);
		#endif

		float ripplesAndSpotsMask = saturate(((geomNormalWS.y * volumeMask * dirMask) + addMask.b)) * eraseMask.b;
		float ripplesIntensity = _RipplesIntensity * ripplesAndSpotsMask * rippleNormalAmp;
		
		if(ripplesAmount > 0)
		{
			rippleNormals = ConstructNormal(ripplesAndSpots.rg, ripplesIntensity);
			rippleNormals = BlendNormalRNM(flatNormal, rippleNormals);
			spots = saturate((((ripplesAndSpots.b * step((1.0 - _SpotsAmount), ripplesAndSpots.b)) * ripplesAndSpotsMask * 2) * _SpotsIntensity) * 5);
		}
		else
		{
			rippleNormals = flatNormal;
			spots = 0;
		}
	#else
		rippleNormals = flatNormal; // use flat normal
		spots = 0;
	#endif
	
	float dripsAndSpots = saturate((dripsMask + spots));
	
	float2 puddlesUV = _PuddlesTiling * positionWS.xz * 0.1;
	float puddlesMask = SAMPLE_TEXTURE2D(_PrimaryMasks, sampler_PrimaryMasks, puddlesUV).r;
	puddlesMask = smoothstep(_PuddlesRange.x, _PuddlesRange.y, _PuddlesAmount * puddlesMask);
	float puddlesSlope = smoothstep((1.0 - _PuddlesSlope), 1.0, geomNormalWS.y);
	float finalPuddlesMask = saturate(pow((((1.0 - smoothstep(0, 1.0, approximateSurfHeight)) * saturate((eraseMask.g * (addMask.g + (puddlesMask * volumeMask)) * puddlesSlope))) * 4) + (saturate((eraseMask.g * (addMask.g + (puddlesMask * volumeMask)) * puddlesSlope)) * 2), 1));
	
	blendNormals = lerp(blendNormals, rippleNormals, finalPuddlesMask);
	float wetnessBlendByNormals = smoothstep(_BlendByNormalsPower, 1.0, approximateSurfHeight);
	float finalAreaMask = saturate(pow(abs(((wetnessBlendByNormals * (eraseMask.r * (volumeMask + addMask.r))) * 4) + ((eraseMask.r * (volumeMask + addMask.r)) * 2)), _BlendByNormalsStrength));
	
	#if defined (SRS_TERRAIN) && !defined(SRS_TERRAIN_BAKE_SHADER)
        float distanceFade = GetDistanceGradient(positionWS, _DistanceFadeFalloff + _DistanceFadeStart + 0.001, _DistanceFadeStart); 
    #else
        float distanceFade = 1;
    #endif

	float wetness = _WetnessAmount * dirMask;

	float3 blendAlbedo = lerp(albedo, (albedo * _WetColor), finalAreaMask * wetness);
	blendAlbedo = lerp(blendAlbedo, blendAlbedo * _PuddlesMult, finalPuddlesMask);

	float blendSmoothness = lerp(saturate((smoothness + (wetness * finalAreaMask))), 0.9, saturate(dripsAndSpots * 2.0));
	blendSmoothness = lerp(blendSmoothness, 0.99, finalPuddlesMask);

	#if defined(SRS_TERRAIN)

		half3 distantNormal = SAMPLE_TEXTURE2D(_NormalLOD, sampler_SRS_depth, uv).rgb;
		distantNormal = distantNormal * 2 - 1;
		distantNormal = distantNormal.xzy;
		normalWS = lerp(distantNormal, blendNormals, distanceFade);
        half4 distantAlbedoSmoothness = SAMPLE_TEXTURE2D(_AlbedoLOD, sampler_SRS_depth, uv);
		albedo = lerp(distantAlbedoSmoothness.rgb, blendAlbedo, distanceFade);
        smoothness = lerp(distantAlbedoSmoothness.a, blendSmoothness, distanceFade);

	#else
		normalWS = blendNormals;
		albedo = blendAlbedo;
		smoothness = blendSmoothness;
	#endif
}
#endif

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
