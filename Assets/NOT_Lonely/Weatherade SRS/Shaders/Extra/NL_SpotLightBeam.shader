// Made with Amplify Shader Editor v1.9.3.3
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "NOT_Lonely/NL_SpotLightBeam"
{
	Properties
	{
		[HideInInspector] _EmissionColor("Emission Color", Color) = (1,1,1,1)
		[HideInInspector] _AlphaCutoff("Alpha Cutoff ", Range(0, 1)) = 0.5
		[HideInInspector]_beamDir("_beamDir", Vector) = (0,0,0,0)
		[HideInInspector]_startRadius("_startRadius", Float) = 0.2
		[HideInInspector]_endRadius("_endRadius", Float) = 1
		[HideInInspector]_length("_length", Float) = 1
		[HideInInspector]_rangeMultiplier("_rangeMultiplier", Float) = 1
		[HideInInspector]_color("_color", Color) = (1,1,1,1)
		[HideInInspector]_intensity("_intensity", Float) = 1
		[HideInInspector]_intersectionsDepthFade("_intersectionsDepthFade", Float) = 3
		[HideInInspector]_cameraFadeDistance("_cameraFadeDistance", Float) = 20
		[HideInInspector]_noiseIntensity("_noiseIntensity", Range( 0 , 1)) = 0.7558382
		[HideInInspector]_spotAngle("_spotAngle", Float) = 0
		[HideInInspector]_noiseSpeed("_noiseSpeed", Vector) = (1,1,0,0)
		[HideInInspector]_noise("noise", 2D) = "white" {}
		[HideInInspector]_noiseTiling("_noiseTiling", Float) = 1
		[HideInInspector]_maskHardness("_maskHardness", Float) = 0.01


		//_TessPhongStrength( "Tess Phong Strength", Range( 0, 1 ) ) = 0.5
		//_TessValue( "Tess Max Tessellation", Range( 1, 32 ) ) = 16
		//_TessMin( "Tess Min Distance", Float ) = 10
		//_TessMax( "Tess Max Distance", Float ) = 25
		//_TessEdgeLength ( "Tess Edge length", Range( 2, 50 ) ) = 16
		//_TessMaxDisp( "Tess Max Displacement", Float ) = 25

		[HideInInspector] _QueueOffset("_QueueOffset", Float) = 0
        [HideInInspector] _QueueControl("_QueueControl", Float) = -1

        [HideInInspector][NoScaleOffset] unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}

		[HideInInspector][ToggleOff] _ReceiveShadows("Receive Shadows", Float) = 1.0
	}

	SubShader
	{
		LOD 0

		

		Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "UniversalMaterialType"="Unlit" }

		Cull Off
		AlphaToMask Off

		

		HLSLINCLUDE
		#pragma target 4.5
		#pragma prefer_hlslcc gles
		// ensure rendering platforms toggle list is visible

		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

		#ifndef ASE_TESS_FUNCS
		#define ASE_TESS_FUNCS
		float4 FixedTess( float tessValue )
		{
			return tessValue;
		}

		float CalcDistanceTessFactor (float4 vertex, float minDist, float maxDist, float tess, float4x4 o2w, float3 cameraPos )
		{
			float3 wpos = mul(o2w,vertex).xyz;
			float dist = distance (wpos, cameraPos);
			float f = clamp(1.0 - (dist - minDist) / (maxDist - minDist), 0.01, 1.0) * tess;
			return f;
		}

		float4 CalcTriEdgeTessFactors (float3 triVertexFactors)
		{
			float4 tess;
			tess.x = 0.5 * (triVertexFactors.y + triVertexFactors.z);
			tess.y = 0.5 * (triVertexFactors.x + triVertexFactors.z);
			tess.z = 0.5 * (triVertexFactors.x + triVertexFactors.y);
			tess.w = (triVertexFactors.x + triVertexFactors.y + triVertexFactors.z) / 3.0f;
			return tess;
		}

		float CalcEdgeTessFactor (float3 wpos0, float3 wpos1, float edgeLen, float3 cameraPos, float4 scParams )
		{
			float dist = distance (0.5 * (wpos0+wpos1), cameraPos);
			float len = distance(wpos0, wpos1);
			float f = max(len * scParams.y / (edgeLen * dist), 1.0);
			return f;
		}

		float DistanceFromPlane (float3 pos, float4 plane)
		{
			float d = dot (float4(pos,1.0f), plane);
			return d;
		}

		bool WorldViewFrustumCull (float3 wpos0, float3 wpos1, float3 wpos2, float cullEps, float4 planes[6] )
		{
			float4 planeTest;
			planeTest.x = (( DistanceFromPlane(wpos0, planes[0]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos1, planes[0]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos2, planes[0]) > -cullEps) ? 1.0f : 0.0f );
			planeTest.y = (( DistanceFromPlane(wpos0, planes[1]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos1, planes[1]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos2, planes[1]) > -cullEps) ? 1.0f : 0.0f );
			planeTest.z = (( DistanceFromPlane(wpos0, planes[2]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos1, planes[2]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos2, planes[2]) > -cullEps) ? 1.0f : 0.0f );
			planeTest.w = (( DistanceFromPlane(wpos0, planes[3]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos1, planes[3]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos2, planes[3]) > -cullEps) ? 1.0f : 0.0f );
			return !all (planeTest);
		}

		float4 DistanceBasedTess( float4 v0, float4 v1, float4 v2, float tess, float minDist, float maxDist, float4x4 o2w, float3 cameraPos )
		{
			float3 f;
			f.x = CalcDistanceTessFactor (v0,minDist,maxDist,tess,o2w,cameraPos);
			f.y = CalcDistanceTessFactor (v1,minDist,maxDist,tess,o2w,cameraPos);
			f.z = CalcDistanceTessFactor (v2,minDist,maxDist,tess,o2w,cameraPos);

			return CalcTriEdgeTessFactors (f);
		}

		float4 EdgeLengthBasedTess( float4 v0, float4 v1, float4 v2, float edgeLength, float4x4 o2w, float3 cameraPos, float4 scParams )
		{
			float3 pos0 = mul(o2w,v0).xyz;
			float3 pos1 = mul(o2w,v1).xyz;
			float3 pos2 = mul(o2w,v2).xyz;
			float4 tess;
			tess.x = CalcEdgeTessFactor (pos1, pos2, edgeLength, cameraPos, scParams);
			tess.y = CalcEdgeTessFactor (pos2, pos0, edgeLength, cameraPos, scParams);
			tess.z = CalcEdgeTessFactor (pos0, pos1, edgeLength, cameraPos, scParams);
			tess.w = (tess.x + tess.y + tess.z) / 3.0f;
			return tess;
		}

		float4 EdgeLengthBasedTessCull( float4 v0, float4 v1, float4 v2, float edgeLength, float maxDisplacement, float4x4 o2w, float3 cameraPos, float4 scParams, float4 planes[6] )
		{
			float3 pos0 = mul(o2w,v0).xyz;
			float3 pos1 = mul(o2w,v1).xyz;
			float3 pos2 = mul(o2w,v2).xyz;
			float4 tess;

			if (WorldViewFrustumCull(pos0, pos1, pos2, maxDisplacement, planes))
			{
				tess = 0.0f;
			}
			else
			{
				tess.x = CalcEdgeTessFactor (pos1, pos2, edgeLength, cameraPos, scParams);
				tess.y = CalcEdgeTessFactor (pos2, pos0, edgeLength, cameraPos, scParams);
				tess.z = CalcEdgeTessFactor (pos0, pos1, edgeLength, cameraPos, scParams);
				tess.w = (tess.x + tess.y + tess.z) / 3.0f;
			}
			return tess;
		}
		#endif //ASE_TESS_FUNCS
		ENDHLSL

		
		Pass
		{
			
			Name "Forward"
			Tags { "LightMode"="UniversalForwardOnly" }

			Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
			ZWrite Off
			ZTest LEqual
			Offset 0,0
			ColorMask RGBA

			

			HLSLPROGRAM

			#pragma instancing_options renderinglayer
			#pragma multi_compile_fog
			#define ASE_FOG 1
			#define _SURFACE_TYPE_TRANSPARENT 1
			#define ASE_ABSOLUTE_VERTEX_POS 1
			#define ASE_SRP_VERSION 140010
			#define REQUIRE_DEPTH_TEXTURE 1


			#pragma shader_feature_local _RECEIVE_SHADOWS_OFF
			#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
			#pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3

			

			#pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
			#pragma multi_compile_fragment _ DEBUG_DISPLAY

			

			#pragma vertex vert
			#pragma fragment frag

			#define SHADERPASS SHADERPASS_UNLIT

			
            #if ASE_SRP_VERSION >=140007
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#endif
		

			
			#if ASE_SRP_VERSION >=140007
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
			#endif
		

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Debug/Debugging3D.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"

			#if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_FRAG_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#pragma multi_compile_instancing


			struct VertexInput
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 positionCS : SV_POSITION;
				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
					float3 positionWS : TEXCOORD0;
				#endif
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					float4 shadowCoord : TEXCOORD1;
				#endif
				#ifdef ASE_FOG
					float fogFactor : TEXCOORD2;
				#endif
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_texcoord4 : TEXCOORD4;
				float4 ase_texcoord5 : TEXCOORD5;
				float3 ase_normal : NORMAL;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
						#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			sampler2D _noise;
			UNITY_INSTANCING_BUFFER_START(NOT_LonelyNL_SpotLightBeam)
				UNITY_DEFINE_INSTANCED_PROP(float4, _color)
				UNITY_DEFINE_INSTANCED_PROP(float3, _beamDir)
				UNITY_DEFINE_INSTANCED_PROP(float2, _noiseSpeed)
				UNITY_DEFINE_INSTANCED_PROP(float, _startRadius)
				UNITY_DEFINE_INSTANCED_PROP(float, _endRadius)
				UNITY_DEFINE_INSTANCED_PROP(float, _rangeMultiplier)
				UNITY_DEFINE_INSTANCED_PROP(float, _length)
				UNITY_DEFINE_INSTANCED_PROP(float, _intensity)
				UNITY_DEFINE_INSTANCED_PROP(float, _maskHardness)
				UNITY_DEFINE_INSTANCED_PROP(float, _intersectionsDepthFade)
				UNITY_DEFINE_INSTANCED_PROP(float, _noiseTiling)
				UNITY_DEFINE_INSTANCED_PROP(float, _noiseIntensity)
				UNITY_DEFINE_INSTANCED_PROP(float, _cameraFadeDistance)
				UNITY_DEFINE_INSTANCED_PROP(float, _spotAngle)
			UNITY_INSTANCING_BUFFER_END(NOT_LonelyNL_SpotLightBeam)


			
			VertexOutput VertexFunction( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				float2 texCoord1 = v.ase_texcoord.xy * float2( 1,1 ) + float2( 0,0 );
				float _startRadius_Instance = UNITY_ACCESS_INSTANCED_PROP(NOT_LonelyNL_SpotLightBeam,_startRadius);
				float _endRadius_Instance = UNITY_ACCESS_INSTANCED_PROP(NOT_LonelyNL_SpotLightBeam,_endRadius);
				float _rangeMultiplier_Instance = UNITY_ACCESS_INSTANCED_PROP(NOT_LonelyNL_SpotLightBeam,_rangeMultiplier);
				float3 _beamDir_Instance = UNITY_ACCESS_INSTANCED_PROP(NOT_LonelyNL_SpotLightBeam,_beamDir);
				float3 worldToObjDir18 = mul( GetWorldToObjectMatrix(), float4( _beamDir_Instance, 0 ) ).xyz;
				float3 beamDirLocal241 = worldToObjDir18;
				float _length_Instance = UNITY_ACCESS_INSTANCED_PROP(NOT_LonelyNL_SpotLightBeam,_length);
				float3 vPos202 = ( ( ( (_startRadius_Instance + (texCoord1.y - 0.0) * (( _endRadius_Instance * _rangeMultiplier_Instance ) - _startRadius_Instance) / (1.0 - 0.0)) * v.normalOS ) + ( texCoord1.y * beamDirLocal241 * ( _rangeMultiplier_Instance * _length_Instance ) ) ) + v.positionOS.xyz );
				
				float3 vertexPos186 = vPos202;
				float4 ase_clipPos186 = TransformObjectToHClip((vertexPos186).xyz);
				float4 screenPos186 = ComputeScreenPos(ase_clipPos186);
				o.ase_texcoord4 = screenPos186;
				float3 customSurfaceDepth372 = vPos202;
				float customEye372 = -TransformWorldToView(TransformObjectToWorld(customSurfaceDepth372)).z;
				o.ase_texcoord3.z = customEye372;
				
				o.ase_texcoord3.xy = v.ase_texcoord.xy;
				o.ase_texcoord5 = v.positionOS;
				o.ase_normal = v.normalOS;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				o.ase_texcoord3.w = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = vPos202;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.positionOS.xyz = vertexValue;
				#else
					v.positionOS.xyz += vertexValue;
				#endif

				v.normalOS = v.normalOS;

				float3 positionWS = TransformObjectToWorld( v.positionOS.xyz );
				float4 positionCS = TransformWorldToHClip( positionWS );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
					o.positionWS = positionWS;
				#endif

				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					VertexPositionInputs vertexInput = (VertexPositionInputs)0;
					vertexInput.positionWS = positionWS;
					vertexInput.positionCS = positionCS;
					o.shadowCoord = GetShadowCoord( vertexInput );
				#endif

				#ifdef ASE_FOG
					o.fogFactor = ComputeFogFactor( positionCS.z );
				#endif

				o.positionCS = positionCS;

				return o;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 normalOS : NORMAL;
				float4 ase_texcoord : TEXCOORD0;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( VertexInput v )
			{
				VertexControl o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				o.vertex = v.positionOS;
				o.normalOS = v.normalOS;
				o.ase_texcoord = v.ase_texcoord;
				return o;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> v)
			{
				TessellationFactors o;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				o.edge[0] = tf.x; o.edge[1] = tf.y; o.edge[2] = tf.z; o.inside = tf.w;
				return o;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			VertexOutput DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				VertexInput o = (VertexInput) 0;
				o.positionOS = patch[0].vertex * bary.x + patch[1].vertex * bary.y + patch[2].vertex * bary.z;
				o.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				o.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = o.positionOS.xyz - patch[i].normalOS * (dot(o.positionOS.xyz, patch[i].normalOS) - dot(patch[i].vertex.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				o.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * o.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], o);
				return VertexFunction(o);
			}
			#else
			VertexOutput vert ( VertexInput v )
			{
				return VertexFunction( v );
			}
			#endif

			half4 frag ( VertexOutput IN
				#ifdef _WRITE_RENDERING_LAYERS
				, out float4 outRenderingLayers : SV_Target1
				#endif
				, bool ase_vface : SV_IsFrontFace ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID( IN );
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
					float3 WorldPosition = IN.positionWS;
				#endif

				float4 ShadowCoords = float4( 0, 0, 0, 0 );

				#if defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
						ShadowCoords = IN.shadowCoord;
					#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
						ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
					#endif
				#endif

				float _intensity_Instance = UNITY_ACCESS_INSTANCED_PROP(NOT_LonelyNL_SpotLightBeam,_intensity);
				float4 _color_Instance = UNITY_ACCESS_INSTANCED_PROP(NOT_LonelyNL_SpotLightBeam,_color);
				float2 texCoord184 = IN.ase_texcoord3.xy * float2( 1,1 ) + float2( 0,0 );
				float _maskHardness_Instance = UNITY_ACCESS_INSTANCED_PROP(NOT_LonelyNL_SpotLightBeam,_maskHardness);
				float4 screenPos186 = IN.ase_texcoord4;
				float4 ase_screenPosNorm186 = screenPos186 / screenPos186.w;
				ase_screenPosNorm186.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm186.z : ase_screenPosNorm186.z * 0.5 + 0.5;
				float _intersectionsDepthFade_Instance = UNITY_ACCESS_INSTANCED_PROP(NOT_LonelyNL_SpotLightBeam,_intersectionsDepthFade);
				float screenDepth186 = LinearEyeDepth(SHADERGRAPH_SAMPLE_SCENE_DEPTH( ase_screenPosNorm186.xy ),_ZBufferParams);
				float distanceDepth186 = saturate( abs( ( screenDepth186 - LinearEyeDepth( ase_screenPosNorm186.z,_ZBufferParams ) ) / ( _intersectionsDepthFade_Instance ) ) );
				float2 appendResult345 = (float2(IN.ase_texcoord5.xyz.x , IN.ase_texcoord5.xyz.y));
				float _noiseTiling_Instance = UNITY_ACCESS_INSTANCED_PROP(NOT_LonelyNL_SpotLightBeam,_noiseTiling);
				float temp_output_356_0 = ( 0.01 * _noiseTiling_Instance );
				float2 _noiseSpeed_Instance = UNITY_ACCESS_INSTANCED_PROP(NOT_LonelyNL_SpotLightBeam,_noiseSpeed);
				float2 temp_output_337_0 = ( _noiseSpeed_Instance * _TimeParameters.x );
				float _noiseIntensity_Instance = UNITY_ACCESS_INSTANCED_PROP(NOT_LonelyNL_SpotLightBeam,_noiseIntensity);
				float lerpResult365 = lerp( 1.0 , ( tex2D( _noise, ( ( appendResult345 * temp_output_356_0 ) + frac( temp_output_337_0 ) ) ).r * tex2D( _noise, ( ( appendResult345 * ( temp_output_356_0 * 0.5 ) ) + frac( ( temp_output_337_0 * -0.3 ) ) ) ).r ) , _noiseIntensity_Instance);
				float _cameraFadeDistance_Instance = UNITY_ACCESS_INSTANCED_PROP(NOT_LonelyNL_SpotLightBeam,_cameraFadeDistance);
				float customEye372 = IN.ase_texcoord3.z;
				float cameraDepthFade372 = (( customEye372 -_ProjectionParams.y - 0.0 ) / 1.0);
				float temp_output_443_0 = ( cameraDepthFade372 / abs( _cameraFadeDistance_Instance ) );
				float3 ase_worldViewDir = ( _WorldSpaceCameraPos.xyz - WorldPosition );
				ase_worldViewDir = normalize(ase_worldViewDir);
				float3 _beamDir_Instance = UNITY_ACCESS_INSTANCED_PROP(NOT_LonelyNL_SpotLightBeam,_beamDir);
				float3 worldToObjDir18 = mul( GetWorldToObjectMatrix(), float4( _beamDir_Instance, 0 ) ).xyz;
				float3 beamDirLocal241 = worldToObjDir18;
				float _spotAngle_Instance = UNITY_ACCESS_INSTANCED_PROP(NOT_LonelyNL_SpotLightBeam,_spotAngle);
				float3 lerpResult220 = lerp( IN.ase_normal , -beamDirLocal241 , (0.0 + (_spotAngle_Instance - 0.0) * (1.0 - 0.0) / (179.0 - 0.0)));
				float3 objToWorldDir233 = mul( GetObjectToWorldMatrix(), float4( ( lerpResult220 * ( ( ase_vface * 2.0 ) - 1.0 ) ), 0 ) ).xyz;
				float3 n160 = objToWorldDir233;
				float dotResult413 = dot( ase_worldViewDir , n160 );
				float opacity248 = saturate( ( ( _intensity_Instance * 0.5 * ( 1.0 - pow( abs( texCoord184.y ) , _maskHardness_Instance ) ) * distanceDepth186 * lerpResult365 * ( saturate( ( _cameraFadeDistance_Instance >= 0.0 ? ( 1.0 - temp_output_443_0 ) : temp_output_443_0 ) ) * saturate( ( cameraDepthFade372 / 0.5 ) ) ) ) * saturate( pow( dotResult413 , 2.0 ) ) ) );
				float4 color252 = ( _intensity_Instance * ( _color_Instance * max( opacity248 , 1.0 ) ) );
				
				float3 BakedAlbedo = 0;
				float3 BakedEmission = 0;
				float3 Color = color252.rgb;
				float Alpha = opacity248;
				float AlphaClipThreshold = 0.5;
				float AlphaClipThresholdShadow = 0.5;

				#ifdef _ALPHATEST_ON
					clip( Alpha - AlphaClipThreshold );
				#endif

				#if defined(_DBUFFER)
					ApplyDecalToBaseColor(IN.positionCS, Color);
				#endif

				#if defined(_ALPHAPREMULTIPLY_ON)
				Color *= Alpha;
				#endif

				#ifdef LOD_FADE_CROSSFADE
					LODFadeCrossFade( IN.positionCS );
				#endif

				#ifdef ASE_FOG
					Color = MixFog( Color, IN.fogFactor );
				#endif

				#ifdef _WRITE_RENDERING_LAYERS
					uint renderingLayers = GetMeshRenderingLayer();
					outRenderingLayers = float4( EncodeMeshRenderingLayer( renderingLayers ), 0, 0, 0 );
				#endif

				return half4( Color, Alpha );
			}
			ENDHLSL
		}

	
	}
	
	CustomEditor "UnityEditor.ShaderGraphUnlitGUI"
	FallBack "Hidden/Shader Graph/FallbackError"
	
	Fallback Off
}
/*ASEBEGIN
Version=19303
Node;AmplifyShaderEditor.CommentaryNode;206;-2500.135,810.6855;Inherit;False;2043.846;918.7817;;18;9;16;26;17;10;370;1;11;241;18;4;202;2;3;27;15;23;6;Vertex Pos;1,1,1,1;0;0
Node;AmplifyShaderEditor.Vector3Node;17;-2390.335,1304.785;Inherit;False;InstancedProperty;_beamDir;_beamDir;0;1;[HideInInspector];Create;True;0;0;0;False;0;False;0,0,0;0,-1,-1.192093E-07;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.RangedFloatNode;10;-2296.847,1113.886;Inherit;False;InstancedProperty;_endRadius;_endRadius;2;1;[HideInInspector];Create;True;0;0;0;False;0;False;1;16.5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;26;-2442.639,1509.883;Inherit;False;InstancedProperty;_rangeMultiplier;_rangeMultiplier;4;1;[HideInInspector];Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TransformDirectionNode;18;-2211.334,1303.785;Inherit;False;World;Object;False;Fast;False;1;0;FLOAT3;0,0,0;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.TextureCoordinatesNode;1;-2449.547,914.886;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;370;-2085.533,1117.725;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;16;-2291.858,1610.884;Inherit;False;InstancedProperty;_length;_length;3;1;[HideInInspector];Create;True;0;0;0;False;0;False;1;20.15;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;9;-2091.942,901.8851;Inherit;False;InstancedProperty;_startRadius;_startRadius;1;1;[HideInInspector];Create;True;0;0;0;False;0;False;0.2;0.23;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;241;-1976.041,1309.282;Inherit;False;beamDirLocal;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;27;-2117.134,1536.584;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.NormalVertexDataNode;4;-1849.144,1046.286;Inherit;False;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TFHCRemapNode;11;-1864.044,876.2855;Inherit;False;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT;0;False;4;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;6;-1651.145,964.285;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;15;-1717.134,1292.585;Inherit;False;3;3;0;FLOAT;0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.CommentaryNode;255;-2513.198,128.9834;Inherit;False;1316.357;533.9451;;12;221;220;235;233;160;226;242;227;230;433;431;428;Normals;1,1,1,1;0;0
Node;AmplifyShaderEditor.SimpleAddOpNode;23;-1456.135,978.584;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.PosVertexDataNode;2;-1491.3,1130.375;Inherit;False;0;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.CommentaryNode;250;-2552.417,-2065.626;Inherit;False;2912.591;1544.475;;56;187;369;215;368;184;185;213;186;34;183;182;367;336;355;321;366;365;343;281;345;357;356;331;330;348;333;337;340;339;338;349;346;334;347;322;256;176;46;248;371;372;373;409;410;414;411;413;417;434;437;440;441;442;443;445;446;Fade Mask;1,1,1,1;0;0
Node;AmplifyShaderEditor.GetLocalVarNode;242;-2423.239,352.8729;Inherit;False;241;beamDirLocal;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;227;-2463.198,451.9279;Inherit;False;InstancedProperty;_spotAngle;_spotAngle;10;1;[HideInInspector];Create;True;0;0;0;False;0;False;0;77.85;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.FaceVariableNode;428;-2044.482,530.2541;Inherit;False;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;3;-1284.945,982.485;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleTimeNode;333;-2377.02,-763.3397;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;357;-2512.26,-1188.631;Inherit;False;Constant;_Float5;Float 5;18;0;Create;True;0;0;0;False;0;False;0.01;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;355;-2507.978,-1108.17;Inherit;False;InstancedProperty;_noiseTiling;_noiseTiling;13;1;[HideInInspector];Create;True;0;0;0;False;0;False;1;1.2;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;336;-2367.02,-901.3397;Inherit;False;InstancedProperty;_noiseSpeed;_noiseSpeed;11;1;[HideInInspector];Create;True;0;0;0;False;0;False;1,1;0.1,-0.004;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.NormalVertexDataNode;221;-2306.359,178.9834;Inherit;False;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.NegateNode;226;-2209.78,356.9987;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.TFHCRemapNode;230;-2262.198,455.928;Inherit;False;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;179;False;3;FLOAT;0;False;4;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;431;-1926.576,530.2542;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;2;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;202;-1126.507,981.3419;Inherit;False;vPos;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;340;-2180.019,-735.0724;Inherit;False;Constant;_Float4;Float 4;18;0;Create;True;0;0;0;False;0;False;-0.3;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;337;-2162.02,-868.3397;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;331;-2361.623,-976.3566;Inherit;False;Constant;_Float3;Float 3;18;0;Create;True;0;0;0;False;0;False;0.5;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;356;-2340.26,-1152.631;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.PosVertexDataNode;281;-2352.323,-1311.628;Inherit;False;0;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.LerpOp;220;-2007.357,253.9834;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;433;-1766.934,529.2106;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;215;-2257.007,-1835.975;Inherit;False;InstancedProperty;_cameraFadeDistance;_cameraFadeDistance;8;1;[HideInInspector];Create;True;0;0;0;False;0;False;20;10.09;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;409;-2463.378,-1667.916;Inherit;False;202;vPos;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;339;-1990.118,-867.5725;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;330;-2155.524,-976.9565;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;345;-2148.27,-1286.425;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;235;-1820.578,248.039;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.CameraDepthFade;372;-2229.808,-1665.87;Inherit;False;3;2;FLOAT3;0,0,0;False;0;FLOAT;1;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.AbsOpNode;373;-1997.808,-1748.87;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.FractNode;334;-1881.917,-1258.501;Inherit;False;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;346;-1891.615,-1363.108;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.FractNode;338;-1823.519,-903.9725;Inherit;False;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;348;-1948.615,-1007.396;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TransformDirectionNode;233;-1643.466,247.4544;Inherit;False;Object;World;False;Fast;False;1;0;FLOAT3;0,0,0;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.SimpleDivideOpNode;443;-1831.158,-1689.365;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;347;-1724.842,-1312.181;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;349;-1696.714,-978.9965;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TexturePropertyNode;321;-1812.937,-1175.586;Inherit;True;Property;_noise;noise;12;1;[HideInInspector];Create;True;0;0;0;False;0;False;None;28715250e68caeb4d9f1b861866f0f99;False;white;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.RegisterLocalVarNode;160;-1420.839,251.326;Inherit;False;n;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;440;-2015.006,-1536.926;Inherit;False;Constant;_Float1;Float 1;15;0;Create;True;0;0;0;False;0;False;0.5;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;437;-1674.006,-1683.926;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;184;-1810.135,-2028.767;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;256;-1539.078,-1291.697;Inherit;True;Property;_noise_a;noise_a;15;0;Create;True;0;0;0;False;0;False;-1;None;28715250e68caeb4d9f1b861866f0f99;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;322;-1542.245,-1069.684;Inherit;True;Property;_noise_b;noise_b;15;0;Create;True;0;0;0;False;0;False;-1;None;28715250e68caeb4d9f1b861866f0f99;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;369;-1580.823,-1901.995;Inherit;False;InstancedProperty;_maskHardness;_maskHardness;14;1;[HideInInspector];Create;True;0;0;0;False;0;False;0.01;0.01;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ViewDirInputsCoordNode;414;-1163.739,-1033.547;Inherit;False;World;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.GetLocalVarNode;411;-1167.299,-864.8604;Inherit;False;160;n;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.Compare;371;-1484.945,-1719.087;Inherit;False;3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;445;-1857.158,-1590.365;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.AbsOpNode;446;-1541.866,-1986.403;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;343;-1228.052,-1158.087;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;366;-1084.687,-1333.353;Inherit;False;Constant;_Float11;Float 11;13;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.PowerNode;368;-1354.825,-1978.995;Inherit;False;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;367;-1230.161,-1258.38;Inherit;False;InstancedProperty;_noiseIntensity;_noiseIntensity;9;1;[HideInInspector];Create;True;0;0;0;False;0;False;0.7558382;1;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;187;-1417.417,-1811.703;Inherit;False;InstancedProperty;_intersectionsDepthFade;_intersectionsDepthFade;7;1;[HideInInspector];Create;True;0;0;0;False;0;False;3;2.73;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;410;-1372.008,-1879.414;Inherit;False;202;vPos;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.DotProductOpNode;413;-922.8789,-964.431;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;213;-1272.88,-1676.033;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;442;-1263.006,-1554.926;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;365;-919.6873,-1329.353;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;185;-1164.127,-1969.848;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;183;-969.9379,-1947.081;Inherit;False;Constant;_Float0;Float 0;10;0;Create;True;0;0;0;False;0;False;0.5;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;34;-2106.267,-615.1611;Inherit;False;InstancedProperty;_intensity;_intensity;6;1;[HideInInspector];Create;True;0;0;0;False;0;False;1;1.04;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.DepthFade;186;-1106.422,-1845.703;Inherit;False;True;True;True;2;1;FLOAT3;0,0,0;False;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.PowerNode;434;-753.2749,-967.9952;Inherit;False;False;2;0;FLOAT;0;False;1;FLOAT;2;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;441;-1069.006,-1614.926;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;182;-655.9155,-1571.746;Inherit;False;6;6;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;417;-561.5622,-970.7198;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;176;-358.6064,-1168.447;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;46;-204.0865,-1167.848;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;251;-2526.43,-416.8221;Inherit;False;1485.467;504.059;;7;364;31;252;243;179;32;377;Color;1,1,1,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;248;-35.57867,-1170.712;Inherit;False;opacity;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;243;-2454.895,-85.46077;Inherit;False;Constant;_Float2;Float 2;15;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;364;-2482.142,-197.1841;Inherit;False;248;opacity;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMaxOpNode;179;-2276.125,-153.4554;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;31;-2340.862,-366.8221;Inherit;False;InstancedProperty;_color;_color;5;1;[HideInInspector];Create;True;0;0;0;False;0;False;1,1,1,1;1,0.9469339,0.7877358,1;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;32;-2085.436,-165.3586;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;377;-1975.679,-370.2365;Inherit;False;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;252;-1469.301,-172.8122;Inherit;False;color;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;249;1018.379,-218.4521;Inherit;False;248;opacity;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;254;1048.751,-402.3643;Inherit;False;252;color;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;203;1024.622,-116.0118;Inherit;False;202;vPos;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;378;1426.172,-361.2155;Float;False;False;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;13;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;ExtraPrePass;0;0;ExtraPrePass;5;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;False;False;False;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Unlit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;0;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;0;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;380;1426.172,-361.2155;Float;False;False;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;13;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;ShadowCaster;0;2;ShadowCaster;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;False;False;False;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Unlit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;False;False;True;False;False;False;False;0;False;;False;False;False;False;False;False;False;False;False;True;1;False;;True;3;False;;False;True;1;LightMode=ShadowCaster;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;381;1426.172,-361.2155;Float;False;False;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;13;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;DepthOnly;0;3;DepthOnly;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;False;False;False;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Unlit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;False;False;True;False;False;False;False;0;False;;False;False;False;False;False;False;False;False;False;True;1;False;;False;False;True;1;LightMode=DepthOnly;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;382;1426.172,-361.2155;Float;False;False;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;13;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;Meta;0;4;Meta;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;False;False;False;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Unlit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=Meta;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;383;1426.172,-361.2155;Float;False;False;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;13;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;Universal2D;0;5;Universal2D;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;False;False;False;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Unlit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;0;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;1;LightMode=Universal2D;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;384;1426.172,-361.2155;Float;False;False;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;13;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;SceneSelectionPass;0;6;SceneSelectionPass;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;False;False;False;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Unlit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=SceneSelectionPass;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;385;1426.172,-361.2155;Float;False;False;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;13;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;ScenePickingPass;0;7;ScenePickingPass;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;False;False;False;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Unlit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=Picking;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;386;1426.172,-361.2155;Float;False;False;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;13;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;DepthNormals;0;8;DepthNormals;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;False;False;False;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Unlit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;False;;True;3;False;;False;True;1;LightMode=DepthNormalsOnly;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;387;1426.172,-361.2155;Float;False;False;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;13;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;DepthNormalsOnly;0;9;DepthNormalsOnly;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;False;False;False;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Unlit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;False;;True;3;False;;False;True;1;LightMode=DepthNormalsOnly;False;True;9;d3d11;metal;vulkan;xboxone;xboxseries;playstation;ps4;ps5;switch;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;379;1425.172,-361.2155;Float;False;True;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;13;NOT_Lonely/NL_SpotLightBeam;2992e84f91cbeb14eab234972e07ea9d;True;Forward;0;1;Forward;8;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;2;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;False;False;False;True;4;RenderPipeline=UniversalPipeline;RenderType=Transparent=RenderType;Queue=Transparent=Queue=0;UniversalMaterialType=Unlit;True;5;True;12;all;0;False;True;5;5;False;;10;False;;1;1;False;;10;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;2;False;;True;3;False;;True;False;0;False;;0;False;;True;1;LightMode=UniversalForwardOnly;False;False;0;;0;0;Standard;21;Surface;1;638478220942390246;  Blend;0;0;Two Sided;0;638478220911780700;Forward Only;0;0;Cast Shadows;0;638478220833995553;  Use Shadow Threshold;0;0;GPU Instancing;1;0;LOD CrossFade;0;638478220817200739;Built-in Fog;1;0;Meta Pass;0;0;Extra Pre Pass;0;0;Tessellation;0;0;  Phong;0;0;  Strength;0.5,False,;0;  Type;0;0;  Tess;16,False,;0;  Min;10,False,;0;  Max;25,False,;0;  Edge Length;16,False,;0;  Max Displacement;25,False,;0;Vertex Position,InvertActionOnDeselection;0;638478221059879661;0;10;False;True;False;False;False;False;False;False;False;False;False;;False;0
WireConnection;18;0;17;0
WireConnection;370;0;10;0
WireConnection;370;1;26;0
WireConnection;241;0;18;0
WireConnection;27;0;26;0
WireConnection;27;1;16;0
WireConnection;11;0;1;2
WireConnection;11;3;9;0
WireConnection;11;4;370;0
WireConnection;6;0;11;0
WireConnection;6;1;4;0
WireConnection;15;0;1;2
WireConnection;15;1;241;0
WireConnection;15;2;27;0
WireConnection;23;0;6;0
WireConnection;23;1;15;0
WireConnection;3;0;23;0
WireConnection;3;1;2;0
WireConnection;226;0;242;0
WireConnection;230;0;227;0
WireConnection;431;0;428;0
WireConnection;202;0;3;0
WireConnection;337;0;336;0
WireConnection;337;1;333;0
WireConnection;356;0;357;0
WireConnection;356;1;355;0
WireConnection;220;0;221;0
WireConnection;220;1;226;0
WireConnection;220;2;230;0
WireConnection;433;0;431;0
WireConnection;339;0;337;0
WireConnection;339;1;340;0
WireConnection;330;0;356;0
WireConnection;330;1;331;0
WireConnection;345;0;281;1
WireConnection;345;1;281;2
WireConnection;235;0;220;0
WireConnection;235;1;433;0
WireConnection;372;2;409;0
WireConnection;373;0;215;0
WireConnection;334;0;337;0
WireConnection;346;0;345;0
WireConnection;346;1;356;0
WireConnection;338;0;339;0
WireConnection;348;0;345;0
WireConnection;348;1;330;0
WireConnection;233;0;235;0
WireConnection;443;0;372;0
WireConnection;443;1;373;0
WireConnection;347;0;346;0
WireConnection;347;1;334;0
WireConnection;349;0;348;0
WireConnection;349;1;338;0
WireConnection;160;0;233;0
WireConnection;437;0;443;0
WireConnection;256;0;321;0
WireConnection;256;1;347;0
WireConnection;256;7;321;1
WireConnection;322;0;321;0
WireConnection;322;1;349;0
WireConnection;322;7;321;1
WireConnection;371;0;215;0
WireConnection;371;2;437;0
WireConnection;371;3;443;0
WireConnection;445;0;372;0
WireConnection;445;1;440;0
WireConnection;446;0;184;2
WireConnection;343;0;256;1
WireConnection;343;1;322;1
WireConnection;368;0;446;0
WireConnection;368;1;369;0
WireConnection;413;0;414;0
WireConnection;413;1;411;0
WireConnection;213;0;371;0
WireConnection;442;0;445;0
WireConnection;365;0;366;0
WireConnection;365;1;343;0
WireConnection;365;2;367;0
WireConnection;185;0;368;0
WireConnection;186;1;410;0
WireConnection;186;0;187;0
WireConnection;434;0;413;0
WireConnection;441;0;213;0
WireConnection;441;1;442;0
WireConnection;182;0;34;0
WireConnection;182;1;183;0
WireConnection;182;2;185;0
WireConnection;182;3;186;0
WireConnection;182;4;365;0
WireConnection;182;5;441;0
WireConnection;417;0;434;0
WireConnection;176;0;182;0
WireConnection;176;1;417;0
WireConnection;46;0;176;0
WireConnection;248;0;46;0
WireConnection;179;0;364;0
WireConnection;179;1;243;0
WireConnection;32;0;31;0
WireConnection;32;1;179;0
WireConnection;377;0;34;0
WireConnection;377;1;32;0
WireConnection;252;0;377;0
WireConnection;379;2;254;0
WireConnection;379;3;249;0
WireConnection;379;5;203;0
ASEEND*/
//CHKSM=9CBF30DD19695081A14E09B804E135540B1269E1