Shader "PixelGame/WaterRippleRing"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 0.75)
        _InnerRadius ("Inner Radius (0-1)", Range(0.0, 0.95)) = 0.55
        _OuterRadius ("Outer Radius (0-1)", Range(0.1, 1.0)) = 0.92
        _EdgeSmoothness ("Edge Smoothness", Range(0.01, 0.25)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent+50"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

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

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _InnerRadius;
                float _OuterRadius;
                float _EdgeSmoothness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // [0, 1] UV'yi merkez [0,0] olacak şekilde [-1, 1]'e dönüştür
                float2 centerUV = (input.uv - 0.5) * 2.0;
                float dist = length(centerUV);

                // Dairesel halka maskesi (Inner ve Outer yumuşak geçiş)
                float innerMask = smoothstep(_InnerRadius - _EdgeSmoothness, _InnerRadius, dist);
                float outerMask = 1.0 - smoothstep(_OuterRadius - _EdgeSmoothness, _OuterRadius, dist);
                float ring = innerMask * outerMask;

                half4 finalColor = _BaseColor;
                finalColor.a *= ring;

                return finalColor;
            }
            ENDHLSL
        }
    }
}
