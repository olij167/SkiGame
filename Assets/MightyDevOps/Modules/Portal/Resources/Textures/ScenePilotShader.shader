// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Unlit/ScenePilotShader"

{
Properties {
    _MainTex ("Base (RGB)", 2D) = "white" {}
    _AlphaTex ("Alpha mask (R)", 2D) = "white" {}
    _Alpha("Alpha" , Range(0,1)) = 0.1
    _Brightness("Brightness" , Range(-1,1)) = 1.0
}
 
SubShader {
    Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
      Lighting Off
      ZWrite Off
 
    Blend SrcAlpha OneMinusSrcAlpha
    
    Pass {
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
         
            #include "UnityCG.cginc"
         
 
            struct appdata_t {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
            };
 
            struct v2f {
                float4 vertex : SV_POSITION;
                half2 texcoord : TEXCOORD0;
            };
 
            sampler2D _MainTex;
            sampler2D _AlphaTex;
         
            float4 _MainTex_ST;
            float _Alpha;
            float _Brightness;
          
            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }
         
            fixed4 frag (v2f i) : SV_Target
            {
                // Subtle animated UV distortion
                float wave = sin(_Time.y * 0.5 + i.texcoord.x * 10.0 + i.texcoord.y * 8.0) * 0.015;
                float2 uv = i.texcoord + float2(wave, wave * 0.4);
                fixed4 col = tex2D(_MainTex, uv);
                fixed4 col2 = tex2D(_AlphaTex, uv);

                // Rim only at the edge
                float2 center = float2(0.5, 0.5);
                float dist = saturate(distance(uv, center) * 2.0); // 0 at center, 1 at edge

                float3 worldPos = mul(unity_ObjectToWorld, float4(i.vertex.xy, 0, 1)).xyz;
                float3 viewDir = normalize(_WorldSpaceCameraPos - worldPos);
                float fresnel = pow(1.0 - saturate(dot(viewDir, float3(0,0,1))), 3.0);
                float3 rimColor = float3(1.0, 1.0, 1.0); // pure white

                // Only add rim at the edge
                col.rgb += rimColor * fresnel * 0.5 * dist;

                // Apply brightness and alpha as before
                col = fixed4(col.r*(1+_Brightness), col.g*(1+_Brightness), col.b*(1+_Brightness), col2.a*_Alpha);

                return col;
            }
        ENDCG
    }
}
 
}

// {
//    Properties
//    {
//       _Color ("Mask Color", Color) = (0, 0, 0, 1) ////////// _Color property added to this shader
//       _MainTex ("Base (RGB)", 2D) = "white" {}
//       _Mask ("Culling Mask", 2D) = "white" {}
//       _Cutoff ("Alpha cutoff", Range (0,1)) = 0.1
//    }
//    SubShader
//    {
//       Tags {"Queue"="Transparent"}
//       //Cull Off ////////// I added this line to have the both sides of my plane mesh visible
//       Lighting Off
//       ZWrite Off
//       Blend SrcAlpha DstColor
//       AlphaTest GEqual [_Cutoff]
//       Pass
//       {
//          SetTexture [_Mask] {
//              constantColor [_Color]
//              Combine texture * constant ////////// The alpha component of the _Color property defines the Culling Mask opacity (depending on the distance between the player and the grid)
//             }
//          SetTexture [_MainTex] {combine texture, previous}
//       }
//    }
// }