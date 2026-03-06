// DepthDownsample.shader
Shader "Hidden/VolumetricFog/DepthDownsample"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        
        Pass
        {
            Name "Depth Downsample"
            ZWrite Off ZTest Always Cull Off
            
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            
            float4 _Resolution;
            
            float Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 texelSize = _Resolution.zw;
                
                // Sample 4 depth values and take the closest (max in reversed-Z)
                float d0 = SampleSceneDepth(uv + float2(-0.25, -0.25) * texelSize);
                float d1 = SampleSceneDepth(uv + float2(0.25, -0.25) * texelSize);
                float d2 = SampleSceneDepth(uv + float2(-0.25, 0.25) * texelSize);
                float d3 = SampleSceneDepth(uv + float2(0.25, 0.25) * texelSize);
                
                #if UNITY_REVERSED_Z
                return max(max(d0, d1), max(d2, d3)); // Closest depth
                #else
                return min(min(d0, d1), min(d2, d3));
                #endif
            }
            ENDHLSL
        }
    }
}