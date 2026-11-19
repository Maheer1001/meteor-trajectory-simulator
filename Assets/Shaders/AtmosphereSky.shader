// Altitude-driven sky for the atmosphere scene (skybox): black with stars at 120 km, deep blue in the stratosphere,
// bright hazy blue near the ground, with a sun disc and a flash term for the end sequence.
Shader "MTS/AtmosphereSky"
{
    Properties
    {
        _SunSize ("Sun size", Float) = 0.9995
        _StarBrightness ("Star brightness", Float) = 0.45
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            #include "MTSLocalFrame.cginc"

            float _SunSize, _StarBrightness;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float Stars(float3 dir, float scale, float pixel)
            {
                float3 p = dir * scale;
                float3 cell = floor(p);
                float3 h = MTS_Hash33(cell);
                if (h.x < 0.975) return 0;
                float3 sd = normalize(cell + 0.5 + (h - 0.5) * 0.6);
                float a = length(cross(dir, sd)) * scale;
                float s = max(pixel * scale, 0.02);
                return exp(-a * a / (s * s)) * (0.2 + pow(h.y, 6.0) * 3.0);
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float3 sun = normalize(_MTS_SunDir.xyz);
                float3 col = MTS_SkyColor(dir, sun, _MTS_CamAlt);

                // stars show through thin air or at night
                float pixel = length(fwidth(dir));
                float dark = 1.0 - saturate(dot(col, float3(0.3, 0.6, 0.1)) * 4.0);
                float stars = Stars(dir, 160.0, pixel) + Stars(dir, 380.0, pixel) * 0.6;
                col += stars * dark * _StarBrightness * float3(0.95, 0.97, 1.0);

                // sun disc
                float cs = dot(dir, sun);
                float disc = smoothstep(_SunSize, _SunSize + 0.0002, cs);
                col += disc * _MTS_SunColor.rgb * 8.0;

                // end-sequence flash lights up the whole sky
                col += _MTS_Flash.rgb * 0.18;
                return float4(col, 1);
            }
            ENDCG
        }
    }
}
