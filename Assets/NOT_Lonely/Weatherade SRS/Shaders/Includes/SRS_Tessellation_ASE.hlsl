#ifndef SRS_TESSELLATION_NEW_INCLUDED
#define SRS_TESSELLATION_NEW_INCLUDED 

uniform float2 _TessSnowdriftRange;
uniform float _TessFactorSnow;

float4 FixedTess( float tessValue )
{
	return tessValue;
}
float CalcDistanceTessFactor (float4 vertex, float minDist, float maxDist, float tess, float4x4 o2w, float3 cameraPos )
{
	float3 wpos = mul(o2w,vertex).xyz;
	float dist = distance (wpos, cameraPos);
	float f = clamp(1.0 - (dist - minDist) / (maxDist - minDist), 0.01, 1.0) * tess;
	return f;
}
float4 CalcTriEdgeTessFactors (float3 triVertexFactors)
{
	float4 tess;
	tess.x = 0.5 * (triVertexFactors.y + triVertexFactors.z);
	tess.y = 0.5 * (triVertexFactors.x + triVertexFactors.z);
	tess.z = 0.5 * (triVertexFactors.x + triVertexFactors.y);
	tess.w = (triVertexFactors.x + triVertexFactors.y + triVertexFactors.z) / 3.0f;
	return tess;
}
float CalcEdgeTessFactor (float3 wpos0, float3 wpos1, float edgeLen, float3 cameraPos, float4 scParams )
{
	float dist = distance (0.5 * (wpos0+wpos1), cameraPos);
	float len = distance(wpos0, wpos1);
	float f = max(len * scParams.y / (edgeLen * dist), 1.0);
	return f;
}
float DistanceFromPlane (float3 pos, float4 plane)
{
	float d = dot (float4(pos,1.0f), plane);
	return d;
}
bool WorldViewFrustumCull (float3 wpos0, float3 wpos1, float3 wpos2, float cullEps, float4 planes[6] )
{
	float4 planeTest;
	planeTest.x = (( DistanceFromPlane(wpos0, planes[0]) > -cullEps) ? 1.0f : 0.0f ) +
					(( DistanceFromPlane(wpos1, planes[0]) > -cullEps) ? 1.0f : 0.0f ) +
					(( DistanceFromPlane(wpos2, planes[0]) > -cullEps) ? 1.0f : 0.0f );
	planeTest.y = (( DistanceFromPlane(wpos0, planes[1]) > -cullEps) ? 1.0f : 0.0f ) +
					(( DistanceFromPlane(wpos1, planes[1]) > -cullEps) ? 1.0f : 0.0f ) +
					(( DistanceFromPlane(wpos2, planes[1]) > -cullEps) ? 1.0f : 0.0f );
	planeTest.z = (( DistanceFromPlane(wpos0, planes[2]) > -cullEps) ? 1.0f : 0.0f ) +
					(( DistanceFromPlane(wpos1, planes[2]) > -cullEps) ? 1.0f : 0.0f ) +
					(( DistanceFromPlane(wpos2, planes[2]) > -cullEps) ? 1.0f : 0.0f );
	planeTest.w = (( DistanceFromPlane(wpos0, planes[3]) > -cullEps) ? 1.0f : 0.0f ) +
					(( DistanceFromPlane(wpos1, planes[3]) > -cullEps) ? 1.0f : 0.0f ) +
					(( DistanceFromPlane(wpos2, planes[3]) > -cullEps) ? 1.0f : 0.0f );
	return !all (planeTest);
}
float4 DistanceBasedTess( float4 v0, float4 v1, float4 v2, float tess, float minDist, float maxDist, float4x4 o2w, float3 cameraPos )
{
	float3 f;
	f.x = CalcDistanceTessFactor (v0,minDist,maxDist,tess,o2w,cameraPos);
	f.y = CalcDistanceTessFactor (v1,minDist,maxDist,tess,o2w,cameraPos);
	f.z = CalcDistanceTessFactor (v2,minDist,maxDist,tess,o2w,cameraPos);
	return CalcTriEdgeTessFactors (f);
}
float4 EdgeLengthBasedTess( float4 v0, float4 v1, float4 v2, float edgeLength, float4x4 o2w, float3 cameraPos, float4 scParams )
{
	float3 pos0 = mul(o2w,v0).xyz;
	float3 pos1 = mul(o2w,v1).xyz;
	float3 pos2 = mul(o2w,v2).xyz;
	float4 tess;
	tess.x = CalcEdgeTessFactor (pos1, pos2, edgeLength, cameraPos, scParams);
	tess.y = CalcEdgeTessFactor (pos2, pos0, edgeLength, cameraPos, scParams);
	tess.z = CalcEdgeTessFactor (pos0, pos1, edgeLength, cameraPos, scParams);
	tess.w = (tess.x + tess.y + tess.z) / 3.0f;
	return tess;
}
float4 EdgeLengthBasedTessCull( float4 v0, float4 v1, float4 v2, float edgeLength, float maxDisplacement, float4x4 o2w, float3 cameraPos, float4 scParams, float4 planes[6] )
{
	float3 pos0 = mul(o2w,v0).xyz;
	float3 pos1 = mul(o2w,v1).xyz;
	float3 pos2 = mul(o2w,v2).xyz;
	float4 tess;
	if (WorldViewFrustumCull(pos0, pos1, pos2, maxDisplacement, planes))
	{
		tess = 0.0f;
	}
	else
	{
		tess.x = CalcEdgeTessFactor (pos1, pos2, edgeLength, cameraPos, scParams);
		tess.y = CalcEdgeTessFactor (pos2, pos0, edgeLength, cameraPos, scParams);
		tess.z = CalcEdgeTessFactor (pos0, pos1, edgeLength, cameraPos, scParams);
		tess.w = (tess.x + tess.y + tess.z) / 3.0f;
	}
	return tess;
}


