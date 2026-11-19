// Camera-facing additive glow (fireball halo, flash, marker). The quad is billboarded in the vertex shader; the
// falloff combines a tight core, a soft halo and optional diffraction spikes.
Shader "MTS/Glow"
{
    Properties
    {
        [HDR] _Color ("Colour", Color) = (1, 0.7, 0.4, 1)
        _Intensity ("Intensity", Float) = 1
        _Core ("Core sharpness", Float) = 40
        _Halo ("Halo falloff", Float) = 5
        _Spikes ("Spikes", Float) = 0.0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+20" "RenderType" = "Transparent" "IgnoreProjector" = "True" "DisableBatching" = "True" }
        Cull Off
        ZWrite Off
        Blend One One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Color;
            float _Intensity, _Core, _Halo, _Spikes;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                float3 centre = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                float scale = length(float3(unity_ObjectToWorld[0].x, unity_ObjectToWorld[1].x, unity_ObjectToWorld[2].x));
                float3 viewCentre = mul(UNITY_MATRIX_V, float4(centre, 1)).xyz;
                float3 viewPos = viewCentre + float3(v.vertex.xy * scale, 0);
                o.pos = mul(UNITY_MATRIX_P, float4(viewPos, 1));
                o.uv = v.vertex.xy * 2.0; // quad spans -0.5..0.5
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float r2 = dot(i.uv, i.uv);
                if (r2 > 1.0) discard;
                float edge = 1.0 - r2;
                float core = exp(-r2 * _Core);
                float halo = exp(-sqrt(r2) * _Halo) * edge;
                float spikes = (exp(-abs(i.uv.x) * 90.0) + exp(-abs(i.uv.y) * 90.0)) * edge * edge * _Spikes;
                float3 col = _Color.rgb * (core * 2.0 + halo + spikes) * _Intensity;
                return float4(col, 1);
            }
            ENDCG
        }
    }
}
