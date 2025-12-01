
#ifndef SRS_TERRAIN_LIT_PASSES_INCLUDED
#define SRS_TERRAIN_LIT_PASSES_INCLUDED

#include "SRS_CoverageCommon.hlsl" //SRS: common functions
#if defined(SRS_SNOW_COVERAGE_SHADER)
    #include "SRS_Lighting.hlsl" //SRS: use this include instead of standard for snow shaders
#elif defined(SRS_RAIN_COVERAGE_SHADER)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#endif
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GBufferOutput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float3 texcoord : TEXCOORD0; //SRS: use float3 to store the tess mask in Z
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 uvMainAndLM              : TEXCOORD0; // xy: control, zw: lightmap
    #ifndef TERRAIN_SPLAT_BASEPASS
        float4 uvSplat01                : TEXCOORD1; // xy: splat0, zw: splat1
        float4 uvSplat23                : TEXCOORD2; // xy: splat2, zw: splat3
    #endif

    #if defined(_NORMALMAP) && !defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
        half4 normal                    : TEXCOORD3;    // xyz: normal, w: viewDir.x
        half4 tangent                   : TEXCOORD4;    // xyz: tangent, w: viewDir.y
        half4 bitangent                 : TEXCOORD5;    // xyz: bitangent, w: viewDir.z
    #else
        half3 normal                    : TEXCOORD3;
        half3 vertexSH                  : TEXCOORD4; // SH
    #endif

    #ifdef _ADDITIONAL_LIGHTS_VERTEX
        half4 fogFactorAndVertexLight   : TEXCOORD6; // x: fogFactor, yzw: vertex light
    #else
        half  fogFactor                 : TEXCOORD6;
    #endif

    float3 positionWS               : TEXCOORD7;

    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
        float4 shadowCoord              : TEXCOORD8;
    #endif

#if defined(DYNAMICLIGHTMAP_ON)
    float2 dynamicLightmapUV        : TEXCOORD9;
#endif

    float4 clipPos                  : SV_POSITION;
    UNITY_VERTEX_OUTPUT_STEREO
    
    //SRS: add instanceID to support tessellation
    #if defined(_TESSELLATION_ON)
        UNITY_VERTEX_INPUT_INSTANCE_ID
    #endif
};

#if defined(SRS_SNOW_COVERAGE_SHADER)
    #include "SRS_SnowCoverage.hlsl"
#elif defined(SRS_RAIN_COVERAGE_SHADER)
    #include "SRS_RainCoverage.hlsl"
#endif

