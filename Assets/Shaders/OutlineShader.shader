Shader "Custom/ScaledOutline"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (1,1,0,1)
        _OutlineWidth ("Outline Width", Range(0.001, 0.1)) = 0.02
        _Scale ("Scale", Range(1, 2)) = 1.2  // Scaling factor for outline expansion
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        // Pass 1: Render the object normally
        // Pass
        // {
        //     Tags { "LightMode"="ForwardBase" }
        //     Cull Back
        //     ZWrite On
        //     ZTest LEqual

        //     CGPROGRAM
        //     #pragma vertex vert
        //     #pragma fragment frag
        //     #include "UnityCG.cginc"

        //     struct appdata
        //     {
        //         float4 vertex : POSITION;
        //         float2 uv : TEXCOORD0;
        //     };

        //     struct v2f
        //     {
        //         float4 pos : SV_POSITION;
        //         float2 uv : TEXCOORD0;
        //     };

        //     sampler2D _MainTex;

        //     v2f vert (appdata v)
        //     {
        //         v2f o;
        //         o.pos = UnityObjectToClipPos(v.vertex);
        //         o.uv = v.uv;
        //         return o;
        //     }

        //     fixed4 frag (v2f i) : SV_Target
        //     {
        //         return tex2D(_MainTex, i.uv);
        //     }
        //     ENDCG
        // }

        // Pass 2: Render the enlarged outline **in front**
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Cull Front  // Render backfaces for outline
            ZWrite On
            ZTest Always  // Always render on top
            Offset 1, 1  // Push outline slightly forward

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float _OutlineWidth;
            float4 _OutlineColor;
            float _Scale;  // Scaling factor

            v2f vert (appdata v)
            {
                v2f o;

                // Transform normal to world space
                float3 worldNormal = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));

                // Expand vertex along normal
                v.vertex.xyz += worldNormal * _OutlineWidth;

                // Scale the outline using UV-based method
                o.uv = (v.uv - 0.5) * _Scale + 0.5;

                // Convert to clip space
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }
    }
}

