// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Hidden/FullVolumeCloudsSkyMaster" {

//v3.5.3 latest version

Properties {
	_MainTex ("Base (RGB)", any) = "black" {} //v5.0.2 fix for VR
	_CloudTex ("Base (RGB)", any) = "black" {}
	_CloudTexP("Base (RGB)", any) = "black" {}
	_SkyTex ("Base (RGB)", any) = "black" {}//v2.1.15

	_ColorRamp ("Colour Palette", 2D) = "gray" {}
	_Close ("Close", float) = 0.0 
	_Far ("Far", float) = 0.0 
	v3LightDir("v3LightDir", Vector) = (0,0,0)
	FogSky("FogSky",float) = 0.0
	_TintColor("Color Tint", Color) = (0,0,0,0)
	ClearSkyFac("Clear Sky Factor",float) = 1.0



	//v3.5 clouds
	_SampleCount0("Sample Count (min)", Float) = 30
        _SampleCount1("Sample Count (max)", Float) = 90
        _SampleCountL("Sample Count (light)", Int) = 16

        [Space]
        _NoiseTex1("Noise Volume", 3D) = ""{}
        _NoiseTex2("Noise Volume", 3D) = ""{}
        _NoiseFreq1("Frequency 1", Float) = 3.1
        _NoiseFreq2("Frequency 2", Float) = 35.1
        _NoiseAmp1("Amplitude 1", Float) = 5
        _NoiseAmp2("Amplitude 2", Float) = 1
        _NoiseBias("Bias", Float) = -0.2

        [Space]
        _Scroll1("Scroll Speed 1", Vector) = (0.01, 0.08, 0.06, 0)
        _Scroll2("Scroll Speed 2", Vector) = (0.01, 0.05, 0.03, 0)

        [Space]
        _Altitude0("Altitude (bottom)", Float) = 1500
        _Altitude1("Altitude (top)", Float) = 3500
        _FarDist("Far Distance", Float) = 30000

        [Space]
        _Scatter("Scattering Coeff", Float) = 0.008
        _HGCoeff("Henyey-Greenstein", Float) = 0.5
        _Extinct("Extinction Coeff", Float) = 0.01

        [Space]
        _SunSize ("Sun Size", Range(0,1)) = 0.04
        _AtmosphereThickness ("Atmoshpere Thickness", Range(0,5)) = 1.0
        _SkyTint ("Sky Tint", Color) = (.5, .5, .5, 1)
        _GroundColor ("Ground", Color) = (.369, .349, .341, 1)
        _Exposure("Exposure", Float) = 3

	     //v3.5 clouds
	     _BackShade ("Back shade of cloud top", Float) = 1
	     _UndersideCurveFactor ("Underside Curve Factor", Float) = 0

	     //v3.5.1
	     _NearZCutoff ("Away from camera Cutoff", Float) = -2
	     _HorizonYAdjust ("Adjust horizon Height", Float) = 0
	     _FadeThreshold ("Fade Near", Float) = 0

	     //v3.5.3
	     _InteractTexture ("_Interact Texture", 2D) = "white" {}
		 _InteractTexturePos("Interact Texture Pos", Vector) = (1 ,1, 0, 0)
		 _InteractTextureAtr("Interact Texture Attributes - 2multi 2offset", Vector) = (1 ,1, 0, 0)
		 _InteractTextureOffset("Interact Texture offsets", Vector) = (0 ,0, 0, 0) //v4.0

			 //v2.1.19
			 _fastest("Fastest mode", Int) = 0
			 _LocalLightPos("Local Light Pos & Intensity", Vector) = (0 ,0, 0, 0) //local light position (x,y,z) and intensity (w)			 
			 _LocalLightColor("Local Light Color & Falloff", Vector) = (0 , 0, 0, 2) //w = _LocalLightFalloff

			 //v2.1.24
			 _HorizonZAdjust("Adjust cloud depth", Float) = 1

			 //v4.1f
			 _mobileFactor("Adjust to 0 to fix Android lighting", Float) = 1
			 _alphaFactor("Adjust to 0 to fix Android lighting", Float) = 1

			 _invertX("Mirror X", Float) = 0
			 _invertRay("Mirror Ray", Float) = 1

			 _WorldSpaceCameraPosC ("Camera", Vector) = (0 , 0, 0, 1)
			 //v4.8
			 varianceAltitude1("varianceAltitude1", Float) = 0

			 //v4.8.6
			 turbidity("turbidity", Float) = 2

			 ////v4.9.4
			 _cameraFarDistanceAdjust("adjust camera far view distance", Float) = 30000
}

CGINCLUDE

//#define UNITY_SINGLE_PASS_STEREO

#include "UnityCG.cginc"
#include "Lighting.cginc"
// #include "AutoLight.cginc"

	//v5.0.2
	//view/projection matrices proved by VolumetricSphere.cs (in none vr, only left eye is used)
	//https://github.com/chriscummings100/worldspaceposteffect/blob/master/Assets/WorldSpacePostEffect/WorldSpacePostEffect.shader
	float4x4 _LeftWorldFromView;
	float4x4 _RightWorldFromView;
	float4x4 _LeftViewFromScreen;
	float4x4 _RightViewFromScreen;

	//v4.8
	float _invertX=0;
	float _invertRay = 1;
	float3 _WorldSpaceCameraPosC;
	float varianceAltitude1 = 0;

	//v4.1f
	float _mobileFactor;
	float _alphaFactor;

	//v3.5.3
    sampler2D _InteractTexture;
    float4 _InteractTexturePos;
    float4 _InteractTextureAtr;
    float4 _InteractTextureOffset; //v4.0

	//v3.5.1
	float _NearZCutoff;
	float _HorizonYAdjust;
	float _HorizonZAdjust;
	float _FadeThreshold;

	//v3.5 clouds
	//#include "ProceduralSkySM.cginc" //changed
	float _BackShade;
	float _UndersideCurveFactor;

	//VFOG
	float4x4 _WorldClip;

	float _SampleCount0=2;
    float _SampleCount1=3;
    int _SampleCountL=4;

    sampler3D _NoiseTex1;
    sampler3D _NoiseTex2;
    float _NoiseFreq1=3.1;
    float _NoiseFreq2=35.1;
    float _NoiseAmp1=5;
    float _NoiseAmp2=1;
    float _NoiseBias=-0.2;

    float3 _Scroll1 = float3 (0.01, 0.08, 0.06);
    float3 _Scroll2 = float3 (0.01, 0.05, 0.03);

    float _Altitude0 = 1500;
    float _Altitude1 = 3500;
    float _FarDist = 30000;

    float _Scatter = 0.008;
    float _HGCoeff = 0.5;
    float _Extinct = 0.01;

    float3 _SkyTint;
    float _SunSize;
    float3 _GroundColor; //v4.0
    float _Exposure; //v4.0

    //v3.5 clouds
    //v3.5.2 clouds
    uniform float4 _CloudTex_TexelSize;
	uniform float4 _CloudTexP_TexelSize;//v4.8.2

	//uniform sampler2D _MainTex;
	//uniform sampler2D _CloudTex;
	//uniform sampler2D _CloudTexP;
	float frameFraction = 0;
	//uniform sampler2D _SkyTex;//v2.1.15

	//v5.0.2 - VR
	UNITY_DECLARE_SCREENSPACE_TEXTURE(_MainTex);
	UNITY_DECLARE_SCREENSPACE_TEXTURE(_CloudTex);
	UNITY_DECLARE_SCREENSPACE_TEXTURE(_CloudTexP);
	UNITY_DECLARE_SCREENSPACE_TEXTURE(_SkyTex);
	//UnityStereoScreenSpaceUVAdjust(i.uv, _MainTex_ST
	/*half4 _MainTex_ST;
	half4 _CloudTex_ST;
	half4 _CloudTexP_ST;
	half4 _SkyTex_ST;*/

	//uniform sampler2D_float _CameraDepthTexture;
	//https://answers.unity.com/questions/1529224/opengles3-fails-to-compile-shaders-hiddenpost-fx-f.html
	//https://docs.unity3d.com/Manual/SL-PlatformDifferences.html
	//    uniform sampler2D_float _CameraDepthTexture;
	//UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
	UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture); //v4.9.4
	
	//SM v1.7
	uniform sampler2D _ColorRamp;
	uniform float _Close;
	uniform float _Far;
	uniform float3 v3LightDir;		// light source
	uniform float FogSky;	
	fixed4 _TintColor; //float3(680E-8, 1550E-8, 3450E-8);
	uniform float ClearSkyFac;
	// x = fog height
	// y = FdotC (CameraY-FogHeight)
	// z = k (FdotC > 0.0)
	// w = a/2
	uniform float4 _HeightParams;
	
	// x = start distance
	uniform float4 _DistanceParams;
	
	int4 _SceneFogMode; // x = fog mode, y = use radial flag
	float4 _SceneFogParams;
	#ifndef UNITY_APPLY_FOG
	half4 unity_FogColor;
	half4 unity_FogDensity;
	#endif	

	uniform float4 _MainTex_TexelSize;
	
	// for fast world space reconstruction
	uniform float4x4 _FrustumCornersWS;
	uniform float4 _CameraWS;
	
	//SM v1.7
	uniform float luminance, Multiplier1, Multiplier2,Multiplier3,bias, lumFac, contrast, turbidity;
	//uniform float mieDirectionalG = 0.7,0.913; 
	float mieDirectionalG;
	float mieCoefficient;//0.054
	float reileigh;
	
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
	float cutoffAngle = 3.141592653589793238462643383279502884197169/1.95;
	float steepness = 1.5;



	//v2.1.19
	int _fastest;
	float4 _LocalLightPos;
	float4 _LocalLightColor;

	//v4.9.4
	float _cameraFarDistanceAdjust;

	//v3.5 clouds
