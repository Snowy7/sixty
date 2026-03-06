Shader "Sixty/VoidCityInstanced"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.02, 0.025, 0.035, 1)
        _GlowColor ("Glow Color", Color) = (0.9, 0.95, 1.0, 1)
        _GlowIntensity ("Glow Intensity", Float) = 3.0
        _EdgeDarken ("Edge Darken", Float) = 0.7
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
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
                float4 _GlowColor;
                float  _GlowIntensity;
                float  _EdgeDarken;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                uint   instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float  fogFactor  : TEXCOORD2;
                float  emissive   : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VoidCubeInstance inst = _InstanceBuffer[input.instanceID];

                // Scale and translate the cube vertex
                float3 worldPos = input.positionOS.xyz * inst.scale + inst.position;
                output.positionWS = worldPos;
                output.positionCS = TransformWorldToHClip(worldPos);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.emissive = inst.emissive;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Simple directional lighting
                float3 normal = normalize(input.normalWS);
                Light mainLight = GetMainLight();
                float ndotl = saturate(dot(normal, mainLight.direction));

                // Edge darkening based on normal direction (top faces lighter)
                float topFactor = saturate(normal.y * 0.5 + 0.5);
                float edgeFactor = lerp(_EdgeDarken, 1.0, topFactor);

                float3 baseCol = _BaseColor.rgb * edgeFactor;
                float3 lit = baseCol * (0.15 + ndotl * 0.35); // Very subtle lighting, mostly ambient

                // Glow cubes
                if (input.emissive > 0.5)
                {
                    lit = _GlowColor.rgb * _GlowIntensity;
                }

                // Fog
                lit = MixFog(lit, input.fogFactor);

                return half4(lit, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