void InitializeInputData(Varyings IN, half3 normalTS, out InputData inputData, out half3 meshNormalWS) //SRS: add meshNormalWS output
{
    inputData = (InputData)0;
    meshNormalWS = NormalizeNormalPerPixel(IN.normal.xyz); //SRS: init meshNormalsWS, assigning the world space vertex normal

    inputData.positionWS = IN.positionWS;
    inputData.positionCS = IN.clipPos;

    #if defined(_NORMALMAP) && !defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
        half3 viewDirWS = half3(IN.normal.w, IN.tangent.w, IN.bitangent.w);
        inputData.tangentToWorld = half3x3(-IN.tangent.xyz, IN.bitangent.xyz, IN.normal.xyz);
        inputData.normalWS = TransformTangentToWorld(normalTS, inputData.tangentToWorld);
        half3 SH = 0;
    #elif defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
        half3 viewDirWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
        float2 sampleCoords = (IN.uvMainAndLM.xy / _TerrainHeightmapRecipSize.zw + 0.5f) * _TerrainHeightmapRecipSize.xy;
        half3 normalWS = TransformObjectToWorldNormal(normalize(SAMPLE_TEXTURE2D(_TerrainNormalmapTexture, sampler_TerrainNormalmapTexture, sampleCoords).rgb * 2 - 1));
        half3 tangentWS = cross(GetObjectToWorldMatrix()._13_23_33, normalWS);
        inputData.normalWS = TransformTangentToWorld(normalTS, half3x3(-tangentWS, cross(normalWS, tangentWS), normalWS));
        meshNormalWS = normalWS; //SRS: assign per pixel normal to use it later in the SnowCoverage function
        half3 SH = 0;
    #else
        half3 viewDirWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
        inputData.normalWS = IN.normal;
        half3 SH = IN.vertexSH;
    #endif

    inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
    inputData.viewDirectionWS = viewDirWS;

    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
        inputData.shadowCoord = IN.shadowCoord;
    #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
        inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
    #else
        inputData.shadowCoord = float4(0, 0, 0, 0);
    #endif

    #ifdef _ADDITIONAL_LIGHTS_VERTEX
        inputData.fogCoord = InitializeInputDataFog(float4(IN.positionWS, 1.0), IN.fogFactorAndVertexLight.x);
        inputData.vertexLighting = IN.fogFactorAndVertexLight.yzw;
    #else
    inputData.fogCoord = InitializeInputDataFog(float4(IN.positionWS, 1.0), IN.fogFactor);
    #endif

    //SRS: SAMPLE_GI moved directly to the fragment function to be called after the SnowCoverage function

    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.clipPos);
    inputData.shadowMask = SAMPLE_SHADOWMASK(IN.uvMainAndLM.zw)

    #if defined(DEBUG_DISPLAY)
    #if defined(DYNAMICLIGHTMAP_ON)
    inputData.dynamicLightmapUV = IN.dynamicLightmapUV;
    #endif
    #if defined(LIGHTMAP_ON)
    inputData.staticLightmapUV = IN.uvMainAndLM.zw;
    #else
    inputData.vertexSH = SH;
    #endif
    #endif
}

#ifndef TERRAIN_SPLAT_BASEPASS

//SRS: NormalMapMix function has been moved into the SRS_TerraiLitInput.hlsl for better reuseabillity

void SplatmapMix(float4 uvMainAndLM, float4 uvSplat01, float4 uvSplat23, inout half4 splatControl, out half weight, out half4 mixedDiffuse, out half4 defaultSmoothness, inout half3 mixedNormal)
{
    half4 diffAlbedo[4];

    //SRS: multiply UV for the terrain LOD maps baking
    uvSplat01 *= _TilingMultiplier;
    uvSplat23 *= _TilingMultiplier;
    //

    diffAlbedo[0] = SAMPLE_TEXTURE2D(_Splat0, sampler_Splat0, uvSplat01.xy);
    diffAlbedo[1] = SAMPLE_TEXTURE2D(_Splat1, sampler_Splat0, uvSplat01.zw);
    diffAlbedo[2] = SAMPLE_TEXTURE2D(_Splat2, sampler_Splat0, uvSplat23.xy);
    diffAlbedo[3] = SAMPLE_TEXTURE2D(_Splat3, sampler_Splat0, uvSplat23.zw);

    // This might be a bit of a gamble -- the assumption here is that if the diffuseMap has no
    // alpha channel, then diffAlbedo[n].a = 1.0 (and _DiffuseHasAlphaN = 0.0)
    // Prior to coming in, _SmoothnessN is actually set to max(_DiffuseHasAlphaN, _SmoothnessN)
    // This means that if we have an alpha channel, _SmoothnessN is locked to 1.0 and
    // otherwise, the true slider value is passed down and diffAlbedo[n].a == 1.0.
    defaultSmoothness = half4(diffAlbedo[0].a, diffAlbedo[1].a, diffAlbedo[2].a, diffAlbedo[3].a);
    defaultSmoothness *= half4(_Smoothness0, _Smoothness1, _Smoothness2, _Smoothness3);

#ifndef _TERRAIN_BLEND_HEIGHT // density blending
    if(_NumLayersCount <= 4)
    {
        // 20.0 is the number of steps in inputAlphaMask (Density mask. We decided 20 empirically)
        half4 opacityAsDensity = saturate((half4(diffAlbedo[0].a, diffAlbedo[1].a, diffAlbedo[2].a, diffAlbedo[3].a) - (1 - splatControl)) * 20.0);
        opacityAsDensity += 0.001h * splatControl;      // if all weights are zero, default to what the blend mask says
        half4 useOpacityAsDensityParam = { _DiffuseRemapScale0.w, _DiffuseRemapScale1.w, _DiffuseRemapScale2.w, _DiffuseRemapScale3.w }; // 1 is off
        splatControl = lerp(opacityAsDensity, splatControl, useOpacityAsDensityParam);
    }
#endif

    // Now that splatControl has changed, we can compute the final weight and normalize
    weight = dot(splatControl, 1.0h);

#ifdef TERRAIN_SPLAT_ADDPASS
    clip(weight <= 0.005h ? -1.0h : 1.0h);
#endif

#ifndef _TERRAIN_BASEMAP_GEN
    // Normalize weights before lighting and restore weights in final modifier functions so that the overal
    // lighting result can be correctly weighted.
    splatControl /= (weight + HALF_MIN);
#endif

    mixedDiffuse = 0.0h;
    mixedDiffuse += diffAlbedo[0] * half4(_DiffuseRemapScale0.rgb * splatControl.rrr, 1.0h);
    mixedDiffuse += diffAlbedo[1] * half4(_DiffuseRemapScale1.rgb * splatControl.ggg, 1.0h);
    mixedDiffuse += diffAlbedo[2] * half4(_DiffuseRemapScale2.rgb * splatControl.bbb, 1.0h);
    mixedDiffuse += diffAlbedo[3] * half4(_DiffuseRemapScale3.rgb * splatControl.aaa, 1.0h);

    NormalMapMix(uvSplat01, uvSplat23, splatControl, mixedNormal);
}

