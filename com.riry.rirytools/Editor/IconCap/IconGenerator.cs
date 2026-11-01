using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// An Icon Generator asset: source textures run through an effect stack (its own,
    /// or its Template's). Each output is named prefix + source name + suffix and saved
    /// beside the asset. Outputs are followed by GUID, so when a source is renamed its
    /// output is renamed to match, at the next Regenerate only.
    /// Edited in IconCap's Icon Generator rollout (Editor/UI/IconCapWindow.cs).
    /// </summary>
    public sealed class IconGenerator : ScriptableObject
    {
        [Serializable]
        internal sealed class Source
        {
            public Texture2D Texture;
            // The GUID of the output made from it.
            public string Output = "";
        }

        [SerializeField] internal List<Source> Sources = new List<Source>();
        [SerializeField] internal string Prefix = "";
        [SerializeField] internal string Suffix = "";
        [SerializeField] internal IconGenerator Template;
        [SerializeField] internal List<IconCapEffect> Effects = new List<IconCapEffect>();

        /// <summary>The effect stack used: the Template's (followed to the end), else its own.</summary>
        internal List<IconCapEffect> Stack
        {
            get
            {
                IconGenerator current = this;
                var seen = new HashSet<IconGenerator>();
                while (current.Template != null && seen.Add(current)) current = current.Template;
                return current.Effects;
            }
        }

        internal string OutputName(Texture2D source)
        {
            return IconCapFiles.PngName(Prefix + source.name + Suffix);
        }

        /// <summary>The source through the stack, at the source's size; a temporary image.</summary>
        internal RenderTexture Generate(Texture2D source, int index, int size = 0)
        {
            int width = size > 0 ? size : source.width;
            int height = size > 0 ? size : source.height;
            RenderTexture scaled = IconCapEffects.Temporary(width, height);
            try
            {
                Graphics.Blit(source, scaled);
                return IconCapEffects.Apply(scaled, Stack, index, width / (float)Mathf.Max(1, source.width));
            }
            finally
            {
                RenderTexture.ReleaseTemporary(scaled);
            }
        }

        /// <summary>
        /// Remakes every output beside the asset. An output whose name no longer matches its
        /// source (the source was renamed) is renamed first, so it keeps its GUID.
        /// Returns the problems, one per line.
        /// </summary>
        internal string Regenerate()
        {
            string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(this)).Replace('\\', '/');
            var problems = new List<string>();
            try
            {
                for (int i = 0; i < Sources.Count; i++)
                {
                    Source source = Sources[i];
                    if (source == null || source.Texture == null) continue;
                    EditorUtility.DisplayProgressBar("Icon Generator", source.Texture.name, i / (float)Math.Max(1, Sources.Count));
                    string target = folder + "/" + OutputName(source.Texture);
                    if (target == AssetDatabase.GetAssetPath(source.Texture))
                    {
                        problems.Add(source.Texture.name + ": the output would replace its source; set a prefix or suffix.");
                        continue;
                    }
                    string current = AssetDatabase.GUIDToAssetPath(source.Output);
                    if (!string.IsNullOrEmpty(current) && File.Exists(current) && current != target)
                    {
                        string error = AssetDatabase.MoveAsset(current, target);
                        if (!string.IsNullOrEmpty(error)) problems.Add(source.Texture.name + ": could not rename " + current + ": " + error);
                    }
                    RenderTexture image = Generate(source.Texture, i);
                    Texture2D read = IconCapEffects.Read(image);
                    RenderTexture.ReleaseTemporary(image);
                    File.WriteAllBytes(target, read.EncodeToPNG());
                    DestroyImmediate(read);
                    AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceUpdate);
                    source.Output = AssetDatabase.AssetPathToGUID(target);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.SetDirty(this);
                AssetDatabase.SaveAssetIfDirty(this);
            }
            return string.Join("\n", problems);
        }
    }
}

// ============================================================================
// GLOSSARY: Icon Generator
// ============================================================================
//
// IconGenerator (this file): the asset, edited in IconCap's Icon Generator rollout (Editor/UI/IconCapWindow.cs)
//   Sources            Source textures, each with the GUID of its Output.
//   Prefix / Suffix    Output name: prefix + source name + suffix + .png, beside the asset.
//   Template           Another generator whose effect stack is used instead of Effects.
//   Effects / Stack    Its own effect stack / the one used (Template's, followed to the end).
//   Generate(source, index, size)  A source through the stack (temporary image); index cycles texture layers.
//   Regenerate()       Remakes every output; renames an output by its GUID when its source was renamed.
