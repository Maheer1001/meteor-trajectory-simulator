// Meteoroid surface: procedural stony/iron albedo and bump from 3D noise in object space, physically lit (Standard),
// plus a black-body emissive term. _HeatColor/_HeatIntensity come from the surface temperature; the leading face
// (towards _FlowDir) glows most and molten cracks glow first.
Shader "MTS/MeteorRock"
{
    Properties
    {
        _Albedo ("Albedo", Color) = (0.46, 0.42, 0.38, 1)
        _Albedo2 ("Albedo (veins)", Color) = (0.24, 0.21, 0.19, 1)
        _Metallic ("Metallic", Range(0, 1)) = 0.15
        _Smoothness ("Smoothness", Range(0, 1)) = 0.25
        _NoiseScale ("Noise scale", Float) = 2.5
        _BumpStrength ("Bump strength", Float) = 0.6
        [HDR] _HeatColor ("Heat colour", Color) = (1, 0.4, 0.1, 1)
        _HeatIntensity ("Heat intensity", Float) = 0
        _FlowDir ("Flow direction (object space)", Vector) = (0, 0, 1, 0)
        _Char ("Fusion crust darkening", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.5
        #include "MTSCommon.cginc"

        float4 _Albedo, _Albedo2, _HeatColor, _FlowDir;
        float _Metallic, _Smoothness, _NoiseScale, _BumpStrength, _HeatIntensity, _Char;

        struct Input
        {
            float3 objPos;
            float3 objNormal;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.objPos = v.vertex.xyz;
            o.objNormal = v.normal;
        }

        float Height(float3 p)
        {
            float n = MTS_Fbm(p * _NoiseScale, 5);
            float pits = smoothstep(0.62, 0.8, MTS_Noise3(p * _NoiseScale * 2.7 + 5.0));
            return n - pits * 0.35;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 p = IN.objPos;
            float h = Height(p);
            float veins = smoothstep(0.45, 0.55, MTS_Fbm(p * _NoiseScale * 1.7 + 11.0, 3));
            float speck = step(0.93, MTS_Hash13(floor(p * _NoiseScale * 40.0)));
            float3 albedo = lerp(_Albedo.rgb, _Albedo2.rgb, veins) * (0.75 + h * 0.5);
            albedo += speck * 0.15 * (1.0 - _Char);
            albedo *= 1.0 - _Char * 0.7;

            // Large-scale relief is real geometry (noise-displaced icosphere); fine detail is in albedo and heat.
            float3 bumped = normalize(IN.objNormal);

            o.Albedo = albedo;
            o.Metallic = _Metallic;
            o.Smoothness = _Smoothness * (0.6 + veins * 0.4);

            float lead = saturate(dot(bumped, normalize(_FlowDir.xyz)) * 0.5 + 0.5);
            lead = lead * lead;
            float cracks = 1.0 - smoothstep(0.0, 0.25, abs(MTS_Fbm(p * _NoiseScale * 3.1 + 2.0, 3) - 0.5));
            float heat = _HeatIntensity * (0.25 + 0.75 * lead) * (0.55 + 0.45 * (1.0 - h) + cracks * 0.6);
            o.Emission = _HeatColor.rgb * heat;
            o.Albedo *= saturate(1.0 - _HeatIntensity * 0.15);
        }
        ENDCG
    }
    FallBack "Diffuse"
}
