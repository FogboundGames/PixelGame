Shader "PixelGame/ShipSilhouetteShadow"
{
    // Gemi gövdesinin mesh'ini ışık yönünde su yüzeyine (_GroundY) yansıtıp düz, yarı saydam bir renkle çizer:
    // her köşe yüksekliği kadar _ShadowDir yönünde kayar, böylece kabin/çatı gibi yüksek kısımlar daha
    // uzağa düşer ve gölge geminin silüetini taşır.
    // Gövdenin üst üste binen üçgenleri yarı saydamlıkta koyu lekeler bırakacağı için stencil ile
    // her piksel yalnızca bir kez boyanır; iki geminin gölgesi çakışınca da tek kat gölge görünür.
    //
    // Yumuşak kenar: aynı gölge birkaç "halka" materyaliyle tekrar çizilir. Her halka silüeti yüzey
    // normalleri yönünde _Softness * _RingT kadar genişletir ve _RingAlpha ile daha saydam boyanır.
    // Stencil sayesinde her halka yalnızca önceki katların boyamadığı dış şeridi doldurur; sonuç
    // kenarda dışa doğru azalan bir geçiştir.
    Properties
    {
        _Color ("Shadow Color", Color) = (0.02, 0.08, 0.22, 0.40)
        _ShadowDir ("Shadow Direction (X, Z per unit height)", Vector) = (0.70, -0.15, 0, 0)
        _GroundY ("Ground Height (object space)", Float) = 0
        _Softness ("Edge Softness (object space)", Float) = 0
        _RingT ("Ring Expand (0 = core, 1 = outermost)", Range(0, 1)) = 0
        _RingAlpha ("Ring Alpha Multiplier", Range(0, 1)) = 1
        _StencilRef ("Stencil Ref", Int) = 37
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
        }

        Pass
        {
            Name "SilhouetteShadow"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            Stencil
            {
                Ref [_StencilRef]
                Comp NotEqual
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _ShadowDir;
                float _GroundY;
                float _Softness;
                float _RingT;
                float _RingAlpha;
                int _StencilRef;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 p = input.positionOS.xyz;

                // Halka genişletmesi: yan yüzleri yatay normalleri yönünde dışarı it
                float2 nxz = input.normalOS.xz;
                float nLen = length(nxz);
                if (nLen > 1e-4) p.xz += (nxz / nLen) * (_Softness * _RingT);

                float height = max(p.y - _GroundY, 0.0);
                p.xz += _ShadowDir.xy * height;
                p.y = _GroundY;
                output.positionCS = TransformObjectToHClip(p);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return half4(_Color.rgb, _Color.a * _RingAlpha);
            }
            ENDHLSL
        }
    }
}
