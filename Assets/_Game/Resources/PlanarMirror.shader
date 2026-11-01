Shader "ConGame/PlanarMirror"
{
    Properties { _BaseMap("Reflection", 2D) = "black" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Cull Back ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 screen : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screen = ComputeScreenPos(output.positionCS);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.screen.xy / input.screen.w;
                half3 reflected = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb;
                return half4(reflected * half3(0.86, 0.89, 0.91), 1);
            }
            ENDHLSL
        }
    }
}
