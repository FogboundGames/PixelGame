Shader "PixelGame/ShipMysteryCover"
{
    // Gizli gemi örtüsü: gemi modelinin UV'lerinden bağımsız (palet atlas UV'leri tek noktaya düşer),
    // nesne uzayında triplanar '?' deseni ile geminin her yüzünü kumaş/örtü gibi kaplar.
    Properties
    {
        _PatternTex ("Pattern (?)", 2D) = "white" {}
        _PatternTiling ("Pattern Tiling (per world unit)", Float) = 1.3
        _TintColor ("Tint", Color) = (1, 1, 1, 1)
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.38
        _RimDarken ("Rim Darken", Range(0, 1)) = 0.25
        _ScrollSpeed ("Pattern Drift (xy)", Vector) = (0.12, 0.18, 0, 0)
        _WobbleAmount ("Wobble Amount", Range(0, 0.1)) = 0.025
        _WobbleSpeed ("Wobble Speed", Float) = 2.2
        _ShimmerStrength ("Shimmer Strength", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "Queue"="Geometry"
            "RenderType"="Opaque"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 patternPos : TEXCOORD0;
                float3 normalOS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 viewDirWS : TEXCOORD3;
            };

            TEXTURE2D(_PatternTex);
            SAMPLER(sampler_PatternTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _PatternTex_ST;
                float _PatternTiling;
                float4 _TintColor;
                float _ShadowStrength;
                float _RimDarken;
                float4 _ScrollSpeed;
                float _WobbleAmount;
                float _WobbleSpeed;
                float _ShimmerStrength;
            CBUFFER_END

            // Animasyonlu '?' deseni: yavaşça çapraz kayar, hafifçe dalgalanır (kumaş gibi)
            half4 SamplePattern(float2 uv)
            {
                float t = _Time.y;
                uv += _ScrollSpeed.xy * t;
                uv += float2(sin(uv.y * 6.2831 + t * _WobbleSpeed), cos(uv.x * 6.2831 + t * _WobbleSpeed * 0.8)) * _WobbleAmount;
                return SAMPLE_TEXTURE2D(_PatternTex, sampler_PatternTex, uv);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);

                // Nesneye yapışık (gemi hareket ederken desen kaymaz) ama dünya ölçeğinde sabit yoğunluklu desen
                float3 objScale = float3(
                    length(UNITY_MATRIX_M._m00_m10_m20),
                    length(UNITY_MATRIX_M._m01_m11_m21),
                    length(UNITY_MATRIX_M._m02_m12_m22));
                output.patternPos = input.positionOS.xyz * objScale * _PatternTiling;

                output.normalOS = input.normalOS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalOS);
                float3 w = pow(abs(n), 4.0);
                w /= max(w.x + w.y + w.z, 1e-4);

                half4 texX = SamplePattern(input.patternPos.zy);
                half4 texY = SamplePattern(input.patternPos.xz);
                half4 texZ = SamplePattern(input.patternPos.xy);
                half3 albedo = (texX.rgb * w.x + texY.rgb * w.y + texZ.rgb * w.z) * _TintColor.rgb;

                // Örtü üzerinde çapraz kayan yumuşak parıltı şeridi: "?" işaretleri sırayla ışıldar
                float band = frac((input.patternPos.x + input.patternPos.z) * 0.35 - _Time.y * 0.45);
                float shimmer = smoothstep(0.0, 0.12, band) * (1.0 - smoothstep(0.12, 0.3, band));
                float glyphMask = saturate((albedo.r - 0.3) * 1.6); // yalnızca beyaz '?' parlar
                albedo += shimmer * _ShimmerStrength * glyphMask;

                // Cartoon tarzı yumuşak ışık: üst yüzler parlak, yanlar hafif gölgeli → gemi formu okunur
                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight();
                float halfLambert = saturate(dot(normalWS, mainLight.direction)) * 0.5 + 0.5;
                float topLight = saturate(normalWS.y) * 0.25;
                float lightTerm = lerp(1.0 - _ShadowStrength, 1.0, saturate(halfLambert + topLight));

                float rim = 1.0 - saturate(dot(normalWS, normalize(input.viewDirWS)));
                float rimTerm = 1.0 - _RimDarken * pow(rim, 3.0);

                return half4(albedo * lightTerm * rimTerm, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
