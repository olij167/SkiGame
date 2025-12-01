#ifndef SRS_TRIPLANAR_INCLUDED
#define SRS_TRIPLANAR_INCLUDED

half3 GetAxisProjection(half3 normalWS)
{
	half3 output = 0;
	half3 vec = normalWS;
	vec.x += 0.0001;
	vec.y += 0.0001;

	vec = abs(vec);
	vec = pow(vec, 50);
	vec /= vec.x + vec.y + vec.z;
	vec = saturate(Remap3D(0.5, 0.501, vec));

	half y = saturate(1-length(vec));
	vec.y += y;

	output = vec;

	return output;
}

half3 CalcSingleSampleTriplanarNormal(half2 srcTriNormal, half3 meshNormalWS, half3 axisMask, half normalScale)
{
	half3 n = ConstructNormal(srcTriNormal, normalScale);

	half2 xDir = n.xy + meshNormalWS.bg;
	half2 yDir = n.xy + meshNormalWS.rb;
	half2 zDir = n.xy + meshNormalWS.rg;

	half absNz = abs(n.z);

	half xMask = absNz * meshNormalWS.x;
	half yMask = absNz * meshNormalWS.y;
	half zMask = absNz * meshNormalWS.z;

	half3 nX = half3(xMask, xDir.yx);
	half3 nY = half3(yDir.x, yMask, yDir.y);
	half3 nZ = half3(zDir.xy, zMask);

	half3 finalNormalWS = normalize(nX * axisMask.x + nY * axisMask.y + nZ * axisMask.z);

	return finalNormalWS;
}


float2 CalcFastTriUV(float3 positionWS, half3 axisMask, half tiling)
{
	positionWS *= tiling;

	float2 xDir = positionWS.bg;
	float2 yDir = positionWS.rg;
	float2 zDir = positionWS.rb;

	xDir *= axisMask.xx;
	yDir *= axisMask.zz;
	zDir *= axisMask.yy;

	return xDir + yDir + zDir;
}

////////////////////////////////////////////////////////////////
///Fast Single Samper Triplanar hard edge | R
real4 FastTriplanar_R(Texture2D tex, SamplerState ss, float3 positionWS, half3 normalWS, half tiling)
{
	half3 axisMask = GetAxisProjection(normalWS);
	float2 uv = CalcFastTriUV(positionWS, axisMask, tiling);
	return SAMPLE_TEXTURE2D(tex, ss, uv).r;
}

////////////////////////////////////////////////////////////////
///Fast Single Samper Triplanar hard edge | RGBA
real4 FastTriplanar_RGBA(Texture2D tex, SamplerState ss, float3 positionWS, half3 normalWS, half tiling)
{
	half3 axisMask = GetAxisProjection(normalWS);
	float2 uv = CalcFastTriUV(positionWS, axisMask, tiling);
	return SAMPLE_TEXTURE2D(tex, ss, uv);
}

////////////////////////////////////////////////////////////////
///Fast Single Samper Triplanar hard edge | RGB - normal, A - mask from initial tex B channel
real4 FastTriplanar_Normals_MaskZ(Texture2D tex, SamplerState ss, float3 positionWS, half3 normalWS, half tiling, half normalScale)
{
	half3 axisMask = GetAxisProjection(normalWS);
	float2 uv = CalcFastTriUV(positionWS, axisMask, tiling);
	real4 sampledTex = SAMPLE_TEXTURE2D(tex, ss, uv);

	half3 finalNormalWS = CalcSingleSampleTriplanarNormal(sampledTex.xy, normalWS, axisMask, normalScale);

	return real4(finalNormalWS, sampledTex.z);
}

////////////////////////////////////////////////////////////////
half3 GetTriBlendMask(float3 normal, half contrast)
{
	float3 alpha = pow(abs(normal), contrast);
	alpha /= dot(1, alpha);
	return alpha;
}

