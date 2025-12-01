#pragma once

#if defined(SHADER_API_D3D11) || defined(SHADER_API_XBOXONE) || defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE) || defined(SHADER_API_VULKAN) || defined(SHADER_API_METAL)
	#define UNITY_SAMPLE_TEX2DARRAY_GRAD(tex,samper,coord,dx,dy) tex.SampleGrad (samper,coord,dx,dy)
#else
	#if defined(UNITY_COMPILER_HLSL2GLSL) || defined(SHADER_TARGET_SURFACE_ANALYSIS)
		#define UNITY_SAMPLE_TEX2DARRAY_GRAD(tex,samper,coord,dx,dy) tex2DArray(tex,coord,dx,dy)
	#endif
#endif

float InverseLerp(float a, float b, float t)
{
    return (t - a)/(b - a);
}

real GetDistanceGradient(float3 posWS, real maxDistance, real startDistance = 0)
{
	real dist = distance(_WorldSpaceCameraPos, posWS);
    real distGradient = saturate(InverseLerp(maxDistance, startDistance, dist));
	return distGradient;
}

real LevelsControlInputRange(real val, real minInput, real maxInput) 
{
	return min(max(val - minInput, 0) / (maxInput - minInput), 1);
}

real Remap(real a, real b, real x)
{
	return x * (1.0 / (b - a)) - (a / (b - a));
}

real3 Remap3D(real a, real b, real3 x)
{
	return x * (1.0 / (b - a)) - (a / (b - a));
}

half DitherAnimated(float2 screenPos)
{
	half framesCount = 32;
    float2 uv = screenPos * _ScreenParams.xy * _BlueNoise_TexelSize.xy;

	float frameNumber = unity_DeltaTime.y * _Time.y + (framesCount - 1);
	frameNumber /= framesCount;
	frameNumber = frac(frameNumber) * framesCount;
	float oneMinusTexelS = 1 - _BlueNoise_TexelSize.x * _BlueNoise_TexelSize.y;

	half dither = SAMPLE_TEXTURE2D_ARRAY_LOD(_BlueNoise, sampler_BlueNoise, uv, frameNumber, 0).r * oneMinusTexelS + oneMinusTexelS;

	return dither;
}

half Dither(float2 screenPos)
{
    float2 uv = screenPos * _ScreenParams.xy * _BlueNoise_TexelSize.xy;

	float oneMinusTexelS = 1 - _BlueNoise_TexelSize.x * _BlueNoise_TexelSize.y;

	half dither = SAMPLE_TEXTURE2D_ARRAY_LOD(_BlueNoise, sampler_BlueNoise, uv, 0, 0).r * oneMinusTexelS + oneMinusTexelS;

	return dither;
}

//height blend coverage with the base surface
float HeightBlendTriplanar(float sourceMask, float3 height, float3 worldMasks, float blendHardness, float blendStrength)
{
	float blendTop = (height.x + height.z) * worldMasks.y * 0.3;
	float blendSides = (worldMasks.x + worldMasks.z) * height.y;
	float heightMix = blendTop + blendSides;
	heightMix = saturate(heightMix * heightMix * blendHardness);
    float blendResult = saturate(pow(((heightMix * sourceMask) * 4) + (sourceMask * 2), blendStrength));
    return blendResult;
}

float BlendBasicMaskWithHeight(float height1, float height2, float blendFactor)
{
	float input1 = 1;
	float input2 = 0;
    float height_start = max(height1, height2) - blendFactor;
    float level1 = max(height1 - height_start, 0);
    float level2 = max(height2 - height_start, 0);
    return ((input1 * level1) + (input2 * level2)) / (level1 + level2);
}

float3 PerturbNormal( float3 surf_pos, float3 surf_norm, float height, float scale )
{
	// "Bump Mapping Unparametrized Surfaces on the GPU" by Morten S. Mikkelsen
	float3 vSigmaS = ddx( surf_pos );
	float3 vSigmaT = ddy( surf_pos );
	float3 vN = surf_norm ;
	float3 vR1 = cross( vSigmaT , vN );
	float3 vR2 = cross( vN , vSigmaS );
	float fDet = dot( vSigmaS , vR1 );
	float dBs = ddx( height );
	float dBt = ddy( height );
	float3 vSurfGrad = scale * 0.05 * sign( fDet ) * ( dBs * vR1 + dBt * vR2 );
	return normalize (abs(fDet) * vN - vSurfGrad);
}

float linstep(float a, float b, float v) {
    return saturate((v - a) / (b - a));
}

float reduceLightBleeding(float pMax, float amount) {
   // Remove the [0, amount] tail and linearly rescale (amount, 1].
   return linstep(amount, 1.0, pMax);
}


float chebyshevUpperBound(float2 moments, float mean, float minVariance, float lightBleedingReduction) {
	// Compute variance
	float variance = moments.y - (moments.x * moments.x);
	variance = max(variance, minVariance);

	// Compute probabilistic upper bound
	float d = mean - moments.x;
	float pMax = variance / (variance + (d * d));

	pMax = reduceLightBleeding(pMax, lightBleedingReduction);

	// One-tailed Chebyshev
	return (mean <= moments.x ? 1.0 : pMax);
}

