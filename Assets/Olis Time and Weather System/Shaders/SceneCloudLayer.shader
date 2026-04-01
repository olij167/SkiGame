Shader "TimeWeather/SceneCloudLayer"
{
    Properties
    {
        _CloudColour("Cloud Colour", Color) = (1,1,1,1)
        _ShadowColour("Shadow Colour", Color) = (0.72,0.75,0.8,1)

        _CloudAlpha("Cloud Alpha", Range(0, 20)) = 6
        _CloudPower("Cloud Power", Range(0, 5)) = 2.2

        _CloudScale("Cloud Scale", Float) = 170
        _DetailScale("Detail Scale", Float) = 340
        _CloudSeperation("Cloud Separation", Range(0, 10)) = 5

        _CloudSpeed("Cloud Speed", Vector) = (0.003, 0.003, 0, 0)
        _DistortSpeed("Distort Speed", Vector) = (0.002, 0.002, 0, 0)
        _DistortScale("Distort Scale", Float) = 220

        _CloudSoftness("Cloud Softness", Range(0.01, 0.5)) = 0.22
        _CloudCoverageBias("Cloud Coverage Bias", Range(-0.5, 0.5)) = 0.03
        _CloudTurbulence("Cloud Turbulence", Range(0, 1)) = 0.30
        _CloudWarpStrength("Cloud Warp Strength", Range(0, 1)) = 0.12

        _DepthFade("Depth Fade", Range(0, 50)) = 6
        _EdgeFade("Edge Fade", Range(0, 1)) = 0.14
        _RadialFade("Radial Fade", Range(0, 1)) = 0.22
        _HeightFade("Height Fade", Range(0, 1)) = 0.16
        _FresnelFade("Fresnel Fade", Range(0, 8)) = 1.25
        _AlphaClip("Alpha Clip", Range(0, 1)) = 0
        _VertexOffset("Vertex Offset", Range(-2, 2)) = 0

        _MacroRoundness("Macro Roundness", Range(0, 2)) = 0.95
        _DetailStrength("Detail Strength", Range(0, 2)) = 0.55
        _DensityContrast("Density Contrast", Range(0.5, 3)) = 1.10

        _ErosionStrength("Erosion Strength", Range(0, 2)) = 0.65
        _SecondLayerStrength("Second Layer Strength", Range(0, 2)) = 0.35
        _SilverLiningStrength("Silver Lining Strength", Range(0, 2)) = 0.35
        _BottomDarkening("Bottom Darkening", Range(0, 2)) = 0.45
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CloudColour;
                half4 _ShadowColour;

                half _CloudAlpha;
                half _CloudPower;

                half _CloudScale;
                half _DetailScale;
                half _CloudSeperation;

                float4 _CloudSpeed;
                float4 _DistortSpeed;
                half _DistortScale;

                half _CloudSoftness;
                half _CloudCoverageBias;
                half _CloudTurbulence;
                half _CloudWarpStrength;

                half _DepthFade;
                half _EdgeFade;
                half _RadialFade;
                half _HeightFade;
                half _FresnelFade;
                half _AlphaClip;
                half _VertexOffset;

                half _MacroRoundness;
                half _DetailStrength;
                half _DensityContrast;

                half _ErosionStrength;
                half _SecondLayerStrength;
                half _SilverLiningStrength;
                half _BottomDarkening;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS    : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                float3 positionOS : TEXCOORD3;
                half3 viewDirWS   : TEXCOORD4;
                float fogFactor   : TEXCOORD5;
            };

            float Hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = Hash21(i + float2(0, 0));
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));

                float x1 = lerp(a, b, f.x);
                float x2 = lerp(c, d, f.x);
                return lerp(x1, x2, f.y);
            }

            float FBM(float2 p, int octaves, float lacunarity, float gain)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;

                [unroll(6)]
                for (int i = 0; i < 6; i++)
                {
                    if (i >= octaves) break;
                    value += ValueNoise(p * frequency) * amplitude;
                    frequency *= lacunarity;
                    amplitude *= gain;
                }

                return value;
            }

            float BillowNoise(float2 p)
            {
                float n = FBM(p, 5, 2.0, 0.5);
                n = 1.0 - abs(n * 2.0 - 1.0);
                return saturate(n);
            }

            float2 DomainWarp(float2 p, float2 flow, float scale, float strength)
            {
                float s = max(0.001, scale);
                float2 q = p / s + flow;

                float wx = FBM(q + float2(17.1, -8.3), 3, 2.0, 0.5);
                float wy = FBM(q + float2(-5.4, 13.7), 3, 2.0, 0.5);

                float2 warp = float2(wx, wy) * 2.0 - 1.0;
                return p + warp * strength * s;
            }

            float SampleMacroCloud(float2 p)
            {
                float baseN = FBM(p, 5, 2.0, 0.5);
                float billow = BillowNoise(p * 0.92 + 7.13);
                return saturate(lerp(baseN, billow, _MacroRoundness));
            }

            float SampleErosion(float2 p)
            {
                float e1 = FBM(p, 4, 2.1, 0.55);
                float e2 = BillowNoise(p * 1.27 - 5.41);
                return saturate(lerp(e1, e2, 0.45));
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 positionOS = IN.positionOS.xyz;
                positionOS.y += IN.normalOS.y * _VertexOffset;

                VertexPositionInputs posInputs = GetVertexPositionInputs(positionOS);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS = normalize(normalInputs.normalWS);
                OUT.uv = IN.uv;
                OUT.positionOS = IN.positionOS.xyz;
                OUT.viewDirWS = SafeNormalize(GetWorldSpaceViewDir(posInputs.positionWS));
                OUT.fogFactor = ComputeFogFactor(posInputs.positionCS.z);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 flow = _CloudSpeed.xy * _Time.y;
                float2 distortFlow = _DistortSpeed.xy * _Time.y;

                float2 worldXZ = IN.positionWS.xz;
                float2 warpedPos = DomainWarp(worldXZ, distortFlow, _DistortScale, _CloudWarpStrength);

                float cloudScale = max(0.001, _CloudScale);
                float detailScale = max(0.001, _DetailScale);

                // Primary layer
                float2 macroP = warpedPos / cloudScale + flow;
                float2 erosionP = warpedPos / detailScale + flow * 1.25;

                // Secondary internal layer for richer massing
                float2 macroP2 = (warpedPos + float2(91.7, -43.2)) / (cloudScale * 0.73) + flow * 0.72;
                float2 erosionP2 = (warpedPos + float2(-57.1, 28.4)) / (detailScale * 0.82) + flow * 1.58;

                float macro1 = SampleMacroCloud(macroP);
                float macro2 = SampleMacroCloud(macroP2);
                float erosion1 = SampleErosion(erosionP);
                float erosion2 = SampleErosion(erosionP2);

                float macroCombined = max(macro1, lerp(0.0, macro2, _SecondLayerStrength));
                float erosionCombined = lerp(erosion1, erosion2, 0.45);

                // Coverage defines broad cloud presence first.
                float coverage = saturate(1.0 - (_CloudPower / 5.0) + _CloudCoverageBias);
                float threshold = lerp(0.76, 0.36, coverage);
                float softness = max(0.01, _CloudSoftness);

                float broadMask = smoothstep(threshold - softness, threshold + softness, macroCombined);

                // Erode outward edges and create internal breakup.
                float erosionAmount = (erosionCombined - 0.5) * (_ErosionStrength * 0.55);
                float densityField = saturate(macroCombined - erosionAmount);

                float mask = smoothstep(threshold - softness, threshold + softness, densityField);
                mask = lerp(mask, broadMask, 0.28);
                mask = pow(saturate(mask), _DensityContrast);

                float2 centered = IN.uv * 2.0 - 1.0;

                // Use fades mainly as cleanup rather than primary silhouette shaping.
                float radialDist = length(centered);
                half radialMask = 1.0h - smoothstep(max(0.0h, 1.0h - _RadialFade), 1.0h, radialDist);

                float edgeX = 1.0 - smoothstep(1.0 - _EdgeFade, 1.0, abs(centered.x));
                float edgeY = 1.0 - smoothstep(1.0 - _EdgeFade, 1.0, abs(centered.y));
                half edgeMask = saturate(min(edgeX, edgeY));

                half heightMask = 1.0h - smoothstep(0.0h, max(0.001h, _HeightFade), saturate(abs(IN.positionOS.y)));

                Light mainLight = GetMainLight();
                half3 lightDir = normalize(mainLight.direction);
                half NdotL = saturate(dot(normalize(IN.normalWS), lightDir) * 0.5h + 0.5h);

                // Stronger internal lighting variation.
                half densityShade = saturate(mask * 0.85h + densityField * 0.28h);

                // Slight darker underside feel from local vertical orientation.
                half underside = saturate((0.5h - IN.normalWS.y * 0.5h) * _BottomDarkening);

                // Silver lining / forward edge glow.
                half rim = pow(1.0h - saturate(dot(normalize(IN.normalWS), normalize(IN.viewDirWS))), max(0.25h, _FresnelFade));
                half silver = rim * saturate(NdotL) * _SilverLiningStrength;

                half3 baseLit = lerp(_ShadowColour.rgb, _CloudColour.rgb, NdotL);
                half3 shaded = lerp(_ShadowColour.rgb * (1.0h + underside * 0.35h), baseLit, densityShade);
                half3 cloudCol = shaded + (_CloudColour.rgb * silver * 0.35h);

                half alpha = mask;
                alpha *= (_CloudAlpha / 20.0h);
                alpha *= lerp(1.0h, edgeMask, 0.65h);
                alpha *= lerp(1.0h, radialMask, 0.55h);
                alpha *= lerp(1.0h, heightMask, saturate(_HeightFade));

                clip(alpha - _AlphaClip);

                half4 col = half4(cloudCol, saturate(alpha));
                col.rgb = MixFog(col.rgb, IN.fogFactor);

                return col;
            }
            ENDHLSL
        }
    }
}