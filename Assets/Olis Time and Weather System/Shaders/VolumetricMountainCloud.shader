Shader "TimeWeather/VolumetricMountainCloud"
{
    Properties
    {
        _CloudScale("Macro Cloud Scale", Float) = 135
        _DetailScale("Detail Scale", Float) = 46
        _StepCount("Step Count", Range(4, 16)) = 8
        _MaxDistance("Max Distance", Float) = 1200

        _BottomFade("Bottom Fade", Range(0.001, 1)) = 0.12
        _TopFade("Top Fade", Range(0.001, 1)) = 0.18
        _HeightDensityBias("Height Density Bias", Range(-1, 1)) = 0.0

        _OccupancySharpness("Occupancy Sharpness", Range(0.1, 4)) = 1.9
        _AxisBlendBias("Axis Blend Bias", Range(0, 1)) = 0.32
        _CoreContrast("Core Contrast", Range(0.5, 3)) = 1.75
        _EntryRampStrength("Entry Ramp Strength", Range(0, 3)) = 1.1

        _LocalSeed("Local Seed", Float) = 0.0
        _LocalClusterOffset("Local Cluster Offset", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Front

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _CloudScale;
                half _DetailScale;
                half _StepCount;
                half _MaxDistance;

                half _BottomFade;
                half _TopFade;
                half _HeightDensityBias;

                half _OccupancySharpness;
                half _AxisBlendBias;
                half _CoreContrast;
                half _EntryRampStrength;

                half _LocalSeed;
                float4 _LocalClusterOffset;
            CBUFFER_END

            // Global volumetric weather controls set by WeatherController.
            float4 _VolCloudColour;
            float4 _VolShadowColour;

            float _VolDensity;
            float _VolAbsorption;
            float _VolLightingStrength;

            float4 _VolCloudSpeed;
            float _VolCloudSoftness;
            float _VolCoverageBias;
            float _VolTurbulence;
            float _VolWarpStrength;

            float _VolAlphaMultiplier;
            float _VolClusterScale;
            float _VolClusterDensity;
            float _VolDetailStrength;
            float _VolEdgeFade;
            float4 _VolFieldOffset;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
                float fogFactor   : TEXCOORD2;
            };

            float Hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float ValueNoise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = Hash21(i + float2(0,0));
                float b = Hash21(i + float2(1,0));
                float c = Hash21(i + float2(0,1));
                float d = Hash21(i + float2(1,1));

                float x1 = lerp(a, b, f.x);
                float x2 = lerp(c, d, f.x);
                return lerp(x1, x2, f.y);
            }

            float ValueNoise3D(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float n000 = Hash31(i + float3(0,0,0));
                float n100 = Hash31(i + float3(1,0,0));
                float n010 = Hash31(i + float3(0,1,0));
                float n110 = Hash31(i + float3(1,1,0));
                float n001 = Hash31(i + float3(0,0,1));
                float n101 = Hash31(i + float3(1,0,1));
                float n011 = Hash31(i + float3(0,1,1));
                float n111 = Hash31(i + float3(1,1,1));

                float nx00 = lerp(n000, n100, f.x);
                float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x);
                float nx11 = lerp(n011, n111, f.x);

                float nxy0 = lerp(nx00, nx10, f.y);
                float nxy1 = lerp(nx01, nx11, f.y);

                return lerp(nxy0, nxy1, f.z);
            }

            float FBM2D(float2 p, int octaves, float lacunarity, float gain)
            {
                float value = 0.0;
                float amp = 0.5;
                float freq = 1.0;

                [unroll(5)]
                for (int i = 0; i < 5; i++)
                {
                    if (i >= octaves) break;
                    value += ValueNoise2D(p * freq) * amp;
                    freq *= lacunarity;
                    amp *= gain;
                }

                return value;
            }

            float FBM3D(float3 p, int octaves, float lacunarity, float gain)
            {
                float value = 0.0;
                float amp = 0.5;
                float freq = 1.0;

                [unroll(5)]
                for (int i = 0; i < 5; i++)
                {
                    if (i >= octaves) break;
                    value += ValueNoise3D(p * freq) * amp;
                    freq *= lacunarity;
                    amp *= gain;
                }

                return value;
            }

            float3 DomainWarp3D(float3 p, float3 drift, float scale, float strength, float seed)
            {
                float s = max(0.001, scale);
                float3 q = p / s + drift + seed * 0.013;

                float wx = FBM3D(q + float3(17.1, 4.2, -3.4), 3, 2.0, 0.5);
                float wy = FBM3D(q + float3(-8.6, 12.4, 5.9), 3, 2.0, 0.5);
                float wz = FBM3D(q + float3(6.3, -11.7, 9.8), 3, 2.0, 0.5);

                float3 warp = float3(wx, wy, wz) * 2.0 - 1.0;
                return p + warp * strength * s;
            }

            float BoxFade(float3 local01, float edgeFade)
            {
                float3 toEdge = min(local01, 1.0 - local01);
                float minEdge = min(min(toEdge.x, toEdge.y), toEdge.z);
                return saturate(minEdge / max(0.0001, edgeFade));
            }

            float VerticalFade(float y01, float bottomFade, float topFade)
            {
                float bottom = saturate(y01 / max(0.0001, bottomFade));
                float top = saturate((1.0 - y01) / max(0.0001, topFade));
                return bottom * top;
            }

            float AxisOccupancy2D(float2 p, float scale, float seed, float scaleJitter)
            {
                float s = max(1.0, scale * scaleJitter);
                float2 q = p / s + seed * 0.071;

                float n1 = FBM2D(q, 4, 2.0, 0.5);
                float n2 = FBM2D(q * 1.93 + 13.7, 3, 2.17, 0.55);
                float billow = 1.0 - abs(n1 * 2.0 - 1.0);

                float occ = lerp(n1, billow, 0.52);
                occ = lerp(occ, n2, 0.32);

                return saturate(occ);
            }

            float AxisIntersectionOccupancy(float3 worldPos, float seed)
            {
                // Slightly different scales per axis reduce visible seam repetition.
                float xy = AxisOccupancy2D(worldPos.xy, _VolClusterScale, seed + 11.0, 1.00);
                float xz = AxisOccupancy2D(worldPos.xz, _VolClusterScale, seed + 23.0, 0.93);
                float yz = AxisOccupancy2D(worldPos.yz, _VolClusterScale, seed + 37.0, 1.08);

                // Soft tri-planar style blend.
                float average = (xy + xz + yz) * (1.0 / 3.0);
                float minimum = min(xy, min(xz, yz));
                float maximum = max(xy, max(xz, yz));
                float intersection = xy * xz * yz;

                // Blend toward average more than before to hide projection seams,
                // then use minimum/intersection to keep real empty gaps and chunk cores.
                float seamSoftened = lerp(average, minimum, 0.35);
                float chunkMask = lerp(seamSoftened, intersection, 0.42);

                float occ = lerp(chunkMask, maximum, _AxisBlendBias * 0.35);
                occ *= lerp(0.95, 1.45, saturate(_VolClusterDensity * 0.5));

                float sharp = max(0.1, _OccupancySharpness);
                occ = pow(saturate(occ), 1.0 / sharp);

                return saturate(occ);
            }

            float InternalBreakup(float3 worldPos, float seed)
            {
                float baseScale = max(0.001, _CloudScale);
                float detailScale = max(0.001, _DetailScale);

                float nBase = FBM3D(worldPos / baseScale + seed * 0.017, 4, 2.0, 0.5);
                float nDetail = FBM3D((worldPos + 37.1) / detailScale + seed * 0.031, 3, 2.2, 0.55);

                float billow = 1.0 - abs(nBase * 2.0 - 1.0);
                float breakup = lerp(nBase, nDetail, saturate(_VolTurbulence * 0.55));
                breakup = lerp(breakup, billow, 0.42 + _VolTurbulence * 0.22);

                return saturate(lerp(breakup, nDetail, _VolDetailStrength * 0.38));
            }

            float SampleDensity(float3 localOS, float3 worldWS)
            {
                float3 local01 = localOS + 0.5;

                float boxFade = BoxFade(local01, _VolEdgeFade);
                float verticalFade = VerticalFade(local01.y, _BottomFade, _TopFade);

                float3 objectWorldPos = unity_ObjectToWorld._m03_m13_m23;
                float seed = _LocalSeed + dot(objectWorldPos, float3(0.0137, 0.0211, 0.0173));

                float3 drift = float3(_VolCloudSpeed.x, 0.0, _VolCloudSpeed.y) * _Time.y;
                float3 fieldOffset = float3(_VolFieldOffset.x, 0.0, _VolFieldOffset.y) + _LocalClusterOffset.xyz;

                // Warp before occupancy sampling to reduce visible projection joins.
                float3 warpedWorld = DomainWarp3D(worldWS + fieldOffset, drift, _CloudScale, _VolWarpStrength, seed);

                float occupancy = AxisIntersectionOccupancy(warpedWorld, seed);
                float breakup = InternalBreakup(warpedWorld, seed);

                float coverage = saturate(1.0 - (_VolDensity * 0.35) + _VolCoverageBias);
                float threshold = lerp(0.74, 0.24, coverage);
                threshold += (local01.y - 0.5) * _HeightDensityBias * 0.22;

                float softness = max(0.01, _VolCloudSoftness);
                float breakupMask = smoothstep(threshold - softness, threshold + softness, breakup);

                // Push density toward chunk cores so clouds separate from terrain better.
                float coreMask = pow(saturate(occupancy * breakupMask), _CoreContrast);
                float density = lerp(occupancy * breakupMask, coreMask, 0.55);

                density *= boxFade;
                density *= verticalFade;

                return saturate(density);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.viewDirWS = GetWorldSpaceViewDir(pos.positionWS);
                OUT.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 rayOriginWS = _WorldSpaceCameraPos;
                float3 rayDirWS = SafeNormalize(IN.positionWS - rayOriginWS);

                float3 boxMinOS = float3(-0.5, -0.5, -0.5);
                float3 boxMaxOS = float3( 0.5,  0.5,  0.5);

                float3 rayOriginOS = mul(unity_WorldToObject, float4(rayOriginWS, 1.0)).xyz;
                float3 rayDirOS = normalize(mul((float3x3)unity_WorldToObject, rayDirWS));

                float3 invDir = 1.0 / max(abs(rayDirOS), 1e-5) * sign(rayDirOS);
                float3 t0 = (boxMinOS - rayOriginOS) * invDir;
                float3 t1 = (boxMaxOS - rayOriginOS) * invDir;

                float3 tsmaller = min(t0, t1);
                float3 tbigger = max(t0, t1);

                float tEnter = max(max(tsmaller.x, tsmaller.y), tsmaller.z);
                float tExit = min(min(tbigger.x, tbigger.y), tbigger.z);

                if (tExit <= max(0.0, tEnter))
                    discard;

                tEnter = max(0.0, tEnter);
                float maxDistance = min(tExit, _MaxDistance);

                int steps = (int)round(_StepCount);
                steps = clamp(steps, 4, 16);

                float marchLength = maxDistance - tEnter;
                float stepSize = marchLength / steps;

                float transmittance = 1.0;
                float3 accumCol = 0.0;

                Light mainLight = GetMainLight();
                float3 lightDirWS = normalize(mainLight.direction);
                float3 lightDirOS = normalize(mul((float3x3)unity_WorldToObject, lightDirWS));

                [loop]
                for (int i = 0; i < 16; i++)
                {
                    if (i >= steps) break;
                    if (transmittance <= 0.01) break;

                    float t = tEnter + stepSize * (i + 0.5);
                    float normalizedMarch = saturate((t - tEnter) / max(0.0001, marchLength));

                    float3 sampleOS = rayOriginOS + rayDirOS * t;
                    float3 sampleWS = mul(unity_ObjectToWorld, float4(sampleOS, 1.0)).xyz;

                    float density = SampleDensity(sampleOS, sampleWS);
                    if (density <= 0.001) continue;

                    // Density ramps inward a bit so the field reads more like chunked clouds
                    // and less like uniformly faint fog.
                    float entryRamp = lerp(0.55, 1.0, pow(normalizedMarch, max(0.001, _EntryRampStrength)));
                    density *= entryRamp;

                    float3 lightProbeOS = sampleOS + lightDirOS * 0.18;
                    float3 lightProbeWS = mul(unity_ObjectToWorld, float4(lightProbeOS, 1.0)).xyz;
                    float lightDensity = SampleDensity(lightProbeOS, lightProbeWS);

                    // Stronger internal shadowing helps clouds read in front of terrain.
                    float shadowing = saturate(1.0 - lightDensity * _VolAbsorption);
                    shadowing = shadowing * shadowing;

                    float3 litCol = lerp(_VolShadowColour.rgb, _VolCloudColour.rgb, shadowing * _VolLightingStrength);

                    float extinction = density * _VolDensity * stepSize * 0.85;
                    float alphaStep = 1.0 - exp(-extinction);

                    accumCol += litCol * alphaStep * transmittance;
                    transmittance *= (1.0 - alphaStep);
                }

                float alpha = (1.0 - transmittance) * _VolAlphaMultiplier;
                alpha = saturate(alpha);

                half4 col = half4(accumCol, alpha);
                col.rgb = MixFog(col.rgb, IN.fogFactor);
                return col;
            }
            ENDHLSL
        }
    }
}