using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Import Presets preferences, kept for this user in every project in
    /// Documents/RiryTools/ImportPresets/preferences.yml. Tags: words that make a file
    /// type of their own (ImportPresetFolderRules) when a file name contains them, in any
    /// case. The file is created with the map names Substance Painter and similar
    /// programs export for PBR, metallic/roughness, specular/glossiness and Unity's
    /// metallic and specular smoothness, and is edited in Set Folder Import Presets.
    /// </summary>
    internal static class ImportPresetPreferences
    {
        internal const string FileName = "preferences.yml";

        internal static readonly string[] DefaultTags =
        {
            // Base color
            "basecolor", "basemap", "albedotransparency", "diffuse",
            // Metallic/roughness
            "metallic", "roughness",
            // Specular/glossiness
            "specular", "glossiness",
            // Unity metallic and specular smoothness
            "metallicsmoothness", "specularsmoothness", "smoothness", "maskmap",
            // Every workflow
            "normal", "height", "mixed_ao", "occlusion", "emissive", "emission", "opacity"
        };

        private static readonly Regex TagPattern = new Regex("^[a-z0-9_]+$", RegexOptions.CultureInvariant);
        private static readonly Regex Item = new Regex(@"^\s*-\s*(.*?)\s*$", RegexOptions.CultureInvariant);
        private static List<string> cached;
        private static DateTime cachedStamp;

        internal static string FilePath
        {
            get { return Path.Combine(ImportPresetLibrary.RootPath, FileName); }
        }

        /// <summary>The tags, lower case; the file is created with DefaultTags when it is missing.</summary>
        internal static List<string> Tags()
        {
            string path = FilePath;
            try
            {
                if (!File.Exists(path)) SaveTags(DefaultTags);
                DateTime stamp = File.GetLastWriteTimeUtc(path);
                if (cached != null && stamp == cachedStamp) return new List<string>(cached);
                var tags = new List<string>();
                bool inTags = false;
                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Split('#')[0].TrimEnd();
                    if (line.Trim().Length == 0) continue;
                    if (!line.StartsWith(" ", StringComparison.Ordinal) && !line.StartsWith("-", StringComparison.Ordinal))
                    {
                        inTags = line.Trim() == "tags:";
                        continue;
                    }
                    Match match = Item.Match(line);
                    if (!inTags || !match.Success) continue;
                    string tag = match.Groups[1].Value.Trim('"', '\'').ToLowerInvariant();
                    if (ValidateTag(tag) == null && !tags.Contains(tag)) tags.Add(tag);
                }
                cached = tags;
                cachedStamp = stamp;
                return new List<string>(tags);
            }
            catch (Exception exception)
            {
                ImportPresetLibrary.ReportOnce("preferences:" + path, "Could not read " + path + ": " + exception.Message);
                return new List<string>();
            }
        }

        internal static void SaveTags(IEnumerable<string> tags)
        {
            Directory.CreateDirectory(ImportPresetLibrary.RootPath);
            var text = new StringBuilder();
            text.Append("# RiryTools Import Presets preferences\n");
            text.Append("# tags: words that make a file type of their own in Set Folder Import Presets when a\n");
            text.Append("# file name contains them, in any case (letters, digits and _ only).\n");
            text.Append("tags:\n");
            foreach (string tag in tags) text.Append("  - ").Append(tag).Append('\n');
            File.WriteAllText(FilePath, text.ToString(), new UTF8Encoding(false));
            cached = null;
        }

        /// <summary>Why a tag cannot be used, or null.</summary>
        internal static string ValidateTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return "Enter a tag.";
            return TagPattern.IsMatch(tag) ? null : "A tag can hold only letters, digits and _.";
        }
    }
}