//	struct appdata_t
//    {
//        float4 vertex : POSITION;
//    };

//    struct v2f
//    {
//        float4 vertex : SV_POSITION;
//        float2 uv : TEXCOORD0;
//        float3 rayDir : TEXCOORD1;
//        float3 groundColor : TEXCOORD2;
//        float3 skyColor : TEXCOORD3;
//        float3 sunColor : TEXCOORD4;
//    };

   
//    v2f vert(appdata_t v)
//    {
//        float4 p = mul(UNITY_MATRIX_MVP, v.vertex);
//
//        v2f o;
//
//        o.vertex = p;
//        o.uv = (p.xy / p.w + 1) * 0.5;
//
//        vert_sky(v.vertex.xyz, o);
//
//        return o;
//    }
    //v3.5 clouds

	//v5.0.2
	//https://docs.unity3d.com/Manual/SinglePassInstancing.html
	struct appdata
	{
		float4 vertex : POSITION;
		float2 texcoord : TEXCOORD0;		

		UNITY_VERTEX_INPUT_INSTANCE_ID //Insert
	};


	struct v2f {
		float4 vertex : SV_POSITION;
		float2 uv : TEXCOORD0;
		float2 uv_depth : TEXCOORD1;
		float4 interpolatedRay : TEXCOORD2;

		//v3.5 clouds
		//float3 rayDir : TEXCOORD3;

       // float3 groundColor : TEXCOORD4;
       // float3 skyColor : TEXCOORD5;
       // float3 sunColor : TEXCOORD6;

       //VFOG
       //PERSPECTIVE PROJECTION
       float3 FarCam : TEXCOORD3;
       //ALL PROJECTIONS
      // float4 worldMul : TEXCOORD4;
      // float3 ViewDir : TEXCOORD5;
       //float4 worldAdd : TEXCOORD6;//
        //v3.5 clouds

		//v5.0.2
		UNITY_VERTEX_INPUT_INSTANCE_ID
		UNITY_VERTEX_OUTPUT_STEREO

	};

	//v3.5
	//#include "ProceduralSkySM.cginc" //changed, requires v2f above
	
	v2f vert (appdata v)//appdata_img v)
	{

		//5.0.2
		UNITY_SETUP_INSTANCE_ID(v);

		v2f o;

		//v5.0.2
		UNITY_INITIALIZE_OUTPUT(v2f, o);
		UNITY_TRANSFER_INSTANCE_ID(v, o);
		UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);


		half index = v.vertex.z;

		v.vertex.z = 0.1;
		o.vertex = UnityObjectToClipPos(v.vertex);
		o.uv = v.texcoord.xy;

		//v3.5
		//VFOG
		//o.uv_depth = v.texcoord.xy;
		o.uv_depth =MultiplyUV(UNITY_MATRIX_TEXTURE0, v.texcoord);

		#if UNITY_UV_STARTS_AT_TOP
		if(_MainTex_TexelSize.y < 0){
			o.uv_depth.y = 1-o.uv_depth.y;
		}
		#endif	
		float2 XY = o.vertex.xy / o.vertex.w * _HorizonZAdjust; //v2.1.24

		//PERSPECTIVE PROJECTION
			float4 farClip = float4(XY,1,1);
			float4 farWorld = mul(_WorldClip, farClip);
			//float3 farWorldScaled = farWorld.xyz/farWorld.w;
			float3 farWorldScaled = farWorld.xyz / farWorld.w * (30000 / _cameraFarDistanceAdjust); //v4.9.4
			
			o.FarCam = farWorldScaled - _WorldSpaceCameraPosC;//-_CameraWS

			//ALL PROJECTIONS
//			float4 nearClip = float4(XY,-1,1);
//			float4 farClip = float4(XY,1,1);
//			float4 nearWorld = mul(_WorldClip, nearClip);
//			float4 farWorld = mul(_WorldClip, farClip);
//			o.worldMul = farWorld - nearWorld;
//			o.worldAdd = nearWorld;


		//o.ViewDir = normalize(WorldSpaceViewDir(v.vertex));
		//v3.5


		
		#if UNITY_UV_STARTS_AT_TOP
		if (_MainTex_TexelSize.y < 0){
			#if defined(UNITY_REVERSED_Z)
				o.uv.y = 1-o.uv.y;; //1-TerrainDepth; //v2.1.19			
			#endif
			//o.uv.y = 1-o.uv.y; //v3.4.9c
		}
		#endif				
		
		o.interpolatedRay = _FrustumCornersWS[(int)index];
		o.interpolatedRay.w = index;


		//v3.5 clouds
		//float4 p = o.vertex;////
		//o.uv = (p.xy / p.w + 1) * 0.5;

       // vert_sky(v.vertex.xyz, o);
       
        //v3.5 clouds
       
		//TANGENT_SPACE_ROTATION;
		return o;
	}

	v2f vertINV(appdata v)//appdata_img v)
	{
		//5.0.2
		UNITY_SETUP_INSTANCE_ID(v);

		v2f o;

		//v5.0.2
		UNITY_INITIALIZE_OUTPUT(v2f, o);
		UNITY_TRANSFER_INSTANCE_ID(v, o);
		UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
		
		half index = v.vertex.z;

		v.vertex.z = 0.1;
		o.vertex = UnityObjectToClipPos(v.vertex);
		o.uv = v.texcoord.xy;

		o.uv_depth = MultiplyUV(UNITY_MATRIX_TEXTURE0, v.texcoord);

#if UNITY_UV_STARTS_AT_TOP
		if (_MainTex_TexelSize.y < 0) {
			o.uv_depth.y = 1 - o.uv_depth.y;
		}
#endif	
		float2 XY = o.vertex.xy / o.vertex.w * _HorizonZAdjust; //v2.1.24

																//PERSPECTIVE PROJECTION
		float4 farClip = float4(XY, 1, 1);
		float4 farWorld = mul(_WorldClip, farClip);
		//float3 farWorldScaled = farWorld.xyz/farWorld.w;
		float3 farWorldScaled = farWorld.xyz / farWorld.w * (30000 / _cameraFarDistanceAdjust); //v4.9.4

		o.FarCam = farWorldScaled - _WorldSpaceCameraPosC;//-_CameraWS

#if UNITY_UV_STARTS_AT_TOP
		if (_MainTex_TexelSize.y < 0) {
#if defined(UNITY_REVERSED_Z)
			o.uv.y = 1 - o.uv.y;; //1-TerrainDepth; //v2.1.19			
#endif
								  //o.uv.y = 1-o.uv.y; //v3.4.9c
		}
#endif				
		o.uv.x = 1 - o.uv.x; //v5.0.3
		o.interpolatedRay = _FrustumCornersWS[(int)index];
		o.interpolatedRay.w = index;


		//v3.5 clouds
		//float4 p = o.vertex;////
		//o.uv = (p.xy / p.w + 1) * 0.5;

		// vert_sky(v.vertex.xyz, o);

		//v3.5 clouds

		//TANGENT_SPACE_ROTATION;
		return o;
	}

	v2f vert1 (appdata_img v)
	{
		v2f o;
		half index = v.vertex.z;

		v.vertex.z = 0.1;
		o.vertex = UnityObjectToClipPos(v.vertex);
		o.uv = v.texcoord.xy;

		o.uv_depth =MultiplyUV(UNITY_MATRIX_TEXTURE0, v.texcoord);

		#if UNITY_UV_STARTS_AT_TOP
		if(_MainTex_TexelSize.y < 0){
			o.uv_depth.y = 1-o.uv_depth.y;
		}
		#endif	
		float2 XY = o.vertex.xy / o.vertex.w * _HorizonZAdjust; //v2.1.24

		//PERSPECTIVE PROJECTION
			float4 farClip = float4(XY,1,1);
			float4 farWorld = mul(_WorldClip, farClip);
			float3 farWorldScaled = farWorld.xyz/farWorld.w;
			o.FarCam = farWorldScaled - _WorldSpaceCameraPosC;//-_CameraWS

			//ALL PROJECTIONS
//			float4 nearClip = float4(XY,-1,1);
//			float4 farClip = float4(XY,1,1);
//			float4 nearWorld = mul(_WorldClip, nearClip);
//			float4 farWorld = mul(_WorldClip, farClip);
//			o.worldMul = farWorld - nearWorld;
//			o.worldAdd = nearWorld;

		//o.ViewDir = normalize(WorldSpaceViewDir(v.vertex));
		//v3.5
				
		#if UNITY_UV_STARTS_AT_TOP
		if (_MainTex_TexelSize.y < 0)
			//	o.uv.y = 1-o.uv.y;
		#endif		
				
		
		o.interpolatedRay = _FrustumCornersWS[(int)index];
		o.interpolatedRay.w = index;

		return o;
	}




	//v3.5
	 //v3.5 clouds
	 float UVRandom(float2 uv)
    {
        float f = dot(float2(12.9898, 78.233), uv);
        return frac(43758.5453 * sin(f));
    }

    float SampleNoise(float3 uvw, float _Altitude1, float _NoiseAmp1, float Alpha)//v3.5.3
    {

    	float AlphaFactor = clamp(Alpha*_InteractTextureAtr.w,_InteractTextureAtr.x,1) ;

        const float baseFreq = 1e-5;

        float4 uvw1 = float4(uvw * _NoiseFreq1 * baseFreq, 0);
        float4 uvw2 = float4(uvw * _NoiseFreq2 * baseFreq, 0);

        uvw1.xyz += _Scroll1.xyz * _Time.x;
        uvw2.xyz += _Scroll2.xyz * _Time.x;

        float n1 = tex3Dlod(_NoiseTex1, uvw1).a;
        float n2 = tex3Dlod(_NoiseTex2, uvw2).a;
        float n = n1 * _NoiseAmp1*AlphaFactor + n2 * _NoiseAmp2;//v3.5.3

        n = saturate(n + _NoiseBias);

        float y = uvw.y - _Altitude0;
        float h = _Altitude1*1 - _Altitude0;//v3.5.3
        n *= smoothstep(0, h * (0.1 + _UndersideCurveFactor), y);
        n *= smoothstep(0, h * 0.4, h - y);

        return n  ;
    }

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

    float MarchLight(float3 pos, float rand, float _Altitude1, float _NoiseAmp1, float Alpha)
    {
		float3 light = float3(v3LightDir.x, _invertRay * v3LightDir.y, v3LightDir.z);//v3LightDir;// _WorldSpaceLightPos0.xyz; //v4.8
        float stride = (_Altitude1 - pos.y) / (light.y * _SampleCountL);

        //v3.5.2
        if(_invertRay * v3LightDir.y < 0){//if(_WorldSpaceLightPos0.y < 0){  //v4.8
       		//if(_WorldSpaceLightPos0.y > -0.01){         
          		stride = (_Altitude0 - pos.y + _WorldSpaceCameraPosC.y ) / (light.y * _SampleCountL*15); //higher helps frame rate A LOT
          	//}
        }

        pos += light * stride * rand;

        float depth = 0;
        UNITY_LOOP for (int s = 0; s < _SampleCountL; s++)
        {
            depth += SampleNoise(pos,_Altitude1,_NoiseAmp1,Alpha) * stride;
            pos += light * stride;
        }

        return BeerPowder(depth);
    }
     //v3.5 clouds









	
	// Applies one of standard fog formulas, given fog coordinate (i.e. distance)
	half ComputeFogFactor (float coord)
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
			fogFac = _SceneFogParams.x * coord; fogFac = exp2(-fogFac*fogFac);
		}
		return saturate(fogFac);
	}

	// Distance-based fog
	float ComputeDistance (float3 camDir, float zdepth)
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
	float ComputeHalfSpace (float3 wsDir)
	{
		float3 wpos = _CameraWS + wsDir;
		float FH = _HeightParams.x;
		float3 C = _CameraWS;
		float3 V = wsDir;
		float3 P = wpos;
		float3 aV = _HeightParams.w * V;
		float FdotC = _HeightParams.y;
		float k = _HeightParams.z;
		float FdotP = P.y-FH;
		float FdotV = wsDir.y;
		float c1 = k * (FdotP + FdotC);
		float c2 = (1-2*k) * FdotP;
		float g = min(c2, 0.0);
		g = -length(aV) * (c1 - g * g / abs(FdotV+1.0e-5f));
		return g;
	}
	
