Shader "Sixty/VoidCityInstanced"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.05, 0.055, 0.07, 1)
        _TopColor ("Top Color", Color) = (0.1, 0.12, 0.16, 1)
        _GlowColor ("Glow Color", Color) = (0.4, 0.85, 0.95, 1)
        _GlowIntensity ("Glow Intensity", Float) = 4.0
        _AmbientStrength ("Ambient Strength", Float) = 0.35
        _NoiseScale ("Noise Scale", Float) = 0.04
        _NoiseStrength ("Noise Strength", Float) = 0.45
        _CavityWidth ("Cavity Width", Float) = 0.08
        _CavityStrength ("Cavity Strength", Float) = 0.5
        _CavityColor ("Cavity Color", Color) = (0.02, 0.025, 0.035, 1)
        _TriangleScale ("Triangle Scale", Float) = 0.5
        _TriangleDensity ("Triangle Density", Range(0, 1)) = 0.06
        _TriangleBrightness ("Triangle Brightness", Float) = 1.3
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct VoidCubeInstance
            {
                float3 position;
                float3 scale;
                float  emissive;
            };

            StructuredBuffer<VoidCubeInstance> _InstanceBuffer;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _TopColor;
                float4 _GlowColor;
                float  _GlowIntensity;
                float  _AmbientStrength;
                float  _NoiseScale;
                float  _NoiseStrength;
                float  _CavityWidth;
                float  _CavityStrength;
                float4 _CavityColor;
                float  _TriangleScale;
                float  _TriangleDensity;
                float  _TriangleBrightness;
            CBUFFER_END

            float hash2to1(float2 p)
            {
                p = frac(p * float2(443.897, 441.423));
                p += dot(p, p.yx + 19.19);
                return frac(p.x * p.y);
            }

            float hash3to1(float3 p)
            {
                p = frac(p * float3(443.897, 441.423, 437.195));
                p += dot(p, p.yzx + 19.19);
                return frac((p.x + p.y) * p.z);
            }

            // Sparse rotated triangle pattern with per-triangle shade variation
            // Returns: triangle mask (0 or 1), shade output for color variation
            float sparseTriangle(float2 worldUV, float scale, float density, out float shade)
            {
                float2 p = worldUV * scale;
                float2 cellId = floor(p);
                float2 cellUV = frac(p) - 0.5; // -0.5 to 0.5

                float cellHash = hash2to1(cellId);
                float cellHash2 = hash2to1(cellId + float2(7.31, 3.17));
                float cellHash3 = hash2to1(cellId + float2(13.7, 29.3));

                // Only some cells have a visible triangle
                float isVisible = step(1.0 - density, cellHash);

                // Per-triangle shade: varies from darker to lighter
                shade = 0.7 + cellHash3 * 0.6; // range 0.7 to 1.3

                // Random rotation per cell
                float angle = cellHash2 * 6.2831853;
                float cosA = cos(angle);
                float sinA = sin(angle);
                float2 rotUV = float2(
                    cellUV.x * cosA - cellUV.y * sinA,
                    cellUV.x * sinA + cellUV.y * cosA
                );

                // Equilateral triangle shape
                float2 tv = rotUV + float2(0.0, 0.18);
                float triSize = 0.35;
                float s = tv.y / triSize + 0.5;
                float edge = abs(tv.x) / (triSize * 0.866);
                float inTri = step(edge, s) * step(0.0, s) * step(s, 1.0);

                return inTri * isVisible;
            }

            // Large-scale 3D value noise
            float valueNoise3D(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float n000 = hash3to1(i);
                float n100 = hash3to1(i + float3(1, 0, 0));
                float n010 = hash3to1(i + float3(0, 1, 0));
                float n110 = hash3to1(i + float3(1, 1, 0));
                float n001 = hash3to1(i + float3(0, 0, 1));
                float n101 = hash3to1(i + float3(1, 0, 1));
                float n011 = hash3to1(i + float3(0, 1, 1));
                float n111 = hash3to1(i + float3(1, 1, 1));

                float nx00 = lerp(n000, n100, f.x);
                float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x);
                float nx11 = lerp(n011, n111, f.x);

                float nxy0 = lerp(nx00, nx10, f.y);
                float nxy1 = lerp(nx01, nx11, f.y);

                return lerp(nxy0, nxy1, f.z);
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                uint   instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float  fogFactor   : TEXCOORD2;
                float  emissive    : TEXCOORD3;
                float  heightNorm  : TEXCOORD4;
                float3 instancePos : TEXCOORD5;
                float3 instanceScale : TEXCOORD6;
                nointerpolation float3 normalOS : TEXCOORD7;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VoidCubeInstance inst = _InstanceBuffer[input.instanceID];

                float3 worldPos = input.positionOS.xyz * inst.scale + inst.position;
                output.positionWS = worldPos;
                output.positionCS = TransformWorldToHClip(worldPos);

                // Transform normal accounting for non-uniform scale
                float3 invScale = 1.0 / max(inst.scale, 0.001);
                float3 scaledNormal = input.normalOS * invScale;
                output.normalWS = normalize(scaledNormal);

                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.emissive = inst.emissive;
                output.heightNorm = saturate(input.positionOS.y + 0.5);
                output.instancePos = inst.position;
                output.instanceScale = inst.scale;
                output.normalOS = input.normalOS;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);

                // Directional lighting
                Light mainLight = GetMainLight();
                float ndotl = saturate(dot(normal, mainLight.direction));

                // Top face gets lighter color, sides get darker base
                float topFactor = saturate(normal.y);
                float3 surfaceColor = lerp(_BaseColor.rgb, _TopColor.rgb, topFactor * 0.7 + input.heightNorm * 0.3);

                // Per-instance color variation
                float instanceHash = hash3to1(input.instancePos * 0.1);
                surfaceColor *= (0.7 + instanceHash * 0.6);

                // Sparse rotated triangles with per-triangle shade — top faces only
                if (topFactor > 0.5)
                {
                    float triShade;
                    float triMask = sparseTriangle(input.positionWS.xz, _TriangleScale, _TriangleDensity, triShade);
                    // Each triangle gets its own shade (darker or lighter)
                    surfaceColor *= lerp(1.0, triShade * _TriangleBrightness, triMask);
                }

                // Large-scale value noise for broad shade variation
                float noise = valueNoise3D(input.positionWS * _NoiseScale);
                noise = noise * 2.0 - 1.0;
                surfaceColor += surfaceColor * noise * _NoiseStrength;

                // Edge cavity: use face normal to detect edges properly
                // For each face, compute distance to the 4 edges of that face
                // using world position relative to instance center and scale
                float3 localPos = (input.positionWS - input.instancePos) / max(input.instanceScale, 0.001);
                float3 absNorm = abs(input.normalOS);

                // Pick the two axes that form this face (not the face normal axis)
                float2 faceUV;
                float2 faceHalfSize;
                if (absNorm.y > 0.5) // top/bottom face
                {
                    faceUV = localPos.xz;
                    faceHalfSize = float2(0.5, 0.5);
                }
                else if (absNorm.x > 0.5) // left/right face
                {
                    faceUV = localPos.yz;
                    faceHalfSize = float2(0.5, 0.5);
                }
                else // front/back face
                {
                    faceUV = localPos.xy;
                    faceHalfSize = float2(0.5, 0.5);
                }

                float2 edgeDist2D = faceHalfSize - abs(faceUV);
                float minEdgeDist = min(edgeDist2D.x, edgeDist2D.y);
                float cavity = smoothstep(0.0, _CavityWidth, minEdgeDist);
                surfaceColor = lerp(_CavityColor.rgb * _CavityStrength + surfaceColor * (1.0 - _CavityStrength), surfaceColor, cavity);

                // Side faces additional darkening
                float sideDarken = lerp(0.55, 1.0, topFactor);
                surfaceColor *= sideDarken;

                // Lighting: ambient + directional
                float3 lit = surfaceColor * (_AmbientStrength + ndotl * 0.5) * mainLight.color;

                // Subtle rim light
                float3 viewDir = normalize(_WorldSpaceCameraPos - input.positionWS);
                float rim = 1.0 - saturate(dot(viewDir, normal));
                rim = pow(rim, 3.0) * 0.08;
                lit += rim * _TopColor.rgb;

                // Glow cubes — per-instance color/intensity variation
                if (input.emissive > 0.5)
                {
                    float glowHash = hash3to1(input.instancePos * 0.37);
                    float glowHash2 = hash3to1(input.instancePos * 1.13);
                    // Warm white to cool cyan variation
                    float3 warmWhite = float3(1.0, 0.92, 0.82);
                    float3 coolCyan = _GlowColor.rgb;
                    float3 glowTint = lerp(warmWhite, coolCyan, glowHash);
                    // Intensity variation per cube
                    float intensityVar = _GlowIntensity * (0.6 + glowHash2 * 0.8);
                    lit = glowTint * intensityVar;
                }

                lit = MixFog(lit, input.fogFactor);

                return half4(lit, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
