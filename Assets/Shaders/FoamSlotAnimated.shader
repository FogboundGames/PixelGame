Shader "PixelGame/FoamSlotAnimated"
{
    Properties
    {
        [MainTexture] _MainTex ("Foam Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Color Tint", Color) = (1, 1, 1, 1)

        [Header(Water Matched Wave Undulation)]
        _WaveSpeed ("Wave Speed (Synchronized with Water)", Range(0.2, 4.0)) = 1.25
        _WaveFrequency ("Wave Frequency", Range(2.0, 30.0)) = 10.0
        _WaveAmplitude ("Wave Amplitude", Range(0.001, 0.035)) = 0.008

        [Header(Foam Breathing and Pulse)]
        _PulseSpeed ("Pulse Speed", Range(0.2, 3.0)) = 1.1
        _PulseAmount ("Pulse Expansion Amount", Range(0.0, 0.04)) = 0.012
        _AlphaBreath ("Alpha Breath Strength", Range(0.0, 0.3)) = 0.08

        [Header(Sunlight Caustic Sparkle on Foam)]
        _ShimmerSpeed ("Shimmer Speed", Range(0.2, 4.0)) = 1.4
        _ShimmerScale ("Shimmer Scale", Range(4.0, 35.0)) = 18.0
        _ShimmerIntensity ("Sparkle Intensity", Range(0.0, 0.8)) = 0.28
        _SparkleColor ("Sparkle Color", Color) = (0.95, 1.0, 1.0, 1.0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "FoamSlotUnlit"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _WaveSpeed;
                float _WaveFrequency;
                float _WaveAmplitude;
                float _PulseSpeed;
                float _PulseAmount;
                float _AlphaBreath;
                float _ShimmerSpeed;
                float _ShimmerScale;
                float _ShimmerIntensity;
                float4 _SparkleColor;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _BaseColor;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 baseUV = input.uv;

                // 1. Organik Sıvı Dalgalanması (Su yüzeyi dalgasıyla tam senkron)
                float timeWave = _Time.y * _WaveSpeed;
                float2 waveOffset;
                waveOffset.x = sin(baseUV.y * _WaveFrequency + timeWave) * _WaveAmplitude
                             + cos(baseUV.x * (_WaveFrequency * 0.72) - timeWave * 0.85) * (_WaveAmplitude * 0.55);
                waveOffset.y = cos(baseUV.x * (_WaveFrequency * 0.88) + timeWave * 1.1) * (_WaveAmplitude * 0.65)
                             + sin(baseUV.y * (_WaveFrequency * 0.55) - timeWave * 0.75) * (_WaveAmplitude * 0.45);

                // 2. Köpük Halkasının Hafif Nefes Alması / Genleşmesi (Breathing Expansion)
                float pulse = sin(_Time.y * _PulseSpeed) * _PulseAmount;
                float2 centerUV = float2(0.5, 0.5);
                float2 dirFromCenter = baseUV - centerUV;

                float2 finalUV = baseUV + waveOffset + dirFromCenter * pulse;

                // 3. Doku Örnekleme
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, finalUV);

                // Kenar taşmalarını temiz tut
                if (finalUV.x < 0.0 || finalUV.x > 1.0 || finalUV.y < 0.0 || finalUV.y > 1.0)
                {
                    col.a = 0.0;
                }

                // 4. Köpük Kabarcıklarında Güneş Parıltısı (Sunlight Sparkle / Shimmer)
                if (col.a > 0.02)
                {
                    float timeShimmer = _Time.y * _ShimmerSpeed;
                    float s1 = sin((finalUV.x * 2.5 + finalUV.y * 3.5) * _ShimmerScale + timeShimmer);
                    float s2 = cos((finalUV.x * 3.2 - finalUV.y * 2.4) * (_ShimmerScale * 0.85) - timeShimmer * 0.9);
                    float sparklePattern = saturate(s1 * s2 * 0.5 + 0.5);
                    float sparkle = pow(sparklePattern, 5.0) * _ShimmerIntensity;

                    // Parıltı ekle
                    col.rgb += _SparkleColor.rgb * (sparkle * col.a);

                    // 5. Hafif Canlılık / Parlaklık Nefesi
                    float breath = 1.0 + sin(_Time.y * _PulseSpeed * 1.4) * _AlphaBreath;
                    col.rgb *= breath;
                    col.a = saturate(col.a * (1.0 + sin(_Time.y * _PulseSpeed * 1.1) * (_AlphaBreath * 0.6)));
                }

                return col * input.color;
            }
            ENDHLSL
        }
    }
}
