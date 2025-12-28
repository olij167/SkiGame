Shader "SkyMaster/SnowShaderSM3_Reflect RainDrops" {
    Properties {    	
        //_SnowCoverage ("Snow Coverage", Range(0, 1)) = 0   
        _SnowBlend ("Snow Blend", Range(0, 50)) = 0.4     
        _LightIntensity ("Light Intensity", Range(-0.5, 50)) = 1
        _SnowBumpDepth ("Snow bump depth", Range(0, 5)) = 1          
        _MainTex ("Base (RGB)", 2D) = "white" {}
        _Bump ("Bump", 2D) = "bump" {}        
        _SnowTexture ("Snow texture", 2D) = "white" {}
        _Depth ("Depth of Snow", Range(0, 0.02)) = 0.01        
        _SnowBump ("Snow Bump", 2D) ="bump" {}        
        _Direction ("Direction of snow", Vector) = (0, 1, 0)         
        _Shininess ("Shininess", Range (0.01, 1)) = 0.078125
        _Wetness ("Wetness", Range (-0, 20)) = 1
        
        _Mask ("Mask", 2D) = "black" {}
        _MainTex2 ("Albedo 1", 2D) = "white" {}
        _NormalMap2 ("NormalMap 1", 2D) = "bump" {}		
		_Water ("Water", 2D) = "white" {}		
		water_level ("Water level", Float) = 1

			water_height ("Water height", Float) = 10

		water_spec ("Water Spec Focus", Float) = 1.2		
		water_tiling ("Water tiling", Float) = 2		
		_BumpPower ("Bump Power", Range (3, 0.01)) = 1 
		
		_Color ("Tint", Color) = (1,1,1,1) 
		
		//v3.0.2
		Snow_Cover_offset ("Snow coverage offset", Float) = 0
		
		[LM_Specular] [LM_Glossiness] _SpecGlossMap("Specular 0", 2D) = "white" {}
		[LM_Specular] [LM_Glossiness] _SpecGlossMap2("Specular 1", 2D) = "white" {}

		//v3.4
		_Cube ("Refelction Map",Cube) = "" {}

        //v4.7 Rain ripples
        _Lux_RainRipples("_Lux_RainRipples", 2D) = "white" {}
        _Lux_RippleWindSpeed("_Lux_RippleWindSpeed", Float) = (0, 0, 0, 0)
        _Lux_WaterFloodlevel("_Lux_WaterFloodlevel", Float) = (0, 0, 0, 0)
        _Lux_RainIntensity("_Lux_RainIntensity", Float) = 0.75
        _Lux_RippleAnimSpeed("_Lux_RippleAnimSpeed", Float) = 0.75
        _Lux_RippleTiling("_Lux_RippleTiling", Float) = 0.75
        _Lux_WaterBumpDistance("_Lux_WaterBumpDistance", Float) = 0.75

       // _puddlesTex("_puddles Tex", 2D) = "white" {}
        _puddlesTexPower("_puddles Tex Power", Float) = 0.75
            _puddlesTexExp("_puddles Tex Exponent", Float) = 1
             _puddlesTexOffset("_puddles Tex Offset", Float) = 0

              _usePlanarReflections("_usePlanarReflections", Float) = 0
            _ReflectionTex("Internal reflection", 2D) = "white" {}
        _planarReflectionDistort("_planarReflectionDistort", Float) = 0
            _ReflectionColor("_ReflectionColor", Color) = (0.1,0.1,0.1,0)
       _FresnelScale("FresnelScale", Range(0.15, 4.0)) = 0.75
            _DistortParams("Distortions (Bump waves, Reflection, Fresnel power, Fresnel bias)", Vector) = (1.0 ,1.0, 2.0, 1.15)
    }

        SubShader{
            Tags { "RenderType" = "Opaque" }
            LOD 200

            CGPROGRAM
            #pragma target 5.0
            #pragma surface surf StandardSpecular fullforwardshadows vertex:vert
            #pragma exclude_renderers gles
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

        //v0.1
       // sampler2D _puddlesTex;
        //float4 _Mask_ST;
        float _puddlesTexPower;
        float _puddlesTexExp;
        float _puddlesTexOffset;
        float _usePlanarReflections;
        sampler2D _ReflectionTex;
        float _planarReflectionDistort;
        float4 _ReflectionColor;
        float _FresnelScale;
        uniform float4 _DistortParams;
        inline half Fresnel(half3 viewVector, half3 worldNormal, half bias, half power)
        {
            half facing = clamp(1.0 - max(dot(-viewVector, worldNormal), 0.0), 0.0, 1.0);
            half refl2Refr = saturate(bias + (1.0 - bias) * pow(facing, power));
            return refl2Refr;
        }

        float _SnowCoverage; 
        float _SnowBlend;  
        float _LightIntensity;
        float _SnowBumpDepth;
                  
        sampler2D _MainTex;
        sampler2D _Bump;        
        sampler2D _SnowTexture;
        float _Depth;        
        sampler2D _SnowBump;        
        float3 _Direction;
        half _Shininess;
        half _Wetness; 
 		
 		sampler2D _MainTex2;
		sampler2D _NormalMap2;
		sampler2D _Water;	
		sampler2D _SpecGlossMap;
		sampler2D _SpecGlossMap2;	
		sampler2D _Mask;		
		uniform float water_level;
		uniform float water_tiling;
		uniform float water_spec;
		
		fixed _BumpPower;
		float4 _Color;
		float Snow_Cover_offset;


		//v3.4
		uniform samplerCUBE _Cube;
		float water_height;


        //v4.7 Rain ripples
    // Global Rain Properties passed in by Script
        float2 _Lux_WaterFloodlevel;
        float _Lux_RainIntensity;
        sampler2D _Lux_RainRipples;
        float4 _Lux_RippleWindSpeed;
        float _Lux_RippleAnimSpeed;
        float _Lux_RippleTiling;
        float _Lux_WaterBumpDistance;
        float2 ComputeRipple(float4 UV, float CurrentTime, float Weight)
        {
            float4 Ripple = tex2Dlod(_Lux_RainRipples, UV);
            // We use multi sampling here in order to improve Sharpness due to the lack of Anisotropic Filtering when using tex2Dlod
            Ripple += tex2Dlod(_Lux_RainRipples, float4(UV.xy, UV.zw * 0.5));
            Ripple *= 0.5;

            Ripple.yz = Ripple.yz * 2 - 1; // Decompress Normal
            float DropFrac = frac(Ripple.w + CurrentTime); // Apply time shift
            float TimeFrac = DropFrac - 1.0f + Ripple.x;
            float DropFactor = saturate(0.2f + Weight * 0.8f - DropFrac);
            float FinalFactor = DropFactor * Ripple.x * sin(clamp(TimeFrac * 9.0f, 0.0f, 3.0f) * 3.141592653589793);
            return Ripple.yz * FinalFactor * 0.35f;
        }
        //  Add Water Ripples to Waterflow
        float3 AddWaterFlowRipples(float2 i_wetFactor, float3 i_worldPos, float2 lambda, float i_worldNormalFaceY, float fadeOutWaterBumps)
        {
            float4 Weights = _Lux_RainIntensity - float4(0, 0.25, 0.5, 0.75);
            Weights = saturate(Weights * 4);
            float animSpeed = _Time.y * _Lux_RippleAnimSpeed;
            float2 Ripple1 = ComputeRipple(float4(i_worldPos.xz * _Lux_RippleTiling + float2(0.25f, 0.0f), lambda), animSpeed, Weights.x);
            float2 Ripple2 = ComputeRipple(float4(i_worldPos.xz * _Lux_RippleTiling + float2(-0.55f, 0.3f), lambda), animSpeed * 0.71, Weights.y);
            float3 rippleNormal = float3(Weights.x * Ripple1.xy + Weights.y * Ripple2.xy, 1);
            // Blend and fade out Ripples 
            return lerp(float3(0, 0, 1), rippleNormal, i_wetFactor.y * i_wetFactor.y * fadeOutWaterBumps * i_worldNormalFaceY * i_worldNormalFaceY);
        }
        //END v4.7 Rain ripples


 
        struct Input {        
            float2 uv_MainTex;
            float2 uv_Bump;
            float2 uv_SnowTexture;
            float2 uv_SnowBump;
            float3 worldNormal;      
            
            float2 uv_MainTex2;
			float2 uv_Water;
			float2 uv_Mask;

			float3 worldRefl;
			float3 worldPos;
            float4 screenPos;
			INTERNAL_DATA
        };        
       
        void vert (inout appdata_full v) {            
            float3 Snow = normalize(_Direction.xyz);
           
            if (dot(v.normal, Snow) >= lerp(1, -1, ((_SnowCoverage+Snow_Cover_offset) * 2) / 3))
            {
                v.vertex.xyz += normalize(v.normal + Snow)  * (_SnowCoverage+Snow_Cover_offset) * _Depth;
            }        

            //o.viewInterpolator.xyz = worldSpaceVertex.xyz - _WorldSpaceCameraPos;
        }
 

        //v0.1
        float4 screenPos;

        void surf (Input IN, inout SurfaceOutputStandardSpecular o) {   
   
   			//PUDDLES
   			float2 Mask_motion = float2(IN.uv_Mask.x,IN.uv_Mask.y+(_Time.x*5))*0.05 ;
			float2 A_motion = float2(IN.uv_MainTex.x,IN.uv_MainTex.y+(_Time.x*5))*0.005 ;
			float2 B_motion = float2(IN.uv_MainTex2.x,IN.uv_MainTex2.y+(_Time.x*1)) ;
		
			fixed blend = tex2D(_Mask, IN.uv_Mask).a*1;	
			fixed4 albedo1 = tex2D(_MainTex, IN.uv_MainTex);
			fixed4 spec1	= tex2D(_SpecGlossMap, IN.uv_MainTex);
		 	fixed3 normal1 = UnpackNormal (tex2D (_Bump, IN.uv_MainTex)); //_NormalMap

			half4 flow = float4(1,1,1,1);
			half4 flow1 = tex2D(_Water, float2(IN.uv_MainTex2.x*water_tiling,IN.uv_MainTex2.y+(_Time.x*0.5)) );

			fixed4 albedo2 = tex2D(_MainTex2, float2(IN.uv_MainTex2.x+flow.r,IN.uv_MainTex2.y+flow.r));
			fixed4 spec2 = tex2D(_SpecGlossMap2, float2(IN.uv_MainTex2.x+flow.r,IN.uv_MainTex2.y+flow.r)) * _Wetness;

		//	spec1.rgb *=  texCUBE (_Cube,WorldReflectionVector(IN,o.Normal)).rgb*1000;
		//	spec2.rgb *=  texCUBE (_Cube,WorldReflectionVector(IN,o.Normal)).rgb*1000;

		 	fixed3 normal2 = UnpackNormal (tex2D (_NormalMap2, float2(IN.uv_MainTex2.x+flow.r,IN.uv_MainTex2.y+flow.r)+ (0.03)  ));
		 	fixed4 specGloss = lerp (spec1, spec2, blend);
   			//END PUDDLES  

            //v0.1
            float maskPuddlesT = 1-tex2D(_Mask, IN.uv_Mask * 1).r;
            float maskPuddles = pow(maskPuddlesT, _puddlesTexExp) * _puddlesTexPower + _puddlesTexOffset;

           

            //v4.7 Rain ripples
            float3 rippleNormal = float3(0, 0, 1);
            float2 wetFactor = float2(0.1, 0.1);
            float2 lambda = float2(1, 1);
            float fadeOutWaterBumps = saturate((_Lux_WaterBumpDistance - distance(_WorldSpaceCameraPos, IN.worldPos)) / 5);
            if (_Lux_RainIntensity > 0) {                
                rippleNormal = AddWaterFlowRipples(wetFactor, IN.worldPos, lambda, 1, fadeOutWaterBumps);
            }            
            //END v4.7 Rain ripples
            //worldNormal.xyz = worldNormal.xyz + 0.2 * saturate(fadeOutWaterBumps * _Lux_RainIntensity * 0.5 * saturate(1110.5 * rippleNormal.xzy));
            IN.worldNormal = IN.worldNormal + 0.2 * saturate(fadeOutWaterBumps * _Lux_RainIntensity * 0.5 * saturate(1110.5 * rippleNormal.xzy));
            normal1 = normal1 + 0.2 * saturate(fadeOutWaterBumps * _Lux_RainIntensity * 0.5 * saturate(1110.5 * rippleNormal.xzy));


            _SnowCoverage = 0;
   			//SNOW
   			float4 SnowTexColor = tex2D(_SnowTexture, IN.uv_SnowTexture);
            float4 MainTexColor = tex2D(_MainTex, IN.uv_MainTex);
            o.Normal = UnpackNormal(tex2D(_Bump, IN.uv_Bump));           

            o.Alpha = MainTexColor.a ;  
            float DirN = dot(WorldNormalVector(IN, o.Normal), _Direction.xyz) * (maskPuddles + 0);
            float Check = lerp(1,-1,(_SnowCoverage+Snow_Cover_offset)/5.5); //divide by 6 to synch with the slower building of snow on Unity trees
            if(DirN >= Check)
            {                
                o.Albedo = lerp (  lerp (albedo1 , albedo2, blend) , SnowTexColor.rgb*_LightIntensity,pow((1-(Check/DirN)),_SnowBlend)) ;
                o.Normal = normalize(o.Normal + UnpackNormal(tex2D(_SnowBump, IN.uv_SnowBump))*_SnowBumpDepth) ;
                o.Specular = _Shininess ;                                 
            }
            else{            	
            	//PUDDLES INTEGRATION

            
            float diff = IN.worldPos.y - water_height;

            	if(diff < 0){

                   
                                       
                    o.Smoothness = (saturate(water_spec * specGloss.a) + water_level * flow1 * specGloss.a);

                    if (_usePlanarReflections == 1) {
                        half4 distortOffset = half4(WorldNormalVector(IN, o.Normal).xz * _planarReflectionDistort * 10.0 , 0, 0);
                        // float2 coords = IN.screenPos.xy / IN.screenPos.w + distortOffset;
                        float4 screenWithOffset = IN.screenPos + distortOffset * 0.01;// +float4(_Time.y * 2, 0, 0, 0);
                        // half4 screenWithOffset = screenPos + distortOffset;
                        half4 rtReflections = tex2Dproj(_ReflectionTex, UNITY_PROJ_COORD(screenWithOffset));//_usePlanarReflections
                        half4 reflectionColor = lerp(rtReflections, _ReflectionColor*2.2, _ReflectionColor.a);

                        float3 wnormal = WorldNormalVector(IN, o.Normal).xyz + IN.worldNormal;
                        wnormal.xz *= _FresnelScale;
                        float3 worldPos =IN.worldPos;// mul(unity_ObjectToWorld, float4(IN.vertex.xyz, 1)).xyz;
                        half3 viewVector = normalize(worldPos - _WorldSpaceCameraPos);
                        half refl2Refr = Fresnel(viewVector, wnormal, _DistortParams.w, _DistortParams.z);

                        half3 baseColor = lerp((albedo2 * saturate(diff)), reflectionColor.rgb , refl2Refr);

                        o.Albedo = baseColor ;// +rtReflections;
                    }
                    else {
                        o.Albedo = (albedo2 * saturate(diff)) + texCUBE(_Cube, WorldReflectionVector(IN, o.Normal)).rgb * 0.1;// +rtReflections;
                    }
                   // o.Smoothness = 0;
            		
            		//o.Emission  = texCUBE (_Cube,WorldReflectionVector(IN,o.Normal)).rgb*0.1; 
            	}else{
            		o.Albedo 		= lerp (albedo1, albedo2, blend) ;
            		o.Smoothness 	= (water_spec*specGloss.a  *2 ) ;
            	}

			 	//o.Specular 		= specGloss.rgb ;
			 	 o.Specular = _Shininess;  

				
			  	o.Normal 		= lerp (normal1, normal2, blend);
			 	o.Normal.z = o.Normal.z*_BumpPower; 
			 	o.Normal = normalize(o.Normal) + IN.worldNormal*10;
	   			//END PUDDLES


            } 	
			 
			///o.Emission  = texCUBE (_Cube,WorldReflectionVector(IN,o.Normal)).rgb*0.1; //      
        }
        ENDCG
    }
    FallBack "Diffuse"
}