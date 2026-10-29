using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// ImportPreset.md in a folder sets import presets for new files in that
    /// folder and its subfolders; the nearest marker wins. Lines:
    ///   preset: Config                  default for unlisted file types
    ///   fbx, obj: Config[/Preset]       per file type, or "none"
    ///   animations: fbx                 extract clips before the preset applies
    ///   betterfbx: blend                export with Better FBX next to the file
    /// The tool only rewrites or deletes a marker whose text is exactly what it generated.
    /// </summary>
    internal static class ImportPresetMarker
    {
        internal const string FileName = "ImportPreset.md";
        internal const string DefaultKey = "preset";
        internal const string AnimationsKey = "animations";
        internal const string BetterFbxKey = "betterfbx";
        private const string NoneValue = "none";

        internal struct Target
        {
            /// <summary>Null means no preset for this file type.</summary>
            public string Configuration;
            /// <summary>Preset file in the configuration, or null to use its rules.</summary>
            public string Preset;
        }

        internal sealed class TypeRule
        {
            public List<string> Extensions = new List<string>();
            public string Configuration;
            public string Preset;
        }

        internal sealed class Rules
        {
            public string DefaultConfiguration;
            public string DefaultPreset;
            public readonly List<TypeRule> TypeRules = new List<TypeRule>();
            public readonly List<string> ExtractAnimations = new List<string>();
            public readonly List<string> BetterFbx = new List<string>();

            /// <summary>The first rule listing the extension, else the default.</summary>
            public Target For(string extension)
            {
                string wanted = ImportPresetLibrary.NormalizeExtension(extension);
                foreach (TypeRule rule in TypeRules)
                {
                    if (rule.Extensions.Contains(wanted))
                        return new Target { Configuration = rule.Configuration, Preset = rule.Preset };
                }
                return new Target { Configuration = DefaultConfiguration, Preset = DefaultPreset };
            }

            public bool IsEmpty
            {
                get
                {
                    return DefaultConfiguration == null && TypeRules.Count == 0 &&
                        ExtractAnimations.Count == 0 && BetterFbx.Count == 0;
                }
            }
        }

        private static readonly Regex RuleLine = new Regex(@"^\s*([A-Za-z0-9.,\s]+?)\s*:\s*(.*?)\s*$",
            RegexOptions.CultureInvariant);
        private static readonly Regex ExtensionToken = new Regex(@"^[a-z0-9]+$", RegexOptions.CultureInvariant);

        internal static string Generate(Rules rules)
        {
            var text = new StringBuilder();
            text.Append("# Import preset\n\n" +
                "New files imported into this folder and its subfolders get import settings from\n" +
                "Documents/RiryTools/ImportPresets. Files that already have import settings are\n" +
                "never changed. A closer ImportPreset.md overrides this one.\n\n" +
                "- `preset`: configuration for file types not listed below\n" +
                "- `ext, ext`: configuration, or configuration/preset, for those types; or `none`\n" +
                "- `animations`: model types whose clips are copied to Animations/ before the preset applies\n" +
                "- `betterfbx`: types exported to FBX with Better FBX; the FBX follows the `fbx` line\n\n");
            if (rules.DefaultConfiguration != null)
                text.Append(DefaultKey).Append(": ").Append(Value(rules.DefaultConfiguration, rules.DefaultPreset)).Append('\n');
            foreach (TypeRule rule in rules.TypeRules)
            {
                if (rule.Extensions.Count == 0) continue;
                text.Append(string.Join(", ", rule.Extensions)).Append(": ")
                    .Append(Value(rule.Configuration, rule.Preset)).Append('\n');
            }
            if (rules.ExtractAnimations.Count > 0)
                text.Append(AnimationsKey).Append(": ").Append(string.Join(", ", rules.ExtractAnimations)).Append('\n');
            if (rules.BetterFbx.Count > 0)
                text.Append(BetterFbxKey).Append(": ").Append(string.Join(", ", rules.BetterFbx)).Append('\n');
            text.Append("\n<!-- Written by RiryTools. Once edited by hand, the tool leaves this file alone. -->\n");
            return text.ToString();
        }

        private static string Value(string configuration, string preset)
        {
            if (configuration == null) return NoneValue;
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
                string key = match.Groups[1].Value.Trim().ToLowerInvariant();
                string value = match.Groups[2].Value.Trim().Trim('`', '*', '_', '"', '\'').Trim();
                if (value.Length == 0) continue;

                if (key == DefaultKey)
                {
                    if (rules.DefaultConfiguration != null) continue;
                    string configuration;
                    string preset;
                    if (TryParseValue(value, out configuration, out preset))
                    {
                        rules.DefaultConfiguration = configuration;
                        rules.DefaultPreset = preset;
                    }
                    continue;
                }
                if (key == AnimationsKey || key == BetterFbxKey)
                {
                    List<string> target = key == AnimationsKey ? rules.ExtractAnimations : rules.BetterFbx;
                    foreach (string extension in ParseExtensions(value))
                        if (!target.Contains(extension)) target.Add(extension);
                    continue;
                }

                List<string> extensions = ParseExtensions(key);
                if (extensions.Count == 0) continue;
                var rule = new TypeRule { Extensions = extensions };
                if (!string.Equals(value, NoneValue, StringComparison.OrdinalIgnoreCase) &&
                    !TryParseValue(value, out rule.Configuration, out rule.Preset))
                    continue;
                rules.TypeRules.Add(rule);
            }
            return rules;
        }

        private static bool TryParseValue(string value, out string configuration, out string preset)
        {
            configuration = null;
            preset = null;
            if (string.Equals(value, NoneValue, StringComparison.OrdinalIgnoreCase)) return true;
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

        /// <summary>Nearest folder at or above the asset's folder that holds a marker.</summary>
        internal static string FindMarkerFolder(string assetPath, Func<string, bool> exists)
        {
            string folder = ParentFolder(assetPath);
            while (!string.IsNullOrEmpty(folder))
            {
                if (exists(PathIn(folder))) return folder;
                folder = ParentFolder(folder);
            }
            return null;
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
                    path + " has no rule lines such as 'preset: <configuration>' or 'png: <configuration>'.");
                return null;
            }
            return rules;
        }

        /// <summary>Rules governing an asset path, or null.</summary>
        internal static Rules RulesFor(string assetPath)
        {
            string folder = FindMarkerFolder(assetPath, File.Exists);
            return folder != null ? ReadRules(folder) : null;
        }
    }
}
