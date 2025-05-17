Shader "Custom/FluffyCloud2D"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {} // The main cloud texture (e.g., a soft gradient or noise)
        _NoiseTex ("Noise Texture", 2D) = "white" {} // A noise texture for distortion/detail
        _NoiseScale ("Noise Scale", Float) = 1.0 // Controls how large the noise features are
        _NoiseStrength ("Noise Strength", Float) = 0.1 // Controls how much the noise distorts
        _CloudColor ("Cloud Color", Color) = (1,1,1,1) // The main color of the cloud
        _Alpha ("Alpha", Range(0,1)) = 1.0 // Overall transparency
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha // Standard alpha blending

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
            sampler2D _NoiseTex;
            float4 _NoiseTex_ST;
            float _NoiseScale;
            float _NoiseStrength;
            float4 _CloudColor;
            float _Alpha;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Sample the noise texture
                float2 noiseUV = i.uv * _NoiseScale;
                float noise = tex2D(_NoiseTex, noiseUV).r; // Using the red channel of the noise texture

                // Apply noise to distort the main texture UVs
                float2 distortedUV = i.uv + (noise - 0.5) * _NoiseStrength; // (noise - 0.5) centers the distortion around the original UV

                // Sample the main cloud texture with distorted UVs
                fixed4 col = tex2D(_MainTex, distortedUV);

                // Apply the cloud color and overall alpha
                col.rgb *= _CloudColor.rgb;
                col.a *= _CloudColor.a * _Alpha;

                return col;
            }
            ENDCG
        }
    }
}