#endif

#ifdef _TERRAIN_BLEND_HEIGHT
void HeightBasedSplatModify(inout half4 splatControl, in half4 masks[4])
{
    // heights are in mask blue channel, we multiply by the splat Control weights to get combined height
    half4 splatHeight = half4(masks[0].b, masks[1].b, masks[2].b, masks[3].b) * splatControl.rgba;
    half maxHeight = max(splatHeight.r, max(splatHeight.g, max(splatHeight.b, splatHeight.a)));

    // Ensure that the transition height is not zero.
    half transition = max(_HeightTransition, 1e-5);

    // This sets the highest splat to "transition", and everything else to a lower value relative to that, clamping to zero
    // Then we clamp this to zero and normalize everything
    half4 weightedHeights = splatHeight + transition - maxHeight.xxxx;
    weightedHeights = max(0, weightedHeights);

    // We need to add an epsilon here for active layers (hence the blendMask again)
    // so that at least a layer shows up if everything's too low.
    weightedHeights = (weightedHeights + 1e-6) * splatControl;

    // Normalize (and clamp to epsilon to keep from dividing by zero)
    half sumHeight = max(dot(weightedHeights, half4(1, 1, 1, 1)), 1e-6);
    splatControl = weightedHeights / sumHeight.xxxx;
}
#endif

void SplatmapFinalColor(inout half4 color, half fogCoord)
{
    color.rgb *= color.a;

    #ifndef TERRAIN_GBUFFER // Technically we don't need fogCoord, but it is still passed from the vertex shader.

    #ifdef TERRAIN_SPLAT_ADDPASS
        color.rgb = MixFogColor(color.rgb, half3(0,0,0), fogCoord);
    #else
        color.rgb = MixFog(color.rgb, fogCoord);
    #endif

    #endif
}

///////////////////////////////////////////////////////////////////////////////
//                  Vertex and Fragment functions                            //
///////////////////////////////////////////////////////////////////////////////

