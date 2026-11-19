// Curved ground of the atmosphere scene. Colours come from the Blue Marble day map (and Black Marble lights at
// night) looked up by latitude/longitude of each pixel, so the ground under the meteor is the real place it falls
// on. Procedural detail hides the map's ~10 km texels up close; aerial perspective hazes distant ground.
Shader "MTS/Ground"
{
    Properties
    {
        _DayTex ("Day (Blue Marble)", 2D) = "white" {}
        _NightTex ("Night lights", 2D) = "black" {}
        _OceanMask ("Ocean mask", 2D) = "black" {}
        _CloudTex ("Clouds (for shadows)", 2D) = "black" {}
        _DetailStrength ("Detail strength", Float) = 0.12
        _Ambient ("Ambient", Float) = 0.12
        _DayBoost ("Day brightness", Float) = 1.25
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            #include "MTSLocalFrame.cginc"

            sampler2D _DayTex, _NightTex, _OceanMask, _CloudTex;
            float _DetailStrength, _Ambient, _DayBoost;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 wp = i.worldPos;
                float3 inertial = MTS_LocalToInertialDir(wp);
                float2 uv = MTS_InertialDirToUV(inertial, _MTS_EarthRotation);
                float2 uvc = MTS_InertialDirToUV(inertial, _MTS_EarthRotation + _MTS_CloudRotation);
                float3 up = MTS_LocalUp(wp);
                float3 sun = normalize(_MTS_SunDir.xyz);

                float3 day = MTS_SampleEquirect(_DayTex, uv).rgb * _DayBoost;
                float ocean = MTS_SampleEquirect(_OceanMask, uv).r;
                float3 lights = MTS_SampleEquirect(_NightTex, uv).rgb;
                float cloudShadow = MTS_SampleEquirect(_CloudTex, uvc).r;

                float camDist = distance(wp, _WorldSpaceCameraPos);
                // procedural detail: fields, ridges, forests; fades out with distance (map takes over)
                float2 q = wp.xz;
                float d1 = MTS_Fbm(float3(q / 2500.0, 0.0), 5);
                float d2 = MTS_Fbm(float3(q / 240.0, 3.0), 4);
                float d3 = MTS_Noise3(float3(q / 30.0, 7.0));
                float nearW = saturate(1.0 - camDist / 60000.0);
                float midW = saturate(1.0 - camDist / 400000.0);
                float detail = (d1 - 0.5) * midW + (d2 - 0.5) * nearW * 0.8 + (d3 - 0.5) * nearW * nearW * 0.4;
                float3 land = day * (1.0 + detail * _DetailStrength * 2.0);
                land = lerp(land, land * float3(0.85, 1.05, 0.8), saturate(d2 - 0.45) * nearW * 0.6 * (1.0 - ocean));
                float3 water = day * (1.0 + (d1 - 0.5) * 0.15 * midW);
                float3 albedo = lerp(land, water, ocean);

                float ndl = dot(up, sun);
                float lit = saturate(ndl) * _MTS_SunColor.a * (1.0 - cloudShadow * 0.45);
                float sky = saturate(ndl * 3.0 + 0.4);
                float3 col = albedo * (lit + _Ambient * sky);

                // sun glint on water
                float3 v = normalize(_WorldSpaceCameraPos - wp);
                float3 h = normalize(sun + v);
                col += pow(saturate(dot(up, h)), 120.0) * 1.5 * ocean * saturate(ndl * 4.0) * (1.0 - cloudShadow);

                // night lights
                float night = 1.0 - smoothstep(-0.1, 0.12, ndl);
                col += lights * lights * float3(1.0, 0.8, 0.55) * 1.4 * night;

                col += albedo * MTS_FlashLight(wp);

                float4 ap = MTS_AerialPerspective(wp, MTS_LocalAltitude(wp));
                col = lerp(col, ap.rgb, ap.a);
                return float4(col, 1);
            }
            ENDCG
        }
    }
}