/*
//partially works on both DX and GL
float chebyshevUpperBound(float2 moments, float mean, float minVariance, float lightBleedingReduction) {
    // Compute variance
    float variance = moments.y - (moments.x * moments.x);
    variance = max(variance, minVariance);

    // Compute probabilistic upper bound
    float d = mean - moments.x;
    float pMax = variance / (variance + (d * d));

    pMax = reduceLightBleeding(pMax, lightBleedingReduction);

    // One-tailed Chebyshev
	float result;
	#if !defined(UNITY_REVERSED_Z)
		result = (mean >= moments.x ? 1.0 : pMax);
	#else
		result = (mean <= moments.x ? 1.0 : pMax);
	#endif
	
    return result;
}
*/

float ComputeBasicAreaMask(float2 moments, float z, float vsmBias, float coverageAreaMaskRange, float bleedReduction) {
    z = 2.0 * -z - 1.0;
	
    float warpedDepth = exp(_VsmExp * z);

    float VSMBias = vsmBias;
    float depthScale = VSMBias * _VsmExp * warpedDepth;
    float minVariance1 = depthScale * depthScale;
	float mask = chebyshevUpperBound(moments.xy, warpedDepth, minVariance1, bleedReduction);
	mask = saturate(smoothstep(0, 10 * coverageAreaMaskRange, (5.0 * mask)) * 2);
	mask = sqrt(mask);
    return mask;
}


void PaintMaskRGBA(float4 paintedMask, out float4 addMask, out float4 eraseMask)
{
	float4 paintMode = round(paintedMask);
	float4 addCov = (paintedMask - 0.5) * 2.0;
	float4 removeCov = paintedMask * 2;
	float4 paintResult = lerp(removeCov, addCov, paintMode);
	
	addMask = lerp(0, paintResult, paintMode);
	eraseMask = lerp(paintResult, 1, paintMode);
}
/*
float PaintMask(float paintInput, out float addMask, out float eraseMask)
{
	float paintMode = round(paintInput);
	float addCov = (paintInput - 0.5) * 2.0;
	float removeCov = paintInput * 2;
	float paintResult = lerp(removeCov, addCov, paintMode);
	
	addMask = lerp(0, paintResult, paintMode);
	eraseMask = lerp(paintResult, 1, paintMode);
}
*/

float DirMask(float2 dirRange, float3 depthCamDir, float3 surfNormal, float dirOffset)
{
	float dirMask = smoothstep(dirRange.x, dirRange.y, (1.0 - saturate((dot(depthCamDir, surfNormal) + 1.0) - dirOffset)));
	return saturate(dirMask);
}

float CalcVolumeMask(float covAreaHardness, float2 uv, float relativeDepth)
{
	float volumeMask = min(min((1.0 - uv.x), (1.0 - uv.y)), min(uv.x, uv.y));
	
	volumeMask = min(volumeMask, relativeDepth);
	volumeMask = smoothstep(0, (1.0 - ((covAreaHardness * 0.5) + 0.5)), volumeMask);
	
	return volumeMask;
}

float CalcBorderMask(float covAreaHardness, float2 uv)
{
	float borderMask = min(min((1.0 - uv.x), (1.0 - uv.y)), min(uv.x, uv.y));
	borderMask = smoothstep(0.0, (1.0 - ((covAreaHardness * 0.5) + 0.5)), borderMask);
	return borderMask;
}


float SplatMask(float covAreaHardness, float2 uv, float relativeDepth, float a, float b)
{
	float volumeMask = smoothstep(0.0, (1.0 - ((covAreaHardness * 0.5) + 0.5)), min(min(min((1.0 - uv.x), (1.0 - uv.y)), min(uv.x, uv.y)), relativeDepth));
	float splatMask = saturate(lerp(a, (a * b), volumeMask));
	return splatMask;
}

real4 SummedBlend4D(real4 a, real4 b, real4 c, real3 weight)
{
	return a * weight.x + b * weight.y + c * weight.z;
}

real3 SummedBlend3D(real3 a, real3 b, real3 c, real3 weight)
{
	return a * weight.x + b * weight.y + c * weight.z;
}

real2 SummedBlend2D(real2 a, real2 b, real2 c, real3 weight)
{
	return a * weight.x + b * weight.y + c * weight.z;
}

real SummedBlend1D(real a, real b, real c, real3 weight)
{
	return a * weight.x + b * weight.y + c * weight.z;
}

half3 ConstructNormal(half2 inputVector, float scale)
{
	half3 n;
    n.xy = inputVector.xy * 2 - 1;
    n.xy *= scale;
    n.z = sqrt(1 - saturate(dot(n.xy, n.xy)));
    n = normalize(n);
	return n;
}