//SM v1.7
float3 totalRayleigh(float3 lambda){
	float pi = 3.141592653589793238462643383279502884197169;
	float n = 1.0003; // refraction of air
	float N = 2.545E25; //molecules per air unit volume 								
	float pn = 0.035;		 
	return (8.0 * pow(pi, 3.0) * pow(pow(n, 2.0) - 1.0, 2.0) * (6.0 + 3.0 * pn)) / (3.0 * N * pow(lambda, float3(4.0,4.0,4.0)) * (6.0 - 7.0 * pn));
}

float rayleighPhase(float cosTheta)
{    
	return (3.0 / 4.0) * (1.0 + pow(cosTheta, 2.0));
} 
      
float3 totalMie(float3 lambda, float3 K, float T)
{   
 	float pi = 3.141592653589793238462643383279502884197169;
 	float v = 4.0; 
	float c = (0.2 * T ) * 10E-18;
	return 0.434 * c * pi * pow((2.0 * pi) / lambda, float3(v - 2.0,v - 2.0,v - 2.0)) * K;
} 

float hgPhase(float cosTheta, float g)
{   
	float pi = 3.141592653589793238462643383279502884197169;
	return (1.0 / (4.0*pi)) * ((1.0 - pow(g, 2.0)) / pow(1.0 - 2.0*g*cosTheta + pow(g, 2.0), 1.5));
} 

float sunIntensity(float zenithAngleCos)
{       
	float cutoffAngle = 3.141592653589793238462643383279502884197169/1.95;//pi/
	float steepness = 1.5;
	float EE = 1000.0;
	return EE * max(0.0, 1.0 - exp(-((cutoffAngle - acos(zenithAngleCos))/steepness)));
} 

float logLuminance(float3 c)
{        
	return log(c.r * 0.2126 + c.g * 0.7152 + c.b * 0.0722);
}

float3 tonemap(float3 HDR) 
{
	float Y = logLuminance(HDR);
	float low = exp(((Y*lumFac+(1.0-lumFac))*luminance) - bias - contrast/2.0);
	float high = exp(((Y*lumFac+(1.0-lumFac))*luminance) - bias + contrast/2.0);
	float3 ldr = (HDR.rgb - low) / (high - low);
	return float3(ldr);
}

	half4 ComputeFog (v2f i, bool distance, bool height) : SV_Target
	{

		//v5.0.2
		UNITY_SETUP_INSTANCE_ID(i);
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

		half4 sceneColor = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, UnityStereoTransformScreenSpaceTex(i.uv)); //v5.0.2

			//v2.1.15 - add skybox to start as backdrop
			//half4 skybox = tex2D(_SkyTex, i.uv);
			//sceneColor.rgb = sceneColor.rgb  + skybox*(1-sceneColor.a);
			//sceneColor = sceneColor  + skybox*(1-sceneColor.a);
			//sceneColor = float4(0,0,0,0);
		
		// Reconstruct world space position & direction
		// towards this screen pixel.
		//float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,i.uv_depth);
		//v5.0.2
		float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, UnityStereoTransformScreenSpaceTex(i.uv_depth));

		float dpth = Linear01Depth(rawDepth);

		//v5.0.2
		//float4 wsDir =  dpth * i.interpolatedRay; //dpth * i.interpolatedRay;
		//float4 wsPos = _CameraWS + wsDir;


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
		float3 ray = (wsPos - _WorldSpaceCameraPosC) / length(wsPos - _WorldSpaceCameraPosC);
		float4 wsDir = float4(ray,1);// dpth * i.interpolatedRay; //dpth * i.interpolatedRay;
		//wsDir = dpth * i.interpolatedRay; //v5.0.3

		
		//SM v1.7
		float3 lightDirection = v3LightDir;// _WorldSpaceLightPos0.xyz;  

		//v4.8
		lightDirection = normalize(lightDirection);

		//v5.0.3
		/*float3 forward = mul((float3x3)unity_CameraToWorld, float3(0, 0, 1));
		if (_invertRay == -1) {
			lightDirection = -reflect(float3(lightDirection.x, -lightDirection.y, lightDirection.z), forward);
			lightDirection = normalize(float3(v3LightDir.x, lightDirection.y, v3LightDir.z));
		}*/

		//float  cosTheta = dot(normalize(wsDir), lightDirection);	
		//float cosTheta = dot(normalize(wsDir).xyz, lightDirection.xyz); //float  cosTheta = dot(normalize(wsDir), lightDirection); //v4.9.4 - PS4 compatibility
		float cosTheta = dot(float3(wsDir.x, _invertRay*wsDir.y, wsDir.z), float3(lightDirection.x, lightDirection.y, lightDirection.z)); //v5.0.3
				
		//float3 up = float3(0.0, 1.0, 0.0);			
		//float3 lambda = float3(680E-8, 550E-8, 450E-8); 
		//float3 K = float3(0.686, 0.678, 0.666);
		//float  rayleighZenithLength = 8.4E3;
		//float  mieZenithLength = 1.25E3;
		////float  mieCoefficient = 0.054;
		//float  pi = 3.141592653589793238462643383279502884197169;		
		//float3 betaR = totalRayleigh(lambda) * reileigh * 1000;		
		//float3 lambda1 = float3(_TintColor.r,_TintColor.g,_TintColor.b)*0.0000001;//  680E-8, 1550E-8, 3450E-8);
		//lambda = lambda1;
		//float3 betaM = totalMie(lambda1, K, turbidity * Multiplier2) * mieCoefficient; 
		//float zenithAngle = acos(max(0.0, dot(up, normalize(lightDirection))));        
		//float sR = rayleighZenithLength / (cos(zenithAngle) + 0.15 * pow(93.885 - ((zenithAngle * 180.0) / pi), -1.253));        
		//float sM = mieZenithLength / (cos(zenithAngle) + 0.15 * pow(93.885 - ((zenithAngle * 180.0) / pi), -1.253));		
		//float  rPhase = rayleighPhase(cosTheta*0.5+0.5);
		//float3 betaRTheta = betaR * rPhase;
		//float  mPhase = hgPhase(cosTheta, mieDirectionalG) * Multiplier1;
		//float3 betaMTheta = betaM * mPhase;	
	 //	float3 Fex = exp(-(betaR * sR + betaM * sM));
		//float  sunE = sunIntensity(dot(lightDirection, up));
		//float3 Lin = ((betaRTheta + betaMTheta) / (betaR + betaM)) * (1 - Fex) + sunE*Multiplier3*0.0001;
		//float  sunsize = 0.0001;
		//float3 L0 = 1.5 * Fex + (sunE * 1.0 * Fex)*sunsize;
		//float3 FragColor = tonemap(Lin+L0);
		
		//v4.9.4
		float3 multMobile = float3(1000, 1000000000, 1);
		if (_mobileFactor > 0) {
			//v3LightDir.y = -v3LightDir.y;
			//lightDirection = -lightDirection;
			//dpth = 1 - dpth;
			//wsDir = dpth * i.interpolatedRay;
			multMobile = float3(0.0000001, 1, 0.0001);
		}

		float3 up = float3(0.0, 1.0, 0.0);
		float3 lambda = float3(680E-8, 550E-8, 450E-8);
		float3 K = float3(0.686, 0.678, 0.666);
		float  rayleighZenithLength = 8.4E3;
		float  mieZenithLength = 1.25E3;
		//float  mieCoefficient = 0.054;
		float  pi = 3.141592653589793238462643383279502884197169;
		float3 betaR = totalRayleigh(lambda) * reileigh * 1000;
		float3 lambda1 = float3(_TintColor.r, _TintColor.g, _TintColor.b)*multMobile.x;//  680E-8, 1550E-8, 3450E-8);//v4.9.3
		lambda = lambda1;
		float3 betaM = totalMie(lambda1, K, turbidity * Multiplier2) * mieCoefficient * multMobile.y;//v4.9.3
		float zenithAngle = acos(max(0.0, dot(up, normalize(lightDirection))));
		float sR = rayleighZenithLength / (cos(zenithAngle) + 0.15 * pow(93.885 - ((zenithAngle * 180.0) / pi), -1.253));
		float sM = mieZenithLength / (cos(zenithAngle) + 0.15 * pow(93.885 - ((zenithAngle * 180.0) / pi), -1.253));
		float  rPhase = rayleighPhase(cosTheta*0.5 + 0.5);
		float3 betaRTheta = betaR * rPhase;
		float  mPhase = hgPhase(cosTheta, mieDirectionalG) * Multiplier1;
		float3 betaMTheta = betaM * mPhase;
		float3 Fex = exp(-(betaR * sR + betaM * sM));
		float  sunE = sunIntensity(dot(lightDirection, up));
		float3 Lin = ((betaRTheta + betaMTheta) / (betaR + betaM)) * (1 - Fex) + sunE * Multiplier3*multMobile.z;//v4.9.3
		float  sunsize = 0.0001;// 0.00001;
		float3 L0 = 1.5 * Fex + (sunE * 1.0 * Fex)*sunsize;
		float3 FragColor = (tonemap(Lin + L0));
		
		
		
		
		

		// Compute fog distance
		float g = _DistanceParams.x;
		if (distance)
			g += ComputeDistance (wsDir, dpth);
		if (height)
			g += ComputeHalfSpace (_invertRay *wsDir); //v4.0 //v4.8

		// Compute fog amount
		half fogFac = ComputeFogFactor (max(0.0,g));//*1.5;
		// Do not fog skybox
		//if (rawDepth >= 0.999999){
		if (rawDepth >= 0.999995  ){
			if(FogSky <= 0){
				fogFac = 1.0;
			}else{
				if (distance){
					fogFac = fogFac*ClearSkyFac;
				}
			}
		}
		//return fogFac; // for debugging
		
		// Lerp between fog color & original scene color
		// by fog amount
		//return lerp (unity_FogColor, sceneColor, fogFac);
		
		
		//SM v1.7
		float4 Final_fog_color = lerp (unity_FogColor+float4(FragColor,1),sceneColor, fogFac) ;			
		float Dist = ComputeDistance (wsDir, dpth);
		if(_Far >0){
			if(Dist > _Close ){
				if(Dist < _Far){ 				
					float greyscale = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, UnityStereoTransformScreenSpaceTex(i.uv)).r;
					Final_fog_color = Final_fog_color* tex2D(_ColorRamp, float2(Dist/_Far, 0.5)); //v5.0.2
				}
			}
		}




	//v3.5
