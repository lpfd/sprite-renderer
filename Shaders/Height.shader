Shader "LeapForward/SpriteRenderer/Height"
{
    Properties
    {
        // No textures needed for raw depth, but we keep the tag for compatibility
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
                float4 vertex : SV_POSITION;
                float2 depth : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                
                // COMPUTE_EYEDEPTH calculates the Z-coordinate in view space
                UNITY_TRANSFER_DEPTH(o.depth);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Convert eye depth to a 0-1 linear value
                // Linear01Depth uses the camera's near and far clip planes
                float d = i.depth.x / _ProjectionParams.z;
                
                #if defined(UNITY_REVERSED_Z)
                    // Handle modern graphics APIs that reverse Z for precision
                    d = 1.0 - d;
                #endif

                return float4(d, d, d, 1.0);
            }
            ENDCG
        }
    }
}