// Expanding blast ring on the ground (unit disc mesh scaled to the ring's final radius). _Radius is the current
// front radius as a fraction of the mesh radius; a bright front with a dusty, fading wake inside it.
Shader "MTS/Shockwave"
{
    Properties
    {
        [HDR] _Color ("Front colour", Color) = (1, 0.8, 0.6, 1)
        _DustColor ("Dust colour", Color) = (0.55, 0.45, 0.38, 1)
        _Radius ("Front radius (0..1)", Range(0, 1)) = 0.5
        _Width ("Front width (0..1)", Float) = 0.03
        _Intensity ("Intensity", Float) = 1
        _Dust ("Dust opacity", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+2" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "MTSCommon.cginc"

            float4 _Color, _DustColor;
            float _Radius, _Width, _Intensity, _Dust;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 p : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.p = v.texcoord.xy; // unit-disc coordinate (flat or curved disc mesh)
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float r = length(i.p);
                if (r > 1.0) discard;
                float ang = atan2(i.p.y, i.p.x);
                float wobble = (MTS_Noise3(float3(ang * 6.0, r * 8.0, _Time.y)) - 0.5) * _Width * 1.5;
                float x = (r - _Radius + wobble) / max(_Width, 1e-4);
                float front = exp(-x * x);
                float inside = saturate(1.0 - r / max(_Radius, 1e-4));
                float wake = r < _Radius ? exp(-(_Radius - r) / max(_Radius * 0.35, 1e-4)) : 0.0;
                float dustN = MTS_Fbm(float3(i.p * 14.0, _Time.y * 0.3), 4);
                float dust = wake * _Dust * (0.5 + dustN);
                float3 col = _Color.rgb * front * _Intensity + _DustColor.rgb * dust * (0.3 + 0.7 * saturate(_MTS_SunColor.a));
                float a = saturate(dust + front * 0.4) * _Intensity;
                return float4(col * a + _Color.rgb * front * _Intensity * 0.6, a);
            }
            ENDCG
        }
    }
}
