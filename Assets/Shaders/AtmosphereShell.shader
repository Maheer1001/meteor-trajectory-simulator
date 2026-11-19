// Atmosphere glow around the globe. Drawn on the inside of a slightly larger sphere with no depth test; each pixel
// intersects its view ray with the planet and the shell analytically and adds light in proportion to the air the
// ray passes through, tinted by how much of that air is sunlit. Works with the camera inside or outside the shell.
Shader "MTS/AtmosphereShell"
{
    Properties
    {
        _Color ("Scatter colour", Color) = (0.32, 0.58, 1.0, 1)
        _SunsetColor ("Sunset colour", Color) = (1.0, 0.45, 0.2, 1)
        _Intensity ("Intensity", Float) = 3.5
        _PlanetRadius ("Planet radius (world units)", Float) = 6371
        _AtmosphereRadius ("Atmosphere radius (world units)", Float) = 6531
        _Falloff ("Density falloff", Float) = 6
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Front
        ZWrite Off
        ZTest Always
        Blend One One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            #include "MTSCommon.cginc"

            float4 _Color, _SunsetColor;
            float _Intensity, _PlanetRadius, _AtmosphereRadius, _Falloff;

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
                float3 centre = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                float3 ro = _WorldSpaceCameraPos;
                float3 rd = normalize(i.worldPos - ro);
                float3 sun = normalize(_MTS_SunDir.xyz);

                float2 ta = MTS_RaySphere(ro, rd, centre, _AtmosphereRadius);
                if (ta.x > ta.y) discard;
                float2 tp = MTS_RaySphere(ro, rd, centre, _PlanetRadius);
                float t0 = max(ta.x, 0.0);
                float t1 = ta.y;
                bool hitsPlanet = tp.x <= tp.y && tp.x > 0.0;
                if (hitsPlanet) t1 = min(t1, tp.x);
                if (t1 <= t0) discard;

                // march a few samples through the shell
                const int STEPS = 10;
                float shell = _AtmosphereRadius - _PlanetRadius;
                float dt = (t1 - t0) / STEPS;
                float3 sum = 0;
                float total = 0;
                for (int k = 0; k < STEPS; k++)
                {
                    float3 p = ro + rd * (t0 + dt * (k + 0.5));
                    float3 up = (p - centre);
                    float r = length(up);
                    up /= r;
                    float h = saturate((r - _PlanetRadius) / shell);
                    float density = exp(-h * _Falloff);
                    float mu = dot(up, sun);
                    float sunlit = smoothstep(-0.18, 0.12, mu);
                    float sunset = saturate(1.0 - abs(mu) * 4.0) * sunlit;
                    float3 c = lerp(_Color.rgb, _SunsetColor.rgb, sunset * 0.7);
                    sum += c * density * sunlit * dt;
                    total += density * dt;
                }

                float norm = 1.0 / shell;
                float3 col = sum * norm * _Intensity * 0.35;
                // forward scattering towards the sun (glow around the limb when looking sunward)
                float cosSun = saturate(dot(rd, sun));
                col *= 1.0 + pow(cosSun, 8.0) * 2.0;
                // the shell over the disc is thinner in appearance than at the limb
                col = 1.0 - exp(-col);
                return float4(col * _MTS_SunColor.a, 1);
            }
            ENDCG
        }
    }
}
