Shader "PixelGame/FoamFrameSlot"
{
    // Dalgalardan oluşan kare slot çerçevesi:
    //  - köpük dokusu (_MainTex) gürültüyle hafifçe çalkalanır (kenarlar kıpırdar),
    //  - köpük kabarcıkları sürekli belirip kaybolur (zayıf köpük aşınır, güçlü çekirdek kalır),
    //  - çerçeveden dışa doğru ince dalgacıklar yayılıp söner,
    //  - içi kenarı net, düz ve hafif bir renkle doldurulur (boş slot belli olsun; kenar köpüğün altında kalır),
    //  - üstüne prosedürel kabarık köpük eklenir: düzensiz kenarlı kalın bir çekirdek bant + kenarlarında
    //    kıpırdayan, şişip sönen baloncuklar (Voronoi) + altında hafif koyu bir gölge (köpük kabarık dursun).
    // Şekil UV uzayında superellipse (|x|^n + |y|^n)^(1/n) ile tanımlı: dokudaki yuvarlak köşeli kareyle aynı.
    Properties
    {
        [MainTexture] _MainTex ("Foam Frame Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Foam Tint", Color) = (1, 1, 1, 1)
        _ShapePower ("Shape Power (2 = circle, 5 = rounded square)", Float) = 5

        [Header(Churn)]
        _WobbleAmount ("Edge Wobble (uv)", Range(0, 0.02)) = 0.010
        _WobbleScale ("Wobble Scale", Float) = 9
        _WobbleSpeed ("Wobble Speed", Float) = 1.8
        _ChurnAmount ("Bubble Churn (0-1)", Range(0, 0.9)) = 0.55
        _ChurnScale ("Bubble Scale", Float) = 38
        _ChurnSpeed ("Bubble Speed", Float) = 1.8

        [Header(Outward Ripples)]
        _RippleColor ("Ripple Color (alpha = strength)", Color) = (1, 1, 1, 0.45)
        _RippleStart ("Ripple Start (shape radius)", Range(0.5, 1.0)) = 0.80
        _RippleTravel ("Ripple Travel", Range(0, 0.3)) = 0.08
        _RippleWidth ("Ripple Width", Range(0.002, 0.05)) = 0.016
        _RippleSpeed ("Ripple Speed (waves/sec)", Float) = 0.45

        [Header(Puffy Foam)]
        _PuffColor ("Puffy Foam Color", Color) = (1, 1, 1, 1)
        _PuffCenter ("Band Center (shape radius)", Range(0.4, 0.95)) = 0.67
        _PuffCoreWidth ("Core Half Width", Range(0, 0.1)) = 0.008
        _PuffBubbleWidth ("Loose Bubble Spread", Range(0, 0.2)) = 0.12
        _PuffEdgeNoise ("Band Edge Wobble", Range(0, 0.08)) = 0.015
        _BubbleScale ("Puff Count Around Frame", Float) = 40
        _BubbleSize ("Puff Size", Range(0.05, 0.6)) = 0.34
        _BubbleSpeed ("Puff Flow Speed", Float) = 1.0
        _PuffLifeSpeed ("Puff Boil Speed (lives/sec)", Float) = 0.9
        _PuffSpill ("Puff Spill Distance", Range(0, 0.08)) = 0.03
        _PuffRowGap ("Inner/Outer Row Gap", Range(0, 0.08)) = 0.022
        _ScreenAspect ("Quad Screen Aspect (h/w)", Float) = 1.17
        _PuffShadowColor ("Under-foam Shadow", Color) = (0.0, 0.28, 0.52, 0.35)
        _PuffShadowOffset ("Shadow Offset (uv, down)", Range(0, 0.03)) = 0.012

        [Header(Inner Fill)]
        _FillColor ("Inner Fill (alpha = strength)", Color) = (0.80, 0.96, 1.0, 0.14)
        _FillRadius ("Inner Fill Edge (shape radius)", Range(0.3, 0.8)) = 0.60
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
            Name "FoamFrameSlot"

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
                float _ShapePower;
                float _WobbleAmount;
                float _WobbleScale;
                float _WobbleSpeed;
                float _ChurnAmount;
                float _ChurnScale;
                float _ChurnSpeed;
                half4 _RippleColor;
                float _RippleStart;
                float _RippleTravel;
                float _RippleWidth;
                float _RippleSpeed;
                half4 _FillColor;
                float _FillRadius;
                half4 _PuffColor;
                float _PuffCenter;
                float _PuffCoreWidth;
                float _PuffBubbleWidth;
                float _PuffEdgeNoise;
                float _BubbleScale;
                float _BubbleSize;
                float _BubbleSpeed;
                float _PuffLifeSpeed;
                float _PuffSpill;
                float _PuffRowGap;
                float _ScreenAspect;
                half4 _PuffShadowColor;
                float _PuffShadowOffset;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash(i);
                float b = Hash(i + float2(1, 0));
                float c = Hash(i + float2(0, 1));
                float d = Hash(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float2 Hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            // Hareketli Voronoi: en yakın hücre noktasına uzaklık (x) ve o hücrenin rastgele kimliği (y)
            float2 Voronoi(float2 x, float t)
            {
                float2 n = floor(x);
                float2 f = frac(x);
                float md = 8.0;
                float id = 0.0;
                [unroll]
                for (int j = -1; j <= 1; j++)
                {
                    [unroll]
                    for (int i = -1; i <= 1; i++)
                    {
                        float2 g = float2(i, j);
                        float2 o = Hash2(n + g);
                        o = 0.5 + 0.42 * sin(t + 6.2831853 * o);
                        float2 r = g + o - f;
                        float d = dot(r, r);
                        if (d < md) { md = d; id = Hash(n + g); }
                    }
                }
                return float2(sqrt(md), id);
            }

            // Superellipse "yarıçapı": dokudaki yuvarlak köşeli kare ile aynı metrik (0 merkez, 1 kenar)
            float ShapeRadius(float2 uv)
            {
                float2 q = abs(uv - 0.5) * 2.0;
                return pow(pow(q.x, _ShapePower) + pow(q.y, _ShapePower), 1.0 / _ShapePower);
            }

            half4 Over(half4 a, half4 b)
            {
                half outA = a.a + b.a * (1.0h - a.a);
                half3 rgb = (a.rgb * a.a + b.rgb * b.a * (1.0h - a.a)) / max(outA, 1e-4h);
                return half4(rgb, outA);
            }

            // Superellipse üzerinde, merkezden "ang" açısıyla çıkan ışının şekil yarıçapı c'deki noktası (UV)
            float2 ShapePoint(float ang, float c)
            {
                float2 d = float2(cos(ang), sin(ang));
                float k = pow(pow(abs(d.x), _ShapePower) + pow(abs(d.y), _ShapePower), 1.0 / _ShapePower);
                return 0.5 + d * (c / max(k, 1e-4)) * 0.5;
            }

            // Bir sıra kabarcık: çerçeve boyunca dizili, her biri kendi yaşam döngüsünde doğar, büyür,
            // dışa/içe kayar ve küçülüp patlar; farklı fazlarda oldukları için çerçeve sürekli kaynar.
            float PuffRow(float2 uv, float t, float ang, float count, float rowCenter, float rowSeed, float aaC)
            {
                float drift = t * _BubbleSpeed * 0.35 * (rowSeed > 0.5 ? 1.0 : -0.7); // sıralar zıt yönlerde akar
                float idx = floor((ang / TWO_PI) * count - drift + 0.5);
                float result = 0.0;
                [unroll]
                for (int k = -2; k <= 2; k++)
                {
                    float ci = idx + k;
                    float key = fmod(ci + 1000.0, count);
                    float h = Hash(float2(key, 3.7 + rowSeed * 11.0));
                    float h2 = Hash(float2(key, 9.1 + rowSeed * 5.0));
                    float h3 = Hash(float2(key, 1.3 + rowSeed * 7.0));

                    // Yaşam döngüsü: 0 -> doğar, 0.5 -> en büyük, 1 -> patlar
                    float life = frac(t * _PuffLifeSpeed * lerp(0.7, 1.3, h3) + h * 7.0);
                    float grow = pow(sin(life * PI), 0.55);

                    float ca = (ci + drift + (h2 - 0.5) * 0.6) / count * TWO_PI;
                    // Yaşlandıkça banttan dışa ya da içe taşar
                    float spill = (h2 - 0.5) * 2.0 * _PuffEdgeNoise + (h3 > 0.5 ? 1.0 : -1.0) * life * _PuffSpill;
                    float2 c = ShapePoint(ca, rowCenter + spill);
                    float rad = _BubbleSize * 0.1 * lerp(0.45, 1.3, h * h) * grow;

                    float2 dv = uv - c;
                    dv.y *= _ScreenAspect; // ekranda yuvarlak dursun
                    float d = length(dv);
                    result = max(result, 1.0 - smoothstep(rad - aaC, rad + aaC, d));
                }
                return result;
            }

            // Kabarık köpük maskesi: iç ve dış iki sıra kaynayan kabarcık + ince bağlayıcı çekirdek +
            // bandın çevresinde belirip kaybolan küçük kopuk baloncuklar
            float PuffyFoam(float2 uv, float t)
            {
                float2 p = uv - 0.5;
                float ang = atan2(p.y, p.x);
                float n = max(_BubbleScale, 4.0);
                // Kenar yumuşatma piksel boyundan (fwidth(d) yuvarlak geçişlerinde sıçrayıp çizgi bırakıyordu)
                float aaC = max(length(fwidth(uv)) * _ScreenAspect, 1e-4);

                float cloud = PuffRow(uv, t, ang, n, _PuffCenter - _PuffRowGap, 0.2, aaC);
                cloud = max(cloud, PuffRow(uv, t, ang, n * 0.85, _PuffCenter + _PuffRowGap, 0.8, aaC));

                float rho = ShapeRadius(uv);
                float aaR = max(fwidth(rho), 1e-4);
                float core = 1.0 - smoothstep(_PuffCoreWidth - aaR, _PuffCoreWidth + aaR, abs(rho - _PuffCenter));

                // Küçük kopuk baloncuklar: kendi yaşam döngüleriyle belirip kaybolur
                float2 cellUV = float2(uv.x, uv.y * _ScreenAspect) * 22.0;
                float2 v = Voronoi(cellUV, t * 1.5);
                float near = 1.0 - smoothstep(0.0, _PuffBubbleWidth, abs(rho - _PuffCenter));
                float small = step(0.55, v.y) * near;
                float blink = sin(frac(t * 0.9 + v.y * 5.0) * PI);
                float br = 0.17 * lerp(0.6, 1.0, frac(v.y * 7.0)) * small * blink;
                float bubble = 1.0 - smoothstep(br - 0.02, br + 0.02, v.x);

                return saturate(max(max(cloud, core), bubble));
            }

            half4 frag(Varyings input) : SV_Target
            {
                float t = _Time.y;
                float2 uv = input.uv;
                float rho = ShapeRadius(uv);
                float aa = max(fwidth(rho), 1e-4);

                // 1. İç dolgu: net kenarlı, düz (bulanık geçiş yok); kenarı köpük bandının altında kalır
                half4 col = half4(_FillColor.rgb, _FillColor.a * (1.0 - smoothstep(_FillRadius - aa, _FillRadius + aa, rho)));

                // 2. Dışa yayılan dalgacıklar (iki dalga, yarım faz arayla)
                [unroll]
                for (int i = 0; i < 2; i++)
                {
                    float ph = frac(t * _RippleSpeed + i * 0.5);
                    float rr = _RippleStart + ph * _RippleTravel;
                    // Dalgacık çizgisi düz bir çerçeve gibi durmasın: yarıçapı ve kalınlığı çevre boyunca dalgalansın
                    float wav = ValueNoise(uv * 11.0 + float2(i * 3.7, t * 0.4)) - 0.5;
                    float width = _RippleWidth * (0.6 + 0.8 * ValueNoise(uv * 23.0 + float2(t * 0.5, i * 5.3)));
                    float ring = 1.0 - smoothstep(width * 0.5, width * 0.5 + aa, abs(rho + wav * 0.03 - rr));
                    // Kesik kesik görünsün: çevre boyunca gürültüyle kır
                    float brk = smoothstep(0.45, 0.7, ValueNoise(uv * 9.0 + float2(i * 7.1, t * 0.6)));
                    float fade = sin(ph * 3.14159265); // doğar, büyür, söner
                    col = Over(half4(_RippleColor.rgb, _RippleColor.a * ring * brk * fade), col);
                }

                // 3. Köpük: kenar çalkantısı + kabarcık aşınması
                float2 wob = float2(
                    ValueNoise(uv * _WobbleScale + float2(t * _WobbleSpeed, 0.0)),
                    ValueNoise(uv * _WobbleScale + float2(17.3, -t * _WobbleSpeed))) - 0.5;
                float2 foamUV = uv + wob * _WobbleAmount * 2.0;
                half4 foam = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, TRANSFORM_TEX(foamUV, _MainTex)) * _BaseColor;

                float bubbles = ValueNoise(uv * _ChurnScale + float2(t * _ChurnSpeed, t * _ChurnSpeed * 0.7));
                bubbles = bubbles * 0.65 + 0.35 * ValueNoise(uv * _ChurnScale * 2.3 - float2(t * _ChurnSpeed * 1.3, 0.0));
                // Zayıf (ince) köpük kabarcık deseniyle aşınır, yoğun köpük çekirdeği hep kalır
                float erode = bubbles * _ChurnAmount;
                foam.a = saturate((foam.a - erode) / max(1.0 - erode, 1e-3));
                col = Over(foam, col);

                // 4. Kabarık köpük: önce altına gölge, sonra beyaz köpük
                float puffShadow = PuffyFoam(uv + float2(0.0, _PuffShadowOffset), t);
                col = Over(half4(_PuffShadowColor.rgb, _PuffShadowColor.a * puffShadow), col);
                float puff = PuffyFoam(uv, t);
                col = Over(half4(_PuffColor.rgb, _PuffColor.a * puff), col);

                return col;
            }
            ENDHLSL
        }
    }
}
