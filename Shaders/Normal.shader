Shader "LeapForward/SpriteRenderer/Normal"
{
    Properties
    {
        _MainTex ("Base (RGB)", 2D) = "white" {}
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        Lighting Off // Explicitly disable lighting

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
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3x3 tbnView : TEXCOORD3; 
            };

            sampler2D _MainTex;
            sampler2D _BumpMap;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                // Transform Normal and Tangent to View Space (Camera Space)
                float3 vNormal = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, v.normal));
                float3 vTangent = normalize(mul((float3x3)UNITY_MATRIX_MV, v.tangent.xyz));
                float3 vBinormal = cross(vNormal, vTangent) * v.tangent.w;

                // Create TBN matrix to move from Tangent -> View Space
                o.tbnView = float3x3(vTangent, vBinormal, vNormal);

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Unpack the normal map (Tangent Space)
                float3 tangentNormal = UnpackNormal(tex2D(_BumpMap, i.uv));

                // Transform the normal map vector into View Space
                float3 viewNormal = normalize(mul(tangentNormal, i.tbnView));

                // Remap from [-1, 1] to [0, 1] for PNG output
                // X: Right/Left, Y: Up/Down, Z: Forward/Back
                float3 colorNormal = viewNormal * 0.5 + 0.5;

                return float4(colorNormal, 1.0);
            }
            ENDCG
        }
    }
}