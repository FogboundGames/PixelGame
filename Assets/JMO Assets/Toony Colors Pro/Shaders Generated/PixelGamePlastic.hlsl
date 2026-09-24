// Toony Colors Pro 2 - Custom PixelGame Plastic Shading Library
// (c) 2026 PixelGame Project
// Isolates custom plastic bevel, top light boost, and procedural edge bevel math
// WARNING: Do NOT delete this file. It is included by PixelGameCartoon.shader.

#ifndef PIXELGAME_PLASTIC_INCLUDED
#define PIXELGAME_PLASTIC_INCLUDED

// Helper function to smooth normals towards a spherical normal for pillow effect
inline float3 ApplyPlasticPillowNormal(float3 objectPos, float3 objectNormal, float pillowRoundness)
{
    if (pillowRoundness > 0.001)
    {
        float3 sphereNorm = normalize(objectPos);
        return normalize(lerp(objectNormal, sphereNorm, pillowRoundness));
    }
    return objectNormal;
}

// Procedural Bevel Normal Smoothing for 90-degree cube faces
// Uses UV proximity to face edges to smoothly bend normal vectors outward, creating glossy plastic Lego edge highlights
// uvMargin: the mesh's OWN flat-face UV island rarely spans the full [0,1] range - its own rounded bevel
// geometry usually reserves an outer margin of UV space (e.g. RoundedCube.obj's flat front face only
// spans UV [0.13, 0.87]). Without remapping, bevelWidth is compared against raw UV distance and can
// never produce a thin line: any bevelWidth smaller than uvMargin does nothing to the flat face at all,
// while the visible "glow" actually comes from the mesh's own margin geometry (which is always wide).
// Remapping into the flat face's own [0,1] local space makes bevelWidth behave as a true, thin edge highlight.
inline float3 CalculateProceduralBevelNormal(float2 uv, float3 normalWS, float bevelWidth, float bevelIntensity, float uvMargin)
{
    if (bevelIntensity <= 0.001 || bevelWidth <= 0.001)
        return normalWS;

    float2 faceUV = saturate((uv - uvMargin) / max(0.0001, 1.0 - 2.0 * uvMargin));

    // Calculate distance from the flat face's own UV boundaries [0, 1]
    float2 edgeDist = min(faceUV, 1.0 - faceUV);
    float minEdge = min(edgeDist.x, edgeDist.y);

    if (minEdge < bevelWidth)
    {
        // Smooth step factor along edge boundary
        float factor = smoothstep(bevelWidth, 0.0, minEdge) * bevelIntensity;
        
        // Determine edge normal perturbation in UV space
        float2 edgeDir = float2(
            (uv.x < 0.5) ? -1.0 : 1.0,
            (uv.y < 0.5) ? -1.0 : 1.0
        );
        
        // Weight by closest component
        float2 mask = (edgeDist.x < edgeDist.y) ? float2(1.0, 0.0) : float2(0.0, 1.0);
        float2 deltaUV = edgeDir * mask * factor;

        // Apply outward perturbation to normal
        float3 perturbedNormal = normalize(normalWS + float3(deltaUV.x, deltaUV.y, 0.0));
        return perturbedNormal;
    }

    return normalWS;
}

// Gets modified specular light and view vectors for stylized fixed plastic highlights
inline void GetPlasticSpecularDirections(
    float3 defaultLightDir,
    float3 defaultViewDir,
    float plasticOn,
    float plasticAngleX,
    out float3 specLightDir,
    out float3 specViewDir)
{
    if (plasticOn > 0.5)
    {
        specViewDir = float3(0.0, 0.0, -1.0);
        specLightDir = normalize(float3(plasticAngleX, 0.68, -0.58));
    }
    else
    {
        specViewDir = defaultViewDir;
        specLightDir = defaultLightDir;
    }
}

// Applies plastic top light boost and bevel edge darkening (AO)
inline float3 ApplyPlasticSurfaceLighting(
    float3 color,
    float3 albedo,
    float3 normalWS,
    float plasticOn,
    float plasticTopLight,
    float plasticBevelAO)
{
    if (plasticOn > 0.5)
    {
        float3 nWS = normalize(normalWS);
        // 1. Top light boost
        float topBoost = smoothstep(0.4, 0.9, nWS.y) * plasticTopLight;
        color += albedo * topBoost;

        // 2. Bevel edge darkening (AO)
        float bottomDark = saturate(-nWS.y * 1.5) * 0.45;
        color *= saturate(1.0 - bottomDark * plasticBevelAO);
    }
    return color;
}

#endif // PIXELGAME_PLASTIC_INCLUDED
