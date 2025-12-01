#ifndef SRS_LIT_GBUFFER_PASS_INCLUDED
#define SRS_LIT_GBUFFER_PASS_INCLUDED

#include "SRS_CoverageCommon.hlsl" //SRS: common functions
#if defined(SRS_SNOW_COVERAGE_SHADER)
    #include "SRS_Lighting.hlsl" //SRS: use this include instead of standard for snow shaders
#elif defined(SRS_RAIN_COVERAGE_SHADER)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#endif

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GBufferOutput.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

// TODO: Currently we support viewDirTS caclulated in vertex shader and in fragments shader.
// As both solutions have their advantages and disadvantages (etc. shader target 2.0 has only 8 interpolators).
// We need to find out if we can stick to one solution, which we needs testing.
// So keeping this until I get manaul QA pass.
#if defined(_PARALLAXMAP) && (SHADER_TARGET >= 30)
#define REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR
#endif

#if (defined(_NORMALMAP) || (defined(_PARALLAXMAP) && !defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR))) || defined(_DETAIL) || defined(_DRIPS_ON) //SRS: define if drips feature enabled
#define REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR
#endif

// keep this file in sync with LitForwardPass.hlsl

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    //SRS
    float4 color : COLOR0; // SRS: vertex color needed for the Paintable Coverage feature
    #if defined(_USE_AVERAGED_NORMALS)
        half3 unifiedNormal : TEXCOORD3;
    #endif
    //
    float4 tangentOS    : TANGENT;
    float3 texcoord     : TEXCOORD0; //SRS: change float2 to float3 to store a tess mask in Z
    float2 staticLightmapUV   : TEXCOORD1;
    float2 dynamicLightmapUV  : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    //SRS
    float4 color : COLOR0; // SRS: vertex color needed for the Paintable Coverage feature
    #if defined(_USE_AVERAGED_NORMALS)
            half3 unifiedWorldNormal : COLOR1;
    #endif 
    //
    float3 uv                       : TEXCOORD0; //SRS use float3 to store eyeDepth in Z

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    float3 positionWS               : TEXCOORD1;
#endif

    half3 normalWS                  : TEXCOORD2;
#if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
    half4 tangentWS                 : TEXCOORD3;    // xyz: tangent, w: sign
#endif
#ifdef _ADDITIONAL_LIGHTS_VERTEX
    half3 vertexLighting            : TEXCOORD4;    // xyz: vertex lighting
#endif

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord              : TEXCOORD5;
#endif

#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    half3 viewDirTS                 : TEXCOORD6;
#endif

    DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 7);
#ifdef DYNAMICLIGHTMAP_ON
    float2  dynamicLightmapUV       : TEXCOORD8; // Dynamic lightmap UVs
#endif

    float4 positionCS               : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

#if defined(SRS_SNOW_COVERAGE_SHADER)
    #include "SRS_SnowCoverage.hlsl"
#elif defined(SRS_RAIN_COVERAGE_SHADER)
    #include "SRS_RainCoverage.hlsl"
#endif

void InitializeInputData(Varyings input, half3 normalTS, out InputData inputData)
{
    inputData = (InputData)0;

    #if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
        inputData.positionWS = input.positionWS;
    #endif

    inputData.positionCS = input.positionCS;
    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    #if defined(_NORMALMAP) || defined(_DETAIL)
        float sgn = input.tangentWS.w;      // should be either +1 or -1
        float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
        inputData.normalWS = TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz));
    #else
        inputData.normalWS = input.normalWS;
    #endif

    inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
    inputData.viewDirectionWS = viewDirWS;

    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
        inputData.shadowCoord = input.shadowCoord;
    #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
        inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
    #else
        inputData.shadowCoord = float4(0, 0, 0, 0);
    #endif

    inputData.fogCoord = 0.0; // we don't apply fog in the guffer pass

    #ifdef _ADDITIONAL_LIGHTS_VERTEX
        inputData.vertexLighting = input.vertexLighting.xyz;
    #else
        inputData.vertexLighting = half3(0, 0, 0);
    #endif

    //SRS: GI sampling moved to the LitGBufferPassFragment

    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
}

