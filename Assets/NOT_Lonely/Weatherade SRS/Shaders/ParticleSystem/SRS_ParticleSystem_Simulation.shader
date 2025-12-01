// Made with Amplify Shader Editor v1.9.9.1
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Hidden/NOT_Lonely/Weatherade/SRS_ParticleSystem_Simulation"
{
	Properties
	{
		_SRS_particles( "_SRS_particles", 2D ) = "white" {}
		_velocityMax( "_velocityMax", Vector ) = ( 0, 1, 0, 0 )
		_velocityMin( "_velocityMin", Vector ) = ( 0, 0, 0, 0 )
		_lifetimeMinMax( "_lifetimeMinMax", Vector ) = ( 0.5, 1, 0, 0 )
		_SRS_localDepth( "_SRS_localDepth", 2D ) = "white" {}
		_swayingFrequency( "_swayingFrequency", Vector ) = ( 0, 0, 0, 0 )
		_swayingAmplitude( "_swayingAmplitude", Vector ) = ( 0, 0, 0, 0 )
		_gradientTexOLT( "gradientTexOLT", 2D ) = "black" {}
		_emissionFlag( "_emissionFlag", Float ) = 1
		_particlesPercentage( "_particlesPercentage", Float ) = 0
		[Toggle] _velCurvesRand( "velCurvesRand", Float ) = 0
		_noiseTex( "_noiseTex", 2D ) = "white" {}
		_velocityMinMultiplier( "_velocityMinMultiplier", Vector ) = ( 0, 0, 0, 0 )
		_velocityMaxMultiplier( "_velocityMaxMultiplier", Vector ) = ( 0, 0, 0, 0 )
		[Toggle] _isPrewarming( "isPrewarming", Float ) = 0
		_emitterSize( "_emitterSize", Vector ) = ( 0, 0, 0, 0 )
		_uvOffset( "_uvOffset", Vector ) = ( 0, 0, 0, 0 )
		_Teleporting( "Teleporting", Float ) = 0
		_AddPos( "AddPos", Vector ) = ( 0, 0, 0, 0 )
		[Toggle( UNITY_REVERSED_Z )] _UnityReversedZ( "UnityReversedZ", Float ) = 0
		_localDepthCamFarHeight( "_localDepthCamFarHeight", Float ) = 0
		_spawnHeightRange( "_spawnHeightRange", Float ) = -2
		[HideInInspector] _texcoord( "", 2D ) = "white" {}

	}

	SubShader
	{
		

		Tags { "RenderType"="Opaque" }

	LOD 100

		

		Blend Off
		AlphaToMask Off
		Cull Back
		ColorMask RGBA
		ZWrite On
		ZTest LEqual
		Offset 0 , 0
		

		CGINCLUDE
			#pragma target 2.0

			float4 ComputeClipSpacePosition( float2 screenPosNorm, float deviceDepth )
			{
				float4 positionCS = float4( screenPosNorm * 2.0 - 1.0, deviceDepth, 1.0 );
			#if UNITY_UV_STARTS_AT_TOP
				positionCS.y = -positionCS.y;
			#endif
				return positionCS;
			}
		ENDCG

		
		Pass
		{
			Name "Unlit"

			CGPROGRAM
				#define ASE_VERSION 19901
				#define ASE_USING_SAMPLING_MACROS 1

				#ifndef UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX
					#define UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input)
				#endif
				#pragma vertex vert
				#pragma fragment frag
				#pragma multi_compile_instancing
				#include "UnityCG.cginc"

				#include "UnityShaderVariables.cginc"
				#define ASE_NEEDS_TEXTURE_COORDINATES0
				#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
				#pragma shader_feature_local _USE_ABSMINVEL
				#pragma shader_feature_local _VEL_CURVES
				#pragma shader_feature_local _WARP_POSITIONS
				#pragma shader_feature_local _SRS_COLLISION
				#pragma shader_feature_local _SRS_LOCALDEPTH
				#pragma warning(disable : 4008)
				#if defined(SHADER_API_D3D11) || defined(SHADER_API_XBOXONE) || defined(UNITY_COMPILER_HLSLCC) || defined(SHADER_API_PSSL) || (defined(SHADER_TARGET_SURFACE_ANALYSIS) && !defined(SHADER_TARGET_SURFACE_ANALYSIS_MOJOSHADER))//ASE Sampler Macros
				#define SAMPLE_TEXTURE2D(tex,samplerTex,coord) tex.Sample(samplerTex,coord)
				#define SAMPLE_TEXTURE2D_LOD(tex,samplerTex,coord,lod) tex.SampleLevel(samplerTex,coord, lod)
				#define SAMPLE_TEXTURE2D_BIAS(tex,samplerTex,coord,bias) tex.SampleBias(samplerTex,coord,bias)
				#define SAMPLE_TEXTURE2D_GRAD(tex,samplerTex,coord,ddx,ddy) tex.SampleGrad(samplerTex,coord,ddx,ddy)
				#else//ASE Sampling Macros
				#define SAMPLE_TEXTURE2D(tex,samplerTex,coord) tex2D(tex,coord)
				#define SAMPLE_TEXTURE2D_LOD(tex,samplerTex,coord,lod) tex2Dlod(tex,float4(coord,0,lod))
				#define SAMPLE_TEXTURE2D_BIAS(tex,samplerTex,coord,bias) tex2Dbias(tex,float4(coord,0,bias))
				#define SAMPLE_TEXTURE2D_GRAD(tex,samplerTex,coord,ddx,ddy) tex2Dgrad(tex,coord,ddx,ddy)
				#endif//ASE Sampling Macros
				


				struct appdata
				{
					float4 vertex : POSITION;
					float4 ase_texcoord : TEXCOORD0;
					UNITY_VERTEX_INPUT_INSTANCE_ID
				};

				struct v2f
				{
					float4 pos : SV_POSITION;
					float4 ase_texcoord : TEXCOORD0;
					UNITY_VERTEX_INPUT_INSTANCE_ID
					UNITY_VERTEX_OUTPUT_STEREO
				};

				UNITY_DECLARE_TEX2D_NOSAMPLER(_SRS_particles);
				uniform float4 _SRS_particles_ST;
				SamplerState sampler_SRS_particles;
				uniform float _emissionFlag;
				UNITY_DECLARE_TEX2D_NOSAMPLER(_noiseTex);
				uniform float4 _noiseTex_ST;
				SamplerState sampler_noiseTex;
				uniform float _particlesPercentage;
				uniform float4x4 _emitterMatrix;
				uniform float3 _emitterSize;
				uniform float _spawnHeightRange;
				uniform float2 _srs_deltaTime;
				uniform float3 _swayingFrequency;
				uniform float3 _swayingAmplitude;
				uniform float3 _velocityMin;
				uniform float3 _velocityMax;
				uniform float _velCurvesRand;
				UNITY_DECLARE_TEX2D_NOSAMPLER(_gradientTexOLT);
				uniform float3 _velocityMinMultiplier;
				uniform float3 _velocityMaxMultiplier;
				uniform float _Teleporting;
				uniform float3 _AddPos;
				uniform float2 _lifetimeMinMax;
				uniform float4x4 _depthCamMatrix;
				uniform float _depthCamFarHeight;
				UNITY_DECLARE_TEX2D_NOSAMPLER(_SRS_depth);
				SamplerState sampler_SRS_depth;
				uniform float4x4 _snowfallDepthCamMatrix;
				uniform float _localDepthCamFarHeight;
				UNITY_DECLARE_TEX2D_NOSAMPLER(_SRS_localDepth);
				uniform float2 _uvOffset;
				SamplerState sampler_SRS_localDepth;
				uniform float _isPrewarming;


				float4 ReadPixels1_g28( int u, int v, Texture2D tex )
				{
					return tex.Load(int3(u, v, 0));
				}
				
				float4 ReadPixels1_g27( int u, int v, Texture2D tex )
				{
					return tex.Load(int3(u, v, 0));
				}
				

				v2f vert ( appdata v )
				{
					v2f o;
					UNITY_SETUP_INSTANCE_ID( v );
					UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( o );
					UNITY_TRANSFER_INSTANCE_ID( v, o );

					o.ase_texcoord.xy = v.ase_texcoord.xy;
					
					//setting value to unused interpolator channels and avoid initialization warnings
					o.ase_texcoord.zw = 0;

					float3 vertexValue = float3( 0, 0, 0 );
					#if ASE_ABSOLUTE_VERTEX_POS
						vertexValue = v.vertex.xyz;
					#endif
					vertexValue = vertexValue;
					#if ASE_ABSOLUTE_VERTEX_POS
						v.vertex.xyz = vertexValue;
					#else
						v.vertex.xyz += vertexValue;
					#endif

					o.pos = UnityObjectToClipPos( v.vertex );
					return o;
				}

				half4 frag( v2f IN  ) : SV_Target
				{
					UNITY_SETUP_INSTANCE_ID( IN );
					UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );
					half4 finalColor;

					float4 ScreenPosNorm = float4( IN.pos.xy * ( _ScreenParams.zw - 1.0 ), IN.pos.zw );
					float4 ClipPos = ComputeClipSpacePosition( ScreenPosNorm.xy, IN.pos.z ) * IN.pos.w;
					float4 ScreenPos = ComputeScreenPos( ClipPos );

					float2 uv_SRS_particles = IN.ase_texcoord.xy * _SRS_particles_ST.xy + _SRS_particles_ST.zw;
					float4 tex2DNode3 = SAMPLE_TEXTURE2D( _SRS_particles, sampler_SRS_particles, uv_SRS_particles );
					float temp_output_2_0_g22 = tex2DNode3.a;
					float temp_output_3_0_g22 = frac( temp_output_2_0_g22 );
					half remainingLifetime90 = ( ( temp_output_2_0_g22 - temp_output_3_0_g22 ) / 1000.0 );
					float3 temp_cast_0 = (0.0).xxx;
					float3 temp_output_4_0_g58 = temp_cast_0;
					float2 uv_noiseTex = IN.ase_texcoord.xy * _noiseTex_ST.xy + _noiseTex_ST.zw;
					float4 tex2DNode587 = SAMPLE_TEXTURE2D( _noiseTex, sampler_noiseTex, uv_noiseTex );
					float noiseStaticC611 = tex2DNode587.b;
					float2 texCoord46 = IN.ase_texcoord.xy * float2( 1,1 ) + float2( 0,0 );
					float2 temp_cast_1 = (0.5).xx;
					float2 break147 = ( ( texCoord46 - temp_cast_1 ) * (_emitterSize).xz );
					float mulTime597 = _Time.y * 0.1;
					float2 texCoord598 = IN.ase_texcoord.xy * float2( 1,1 ) + float2( 0,0 );
					float2 appendResult599 = (float2(( frac( mulTime597 ) + texCoord598.x ) , texCoord598.y));
					float4 tex2DNode593 = SAMPLE_TEXTURE2D( _noiseTex, sampler_noiseTex, appendResult599 );
					float noiseDynamicA600 = tex2DNode593.r;
					float randomStartPosY570 =  (_spawnHeightRange + ( noiseDynamicA600 - 0.0 ) * ( 0.0 - _spawnHeightRange ) / ( 1.0 - 0.0 ) );
					float4 appendResult70 = (float4(break147.x , randomStartPosY570 , break147.y , 1.0));
					float3 initPos413 = (mul( _emitterMatrix, appendResult70 )).xyz;
					float3 hidePos424 = float3( 0, -9999, 0 );
					float3 visibleParticles428 = ( step( noiseStaticC611 ,  (0.0 + ( ( _particlesPercentage > 0.0 ? pow( 2.0 , ( ( _particlesPercentage * 10.0 ) - 10.0 ) ) : 0.0 ) - 0.0001 ) * ( 1.0 - 0.0 ) / ( 1.0 - 0.0001 ) ) ) > 0.0 ? initPos413 : hidePos424 );
					float3 appendResult65 = (float3(tex2DNode3.r , tex2DNode3.g , tex2DNode3.b));
					float3 temp_output_1_0_g32 = appendResult65;
					float3 temp_output_6_0_g32 = frac( temp_output_1_0_g32 );
					float3 temp_cast_2 = (1.0).xxx;
					float3 lastDir827 = ( ( temp_output_6_0_g32 * 2.0 ) - temp_cast_2 );
					float noiseStaticA591 = tex2DNode587.r;
					float temp_output_13_0_g31 = noiseStaticA591;
					float swayingX211 = ( sin( ( ( remainingLifetime90 + temp_output_13_0_g31 ) * ( temp_output_13_0_g31 * _swayingFrequency.x ) ) ) * ( temp_output_13_0_g31 * _swayingAmplitude.x ) );
					float noiseStaticB592 = tex2DNode587.g;
					float3 appendResult60 = (float3(noiseStaticA591 , noiseStaticB592 , noiseStaticC611));
					float3 noiseStatic902 = appendResult60;
					float3 lerpResult905 = lerp( _velocityMin , _velocityMax , noiseStatic902);
					float3 appendResult562 = (float3(noiseStaticA591 , noiseStaticB592 , noiseStaticC611));
					half previousStartLifetime515 = ( 1000.0 * temp_output_3_0_g22 );
					float lifetimeUVx533 =  (0.0 + ( remainingLifetime90 - 0.0 ) * ( (float)255 - 0.0 ) / ( previousStartLifetime515 - 0.0 ) );
					int u1_g28 = (int)lifetimeUVx533;
					int v1_g28 = 2;
					Texture2D tex1_g28 =(Texture2D)_gradientTexOLT;
					float4 localReadPixels1_g28 = ReadPixels1_g28( u1_g28 , v1_g28 , tex1_g28 );
					float3 velMinOLT534 = ( (localReadPixels1_g28).xyz * _velocityMinMultiplier );
					int u1_g27 = (int)lifetimeUVx533;
					int v1_g27 = 3;
					Texture2D tex1_g27 =(Texture2D)_gradientTexOLT;
					float4 localReadPixels1_g27 = ReadPixels1_g27( u1_g27 , v1_g27 , tex1_g27 );
					float3 velMaxOLT547 = ( (localReadPixels1_g27).xyz * _velocityMaxMultiplier );
					#ifdef _VEL_CURVES
					float3 staticSwitch556 = (( _velCurvesRand )?( velMinOLT534 ):(  (velMinOLT534 + ( appendResult562 - float3( 0,0,0 ) ) * ( velMaxOLT547 - velMinOLT534 ) / ( float3( 1,1,1 ) - float3( 0,0,0 ) ) ) ));
					#else
					float3 staticSwitch556 = lerpResult905;
					#endif
					float3 temp_cast_6 = (1.0).xxx;
					float3 randomSign914 = sign( ( ( noiseStatic902 * 2.0 ) - temp_cast_6 ) );
					#ifdef _USE_ABSMINVEL
					float3 staticSwitch918 = ( staticSwitch556 * randomSign914 );
					#else
					float3 staticSwitch918 = staticSwitch556;
					#endif
					float3 finalVelocity693 = staticSwitch918;
					float3 break179 = finalVelocity693;
					float temp_output_13_0_g29 = noiseStaticB592;
					float swayingY212 = ( sin( ( ( remainingLifetime90 + temp_output_13_0_g29 ) * ( temp_output_13_0_g29 * _swayingFrequency.y ) ) ) * ( temp_output_13_0_g29 * _swayingAmplitude.y ) );
					float temp_output_13_0_g30 = noiseStaticC611;
					float swayingZ213 = ( sin( ( ( ( 1.0 - remainingLifetime90 ) + temp_output_13_0_g30 ) * ( temp_output_13_0_g30 * _swayingFrequency.z ) ) ) * ( temp_output_13_0_g30 * _swayingAmplitude.z ) );
					float3 appendResult180 = (float3(( swayingX211 + break179.x ) , ( break179.y + swayingY212 ) , ( break179.z + swayingZ213 )));
					float3 particlesLastPos123 = ( ( temp_output_1_0_g32 - temp_output_6_0_g32 ) / 1000.0 );
					float3 curParticlePos777 = ( ( appendResult180 * _srs_deltaTime.x ) + particlesLastPos123 );
					float3 temp_output_789_0 = ( curParticlePos777 - particlesLastPos123 );
					float3 temp_cast_7 = (1.0).xxx;
					float3 normalizeResult890 = normalize( ( _srs_deltaTime.x == 0.0 ? lastDir827 : ( sign( temp_output_789_0 ) * min( abs( temp_output_789_0 ) , temp_cast_7 ) ) ) );
					float3 temp_output_4_0_g56 = normalizeResult890;
					float4 appendResult715 = (float4(0.0 , 0.0 , 0.0 , 1.0));
					float3 emitterCenter716 = (mul( _emitterMatrix, appendResult715 )).xyz;
					float3 break751 = emitterCenter716;
					float3 emitterSize721 = _emitterSize;
					float3 appendResult752 = (float3(break751.x , ( break751.y - ( 0.5 * (emitterSize721).y ) ) , break751.z));
					float3 temp_output_717_0 = ( curParticlePos777 - appendResult752 );
					float3 break757 = emitterSize721;
					float3 appendResult756 = (float3(( 0.5 * break757.x ) , ( break757.y * 0.5 ) , ( 0.5 * break757.z )));
					float3 warpPos782 = ( _Teleporting == 1.0 ? ( _AddPos + curParticlePos777 ) : ( abs( temp_output_717_0 ) > appendResult756 ? ( ( ( temp_output_717_0 - -temp_output_717_0 ) * 0.01 ) + ( -temp_output_717_0 + appendResult752 ) ) : curParticlePos777 ) );
					#ifdef _WARP_POSITIONS
					float3 staticSwitch776 = warpPos782;
					#else
					float3 staticSwitch776 = curParticlePos777;
					#endif
					float3 simulatedParticlePos373 = staticSwitch776;
					float3 temp_output_2_0_g56 = simulatedParticlePos373;
					float noiseDynamicB601 = tex2DNode593.g;
					float inititalLifetime99 =  (_lifetimeMinMax.x + ( noiseDynamicB601 - 0.0 ) * ( _lifetimeMinMax.y - _lifetimeMinMax.x ) / ( 1.0 - 0.0 ) );
					float currentRemainingLifetime377 = ( remainingLifetime90 - _srs_deltaTime.x );
					float3 break2_g54 = simulatedParticlePos373;
					float4 appendResult9_g54 = (float4(break2_g54.x , ( ( break2_g54.y + _depthCamFarHeight ) * 0.5 ) , break2_g54.z , 1.0));
					float4 temp_output_11_0_g54 = mul( _depthCamMatrix, appendResult9_g54 );
					float4 break14_g54 = ( ( temp_output_11_0_g54 * 0.5 ) + 0.5 );
					float2 appendResult15_g54 = (float2(break14_g54.x , break14_g54.y));
					float2 appendResult471 = (float2(( 1.0 - (temp_output_11_0_g54).z ) , SAMPLE_TEXTURE2D( _SRS_depth, sampler_SRS_depth, appendResult15_g54 ).b));
					float3 break2_g53 = simulatedParticlePos373;
					float4 appendResult9_g53 = (float4(break2_g53.x , ( ( break2_g53.y + _localDepthCamFarHeight ) * 0.5 ) , break2_g53.z , 1.0));
					float4 temp_output_11_0_g53 = mul( _snowfallDepthCamMatrix, appendResult9_g53 );
					float4 break14_g53 = ( ( temp_output_11_0_g53 * 0.5 ) + 0.5 );
					float2 appendResult15_g53 = (float2(break14_g53.x , break14_g53.y));
					float2 appendResult460 = (float2(( 1.0 - (temp_output_11_0_g53).z ) , SAMPLE_TEXTURE2D( _SRS_localDepth, sampler_SRS_localDepth, ( appendResult15_g53 + _uvOffset ) ).r));
					#ifdef _SRS_LOCALDEPTH
					float2 staticSwitch455 = appendResult460;
					#else
					float2 staticSwitch455 = appendResult471;
					#endif
					float2 break462 = staticSwitch455;
					#ifdef UNITY_REVERSED_Z
					float staticSwitch926 = break462.y;
					#else
					float staticSwitch926 = ( 1.0 - break462.y );
					#endif
					#ifdef _SRS_COLLISION
					float staticSwitch481 = ( break462.x <= staticSwitch926 ? 0.0 : currentRemainingLifetime377 );
					#else
					float staticSwitch481 = currentRemainingLifetime377;
					#endif
					float lerpResult829 = lerp( staticSwitch481 , currentRemainingLifetime377 , _isPrewarming);
					float finalRemainingLifetime375 = lerpResult829;
					float packedLifetime512 = ( ( ( remainingLifetime90 <= 0.0 ? inititalLifetime99 : previousStartLifetime515 ) / 1000.0 ) + floor( ( finalRemainingLifetime375 * 1000.0 ) ) );
					float4 appendResult94 = (float4(( ( 0.5 + ( 0.5 * temp_output_4_0_g56 ) ) + floor( ( temp_output_2_0_g56 * 1000.0 ) ) ) , packedLifetime512));
					float4 simulatedValues126 = appendResult94;
					float3 stopEmitting398 = ( remainingLifetime90 > 0.0 ? (simulatedValues126).xyz : hidePos424 );
					float3 initPosFinal683 = ( _emissionFlag == 1.0 ? visibleParticles428 : stopEmitting398 );
					float3 temp_output_2_0_g58 = initPosFinal683;
					float4 appendResult96 = (float4(( ( 0.5 + ( 0.5 * temp_output_4_0_g58 ) ) + floor( ( temp_output_2_0_g58 * 1000.0 ) ) ) , ( ( inititalLifetime99 / 1000.0 ) + floor( ( inititalLifetime99 * 1000.0 ) ) )));
					float4 initState118 = appendResult96;
					

					finalColor = ( remainingLifetime90 <= 0.0 ? initState118 : simulatedValues126 );

					return finalColor;
				}
			ENDCG
		}
	}
	
	
	Fallback Off
}
/*ASEBEGIN
Version=19901
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;122;-3482.261,370.5298;Inherit;False;1281.136;589.4647;;3;677;3;8;Data Tex;1,1,1,1;0;0
Node;AmplifyShaderEditor.TexturePropertyNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;8;-3432.261,445.7085;Inherit;True;Property;_SRS_particles;_SRS_particles;0;0;Create;True;0;0;0;False;0;False;None;None;False;white;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3;-3185.261,444.7085;Inherit;True;Property;_output;output;0;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;677;-2841.371,688.9377;Inherit;False;UnpackFloats;-1;;22;fb7d2f1b3bb77824887f04cb05937452;0;1;2;FLOAT;0;False;2;FLOAT;0;FLOAT;1
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;526;-2029.915,3664.73;Inherit;False;761.926;277.5641;;5;533;896;568;531;569;Calculate UV.x from lifetime;1,1,1,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;515;-2598.591,816.2631;Half;False;previousStartLifetime;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;90;-2598.461,653.8773;Half;False;remainingLifetime;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TexturePropertyNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;589;-4688.784,-794.3599;Inherit;True;Property;_noiseTex;_noiseTex;12;0;Create;True;0;0;0;False;0;False;None;None;False;white;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;569;-2007.097,3713.254;Inherit;False;90;remainingLifetime;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;568;-2016.622,3786.056;Inherit;False;515;previousStartLifetime;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.IntNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;896;-2006.459,3861.603;Inherit;False;Constant;_GradientTexSize;GradientTexSize;20;0;Create;True;0;0;0;False;0;False;255;0;False;0;1;INT;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;708;-5068.111,-1002.194;Inherit;False;1323.149;761;;6;600;601;611;591;592;612;Noise;1,1,1,1;0;0
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;587;-4336.24,-864.4512;Inherit;True;Property;_staticNoise;_staticNoise;16;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.TFHCRemapNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;531;-1751.976,3736.294;Inherit;False;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;4;False;3;FLOAT;0;False;4;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.TexturePropertyNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;551;-2550.235,2810.279;Inherit;True;Property;_gradientTexOLT;gradientTexOLT;8;0;Create;True;0;0;0;False;0;False;None;None;False;black;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;125;-2086.075,-748.4064;Inherit;False;5183.365;1625.041;;66;693;914;913;911;912;908;910;907;905;902;890;887;777;124;779;776;373;783;56;57;556;566;819;826;828;823;825;822;820;126;94;519;790;795;789;824;821;63;377;567;64;563;559;607;614;608;613;558;606;562;605;60;179;200;181;218;216;217;182;180;92;87;781;915;916;918;Simulated;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;527;-2050.752,2621.343;Inherit;False;1490.446;408.2629;;8;534;894;539;901;541;554;616;617;Velocity Min over lifetime;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;528;-2047.914,3194.754;Inherit;False;1479.076;377.5332;;8;898;544;897;545;555;618;619;547;Velocity Max over lifetime;1,1,1,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;591;-3977.111,-952.194;Inherit;False;noiseStaticA;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;592;-3976.111,-868.194;Inherit;False;noiseStaticB;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;611;-3975.388,-789.3767;Inherit;False;noiseStaticC;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;550;-2311.332,2810.333;Inherit;False;gradientTexOLT;-1;True;1;0;SAMPLER2D;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;533;-1511.99,3736.398;Inherit;False;lifetimeUVx;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;605;-2029.571,-716.9012;Inherit;False;591;noiseStaticA;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;606;-2025.416,-643.5182;Inherit;False;592;noiseStaticB;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;613;-2027.076,-564.5056;Inherit;False;611;noiseStaticC;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;544;-1966.914,3276.83;Inherit;False;533;lifetimeUVx;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.IntNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;898;-1976.459,3360.603;Inherit;False;Constant;_TexelIndexY;TexelIndexY;20;0;Create;True;0;0;0;False;0;False;3;0;False;0;1;INT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;545;-1987.535,3465.36;Inherit;False;550;gradientTexOLT;1;0;OBJECT;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.IntNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;901;-1961.459,2813.603;Inherit;False;Constant;_TexelIndexY1;TexelIndexY;20;0;Create;True;0;0;0;False;0;False;2;0;False;0;1;INT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;539;-1964.752,2728.42;Inherit;False;533;lifetimeUVx;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;541;-1981.37,2903.95;Inherit;False;550;gradientTexOLT;1;0;OBJECT;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;60;-1812.022,-685.4061;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;897;-1702.459,3316.603;Inherit;False;TexLoad;-1;;27;24997ab8bd7822d44bede2b16c924318;0;3;2;INT;0;False;3;INT;0;False;4;OBJECT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;894;-1709.086,2758.294;Inherit;False;TexLoad;-1;;28;24997ab8bd7822d44bede2b16c924318;0;3;2;INT;0;False;3;INT;0;False;4;OBJECT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.ComponentMaskNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;555;-1467.659,3311.404;Inherit;False;True;True;True;False;1;0;FLOAT4;0,0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.Vector3Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;618;-1486.823,3409.424;Inherit;False;Property;_velocityMaxMultiplier;_velocityMaxMultiplier;14;0;Create;True;0;0;0;False;0;False;0,0,0;1,1,1;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.ComponentMaskNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;554;-1441.179,2759.189;Inherit;False;True;True;True;False;1;0;FLOAT4;0,0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.Vector3Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;616;-1459.343,2870.209;Inherit;False;Property;_velocityMinMultiplier;_velocityMinMultiplier;13;0;Create;True;0;0;0;False;0;False;0,0,0;1,1,1;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;902;-1648,-656;Inherit;False;noiseStatic;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;619;-1160.823,3321.424;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;617;-1115.343,2761.209;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;907;-2064,352;Inherit;False;902;noiseStatic;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;910;-2032,448;Inherit;False;Constant;_Float14;Float 14;20;0;Create;True;0;0;0;False;0;False;2;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;608;-2032.486,-99.69794;Inherit;False;592;noiseStaticB;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;614;-2034.662,-23.4093;Inherit;False;611;noiseStaticC;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;607;-2033.501,-174.0081;Inherit;False;591;noiseStaticA;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;547;-913.3785,3328.243;Inherit;False;velMaxOLT;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;534;-844.2156,2764.833;Inherit;False;velMinOLT;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;908;-1840,352;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;912;-1840,480;Inherit;False;Constant;_Float15;Float 15;20;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;562;-1811.885,-154.3185;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;558;-1974.984,177.8331;Inherit;False;547;velMaxOLT;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;559;-1972.984,82.83308;Inherit;False;534;velMinOLT;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;911;-1664,352;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.TFHCRemapNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;563;-1654.069,-155.3497;Inherit;False;5;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;1,1,1;False;3;FLOAT3;0,0,0;False;4;FLOAT3;1,1,1;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;567;-1638.619,58.57329;Inherit;False;534;velMinOLT;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.Vector3Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;57;-2016.204,-330.705;Inherit;False;Property;_velocityMax;_velocityMax;1;0;Create;True;0;0;0;False;0;False;0,1,0;0.5,-1,0.5;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.Vector3Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;56;-2014.504,-481.3044;Inherit;False;Property;_velocityMin;_velocityMin;2;0;Create;True;0;0;0;False;0;False;0,0,0;-0.5,-0.5,-0.5;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.SignOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;913;-1488,352;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.ToggleSwitchNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;566;-1420.619,-160.4267;Inherit;False;Property;_velCurvesRand;velCurvesRand;11;0;Create;True;0;0;0;False;0;False;0;True;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.LerpOp, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;905;-1408,-592;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;914;-1376,352;Inherit;False;randomSign;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;176;-3216.596,1944.839;Inherit;False;90;remainingLifetime;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;916;-1040,-304;Inherit;False;914;randomSign;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.StaticSwitch, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;556;-1206.381,-536.7184;Inherit;False;Property;_VEL_CURVES;VEL_CURVES;12;0;Create;True;0;0;0;False;0;False;0;0;0;False;;Toggle;2;Key0;Key1;Create;True;True;All;9;1;FLOAT3;0,0,0;False;0;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;4;FLOAT3;0,0,0;False;5;FLOAT3;0,0,0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;117;-2161.606,-2130.084;Inherit;False;3454.121;1244.036;;33;101;118;683;578;848;849;721;147;95;413;719;716;383;792;679;96;429;399;386;385;714;66;46;715;70;74;146;145;144;75;400;411;441;Initial State;1,1,1,1;0;0
Node;AmplifyShaderEditor.Vector3Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;219;-3454.479,1941.547;Inherit;False;Property;_swayingFrequency;_swayingFrequency;6;0;Create;True;0;0;0;False;0;False;0,0,0;10,5,10;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.Vector3Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;220;-3444.403,2106.809;Inherit;False;Property;_swayingAmplitude;_swayingAmplitude;7;0;Create;True;0;0;0;False;0;False;0,0,0;0.5,0.2,0.5;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.OneMinusNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;362;-2976.146,2050.35;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;609;-3204.04,1771.475;Inherit;False;591;noiseStaticA;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;615;-3203.011,1853.985;Inherit;False;592;noiseStaticB;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;610;-3186.573,2145.121;Inherit;False;611;noiseStaticC;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;915;-752,-448;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;370;-2815.622,1938.62;Inherit;False;Swaying;-1;;29;059bd76c3e9e90d4e9866aa072ef1dce;0;4;7;FLOAT;0;False;11;FLOAT;0;False;13;FLOAT;0;False;12;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;74;-1571.996,-1632.892;Inherit;False;Constant;_Float3;Float 3;5;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;371;-2800.631,2109.714;Inherit;False;Swaying;-1;;30;059bd76c3e9e90d4e9866aa072ef1dce;0;4;7;FLOAT;0;False;11;FLOAT;0;False;13;FLOAT;0;False;12;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;372;-2827.364,1778.859;Inherit;False;Swaying;-1;;31;059bd76c3e9e90d4e9866aa072ef1dce;0;4;7;FLOAT;0;False;11;FLOAT;0;False;13;FLOAT;0;False;12;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;918;-528,-528;Inherit;False;Property;_USE_ABSMINVEL;USE_ABSMINVEL;12;0;Create;True;0;0;0;True;0;False;0;0;0;False;;Toggle;2;Key0;Key1;Create;True;True;All;9;1;FLOAT3;0,0,0;False;0;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;4;FLOAT3;0,0,0;False;5;FLOAT3;0,0,0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;212;-2504.872,1934.899;Inherit;False;swayingY;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;213;-2502.914,2114.104;Inherit;False;swayingZ;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;211;-2513.79,1760.932;Inherit;False;swayingX;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;715;-1255.677,-1645.868;Inherit;False;FLOAT4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;1;False;1;FLOAT4;0
Node;AmplifyShaderEditor.Matrix4X4Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;66;-1802.157,-2009.532;Inherit;False;Global;_emitterMatrix;_emitterMatrix;5;0;Create;True;0;0;0;False;0;False;0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0;0;1;FLOAT4x4;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;693;-224,-560;Inherit;False;finalVelocity;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;65;-2861.589,448.5301;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;714;-1128.677,-1712.868;Inherit;False;2;2;0;FLOAT4x4;0,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1;False;1;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.Vector3Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;849;-2131.182,-1593.94;Inherit;False;Property;_emitterSize;_emitterSize;16;0;Create;True;0;0;0;False;0;False;0,0,0;0,0,0;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;217;0,-416;Inherit;False;212;swayingY;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;216;16,-688;Inherit;False;211;swayingX;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;218;16,-304;Inherit;False;213;swayingZ;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.BreakToComponentsNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;179;16,-576;Inherit;False;FLOAT3;1;0;FLOAT3;0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;781;-1145.286,-39.69216;Inherit;False;2835.245;842.2164;;32;782;778;718;855;858;857;757;722;766;732;717;727;750;747;728;748;749;780;759;756;856;851;854;752;853;852;751;711;869;870;872;873;Warp Positions;1,1,1,1;0;0
Node;AmplifyShaderEditor.ComponentMaskNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;719;-793.6173,-1547.734;Inherit;False;True;True;True;False;1;0;FLOAT4;0,0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;721;-1907.069,-1536.036;Inherit;False;emitterSize;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;889;-2718.291,448.0754;Inherit;False;Unpack3dVectors;-1;;32;b7fd13cda2d9947418e24c4aad49bb03;0;1;1;FLOAT3;0,0,0;False;2;FLOAT3;4;FLOAT3;3
Node;AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;182;240,-416;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;181;224,-688;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;200;240,-544;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;123;-2475.706,442.5309;Inherit;False;particlesLastPos;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;716;-594.6772,-1544.868;Inherit;False;emitterCenter;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;852;-1138.937,666.2315;Inherit;False;721;emitterSize;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;180;432,-576;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.Vector2Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;824;336,-240;Inherit;False;Global;_srs_deltaTime;_srs_deltaTime;21;0;Create;True;0;0;0;False;0;False;0,0;0.008992205,111.2074;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.ComponentMaskNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;853;-964.9373,665.2315;Inherit;False;False;True;False;True;1;0;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;855;-946.9373,544.2315;Inherit;False;Constant;_Float17;Float 17;17;0;Create;True;0;0;0;False;0;False;0.5;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;718;-1133.286,392.716;Inherit;False;716;emitterCenter;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;63;656,-576;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;124;640,-432;Inherit;False;123;particlesLastPos;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.BreakToComponentsNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;751;-925.7868,393.2543;Inherit;False;FLOAT3;1;0;FLOAT3;0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;854;-761.9373,542.2315;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;64;864,-528;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;851;-627.9373,411.2315;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;777;1040,-528;Inherit;False;curParticlePos;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;752;-420.9605,386.6328;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;778;-525.0569,246.2133;Inherit;False;777;curParticlePos;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;717;-286.9638,269.2224;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.NegateNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;727;-144.3369,353.3658;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;722;-169.0801,18.30784;Inherit;False;721;emitterSize;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;748;130.5774,324.491;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;750;117.2776,421.4913;Inherit;False;Constant;_Float2;Float 2;19;0;Create;True;0;0;0;False;0;False;0.01;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;766;275.3899,-8.51103;Inherit;False;Constant;_Float18;Float 18;21;0;Create;True;0;0;0;False;0;False;0.5;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.BreakToComponentsNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;757;33.90099,20.27647;Inherit;False;FLOAT3;1;0;FLOAT3;0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;858;141.2474,163.5209;Inherit;False;Constant;_Float16;Float 16;17;0;Create;True;0;0;0;False;0;False;0.5;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;856;480.2626,150.5753;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0.5;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;759;478.2648,-2.8862;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0.5;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;749;286.5775,347.491;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;728;-17.88416,467.5663;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;857;310.2474,77.52094;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;756;635.9763,62.1265;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;780;531.1874,523.3977;Inherit;False;777;curParticlePos;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;747;471.7777,423.9909;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.AbsOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;732;273.2593,258.0693;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.Vector3Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;872;917.8788,67.14111;Inherit;False;Property;_AddPos;AddPos;19;0;Create;True;0;0;0;False;0;False;0,0,0;0,0,0;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.Compare, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;711;803.791,280.1786;Inherit;False;2;4;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;870;936.8788,1.141113;Inherit;False;Property;_Teleporting;Teleporting;18;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;873;1151.081,223.4006;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.Compare, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;869;1304.879,204.1411;Inherit;False;0;4;0;FLOAT;0;False;1;FLOAT;1;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;782;1477.011,207.1336;Inherit;False;warpPos;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;783;1664,-528;Inherit;False;782;warpPos;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;779;1648,-656;Inherit;False;777;curParticlePos;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;380;-2016,1056;Inherit;False;3588.787;1343.682;;14;375;829;830;481;323;926;378;984;983;462;455;479;480;997;Collision;1,1,1,1;0;0
Node;AmplifyShaderEditor.StaticSwitch, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;776;1872,-608;Inherit;False;Property;_WARP_POSITIONS;WARP_POSITIONS;18;0;Create;True;0;0;0;False;0;False;0;0;0;False;;Toggle;2;Key0;Key1;Create;True;True;All;9;1;FLOAT3;0,0,0;False;0;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;4;FLOAT3;0,0,0;False;5;FLOAT3;0,0,0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;373;2112,-608;Inherit;False;simulatedParticlePos;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;480;-1968,1872;Inherit;False;1508.162;497.6581;;7;460;288;374;974;868;289;867;Local Depth;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;479;-1696,1328;Inherit;False;1228.208;455.3425;;3;478;471;454;Global Depth;1,1,1,1;0;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;974;-1936,2112;Inherit;False;Property;_localDepthCamFarHeight;_localDepthCamFarHeight;21;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;374;-1904,2208;Inherit;False;373;simulatedParticlePos;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.Matrix4X4Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;288;-1920,1936;Inherit;False;Global;_snowfallDepthCamMatrix;_snowfallDepthCamMatrix;6;0;Create;True;0;0;0;True;0;False;1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1;0;1;FLOAT4x4;0
Node;AmplifyShaderEditor.Vector2Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;868;-1296,2128;Inherit;False;Property;_uvOffset;_uvOffset;17;0;Create;True;0;0;0;False;0;False;0,0;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;478;-1664,1472;Inherit;False;373;simulatedParticlePos;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;995;-1552,1920;Inherit;False;SRS Depth UV;-1;;53;c0be0a97a9ceb294982346b1f0fe2eb3;0;3;20;FLOAT4x4;1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1;False;21;FLOAT;0;False;25;FLOAT3;0,0,0;False;2;FLOAT2;0;FLOAT;24
Node;AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;867;-1120,2080;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;996;-1392,1424;Inherit;False;SRS Depth UV;-1;;54;c0be0a97a9ceb294982346b1f0fe2eb3;0;3;20;FLOAT4x4;1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1;False;21;FLOAT;0;False;25;FLOAT3;0,0,0;False;2;FLOAT2;0;FLOAT;24
Node;AmplifyShaderEditor.SimpleTimeNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;597;-5009.111,-548.194;Inherit;False;1;0;FLOAT;0.1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;289;-992,2048;Inherit;True;Property;_SRS_localDepth;_SRS_localDepth;5;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;454;-976,1552;Inherit;True;Global;_SRS_depth;_SRS_depth;4;0;Create;True;0;0;0;False;0;False;-1;None;;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.FractNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;595;-4813.111,-548.194;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;598;-4939.111,-400.194;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;460;-672,1952;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;471;-672,1456;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;92;400,-384;Inherit;False;90;remainingLifetime;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;602;-4666.962,-550.5592;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;455;-368,1776;Inherit;False;Property;_SRS_LOCALDEPTH;SRS_LOCALDEPTH;13;0;Create;True;0;0;0;True;0;False;0;0;0;False;;Toggle;2;Key0;Key1;Create;True;True;All;9;1;FLOAT2;0,0;False;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT2;0,0;False;6;FLOAT2;0,0;False;7;FLOAT2;0,0;False;8;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;87;672,-320;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;599;-4533.111,-541.194;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.BreakToComponentsNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;462;-64,1776;Inherit;False;FLOAT2;1;0;FLOAT2;0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;377;864,-320;Inherit;False;currentRemainingLifetime;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;593;-4335.111,-662.194;Inherit;True;Property;_dynamicNoise;_dynamicNoise;16;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.OneMinusNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;983;176,1808;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;984;160,1904;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;116;-3480.304,-200.0838;Inherit;False;763.3798;434.3144;;3;604;99;115;Init Life Time;1,1,1,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;601;-3977.962,-493.5592;Inherit;False;noiseDynamicB;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;378;320,1984;Inherit;False;377;currentRemainingLifetime;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;997;528,1744;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;926;336,1824;Inherit;False;Property;_UnityReversedZ;UnityReversedZ;20;0;Create;True;0;0;0;True;0;False;0;0;0;True;UNITY_REVERSED_Z;Toggle;2;Key0;Key1;Fetch;False;True;All;9;1;FLOAT;0;False;0;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT;0;False;7;FLOAT;0;False;8;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;604;-3411.236,-118.8692;Inherit;False;601;noiseDynamicB;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;115;-3403.68,42.13861;Inherit;False;Property;_lifetimeMinMax;_lifetimeMinMax;3;0;Create;True;0;0;0;False;0;False;0.5,1;20,30;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;795;1152,-320;Inherit;False;777;curParticlePos;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;790;1152,-240;Inherit;False;123;particlesLastPos;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.Compare, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;323;624,1808;Inherit;False;5;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TFHCRemapNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;86;-3184.102,-34.84671;Inherit;False;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT;0;False;4;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;789;1376,-304;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.StaticSwitch, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;481;800,1728;Inherit;False;Property;_SRS_COLLISION;SRS_COLLISION;15;0;Create;True;0;0;0;True;0;False;0;0;0;False;;Toggle;2;Key0;Key1;Create;True;True;All;9;1;FLOAT;0;False;0;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT;0;False;7;FLOAT;0;False;8;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;830;848,1936;Inherit;False;Property;_isPrewarming;isPrewarming;15;1;[Toggle];Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;552;3344,-640;Inherit;False;985.3378;541.0469;;8;678;512;513;518;517;514;522;516;Pack Init Lifetime and Simulated Lifetime to single float;1,1,1,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;99;-2963.762,-39.69425;Inherit;False;inititalLifetime;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;600;-3981.111,-586.194;Inherit;False;noiseDynamicA;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;821;1536,-224;Inherit;False;Constant;_Float19;Float 19;21;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.AbsOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;819;1600,-304;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.LerpOp, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;829;1056,1792;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;145;-2054.125,-1735.634;Inherit;False;Constant;_Float0;Float 0;5;0;Create;True;0;0;0;False;0;False;0.5;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;827;-2462.433,537.827;Inherit;False;lastDir;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;46;-2124.613,-1869.299;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;603;-4293.911,-20.41521;Inherit;False;600;noiseDynamicA;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;516;3456,-416;Inherit;False;Constant;_Float13;Float 13;13;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;522;3392,-512;Inherit;False;90;remainingLifetime;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;517;3408,-336;Inherit;False;99;inititalLifetime;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;518;3408,-224;Inherit;False;515;previousStartLifetime;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMinOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;820;1776,-288;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SignOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;822;1776,-384;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;375;1296,1792;Inherit;False;finalRemainingLifetime;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;998;-4400,256;Inherit;False;Property;_spawnHeightRange;_spawnHeightRange;22;0;Create;True;0;0;0;False;0;False;-2;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;999;-4336,352;Inherit;False;Constant;_Float5;Float 5;23;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;411;-644.0113,-1410.667;Inherit;False;1826.266;478.5063;;15;859;669;445;406;410;414;427;428;401;862;402;861;864;865;866;Percentage of visible particles;1,1,1,1;0;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;144;-1898.126,-1791.634;Inherit;False;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TFHCRemapNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;572;-4054.458,21.4651;Inherit;False;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT;0;False;4;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ComponentMaskNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;848;-1937.182,-1647.94;Inherit;False;True;False;True;False;1;0;FLOAT3;0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Compare, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;514;3696,-352;Inherit;False;5;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;513;3616,-592;Inherit;False;375;finalRemainingLifetime;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;823;2000,-320;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;828;1968,-400;Inherit;False;827;lastDir;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;826;1984,-480;Inherit;False;Constant;_Float21;Float 21;21;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;146;-1724.126,-1743.634;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;570;-3845.081,16.26883;Inherit;False;randomStartPosY;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;864;-537.9673,-1100.377;Inherit;False;Constant;_Float20;Float 20;17;0;Create;True;0;0;0;False;0;False;10;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;402;-613.1894,-1305.156;Inherit;False;Property;_particlesPercentage;_particlesPercentage;10;0;Create;True;0;0;0;False;0;False;0;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;678;3904,-480;Inherit;False;PackFloats;-1;;55;cc38e1ee9efb8cd41a684a3cdb95f050;0;2;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Compare, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;825;2160,-432;Inherit;False;0;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.BreakToComponentsNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;147;-1567.237,-1740.379;Inherit;False;FLOAT2;1;0;FLOAT2;0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;578;-1731.82,-1866.355;Inherit;False;570;randomStartPosY;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;861;-385.133,-1263.114;Inherit;False;2;2;0;FLOAT;10;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;512;4096,-560;Inherit;False;packedLifetime;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.NormalizeNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;890;2336,-336;Inherit;False;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;70;-1407.158,-1747.908;Inherit;False;FLOAT4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;1;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;862;-208.72,-1277.529;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;10;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;865;-208.5132,-1374.746;Inherit;False;Constant;_Float22;Float 22;17;0;Create;True;0;0;0;False;0;False;2;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;519;2400,-448;Inherit;False;512;packedLifetime;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;887;2384,-560;Inherit;False;Pack3dVectors;-1;;56;3d6a5c500d240fa4aa147b4a8c2bbcd0;0;2;2;FLOAT3;0,0,0;False;4;FLOAT3;0,0,0;False;1;FLOAT3;6
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;75;-1239.168,-1863.919;Inherit;False;2;2;0;FLOAT4x4;0,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1;False;1;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.PowerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;859;-49.74551,-1308.077;Inherit;False;False;2;0;FLOAT;2;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;94;2624,-512;Inherit;False;FLOAT4;4;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;441;-2117.065,-1147.957;Inherit;False;461.001;164.13;;3;424;451;452;NaN pos;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;400;-1643.834,-1426.868;Inherit;False;963.9756;449;;7;425;390;398;393;394;395;392;Stop emitting and reset positions to NaN;1,1,1,1;0;0
Node;AmplifyShaderEditor.ComponentMaskNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;95;-941.187,-2045.056;Inherit;False;True;True;True;False;1;0;FLOAT4;0,0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.Compare, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;866;122.8829,-1286.145;Inherit;False;2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector3Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;893;-2120.403,-1004.333;Inherit;False;Constant;_Vector0;Vector 0;20;0;Create;True;0;0;0;False;0;False;0,-9999,0;0,0,0;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;126;2784,-512;Inherit;False;simulatedValues;-1;True;1;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;390;-1586.17,-1203.369;Inherit;False;126;simulatedValues;1;0;OBJECT;;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;413;-727.8301,-2046.874;Inherit;False;initPos;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;669;262.3624,-1350.23;Inherit;False;611;noiseStaticC;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.TFHCRemapNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;445;280.2695,-1224.863;Inherit;False;5;0;FLOAT;0;False;1;FLOAT;0.0001;False;2;FLOAT;1;False;3;FLOAT;0;False;4;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;424;-1854.064,-1097.366;Inherit;False;hidePos;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.ComponentMaskNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;392;-1359.17,-1203.369;Inherit;False;True;True;True;False;1;0;FLOAT4;0,0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;395;-1295.17,-1299.369;Inherit;False;Constant;_Float4;Float 4;9;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;425;-1315.45,-1101.059;Inherit;False;424;hidePos;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;414;458.0653,-1114.196;Inherit;False;413;initPos;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;410;478.106,-1190.552;Inherit;False;Constant;_Float8;Float 8;10;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;394;-1377.77,-1381.969;Inherit;False;90;remainingLifetime;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.StepOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;406;491.9828,-1313.418;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;427;465.468,-1033.062;Inherit;False;424;hidePos;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.Compare, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;393;-1071.17,-1347.369;Inherit;False;2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.Compare, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;401;733.182,-1231.418;Inherit;False;2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;398;-884.5475,-1348.533;Inherit;False;stopEmitting;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;428;915.855,-1223.047;Inherit;False;visibleParticles;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;386;-472.5097,-1943.349;Inherit;False;Constant;_Float1;Float 1;9;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;429;-515.0244,-1822.401;Inherit;False;428;visibleParticles;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;383;-509.1054,-2028.609;Inherit;False;Property;_emissionFlag;_emissionFlag;9;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;399;-493.3976,-1650.832;Inherit;False;398;stopEmitting;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.Compare, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;385;-272.3177,-1894.577;Inherit;False;0;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;792;-51.44471,-1856.755;Inherit;False;Constant;_Float10;Float 10;21;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;683;-103.0732,-1952.326;Inherit;False;initPosFinal;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;101;-210.6407,-1674.26;Inherit;False;99;inititalLifetime;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;679;70.2198,-1728.936;Inherit;False;PackFloats;-1;;57;cc38e1ee9efb8cd41a684a3cdb95f050;0;2;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;888;134.4948,-1949.286;Inherit;False;Pack3dVectors;-1;;58;3d6a5c500d240fa4aa147b4a8c2bbcd0;0;2;2;FLOAT3;0,0,0;False;4;FLOAT3;0,0,0;False;1;FLOAT3;6
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;96;364.6811,-1914.438;Inherit;False;FLOAT4;4;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;128;4736,32;Inherit;False;1338.684;541.2371;;6;83;127;91;119;82;0;Output;1,1,1,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;118;527.791,-1917.171;Inherit;False;initState;-1;True;1;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;82;4848,208;Inherit;False;Constant;_zero;zero;5;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;119;4800,304;Inherit;False;118;initState;1;0;OBJECT;;False;1;FLOAT4;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;91;4768,96;Inherit;False;90;remainingLifetime;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;127;4768,400;Inherit;False;126;simulatedValues;1;0;OBJECT;;False;1;FLOAT4;0
Node;AmplifyShaderEditor.Vector2Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;574;-4448,80;Inherit;False;Constant;_randomStartPosY;_randomStartPosY;16;0;Create;True;0;0;0;False;0;False;-2,0;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.Compare, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;83;5056,192;Inherit;False;5;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT4;0,0,0,0;False;3;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SimpleDivideOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;452;-1962.216,-1098.372;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;451;-2102.216,-1088.372;Inherit;False;Constant;_Float9;Float 9;10;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;612;-3983.388,-414.3767;Inherit;False;noiseDynamicC;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;0;5840,192;Float;False;True;-1;2;;100;5;Hidden/NOT_Lonely/Weatherade/SRS_ParticleSystem_Simulation;0770190933193b94aaa3065e307002fa;True;Unlit;0;0;Unlit;2;False;True;0;1;False;;0;False;;0;1;False;;0;False;;True;0;False;;0;False;;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;True;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;1;RenderType=Opaque=RenderType;True;0;False;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;2;Include;;False;;Native;False;0;0;;Pragma;warning(disable : 4008);False;;Custom;False;0;0;;;0;0;Standard;1;Vertex Position,InvertActionOnDeselection;1;0;0;1;True;False;;True;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;161;-3488.031,1701.138;Inherit;False;1220.252;621.9978;;0;Horizontal Swaying;1,1,1,1;0;0
WireConnection;3;0;8;0
WireConnection;677;2;3;4
WireConnection;515;0;677;1
WireConnection;90;0;677;0
WireConnection;587;0;589;0
WireConnection;587;7;589;1
WireConnection;531;0;569;0
WireConnection;531;2;568;0
WireConnection;531;4;896;0
WireConnection;591;0;587;1
WireConnection;592;0;587;2
WireConnection;611;0;587;3
WireConnection;550;0;551;0
WireConnection;533;0;531;0
WireConnection;60;0;605;0
WireConnection;60;1;606;0
WireConnection;60;2;613;0
WireConnection;897;2;544;0
WireConnection;897;3;898;0
WireConnection;897;4;545;0
WireConnection;894;2;539;0
WireConnection;894;3;901;0
WireConnection;894;4;541;0
WireConnection;555;0;897;0
WireConnection;554;0;894;0
WireConnection;902;0;60;0
WireConnection;619;0;555;0
WireConnection;619;1;618;0
WireConnection;617;0;554;0
WireConnection;617;1;616;0
WireConnection;547;0;619;0
WireConnection;534;0;617;0
WireConnection;908;0;907;0
WireConnection;908;1;910;0
WireConnection;562;0;607;0
WireConnection;562;1;608;0
WireConnection;562;2;614;0
WireConnection;911;0;908;0
WireConnection;911;1;912;0
WireConnection;563;0;562;0
WireConnection;563;3;559;0
WireConnection;563;4;558;0
WireConnection;913;0;911;0
WireConnection;566;0;563;0
WireConnection;566;1;567;0
WireConnection;905;0;56;0
WireConnection;905;1;57;0
WireConnection;905;2;902;0
WireConnection;914;0;913;0
WireConnection;556;1;905;0
WireConnection;556;0;566;0
WireConnection;362;0;176;0
WireConnection;915;0;556;0
WireConnection;915;1;916;0
WireConnection;370;7;176;0
WireConnection;370;11;219;2
WireConnection;370;13;615;0
WireConnection;370;12;220;2
WireConnection;371;7;362;0
WireConnection;371;11;219;3
WireConnection;371;13;610;0
WireConnection;371;12;220;3
WireConnection;372;7;176;0
WireConnection;372;11;219;1
WireConnection;372;13;609;0
WireConnection;372;12;220;1
WireConnection;918;1;556;0
WireConnection;918;0;915;0
WireConnection;212;0;370;0
WireConnection;213;0;371;0
WireConnection;211;0;372;0
WireConnection;715;3;74;0
WireConnection;693;0;918;0
WireConnection;65;0;3;1
WireConnection;65;1;3;2
WireConnection;65;2;3;3
WireConnection;714;0;66;0
WireConnection;714;1;715;0
WireConnection;179;0;693;0
WireConnection;719;0;714;0
WireConnection;721;0;849;0
WireConnection;889;1;65;0
WireConnection;182;0;179;2
WireConnection;182;1;218;0
WireConnection;181;0;216;0
WireConnection;181;1;179;0
WireConnection;200;0;179;1
WireConnection;200;1;217;0
WireConnection;123;0;889;4
WireConnection;716;0;719;0
WireConnection;180;0;181;0
WireConnection;180;1;200;0
WireConnection;180;2;182;0
WireConnection;853;0;852;0
WireConnection;63;0;180;0
WireConnection;63;1;824;1
WireConnection;751;0;718;0
WireConnection;854;0;855;0
WireConnection;854;1;853;0
WireConnection;64;0;63;0
WireConnection;64;1;124;0
WireConnection;851;0;751;1
WireConnection;851;1;854;0
WireConnection;777;0;64;0
WireConnection;752;0;751;0
WireConnection;752;1;851;0
WireConnection;752;2;751;2
WireConnection;717;0;778;0
WireConnection;717;1;752;0
WireConnection;727;0;717;0
WireConnection;748;0;717;0
WireConnection;748;1;727;0
WireConnection;757;0;722;0
WireConnection;856;0;766;0
WireConnection;856;1;757;2
WireConnection;759;0;766;0
WireConnection;759;1;757;0
WireConnection;749;0;748;0
WireConnection;749;1;750;0
WireConnection;728;0;727;0
WireConnection;728;1;752;0
WireConnection;857;0;757;1
WireConnection;857;1;858;0
WireConnection;756;0;759;0
WireConnection;756;1;857;0
WireConnection;756;2;856;0
WireConnection;747;0;749;0
WireConnection;747;1;728;0
WireConnection;732;0;717;0
WireConnection;711;0;732;0
WireConnection;711;1;756;0
WireConnection;711;2;747;0
WireConnection;711;3;780;0
WireConnection;873;0;872;0
WireConnection;873;1;780;0
WireConnection;869;0;870;0
WireConnection;869;2;873;0
WireConnection;869;3;711;0
WireConnection;782;0;869;0
WireConnection;776;1;779;0
WireConnection;776;0;783;0
WireConnection;373;0;776;0
WireConnection;995;20;288;0
WireConnection;995;21;974;0
WireConnection;995;25;374;0
WireConnection;867;0;995;0
WireConnection;867;1;868;0
WireConnection;996;25;478;0
WireConnection;289;1;867;0
WireConnection;454;1;996;0
WireConnection;595;0;597;0
WireConnection;460;0;995;24
WireConnection;460;1;289;1
WireConnection;471;0;996;24
WireConnection;471;1;454;3
WireConnection;602;0;595;0
WireConnection;602;1;598;1
WireConnection;455;1;471;0
WireConnection;455;0;460;0
WireConnection;87;0;92;0
WireConnection;87;1;824;1
WireConnection;599;0;602;0
WireConnection;599;1;598;2
WireConnection;462;0;455;0
WireConnection;377;0;87;0
WireConnection;593;0;589;0
WireConnection;593;1;599;0
WireConnection;593;7;589;1
WireConnection;983;0;462;1
WireConnection;984;0;462;1
WireConnection;601;0;593;2
WireConnection;997;0;462;0
WireConnection;926;1;983;0
WireConnection;926;0;984;0
WireConnection;323;0;997;0
WireConnection;323;1;926;0
WireConnection;323;3;378;0
WireConnection;86;0;604;0
WireConnection;86;3;115;1
WireConnection;86;4;115;2
WireConnection;789;0;795;0
WireConnection;789;1;790;0
WireConnection;481;1;378;0
WireConnection;481;0;323;0
WireConnection;99;0;86;0
WireConnection;600;0;593;1
WireConnection;819;0;789;0
WireConnection;829;0;481;0
WireConnection;829;1;378;0
WireConnection;829;2;830;0
WireConnection;827;0;889;3
WireConnection;820;0;819;0
WireConnection;820;1;821;0
WireConnection;822;0;789;0
WireConnection;375;0;829;0
WireConnection;144;0;46;0
WireConnection;144;1;145;0
WireConnection;572;0;603;0
WireConnection;572;3;998;0
WireConnection;572;4;999;0
WireConnection;848;0;849;0
WireConnection;514;0;522;0
WireConnection;514;1;516;0
WireConnection;514;2;517;0
WireConnection;514;3;518;0
WireConnection;823;0;822;0
WireConnection;823;1;820;0
WireConnection;146;0;144;0
WireConnection;146;1;848;0
WireConnection;570;0;572;0
WireConnection;678;1;513;0
WireConnection;678;2;514;0
WireConnection;825;0;824;1
WireConnection;825;1;826;0
WireConnection;825;2;828;0
WireConnection;825;3;823;0
WireConnection;147;0;146;0
WireConnection;861;0;402;0
WireConnection;861;1;864;0
WireConnection;512;0;678;0
WireConnection;890;0;825;0
WireConnection;70;0;147;0
WireConnection;70;1;578;0
WireConnection;70;2;147;1
WireConnection;70;3;74;0
WireConnection;862;0;861;0
WireConnection;862;1;864;0
WireConnection;887;2;373;0
WireConnection;887;4;890;0
WireConnection;75;0;66;0
WireConnection;75;1;70;0
WireConnection;859;0;865;0
WireConnection;859;1;862;0
WireConnection;94;0;887;6
WireConnection;94;3;519;0
WireConnection;95;0;75;0
WireConnection;866;0;402;0
WireConnection;866;2;859;0
WireConnection;126;0;94;0
WireConnection;413;0;95;0
WireConnection;445;0;866;0
WireConnection;424;0;893;0
WireConnection;392;0;390;0
WireConnection;406;0;669;0
WireConnection;406;1;445;0
WireConnection;393;0;394;0
WireConnection;393;1;395;0
WireConnection;393;2;392;0
WireConnection;393;3;425;0
WireConnection;401;0;406;0
WireConnection;401;1;410;0
WireConnection;401;2;414;0
WireConnection;401;3;427;0
WireConnection;398;0;393;0
WireConnection;428;0;401;0
WireConnection;385;0;383;0
WireConnection;385;1;386;0
WireConnection;385;2;429;0
WireConnection;385;3;399;0
WireConnection;683;0;385;0
WireConnection;679;1;101;0
WireConnection;679;2;101;0
WireConnection;888;2;683;0
WireConnection;888;4;792;0
WireConnection;96;0;888;6
WireConnection;96;3;679;0
WireConnection;118;0;96;0
WireConnection;83;0;91;0
WireConnection;83;1;82;0
WireConnection;83;2;119;0
WireConnection;83;3;127;0
WireConnection;452;0;451;0
WireConnection;452;1;451;0
WireConnection;612;0;593;3
WireConnection;0;0;83;0
ASEEND*/
//CHKSM=E28DB7AB00B910D1E7BD6F505654C52FE4802ED4