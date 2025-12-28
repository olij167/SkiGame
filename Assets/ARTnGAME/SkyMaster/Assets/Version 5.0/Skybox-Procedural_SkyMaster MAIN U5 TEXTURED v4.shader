// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'
// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "SkyMaster/SkyMasterShaderE_SKYBOX U5 v4 Textured" 
{

	Properties{
		_MainTex("Greyscale (R) Alpha (A)", 2D) = "white" {}
		_ColorRamp("Colour Palette", 2D) = "gray" {}
		_Coloration("Coloration Ammount", Float) = 0
		_TintColor("Color Tint", Color) = (0,0,0,0)


		fOuterRadius("fOuterRadius", Float) = 0
		fOuterRadius2("fOuterRadius2", Float) = 0
		fInnerRadius("fInnerRadius", Float) = 0
		fInnerRadius2("fInnerRadius2", Float) = 0

		fKrESun("fKrESun", Float) = 0			// Kr * ESun
		fKmESun("fKmESun", Float) = 0			// Km * ESun
		fKr4PI("fKr4PI", Float) = 0			// Kr * 4 * PI
		fKm4PI("fKm4PI", Float) = 0			// Km * 4 * PI
		fScale("fScale", Float) = 0			// 1 / (fOuterRadius - fInnerRadius)
		fScaleDepth("fScaleDepth", Float) = 0		//
		fScaleOverScaleDepth("fScaleOverScaleDepth", Float) = 0 // fScale / fScaleDepth

		fExposure("fExposure", Float) = 0		// HDR 

		v3CameraPos("v3CameraPos", Vector) = (0,0,0)		// camera
		v3LightDir("v3LightDir", Vector) = (0,0,0)		// light source
		v3LightDirMoon("v3LightDirMoon", Vector) = (0,0,0)		// moon light
		v3InvWavelength("v3InvWavelength", Vector) = (0,0,0)//  	

		fCameraHeight("fCameraHeight", Float) = 0    // height
		fCameraHeight2("fCameraHeight2", Float) = 0   // 	  	


		fSamples("fSamples", Float) = 2

		Bump_strenght("Bump_strenght", Float) = 0
		g("g", Float) = 0				// The Mie phase asymmetry factor
		g2("g2", Float) = 0				// The Mie phase asymmetry factor squared

		Horizon_adj("Horizon", Float) = 0
		HorizonY("HorizonY", Float) = 0
		GroundColor("GroundColor", Color) = (0,0,0,1)

		_Obliqueness("Obliqueness", Vector) = (0, 0, 0, 0)

		White_cutoff("White replace factor", Float) = 1.1//0.9991
		SunBlend("Sun blend", Float) = 26
		SunColor("Sun Tint", Color) = (0,0,0,0)

			//v0.1
			_Tint("Tint Color", Color) = (.5, .5, .5, .5)
			[Gamma] _Exposure("Exposure", Range(0, 8)) = 1.0
			_Rotation("Rotation", Range(0, 360)) = 0
			[NoScaleOffset] _MainTexSKYBOX("Spherical  (HDR)", 2D) = "grey" {}
			[KeywordEnum(6 Frames Layout, Latitude Longitude Layout)] _Mapping("Mapping", Float) = 1
			[Enum(360 Degrees, 0, 180 Degrees, 1)] _ImageType("Image Type", Float) = 0
			[Toggle] _MirrorOnBack("Mirror on Back", Float) = 0
			[Enum(None, 0, Side by Side, 1, Over Under, 2)] _Layout("3D Layout", Float) = 0
				SkyBoxBlend("Skybox blend", Float) = 1
	}

		SubShader
			{
				////////////////////////////Tags { "RenderType"="Transparent" }
					Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
					Cull Off ZWrite Off
				Pass
				{
				//Cull Front

				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag

				//v0.1
				#pragma multi_compile_local __ _MAPPING_6_FRAMES_LAYOUT

				#include "UnityCG.cginc"
				#include "Lighting.cginc"

				//#pragma exclude_renderers opengl 
				//#pragma target 3.0	

				//#pragma glsl

				samplerCUBE _CubeTex;

				uniform float fOuterRadius;		// outerradius
				uniform float fOuterRadius2;	// 
				uniform float fInnerRadius;		// inner radius
				uniform float fInnerRadius2;	// 


				uniform float fKrESun;			// Kr * ESun
				uniform float fKmESun;			// Km * ESun
				uniform float fKr4PI;			// Kr * 4 * PI
				uniform float fKm4PI;			// Km * 4 * PI
				uniform float fScale;			// 1 / (fOuterRadius - fInnerRadius)
				uniform float fScaleDepth;		//
				uniform float fScaleOverScaleDepth; // fScale / fScaleDepth

				uniform float fExposure = 0.7;		// HDR 


				uniform float3 v3CameraPos;		// camera
				uniform float3 v3LightDir;		// light source
				uniform float3 v3LightDirMoon;		// moon light
				uniform float3 v3InvWavelength; //  	

				uniform float fCameraHeight;    // height
				uniform float fCameraHeight2;   // 			  	

				//	
				uniform int nSamples = 2;
				uniform float fSamples = 2.0;

				uniform float Bump_strenght = 2;
				uniform float _Coloration = 0;

				uniform float Horizon_adj = 0;
				uniform float HorizonY = 0;

				uniform float g;				// The Mie phase asymmetry factor
				uniform float g2;				// The Mie phase asymmetry factor squared
				fixed4 _TintColor;
				fixed4 GroundColor;

				float4 _Obliqueness;
				float White_cutoff;
				float SunBlend;
				float3 SunColor;


				//v0.1
				sampler2D _MainTexSKYBOX;
				float4 _MainTex_TexelSize;
				half4 _MainTex_HDR;
				half4 _Tint;
				half _Exposure;
				float _Rotation;
	#ifndef _MAPPING_6_FRAMES_LAYOUT
				bool _MirrorOnBack;
				int _ImageType;
				int _Layout;
	#endif
	#ifndef _MAPPING_6_FRAMES_LAYOUT
				inline float2 ToRadialCoords(float3 coords)
				{
					float3 normalizedCoords = normalize(coords);
					float latitude = acos(normalizedCoords.y);
					float longitude = atan2(normalizedCoords.z, normalizedCoords.x);
					float2 sphereCoords = float2(longitude, latitude) * float2(0.5 / UNITY_PI, 1.0 / UNITY_PI);
					return float2(0.5, 1.0) - sphereCoords;
				}
	#endif
	#ifdef _MAPPING_6_FRAMES_LAYOUT
				inline float2 ToCubeCoords(float3 coords, float3 layout, float4 edgeSize, float4 faceXCoordLayouts, float4 faceYCoordLayouts, float4 faceZCoordLayouts)
				{
					// Determine the primary axis of the normal
					float3 absn = abs(coords);
					float3 absdir = absn > float3(max(absn.y, absn.z), max(absn.x, absn.z), max(absn.x, absn.y)) ? 1 : 0;
					// Convert the normal to a local face texture coord [-1,+1], note that tcAndLen.z==dot(coords,absdir)
					// and thus its sign tells us whether the normal is pointing positive or negative
					float3 tcAndLen = mul(absdir, float3x3(coords.zyx, coords.xzy, float3(-coords.xy, coords.z)));
					tcAndLen.xy /= tcAndLen.z;
					// Flip-flop faces for proper orientation and normalize to [-0.5,+0.5]
					bool2 positiveAndVCross = float2(tcAndLen.z, layout.x) > 0;
					tcAndLen.xy *= (positiveAndVCross[0] ? absdir.yx : (positiveAndVCross[1] ? float2(absdir[2], 0) : float2(0, absdir[2]))) - 0.5;
					// Clamp values which are close to the face edges to avoid bleeding/seams (ie. enforce clamp texture wrap mode)
					tcAndLen.xy = clamp(tcAndLen.xy, edgeSize.xy, edgeSize.zw);
					// Scale and offset texture coord to match the proper square in the texture based on layout.
					float4 coordLayout = mul(float4(absdir, 0), float4x4(faceXCoordLayouts, faceYCoordLayouts, faceZCoordLayouts, faceZCoordLayouts));
					tcAndLen.xy = (tcAndLen.xy + (positiveAndVCross[0] ? coordLayout.xy : coordLayout.zw)) * layout.yz;
					return tcAndLen.xy;
				}
	#endif
				float3 RotateAroundYInDegrees(float3 vertex, float degrees)
				{
					float alpha = degrees * UNITY_PI / 180.0;
					float sina, cosa;
					sincos(alpha, sina, cosa);
					float2x2 m = float2x2(cosa, -sina, sina, cosa);
					return float3(mul(m, vertex.xz), vertex.y).xzy;
				}
				float SkyBoxBlend;
				//END v0.1



				struct fragIO
				{
					float3 c0 : COLOR0;
					float3 c1 : COLOR1;
					float3 v3Direction : TEXCOORD0;
					float4 pos : SV_POSITION;
					float3 uv : TEXCOORD1;
					half4 normalAndSunExp : TEXCOORD2;

	#ifdef _MAPPING_6_FRAMES_LAYOUT
					float3 layout : TEXCOORD3;
					float4 edgeSize : TEXCOORD4;
					float4 faceXCoordLayouts : TEXCOORD5;
					float4 faceYCoordLayouts : TEXCOORD6;
					float4 faceZCoordLayouts : TEXCOORD7;
	#else
					float2 image180ScaleAndCutoff : TEXCOORD3;
					float4 layout3DScaleAndOffset : TEXCOORD4;
	#endif
					UNITY_VERTEX_OUTPUT_STEREO
				};

				float scale(float fCos)
				{
					float x = 1.0 - fCos;
					return fScaleDepth * exp(-0.00287 + x * (0.459 + x * (3.83 + x * (-6.80 + x * 5.25))));
				}

				float getNearIntersection(float3 pos, float3 ray, float distance, float radius) {
					float B = 2.0 * dot(ray ,pos);
					float C = distance - radius;
					float det = max(0.0, B*B - 4.0 * C);
					return 0.5 * (-B - sqrt(det));
				}
				uniform float4 _MainTex_ST;
				uniform float4 _ColorRamp_ST;

				fragIO vert(appdata_base v)
				{


					fragIO OUTPUT;
					UNITY_SETUP_INSTANCE_ID(v);
					UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);



					float3 rotated = RotateAroundYInDegrees(v.vertex, _Rotation);
					OUTPUT.pos = UnityObjectToClipPos(rotated);
					OUTPUT.uv = v.vertex.xyz;
	#ifdef _MAPPING_6_FRAMES_LAYOUT
					// layout and edgeSize are solely based on texture dimensions and can thus be precalculated in the vertex shader.
					float sourceAspect = float(_MainTex_TexelSize.z) / float(_MainTex_TexelSize.w);
					// Use the halfway point between the 1:6 and 3:4 aspect ratios of the strip and cross layouts to
					// guess at the correct format.
					bool3 aspectTest =
						sourceAspect >
						float3(1.0, 1.0f / 6.0f + (3.0f / 4.0f - 1.0f / 6.0f) / 2.0f, 6.0f / 1.0f + (4.0f / 3.0f - 6.0f / 1.0f) / 2.0f);
					// For a given face layout, the coordinates of the 6 cube faces are fixed: build a compact representation of the
					// coordinates of the center of each face where the first float4 represents the coordinates of the X axis faces,
					// the second the Y, and the third the Z. The first two float componenents (xy) of each float4 represent the face
					// coordinates on the positive axis side of the cube, and the second (zw) the negative.
					// layout.x is a boolean flagging the vertical cross layout (for special handling of flip-flops later)
					// layout.yz contains the inverse of the layout dimensions (ie. the scale factor required to convert from
					// normalized face coords to full texture coordinates)
					if (aspectTest[0]) // horizontal
					{
						if (aspectTest[2])
						{ // horizontal strip
							OUTPUT.faceXCoordLayouts = float4(0.5, 0.5, 1.5, 0.5);
							OUTPUT.faceYCoordLayouts = float4(2.5, 0.5, 3.5, 0.5);
							OUTPUT.faceZCoordLayouts = float4(4.5, 0.5, 5.5, 0.5);
							OUTPUT.layout = float3(-1, 1.0 / 6.0, 1.0 / 1.0);
						}
						else
						{ // horizontal cross
							OUTPUT.faceXCoordLayouts = float4(2.5, 1.5, 0.5, 1.5);
							OUTPUT.faceYCoordLayouts = float4(1.5, 2.5, 1.5, 0.5);
							OUTPUT.faceZCoordLayouts = float4(1.5, 1.5, 3.5, 1.5);
							OUTPUT.layout = float3(-1, 1.0 / 4.0, 1.0 / 3.0);
						}
					}
					else
					{
						if (aspectTest[1])
						{ // vertical cross
							OUTPUT.faceXCoordLayouts = float4(2.5, 2.5, 0.5, 2.5);
							OUTPUT.faceYCoordLayouts = float4(1.5, 3.5, 1.5, 1.5);
							OUTPUT.faceZCoordLayouts = float4(1.5, 2.5, 1.5, 0.5);
							OUTPUT.layout = float3(1, 1.0 / 3.0, 1.0 / 4.0);
						}
						else
						{ // vertical strip
							OUTPUT.faceXCoordLayouts = float4(0.5, 5.5, 0.5, 4.5);
							OUTPUT.faceYCoordLayouts = float4(0.5, 3.5, 0.5, 2.5);
							OUTPUT.faceZCoordLayouts = float4(0.5, 1.5, 0.5, 0.5);
							OUTPUT.layout = float3(-1, 1.0 / 1.0, 1.0 / 6.0);
						}
					}
					// edgeSize specifies the minimum (xy) and maximum (zw) normalized face texture coordinates that will be used for
					// sampling in the texture. Setting these to the effective size of a half pixel horizontally and vertically
					// effectively enforces clamp mode texture wrapping for each individual face.
					OUTPUT.edgeSize.xy = _MainTex_TexelSize.xy * 0.5 / OUTPUT.layout.yz - 0.5;
					OUTPUT.edgeSize.zw = -OUTPUT.edgeSize.xy;
	#else // !_MAPPING_6_FRAMES_LAYOUT
					// Calculate constant horizontal scale and cutoff for 180 (vs 360) image type
					if (_ImageType == 0)  // 360 degree
						OUTPUT.image180ScaleAndCutoff = float2(1.0, 1.0);
					else  // 180 degree
						OUTPUT.image180ScaleAndCutoff = float2(2.0, _MirrorOnBack ? 1.0 : 0.5);
					// Calculate constant scale and offset for 3D layouts
					if (_Layout == 0) // No 3D layout
						OUTPUT.layout3DScaleAndOffset = float4(0, 0, 1, 1);
					else if (_Layout == 1) // Side-by-Side 3D layout
						OUTPUT.layout3DScaleAndOffset = float4(unity_StereoEyeIndex, 0, 0.5, 1);
					else // Over-Under 3D layout
						OUTPUT.layout3DScaleAndOffset = float4(0, 1 - unity_StereoEyeIndex, 1, 0.5);
	#endif






					float3 v3Pos = v.vertex.xyz;// - (_WorldSpaceCameraPos - v3CameraPos)/480000;


					float3 v3Ray = v3Pos;// + (_WorldSpaceCameraPos - v3CameraPos)/50000 ;						

					float fNear = getNearIntersection(v3Ray, v3Pos, fCameraHeight2, fOuterRadius2);

					float fFar = length(v3Ray);
					v3Ray /= fFar;

					float3 v3Start = ((v3Ray - 0) * (0 + 10.1)) + (-v3CameraPos) - float3(0,-Horizon_adj,0); //v3.4.2 - removed _WorldSpaceCameraPos	in editor

					fCameraHeight = length(v3CameraPos);		//v3.4.2 - removed _WorldSpaceCameraPos	in editor		

					float3 AA = normalize(mul((float3x3)unity_ObjectToWorld, v.vertex.xyz));
					OUTPUT.normalAndSunExp.xyz = AA;
					OUTPUT.normalAndSunExp.w = (6 > 0) ? (256.0 / 6) : 0.0;
					//OUTPUT.normalAndSunExp.w = 0;

		//				if(AA.y <=-0.07){
		//					fScaleOverScaleDepth = - fScaleOverScaleDepth;
		//				}										
						float fInvScaleDepth = -(fScaleOverScaleDepth * (fInnerRadius - fCameraHeight));

						float fStartDepth = exp(-fInvScaleDepth);
							float fStartAngle = dot(v3Ray, v3Start) / fOuterRadius;
							float fStartOffset = fStartDepth * scale(fStartAngle);

						float fSampleLength = fFar / fSamples;
						float fScaledLength = fSampleLength * fScale;
						float3 v3SampleRay = v3Ray * fSampleLength;
						float3 v3SamplePoint = v3Start + v3SampleRay * 0.5;

						float3 v3FrontColor = float3(0.0, 0.0, 0.0);

						nSamples = 2;


						for (int i = 0; i < nSamples; i++)
						{
							float fHeight = length(v3SamplePoint);
							float fDepth = exp(fScaleOverScaleDepth * (fInnerRadius - fHeight));
							float fLightAngle = dot(v3LightDir, v3SamplePoint) / fHeight;
							float fCameraAngle = dot(v3Ray, v3SamplePoint) / fHeight;
							float fScatter = (fStartOffset + fDepth * (scale(fLightAngle) - scale(fCameraAngle))) * 1;
							float3 v3Attenuate = exp(-fScatter * (v3InvWavelength * fKr4PI + fKm4PI));
							v3FrontColor += v3Attenuate * (fDepth * fScaledLength);
							v3SamplePoint += v3SampleRay;
						}

						//UNITY_INITIALIZE_OUTPUT(fragIO,OUTPUT);


				//		OUTPUT.pos = mul(UNITY_MATRIX_MVP, v.vertex);
						OUTPUT.pos = UnityObjectToClipPos(v.vertex);

						//v.2.0.1
						OUTPUT.pos.y += _Obliqueness.z * OUTPUT.pos.y;
						OUTPUT.pos.x += _Obliqueness.x * OUTPUT.pos.x;

		//				OUTPUT.uv = v.texcoord.xy; //v0.1 removed


						OUTPUT.c0 = (v3FrontColor) * (v3InvWavelength * fKrESun); //* float4(distance(v3LightDir,v3Pos),0,0,1)
						OUTPUT.c1 = (v3FrontColor)* fKmESun;
						//OUTPUT.v3Direction = (_WorldSpaceCameraPos - v3CameraPos) - v3Pos;

						OUTPUT.v3Direction = v.vertex.xyz;


						return OUTPUT;

						//NOTES - Play with Fexposure or G, to get an eclipse !!!!!!!!!!!

					}

				 sampler2D _MainTex;
				 sampler2D _ColorRamp;


				 //  Mie 
				 float getMiePhase(float fCos, float fCos2, float g, float g2)
				 {
					 return 1.5 * ((1.0 - g2) / (2.0 + g2)) * (1.0 + fCos2) / pow(1.0 + g2 - 2.0*g*fCos, 1.5);
				 }

				 //  Rayleigh
				 float getRayleighPhase(float fCos2)
				 {
					 return 0.75 + 0.75*fCos2;
				 }



			fixed4 frag(fragIO IN) : COLOR
			{
					float3 NEW_D = normalize(IN.v3Direction);
					float4 result;
					result = tex2D(_MainTex, _MainTex_ST.xy * IN.uv);
					///////////

					float fCos = dot(-v3LightDir, NEW_D);
					float fCos2 = fCos * fCos;
					float3 Scolor = (getRayleighPhase(fCos2) + ((1.5 * ((1.0 - g2) / (2.0 + g2)) * (1.0 + fCos2) / pow(1.0 + g2 - 2.0*g*fCos, 1.5)))) * IN.c0 + getMiePhase(fCos, fCos2, g, g2) * IN.c1;

					Scolor = 1.0 - exp((-fExposure / 0.01) * (Scolor));
					float4 Out_Color = (float4(Scolor,1.0) + (_Coloration*result)) + _TintColor;

					if (IN.normalAndSunExp.y < HorizonY) {
						Out_Color = GroundColor;
					}
					result = tex2D(_ColorRamp, _ColorRamp_ST.xy * IN.uv);

					//v3.0
					if (Out_Color.r >= White_cutoff) {
						if (Out_Color.g >= White_cutoff) {
							if (Out_Color.b >= White_cutoff) {
								float A = distance(v3LightDir, IN.v3Direction) * SunBlend;//26;
								Out_Color = float4(lerp(Out_Color.r, SunColor.r, 2.2 - A), lerp(Out_Color.g, SunColor.g, 2.25 - A) + result.g / 3, lerp(Out_Color.b, SunColor.b, 2.25 - A) + result.g / 1, 1);
							}
						}
					}


					//v0.1
#ifdef _MAPPING_6_FRAMES_LAYOUT
					float2 tc = ToCubeCoords(IN.uv, IN.layout, IN.edgeSize, IN.faceXCoordLayouts, IN.faceYCoordLayouts, IN.faceZCoordLayouts);
#else
					float2 tc = ToRadialCoords(IN.uv);
					if (tc.x > IN.image180ScaleAndCutoff[1])
						return half4(0, 0, 0, 1);
					tc.x = fmod(tc.x*IN.image180ScaleAndCutoff[0], 1);
					tc = (tc + IN.layout3DScaleAndOffset.xy) * IN.layout3DScaleAndOffset.zw;
#endif

					half4 tex = tex2D(_MainTexSKYBOX, tc);
					half3 c = DecodeHDR(tex, _MainTex_HDR);
					c = c * _Tint.rgb * unity_ColorSpaceDouble.rgb;
					c *= _Exposure;

					Out_Color.rgb = Out_Color.rgb + SkyBoxBlend * c.rgb;
					//END v0.1

					return Out_Color;//+2*_Coloration*result1;
		}

				 ENDCG
			 }
			}
}