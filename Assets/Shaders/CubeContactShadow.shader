// Küp altı temas gölgesi. Üst üste binen gölgeler koyulaşmasın diye normal alfa karışımı yerine
// "en koyu kazanır" (BlendOp Min) kullanılır: her gölge, zemin renginin kendi alfasıyla gölgelenmiş hâlini yazar,
// GPU her pikselde mevcut renkle karşılaştırıp koyu olanı tutar. Bu yüzden zemin (kum) renginin bilinmesi gerekir.
Shader "PixelGame/CubeContactShadow"
{
    Properties
    {
        _MainTex ("Shadow Mask (alpha)", 2D) = "white" {}
        _Color ("Shadow Color (alpha = koyuluk)", Color) = (0.04, 0.06, 0.14, 0.6)
        _GroundColor ("Ground Color (gölgenin düştüğü kum)", Color) = (0.835, 0.878, 0.929, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            ZWrite Off
            Cull Off
            BlendOp Min
            Blend One One

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _GroundColor;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * _Color.a * input.color.a;
                half3 shaded = lerp(_GroundColor.rgb, _Color.rgb, a);
                // Gölgenin olmadığı (alfa ~0) yerlerde beyaz yaz: Min ile zemin hiç değişmez
                half3 outColor = lerp(half3(1, 1, 1), shaded, saturate(a * 40.0));
                return half4(outColor, 1);
            }
            ENDHLSL
        }
    }
}