half3 CalcFastTriplanarNormalSoft(half2 srcTriNormal, half3 meshNormalWS, half3 axisMask, half normalScale)
{
	half3 n = ConstructNormal(srcTriNormal, normalScale);

	half2 xDir = n.xy + meshNormalWS.bg;
	half2 yDir = n.yx + meshNormalWS.rb;
	half2 zDir = n.xy + meshNormalWS.rg;

	half absNz = abs(n.z);

	half xMask = absNz * meshNormalWS.x;
	half yMask = absNz * meshNormalWS.y;
	half zMask = absNz * meshNormalWS.z;

	half3 nX = half3(xMask, xDir.yx);
	half3 nY = half3(yDir.x, yMask, yDir.y);
	half3 nZ = half3(zDir.xy, zMask);


	half3 finalNormalWS = nX * axisMask.x + nY * axisMask.y + nZ * axisMask.z;

	return normalize(finalNormalWS);
}

void CalcFastTriSoftUV(float3 position, half tiling, half dither, half3 triMask, out float4 derivatives, out float2 uv)
{
	float2 uvX = position.zy;
	float2 uvY = position.zx;
	float2 uvZ = position.xy;

	dither *= 0.5;

	float3 duvwdx = ddx((position * 1.3) * tiling);
	float3 duvwdy = ddy((position * 1.3) * tiling);

	float2 duvdx; 
	float2 duvdy; 

	float2 tempUV = 0;

	if (triMask.x > dither) {
	    tempUV = uvX;
	    duvdx = duvwdx.zy;
	    duvdy = duvwdy.zy;
	} else if (1.0 - triMask.z > dither) {
	    tempUV = uvY;
	    duvdx = duvwdx.zx;
	    duvdy = duvwdy.zx;	
	} else {
	    tempUV = uvZ;
	    duvdx = duvwdx.xy;
	    duvdy = duvwdy.xy;
	}

	tempUV *= tiling;
	uv = tempUV;
	derivatives = float4(duvdx, duvdy);
}

half2 CalcFastTriSoftUV_LOD(float3 position, half tiling, half dither, half3 triMask)
{
	float2 uvX = position.zy;
	float2 uvY = position.zx;
	float2 uvZ = position.xy;

	dither *= 0.5;

	float2 uv = 0;

	if (triMask.x > dither) {
	    uv = uvX;
	} else if (1.0 - triMask.z > dither) {
	    uv = uvY;
	} else {
	    uv = uvZ;
	}

	uv *= tiling;

	return uv;
}


///////////////////////////////////////////////////////////////////////////////////////////
///Fast Single Sampler Triplanar Mapping | RGBA
real4 FastTriplanarSoft_RGBA(Texture2D tex, SamplerState ss, float3 position, half3 normal, half tiling, half dither, half3 triMask)
{
	float4 derivatives = 0;
	float2 uv = 0;
	CalcFastTriSoftUV(position, tiling, dither, triMask, derivatives, uv);

    return SAMPLE_TEXTURE2D_GRAD(tex, ss, uv, derivatives.xy, derivatives.zw);
}

///////////////////////////////////////////////////////////////////////////////////////////
///Fast Single Sampler Triplanar Mapping | R
real FastTriplanarSoft_R(Texture2D tex, SamplerState ss, float3 position, half3 normal, half tiling, half dither, half3 triMask)
{
	float4 derivatives = 0;
	float2 uv = 0;
	CalcFastTriSoftUV(position, tiling, dither, triMask, derivatives, uv);

    return SAMPLE_TEXTURE2D_GRAD(tex, ss, uv, derivatives.xy, derivatives.zw).r;
}

///////////////////////////////////////////////////////////////////////////////////////////
///Fast Single Sampler Triplanar Mapping | R LOD (vertex shader version)
real FastTriplanarSoft_R(Texture2D tex, SamplerState ss, float3 position, half3 normal, half tiling, half dither, half3 triMask, half lod)
{
	float2 uv = CalcFastTriSoftUV_LOD(position, tiling, dither, triMask);

    return SAMPLE_TEXTURE2D_LOD(tex, ss, uv, lod).r;
}

