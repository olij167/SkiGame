Shader "Sky/AtmosphereImageEffect"
{
	Properties
	{
		_MainTex("Base (RGB)", 2D) = "white" {}
	}
		SubShader
	{
		Pass
		{
		//Blend SrcAlpha OneMinusSrcAlpha

		Blend SrcAlpha OneMinusSrcAlpha // Traditional transparency
		//Blend One OneMinusSrcAlpha // Premultiplied transparency
		//Blend One One // Additive
		//Blend OneMinusDstColor One // Soft Additive
		//Blend DstColor Zero // Multiplicative
		//Blend DstColor SrcColor // 2x Multiplicative

		ZTest Off Cull Off ZWrite Off Fog { Mode Off }

		CGPROGRAM
		#pragma vertex vert
		#pragma fragment frag
		#include "UnityCG.cginc"
		#include "Lighting.cginc"//v5.2.2
		#pragma target 4.0
		#include "Atmosphere.cginc"
		#pragma multi_compile_instancing //v5.2.2 VR

		//v5.2.2 - VR
		//view/projection matrices proved by VolumetricSphere.cs (in none vr, only left eye is used)
		//https://github.com/chriscummings100/worldspaceposteffect/blob/master/Assets/WorldSpacePostEffect/WorldSpacePostEffect.shader
		float4x4 _LeftWorldFromView;
		float4x4 _RightWorldFromView;
		float4x4 _LeftViewFromScreen;
		float4x4 _RightViewFromScreen;
		uniform float4 _MainTex_TexelSize;

			sampler2D _MainTex;

			struct appdata {
				float4 vertex : POSITION;
				float3 texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			float imageFXBlend = 1;

			struct v2f
			{
				float4 pos : SV_POSITION;
				float4 uv : TEXCOORD0;
				//float3 ray : TEXCOORD1;
				//v5.2.2
				float2 uv_depth : TEXCOORD1;
				UNITY_VERTEX_OUTPUT_STEREO
			};

			v2f vert(appdata v)
			{
				v2f o;

				//v5.2.2
				UNITY_SETUP_INSTANCE_ID(v); //Insert
				UNITY_INITIALIZE_OUTPUT(v2f, o); //Insert
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); //Insert

				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = ComputeScreenPos(o.pos);


				//v5.2.2 - VR
				o.uv_depth = MultiplyUV(UNITY_MATRIX_TEXTURE0, v.texcoord);

#if UNITY_UV_STARTS_AT_TOP
				if (_MainTex_TexelSize.y < 0) {
					o.uv_depth.y = 1 - o.uv_depth.y;
				}
#endif	

				/*float4x4 proj, eyeToWorld;
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
				o.ray = mul(eyeToWorld, o.ray);*/


				//o.uv.y = 1-o.uv.y;
				//o.ray.xyz = mul(UNITY_MATRIX_MV, v.vertex).xyz * float3(-1.0, -1.0, 1.0);
				return o;
			}

			sampler2D _CameraDepthTexture;
			sampler2D _CameraDepthNormalsTexture;
			//	float4x4 _ViewProjectInverse;
				float4x4 _CameraInv;
				//float4x4 _ViewMatrix;
				// float4x4 _CameraToWorld;
				float4 _CamScreenDir;
				float4 _LightDir;
				float4 _LightColor;
				//	float4x4 unity_WorldToLight;
					float _FarPlane;
					int invertY; //v5.2.1

					half4 frag(v2f i) : COLOR
					{

						//v5.2.2 - VR
						UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); //Insert
						float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, UnityStereoTransformScreenSpaceTex(i.uv));
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
						float3 rayA = (wsPos - _WorldSpaceCameraPos) / length(wsPos - _WorldSpaceCameraPos);
						//float4 wsDir = specialFX.x * float4(ray, 1);// dpth * i.interpolatedRay; //dpth * i.interpolatedRay;
						//float4 wsDir = depth * float4(ray, 1);

						//			float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
						//			depth = Linear01Depth(depth);



						float4 normal = tex2D(_CameraDepthNormalsTexture, i.uv);
						half3 ray = float3(i.uv.x * 2 - 1, i.uv.y * 2 - 1, 1);

						ray *= _CamScreenDir.xyz;
						ray = ray * (_FarPlane / ray.z);
						float3 viewNorm = mul(_CameraInv,normal);
						float4 vpos = float4(rayA * depth,0.995);

						//v5.2.2
					//	float3 wpos = mul (unity_CameraToWorld, vpos).xyz;  
						float3 wpos = wsPos;


						float4 surfaceColor = tex2D(_MainTex, i.uv);

						//v5.2.1 - 0.9
						if (invertY == 1) {
							surfaceColor = tex2D(_MainTex, float2(i.uv.x, 1 - i.uv.y));
						}

						float3 viewDir = normalize(wpos - _WorldSpaceCameraPos.xyz);

						float3 attenuation;
						float irradianceFactor = 0;
						float3 inscat = GetInscatteredLight(wpos,viewDir,attenuation,irradianceFactor);
						//float3 reflected = GetReflectedLight(wpos, depth,attenuation,irradianceFactor, normal,surfaceColor);
						float3 reflected = GetReflectedLight(wpos, depth, clamp(attenuation, 0.8, 1), irradianceFactor, normal, surfaceColor); // v1.0.83  v5.2.1 - 0.9

						//return float4(reflected, 1);
						//return saturate(float4(inscat + reflected,imageFXBlend));
						//return saturate(float4(inscat + reflected + surfaceColor.rgb*2, imageFXBlend * (1-surfaceColor.r)));
						return saturate(float4(inscat + reflected, imageFXBlend));
					}
					ENDCG
				}
	}
}
