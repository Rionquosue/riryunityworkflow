using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Permanent record of every GUID the Regenerate GUID command replaced, kept
    /// in ProjectSettings so it is versioned with the project. Repair Missing GUID
    /// References follows it, and every version of it in Git history, to relink
    /// files restored from before a regeneration.
    /// </summary>
    internal static class GuidRemapLedger
    {
        internal const string RelativePath = "ProjectSettings/RiryToolsGuidRemap.tsv";
        private const int MaxChain = 64;
        private const string Header =
            "# RiryTools GUID remap ledger: old GUID, new GUID, asset path, UTC time. Keep this file in version control.\n";

        private static string FullPath
        {
            get { return Path.Combine(RiryToolsPaths.ProjectRoot, RelativePath); }
        }

        internal static void Append(IEnumerable<RegenerateGuid.Target> targets)
        {
            var text = new StringBuilder(File.Exists(FullPath) ? "" : Header);
            string time = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            foreach (RegenerateGuid.Target target in targets)
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
            foreach (string line in File.ReadAllLines(FullPath)) AddRecord(line, map, true);
            return map;
        }

        /// <summary>
        /// The current ledger plus every record any version of it ever held in Git
        /// (all branches, remotes and the reflog), so records lost when the file was
        /// deleted, reverted or overwritten still count. A current record wins over
        /// a past one, and a newer past record over an older one. Without Git, or
        /// outside a repository, this is the current ledger.
        /// </summary>
        internal static Dictionary<string, string> LoadWithHistory(GuidToolLog log)
        {
            Dictionary<string, string> map = Load();
            GitRunner.Result history;
            try
            {
                history = GitRunner.Run(RiryToolsPaths.ProjectRoot, null, 120000, "log", "--all", "--reflog", "-p",
                    "--format=", "--unified=0", "--no-color", "--no-ext-diff", "--", RelativePath);
            }
            catch (GitNotInstalledException exception)
            {
                log.Write("Ledger history not searched: " + exception.Message);
                return map;
            }
            if (!history.Succeeded)
            {
                log.Write("Ledger history not searched: " + history.Describe("Reading the ledger's Git history"));
                return map;
            }
            // Newest first. Added lines are records as written; removed lines were
            // records once, so they only fill what no added line holds.
            string[] lines = history.Output.Replace("\r", "").Split('\n');
            foreach (string marker in new[] { "+", "-" })
            {
                foreach (string line in lines)
                {
                    if (line.StartsWith(marker, StringComparison.Ordinal) && !line.StartsWith(marker + marker + marker, StringComparison.Ordinal))
                        AddRecord(line.Substring(1), map, false);
                }
            }
            return map;
        }

        private static void AddRecord(string line, Dictionary<string, string> map, bool replace)
        {
            string[] fields = line.Split('\t');
            if (fields.Length < 2 || line.StartsWith("#", StringComparison.Ordinal) ||
                fields[0].Length != RegenerateGuid.GuidLength || fields[1].Length != RegenerateGuid.GuidLength)
                return;
            if (replace || !map.ContainsKey(fields[0])) map[fields[0]] = fields[1];
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
