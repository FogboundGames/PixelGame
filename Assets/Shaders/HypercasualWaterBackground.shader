Shader "PixelGame/HypercasualWaterBackground"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite / Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Water Area Settings)]
        _WaterThresholdV ("Water Line V (0=Bottom, 1=Top)", Range(0.2, 0.6)) = 0.44
        _WaterTransitionSmooth ("Water Transition Softness", Range(0.01, 0.15)) = 0.04
        _WaterBlueDominance ("Water Blue Detection Factor", Range(0.0, 0.5)) = 0.08

        [Header(Gentle Wave Undulation)]
        _WaveSpeed ("Wave Speed", Range(0.2, 4.0)) = 1.25
        _WaveFrequency ("Wave Frequency", Range(4.0, 35.0)) = 16.0
        _WaveAmplitude ("Wave Amplitude", Range(0.001, 0.025)) = 0.006

        [Header(Sunlight Caustics and Shimmer)]
        _ShimmerSpeed ("Shimmer Speed", Range(0.2, 4.0)) = 1.5
        _ShimmerScale ("Shimmer Scale", Range(4.0, 30.0)) = 15.0
        _ShimmerIntensity ("Shimmer Intensity", Range(0.0, 0.6)) = 0.20
        _ShimmerColor ("Shimmer Color", Color) = (0.75, 0.96, 1.0, 1.0)

        [Header(Shoreline Wave Lapping)]
        _TideSpeed ("Shore Tide Speed", Range(0.3, 3.0)) = 1.2
        _TideHeight ("Shore Tide Height", Range(0.001, 0.02)) = 0.005

        // UI Canvas masking properties
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

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
                float4 worldPos : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _WaterThresholdV;
                float _WaterTransitionSmooth;
                float _WaterBlueDominance;

                float _WaveSpeed;
                float _WaveFrequency;
                float _WaveAmplitude;

                float _ShimmerSpeed;
                float _ShimmerScale;
                float _ShimmerIntensity;
                float4 _ShimmerColor;

                float _TideSpeed;
                float _TideHeight;

                // Dinamik interaktif dalgalar (Interactive Ripples)
                // Her eleman: (uvX, uvY, radius, intensity)
                float4 _Ripple0;
                float4 _Ripple1;
                float4 _Ripple2;
                float4 _Ripple3;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _Color;
                output.uv = input.uv;
                output.worldPos = input.positionOS;
                return output;
            }

            // Tek bir interaktif dalga halkasının UV kaydırmasını ve köpük parlamasını hesaplar
            void CalculateRipple(float4 rippleData, float2 uv, inout float2 uvOffset, inout float rippleGlow)
            {
                if (rippleData.w <= 0.001) return;

                float2 center = rippleData.xy;
                float radius = rippleData.z;
                float strength = rippleData.w;

                // Aspect ratio düzeltmesi (1080/1920 ~ 0.5625)
                float2 diff = uv - center;
                diff.y *= 1.777f; // Dairesel görünmesi için dikey ölçekleme
                float dist = length(diff);

                float waveWidth = 0.06;
                float delta = dist - radius;

                if (abs(delta) < waveWidth && radius > 0.001)
                {
                    float factor = 1.0 - (abs(delta) / waveWidth);
                    float sinWave = sin(delta / waveWidth * 3.14159);
                    
                    float2 normDir = (dist > 0.0001) ? normalize(diff) : float2(0, 1);
                    uvOffset += normDir * (sinWave * strength * 0.015 * factor);
                    rippleGlow += saturate(factor * strength * 0.35);
                }
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 baseUV = input.uv;

                // 1. Önce ham dokudan pikselleri örnekleyerek su mu kum mu olduğunu kesinleştir
                half4 rawCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, baseUV);

                // Su maskesi: v < _WaterThresholdV ve (b > r + _WaterBlueDominance)
                float vFactor = smoothstep(_WaterThresholdV + _WaterTransitionSmooth, _WaterThresholdV - _WaterTransitionSmooth, baseUV.y);
                float blueFactor = smoothstep(0.0, 0.15, (rawCol.b - rawCol.r) - _WaterBlueDominance);
                float waterMask = saturate(vFactor * (blueFactor * 0.85 + 0.15));

                // 2. Kıyı Gel-Giti (Shoreline Tide Breathing)
                float tide = sin(_Time.y * _TideSpeed) * _TideHeight;

                // 3. Organik Sıvı Dalgalanması (Harmonik Çift Sinüs & Kosinüs UV Distorsiyonu)
                float timeWave = _Time.y * _WaveSpeed;
                float2 waveOffset;
                waveOffset.x = sin(baseUV.y * _WaveFrequency + timeWave) * _WaveAmplitude
                             + cos(baseUV.x * (_WaveFrequency * 0.72) - timeWave * 0.85) * (_WaveAmplitude * 0.55);
                waveOffset.y = cos(baseUV.x * (_WaveFrequency * 0.88) + timeWave * 1.1) * (_WaveAmplitude * 0.65)
                             + sin(baseUV.y * (_WaveFrequency * 0.55) - timeWave * 0.75) * (_WaveAmplitude * 0.45)
                             + tide * 0.4;

                // 4. Dinamik İnteraktif Dalgalar (Gemiler hareket ettiğinde / yanaştığında)
                float2 rippleUVOffset = float2(0, 0);
                float rippleGlow = 0.0;
                CalculateRipple(_Ripple0, baseUV, rippleUVOffset, rippleGlow);
                CalculateRipple(_Ripple1, baseUV, rippleUVOffset, rippleGlow);
                CalculateRipple(_Ripple2, baseUV, rippleUVOffset, rippleGlow);
                CalculateRipple(_Ripple3, baseUV, rippleUVOffset, rippleGlow);

                // Yalnızca SU bölgesini dalgalandır! Kum alanını (%100) sabit ve net tut!
                float2 finalUV = baseUV + (waveOffset + rippleUVOffset) * waterMask;

                half4 texCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, finalUV);

                // 5. Tropik Güneş Kostik Parıltısı (Sunlight Sparkle / Shimmer)
                if (waterMask > 0.01)
                {
                    float timeShimmer = _Time.y * _ShimmerSpeed;
                    float s1 = sin((finalUV.x * 1.2 + finalUV.y * 1.5) * _ShimmerScale + timeShimmer);
                    float s2 = cos((finalUV.x * 1.8 - finalUV.y * 1.1) * (_ShimmerScale * 0.9) - timeShimmer * 0.85);
                    float shimmerPattern = saturate(s1 * s2 * 0.5 + 0.5);
                    float shimmer = pow(shimmerPattern, 4.0) * _ShimmerIntensity * waterMask;

                    // Parıltıyı ve interaktif dalga köpüğünü renge ekle
                    texCol.rgb += _ShimmerColor.rgb * shimmer;
                    texCol.rgb += half3(0.9, 0.98, 1.0) * (rippleGlow * waterMask);
                }

                return texCol * input.color;
            }
            ENDHLSL
        }
    }
}
