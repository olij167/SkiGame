// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'
// Upgrade NOTE: replaced '_World2Object' with 'unity_WorldToObject'
// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "SkyMaster/DropletsSMv35a" {
Properties {
	_BumpAmt  ("Distortion", range (0,300)) = 10
	_BumpAmt2  ("Distortion2", range (0,1300)) = 0.01
	_BumpAmt3  ("Distortion3", range (0,2)) = 0.01
	_MainTex ("Tint Color (RGB)", 2D) = "white" {}
	_BumpMap ("Normalmap", 2D) = "bump" {}	
	_BumpMapWindow ("Normalmap 2", 2D) = "bump" {}	
	_Drops ("Water Drops", 2D) = "white" {}
	//_Drops2 ("Water Drops", 2D) = "white" {}
	_Speed ("Speed", Float) = 6
	_Speed2 ("Slider Time", Float) = 3
	_WaterAmount ("Water intensity", Float) = 1
	_FreezeFacrorA ("Freeze outwards1 (1,1,0)", range (-1,1)) = 0
	_FreezeFacrorB ("Freeze outwards2 (1,0,0 = uniform)", range (-1,1)) = 0
	_FreezeFacrorC ("Freeze inwards (0,0,1)", range (-1,1)) = 0

	//v3.5
	_TransparencyAmount ("Color Amount", range (1,101)) = 3
	_Transparency2Amount ("Bump Shift Amount", range (0,5)) = 0.5
	_TopFade ("Top Fade", range (0,5)) = 0.5
	_Bloom ("_Bloom", range (0,0.12)) = 0
	_sunLightPower ("_sunLightPower", range (-1,2)) = 0.1


	//v4.0
//	_CausticTex ("Caustic", 2D) = "white" {}
//		_waterColour ("Colour", Color) = (1,1,1,1)
//		_waterPeriod ("Period", Range(0,50)) = 1
//		_waterMagnitude ("Magnitude", Range(0,0.05)) = 0.05
//		_offset ("offset", Range(0,10)) = 1
		  _SpecColor1 ("Specular Material Color", Color) = (1,1,1,1) 
      _Shininess ("Shininess", Float) = 10
       _Color ("Diffuse Material Color", Color) = (1,1,1,1) 
       _specularEnchance ("_specularEnchance", Float) = 7
}

