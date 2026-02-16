#ifndef LUMI_SPLINE_MESH_INCLUDED
#define LUMI_SPLINE_MESH_INCLUDED

// SplineMesh.cginc
// Include this in custom shaders that use SplineMeshRenderer.
//
// Data layout in uv0 (TEXCOORD0) as float4:
//   Main body:
//     x = distance along spline (0-1)
//     y = perpendicular distance from center (-1 to 1)
//   Caps:
//     x = tangent offset from endpoint / radius (-1 to 1)
//     y = perpendicular offset from endpoint / radius (-1 to 1)
//   z = region indicator (0 = start cap, 0.5 = main body, 1 = end cap)
//   w = unused

struct SplineData
{
    float distFromSegment; // 0 at center/endpoint, 1 at edge
    float alongSpline;     // 0-1 along filled portion (only valid for main body)
    float perpendicular;   // -1 to 1 across width (only valid for main body)
    bool isCap;
    bool isStartCap;
    bool isEndCap;
};

// Decode the raw spline data packed into uv0 by SplineMeshRenderer.
// Pass v.texcoord.xyz from a float4 TEXCOORD0 input.
SplineData DecodeSplineData(float3 raw)
{
    SplineData s;
    s.isStartCap = raw.z < 0.3;
    s.isEndCap   = raw.z > 0.7;
    s.isCap      = s.isStartCap || s.isEndCap;

    if (s.isCap)
    {
        float dist = length(raw.xy);
        s.distFromSegment = dist;
        s.alongSpline     = s.isEndCap ? 1.0 : 0.0;
        s.perpendicular   = 0.0;
    }
    else
    {
        s.distFromSegment = abs(raw.y);
        s.alongSpline     = raw.x;
        s.perpendicular   = raw.y;
    }

    return s;
}

// Clip pixels that fall outside the round cap radius. Call in fragment shader.
void ClipSplineCaps(SplineData s)
{
    if (s.isCap)
    {
        clip(1.0 - s.distFromSegment);
    }
}

#endif 
