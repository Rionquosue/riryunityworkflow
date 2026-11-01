using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    internal enum IconCapBackground
    {
        Transparent,
        Opaque,
        TransparentWithBackground
    }

    internal enum IconCapLighting
    {
        Scene,
        Hdri,
        Flat
    }

    internal enum IconCapFov
    {
        SceneView,
        Custom,
        Lens35,
        Lens50,
        Lens85
    }

    internal enum IconCapCorner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    internal enum IconCapEffectType
    {
        Overlay,
        DrawUnder,
        Mask,
        InverseMask,
        GrayOut,
        CornerBadge,
        CustomMaterial,
        CropAndZoom,
        RotateAndFlip,
        Padding,
        Adjustments,
        Outline,
        DropShadow,
        ColourOverlay,
        HueShift,
        Invert,
        Grayscale,
        Posterize,
        Sharpen
    }

    /// <summary>
    /// The capture camera: the view taken from the Scene View (position, rotation,
    /// projection) and the settings the user sets (field of view, far clip, roll).
    /// </summary>
    [Serializable]
    internal sealed class IconCapCamera
    {
        // The view, from the Scene View camera at capture time.
        public Vector3 Position;
        public Quaternion Rotation = Quaternion.identity;
        public bool Orthographic;
        public float OrthographicSize = 1f;
        public float SceneFov = 60f;
        public float NearClip = 0.03f;

        // The settings.
        public IconCapFov FovMode = IconCapFov.SceneView;
        public float Fov = 30f;
        public float FarClip = 1000f;
        public float Roll;

        /// <summary>The vertical field of view used; a lens is a full-frame focal length.</summary>
        internal float FieldOfView
        {
            get
            {
                switch (FovMode)
                {
                    case IconCapFov.Custom: return Fov;
                    case IconCapFov.Lens35: return Camera.FocalLengthToFieldOfView(35f, IconCapRenderer.SensorHeight);
                    case IconCapFov.Lens50: return Camera.FocalLengthToFieldOfView(50f, IconCapRenderer.SensorHeight);
                    case IconCapFov.Lens85: return Camera.FocalLengthToFieldOfView(85f, IconCapRenderer.SensorHeight);
                    default: return SceneFov;
                }
            }
        }

        /// <summary>Takes the view from a Scene View camera; the settings are kept.</summary>
        internal void TakeView(Camera view)
        {
            Position = view.transform.position;
            Rotation = view.transform.rotation;
            Orthographic = view.orthographic;
            OrthographicSize = view.orthographicSize;
            SceneFov = view.fieldOfView;
            NearClip = view.nearClipPlane;
        }
    }

    [Serializable]
    internal sealed class IconCapBackgroundSettings
    {
        public IconCapBackground Mode = IconCapBackground.Transparent;
        public Color Colour = new Color(0.2f, 0.2f, 0.2f, 1f);
        // Opaque only: the skybox instead of Colour.
        public bool Skybox;
    }

    [Serializable]
    internal sealed class IconCapOptions
    {
        public bool AlphaCutout = true;
        public IconCapLighting Lighting = IconCapLighting.Scene;
        // GUID of the HDRI cubemap.
        public string Hdri = "";
        public bool ExcludeGlobal;
        public bool ExcludeScene;
        public bool ExcludeWorld;
        public bool ExcludeReflections;
        public bool ExcludeBaked;
        public bool ExcludeShadows;
    }

    /// <summary>What is captured: the selected renderers, or one isolated item, as GlobalObjectId strings.</summary>
    [Serializable]
    internal sealed class IconCapObjects
    {
        public bool OnlySelected;
        public List<string> Renderers = new List<string>();
        public string Isolated = "";
    }

    /// <summary>One effect of an effect stack. Assets are kept as GUIDs, so a job file can name them.</summary>
    [Serializable]
    internal sealed class IconCapEffect
    {
        public IconCapEffectType Type;
        public bool Enabled = true;

        // Texture layers: the textures (cycled across an icon list) and their tint.
        public List<string> Textures = new List<string>();
        public Color Tint = Color.white;
        public IconCapCorner Corner = IconCapCorner.TopRight;
        // Custom material: the material's GUID.
        public string Material = "";

        // Transform.
        public float Zoom = 1f;
        public Vector2 Centre = new Vector2(0.5f, 0.5f);
        public float Angle;
        public bool FlipX;
        public bool FlipY;
        public float Padding = 0.1f;

        // Adjustments.
        public float Brightness;
        public float Contrast = 1f;
        public float Saturation = 1f;

        // Outline, drop shadow and colour overlay (the alpha is the strength of an overlay).
        public Color Colour = Color.black;
        public float Thickness = 4f;
        public float Softness = 0.2f;
        public Vector2 Offset = new Vector2(4f, 4f);
        public float Blur = 4f;

        // Filters; Amount is also how much Gray out darkens.
        public float Hue = 35f;
        public int Levels = 6;
        public float Amount = 1f;

        internal static IconCapEffect Create(IconCapEffectType type)
        {
            var effect = new IconCapEffect { Type = type };
            if (type == IconCapEffectType.GrayOut) effect.Amount = 0.5f;
            if (type == IconCapEffectType.ColourOverlay) effect.Colour = new Color(1f, 1f, 1f, 0.5f);
            if (type == IconCapEffectType.DropShadow) effect.Colour = new Color(0f, 0f, 0f, 0.6f);
            return effect;
        }

        /// <summary>The texture for the icon at this place in an icon list.</summary>
        internal Texture2D TextureFor(int iconIndex)
        {
            var textures = new List<Texture2D>();
            foreach (string guid in Textures)
            {
                Texture2D texture = IconCapAssets.Load<Texture2D>(guid);
                if (texture != null) textures.Add(texture);
            }
            if (textures.Count == 0) return null;
            return textures[((iconIndex % textures.Count) + textures.Count) % textures.Count];
        }
    }

    /// <summary>Everything a capture is made with, apart from what is captured.</summary>
    [Serializable]
    internal sealed class IconCapSettings
    {
        public int Size = 512;
        public IconCapCamera Camera = new IconCapCamera();
        public IconCapBackgroundSettings Background = new IconCapBackgroundSettings();
        public IconCapOptions Options = new IconCapOptions();
        public List<IconCapEffect> Effects = new List<IconCapEffect>();
    }

    /// <summary>
    /// One capture: everything needed to make it again. Listed under This session,
    /// and saved in a job of an export folder's .iconcap.json.
    /// </summary>
    [Serializable]
    internal sealed class IconCapEntry
    {
        public string FileName = "";
        // The full path it was saved to; empty until it is exported.
        public string SavedPath = "";
        public IconCapSettings Settings = new IconCapSettings();
        public IconCapObjects Objects = new IconCapObjects();

        // The captured image, this session only.
        [NonSerialized] public Texture2D Image;

        internal bool Exported
        {
            get { return SavedPath.Length > 0; }
        }
    }

    [Serializable]
    internal sealed class IconCapJob
    {
        public string Name = "";
        public List<IconCapEntry> Entries = new List<IconCapEntry>();
    }

    /// <summary>The contents of an export folder's .iconcap.json.</summary>
    [Serializable]
    internal sealed class IconCapJobFile
    {
        public List<IconCapJob> Jobs = new List<IconCapJob>();
    }

    /// <summary>Asset and scene object references kept as strings.</summary>
    internal static class IconCapAssets
    {
        internal static T Copy<T>(T value)
        {
            return JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        }

        internal static string Guid(UnityEngine.Object asset)
        {
            string guid;
            long localId;
            return asset != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out guid, out localId) ? guid : "";
        }

        internal static T Load<T>(string guid) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(guid)) return null;
            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
        }

        internal static string SceneId(UnityEngine.Object target)
        {
            return target == null ? "" : GlobalObjectId.GetGlobalObjectIdSlow(target).ToString();
        }

        internal static T Find<T>(string id) where T : UnityEngine.Object
        {
            GlobalObjectId parsed;
            if (string.IsNullOrEmpty(id) || !GlobalObjectId.TryParse(id, out parsed)) return null;
            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(parsed) as T;
        }
    }
}

