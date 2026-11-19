// Ground damage under the blast (unit-disc UV on a flat or curved disc mesh): scorched, flattened ground revealed by
// the passing shock front. With _Radial = 1 it shows the radial "butterfly" of a forest felled by an airburst
// (Tunguska): trees laid flat pointing away from ground zero, a standing-but-burnt zone right under the burst.
Shader "MTS/GroundScar"
{
    Properties
    {
        _Color ("Scorched colour", Color) = (0.22, 0.18, 0.14, 1)
        _StreakColor ("Fallen-tree colour", Color) = (0.42, 0.34, 0.24, 1)
        _Reveal ("Reveal radius (0..1)", Range(0, 1)) = 1
        _Radial ("Radial streaks", Range(0, 1)) = 1
        _Opacity ("Opacity", Range(0, 1)) = 0.85
        _Glow ("Glow", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-2" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            #include "MTSCommon.cginc"

            float4 _Color, _StreakColor;
            float _Reveal, _Radial, _Opacity, _Glow;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 p : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.p = v.texcoord.xy;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float r = length(i.p);
                if (r > 1.0) discard;
                float ang = atan2(i.p.y, i.p.x);
                float n = MTS_Fbm(float3(i.p * 6.0, 1.0), 4);
                // radial streaks: fine angular noise smeared along the radius
                float2 dir = i.p / max(r, 1e-5); // seam-free angular coordinate
                float streak = MTS_Noise3(float3(dir * 41.0, r * 3.0)) * 0.6 + MTS_Noise3(float3(dir * 14.0, r * 1.5 + 4.0)) * 0.4;
                float3 col = lerp(_Color.rgb, _StreakColor.rgb, saturate(streak * _Radial * 1.2 + n * 0.3));
                // a burnt "standing" zone right under the burst, irregular outer edge (the butterfly shape)
                float lobe = 0.75 + 0.25 * sin(ang * 2.0 + 0.6) + (n - 0.5) * 0.3;
                float extent = 1.0 - smoothstep(lobe * 0.7, lobe, r);
                float centre = 1.0 - smoothstep(0.05, 0.14, r);
                col = lerp(col, _Color.rgb * 0.6, centre * _Radial);
                float lit = 0.35 + 0.65 * saturate(_MTS_SunDir.y * 2.0 + 0.3) * _MTS_SunColor.a;
                col *= lit;
                col += float3(1.0, 0.45, 0.12) * _Glow * (1.0 - r) * (0.5 + n);
                float a = extent * _Opacity * (0.75 + 0.25 * n) * smoothstep(_Reveal + 0.02, _Reveal - 0.05, r);
                return float4(col, a);
            }
            ENDCG
        }
    }
}
