#ifndef MTS_COMMON_INCLUDED
#define MTS_COMMON_INCLUDED

// Shared helpers for the meteor simulator visuals (Built-in pipeline, gamma colour space).

#define MTS_PI 3.14159265359
#define MTS_TWO_PI 6.28318530718

// Global values set by the scene visuals (see MTS.Visuals.VisualsGlobals).
float4 _MTS_SunDir;        // xyz: direction towards the Sun in world (display) axes
float4 _MTS_SunColor;      // rgb: sunlight colour, a: intensity
float  _MTS_EarthRotation; // radians the Earth has turned since t = 0 (omega * t)
float  _MTS_CloudRotation; // extra cloud drift (radians)
float4 _MTS_Flash;         // rgb: flash colour * intensity, a: unused
float4 _MTS_FlashPos;      // xyz: flash world position, w: radius of influence (m)

float MTS_Hash13(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float3 MTS_Hash33(float3 p)
{
    p = frac(p * float3(0.1031, 0.1030, 0.0973));
    p += dot(p, p.yxz + 33.33);
    return frac((p.xxy + p.yxx) * p.zyx);
}

float MTS_Noise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    float3 u = f * f * (3.0 - 2.0 * f);
    float n000 = MTS_Hash13(i);
    float n100 = MTS_Hash13(i + float3(1, 0, 0));
    float n010 = MTS_Hash13(i + float3(0, 1, 0));
    float n110 = MTS_Hash13(i + float3(1, 1, 0));
    float n001 = MTS_Hash13(i + float3(0, 0, 1));
    float n101 = MTS_Hash13(i + float3(1, 0, 1));
    float n011 = MTS_Hash13(i + float3(0, 1, 1));
    float n111 = MTS_Hash13(i + float3(1, 1, 1));
    return lerp(lerp(lerp(n000, n100, u.x), lerp(n010, n110, u.x), u.y),
                lerp(lerp(n001, n101, u.x), lerp(n011, n111, u.x), u.y), u.z);
}

float MTS_Fbm(float3 p, int octaves)
{
    float sum = 0.0;
    float amp = 0.5;
    for (int i = 0; i < octaves; i++)
    {
        sum += amp * MTS_Noise3(p);
        p = p * 2.03 + 17.1;
        amp *= 0.5;
    }
    return sum;
}

// Equirectangular UV of an inertial-frame direction (simulation axes, +Y = pole), matching MTS.Core.Geo:
// latitude = asin(y), longitude = atan2(-z, x) - rotation.
float2 MTS_InertialDirToUV(float3 d, float rotation)
{
    float lat = asin(clamp(d.y, -1.0, 1.0));
    float lon = atan2(-d.z, d.x) - rotation;
    return float2(frac(lon / MTS_TWO_PI + 0.5), lat / MTS_PI + 0.5);
}

// Samples an equirectangular texture without a visible seam (picks the derivative set that does not jump).
float4 MTS_SampleEquirect(sampler2D tex, float2 uv)
{
    float2 uvAlt = float2(frac(uv.x + 0.5) - 0.5, uv.y);
    float2 dx = ddx(uv), dy = ddy(uv);
    float2 dxAlt = ddx(uvAlt), dyAlt = ddy(uvAlt);
    if (abs(dxAlt.x) < abs(dx.x)) dx.x = dxAlt.x;
    if (abs(dyAlt.x) < abs(dy.x)) dy.x = dyAlt.x;
    return tex2Dgrad(tex, uv, dx, dy);
}

// Analytic ray / sphere intersection. Returns (tNear, tFar); tNear > tFar means a miss.
float2 MTS_RaySphere(float3 ro, float3 rd, float3 centre, float radius)
{
    float3 oc = ro - centre;
    float b = dot(oc, rd);
    float c = dot(oc, oc) - radius * radius;
    float h = b * b - c;
    if (h < 0.0) return float2(1, -1);
    h = sqrt(h);
    return float2(-b - h, -b + h);
}

// Simple single-colour sky model (gamma space, HDR). dir and sun are in the local frame (+Y = up).
// camAlt in metres. Returns the in-scattered sky colour seen along dir.
float3 MTS_SkyColor(float3 dir, float3 sun, float camAlt)
{
    const float R = 6371000.0;
    float thin = exp(-max(camAlt, 0.0) / 9000.0);          // relative air above the camera
    float dip = sqrt(saturate(2.0 * max(camAlt, 0.0) / R)); // horizon dip (sine, small-angle)
    float e = dir.y + dip;                                   // elevation above the visible horizon

    float sunUp = sun.y;
    float day = saturate(sunUp * 3.0 + 0.25);
    float lowSun = saturate(1.0 - abs(sunUp) * 3.5);

    float3 zenith = float3(0.3, 0.5, 0.86);
    float3 horizon = float3(0.62, 0.76, 0.93);
    float h = pow(saturate(1.0 - max(e, 0.0)), 5.0);
    float3 col = lerp(zenith, horizon, h);

    // warm horizon band when the sun is low
    float sunSide = saturate(dot(normalize(float2(dir.x, dir.z) + 1e-5), normalize(float2(sun.x, sun.z) + 1e-5)) * 0.5 + 0.5);
    col = lerp(col, float3(1.0, 0.55, 0.28), h * lowSun * sunSide * 0.75);

    float cosSun = dot(dir, sun);
    float mie = pow(saturate(cosSun), 6.0) * 0.35 + pow(saturate(cosSun), 64.0) * 0.8;
    float3 sky = col * day + mie * _MTS_SunColor.rgb * day;

    // Above the air the sky turns black; keep a thin bright limb along the horizon.
    float limb = exp(-abs(e) * 60.0) * (0.35 + 0.65 * day);
    float3 space = horizon * limb * 0.6 * saturate(1.0 - thin * 5.0);
    float airAmount = saturate(thin * 4.0);
    float3 result = lerp(space, sky, airAmount);

    // below the horizon (looking at the haze over the ground)
    if (e < 0.0)
    {
        result = lerp(result, horizon * day * 0.85, saturate(-e * 8.0) * airAmount);
    }

    return result;
}

// Optical depth (in units of sea-level metres of air) along a straight path of length dist between altitudes a and b.
float MTS_OpticalDepth(float dist, float a, float b)
{
    const float H = 8000.0;
    float dh = b - a;
    float ea = exp(-max(a, 0.0) / H);
    float eb = exp(-max(b, 0.0) / H);
    if (abs(dh) < 1.0) return dist * ea;
    return dist * H * (ea - eb) / dh;
}

#endif