Category {

	Tags { "Queue"="Transparent" "RenderType"="Opaque" "IgnoreProjector"="True"}

	SubShader {

		GrabPass {
			Name "BASE"
			Tags { "LightMode" = "Always" }
		}		

		Pass {
			Name "BASE"
			Tags { "LightMode" = "Always" }
			
CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_fog
#include "UnityCG.cginc"
#include "Lighting.cginc"

struct appdata_t {
	float4 vertex : POSITION;
	float2 texcoord: TEXCOORD0;
	 float3 normal : NORMAL; //v4.0
};

struct v2f {
	float4 vertex : SV_POSITION;
	float4 uvgrab : TEXCOORD0;
	float2 uvbump : TEXCOORD1;
	float2 uvmain : TEXCOORD2;
	float2 uvDrops : TEXCOORD3;	
	  float3 worldPos : TEXCOORD4;
	//float2 uvDrops2 : TEXCOORD4;	
	 float4 col : COLOR;//v4.0
	UNITY_FOG_COORDS(5)
};

float _BumpAmt;
float _BumpAmt2;
float _BumpAmt3;
float4 _BumpMap_ST;

float4 _BumpMapWindow_ST;

float4 _MainTex_ST;
float4 _Drops_ST;

sampler2D _Drops;

//float4 _Drops2_ST;

//sampler2D _Drops2;

uniform float _Speed;
uniform float _Speed2;
uniform float _WaterAmount;
uniform float _FreezeFacrorA;
uniform float _FreezeFacrorB;
uniform float _FreezeFacrorC;

//v3.5
float _TransparencyAmount;
float _Transparency2Amount;
float _TopFade;
float _Bloom;


//v4.0
float _specularEnchance;
float _sunLightPower;
//sampler2D _CausticTex;
//			
//			fixed4 _waterColour;
//			float  _waterPeriod;
//			float  _waterMagnitude;
//			float  _offset;

//float2 sinusoid (float2 x, float2 m, float2 M, float2 p) {
//	 float2 e   = M - m;
//	 float2 c = 3.1415 * 2.0 / p;
//	 return e / 2.0 * (1.0 + sin(x * c)) + m;
//}
 uniform float4 _Color; 
         uniform float4 _SpecColor1; 
         uniform float _Shininess;


v2f vert (appdata_t v)
{
	v2f o;
	o.vertex = UnityObjectToClipPos(v.vertex);

	//v4.0
	//o.pos = UnityObjectToClipPos(vertex);
    o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
//    half3 wNormal = UnityObjectToWorldNormal(normal);
//                half3 wTangent = UnityObjectToWorldDir(tangent.xyz);
//                half tangentSign = tangent.w * unity_WorldTransformParams.w;
//                half3 wBitangent = cross(wNormal, wTangent) * tangentSign;
//                o.tspace0 = half3(wTangent.x, wBitangent.x, wNormal.x);
//                o.tspace1 = half3(wTangent.y, wBitangent.y, wNormal.y);
//                o.tspace2 = half3(wTangent.z, wBitangent.z, wNormal.z);



	#if UNITY_UV_STARTS_AT_TOP
	float scale = -1.0;
	#else
	float scale = 1.0;
	#endif
	o.uvgrab.xy = (float2(o.vertex.x, o.vertex.y*scale) + o.vertex.w) * 0.5;
	o.uvgrab.zw = o.vertex.zw;
	o.uvbump = TRANSFORM_TEX( v.texcoord, _BumpMap );
	o.uvmain = TRANSFORM_TEX( v.texcoord, _MainTex );
	o.uvDrops = TRANSFORM_TEX( v.texcoord, _Drops );
	//o.uvDrops2 = TRANSFORM_TEX( v.texcoord, _Drops2 );
	UNITY_TRANSFER_FOG(o,o.vertex);





	////v4.0
	//https://en.wikibooks.org/wiki/Cg_Programming/Unity/Specular_Highlights
	 float4x4 modelMatrix = unity_ObjectToWorld;
            float3x3 modelMatrixInverse = unity_WorldToObject;
            float3 normalDirection = normalize(
               mul(v.normal, modelMatrixInverse));
            float3 viewDirection = normalize(_WorldSpaceCameraPos 
               - mul(modelMatrix, v.vertex).xyz);
            float3 lightDirection;
            float attenuation;
 
            if (0.0 == _WorldSpaceLightPos0.w) // directional light?
            {
               attenuation = 1.0; // no attenuation
               lightDirection = normalize(_WorldSpaceLightPos0.xyz);
            } 
            else // point or spot light
            {
               float3 vertexToLightSource = _WorldSpaceLightPos0.xyz
                  - mul(modelMatrix, v.vertex).xyz;
               float distance = length(vertexToLightSource);
               attenuation = 1.0 / distance; // linear attenuation 
               lightDirection = normalize(vertexToLightSource);
            }
 
            float3 ambientLighting = 
               UNITY_LIGHTMODEL_AMBIENT.rgb * _Color.rgb;
 
            float3 diffuseReflection = 
               attenuation * pow(_LightColor0,_sunLightPower).rgb * _Color.rgb
               * max(0.0, dot(normalDirection, lightDirection));
 
            float3 specularReflection;
            if (dot(normalDirection, lightDirection) < 0.0) 
               // light source on the wrong side?
            {
               specularReflection = float3(0.0, 0.0, 0.0); 
                  // no specular reflection
            }
            else // light source on the right side
            {
               specularReflection = attenuation * pow(_LightColor0,_sunLightPower).rgb 
                  * _SpecColor1.rgb * pow(max(0.0, dot(
                  reflect(-lightDirection, normalDirection), 
                  viewDirection)), _Shininess);
            }
 
            o.col = float4(ambientLighting + diffuseReflection 
               + specularReflection, 1.0);
            o.vertex = UnityObjectToClipPos(v.vertex);
           // return output;

	///END v4.0



	return o;
}

sampler2D _GrabTexture;
float4 _GrabTexture_TexelSize;
sampler2D _BumpMap;
sampler2D _BumpMapWindow;
sampler2D _MainTex;

half4 frag (v2f i) : SV_Target
{	
	half4 Wmotion = tex2D(_Drops, float2(i.uvDrops.x,(_Speed*_Time.x)+i.uvDrops.y))*cos(_Time.x)*0.05;
	half4 Wmotion2 = tex2D(_Drops, float2(i.uvDrops.x,(_Speed2*_Time.x)+i.uvDrops.y))*cos(_Time.x)*0.01;

	half4 tint = tex2D(_MainTex, i.uvmain);

		//v3.5
	half3 bump2 = UnpackNormal(tex2D( _BumpMapWindow, i.uvmain));


	//v4.0
	//https://docs.unity3d.com/Manual/SL-VertexFragmentShaderExamples.html
//	 half3 worldNormal;
//                worldNormal.x = dot(i.tspace0, tnormal);
//                worldNormal.y = dot(i.tspace1, tnormal);
//                worldNormal.z = dot(i.tspace2, tnormal);
//half3 worldViewDir = normalize(UnityWorldSpaceViewDir(i.worldPos));
//                half3 worldRefl = reflect(-worldViewDir, bump2);
//                half4 skyData = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, worldRefl);
//                half3 skyColor = DecodeHDR (skyData, unity_SpecCube0_HDR);     


	
	//	half2 bump = UnpackNormal(tex2D( _BumpMap, float2(i.uvDrops.x,(_Speed*_Time.x)+i.uvDrops.y) )).rg ;
	half2 bump = UnpackNormal(tex2D( _BumpMap, float2(i.uvDrops.x,(_Speed*_Time.x)+i.uvDrops.y) )).rg  *  (_FreezeFacrorA*(1-(_FreezeFacrorB*(tint.r*_Transparency2Amount))) + ((_FreezeFacrorC*(tint.r*_Transparency2Amount))) );

	//v3.5
	//if(_TopFade >0.005){
		_WaterAmount = _WaterAmount + (_TopFade * i.uvgrab.y*10);
	//}


	float2 offset = bump * _BumpAmt * _GrabTexture_TexelSize.xy + bump2*_BumpAmt2*0.01;
	i.uvgrab.xy = offset * i.uvgrab.z + i.uvgrab.xy;

	//v3.5
	//_WaterAmount = _WaterAmount * _TopFade * 0;
	//i.uvgrab = i.uvgrab* _TopFade * 0;


	//v4.0
//	fixed4 noise = tex2D(_BumpMap, i.uvDrops);
//	//fixed4 tint = tex2D(_MainTex, i.texcoord);	 
//	float time = _Time[1];	 
//	float2 waterDisplacement =
//	sinusoid
//	(
//	 float2 (time, time) + (noise.xy) * _offset,
//	 float2(-_waterMagnitude, -_waterMagnitude),
//	 float2(+_waterMagnitude, +_waterMagnitude),
//	 float2(_waterPeriod, _waterPeriod)
//	);	 
//	i.uvgrab.xy += waterDisplacement;
//	fixed4 causticColour = tex2D(_CausticTex, i.uvDrops.xy*0.25 + waterDisplacement*5);



	//half4 col = tex2Dproj( _GrabTexture, UNITY_PROJ_COORD(i.uvgrab)-float4(Wmotion.r+Wmotion2.r,Wmotion.r+Wmotion2.r,0,0));
	half4 col = tex2Dproj( _GrabTexture, UNITY_PROJ_COORD(i.uvgrab)-(_WaterAmount*float4(Wmotion.r+Wmotion2.r,Wmotion.r+Wmotion2.r,0,0)  ) );
	
	col += tint/_TransparencyAmount;

	//4.0
	col += bump2.r * col * _BumpAmt3 ;// + float4(skyColor,1)*0.1;
	//col = col * _waterColour * causticColour;
	float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
	col = col * pow(_LightColor0,_sunLightPower) + 0.4*i.col * (dot(lightDirection,offset*_specularEnchance)+2);






	//	col *= 1;//tint ;//(1-tint.r);

	//v3.5
	if(_Bloom >0.005){
		col = col * _Bloom * clamp(i.uvgrab.y+11.1,7,11);
	}

	UNITY_APPLY_FOG(i.fogCoord, col);
	return col*1.0;
}
ENDCG
		}
	}

	// Fallback 
	SubShader {
		Blend DstColor Zero
		Pass {
			Name "BASE"
			SetTexture [_MainTex] {	combine texture }
		}
	}
}
}