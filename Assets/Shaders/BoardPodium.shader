Shader "PixelGame/BoardPodium"
{
    Properties
    {
        [PerRendererData] _MainTex ("Mask Doku (RGB: Bölgeler, A: Alfa)", 2D) = "white" {}
        [Header(Canli Renk Ayarlari)]
        _OuterColor ("Dış Podyum Rengi (Outer)", Color) = (0.706, 0.761, 0.804, 1.0)
        _RimColor ("Çerçeve Bordür Rengi (Rim)", Color) = (0.686, 0.745, 0.792, 1.0)
        _GrooveColor ("Oluk Çizgisi Rengi (Groove)", Color) = (0.412, 0.463, 0.510, 1.0)
        _CanvasColor ("İç Tuval Rengi (Canvas)", Color) = (0.898, 0.925, 0.949, 1.0)
        _ShadowColor ("Dış Sahte Gölge Rengi (Shadow)", Color) = (0.08, 0.11, 0.15, 1.0)
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

            sampler2D _MainTex;
            fixed4 _OuterColor;
            fixed4 _RimColor;
            fixed4 _GrooveColor;
            fixed4 _CanvasColor;
            fixed4 _ShadowColor;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 mask = tex2D(_MainTex, IN.texcoord);
                
                // Bölge hesaplama (R: Dış Zemin, G: Bordür, B: Tuval, Geriye Kalan Solid: Oluk)
                float isSolid = saturate(mask.r + mask.g + mask.b);
                float isGroove = saturate(1.0 - isSolid) * (mask.a > 0.9 ? 1.0 : 0.0);

                fixed3 boardRgb = _OuterColor.rgb * mask.r
                                + _RimColor.rgb * mask.g
                                + _CanvasColor.rgb * mask.b
                                + _GrooveColor.rgb * isGroove;

                fixed4 finalCol;
                if (mask.a > 0.95)
                {
                    finalCol = fixed4(boardRgb, 1.0);
                }
                else
                {
                    // Dış yumuşak podyum gölgesi
                    finalCol = fixed4(_ShadowColor.rgb, mask.a);
                }

                finalCol.rgb *= IN.color.rgb;
                finalCol.a *= IN.color.a;

                return finalCol;
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}
