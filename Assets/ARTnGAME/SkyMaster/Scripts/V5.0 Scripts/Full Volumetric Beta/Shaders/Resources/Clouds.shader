Shader "Hidden/Clouds"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
		_WeatherTexture("Texture", 2D) = "white" {}

		//v0.7a - VORTEX
			vortexPosRadius("vortexPosRadius", Float) = (0, 0, 0, 0)
			vortexControlsA("vortexControlsA", Float) = (0, 0, 0, 0)
	}
		SubShader
	{
		// No culling or depth
		Cull Off ZWrite Off ZTest Off

		Pass
	{
		CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma target 3.0
#pragma multi_compile __ DEBUG_NO_LOW_FREQ_NOISE
#pragma multi_compile __ DEBUG_NO_HIGH_FREQ_NOISE
#pragma multi_compile __ DEBUG_NO_CURL
#pragma multi_compile __ ALLOW_IN_CLOUDS
#pragma multi_compile __ RANDOM_JITTER_WHITE RANDOM_JITTER_BLUE
#pragma multi_compile __ RANDOM_UNIT_SPHERE
#pragma multi_compile __ SLOW_LIGHTING

#pragma multi_compile_instancing //v5.2.2 VR

#include "UnityCG.cginc"
#include "Lighting.cginc"//v5.2.2

#define BIG_STEP 3.0


		//VORTEX
			//v0.8
			float4 vortexPosRadius;
			float4 	vortexControlsA;

	//v5.2.2 - VR
	//view/projection matrices proved by VolumetricSphere.cs (in none vr, only left eye is used)
	//https://github.com/chriscummings100/worldspaceposteffect/blob/master/Assets/WorldSpacePostEffect/WorldSpacePostEffect.shader
	float4x4 _LeftWorldFromView;
	float4x4 _RightWorldFromView;
	float4x4 _LeftViewFromScreen;
	float4x4 _RightViewFromScreen;

		struct appdata
	{
		float4 vertex : POSITION;
		float2 uv : TEXCOORD0;
		UNITY_VERTEX_INPUT_INSTANCE_ID
	};

	struct v2f
	{
		float2 uv : TEXCOORD0;
		float4 pos : SV_POSITION;
		float4 ray : TEXCOORD1;

		//v5.2.2
		float2 uv_depth : TEXCOORD2;
		UNITY_VERTEX_OUTPUT_STEREO
	};

	uniform int _renderInFront = 0;//v0.1

	//v0.9 - v5.2.1
	float4 specialFX;

	//v0.6
	float depthDilation;

	//v0.6
	float4 _CameraWSOffset;

	//v5.0
	float4 _LocalLightPos;
	float4 _LocalLightColor;

	//v0.3
	int scatterOn = 1;
	int sunRaysOn = 1;
	float zeroCountSteps = 0;
	int sunShaftSteps = 5;

	//v0.2
	//v3.5.3
	sampler2D _InteractTexture;
	float4 _InteractTexturePos;
	float4 _InteractTextureAtr;
	float4 _InteractTextureOffset; //v4.0

	float _Scatter = 0.008;
	float _HGCoeff = 0.5;
	float _Extinct = 0.01;

	float3 _SkyTint = float3(.5, .5, .5);
	//float _SunSize = 0.04;
	//float3 _GroundColor=float3(.369, .349, .341); //v4.0
	//float _Exposure=3; //v4.0
	float _BackShade=1;
	//float _UndersideCurveFactor=1;

	int4 _SceneFogMode = int4(0,0,1,1); // x = fog mode, y = use radial flag
	float4 _SceneFogParams = int4(0, 1, 1, 1);
#ifndef UNITY_APPLY_FOG
	half4 unity_FogColor;
	half4 unity_FogDensity;
#endif
	// for fast world space reconstruction
	uniform float4x4 _FrustumCornersWS;
	uniform float4 _CameraWS;
	uniform float4 _HeightParams = float4(0,1000,0,0);

	// x = start distance
	uniform float4 _DistanceParams;


	uniform float luminance, Multiplier1, Multiplier2, Multiplier3, bias, lumFac, contrast, turbidity = 1;
	//uniform float mieDirectionalG = 0.7,0.913; 
	float mieDirectionalG=0.94;
	float mieCoefficient=0.054;//0.054
	float reileigh=0.01;

	uniform float e = 2.71828182845904523536028747135266249775724709369995957;
	uniform float pi = 3.141592653589793238462643383279502884197169;
	uniform float n = 1.0003;
	uniform float N = 2.545E25;
	uniform float pn = 0.035;
	uniform float3 lambda = float3(680E-9, 550E-9, 450E-9);
	uniform float3 K = float3(0.686, 0.678, 0.666);//const vec3 K = vec3(0.686, 0.678, 0.666);
	uniform float v = 4.0;
	uniform float rayleighZenithLength = 8.4E3;
	uniform float mieZenithLength = 1.25E3;
	uniform float EE = 1000.0;
	uniform float sunAngularDiameterCos = 0.999956676946448443553574619906976478926848692873900859324;
	// 66 arc seconds -> degrees, and the cosine of that
	float cutoffAngle = 3.141592653589793238462643383279502884197169 / 1.95;
	float steepness = 1.5;
	float HenyeyGreenstein(float cosine)
	{
		float g2 = _HGCoeff * _HGCoeff;
		return 0.5 * (1 - g2) / pow(1 + g2 - 2 * _HGCoeff * cosine, 1.5);
	}

	float Beer(float depth)
	{
		return exp(-_Extinct * depth * _BackShade);  // return exp(-_Extinct * depth); //_BackShade v3.5
	}

	float BeerPowder(float depth)
	{
		return exp(-_Extinct * depth) * (1 - exp(-_Extinct * 2 * depth));
	}
	// Applies one of standard fog formulas, given fog coordinate (i.e. distance)
	half ComputeFogFactor(float coord)
	{
		float fogFac = 0.0;
		if (_SceneFogMode.x == 1) // linear
		{
			// factor = (end-z)/(end-start) = z * (-1/(end-start)) + (end/(end-start))
			fogFac = coord * _SceneFogParams.z + _SceneFogParams.w;
		}
		if (_SceneFogMode.x == 2) // exp
		{
			// factor = exp(-density*z)
			fogFac = _SceneFogParams.y * coord; fogFac = exp2(-fogFac);
		}
		if (_SceneFogMode.x == 3) // exp2
		{
			// factor = exp(-(density*z)^2)
			fogFac = _SceneFogParams.x * coord; fogFac = exp2(-fogFac * fogFac);
		}
		return saturate(fogFac);
	}

	// Distance-based fog
	float ComputeDistance(float3 camDir, float zdepth)
	{
		float dist;
		if (_SceneFogMode.y == 1)
			dist = length(camDir);
		else
			dist = zdepth * _ProjectionParams.z;
		// Built-in fog starts at near plane, so match that by
		// subtracting the near value. Not a perfect approximation
		// if near plane is very large, but good enough.
		dist -= _ProjectionParams.y;
		return dist;
	}

	// Linear half-space fog, from https://www.terathon.com/lengyel/Lengyel-UnifiedFog.pdf
	float ComputeHalfSpace(float3 wsDir)
	{
		float3 wpos = _CameraWS + wsDir + _CameraWSOffset;
		float FH = _HeightParams.x;
		float3 C = _CameraWS + _CameraWSOffset;
		float3 V = wsDir;
		float3 P = wpos;
		float3 aV = _HeightParams.w * V;
		float FdotC = _HeightParams.y;
		float k = _HeightParams.z;
		float FdotP = P.y - FH;
		float FdotV = wsDir.y;
		float c1 = k * (FdotP + FdotC);
		float c2 = (1 - 2 * k) * FdotP;
		float g = min(c2, 0.0);
		g = -length(aV) * (c1 - g * g / abs(FdotV + 1.0e-5f));
		return g;
	}

	//SM v1.7
	float3 totalRayleigh(float3 lambda) {
		float pi = 3.141592653589793238462643383279502884197169;
		float n = 1.0003; // refraction of air
		float N = 2.545E25; //molecules per air unit volume 								
		float pn = 0.035;
		return (8.0 * pow(pi, 3.0) * pow(pow(n, 2.0) - 1.0, 2.0) * (6.0 + 3.0 * pn)) / (3.0 * N * pow(lambda, float3(4.0, 4.0, 4.0)) * (6.0 - 7.0 * pn));
	}

	float rayleighPhase(float cosTheta)
	{
		return (3.0 / 4.0) * (1.0 + pow(cosTheta, 2.0));
	}

	float3 totalMie(float3 lambda, float3 K, float T)
	{
		float pi = 3.141592653589793238462643383279502884197169;
		float v = 4.0;
		float c = (0.2 * T) * 10E-18;
		return 0.434 * c * pi * pow((2.0 * pi) / lambda, float3(v - 2.0, v - 2.0, v - 2.0)) * K;
	}

	float hgPhase(float cosTheta, float g)
	{
		float pi = 3.141592653589793238462643383279502884197169;
		return (1.0 / (4.0*pi)) * ((1.0 - pow(g, 2.0)) / pow(1.0 - 2.0*g*cosTheta + pow(g, 2.0), 1.5));
	}

	float sunIntensity(float zenithAngleCos)
	{
		float cutoffAngle = 3.141592653589793238462643383279502884197169 / 1.95;//pi/
		float steepness = 1.5;
		float EE = 1000.0;
		return EE * max(0.0, 1.0 - exp(-((cutoffAngle - acos(zenithAngleCos)) / steepness)));
	}

	float logLuminance(float3 c)
	{
		return log(c.r * 0.2126 + c.g * 0.7152 + c.b * 0.0722);
	}

	float3 tonemap(float3 HDR)
	{
		float Y = logLuminance(HDR);
		float low = exp(((Y*lumFac + (1.0 - lumFac))*luminance) - bias - contrast / 2.0);
		float high = exp(((Y*lumFac + (1.0 - lumFac))*luminance) - bias + contrast / 2.0);
		float3 ldr = (HDR.rgb - low) / (high - low);
		return float3(ldr);
	}








	uniform sampler2D_float _CameraDepthTexture;

	uniform float4x4 _CameraInvViewMatrix;
	uniform float4x4 _FrustumCornersES;
	//uniform float4 _CameraWS;
	uniform float _FarPlane;

	uniform sampler2D _MainTex;
	uniform float4 _MainTex_TexelSize;

	uniform sampler2D _AltoClouds;
	uniform sampler3D _ShapeTexture;
	uniform sampler3D _DetailTexture;
	uniform sampler2D _WeatherTexture;
	uniform sampler2D _CurlNoise;
	uniform sampler2D _BlueNoise;
	uniform float4 _BlueNoise_TexelSize;
	uniform float4 _Randomness;
	uniform float _SampleMultiplier;

	uniform float3 _SunDir;
	uniform float3 _PlanetCenter;
	uniform float3 _SunColor;

	uniform float4 _CloudBaseColor;
	uniform float3 _CloudTopColor;

	uniform float3 _ZeroPoint;
	uniform float _SphereSize;
	uniform float2 _CloudHeightMinMax;
	uniform float _Thickness;

	uniform float _Coverage;
	uniform float _AmbientLightFactor;
	uniform float _SunLightFactor;
	uniform float _HenyeyGreensteinGForward;
	uniform float _HenyeyGreensteinGBackward;
	uniform float _LightStepLength;
	uniform float _LightConeRadius;

	uniform float _Density;

	uniform float _Scale;
	uniform float _DetailScale;
	uniform float _WeatherScale;
	uniform float _CurlDistortScale;
	uniform float _CurlDistortAmount;

	uniform float _WindSpeed;
	uniform float3 _WindDirection;
	uniform float3 _WindOffset;
	uniform float2 _CoverageWindOffset;
	uniform float2 _HighCloudsWindOffset;

	uniform float _CoverageHigh;
	uniform float _CoverageHighScale;
	uniform float _HighCloudsScale;

	uniform float2 _LowFreqMinMax;
	uniform float _HighFreqModifier;

	uniform float4 _Gradient1;
	uniform float4 _Gradient2;
	uniform float4 _Gradient3;

	uniform int _Steps;

	v2f vert(appdata v)
	{
		v2f o;

		//v5.2.2
		UNITY_SETUP_INSTANCE_ID(v); //Insert
		UNITY_INITIALIZE_OUTPUT(v2f, o); //Insert
		UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); //Insert

		half index = v.vertex.z;
		v.vertex.z = 0.1;

		o.pos = UnityObjectToClipPos(v.vertex);
		o.uv = v.uv.xy;

#if UNITY_UV_STARTS_AT_TOP
		if (_MainTex_TexelSize.y < 0)
			o.uv.y = 1 - o.uv.y;
#endif

		// Get the eyespace view ray (normalized)
		o.ray = _FrustumCornersES[(int)index];
		// Dividing by z "normalizes" it in the z axis
		// Therefore multiplying the ray by some number i gives the viewspace position
		// of the point on the ray with [viewspace z]=i
		o.ray /= abs(o.ray.z);

		//v5.2.2 - VR
		o.uv_depth = MultiplyUV(UNITY_MATRIX_TEXTURE0, v.uv);
#if UNITY_UV_STARTS_AT_TOP
		if (_MainTex_TexelSize.y < 0) {
			o.uv_depth.y = 1 - o.uv_depth.y;
		}
#endif	


		// Transform the ray from eyespace to worldspace
		//o.ray = mul(_CameraInvViewMatrix, o.ray); //v5.2.2
		//v5.0.2
		//pick one of the passed in projection/view matrices based on stereo eye selection (always left if not vr)
		float4x4 proj, eyeToWorld;
		if (unity_StereoEyeIndex == 0)
		{
			proj = _LeftViewFromScreen;
			eyeToWorld = _LeftWorldFromView;
		}
		else
		{
			proj = _RightViewFromScreen;
			eyeToWorld = _RightWorldFromView;
		}
		////bit of matrix math to take the screen space coord (u,v,depth) and transform to world space
		//float2 uvClip = i.uv * 2.0 - 1.0;
		//float4 clipPos = float4(uvClip, rawDepth, 1.0);
		//float4 viewPos = mul(proj, clipPos); // inverse projection by clip position
		//viewPos /= viewPos.w; // perspective division
		//float3 wsPos = mul(eyeToWorld, viewPos).xyz; // FIXED !!!! for STEREO VR		
		//float3 ray = (wsPos - _WorldSpaceCameraPos) / length(wsPos - _WorldSpaceCameraPos);
		o.ray = mul(eyeToWorld, o.ray);

		return o;
	}

	// http://momentsingraphics.de/?p=127#jittering
	float getRandomRayOffset(float2 uv) // uses blue noise texture to get random ray offset
	{
		float noise = tex2D(_BlueNoise, uv).x;
		noise = mad(noise, 2.0, -1.0);
		return noise;
	}

	// http://byteblacksmith.com/improvements-to-the-canonical-one-liner-glsl-rand-for-opengl-es-2-0/
	float rand(float2 co) {
		float a = 12.9898;
		float b = 78.233;
		float c = 43758.5453;
		float dt = dot(co.xy, float2(a, b));
		float sn = fmod(dt, 3.14);

		return 2.0 * frac(sin(sn) * c) - 1.0;
	}

	float weatherDensity(float3 weatherData) // Gets weather density from weather texture sample and adds 1 to it.
	{
		return weatherData.b + 1.0;
	}

	// from GPU Pro 7 - remaps value from one range to other range
	float remap(float original_value, float original_min, float original_max, float new_min, float new_max)
	{
		return new_min + (((original_value - original_min) / (original_max - original_min)) * (new_max - new_min));
	}

	//v0.7a - VORTEX
	float3x3 rotationMatrix(float3 axis, float angle)
	{
		axis = normalize(axis);
		float s = sin(angle);
		float c = cos(angle);
		float oc = 1.0 - c;

		return float3x3 (oc * axis.x * axis.x + c, oc * axis.x * axis.y - axis.z * s, oc * axis.z * axis.x + axis.y * s,
			oc * axis.x * axis.y + axis.z * s, oc * axis.y * axis.y + c, oc * axis.y * axis.z - axis.x * s,
			oc * axis.z * axis.x - axis.y * s, oc * axis.y * axis.z + axis.x * s, oc * axis.z * axis.z + c);
	}

	// returns height fraction [0, 1] for point in cloud
	float getHeightFractionForPoint(float3 pos)
	{
#if defined(DEBUG_NO_LOW_FREQ_NOISE) //VORTEX
		//v0.7 VORTEX
		//float4 vortexPosRadius = vortexPosRadius;// float4(0, 0, 0, 1100); //v0.8
		float distanceToVortexCenter = length(vortexPosRadius.xz - pos.xz);
		float4 thick = _Thickness;
		if (distanceToVortexCenter < vortexPosRadius.w) { //if (distanceToVortexCenter < 280000) { //v0.8
			//pos.y = pos.y + 1000*distanceToVortexCenter;
			thick = _Thickness * 5;
		}
		return ((distance(pos, _PlanetCenter) - (_SphereSize + _CloudHeightMinMax.x)) / thick);
#else
		return saturate((distance(pos,  _PlanetCenter) - (_SphereSize + _CloudHeightMinMax.x)) / _Thickness);
#endif
	}

	// samples the gradient
	float sampleGradient(float4 gradient, float height)
	{
		return smoothstep(gradient.x, gradient.y, height) - smoothstep(gradient.z, gradient.w, height);
	}

	// lerps between cloud type gradients and samples it
	float getDensityHeightGradient(float height, float3 weatherData)
	{
		float type = weatherData.g;
		float4 gradient = lerp(lerp(_Gradient1, _Gradient2, type * 2.0), _Gradient3, saturate((type - 0.5) * 2.0));
		return sampleGradient(gradient, height);
	}

	// samples weather texture
	float3 sampleWeather(float3 pos) {
		float3 weatherData = tex2Dlod(_WeatherTexture, float4((pos.xz + _CoverageWindOffset) * _WeatherScale, 0, 0)).rgb;


		//VORTEX
#if defined(DEBUG_NO_LOW_FREQ_NOISE) //VORTEX
		//VORTEX
		float distanceToVortexCenter = length(vortexPosRadius.xz - pos.xz);
		//v0.7
		float3x3 rotator = rotationMatrix(float3(0, 1, 0), 1 * (_Time.y * vortexControlsA.x));
		float3 posVertex = mul(float3(pos.x, 0, pos.z), rotator);
		float3 weatherData2 = tex2Dlod(_WeatherTexture, float4((posVertex.xz + _CoverageWindOffset) * _WeatherScale, 0, 0)).rgb;
		//if (distanceToVortexCenter > 0) {				
			//weatherData.r = 1;
		//
		if (distanceToVortexCenter > vortexPosRadius.w) {//if (distanceToVortexCenter > 280000) { //280000 /110000 //vortexPosRadius
			//posVertex = pos;
			//weatherData.r = 1;
		}
		else {
			weatherData = lerp(weatherData2, weatherData, 1 - saturate(distanceToVortexCenter / 1000));
			weatherData.r *= 14;
		}
		//v0.7a
		//weatherData.r = weatherData.r + (distanceToVortexCenter / 100000) - 0.2;
#endif


		weatherData.r = saturate(weatherData.r - _Coverage);

		//v0.2
		float4 texInteract = tex2Dlod(_InteractTexture,0.0003*float4(
	     //_InteractTexturePos.x*pos.x + _InteractTexturePos.z*-_Scroll1.x * _Time.x + _InteractTextureOffset.x,
	     //_InteractTexturePos.y*pos.z + _InteractTexturePos.w*-_Scroll1.z * _Time.x + _InteractTextureOffset.y,
		_InteractTexturePos.x*pos.x + _InteractTexturePos.z * _Time.x + _InteractTextureOffset.x,
		_InteractTexturePos.y*pos.z + _InteractTexturePos.w * _Time.x + _InteractTextureOffset.y,
	     0,0)); 
		float3 _LocalLightPos = float3(0, 0, 0);
	    float diffPos = length(_LocalLightPos.xyz - pos);
	     texInteract.a = texInteract.a +clamp( _InteractTextureAtr.z * 0.1*(1 - 0.00024*diffPos),-1.5,0);
		 weatherData = weatherData * clamp(texInteract.a*_InteractTextureAtr.w, _InteractTextureAtr.y, 1);
	  //  _NoiseAmp2 = _NoiseAmp2*clamp(texInteract.a*_InteractTextureAtr.w,_InteractTextureAtr.y,1);

		return weatherData;
	}

	// samples cloud density
	float sampleCloudDensity(float3 p, float heightFraction, float3 weatherData, float lod, bool sampleDetail)
	{
		float3 pos = p + _WindOffset; // add wind offset
		pos += heightFraction * _WindDirection * 700.0; // shear at higher altitude

//#if defined(DEBUG_NO_LOW_FREQ_NOISE)
//		float cloudSample = 0.7;
//		cloudSample = remap(cloudSample, _LowFreqMinMax.x, _LowFreqMinMax.y, 0.0, 1.0);
//#else
#if defined(DEBUG_NO_LOW_FREQ_NOISE) //VORTEX
		float cloudSample = 0;
		if (vortexControlsA.w == 1) {
			//		float cloudSample = 0.7;
			//		cloudSample = remap(cloudSample, _LowFreqMinMax.x, _LowFreqMinMax.y, 0.0, 1.0);
			//float cloudSample = tex3Dlod(_ShapeTexture, float4(pos * _Scale, lod)).r; // sample cloud shape texture
			//v0.7a
			//float4 vortexPosRadius = float4(0, 0, 0, 1000);
			float distanceToVortexCenter = length(vortexPosRadius.xz - pos.xz);
			float3x3 rotator = rotationMatrix(float3(0, 1, 0), 1 * ((_Time.y * 2 * vortexControlsA.y) - 700 * (distanceToVortexCenter / 10000000)));
			float3 posVertex = mul(float3(pos.x, 0, pos.z), rotator);
			posVertex.y = pos.y;
			if (distanceToVortexCenter > vortexPosRadius.w) {//if (distanceToVortexCenter > 150000) { //v0.8
				posVertex = pos;
			}
			//posVertex.y = posVertex.y - (_Time.y * 12);
			cloudSample = tex3Dlod(_ShapeTexture, float4(posVertex * _Scale, lod)).r;
			cloudSample = remap(cloudSample * pow(1.2 - heightFraction, 0.1), _LowFreqMinMax.x, _LowFreqMinMax.y, 0.0, 1.0); // pick certain range from sample texture
		}
		else {
			//LONG VORTEX - TYPHOON
			float distanceToVortexCenter = length(vortexPosRadius.xz - pos.xz);
			if (pos.y < 15000 * vortexControlsA.w) {
				distanceToVortexCenter = length(vortexPosRadius.xz - pos.xz)*(0.0001*abs(15000 * vortexControlsA.w - pos.y));
			}
			float3x3 rotator = rotationMatrix(float3(0.005 * 0, 1, 0), 1 * ((_Time.y * 2 * vortexControlsA.y) - 700 * (distanceToVortexCenter / 10000000)));
			float3 posVertex = mul(float3(pos.x, pos.y - 40101 * (2 * (_Time.y) + 5) * vortexControlsA.z, pos.z), rotator);

			if (distanceToVortexCenter > vortexPosRadius.w) {
				posVertex = pos;
			}

			cloudSample = tex3Dlod(_ShapeTexture, float4(posVertex * _Scale, lod)).r;

			if (posVertex.y < 15000 * vortexControlsA.w && distanceToVortexCenter > vortexPosRadius.w) { //posVertex.y < 20000 &&
				cloudSample = remap(cloudSample * pow(1.2 - heightFraction, 0.1), _LowFreqMinMax.x, _LowFreqMinMax.y, 0.0, 1.0) * (1 / abs(posVertex.y - 15000 * vortexControlsA.w));
			}
			else {
				if (posVertex.y < 15000 * vortexControlsA.w) {
					cloudSample = remap(cloudSample * pow(1.2 - heightFraction, 0.1), _LowFreqMinMax.x, _LowFreqMinMax.y, 0.0, 1.0);
				}
				else {
					cloudSample = remap(cloudSample * pow(1.2 - heightFraction, 0.1), _LowFreqMinMax.x, _LowFreqMinMax.y, 0.0, 1.0); // pick certain range from sample texture
				}
			}
		}
#else
		float cloudSample = tex3Dlod(_ShapeTexture, float4(pos * _Scale, lod)).r; // sample cloud shape texture
		cloudSample = remap(cloudSample * pow(1.2 - heightFraction, 0.1), _LowFreqMinMax.x, _LowFreqMinMax.y, 0.0, 1.0); // pick certain range from sample texture
#endif
		cloudSample *= getDensityHeightGradient(heightFraction, weatherData); // multiply cloud by its type gradient

		float cloudCoverage = weatherData.r;
		cloudSample = saturate(remap(cloudSample, saturate(heightFraction / cloudCoverage), 1.0, 0.0, 1.0)); // Change cloud coverage based by height and use remap to reduce clouds outside coverage
		cloudSample *= cloudCoverage; // multiply by cloud coverage to smooth them out, GPU Pro 7

#if defined(DEBUG_NO_HIGH_FREQ_NOISE)
		cloudSample = remap(cloudSample, 0.2, 1.0, 0.0, 1.0);
#else
		if (cloudSample > 0.0 && sampleDetail) // If cloud sample > 0 then erode it with detail noise
		{
#if defined(DEBUG_NO_CURL)
#else
			float3 curlNoise = mad(tex2Dlod(_CurlNoise, float4(p.xz * _CurlDistortScale, 0, 0)).rgb, 2.0, -1.0); // sample Curl noise and transform it from [0, 1] to [-1, 1]
			pos += float3(curlNoise.r, curlNoise.b, curlNoise.g) * heightFraction * _CurlDistortAmount; // distort position with curl noise
#endif
			float detailNoise = tex3Dlod(_DetailTexture, float4(pos * _DetailScale, lod)).r; // Sample detail noise

			float highFreqNoiseModifier = lerp(1.0 - detailNoise, detailNoise, saturate(heightFraction * 10.0)); // At lower cloud levels invert it to produce more wispy shapes and higher billowy

			cloudSample = remap(cloudSample, highFreqNoiseModifier * _HighFreqModifier, 1.0, 0.0, 1.0); // Erode cloud edges
		}
#endif

		return max(cloudSample * _SampleMultiplier, 0.0);
	}

	// GPU Pro 7
	float beerLaw(float density)
	{
		float d = -density * _Density;
		return max(exp(d), exp(d * 0.5)*0.7);
	}

	// GPU Pro 7
	float HenyeyGreensteinPhase(float cosAngle, float g)
	{
		float g2 = g * g;
		return ((1.0 - g2) / pow(1.0 + g2 - 2.0 * g * cosAngle, 1.5)) / 4.0 * 3.1415;
	}

	// GPU Pro 7
	float powderEffect(float density, float cosAngle)
	{
		float powder = 1.0 - exp(-density * 2.0);
		return lerp(1.0f, powder, saturate((-cosAngle * 0.5f) + 0.5f));
	}

	float calculateLightEnergy(float density, float cosAngle, float powderDensity) { // calculates direct light components and multiplies them together
		float beerPowder = 2.0 * beerLaw(density) * powderEffect(powderDensity, cosAngle);
		float HG = max(HenyeyGreensteinPhase(cosAngle, _HenyeyGreensteinGForward), HenyeyGreensteinPhase(cosAngle, _HenyeyGreensteinGBackward)) * 0.07 + 0.8;
		return beerPowder * HG;
	}

	float randSimple(float n) // simple hash function for more random light vectors
	{
		return mad(frac(sin(n) * 43758.5453123), 2.0, -1.0);
	}

	float3 rand3(float3 n) // random vector
	{
		return normalize(float3(randSimple(n.x), randSimple(n.y), randSimple(n.z)));
	}

	float3 sampleConeToLight(float3 pos, float3 lightDir, float cosAngle, float density, float3 initialWeather, float lod)
	{
#if defined(RANDOM_UNIT_SPHERE)
#else
		const float3 RandomUnitSphere[5] = // precalculated random vectors
		{
			{ -0.6, -0.8, -0.2 },
		{ 1.0, -0.3, 0.0 },
		{ -0.7, 0.0, 0.7 },
		{ -0.2, 0.6, -0.8 },
		{ 0.4, 0.3, 0.9 }
		};
#endif
		float heightFraction;
		float densityAlongCone = 0.0;
		const int steps = 5; // light cone step count
		float3 weatherData;
		for (int i = 0; i < steps; i++) {
			pos += lightDir * _LightStepLength; // march forward
#if defined(RANDOM_UNIT_SPHERE) // apply random vector to achive cone shape
			float3 randomOffset = rand3(pos) * _LightStepLength * _LightConeRadius * ((float)(i + 1));
#else
			float3 randomOffset = RandomUnitSphere[i] * _LightStepLength * _LightConeRadius * ((float)(i + 1));
#endif
			float3 p = pos + randomOffset; // light sample point
			// sample cloud
			heightFraction = getHeightFractionForPoint(p); 
			weatherData = sampleWeather(p);
			densityAlongCone += sampleCloudDensity(p, heightFraction, weatherData, lod + ((float)i) * 0.5, true) * weatherDensity(weatherData);
		}

#if defined(SLOW_LIGHTING) // if doing slow lighting then do more samples in straight line
		pos += 24.0 * _LightStepLength * lightDir;
		weatherData = sampleWeather(pos);
		heightFraction = getHeightFractionForPoint(pos);
		densityAlongCone += sampleCloudDensity(pos, heightFraction, weatherData, lod, true) * 2.0;
		int j = 0;
		while (1) {
			if (j > 22) {
				break;
			}
			pos += 4.25 * _LightStepLength * lightDir;
			weatherData = sampleWeather(pos);
			if (weatherData.r > 0.05) {
				heightFraction = getHeightFractionForPoint(pos);
				densityAlongCone += sampleCloudDensity(pos, heightFraction, weatherData, lod, true);
			}

			j++;
		}
#else
		pos += 32.0 * _LightStepLength * lightDir; // light sample from further away
		weatherData = sampleWeather(pos);
		heightFraction = getHeightFractionForPoint(pos);
		densityAlongCone += sampleCloudDensity(pos, heightFraction, weatherData, lod + 2, false) * weatherDensity(weatherData) * 3.0;
#endif
		
		return calculateLightEnergy(densityAlongCone, cosAngle, density) * _SunColor;
	}

	// raymarches clouds
	fixed4 raymarch(float3 ro, float3 rd, float steps, float3 depth, float cosAngle, float2 duv)
	{
		float3 pos = ro;
		fixed4 res = 0.0; // cloud color
		float lod = 0.0;
		float zeroCount = 0.0; // number of times cloud sample has been 0
		float stepLength = BIG_STEP; // step length multiplier, 1.0 when doing small steps


		for (float i = 0.0; i < steps; i += stepLength)
		{
			//if (distance(_CameraWS, pos) >= depth || res.a >= 0.99) { // check if is behind some geometrical object or that cloud color aplha is almost 1
			//if (distance(_CameraWS + _CameraWSOffset, pos) >= depth * 0.02 || res.a >= 0.99) { //v5.0.2
			if (distance(_CameraWS + _CameraWSOffset, pos) >= length(depth) || res.a >= 0.99) { //v5.2.1
				//break;  // if it is then raymarch ends

				//v0.1 - add option to rernder in front of all objects for reflections
				if (_renderInFront == 0) {
					break;
				}
			}
			float heightFraction = getHeightFractionForPoint(pos);
#if defined(ALLOW_IN_CLOUDS) // if it is allowed to fly in the clouds, then we need to check that the sample position is above the ground and in the cloud layer
			if (pos.y < _ZeroPoint.y || heightFraction < 0.0 || heightFraction > 1.0) {
				//break; //v0.1
			}
#endif
			float3 weatherData = sampleWeather(pos); // sample weather
			if (weatherData.r <= 0.1) // if value is low, then continue marching, at some specific weather textures makes it a bit faster.
			{
				pos += rd * stepLength;
				zeroCount += 1.0;
				stepLength = zeroCount > 10.0 ? BIG_STEP : 1.0;
				continue;
			}

			float cloudDensity = saturate(sampleCloudDensity(pos, heightFraction, weatherData, lod, true)); // sample the cloud

			if (cloudDensity > 0.0) // check if cloud density is > 0 //NASOS >=
			{

				//NASOS
		//		float adder1 = 0;
		//		if (cloudDensity == 0.0) {
					//cloudDensity = 0.004*(pow(dot(_SunDir, pos),2))*0.0004 * 0.00005;// *length(_SunDir - pos) * 0.00002;
					//float3 directLightA = sampleConeToLight(pos, _SunDir, cosAngle, cloudDensity, weatherData, lod);
		//			cloudDensity = saturate(0.004*(pow(dot(_SunDir, pos), 1))*0.0002 - 0.0000001*length(_SunDir - pos)) + 0.002;//0.004;

				//	float3 directLightA = sampleConeToLight(pos, _SunDir, cosAngle, cloudDensity, weatherData, lod);
					//cloudDensity =(( cloudDensity * directLightA.r*directLightA.g*directLightA.b))*0.4;
					//cloudDensity = (cloudDensity / (directLightA.r))*1.4;
					//cloudDensity = (cloudDensity *2/(directLightA.b + directLightA.r));

				//	cloudDensity = 0.008;

		//			zeroCount += 1.0;
				//	adder1 = 1;
					//break;
		//			break;
		//		}

				zeroCount = 0.0; // set zero cloud density counter to 0

				if (stepLength > 1.0) // if we did big steps before
				{
					i -= stepLength - 1.0; // then move back, previous 0 density location + one small step
					pos -= rd * (stepLength - 1.0);
					weatherData = sampleWeather(pos); // sample weather
					cloudDensity = saturate(sampleCloudDensity(pos, heightFraction, weatherData, lod, true)); // and cloud again
				}

				float4 particle = cloudDensity; // construct cloud particle
				float3 directLight = sampleConeToLight(pos, _SunDir, cosAngle, cloudDensity, weatherData, lod); // calculate direct light energy and color
				float3 ambientLight = lerp(_CloudBaseColor.rgb, _CloudTopColor, heightFraction); // and ambient

				directLight *= _SunLightFactor; // multiply them by their uniform factors


				//if (adder1 == 1) {
					//directLight = directLight * 0.45 + directLight / directLight.r*0.6;
					//particle = particle * directLight.r * directLight.g*2;
				//}


				ambientLight *= _AmbientLightFactor;

				//NASOS - Local Lights - _LocalLightColor
				//float divider = length(pos - float3(0, 3000+1010*cos(_Time.y*5), 0)); //v5.0.1
				float divider = length(pos - _LocalLightPos.xyz); //v5.0.1
				//float colorSample = 111111111;
				//float colorSample = pow(10, 8);
				float colorSample = pow(10,8) ;
				//particle.rgb = directLight + ambientLight + cloudDensity*float3(colorSample, 0, colorSample + pow(10, 8) * cos(_Time.y * 8)) / (pow(divider,2.5));// +10 / (abs(pos.xyz - float3(0, 0, 0))); // add lights up and set cloud particle color
				//v5.0.1
				particle.rgb = directLight + ambientLight + cloudDensity * _LocalLightColor.rgb * colorSample / (pow(divider, _LocalLightColor.w));// +10 / (abs(pos.xyz - float3(0, 0, 0))); // add lights up and set cloud particle color


				particle.rgb *= particle.a; // multiply color by clouds density
				res = (1.0 - res.a) * particle + res; // use premultiplied alpha blending to acumulate samples

				//NASOS
				//res = res + (dot(_SunDir, pos))*0.00001;

			}
			else // if cloud sample was 0, then increase zero cloud sample counter
			{
				//

				if (sunRaysOn == 1) {
					//NASOS
					//cloudDensity = saturate(0.004*(pow(dot(_SunDir, pos), 1))*0.0002 - 0.0000001*length(_SunDir - pos)) + 0.002;//0.004;
					//float3 directLight = sampleConeToLight(pos, _SunDir, cosAngle, cloudDensity, weatherData, lod);
					//res = float4(0.22, 0, 0, 0) + (dot(_SunDir,pos))*0.00001; //NASOS
					// res = float4(directLight,0) * 0.1;

					///////////ADD ALL FROM ABOVE
					//NASOS
					float adder1 = 0;
					//if (cloudDensity == 0.0) {
						//cloudDensity = 0.004*(pow(dot(_SunDir, pos),2))*0.0004 * 0.00005;// *length(_SunDir - pos) * 0.00002;
						//float3 directLightA = sampleConeToLight(pos, _SunDir, cosAngle, cloudDensity, weatherData, lod);
					//cloudDensity = saturate(0.004*(pow(dot(_SunDir, pos), 1))*0.0002 - 0.0000001*length(_SunDir - pos)) + 0.006;//0.004;
					cloudDensity = saturate(0.08*(pow(dot(_SunDir, pos), 1)) - 0.0001)*0.03 + 0.226;//0.004;
					//cloudDensity = cloudDensity * 0.5;
					//	float3 directLightA = sampleConeToLight(pos, _SunDir, cosAngle, cloudDensity, weatherData, lod);
					//cloudDensity =(( cloudDensity * directLightA.r*directLightA.g*directLightA.b))*0.4;
					//cloudDensity = (cloudDensity / (directLightA.r))*1.4;
					//cloudDensity = (cloudDensity *2/(directLightA.b + directLightA.r));

					//	cloudDensity = 0.008;

					//zeroCount += 1.0;
					//	adder1 = 1;
					//break;
					//break;
				//}

					//zeroCount = 0;
					zeroCount += zeroCountSteps;// 0.0; // set zero cloud density counter to 0

					//if (stepLength > 1.0) // if we did big steps before
					//{
					//	i -= stepLength - 1.0; // then move back, previous 0 density location + one small step
					//	pos -= rd * (stepLength - 1.0);
					//	weatherData = sampleWeather(pos); // sample weather
					//	cloudDensity = saturate(sampleCloudDensity(pos, heightFraction, weatherData, lod, true)); // and cloud again
					//}

					float4 particle = cloudDensity;// +0.000000005*depth; // construct cloud particle
					float3 directLight = 0;// sampleConeToLight(pos, _SunDir, cosAngle, cloudDensity, weatherData, lod); // calculate direct light energy and color
					//directLight = 1-sampleConeToLight(pos, _SunDir, cosAngle, cloudDensity, weatherData, lod);


					float3 posA = pos;
					float3 lightDir = _SunDir;
						//float cosAngle, 
					float density = cloudDensity;
					float3 initialWeather = weatherData;
					//	float lod
					//float3 sampleConeToLight(float3 pos, float3 lightDir, float cosAngle, float density, float3 initialWeather, float lod)
					//{
#if defined(RANDOM_UNIT_SPHERE)
#else
						const float3 RandomUnitSphere[5] = // precalculated random vectors
						{
							{ -0.6, -0.8, -0.2 },
						{ 1.0, -0.3, 0.0 },
						{ -0.7, 0.0, 0.7 },
						{ -0.2, 0.6, -0.8 },
						{ 0.4, 0.3, 0.9 }
						};
#endif
						float heightFraction;
						float densityAlongCone = -0.16;
						const int steps = sunShaftSteps;// 5; // light cone step count
						float3 weatherData;
						for (int i = 0; i < steps; i=i+1) {
							posA += (lightDir) * _LightStepLength * 95; // march forward
#if defined(RANDOM_UNIT_SPHERE) // apply random vector to achive cone shape
							float3 randomOffset = rand3(posA) * _LightStepLength * _LightConeRadius * ((float)(i + 1));
#else
							float3 randomOffset = RandomUnitSphere[i] * _LightStepLength * _LightConeRadius * ((float)(i + 1));
#endif
							float3 p = posA + randomOffset; // light sample point
														   // sample cloud
							heightFraction = getHeightFractionForPoint(p);
							weatherData = sampleWeather(p);
							densityAlongCone += 1111111*sampleCloudDensity(p, heightFraction, weatherData, 0, true) ;
						}

//#if defined(SLOW_LIGHTING) // if doing slow lighting then do more samples in straight line
//						posA += 24.0 * _LightStepLength * lightDir;
//						weatherData = sampleWeather(posA);
//						heightFraction = getHeightFractionForPoint(posA);
//						densityAlongCone += sampleCloudDensity(posA, heightFraction, weatherData, lod, true) * 2.0;
//						int j = 0;
//						while (1) {
//							if (j > 22) {
//								break;
//							}
//							posA += 4.25 * _LightStepLength * lightDir;
//							weatherData = sampleWeather(posA);
//							if (weatherData.r > 0.05) {
//								heightFraction = getHeightFractionForPoint(posA);
//								densityAlongCone += sampleCloudDensity(posA, heightFraction, weatherData, lod, true);
//							}
//
//							j++;
//						}
//#else
						posA += 32.0 * _LightStepLength * lightDir; // light sample from further away
						weatherData = sampleWeather(posA);
						heightFraction = getHeightFractionForPoint(posA);
						densityAlongCone += sampleCloudDensity(posA, heightFraction, weatherData, lod + 2, false) * weatherDensity(weatherData) * 3.0;
//#endif

						float densityA = densityAlongCone;
						//float cosAngle
						float powderDensity = density;

						//float calculateLightEnergy(float density, float cosAngle, float powderDensity) { // calculates direct light components and multiplies them together
							float beerPowder = 1.2 * beerLaw(densityA) * powderEffect(powderDensity, cosAngle);
							float HG = max(HenyeyGreensteinPhase(cosAngle, _HenyeyGreensteinGForward), HenyeyGreensteinPhase(cosAngle, _HenyeyGreensteinGBackward)) * 0.07 + 0.8;
							//return beerPowder * HG;
						//}
							
						directLight = (beerPowder) * HG * _SunColor;
						//directLight = (1) * HG * _SunColor * 2;

						//return calculateLightEnergy(densityAlongCone, cosAngle, density) * _SunColor;
					//}


					//////////////END


					//float3 ambientLight = lerp(_CloudBaseColor, _CloudTopColor, heightFraction); // and ambient

					//directLight *= _SunLightFactor; // multiply them by their uniform factors

					//if (adder1 == 1) {
					//directLight = directLight * 0.45 + directLight / directLight.r*0.6;
					//particle = particle * directLight.r * directLight.g*2;
					//}

					//ambientLight *= _AmbientLightFactor;

					//NASOS
					//float divider = length(pos - float3(0, 3000, 0));
					//float colorSample = 111111111;
					//float colorSample = pow(10, 8);
					particle.rgb = 0.5*directLight + cloudDensity;// *float3(colorSample, 0, colorSample) / (pow(divider, 2.5));// +10 / (abs(pos.xyz - float3(0, 0, 0))); // add lights up and set cloud particle color

					particle.rgb *= particle.a; // multiply color by clouds density

					//getRandomRayOffset((duv + _Randomness.xy) * _ScreenParams.xy * _BlueNoise_TexelSize.xy)
					//float rand = getRandomRayOffset((duv + _Randomness.xy) * _ScreenParams.xy * _BlueNoise_TexelSize.xy);
				//	res = (1.0 - res.a) * particle + res; // use premultiplied alpha blending to acumulate samples

					//res = res + 0.001*cos(_Time.x*duv.x*30) + 0.001*cos(_Time.x*duv.y * 130);
					//res = res + length(pos - _SunDir) * 0.00000002 * directLight.r* directLight.g* directLight.b;
				//	res = res + 0.0005 * directLight.r* directLight.g* directLight.b;

					//(1.0 - res.a) * particle
					//res = (1.0 - res.a) * 0.0005 * directLight.r* directLight.g* directLight.b * float4(1.5, 0.7, 0.5, 1)*11 + res ;

					res = (1.0 - res.a) * 0.0008 * 1 * 1 * directLight.b * float4(1.5, 0.7, 0.5, 1) * 11 + res;

				}
				else {
					zeroCount += 1.0;
				}

				//NASOS
				//res = res + (dot(_SunDir, pos))*0.00001;
			}
			stepLength = zeroCount > 10.0 ? BIG_STEP : 1.0; // check if we need to do big or small steps

			pos += rd * stepLength; // march forward
		}

		//v5.0.2
		res.a = res.a + 10 * _CloudBaseColor.a*res.a;

		return res;
	}

	// https://www.scratchapixel.com/lessons/3d-basic-rendering/minimal-ray-tracer-rendering-simple-shapes/ray-sphere-intersection
	float3 findRayStartPos(float3 rayOrigin, float3 rayDirection, float3 sphereCenter, float radius)
	{
		float3 l = rayOrigin - sphereCenter;
		float a = 1.0;
		float b = 2.0 * dot(rayDirection, l);
		float c = dot(l, l) - pow(radius, 2);
		float D = pow(b, 2) - 4.0 * a * c;
		if (D < 0.0)
		{
			return rayOrigin;
		}
		else if (abs(D) - 0.00005 <= 0.0)
		{
			return rayOrigin + rayDirection * (-0.5 * b / a);
		}
		else
		{
			float q = 0.0;
			if (b > 0.0)
			{
				q = -0.5 * (b + sqrt(D));
			}
			else
			{
				q = -0.5 * (b - sqrt(D));
			}
			float h1 = q / a;
			float h2 = c / q;
			float2 t = float2(min(h1, h2), max(h1, h2));
			if (t.x < 0.0) {
				t.x = t.y;
				if (t.x < 0.0) {
					return rayOrigin;
				}
			}
			return rayOrigin + t.x * rayDirection;
		}
	}

	

	fixed4 altoClouds(float3 ro, float3 rd, float depth, float cosAngle) { // samples high altitude clouds
		fixed4 res = 0.0;
		float3 pos = findRayStartPos(ro, rd, _PlanetCenter, _SphereSize + _CloudHeightMinMax.y + 3000.0); // finds sample position
		float dist = distance(ro, pos);
		if (dist < depth && pos.y > _ZeroPoint.y && dist > 0.0) { // chekcs for depth texture, above ground 

			float alto = tex2Dlod(_AltoClouds, float4((pos.xz + _HighCloudsWindOffset) * _HighCloudsScale, 0, 0)).r * 2.0; // samples high altitude cloud texture

			float coverage = tex2Dlod(_WeatherTexture, float4((pos.xz + _HighCloudsWindOffset) * _CoverageHighScale, 0, 0)).r; // same as with volumetric clouds
			coverage = saturate(coverage - _CoverageHigh);

			alto = remap(alto, 1.0 - coverage, 1.0, 0.0, 1.0);
			alto *= coverage;
			float3 directLight = max(HenyeyGreensteinPhase(cosAngle, _HenyeyGreensteinGForward), HenyeyGreensteinPhase(cosAngle, _HenyeyGreensteinGBackward)) * _SunColor; // for high altitude clouds uses HG phase
			directLight *= _SunLightFactor * 0.2;
			float3 ambientLight = _CloudTopColor * _AmbientLightFactor * 1.5; // ambient light is the high cloud layer ambient color
			float4 aLparticle = float4(min(ambientLight + directLight, 0.7), alto);

			aLparticle.rgb *= aLparticle.a;

			res = aLparticle;
		}

		return saturate(res);
	}



	// samples cloud density
	float sampleCloudDensity1(float3 p, float heightFraction, float3 weatherData, float lod, bool sampleDetail)
	{
		float3 pos = p;

		float cloudSample = tex3Dlod(_ShapeTexture, float4(pos * 0.00001, 0)).r; // sample cloud shape texture
																				 //cloudSample = remap(cloudSample * pow(1.2 - heightFraction, 0.1), _LowFreqMinMax.x, _LowFreqMinMax.y, 0.0, 1.0); // pick certain range from sample texture

		float cloudCoverage = 1;// weatherData.r;
		cloudSample = saturate(remap(cloudSample, (heightFraction / cloudCoverage), 1.0, 0.0, 1.0)); // Change cloud coverage based by height and use remap to reduce clouds outside coverage


		return cloudSample;
	}

	// raymarches clouds
	float4 raymarch1(float3 ro, float3 rd, float steps, float depth, float cosAngle, float2 duv)
	{
		float3 pos = ro;
		float4 res = 0.0; // cloud color
		float lod = 0.0;
		float zeroCount = 0.0; // number of times cloud sample has been 0
		float stepLength = 2; // step length multiplier, 1.0 when doing small steps

		for (float i = 0.0; i < 151; i += stepLength)
		{
			float heightFraction = 0.7;// getHeightFractionForPoint(pos);
			float3 weatherData = float3(1, 1, 1);// sampleWeather(pos); // sample weather			
			float cloudDensity = (sampleCloudDensity1(pos, heightFraction, weatherData, lod, true)); // sample the cloud

			if (cloudDensity >= 0.0)
			{
				zeroCount = 0.0; // set zero cloud density counter to 0
				float4 particle = cloudDensity; // construct cloud particle							
				res = particle + res; // use premultiplied alpha blending to acumulate samples	
			}
			//stepLength = zeroCount > 10.0 ? BIG_STEP : 1.0; // check if we need to do big or small steps
			pos += rd * stepLength; // march forward
		}
		//return  float4(ro, 1);
		return res;
	}

	// from GPU Pro 7 - remaps value from one range to other range //v5.2.1
	/*float remap(float original_value, float original_min, float original_max, float new_min, float new_max)
	{
		return new_min + (((original_value - original_min) / (original_max - original_min)) * (new_max - new_min));
	}*/

	fixed4 frag(v2f i) : SV_Target
	{
		//v5.2.2 - VR
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); //Insert
		float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, UnityStereoTransformScreenSpaceTex(i.uv_depth));
		float depth = Linear01Depth(rawDepth);
		//v5.0.2
		//pick one of the passed in projection/view matrices based on stereo eye selection (always left if not vr)
		float4x4 proj, eyeToWorld;
		if (unity_StereoEyeIndex == 0)
		{
			proj = _LeftViewFromScreen;
			eyeToWorld = _LeftWorldFromView;
		}
		else
		{
			proj = _RightViewFromScreen;
			eyeToWorld = _RightWorldFromView;
		}
		//bit of matrix math to take the screen space coord (u,v,depth) and transform to world space
		float2 uvClip = i.uv * 2.0 - 1.0;
		float4 clipPos = float4(uvClip, rawDepth, 1.0);
		float4 viewPos = mul(proj, clipPos); // inverse projection by clip position
		viewPos /= viewPos.w; // perspective division
		float3 wsPos = mul(eyeToWorld, viewPos).xyz; // FIXED !!!! for STEREO VR

		//v5.2.4
		if (_renderInFront) { //REFLECTIONS
			wsPos.y = -wsPos.y;
		}

		float3 ray = (wsPos - _WorldSpaceCameraPos) / length(wsPos - _WorldSpaceCameraPos);
		//float4 wsDir = specialFX.x * float4(ray, 1);// dpth * i.interpolatedRay; //dpth * i.interpolatedRay;
		//float4 wsDir = depth * float4(ray, 1);



		// ray origin (camera position)
		float3 ro = _CameraWS + _CameraWSOffset;
		// ray direction
		float3 rd = normalize(ray.xyz);//normalize(i.ray.xyz); //v5.2.2

		float2 duv = i.uv;
