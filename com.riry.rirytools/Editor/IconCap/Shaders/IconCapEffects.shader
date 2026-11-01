// IconCap effect stack: one pass per effect, run with Graphics.Blit on straight-alpha
// images (IconCapEffects.cs). The pass numbers are IconCapEffects' Pass constants.
Shader "Hidden/RiryTools/IconCap/Effects"
{
    Properties
    {
        _MainTex ("Image", 2D) = "white" {}
        _LayerTex ("Layer", 2D) = "white" {}
        _MaskTex ("Mask", 2D) = "white" {}
        _Cube ("Cubemap", Cube) = "" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _LayerTex;
    sampler2D _MaskTex;
    samplerCUBE _Cube;
    float4 _Tint;
    float4 _Colour;
    float4 _Corner;
    float4 _Centre;
    float4 _Offset;
    float _Zoom;
    float _Angle;
    float _FlipX;
    float _FlipY;
    float _Padding;
    float _Brightness;
    float _Contrast;
    float _Saturation;
    float _Thickness;
    float _Softness;
    float _Blur;
    float _Hue;
    float _Levels;
    float _Amount;
    float _HasLayer;
    float _Opaque;

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
    };

    v2f vert (appdata_img v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.texcoord;
        return o;
    }

    // Straight-alpha "top over bottom".
    float4 Over (float4 top, float4 bottom)
    {
        float a = top.a + bottom.a * (1 - top.a);
        float3 rgb = (top.rgb * top.a + bottom.rgb * bottom.a * (1 - top.a)) / max(a, 1e-5);
        return float4(rgb, a);
    }

    // The image at uv; transparent outside it.
    float4 Image (float2 uv)
    {
        float inside = step(0, uv.x) * step(uv.x, 1) * step(0, uv.y) * step(uv.y, 1);
        float4 c = tex2Dlod(_MainTex, float4(saturate(uv), 0, 0));
        return float4(c.rgb, c.a * inside);
    }

    float Luma (float3 c)
    {
        return dot(c, float3(0.2126, 0.7152, 0.0722));
    }

    float3 RgbToHsv (float3 c)
    {
        float4 k = float4(0, -1.0 / 3.0, 2.0 / 3.0, -1);
        float4 p = lerp(float4(c.bg, k.wz), float4(c.gb, k.xy), step(c.b, c.g));
        float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
        float d = q.x - min(q.w, q.y);
        return float3(abs(q.z + (q.w - q.y) / (6 * d + 1e-5)), d / (q.x + 1e-5), q.x);
    }

    float3 HsvToRgb (float3 c)
    {
        float3 p = abs(frac(c.xxx + float3(1, 2.0 / 3.0, 1.0 / 3.0)) * 6 - 3);
        return c.z * lerp(float3(1, 1, 1), saturate(p - 1), c.y);
    }
    ENDCG

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always
        Blend Off

        // 0 Overlay: the layer over the image.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 frag (v2f i) : SV_Target
            {
                return Over(tex2D(_LayerTex, i.uv) * _Tint, tex2D(_MainTex, i.uv));
            }
            ENDCG
        }

        // 1 Draw under: the layer behind the image.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 frag (v2f i) : SV_Target
            {
                return Over(tex2D(_MainTex, i.uv), tex2D(_LayerTex, i.uv) * _Tint);
            }
            ENDCG
        }

        // 2 Mask: cut to the layer's shape.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 frag (v2f i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv);
                return float4(c.rgb, c.a * tex2D(_LayerTex, i.uv).a * _Tint.a);
            }
            ENDCG
        }

        // 3 Inverse mask: cut to the inverse of the layer's shape.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 frag (v2f i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv);
                return float4(c.rgb, c.a * (1 - tex2D(_LayerTex, i.uv).a * _Tint.a));
            }
            ENDCG
        }

        // 4 Gray out: desaturate, darken by _Amount and tint; with a layer, keep the
        // original colours where the image differs from it.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 frag (v2f i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv);
                float3 grey = Luma(c.rgb) * (1 - _Amount) * _Tint.rgb;
                float keep = 0;
                if (_HasLayer > 0.5)
                {
                    float4 t = tex2D(_LayerTex, i.uv);
                    keep = saturate((length(c.rgb - t.rgb) + abs(c.a - t.a)) * 4);
                }
                return float4(lerp(grey, c.rgb, keep), c.a);
            }
            ENDCG
        }

        // 5 Corner badge: the layer at half size in the corner whose lower-left is _Corner.xy.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 frag (v2f i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv);
                float2 b = (i.uv - _Corner.xy) * 2;
                if (b.x < 0 || b.x > 1 || b.y < 0 || b.y > 1) return c;
                return Over(tex2D(_LayerTex, b) * _Tint, c);
            }
            ENDCG
        }

        // 6 Crop and zoom: _Zoom times larger around _Centre; the output stays square.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 frag (v2f i) : SV_Target
            {
                return Image((i.uv - 0.5) / max(_Zoom, 1e-3) + _Centre.xy);
            }
            ENDCG
        }

        // 7 Rotate and flip: _Angle degrees clockwise, then mirrored.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 frag (v2f i) : SV_Target
            {
                float2 p = i.uv - 0.5;
                if (_FlipX > 0.5) p.x = -p.x;
                if (_FlipY > 0.5) p.y = -p.y;
                float a = radians(_Angle);
                float s = sin(a);
                float co = cos(a);
                p = float2(p.x * co - p.y * s, p.x * s + p.y * co);
                return Image(p + 0.5);
            }
            ENDCG
        }

        // 8 Padding: the image shrunk by _Padding of the width on every side.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 frag (v2f i) : SV_Target
            {
                return Image((i.uv - 0.5) / max(1 - 2 * _Padding, 1e-3) + 0.5);
            }
            ENDCG
        }

        // 9 Brightness, contrast and saturation.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 frag (v2f i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv);
                float3 rgb = (c.rgb - 0.5) * _Contrast + 0.5 + _Brightness;
                rgb = lerp(Luma(rgb).xxx, rgb, _Saturation);
                return float4(saturate(rgb), c.a);
            }
            ENDCG
        }

        // 10 Outline: _Thickness pixels of _Colour around the shape; _Softness fades its edge.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            float4 frag (v2f i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv);
                float r = max(_Thickness, 1e-3);
                float inner = r * (1 - saturate(_Softness));
                float a = c.a;
                [loop] for (int ring = 1; ring <= 12; ring++)
                {
                    float d = r * ring / 12.0;
                    float w = 1 - saturate((d - inner) / max(r - inner, 1e-3));
                    [loop] for (int k = 0; k < 32; k++)
                    {
                        float angle = k * 6.2831853 / 32.0;
                        float2 o = float2(cos(angle), sin(angle)) * d * _MainTex_TexelSize.xy;
                        a = max(a, Image(i.uv + o).a * w);
                    }
                }
                return Over(c, float4(_Colour.rgb, _Colour.a * a));
            }
            ENDCG
        }

        // 11 Drop shadow: the shape in _Colour, moved by _Offset pixels (x right, y down)
        // and blurred over _Blur pixels, behind the image.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            float4 frag (v2f i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv);
                float2 at = i.uv - float2(_Offset.x, -_Offset.y) * _MainTex_TexelSize.xy;
                float sum = Image(at).a;
                float count = 1;
                [loop] for (int ring = 1; ring <= 6; ring++)
                {
                    float d = _Blur * ring / 6.0;
                    [loop] for (int k = 0; k < 16; k++)
                    {
                        float angle = k * 6.2831853 / 16.0;
                        sum += Image(at + float2(cos(angle), sin(angle)) * d * _MainTex_TexelSize.xy).a;
                        count += 1;
                    }
                }
                return Over(c, float4(_Colour.rgb, _Colour.a * sum / count));
            }
            ENDCG
        }

        // 12 Colour overlay: towards _Colour by its alpha.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 frag (v2f i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv);
                return float4(lerp(c.rgb, _Colour.rgb, _Colour.a), c.a);
            }
            ENDCG
        }

        // 13 Hue shift by _Hue degrees.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 frag (v2f i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv);
                float3 hsv = RgbToHsv(saturate(c.rgb));
                hsv.x = frac(hsv.x + _Hue / 360.0);
                return float4(HsvToRgb(hsv), c.a);
            }
            ENDCG
        }

        // 14 Invert.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 frag (v2f i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv);
                return float4(1 - saturate(c.rgb), c.a);
            }
            ENDCG
        }

        // 15 Grayscale.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 frag (v2f i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv);
                return float4(Luma(c.rgb).xxx, c.a);
            }
            ENDCG
        }

        // 16 Posterize to _Levels levels per channel.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 frag (v2f i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv);
                float steps = max(_Levels - 1, 1);
                return float4(round(saturate(c.rgb) * steps) / steps, c.a);
            }
            ENDCG
        }

        // 17 Sharpen by _Amount.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 frag (v2f i) : SV_Target
            {
                float2 t = _MainTex_TexelSize.xy;
                float4 c = tex2D(_MainTex, i.uv);
                float3 around = tex2D(_MainTex, i.uv + float2(t.x, 0)).rgb + tex2D(_MainTex, i.uv - float2(t.x, 0)).rgb +
                    tex2D(_MainTex, i.uv + float2(0, t.y)).rgb + tex2D(_MainTex, i.uv - float2(0, t.y)).rgb;
                return float4(saturate(c.rgb + (c.rgb * 4 - around) * _Amount), c.a);
            }
            ENDCG
        }

        // 18 Composite: the colour pass with the mask pass as alpha, or opaque.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 frag (v2f i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv);
                return float4(c.rgb, _Opaque > 0.5 ? 1 : tex2D(_MaskTex, i.uv).r);
            }
            ENDCG
        }

        // 19 A cubemap unwrapped to latitude/longitude, for the HDRI ambient light.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 frag (v2f i) : SV_Target
            {
                float theta = (1 - i.uv.y) * UNITY_PI;
                float phi = i.uv.x * 2 * UNITY_PI;
                float3 dir = float3(sin(theta) * cos(phi), cos(theta), sin(theta) * sin(phi));
                return float4(texCUBElod(_Cube, float4(dir, 0)).rgb, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}

// ============================================================================
// GLOSSARY: IconCap effects shader
// ============================================================================
//
// Passes 0-17   Overlay, Draw under, Mask, Inverse mask, Gray out, Corner badge, Crop and zoom, Rotate and flip,
//               Padding, Brightness/contrast/saturation, Outline, Drop shadow, Colour overlay, Hue shift, Invert,
//               Grayscale, Posterize, Sharpen (IconCapEffectType order without Custom material).
// Pass 18       Composite: colour with the alpha pass as alpha (_MaskTex), or opaque (_Opaque).
// Pass 19       Lat/long unwrap of _Cube for the HDRI ambient light.
// Over / Image  Straight-alpha "over"; the image, transparent outside 0-1.
