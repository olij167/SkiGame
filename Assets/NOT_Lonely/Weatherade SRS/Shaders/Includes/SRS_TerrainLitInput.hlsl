#ifndef UNIVERSAL_TERRAIN_LIT_INPUT_INCLUDED
#define UNIVERSAL_TERRAIN_LIT_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _MainTex_ST;
    half4 _BaseColor;
    half _Cutoff;
CBUFFER_END

#define _Surface 0.0 // Terrain is always opaque

CBUFFER_START(_Terrain)
    half _NormalScale0, _NormalScale1, _NormalScale2, _NormalScale3;
    half _Metallic0, _Metallic1, _Metallic2, _Metallic3;
    half _Smoothness0, _Smoothness1, _Smoothness2, _Smoothness3;
    half4 _DiffuseRemapScale0, _DiffuseRemapScale1, _DiffuseRemapScale2, _DiffuseRemapScale3;
    half4 _MaskMapRemapOffset0, _MaskMapRemapOffset1, _MaskMapRemapOffset2, _MaskMapRemapOffset3;
    half4 _MaskMapRemapScale0, _MaskMapRemapScale1, _MaskMapRemapScale2, _MaskMapRemapScale3;

    float4 _Control_ST;
    float4 _Control_TexelSize;
    half _DiffuseHasAlpha0, _DiffuseHasAlpha1, _DiffuseHasAlpha2, _DiffuseHasAlpha3;
    half _LayerHasMask0, _LayerHasMask1, _LayerHasMask2, _LayerHasMask3;
    half4 _Splat0_ST, _Splat1_ST, _Splat2_ST, _Splat3_ST;
    half _HeightTransition;
    half _NumLayersCount;

    #ifdef UNITY_INSTANCING_ENABLED
    float4 _TerrainHeightmapRecipSize;   // float4(1.0f/width, 1.0f/height, 1.0f/(width-1), 1.0f/(height-1))
    float4 _TerrainHeightmapScale;       // float4(hmScale.x, hmScale.y / (float)(kMaxHeight), hmScale.z, 0.0f)
    #endif
    #ifdef SCENESELECTIONPASS
    int _ObjectId;
    int _PassValue;
    #endif

////////////////////////////////
///SRS snow shader properties///
////////////////////////////////
//Basic coverage settings
half _CoverageAmount;

half3 _CoverageColor;
half3 _CoverageColor1;
half3 _CoverageColor2;

half2 _Cov0Smoothness;
half2 _Cov1Smoothness;
half2 _Cov2Smoothness;

half _CoverageNormalScale0;
half _CoverageNormalScale1;
half _CoverageNormalScale2;

half2 _HeightMap0Contrast;
half2 _HeightMap1Contrast;
half2 _HeightMap2Contrast;

half _CoverageTiling;
half _CoverageTiling1;
half _CoverageTiling2;

half2 _SssMaskRemap0;
half2 _SssMaskRemap1;
half2 _SssMaskRemap2;

half2 _EnhanceRemap0;
half2 _EnhanceRemap1;
half2 _EnhanceRemap2;

half _EmissionMasking;
half _CoverageNormalsOverlay;

//Detail map
half _DetailTiling; 
half _DetailDistance;
half2 _DetailTexRemap; 
half _DetailNormalScale;

//Area mask
half _CoverageAreaMaskRange;
half _CoverageAreaBias;
half _CoverageLeakReduction;
half _PrecipitationDirOffset;
half2 _PrecipitationDirRange;

//Tessellation
float _TessEdgeL;
float _TessFactorSnow;
float2 _TessSnowdriftRange;
float _TessMaxDisp;

//Displacement
half _CoverageDisplacement;
half _CoverageDisplacement1;
half _CoverageDisplacement2;
half _CoverageDisplacementOffset;
half _HeightMap0LOD;
half _HeightMap1LOD;
half _HeightMap2LOD;

