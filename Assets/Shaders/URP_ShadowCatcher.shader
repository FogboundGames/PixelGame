Shader "Custom/URP_ShadowCatcher"
{
    Properties
    {
        _ShadowColor ("Shadow Color", Color) = (0.04, 0.07, 0.14, 0.45)
    }
    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent-10" 
            "RenderPipeline"="UniversalPipeline" 
        }
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };
            
            CBUFFER_START(UnityPerMaterial)
                float4 _ShadowColor;
            CBUFFER_END
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                float3 positionWS = input.positionWS;

                // ------------------------------------------------------------------
                // KORUMA 1 — Cascade taşması (hayalet gölge kopyaları)
                // Bu yakalayıcı düzlem (Ground_ShadowCatcher) 14x24 birim; ekranın
                // tamamını kaplıyor. Cascade kürelerinin DIŞINDA kalan pikseller için
                // TransformWorldToShadowCoord geçersiz bir atlas koordinatı üretir ve
                // komşu cascade karesinden okuma yapar -> uzaktaki slot/gemi
                // gölgelerinin sol üst köşede "hayalet" kopyaları belirir.
                // Cascade dışındaki pikselleri gölgesiz sayarak bunu kapatıyoruz.
                // ------------------------------------------------------------------
                #if defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    if (ComputeCascadeIndex(positionWS) >= half(4.0))
                        return half4(_ShadowColor.rgb, half(0.0));
                #endif

                float4 shadowCoord = TransformWorldToShadowCoord(positionWS);

                // ------------------------------------------------------------------
                // KORUMA 2 — Atlas sınırları
                // Soft-shadow filtresi / bias kenarlarda atlasın dışına taşabiliyor.
                // UV veya derinlik [0,1] aralığının dışındaysa gölge yok.
                // ------------------------------------------------------------------
                if (any(shadowCoord.xy < 0.0) || any(shadowCoord.xy > 1.0) ||
                    shadowCoord.z < 0.0 || shadowCoord.z > 1.0)
                    return half4(_ShadowColor.rgb, half(0.0));

                Light mainLight = GetMainLight(shadowCoord);
                half shadowAtten = mainLight.shadowAttenuation;

                // ------------------------------------------------------------------
                // KORUMA 3 — Shadow distance sönümlemesi
                // URP'nin kendi mesafe fade'i; gölge menzilinin bittiği yerde sert
                // kesik yerine yumuşak geçiş verir (1 = tamamen sönmüş).
                // ------------------------------------------------------------------
                shadowAtten = lerp(shadowAtten, half(1.0), GetMainLightShadowFade(positionWS));

                half shadowFactor = saturate(1.0 - shadowAtten);
                half4 color = _ShadowColor;
                color.a *= shadowFactor;
                return color;
            }
            ENDHLSL
        }
    }
}
