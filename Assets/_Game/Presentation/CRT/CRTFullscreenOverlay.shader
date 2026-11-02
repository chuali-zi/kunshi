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
        _TitleTex ("Title ink", 2D) = "black" {}
        _CutInTex ("Realisation panel", 2D) = "black" {}
        _GlassTex ("Glass fracture", 2D) = "black" {}
        _DarkAmount ("Lights out grade", Range(0, 1)) = 0
        _DeathTitle ("Death title", Range(0, 1)) = 0
        _ScreenCrack ("Camera bullet fracture", Range(0, 1)) = 0
        _CutInAmount ("Realisation", Range(0, 1)) = 0
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
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_TitleTex); SAMPLER(sampler_TitleTex);
            TEXTURE2D(_CutInTex); SAMPLER(sampler_CutInTex);
            TEXTURE2D(_GlassTex); SAMPLER(sampler_GlassTex);
            float _DarkAmount, _DeathTitle, _ScreenCrack, _CutInAmount;
            float _NoiseStrength;
            float _CrackAmount;
            float _PulseAmount;
            float _Blackout;
            float _TitleAmount;
            float _ReverseShot;
            float _Vignette;

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float fracture = saturate(max(_CrackAmount, _ScreenCrack));
                float4 glass = SAMPLE_TEXTURE2D(_GlassTex, sampler_GlassTex, uv);
                float2 sourceUv = uv + fracture * glass.a * (glass.rg - 0.5) * 0.009;
                float3 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, sourceUv).rgb;

                if (_DarkAmount > 0.0)
                {
                    float luma = max(0.0, dot(color, float3(0.2126, 0.7152, 0.0722)));
                    color = lerp(color, luma * float3(0.77, 0.86, 1.0), _DarkAmount * 0.97);
                }
                float grain = (Hash(uv * float2(320.0, 180.0) + _Time.yy) - 0.5) * _NoiseStrength * 0.18;
                color += grain;
                float2 centered = uv * 2.0 - 1.0;
                color *= saturate(1.0 - dot(centered, centered) * _Vignette);
                if (_PulseAmount > 0.0) color = lerp(color, float3(1,1,1), saturate(_PulseAmount) * 0.55);
                if (fracture > 0.0) color = lerp(color, glass.rgb, glass.a * fracture);
                if (_ReverseShot > 0.0) color = lerp(color, 1.0 - color, saturate(_ReverseShot));
                if (_Blackout > 0.0) color *= 1.0 - saturate(_Blackout);

                if (_CutInAmount > 0.0)
                {
                    float4 panel = SAMPLE_TEXTURE2D(_CutInTex, sampler_CutInTex, uv);
                    float opacity = panel.a * _CutInAmount * (1.0 - _Blackout);
                    color = opacity >= 1.0 ? panel.rgb : lerp(color, panel.rgb, opacity);
                }
                float title = max(_DeathTitle, _TitleAmount);
                if (title > 0.0)
                {
                    color *= 1.0 - _DeathTitle * 0.48;
                    float2 titleUv = (uv - float2(0.67, 0.73)) / float2(0.37, 0.30) + 0.5;
                    if (all(titleUv >= 0.0) && all(titleUv <= 1.0))
                    {
                        float4 ink = SAMPLE_TEXTURE2D(_TitleTex, sampler_TitleTex, titleUv);
                        color = lerp(color, ink.rgb, ink.a * title);
                    }
                }
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