//Traces
half4 _TracesColor;
half4 _TracesColor1;
half4 _TracesColor2;
half _TracesBaseBlend0;
half _TracesBaseBlend1;
half _TracesBaseBlend2;
half _TracesBlendFactor;
half2 _TracesColorBlendRange;
half2 _TracesColorBlendRange1;
half2 _TracesColorBlendRange2;
half _TracesNormalScale;
half _TracesNormalScale1;
half _TracesNormalScale2;
half _TraceDetailTiling;
half _TraceDetailNormalScale;
half _TraceDetailIntensity;

//Blend by normals
half _BlendByNormalsStrength;
half _BlendByNormalsPower;

//Distance fade
half _DistanceFadeStart;
half _DistanceFadeFalloff;

//Sparkle and SSS
half _SSS_intensity;
half _SparklesAmount;
half _SparklesBrightness;
half _SparkleDistFalloff;
half _LocalSparkleTiling;
half _ScreenSpaceSparklesTiling;
half _SparklesHighlightMaskExpansion;
half _HighlightBrightness0;
half _HighlightBrightness1;
half _HighlightBrightness2;

half _ColorEnhance;

//Other
half _CoverageAreaFalloffHardness;
half _MaskCoverageByAlpha;

half _TilingMultiplier;

#if defined(_PAINTABLE_COVERAGE_ON)
    float4 _PaintedMask_ST;
    float4 _PaintedMask_TexelSize;
#endif

#if defined (SRS_TERRAIN_BAKE_SHADER)
    uniform float _MapID;
#endif
//----------------------------
CBUFFER_END

//SRS global values
uniform float3 _depthCamDir;
uniform float _VsmExp;
uniform float4x4 _depthCamMatrix;
uniform float4x4 _SRS_TraceSurfCamMatrix;

//SRS textures
TEXTURE2D(_SRS_depth);         SAMPLER(sampler_SRS_depth);
TEXTURE2D(_SRS_TraceTex);
TEXTURE2D(_TraceDetailTex);    SAMPLER(sampler_TraceDetailTex);
TEXTURE2D(_SparkleTex);        SAMPLER(sampler_SparkleTex);
TEXTURE2D(_AlbedoLOD);
TEXTURE2D(_NormalLOD);
TEXTURE2D(_PaintedMask);
TEXTURE2D(_PaintedMaskNormal);
TEXTURE2D(_CoverageTex0);
TEXTURE2D(_CoverageTex1);
TEXTURE2D(_CoverageTex2);
TEXTURE2D(_CoverageDetailTex);

TEXTURE2D_ARRAY(_BlueNoise);
SAMPLER(sampler_BlueNoise);
float4 _BlueNoise_TexelSize;
float4 _SparkleTex_TexelSize;
//

TEXTURE2D(_Control);    SAMPLER(sampler_Control);
TEXTURE2D(_Splat0);     SAMPLER(sampler_Splat0);
TEXTURE2D(_Splat1);
TEXTURE2D(_Splat2);
TEXTURE2D(_Splat3);

#ifdef _NORMALMAP
TEXTURE2D(_Normal0);     SAMPLER(sampler_Normal0);
TEXTURE2D(_Normal1);
TEXTURE2D(_Normal2);
TEXTURE2D(_Normal3);
#endif

#ifdef _MASKMAP
TEXTURE2D(_Mask0);      SAMPLER(sampler_Mask0);
TEXTURE2D(_Mask1);
TEXTURE2D(_Mask2);
TEXTURE2D(_Mask3);
#endif

TEXTURE2D(_MainTex);       SAMPLER(sampler_MainTex);
TEXTURE2D(_SpecGlossMap);  SAMPLER(sampler_SpecGlossMap);
TEXTURE2D(_MetallicTex);   SAMPLER(sampler_MetallicTex);

#if defined(UNITY_INSTANCING_ENABLED) && defined(_TERRAIN_INSTANCED_PERPIXEL_NORMAL)
#define ENABLE_TERRAIN_PERPIXEL_NORMAL
#endif

#ifdef UNITY_INSTANCING_ENABLED
TEXTURE2D(_TerrainHeightmapTexture);
TEXTURE2D(_TerrainNormalmapTexture);
SAMPLER(sampler_TerrainNormalmapTexture);
#endif

