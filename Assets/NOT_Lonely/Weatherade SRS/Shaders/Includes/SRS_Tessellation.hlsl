#ifndef SRS_TESSELLATION_INCLUDED
#define SRS_TESSELLATION_INCLUDED 

struct TessellationFactors {
    float edge[3] : SV_TessFactor;
    float inside : SV_InsideTessFactor;
};

struct TessellationControlPoint {
	float4 positionOS : INTERNALTESSPOS;
	float3 normalOS : NORMAL;
    float3 texcoord : TEXCOORD0;
    #if !defined(SRS_TERRAIN)
        float4 color : COLOR0;
    #endif
    #if defined(_USE_AVERAGED_NORMALS)
        half3 unifiedNormal : TEXCOORD3;
    #endif
    #if defined (SRS_UNIVERSAL_FORWARD_LIT_PASS_INCLUDED) || defined (SRS_LIT_GBUFFER_PASS_INCLUDED)
	    float4 tangentOS : TANGENT;
	    float2 staticLightmapUV : TEXCOORD1;
        float2 dynamicLightmapUV  : TEXCOORD2;
    #endif
    UNITY_VERTEX_INPUT_INSTANCE_ID 
    UNITY_VERTEX_OUTPUT_STEREO
};

bool TriangleIsBelowClipPlane (float3 p0, float3 p1, float3 p2, int planeIndex, float bias) {
	float4 plane = unity_CameraWorldClipPlanes[planeIndex];
	return
		dot(float4(p0, 1), plane) < bias &&
		dot(float4(p1, 1), plane) < bias &&
		dot(float4(p2, 1), plane) < bias;
}

bool TriangleIsCulled (float3 p0, float3 p1, float3 p2, float bias) {
	return TriangleIsBelowClipPlane(p0, p1, p2, 0, bias) || 
    TriangleIsBelowClipPlane(p0, p1, p2, 1, bias) || 
    TriangleIsBelowClipPlane(p0, p1, p2, 2, bias) || 
    TriangleIsBelowClipPlane(p0, p1, p2, 3, bias);
}

float3 BarycentricInterpolate(float3 bary, float3 a, float3 b, float3 c) {
    return bary.x * a + bary.y * b + bary.z * c;
}

float TessellationEdgeFactor (float3 p0, float3 p1, float mask) 
{
	float edgeLength = distance(p0, p1);

	float3 edgeCenter = (p0 + p1) * 0.5;
	float viewDistance = distance(edgeCenter, _WorldSpaceCameraPos);
    float factor = edgeLength * _ScreenParams.y / (_TessEdgeL * viewDistance);
    return max(1, factor * mask);
}

//Distance based tess functions
float GetDistTessFactor (float3 posWS, float minDist, float maxDist, float tess)
{
   float dist = distance (posWS, _WorldSpaceCameraPos);
   return clamp(1.0 - (dist - minDist) / (maxDist - minDist), 0.01, 1.0) * tess;
}

float4 GetEdgeFactors (float3 triFactors)
{
   float4 tess;
   tess.x = (triFactors.y + triFactors.z) * 0.5;
   tess.y = (triFactors.x + triFactors.z) * 0.5;
   tess.z = (triFactors.x + triFactors.y) * 0.5;
   tess.w = (triFactors.x + triFactors.y + triFactors.z) * (1 / 3.0);
   return tess;
}

float4 DistanceBasedTessellation (float3 p0, float3 p1, float3 p2, float minDist, float maxDist, float tess)
{
   float3 factors;
   factors.x = GetDistTessFactor (p0, minDist, maxDist, tess);
   factors.y = GetDistTessFactor (p1, minDist, maxDist, tess);
   factors.z = GetDistTessFactor (p2, minDist, maxDist, tess);
   return GetEdgeFactors (factors);
}
//