// Used in Standard Terrain shader
Varyings SplatmapVert(Attributes v)
{
    Varyings o = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(v);
    #if defined(_TESSELLATION_ON)
        UNITY_TRANSFER_INSTANCE_ID(v, o);
    #endif
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    //SRS: do instancing in this shader stage only if it's not a tessellation shader
    #if !defined(_TESSELLATION_ON)
        TerrainInstancing(v.positionOS, v.normalOS, v.texcoord.xy);
    #endif

    //SRS: replace standard GetVertexPositionInputs function. The Displace function is inside it.
    #if defined(SRS_SNOW_COVERAGE_SHADER)
        VertexPositionInputs attributes = NL_GetVertexPositionInputs(v);
    #else
        VertexPositionInputs attributes = GetVertexPositionInputs(v.positionOS.xyz);
    #endif
    //

    o.uvMainAndLM.xy = v.texcoord.xy;
    o.uvMainAndLM.zw = v.texcoord.xy * unity_LightmapST.xy + unity_LightmapST.zw;

    #ifndef TERRAIN_SPLAT_BASEPASS
        o.uvSplat01.xy = TRANSFORM_TEX(v.texcoord.xy, _Splat0);
        o.uvSplat01.zw = TRANSFORM_TEX(v.texcoord.xy, _Splat1);
        o.uvSplat23.xy = TRANSFORM_TEX(v.texcoord.xy, _Splat2);
        o.uvSplat23.zw = TRANSFORM_TEX(v.texcoord.xy, _Splat3);
    #endif

#if defined(DYNAMICLIGHTMAP_ON)
    o.dynamicLightmapUV = v.texcoord.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
#endif

    #if defined(_NORMALMAP) && !defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
        half3 viewDirWS = GetWorldSpaceNormalizeViewDir(attributes.positionWS);
        float4 vertexTangent = float4(cross(float3(0, 0, 1), v.normalOS), 1.0);
        VertexNormalInputs normalInput = GetVertexNormalInputs(v.normalOS, vertexTangent);

        o.normal = half4(normalInput.normalWS, viewDirWS.x);
        o.tangent = half4(normalInput.tangentWS, viewDirWS.y);
        o.bitangent = half4(normalInput.bitangentWS, viewDirWS.z);
    #else
        o.normal = TransformObjectToWorldNormal(v.normalOS);
        o.vertexSH = SampleSH(o.normal);
    #endif

    half fogFactor = 0;
    #if !defined(_FOG_FRAGMENT)
        fogFactor = ComputeFogFactor(attributes.positionCS.z);
    #endif

    #ifdef _ADDITIONAL_LIGHTS_VERTEX
        o.fogFactorAndVertexLight.x = fogFactor;
        o.fogFactorAndVertexLight.yzw = VertexLighting(attributes.positionWS, o.normal.xyz);
    #else
        o.fogFactor = fogFactor;
    #endif

    o.positionWS = attributes.positionWS;
    o.clipPos = attributes.positionCS;

    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
        o.shadowCoord = GetShadowCoord(attributes);
    #endif

    return o;
}

void ComputeMasks(out half4 masks[4], half4 hasMask, Varyings IN)
{
    masks[0] = 0.5h;
    masks[1] = 0.5h;
    masks[2] = 0.5h;
    masks[3] = 0.5h;

#ifdef _MASKMAP
    masks[0] = lerp(masks[0], SAMPLE_TEXTURE2D(_Mask0, sampler_Mask0, IN.uvSplat01.xy), hasMask.x);
    masks[1] = lerp(masks[1], SAMPLE_TEXTURE2D(_Mask1, sampler_Mask0, IN.uvSplat01.zw), hasMask.y);
    masks[2] = lerp(masks[2], SAMPLE_TEXTURE2D(_Mask2, sampler_Mask0, IN.uvSplat23.xy), hasMask.z);
    masks[3] = lerp(masks[3], SAMPLE_TEXTURE2D(_Mask3, sampler_Mask0, IN.uvSplat23.zw), hasMask.w);
#endif

    masks[0] *= _MaskMapRemapScale0.rgba;
    masks[0] += _MaskMapRemapOffset0.rgba;
    masks[1] *= _MaskMapRemapScale1.rgba;
    masks[1] += _MaskMapRemapOffset1.rgba;
    masks[2] *= _MaskMapRemapScale2.rgba;
    masks[2] += _MaskMapRemapOffset2.rgba;
    masks[3] *= _MaskMapRemapScale3.rgba;
    masks[3] += _MaskMapRemapOffset3.rgba;
}

