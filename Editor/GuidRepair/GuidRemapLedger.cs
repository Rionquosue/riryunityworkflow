#if UNITY_EDITOR
// Target editor: Unity 2022.3 LTS (including 2022.3.22f1).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Riry.UnityWorkflow.Editor
{
    /// <summary>
    /// Permanent record of every GUID the Regenerate GUID command replaced, kept
    /// in ProjectSettings so it is versioned with the project. Repair Missing GUID
    /// References follows it to relink files restored from before a regeneration.
    /// </summary>
    internal static class GuidRemapLedger
    {
        internal const string RelativePath = "ProjectSettings/RiryGuidRemap.tsv";
        private const int MaxChain = 64;
        private const string Header =
            "# Riry GUID remap ledger: old GUID, new GUID, asset path, UTC time. Keep this file in version control.\n";

        /// <summary>Tests point the ledger at a temporary file.</summary>
        internal static string PathOverride;

        private static string FullPath
        {
            get
            {
                return !string.IsNullOrEmpty(PathOverride)
                    ? PathOverride
                    : Path.Combine(RegenerateAssetGuid.ProjectRootPath, RelativePath);
            }
        }

        internal static void Append(IEnumerable<RegenerateAssetGuid.Target> targets)
        {
            var text = new StringBuilder(File.Exists(FullPath) ? "" : Header);
            string time = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            foreach (RegenerateAssetGuid.Target target in targets)
            {
                // The path is informational; tabs or line breaks in it must not split the record.
                text.Append(target.OldGuid).Append('\t').Append(target.NewGuid).Append('\t')
                    .Append(target.AssetPath.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' '))
                    .Append('\t').Append(time).Append('\n');
            }
            File.AppendAllText(FullPath, text.ToString(), new UTF8Encoding(false));
        }

        /// <summary>Old GUID to the GUID that replaced it; a later record wins.</summary>
        internal static Dictionary<string, string> Load()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(FullPath)) return map;
            foreach (string line in File.ReadAllLines(FullPath))
            {
                string[] fields = line.Split('\t');
                if (fields.Length >= 2 && !line.StartsWith("#", StringComparison.Ordinal) &&
                    fields[0].Length == RegenerateAssetGuid.GuidLength && fields[1].Length == RegenerateAssetGuid.GuidLength)
                    map[fields[0]] = fields[1];
            }
            return map;
        }

        /// <summary>
        /// Follows old to new GUIDs until one <paramref name="exists"/>; null when
        /// the chain ends, loops, or no GUID in it exists.
        /// </summary>
        internal static string Resolve(string guid, Dictionary<string, string> map, Func<string, bool> exists)
        {
            for (int step = 0; step < MaxChain && map.TryGetValue(guid, out guid); step++)
            {
                if (exists(guid)) return guid;
            }
            return null;
        }
    }
}
#endif
