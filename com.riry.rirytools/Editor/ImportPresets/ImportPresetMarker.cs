using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// ImportPreset.md in a folder sets import presets for new files directly in
    /// that folder; subfolders are not affected. Lines:
    ///   Image files: Config/Preset      one per file type (ImportPresetFolderRules.Label)
    ///   _normal files: Config/Preset    one per decorator: files whose name contains _normal
    ///   betterfbx: blend                export .blend files with Better FBX next to them
    /// A file type with no line gets no preset. The tool only rewrites or deletes a
    /// marker whose text is exactly what it generated.
    /// </summary>
    internal static class ImportPresetMarker
    {
        internal const string FileName = "ImportPreset.md";
        internal const string BetterFbxKey = "betterfbx";

        internal struct Target
        {
            /// <summary>Null means no preset for this file type.</summary>
            public string Configuration;
            /// <summary>Preset file in the configuration, or null to use its rules.</summary>
            public string Preset;
        }

        internal sealed class TypeRule
        {
            // A file type, such as "Image files".
            public string Label;
            public string Configuration;
            public string Preset;
        }

        internal sealed class Rules
        {
            public readonly List<TypeRule> TypeRules = new List<TypeRule>();
            public readonly List<string> BetterFbx = new List<string>();

            /// <summary>The preset for a file type, or a Target with no configuration.</summary>
            public Target For(string label)
            {
                TypeRule rule = TypeRules.Find(item => string.Equals(item.Label, label, StringComparison.OrdinalIgnoreCase));
                return rule != null ? new Target { Configuration = rule.Configuration, Preset = rule.Preset } : new Target();
            }

            /// <summary>The preset for a file: its decorator's type when that has one, else its plain type's.</summary>
            public Target For(AssetImporter importer)
            {
                return ImportPresetFolderRules.Effective(this, importer,
                    ImportPresetFolderRules.Decorators(ParentFolder(importer.assetPath) ?? ""));
            }

            /// <summary>Sets a file type's preset; a null configuration removes it.</summary>
            public void Set(string label, string configuration, string preset)
            {
                TypeRules.RemoveAll(item => string.Equals(item.Label, label, StringComparison.OrdinalIgnoreCase));
                if (configuration != null) TypeRules.Add(new TypeRule { Label = label, Configuration = configuration, Preset = preset });
                TypeRules.Sort((left, right) => string.Compare(left.Label, right.Label, StringComparison.OrdinalIgnoreCase));
            }

            public bool IsEmpty
            {
                get { return TypeRules.Count == 0 && BetterFbx.Count == 0; }
            }
        }

        private static readonly Regex RuleLine = new Regex(@"^\s*([A-Za-z0-9_.,\s]+?)\s*:\s*(.*?)\s*$",
            RegexOptions.CultureInvariant);
        private static readonly Regex ExtensionToken = new Regex(@"^[a-z0-9]+$", RegexOptions.CultureInvariant);

        internal static string Generate(Rules rules)
        {
            var text = new StringBuilder();
            text.Append("# Import preset\n\n" +
                "New files imported directly into this folder (not its subfolders) get import\n" +
                "settings from Documents/RiryTools/ImportPresets. Files that already have import\n" +
                "settings are never changed.\n\n" +
                "- `<file type>`: configuration/preset for that type of file\n" +
                "- `betterfbx`: types exported to FBX with Better FBX\n\n");
            foreach (TypeRule rule in rules.TypeRules)
            {
                if (rule.Configuration == null) continue;
                text.Append(rule.Label).Append(": ").Append(Value(rule.Configuration, rule.Preset)).Append('\n');
            }
            if (rules.BetterFbx.Count > 0)
                text.Append(BetterFbxKey).Append(": ").Append(string.Join(", ", rules.BetterFbx)).Append('\n');
            text.Append("\n<!-- Written by RiryTools. Once edited by hand, the tool leaves this file alone. -->\n");
            return text.ToString();
        }

        private static string Value(string configuration, string preset)
        {
            return preset == null ? configuration : configuration + "/" + StripPresetExtension(preset);
        }

        /// <summary>Reads rule lines; prose, headings, lists and comments are ignored.</summary>
        internal static Rules Parse(string content)
        {
            var rules = new Rules();
            if (string.IsNullOrEmpty(content)) return rules;
            bool inComment = false;
            foreach (string rawLine in content.Replace("\r", "").Split('\n'))
            {
                string line = rawLine;
                if (inComment)
                {
                    int end = line.IndexOf("-->", StringComparison.Ordinal);
                    if (end < 0) continue;
                    line = line.Substring(end + 3);
                    inComment = false;
                }
                int start = line.IndexOf("<!--", StringComparison.Ordinal);
                if (start >= 0)
                {
                    int end = line.IndexOf("-->", start, StringComparison.Ordinal);
                    inComment = end < 0;
                    line = line.Substring(0, start) + (end < 0 ? "" : line.Substring(end + 3));
                }

                Match match = RuleLine.Match(line);
                if (!match.Success) continue;
                string key = match.Groups[1].Value.Trim();
                string value = match.Groups[2].Value.Trim().Trim('`', '*', '_', '"', '\'').Trim();
                if (value.Length == 0) continue;

                if (string.Equals(key, BetterFbxKey, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (string extension in ParseExtensions(value))
                        if (!rules.BetterFbx.Contains(extension)) rules.BetterFbx.Add(extension);
                    continue;
                }

                string configuration;
                string preset;
                if (rules.For(key).Configuration != null || !TryParseValue(value, out configuration, out preset)) continue;
                rules.TypeRules.Add(new TypeRule { Label = key, Configuration = configuration, Preset = preset });
            }
            return rules;
        }

        private static bool TryParseValue(string value, out string configuration, out string preset)
        {
            preset = null;
            int slash = value.IndexOf('/');
            configuration = (slash < 0 ? value : value.Substring(0, slash)).Trim();
            if (slash >= 0)
            {
                preset = StripPresetExtension(value.Substring(slash + 1).Trim());
                if (preset.Length == 0) preset = null;
            }
            return configuration.Length > 0;
        }

        /// <summary>Lower-case extensions without dots; invalid tokens make the list empty.</summary>
        internal static List<string> ParseExtensions(string text)
        {
            var extensions = new List<string>();
            foreach (string token in text.Split(','))
            {
                string extension = ImportPresetLibrary.NormalizeExtension(token);
                if (!ExtensionToken.IsMatch(extension)) return new List<string>();
                if (!extensions.Contains(extension)) extensions.Add(extension);
            }
            return extensions;
        }

        private static string StripPresetExtension(string preset)
        {
            return preset.EndsWith(ImportPresetLibrary.PresetExtension, StringComparison.OrdinalIgnoreCase)
                ? preset.Substring(0, preset.Length - ImportPresetLibrary.PresetExtension.Length)
                : preset;
        }

        /// <summary>True when the text is unchanged tool output (line endings aside).</summary>
        internal static bool IsGenerated(string content)
        {
            if (string.IsNullOrEmpty(content)) return false;
            Rules rules = Parse(content);
            return !rules.IsEmpty && Normalize(content) == Normalize(Generate(rules));
        }

        private static string Normalize(string content)
        {
            return content.Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd();
        }

        internal static string PathIn(string folder)
        {
            return folder.TrimEnd('/') + "/" + FileName;
        }

        internal static string ParentFolder(string path)
        {
            int slash = path.Replace('\\', '/').TrimEnd('/').LastIndexOf('/');
            return slash > 0 ? path.Substring(0, slash) : null;
        }

        /// <summary>Rules of a folder's marker, or null with a warning when unreadable or empty.</summary>
        internal static Rules ReadRules(string folder)
        {
            string path = PathIn(folder);
            string content;
            try
            {
                content = File.ReadAllText(path);
            }
            catch (Exception exception)
            {
                ImportPresetLibrary.ReportOnce("read:" + path, "Could not read " + path + ": " + exception.Message);
                return null;
            }
            Rules rules = Parse(content);
            if (rules.IsEmpty)
            {
                ImportPresetLibrary.ReportOnce("parse:" + path + content.GetHashCode(),
                    path + " has no rule lines such as 'Image files: <configuration>/<preset>'.");
                return null;
            }
            return rules;
        }

        /// <summary>Rules of the marker in the asset's own folder (not a folder above it), or null.</summary>
        internal static Rules RulesFor(string assetPath)
        {
            string folder = ParentFolder(assetPath);
            return folder != null && File.Exists(PathIn(folder)) ? ReadRules(folder) : null;
        }
    }
}
