Shader "Sixty/CrystalEntity"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.2, 0.85, 0.9, 1)
        _EdgeColor ("Edge Color", Color) = (0.6, 1.0, 1.0, 1)
        _EmissionColor ("Emission Color", Color) = (0.3, 0.9, 0.95, 1)
        _EmissionIntensity ("Emission Intensity", Float) = 1.5
        _FresnelPower ("Fresnel Power", Float) = 3.0
        _FresnelIntensity ("Fresnel Intensity", Float) = 0.6
        _FacetStrength ("Facet Strength", Range(0, 1)) = 0.85
        _SpecularPower ("Specular Power", Float) = 64.0
        _SpecularIntensity ("Specular Intensity", Float) = 1.2
        _AmbientStrength ("Ambient Strength", Float) = 0.25
        _CavityWidth ("Cavity Width", Float) = 0.06
        _CavityStrength ("Cavity Strength", Float) = 0.5
        _CavityColor ("Cavity Color", Color) = (0.05, 0.15, 0.18, 1)
        [Toggle] _UseFlashColor ("Use Flash Color", Float) = 0
        _FlashColor ("Flash Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 200

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

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _EdgeColor;
                float4 _EmissionColor;
                float  _EmissionIntensity;
                float  _FresnelPower;
                float  _FresnelIntensity;
                float  _FacetStrength;
                float  _SpecularPower;
                float  _SpecularIntensity;
                float  _AmbientStrength;
                float  _CavityWidth;
                float  _CavityStrength;
                float4 _CavityColor;
                float  _UseFlashColor;
                float4 _FlashColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float  fogFactor  : TEXCOORD2;
                nointerpolation float3 normalOS : TEXCOORD3;
                float3 positionOS : TEXCOORD4;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs vpi = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vpi.positionCS;
                output.positionWS = vpi.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.normalOS = input.normalOS;
                output.positionOS = input.positionOS.xyz;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Flat (faceted) normal from screen-space derivatives for crystal look
                float3 smoothNormal = normalize(input.normalWS);
                float3 dpdx_val = ddx(input.positionWS);
                float3 dpdy_val = ddy(input.positionWS);
                float3 flatNormal = normalize(cross(dpdx_val, dpdy_val));

                // Blend between smooth and faceted
                float3 normal = normalize(lerp(smoothNormal, flatNormal, _FacetStrength));

                // Flash override
                if (_UseFlashColor > 0.5)
                {
                    return half4(_FlashColor.rgb * 2.0, 1);
                }

                // Lighting
                Light mainLight = GetMainLight();
                float ndotl = saturate(dot(normal, mainLight.direction));

                // View
                float3 viewDir = normalize(_WorldSpaceCameraPos - input.positionWS);

                // Fresnel rim glow
                float fresnel = pow(1.0 - saturate(dot(viewDir, normal)), _FresnelPower);
                fresnel *= _FresnelIntensity;

                // Specular (Blinn-Phong)
                float3 halfDir = normalize(mainLight.direction + viewDir);
                float spec = pow(saturate(dot(normal, halfDir)), _SpecularPower) * _SpecularIntensity;

                // Edge silhouette detection using the smooth normal vs view angle
                float edgeDot = 1.0 - saturate(dot(viewDir, smoothNormal));
                float edgeLine = smoothstep(0.55, 0.85, edgeDot);

                // Facet variation: each flat face gets slightly different brightness
                float facetDiff = 1.0 - saturate(dot(flatNormal, smoothNormal));
                float facetShade = lerp(0.85, 1.15, facetDiff * 3.0);

                // Base color with lighting
                float3 color = _BaseColor.rgb * facetShade * (_AmbientStrength + ndotl * 0.65) * mainLight.color;

                // Cavity: face-local edge darkening (same logic as void cubes)
                float3 absNorm = abs(input.normalOS);
                float2 faceUV;
                if (absNorm.y > absNorm.x && absNorm.y > absNorm.z)
                    faceUV = input.positionOS.xz;
                else if (absNorm.x > absNorm.z)
                    faceUV = input.positionOS.yz;
                else
                    faceUV = input.positionOS.xy;

                float2 edgeDist2D = float2(0.5, 0.5) - abs(faceUV);
                float minEdgeDist = min(edgeDist2D.x, edgeDist2D.y);
                float cavity = smoothstep(0.0, _CavityWidth, minEdgeDist);
                color = lerp(_CavityColor.rgb * _CavityStrength + color * (1.0 - _CavityStrength), color, cavity);

                // Specular highlight
                color += spec * mainLight.color * 0.5;

                // Fresnel rim
                color += fresnel * _EdgeColor.rgb;

                // Silhouette edge highlight (clean bright edges)
                color += edgeLine * _EdgeColor.rgb * 1.5;

                // Emission core glow
                color += _EmissionColor.rgb * _EmissionIntensity * (0.25 + fresnel * 0.5);

                color = MixFog(color, input.fogFactor);

                return half4(color, 1);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Lit"
}
