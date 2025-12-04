// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "SkyMaster/snowRainVERTEX_SM_VORTEX" {
    Properties {
		_MainTex ("Base (RGB)", 2D) = "white" {}
		alpha ("texture alpha multiplier", Float) = 0.1
		alphaPow("texture alpha power", Float) = 1

		//v0.7
		vortexControl("vortex Controls (rotation power, speed, thickness, cutoff distance", Vector) = (30,1,1,1)
		vortexPosRadius("vortex Center Position", Vector) = (0,0,0,1)
    }
	SubShader {
   		Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
		ZWrite Off
		Cull Off
		Blend SrcAlpha OneMinusSrcAlpha // alpha blending
		
        Pass {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
 			#pragma target 3.0
 			
 			#include "UnityCG.cginc"

            uniform sampler2D _MainTex;
			float4 _MainTex_ST;

			float alpha;
			float alphaPow;


			//v0.7
			float4 vortexControl;
			float4 vortexPosRadius;// = float4(0, 0, 0, 1000);
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


 			struct appdata_custom {
				float4 vertex : POSITION;
				fixed4 color : COLOR;
				float2 texcoord : TEXCOORD0;
			};

 			struct v2f {
 				float4 pos:SV_POSITION;
				float2 uv:TEXCOORD0;
				fixed4 color:COLOR;
 			};
 			
 			float4x4 _PrevInvMatrix;
			float3   _TargetPosition;
			float    _Range;
			float    _RangeR;
			float    _Size;
			float3   _MoveTotal;
			float3   _CamUp;
			float3    _MoveR;
			float isRain;
			float rainLength;
   
            v2f vert(appdata_custom v)
            {
				v2f o;

				float3 mv = v.vertex.xyz;

				mv += _MoveTotal;

				float3 target = _TargetPosition;
				float3 trip;
				
				trip = floor( ((target - mv)*_RangeR + 1) * 0.5 );
				trip *= (_Range * 2);
				mv += trip;

				o.color = v.color;//v0.1

				if(isRain==1){
					_CamUp.xyz = float3(-_MoveR.x,1,-_MoveR.z);					
				}

				//if(0==0){
					float3 diff = _CamUp * _Size;
					float3 finalposition;
					float3 tv0 = mv;
					if(isRain==0){
						tv0.x += sin(mv.x*0.2) * sin(mv.y*0.3) * sin(mv.x*0.9) * sin(mv.y*0.8);
						tv0.z += sin(mv.x*0.1) * sin(mv.y*0.2) * sin(mv.x*0.8) * sin(mv.y*1.2);
					}else{
						//_Size = _Size*12;
						diff = diff * 4 * rainLength;
					}
					
					{
						float3 eyeVector = ObjSpaceViewDir(float4(tv0, 0));
						float3 sideVector = normalize(cross(eyeVector,diff));


						tv0 += (v.texcoord.x-0.5f)*sideVector * _Size;
						tv0 += (v.texcoord.y-0.5f)*diff;
						finalposition = tv0;
					}


					//v0.7a - VORTEX
					float distanceToVortexCenter = length(vortexPosRadius.xz - finalposition.xz);
					float3x3 rotator = rotationMatrix(float3(0, 1, 0), 1 * ((_Time.y * 0.5 * vortexControl.y) - vortexControl.x * 700 * (distanceToVortexCenter / 10000000)));
					float3 posVertexA = float3(finalposition.x, finalposition.y, finalposition.z);
					posVertexA = posVertexA - vortexPosRadius;
					float3 posVertex = mul(posVertexA, rotator);
					posVertex = posVertex + vortexPosRadius;
					//posVertex.y = finalposition.y;
					if (distanceToVortexCenter <  vortexControl.w) {
						finalposition = posVertex*rainLength;
					}

            				    
					o.uv = MultiplyUV(UNITY_MATRIX_TEXTURE0, v.texcoord);
					o.pos = UnityObjectToClipPos( float4(finalposition,1));
				//}					

            	return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {	
				float4 tex = tex2D(_MainTex, i.uv*_MainTex_ST.xy + _MainTex_ST.zw);
				return float4(tex.rgb, pow(tex.a,alphaPow) * alpha)*5;//return i.color;
            }

            ENDCG
        }
    }
}