TessellationFactors PatchConstantFunction (InputPatch<TessellationControlPoint, 3> patch) {

    float3 p0 = TransformObjectToWorld(patch[0].positionOS.xyz).xyz;
	float3 p1 = TransformObjectToWorld(patch[1].positionOS.xyz).xyz;
	float3 p2 = TransformObjectToWorld(patch[2].positionOS.xyz).xyz;

	TessellationFactors f = (TessellationFactors)0;
    if (TriangleIsCulled(p0, p1, p2, -_TessMaxDisp)) {
		f.edge[0] = f.edge[1] = f.edge[2] = f.inside = 0;
	}
	else {

        //data driven tess multipliers
        float3 multipliers = 0;
        [unroll] for(int i = 0; i < 3; i++)
        {
            multipliers[i] = patch[i].texcoord.z;
        }

        float3 mask;
        mask.x = (multipliers[1] + multipliers[2]) / 2;
        mask.y = (multipliers[2] + multipliers[0]) / 2;
        mask.z = (multipliers[0] + multipliers[1]) / 2;
        
        //Edge length tessellation
        f.edge[0] = TessellationEdgeFactor(p1, p2, mask.x);
        f.edge[1] = TessellationEdgeFactor(p2, p0, mask.y);
        f.edge[2] = TessellationEdgeFactor(p0, p1, mask.z);
	    f.inside = (TessellationEdgeFactor(p1, p2, mask.x) + TessellationEdgeFactor(p2, p0, mask.y) + TessellationEdgeFactor(p0, p1, mask.z)) * (1 / 3.0);
        //

        /*
        //distance based tessellation
        float4 factors = DistanceBasedTessellation(p0, p1, p2, 10, 50, 12);
        f.edge[0] = factors.x;
        f.edge[1] = factors.y;
        f.edge[2] = factors.z;
        f.inside = factors.w;
        //
        */
    }

	return f;
}

[domain("tri")]
[outputcontrolpoints(3)]
[outputtopology("triangle_cw")]
[partitioning("fractional_odd")]
[patchconstantfunc("PatchConstantFunction")]
TessellationControlPoint HullProgram (InputPatch<TessellationControlPoint, 3> patch, uint id : SV_OutputControlPointID) 
{
	return patch[id];
}