///////////////////////////////////////////////////////////////////////////////////////////
/// Fast Single Sampler Triplanar Mapping (packed with normals and one extra mask):
/// Input normal in RG, and an extra mask in B. Assemble triplanar UV, sample the texture,
/// calculate XYZ world normal and output it along with the extra mask packed in A.
real4 FastTriplanarSoft_Normals_MaskZ(Texture2D tex, SamplerState ss, float3 position, half3 normal, half contrast, half tiling, half normalScale, half dither)
{
	half3 triMask = GetTriBlendMask(normal, contrast);
	real4 sampledTex = FastTriplanarSoft_RGBA(tex, ss, position, normal, tiling, dither, triMask);
	half3 finalNormalWS = CalcFastTriplanarNormalSoft(sampledTex.xy, normal, triMask, normalScale);

	real4 output = real4(finalNormalWS.xyz, sampledTex.z);

	return output;
}

///////////////////////////////////////////////////////////////////////////////////////////
/// Fast Single Sampler Triplanar Mapping (packed with normals and one extra mask):
/// Input normal in RG, and an extra mask in A. Assemble triplanar UV, sample the texture,
/// calculate XYZ world normal and output it along with the extra mask packed in A.
real4 FastTriplanarSoft_Normals_MaskW(Texture2D tex, SamplerState ss, float3 position, half3 normal, half contrast, half tiling, half normalScale, half dither)
{
	half3 triMask = GetTriBlendMask(normal, contrast);
	real4 sampledTex = FastTriplanarSoft_RGBA(tex, ss, position, normal, tiling, dither, triMask);
	half3 finalNormalWS = CalcFastTriplanarNormalSoft(sampledTex.xy, normal, triMask, normalScale);

	real4 output = real4(finalNormalWS.xyz, sampledTex.w);

	return output;
}

real4 TrueTriplanar(Texture2D tex, SamplerState ss, float3 positionWS, half3 normalWS, half tiling, half normalScale)
{
	float3 absGeomNormal = abs(normalWS);
	float3 worldMasks = saturate(absGeomNormal - (0.3).xxx);
	
	float2 uvX = positionWS.zy * tiling.xx;
    float2 uvY = positionWS.xz * tiling.xx;
    float2 uvZ = positionWS.xy * tiling.xx;

    float4 coverageMasks0X = SAMPLE_TEXTURE2D(tex, ss, uvX);
	float4 coverageMasks0Y = SAMPLE_TEXTURE2D(tex, ss, uvY);
	float4 coverageMasks0Z = SAMPLE_TEXTURE2D(tex, ss, uvZ);

    float2 heightAndSmoothness = saturate(coverageMasks0Y.zw * worldMasks.y + coverageMasks0X.zw * worldMasks.x + coverageMasks0Z.zw * worldMasks.z);

    half3 nX = ConstructNormal(coverageMasks0X.rg, normalScale);
    half3 nY = ConstructNormal(coverageMasks0Y.rg, normalScale);
    half3 nZ = ConstructNormal(coverageMasks0Z.rg, normalScale);

	//swizzle world normals to match tangent space and apply reoriented normal mapping blend
    nX = BlendReorientedNormal(half3(normalWS.zy, absGeomNormal.x), nX);
    nY = BlendReorientedNormal(half3(normalWS.xz, absGeomNormal.y), nY);
    nZ = BlendReorientedNormal(half3(normalWS.xy, absGeomNormal.z), nZ);

    //prevent return value of 0
    half3 axisSign = normalWS < 0 ? -1 : 1;

    // apply world space sign to tangent space Z
    nX.z *= axisSign.x;
    nY.z *= axisSign.y;
    nZ.z *= axisSign.z;

    //swizzle tangent normals to match world normal and blend together
    half3 n = normalize(
    nX.zyx * worldMasks.x +
    nY.xzy * worldMasks.y +
    nZ.xyz * worldMasks.z
    );

	return real4(n, heightAndSmoothness.x);
}

#endif