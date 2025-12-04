// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Hidden/FullVolumetricCloudsSkyMaster" { 
Properties {
	_MainTex ("Base (RGB)", 2D) = "black" {}
	_CloudTex ("Base (RGB)", 2D) = "black" {}

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
        _Exposure("Exposure", Range(0, 8)) = 1.3

	     //v3.5 clouds
	     _BackShade ("Back shade of cloud top", Float) = 1
	     _UndersideCurveFactor ("Underside Curve Factor", Float) = 0

	     //v3.5.1
	     _NearZCutoff ("Away from camera Cutoff", Float) = -2
	     _HorizonYAdjust ("Adjust horizon Height", Float) = 0
	     _FadeThreshold ("Fade Near", Float) = 0
}

CGINCLUDE

	#include "UnityCG.cginc"
	#include "Lighting.cginc"
	// #include "AutoLight.cginc"

	//v3.5.1
	float _NearZCutoff;
	float _HorizonYAdjust;
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


    //v3.5 clouds



	uniform sampler2D _MainTex;
	uniform sampler2D _CloudTex;
	uniform sampler2D_float _CameraDepthTexture;
	
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
	uniform float luminance, Multiplier1, Multiplier2,Multiplier3,bias, lumFac, contrast,turbidity;
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
       float3 ViewDir : TEXCOORD4;
       //float4 worldAdd : TEXCOORD6;//
        //v3.5 clouds

     

	};

	//v3.5
	//#include "ProceduralSkySM.cginc" //changed, requires v2f above
	
	v2f vert (appdata_img v)
	{
		v2f o;
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
		float2 XY = o.vertex.xy / o.vertex.w;

		//PERSPECTIVE PROJECTION
			float4 farClip = float4(XY+float2(0,-0.2),1,1);
			float4 farWorld = mul(_WorldClip, farClip);
			float3 farWorldScaled = farWorld.xyz/farWorld.w;
			o.FarCam = farWorldScaled - _WorldSpaceCameraPos;//-_CameraWS

			//ALL PROJECTIONS
//			float4 nearClip = float4(XY,-1,1);
//			float4 farClip = float4(XY,1,1);
//			float4 nearWorld = mul(_WorldClip, nearClip);
//			float4 farWorld = mul(_WorldClip, farClip);
//			o.worldMul = farWorld - nearWorld;
//			o.worldAdd = nearWorld;


		o.ViewDir = normalize(WorldSpaceViewDir(v.vertex));
		//v3.5


		
		#if UNITY_UV_STARTS_AT_TOP
		if (_MainTex_TexelSize.y < 0)
			o.uv.y = 1-o.uv.y;
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




	//v3.5
	 //v3.5 clouds
	 float UVRandom(float2 uv)
    {
        float f = dot(float2(12.9898, 78.233), uv);
        return frac(43758.5453 * sin(f));
    }

    float SampleNoise(float3 uvw)
    {
        const float baseFreq = 1e-5;

        float4 uvw1 = float4(uvw * _NoiseFreq1 * baseFreq, 0);
        float4 uvw2 = float4(uvw * _NoiseFreq2 * baseFreq, 0);

        uvw1.xyz += _Scroll1.xyz * _Time.x;
        uvw2.xyz += _Scroll2.xyz * _Time.x;

        float n1 = tex3Dlod(_NoiseTex1, uvw1).a;
        float n2 = tex3Dlod(_NoiseTex2, uvw2).a;
        float n = n1 * _NoiseAmp1 + n2 * _NoiseAmp2;

        n = saturate(n + _NoiseBias);

    //    float y = uvw.y - _Altitude0 +  _WorldSpaceCameraPos.y;
    //    float h = _Altitude1 - _Altitude0 +  _WorldSpaceCameraPos.y;

    	float higer=_Altitude0;
    	if(_WorldSpaceCameraPos.y > _Altitude0){
    		higer = _WorldSpaceCameraPos.y;
    	}
        float y = (uvw.y - higer) + 0;
        float h = _Altitude1 - higer -0;

        n *= smoothstep(0, h * (0.1 + _UndersideCurveFactor), y);
        n *= smoothstep(0, h * 0.4, h - y);

        return n;
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

    float MarchLight(float3 pos, float rand)
    {
        float3 light = _WorldSpaceLightPos0.xyz;
        float stride = (_Altitude1+  _WorldSpaceCameraPos.y - pos.y) / (light.y * _SampleCountL);

        pos += light * stride * rand;

        float depth = 0;
        UNITY_LOOP for (int s = 0; s < _SampleCountL; s++)
        {
            depth += SampleNoise(pos) * stride;
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
		half4 sceneColor = tex2D(_MainTex, i.uv);
		
		// Reconstruct world space position & direction
		// towards this screen pixel.
		float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,i.uv_depth);
		float dpth = Linear01Depth(rawDepth);
		float4 wsDir = dpth * i.interpolatedRay;
		float4 wsPos = _CameraWS + wsDir;
		
		//SM v1.7
		float3 lightDirection = v3LightDir;// _WorldSpaceLightPos0.xyz;  
		float  cosTheta = dot(normalize(wsDir), lightDirection);		
				
		float3 up = float3(0.0, 0.0, 1.0);			
		float3 lambda = float3(680E-8, 550E-8, 450E-8); 
		float3 K = float3(0.686, 0.678, 0.666);
		float  rayleighZenithLength = 8.4E3;
		float  mieZenithLength = 1.25E3;
		//float  mieCoefficient = 0.054;
		float  pi = 3.141592653589793238462643383279502884197169;		
		float3 betaR = totalRayleigh(lambda) * reileigh * 1000;		
		float3 lambda1 = float3(_TintColor.r,_TintColor.g,_TintColor.b)*0.0000001;//  680E-8, 1550E-8, 3450E-8);
		lambda = lambda1;
		float3 betaM = totalMie(lambda1, K, turbidity * Multiplier2) * mieCoefficient; 
		float zenithAngle = acos(max(0.0, dot(up, normalize(lightDirection))));        
		float sR = rayleighZenithLength / (cos(zenithAngle) + 0.15 * pow(93.885 - ((zenithAngle * 180.0) / pi), -1.253));        
		float sM = mieZenithLength / (cos(zenithAngle) + 0.15 * pow(93.885 - ((zenithAngle * 180.0) / pi), -1.253));		
		float  rPhase = rayleighPhase(cosTheta*0.5+0.5);
		float3 betaRTheta = betaR * rPhase;
		float  mPhase = hgPhase(cosTheta, mieDirectionalG) * Multiplier1;
		float3 betaMTheta = betaM * mPhase;	
	 	float3 Fex = exp(-(betaR * sR + betaM * sM));
		float  sunE = sunIntensity(dot(lightDirection, up));
		float3 Lin = ((betaRTheta + betaMTheta) / (betaR + betaM)) * (1 - Fex) + sunE*Multiplier3*0.0001;
		float  sunsize = 0.0001;
		float3 L0 = 1.5 * Fex + (sunE * 1.0 * Fex)*sunsize;
		float3 FragColor = tonemap(Lin+L0);
		
		
		
		
		
		
		

		// Compute fog distance
		float g = _DistanceParams.x;
		if (distance)
			g += ComputeDistance (wsDir, dpth);
		if (height)
			g += ComputeHalfSpace (wsDir); //v4.0

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
					float greyscale = tex2D(_MainTex, i.uv).r;					
					Final_fog_color = Final_fog_color*tex2D(_ColorRamp, float2(Dist/_Far, 0.5));
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
		//float3 PixelWorld = (i.FarCam * depthVOLIN) - _WorldSpaceCameraPos +float3(0,_HorizonYAdjust,0); //ORTHO -14000 //v3.5.1
		float3 PixelWorld = (i.FarCam * depthVOLIN) + 0 +float3(0,_HorizonYAdjust,0); //ORTHO -14000 //v3.5.1

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

        float signer = -1;
        //if( _WorldSpaceCameraPos.y > i.interpolatedRay.y){
        if(i.interpolatedRay.y < _WorldSpaceCameraPos.y){
        	//signer = 1;
        }
		//ray = vert_skyFRAG(PixelWorld);
		//float3 ray = -1*signer*normalize(i.interpolatedRay)*0.11 + float3(0,_HorizonYAdjust,0)*0.00001;  ;//-(PixelWorld)*0.00001;
		float3 ray =signer*(PixelWorld)*0.00001;


		//ray =normalize(i.interpolatedRay) ;
		//ray.y = - ray.y;
		//float signer = 1;
//		 if( _WorldSpaceCameraPos.y > _Altitude0){
//			ray.y = - ray.y;
//			//ray.y =0.001* (_Altitude0-_WorldSpaceCameraPos.y)*ray.y;
//		}

		 //if( _WorldSpaceCameraPos.y < PixelWorld.y*0.00001){
			// ray.y = - ray.y;
			// ray.y =(PixelWorld.y*0.00001 - _WorldSpaceCameraPos.y) *ray.y;
		// }

		//ray.y = -ray.y - 0.1*i.ViewDir.z;

		int samples = lerp(_SampleCount1, _SampleCount0, ray.y);
		//int samples = lerp(_SampleCount1, _SampleCount0/(length(ray)*10000), ray.y);
        //int samples = lerp(_SampleCount1*length(ray)*5, _SampleCount0, ray.y);
       // samples = lerp(_SampleCount1/(length(ray)*length(ray)*length(ray)*5), _SampleCount0, ray.y);
       // _SampleCountL = _SampleCountL*length(ray)*4;



       float low_point = _Altitude0;
       if(_WorldSpaceCameraPos.y > _Altitude0){
       	low_point = _WorldSpaceCameraPos.y;
       }

        float dist0 = (low_point) / ray.y;//_Altitude0-
        float dist1 = (_Altitude1) / ray.y;//float dist1 = (_Altitude1 +  _WorldSpaceCameraPos.y) / ray.y;
        float stride = (dist1 - dist0) / samples;

        //if( _WorldSpaceCameraPos.y > _Altitude0){
       		// if (ray.y > 0.01 || dist0 >= _FarDist || ray.z < _NearZCutoff) return fixed4(sky, 1); //v3.5.1
       // }
       // if (ray.y < 0.01 || dist0 >= _FarDist || ray.z < _NearZCutoff) return fixed4(sky, 1); //v3.5.1

       //if (dist0 >= _FarDist ) return fixed4(sky, 1); //v3.5.1
 
       // ray.y = - ray.y;
//       if (_WorldSpaceCameraPos.y < _Altitude0 ) 
//       {
//	        if (i.interpolatedRay.y  <_WorldSpaceCameraPos.y ) {
//	       		return fixed4(sky, 1); //v3.5.1
//	       	}
//       }else{
//       		if (i.interpolatedRay.y  >_WorldSpaceCameraPos.y-1001 ) {
//	       		return fixed4(sky, 1); //v3.5.1
//	       	}
//       }

//       float signer2 = sign(_WorldSpaceCameraPos.y - _Altitude0) ;
//       if (signer2*i.interpolatedRay.y  >_WorldSpaceCameraPos.y ) {
//	       		return fixed4(sky, 1); //v3.5.1
//	   }

       // if (dist0 >= _FarDist) return fixed4(sky, 1);

        float3 light = -_WorldSpaceLightPos0.xyz;//_WorldSpaceLightPos0.xyz;
        float hg = HenyeyGreenstein(dot(ray, light));

        float2 uv = i.uv + _Time.x;
        float offs = UVRandom(uv) * (dist1 - dist0) / samples;


       

        //float3 pos = _WorldSpaceCameraPos + ray * (dist0 + offs);

        float3 pos =  ray * (dist0 + offs);
//        if(i.interpolatedRay.y < _WorldSpaceCameraPos.y){
//	        pos.y = pos.y + _WorldSpaceCameraPos.y; 
//	        pos.x = pos.x - _WorldSpaceCameraPos.x; 
//	        pos.z = pos.z - _WorldSpaceCameraPos.z; 
//        }else{
//            pos.y = pos.y + _WorldSpaceCameraPos.y; 
//	        pos.x = pos.x + _WorldSpaceCameraPos.x; 
//	        pos.z = pos.z + _WorldSpaceCameraPos.z; 
//        }
        pos.y = pos.y + _WorldSpaceCameraPos.y; 
	        pos.x = pos.x + _WorldSpaceCameraPos.x; 
	        pos.z = pos.z + _WorldSpaceCameraPos.z; 

       pos = _WorldSpaceCameraPos;
//        if( _WorldSpaceCameraPos.y > pos.y){
//			//ray.y = - ray.y;
//			//ray.y = 0.001*(pos.y-_WorldSpaceCameraPos.y) * ray.y;
//
//
//			 samples = lerp(_SampleCount1, _SampleCount0, ray.y);
//			dist0 = (_Altitude0 -  _WorldSpaceCameraPos.y) / ray.y;
//	        dist1 = (_Altitude1 +  _WorldSpaceCameraPos.y) / ray.y;
//	        stride = (dist1 - dist0) / samples;
//
//	        hg = HenyeyGreenstein(dot(ray, light));
//
//	        uv = i.uv + _Time.x;
//	        offs = UVRandom(uv) * (dist1 - dist0) / samples;
//
//	        pos =  ray * (dist0 + offs);
//	        pos.y = pos.y + _WorldSpaceCameraPos.y; 
//	        pos.x = pos.x - _WorldSpaceCameraPos.x; 
//        	pos.z = pos.z - _WorldSpaceCameraPos.z; 
//
//		}
//        if (ray.y < 0.0001 ) return fixed4(sky, 1); //v3.5.1

//		 if( _WorldSpaceCameraPos.y > _Altitude0){
//		 		    pos =  ray * (dist0 + offs);
//		 			pos.y = pos.y + _WorldSpaceCameraPos.y; 
//			        pos.x = pos.x - _WorldSpaceCameraPos.x; 
//		        	pos.z = pos.z - _WorldSpaceCameraPos.z; 
//		 }
       	
        	
        //	float4 wsDir = dpth * i.interpolatedRay;
      	//pos.y = pos.y * viewDir.y;
      
      	//v3.5.1
      	float dist = length(wsPos.xyz - _WorldSpaceCameraPos.xyz); //cut camera to remove low fog
      	if( dist < _FadeThreshold){			
			//return fixed4(sky, 1);  
		}





        float3 acc = 0;

        float depth = 0;
        UNITY_LOOP for (int s = 0; s < samples; s++)
        {

           //if (_WorldSpaceCameraPos.y < pos.y  ) 
	       //{
		        if (i.interpolatedRay.y  <_WorldSpaceCameraPos.y ) {
		       		//return fixed4(sky, 1); //v3.5.1
		       	}
//	       }else{
	       		if (i.interpolatedRay.y  >_WorldSpaceCameraPos.y-1001 ) {
		       		//return fixed4(sky, 1); //v3.5.1
		       	}
//	       }

			if(dot(i.ViewDir,_WorldSpaceCameraPos) < 89){
				//pos = 0;
			}

	        //if( _WorldSpaceCameraPos.y > pos.y){
			//	ray.y = - ray.y;
			//	ray.y = 0.001*(pos.y-_WorldSpaceCameraPos.y) * ray.y;

//				 samples = lerp(_SampleCount1, _SampleCount0, ray.y);
//				dist0 = (_Altitude0 -  _WorldSpaceCameraPos.y) / ray.y;
//		        dist1 = (_Altitude1 +  _WorldSpaceCameraPos.y) / ray.y;
//		        stride = (dist1 - dist0) / samples;
//
//		        hg = HenyeyGreenstein(dot(ray, light));
//
//		        uv = i.uv + _Time.x;
//		        offs = UVRandom(uv) * (dist1 - dist0) / samples;
//
//		        pos =  ray * (dist0 + offs);
//		        pos.y = pos.y + _WorldSpaceCameraPos.y; 
//		        pos.x = pos.x - _WorldSpaceCameraPos.x; 
//	        	pos.z = pos.z - _WorldSpaceCameraPos.z; 

			//}

        	//float rand2 = UVRandom(uv + s + 0.5);
           // float n = SampleNoise(pos+ float3(rand2, cos(_Time.y)*rand2,2*cos(_Time.y+0.2)*rand2  ));
            float n = SampleNoise(pos);
            if (n > 0)
            {
                float density = n * stride;
                float rand = UVRandom(uv + s + 1);
               //float scatter = density * _Scatter * hg * MarchLight(pos, rand * 0.5);
               // float scatter = density * _Scatter * hg * MarchLight(pos + float3(rand, cos(_Time.y)*rand,3*cos(_Time.y+0.1)*rand  ), rand * 0.1*cos(_Time.y+0.1));
              //  float scatter = density * _Scatter * hg * MarchLight(pos + float3(rand, 1*cos(5*_Time.y+0.1)*rand,2*cos(7*_Time.y+0.2)*rand  ), rand * 0.15*cos(2*_Time.y+0.11));

               float scatter = density * _Scatter * hg * MarchLight(pos, rand * 0.001); //v4.0

                acc += _LightColor0 * scatter * BeerPowder(depth)*_SkyTint;
                depth += density;
            }

            	 

            pos += ray  *stride ;
        }

       // float3 Final_sky_color = lerp (sky,1, 1-saturate(dist0)) ;			

        //Final_fog_color = lerp (unity_FogColor+float4(FragColor,1),sceneColor, fogFac) ;
       // float3 Final_fog_color3 = lerp (acc+Beer(depth)*sky,sceneColor, fogFac) ;	

        acc += Beer(depth) * sky+FragColor*_SunSize*acc;

        // acc = lerp(acc, sky, saturate((dist0*dist0 / (_FarDist*_FarDist*0.04)))-0.000035*(dist0));
       // acc = lerp(acc, sky, saturate(((dist0*dist0) / (_FarDist*_FarDist*0.08))));
          acc = lerp(acc, sky*0.96, saturate(((dist0) / (_FarDist*0.5)))+0.03);
        //	 return half4(acc, 1);
        //END v3.5 clouds



       // float4 finalColor = float4(acc+FragColor*_SunSize*acc,1)*1;
        float4 finalColor = float4(acc,1);
        //finalColor = color1+finalColor;


        half4 cloudColor = tex2D(_CloudTex, i.uv);

        //half4 outColor = finalColor;
        //if(cloudColor.r > 0){
        //	outColor.rgb = finalColor.rgb/2 + cloudColor.rgb/2;
        //}

		//return finalColor;

		return float4(finalColor.rgb, lerp(Beer(depth), 0.96, saturate(((dist0) / (_FarDist*0.5)))+0.03) ); //v4.0 define alpha for correct blending with background
				
	}

	half4 ComputeFogAdd (v2f i, bool distance, bool height) : SV_Target
	{
		half4 sceneColor = tex2D(_MainTex, i.uv);
		half4 cloudColor = tex2D(_CloudTex, i.uv);
		return cloudColor + sceneColor * cloudColor.a; //float4(cloudColor.rgb,0) * float4(sceneColor.rgb,0) +sceneColor; //v4.0 
		//return cloudColor + sceneColor;
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
		half4 frag (v2f i) : SV_Target { return ComputeFog (i, true, false); }
		ENDCG
	}
	// 2: height
	Pass
	{
		CGPROGRAM
		#pragma vertex vert
		#pragma fragment frag
		#pragma target 3.0
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
		half4 frag (v2f i) : SV_Target { return ComputeFogAdd (i, false, true); }
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