// Used in Standard Terrain shader
#ifdef TERRAIN_GBUFFER
GBufferFragOutput SplatmapFragment(Varyings IN)
#else
void SplatmapFragment(
    Varyings IN
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out float4 outRenderingLayers : SV_Target1
#endif
    )
#endif
{
    #if defined(_TESSELLATION_ON)
        UNITY_SETUP_INSTANCE_ID(IN);
    #endif
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
#ifdef _ALPHATEST_ON
    ClipHoles(IN.uvMainAndLM.xy);
#endif

    half3 normalTS = half3(0.0h, 0.0h, 1.0h);
#ifdef TERRAIN_SPLAT_BASEPASS
    half3 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uvMainAndLM.xy).rgb;
    half smoothness = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uvMainAndLM.xy).a;
    half metallic = SAMPLE_TEXTURE2D(_MetallicTex, sampler_MetallicTex, IN.uvMainAndLM.xy).r;
    half alpha = 1;
    half occlusion = 1;
#else

    half4 hasMask = half4(_LayerHasMask0, _LayerHasMask1, _LayerHasMask2, _LayerHasMask3);
    half4 masks[4];
    ComputeMasks(masks, hasMask, IN);

    float2 splatUV = (IN.uvMainAndLM.xy * (_Control_TexelSize.zw - 1.0f) + 0.5f) * _Control_TexelSize.xy;
    half4 splatControl = SAMPLE_TEXTURE2D(_Control, sampler_Control, splatUV);

    half alpha = dot(splatControl, 1.0h);
#ifdef _TERRAIN_BLEND_HEIGHT
    // disable Height Based blend when there are more than 4 layers (multi-pass breaks the normalization)
    if (_NumLayersCount <= 4)
        HeightBasedSplatModify(splatControl, masks);
#endif

    half weight;
    half4 mixedDiffuse;
    half4 defaultSmoothness;
    SplatmapMix(IN.uvMainAndLM, IN.uvSplat01, IN.uvSplat23, splatControl, weight, mixedDiffuse, defaultSmoothness, normalTS);
    half3 albedo = mixedDiffuse.rgb;

    half4 defaultMetallic = half4(_Metallic0, _Metallic1, _Metallic2, _Metallic3);
    half4 defaultOcclusion = half4(_MaskMapRemapScale0.g, _MaskMapRemapScale1.g, _MaskMapRemapScale2.g, _MaskMapRemapScale3.g) +
                            half4(_MaskMapRemapOffset0.g, _MaskMapRemapOffset1.g, _MaskMapRemapOffset2.g, _MaskMapRemapOffset3.g);

    half4 maskSmoothness = half4(masks[0].a, masks[1].a, masks[2].a, masks[3].a);
    defaultSmoothness = lerp(defaultSmoothness, maskSmoothness, hasMask);
    half smoothness = dot(splatControl, defaultSmoothness);

    half4 maskMetallic = half4(masks[0].r, masks[1].r, masks[2].r, masks[3].r);
    defaultMetallic = lerp(defaultMetallic, maskMetallic, hasMask);
    half metallic = dot(splatControl, defaultMetallic);

    half4 maskOcclusion = half4(masks[0].g, masks[1].g, masks[2].g, masks[3].g);
    defaultOcclusion = lerp(defaultOcclusion, maskOcclusion, hasMask);
    half occlusion = dot(splatControl, defaultOcclusion);
#endif

    //SRS: add a variable for the mesh normals. If per pixel normal is used, 
    //it's the sampled per pixel normal, otherwise it's just the original vertex normal
    half3 meshNormalWS = 0;

    InputData inputData;
    InitializeInputData(IN, normalTS, inputData, meshNormalWS);

    //SRS: call SnowCoverage here to ensure that proper normals will be used in SAMPLE_GI stage
    half snowMask = 0;
    half rawSmoothness = 0;
    #if defined(_COVERAGE_ON)  
        half dither = DitherAnimated(inputData.normalizedScreenSpaceUV);

        #if defined(SRS_SNOW_COVERAGE_SHADER)
            half sssMask = 0;
            half highlightBrightness = 0;
            SnowCoverage(inputData.normalWS, inputData.positionWS, albedo, metallic, smoothness, rawSmoothness, occlusion, alpha, meshNormalWS, snowMask, 0, sssMask, highlightBrightness, IN.uvMainAndLM.xy, dither);
        #elif defined(SRS_RAIN_COVERAGE_SHADER)
            half4 tangentWS = half4(1, 0, 0, 1);
            RainCoverage(inputData.normalWS, inputData.positionWS, albedo, smoothness, IN.uvMainAndLM.xy, 0, splatUV, meshNormalWS, 0, tangentWS);
        #endif
    #endif
