// IconCap draws a captured renderer with one copy of this shader per material
// (IconCapRenderer.cs). Pass 0 is the alpha pass: white where the renderer covers.
// Pass 1 is Flat lighting: the base colour, unlit. ICONCAP_CLIP alpha-tests with the
// material's own cutoff; it is set only for a material with alpha clipping.
Shader "Hidden/RiryTools/IconCap/Draw"
{
    Properties
    {
        _MainTex ("Base", 2D) = "white" {}
        _Color ("Colour", Color) = (1, 1, 1, 1)
        _Cutoff ("Cutoff", Float) = 0.5
        _Cull ("Cull", Float) = 2
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_ST;
    float4 _Color;
    float _Cutoff;

    struct appdata
    {
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
    };

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
    };

    v2f vert (appdata v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = TRANSFORM_TEX(v.uv, _MainTex);
        return o;
    }

    float4 Base (float2 uv)
    {
        float4 c = tex2D(_MainTex, uv) * _Color;
        #ifdef ICONCAP_CLIP
        clip(c.a - _Cutoff);
        #endif
        return c;
    }
    ENDCG

    SubShader
    {
        Tags { "Queue" = "Geometry" }

        // 0 Alpha pass.
        Pass
        {
            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ ICONCAP_CLIP
            float4 frag (v2f i) : SV_Target
            {
                Base(i.uv);
                return float4(1, 1, 1, 1);
            }
            ENDCG
        }

        // 1 Flat: the base colour with no light calculation.
        Pass
        {
            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ ICONCAP_CLIP
            float4 frag (v2f i) : SV_Target
            {
                return float4(Base(i.uv).rgb, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}

// ============================================================================
// GLOSSARY: IconCap draw shader
// ============================================================================
//
// Pass 0        Alpha pass: white where the renderer covers.
// Pass 1        Flat: base colour (_MainTex * _Color), unlit.
// ICONCAP_CLIP  Alpha test with _Cutoff; on only for a material with alpha clipping while Alpha cutout is on.
// _Cull         The material's own cull mode.
