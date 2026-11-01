using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Renders a square capture with a hidden camera placed where the Scene View camera
    /// was. Only the captured renderers draw: every other renderer is turned off for the
    /// render (Renderer.forceRenderingOff, which is not saved). Lighting overrides and
    /// exclusions are set for the render and put back straight after, so every change
    /// to the scene is undone. Processing order: render with the camera, then the effect
    /// stack (IconCapEffects). Transparent backgrounds take their alpha from a second pass
    /// that draws only the captured renderers (Shaders/IconCapDraw.shader).
    /// </summary>
    internal static class IconCapRenderer
    {
        internal const string DrawShaderName = "Hidden/RiryTools/IconCap/Draw";
        // Full-frame sensor height for the lens choices, in millimetres.
        internal const float SensorHeight = 24f;
        internal const int MaxSize = 4096;
        private const int AlphaPass = 0;
        private const int FlatPass = 1;
        private const int HdriLatLongWidth = 64;

        private static Camera captureCamera;
        private static Shader drawShader;
        private static Material hdriSkybox;
        private static string hdriProbeGuid;
        private static SphericalHarmonicsL2 hdriProbe;

        /// <summary>
        /// The renderers a capture draws: everything under the isolated item; else, with
        /// only selected objects, those renderers; else every skinned mesh renderer in the
        /// open scenes (or in the prefab open in Prefab Mode). Null with a reason when the
        /// objects cannot be found.
        /// </summary>
        internal static List<Renderer> Targets(bool onlySelected, IEnumerable<Renderer> selected, GameObject isolated, out string problem)
        {
            problem = null;
            var targets = new List<Renderer>();
            if (isolated != null)
            {
                targets.AddRange(isolated.GetComponentsInChildren<Renderer>());
            }
            else if (onlySelected)
            {
                foreach (Renderer renderer in selected)
                {
                    if (renderer != null && !targets.Contains(renderer)) targets.Add(renderer);
                }
                if (targets.Count == 0) problem = "Add the objects to capture, or turn off Capture only selected objects.";
            }
            else
            {
                targets.AddRange(StageUtility.GetCurrentStageHandle().FindComponentsOfType<SkinnedMeshRenderer>());
                if (targets.Count == 0) problem = "There is no skinned mesh renderer to capture.";
            }
            return problem == null ? targets : null;
        }

        /// <summary>The renderers of a saved entry, found again in the open scenes.</summary>
        internal static List<Renderer> Targets(IconCapObjects objects, out string problem)
        {
            GameObject isolated = null;
            if (!string.IsNullOrEmpty(objects.Isolated))
            {
                isolated = IconCapAssets.Find<GameObject>(objects.Isolated);
                if (isolated == null)
                {
                    problem = "The isolated item is not in the open scenes.";
                    return null;
                }
            }
            var selected = new List<Renderer>();
            foreach (string id in objects.Renderers)
            {
                var renderer = IconCapAssets.Find<Renderer>(id);
                if (renderer != null) selected.Add(renderer);
            }
            if (isolated == null && objects.OnlySelected && selected.Count < objects.Renderers.Count)
            {
                problem = "Some of the captured objects are not in the open scenes.";
                return null;
            }
            return Targets(objects.OnlySelected, selected, isolated, out problem);
        }

        /// <summary>
        /// Renders the targets at size with the settings, then the effect stack, and
        /// returns a temporary image (release it with RenderTexture.ReleaseTemporary).
        /// iconIndex picks texture layers' textures.
        /// </summary>
        internal static RenderTexture Render(IconCapSettings settings, List<Renderer> targets, int size, int iconIndex)
        {
            RenderTexture raw = RenderRaw(settings, targets, size);
            try
            {
                return IconCapEffects.Apply(raw, settings.Effects, iconIndex, size / (float)Mathf.Max(1, settings.Size));
            }
            finally
            {
                RenderTexture.ReleaseTemporary(raw);
            }
        }

        /// <summary>The capture before the effect stack: colour, with the alpha pass as alpha unless opaque.</summary>
        internal static RenderTexture RenderRaw(IconCapSettings settings, List<Renderer> targets, int size)
        {
            size = Mathf.Clamp(size, 1, MaxSize);
            Camera camera = CaptureCamera();
            Place(camera, settings.Camera);
            IconCapBackgroundSettings background = settings.Background;
            bool opaque = background.Mode == IconCapBackground.Opaque;

            var live = new List<Renderer>();
            foreach (Renderer renderer in targets)
            {
                if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy) live.Add(renderer);
            }

            var hidden = new List<Renderer>();
            var state = new SceneState();
            var created = new List<Material>();
            RenderTexture colour = RenderTexture.GetTemporary(size, size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture alpha = opaque ? null : RenderTexture.GetTemporary(size, size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            RenderTexture result = IconCapEffects.Temporary(size, size);
            try
            {
                var keep = new HashSet<Renderer>(live);
                foreach (Renderer renderer in StageUtility.GetCurrentStageHandle().FindComponentsOfType<Renderer>())
                {
                    if (keep.Contains(renderer) || renderer.forceRenderingOff) continue;
                    renderer.forceRenderingOff = true;
                    hidden.Add(renderer);
                }
                state.Apply(settings.Options);

                // Colour pass.
                camera.targetTexture = colour;
                camera.clearFlags = opaque && background.Skybox ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
                Color clear = background.Mode == IconCapBackground.Transparent ? Color.clear : background.Colour;
                clear.a = background.Mode == IconCapBackground.Transparent ? 0f : 1f;
                camera.backgroundColor = clear;
                if (settings.Options.Lighting == IconCapLighting.Flat)
                {
                    camera.cullingMask = 0;
                    RenderWith(camera, Draws(live, FlatPass, settings.Options.AlphaCutout, created));
                }
                else
                {
                    camera.cullingMask = ~0;
                    camera.Render();
                }

                // Alpha pass.
                if (alpha != null)
                {
                    camera.targetTexture = alpha;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = Color.clear;
                    camera.cullingMask = 0;
                    RenderWith(camera, Draws(live, AlphaPass, settings.Options.AlphaCutout, created));
                }
            }
            finally
            {
                camera.targetTexture = null;
                state.Restore();
                foreach (Renderer renderer in hidden)
                {
                    if (renderer != null) renderer.forceRenderingOff = false;
                }
                foreach (Material material in created) UnityEngine.Object.DestroyImmediate(material);
            }

            Material composite = IconCapEffects.Material;
            composite.SetTexture("_MaskTex", alpha != null ? (Texture)alpha : Texture2D.whiteTexture);
            composite.SetFloat("_Opaque", opaque ? 1f : 0f);
            Graphics.Blit(colour, result, composite, IconCapEffects.CompositePass);
            RenderTexture.ReleaseTemporary(colour);
            if (alpha != null) RenderTexture.ReleaseTemporary(alpha);
            return result;
        }

        private static Camera CaptureCamera()
        {
            if (captureCamera != null) return captureCamera;
            GameObject holder = EditorUtility.CreateGameObjectWithHideFlags("RiryTools IconCap Camera", HideFlags.HideAndDontSave, typeof(Camera));
            captureCamera = holder.GetComponent<Camera>();
            captureCamera.enabled = false;
            captureCamera.renderingPath = RenderingPath.Forward;
            captureCamera.allowHDR = false;
            captureCamera.allowMSAA = false;
            return captureCamera;
        }

        private static void Place(Camera camera, IconCapCamera view)
        {
            camera.transform.SetPositionAndRotation(view.Position, view.Rotation * Quaternion.Euler(0f, 0f, view.Roll));
            camera.orthographic = view.Orthographic;
            camera.orthographicSize = view.OrthographicSize;
            camera.fieldOfView = Mathf.Clamp(view.FieldOfView, 1f, 179f);
            camera.nearClipPlane = Mathf.Max(0.0001f, view.NearClip);
            camera.farClipPlane = Mathf.Max(camera.nearClipPlane + 0.001f, view.FarClip);
            camera.aspect = 1f;
            // In Prefab Mode the prefab lives in its own preview scene.
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            camera.scene = stage != null ? stage.scene : default(Scene);
        }

        private static void RenderWith(Camera camera, CommandBuffer draws)
        {
            camera.AddCommandBuffer(CameraEvent.AfterForwardOpaque, draws);
            try
            {
                camera.Render();
            }
            finally
            {
                camera.RemoveCommandBuffer(CameraEvent.AfterForwardOpaque, draws);
                draws.Release();
            }
        }

        // Each renderer's materials drawn with a copy of the Draw shader made from them.
        private static CommandBuffer Draws(List<Renderer> renderers, int pass, bool alphaCutout, List<Material> created)
        {
            if (drawShader == null) drawShader = Shader.Find(DrawShaderName);
            if (drawShader == null) throw new InvalidOperationException("IconCap: the shader " + DrawShaderName + " is missing.");
            var draws = new CommandBuffer { name = "RiryTools IconCap" };
            var made = new Dictionary<Material, Material>();
            foreach (Renderer renderer in renderers)
            {
                Material[] materials = renderer.sharedMaterials;
                int subMeshes = SubMeshCount(renderer);
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null) continue;
                    Material draw;
                    if (!made.TryGetValue(materials[i], out draw))
                    {
                        draw = DrawMaterial(materials[i], alphaCutout);
                        made[materials[i]] = draw;
                        created.Add(draw);
                    }
                    draws.DrawRenderer(renderer, draw, Mathf.Clamp(i, 0, Mathf.Max(0, subMeshes - 1)), pass);
                }
            }
            return draws;
        }

        private static int SubMeshCount(Renderer renderer)
        {
            var skinned = renderer as SkinnedMeshRenderer;
            if (skinned != null) return skinned.sharedMesh != null ? skinned.sharedMesh.subMeshCount : 1;
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            return filter != null && filter.sharedMesh != null ? filter.sharedMesh.subMeshCount : 1;
        }

        private static Material DrawMaterial(Material source, bool alphaCutout)
        {
            var draw = new Material(drawShader) { hideFlags = HideFlags.HideAndDontSave };
            string texture = FirstProperty(source, "_MainTex", "_BaseMap", "_BaseColorMap");
            if (texture != null)
            {
                draw.SetTexture("_MainTex", source.GetTexture(texture));
                draw.SetTextureScale("_MainTex", source.GetTextureScale(texture));
                draw.SetTextureOffset("_MainTex", source.GetTextureOffset(texture));
            }
            string colour = FirstProperty(source, "_Color", "_BaseColor");
            draw.SetColor("_Color", colour != null ? source.GetColor(colour) : Color.white);
            draw.SetFloat("_Cutoff", source.HasProperty("_Cutoff") ? source.GetFloat("_Cutoff") : 0.5f);
            string cull = FirstProperty(source, "_Cull", "_CullMode");
            draw.SetFloat("_Cull", cull != null ? source.GetFloat(cull) : (float)CullMode.Back);
            if (alphaCutout && HasAlphaClipping(source)) draw.EnableKeyword("ICONCAP_CLIP");
            return draw;
        }

        /// <summary>
        /// Whether the material alpha-tests, by its own settings: the _ALPHATEST_ON keyword
        /// (Standard and most PBR shaders), _AlphaClip on (URP), or a TransparentCutout
        /// render type. A material without alpha clipping is drawn solid.
        /// </summary>
        internal static bool HasAlphaClipping(Material material)
        {
            if (material.IsKeywordEnabled("_ALPHATEST_ON")) return true;
            if (material.HasProperty("_AlphaClip") && material.GetFloat("_AlphaClip") > 0.5f) return true;
            return material.GetTag("RenderType", true, "") == "TransparentCutout";
        }

        private static string FirstProperty(Material material, params string[] names)
        {
            foreach (string name in names)
            {
                if (material.HasProperty(name)) return name;
            }
            return null;
        }

        /// <summary>The scene's lighting while a capture renders, and what it was before.</summary>
        private sealed class SceneState
        {
            private readonly List<Light> lights = new List<Light>();
            private readonly List<ReflectionProbe> probes = new List<ReflectionProbe>();
            private bool renderSettingsChanged;
            private Material skybox;
            private AmbientMode ambientMode;
            private Color ambientLight;
            private SphericalHarmonicsL2 ambientProbe;
            private DefaultReflectionMode reflectionMode;
            private Texture customReflection;
            private float reflectionIntensity;
            private bool lightmapsChanged;
            private LightmapData[] lightmaps;
            private LightProbes lightProbes;
            private bool shadowsChanged;
            private ShadowQuality shadows;

            internal void Apply(IconCapOptions options)
            {
                bool flat = options.Lighting == IconCapLighting.Flat;
                bool hdri = options.Lighting == IconCapLighting.Hdri;
                Cubemap cube = hdri ? IconCapAssets.Load<Cubemap>(options.Hdri) : null;
                if (!flat)
                {
                    foreach (Light light in StageUtility.GetCurrentStageHandle().FindComponentsOfType<Light>())
                    {
                        if (!light.enabled) continue;
                        bool directional = light.type == LightType.Directional;
                        bool scene = light.type == LightType.Point || light.type == LightType.Spot;
                        if (!(directional && options.ExcludeGlobal) && !(scene && options.ExcludeScene)) continue;
                        light.enabled = false;
                        lights.Add(light);
                    }
                }
                if (options.ExcludeReflections || cube != null)
                {
                    foreach (ReflectionProbe probe in StageUtility.GetCurrentStageHandle().FindComponentsOfType<ReflectionProbe>())
                    {
                        if (!probe.enabled) continue;
                        probe.enabled = false;
                        probes.Add(probe);
                    }
                }
                if (options.ExcludeWorld || options.ExcludeReflections || cube != null)
                {
                    renderSettingsChanged = true;
                    skybox = RenderSettings.skybox;
                    ambientMode = RenderSettings.ambientMode;
                    ambientLight = RenderSettings.ambientLight;
                    ambientProbe = RenderSettings.ambientProbe;
                    reflectionMode = RenderSettings.defaultReflectionMode;
                    customReflection = RenderSettings.customReflectionTexture;
                    reflectionIntensity = RenderSettings.reflectionIntensity;
                    if (cube != null)
                    {
                        RenderSettings.skybox = HdriSkybox(cube);
                        RenderSettings.ambientMode = AmbientMode.Skybox;
                        RenderSettings.ambientProbe = HdriProbe(options.Hdri, cube);
                        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                        RenderSettings.customReflectionTexture = cube;
                    }
                    if (options.ExcludeWorld)
                    {
                        RenderSettings.ambientMode = AmbientMode.Flat;
                        RenderSettings.ambientLight = Color.black;
                        RenderSettings.ambientProbe = new SphericalHarmonicsL2();
                    }
                    if (options.ExcludeReflections) RenderSettings.reflectionIntensity = 0f;
                }
                if (options.ExcludeBaked)
                {
                    lightmapsChanged = true;
                    lightmaps = LightmapSettings.lightmaps;
                    lightProbes = LightmapSettings.lightProbes;
                    LightmapSettings.lightmaps = new LightmapData[0];
                    LightmapSettings.lightProbes = null;
                }
                if (options.ExcludeShadows)
                {
                    shadowsChanged = true;
                    shadows = QualitySettings.shadows;
                    QualitySettings.shadows = ShadowQuality.Disable;
                }
            }

            internal void Restore()
            {
                foreach (Light light in lights)
                {
                    if (light != null) light.enabled = true;
                }
                foreach (ReflectionProbe probe in probes)
                {
                    if (probe != null) probe.enabled = true;
                }
                if (renderSettingsChanged)
                {
                    RenderSettings.skybox = skybox;
                    RenderSettings.ambientMode = ambientMode;
                    RenderSettings.ambientLight = ambientLight;
                    RenderSettings.ambientProbe = ambientProbe;
                    RenderSettings.defaultReflectionMode = reflectionMode;
                    RenderSettings.customReflectionTexture = customReflection;
                    RenderSettings.reflectionIntensity = reflectionIntensity;
                }
                if (lightmapsChanged)
                {
                    LightmapSettings.lightmaps = lightmaps;
                    LightmapSettings.lightProbes = lightProbes;
                }
                if (shadowsChanged) QualitySettings.shadows = shadows;
            }
        }

        private static Material HdriSkybox(Cubemap cube)
        {
            if (hdriSkybox == null)
            {
                Shader shader = Shader.Find("Skybox/Cubemap");
                hdriSkybox = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
            hdriSkybox.SetTexture("_Tex", cube);
            return hdriSkybox;
        }

        // The HDRI's ambient light as spherical harmonics, worked out once per cubemap.
        private static SphericalHarmonicsL2 HdriProbe(string guid, Cubemap cube)
        {
            if (hdriProbeGuid == guid) return hdriProbe;
            int width = HdriLatLongWidth;
            int height = width / 2;
            RenderTexture unwrapped = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            RenderTexture previous = RenderTexture.active;
            var pixels = new Texture2D(width, height, TextureFormat.RGBAHalf, false, true);
            try
            {
                Material material = IconCapEffects.Material;
                material.SetTexture("_Cube", cube);
                Graphics.Blit(null, unwrapped, material, IconCapEffects.LatLongPass);
                RenderTexture.active = unwrapped;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(unwrapped);
            }
            var probe = new SphericalHarmonicsL2();
            for (int y = 0; y < height; y++)
            {
                float theta = (1f - (y + 0.5f) / height) * Mathf.PI;
                float solidAngle = (2f * Mathf.PI / width) * (Mathf.PI / height) * Mathf.Sin(theta);
                for (int x = 0; x < width; x++)
                {
                    float phi = (x + 0.5f) / width * 2f * Mathf.PI;
                    var direction = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
                    probe.AddDirectionalLight(direction, pixels.GetPixel(x, y), solidAngle / Mathf.PI);
                }
            }
            UnityEngine.Object.DestroyImmediate(pixels);
            hdriProbeGuid = guid;
            hdriProbe = probe;
            return probe;
        }
    }
}