#if UNITY_UV_STARTS_AT_TOP
		if (_MainTex_TexelSize.y < 0)
			duv.y = 1 - duv.y;
#endif
		float3 rs;
		float3 re;

#if defined(DEBUG_NO_LOW_FREQ_NOISE) //VORTEX
		//v0.7
		//float4 vortexPosRadius = float4(0, 0, 0, 1100);
		float distanceToVortexCenter = length(vortexPosRadius.xz - wsPos.xz);
		float3 thick = float3(0, 0, 0);
		if (distanceToVortexCenter < vortexPosRadius.w * 0.93) { //if (distanceToVortexCenter < 280000 * 0.93) { //v0.8
			//pos.y = pos.y + 1000*distanceToVortexCenter;
			thick = float3(0, 80000, 0);
			//ro = ro + thick.y;
			//rd = rd + thick.y;
			//_CloudHeightMinMax.x = _CloudHeightMinMax.x + thick.y;
			_CloudHeightMinMax.y = _CloudHeightMinMax.y + thick.y;
		}
#endif

		float steps;
		float stepSize;
		// Ray start pos
#if defined(ALLOW_IN_CLOUDS) // if in cloud flying is allowed, then figure out if camera is below, above or in the cloud layer and set 
		// starting and end point accordingly.
		bool aboveClouds = false;
		float distanceCameraPlanet = distance(_CameraWS + _CameraWSOffset, _PlanetCenter);
		if (distanceCameraPlanet < _SphereSize + _CloudHeightMinMax.x-15000) // Below clouds, v0.1 subtract 21000
		{
			rs = findRayStartPos(ro, rd, _PlanetCenter, _SphereSize + _CloudHeightMinMax.x);
			if (rs.y < _ZeroPoint.y) // If ray starting position is below horizon
			{
				return 0.0;// v0.1
			}
			re = findRayStartPos(ro, rd, _PlanetCenter, _SphereSize + _CloudHeightMinMax.y);
			steps = lerp(_Steps, _Steps * 0.5, rd.y);
			stepSize = (distance(re, rs)) / steps;
		}
		else if (distanceCameraPlanet > _SphereSize + _CloudHeightMinMax.y) // Above clouds
		{
			rs = findRayStartPos(ro, rd, _PlanetCenter, _SphereSize + _CloudHeightMinMax.y);
			re = rs + rd * _FarPlane;
			steps = lerp(_Steps, _Steps * 0.5, rd.y);
			stepSize = (distance(re, rs)) / steps;
			aboveClouds = true;
		}
		else // In clouds
		{
			rs = ro;
			re = rs + rd * _FarPlane;

			steps = lerp(_Steps, _Steps * 0.5, rd.y);
			stepSize = (distance(re, rs)) / steps;
		}

