// Assets/Shaders/GridFunctions.hlsl
#ifndef GRID_FUNCTIONS_INCLUDED
#define GRID_FUNCTIONS_INCLUDED

// Main implementation (float precision)
float GetEnemyInfluence_float(
    float2 worldXZ,                // world XZ position to evaluate
    UnityTexture2D EnemyPositionsTex,   // tex containing positions (r = x, g = z)
    UnitySamplerState SS,               // sampler state (not used, but needed for SampleLevel)
    float2 UV,                     // UV coords (not used, but needed for SampleLevel)
    float EnemyCount,              // number of valid entries in texture
    float Radius,                  // influence radius
    float Falloff,                  // falloff exponent (1 = linear, 2 = quadratic)
    out float output
)
{
    if (EnemyCount <= 0.0)
    {
        output = 0.0;
        return 0.0;
    }

    int count = (int)EnemyCount;
    float total = 0.0;
    
    // Get texture width from texel size (assumes 1D texture or width stored in _TexelSize.z)
    // For a texture of width W, _TexelSize.z = W
    float textureWidth = EnemyPositionsTex.texelSize.z;

    // Loop (bounded by count). Unrolling can be added if desired.
    for (int i = 0; i < count; ++i)
    {
        // sample at texel center (normalized uv) - divide by texture width, not count
        float u = (i + 0.5) / textureWidth;
        float2 uv = float2(u, 0.5);
        float4 pos = SAMPLE_TEXTURE2D_LOD(EnemyPositionsTex, EnemyPositionsTex.samplerstate, uv, 0);
        float2 enemyXZ = pos.rg;  // explicitly use .rg to be clear

        float d = distance(worldXZ, enemyXZ);
        float influence = saturate(1.0 - d / Radius);      // linear falloff
        if (Falloff != 1.0)
            influence = pow(influence, Falloff);          // shape the falloff
        total += influence;
    }

    // clamp so value doesn't explode if many enemies overlap
    output = saturate(total);
    return output;
}

#endif // GRID_FUNCTIONS_INCLUDED
