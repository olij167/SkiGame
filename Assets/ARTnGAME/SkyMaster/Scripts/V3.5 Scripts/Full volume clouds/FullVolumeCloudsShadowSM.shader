// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'
// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "SkyMaster/FullVolumeCloudsShadowSM" {
    Properties {
        _SunColor ("_SunColor", Color) = (0.95,0.95,0.95,0.8)
        _ShadowColor ("_ShadowColor", Color) = (0.05,0.05,0.1,0.3)
        _ColorDiff ("_ColorDiff", Float ) = 0.5
        _CloudMap ("_CloudMap", 2D) = "white" {}
        _CloudMap1 ("_CloudMap1", 2D) = "white" {}
        _Density ("_Density", Float ) = -0.4
        _Coverage ("_Coverage", Float ) = 4250
        _Transparency ("_Transparency", Float ) = 1
        _Velocity1 ("_Velocity1", Vector ) = (1,23,0,1) //w=0
        _Velocity2 ("_Velocity2", Vector ) = (1,22,0,1)  //w=0 
        _LightingControl ("_LightingControl", Vector) = (1,1,-1,0)       
        _HorizonFactor ("_HorizonFactor", Range(0, 10)) = 2    
        _EdgeFactors ("_EdgeFactor2", Vector) = (0,0.52,-1,0) 
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
        //_Mode ("_Mode", Range(0,5)) = 0
        _CutHeight ("_CutHeight", Float) = 240
        _CutHeight2 ("_CutHeight2", Float) = 1285
        Thickness ("Thickness", Float) = 1
         _CoverageOffset ("_Coverage Offset", Float ) = -0.15
          _ColorDiffOffset ("_ColorDiff Offset", Float ) = -0.1
			_TranspOffset("_Transparency Offset", Float) = 0

        ///SCATTER
        _Control ("Control Color", COLOR) = (1,1,1)
        _Color ("Color", COLOR) = (1,1,1) 
        _FogColor ("Fog Color", COLOR) = (1,1,1) 
        _FogFactor ("Fog factor", float) = 1
        _FogUnity ("Fog on/off(1,0)", float) = 0
        _PaintMap ("_CloudMap", 2D) = "white" {}



        //FULL VOLUME SHADOWS
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


         //v3.5 clouds
	     _BackShade ("Back shade of cloud top", Float) = 1
	     _UndersideCurveFactor ("Underside Curve Factor", Float) = 0

        //v3.5.3
	     _InteractTexture ("_Interact Texture", 2D) = "white" {}
         _InteractTexturePos ("Interact Texture Pos", Vector) = (1 ,1, 0, 0)	
         _InteractTextureAtr ("Interact Texture Attributes - 2multi 2offset", Vector) = (1 ,1, 0, 0)
         _InteractTextureOffset ("Interact Texture offsets", Vector) = (0 ,0, 0, 0) //v4.0

         //v3.5.1
	     _NearZCutoff ("Away from camera Cutoff", Float) = -2
	     _HorizonYAdjust ("Adjust horizon Height", Float) = 0
	     _FadeThreshold ("Fade Near", Float) = 0

		//v5.0c URP - v5.0.8 SRP
		_LocalLightPos("Local Light Pos & Intensity", Vector) = (0 ,0, 0, 0) //local light position (x,y,z) and intensity (w)	 

		//v5.1.0
		//FULL VOLUME CLOUDS
		_WeatherMap("_WeatherMap", 2D) = "white" {}
		_WeatherScale("_WeatherScale", Float) = 0
		_CoverageA("_CoverageA", Float) = 0
		_WindSpeed("_WindSpeed", Float) = 0
		_WindDirection("_WindDirection", Vector) = (0 ,0, 0, 0)
		_WindOffset("_WindOffset", Vector) = (0 ,0, 0, 0)
		_CoverageWindOffset("_CoverageWindOffset", Vector) = (0 ,0, 0, 0)

			//add offset for map origin reset
			cameraWSOffset("cameraWSOffset", Vector) = (0 ,0, 0, 0)

			//CHOOSE MODE, 0 = 2D noise shader clouds, 1 = image effect 3D noise clouds
			_shadowMode("Shadow Mode (0,1)", Float) = 1

    }
    SubShader {
        Tags {
            "IgnoreProjector"="True"
            "Queue"="Transparent"//"AlphaTest"
          //   "Queue"="AlphaTest"//"AlphaTest"
            "RenderType"="Transparent"
        }       
        Pass {
            Name "ForwardBase"
            Tags {
                "LightMode"="ForwardBase"
            }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            
            CGPROGRAM
            #include "UnityCG.cginc"

            //SCATTER
            #include "AutoLight.cginc"
            #include "Lighting.cginc"

            #pragma vertex vert
            #pragma fragment frag
            //#define UNITY_PASS_FORWARDBASE v4.1e
            #pragma multi_compile_fog 
            #pragma multi_compile_fwdbase    
            //#pragma fullforwardshadows   v4.1e               
            #pragma target 3.0

			uniform sampler2D _CloudMap; 
            uniform float4 _CloudMap_ST;
            uniform sampler2D _CloudMap1; 
            uniform float4 _CloudMap1_ST;
            //float4 _LightColor0;
            uniform float4 _SunColor;
            uniform float4 _ShadowColor;
            uniform float _ColorDiff;
            uniform float _Density;
            uniform float _Coverage;
            uniform float _Transparency;         
            uniform float _HorizonFactor;
            uniform float4 _LightingControl;
            uniform float2 _EdgeFactors;
            uniform float4 _Velocity1;
            uniform float4 _Velocity2;
            // uniform int _Mode;
            uniform float _CutHeight;
             uniform float _CutHeight2;
            uniform float4 _FogColor ;
            uniform float _FogFactor;
            uniform float _FogUnity;
            uniform float Thickness;
            uniform float _CoverageOffset;
               uniform float  _ColorDiffOffset;

            uniform sampler2D _PaintMap;
            uniform float4 _PaintMap_ST;

            //SCATTER			
			float3 _Color;
			float3 _Control;





			//FULL VOLUME CLOUDS SHADOWs
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

    //v3.5.3
    sampler2D _InteractTexture;
    float4 _InteractTexturePos;
    float4 _InteractTextureAtr;
    float4 _InteractTextureOffset; //v4.0

    //v3.5.1
	float _NearZCutoff;
	float _HorizonYAdjust;
	float _FadeThreshold;

    float _BackShade;
	float _UndersideCurveFactor;

	//v5.0c URP - v5.0.8 SRP
	float4 _LocalLightPos;

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

    float BeerPowder(float depth)
    {
    	float _Extinct = 0.01;
        return exp(-_Extinct * depth) * (1 - exp(-_Extinct * 2 * depth));
    }
    float Beer(float depth)
    {
    float _Extinct = 0.01;
      return exp(-_Extinct * depth * _BackShade);  // return exp(-_Extinct * depth); //_BackShade v3.5
    }


	//v5.0.1
	//FULLVOLUME CLOUDS
	//FULL VOLUME CLOUDS
	//TEXTURE2D(_WeatherMap); SAMPLER(sampler_WeatherMap); float4 _WeatherMap_TexelSize; //_WeatherMap
	sampler2D _WeatherMap;  float4 _WeatherMap_TexelSize; 
	//uniform float _Coverage;
	uniform float _WeatherScale;
	uniform float _CoverageA;
	uniform float _WindSpeed;
	uniform float3 _WindDirection;
	uniform float3 _WindOffset;
	uniform float2 _CoverageWindOffset;
	// samples weather texture
	float3 sampleWeather(float3 pos) {
		//float3 weatherData = tex2Dlod(_WeatherMap, float4((pos.xz + _CoverageWindOffset) * _WeatherScale, 0, 0)).rgb;
		//float3 weatherData = tex2D(_WeatherMap, float2((pos.xz + _CoverageWindOffset) * _WeatherScale)).rgb;
		//float3 weatherData = SAMPLE_TEXTURE2D(_WeatherMap, sampler_WeatherMap, float2((pos.xz + _CoverageWindOffset) * _WeatherScale*0.1)).rgb;
		//float3 weatherData = SAMPLE_TEXTURE2D(_WeatherMap, sampler_WeatherMap,
		//	float2((pos.xz + _CoverageWindOffset) * _WeatherScale*0.1) + float2(_WindDirection.x*_Time.y*_WindSpeed, _WindDirection.z*_Time.y*_WindSpeed)).rgb;
		//weatherData.r = saturate(weatherData.r - _CoverageA);

		float3 weatherData = tex2D(_WeatherMap,
			float2((pos.xz + _CoverageWindOffset) * _WeatherScale*0.1) 
			+ float2(_WindDirection.x*_Time.y*_WindSpeed, _WindDirection.z*_Time.y*_WindSpeed)).rgb;

		weatherData.r = saturate(weatherData.r - _CoverageA);

		//v0.2
		//float4 texInteract = tex2Dlod(_InteractTexture, 0.0003*float4(
		//	_InteractTexturePos.x*pos.x + _InteractTexturePos.z * _Time.x + _InteractTextureOffset.x,
		//	_InteractTexturePos.y*pos.z + _InteractTexturePos.w * _Time.x + _InteractTextureOffset.y,
		//	0, 0));
		/*float4 texInteract = SAMPLE_TEXTURE2D(_InteractTexture, sampler_InteractTexture, float2(
			_InteractTexturePos.x*pos.x + _InteractTexturePos.z * _Time.x + _InteractTextureOffset.x,
			_InteractTexturePos.y*pos.z + _InteractTexturePos.w * _Time.x + _InteractTextureOffset.y));*/
		float4 texInteract = tex2D(_InteractTexture, float2(
			_InteractTexturePos.x*pos.x + _InteractTexturePos.z * _Time.x + _InteractTextureOffset.x,
			_InteractTexturePos.y*pos.z + _InteractTexturePos.w * _Time.x + _InteractTextureOffset.y));
		float3 _LocalLightPos = float3(0, 0, 0);
		
		float diffPos = length(_LocalLightPos.xyz - pos);
		texInteract.a = texInteract.a + clamp(_InteractTextureAtr.z * 0.1*(1 - 0.00024*diffPos), -1.5, 0);
		weatherData = weatherData * clamp(texInteract.a*_InteractTextureAtr.w, _InteractTextureAtr.y, 1);

		return weatherData;
	}
	float4 cameraWSOffset;
	float _shadowMode;





            struct VertexInput {
                float4 vertex : POSITION;
                float3 normal : NORMAL;    
                float2 texcoord0 : TEXCOORD0;
            };
            struct VertexOutput {
                float4 pos : SV_POSITION;
                float2 uv0 : TEXCOORD0;
                float4 worldPos : TEXCOORD1;    
                   float3 ForwLight: TEXCOORD2;  
                   float3 camPos:TEXCOORD3;  
                     float3 normal : TEXCOORD4;                      
                LIGHTING_COORDS(5,6)                    
                UNITY_FOG_COORDS(7)
            };
            VertexOutput vert (VertexInput v) {           
             	VertexOutput o;    
                o.uv0 = v.texcoord0;    
                o.pos = UnityObjectToClipPos(v.vertex );                         
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);

               // o.worldPos.y = o.worldPos.y +1100;
                  //o.pos.y = o.pos.y -3000; //DOME

                //SCATTER 
                o.ForwLight =ObjSpaceLightDir(v.vertex); //ObjSpaceLightDir(v.vertex);
                o.camPos = normalize(WorldSpaceViewDir(v.vertex));
                o.normal = v.normal;
                TRANSFER_VERTEX_TO_FRAGMENT(o);		


                UNITY_TRANSFER_FOG(o,o.pos);
                return o;
            }
            float4 frag(VertexOutput i) : COLOR {

				//if (_shadowMode == 0) {
					float change_h = _CutHeight;//240;
					float PosDiff = Thickness * 0.0006*(i.worldPos.y - change_h) - 0.4;

					float2 UVs = _Density * float2(i.worldPos.x,i.worldPos.z);
					float4 TimingF = 0.0012;//0.0012

					float2 UVs1 = _Velocity1 * TimingF*_Time.y + UVs;

					float4 cloudTexture = tex2D(_CloudMap,UVs1 + _CloudMap_ST);
					float4 cloudTexture1 = tex2D(_CloudMap1,UVs1 + _CloudMap1_ST);

					//_PaintMap
					//float4 paintTexture1 = tex2D(_PaintMap,float4(_PaintMap_ST.xy,UVs1*_PaintMap_ST.zw));
					float4 paintTexture1 = tex2D(_PaintMap,UVs1*_PaintMap_ST.zw*_PaintMap_ST.xy);

					float2 UVs2 = (_Velocity2*TimingF*_Time.y + float2(_EdgeFactors.x,_EdgeFactors.y) + UVs);

					float4 Texture1 = tex2D(_CloudMap,UVs2*_Velocity1.w + _CloudMap_ST);
					float4 Texture2 = tex2D(_CloudMap1,UVs2*_Velocity2.w + _CloudMap1_ST);

					float DER = i.worldPos.y*0.001;
					float3 normalA = (((DER*((_Coverage + _CoverageOffset) + ((cloudTexture.rgb * 2) - 1))) - (1 - (Texture1.rgb * 2)))) * 1;             /////// -0.25 coverage	(-0.35,5)
					float3 normalN = normalize(normalA);

					//SCATTER              
					fixed atten = LIGHT_ATTENUATION(i);

					//     	float DER1 = -(i.worldPos.y)*PosDiff-95;  //-95
					float DER1 = (i.worldPos.y)*PosDiff - 95;  //-95
					float PosTDiff = i.worldPos.y;
					if (i.worldPos.y > change_h) {
						DER1 = (1 - cloudTexture1.a);
					}

					float shaper = (_Transparency + 4.5) *((DER1*saturate(((_Coverage + _CoverageOffset) - (0.8*PosDiff) + cloudTexture1.a* (Texture2.a))))); /////////////////// DIFERMCE			//////////////// * 30   _Transparency /////// -0.3 coverage	
					//	float shaper = (_Transparency+4.5) *( (DER1*saturate(( (_Coverage +_CoverageOffset)   -(0.8*PosDiff)+cloudTexture1.a ))))* (Texture2.a)   ;

					float3 lightDirect = normalize(_WorldSpaceLightPos0.xyz);
					lightDirect.y = -lightDirect.y;

					float ColDiff = (_ColorDiff + _ColorDiffOffset) + ((1 + (DER*_LightingControl.r*_LightingControl.g))*0.5);

					float verticalFactor = dot(lightDirect, float3(0,1,0));
					float Lerpfactor = (ColDiff + (_ShadowColor.a*(dot(lightDirect,normalN) - 1)*ColDiff));

					float ColB = _SunColor.rgb;

					change_h = _CutHeight2;   //10
					PosDiff = 0.0004*(i.worldPos.y - change_h);
					PosTDiff = i.worldPos.y*PosDiff;
					DER1 = -(i.worldPos.y)*PosDiff;

					if (i.worldPos.y > change_h) {
						DER1 = (1 - cloudTexture1.a) *  PosTDiff;
					}
					ColB = 1 * _SunColor.a*(1 - cloudTexture1.a)*_SunColor.rgb*DER1*(1 - verticalFactor);


					//SCATTER
					 //    	float diff = saturate(dot((-i.camPos), normalize(i.ForwLight)))+0.7;
					float diff = saturate(dot((normalN), normalize(i.ForwLight))) + 0.7;

					float diff2 = distance(_WorldSpaceCameraPos,i.worldPos)*distance(_WorldSpaceCameraPos,i.worldPos);

					float3 finalCol = diff * _LightColor0.rgb* atten;

					float3 endColor = (_Control.x*lerp(_ShadowColor.rgb,(0.7)*ColB, Lerpfactor) + _Control.z*float4(min(finalCol.rgb,1),Texture1.a))*_SunColor.rgb;//_Color;

					float4 Fcolor = float4(saturate(endColor + (_FogFactor / 3)  *diff2*_FogColor*0.00000001 + 0),saturate(shaper - 0.01*(_HorizonFactor*0.00001*diff2))); //-8 _FogFactor
					
				//}
			

				//FULL VOLUME CLOUD SHADOWS
				//if (_shadowMode == 1) {
					int samples = 2;
					float depth = 0;
					float3 pos = i.worldPos;//i.pos;
					float3 PixelWorld = pos + _WorldSpaceCameraPos + float3(0, _HorizonYAdjust, 0);
					float3 ray = PixelWorld;

					// float dist0 = _Altitude0 / ray.y;
					// float dist1 = _Altitude1 / ray.y;
					float dist0 = _Altitude0 / ray.y;
					float dist1 = _Altitude1 / ray.y;
					float stride = (dist1 - dist0) / samples;

					float2 uv = i.uv0 + _Time.x;
					float offs = UVRandom(uv) * (dist1 - dist0) / samples;

					//	pos =  ray * (dist0 + offs);
					//	pos.y = pos.y + _WorldSpaceCameraPos.y; 
								//v3.5.2
					 //   pos.x = pos.x + _WorldSpaceCameraPos.x;
					//    pos.z = pos.z + _WorldSpaceCameraPos.z;

					float3 acc = 0;

					//float4 texInteract = tex2D(_InteractTexture,0.0003*float4(_InteractTexturePos.x*pos.x + _InteractTexturePos.z,_InteractTexturePos.y*pos.z + _InteractTexturePos.w,0,0)); 
				   // float4 texInteract1 = tex2D(_InteractTexture,0.0003*float4(_InteractTexturePos.x*pos.x + _InteractTexturePos.z,_InteractTexturePos.y*pos.z + _InteractTexturePos.w,1010,1110)); 

					//v4.0
					float4 texInteract = tex2Dlod(_InteractTexture, 0.0003*float4(
						_InteractTexturePos.x*pos.x + _InteractTexturePos.z*-_Scroll1.x * _Time.x + _InteractTextureOffset.x,
						_InteractTexturePos.y*pos.z + _InteractTexturePos.w*-_Scroll1.z * _Time.x + _InteractTextureOffset.y,
						0, 0));
					float4 texInteract1 = tex2Dlod(_InteractTexture, 0.0003*float4(
						_InteractTexturePos.x*pos.x + _InteractTexturePos.z*-_Scroll1.x * _Time.x + _InteractTextureOffset.x,
						_InteractTexturePos.y*pos.z + _InteractTexturePos.w*-_Scroll1.z * _Time.x + _InteractTextureOffset.y,
						0, 0));
					
					UNITY_LOOP for (int s = 0; s < samples; s++)
					{
						//v3.5.3
					// float4 texInteract = tex2D(_InteractTexture,0.0003*float2(_InteractTexturePos.x*pos.x + _InteractTexturePos.z,_InteractTexturePos.y*pos.z + _InteractTexturePos.w)); //tex2Dlod(_InteractTexture,float4(ScaledTexPoint,0.0,0.0));

					//v5.0c URP - v5.0.8 SRP - add local light
						float diffPos = length(_LocalLightPos.xyz - pos);
						texInteract.a = texInteract.a + clamp(_InteractTextureAtr.z * 0.1*(1 - 0.00024*diffPos), -1.5, 0);

						//_NoiseAmp1 = clamp(texInteract.a*_InteractTextureAtr.w,_InteractTextureAtr.z,1) * _NoiseAmp1;
					   // _NoiseAmp2 = clamp(texInteract.a,0.9,1) * _NoiseAmp2;
						//_Altitude1 = clamp(texInteract.a,0.1,1)*1001 + _Altitude1;
						_NoiseAmp2 = _NoiseAmp2 * clamp(texInteract.a*_InteractTextureAtr.w, _InteractTextureAtr.y, 1);

						//float rand2 = UVRandom(uv + s + 0.5);
					   // float n = SampleNoise(pos+ float3(rand2, cos(_Time.y)*rand2,2*cos(_Time.y+0.2)*rand2  ));
						float n = SampleNoise(pos, _Altitude1, _NoiseAmp1, texInteract.a);//float n = SampleNoise(pos,_Altitude1,_NoiseAmp1*clamp(texInteract.a*_InteractTextureAtr.w,_InteractTextureAtr.x,1));//float n = SampleNoise(pos,_Altitude1,_NoiseAmp1);// //v3.5.3
						if (n > 0)
						{
							float density = n * stride;
							float rand = UVRandom(uv + s + 1);
							//float scatter = density * _Scatter * hg * MarchLight(pos, rand * 0.5);
							// float scatter = density * _Scatter * hg * MarchLight(pos + float3(rand, cos(_Time.y)*rand,3*cos(_Time.y+0.1)*rand  ), rand * 0.1*cos(_Time.y+0.1));
						   //  float scatter = density * _Scatter * hg * MarchLight(pos + float3(rand, 1*cos(5*_Time.y+0.1)*rand,2*cos(7*_Time.y+0.2)*rand  ), rand * 0.15*cos(2*_Time.y+0.11));

							float scatter = 0.1;// density * _Scatter * hg * MarchLight(pos, rand * 0.001,_Altitude1,_NoiseAmp1,texInteract.a); //v4.0

							 //_LocalLightPos
							// acc += _LightColor0 * scatter * BeerPowder(depth) *_SkyTint  
							 //	+  BeerPowder(depth) * scatter * intensityMod / pow(length(_LocalLightPos.xyz - pos), _LocalLightColor.w);//v2.1.19

							acc += _LightColor0 * scatter* BeerPowder(depth) + BeerPowder(depth) * scatter;//v2.1.19
							
							depth += density;
						}
						pos += ray * stride;
					}
					acc += Beer(depth) * 1 + 1 * 1 * acc;
					acc = lerp(acc, 1 * 0.96, saturate(((dist0) / (_FarDist*0.5))) + 0.03);
					// float4 finalColor = float4(acc+FragColor*_SunSize*acc,1)*1;
					float4 finalColor = float4(acc, 1);
					
					//v4.0
					pos = i.worldPos;
					//float n2 = SampleNoise(pos,_Altitude1,_NoiseAmp1,1-texInteract1.a*_InteractTextureAtr.x);
					float n22 = SampleNoise(pos + float3(0, 2000, 0), _Altitude1 + 1050, _NoiseAmp1, 1 - texInteract1.a*_InteractTextureAtr.x);
					//v4.1
					float n33 = SampleNoise(pos + float3(0, 0, 0), _Altitude1 + 0, _NoiseAmp1, 1);
					float n2 = SampleNoise(pos, _Altitude1, _NoiseAmp1, (texInteract.a)*0.0001*(1 - _InteractTextureAtr.x + 0.6));
					
					//        float accumulate = 0;
					//        UNITY_LOOP for (int s = 0; s < samples; s=s+1)
					//        {
					//        	float n1 = SampleNoise(pos,_Altitude1,_NoiseAmp1,1-texInteract1.a*_InteractTextureAtr.x);//float n = SampleNoise(pos,_Altitude1,_NoiseAmp1*clamp(texInteract.a*_InteractTextureAtr.w,_InteractTextureAtr.x,1));//float n = SampleNoise(pos,_Altitude1,_NoiseAmp1);// //v3.5.3
					//            if (n1 >= 0)
					//            {
					//                float density = n1 * stride;
					//                //float rand = UVRandom(uv + s + 1);
					//				accumulate += n1;//v2.1.19
					//				depth += density;
					//            }
					//            pos += ray * stride ;
					//        }

					// return float4(texInteract1.rgb+1-n1*100, texInteract1.a +(_InteractTextureAtr.x-0.4)  + n1*100);
					//return float4(float3(0,0,0),  texInteract1.a +(_InteractTextureAtr.x-0.4)   + n2*111 + n22*111);
					// return float4(texInteract1.rgb,(texInteract1.a+(_InteractTextureAtr.x-0.4)) * (n2*111111+0));
					
					//clip(   - ((1111  + ((1-texInteract.a)*1)*151111*(1-_InteractTextureAtr.x)   )-(n33+n22)*111100)         -  _Cutoff+0.4 );
									//SHADOW_CASTER_FRAGMENT(i)

					float4 finalTex = texInteract1 * 1;

					return float4(finalTex.r, finalTex.g, finalTex.b, 0.00004*((1111 + ((1 - texInteract.a) * 1) * 151111 * (1 - _InteractTextureAtr.x)) - (n33 + n22) * 111100));
					//return float4(texInteract1); //v4.1
				//}

				//if (_shadowMode == 2) {

				//}


	          //  if(_FogUnity==1){
	           //    UNITY_APPLY_FOG(i.fogCoord, Fcolor);
	          //  }
               // return (float4(Fcolor.r,Fcolor.g,Fcolor.b, Fcolor.a*paintTexture1.a));
                //return (float4(Fcolor.r,Fcolor.g,Fcolor.b, Fcolor.a)*paintTexture1);
            }
            ENDCG
        }











        ///v3.4.8


