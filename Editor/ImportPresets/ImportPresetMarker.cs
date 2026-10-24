#if UNITY_EDITOR
// Target editor: Unity 2022.3 LTS (including 2022.3.22f1).
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Riry.UnityWorkflow.Editor
{
    /// <summary>
    /// ImportPreset.md in a folder names the configuration for new files in that
    /// folder and its subfolders; the nearest marker wins. The tool only rewrites
    /// or deletes a marker whose text is exactly what it generated.
    /// </summary>
    internal static class ImportPresetMarker
    {
        internal const string FileName = "ImportPreset.md";

        private static readonly Regex PresetLine = new Regex(@"^\s*preset\s*:\s*(.*?)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline);

        internal static string Generate(string configuration)
        {
            return "# Import preset\n\n" +
                "New files imported into this folder and its subfolders use the import preset\n" +
                "configuration below, from Documents/UnityTools/ImportPresets. Files that already\n" +
                "have import settings are never changed. A closer ImportPreset.md overrides this one.\n\n" +
                "preset: " + configuration + "\n\n" +
                "<!-- Written by Riry Unity Workflow. Once edited by hand, the tool leaves this file alone. -->\n";
        }

        /// <summary>The configuration named by the first "preset:" line, or null.</summary>
        internal static string Parse(string content)
        {
            if (string.IsNullOrEmpty(content)) return null;
            Match match = PresetLine.Match(content.Replace("\r", ""));
            if (!match.Success) return null;
            // Allow Markdown emphasis or quotes around the name.
            string name = match.Groups[1].Value.Trim().Trim('`', '*', '_', '"', '\'').Trim();
            return name.Length > 0 ? name : null;
        }

        /// <summary>True when the text is unchanged tool output (line endings aside).</summary>
        internal static bool IsGenerated(string content)
        {
            string name = Parse(content);
            return name != null && Normalize(content) == Normalize(Generate(name));
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

        /// <summary>Configuration named by a folder's marker, or null with a warning.</summary>
        internal static string ReadConfiguration(string folder)
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
            string name = Parse(content);
            if (name == null)
                ImportPresetLibrary.ReportOnce("parse:" + path + content.GetHashCode(),
                    path + " has no 'preset: <configuration>' line.");
            return name;
        }
    }
}
#endif
