// Trajectory line (LineRenderer): HDR additive colour from vertex colour, optional dashes along the line
// (texture mode Tile so uv.x is in line widths), fading out towards the line's ends.
Shader "MTS/TrajectoryLine"
{
    Properties
    {
        [HDR] _Color ("Colour", Color) = (1, 0.6, 0.2, 1)
        _Dash ("Dash frequency (0 = solid)", Float) = 0
        _DashFill ("Dash fill", Range(0, 1)) = 0.55
        _Scroll ("Dash scroll speed", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+1" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
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
            float _Dash, _DashFill, _Scroll;

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
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float across = 1.0 - abs(i.uv.y * 2.0 - 1.0);
                float soft = smoothstep(0.0, 0.6, across);
                float dash = 1.0;
                if (_Dash > 0)
                {
                    float d = frac(i.uv.x * _Dash - _Time.y * _Scroll);
                    dash = smoothstep(0.0, 0.08, d) * (1.0 - smoothstep(_DashFill - 0.08, _DashFill, d));
                }
                float3 col = _Color.rgb * i.color.rgb * i.color.a * soft * dash;
                return float4(col, 1);
            }
            ENDCG
        }
    }
}