//
//         Pass {
//            Name "ForwardAdd"
//            Tags {
//                "LightMode"="ForwardAdd"
//            }
//            Blend One One
//            //ZWrite Off
//            
//            CGPROGRAM
//            #pragma vertex vert
//            #pragma fragment frag
//          //  #define UNITY_PASS_FORWARDADD
//            #include "UnityCG.cginc"
//            #include "AutoLight.cginc"
//             #include "Lighting.cginc"
//            #pragma multi_compile_fwdadd
//            #pragma multi_compile_fog
//            #pragma target 3.0
//
//            uniform sampler2D _CloudMap; 
//            uniform float4 _CloudMap_ST;
//            uniform sampler2D _CloudMap1; 
//            uniform float4 _CloudMap1_ST;
//            //float4 _LightColor0;
//            uniform float4 _SunColor;
//            uniform float4 _ShadowColor;
//            uniform float _ColorDiff;
//            uniform float _Density;
//            uniform float _Coverage;
//            uniform float _Transparency;         
//            uniform float _HorizonFactor;
//            uniform float4 _LightingControl;
//            uniform float2 _EdgeFactors;
//            uniform float4 _Velocity1;
//            uniform float4 _Velocity2;
//            // uniform int _Mode;
//            uniform float _CutHeight;
//             uniform float _CutHeight2;
//            uniform float4 _FogColor ;
//            uniform float _FogFactor;
//            uniform float _FogUnity;
//            uniform float Thickness;
//            uniform float _CoverageOffset;
//               uniform float  _ColorDiffOffset;
//
//            uniform sampler2D _PaintMap;
//            uniform float4 _PaintMap_ST;
//
//            //SCATTER			
//			float3 _Color;
//			float3 _Control;
//
//
//
//
//
//			//FULL VOLUME CLOUDS SHADOWs
//			//VFOG
//	float4x4 _WorldClip;
//
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
//    //v3.5.3
//    sampler2D _InteractTexture;
//    float4 _InteractTexturePos;
//    float4 _InteractTextureAtr;
//
//    //v3.5.1
//	float _NearZCutoff;
//	float _HorizonYAdjust;
//	float _FadeThreshold;
//
//    float _BackShade;
//	float _UndersideCurveFactor;
//
//			 //v3.5 clouds
//	 float UVRandom(float2 uv)
//    {
//        float f = dot(float2(12.9898, 78.233), uv);
//        return frac(43758.5453 * sin(f));
//    }
//
//    float SampleNoise(float3 uvw, float _Altitude1, float _NoiseAmp1, float Alpha)//v3.5.3
//    {
//
//    	float AlphaFactor = clamp(Alpha*_InteractTextureAtr.w,_InteractTextureAtr.x,1) ;
//
//        const float baseFreq = 1e-5;
//
//        float4 uvw1 = float4(uvw * _NoiseFreq1 * baseFreq, 0);
//        float4 uvw2 = float4(uvw * _NoiseFreq2 * baseFreq, 0);
//
//        uvw1.xyz += _Scroll1.xyz * _Time.x;
//        uvw2.xyz += _Scroll2.xyz * _Time.x;
//
//        float n1 = tex3Dlod(_NoiseTex1, uvw1).a;
//        float n2 = tex3Dlod(_NoiseTex2, uvw2).a;
//        float n = n1 * _NoiseAmp1*AlphaFactor + n2 * _NoiseAmp2;//v3.5.3
//
//        n = saturate(n + _NoiseBias);
//
//        float y = uvw.y - _Altitude0;
//        float h = _Altitude1*1 - _Altitude0;//v3.5.3
//        n *= smoothstep(0, h * (0.1 + _UndersideCurveFactor), y);
//        n *= smoothstep(0, h * 0.4, h - y);
//
//        return n  ;
//    }
//
//
//
//
//            struct VertexInput {
//                float4 vertex : POSITION;
//                float3 normal : NORMAL;    
//                float2 texcoord0 : TEXCOORD0;
//                float4 tangent: TANGENT;
//            };
//            struct VertexOutput {
//                float4 pos : SV_POSITION;
//                float2 uv0 : TEXCOORD0;
//                float4 worldPos : TEXCOORD1;
//                float3 ForwLight: TEXCOORD2;                         
//                LIGHTING_COORDS(3,4)                    
//                UNITY_FOG_COORDS(5)
//            };
//            VertexOutput vert (VertexInput v) {           
//             	VertexOutput o;    
//
//
//
//                o.uv0 = v.texcoord0;    
//                o.pos = UnityObjectToClipPos(v.vertex );                         
//                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
//
//                 //SCATTER
//               //  TANGENT_SPACE_ROTATION;
//                // o.ForwLight = mul(rotation,ObjSpaceLightDir(v.vertex)); //ObjSpaceLightDir(v.vertex);
//
//                o.ForwLight =ObjSpaceLightDir(v.vertex); //ObjSpaceLightDir(v.vertex);
//
//                TRANSFER_VERTEX_TO_FRAGMENT(o);	
//
//                UNITY_TRANSFER_FOG(o,o.pos);
//                return o;
//            }
//            float4 frag(VertexOutput i) : COLOR {
//                            
//                float change_h = _CutHeight;//240;
//				float PosDiff = Thickness*0.0006*(i.worldPos.y-change_h)-0.4;
//
//                float2 UVs = _Density*float2(i.worldPos.x,i.worldPos.z);
//                float4 TimingF = 0.0012;//0.0012
//
//                float2 UVs1 = _Velocity1*TimingF*_Time.y + UVs;
//
//                float4 cloudTexture = tex2D(_CloudMap,UVs1+_CloudMap_ST);
//                float4 cloudTexture1 = tex2D(_CloudMap1,UVs1+_CloudMap1_ST);
//
//                //_PaintMap
//                //float4 paintTexture1 = tex2D(_PaintMap,float4(_PaintMap_ST.xy,UVs1*_PaintMap_ST.zw));
//                float4 paintTexture1 = tex2D(_PaintMap,UVs1*_PaintMap_ST.zw*_PaintMap_ST.xy);
//
//                float2 UVs2 = (_Velocity2*TimingF*_Time.y + float2(_EdgeFactors.x,_EdgeFactors.y) + UVs);
//
//                float4 Texture1 = tex2D(_CloudMap,UVs2*_Velocity1.w+_CloudMap_ST); 
//                float4 Texture2 = tex2D(_CloudMap1,UVs2*_Velocity2.w+_CloudMap1_ST); 
//
//                float DER = i.worldPos.y*0.001;               
//                float3 normalA = (((DER*( (_Coverage +_CoverageOffset) +((cloudTexture.rgb*2)-1)))-(1-(Texture1.rgb*2)))) * 1;             /////// -0.25 coverage	(-0.35,5)
//             	float3 normalN = normalize(normalA); 
//
//             	//SCATTER              
//               	fixed atten = LIGHT_ATTENUATION(i);               
//              		
//        //     	float DER1 = -(i.worldPos.y)*PosDiff-95;  //-95
//        		float DER1 = (i.worldPos.y)*PosDiff-95;  //-95
//             	float PosTDiff = i.worldPos.y;
//             	if(i.worldPos.y > change_h){             		
//             		DER1 = (1-cloudTexture1.a);
//             	}
//
//             	float shaper = (_Transparency+4.5) *( (DER1*saturate(( (_Coverage +_CoverageOffset)   -(0.8*PosDiff)+cloudTexture1.a* (Texture2.a) ))))   ; /////////////////// DIFERMCE			//////////////// * 30   _Transparency /////// -0.3 coverage	
//             //	float shaper = (_Transparency+4.5) *( (DER1*saturate(( (_Coverage +_CoverageOffset)   -(0.8*PosDiff)+cloudTexture1.a ))))* (Texture2.a)   ;
//
//                float3 lightDirect = normalize(_WorldSpaceLightPos0.xyz);
//               	lightDirect.y = -lightDirect.y;
//               
//                float ColDiff =  (_ColorDiff+_ColorDiffOffset)+((1+(DER*_LightingControl.r*_LightingControl.g))*0.5); 
//
//                float verticalFactor = dot(lightDirect, float3(0,1,0));
//             	float Lerpfactor = (ColDiff+(_ShadowColor.a*(dot(lightDirect,normalN)-1)*ColDiff));
//
//                float ColB = _SunColor.rgb;
//           
//	                change_h =_CutHeight2;   //10
//	                PosDiff =  0.0004*(i.worldPos.y-change_h);  
//	                PosTDiff = i.worldPos.y*PosDiff;          
//	             	DER1 = -(i.worldPos.y)*PosDiff;
//
//	             	if(i.worldPos.y > change_h){	             		
//	             		DER1 = (1-cloudTexture1.a) *  PosTDiff ;
//	             	}
//	             	ColB =1*_SunColor.a*(1-cloudTexture1.a)*_SunColor.rgb*DER1*(1-verticalFactor);
//             	
//
//             	//SCATTER
//         //    	float diff = saturate(dot((-i.camPos), normalize(i.ForwLight)))+0.7;
//             	float diff = saturate(dot((normalN), normalize(i.ForwLight)))+0.7;
//             
//             	float diff2 = distance(_WorldSpaceCameraPos,i.worldPos)*distance(_WorldSpaceCameraPos,i.worldPos);           
//
//	            float3 finalCol = diff*_LightColor0.rgb* atten;	
//
//	            float3 endColor =( _Control.x*lerp(_ShadowColor.rgb,(0.7)*ColB, Lerpfactor) + _Control.z*float4(min(finalCol.rgb,1),Texture1.a)  )*_SunColor.rgb;//_Color;
//	          
//	            float4 Fcolor = float4(saturate(endColor + (_FogFactor/3)  *diff2*_FogColor*0.00000001 + 0),saturate(shaper - 0.01*(_HorizonFactor*0.00001*diff2)  )) ; //-8 _FogFactor
//	            
//
//	            if(_FogUnity==1){
//	               UNITY_APPLY_FOG(i.fogCoord, Fcolor);
//	            }
//
//                if(_WorldSpaceLightPos0.w != 0.0){ //if non directional
//               	 	//return (float4(Fcolor.r,Fcolor.g,Fcolor.b, Fcolor.a*paintTexture1.a))*atten*0.3*atten;
//               	 	return (float4(Fcolor.r,Fcolor.g,Fcolor.b, Fcolor.a*paintTexture1.a))*atten*1.6*atten*(shaper+0.01);
//                }else{
//                 	return float4(0,0,0,0); //if directional
//                }
//
//            }
//            ENDCG 
//        }
//
//
//
//        ///END v3.4.8
//
//
//
//
//







         Pass {
            Name "ShadowCaster"
            Tags {
                "LightMode"="ShadowCaster"
            }

            Fog {Mode Off}
			ZWrite On ZTest Less Cull Off
			//Blend SrcAlpha OneMinusSrcAlpha
			Offset 1, 1

                   //cull off    
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #define UNITY_PASS_SHADOWCASTER
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #pragma fragmentoption ARB_precision_hint_fastest
            #pragma multi_compile_fog 
            #pragma multi_compile_shadowcaster                    
            #pragma target 3.0

           
			


            uniform sampler2D _CloudMap; 
            uniform float4 _CloudMap_ST;
            uniform sampler2D _CloudMap1; 
            uniform float4 _CloudMap1_ST;
            //uniform float4 _LightColor0;          
            uniform float _Density;
            uniform float _Coverage;
            uniform float _Transparency;         
            uniform float2 _EdgeFactors;
            uniform float4 _Velocity1;
            uniform float4 _Velocity2;
            uniform float _Cutoff;
            uniform float _CutHeight;

          
            uniform float Thickness;
            uniform float _CoverageOffset;
			uniform float _TranspOffset;

              uniform sampler2D _PaintMap;
            uniform float4 _PaintMap_ST;





            //FULL VOLUME CLOUDS SHADOWs
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

    //v3.5.3
    sampler2D _InteractTexture;
    float4 _InteractTexturePos;
    float4 _InteractTextureAtr;
     float4 _InteractTextureOffset; //v4.0

    //v3.5.1
	float _NearZCutoff;
	float _HorizonYAdjust;
	float _FadeThreshold;

    float _BackShade;
	float _UndersideCurveFactor;

	//v5.0c URP - v5.0.8 SRP
	float4 _LocalLightPos;

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



     float BeerPowder(float depth)
    {
    	float _Extinct = 0.01;
        return exp(-_Extinct * depth) * (1 - exp(-_Extinct * 2 * depth));
    }
    float Beer(float depth)
    {
    float _Extinct = 0.01;
      return exp(-_Extinct * depth * _BackShade);  // return exp(-_Extinct * depth); //_BackShade v3.5
    }


	//v5.0.1
	//FULLVOLUME CLOUDS
	//FULL VOLUME CLOUDS
	//TEXTURE2D(_WeatherMap); SAMPLER(sampler_WeatherMap); float4 _WeatherMap_TexelSize; //_WeatherMap
	sampler2D _WeatherMap;  float4 _WeatherMap_TexelSize;
	//uniform float _Coverage;
	uniform float _WeatherScale;
	uniform float _CoverageA;
	uniform float _WindSpeed;
	uniform float3 _WindDirection;
	uniform float3 _WindOffset;
	uniform float2 _CoverageWindOffset;
	// samples weather texture
	float3 sampleWeather(float3 pos) {
		//float3 weatherData = tex2Dlod(_WeatherMap, float4((pos.xz + _CoverageWindOffset) * _WeatherScale, 0, 0)).rgb;
		//float3 weatherData = tex2D(_WeatherMap, float2((pos.xz + _CoverageWindOffset) * _WeatherScale)).rgb;
		//float3 weatherData = SAMPLE_TEXTURE2D(_WeatherMap, sampler_WeatherMap, float2((pos.xz + _CoverageWindOffset) * _WeatherScale*0.1)).rgb;
		//float3 weatherData = SAMPLE_TEXTURE2D(_WeatherMap, sampler_WeatherMap,
		//	float2((pos.xz + _CoverageWindOffset) * _WeatherScale*0.1) + float2(_WindDirection.x*_Time.y*_WindSpeed, _WindDirection.z*_Time.y*_WindSpeed)).rgb;
		//weatherData.r = saturate(weatherData.r - _CoverageA);

		float3 weatherData = tex2D(_WeatherMap,
			float2((pos.xz + _CoverageWindOffset) * _WeatherScale*0.1)
			+ float2(_WindDirection.x*_Time.y*_WindSpeed, _WindDirection.z*_Time.y*_WindSpeed)).rgb;

		weatherData.r = saturate(weatherData.r - _CoverageA);

		//v0.2
		//float4 texInteract = tex2Dlod(_InteractTexture, 0.0003*float4(
		//	_InteractTexturePos.x*pos.x + _InteractTexturePos.z * _Time.x + _InteractTextureOffset.x,
		//	_InteractTexturePos.y*pos.z + _InteractTexturePos.w * _Time.x + _InteractTextureOffset.y,
		//	0, 0));
		/*float4 texInteract = SAMPLE_TEXTURE2D(_InteractTexture, sampler_InteractTexture, float2(
		_InteractTexturePos.x*pos.x + _InteractTexturePos.z * _Time.x + _InteractTextureOffset.x,
		_InteractTexturePos.y*pos.z + _InteractTexturePos.w * _Time.x + _InteractTextureOffset.y));*/
		float4 texInteract = tex2D(_InteractTexture, float2(
			_InteractTexturePos.x*pos.x + _InteractTexturePos.z * _Time.x + _InteractTextureOffset.x,
			_InteractTexturePos.y*pos.z + _InteractTexturePos.w * _Time.x + _InteractTextureOffset.y));
		float3 _LocalLightPos = float3(0, 0, 0);

		float diffPos = length(_LocalLightPos.xyz - pos);
		texInteract.a = texInteract.a + clamp(_InteractTextureAtr.z * 0.1*(1 - 0.00024*diffPos), -1.5, 0);
		weatherData = weatherData * clamp(texInteract.a*_InteractTextureAtr.w, _InteractTextureAtr.y, 1);

		return weatherData;
	}
	float4 cameraWSOffset;
	float _shadowMode;


            struct VertexInput {
                float4 vertex : POSITION;
                float3 normal : NORMAL;    
                float2 texcoord0 : TEXCOORD0;
            };
            struct VertexOutput {
                V2F_SHADOW_CASTER;               
                float2 uv0 : TEXCOORD1;
                float4 worldPos : TEXCOORD2;               
            };
            VertexOutput vert (VertexInput v) {           
             	VertexOutput o;    
           // TRANSFER_SHADOW_CASTER(o)
                o.uv0 = v.texcoord0;    
                o.pos = UnityObjectToClipPos(v.vertex );                         
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);

                // o.pos.y = o.pos.y -3000;

                	TRANSFER_SHADOW_CASTER(o)
                return o;
            }
            float4 frag(VertexOutput i) : COLOR {
                            
////                 float2 UVs = _Density*float2(i.worldPos.x,i.worldPos.z);
////                float4 TimingF = 0.0012;
////                float2 UVs1 = _Velocity1*TimingF*_Time.y + UVs;
////                float4 cloudTexture = tex2D(_CloudMap,UVs1+_CloudMap_ST);
////                float4 cloudTexture1 = tex2D(_CloudMap1,UVs1+_CloudMap1_ST);
////                float2 UVs2 = (_Velocity2*TimingF*_Time.y + float2(_EdgeFactors.x,_EdgeFactors.y) + UVs);
////                float4 Texture1 = tex2D(_CloudMap,UVs2+_CloudMap_ST); 
////                float4 Texture2 = tex2D(_CloudMap1,UVs2+_CloudMap1_ST); 
////
////                float DER = i.worldPos.y*0.001;               
////                float3 normalA = (((DER*((_Coverage  +_CoverageOffset)+((cloudTexture.rgb*2)-1)))-(1-(Texture1.rgb*2))));             	
////             	float3 normalN = normalize(normalA); 
////
////				float change_h =_CutHeight;
////				float PosDiff = Thickness*0.0006*(i.worldPos.y-change_h);
////             	float DER1 = -(i.worldPos.y+0)*PosDiff;
////             	float PosTDiff = i.worldPos.y*PosDiff;
////             	if(i.worldPos.y > change_h){             		
////             		DER1 = (1-cloudTexture1.a) *  PosTDiff;
////             		//DER1 =  PosTDiff;
////             	}
//
//				float change_h = _CutHeight;//240;
//				float PosDiff = Thickness*0.0006*(i.worldPos.y-change_h)-0.4;
//
//                float2 UVs = _Density*float2(i.worldPos.x,i.worldPos.z);
//                float4 TimingF = 0.0012;//0.0012
//
//                float2 UVs1 = _Velocity1*TimingF*_Time.y + UVs;
//
//                float4 cloudTexture = tex2D(_CloudMap,UVs1+_CloudMap_ST);
//                float4 cloudTexture1 = tex2D(_CloudMap1,UVs1+_CloudMap1_ST);
//
//                //_PaintMap
//                // float4 paintTexture1 = tex2D(_PaintMap,UVs1+_CloudMap1_ST);
//                float4 paintTexture1 = tex2D(_PaintMap,UVs1*_PaintMap_ST.zw*_PaintMap_ST.xy);
//
//                float2 UVs2 = (_Velocity2*TimingF*_Time.y + float2(_EdgeFactors.x,_EdgeFactors.y) + UVs);
//
//                float4 Texture1 = tex2D(_CloudMap,UVs2*_Velocity1.w+_CloudMap_ST); 
//                float4 Texture2 = tex2D(_CloudMap1,UVs2*_Velocity2.w+_CloudMap1_ST); 
//
//                float DER = i.worldPos.y*0.001;               
//                float3 normalA = (((DER*( (_Coverage +_CoverageOffset) +((cloudTexture.rgb*2)-1)))-(1-(Texture1.rgb*2)))) * 1;             /////// -0.25 coverage	(-0.35,5)
//             	float3 normalN = normalize(normalA); 
//
//             	//SCATTER              
//         //      	fixed atten = LIGHT_ATTENUATION(i);               
//              		
//        //     	float DER1 = -(i.worldPos.y)*PosDiff-95;  //-95
//        		float DER1 = (i.worldPos.y)*PosDiff-95;  //-95
//             	float PosTDiff = i.worldPos.y;
//             	if(i.worldPos.y > change_h){             		
//             		DER1 = (1-cloudTexture1.a);
//             	}
//
//           //  	float shaper = _Transparency*((DER1*saturate(((_Coverage  +_CoverageOffset)+cloudTexture1.a)))*Texture2.a);
//             	float shaper = (_Transparency+4.5) *( (DER1*saturate(( (_Coverage +_CoverageOffset)   -(0.8*PosDiff)+cloudTexture1.a* (Texture2.a) ))))   ;

				float3 pos = i.worldPos;

				/* //v5.1.0
				float change_h = _CutHeight;//240;
				float PosDiff = Thickness*0.0006*(i.worldPos.y-change_h)-0.4;

                float2 UVs = _Density*float2(i.worldPos.x,i.worldPos.z);
                float4 TimingF = 0.0012;//0.0012

                float2 UVs1 = _Velocity1*TimingF*_Time.y + UVs;

                //float4 cloudTexture = tex2D(_CloudMap,UVs1+_CloudMap_ST);
                float4 cloudTexture1 = tex2D(_CloudMap1,UVs1+_CloudMap1_ST);
				
                	float3 pos = i.worldPos;//i.pos;
                	 float4 texInteract1 = tex2Dlod(_InteractTexture,1*0.003*float4(_InteractTexturePos.x*pos.x + _InteractTexturePos.z,_InteractTexturePos.y*pos.z + _InteractTexturePos.w,0,0)); 
                	// cloudTexture1 = cloudTexture1 + (texInteract1.a);//texInteract1.a;
                	  //cloudTexture1 = cloudTexture1 + (texInteract1.a);//texInteract1.a;
                	  cloudTexture1 = float4(cloudTexture1.r,cloudTexture1.g,cloudTexture1.b,texInteract1.a+0.5+cloudTexture1.a/2);// + (texInteract1.a);//texInteract1.a;

                //_PaintMap
                //float4 paintTexture1 = tex2D(_PaintMap,float4(_PaintMap_ST.xy,UVs1*_PaintMap_ST.zw));
                float4 paintTexture1 = tex2D(_PaintMap,UVs1*_PaintMap_ST.zw*_PaintMap_ST.xy);

                float2 UVs2 = (_Velocity2*TimingF*_Time.y + float2(_EdgeFactors.x,_EdgeFactors.y) + UVs);

                float4 Texture1 = tex2D(_CloudMap,UVs2*_Velocity1.w+_CloudMap_ST); 
                float4 Texture2 = tex2D(_CloudMap1,UVs2*_Velocity2.w+_CloudMap1_ST); 

                float DER = i.worldPos.y*0.001;               
                float3 normalA = (((DER*( (_Coverage +_CoverageOffset) +((cloudTexture1.rgb*2)-1)))-(1-(Texture1.rgb*2)))) * 1;             /////// -0.25 coverage	(-0.35,5)
             	float3 normalN = normalize(normalA); 

             	//SCATTER              
               	//fixed atten = LIGHT_ATTENUATION(i);               
              		
        //     	float DER1 = -(i.worldPos.y)*PosDiff-95;  //-95
        		float DER1 = (i.worldPos.y)*PosDiff-95;  //-95
             	float PosTDiff = i.worldPos.y;
             	if(i.worldPos.y > change_h){             		
             		DER1 = (1-cloudTexture1.a);
             	}

             	float shaper = (_Transparency+4.5) *( (DER1*saturate(( (_Coverage +_CoverageOffset)   -(0.8*PosDiff)+cloudTexture1.a* (Texture2.a) ))))   ; 

				*/

				if (_shadowMode == 0) {
					float change_h = _CutHeight;//240;
					float PosDiff = 0.0006*(i.worldPos.y - change_h);

					float2 UVs = _Density * float2(i.worldPos.x, i.worldPos.z);
					float4 TimingF = 0.0012;

					float2 UVs1 = _Velocity1 * TimingF*_Time.y + UVs;

					float4 cloudTexture = tex2D(_CloudMap, UVs1 + _CloudMap_ST);
					float4 cloudTexture1 = tex2D(_CloudMap1, UVs1 + _CloudMap1_ST);

					//_PaintMap
					// float4 paintTexture1 = tex2D(_PaintMap,UVs1+_CloudMap1_ST);
					float4 paintTexture1 = tex2D(_PaintMap, UVs1*_PaintMap_ST.xy + _PaintMap_ST.zw); //v5.0.9

					float2 UVs2 = (_Velocity2*TimingF*_Time.y / 1.4 + float2(_EdgeFactors.x, _EdgeFactors.y) + UVs);

					float4 Texture1 = tex2D(_CloudMap, UVs2 + _CloudMap_ST);
					float4 Texture2 = tex2D(_CloudMap1, UVs2 + _CloudMap1_ST);

					float DER = i.worldPos.y*0.001;
					float3 normalA = (((DER*((_Coverage + _CoverageOffset) + ((cloudTexture.rgb * 2) - 1))) - (1 - (Texture1.rgb * 2))));
					float3 normalN = normalize(normalA);

					float DER1 = -(i.worldPos.y - 0) * 1;
					float PosTDiff = i.worldPos.y * 1;
					if (i.worldPos.y > change_h) {
						DER1 = (1 - cloudTexture1.a) *  PosTDiff;
					}
					
					float shaper = ((_Transparency + _TranspOffset) - 0.48 + 0.0)*((DER1*(((_Coverage + _CoverageOffset) + cloudTexture1.a)))*Texture2.a);

					change_h = 10;
					PosDiff = Thickness * 0.0006*(i.worldPos.y - change_h);
					PosTDiff = i.worldPos.y*PosDiff;
					DER1 = -(i.worldPos.y + 0)*PosDiff;

					if (i.worldPos.y > change_h) {
						DER1 = (1 - cloudTexture1.a) *  PosTDiff;
					}

					if (i.worldPos.y > 150) { 																																///////////////////////////// 650
						DER1 = (1 - cloudTexture1.a) *  PosTDiff*0.07;
						shaper = shaper * shaper;
					}

					clip(shaper*paintTexture1 - _Cutoff + 0.4);
					SHADOW_CASTER_FRAGMENT(i)
				}



	            //FULL VOLUME CLOUD SHADOWS
				else if (_shadowMode == 1) {
					int samples = 2;
					float depth = 0;

					float3 PixelWorld = pos + _WorldSpaceCameraPos + float3(0, _HorizonYAdjust, 0);

					float3 ray = PixelWorld;

					// float dist0 = _Altitude0 / ray.y;
					// float dist1 = _Altitude1 / ray.y;
					float dist0 = _Altitude0 / ray.y;
					float dist1 = _Altitude1 / ray.y;
					float stride = (dist1 - dist0) / samples;

					float2 uv = i.uv0 + _Time.x;
					float offs = UVRandom(uv) * (dist1 - dist0) / samples;

					//	pos =  ray * (dist0 + offs);
					//	pos.y = pos.y + _WorldSpaceCameraPos.y; 
					//v3.5.2
					//   pos.x = pos.x + _WorldSpaceCameraPos.x;
					//    pos.z = pos.z + _WorldSpaceCameraPos.z;

					float3 acc = 0;
					//v4.0
					//float4 texInteract = tex2D(_InteractTexture,0.0003*float4(_InteractTexturePos.x*pos.x + _InteractTexturePos.z,_InteractTexturePos.y*pos.z + _InteractTexturePos.w,0,0)); 
					float4 texInteract = tex2Dlod(_InteractTexture, 0.0003*float4(
						_InteractTexturePos.x*pos.x + _InteractTexturePos.z*-_Scroll1.x * _Time.x + _InteractTextureOffset.x,
						_InteractTexturePos.y*pos.z + _InteractTexturePos.w*-_Scroll1.z * _Time.x + _InteractTextureOffset.y,
						0, 0));

					UNITY_LOOP for (int s = 0; s < samples; s++)
					{

						//v3.5.3
					   // float4 texInteract = tex2D(_InteractTexture,0.0003*float2(_InteractTexturePos.x*pos.x + _InteractTexturePos.z,_InteractTexturePos.y*pos.z + _InteractTexturePos.w)); //tex2Dlod(_InteractTexture,float4(ScaledTexPoint,0.0,0.0));

					   //v5.0c URP - v5.0.8 SRP - add local light
						float diffPos = length(_LocalLightPos.xyz - pos);
						texInteract.a = texInteract.a + clamp(_InteractTextureAtr.z * 0.1*(1 - 0.00024*diffPos), -1112.5, 10);

						//_NoiseAmp1 = clamp(texInteract.a*_InteractTextureAtr.w,_InteractTextureAtr.z,1) * _NoiseAmp1;
						// _NoiseAmp2 = clamp(texInteract.a,0.9,1) * _NoiseAmp2;
						//_Altitude1 = clamp(texInteract.a,0.1,1)*1001 + _Altitude1;
						_NoiseAmp2 = _NoiseAmp2 * clamp(texInteract.a*_InteractTextureAtr.w, _InteractTextureAtr.y, 1);

						//float rand2 = UVRandom(uv + s + 0.5);
						// float n = SampleNoise(pos+ float3(rand2, cos(_Time.y)*rand2,2*cos(_Time.y+0.2)*rand2  ));
						float n = SampleNoise(pos, _Altitude1, _NoiseAmp1, texInteract.a);//float n = SampleNoise(pos,_Altitude1,_NoiseAmp1*clamp(texInteract.a*_InteractTextureAtr.w,_InteractTextureAtr.x,1));//float n = SampleNoise(pos,_Altitude1,_NoiseAmp1);// //v3.5.3
						if (n > 0)
						{
							float density = n * stride;
							float rand = UVRandom(uv + s + 1);
							//float scatter = density * _Scatter * hg * MarchLight(pos, rand * 0.5);
							// float scatter = density * _Scatter * hg * MarchLight(pos + float3(rand, cos(_Time.y)*rand,3*cos(_Time.y+0.1)*rand  ), rand * 0.1*cos(_Time.y+0.1));
							//  float scatter = density * _Scatter * hg * MarchLight(pos + float3(rand, 1*cos(5*_Time.y+0.1)*rand,2*cos(7*_Time.y+0.2)*rand  ), rand * 0.15*cos(2*_Time.y+0.11));

							float scatter = 0.1;// density * _Scatter * hg * MarchLight(pos, rand * 0.001,_Altitude1,_NoiseAmp1,texInteract.a); //v4.0

							//_LocalLightPos
							// acc += _LightColor0 * scatter * BeerPowder(depth) *_SkyTint  
							//	+  BeerPowder(depth) * scatter * intensityMod / pow(length(_LocalLightPos.xyz - pos), _LocalLightColor.w);//v2.1.19

							acc += _LightColor0 * scatter* BeerPowder(depth) + BeerPowder(depth) * scatter;//v2.1.19

							depth += density;
						}
						pos += ray * stride;
					}
					acc += Beer(depth) * 1 + 1 * 1 * acc;
					acc = lerp(acc, 1 * 0.96, saturate(((dist0) / (_FarDist*0.5))) + 0.03);
					// float4 finalColor = float4(acc+FragColor*_SunSize*acc,1)*1;
					float4 finalColor = float4(acc, 1);

					//v4.0
					pos = i.worldPos;
					float n2 = SampleNoise(pos, _Altitude1, _NoiseAmp1, (texInteract.a)*0.0001*(1 - _InteractTextureAtr.x + 0.6));
					// float n22 = SampleNoise(pos + float3(0,2000,0),_Altitude1+1050,_NoiseAmp1,1-texInteract1.a*_InteractTextureAtr.x);
					//  float n33 = SampleNoise(pos + float3(0,0,0),_Altitude1+0,_NoiseAmp1,1-texInteract1.a*_InteractTextureAtr.x);

					float n22 = SampleNoise(pos + float3(0, 2000, 0), _Altitude1 + 1050, _NoiseAmp1, 1);
					float n33 = SampleNoise(pos + float3(0, 0, 0), _Altitude1 + 0, _NoiseAmp1, 1);
					// return float4(float3(0,0,0),  texInteract1.a +(_InteractTextureAtr.x-0.4)   + n2*111 + n22*111);

					// return float4(texInteract1);
					//shaper = texInteract1.a; 
					//clip(texInteract1.a  - _Cutoff);
					//clip(0.001*(1-texInteract1.a) - _Cutoff+0.4);
					// clip( (texInteract1.a) - _Cutoff );

					//v4.0
					//clip(shaper*cloudTexture1.a - _Cutoff+0.4);
					//clip((texInteract.a)*0.0001 - _Cutoff+0.4 );
					//clip((texInteract.a)*0.0001 - _Cutoff+0.4   - (1-n2*0.00008)*0.00003 );
					// clip((texInteract.a)*0.0001*(1-_InteractTextureAtr.x+0.6) - _Cutoff+0.4   - (1-n2*0.00008)*0.00003 );
					//clip((texInteract.a)*0.0001*(1-_InteractTextureAtr.x+0.6) - _Cutoff+0.4   - (n2*11110.00008)*10.3 );
					//clip( (texInteract.a)*0.0001-  _Cutoff+0.4   - (1-((n33+n22)*11110.00008)*10.3) );
					//clip(   - ((1111  + ((1-texInteract.a)*1)*151111*(1-_InteractTextureAtr.x)   )-(n33+n22)*111100)         -  _Cutoff+0.4 );
					clip(-((1111 + ((1 - texInteract.a) * 1) * 31111 * (1 - _InteractTextureAtr.x)) - (n22 + n22) * 18100) - _Cutoff + 0.4);//v5.0c URP - v5.0.8 SRP
					SHADOW_CASTER_FRAGMENT(i)
				}
				else { //FULL VOLUMETRIC CLOUDS MODE = 2
					float3 woeldPos = pos + cameraWSOffset;// IN.WorldSpacePosition + _WorldSpaceCameraPos; // MAKE CAMERA RELATIVE !!! for HDRP
					float change_h = _CutHeight;
					float PosDiff = 0.0006*(woeldPos.y - change_h); //float PosDiff = 0.0006*(i.worldPos.y - change_h);
					float2 UVs = _Density * float2(woeldPos.x, woeldPos.z);
					float4 TimingF = 0.0012;
					float2 UVs1 = _Velocity1 * TimingF*_Time.y + UVs;

					/*float4 cloudTexture = SAMPLE_TEXTURE2D(_WeatherMap, sampler_WeatherMap, UVs1);
					float4 cloudTexture1 = SAMPLE_TEXTURE2D(_WeatherMap, sampler_WeatherMap, UVs1);
					float4 paintTexture1 = SAMPLE_TEXTURE2D(_PaintMap, sampler_PaintMap, UVs1);*/
					float4 cloudTexture = tex2D(_WeatherMap, UVs1);
					float4 cloudTexture1 = tex2D(_WeatherMap, UVs1);
					float4 paintTexture1 = tex2D(_PaintMap, UVs1);
					
					float2 UVs2 = (_Velocity2*TimingF*_Time.y / 1.4 + float2(_EdgeFactors.x, _EdgeFactors.y) + UVs);

					/*float4 Texture1 = SAMPLE_TEXTURE2D(_WeatherMap, sampler_WeatherMap, UVs2);
					float4 Texture2 = SAMPLE_TEXTURE2D(_WeatherMap, sampler_WeatherMap, UVs2);*/
					float4 Texture1 = tex2D(_WeatherMap, UVs2);
					float4 Texture2 = tex2D(_WeatherMap, UVs2);

					//FULL VOLUME
					cloudTexture.rgb = sampleWeather(1 * float3(woeldPos.x, 0, woeldPos.z));
					cloudTexture1.rgb = sampleWeather(1 * float3(woeldPos.x, 0, woeldPos.z));
					Texture1.rgb = sampleWeather(1 * float3(woeldPos.x, 0, woeldPos.z));
					Texture2.rgb = sampleWeather(1 * float3(woeldPos.x, 0, woeldPos.z));
					Texture1.a = Texture1.r + Texture1.g * 1 + Texture1.b * 0;
					Texture2.a = Texture2.r + Texture2.g * 1 + Texture2.b * 0;
					cloudTexture.a = cloudTexture.r + cloudTexture.g * 1 + cloudTexture.b * 0;
					cloudTexture1.a = cloudTexture1.r + cloudTexture1.g * 1 + cloudTexture1.b * 0;

					float DER = woeldPos.y*0.001;
					float3 normalA = (((DER*((_Coverage + _CoverageOffset) + ((cloudTexture.rgb * 2) - 1))) - (1 - (Texture1.rgb * 2))));
					float3 normalN = normalize(normalA);

					float DER1 = -(woeldPos.y - 0) * 1;
					float PosTDiff = woeldPos.y * 1;
					if (woeldPos.y > change_h) {
						DER1 = (1 - cloudTexture1.a) *  PosTDiff;
					}
					float shaper = ((_Transparency + _TranspOffset) - 0.48 + 0.0)*((DER1*(((_Coverage + _CoverageOffset) + cloudTexture1.a)))*Texture2.a);
					change_h = 10;
					PosDiff = Thickness * 0.0006*(woeldPos.y - change_h);
					PosTDiff = woeldPos.y*PosDiff;
					DER1 = -(woeldPos.y + 0)*PosDiff;
					if (woeldPos.y > change_h) {
						DER1 = (1 - cloudTexture1.a) *  PosTDiff;
					}
					if (woeldPos.y > 150) { 																																///////////////////////////// 650
						DER1 = (1 - cloudTexture1.a) *  PosTDiff*0.07;
						shaper = shaper * shaper;
					}

					shaper = 1.5 *(Texture2.r) - 1.5*Texture2.b - 1.5*Texture2.g;
					//clip(1 - (((shaper) * 1) - 0.45 + 0.4));
					clip( (((shaper) * 1) - 0.45 + 0.4));
					//surface.AlphaClipThreshold = 1 - (((shaper) * 1) - 0.45 + 0.4);
					//surface.Alpha = 0;// _Cutoff + 0.4;// -_Cutoff + 0.4;
					SHADOW_CASTER_FRAGMENT(i)
				}

            }
            ENDCG
        }



       
    }
    //FallBack "Diffuse"
   // Fallback "Transparent/VertexLit"
     Fallback "Transparent/Cutout/VertexLit"
}