// Cloud deck of the atmosphere scene: a curved layer a few km up whose coverage comes from the Blue Marble cloud map
// at the real location, broken up with procedural noise. A clear zone (_ClearCentre/_ClearRadius) keeps the
// ground observer's view of the sky open.
Shader "MTS/CloudDeck"
{
    Properties
    {
        _CloudTex ("Clouds", 2D) = "black" {}
        _Coverage ("Extra coverage", Range(-1, 1)) = 0.05
        _Opacity ("Opacity", Range(0, 1)) = 0.92
        _ClearCentre ("Clear zone centre (xz)", Vector) = (0, 0, 0, 0)
        _ClearRadius ("Clear zone radius (m)", Float) = 25000
        _DetailScale ("Detail scale (m)", Float) = 6000
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-20" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            #include "MTSLocalFrame.cginc"

            sampler2D _CloudTex;
            float _Coverage, _Opacity, _ClearRadius, _DetailScale;
            float4 _ClearCentre;

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
                float2 uvc = MTS_InertialDirToUV(inertial, _MTS_EarthRotation + _MTS_CloudRotation);
                float big = MTS_SampleEquirect(_CloudTex, uvc).r;

                float camDist = distance(wp, _WorldSpaceCameraPos);
                float2 q = wp.xz / _DetailScale;
                float n1 = MTS_Fbm(float3(q, _Time.x * 0.2), 5);
                float n2 = MTS_Fbm(float3(q * 4.0, 1.7 + _Time.x * 0.4), 3);
                float detailW = saturate(1.0 - camDist / 300000.0);
                float density = big * 1.25 + _Coverage + ((n1 - 0.5) * 0.7 + (n2 - 0.5) * 0.3 * detailW) * (0.25 + big);
                float alpha = smoothstep(0.4, 0.8, density) * _Opacity;

                float clearD = distance(wp.xz, _ClearCentre.xz);
                alpha *= smoothstep(_ClearRadius * 0.6, _ClearRadius * 1.6, clearD);
                if (alpha < 0.003) discard;

                float3 up = MTS_LocalUp(wp);
                float3 sun = normalize(_MTS_SunDir.xyz);
                float ndl = dot(up, sun);
                float lit = saturate(ndl * 1.5 + 0.15) * _MTS_SunColor.a;
                bool below = _WorldSpaceCameraPos.y < wp.y;
                float thickness = smoothstep(0.4, 1.0, density);
                float3 col = float3(1.0, 0.98, 0.96) * (0.25 + lit * (below ? lerp(0.8, 0.4, thickness) : lerp(0.95, 1.2, n2)));
                // warm low sun
                col *= lerp(float3(1, 1, 1), float3(1.0, 0.75, 0.55), saturate(1.0 - ndl * 3.0));
                col += MTS_FlashLight(wp) * 0.6;

                float4 ap = MTS_AerialPerspective(wp, MTS_LocalAltitude(wp));
                col = lerp(col, ap.rgb, ap.a * 0.85);
                return float4(col, alpha);
            }
            ENDCG
        }
    }
}
