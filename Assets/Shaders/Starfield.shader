// Procedural star background (skybox). Three layers of hashed stars with a realistic brightness distribution, colour
// temperatures from blue-white to orange, and a faint Milky Way band. No texture, no licence concerns.
Shader "MTS/Starfield"
{
    Properties
    {
        _Density ("Density", Float) = 1
        _Brightness ("Brightness", Float) = 0.9
        _MilkyWay ("Milky Way", Float) = 0.12
        _MilkyWayNormal ("Milky Way plane normal", Vector) = (0.32, 0.87, 0.37, 0)
        _Fade ("Fade (0 = invisible)", Range(0, 1)) = 1
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
            #include "MTSCommon.cginc"

            float _Density, _Brightness, _MilkyWay, _Fade;
            float4 _MilkyWayNormal;

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

            float3 StarLayer(float3 dir, float scale, float pixel, float threshold)
            {
                float3 p = dir * scale;
                float3 cell = floor(p);
                float3 h = MTS_Hash33(cell);
                float present = step(threshold, h.x);
                float3 starPos = cell + 0.5 + (h - 0.5) * 0.6;
                float3 starDir = normalize(starPos);
                float ang = length(cross(dir, starDir)) * scale; // angle in cell units
                float size = max(pixel * scale * 0.9, 0.02);
                float mag = pow(frac(h.y * 13.7 + h.z * 7.1), 6.0);     // many faint, few bright
                float core = exp(-ang * ang / (size * size));
                float temp = h.z;
                float3 tint = temp < 0.25 ? float3(0.7, 0.8, 1.0) : (temp > 0.85 ? float3(1.0, 0.78, 0.55) : float3(1.0, 0.96, 0.9));
                return tint * core * (0.15 + mag * 3.0) * present;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float pixel = length(fwidth(dir));
                float3 col = 0;
                col += StarLayer(dir, 90.0, pixel, 0.92);
                col += StarLayer(dir, 220.0, pixel, 0.955) * 0.6;
                col += StarLayer(dir, 520.0, pixel, 0.985) * 0.4 * _Density;

                // Milky Way: a noisy band around a great circle
                float band = dot(dir, normalize(_MilkyWayNormal.xyz));
                float glow = exp(-band * band * 18.0);
                float clouds = MTS_Fbm(dir * 6.0, 5);
                float dust = smoothstep(0.35, 0.75, MTS_Fbm(dir * 11.0 + 3.0, 4));
                float3 mw = float3(0.55, 0.6, 0.75) * glow * (0.4 + clouds) * (1.0 - dust * 0.6 * glow);
                col += mw * _MilkyWay;
                col += StarLayer(dir, 900.0, pixel, 0.97) * glow * 0.35; // dense faint stars in the band

                return float4(col * _Brightness * _Fade, 1);
            }
            ENDCG
        }
    }
}