half3 BlendNormalsWS(half3 n1, half3 n2, half3 nMeshWS)
{
    float4 q = float4(cross(nMeshWS, n2), dot(nMeshWS, n2) + 1) / sqrt(2 * (dot(nMeshWS, n2) + 1));
    return n1 * (q.w * q.w - dot(q.xyz, q.xyz)) + 2 * q.xyz * dot(q.xyz, n1) + 2 * q.w * cross(q.xyz, n1);
}

half3 BlendReorientedNormal(half3 n1, half3 n2)
{
    n1.z += 1;
    n2.xy = -n2.xy;
	half3 n = n1 * dot(n1, n2) / n1.z - n2;
	n = clamp(n, -0.999, 0.999);
    return n;
}

float2 Hash2D2D (float2 s)
{
	return frac(sin(fmod(float2(dot(s, float2(127.1,311.7)), dot(s, float2(269.5,183.3))), 3.14159))*43758.5453);
}

float4x3 CalcBW_vx(float2 uv)
{

	float2 skewUV = mul(float2x2 (1.0 , 0.0 , -0.57735027 , 1.15470054), uv * 3.464);

	float2 vxID = float2 (floor(skewUV));
	float3 barry = float3 (frac(skewUV), 0);
	barry.z = 1.0-barry.x-barry.y;
 
	float4x3 BW_vx = ((barry.z>0) ? 
		float4x3(float3(vxID, 0), float3(vxID + float2(0, 1), 0), float3(vxID + float2(1, 0), 0), barry.zyx) :
		float4x3(float3(vxID + float2 (1, 1), 0), float3(vxID + float2 (1, 0), 0), float3(vxID + float2 (0, 1), 0), float3(-barry.z, 1.0-barry.y, 1.0-barry.x)));
	
	return BW_vx;
}

float4 StochasticTex2DArray(Texture2DArray tex, SamplerState ss, float2 uv, half curFrame)
{
	float4x3 BW_vx = CalcBW_vx(uv);
	float2 dx = ddx(uv);
	float2 dy = ddy(uv);

	return mul(UNITY_SAMPLE_TEX2DARRAY_GRAD(tex, ss, float3(uv + Hash2D2D(BW_vx[0].xy), curFrame), dx, dy), BW_vx[3].x) + 
		   mul(UNITY_SAMPLE_TEX2DARRAY_GRAD(tex, ss, float3(uv + Hash2D2D(BW_vx[1].xy), curFrame), dx, dy), BW_vx[3].y) + 
		   mul(UNITY_SAMPLE_TEX2DARRAY_GRAD(tex, ss, float3(uv + Hash2D2D(BW_vx[2].xy), curFrame), dx, dy), BW_vx[3].z);
}

//Unity macros version
float4 StochasticTex2D(Texture2D tex, SamplerState ss, float2 uv)
{
	float4x3 BW_vx = CalcBW_vx(uv);
	float2 dx = ddx(uv);
	float2 dy = ddy(uv);
 
	return mul(SAMPLE_TEXTURE2D_GRAD(tex, ss, uv + Hash2D2D(BW_vx[0].xy), dx, dy), BW_vx[3].x) + 
		   mul(SAMPLE_TEXTURE2D_GRAD(tex, ss, uv + Hash2D2D(BW_vx[1].xy), dx, dy), BW_vx[3].y) + 
		   mul(SAMPLE_TEXTURE2D_GRAD(tex, ss, uv + Hash2D2D(BW_vx[2].xy), dx, dy), BW_vx[3].z);
}

//Unity macros version
float4 StochasticTex2DLod(Texture2D tex, SamplerState ss, float2 uv, float lod)
{
	float4x3 BW_vx = CalcBW_vx(uv);

	return mul(SAMPLE_TEXTURE2D_LOD(tex, ss, uv + Hash2D2D(BW_vx[0].xy), lod), BW_vx[3].x) + 
		   mul(SAMPLE_TEXTURE2D_LOD(tex, ss, uv + Hash2D2D(BW_vx[1].xy), lod), BW_vx[3].y) + 
		   mul(SAMPLE_TEXTURE2D_LOD(tex, ss, uv + Hash2D2D(BW_vx[2].xy), lod), BW_vx[3].z);
}

#if defined (_USE_BLUE_NOISE_DITHER)
void AnimatedCrossFadeLOD(float2 vpos)
{
    float2 uv = vpos/16; //devide by texture size to get a pixel perfect texture on screen
    half framesCount = 32;
	float frameNumber = unity_DeltaTime.y * _Time.y + (framesCount - 1);
	frameNumber /= framesCount;
	frameNumber = frac(frameNumber) * framesCount;
	float oneMinusTexelS = 1 - _BlueNoise_TexelSize.x * _BlueNoise_TexelSize.y;
	half dither = UNITY_SAMPLE_TEX2DARRAY_LOD(_BlueNoise, float3(uv, frameNumber), 0).r;
    float sgn = unity_LODFade.x > 0 ? 1.0f : -1.0f;
    clip(unity_LODFade.x - dither * sgn);
}
#endif

#include "SRS_Triplanar.hlsl"