[domain("tri")]
Varyings DomainProgram (TessellationFactors factors, const OutputPatch<TessellationControlPoint, 3> patch, float3 barycentricCoordinates : SV_DomainLocation) 
{
	Attributes data = (Attributes)0;

    UNITY_TRANSFER_INSTANCE_ID(patch[0], data);

    #define DOMAIN_PROGRAM_INTERPOLATE(fieldName) data.fieldName = \
		patch[0].fieldName * barycentricCoordinates.x + \
		patch[1].fieldName * barycentricCoordinates.y + \
		patch[2].fieldName * barycentricCoordinates.z;

	DOMAIN_PROGRAM_INTERPOLATE(positionOS)
    DOMAIN_PROGRAM_INTERPOLATE(normalOS)
    DOMAIN_PROGRAM_INTERPOLATE(texcoord)

    #if defined (SRS_UNIVERSAL_FORWARD_LIT_PASS_INCLUDED) || defined (SRS_LIT_GBUFFER_PASS_INCLUDED)
	    DOMAIN_PROGRAM_INTERPOLATE(tangentOS)
	    DOMAIN_PROGRAM_INTERPOLATE(staticLightmapUV)
        DOMAIN_PROGRAM_INTERPOLATE(dynamicLightmapUV)
    #endif

    #if !defined(SRS_TERRAIN)
        DOMAIN_PROGRAM_INTERPOLATE(color)
        #if defined(_USE_AVERAGED_NORMALS)
            DOMAIN_PROGRAM_INTERPOLATE(unifiedNormal)
        #endif
    #endif
    
    /*
    //Phong
    float3 pp[3];
	for (int i = 0; i < 3; ++i)
		pp[i] = data.positionOS.xyz - patch[i].normalOS * (dot(data.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
	float phongStrength = 1;
	data.positionOS.xyz = phongStrength * (pp[0]*barycentricCoordinates.x + pp[1]*barycentricCoordinates.y + pp[2]*barycentricCoordinates.z) + (1.0f-phongStrength) * data.positionOS.xyz;
    //
    */


    #if defined(SRS_UNIVERSAL_FORWARD_LIT_PASS_INCLUDED)
        Varyings v = LitPassVertex(data);
    #elif defined (SRS_DEPTH_ONLY_PASS_INCLUDED)
        Varyings v = DepthOnlyVertex(data);
    #elif defined (SRS_SHADOW_CASTER_PASS_INCLUDED) || defined(SRS_TERRAIN_SHADOW_CASTER_PASS)
        Varyings v = ShadowPassVertex(data);
    #elif defined (SRS_LIT_GBUFFER_PASS_INCLUDED)
        Varyings v = LitGBufferPassVertex(data);
    #elif defined (SRS_TERRAIN_UNIVERSAL_FORWARD_PASS) || defined(SRS_TERRAIN_UNIVERSAL_GBUFFER_PASS)
        Varyings v = SplatmapVert(data);
    #elif defined (SRS_TERRAIN_DEPTH_ONLY_INCLUDED)
        Varyings v = DepthOnlyVertex(data);
    #elif defined (SRS_DEPTH_NORMALS_PASS_INCLUDED)
        Varyings v = DepthNormalsVertex(data);
    #endif

    UNITY_TRANSFER_INSTANCE_ID(patch[0], v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(v);

    return v;

}

TessellationControlPoint TessellationVertexProgram (Attributes v) 
{
    TessellationControlPoint p = (TessellationControlPoint)0;

    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, p);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(p);

    #if defined(SRS_TERRAIN)
        TerrainInstancing(v.positionOS, v.normalOS, v.texcoord.xy);
    #endif

    float3 worldPos = TransformObjectToWorld(v.positionOS.xyz);
    float4 relativeSurfPos = mul(_SRS_TraceSurfCamMatrix, float4(worldPos, 1));
    float2 traceMaskUV = (0.5 + ( 0.5 * relativeSurfPos)).xy;
    traceMaskUV.y = 1 - traceMaskUV.y;
    float4 addMask = float4(0, 0, 0, 0);
	float4 eraseMask = float4(1, 1, 1, 1);
	#ifdef _PAINTABLE_COVERAGE_ON
        #ifdef SRS_TERRAIN
            float2 covSplatUV = (v.texcoord.xy * (_PaintedMask_TexelSize.zw - 1.0f) + 0.5f) * _PaintedMask_TexelSize.xy;
	        float4 paintedMask = SAMPLE_TEXTURE2D_LOD(_PaintedMask, sampler_SRS_depth, covSplatUV, 0.0);
	        PaintMaskRGBA(paintedMask, addMask, eraseMask);
        #else
            PaintMaskRGBA(v.color, addMask, eraseMask);
        #endif
	#endif
    float covAmount = (eraseMask.r * _CoverageAmount);
    float covAmountFinal = (1.0 - (covAmount * 2.0));
    //sample VSM (depth coverage mask)
	relativeSurfPos = mul(_depthCamMatrix, float4(worldPos, 1));
	float2 srsDepthUV = (0.5 + ( 0.5 * relativeSurfPos)).xy;
	srsDepthUV.y = 1 - srsDepthUV.y;
    float2 moments = SAMPLE_TEXTURE2D_LOD(_SRS_depth, sampler_SRS_depth, srsDepthUV, 0).xy;
	float basicAreaMask = ComputeBasicAreaMask(moments, relativeSurfPos.z, _CoverageAreaBias, _CoverageAreaMaskRange, _CoverageLeakReduction); //basic occluded area mask
    
    float dirMask = DirMask(_PrecipitationDirRange, _depthCamDir, TransformObjectToWorldNormal(v.normalOS), _PrecipitationDirOffset); //precipitation direction mask
    float volumeMask = CalcVolumeMask(_CoverageAreaFalloffHardness, srsDepthUV, relativeSurfPos.z); //Weatherade volume mask
    
    float snowHeightMix = lerp(0, 1, covAmount);
    float snowMask = lerp(snowHeightMix, basicAreaMask * snowHeightMix, volumeMask) * dirMask;
    snowMask = saturate(snowMask * 10 + addMask.r);
    float snowAreaTess = lerp(0, 1, snowMask * _TessFactorSnow);
    
    float snowdriftSlopeMask = smoothstep(_TessSnowdriftRange.x, _TessSnowdriftRange.y, basicAreaMask * covAmount + addMask.r);
    float snowdriftSlope = sin(PI * snowdriftSlopeMask);
    
    #if defined(_TRACES_ON)
        float traceMaskSampled = SAMPLE_TEXTURE2D_LOD(_SRS_TraceTex, sampler_SRS_depth, traceMaskUV, 0).z;
        float tessMask = saturate(traceMaskSampled * 150 + snowdriftSlope + snowAreaTess);//pack the trace mask into the mesh uv.z to access it from the tessellator
    #else
        float tessMask = saturate(snowdriftSlope + snowAreaTess);
    #endif
    
    v.texcoord.z = tessMask * volumeMask;

	p.positionOS = v.positionOS;
	p.normalOS = v.normalOS;
    p.texcoord = v.texcoord;
    #if defined(_USE_AVERAGED_NORMALS)
        p.unifiedNormal = v.unifiedNormal;
    #endif

    #if !defined(SRS_TERRAIN)
        p.color = v.color;
    #endif

    #if defined (SRS_UNIVERSAL_FORWARD_LIT_PASS_INCLUDED) || defined (SRS_LIT_GBUFFER_PASS_INCLUDED)
	    p.tangentOS = v.tangentOS;
	    p.staticLightmapUV = v.staticLightmapUV;
        p.dynamicLightmapUV = v.dynamicLightmapUV;
    #endif

	return p;
}
#endif