/*
    #if defined (_USE_COVERAGE_DETAIL) && defined (_NORMALMAP)
        half4 detailTex = FastTriplanarSoft_Normals_MaskZ(_CoverageDetailTex, sampler_SRS_depth, inputData.positionWS, inputData.normalWS, 10, 0.5, 1, dither);
        half4 detailTex1 = FastTriplanarSoft_Normals_MaskZ(_Splat0, sampler_SRS_depth, inputData.positionWS, inputData.normalWS, 10, 0.5, 1, dither);
        half4 detailTex2 = FastTriplanarSoft_Normals_MaskZ(_Splat1, sampler_SRS_depth, inputData.positionWS, inputData.normalWS, 11, 0.5, 1, dither);
        half4 detailTex3 = FastTriplanarSoft_Normals_MaskZ(_Splat2, sampler_SRS_depth, inputData.positionWS, inputData.normalWS, 12, 0.5, 1, dither);
        half4 detailTex4 = FastTriplanarSoft_Normals_MaskZ(_Splat3, sampler_SRS_depth, inputData.positionWS, inputData.normalWS, 13, 0.5, 1, dither);
        half4 detailTex5 = FastTriplanarSoft_Normals_MaskZ(_Normal0, sampler_SRS_depth, inputData.positionWS, inputData.normalWS, 14, 0.5, 1, dither);
        half4 detailTex6 = FastTriplanarSoft_Normals_MaskZ(_Normal1, sampler_SRS_depth, inputData.positionWS, inputData.normalWS, 15, 0.5, 1, dither);
        half4 detailTex7 = FastTriplanarSoft_Normals_MaskZ(_Normal2, sampler_SRS_depth, inputData.positionWS, inputData.normalWS, 16, 0.5, 1, dither);
        half4 detailTex8 = FastTriplanarSoft_Normals_MaskZ(_Normal3, sampler_SRS_depth, inputData.positionWS, inputData.normalWS, 17, 0.5, 1, dither);
        half4 detailTex9 = FastTriplanarSoft_Normals_MaskZ(_SRS_depth, sampler_SRS_depth, inputData.positionWS, inputData.normalWS, 18, 0.5, 1, dither);
      
      */