//	float _SampleCount0=2;
//    float _SampleCount1=3;
//    int _SampleCountL=4;
//
//    sampler3D _NoiseTex1;
//    sampler3D _NoiseTex2;
//    float _NoiseFreq1=3.1;
//    float _NoiseFreq2=35.1;
//    float _NoiseAmp1=5;
//    float _NoiseAmp2=1;
//    float _NoiseBias=-0.2;
//
//    float3 _Scroll1 = float3 (0.01, 0.08, 0.06);
//    float3 _Scroll2 = float3 (0.01, 0.05, 0.03);
//
//    float _Altitude0 = 1500;
//    float _Altitude1 = 3500;
//    float _FarDist = 30000;
//
//    float _Scatter = 0.008;
//    float _HGCoeff = 0.5;
//    float _Extinct = 0.01;


		//VFOG
		//_WorldSpaceCameraPos.y -= 100 - 100*i.ViewDir.y;


		//ALL PROJECTIONS
//		float depthVOLIN = UNITY_SAMPLE_DEPTH(tex2D(_CameraDepthTexture,i.uv_depth));  //SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,i.uv_depth);
//		float4 PixelWorldW =  (i.worldMul*depthVOLIN) + i.worldAdd;
//		float3 PixelWorld = PixelWorldW.xyz/PixelWorldW.w;
	
		//PERSPECTIVE PROJECTION
		float depthVOLIN = dpth;//Linear01Depth(UNITY_SAMPLE_DEPTH(tex2D(_CameraDepthTexture,i.uv_depth)));  //SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,i.uv_depth);
		//float3 PixelWorld = (i.FarCam * depthVOLIN) + _WorldSpaceCameraPos ;
		//float3 PixelWorld = (i.FarCam * depthVOLIN) + _WorldSpaceCameraPos + float3(0, _HorizonYAdjust, 0); //ORTHO -14000 //v3.5.1

		//v2.1.19
		if (_fastest == 0) {
			depthVOLIN = 1;
		}		
		float3 PixelWorld = (i.FarCam * depthVOLIN) + _WorldSpaceCameraPosC + float3(0, _HorizonYAdjust, 0); //ORTHO -14000 //v3.5.1

		//v5.0.2
		//https://github.com/chriscummings100/worldspaceposteffect
		//PixelWorld = wsPos;

		//MINE
        //float3 viewDir = UNITY_MATRIX_IT_MV[2].xyz;
        //PixelWorld = mul(PixelWorld,i.ViewDir);


		//_WorldSpaceCameraPos.y -= 540;
		//_WorldSpaceCameraPos.y += 540; // define new height

		 //v3.5 clouds
		 //MINE
       // fixed4 color1 = tex2D(_MainTex,i.uv);
		float3 sky = Final_fog_color;//Final_fog_color;//sceneColor;//Final_fog_color;//color1//frag_sky(i);	//v3.5a

       	// float3 ray = -i.rayDir;

        //MINE
		//ray = vert_skyFRAG(PixelWorld);
		//float3 ray = (wsPos - _WorldSpaceCameraPosC)/length(wsPos - _WorldSpaceCameraPosC);// -(PixelWorld)*0.00001; //v5.0.2
		//ray.y = - ray.y;

		//ray.y = ray.y - 0.3*i.ViewDir.z;

		int samples = lerp(_SampleCount1, _SampleCount0, ray.y);
		//int samples = lerp(_SampleCount1, _SampleCount0/(length(ray)*10000), ray.y);
        //int samples = lerp(_SampleCount1*length(ray)*5, _SampleCount0, ray.y);
        // samples = lerp(_SampleCount1/(length(ray)*length(ray)*length(ray)*5), _SampleCount0, ray.y);
        // _SampleCountL = _SampleCountL*length(ray)*4;



		//v3.5.3
		//half2 tileableUv = half2(0,0);// mul(_Object2World,(v.vertex)).xz;	
		//float WorldScale=_InteractTexturePos.y;
		//float3 CamPos = float3(0,0,0);//_DepthCameraPos;//_WorldSpaceCameraPos;
		//float3 Origin =  float3(0,0,0);//float2(CamPos.x - WorldScale/2.0 , CamPos.z - WorldScale/2.0);
		//float2 UnscaledTexPoint = float2(tileableUv.x - Origin.x , tileableUv.y - Origin.y);
		//float2 ScaledTexPoint = float2(UnscaledTexPoint.x/WorldScale , UnscaledTexPoint.y/WorldScale);
		//float4 texInteract = tex2D(_InteractTexture,float2(PixelWorld.x,PixelWorld.z)); //tex2Dlod(_InteractTexture,float4(ScaledTexPoint,0.0,0.0));
		//_Altitude1 = texInteract.a * _Altitude1 * 1.2;
		// float4 texInteract = tex2D(_InteractTexture,210.105*float2(PixelWorld.x,PixelWorld.z)); //tex2Dlod(_InteractTexture,float4(ScaledTexPoint,0.0,0.0));
		//_NoiseAmp1 = (1-texInteract.a) * _NoiseAmp1 * 1.2;

		//v5.0.4
		ray  = ray + float3(0, _HorizonYAdjust * 0.0001, 0);

		//v4.8.2
		float dist0 = _Altitude0 / ray.y;
		float3 pos = ray * (dist0);
		pos = pos + _WorldSpaceCameraPosC;	
		//if (varianceAltitude1 == 1) {
		_Altitude1 = _Altitude1 + varianceAltitude1 * (cos(pos.x * 0.001) * 1500 + cos(pos.z * 0.001) * 2000 + cos(pos.x * 0.0005) * 300 + cos(pos.z * 0.0003) * 600);
		_Altitude1 = _Altitude1 + varianceAltitude1 * (abs(cos(pos.x * 0.001)) * 1500 + abs(sin(pos.z * 0.001)) * 2000);
		//_Altitude1 = _Altitude1 + varianceAltitude1 * (cos(pos.x * 0.001 + pos.z * 0.0012 + pos.z * 0.0014) * 2200);
		//}

        //float dist0 = _Altitude0 / ray.y; //v4.8.2
        float dist1 = _Altitude1 / ray.y;
        float stride = (dist1 - dist0) / samples;

        //
		//v2.1.19
		float alphaFactor = 1;
		if (_fastest == 0) {
			alphaFactor = _alphaFactor; //alphaFactor = 0; //v4.1f
		}
		//if (ray.y < 0.01 || dist0 >= _FarDist || ray.z < _NearZCutoff) return fixed4(sky, alphaFactor); //v3.5.1 //v2.1.19
		if (ray.y < 0.01 || dist0 >= _FarDist || ray.z < _NearZCutoff) return fixed4(0,0,0, alphaFactor); //v4.1

       // if (dist0 >= _FarDist) return fixed4(sky, 1);

		float3 light = v3LightDir;// _WorldSpaceLightPos0.xyz;//_WorldSpaceLightPos0.xyz; v4.8
        float hg = HenyeyGreenstein(dot(ray, light));

        float2 uv = i.uv + _Time.x;
        float offs = UVRandom(uv) * (dist1 - dist0) / samples;


       

        //float3 pos = _WorldSpaceCameraPos + ray * (dist0 + offs);

		
		//v4.8.2
		pos = pos + ray * (offs);
     //   float3 pos =  ray * (dist0 + offs);
     //  	pos.y = pos.y + _WorldSpaceCameraPosC.y;
     //   //	float4 wsDir = dpth * i.interpolatedRay;
     // 	//pos.y = pos.y * viewDir.y;

     // 		//v3.5.2
	    //pos.x = pos.x + _WorldSpaceCameraPosC.x;
	    //pos.z = pos.z + _WorldSpaceCameraPosC.z;


	    //v3.5.3
	   // float4 texInteract = tex2D(_InteractTexture,0.0003*float2(_InteractTexturePos.x*pos.x + _InteractTexturePos.z,_InteractTexturePos.y*pos.z + _InteractTexturePos.w)); //tex2Dlod(_InteractTexture,float4(ScaledTexPoint,0.0,0.0));
	     //float4 texInteract = tex2Dlod(_InteractTexture,0.0003*float4(_InteractTexturePos.x*pos.x + _InteractTexturePos.z,_InteractTexturePos.y*pos.z + _InteractTexturePos.w,0,0)); 
		//_NoiseAmp1 = clamp(texInteract.a*_InteractTextureAtr.w,_InteractTextureAtr.z,1) * _NoiseAmp1;
	   // _NoiseAmp2 = clamp(texInteract.a,0.9,1) * _NoiseAmp2;
	    //_Altitude1 = clamp(texInteract.a,0.1,1)*1001 + _Altitude1;
	    //_NoiseAmp2 = _NoiseAmp2*clamp(texInteract.a*_InteractTextureAtr.w,_InteractTextureAtr.y,1);


      	//v3.5.1
      	float dist = length(wsPos.xyz - _WorldSpaceCameraPosC.xyz);
      	if( dist < _FadeThreshold){			
			return fixed4(sky, 1);  
		}

        float3 acc = 0;

		//v2.1.19
		float3 intensityMod  = _LocalLightPos.w * _LocalLightColor.xyz * pow(10, 7);

        float depth = 0;
        //float expose = 0.00001;
        float preDevide = samples/_Exposure;
        float3 groundColor1 = _GroundColor.rgb*0.0006;
        float3 light1 = _LightColor0 *_SkyTint;
        float scatterHG = _Scatter * hg;
        UNITY_LOOP for (int s = 0; s < samples; s++)
        {

           //v3.5.3
	   // float4 texInteract = tex2D(_InteractTexture,0.0003*float2(_InteractTexturePos.x*pos.x + _InteractTexturePos.z,_InteractTexturePos.y*pos.z + _InteractTexturePos.w)); //tex2Dlod(_InteractTexture,float4(ScaledTexPoint,0.0,0.0));

	    //v4.0
//	     float4 texInteract = tex2Dlod(_InteractTexture,0.0003*float4(_InteractTexturePos.x*pos.x + _InteractTexturePos.z,_InteractTexturePos.y*pos.z + _InteractTexturePos.w,0,0)); 
 		 //uvw1.xyz += _Scroll1.xyz * _Time.x;	    
	     float4 texInteract = tex2Dlod(_InteractTexture,0.0003*float4(
	     _InteractTexturePos.x*pos.x + _InteractTexturePos.z*-_Scroll1.x * _Time.x + _InteractTextureOffset.x,
	     _InteractTexturePos.y*pos.z + _InteractTexturePos.w*-_Scroll1.z * _Time.x + _InteractTextureOffset.y,
	     0,0)); 

		//_NoiseAmp1 = clamp(texInteract.a*_InteractTextureAtr.w,_InteractTextureAtr.z,1) * _NoiseAmp1;
	   // _NoiseAmp2 = clamp(texInteract.a,0.9,1) * _NoiseAmp2;
	    //_Altitude1 = clamp(texInteract.a,0.1,1)*1001 + _Altitude1;

	    //v4.0
	    //texInteract.a = texInteract.a - _InteractTextureAtr.z * (1 - 0.00024*length(_LocalLightPos.xyz - pos));
	    float diffPos = length(_LocalLightPos.xyz - pos);
	     texInteract.a = texInteract.a +clamp( _InteractTextureAtr.z * 0.1*(1 - 0.00024*diffPos),-1.5,0);

	    _NoiseAmp2 = _NoiseAmp2*clamp(texInteract.a*_InteractTextureAtr.w,_InteractTextureAtr.y,1);

        	//float rand2 = UVRandom(uv + s + 0.5);
           // float n = SampleNoise(pos+ float3(rand2, cos(_Time.y)*rand2,2*cos(_Time.y+0.2)*rand2  ));
            float n = SampleNoise(pos,_Altitude1,_NoiseAmp1,texInteract.a);//float n = SampleNoise(pos,_Altitude1,_NoiseAmp1*clamp(texInteract.a*_InteractTextureAtr.w,_InteractTextureAtr.x,1));//float n = SampleNoise(pos,_Altitude1,_NoiseAmp1);// //v3.5.3

            float expose = 0.00001;
            if(s < preDevide){  //if(s < samples/3){
            	expose=0;
            }
            //else{
            	//expose=0.0001;
            //}

            if (n >= expose) //v4.0 added >= than only >, for better underlight control
            {
                float density = n * stride;
                float rand = UVRandom(uv + s + 1);
               //float scatter = density * _Scatter * hg * MarchLight(pos, rand * 0.5);
               // float scatter = density * _Scatter * hg * MarchLight(pos + float3(rand, cos(_Time.y)*rand,3*cos(_Time.y+0.1)*rand  ), rand * 0.1*cos(_Time.y+0.1));
              //  float scatter = density * _Scatter * hg * MarchLight(pos + float3(rand, 1*cos(5*_Time.y+0.1)*rand,2*cos(7*_Time.y+0.2)*rand  ), rand * 0.15*cos(2*_Time.y+0.11));

               //float scatter = density * _Scatter * hg * MarchLight(pos, rand * 0.001,_Altitude1,_NoiseAmp1,texInteract.a); //v4.0
               float scatter = density * scatterHG * MarchLight(pos, rand * 0.001,_Altitude1,_NoiseAmp1,texInteract.a); //v4.0

			   //acc += _LightColor0 * scatter * BeerPowder(depth)*_SkyTint
               //acc += _LightColor0 * scatter * BeerPowder(depth)*_SkyTint    + BeerPowder(depth)*scatter *float3(1,0,0)*1 * pow(10,7)/pow(length(float3(0,1500,4000) - pos),2);//v2.1.19
                
				//_LocalLightPos
				//v4.0 - global tint
		        //affect sky only using: Beer(depth)*float3(1,0,0)
		       	//affect clouds only using (1-Beer(depth))*float3(1,0,0)

		       	float3 beer1 = BeerPowder(depth) * intensityMod / pow(diffPos, _LocalLightColor.w);
		       	float beer2 = 1-Beer(depth);

		       	acc += light1 * scatter * BeerPowder(depth)  + beer2*groundColor1 +  (beer2*0.01*_LightColor0 +  scatter) * beer1;//v2.1.19

			    //acc += _LightColor0 * scatter * BeerPowder(depth)*_SkyTint  + (1-Beer(depth))*_GroundColor.rgb*0.0006 +  (1-Beer(depth))*0.01*_LightColor0*(BeerPowder(depth) * intensityMod / pow(length(_LocalLightPos.xyz - pos), _LocalLightColor.w)) 
				//	+  scatter * BeerPowder(depth) * intensityMod / pow(length(_LocalLightPos.xyz - pos), _LocalLightColor.w);//v2.1.19
			    //acc += _LightColor0 * scatter * BeerPowder(depth)*_SkyTint  
				//	+  BeerPowder(depth) * scatter * intensityMod / pow(length(_LocalLightPos.xyz - pos), _LocalLightColor.w);//v2.1.19

				depth += density;
            }
            pos += ray * stride ;
        }

       // float3 Final_sky_color = lerp (sky,1, 1-saturate(dist0)) ;			

        //Final_fog_color = lerp (unity_FogColor+float4(FragColor,1),sceneColor, fogFac) ;
       // float3 Final_fog_color3 = lerp (acc+Beer(depth)*sky,sceneColor, fogFac) ;	

		if (_mobileFactor > 0) { //v4.1f
			acc += Beer(depth) * sky + FragColor * _SunSize*acc;
			// acc = lerp(acc, sky, saturate((dist0*dist0 / (_FarDist*_FarDist*0.04)))-0.000035*(dist0));
		    // acc = lerp(acc, sky, saturate(((dist0*dist0) / (_FarDist*_FarDist*0.08))));
			acc = lerp(acc, sky*0.96, saturate(((dist0) / (_FarDist*0.5))) + 0.03);
			//	 return half4(acc, 1);
			//END v3.5 clouds
		}
		else {
			acc += FragColor * _SunSize * acc; //v4.9.4
		}

       // float4 finalColor = float4(acc+FragColor*_SunSize*acc,1)*1;
        float4 finalColor = float4(acc,1);
        //finalColor = color1+finalColor;


		half4 cloudColor = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CloudTex, UnityStereoTransformScreenSpaceTex(i.uv));// tex2D(_CloudTex, UnityStereoTransformScreenSpaceTex(i.uv)); //v5.0.2

        //half4 outColor = finalColor;
        //if(cloudColor.r > 0){
        //	outColor.rgb = finalColor.rgb/2 + cloudColor.rgb/2;
        //}

		//return finalColor;

		return float4(finalColor.rgb, pow(lerp(Beer(depth), 0.96, saturate(((dist0) / (_FarDist*0.5)))+0.03),2) ); //v4.0 define alpha for correct blending with background //v4.7
				
	}

	//v3.5.2
	float gaussed(float sig, float pos){
		//from "Fitting a Gaussion Function to binned data" paper
		float numer = pos*pos;
		float denom = sig*sig;
		float ratio = numer/denom;
		return (0.39894/sig) * exp(-0.5*ratio); 
	}

	half4 ComputeFogAddCombine(v2f i) : SV_Target
	{
		//v2.1.19		
		//if (splitFrames) {
		float fac1 = _WorldClip[2][3];
		float fac2 = _WorldClip[0][3];
		float fac3 = _WorldClip[1][3];
		//}
		//v5.0.2
		half4 cloudColor = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CloudTex, UnityStereoTransformScreenSpaceTex(i.uv) + float4(1 * 0 * 0.0014 , -1 * 0 * 0.0553, 0, 0)); //fac1 + float4(fac2, fac3, 0, 0)
		//v4.8.3 - interpolate colors
		//if (frameFraction > 0) {
		half4 cloudColorP = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CloudTexP, UnityStereoTransformScreenSpaceTex(i.uv) + float4(0* 0.019, 0 * 0.022, 0, 0));// +float4(-fac2 * 0.002, fac3 * 0.004, 0, 0));
		//if (cloudColorP.r > 0.1) {
			//cloudColor.rgb = lerp(cloudColorP.rgb, cloudColor.rgb, frameFraction);
			cloudColor = lerp(cloudColorP, cloudColor, frameFraction);
		//}
		//}		
			return float4(cloudColor.rgb, cloudColor.a);//cloudColorP;// float4(cloudColor.rgb, cloudColorP.a);
	}
		half4 ComputeFogAddCombineINV(v2f i) : SV_Target
	{		
		//v5.0.2
		half4 cloudColor = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, UnityStereoTransformScreenSpaceTex(i.uv)); 
			
		
		return float4(cloudColor.rgb, cloudColor.a);//cloudColorP;// float4(cloudColor.rgb, cloudColorP.a);
	}

		half4 ComputeFogAdd (v2f i, bool distance, bool height, bool splitFrames) : SV_Target
	{	

		//v2.1.19
		float fac1 = 1;
		float fac2 = 0;
		float fac3 = 0;
		if (splitFrames) {
			fac1 = _WorldClip[2][3];
			fac2 = _WorldClip[0][3];
			fac3 = _WorldClip[1][3];			
		}

		float2 iuvs = UnityStereoTransformScreenSpaceTex(i.uv);// float2(i.uv.x, (i.uv.y) * (1 + i.uv.x * fac2* fac2 * 6.5));
		// float2(i.uv.x, (i.uv.y) * (0.8 + i.uv.x * fac2));
		//float2(i.uv.x, i.uv.y * pow(i.uv.x, 0.01 * fac2 * 30));// i.uv;
		//float2(i.uv.x, (i.uv.y - i.uv.x*0.001) * (0.8 - i.uv.x * fac2) );// i.uv;


		half4 sceneColor = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, iuvs);// i.uv); //v4.8.3 //v5.0.2

		half4 cloudColor = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CloudTex, iuvs*fac1 +float4(fac2, fac3,0,0)); //v5.0.2

		//v2.1.19
		float2 uvs = iuvs; //v4.8
		if (_invertX == 1) {
			uvs = float2(1- iuvs.x, iuvs.y);
		}
		half4 skybox = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_SkyTex, uvs*fac1 + float4(fac2, fac3, 0, 0)); //v4.8.3 //v5.0.2
		if (_fastest == 0) {
			 //v2.1.15
		}
		//cloudColor = cloudColor + skybox;
		//v2.1.14 - fix borders issue
		//half4 sceneColor = tex2D(_MainTex, i.uv +  fixed2(-0.0005,-_CloudTex_TexelSize.y*1.1));

		//v2.1.15 - fix borders issue
		//half4 sceneColor = tex2D(_MainTex, i.uv +  fixed2(-0.0005,-_CloudTex_TexelSize.y*0.7));
		//half4 sceneColor = tex2D(_MainTex, i.uv +  fixed2(-0.0005,-0.0035));
		//half4 sceneColor = tex2D(_MainTex, i.uv +  fixed2(-0.0001,-_CloudTex_TexelSize.y*0.3));
		//half4 sceneColor = tex2D(_MainTex, i.uv +  fixed2(-0.0001,+_CloudTex_TexelSize.y*0.9));
		//half4 sceneColor = tex2D(_MainTex, i.uv +  fixed2(-0.0005,0.0015));

		//v3.5.2 - add self blur
