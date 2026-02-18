Shader "LeapForward/SpriteRenderer/ShadowMap"
{
    Properties
    {
        _ShadowMap ("Shadow Map", 2D) = "white" {}
    }
    SubShader
    {
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
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 shadowCoord : TEXCOORD0;
            };

            sampler2D _ShadowMap;
            float4x4 _WorldToShadowMatrix;
            float _ShadowBias;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                
                // 1. Get World Space Position
                float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
                
                // 2. Transform World Position to Shadow Space
                // This matrix should map world space to [0, 1] UV and Depth
                o.shadowCoord = mul(_WorldToShadowMatrix, worldPos);
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Perspective divide (required if the light was Perspective, 
                // but harmless for Orthographic)
                float3 shadowUV = i.shadowCoord.xyz / i.shadowCoord.w;

                // 3. Sample the Depth from the Shadow Map
                float recordedDepth = tex2D(_ShadowMap, shadowUV.xy).r;

                // 4. Compare current depth with recorded depth
                // We add a small bias to prevent "Shadow Acne"
                float currentDepth = shadowUV.z;
                
                if (currentDepth > recordedDepth + _ShadowBias)
                {
                    return fixed4(0, 0, 0, 1); // In Shadow (Black)
                }
                
                return fixed4(1, 1, 1, 1); // In Light (White)
            }
            ENDCG
        }
    }
}

