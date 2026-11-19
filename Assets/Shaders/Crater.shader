// Crater decal (unit disc mesh scaled to the final crater radius): dark scorched bowl, raised bright rim, and an
// ejecta blanket with rays fading out to ~2.5 radii. Glows while _Heat is above zero.
Shader "MTS/Crater"
{
    Properties
    {
        _BowlColor ("Bowl", Color) = (0.08, 0.07, 0.06, 1)
        _RimColor ("Rim", Color) = (0.55, 0.48, 0.4, 1)
        _EjectaColor ("Ejecta", Color) = (0.42, 0.37, 0.32, 1)
        [HDR] _HeatColor ("Heat", Color) = (3, 1.0, 0.3, 1)
        _Heat ("Heat", Range(0, 1)) = 0
        _Reveal ("Reveal", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-1" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "MTSCommon.cginc"

            float4 _BowlColor, _RimColor, _EjectaColor, _HeatColor;
            float _Heat, _Reveal;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 p : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.p = v.texcoord.xy * 2.5; // mesh radius = 2.5 crater radii (unit-disc UV)
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float r = length(i.p);
                float ang = atan2(i.p.y, i.p.x);
                float n = MTS_Fbm(float3(i.p * 3.0, 0), 4);
                float rays = pow(saturate(MTS_Noise3(float3(ang * 9.0, 0, 0)) * 1.3 - 0.2), 2.0);
                float rimR = 1.0 + (n - 0.5) * 0.12;
                float bowl = 1.0 - smoothstep(rimR * 0.82, rimR * 0.98, r);
                float rim = exp(-pow((r - rimR) / 0.12, 2.0));
                float ejecta = saturate(1.0 - (r - rimR) / (1.5 * (0.6 + rays))) * step(rimR, r);
                float3 col = _EjectaColor.rgb * (0.7 + n * 0.5);
                col = lerp(col, _RimColor.rgb * (0.85 + n * 0.3), rim);
                col = lerp(col, _BowlColor.rgb * (0.7 + n * 0.6), bowl);
                float lit = 0.35 + 0.65 * saturate(_MTS_SunDir.y * 2.0 + 0.3) * _MTS_SunColor.a;
                col *= lit;
                col += _HeatColor.rgb * _Heat * bowl * (0.6 + n);
                float a = saturate(bowl + rim + ejecta * 0.85 * (0.6 + n * 0.5));
                a *= smoothstep(_Reveal * 2.5 + 0.01, _Reveal * 2.5 - 0.2, r);
                return float4(col, a);
            }
            ENDCG
        }
    }
}
