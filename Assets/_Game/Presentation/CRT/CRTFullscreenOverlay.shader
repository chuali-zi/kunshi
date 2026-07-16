Shader "ConGame/CRT/FullscreenOverlay"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _NoiseStrength ("Noise", Range(0, 1)) = 0.1
        _CrackAmount ("Crack", Range(0, 1)) = 0
        _PulseAmount ("Pulse", Range(0, 1)) = 0
        _Blackout ("Blackout", Range(0, 1)) = 0
        _TitleAmount ("Title", Range(0, 1)) = 0
        _ReverseShot ("Reverse", Range(0, 1)) = 0
        _Vignette ("Vignette", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        ZWrite Off ZTest Always Cull Off

        Pass
        {
            Name "CRTOverlay"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float _NoiseStrength;
            float _CrackAmount;
            float _PulseAmount;
            float _Blackout;
            float _TitleAmount;
            float _ReverseShot;
            float _Vignette;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float2 crackUv = uv + (_CrackAmount * 0.012 * float2(Hash(uv + _Time.y), Hash(uv.yx - _Time.y)) - 0.006);
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, crackUv);

                float scan = sin(uv.y * 780.0 + _Time.y * 12.0) * 0.04 * saturate(_NoiseStrength + _CrackAmount);
                float noise = (Hash(uv * float2(320.0, 180.0) + _Time.yy) - 0.5) * _NoiseStrength;
                color.rgb += scan + noise;

                float2 centered = uv * 2.0 - 1.0;
                float vignette = saturate(1.0 - dot(centered, centered) * _Vignette);
                color.rgb *= vignette;

                float pulse = saturate(_PulseAmount);
                color.rgb = lerp(color.rgb, half3(1, 1, 1), pulse * 0.55);
                color.rgb = lerp(color.rgb, half3(0.75, 0.78, 0.82), saturate(_CrackAmount) * 0.25);
                color.rgb = lerp(color.rgb, 1.0 - color.rgb, saturate(_ReverseShot));
                color.rgb = lerp(color.rgb, 0, saturate(_Blackout));

                // Soft title plate without UI text: dark band + bright center bar.
                float band = smoothstep(0.55, 0.42, abs(uv.y - 0.48));
                color.rgb = lerp(color.rgb, half3(0.02, 0.02, 0.03), band * _TitleAmount * 0.85);
                float bar = smoothstep(0.08, 0.0, abs(uv.y - 0.48)) * smoothstep(0.42, 0.28, abs(uv.x - 0.5));
                color.rgb = lerp(color.rgb, half3(0.92, 0.92, 0.9), bar * _TitleAmount);

                return color;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