//		fixed4 cloudColorUp3 	= tex2D(_CloudTex, i.uv + fixed2(_CloudTex_TexelSize.x,_CloudTex_TexelSize.y*3));
//		fixed4 cloudColorDown3 	= tex2D(_CloudTex, i.uv + fixed2(0,_CloudTex_TexelSize.y*3));
//		fixed4 cloudColorLeft3 	= tex2D(_CloudTex, i.uv + fixed2(_CloudTex_TexelSize.x*3,0));
//		fixed4 cloudColorRight3 = tex2D(_CloudTex, i.uv + fixed2(_CloudTex_TexelSize.x*3,_CloudTex_TexelSize.y));
//		fixed4 sum3 = cloudColorUp3 + cloudColorDown3 + cloudColorLeft3 + cloudColorRight3;
////		  
//		fixed4 cloudColorUp2 	= tex2D(_CloudTex, i.uv + fixed2(0,_CloudTex_TexelSize.y*2));
//		fixed4 cloudColorDown2 	= tex2D(_CloudTex, i.uv + fixed2(0,_CloudTex_TexelSize.y*2));
//		fixed4 cloudColorLeft2 	= tex2D(_CloudTex, i.uv + fixed2(_CloudTex_TexelSize.x*2,0));
//		fixed4 cloudColorRight2 = tex2D(_CloudTex, i.uv + fixed2(_CloudTex_TexelSize.x*2,0));
//		fixed4 sum2 = cloudColorUp2 + cloudColorDown2 + cloudColorLeft2 + cloudColorRight2;
//
//		fixed4 cloudColorUp 	= tex2D(_CloudTex, i.uv + fixed2(0,_CloudTex_TexelSize.y));
//		fixed4 cloudColorDown 	= tex2D(_CloudTex, i.uv + fixed2(0,_CloudTex_TexelSize.y));
//		fixed4 cloudColorLeft 	= tex2D(_CloudTex, i.uv + fixed2(_CloudTex_TexelSize.x,0));
//		fixed4 cloudColorRight = tex2D(_CloudTex, i.uv + fixed2(_CloudTex_TexelSize.x,0));
//		fixed4 sum = cloudColorUp + cloudColorDown + cloudColorLeft + cloudColorRight;
//
//		fixed4 cloudColorUpD 	= tex2D(_CloudTex, i.uv + fixed2(-_CloudTex_TexelSize.x,_CloudTex_TexelSize.y));
//		fixed4 cloudColorDownD 	= tex2D(_CloudTex, i.uv + fixed2(_CloudTex_TexelSize.x,_CloudTex_TexelSize.y));
//		fixed4 cloudColorLeftD 	= tex2D(_CloudTex, i.uv + fixed2(_CloudTex_TexelSize.x,_CloudTex_TexelSize.y));
//		fixed4 cloudColorRightD = tex2D(_CloudTex, i.uv + fixed2(_CloudTex_TexelSize.x,-_CloudTex_TexelSize.y));
//		fixed4 sumD = cloudColorUpD + cloudColorDownD + cloudColorLeftD + cloudColorRightD;

