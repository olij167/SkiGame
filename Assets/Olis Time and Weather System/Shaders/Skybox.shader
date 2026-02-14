// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Skybox/ProceduralGradient"
{
    Properties
    {
        _SkyColor1("Top Color", Color) = (0.37, 0.52, 0.73, 0)
        _SkyExponent1("Top Exponent", Float) = 0.5

        _SkyColor2("Horizon Color", Color) = (0.89, 0.96, 1, 0)
        _HorizonWidth("Horizon Blend Width", Range(0.001, 0.5)) = 0.08

        _SkyColor3("Ground Color", Color) = (0.89, 0.96, 1, 0)
        _GroundExponent("Ground Exponent", Range(0.1, 8.0)) = 1.5

        _SkyIntensity("Sky Intensity", Float) = 1.75

        _MoonColor("Moon Color", Color) = (1, 0.99, 0.87, 1)
        _MoonIntensity("Moon Intensity", Range(0.0,20.0)) = 10.0
        _MoonScale("Moon Scale", Range(0.0,1.0)) = 1.0

        _SunColor("Sun Color", Color) = (1, 0.99, 0.87, 1)
        _SunIntensity("Sun Intensity", Range(0.0,20.0)) = 10.0
        _SunScale("Sun Scale", Range(0.0,1.0)) = 1.0

        _NightStars("Night Stars", Cube) = "starstexture" {}
        _NightOpacity("Night Opacity", Range(0.0,1.0)) = 1.0
        _NightSkySpeed("NightSky Speed", Float) = 2

        // --- Skybox Clouds (seamless, minimal) ---
        _CloudTex("Cloud Noise (Grayscale)", 2D) = "gray" {}
        _CloudColor("Cloud Color", Color) = (1,1,1,1)

        // Debug/quality: allow turning the skybox cloud layer off entirely.
        // (Does not affect the 3D cloud planes.)
        [Toggle]_CloudsEnabled("Clouds Enabled", Float) = 1

        // Driven by WeatherController (keep names identical)
        _CloudAlpha("Cloud Alpha", Range(0,1)) = 0
        _CloudPower("Cloud Power (0=overcast,5=clear)", Range(0,5)) = 5
        _CloudSpeed("Cloud Speed", Vector) = (0,0,0,0)

        // Artist knobs (keep small)
        _CloudTiling("Cloud Tiling", Float) = 0.85
        _CloudMotionScale("Cloud Motion Scale", Range(0, 400)) = 140
        _CloudWarpStrength("Cloud Warp Strength", Range(0, 1)) = 0.16
        _CloudHorizonFade("Cloud Horizon Fade", Range(0,1)) = 0.22

        // Overcast visual response (kept minimal)
        _CloudShadowStrength("Cloud Shadow Strength", Range(0,1)) = 0.65
        _CloudGreyStrength("Cloud Grey Strength", Range(0,1)) = 0.55
        _CloudSunLightStrength("Cloud Sun Light Strength", Range(0,1)) = 0.60

        // Overcast sun/sky attenuation (skybox only)
        _OvercastSunDim("Overcast Sun Dim", Range(0,1)) = 0.75
        _OvercastSkyDim("Overcast Sky Dim", Range(0,1)) = 0.35

        // Driven by WeatherController (optional but already set in your script)
        [HideInInspector]_CloudSeed("Cloud Seed", Float) = 0.0

        _FogColor("Fog Color", Color) = (0.7,0.7,0.75,1)
        _FogDensity("Fog Density", Range(0, 1)) = 0
        _FogSkyStrength("Fog Sky Strength", Range(0, 8)) = 2.5
        _FogHorizonBoost("Fog Horizon Boost", Range(0, 8)) = 3.0
        _FogMax("Fog Max Blend", Range(0,1)) = 0.95

        // Driven by TimeController (your scripts already set these)
        [HideInInspector]_SunDir("Sun Dir", Vector) = (0,1,0,0)
        [HideInInspector]_MoonDir("Moon Dir", Vector) = (0,-1,0,0)
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    struct appdata
    {
        float4 position : POSITION;
        float3 texcoord : TEXCOORD0;
    };

    struct v2f
    {
        float4 position : SV_POSITION;
        float3 texcoord  : TEXCOORD0;
        float3 texcoord1 : TEXCOORD1;
    };

    half3 _SkyColor1;
    half  _SkyExponent1;

    half3 _SkyColor2;
    half  _HorizonWidth;

    half3 _SkyColor3;
    half  _GroundExponent;

    half  _SkyIntensity;

    half3 _MoonColor;
    half  _MoonIntensity;
    half  _MoonScale;

    half3 _SunColor;
    half  _SunIntensity;
    half  _SunScale;

    samplerCUBE _NightStars;
    half  _NightOpacity;
    half  _NightSkySpeed;

    // Clouds
    sampler2D _CloudTex;
    float4 _CloudTex_ST;
    half4 _CloudColor;
    half  _CloudsEnabled;

    half  _CloudAlpha;
    half  _CloudPower;
    float4 _CloudSpeed;

    half  _CloudTiling;
    half  _CloudMotionScale;
    half  _CloudWarpStrength;
    half  _CloudHorizonFade;

    half _CloudShadowStrength;
    half _CloudGreyStrength;
    half _CloudSunLightStrength;

    half _OvercastSunDim;
    half _OvercastSkyDim;

    half  _CloudSeed;

    half4 _FogColor;
    half  _FogDensity;
    half  _FogSkyStrength;
    half  _FogHorizonBoost;
    half  _FogMax;

    float4 _SunDir;
    float4 _MoonDir;

    v2f vert(appdata v)
    {
        v2f o;
        o.position = UnityObjectToClipPos(v.position);
        o.texcoord = v.texcoord;

        // Night sky rotation
        o.texcoord1 = v.texcoord;
        float s = sin(_NightSkySpeed * _Time);
        float c = cos(_NightSkySpeed * _Time);
        float2x2 rot = float2x2(c, -s, s, c);
        rot *= 0.5;
        rot += 0.5;
        rot = rot * 2 - 1;
        o.texcoord1.xy = mul(o.texcoord1.xy, rot);

        return o;
    }

    // Simple helper: sample noise texture with ST applied
    half Noise(float2 uv)
    {
        uv = uv * _CloudTex_ST.xy + _CloudTex_ST.zw;
        return tex2D(_CloudTex, uv).r;
    }

    // Tri-planar sampling using view-direction space:
    // continuous on the sphere and avoids horizon projection stretching.
    half TriplanarNoise(float3 dir, float scale, float2 motion)
    {
        float3 a = abs(dir);
        // Sharpen weights slightly so the blend doesn't look "muddy"
        float3 w = pow(a, 4.0);
        w /= (w.x + w.y + w.z + 1e-6);

        // 3 planar projections (use dir components directly)
        float2 uvX = dir.zy * scale + motion;
        float2 uvY = dir.xz * scale + motion;
        float2 uvZ = dir.xy * scale + motion;

        half nx = Noise(uvX);
        half ny = Noise(uvY);
        half nz = Noise(uvZ);

        return nx * w.x + ny * w.y + nz * w.z;
    }

    half CloudMask(float3 dir)
    {
        if (dir.y <= 0.001) return 0;

        // Fade near horizon to prevent any residual pattern emphasis
        // Fade only very near the horizon. Treat _CloudHorizonFade as a *width*, not a cutoff.
        half hFade = smoothstep(0.0h, max(0.001h, _CloudHorizonFade), dir.y);

        // Motion (boosted)
        float2 motion = (_CloudSpeed.xy * _CloudMotionScale) * _Time.y;

        // Seed offsets (stable per-day/preset from WeatherController)
        // Using non-integer constants prevents obvious alignment.
        float2 seedOff = float2(_CloudSeed * 0.0137, _CloudSeed * 0.0211);

        // Base scale: interpret _CloudTiling as "cloud size"
        // Higher tiling => finer clouds. We invert so increasing tiling gives smaller features.
        float baseScale = lerp(0.35, 2.50, saturate(_CloudTiling));

        // Irrational ratio scale to break repetition
        const float R = 1.7320508; // ~sqrt(3)

        // Domain warp (continuous): one cheap triplanar sample
        half warp = TriplanarNoise(dir, baseScale * 0.55, motion * 0.35 + seedOff) * 2.0h - 1.0h;

        // Apply warp by nudging the direction slightly (continuous, no UV seams)
        float3 dWarp = normalize(dir + float3(warp, warp * 0.6, -warp * 0.8) * (_CloudWarpStrength * 0.18));

        // Two-layer cloud field (continuous + large combined period)
        half n1 = TriplanarNoise(dWarp, baseScale, motion + seedOff);
        half n2 = TriplanarNoise(dWarp, baseScale * R, motion * float2(-0.73, 0.61) + seedOff * 1.37);

        half n = lerp(n1, n2, 0.45h);

        // Coverage semantics: 0 = more clouds, 5 = clear
        half coverage = saturate(1.0h - (_CloudPower / 5.0h));

        // Shape curve tuned to feel “cloud-like” without extra params
        half threshold = lerp(0.82h, 0.36h, coverage);
        half softness  = lerp(0.07h, 0.20h, coverage);

        half m = smoothstep(threshold - softness, threshold + softness, n);

        return m * hFade * _CloudAlpha;
    }

    half4 frag(v2f i) : COLOR
    {
        float3 v = normalize(i.texcoord);

        // --- Seamless sky/ground blend ---
        float y = v.y;
        float hw = max(0.0001, _HorizonWidth);
        float above = smoothstep(-hw, hw, y);

        float skyT = saturate(y);
        skyT = pow(skyT, max(0.0001, _SkyExponent1));
        half3 skyCol = lerp(_SkyColor2, _SkyColor1, skyT);

        float groundT = saturate(-y);
        groundT = pow(groundT, max(0.0001, _GroundExponent));
        half3 groundCol = lerp(_SkyColor2, _SkyColor3, groundT);

        half3 c_sky = lerp(groundCol, skyCol, above);

        // --- Sun / moon direction: prefer explicit vectors from TimeController ---
        float3 sunDir = normalize(_SunDir.xyz);
        if (dot(sunDir, sunDir) < 0.001) sunDir = normalize(_WorldSpaceLightPos0.xyz);

        float3 moonDir = normalize(_MoonDir.xyz);
        if (dot(moonDir, moonDir) < 0.001) moonDir = -sunDir;

        half3 sun = _SunColor * min(pow(max(0, dot(v, sunDir)), 550 / max(0.0001, _SunScale)), 1);
        half3 c_sun = sun * above;

        half3 moon = _MoonColor * pow(max(0, dot(v, moonDir)), 550 / max(0.0001, _MoonScale));
        half3 c_moon = moon * above;

        float3 nightSky = texCUBE(_NightStars, i.texcoord1).rgb * (above * _NightOpacity);

        // Overcast factor derived from existing weather-driven params:
        // _CloudPower: 0=overcast, 5=clear
        // _CloudAlpha: overall cloud visibility
        half overcast = saturate(1.0h - (_CloudPower / 5.0h));
        half cloudiness = saturate(overcast * _CloudAlpha);

        // Never subtract into negatives — lerp to a sane floor instead.
        half sunAtten = lerp(1.0h, 0.35h, cloudiness); // sun disc fades under heavy overcast
        half skyAtten = lerp(1.0h, 0.70h, cloudiness); // sky darkens but never black

        half4 skycol = half4((c_sky * (_SkyIntensity * skyAtten)) + (c_sun * (_SunIntensity * sunAtten)) + nightSky, 0);


        // --- Clouds ---
        half cm = (_CloudsEnabled > 0.5h && _CloudAlpha > 0.001h) ? CloudMask(v) : 0.0h;

        // Base tint: bias toward horizon color so clouds sit in atmosphere
        half3 baseTint = lerp(_SkyColor2, _CloudColor.rgb, 0.65h);

        // Compute a greyscale version of the cloud tint for desaturation
        half lum = dot(baseTint, half3(0.299h, 0.587h, 0.114h));
        half3 grey = lum.xxx;

        // Desaturate toward grey as it becomes overcast
        half greyAmt = saturate(overcast * _CloudGreyStrength);
        half3 tint = lerp(baseTint, grey, greyAmt);

        // Darken as it becomes overcast
        half dark = lerp(1.0h, 1.0h - _CloudShadowStrength, overcast);

       // --- Cloud lighting (cheap phase-ish response) ---
        half NdL = saturate(dot(normalize(v), sunDir) * 0.5h + 0.5h); // 0..1
        // forward scattering peak near sun (clear skies)
        half forward = pow(NdL, 10.0h);
        half wide    = pow(NdL, 2.0h);

        // Overcast flattens directional lighting
        half dirAmt = (1.0h - overcast) * _CloudSunLightStrength;

        // “Silver lining” like boost near sun when clear
        half lightTerm = 1.0h + dirAmt * (0.55h * wide + 0.95h * forward);

        // Overcast extinction: thicker clouds reduce brightness harder
        half thick = saturate(overcast * _CloudShadowStrength);
        half extinction = lerp(1.0h, 0.55h, thick);

        // Final
        half3 cloudCol = tint * dark * lightTerm * extinction;

        // Composite
        skycol.rgb = lerp(skycol.rgb, cloudCol, cm);

        // --- Skybox fog integration (makes fog feel all-consuming) ---
        // Treat sky as “infinite distance”: fog comes from density + view angle.
        // Stronger near horizon, and can still wash out zenith in heavy fog.

        half dens = saturate(_FogDensity);

        // Horizon factor: 1 at horizon, 0 at zenith (above)
        half horizon = 1.0h - saturate((v.y - 0.02h) / 0.45h); // tuneable band
        horizon = pow(horizon, 1.35h);

        // Base fog amount: exponential-ish response
        half fogAmt = 1.0h - exp(-dens * _FogSkyStrength);

        // Boost near horizon
        fogAmt = saturate(fogAmt + horizon * dens * _FogHorizonBoost);

        // Clamp so sky never becomes 100% flat unless you want it to
        fogAmt = min(fogAmt, _FogMax);

        // Apply fog equally to sky + clouds result
        skycol.rgb = lerp(skycol.rgb, _FogColor.rgb, fogAmt);


        return skycol;
    }

    ENDCG

    SubShader
    {
        Tags { "RenderType" = "Skybox" "Queue" = "Background" }
        Pass
        {
            ZWrite Off
            Cull Off
            Fog { Mode Off }
            CGPROGRAM
            #pragma fragmentoption ARB_precision_hint_fastest
            #pragma vertex vert
            #pragma fragment frag
            ENDCG
        }
    }
}
