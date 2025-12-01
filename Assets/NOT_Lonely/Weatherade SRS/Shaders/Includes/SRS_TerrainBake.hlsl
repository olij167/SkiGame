#ifndef SRS_TERRAIN_BAKE_INCLUDED
#define SRS_TERRAIN_BAKE_INCLUDED

//SelectMap(half4(albedo.rgb, smoothness), inputData.normalWS, snowMask)

half3 SelectMap(float4 mixedAlbedo, float3 worldNormal, float finalMask)
{
    half3 map = float3(0, 0, 0);
	half3 nrm = worldNormal * 0.5 + 0.5;

	map = _MapID == 3.0 ? half3(finalMask, finalMask, finalMask) : map; //coverage mask
	map = _MapID == 2.0 ? half3(mixedAlbedo.a, mixedAlbedo.a, mixedAlbedo.a) : map; //smoothness
    map = _MapID == 1.0 ? nrm.xzy : map; //normals
	map = _MapID == 0.0 ? mixedAlbedo.rgb : map; //albedo

    return map;
}

#endif