#else
		rs = findRayStartPos(ro, rd, _PlanetCenter, _SphereSize + _CloudHeightMinMax.x);
		if (rs.y < _ZeroPoint.y) // If ray starting position is below horizon
		{
			return 0.0;
		}
		re = findRayStartPos(ro, rd, _PlanetCenter, _SphereSize + _CloudHeightMinMax.y);
		steps = lerp(_Steps, _Steps * 0.5, rd.y);
		stepSize = (distance(re, rs)) / steps;
#endif

		// Ray end pos


#if defined(RANDOM_JITTER_WHITE)
		rs += rd * stepSize * rand(_Time.zw + duv) * BIG_STEP * 0.75;
#endif
#if defined(RANDOM_JITTER_BLUE)
		rs += rd * stepSize * BIG_STEP * 0.75 * getRandomRayOffset((duv + _Randomness.xy) * _ScreenParams.xy * _BlueNoise_TexelSize.xy);
#endif

		// Convert from depth buffer (eye space) to true distance from camera
		// This is done by multiplying the eyespace depth by the length of the "z-normalized"
		// ray (see vert()).  Think of similar triangles: the view-space z-distance between a point
		// and the camera is proportional to the absolute distance.
		//float depth = Linear01Depth(tex2D(_CameraDepthTexture, duv).r); //v5.2.2

		//v0.6
		//float depthDilation = 450;
		if (depthDilation != 0) {
			float depthOffset = 0.00001 * depthDilation;
			float zsampleA1 = tex2D(_CameraDepthTexture, duv.xy + float2(depthOffset, depthOffset));
			float depthA1 = Linear01Depth(zsampleA1 * (zsampleA1 < 1.0));
			float zsampleA2 = tex2D(_CameraDepthTexture, duv.xy + float2(-depthOffset, -depthOffset));
			float depthA2 = Linear01Depth(zsampleA2 * (zsampleA2 < 1.0));
			float zsampleA3 = tex2D(_CameraDepthTexture, duv.xy + float2(-depthOffset, depthOffset));
			float depthA3 = Linear01Depth(zsampleA3 * (zsampleA3 < 1.0));
			float zsampleA4 = tex2D(_CameraDepthTexture, duv.xy + float2(depthOffset, -depthOffset));
			float depthA4 = Linear01Depth(zsampleA4 * (zsampleA4 < 1.0));
			depth = (depth + depthA1 + depthA2 + depthA3 + depthA4) / 5;
		}

		if (depth == 1.0) {
			depth = 100.0;
		}
		depth *= _FarPlane *1; //v0.9 - v5.2.1 //depth *= _FarPlane *100;
		float cosAngle = dot(rd, _SunDir);

		//v0.3
		///////////// SCATTER
		float3 _TintColor = _SkyTint;// float4(1, 1, 1, 1);
		// towards this screen pixel.
		//float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv_depth);
		//float dpth = Linear01Depth(rawDepth);
		float4 wsDir = depth*i.ray;// *i.interpolatedRay;
	//	float4 wsPos = _CameraWS + wsDir + _CameraWSOffset; //v5.2.2

		//SM v1.7
		float3 lightDirection = _SunDir;// v3LightDir;// _WorldSpaceLightPos0.xyz;  
		float  cosTheta = dot(normalize(wsDir), lightDirection);

		float3 up = float3(0.0, 0.0, 1.0);
		float3 lambda = float3(680E-8, 550E-8, 450E-8);
		float3 K = float3(0.686, 0.678, 0.666);
		float  rayleighZenithLength = 8.4E3;
		float  mieZenithLength = 1.25E3;
		//float  mieCoefficient = 0.054;
		float  pi = 3.141592653589793238462643383279502884197169;
		float3 betaR = totalRayleigh(lambda) * reileigh * 1000;
		float3 lambda1 = float3(_TintColor.r, _TintColor.g, _TintColor.b)*0.0000001;//  680E-8, 1550E-8, 3450E-8);
		lambda = lambda1;
		float3 betaM = totalMie(lambda1, K, turbidity * Multiplier2) * mieCoefficient;
		float zenithAngle = acos(max(0.0, dot(up, normalize(lightDirection))));
		float sR = rayleighZenithLength / (cos(zenithAngle) + 0.15 * pow(93.885 - ((zenithAngle * 180.0) / pi), -1.253));
		float sM = mieZenithLength / (cos(zenithAngle) + 0.15 * pow(93.885 - ((zenithAngle * 180.0) / pi), -1.253));
		float  rPhase = rayleighPhase(cosTheta*0.5 + 0.5);
		float3 betaRTheta = betaR * rPhase;
		float  mPhase = hgPhase(cosTheta, mieDirectionalG) * Multiplier1;
		float3 betaMTheta = betaM * mPhase;
		float3 Fex = exp(-(betaR * sR + betaM * sM));
		float  sunE = sunIntensity(dot(lightDirection, up));
		float3 Lin = ((betaRTheta + betaMTheta) / (betaR + betaM)) * (1 - Fex) + sunE * Multiplier3*0.0001;
		float  sunsize = 0.0001;
		float3 L0 = 1.5 * Fex + (sunE * 1.0 * Fex)*sunsize;
		float3 FragColor = tonemap(Lin + L0);

		// Compute fog distance
		float g = _DistanceParams.x;
		//if (distance)
		//	g += ComputeDistance(wsDir, depth);
		//if (height)
			g += ComputeHalfSpace(wsDir); //v4.0

										  // Compute fog amount
		half fogFac = ComputeFogFactor(max(0.0, g));//*1.5;
													// Do not fog skybox
													//if (rawDepth >= 0.999999){
		if (depth >0){// rawDepth >= 0.999995) {
			if (1==0){//FogSky <= 0) {
				fogFac = 1.0;
			}
			else {
				//if (distance) {
				//	fogFac = fogFac * ClearSkyFac;
				//}
			}
		}
		//return fogFac; // for debugging
		//half4 sceneColor = tex2D(_MainTex, i.uv);
		//float4 Final_fog_color = lerp(unity_FogColor + float4(FragColor, 1), sceneColor, fogFac);
		
		//fogFac = float4(fogFac *FragColor.xyz*1,1);
		//float4 fogFac2 = float4(0.5*fogFac +0.5*FragColor.xyz * 1, 1);
		float4 fogFac2 = float4(1*FragColor.xyz * 1, 1);

		//fogFac = Final_fog_color;
		//////////// END SCATTER


		fixed4 clouds2D =  altoClouds(ro, rd, depth, cosAngle); // sample high altitude clouds
		//fixed4 clouds3D =  raymarch(rs, rd * stepSize, steps, depth, cosAngle, duv) * 1;// fogFac; // raymarch volumetric clouds
		float4 clouds3D =  raymarch(rs, rd * stepSize, steps, wsDir, cosAngle, duv);//v5.2.1

		if (scatterOn == 1) {
			clouds3D = float4(clouds3D.rgb*fogFac2.rgb, clouds3D.a);
		}


		//v0.9a - v5.2.1
//SELF SHADOW !!!	
		if (specialFX.y != 0) {
			float checkVar = -0.25 * specialFX.y;
#if defined(ALLOW_IN_CLOUDS) 
			if (aboveClouds) {
				checkVar = -0.25* specialFX.y;
			}
#endif
			float dotSun = dot(_WorldSpaceLightPos0, normalize(rs));// _SunDir); 
			if (dotSun < checkVar)
			{
				clouds3D.rgb = 0;
			}
			else
			{
				clouds3D.rgb *= remap(dotSun, checkVar, 1, 0, 2.4*(1 + specialFX.z));
			}
		}



#if defined(ALLOW_IN_CLOUDS)
		if (aboveClouds) // use premultiplied alpha blending to combine low and high clouds
		{
			
			return clouds3D * (1.0 - clouds2D.a) + clouds2D;
		}
		else
		{
			
			return clouds2D * (1.0 - clouds3D.a) + clouds3D;
		}

#else
		return clouds2D * (1.0 - clouds3D.a) + clouds3D;
#endif
	}
		ENDCG
	}
	}
}