using System;
using System.Collections.Generic;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Runs an effect stack on the GPU: each enabled effect is one Blit of
    /// Hidden/RiryTools/IconCap/Effects (Shaders/IconCapEffects.shader), from the
    /// bottom of the list to the top. Images are straight alpha.
    /// </summary>
    internal static class IconCapEffects
    {
        internal const string ShaderName = "Hidden/RiryTools/IconCap/Effects";

        // Shader passes besides the effects (whose pass is their IconCapEffectType, apart from
        // CustomMaterial, which runs the user's material, and those after it, one lower).
        internal const int CompositePass = 18;
        internal const int LatLongPass = 19;

        private static Material material;

        internal static Material Material
        {
            get
            {
                if (material != null) return material;
                Shader shader = Shader.Find(ShaderName);
                if (shader == null) throw new InvalidOperationException("IconCap: the shader " + ShaderName + " is missing.");
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                return material;
            }
        }

        /// <summary>Display names, in Add effect order.</summary>
        internal static string Name(IconCapEffectType type)
        {
            switch (type)
            {
                case IconCapEffectType.DrawUnder: return "Draw under";
                case IconCapEffectType.InverseMask: return "Inverse mask";
                case IconCapEffectType.GrayOut: return "Gray out";
                case IconCapEffectType.CornerBadge: return "Corner badge";
                case IconCapEffectType.CustomMaterial: return "Custom material";
                case IconCapEffectType.CropAndZoom: return "Crop and zoom";
                case IconCapEffectType.RotateAndFlip: return "Rotate and flip";
                case IconCapEffectType.Adjustments: return "Brightness, contrast and saturation";
                case IconCapEffectType.DropShadow: return "Drop shadow";
                case IconCapEffectType.ColourOverlay: return "Colour overlay";
                case IconCapEffectType.HueShift: return "Hue shift";
                default: return type.ToString();
            }
        }

        /// <summary>The Add effect menu path: the group, then the name.</summary>
        internal static string MenuPath(IconCapEffectType type)
        {
            if (type <= IconCapEffectType.CustomMaterial) return "Texture layers/" + Name(type);
            if (type <= IconCapEffectType.Padding) return "Transform/" + Name(type);
            if (type == IconCapEffectType.Adjustments) return "Adjustments/" + Name(type);
            if (type <= IconCapEffectType.DropShadow) return "Outline and drop shadow/" + Name(type);
            return "Filters/" + Name(type);
        }

        internal static bool UsesTextures(IconCapEffectType type)
        {
            return type <= IconCapEffectType.CornerBadge;
        }

        /// <summary>A temporary straight-alpha image of the size; release it with RenderTexture.ReleaseTemporary.</summary>
        internal static RenderTexture Temporary(int width, int height)
        {
            RenderTexture texture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }

        /// <summary>
        /// Runs the stack on source, bottom to top, and returns a new temporary image;
        /// source is left as it is. iconIndex picks each texture layer's texture;
        /// pixelScale is the image's size over the final size, for pixel settings.
        /// </summary>
        internal static RenderTexture Apply(Texture source, IList<IconCapEffect> effects, int iconIndex, float pixelScale)
        {
            RenderTexture current = Temporary(source.width, source.height);
            Graphics.Blit(source, current);
            if (effects == null) return current;
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                IconCapEffect effect = effects[i];
                if (effect == null || !effect.Enabled) continue;
                RenderTexture next = Temporary(current.width, current.height);
                if (!ApplyOne(effect, current, next, iconIndex, pixelScale)) Graphics.Blit(current, next);
                RenderTexture.ReleaseTemporary(current);
                current = next;
            }
            return current;
        }

        // False when the effect has nothing to do (no texture or material set).
        private static bool ApplyOne(IconCapEffect effect, RenderTexture source, RenderTexture destination, int iconIndex, float pixelScale)
        {
            Material m = Material;
            if (effect.Type == IconCapEffectType.CustomMaterial)
            {
                Material custom = IconCapAssets.Load<Material>(effect.Material);
                if (custom == null) return false;
                Graphics.Blit(source, destination, custom);
                return true;
            }

            Texture2D layer = UsesTextures(effect.Type) ? effect.TextureFor(iconIndex) : null;
            if (UsesTextures(effect.Type) && layer == null && effect.Type != IconCapEffectType.GrayOut) return false;
            m.SetTexture("_LayerTex", layer != null ? layer : Texture2D.whiteTexture);
            m.SetFloat("_HasLayer", layer != null ? 1f : 0f);
            m.SetColor("_Tint", effect.Tint);
            m.SetColor("_Colour", effect.Colour);
            m.SetVector("_Corner", CornerOrigin(effect.Corner));
            m.SetVector("_Centre", effect.Centre);
            m.SetVector("_Offset", effect.Offset * pixelScale);
            m.SetFloat("_Zoom", effect.Zoom);
            m.SetFloat("_Angle", effect.Angle);
            m.SetFloat("_FlipX", effect.FlipX ? 1f : 0f);
            m.SetFloat("_FlipY", effect.FlipY ? 1f : 0f);
            m.SetFloat("_Padding", Mathf.Clamp(effect.Padding, 0f, 0.45f));
            m.SetFloat("_Brightness", effect.Brightness);
            m.SetFloat("_Contrast", effect.Contrast);
            m.SetFloat("_Saturation", effect.Saturation);
            m.SetFloat("_Thickness", effect.Thickness * pixelScale);
            m.SetFloat("_Softness", effect.Softness);
            m.SetFloat("_Blur", effect.Blur * pixelScale);
            m.SetFloat("_Hue", effect.Hue);
            m.SetFloat("_Levels", Mathf.Max(2, effect.Levels));
            m.SetFloat("_Amount", effect.Amount);
            int pass = (int)effect.Type > (int)IconCapEffectType.CustomMaterial ? (int)effect.Type - 1 : (int)effect.Type;
            Graphics.Blit(source, destination, m, pass);
            return true;
        }

        // The lower-left corner of a half-size badge, in UV.
        private static Vector4 CornerOrigin(IconCapCorner corner)
        {
            switch (corner)
            {
                case IconCapCorner.TopLeft: return new Vector4(0f, 0.5f);
                case IconCapCorner.BottomLeft: return new Vector4(0f, 0f);
                case IconCapCorner.BottomRight: return new Vector4(0.5f, 0f);
                default: return new Vector4(0.5f, 0.5f);
            }
        }

        /// <summary>Reads an image back as a Texture2D (RGBA32, sRGB), kept out of the scene.</summary>
        internal static Texture2D Read(RenderTexture image)
        {
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = image;
                var texture = new Texture2D(image.width, image.height, TextureFormat.RGBA32, false, false)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                texture.ReadPixels(new Rect(0, 0, image.width, image.height), 0, 0);
                texture.Apply();
                return texture;
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }
    }
}

// ============================================================================
// GLOSSARY: IconCap effect stack
// ============================================================================
//
// IconCapEffects (this file)
//   Apply(source, effects, iconIndex, pixelScale)  Runs the enabled effects bottom to top into a new temporary image.
//   Name(type) / MenuPath(type)                     Display name; Add effect menu path (Texture layers, Transform,
//                                                   Adjustments, Outline and drop shadow, Filters).
//   UsesTextures(type)                              Overlay, Draw under, Mask, Inverse mask, Gray out, Corner badge.
//   Temporary(w, h) / Read(image)                   A temporary ARGB32 sRGB image; read back as a Texture2D.
//   Material                                        Hidden/RiryTools/IconCap/Effects (Shaders/IconCapEffects.shader):
//                                                   one pass per effect (CustomMaterial runs the user's material instead),
//                                                   then CompositePass (18) and LatLongPass (19).
// Pixel settings (Thickness, Blur, Offset) are in pixels of the final image; pixelScale scales them for previews.
