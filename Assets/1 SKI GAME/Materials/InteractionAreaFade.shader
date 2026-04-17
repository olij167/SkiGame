Shader "Custom/URP/InteractionAreaFlowingVolume"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.20, 0.75, 1.00, 0.18)
        _Intensity ("Body Intensity", Range(0, 3)) = 1.0

        _HeightMin ("Height Min", Range(0,1)) = 0
        _HeightMax ("Height Max", Range(0,1)) = 1

        _TopFadeStart ("Top Fade Start", Range(0, 1)) = 0.15
        _TopFadeEnd ("Top Fade End", Range(0, 1)) = 0.85
        _TopFadeSoftness ("Top Fade Softness", Range(0.25, 6)) = 1.5

        _EdgeFade ("Horizontal Edge Fade", Range(0, 4)) = 0.75

        _FlowStrength ("Flow Strength", Range(0, 4)) = 0.9
        _FlowSpeed ("Flow Speed", Range(0, 5)) = 1.2

        _NoiseScale ("Noise Scale", Range(0.25, 12)) = 4.0
        _NoiseStretch ("Noise Vertical Stretch", Range(0.25, 6)) = 1.8
        _NoiseContrast ("Noise Contrast", Range(0.25, 8)) = 1.5
        _NoiseBreakup ("Noise Breakup", Range(0, 1)) = 0.35

        _FlickerStrength ("Flicker Strength", Range(0, 1)) = 0.08
        _FlickerSpeed ("Flicker Speed", Range(0, 12)) = 4.0

        _FlowFadeStart ("Flow Fade Start", Range(0, 1)) = 0.0
        _FlowFadeEnd ("Flow Fade End", Range(0, 1)) = 0.9
        _FlowFadeSoftness ("Flow Fade Softness", Range(0.25, 6)) = 1.25
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionOS  : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Intensity;

                float _HeightMin;
                float _HeightMax;

                float _TopFadeStart;
                float _TopFadeEnd;
                float _TopFadeSoftness;

                float _EdgeFade;

                float _FlowStrength;
                float _FlowSpeed;

                float _NoiseScale;
                float _NoiseStretch;
                float _NoiseContrast;
                float _NoiseBreakup;

                float _FlickerStrength;
                float _FlickerSpeed;

                float _FlowFadeStart;
                float _FlowFadeEnd;
                float _FlowFadeSoftness;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionOS = IN.positionOS.xyz;
                return OUT;
            }

            float SafeInvLerp(float a, float b, float v)
            {
                return saturate((v - a) / max(0.0001, b - a));
            }

            float ComputeVerticalMask(float y01, float start, float end, float softness)
            {
                if (abs(end - start) < 0.0001)
                {
                    if (start <= 0.0001)
                        return 0.0;

                    if (start >= 0.9999)
                        return 1.0;

                    return y01 <= start ? 1.0 : 0.0;
                }

                float t = saturate((y01 - start) / (end - start));
                t = pow(t, max(0.0001, softness));
                return 1.0 - t;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float Noise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));

                float2 u = f * f * (3.0 - 2.0 * f);

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float FBM(float2 p)
            {
                float v = 0.0;
                float a = 0.5;

                v += Noise2D(p) * a; p *= 2.02; a *= 0.5;
                v += Noise2D(p) * a; p *= 2.03; a *= 0.5;
                v += Noise2D(p) * a; p *= 2.01; a *= 0.5;
                v += Noise2D(p) * a;

                return saturate(v / 0.9375);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 posOS = IN.positionOS;

                // Maps the actual mesh height range to 0..1.
                float y01 = saturate((posOS.y - _HeightMin) / max(0.0001, (_HeightMax - _HeightMin)));

                // -----------------------------
                // 1. AUTHORITATIVE BODY ALPHA
                // -----------------------------
                // This controls the actual visible volume silhouette.
                // Nothing else is allowed to override it.
                float topAlphaMask = ComputeVerticalMask(y01, _TopFadeStart, _TopFadeEnd, _TopFadeSoftness);

                float radial = length(posOS.xz);
                float edgeMask = saturate(1.0 - radial * _EdgeFade);

                float bodyAlphaMask = saturate(topAlphaMask * lerp(0.65, 1.0, edgeMask));

                // -----------------------------
                // 2. FLOW-SPECIFIC VERTICAL MASK
                // -----------------------------
                // Separate from the body fade so the artist can shape how
                // high the animated noise remains active without affecting
                // the main silhouette.
                float flowFadeT = SafeInvLerp(_FlowFadeStart, _FlowFadeEnd, y01);
                flowFadeT = pow(flowFadeT, _FlowFadeSoftness);
                float flowVerticalMask = 1.0 - flowFadeT;

                // -----------------------------
                // 3. CONTINUOUS RISING FLOW
                // -----------------------------
                float t = _Time.y * _FlowSpeed;

                float2 uv1 = float2(posOS.x * _NoiseScale, posOS.y * (_NoiseScale * _NoiseStretch) - t);
                float2 uv2 = float2(posOS.z * (_NoiseScale * 0.82) + 11.7, posOS.y * (_NoiseScale * _NoiseStretch * 1.21) - t * 1.31);
                float2 uv3 = float2((posOS.x + posOS.z) * (_NoiseScale * 0.57) - 4.2, posOS.y * (_NoiseScale * _NoiseStretch * 0.77) - t * 0.69);

                float n1 = FBM(uv1);
                float n2 = FBM(uv2);
                float n3 = FBM(uv3);

                float flowNoise = n1 * 0.5 + n2 * 0.3 + n3 * 0.2;

                // Noise breakup gives more holes / fragmentation without affecting body silhouette.
                float breakup = Noise2D(float2(posOS.x * (_NoiseScale * 0.45) + 23.1, posOS.z * (_NoiseScale * 0.45) - 9.4));
                flowNoise = lerp(flowNoise, flowNoise * breakup, _NoiseBreakup);

                // Contrast controls how soft vs defined the flow looks.
                flowNoise = saturate(pow(saturate(flowNoise), _NoiseContrast));

                float flowMask = flowNoise * flowVerticalMask;

                // -----------------------------
                // 4. SUBTLE FLICKER
                // -----------------------------
                // Only affects the inner glow, never the body alpha.
                float flickerA = sin(_Time.y * _FlickerSpeed + posOS.x * 7.1 + posOS.z * 5.3) * 0.5 + 0.5;
                float flickerB = sin(_Time.y * (_FlickerSpeed * 1.73) + posOS.x * 11.7 - posOS.z * 8.9) * 0.5 + 0.5;
                float flicker = lerp(1.0, flickerA * flickerB * 1.5, _FlickerStrength);

                float finalFlowGlow = flowMask * _FlowStrength * flicker;

                // Clamp all glow by the body alpha mask so it can never reveal the top edge.
                finalFlowGlow *= bodyAlphaMask;

                half3 baseRgb = _BaseColor.rgb;
                half baseAlpha = _BaseColor.a * bodyAlphaMask * _Intensity;

                half3 glowRgb = baseRgb * finalFlowGlow * _Intensity;
                half3 finalRgb = baseRgb + glowRgb;

                half finalA = saturate(baseAlpha + finalFlowGlow * 0.14 * _Intensity);

                return half4(finalRgb, finalA);
            }
            ENDHLSL
        }
    }
}