// ============================================================================
// GLOSSARY: IconCap rendering
// ============================================================================
//
// IconCapRenderer (this file)
//   Targets(onlySelected, selected, isolated, out problem)  Isolated item and its children; else the selected
//                                renderers; else every skinned mesh renderer in the open scenes or Prefab Mode.
//   Targets(objects, out problem)  The same for a saved entry (GlobalObjectIds).
//   Render(settings, targets, size, iconIndex)  RenderRaw, then the effect stack.
//   RenderRaw(settings, targets, size)  Colour pass (camera; Flat draws the base colour unlit), alpha pass (only the
//                                targets, white; clipped by the material's cutoff when Alpha cutout is on and the
//                                material has alpha clipping), composite (Opaque has no alpha pass).
//   HasAlphaClipping(material)   _ALPHATEST_ON, _AlphaClip on, or RenderType TransparentCutout.
//   SceneState                   Exclude switches (directional lights, point and spot lights, ambient, reflections,
//                                lightmaps and light probes, shadows) and the HDRI override, undone after the render.
//   HdriProbe / HdriSkybox       The HDRI's ambient light (spherical harmonics from a 64x32 unwrap) and its skybox.
//   SensorHeight (24 mm)         The full-frame sensor the 35/50/85mm lenses are worked out for.
//   MaxSize (4096)               Largest capture size.
// Other renderers are turned off with Renderer.forceRenderingOff, which is not saved with the scene.
