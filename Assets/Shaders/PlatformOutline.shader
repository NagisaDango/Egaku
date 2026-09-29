Shader "Hidden/Egaku/PlatformOutline"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _OriginalMask ("Original Platform Mask", 2D) = "black" {}
        _OccluderMask ("Nearer Platform Mask", 2D) = "black" {}
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth ("Outline Width (Pixels)", Float) = 2
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        // Pass 0 erodes horizontal coverage. The composite pass erodes vertically and
        // subtracts that result from the original group mask to make an inner edge.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment fragHorizontal
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _OutlineWidth;

            fixed4 fragHorizontal(v2f_img i) : SV_Target
            {
                float kept = 1;
                int radius = (int)ceil(_OutlineWidth);
                for (int x = -32; x <= 32; x++)
                {
                    if (abs(x) <= radius)
                        kept = min(kept, step(0.01, tex2D(_MainTex, i.uv + float2(x * _MainTex_TexelSize.x, 0)).a));
                }
                return float4(1, 1, 1, kept);
            }
            ENDCG
        }

        // Pass 1 removes nearer platform groups from a rear group's mask before erosion.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment fragVisibleMask
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _OccluderMask;

            fixed4 fragVisibleMask(v2f_img i) : SV_Target
            {
                float own = step(0.01, tex2D(_MainTex, i.uv).a);
                float covered = step(0.01, tex2D(_OccluderMask, i.uv).a);
                return float4(1, 1, 1, own * (1 - covered));
            }
            ENDCG
        }

        // Pass 2 combines nearer groups for scenes with more than two terrain kinds.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment fragUnion
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _OccluderMask;

            fixed4 fragUnion(v2f_img i) : SV_Target
            {
                float own = step(0.01, tex2D(_MainTex, i.uv).a);
                float covered = step(0.01, tex2D(_OccluderMask, i.uv).a);
                return float4(1, 1, 1, max(own, covered));
            }
            ENDCG
        }

        // Pass 3 composites the inner edge onto the current scene image.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment fragComposite
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _OriginalMask;
            sampler2D _SceneTex;
            float4 _MainTex_TexelSize;
            float4 _OutlineColor;
            float _OutlineWidth;

            fixed4 fragComposite(v2f_img i) : SV_Target
            {
                float kept = 1;
                int radius = (int)ceil(_OutlineWidth);
                for (int y = -32; y <= 32; y++)
                {
                    if (abs(y) <= radius)
                        kept = min(kept, step(0.01, tex2D(_MainTex, i.uv + float2(0, y * _MainTex_TexelSize.y)).a));
                }

                float inside = step(0.01, tex2D(_OriginalMask, i.uv).a);
                float innerEdge = inside * (1 - kept);
                fixed4 scene = tex2D(_SceneTex, i.uv);
                scene.rgb = lerp(scene.rgb, _OutlineColor.rgb, innerEdge * _OutlineColor.a);
                return scene;
            }
            ENDCG
        }
    }
    Fallback Off
}
