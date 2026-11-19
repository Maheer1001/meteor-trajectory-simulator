// Shock-layer plasma around the meteoroid: a soft additive glow volume, compressed in front and drawn out into a
// tapering wake behind (object +Z = direction of travel). Brightness peaks where the view ray crosses the most
// glowing gas (the centre of the silhouette) and fades to nothing at the edges, so it never shows a hard shell.
Shader "MTS/PlasmaSheath"
{
    Properties
    {
        [HDR] _Color ("Outer colour", Color) = (1, 0.5, 0.2, 1)
        [HDR] _CoreColor ("Core colour", Color) = (1, 0.95, 0.85, 1)
        _Intensity ("Intensity", Float) = 1
        _Stretch ("Wake stretch", Float) = 6
        _Front ("Front compression", Range(0.2, 1)) = 0.8
        _NoiseSpeed ("Noise speed", Float) = 4
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+5" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Back
        ZWrite Off
        Blend One One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            #include "MTSCommon.cginc"

            float4 _Color, _CoreColor;
            float _Intensity, _Stretch, _Front, _NoiseSpeed;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 objPos : TEXCOORD2;
                float wake : TEXCOORD3;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                float3 p = v.vertex.xyz;
                float back = saturate(-p.z);             // 0 at the equator, 1 at the rear pole
                float z = p.z < 0 ? p.z * _Stretch : p.z * _Front;
                float taper = lerp(1.0, 0.18, pow(back, 0.7));
                float3 q = float3(p.xy * taper, z);
                float3 n = normalize(float3(v.normal.xy / max(taper, 0.2), v.normal.z / (p.z < 0 ? _Stretch : _Front)));
                o.pos = UnityObjectToClipPos(float4(q, 1));
                o.worldPos = mul(unity_ObjectToWorld, float4(q, 1)).xyz;
                o.worldNormal = UnityObjectToWorldNormal(n);
                o.objPos = v.vertex.xyz;
                o.wake = back;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.worldNormal);
                float3 v = normalize(_WorldSpaceCameraPos - i.worldPos);
                float ndv = saturate(abs(dot(n, v)));
                float t = _Time.y * _NoiseSpeed;
                float turb = MTS_Fbm(i.objPos * float3(3.0, 3.0, 1.2) + float3(0, 0, t), 4);
                float streak = MTS_Noise3(float3(i.objPos.xy * 9.0, i.objPos.z * 1.2 + t * 2.5));
                float density = pow(ndv, 1.4);                      // soft edges
                float along = pow(1.0 - i.wake, 1.6);               // wake fades towards its tip
                float glow = density * along * (0.55 + turb * 0.9) * (0.75 + streak * 0.5);
                float hot = saturate(density * (1.0 - i.wake * 1.5));
                float3 col = lerp(_Color.rgb, _CoreColor.rgb, hot) * glow * _Intensity;
                return float4(col, 1);
            }
            ENDCG
        }
    }
}