// ============================================================================
// GLOSSARY: IconCap data
// ============================================================================
//
// IconCapCamera           View (Position, Rotation, Orthographic, OrthographicSize, SceneFov, NearClip; TakeView) and
//                         settings (FovMode: Scene View, Custom or a 35/50/85mm lens; Fov, FarClip, Roll). FieldOfView.
// IconCapBackgroundSettings  Mode (Transparent, Opaque, Transparent with background), Colour, Skybox (Opaque only).
// IconCapOptions          AlphaCutout, Lighting (Scene, HDRI, Flat), Hdri (cubemap GUID), the six Exclude switches.
// IconCapObjects          OnlySelected, Renderers and Isolated as GlobalObjectId strings.
// IconCapEffect           One effect: Type, Enabled and the settings of every type; Textures and Material are GUIDs.
//                         Create(type) sets a type's defaults; TextureFor(iconIndex) cycles the textures.
// IconCapSettings         Size, Camera, Background, Options, Effects: what a capture is made with; a preset holds these.
// IconCapEntry            One capture: FileName, SavedPath (empty until exported), Settings, Objects; Image this session.
// IconCapJob / IconCapJobFile  A named list of entries / an export folder's .iconcap.json.
// IconCapAssets           Copy (a deep copy), Guid / Load (assets), SceneId / Find (scene objects).
