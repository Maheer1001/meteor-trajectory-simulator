// Meteoroid surface, driven by its composition (MTS.Visuals.RockLook): up to four material layers laid out as patches
// and veins in proportion to their mass fractions. Each layer has its own pattern family (chondrules, CAIs, metal
// flakes, pyroxene speckle, olivine crystals, Widmanstaetten banding, frost, boulders) computed from 3D noise in object
// space, with its own albedo, metalness, smoothness and relief (screen-space bump). Physically lit (Standard), plus a
// black-body emissive term: _HeatColor/_HeatIntensity come from the surface temperature; the leading face (towards
// _FlowDir) glows most and molten cracks glow first.
Shader "MTS/MeteorRock"
{
    Properties
    {
        _NoiseScale ("Noise scale", Float) = 2.5
        _BumpStrength ("Bump strength", Float) = 1.0
        [HDR] _HeatColor ("Heat colour", Color) = (1, 0.4, 0.1, 1)
        _HeatIntensity ("Heat intensity", Float) = 0
        _FlowDir ("Flow direction (object space)", Vector) = (0, 0, 1, 0)
        _EnvUp ("Environment up (object space)", Vector) = (0, 1, 0, 0)
        _Char ("Fusion crust darkening", Range(0, 1)) = 0
        _LayerCount ("Layer count", Float) = 1
        _LayerEdges ("Layer edges", Vector) = (1, 1, 1, 0)
        _LayerStyles ("Layer styles", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.5
        #include "MTSCommon.cginc"

        float4 _HeatColor, _FlowDir, _EnvUp, _LayerEdges, _LayerStyles;
        float _NoiseScale, _BumpStrength, _HeatIntensity, _Char, _LayerCount;
        float4 _LBase[4];   // rgb matrix colour
        float4 _LAccent[4]; // rgb inclusion colour
        float4 _LParam[4];  // x metallic, y roughness, z inclusions, w ice

        struct Input
        {
            float3 objPos;
            float3 objNormal;
            float4 objTangent;
            float3 viewObj;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.objPos = v.vertex.xyz;
            o.objNormal = v.normal;
            o.objTangent = v.tangent;
            o.viewObj = ObjSpaceViewDir(v.vertex);
        }

        // Cellular noise: x = distance to nearest feature point, y = to second nearest, z = hash of the nearest cell.
        float3 Voronoi(float3 p)
        {
            float3 i = floor(p);
            float3 f = frac(p);
            float d1 = 8.0, d2 = 8.0, id = 0.0;
            for (int z = -1; z <= 1; z++)
            for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
            {
                float3 g = float3(x, y, z);
                float3 o = MTS_Hash33(i + g);
                float3 r = g + o - f;
                float d = dot(r, r);
                if (d < d1) { d2 = d1; d1 = d; id = MTS_Hash13(i + g + 7.31); }
                else if (d < d2) { d2 = d; }
            }
            return float3(sqrt(d1), sqrt(d2), id);
        }

        struct Layer
        {
            float3 albedo;
            float metal;
            float smooth;
            float height;
            float glint;
        };

        Layer Pattern(int style, float3 p, float3 base, float3 accent, float4 prm, float n)
        {
            Layer o;
            float S = _NoiseScale;
            float incl = prm.z;
            o.albedo = base * (0.78 + 0.45 * n);
            o.metal = prm.x * 0.3;
            o.smooth = (1.0 - prm.y) * 0.6;
            o.height = n;
            o.glint = 0.0;

            if (style == 0) // ordinary chondrite: pale round chondrules, a few metal flecks
            {
                float3 v = Voronoi(p * S * 8.0 + 3.1);
                float keep = step(v.z, 0.25 + incl * 0.6);
                float r = 0.12 + 0.2 * frac(v.z * 13.7);
                float ch = keep * (1.0 - smoothstep(r, r + 0.1, v.x)) * (0.55 + 0.45 * frac(v.z * 3.3));
                float rim = keep * smoothstep(r - 0.06, r, v.x) * (1.0 - smoothstep(r, r + 0.05, v.x));
                o.albedo = lerp(o.albedo, accent * (0.9 + 0.2 * frac(v.z * 7.1)), ch);
                o.albedo *= 1.0 - rim * 0.15;
                float3 m = Voronoi(p * S * 22.0 + 9.0);
                float fleck = step(0.86, m.z) * (1.0 - smoothstep(0.12, 0.2, m.x));
                o.albedo = lerp(o.albedo, float3(0.78, 0.76, 0.72), fleck);
                o.metal = lerp(o.metal, 1.0, fleck);
                o.smooth = lerp(o.smooth, 0.75, fleck);
                o.glint = fleck;
                o.height = n + ch * 0.08;
            }
            else if (style == 1) // carbonaceous: near-black, irregular white CAIs, sparse grey chondrules
            {
                float w = MTS_Fbm(p * S * 4.0 + 21.0, 3);
                float3 v = Voronoi(p * S * 2.8 + w * 1.4);
                float keep = step(v.z, 0.35 + incl * 0.9);
                float cai = keep * (1.0 - smoothstep(0.2, 0.27, v.x + (w - 0.5) * 0.3));
                o.albedo = base * (0.85 + 0.35 * n);
                o.albedo = lerp(o.albedo, accent * (0.85 + 0.25 * w), cai);
                float3 c = Voronoi(p * S * 9.0 + 5.0);
                float chon = step(0.8, c.z) * (1.0 - smoothstep(0.18, 0.26, c.x));
                o.albedo = lerp(o.albedo, base * 3.2 + 0.05, chon * 0.7);
                o.smooth = 0.12 + cai * 0.1;
                o.metal = 0.0;
                o.height = n * 0.8 + cai * 0.12;
            }
            else if (style == 2) // enstatite: grey with many bright metal flakes
            {
                float3 m = Voronoi(p * S * 13.0 + 4.0);
                float flake = step(m.z, 0.2 + incl * 0.5) * (1.0 - smoothstep(0.2, 0.3, m.x));
                o.albedo = lerp(o.albedo, float3(0.82, 0.8, 0.76), flake);
                o.metal = lerp(o.metal, 1.0, flake);
                o.smooth = lerp(o.smooth, 0.8, flake);
                o.glint = flake;
                float3 v = Voronoi(p * S * 5.0 + 1.0);
                float ch = step(v.z, 0.3) * (1.0 - smoothstep(0.2, 0.27, v.x));
                o.albedo = lerp(o.albedo, accent, ch * 0.5);
                o.height = n + ch * 0.1;
            }
            else if (style == 3) // basaltic achondrite: light grey with dark pyroxene speckle and pale feldspar laths
            {
                float3 g = Voronoi(p * S * 24.0 + 2.0 + (MTS_Noise3(p * S * 9.0) - 0.5) * 0.8);
                float px = step(g.z, 0.2 + incl * 0.45) * (1.0 - smoothstep(0.22, 0.4, g.x));
                float lath = smoothstep(0.62, 0.75, MTS_Noise3(p * S * float3(30.0, 6.0, 30.0)));
                o.albedo = base * (0.9 + 0.25 * n);
                o.albedo = lerp(o.albedo, base * 1.18 + 0.04, lath * 0.6);
                o.albedo = lerp(o.albedo, accent, px * 0.9);
                o.height = n + px * 0.05;
            }
            else if (style == 4) // pallasite: Fe-Ni matrix holding large gold-green olivine crystals
            {
                float3 v = Voronoi(p * S * 2.4 + 6.0);
                float edge = v.y - v.x;
                float crystal = smoothstep(0.13, 0.22, edge) * (1.0 - smoothstep(0.62, 0.7, v.x));
                float tint = frac(v.z * 17.3);
                float3 olivine = lerp(accent * float3(0.75, 0.95, 0.9), accent * float3(1.1, 0.7, 0.3), tint * 0.8) * lerp(0.55, 1.0, frac(v.z * 5.1));
                olivine *= 0.75 + 0.5 * MTS_Noise3(p * S * 14.0 + v.z * 40.0);
                float3 metalCol = base * (0.95 + 0.3 * MTS_Noise3(p * S * 25.0));
                o.albedo = lerp(metalCol, olivine, crystal);
                o.metal = lerp(prm.x + 0.25, 0.0, crystal);
                o.smooth = lerp(0.7, 0.85, crystal);
                o.glint = crystal * 0.5;
                o.height = n * 0.4 - crystal * 0.18;
            }
            else if (style == 5) // iron: Widmanstaetten lamellae (three orientations), thumbprint regmaglypts
            {
                float3 q = p * S * 1.7 + (MTS_Fbm(p * S * 1.5, 2) - 0.5) * 0.4;
                float b1 = abs(frac(dot(q, float3(0.577, 0.577, 0.577)) * 2.0) - 0.5);
                float b2 = abs(frac(dot(q, float3(0.577, -0.577, 0.577)) * 2.0 + 0.3) - 0.5);
                float b3 = abs(frac(dot(q, float3(-0.577, 0.577, 0.577)) * 2.0 + 0.6) - 0.5);
                float3 dom = Voronoi(p * S * 1.2);
                float sel = dom.z;
                float band = sel < 0.33 ? b1 : (sel < 0.66 ? b2 : b3);
                float xb = min(min(b1, b2), b3);
                float lam = 1.0 - smoothstep(0.1, 0.17, lerp(band, xb, 0.3));
                float3 thumbs = Voronoi(p * S * 1.9 + 11.0);
                float dimple = smoothstep(0.0, 0.7, thumbs.x);
                o.albedo = lerp(base, accent, lam) * (0.85 + 0.25 * n);
                o.metal = saturate(prm.x + 0.05);
                o.smooth = lerp(0.5, 0.78, lam) * (0.75 + 0.25 * (1.0 - prm.y));
                o.glint = lam * 0.6;
                o.height = dimple * 0.6 + n * 0.25 + lam * 0.05;
            }
            else if (style == 6) // cometary: dark dust with bright bluish frost, sparkling ice grains
            {
                float frostN = MTS_Fbm(p * S * 1.6 + 13.0, 4);
                float thr = 0.64 - prm.w * 0.18;
                float frost = smoothstep(thr, thr + 0.05, frostN + (MTS_Noise3(p * S * 12.0) - 0.5) * 0.12);
                float3 grains = Voronoi(p * S * 18.0);
                float sparkle = step(0.9, grains.z) * (1.0 - smoothstep(0.1, 0.18, grains.x));
                o.albedo = base * (0.7 + 0.6 * n);
                o.albedo = lerp(o.albedo, accent * (0.9 + 0.2 * frostN), frost);
                o.albedo += sparkle * accent * 0.4 * (1.0 - frost);
                o.smooth = lerp(0.15, 0.7, frost);
                o.metal = 0.0;
                o.glint = sparkle + frost * 0.3;
                o.height = n * 1.2 + frost * 0.1;
            }
            else // rubble pile: boulders of varied tone with dark gaps
            {
                float3 v = Voronoi(p * S * 1.5 + 2.0);
                float gap = 1.0 - smoothstep(0.03, 0.14, v.y - v.x);
                float tone = lerp(0.65, 1.3, frac(v.z * 11.3));
                float3 rockCol = lerp(base, accent, step(0.72, frac(v.z * 5.7)) * 0.8) * tone * (0.8 + 0.4 * n);
                o.albedo = lerp(rockCol, base * 0.18, gap);
                o.smooth = 0.12;
                o.metal = 0.0;
                o.height = saturate(v.y - v.x) * 1.2 + n * 0.4 - gap * 0.4;
            }

            return o;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 p = IN.objPos;
            float3 N = normalize(IN.objNormal);
            float n = MTS_Fbm(p * _NoiseScale + 0.5, 4);

            // which material: an equalised, domain-warped noise field split at the cumulative mass fractions
            float3 warp = float3(MTS_Fbm(p * 1.7 + 3.0, 2), MTS_Fbm(p * 1.7 + 9.0, 2), MTS_Fbm(p * 1.7 + 15.0, 2)) - 0.5;
            float field = MTS_Fbm(p * 2.1 + warp * 1.2 + 3.7, 4);
            float u = 1.0 / (1.0 + exp(-1.702 * (field - 0.47) / 0.105));
            float edges[5] = { -1.0, _LayerEdges.x, _LayerEdges.y, _LayerEdges.z, 2.0 };
            int count = (int)clamp(_LayerCount, 1.0, 4.0);
            edges[count] = 2.0;
            const float soft = 0.025;

            float3 albedo = 0;
            float metal = 0, smooth = 0, height = 0, glint = 0, wsum = 0;
            [loop]
            for (int i = 0; i < 4; i++)
            {
                if (i >= count) break;
                float w = smoothstep(edges[i] - soft, edges[i] + soft, u) * (1.0 - smoothstep(edges[i + 1] - soft, edges[i + 1] + soft, u));
                if (w < 0.002) continue;
                Layer l = Pattern((int)_LayerStyles[i], p, _LBase[i].rgb, _LAccent[i].rgb, _LParam[i], n);
                albedo += l.albedo * w;
                metal += l.metal * w;
                smooth += l.smooth * w;
                height += l.height * w;
                glint += l.glint * w;
                wsum += w;
            }
            wsum = max(wsum, 1e-4);
            albedo /= wsum; metal /= wsum; smooth /= wsum; height /= wsum; glint /= wsum;

            // fusion crust from heating
            albedo *= 1.0 - _Char * 0.7;
            smooth = lerp(smooth, 0.35, _Char * 0.6);
            metal *= 1.0 - _Char * 0.5;

            // screen-space bump from the layer relief (object space), expressed in the mesh's tangent frame
            float3 dpdx = ddx(p), dpdy = ddy(p);
            float dhdx = ddx(height), dhdy = ddy(height);
            float3 r1 = cross(dpdy, N), r2 = cross(N, dpdx);
            float det = dot(dpdx, r1);
            float3 grad = sign(det) * (dhdx * r1 + dhdy * r2);
            float3 Nb = normalize(abs(det) * N - grad * _BumpStrength * 0.08);
            float3 T = normalize(IN.objTangent.xyz - N * dot(IN.objTangent.xyz, N));
            float3 B = cross(N, T) * (IN.objTangent.w < 0 ? -1.0 : 1.0);
            o.Normal = normalize(float3(dot(Nb, T), dot(Nb, B), dot(Nb, N)));

            o.Albedo = albedo;
            o.Metallic = saturate(metal);
            o.Smoothness = saturate(smooth);

            // cheap environment for bare metal and ice: Earth-lit from "below", black space above
            float3 V = normalize(IN.viewObj);
            float3 R = reflect(-V, Nb);
            float envUp = dot(R, normalize(_EnvUp.xyz));
            float3 env = lerp(float3(0.42, 0.5, 0.62), float3(0.05, 0.055, 0.07), saturate(envUp * 0.6 + 0.5));
            float3 sun = normalize(mul((float3x3)unity_WorldToObject, _MTS_SunDir.xyz));
            float sparkle = pow(saturate(dot(R, sun)), 60.0) * glint * 1.5;
            float3 emit = (env * albedo * (metal * 0.9 + glint * 0.3) + sparkle * _MTS_SunColor.rgb) * (1.0 - _Char);

            float lead = saturate(dot(Nb, normalize(_FlowDir.xyz)) * 0.5 + 0.5);
            lead = lead * lead;
            float cracks = 1.0 - smoothstep(0.0, 0.25, abs(MTS_Fbm(p * _NoiseScale * 3.1 + 2.0, 3) - 0.5));
            float heat = _HeatIntensity * (0.25 + 0.75 * lead) * (0.55 + 0.45 * (1.0 - saturate(height)) + cracks * 0.6);
            o.Emission = emit + _HeatColor.rgb * heat;
            o.Albedo *= saturate(1.0 - _HeatIntensity * 0.15);
        }
        ENDCG
    }
    FallBack "Diffuse"
}
