using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Remembers the size and write time of files this tool wrote (exported FBX
    /// files and extracted clips), and for a clip the GUID of the model it came
    /// from. A file is replaced automatically only while it still matches; once
    /// changed by hand, or never written by the tool, it stays. A clip made from
    /// another model is never replaced (OwnedByOther).
    /// </summary>
    internal static class ImportPresetOwnedFiles
    {
        private static string StorePath
        {
            get { return Path.Combine(RiryToolsPaths.Library("ImportPresets"), "owned.txt"); }
        }

        /// <summary>Records the file as written now; <paramref name="owner"/> is the GUID of the model a clip came from.</summary>
        internal static void Record(string path, string owner = "")
        {
            // A path that cannot be stored safely is simply never treated as owned.
            if (path.IndexOfAny(new[] { '\t', '\n', '\r' }) >= 0 || !File.Exists(path)) return;
            Dictionary<string, string> entries = Load();
            entries[path] = Stamp(path) + (string.IsNullOrEmpty(owner) ? "" : "\t" + owner);
            Save(entries);
        }

        internal static bool IsUnchanged(string path)
        {
            if (!File.Exists(path)) return false;
            string entry;
            return Load().TryGetValue(path, out entry) && StampOf(entry) == Stamp(path);
        }

        /// <summary>
        /// True when the file was recorded as made from a model other than <paramref name="owner"/> (a GUID). Entries
        /// recorded before owners were kept have none, and count as no other model's.
        /// </summary>
        internal static bool OwnedByOther(string path, string owner)
        {
            string entry;
            if (!Load().TryGetValue(path, out entry)) return false;
            string recorded = OwnerOf(entry);
            // A model that is gone owns nothing any more.
            return recorded.Length > 0 && recorded != owner && !string.IsNullOrEmpty(UnityEditor.AssetDatabase.GUIDToAssetPath(recorded));
        }

        /// <summary>The asset path of the model the file was recorded as made from, or "".</summary>
        internal static string OwnerPath(string path)
        {
            string entry;
            return Load().TryGetValue(path, out entry) ? UnityEditor.AssetDatabase.GUIDToAssetPath(OwnerOf(entry)) : "";
        }

        // An entry is "length<TAB>ticks" with an optional "<TAB>owner GUID".
        private static string StampOf(string entry)
        {
            int second = entry.IndexOf('\t');
            int third = second < 0 ? -1 : entry.IndexOf('\t', second + 1);
            return third < 0 ? entry : entry.Substring(0, third);
        }

        private static string OwnerOf(string entry)
        {
            string stamp = StampOf(entry);
            return stamp.Length < entry.Length ? entry.Substring(stamp.Length + 1) : "";
        }

        private static string Stamp(string path)
        {
            var info = new FileInfo(path);
            return info.Length + "\t" + info.LastWriteTimeUtc.Ticks;
        }

        private static Dictionary<string, string> Load()
        {
            var entries = new Dictionary<string, string>(StringComparer.Ordinal);
            string store = StorePath;
            if (!File.Exists(store)) return entries;
            foreach (string line in File.ReadAllLines(store))
            {
                int tab = line.IndexOf('\t');
                if (tab > 0) entries[line.Substring(0, tab)] = line.Substring(tab + 1);
            }
            return entries;
        }

        private static void Save(Dictionary<string, string> entries)
        {
            string store = StorePath;
            Directory.CreateDirectory(Path.GetDirectoryName(store));
            var text = new StringBuilder();
            foreach (KeyValuePair<string, string> entry in entries)
            {
                if (File.Exists(entry.Key)) text.Append(entry.Key).Append('\t').Append(entry.Value).Append('\n');
            }
            File.WriteAllText(store, text.ToString(), new UTF8Encoding(false));
        }
    }
}
