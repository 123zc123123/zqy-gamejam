Shader "DouQuqu/SpriteOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth ("Outline Width (px)", Range(0, 64)) = 16
        _OutlineSoftness ("Outline Softness (px)", Range(0, 16)) = 4
        _OutlineAlphaCutoff ("Alpha Cutoff", Range(0.01, 0.99)) = 0.12
        _PixelsPerUnit ("Pixels Per Unit", Float) = 100
        _OutlineCenter ("Sprite Mesh Center", Vector) = (0, 0, 0, 0)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
            "DisableBatching" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Off
        Blend One OneMinusSrcAlpha

        // 先画本体并写入 stencil。后画的部件会盖住先画部件的内描边，合成一圈外轮廓。
        Pass
        {
            Name "Sprite"
            Stencil
            {
                Ref 128
                ReadMask 128
                WriteMask 128
                Comp Always
                Pass Replace
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ PIXELSNAP_ON
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            float _OutlineAlphaCutoff;

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
#ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
#endif
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 sprite = tex2D(_MainTex, IN.texcoord) * IN.color;
                clip(sprite.a - _OutlineAlphaCutoff);
                sprite.rgb *= sprite.a;
                return sprite;
            }
            ENDCG
        }

        // 网格沿部件中心外扩，不改 UV，避免图集采样串层、也不撕 SpriteSkin。
        Pass
        {
            Name "Outline"
            Stencil
            {
                Ref 128
                ReadMask 128
                WriteMask 128
                Comp NotEqual
                Pass Keep
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            fixed4 _OutlineColor;
            float _OutlineWidth;
            float _OutlineSoftness;
            float _PixelsPerUnit;
            float4 _OutlineCenter;

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                float padPx = max(0.0, _OutlineWidth + _OutlineSoftness);
                float padUnit = padPx / max(_PixelsPerUnit, 1.0);
                float2 center = _OutlineCenter.xy;
                float2 delta = IN.vertex.xy - center;
                float dist = length(delta);
                float2 dir = dist > 1e-5 ? delta / dist : float2(0, 1);
                float4 vertex = IN.vertex;
                vertex.xy += dir * padUnit;
                OUT.vertex = UnityObjectToClipPos(vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                if (_OutlineWidth < 0.001) discard;

                fixed4 outline = _OutlineColor;
                outline.a *= IN.color.a;
                outline.rgb *= outline.a;
                return outline;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