struct VertexControl
{
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
    #endif
    UNITY_VERTEX_INPUT_INSTANCE_ID 
};
struct TessellationFactors
{
	float edge[3] : SV_TessFactor;
	float inside : SV_InsideTessFactor;
};
VertexControl vert (Attributes v)
{
	VertexControl o = (VertexControl)0;
	UNITY_SETUP_INSTANCE_ID(v);
	UNITY_TRANSFER_INSTANCE_ID(v, o);
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
    float2 moments = SAMPLE_TEXTURE2D_LOD(_SRS_depth, vertex_linear_clamp_sampler, srsDepthUV, 0).xy;
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
        float traceMaskSampled = SAMPLE_TEXTURE2D_LOD(_SRS_TraceTex, vertex_linear_clamp_sampler, traceMaskUV, 0).z;
        float tessMask = saturate(traceMaskSampled * 150 + snowdriftSlope + snowAreaTess);//pack the trace mask into the mesh uv.z to access it from the tessellator
    #else
        float tessMask = saturate(snowdriftSlope + snowAreaTess);
    #endif
    
    v.texcoord.z = tessMask * volumeMask;

	o.positionOS = v.positionOS;
	o.normalOS = v.normalOS;
    o.texcoord = v.texcoord;
    #if defined(_USE_AVERAGED_NORMALS)
        o.unifiedNormal = v.unifiedNormal;
    #endif

    #if !defined(SRS_TERRAIN)
        o.color = v.color;
    #endif

    #if defined (SRS_UNIVERSAL_FORWARD_LIT_PASS_INCLUDED) || defined (SRS_LIT_GBUFFER_PASS_INCLUDED)
	    o.tangentOS = v.tangentOS;
	    o.staticLightmapUV = v.staticLightmapUV;
    #endif
	return o;
}
TessellationFactors TessellationFunction (InputPatch<VertexControl,3> v)
{
	TessellationFactors o;
	float4 tf = 1;
	float edgeLength = _TessEdgeL; float tessMaxDisp = _TessMaxDisp;
	
	tf = EdgeLengthBasedTessCull(v[0].positionOS, v[1].positionOS, v[2].positionOS, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );

	o.edge[0] = tf.x; o.edge[1] = tf.y; o.edge[2] = tf.z; o.inside = tf.w;
	return o;
}
[domain("tri")]
[partitioning("fractional_odd")]
[outputtopology("triangle_cw")]
[patchconstantfunc("TessellationFunction")]
[outputcontrolpoints(3)]
VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
{
	return patch[id];
}
[domain("tri")]
Varyings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
{
	Attributes o = (Attributes) 0;
	o.positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
	o.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
    #if defined (SRS_UNIVERSAL_FORWARD_LIT_PASS_INCLUDED)
	    o.tangentOS = patch[0].tangentOS * bary.x + patch[1].tangentOS * bary.y + patch[2].tangentOS * bary.z;
        o.staticLightmapUV = patch[0].staticLightmapUV * bary.x + patch[1].staticLightmapUV * bary.y + patch[2].staticLightmapUV * bary.z;
    #endif  
	o.texcoord = patch[0].texcoord * bary.x + patch[1].texcoord * bary.y + patch[2].texcoord * bary.z;
	
	#if defined(ASE_PHONG_TESSELLATION)
	float3 pp[3];
	for (int i = 0; i < 3; ++i)
		pp[i] = o.positionOS.xyz - patch[i].normalOS * (dot(o.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
	float phongStrength = _TessPhongStrength;
	o.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * o.positionOS.xyz;
	#endif
	UNITY_TRANSFER_INSTANCE_ID(patch[0], o);

    #if defined(SRS_UNIVERSAL_FORWARD_LIT_PASS_INCLUDED)
	    return LitPassVertex(o);
    #elif defined (SRS_DEPTH_ONLY_PASS_INCLUDED)
        return DepthOnlyVertex(o);
    #endif
}
#endif