// Alpha-blended smoke / dust particle, lit by the Sun with a simple wrap term, faded near the camera so huge puffs
// never fill the screen. Vertex colour alpha drives opacity over the particle's lifetime.
Shader "MTS/ParticleSmoke"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (0.55, 0.52, 0.5, 1)
        _Ambient ("Ambient", Float) = 0.35
        _NearFade ("Near fade distance", Float) = 20
        _Emission ("Emission (glow from within)", Color) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-5" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
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

            sampler2D _MainTex;
            float4 _MainTex_ST, _Color, _Emission;
            float _Ambient, _NearFade;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float fade : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.fade = saturate(distance(wp, _WorldSpaceCameraPos) / _NearFade - 0.2);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 t = tex2D(_MainTex, i.uv);
                float sunUp = saturate(_MTS_SunDir.y * 2.0 + 0.4);
                float light = _Ambient + (1.0 - _Ambient) * sunUp * _MTS_SunColor.a;
                // fake self-shadowing: darker towards the puff's lower-left (texture's green holds a lighting term)
                light *= lerp(0.65, 1.1, t.g);
                float3 col = _Color.rgb * i.color.rgb * light + _Emission.rgb * _Emission.a * i.color.a;
                col += _MTS_Flash.rgb * 0.15;
                return float4(col, t.a * i.color.a * _Color.a * i.fade);
            }
            ENDCG
        }
    }
}