///////////////////////////////////////////////////////////////////////////////
//                  Vertex and Fragment functions                            //
///////////////////////////////////////////////////////////////////////////////

// Used in Standard (Physically Based) shader
Varyings LitGBufferPassVertex(Attributes input)
{
    Varyings output = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    // normalWS and tangentWS already normalize.
    // this is required to avoid skewing the direction during interpolation
    // also required for per-vertex lighting and SH evaluation

    //SRS
    #if defined(_COVERAGE_ON)
        #if defined(_USE_AVERAGED_NORMALS)
            VertexNormalInputs normalInput = NL_GetVertexNormalInputs(input.normalOS, input.unifiedNormal, input.tangentOS);
            output.unifiedWorldNormal = TransformObjectToWorldNormal(input.unifiedNormal);
        #else
            VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
        #endif

        #if defined (_PAINTABLE_COVERAGE_ON)
            output.color = input.color;
        #endif
    #else
        VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    #endif
    #if defined(SRS_SNOW_COVERAGE_SHADER)
        VertexPositionInputs vertexInput = NL_GetVertexPositionInputs(input);
    #elif defined(SRS_RAIN_COVERAGE_SHADER)
        VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    #endif
    //

    output.uv.xy = TRANSFORM_TEX(input.texcoord.xy, _BaseMap); //SRS: use only XY since Z is used for the tess mask
    
    //SRS: add eyeDepth
    output.uv.z = -TransformWorldToView(vertexInput.positionWS.xyz).z;

    // already normalized from normal transform to WS.
    output.normalWS = normalInput.normalWS;

    #if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR) || defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
        real sign = input.tangentOS.w * GetOddNegativeScale();
        half4 tangentWS = half4(normalInput.tangentWS.xyz, sign);
    #endif

    #if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
        output.tangentWS = tangentWS;
    #endif

    #if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
        half3 viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);
        half3 viewDirTS = GetViewDirectionTangentSpace(tangentWS, output.normalWS, viewDirWS);
        output.viewDirTS = viewDirTS;
    #endif

    OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
#ifdef DYNAMICLIGHTMAP_ON
    output.dynamicLightmapUV = input.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
#endif
    OUTPUT_SH(output.normalWS.xyz, output.vertexSH);

    #ifdef _ADDITIONAL_LIGHTS_VERTEX
        half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);
        output.vertexLighting = vertexLight;
    #endif

    #if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
        output.positionWS = vertexInput.positionWS;
    #endif

    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
        output.shadowCoord = GetShadowCoord(vertexInput);
    #endif

    output.positionCS = vertexInput.positionCS;

    return output;
}