/*
        half4 detailTex = TrueTriplanar(_CoverageDetailTex, sampler_SRS_depth, inputData.positionWS, inputData.normalWS, 0.5, 1);
        half4 detailTex1 = TrueTriplanar(_Splat0, sampler_SRS_depth, inputData.positionWS, inputData.normalWS, 0.5, 1);
        half4 detailTex2 = TrueTriplanar(_Splat1, sampler_SRS_depth, inputData.positionWS, inputData.normalWS, 0.5, 1);
        half4 detailTex3 = TrueTriplanar(_Splat2, sampler_SRS_depth, inputData.positionWS, inputData.normalWS, 0.5, 1);
        half4 detailTex4 = TrueTriplanar(_Splat3, sampler_SRS_depth, inputData.positionWS, inputData.normalWS, 0.5, 1);
        half4 detailTex5 = TrueTriplanar(_Normal0, sampler_SRS_depth, inputData.positionWS, inputData.normalWS, 0.5, 1);
        half4 detailTex6 = TrueTriplanar(_Normal1, sampler_SRS_depth, inputData.positionWS, inputData.normalWS, 0.5, 1);
        half4 detailTex7 = TrueTriplanar(_Normal2, sampler_SRS_depth, inputData.positionWS, inputData.normalWS, 0.5, 1);
        half4 detailTex8 = TrueTriplanar(_Normal3, sampler_SRS_depth, inputData.positionWS, inputData.normalWS, 0.5, 1);
        half4 detailTex9 = TrueTriplanar(_SRS_depth, sampler_SRS_depth, inputData.positionWS, inputData.normalWS, 0.5, 1);
        
*/
/*
        half4 detailTex = FastTriplanar_Normals_MaskZ(_CoverageDetailTex, sampler_SRS_depth, inputData.positionWS, inputData.normalWS, 0.5, 1);
        half4 detailTex1 = FastTriplanar_Normals_MaskZ(_Splat0, sampler_SRS_depth, inputData.positionWS, inputData.normalWS, 0.5, 1);
        half4 detailTex2 = FastTriplanar_Normals_MaskZ(_Splat1, sampler_SRS_depth, inputData.positionWS, inputData.normalWS, 0.5, 1);
        half4 detailTex3 = FastTriplanar_Normals_MaskZ(_Splat2, sampler_SRS_depth, inputData.positionWS, inputData.normalWS, 0.5, 1);
        half4 detailTex4 = FastTriplanar_Normals_MaskZ(_Splat3, sampler_SRS_depth, inputData.positionWS, inputData.normalWS, 0.5, 1);
        half4 detailTex5 = FastTriplanar_Normals_MaskZ(_Normal0, sampler_SRS_depth, inputData.positionWS, inputData.normalWS, 0.5, 1);
        half4 detailTex6 = FastTriplanar_Normals_MaskZ(_Normal1, sampler_SRS_depth, inputData.positionWS, inputData.normalWS, 0.5, 1);
        half4 detailTex7 = FastTriplanar_Normals_MaskZ(_Normal2, sampler_SRS_depth, inputData.positionWS, inputData.normalWS, 0.5, 1);
        half4 detailTex8 = FastTriplanar_Normals_MaskZ(_Normal3, sampler_SRS_depth, inputData.positionWS, inputData.normalWS, 0.5, 1);
        half4 detailTex9 = FastTriplanar_Normals_MaskZ(_SRS_depth, sampler_SRS_depth, inputData.positionWS, inputData.normalWS, 0.5, 1);
*/
/*
        detailTex += saturate(detailTex1 + detailTex2 + detailTex3 + detailTex4 + detailTex5 + detailTex6 + detailTex7 + detailTex8 + detailTex9) * 0.0001;
        albedo = detailTex.rgb;
    #endif
    */
    

#if !defined(SRS_TERRAIN_BAKE_SHADER) && !defined(SRS_RAIN_TERRAIN_BAKE_SHADER)
    //SRS: get SH
    #if defined(_NORMALMAP) || defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
        half3 SH = 0;
    #else
        half3 SH = IN.vertexSH;
    #endif

    //This part is moved from the InitializeInputData function to be after the SnowCoverage
    //to ensure that proper normals are used when GI is sampled
    #if defined(DYNAMICLIGHTMAP_ON)
        inputData.bakedGI = SAMPLE_GI(IN.uvMainAndLM.zw, IN.dynamicLightmapUV, SH, inputData.normalWS);
    #else
        inputData.bakedGI = SAMPLE_GI(IN.uvMainAndLM.zw, SH, inputData.normalWS);
    #endif
    //

    //SETUP_DEBUG_TEXTURE_DATA(inputData, IN.uvMainAndLM.xy, _BaseMap);

#if defined(_DBUFFER)
    half3 specular = half3(0.0h, 0.0h, 0.0h);
    ApplyDecal(IN.clipPos,
        albedo,
        specular,
        inputData.normalWS,
        metallic,
        occlusion,
        smoothness);
#endif

#endif

