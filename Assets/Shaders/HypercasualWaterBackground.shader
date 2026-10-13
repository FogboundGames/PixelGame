Shader "PixelGame/HypercasualWaterBackground"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite / Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Water Area Settings)]
        _WaterMinV ("Water Min V (Bottom Shore)", Range(0.0, 0.4)) = 0.01
        _WaterMaxV ("Water Max V (Top Shore)", Range(0.5, 1.0)) = 0.88
        _WaterTransitionSmooth ("Water Transition Softness", Range(0.01, 0.15)) = 0.03
        _WaterBlueDominance ("Water Blue Detection Factor", Range(-0.05, 0.2)) = 0.02
        _WaterDarkness ("Water Darkness / Deep Tint Blend", Range(0.0, 1.0)) = 0.15
        _WaterBrightness ("Water Brightness Multiplier", Range(0.3, 1.5)) = 1.0
        _WaterDeepTint ("Water Deep Ocean Color Tint", Color) = (0.04, 0.40, 0.78, 1.0)
        _CausticSoften ("Caustic Highlight Soften (0=texture as is)", Range(0.0, 1.5)) = 0.0
        _CausticMaxV ("Caustic Soften Top V (slot lines above stay)", Range(0.0, 1.0)) = 0.265
        _StillWaterAboveV ("Still Water Above V (slot şeridi dalgalanmaz, 0 = kapalı)", Range(0.0, 1.0)) = 0.0

        [Header(Sand Beach Settings)]
        _SandBrightness ("Sand Brightness", Range(0.8, 1.8)) = 1.0
        _SandLighten ("Sand Lighten (0=Normal, 1=Extra Light)", Range(0.0, 1.0)) = 0.0

        [Header(Gentle Wave Undulation)]
        _WaveSpeed ("Wave Speed", Range(0.2, 4.0)) = 0.50
        _WaveFrequency ("Wave Frequency", Range(4.0, 35.0)) = 6.0
        _WaveAmplitude ("Wave Amplitude", Range(0.001, 0.025)) = 0.0035

        [Header(Sunlight Caustics and Shimmer)]
        _ShimmerSpeed ("Shimmer Speed", Range(0.2, 4.0)) = 1.0
        _ShimmerScale ("Shimmer Scale", Range(4.0, 30.0)) = 16.0
        _ShimmerIntensity ("Shimmer Intensity", Range(0.0, 0.6)) = 0.0
        _ShimmerColor ("Shimmer Color", Color) = (0.75, 0.96, 1.0, 1.0)

        [Header(Shoreline Wave Lapping)]
        _TideSpeed ("Shore Tide Speed", Range(0.3, 3.0)) = 0.8
        _TideHeight ("Shore Tide Height", Range(0.001, 0.02)) = 0.003

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
                float _WaterMinV;
                float _WaterMaxV;
                float _WaterTransitionSmooth;
                float _WaterBlueDominance;
                float _WaterDarkness;
                float _WaterBrightness;
                float4 _WaterDeepTint;
                float _CausticSoften;
                float _CausticMaxV;
                float _StillWaterAboveV;

                float _SandBrightness;
                float _SandLighten;

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

                // Su maskesi: Ortadaki lagün alanı (V: _WaterMinV.._WaterMaxV) ve mavi renk baskınlığı
                float vTop = smoothstep(_WaterMaxV + _WaterTransitionSmooth, _WaterMaxV - _WaterTransitionSmooth, baseUV.y);
                float vBottom = smoothstep(_WaterMinV - _WaterTransitionSmooth, _WaterMinV + _WaterTransitionSmooth, baseUV.y);
                float vZone = vTop * vBottom;
                float blueFactor = smoothstep(0.0, 0.12, (rawCol.b - rawCol.r) - _WaterBlueDominance);
                float waterMask = saturate(vZone * blueFactor);

                // 2. Kıyı Gel-Giti (Shoreline Tide Breathing - Yumuşak ve sakin nefes alma)
                float tide = sin(_Time.y * _TideSpeed) * _TideHeight;

                // 3. Hypercasual Sıvı Dalgalanması (Geniş, akıcı, tatlı lagün salınımı)
                float timeWave = _Time.y * _WaveSpeed;
                float2 waveOffset;
                waveOffset.x = sin(baseUV.y * _WaveFrequency + timeWave * 0.85) * _WaveAmplitude
                             + cos(baseUV.x * (_WaveFrequency * 0.70) - timeWave * 0.65) * (_WaveAmplitude * 0.55);
                waveOffset.y = cos(baseUV.x * (_WaveFrequency * 0.80) + timeWave * 0.75) * (_WaveAmplitude * 0.65)
                             + sin(baseUV.y * (_WaveFrequency * 0.50) - timeWave * 0.50) * (_WaveAmplitude * 0.45)
                             + tide * 0.35;

                // 4. Dinamik İnteraktif Dalgalar (Gemiler hareket ettiğinde / yanaştığında)
                float2 rippleUVOffset = float2(0, 0);
                float rippleGlow = 0.0;
                CalculateRipple(_Ripple0, baseUV, rippleUVOffset, rippleGlow);
                CalculateRipple(_Ripple1, baseUV, rippleUVOffset, rippleGlow);
                CalculateRipple(_Ripple2, baseUV, rippleUVOffset, rippleGlow);
                CalculateRipple(_Ripple3, baseUV, rippleUVOffset, rippleGlow);

                // Yalnızca SU bölgesini dalgalandır! Kum alanını (%100) sabit ve net tut!
                // Slot kesikli çizgileri resme çizili: bu V'nin üstündeki su (slot şeridi) dalgalanmaz, çizgiler sabit kalır
                float stillMask = _StillWaterAboveV > 0.001 ? smoothstep(_StillWaterAboveV - 0.025, _StillWaterAboveV, baseUV.y) : 0.0;
                float2 finalUV = baseUV + (waveOffset + rippleUVOffset) * waterMask * (1.0 - stillMask);

                half4 texCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, finalUV);

                // Zemin (Ground) alanını tonlama / açma ayarı (Off-White Leaning Gray)
                float sandFactor = (1.0 - waterMask);
                if (sandFactor > 0.01 && (_SandBrightness != 1.0 || _SandLighten > 0.001))
                {
                    half3 lightGround = texCol.rgb * _SandBrightness;
                    lightGround = lerp(lightGround, half3(0.96, 0.97, 0.985), _SandLighten);
                    texCol.rgb = lerp(texCol.rgb, lightGround, sandFactor);
                }

                // Su alanını koyulaştırma ve gemileri ön plana çıkarma (Darker & Deeper Tropical Ocean Water)
                if (waterMask > 0.005)
                {
                    // Parlaklığı kıs ve kontrastı artır
                    half3 darkened = texCol.rgb * _WaterBrightness;
                    // Canlı ve zengin derin okyanus rengi ile zenginleştir (soluklaşmadan derinleşsin)
                    half3 deepOcean = lerp(darkened, darkened * _WaterDeepTint.rgb * 1.85, _WaterDarkness);
                    texCol.rgb = lerp(texCol.rgb, deepOcean, waterMask);

                    // Resme gömülü beyaz ışık hareleri (caustic) çok parlak: su zemininde kırmızı kanal ~0,
                    // harelerde yükselir. Kırmızı kadarını (mavi ağırlıklı az) düşürerek yalnızca hareleri yumuşat.
                    if (_CausticSoften > 0.001)
                    {
                        half caustic = texCol.r;
                        // Slot kesikli çizgilerinin olduğu şerit (resimde V ~0.27-0.33) korunur: yalnızca altındaki açık su
                        half belowSlots = 1.0 - smoothstep(_CausticMaxV - 0.025, _CausticMaxV, baseUV.y);
                        texCol.rgb = max(0, texCol.rgb - caustic * belowSlots * _CausticSoften * waterMask * half3(1.0, 0.85, 0.35));
                    }
                }

                // 5. İnteraktif Dalga Köpüğü & Parıltı
                // Kullanıcı isteği: Aşağı doğru inen hafif parlaklık şeridi kaldırıldı (temiz hypercasual lagün).
                if (waterMask > 0.01 && rippleGlow > 0.001)
                {
                    texCol.rgb += half3(0.9, 0.98, 1.0) * (rippleGlow * waterMask);
                }

                return texCol * input.color;
            }
            ENDHLSL
        }
    }
}
