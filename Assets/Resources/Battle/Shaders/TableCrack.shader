Shader "DouQuqu/UI/TableCrack"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _InnerRect ("Inner Rect", Vector) = (0, 0, 1, 1)
        _OuterRect ("Outer Rect", Vector) = (0, 0, 1, 1)
        _CrackProgress ("Crack Progress", Range(0, 1)) = 0
        _Collapse ("Collapse", Range(0, 1)) = 0
        _TableSize ("Table Size", Vector) = (1840, 2740, 0, 0)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float4 _InnerRect;
            float4 _OuterRect;
            float _CrackProgress;
            float _Collapse;
            float4 _TableSize;

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            float2 Hash22(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            float Hash21(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                UNITY_UNROLL
                for (int i = 0; i < 4; i++)
                {
                    v += a * Noise(p);
                    p = p * 2.03 + float2(17.1, 9.7);
                    a *= 0.5;
                }
                return v;
            }

            void Voronoi(float2 uv, out float edge, out float id)
            {
                float2 n = floor(uv);
                float2 f = frac(uv);
                float md = 8.0;
                float2 b = 0;
                float2 mr = 0;
                UNITY_UNROLL
                for (int j = -1; j <= 1; j++)
                UNITY_UNROLL
                for (int i = -1; i <= 1; i++)
                {
                    float2 g = float2(i, j);
                    float2 o = Hash22(n + g);
                    float2 r = g + o - f;
                    float d = dot(r, r);
                    if (d < md)
                    {
                        md = d;
                        mr = r;
                        b = g;
                    }
                }
                id = Hash21(n + b);
                md = 8.0;
                UNITY_UNROLL
                for (int jj = -1; jj <= 1; jj++)
                UNITY_UNROLL
                for (int ii = -1; ii <= 1; ii++)
                {
                    float2 g = b + float2(ii, jj);
                    float2 o = Hash22(n + g);
                    float2 r = g + o - f;
                    float2 diff = r - mr;
                    if (dot(diff, diff) > 1e-5)
                        md = min(md, dot(0.5 * (mr + r), normalize(diff)));
                }
                edge = md;
            }

            float Inward01(float2 uv)
            {
                float2 outerC = 0.5 * (_OuterRect.xy + _OuterRect.zw);
                float2 innerC = 0.5 * (_InnerRect.xy + _InnerRect.zw);
                float2 outerH = max(0.5 * (_OuterRect.zw - _OuterRect.xy), float2(1e-5, 1e-5));
                float2 innerH = max(0.5 * (_InnerRect.zw - _InnerRect.xy), float2(1e-5, 1e-5));
                float2 d = abs(uv - outerC);
                float outerU = max(d.x / outerH.x, d.y / outerH.y);
                float2 di = abs(uv - innerC);
                float innerU = max(di.x / innerH.x, di.y / innerH.y);
                float ratio = max(innerH.x / outerH.x, innerH.y / outerH.y);
                float inward = saturate((1.0 - outerU) / max(1e-4, 1.0 - ratio));
                float inRing = step(outerU, 1.002) * step(1.0, innerU);
                return inward * inRing;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float progress = saturate(_CrackProgress);
                float collapse = saturate(_Collapse);
                float inward = Inward01(uv);

                fixed4 col = (tex2D(_MainTex, uv) + _TextureSampleAdd) * IN.color;

                if (inward > 0.0001 && (progress > 0.001 || collapse > 0.001))
                {
                    float2 px = max(_TableSize.xy, float2(1, 1));
                    float2 p = uv * px / 120.0;
                    p += Fbm(p * 0.7) * 0.45;

                    float edge;
                    float id;
                    Voronoi(p, edge, id);

                    float hair = 1.0 - abs(2.0 * Fbm(uv * px / 48.0 + float2(3.1, 8.4)) - 1.0);
                    hair = smoothstep(0.62, 0.92, hair);

                    float jagged = inward + (id - 0.5) * 0.14 + (Fbm(uv * 18.0) - 0.5) * 0.1;
                    float front = progress * 1.12;
                    float reveal = saturate((front - jagged) / 0.1);
                    reveal *= smoothstep(0.0, 0.05, 1.0 - inward + 0.03);

                    float width = lerp(0.045, 0.22, collapse);
                    float crack = 1.0 - smoothstep(0.0, width, edge);
                    crack = max(crack, hair * 0.55 * (1.0 - collapse * 0.35));
                    crack *= reveal;

                    col.rgb = lerp(col.rgb, col.rgb * 0.08, crack * (1.0 - collapse * 0.5));
                    col.a *= 1.0 - saturate(crack * lerp(0.05, 1.35, collapse));

                    float cellDie = saturate((collapse - id * 0.4) / 0.6);
                    col.a *= 1.0 - cellDie;
                    col.rgb *= 1.0 - collapse * 0.35;
                }

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif
                return col;
            }
        ENDCG
        }
    }
}
