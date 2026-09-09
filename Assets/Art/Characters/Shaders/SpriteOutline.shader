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
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    fixed4 _Color;
    fixed4 _OutlineColor;
    float _OutlineWidth;
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

    v2f SpriteVert(appdata_t IN)
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

    v2f OutlineVert(appdata_t IN, float2 dir)
    {
        v2f OUT;
        float padUnit = max(0.0, _OutlineWidth) / max(_PixelsPerUnit, 1.0);
        float4 vertex = IN.vertex;
        vertex.xy += dir * padUnit;
        OUT.vertex = UnityObjectToClipPos(vertex);
        OUT.texcoord = IN.texcoord;
        OUT.color = IN.color;
        return OUT;
    }

    fixed4 SpriteFrag(v2f IN)
    {
        fixed4 sprite = tex2D(_MainTex, IN.texcoord) * IN.color;
        clip(sprite.a - _OutlineAlphaCutoff);
        sprite.rgb *= sprite.a;
        return sprite;
    }

    // 平移后的不透明像素 − 原剪影 stencil = 贴着 alpha 的一圈描边。
    // 必须 clip 到贴图 alpha，否则会把网格里的透明三角填成黑块。
    fixed4 OutlineFrag(v2f IN)
    {
        if (_OutlineWidth < 0.001) discard;
        fixed4 sprite = tex2D(_MainTex, IN.texcoord);
        clip(sprite.a - _OutlineAlphaCutoff);
        fixed4 outline = _OutlineColor;
        outline.a *= IN.color.a;
        outline.rgb *= outline.a;
        return outline;
    }
    ENDCG

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
            v2f vert(appdata_t IN) { return SpriteVert(IN); }
            fixed4 frag(v2f IN) : SV_Target { return SpriteFrag(IN); }
            ENDCG
        }

        // 八向平移网格，按贴图 alpha 裁切。不改 UV，避免图集串层，也不撕 SpriteSkin。
        Pass
        {
            Name "OutlineE"
            Stencil { Ref 128 ReadMask 128 WriteMask 128 Comp NotEqual Pass Keep }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            v2f vert(appdata_t IN) { return OutlineVert(IN, float2(1, 0)); }
            fixed4 frag(v2f IN) : SV_Target { return OutlineFrag(IN); }
            ENDCG
        }
        Pass
        {
            Name "OutlineW"
            Stencil { Ref 128 ReadMask 128 WriteMask 128 Comp NotEqual Pass Keep }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            v2f vert(appdata_t IN) { return OutlineVert(IN, float2(-1, 0)); }
            fixed4 frag(v2f IN) : SV_Target { return OutlineFrag(IN); }
            ENDCG
        }
        Pass
        {
            Name "OutlineN"
            Stencil { Ref 128 ReadMask 128 WriteMask 128 Comp NotEqual Pass Keep }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            v2f vert(appdata_t IN) { return OutlineVert(IN, float2(0, 1)); }
            fixed4 frag(v2f IN) : SV_Target { return OutlineFrag(IN); }
            ENDCG
        }
        Pass
        {
            Name "OutlineS"
            Stencil { Ref 128 ReadMask 128 WriteMask 128 Comp NotEqual Pass Keep }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            v2f vert(appdata_t IN) { return OutlineVert(IN, float2(0, -1)); }
            fixed4 frag(v2f IN) : SV_Target { return OutlineFrag(IN); }
            ENDCG
        }
        Pass
        {
            Name "OutlineNE"
            Stencil { Ref 128 ReadMask 128 WriteMask 128 Comp NotEqual Pass Keep }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            v2f vert(appdata_t IN) { return OutlineVert(IN, float2(0.7071, 0.7071)); }
            fixed4 frag(v2f IN) : SV_Target { return OutlineFrag(IN); }
            ENDCG
        }
        Pass
        {
            Name "OutlineNW"
            Stencil { Ref 128 ReadMask 128 WriteMask 128 Comp NotEqual Pass Keep }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            v2f vert(appdata_t IN) { return OutlineVert(IN, float2(-0.7071, 0.7071)); }
            fixed4 frag(v2f IN) : SV_Target { return OutlineFrag(IN); }
            ENDCG
        }
        Pass
        {
            Name "OutlineSE"
            Stencil { Ref 128 ReadMask 128 WriteMask 128 Comp NotEqual Pass Keep }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            v2f vert(appdata_t IN) { return OutlineVert(IN, float2(0.7071, -0.7071)); }
            fixed4 frag(v2f IN) : SV_Target { return OutlineFrag(IN); }
            ENDCG
        }
        Pass
        {
            Name "OutlineSW"
            Stencil { Ref 128 ReadMask 128 WriteMask 128 Comp NotEqual Pass Keep }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            v2f vert(appdata_t IN) { return OutlineVert(IN, float2(-0.7071, -0.7071)); }
            fixed4 frag(v2f IN) : SV_Target { return OutlineFrag(IN); }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
