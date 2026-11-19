// Earth seen from space: Blue Marble day side, Black Marble city lights on the night side, a drifting cloud layer,
// ocean sun glint and a twilight band. The globe is a unit sphere scaled to the Earth's radius; texture coordinates
// come from the per-pixel direction so the rotation (_MTS_EarthRotation) matches MTS.Core.Geo exactly.
Shader "MTS/Earth"
{
    Properties
    {
        _DayTex ("Day (Blue Marble)", 2D) = "white" {}
        _NightTex ("Night lights (Black Marble)", 2D) = "black" {}
        _CloudTex ("Clouds", 2D) = "black" {}
        _OceanMask ("Ocean mask", 2D) = "black" {}
        _NightIntensity ("Night lights intensity", Float) = 1.6
        _CloudOpacity ("Cloud opacity", Range(0, 1)) = 0.9
        _SpecPower ("Ocean glint power", Float) = 60
        _SpecIntensity ("Ocean glint intensity", Float) = 1.2
        _Ambient ("Ambient", Float) = 0.02
        _RimColor ("Rim colour", Color) = (0.35, 0.6, 1.0, 1)
        _DayBoost ("Day brightness", Float) = 1.15
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
            #include "MTSCommon.cginc"

            sampler2D _DayTex, _NightTex, _CloudTex, _OceanMask;
            float _NightIntensity, _CloudOpacity, _SpecPower, _SpecIntensity, _Ambient, _DayBoost;
            float4 _RimColor;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 objDir : TEXCOORD1;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.objDir = v.vertex.xyz;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.objDir);               // display axes (object is not rotated)
                float3 inertial = float3(n.x, n.y, -n.z);    // display -> simulation frame
                float2 uv = MTS_InertialDirToUV(inertial, _MTS_EarthRotation);
                float2 uvc = MTS_InertialDirToUV(inertial, _MTS_EarthRotation + _MTS_CloudRotation);

                float3 sun = normalize(_MTS_SunDir.xyz);
                float3 v = normalize(_WorldSpaceCameraPos - i.worldPos);
                float ndl = dot(n, sun);
                float ndv = saturate(dot(n, v));

                float3 day = MTS_SampleEquirect(_DayTex, uv).rgb * _DayBoost;
                float3 lights = MTS_SampleEquirect(_NightTex, uv).rgb;
                float ocean = MTS_SampleEquirect(_OceanMask, uv).r;
                float cloud = MTS_SampleEquirect(_CloudTex, uvc).r;
                cloud = saturate((cloud - 0.08) * 1.25) * _CloudOpacity;

                float lit = saturate(ndl);
                float dayMask = smoothstep(-0.12, 0.18, ndl);

                // soft cloud shadows shifted towards the sun
                float3 surface = day * (lit * _MTS_SunColor.a + _Ambient);
                surface *= 1.0 - cloud * 0.35;

                // twilight: warm the terminator
                float twilight = saturate(1.0 - abs(ndl) * 5.0);
                float3 twilightTint = lerp(float3(1, 1, 1), float3(1.0, 0.62, 0.38), twilight * 0.8);

                // ocean glint (Blinn-Phong) hidden under clouds
                float3 h = normalize(sun + v);
                float spec = pow(saturate(dot(n, h)), _SpecPower) * _SpecIntensity * ocean * (1.0 - cloud) * lit;
                float3 glint = spec * float3(1.0, 0.92, 0.8);

                float3 cloudCol = (lit * _MTS_SunColor.a * 1.05 + _Ambient) * twilightTint;
                float3 col = lerp(surface * twilightTint, cloudCol, cloud) + glint;

                // city lights on the night side, dimmed by clouds
                float night = 1.0 - dayMask;
                float3 cityLights = lights * lights * float3(1.0, 0.8, 0.55) * _NightIntensity * night * (1.0 - cloud * 0.75);
                col += cityLights;

                // limb brightening by the atmosphere (lit side only)
                float rim = pow(1.0 - ndv, 3.0);
                col = lerp(col, _RimColor.rgb * (0.25 + lit), rim * saturate(ndl * 2.0 + 0.35) * 0.85);

                return float4(col, 1);
            }
            ENDCG
        }
    }
}
