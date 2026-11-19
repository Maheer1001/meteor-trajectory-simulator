#ifndef MTS_LOCAL_FRAME_INCLUDED
#define MTS_LOCAL_FRAME_INCLUDED

#include "MTSCommon.cginc"

// Local East-Up-North frame of the atmosphere scene (set by MTS.Visuals.AtmosphereVisuals).
// World position (metres) = east * x + up * y + north * z relative to the anchor's surface point.
float4 _MTS_FrameAnchor; // inertial position of the anchor surface point (m)
float4 _MTS_FrameEast;
float4 _MTS_FrameUp;
float4 _MTS_FrameNorth;
float  _MTS_CamAlt;      // camera altitude (m)

static const float MTS_EARTH_RADIUS = 6371000.0;

float3 MTS_LocalToInertialDir(float3 worldPos)
{
    float3 p = _MTS_FrameAnchor.xyz + _MTS_FrameEast.xyz * worldPos.x + _MTS_FrameUp.xyz * worldPos.y + _MTS_FrameNorth.xyz * worldPos.z;
    return normalize(p);
}

float3 MTS_LocalUp(float3 worldPos)
{
    return normalize(worldPos - float3(0, -MTS_EARTH_RADIUS, 0));
}

float MTS_LocalAltitude(float3 worldPos)
{
    return length(worldPos - float3(0, -MTS_EARTH_RADIUS, 0)) - MTS_EARTH_RADIUS;
}

// Haze colour and amount between the camera and a point.
float4 MTS_AerialPerspective(float3 worldPos, float pointAlt)
{
    float3 toP = worldPos - _WorldSpaceCameraPos;
    float dist = length(toP);
    float3 rd = toP / max(dist, 1e-3);
    float od = MTS_OpticalDepth(dist, _MTS_CamAlt, pointAlt);
    float haze = 1.0 - exp(-od / 42000.0);
    float3 hazeDir = normalize(float3(rd.x, max(rd.y, -0.02) * 0.3 + 0.01, rd.z));
    float3 sun = normalize(_MTS_SunDir.xyz);
    float3 col = MTS_SkyColor(hazeDir, sun, min(_MTS_CamAlt, 2000.0)) * 0.92;
    return float4(col, haze);
}

float3 MTS_FlashLight(float3 worldPos)
{
    float d = distance(worldPos, _MTS_FlashPos.xyz);
    float f = 1.0 / (1.0 + pow(d / max(_MTS_FlashPos.w, 1.0), 2.0));
    return _MTS_Flash.rgb * f;
}

#endif
