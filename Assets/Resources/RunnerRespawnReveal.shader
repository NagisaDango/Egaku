Shader "Egaku/Runner Respawn Reveal"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Material Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _RevealRect ("World Min XY and Size XY", Vector) = (0, 0, 1, 1)
        [HideInInspector] _RevealProgress ("Reveal Progress", Range(0, 1)) = 1
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
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _RevealRect;
            float _RevealProgress;

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
                float2 position = saturate((input.worldPosition - _RevealRect.xy) / _RevealRect.zw);
                // Each row is traced left to right before advancing upward. A shared world
                // rectangle makes all of the Runner's separate sprites reveal together.
                const float rowCount = 5.0;
                float row = min(floor(position.y * rowCount), rowCount - 1.0);
                float drawOrder = (row + position.x) / rowCount;
                clip(_RevealProgress - drawOrder);

                fixed4 sprite = tex2D(_MainTex, input.uv) * input.color * _Color;
                // A narrow dark leading edge suggests fresh ink without changing the
                // player's selected body color after the effect ends.
                float inkEdge = 1.0 - smoothstep(0.0, 0.025, _RevealProgress - drawOrder);
                sprite.rgb = lerp(sprite.rgb, sprite.rgb * 0.35, inkEdge * 0.7);
                return sprite;
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}
