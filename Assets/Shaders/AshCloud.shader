// Spreading dust and ash pall after a large impact (unit-disc UV on a curved disc a few km up): billowy, sunlit from
// above, its edge pushed out to _Radius (0..1 of the mesh radius) with a ragged, lobed front.
Shader "MTS/AshCloud"
{
    Properties
    {
        _Color ("Ash colour", Color) = (0.36, 0.31, 0.27, 1)
        _Radius ("Front radius (0..1)", Range(0, 1)) = 0.5
        _Opacity ("Opacity", Range(0, 1)) = 0.85
        _Scale ("Billow scale", Float) = 9
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+1" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
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

            float4 _Color;
            float _Radius, _Opacity, _Scale;

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
                float t = _Time.y * 0.05;
                float n = MTS_Fbm(float3(i.p * _Scale, t), 5);
                float2 dir = i.p / max(r, 1e-5);
                float lobes = (MTS_Fbm(float3(dir * 0.6, 5.0), 3) - 0.5) * 0.35 * _Radius;
                float front = _Radius + lobes + (n - 0.5) * 0.08;
                float inside = 1.0 - smoothstep(front - 0.06, front, r);
                float a = inside * saturate(n * 1.8 - 0.25) * _Opacity;
                float lit = 0.3 + 0.7 * saturate(_MTS_SunDir.y * 2.0 + 0.2) * _MTS_SunColor.a;
                float3 col = _Color.rgb * lit * (0.65 + n * 0.7);
                return float4(col, a);
            }
            ENDCG
        }
    }
}