UNITY_INSTANCING_BUFFER_START(Terrain)
UNITY_DEFINE_INSTANCED_PROP(float4, _TerrainPatchInstanceData)  // float4(xBase, yBase, skipScale, ~)
UNITY_INSTANCING_BUFFER_END(Terrain)

#ifdef _ALPHATEST_ON
TEXTURE2D(_TerrainHolesTexture);
SAMPLER(sampler_TerrainHolesTexture);

void ClipHoles(float2 uv)
{
    float hole = SAMPLE_TEXTURE2D(_TerrainHolesTexture, sampler_TerrainHolesTexture, uv).r;
    clip(hole == 0.0f ? -1 : 1);
}
#endif

half4 SampleMetallicSpecGloss(float2 uv, half albedoAlpha)
{
    half4 specGloss;
    specGloss = SAMPLE_TEXTURE2D(_MetallicTex, sampler_MetallicTex, uv);
    specGloss.a = albedoAlpha;
    return specGloss;
}

inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData outSurfaceData)
{
    outSurfaceData = (SurfaceData)0;
    half4 albedoSmoothness = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
    outSurfaceData.alpha = 1;

    half4 specGloss = SampleMetallicSpecGloss(uv, albedoSmoothness.a);
    outSurfaceData.albedo = albedoSmoothness.rgb;

    outSurfaceData.metallic = specGloss.r;
    outSurfaceData.specular = half3(0.0h, 0.0h, 0.0h);

    outSurfaceData.smoothness = specGloss.a;
    outSurfaceData.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap));
    outSurfaceData.occlusion = 1;
    outSurfaceData.emission = 0;
}

//SRS: this function moved here from the SRS_TerrainLitPasses.hlsl
#ifndef TERRAIN_SPLAT_BASEPASS
void NormalMapMix(float4 uvSplat01, float4 uvSplat23, inout half4 splatControl, inout half3 mixedNormal)
{
    #if defined(_NORMALMAP)
        half3 nrm = half(0.0);
        nrm += splatControl.r * UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal0, sampler_Normal0, uvSplat01.xy), _NormalScale0);
        nrm += splatControl.g * UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal1, sampler_Normal0, uvSplat01.zw), _NormalScale1);
        nrm += splatControl.b * UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal2, sampler_Normal0, uvSplat23.xy), _NormalScale2);
        nrm += splatControl.a * UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal3, sampler_Normal0, uvSplat23.zw), _NormalScale3);

        // avoid risk of NaN when normalizing.
        #if HAS_HALF
            nrm.z += half(0.01);
        #else
            nrm.z += 1e-5f;
        #endif

        mixedNormal = normalize(nrm.xyz);
    #endif
}
#endif

void TerrainInstancing(inout float4 positionOS, inout float3 normal, inout float2 uv)
{
#ifdef UNITY_INSTANCING_ENABLED
    float2 patchVertex = positionOS.xy;
    float4 instanceData = UNITY_ACCESS_INSTANCED_PROP(Terrain, _TerrainPatchInstanceData);

    float2 sampleCoords = (patchVertex.xy + instanceData.xy) * instanceData.z; // (xy + float2(xBase,yBase)) * skipScale
    float height = UnpackHeightmap(_TerrainHeightmapTexture.Load(int3(sampleCoords, 0)));

    positionOS.xz = sampleCoords * _TerrainHeightmapScale.xz;
    positionOS.y = height * _TerrainHeightmapScale.y;

#ifdef ENABLE_TERRAIN_PERPIXEL_NORMAL
    #if defined(_DISPLACEMENT_ON)
        normal = _TerrainNormalmapTexture.Load(int3(sampleCoords, 0)).rgb * 2 - 1; //SRS: sample normal map anyways to get correct normals for the displacement
    #else
        normal = half3(0, 1, 0);
    #endif
#else
    normal = _TerrainNormalmapTexture.Load(int3(sampleCoords, 0)).rgb * 2 - 1;
#endif
    uv = sampleCoords * _TerrainHeightmapRecipSize.zw;
#endif
}

/*
void TerrainInstancing(inout float4 positionOS, inout float3 normal)
{
    float2 uv = { 0, 0 };
    TerrainInstancing(positionOS, normal, uv);
}
*/
#endif
