Shader "LeapForward/SpriteRenderer/Albedo"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Main Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        // Tag as Opaque so the RenderWithShader tool picks it up
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Sample the texture and multiply by the tint color
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;
                
                // Ensure the alpha is 1 here; the camera transparency 
                // in your tool will handle the background masking.
                return col;
            }
            ENDCG
        }
    }
}