#ifdef TERRAIN_GBUFFER
    
    Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);

    //SRS: calculate sparkle and SSS, then add it to the color output
    half3 emission = 0;
    #if defined(_COVERAGE_ON) && (defined(_SPARKLE_ON) || defined(_SSS_ON))
        half lightmapMask = GetLightmapMask(inputData.bakedGI);
        float distMask = GetDistanceGradient(inputData.positionWS, _SparkleDistFalloff);

        half effectsMask = lightmapMask * pow(snowMask, 2);
        
        #if defined(_SPARKLE_ON)
            emission.rgb += CalcSparkle(_SparkleTex, sampler_SparkleTex, _SparkleTex_TexelSize.xy, _SparklesAmount, _SparklesBrightness, _LocalSparkleTiling, 
            _ScreenSpaceSparklesTiling, _SparklesHighlightMaskExpansion, highlightBrightness, inputData.normalizedScreenSpaceUV, inputData.positionWS, inputData.normalWS, inputData.viewDirectionWS, 
            mainLight.direction, mainLight.color * mainLight.shadowAttenuation, 0, rawSmoothness, distMask * 0.5) * effectsMask * distMask;
        #endif
        #if defined(_SSS_ON)
            emission.rgb += SSS(inputData.normalWS, inputData.viewDirectionWS, mainLight.direction, mainLight.color * mainLight.shadowAttenuation, _SSS_intensity, effectsMask * distMask * sssMask);
        #endif
        emission.rgb += EnhanceColor(mainLight.color * mainLight.shadowAttenuation, 0, rawSmoothness, effectsMask * distMask, _ColorEnhance);
    #endif
    //

    BRDFData brdfData;
    InitializeBRDFData(albedo, metallic, /* specular */ half3(0.0h, 0.0h, 0.0h), smoothness, alpha, brdfData);

    // Baked lighting.
    half4 color;
    //SRS: GetMainLight has moved upper
    MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI, inputData.shadowMask);
    color.rgb = GlobalIllumination(brdfData, inputData.bakedGI, occlusion, inputData.positionWS, inputData.normalWS, inputData.viewDirectionWS);
    color.a = alpha;
    color.rgb += emission; //SRS: add sparkle and SSS emission to the color here
    SplatmapFinalColor(color, inputData.fogCoord);

    // Dynamic lighting: emulate SplatmapFinalColor() by scaling gbuffer material properties. This will not give the same results
    // as forward renderer because we apply blending pre-lighting instead of post-lighting.
    // Blending of smoothness and normals is also not correct but close enough?
    brdfData.albedo.rgb *= alpha;
    brdfData.diffuse.rgb *= alpha;
    brdfData.specular.rgb *= alpha;
    brdfData.reflectivity *= alpha;
    inputData.normalWS = inputData.normalWS * alpha;
    smoothness *= alpha;
    rawSmoothness *= alpha;

    return PackGBuffersBRDFData(brdfData, inputData, smoothness, color.rgb, occlusion);

#else
    //SRS: do custom lighting for the snow to provide sparkle and SSS effects
    #if !defined(SRS_TERRAIN_BAKE_SHADER) && !defined(SRS_RAIN_TERRAIN_BAKE_SHADER)
        #if defined(_COVERAGE_ON) && defined(SRS_SNOW_COVERAGE_SHADER)
            half4 color = NL_UniversalFragmentPBR(inputData, albedo, metallic, /* specular */ half3(0.0h, 0.0h, 0.0h), smoothness, occlusion, /* emission */ half3(0, 0, 0), alpha, snowMask, sssMask, rawSmoothness, highlightBrightness);
        #else
            half4 color = UniversalFragmentPBR(inputData, albedo, metallic, /* specular */ half3(0.0h, 0.0h, 0.0h), smoothness, occlusion, /* emission */ half3(0, 0, 0), alpha);
        #endif
    #else
        half4 color = half4(1, 1, 1, alpha);
        color.rgb = SelectMap(half4(albedo.rgb, smoothness), inputData.normalWS, snowMask);
    #endif
    //

    SplatmapFinalColor(color, inputData.fogCoord);

    outColor = half4(color.rgb, 1.0h);

#ifdef _WRITE_RENDERING_LAYERS
    uint renderingLayers = GetMeshRenderingLayer();
    outRenderingLayers = float4(EncodeMeshRenderingLayer(renderingLayers), 0, 0, 0);
#endif
#endif
}
#endif
