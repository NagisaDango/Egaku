Shader "Egaku/Water Surface 2D"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Material Tint", Color) = (1, 1, 1, 1)
        _SurfaceTint ("Surface Color Multiplier", Color) = (1, 1, 1, 1)
        _DeepTint ("Deep Color Multiplier", Color) = (0.7, 0.88, 0.95, 1)
        _DepthRange ("Depth Gradient (World Units)", Range(0.1, 12)) = 3
        _WaveAmplitude ("Wave Height (World Units)", Range(0, 0.5)) = 0.1
        _WaveFrequency ("Wave Frequency", Range(0, 6)) = 1.3
        _WaveSpeed ("Wave Speed", Range(-5, 5)) = 1.1
        _SmallWaveAmplitude ("Small Wave Height (World Units)", Range(0, 0.25)) = 0.025
        _SmallWaveFrequency ("Small Wave Frequency", Range(0, 12)) = 3.1
        _SmallWaveSpeed ("Small Wave Speed", Range(-5, 5)) = -0.7
        _FoamColor ("Surface Highlight Color", Color) = (0.9, 1, 1, 1)
        _FoamWidth ("Surface Highlight Width (World Units)", Range(0, 0.5)) = 0.06
        _FoamStrength ("Surface Highlight Strength", Range(0, 1)) = 0.35
        [HideInInspector] _SurfaceY ("Physics Surface Y", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _SurfaceTint;
            fixed4 _DeepTint;
            fixed4 _FoamColor;
            float _DepthRange;
            float _WaveAmplitude;
            float _WaveFrequency;
            float _WaveSpeed;
            float _SmallWaveAmplitude;
            float _SmallWaveFrequency;
            float _SmallWaveSpeed;
            float _FoamWidth;
            float _FoamStrength;
            float _SurfaceY;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 worldPosition : TEXCOORD1;
                fixed4 color : COLOR;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.worldPosition = mul(unity_ObjectToWorld, input.vertex).xy;
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                // World-space waves keep the same width and height when level designers scale Float prefabs.
                float x = input.worldPosition.x;
                float waveY = _SurfaceY
                    + sin(x * _WaveFrequency + _Time.y * _WaveSpeed) * _WaveAmplitude
                    + sin(x * _SmallWaveFrequency + _Time.y * _SmallWaveSpeed) * _SmallWaveAmplitude;
                float depth = waveY - input.worldPosition.y;

                // Fade only the visual edge; the BuoyancyEffector2D surface stays a flat gameplay line.
                float edgeWidth = max(fwidth(depth), 0.001);
                float coverage = saturate(depth / edgeWidth + 0.5);
                fixed4 sprite = tex2D(_MainTex, input.uv);
                fixed4 tint = input.color * _Color;
                fixed3 water = tint.rgb * lerp(_SurfaceTint.rgb, _DeepTint.rgb,
                    saturate(depth / max(_DepthRange, 0.001)));
                float highlight = (1.0 - smoothstep(0.0, max(_FoamWidth, 0.001), depth))
                    * _FoamStrength * coverage;
                water = lerp(water, _FoamColor.rgb, highlight);
                return fixed4(water * sprite.rgb, tint.a * sprite.a * coverage);
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}
