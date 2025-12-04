// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "SkyMaster/iOS_Depth_Grab"
{
	SubShader
	{
		// No culling or depth
		ZTest Always
		ZWrite On

		

		// Writes to a single-component texture (TextureFormat.Depth)
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"
			#include "Lighting.cginc"
			#include "AutoLight.cginc"
			#include "./WaterIncludeSM30_iOS.cginc"

			UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture); //v4.9.4


			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};
			struct v2f
			{
				fixed2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};
			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}
			//sampler2D _CameraDepthTexture;
			#if 1==0 && !defined(SHADER_API_D3D9) && !defined(SHADER_API_D3D11_9X)
				float frag(v2f i) : SV_Depth
				{
					fixed4 col = tex2D(_CameraDepthTexture, i.uv);

					//float depth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, i.texcoordStereo));
					//half depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, UNITY_PROJ_COORD(i.uv));//_camDepthTex  -- _CameraDepthTexture - iOS
					//depth = 0.5;// LinearEyeDepth(depth);
					//return float4(depth, depth, depth, 0);

				return col.r;
				}
			#else
				void frag(v2f i, out float4 dummycol:COLOR, out float depth : DEPTH)
				{
					fixed4 col = tex2D(_CameraDepthTexture, i.uv);
					dummycol = col;
					depth = col.r;
					//return 0.5;// col.r;
				}
			#endif
			ENDCG
		}
	}
}