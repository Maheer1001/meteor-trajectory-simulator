// Impact / airburst fireball and the mushroom cap it becomes: a turbulent sphere that glows white-hot, cools through
// orange and red and turns into dark, sunlit smoke. Premultiplied alpha so it can both glow and hide what is behind.
// _Heat 1 = white-hot, 0 = cold smoke. _Opacity fades the whole thing.
Shader "MTS/Fireball"
{
    Properties
    {
        _Heat ("Heat", Range(0, 1)) = 1
        _Opacity ("Opacity", Range(0, 1)) = 1
        _Intensity ("Glow intensity", Float) = 3
        _Turbulence ("Turbulence scale", Float) = 3
        _SmokeColor ("Smoke colour", Color) = (0.32, 0.27, 0.23, 1)
        _Seed ("Seed", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+8" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Back
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            #include "MTSLocalFrame.cginc"

            float _Heat, _Opacity, _Intensity, _Turbulence, _Seed;
            float4 _SmokeColor;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 objPos : TEXCOORD2;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                // billowing surface: push vertices out by low-frequency noise
                float t = _Time.y * 0.15;
                float b = MTS_Fbm(v.vertex.xyz * 2.2 + float3(0, -t, _Seed), 3);
                float4 p = float4(v.vertex.xyz * (0.85 + b * 0.35), 1);
                o.pos = UnityObjectToClipPos(p);
                o.worldPos = mul(unity_ObjectToWorld, p).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.objPos = v.vertex.xyz;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                if (MTS_LocalAltitude(i.worldPos) < 0.0) discard; // the lower half is underground
                float3 n = normalize(i.worldNormal);
                float3 v = normalize(_WorldSpaceCameraPos - i.worldPos);
                float ndv = saturate(dot(n, v));
                float t = _Time.y * 0.25;
                float turb = MTS_Fbm(i.objPos * _Turbulence + float3(0, -t, _Seed), 5);
                float cells = MTS_Fbm(i.objPos * _Turbulence * 3.0 + float3(t, 0, _Seed * 2.0), 3);

                // temperature varies over the surface: hot cores between cooler billows
                float heat = saturate(_Heat * (0.75 + (turb - 0.45) * 1.2) + (cells - 0.5) * 0.25 * _Heat);
                float3 hot = lerp(float3(1.0, 0.35, 0.08), float3(1.0, 0.85, 0.55), saturate(heat * 1.6 - 0.3));
                hot = lerp(hot, float3(1.0, 0.98, 0.92), saturate(heat * 2.0 - 1.2));
                float glow = pow(heat, 1.5) * _Intensity * (0.6 + 0.6 * ndv);

                float3 sun = normalize(_MTS_SunDir.xyz);
                float lit = saturate(dot(n, sun) * 0.6 + 0.45) * _MTS_SunColor.a;
                float3 smoke = _SmokeColor.rgb * (0.35 + 0.9 * lit) * (0.7 + turb * 0.6);
                float3 col = lerp(smoke, hot * glow, saturate(_Heat * 1.4));
                col += hot * glow * saturate(_Heat * 3.0 - 0.2) * 0.5;

                float edge = smoothstep(0.0, 0.45, ndv);
                float a = saturate(edge * (0.75 + turb * 0.5)) * _Opacity;
                return float4(col * a, a * saturate(1.0 - _Heat * 0.35));
            }
            ENDCG
        }
    }
}
