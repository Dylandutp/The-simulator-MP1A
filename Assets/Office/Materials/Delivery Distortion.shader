Shader "Office/Delivery UV Distortion"
{
    Properties
    {
        _BaseMap ("Yellow Pattern", 2D) = "white" {}
        _NoiseMap ("Distortion Noise", 2D) = "gray" {}
        _Distortion ("Distortion Strength", Range(0, 0.3)) = 0.12
        _NoiseOffset ("Noise Offset", Vector) = (0,0,0,0)
        _InwardOffset ("Inward Flow", Float) = 0
        _RingCount ("Number of Bands", Range(1, 8)) = 4
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _NoiseOffset;
                float _Distortion;
                float _InwardOffset;
                float _RingCount;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap,
                    input.uv * 2 + _NoiseOffset.xy).rg * 2 - 1;
                float2 distortedUV = input.uv + noise * (_Distortion / (2 * _RingCount));
                float2 fromCenter = abs(distortedUV - 0.5) * 2;
                float distanceToCenter = max(fromCenter.x, fromCenter.y);
                float2 uv = float2(distanceToCenter * _RingCount + _InwardOffset, 0.5);
                return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
            }
            ENDHLSL
        }
    }
}
