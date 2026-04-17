Shader "Custom/HeatmapShader"
{
    Properties
    {
        _MainTex ("Base (RGB)", 2D) = "white" {}
        _HeatmapTex ("Heatmap (RGBA)", 2D) = "white" {}
        _HeatmapOrigin ("Heatmap Origin", Vector) = (0, 0, 0, 0)
        _HeatmapCellSize ("Heatmap Cell Size", Float) = 1.0
        _HeatmapGridWidth ("Heatmap Grid Width", Float) = 1.0
        _HeatmapGridHeight ("Heatmap Grid Height", Float) = 1.0
        _WorldBottomLeft ("World Bottom Left", Vector) = (0, 0, 0, 0)
        _WorldTopRight ("World Top Right", Vector) = (1, 1, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float2 texcoord : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            sampler2D _HeatmapTex;
            float4 _HeatmapOrigin;
            float _HeatmapCellSize;
            float _HeatmapGridWidth;
            float _HeatmapGridHeight;
            float4 _WorldBottomLeft;
            float4 _WorldTopRight;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Calculate world coordinates
                float worldX = lerp(_WorldBottomLeft.x, _WorldTopRight.x, i.texcoord.x);
                float worldY = lerp(_WorldBottomLeft.y, _WorldTopRight.y, i.texcoord.y);

                // Determine the corresponding cell in the heatmap
                float gridX = (worldX - _HeatmapOrigin.x) / _HeatmapCellSize;
                float gridY = (worldY - _HeatmapOrigin.y) / _HeatmapCellSize;

                // Clamp the coordinates to avoid wrapping
                gridX = clamp(gridX, 0.0, _HeatmapGridWidth - 1.0);
                gridY = clamp(gridY, 0.0, _HeatmapGridHeight - 1.0);

                // Sample the heatmap texture
                fixed4 heatmapColor = tex2D(_HeatmapTex, float2(gridX / _HeatmapGridWidth, gridY / _HeatmapGridHeight));

                // Handle alpha correctly
                if (heatmapColor.a == 0.0)
                {
                    heatmapColor.rgb = float3(0, 0, 0); // Transparent areas should not add color
                }

                // Blend heatmap color with base color
                fixed4 baseColor = tex2D(_MainTex, i.texcoord);
                return lerp(baseColor, heatmapColor, heatmapColor.a);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
