Shader "PixelGame/BoardPodium"
{
    // Prosedürel yuvarlak köşeli çerçeve (SDF). Sprite yalnızca kare bir quad sağlar; şekil UV uzayında çizilir.
    // Katmanlar (arkadan öne): dış yumuşak gölge → çerçeve kalınlığı (alt yan duvar) → beyaz üst yüz
    // → iç eğim (lip) → tuval + üst kenardan düşen iç gölge.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite (yalnızca quad)", 2D) = "white" {}
        [Header(Canli Renk Ayarlari)]
        _OuterColor ("Çerçeve Üst Yüz Rengi (Rim Top)", Color) = (0.965, 0.976, 0.992, 1.0)
        _RimColor ("Çerçeve Kalınlık / Kenar Rengi (Side)", Color) = (0.765, 0.824, 0.910, 1.0)
        _GrooveColor ("İç Eğim Rengi (Inner Lip)", Color) = (0.706, 0.788, 0.902, 1.0)
        _CanvasColor ("İç Tuval Rengi (Canvas)", Color) = (0.800, 0.875, 0.965, 1.0)
        _ShadowColor ("Dış Gölge Rengi (A: opaklık)", Color) = (0.30, 0.38, 0.55, 0.55)

        [Header(Geometri UV)]
        _OuterHalf ("Dış Yarı Boyut", Range(0.3, 0.5)) = 0.466
        _InnerHalf ("Tuval Yarı Boyut", Range(0.2, 0.5)) = 0.434
        _OuterRadius ("Dış Köşe Yarıçapı", Range(0, 0.25)) = 0.09
        _InnerRadius ("Tuval Köşe Yarıçapı", Range(0, 0.25)) = 0.06
        _LipWidth ("İç Eğim Genişliği", Range(0, 0.03)) = 0.007
        _Depth ("Çerçeve Kalınlığı (alt)", Range(0, 0.03)) = 0.012

        [Header(Golgeler)]
        _ShadowOffset ("Dış Gölge Kayması", Range(0, 0.04)) = 0.012
        _ShadowBlur ("Dış Gölge Yumuşaklığı", Range(0.001, 0.05)) = 0.016
        _InnerShadowOffset ("İç Gölge Kayması", Range(0, 0.05)) = 0.018
        _InnerShadowBlur ("İç Gölge Yumuşaklığı", Range(0.001, 0.08)) = 0.03
        _InnerShadowStrength ("İç Gölge Gücü", Range(0, 1)) = 0.22

        [Header(Ayri Golge Katmani)]
        [Toggle] _ShadowOnly ("Sadece Dış Gölge Çiz (çerçevenin arkasındaki gölge katmanı)", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent-50"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "False"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            fixed4 _OuterColor, _RimColor, _GrooveColor, _CanvasColor, _ShadowColor;
            float _OuterHalf, _InnerHalf, _OuterRadius, _InnerRadius, _LipWidth, _Depth;
            float _ShadowOffset, _ShadowBlur, _InnerShadowOffset, _InnerShadowBlur, _InnerShadowStrength;
            float _ShadowOnly;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color;
                return OUT;
            }

            float sdRoundBox(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            // Önceki rengin üstüne (premultiplied olmayan) "over" karışımı
            void Over(inout float3 col, inout float a, float3 c, float k)
            {
                float na = k + a * (1.0 - k);
                col = (c * k + col * a * (1.0 - k)) / max(na, 1e-5);
                a = na;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 p = IN.texcoord - 0.5;
                float2 outerB = float2(_OuterHalf, _OuterHalf);
                float2 innerB = float2(_InnerHalf, _InnerHalf);

                float dFace = sdRoundBox(p, outerB, _OuterRadius);
                float aa = max(fwidth(dFace), 1e-5);

                float3 col = _ShadowColor.rgb;
                float a = 0.0;

                // Ayrı gölge katmanı: yalnızca yumuşak, yuvarlatılmış dış gölge (kayma, katmanın kendi konumuyla verilir)
                if (_ShadowOnly > 0.5)
                {
                    float dOnly = sdRoundBox(p, outerB, _OuterRadius);
                    float k = 1.0 - smoothstep(-_ShadowBlur * 0.6, _ShadowBlur, dOnly);
                    return fixed4(_ShadowColor.rgb, _ShadowColor.a * k * k * IN.color.a);
                }

                // 1. Dış yumuşak gölge (aşağı kaymış, kalınlığı da kapsar)
                float2 shB = outerB + float2(0.0, _Depth * 0.5);
                float dSh = sdRoundBox(p - float2(0.0, -_Depth * 0.5 - _ShadowOffset), shB, _OuterRadius);
                float sh = 1.0 - smoothstep(-_ShadowBlur * 0.6, _ShadowBlur, dSh);
                a = _ShadowColor.a * sh * sh;

                // 2. Çerçeve kalınlığı: üst yüzün aşağı doğru süpürülmüş hali (yan duvar)
                float dWall = sdRoundBox(p - float2(0.0, -_Depth * 0.5), outerB + float2(0.0, _Depth * 0.5), _OuterRadius);
                float wallK = 1.0 - smoothstep(-aa, aa, dWall);
                float wallT = saturate((p.y + _OuterHalf + _Depth) / max(_Depth * 2.0, 1e-4));
                float3 wallCol = _RimColor.rgb * lerp(0.86, 1.0, wallT);
                Over(col, a, wallCol, wallK);

                // 3. Beyaz üst yüz (hafif dikey ışık + dış kenarda ince yumuşak kontür)
                float faceK = 1.0 - smoothstep(-aa, aa, dFace);
                float3 faceCol = _OuterColor.rgb * lerp(0.975, 1.015, saturate(p.y / _OuterHalf * 0.5 + 0.5));
                float edge = 1.0 - smoothstep(0.0, 0.006, -dFace);
                faceCol = lerp(faceCol, _RimColor.rgb, edge * 0.45);
                Over(col, a, faceCol, faceK);

                // 4. İç eğim (tuvale inen duvar): üst kenar gölgede, alt kenar aydınlık
                float dLip = sdRoundBox(p, innerB + _LipWidth, _InnerRadius + _LipWidth);
                float lipK = 1.0 - smoothstep(-aa, aa, dLip);
                float2 lipN = normalize(p + 1e-5);
                float topness = saturate(lipN.y * 0.5 + 0.5);
                float3 lipCol = _GrooveColor.rgb * lerp(1.08, 0.86, topness);
                Over(col, a, lipCol, lipK);

                // 5. Tuval + üst kenardan düşen iç gölge
                float dCanvas = sdRoundBox(p, innerB, _InnerRadius);
                float canvasK = 1.0 - smoothstep(-aa, aa, dCanvas);
                float dIn = sdRoundBox(p - float2(0.0, -_InnerShadowOffset), innerB, _InnerRadius);
                float inSh = saturate((dIn + _InnerShadowBlur) / _InnerShadowBlur);
                inSh = inSh * inSh * (3.0 - 2.0 * inSh);
                float3 canvasCol = _CanvasColor.rgb * (1.0 - inSh * _InnerShadowStrength);
                Over(col, a, canvasCol, canvasK);

                fixed4 finalCol = fixed4(col, a);
                finalCol.rgb *= IN.color.rgb;
                finalCol.a *= IN.color.a;
                return finalCol;
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}