//		cloudColor = (cloudColor + sum + sumD + 0.5*sum3)/(1+4+4+2);
//
//		cloudColor = (cloudColor + sum + sum2 + sum3)/(1+4+4+4);





		//v2.1.13 - Gauss automated
		if (1 == 1) {
			int rows = 5;
			int iterations = 0.5*(rows - 1);
			float blurStrength = 0.4;
			UNITY_LOOP for (int i1 = -iterations; i1 <= iterations; ++i1) {
				UNITY_LOOP for (int j = -iterations; j <= iterations; ++j) {
					//if(cloudColor.a < 1){
					//v2.1.19
					//v5.0.2
					cloudColor += gaussed(3, float(i1))*
						UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CloudTex, float4(fac2, fac3, 0, 0) + fac1 *float2(iuvs.x + blurStrength*i1*_CloudTex_TexelSize.x, iuvs.y + blurStrength*j*_CloudTex_TexelSize.y));
					//}
				}
			}
			cloudColor = 1.2*cloudColor / (rows);
		}

		//v2.1.19
		if (splitFrames) {

			//if (i.uv.x < -fac2) {
			//	cloudColor = tex2D(_CloudTex, float4(0, i.uv.y + fac3, 0, 0));
			//}

			if (iuvs.x < -2*fac2) {
				float distFromEdge1 = iuvs.x + 2 * fac2;
				//cloudColor = tex2D(_CloudTex, float4(0, i.uv.y + fac3, 0, 0));
				cloudColor = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CloudTex, float4((0 - fac2) + (distFromEdge1 / 2), iuvs.y + fac3, 0, 0));//v5.0.2
			}

			//if (i.uv.x > 1 - fac2) {
			//	cloudColor = tex2D(_CloudTex, float4(1 - fac2, i.uv.y + fac3, 0, 0));//float4(1,0,0,0 );// tex2D(_CloudTex, float4(0, i.uv.y + fac3, 0, 0));
			//}

			if (iuvs.x > 1 - 2*fac2) {
				//cloudColor = tex2D(_CloudTex, float4(1 - fac2, i.uv.y + fac3, 0, 0));//float4(1,0,0,0 );// tex2D(_CloudTex, float4(0, i.uv.y + fac3, 0, 0));
				float distFromEdge2 = iuvs.x - (1 - 2 * fac2);
				cloudColor = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CloudTex, float4( (1 - (1 * fac2)) + (distFromEdge2 / 2), iuvs.y + fac3, 0, 0));//v5.0.2
			}

			//if (i.uv.y > 1 - fac3) {
			//	cloudColor = tex2D(_CloudTex, float4(i.uv.x + fac2, 1 - fac3, 0, 0));
			//}

			//fac3 = 41;
			if (iuvs.y > 1 - 2*fac3) {
				//cloudColor = tex2D(_CloudTex, float4(i.uv.x + fac2, 1 - fac3, 0, 0)); // float4(1, 0, 0, 0);//cloudColor = tex2D(_CloudTex, float4(i.uv.y + fac2,0 , 0, 0));
				
				//scale mode instead of copy edge pixels, start at twice fac3 distance and distribute pixels very second one (e.g. UV 1 in Y will get 1-2*fac3 pixel)
				//depending on distance 
				float distFromEdge = iuvs.y - (1 - 2*fac3);
				cloudColor = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CloudTex, float4(iuvs.x + fac2, (1 - (1*fac3)) + (distFromEdge/2), 0, 0));//v5.0.2

			}

			//if (i.uv.y < -fac3) {
			//	cloudColor = tex2D(_CloudTex, float4(i.uv.x + fac2, 0, 0, 0));
				////cloudColor = tex2D(_CloudTex, float4(i.uv.y + fac2, 1 - fac3, 0, 0));//float4(1,0,0,0 );// tex2D(_CloudTex, float4(0, i.uv.y + fac3, 0, 0));
			//}

			if (iuvs.y < -2 * fac3) {
				float distFromEdge3 = iuvs.y + 2 * fac3;
				//cloudColor = tex2D(_CloudTex, float4(0, i.uv.y + fac3, 0, 0));
				cloudColor = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CloudTex, float4(iuvs.x + fac2,  (0 - fac3) + (distFromEdge3 / 2),  0, 0));//v5.0.2
			}

			//CORNERS - sample from edge
			if (iuvs.y >  1 - 2*fac3) {
				if (iuvs.x <  -2*fac2) {
					cloudColor = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CloudTex, float4(0 - 2 * fac2, 1 - 2 * fac3, 0, 0)); // float4(1, 1, 1, 1);//v5.0.2
					//cloudColor = tex2D(_CloudTex, float4(0, 1, 0, 0))*1;
				}
			}
			if (iuvs.y >  1 - 2 * fac3) {
				if (iuvs.x > 1 - 2 * fac2) {
					cloudColor = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CloudTex, float4(1 - 2 * fac2, 1 - 2 * fac3, 0, 0));//v5.0.2
				}
			}
			if (iuvs.y < - 2 * fac3) {
				if (iuvs.x <  -2 * fac2) {
					cloudColor = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CloudTex, float4(0 - 2 * fac2,  - 2 * fac3, 0, 0));//v5.0.2
				}
			}
			if (iuvs.y < -2 * fac3) {
				if (iuvs.x > 1 - 2 * fac2) {
					cloudColor = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CloudTex, float4(1 - 2 * fac2,  - 2 * fac3, 0, 0));//v5.0.2
				}
			}
			////
		}

		// TEST SCALER
		//float fac33 = 0.05;
		//float fac22 = 0;
		//if (i.uv.y > 1 - 2*fac33) {			
			//float distFromEdge = i.uv.y - (1 - 2 * fac33);
			//cloudColor = tex2D(_CloudTex, float4(i.uv.x + fac22, (1 - (2 * fac33)) + distFromEdge/2, 0, 0));
		//}
		// END TEST SCALER

        //v2.1.14
