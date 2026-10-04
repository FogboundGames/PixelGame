Shader "PixelGame/WhirlpoolSlot"
{
    // Köpük halkası slotunu cartoon bir girdaba çevirir:
    //  - halkanın içinde açık/koyu iki tonlu su bantları logaritmik spiral şeklinde merkeze kıvrılır;
    //    desen döndükçe içe doğru akıyormuş gibi görünür,
    //  - her bandın ön kenarında dışta kalın, merkeze doğru incelen net bir köpük çizgisi vardır,
    //  - merkezde koyu, yumuşak kenarlı bir çukur bulunur,
    //  - köpük halkası dokusu (_MainTex) merkez etrafında yavaşça döner.
    // Hepsi UV uzayında (merkez 0.5) hesaplanır; slot mesh'i elips ölçeklense de girdap onunla birlikte uzar.
    Properties
    {
        [MainTexture] _MainTex ("Foam Ring Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Ring Tint", Color) = (1, 1, 1, 1)
        _RingSpin ("Ring Spin (deg/sec)", Float) = 14

        [Header(Spiral)]
        _SpiralArms ("Arm Count", Float) = 3
        _SpiralTwist ("Twist (log spiral tightness)", Float) = 2.2
        _SpiralSpeed ("Flow Speed (rad/sec)", Float) = 2.2
        _SpiralOuter ("Outer Radius (uv)", Range(0.1, 0.5)) = 0.29
        _BandLight ("Light Band Color", Color) = (0.72, 0.94, 1.0, 0.30)
        _BandDark ("Dark Band Color", Color) = (0.04, 0.38, 0.62, 0.30)

        [Header(Foam Lines)]
        _FoamColor ("Foam Line Color", Color) = (1, 1, 1, 0.95)
        _FoamWidthRim ("Foam Width at Rim (0-1 of arm)", Range(0, 0.5)) = 0.16
        _FoamWidthCenter ("Foam Width at Center (0-1 of arm)", Range(0, 0.5)) = 0.03

        [Header(Center Hole)]
        _DepthColor ("Hole Color (alpha = strength)", Color) = (0.02, 0.20, 0.38, 0.85)
        _DepthRadius ("Hole Radius (uv)", Range(0.0, 0.2)) = 0.045
        _DepthSoftness ("Hole Softness (uv)", Range(0.005, 0.2)) = 0.07
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
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "WhirlpoolSlot"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _BaseColor;
                float _RingSpin;
                float _SpiralArms;
                float _SpiralTwist;
                float _SpiralSpeed;
                float _SpiralOuter;
                half4 _BandLight;
                half4 _BandDark;
                half4 _FoamColor;
                float _FoamWidthRim;
                float _FoamWidthCenter;
                half4 _DepthColor;
                float _DepthRadius;
                float _DepthSoftness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            // "a" katmanını "b"nin üstüne koyar
            half4 Over(half4 a, half4 b)
            {
                half outA = a.a + b.a * (1.0h - a.a);
                half3 rgb = (a.rgb * a.a + b.rgb * b.a * (1.0h - a.a)) / max(outA, 1e-4h);
                return half4(rgb, outA);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 p = input.uv - 0.5;
                float r = max(length(p), 1e-4);
                float ang = atan2(p.y, p.x);
                float t = _Time.y;
                float rn = saturate(r / _SpiralOuter); // 0 merkez, 1 dış kenar

                // Logaritmik spiral: merkeze yaklaştıkça kollar sıkılaşır; zamanla kayan faz içe akış hissi verir
                float phase = ang * _SpiralArms + log(max(rn, 0.02)) * _SpiralTwist * _SpiralArms + t * _SpiralSpeed;
                float f = frac(phase / 6.2831853);

                // Kenar yumuşatma: fazın ekran türevi (atan2 dikişinde sıçramasın diye analitik)
                float pixel = length(fwidth(p));
                float aa = pixel * sqrt(_SpiralArms * _SpiralArms + (_SpiralTwist * _SpiralArms) * (_SpiralTwist * _SpiralArms)) / (r * 6.2831853);
                aa = max(aa, 1e-3);

                // Girdap alanı: dış kenarda halkaya karışır, merkezde çukura kaybolur
                float region = (1.0 - smoothstep(0.82, 1.0, rn)) * smoothstep(0.0, 0.25, rn);

                // 1. İki tonlu su bantları (net kenar)
                float lightMask = smoothstep(0.5 - aa, 0.5 + aa, f) * (1.0 - smoothstep(1.0 - aa, 1.0, f));
                half4 band = lerp(_BandDark, _BandLight, lightMask);
                half4 col = half4(band.rgb, band.a * region);

                // 2. Bandın ön kenarındaki köpük çizgisi: dışta kalın, merkeze doğru incelir
                float foamW = lerp(_FoamWidthCenter, _FoamWidthRim, rn);
                float foam = smoothstep(0.5 - aa, 0.5 + aa, f) * (1.0 - smoothstep(0.5 + foamW - aa, 0.5 + foamW + aa, f));
                col = Over(half4(_FoamColor.rgb, _FoamColor.a * foam * region), col);

                // 3. Merkez çukuru
                float hole = 1.0 - smoothstep(_DepthRadius, _DepthRadius + _DepthSoftness, r);
                col = Over(half4(_DepthColor.rgb, _DepthColor.a * hole), col);

                // 4. Dönen köpük halkası
                float s, c;
                sincos(radians(_RingSpin) * t, s, c);
                float2 rp = float2(p.x * c - p.y * s, p.x * s + p.y * c) + 0.5;
                half4 ring = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, TRANSFORM_TEX(rp, _MainTex)) * _BaseColor;
                col = Over(ring, col);

                return col;
            }
            ENDHLSL
        }
    }
}
