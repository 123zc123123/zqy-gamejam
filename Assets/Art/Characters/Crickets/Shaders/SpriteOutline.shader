Shader "DouQuqu/SpriteOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (0.22, 0.92, 0.82, 1)
        _OutlineWidth ("Outline Width (px)", Range(0, 64)) = 20
        _OutlineSoftness ("Outline Softness (px)", Range(0, 16)) = 6
        _OutlineAlphaCutoff ("Alpha Cutoff", Range(0.01, 0.99)) = 0.12
        _PixelsPerUnit ("Pixels Per Unit", Float) = 100
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
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ PIXELSNAP_ON
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _OutlineColor;
            float _OutlineWidth;
            float _OutlineSoftness;
            float _OutlineAlphaCutoff;
            float _PixelsPerUnit;

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
                float padPx = _OutlineWidth + _OutlineSoftness + 1.5;
                float padUnit = padPx / max(_PixelsPerUnit, 1.0);
                float2 padUV = padPx * _MainTex_TexelSize.xy;

                // 身体图左/右/底几乎贴边，必须把四边形撑出贴图，描边才画得下。
                float4 vertex = IN.vertex;
                vertex.xy += sign(vertex.xy) * padUnit;
                OUT.vertex = UnityObjectToClipPos(vertex);
                OUT.texcoord = IN.texcoord * (1.0 + 2.0 * padUV) - padUV;
                OUT.color = IN.color * _Color;
#ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
#endif
                return OUT;
            }

            float SampleAlpha(float2 uv)
            {
                // Clamp 会把越界 UV 吸回不透明边缘，描边会贴着包围盒切成直角。
                float inside = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);
                return inside * tex2D(_MainTex, uv).a;
            }

            // 等距圆核：用欧氏距离膨胀，避免 8 邻域把圆盘描成八边形。
            float MinOpaqueDistance(float2 uv, float width)
            {
                float minDist = width + 2.0;
                const int dirs = 24;
                const int rings = 10;
                [loop]
                for (int i = 0; i < dirs; i++)
                {
                    float ang = 6.2831853 * i / dirs;
                    float2 dir = float2(cos(ang), sin(ang));
                    [loop]
                    for (int r = 1; r <= rings; r++)
                    {
                        float dist = width * r / rings;
                        float2 offset = dir * _MainTex_TexelSize.xy * dist;
                        float hit = step(_OutlineAlphaCutoff, SampleAlpha(uv + offset));
                        minDist = min(minDist, lerp(width + 2.0, dist, hit));
                    }
                }
                return minDist;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                fixed4 sprite = tex2D(_MainTex, uv) * IN.color;
                float inRect = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);
                sprite.a *= inRect;

                float cutoff = _OutlineAlphaCutoff;
                float spriteMask = saturate((sprite.a - cutoff) / max(0.0001, 1.0 - cutoff));

                float outlineMask = 0;
                if (_OutlineWidth > 0.001)
                {
                    float dist = MinOpaqueDistance(uv, _OutlineWidth);
                    float soft = max(1.5, _OutlineSoftness);
                    outlineMask = 1.0 - smoothstep(_OutlineWidth - soft, _OutlineWidth, dist);
                }
                outlineMask *= 1.0 - spriteMask;

                sprite.rgb *= sprite.a;

                fixed4 outline = _OutlineColor;
                outline.a *= outlineMask * IN.color.a;
                outline.rgb *= outline.a;

                return sprite + outline;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