//        o.uv.xy = v.texcoord.xy;
//		o.uv01 =  v.texcoord.xyxy + _Offsets.xyxy * float4(1,1, -1,-1) * _MainTex_TexelSize.xyxy / 6.0;
//		o.uv23 =  v.texcoord.xyxy + _Offsets.xyxy * float4(2,2, -2,-2) * _MainTex_TexelSize.xyxy / 6.0;
//		o.uv45 =  v.texcoord.xyxy + _Offsets.xyxy * float4(3,3, -3,-3) * _MainTex_TexelSize.xyxy / 6.0;
//		o.uv67 =  v.texcoord.xyxy + _Offsets.xyxy * float4(4,4, -4,-4) * _MainTex_TexelSize.xyxy / 6.0;
//		o.uv89 =  v.texcoord.xyxy + _Offsets.xyxy * float4(5,5, -5,-5) * _MainTex_TexelSize.xyxy / 6.0;
//
//		float4 sum = float4 (0,0,0,0);
//		float w = 0;
//		float weights = 0;
//		const float G_WEIGHTS[9] = {1.0, 0.8, 0.65, 0.5, 0.4, 0.2, 0.1, 0.05, 0.025}; 
//
//		float4 sampleA = tex2D(_MainTex, i.uv.xy);
//
//		float4 sampleB = tex2D(_MainTex, i.uv01.xy);
//		float4 sampleC = tex2D(_MainTex, i.uv01.zw);
//		float4 sampleD = tex2D(_MainTex, i.uv23.xy);
//		float4 sampleE = tex2D(_MainTex, i.uv23.zw);
//		float4 sampleF = tex2D(_MainTex, i.uv45.xy);
//		float4 sampleG = tex2D(_MainTex, i.uv45.zw);
//		float4 sampleH = tex2D(_MainTex, i.uv67.xy);
//		float4 sampleI = tex2D(_MainTex, i.uv67.zw);
//		float4 sampleJ = tex2D(_MainTex, i.uv89.xy);
//		float4 sampleK = tex2D(_MainTex, i.uv89.zw);
//
//		w = sampleA.a * G_WEIGHTS[0]; sum += sampleA * w; weights += w;
//		w = sampleB.a * G_WEIGHTS[1]; sum += sampleB * w; weights += w;
//		w = sampleC.a * G_WEIGHTS[1]; sum += sampleC * w; weights += w;
//		w = sampleD.a * G_WEIGHTS[2]; sum += sampleD * w; weights += w;
//		w = sampleE.a * G_WEIGHTS[2]; sum += sampleE * w; weights += w;
//		w = sampleF.a * G_WEIGHTS[3]; sum += sampleF * w; weights += w;
//		w = sampleG.a * G_WEIGHTS[3]; sum += sampleG * w; weights += w;
//		w = sampleH.a * G_WEIGHTS[4]; sum += sampleH * w; weights += w;
//		w = sampleI.a * G_WEIGHTS[4]; sum += sampleI * w; weights += w;
//		w = sampleJ.a * G_WEIGHTS[5]; sum += sampleJ * w; weights += w;
//		w = sampleK.a * G_WEIGHTS[5]; sum += sampleK * w; weights += w;
//
//		sum /= weights + 1e-4f;


 		//v2.1.14
		float4 sum = float4 (0,0,0,0);
		float w = 0;
		float weights = 0;
		const float G_WEIGHTS[9] = {1.0, 0.8, 0.65, 0.5, 0.4, 0.2, 0.1, 0.05, 0.025}; 

		float4 sampleA =  cloudColor;// tex2D(_CloudTex, i.uv.xy); // v2.1.15
		float texelX = _CloudTex_TexelSize.x/6.0;
		float texelY = _CloudTex_TexelSize.y/6.0;

		//v5.0.2
		float4 sampleB = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CloudTex, iuvs.xy + fixed2(texelX,texelY));
		float4 sampleC = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CloudTex, iuvs.xy + fixed2(-texelX,-texelY));
		float4 sampleD = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CloudTex, iuvs.xy + fixed2(2*texelX,2*texelY));
		float4 sampleE = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CloudTex, iuvs.xy + fixed2(-2*texelX,-2*texelY));
		float4 sampleF = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CloudTex, iuvs.xy + fixed2(3*texelX,3*texelY));
		float4 sampleG = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CloudTex, iuvs.xy + fixed2(-3*texelX,-3*texelY));
		float4 sampleH = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CloudTex, iuvs.xy + fixed2(4*texelX,4*texelY));
		float4 sampleI = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CloudTex, iuvs.xy + fixed2(-4*texelX,-4*texelY));
		float4 sampleJ = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CloudTex, iuvs.xy + fixed2(5*texelX,5*texelY));
		float4 sampleK = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CloudTex, iuvs.xy + fixed2(-5*texelX,-5*texelY));

		//v2.1.15
