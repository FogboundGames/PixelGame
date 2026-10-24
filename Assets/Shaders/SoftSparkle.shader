Shader "PixelGame/SoftSparkle"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _BaseMap ("Particle Texture", 2D) = "white" {}
        _CoreGlow ("Core Glow Strength", Range(0.5, 3.0)) = 1.4
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent+100"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

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

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _CoreGlow;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Yumuşak, zarif prosedürel parıltı yıldızı ve halesi:
                // 1) Merkezdeki yumuşak dairesel ışıma
                float2 uvCentered = input.uv - 0.5;
                float dist = length(uvCentered);
                float radialCore = saturate(1.0 - dist * 2.2);
                radialCore = radialCore * radialCore;

                // 2) İnce, zarif 4-noktalı yıldız ışınları
                float2 d = abs(uvCentered);
                float rayX = saturate(1.0 - d.y * 12.0) * saturate(1.0 - d.x * 2.2);
                float rayY = saturate(1.0 - d.x * 12.0) * saturate(1.0 - d.y * 2.2);
                float star = pow(rayX + rayY, 2.0) * 1.8 + radialCore * 0.9;

                // 3) Çapraz minik ışınlar (8-noktalı zarif ışıltı dokunuşu)
                float2 diag = abs(float2(uvCentered.x + uvCentered.y, uvCentered.x - uvCentered.y) * 0.7071);
                float rayD1 = saturate(1.0 - diag.y * 14.0) * saturate(1.0 - diag.x * 3.5);
                float rayD2 = saturate(1.0 - diag.x * 14.0) * saturate(1.0 - diag.y * 3.5);
                star += (rayD1 + rayD2) * 0.40;

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                
                half4 finalColor = _BaseColor * input.color * tex;
                finalColor.rgb *= star * _CoreGlow;
                finalColor.a = saturate(star * input.color.a);

                return finalColor;
            }
            ENDHLSL
        }
    }
}