// Used in Standard (Physically Based) shader
GBufferFragOutput LitGBufferPassFragment(Varyings input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

#if defined(_PARALLAXMAP)
    #if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
        half3 viewDirTS = input.viewDirTS;
    #else
        half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
        half3 viewDirTS = GetViewDirectionTangentSpace(input.tangentWS, input.normalWS, viewDirWS);
    #endif
    ApplyPerPixelDisplacement(viewDirTS, input.uv);
#endif

    SurfaceData surfaceData;
    InitializeStandardLitSurfaceData(input.uv.xy, surfaceData);//SRS: use ony xy, since Z used to store the eyeDepth value

#ifdef LOD_FADE_CROSSFADE
    LODFadeCrossFade(input.positionCS);
#endif

    InputData inputData;
    InitializeInputData(input, surfaceData.normalTS, inputData);
    //SETUP_DEBUG_TEXTURE_DATA(inputData, input.uv, _BaseMap);

    //SRS: call  SnowCoverage here to ensure that proper normals will be used in SAMPLE_GI stage
    half rawSmoothness = 0;
    #if defined(_COVERAGE_ON)
        half dither = DitherAnimated(inputData.normalizedScreenSpaceUV);
        #if defined(SRS_SNOW_COVERAGE_SHADER)
            half snowMask = 0;
            half sssMask = 0;
            half highlightBrightness = 0;
            SnowCoverage(inputData.normalWS, inputData.positionWS, surfaceData.albedo, surfaceData.metallic, surfaceData.smoothness, rawSmoothness, surfaceData.occlusion, surfaceData.alpha, input.normalWS, snowMask, input.color, sssMask, highlightBrightness, 0, dither);
            #if defined(_EMISSION)
                surfaceData.emission *= lerp(1, 1-snowMask, _EmissionMasking);
            #endif
        #elif defined(SRS_RAIN_COVERAGE_SHADER)
            #if defined(_USE_AVERAGED_NORMALS)
                half3 unifiedNormal = input.unifiedWorldNormal;
            #else
                half3 unifiedNormal = 0;
            #endif 

            #if defined(_DRIPS_ON)
                half4 tangentWS = input.tangentWS;
            #else
                half4 tangentWS = 0;
            #endif
            
            RainCoverage(inputData.normalWS, inputData.positionWS, surfaceData.albedo, surfaceData.smoothness, input.uv.xy, input.color, 0, input.normalWS, unifiedNormal, tangentWS);
        #endif
    #endif
    //
    //SRS: This part is moved from the InitializeInputData function to be after the SnowCoverage
    //to ensure that proper normals are used
    #if defined(DYNAMICLIGHTMAP_ON)
        inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV, input.vertexSH, inputData.normalWS);
    #else
        inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
    #endif

#ifdef _DBUFFER
    ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
#endif

    // Stripped down version of UniversalFragmentPBR().

    Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);//SRS: move this line above the InitializeBRDFData
    
    //SRS: calculate sparkle and SSS, then add it to the surface emission
    #if defined(_COVERAGE_ON) && (defined(_SPARKLE_ON) || defined(_SSS_ON))
        half lightmapMask = GetLightmapMask(inputData.bakedGI);
        float distMask = GetDistanceGradient(inputData.positionWS, _SparkleDistFalloff);

        half effectsMask = lightmapMask * pow(snowMask, 2);
        
        #if defined(_SPARKLE_ON)
            surfaceData.emission.rgb += CalcSparkle(_SparkleTex, sampler_SparkleTex, _SparkleTex_TexelSize.xy, _SparklesAmount, _SparklesBrightness, _LocalSparkleTiling, 
            _ScreenSpaceSparklesTiling, _SparklesHighlightMaskExpansion, highlightBrightness, inputData.normalizedScreenSpaceUV, inputData.positionWS, inputData.normalWS, inputData.viewDirectionWS, 
            mainLight.direction, mainLight.color * mainLight.shadowAttenuation, 0, rawSmoothness, distMask * 0.5) * effectsMask * distMask;
        #endif
        #if defined(_SSS_ON)
            surfaceData.emission.rgb += SSS(inputData.normalWS, inputData.viewDirectionWS, mainLight.direction, mainLight.color * mainLight.shadowAttenuation, _SSS_intensity, effectsMask * distMask * sssMask);
        #endif
        surfaceData.emission.rgb += EnhanceColor(mainLight.color * mainLight.shadowAttenuation, 0, rawSmoothness, effectsMask * distMask, _ColorEnhance);
    #endif
    //

    // in LitForwardPass GlobalIllumination (and temporarily LightingPhysicallyBased) are called inside UniversalFragmentPBR
    // in Deferred rendering we store the sum of these values (and of emission as well) in the GBuffer
    BRDFData brdfData;
    InitializeBRDFData(surfaceData.albedo, surfaceData.metallic, surfaceData.specular, surfaceData.smoothness, surfaceData.alpha, brdfData);
   
    MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI, inputData.shadowMask);
    half3 color = GlobalIllumination(brdfData, inputData.bakedGI, surfaceData.occlusion, inputData.positionWS, inputData.normalWS, inputData.viewDirectionWS);
    
    return PackGBuffersBRDFData(brdfData, inputData, surfaceData.smoothness, surfaceData.emission + color, surfaceData.occlusion);
}

#endif
