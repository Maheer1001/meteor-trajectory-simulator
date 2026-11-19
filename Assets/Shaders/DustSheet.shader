// Ejecta curtain of a crater-forming impact (frustum mesh, uv.x around, uv.y up): an inverted cone of rock and dust
// thrown out of the growing crater, streaked into rays, glowing at the base while hot, thinning towards its rim.
Shader "MTS/DustSheet"
{
    Properties
    {
        _Color ("Dust colour", Color) = (0.5, 0.42, 0.34, 1)
        [HDR] _HotColor ("Hot colour", Color) = (2.2, 0.9, 0.3, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.8
        _Heat ("Heat", Range(0, 1)) = 0.5
        _Rays ("Ray count", Float) = 40
        _Seed ("Seed", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+3" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            #include "MTSCommon.cginc"

            float4 _Color, _HotColor;
            float _Opacity, _Heat, _Rays, _Seed;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float u = i.uv.x, h = i.uv.y;
                float ray = MTS_Fbm(float3(u * _Rays, h * 3.0 - _Time.y * 0.2, _Seed), 4);
                float ray2 = MTS_Noise3(float3(u * _Rays * 3.7, h * 8.0, _Seed + 3.0));
                float density = saturate(ray * 1.6 - 0.35 + (ray2 - 0.5) * 0.4);
                float fade = smoothstep(1.0, 0.45, h) * smoothstep(0.0, 0.06, h);
                float a = density * fade * _Opacity;
                float lit = 0.4 + 0.6 * saturate(_MTS_SunDir.y * 2.0 + 0.3) * _MTS_SunColor.a;
                float3 col = _Color.rgb * lit * (0.7 + ray * 0.5);
                col += _HotColor.rgb * _Heat * pow(saturate(1.0 - h * 1.8), 2.0) * density;
                return float4(col * a, a);
            }
            ENDCG
        }
    }
}
