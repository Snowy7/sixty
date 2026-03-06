// VolumetricFogComposite.shader - COMPLETE VERSION WITH DISTANCE FADE
Shader "Hidden/VolumetricFog/Composite"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        
        Pass
        {
            Name "Fog Composite"
            ZWrite Off ZTest Always Cull Off
            
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            
            TEXTURE2D(_SourceTexture);
            TEXTURE2D(_FogTexture);
            TEXTURE2D(_FullResDepth);
            TEXTURE2D(_HalfResDepth);
            
            float _IsHalfRes;
            float4 _Resolution;      // Full res
            float4 _HalfResolution;  // Half res
            
            float _DistanceFadeEnabled;
            float _DistanceFadeStart;
            float _DistanceFadeEnd;
            float _DistanceFadeExponent;
            float _DistanceFadeStrength;
            float4 _DistanceFadeColor;
            float _DistanceFadeAffectsSkybox;
            
            // ===================== DEPTH HELPERS =====================
            
            float GetLinearDepth(float rawDepth)
            {
                #if UNITY_REVERSED_Z
                return LinearEyeDepth(rawDepth, _ZBufferParams);
                #else
                return LinearEyeDepth(rawDepth, _ZBufferParams);
                #endif
            }
            
            // Convert linear depth (eye space) to world distance
            float LinearDepthToWorldDistance(float linearDepth)
            {
                // Linear depth is already distance from camera in world space
                return linearDepth;
            }
            
            // ===================== EDGE-AWARE WEIGHTS =====================
            
            float DepthWeight(float sampleDepth, float centerDepth)
            {
                // Aggressive depth weighting to preserve edges
                float depthDiff = abs(sampleDepth - centerDepth);
                float threshold = centerDepth * 0.02; // 2% of depth as threshold
                return exp(-depthDiff * depthDiff / (threshold * threshold + 0.001));
            }
            
            float SpatialWeight(float2 offset)
            {
                // Gaussian-ish spatial weight
                return exp(-dot(offset, offset) * 2.0);
            }
            
            // ===================== UPSAMPLING METHODS =====================
            
            float4 BilateralUpsample(float2 uv, float centerDepth)
            {
                float centerLinearDepth = GetLinearDepth(centerDepth);
                
                // Sample pattern for bilateral upsampling
                float2 halfResUV = uv;
                float2 halfResTexelSize = _HalfResolution.zw;
                
                float4 result = 0;
                float totalWeight = 0;
                
                // 3x3 bilateral filter
                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 offset = float2(x, y);
                        float2 sampleUV = halfResUV + offset * halfResTexelSize;
                        
                        // Clamp to valid range
                        sampleUV = saturate(sampleUV);
                        
                        // Sample half-res depth at this location
                        float sampleHalfResDepth = SAMPLE_TEXTURE2D_LOD(_HalfResDepth, sampler_PointClamp, sampleUV, 0).r;
                        float sampleLinearDepth = GetLinearDepth(sampleHalfResDepth);
                        
                        // Calculate bilateral weight
                        float dWeight = DepthWeight(sampleLinearDepth, centerLinearDepth);
                        float sWeight = SpatialWeight(offset);
                        float weight = dWeight * sWeight;
                        
                        // Sample fog
                        float4 fogSample = SAMPLE_TEXTURE2D_LOD(_FogTexture, sampler_LinearClamp, sampleUV, 0);
                        
                        result += fogSample * weight;
                        totalWeight += weight;
                    }
                }
                
                // Normalize
                result /= max(totalWeight, 0.0001);
                
                return result;
            }
            
            float4 EdgeAwareUpsample(float2 uv, float centerDepth)
            {
                float centerLinearDepth = GetLinearDepth(centerDepth);
                float2 fullResTexelSize = _Resolution.zw;
                
                // Sample neighboring full-res depths to detect edges
                float depthLeft = GetLinearDepth(SampleSceneDepth(uv + float2(-1, 0) * fullResTexelSize));
                float depthRight = GetLinearDepth(SampleSceneDepth(uv + float2(1, 0) * fullResTexelSize));
                float depthUp = GetLinearDepth(SampleSceneDepth(uv + float2(0, -1) * fullResTexelSize));
                float depthDown = GetLinearDepth(SampleSceneDepth(uv + float2(0, 1) * fullResTexelSize));
                
                // Sobel-like edge detection
                float edgeH = abs(depthLeft - depthRight);
                float edgeV = abs(depthUp - depthDown);
                float edgeMagnitude = sqrt(edgeH * edgeH + edgeV * edgeV);
                
                // Threshold for edge detection (relative to depth)
                float edgeThreshold = centerLinearDepth * 0.05;
                float isEdge = saturate(edgeMagnitude / edgeThreshold);
                
                // At edges: use bilateral upsampling with strong depth weight
                // Away from edges: use simple bilinear sampling
                
                if (isEdge > 0.5)
                {
                    // Edge pixel: use bilateral upsampling
                    return BilateralUpsample(uv, centerDepth);
                }
                else
                {
                    // Non-edge pixel: simple bilinear is fine
                    return SAMPLE_TEXTURE2D_LOD(_FogTexture, sampler_LinearClamp, uv, 0);
                }
            }
            
            // ===================== MAIN FRAGMENT SHADER =====================
            
            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float4 sceneColor = SAMPLE_TEXTURE2D(_SourceTexture, sampler_LinearClamp, uv);

                float fullResDepth = SampleSceneDepth(uv);

                // Sample fog (existing code)
                float4 fog;
                if (_IsHalfRes > 0.5)
                    fog = EdgeAwareUpsample(uv, fullResDepth);
                else
                    fog = SAMPLE_TEXTURE2D(_FogTexture, sampler_LinearClamp, uv);

                float3 scattering = fog.rgb;
                float transmittance = fog.a;

                // ---- Distance visibility fade (full-res depth) ----
                if (_DistanceFadeEnabled > 0.5)
                {
                    // Detect skybox pixels (depth == far clip in raw depth space)
                    float isSky = step(abs(fullResDepth - UNITY_RAW_FAR_CLIP_VALUE), 1e-4);

                    // Convert to eye depth (meters-ish)
                    float eyeDepth = LinearEyeDepth(fullResDepth, _ZBufferParams);

                    float denom = max(1e-3, _DistanceFadeEnd - _DistanceFadeStart);
                    float t = saturate((eyeDepth - _DistanceFadeStart) / denom);

                    // Smooth + controllable curve
                    t = smoothstep(0.0, 1.0, t);
                    t = pow(t, _DistanceFadeExponent);

                    // Optional: don’t apply to skybox
                    if (_DistanceFadeAffectsSkybox < 0.5)
                        t *= (1.0 - isSky);

                    t *= _DistanceFadeStrength;

                    // Fade object visibility by reducing transmittance
                    float transOld = transmittance;
                    float transNew = lerp(transOld, 0.0, t);

                    // Replace the removed visibility with a stable fog color
                    // (keeps things smooth and prevents “dark cutout silhouettes”)
                    float removed = transOld - transNew;

                    transmittance = transNew;
                    scattering += _DistanceFadeColor.rgb * removed;
                }

                // Composite
                float3 result = sceneColor.rgb * transmittance + scattering;
                return float4(result, sceneColor.a);
            }
            ENDHLSL
        }
    }
}