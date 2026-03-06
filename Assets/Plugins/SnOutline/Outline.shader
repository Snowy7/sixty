// Outline.shader
Shader "Hidden/Outline"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        // Pass 0: Main outline detection
        Pass
        {
            Name "OutlineDetection"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Thickness;
            float4 _OutlineColor;
            float _DepthThreshold;
            float _NormalThreshold;
            float _ColorThreshold;
            float _GlobalIntensity;
            float _UseDepth;
            float _UseNormals;
            float _UseColor;

            TEXTURE2D(_MaskTexture);
            SAMPLER(sampler_MaskTexture);

            float SampleLinearDepth(float2 uv)
            {
                float rawDepth = SampleSceneDepth(uv);
                return LinearEyeDepth(rawDepth, _ZBufferParams);
            }

            float3 SampleNormal(float2 uv)
            {
                return SampleSceneNormals(uv);
            }

            float4 SampleColor(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);
            }

            float RobertsCrossDepth(float2 uv, float2 texelSize)
            {
                float2 offset = texelSize * _Thickness;
                
                float d0 = SampleLinearDepth(uv);
                float d1 = SampleLinearDepth(uv + float2(offset.x, 0));
                float d2 = SampleLinearDepth(uv + float2(0, offset.y));
                float d3 = SampleLinearDepth(uv + offset);

                float diff1 = d3 - d0;
                float diff2 = d2 - d1;

                float depthScale = 1.0 / max(d0, 0.001);
                return sqrt(diff1 * diff1 + diff2 * diff2) * depthScale;
            }

            float SmoothNormalEdge(float2 uv, float2 texelSize)
            {
                float2 offset = texelSize * _Thickness;
                
                float3 center = SampleNormal(uv);
                
                float3 n1 = SampleNormal(uv + float2(-offset.x, 0));
                float3 n2 = SampleNormal(uv + float2(offset.x, 0));
                float3 n3 = SampleNormal(uv + float2(0, -offset.y));
                float3 n4 = SampleNormal(uv + float2(0, offset.y));

                float3 n5 = SampleNormal(uv + float2(-offset.x, -offset.y) * 0.707);
                float3 n6 = SampleNormal(uv + float2(offset.x, -offset.y) * 0.707);
                float3 n7 = SampleNormal(uv + float2(-offset.x, offset.y) * 0.707);
                float3 n8 = SampleNormal(uv + float2(offset.x, offset.y) * 0.707);

                float edge = 0;
                edge += 1.0 - saturate(dot(center, n1));
                edge += 1.0 - saturate(dot(center, n2));
                edge += 1.0 - saturate(dot(center, n3));
                edge += 1.0 - saturate(dot(center, n4));
                
                edge += (1.0 - saturate(dot(center, n5))) * 0.5;
                edge += (1.0 - saturate(dot(center, n6))) * 0.5;
                edge += (1.0 - saturate(dot(center, n7))) * 0.5;
                edge += (1.0 - saturate(dot(center, n8))) * 0.5;

                return edge / 6.0;
            }

            float SobelColor(float2 uv, float2 texelSize)
            {
                float2 offset = texelSize * _Thickness;
                
                float3 c00 = SampleColor(uv + float2(-offset.x, -offset.y)).rgb;
                float3 c10 = SampleColor(uv + float2(0, -offset.y)).rgb;
                float3 c20 = SampleColor(uv + float2(offset.x, -offset.y)).rgb;
                float3 c01 = SampleColor(uv + float2(-offset.x, 0)).rgb;
                float3 c21 = SampleColor(uv + float2(offset.x, 0)).rgb;
                float3 c02 = SampleColor(uv + float2(-offset.x, offset.y)).rgb;
                float3 c12 = SampleColor(uv + float2(0, offset.y)).rgb;
                float3 c22 = SampleColor(uv + float2(offset.x, offset.y)).rgb;

                float3 sobelX = c00 + 2*c01 + c02 - c20 - 2*c21 - c22;
                float3 sobelY = c00 + 2*c10 + c20 - c02 - 2*c12 - c22;

                return sqrt(dot(sobelX, sobelX) + dot(sobelY, sobelY));
            }

            // Use point sampling to avoid half-values at edges
float SampleMask(float2 uv)
{
    return SAMPLE_TEXTURE2D(_MaskTexture, sampler_PointClamp, uv).r;
}

float SampleMaskDilated(float2 uv, float2 texelSize)
{
    float2 o = texelSize * _Thickness;

    float m = 0;
    m = max(m, SampleMask(uv));
    m = max(m, SampleMask(uv + float2( o.x, 0)));
    m = max(m, SampleMask(uv + float2(-o.x, 0)));
    m = max(m, SampleMask(uv + float2(0,  o.y)));
    m = max(m, SampleMask(uv + float2(0, -o.y)));
    m = max(m, SampleMask(uv + float2( o.x,  o.y)));
    m = max(m, SampleMask(uv + float2(-o.x,  o.y)));
    m = max(m, SampleMask(uv + float2( o.x, -o.y)));
    m = max(m, SampleMask(uv + float2(-o.x, -o.y)));

    return m;
}

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 texelSize = _BlitTexture_TexelSize.xy;
                float4 sceneColor = SampleColor(uv);

                float mask = SampleMaskDilated(uv, texelSize);
                if (mask < 0.5)
                    return sceneColor;

                float outline = 0;

                if (_UseDepth > 0.5)
                {
                    float depthEdge = RobertsCrossDepth(uv, texelSize);
                    outline = max(outline, smoothstep(_DepthThreshold * 0.5, _DepthThreshold, depthEdge));
                }

                if (_UseNormals > 0.5)
                {
                    float normalEdge = SmoothNormalEdge(uv, texelSize);
                    outline = max(outline, smoothstep(_NormalThreshold * 0.5, _NormalThreshold, normalEdge));
                }

                if (_UseColor > 0.5)
                {
                    float colorEdge = SobelColor(uv, texelSize);
                    outline = max(outline, smoothstep(_ColorThreshold * 0.5, _ColorThreshold, colorEdge));
                }

                outline = saturate(outline * _GlobalIntensity) * mask;
return lerp(sceneColor, float4(_OutlineColor.rgb, 1), outline * _OutlineColor.a);
            }
            ENDHLSL
        }

        // Pass 1: Simple unlit for mask rendering
        Pass
        {
                Name "MaskPass"
    Tags { "LightMode" = "SRPDefaultUnlit" }
    ZWrite Off
    ZTest LEqual
            
            HLSLPROGRAM
            #pragma vertex VertMask
            #pragma fragment FragMask

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct AttributesMask
            {
                float4 positionOS : POSITION;
            };

            struct VaryingsMask
            {
                float4 positionCS : SV_POSITION;
            };

            VaryingsMask VertMask(AttributesMask input)
            {
                VaryingsMask output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            float4 FragMask(VaryingsMask input) : SV_Target
            {
                return float4(1, 1, 1, 1);
            }
            ENDHLSL
        }
    }
}