//		float4 sampleB1 = tex2D(_CloudTex, i.uv.xy + fixed2(texelX,-texelY));
//		float4 sampleB2 = tex2D(_CloudTex, i.uv.xy + fixed2(-texelX,texelY));
//		float4 sampleC1 = tex2D(_CloudTex, i.uv.xy + fixed2(0,-texelY));
//		float4 sampleC2 = tex2D(_CloudTex, i.uv.xy + fixed2(0,texelY));
//		float4 sampleD1 = tex2D(_CloudTex, i.uv.xy + fixed2(texelX,0));
//		float4 sampleD2 = tex2D(_CloudTex, i.uv.xy + fixed2(-texelX,0));

		//if pixel is white and has at least one black pixel near, darken it
		if(sampleA.a > 0.5){//0.5 for first if //0.35 lst if
			if(sampleB.a * sampleC.a * sampleD.a * sampleE.a * sampleF.a * sampleG.a * sampleH.a * sampleI.a < 0.004){
			//if(sampleB.a * sampleC.a * sampleD.a * sampleE.a *1 < 0.01){
			//if(sampleB.a * sampleC.a * sampleB1.a *sampleB2.a * sampleC1.a * sampleC2.a * sampleD1.a *sampleD2.a < 0.0001){
		  	 	sampleA = sampleA/2;
		  	 	sampleB = sampleB/2;
		  	 	sampleC = sampleC/2;
		  	 	sampleD = sampleD/2;
		  	 	sampleE = sampleE/2;
		  	 	sampleF = sampleF/2;
		  	 	sampleG = sampleG/2;
		  	 	//weights = 111;
				//sampleA = sampleB= sampleC= sampleD= sampleE= sampleF= sampleG= sampleH= sampleI= sampleJ= sampleK= float4(0,0,0,1);
		   }
		}

		w = sampleA.a * G_WEIGHTS[0]; sum += sampleA * w; weights += w;
		w = sampleB.a * G_WEIGHTS[1]; sum += sampleB * w; weights += w;
		w = sampleC.a * G_WEIGHTS[1]; sum += sampleC * w; weights += w;
		w = sampleD.a * G_WEIGHTS[2]; sum += sampleD * w; weights += w;
		w = sampleE.a * G_WEIGHTS[2]; sum += sampleE * w; weights += w;
		w = sampleF.a * G_WEIGHTS[3]; sum += sampleF * w; weights += w;
		w = sampleG.a * G_WEIGHTS[3]; sum += sampleG * w; weights += w;
		w = sampleH.a * G_WEIGHTS[4]; sum += sampleH * w; weights += w;
		w = sampleI.a * G_WEIGHTS[4]; sum += sampleI * w; weights += w;
		w = sampleJ.a * G_WEIGHTS[5]; sum += sampleJ * w; weights += w;
		w = sampleK.a * G_WEIGHTS[5]; sum += sampleK * w; weights += w;

		sum /= weights + 1e-4f;



		//v2.1.15 - fix borders issue
		//return sum + sceneColor * (sampleA.a*2 + sum.a/2)/2;
	//	return sum + float4( sceneColor.rgb * (sampleA.a*2 + sum.a/2)/2,0);
		//return  float4(sum.a,sum.a,sum.a,sum.a); // clouds alpha preview
		//return  float4(sceneColor.a,sceneColor.a,sceneColor.a,sceneColor.a); // scene alpha preview
		//return  float4(sceneColor.r,sceneColor.g,sceneColor.b,sceneColor.a) + (cloudColor+skybox)*(1-sceneColor.a); // scene alpha preview
		//return sum + sceneColor * sceneColor.a;
		//return sum + sceneColor * sum.a;

		//return sum+skybox + (sceneColor) * sceneColor.a;
		//return sum + sceneColor * (1- sceneColor.a);

		//return   float4(sceneColor.a,sceneColor.a,sceneColor.a,sceneColor.a) ;

		//return clamp((sum)+(skybox*1.2)* (1- clamp(sceneColor.a,0,1))* sum.a + sceneColor* sum.a,0,1);//v2.1.15
		//return clamp((sum)+(skybox*1.2)* (1- clamp(sceneColor.a,0,1))* sum.a  + sceneColor* 1,0,1);//v2.1.15


		//return cloudColor + sceneColor * cloudColor.a; //float4(cloudColor.rgb,0) * float4(sceneColor.rgb,0) +sceneColor; //v4.0 
		//return cloudColor + sceneColor;

		//float4 ds1 =  sum + float4( (sceneColor.rgb) * (sampleA.a*2 + sum.a/2)/2,0);  //v2.1.19



		
		

		//v2.1.19
		//v2.1.19
		if (_fastest == 0) {
			//return (skybox*(1 - sceneColor.a));
			//return cloudColor+skybox*(1-cloudColor.a) + sceneColor*(cloudColor.a);
			//return clamp(cloudColor*(1 - sceneColor.a), 0, 1) + sceneColor*(1) + float4(skybox.rgb*(cloudColor.a)*(1-	sceneColor.a),0);
			return clamp(cloudColor*(1 - sceneColor.a), 0, 1) + clamp(sceneColor, 0, 1) + clamp(float4(skybox.rgb*(cloudColor.a)*(1 - sceneColor.a), 0), 0, 1);
			//return clamp(cloudColor*(1 - sceneColor.a), 0, 1) + ds1 + skybox*(cloudColor.a)*(1 - sceneColor.a);
		}else{
			float4 ds1 = sum + float4((sceneColor.rgb) * (sampleA.a * 2 + sum.a / 2) / 2, 0);  //v2.1.19
			return ds1; //v2.1.19
		}
		
	}

ENDCG

SubShader
{
	ZTest Always Cull Off ZWrite Off Fog { Mode Off }

	// 0: distance + height
	Pass
	{
		CGPROGRAM
		#pragma vertex vert
		#pragma fragment frag
		#pragma target 3.0
		#pragma multi_compile_instancing
		half4 frag (v2f i) : SV_Target { return ComputeFog (i, true, true); }
		ENDCG
	}
	// 1: distance
	Pass
	{
		CGPROGRAM
		#pragma vertex vert
		#pragma fragment frag
		#pragma target 3.0
		#pragma multi_compile_instancing
		half4 frag (v2f i) : SV_Target { return ComputeFog (i, true, false); }
		ENDCG
	}
	// 2: height
	Pass
	{
		CGPROGRAM
		#pragma vertex vert //vert1
		#pragma fragment frag
		#pragma target 3.0
		#pragma multi_compile_instancing
		half4 frag (v2f i) : SV_Target { return ComputeFog (i, false, true); }
		ENDCG
	}
	// 3: combine
	Pass
	{
		CGPROGRAM
		#pragma vertex vert
		#pragma fragment frag
		#pragma target 3.0
		#pragma multi_compile_instancing
		half4 frag (v2f i) : SV_Target { return ComputeFogAdd (i, false, true,false); }
		ENDCG
	}
	// 4: combine split frames
	Pass
	{
		CGPROGRAM
		#pragma vertex vert
		#pragma fragment frag
		#pragma target 3.0
		#pragma multi_compile_instancing
		half4 frag(v2f i) : SV_Target{ return ComputeFogAdd(i, false, true,true); }
		ENDCG
	}
	// 5: combine previosu and current frames
	Pass
{
	CGPROGRAM
	#pragma vertex vert
	#pragma fragment frag
	#pragma target 3.0
	#pragma multi_compile_instancing
	half4 frag(v2f i) : SV_Target{ return ComputeFogAddCombine(i); }
	ENDCG
}

	// 6: combine previosu and current frames
	Pass
	{
		CGPROGRAM
		#pragma vertex vertINV
		#pragma fragment frag
		#pragma target 3.0
		#pragma multi_compile_instancing
		half4 frag(v2f i) : SV_Target{ return ComputeFogAddCombineINV(i); }
		ENDCG
	}

}

Fallback off

}

//Part of the code is based on MIT licensed code 
//MIT License
//Copyright(c) 2016 Unity Technologies
//Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files 
//(the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge,
//publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, 
// subject to the following conditions:
//The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF 
//MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR 
//ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH 
//THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.