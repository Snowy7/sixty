// VolumetricFogRaymarch.shader
Shader "Hidden/VolumetricFog/Raymarch"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        
        Pass
        {
            Name "Fog Raymarch"
            ZWrite Off ZTest Always Cull Off
            
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            
            // Textures
            TEXTURE2D(_SourceTexture);
            TEXTURE2D(_FullResDepth);
            TEXTURE2D(_HalfResDepth);
            TEXTURE2D(_HistoryTexture);
            
            
            // Fog parameters
            float _Density;
            float _BaseHeight;
            float _MaxHeight;
            float _HeightFalloff;
            float4 _Albedo;
            float4 _AmbientLight;
            float _Anisotropy;
            float _ScatteringIntensity;
            float _Extinction;
            
            // Quality
            int _Steps;
            float _MaxDistance;
            float _TemporalBlend;
            int _FrameIndex;
            
            // Noise
            float _NoiseEnabled;
            float _NoiseScale;
            float _NoiseIntensity;
            float3 _NoiseWind;
            int _NoiseOctaves;
            
            // Lighting
            float _MainLightEnabled;
            float _AdditionalLightsEnabled;
            int _MaxAdditionalLights;
            float _PointLightIntensity;
            float _SpotLightIntensity;
            
            // Shadows
            float _VolumetricShadows;
            float _ShadowIntensity;
            int _ShadowSteps;
            
            // Matrices
            float4 _Resolution;
            float3 _CameraForward;
            
            // Flags
            float _UseTemporalReprojection;
            float _IsHalfRes;
            
            // ===================== NOISE =====================
            
            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }
            
            float ValueNoise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                
                return lerp(
                    lerp(lerp(Hash(i + float3(0,0,0)), Hash(i + float3(1,0,0)), f.x),
                         lerp(Hash(i + float3(0,1,0)), Hash(i + float3(1,1,0)), f.x), f.y),
                    lerp(lerp(Hash(i + float3(0,0,1)), Hash(i + float3(1,0,1)), f.x),
                         lerp(Hash(i + float3(0,1,1)), Hash(i + float3(1,1,1)), f.x), f.y), f.z);
            }
            
            float FBM(float3 p, int octaves)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;
                float maxValue = 0.0;
                
                [unroll(5)]
                for (int i = 0; i < octaves; i++)
                {
                    value += amplitude * ValueNoise(p * frequency);
                    maxValue += amplitude;
                    amplitude *= 0.5;
                    frequency *= 2.0;
                }
                
                return value / maxValue;
            }
            
            // ===================== PHASE FUNCTIONS =====================
            
            float HenyeyGreenstein(float cosTheta, float g)
            {
                float g2 = g * g;
                float denom = 1.0 + g2 - 2.0 * g * cosTheta;
                return (1.0 - g2) / (4.0 * PI * pow(abs(denom), 1.5) + 0.0001);
            }
            
            float DualLobePhase(float cosTheta, float g)
            {
                // Blend forward and back scattering for more realistic look
                float forward = HenyeyGreenstein(cosTheta, g);
                float backward = HenyeyGreenstein(cosTheta, -g * 0.3);
                return lerp(backward, forward, 0.7);
            }
            
            // ===================== DENSITY =====================
            
            float GetHeightFog(float3 pos)
            {
                float height = pos.y;
                
                // Below base height: full density
                // Above max height: no density
                // Between: exponential falloff
                if (height > _MaxHeight) return 0;
                if (height < _BaseHeight) return 1;
                
                float normalizedHeight = (height - _BaseHeight) / max(_MaxHeight - _BaseHeight, 0.001);
                return exp(-normalizedHeight * _HeightFalloff * 10.0);
            }
            
            float SampleDensity(float3 pos)
            {
                float density = _Density * GetHeightFog(pos);
                
                if (_NoiseEnabled > 0.5 && density > 0.0001)
                {
                    float3 noisePos = pos * _NoiseScale + _Time.y * _NoiseWind;
                    float noise = FBM(noisePos, _NoiseOctaves);
                    noise = saturate(noise * 2.0); // Remap to 0-1
                    density *= lerp(1.0, noise, _NoiseIntensity);
                }
                
                return max(0, density);
            }
            
            // ===================== LIGHTING =====================
            
            float SampleShadow(float3 worldPos)
            {
                if (_VolumetricShadows < 0.5) return 1.0;
                
                float4 shadowCoord = TransformWorldToShadowCoord(worldPos);
                float shadow = MainLightRealtimeShadow(shadowCoord);
                return lerp(1.0, shadow, _ShadowIntensity);
            }
            
            float3 ComputeMainLightScattering(float3 pos, float3 viewDir, float density)
            {
                if (_MainLightEnabled < 0.5 || density < 0.0001) return 0;
                
                Light mainLight = GetMainLight();
                float cosTheta = dot(viewDir, mainLight.direction);
                float phase = DualLobePhase(cosTheta, _Anisotropy);
                float shadow = SampleShadow(pos);
                
                return mainLight.color * phase * shadow * _ScatteringIntensity;
            }
            
            float3 ComputeAdditionalLightsScattering(float3 pos, float3 viewDir, float density)
            {
                if (_AdditionalLightsEnabled < 0.5 || density < 0.0001) return 0;
                
                float3 totalLight = 0;
                uint lightCount = min(GetAdditionalLightsCount(), (uint)_MaxAdditionalLights);
                
                [loop]
                for (uint i = 0; i < lightCount; i++)
                {
                    Light light = GetAdditionalLight(i, pos, 1.0);
                    
                    float cosTheta = dot(viewDir, light.direction);
                    // Use less anisotropic phase for point/spot lights
                    float phase = DualLobePhase(cosTheta, _Anisotropy * 0.5);
                    
                    float attenuation = light.distanceAttenuation * light.shadowAttenuation;
                    
                    // Determine intensity multiplier based on light type
                    // URP doesn't expose light type directly, but we can estimate from attenuation pattern
                    float intensityMul = _PointLightIntensity;
                    
                    totalLight += light.color * phase * attenuation * intensityMul * _ScatteringIntensity;
                }
                
                return totalLight;
            }
            
            // ===================== RAYMARCHING =====================
            
            float InterleavedGradientNoise2(float2 pixelCoord, int frame)
            {
                pixelCoord += (float)(frame % 64) * 5.588238f;
                return frac(52.9829189f * frac(0.06711056f * pixelCoord.x + 0.00583715f * pixelCoord.y));
            }
            
            float4 RaymarchFog(float2 uv, float2 pixelCoord)
            {
                // Sample depth
                float depth;
                if (_IsHalfRes > 0.5)
                {
                    depth = SAMPLE_TEXTURE2D_LOD(_HalfResDepth, sampler_PointClamp, uv, 0).r;
                }
                else
                {
                    depth = SampleSceneDepth(uv);
                }
                
                // Reconstruct world position
                #if UNITY_REVERSED_Z
                float depthValue = depth;
                #else
                float depthValue = lerp(UNITY_NEAR_CLIP_VALUE, 1, depth);
                #endif
                
                float3 worldPos = ComputeWorldSpacePosition(uv, depthValue, UNITY_MATRIX_I_VP);
                float3 camPos = GetCameraPositionWS();
                float3 rayDir = normalize(worldPos - camPos);
                
                float sceneDistance = length(worldPos - camPos);
                float rayLength = min(sceneDistance, _MaxDistance);
                
                // Adaptive step size - larger steps farther away
                float baseStepSize = rayLength / (float)_Steps;
                
                // Jitter to reduce banding
                float jitter = InterleavedGradientNoise2(pixelCoord, _FrameIndex);
                
                float transmittance = 1.0;
                float3 scatteredLight = 0;
                
                float currentDist = baseStepSize * jitter;
                
                [loop]
                for (int i = 0; i < _Steps && currentDist < rayLength && transmittance > 0.01; i++)
                {
                    float3 samplePos = camPos + rayDir * currentDist;
                    
                    // Adaptive step size: smaller steps near camera, larger far away
                    float adaptiveStep = baseStepSize * (1.0 + currentDist * 0.01);
                    adaptiveStep = min(adaptiveStep, baseStepSize * 3.0);
                    
                    float density = SampleDensity(samplePos);
                    
                    if (density > 0.0001)
                    {
                        // Beer-Lambert extinction
                        float extinction = density * _Extinction * adaptiveStep;
                        float sampleTransmittance = exp(-extinction);
                        
                        // In-scattering
                        float3 lightScattering = ComputeMainLightScattering(samplePos, rayDir, density);
                        lightScattering += ComputeAdditionalLightsScattering(samplePos, rayDir, density);
                        
                        // Add ambient
                        float3 ambient = _AmbientLight.rgb * density;
                        
                        // Energy-conserving integration
                        float3 scatteringIntegral = (lightScattering + ambient) * _Albedo.rgb;
                        scatteringIntegral *= (1.0 - sampleTransmittance) / max(_Extinction, 0.0001);
                        
                        scatteredLight += transmittance * scatteringIntegral;
                        transmittance *= sampleTransmittance;
                    }
                    
                    currentDist += adaptiveStep;
                }
                
                return float4(scatteredLight, transmittance);
            }
            
            // ===================== TEMPORAL REPROJECTION =====================
            
            float2 GetReprojectedUV(float3 worldPos)
            {
                float4 prevClip = mul(_PrevViewProjMatrix, float4(worldPos, 1.0));
                float2 prevUV = (prevClip.xy / prevClip.w) * 0.5 + 0.5;
                
                #if UNITY_UV_STARTS_AT_TOP
                prevUV.y = 1.0 - prevUV.y;
                #endif
                
                return prevUV;
            }
            
            float4 SampleHistoryWithClamp(float2 uv, float4 currentFog)
            {
                // Check bounds
                if (any(uv < 0) || any(uv > 1))
                    return currentFog;
                
                float4 history = SAMPLE_TEXTURE2D_LOD(_HistoryTexture, sampler_LinearClamp, uv, 0);
                
                // Neighborhood clamping to reduce ghosting
                // Simple variance-based clamping
                float4 minVal = currentFog - 0.15;
                float4 maxVal = currentFog + 0.15;
                
                history = clamp(history, minVal, maxVal);
                
                return history;
            }
            
            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 pixelCoord = uv * _Resolution.xy;
                
                // Raymarch fog
                float4 fogResult = RaymarchFog(uv, pixelCoord);
                
                // Temporal reprojection
                if (_UseTemporalReprojection > 0.5)
                {
                    // Get depth for reprojection
                    float depth;
                    if (_IsHalfRes > 0.5)
                        depth = SAMPLE_TEXTURE2D_LOD(_HalfResDepth, sampler_PointClamp, uv, 0).r;
                    else
                        depth = SampleSceneDepth(uv);
                    
                    float3 worldPos = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                    float2 historyUV = GetReprojectedUV(worldPos);
                    
                    float4 historyFog = SampleHistoryWithClamp(historyUV, fogResult);
                    
                    fogResult = lerp(fogResult, historyFog, _TemporalBlend);
                }
                
                return fogResult;
            }
            ENDHLSL
        }
    }
}