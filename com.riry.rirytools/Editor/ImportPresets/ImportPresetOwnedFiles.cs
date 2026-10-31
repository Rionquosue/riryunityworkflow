using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Remembers the size and write time of files this tool wrote (exported FBX
    /// files and extracted clips). A file is replaced automatically only while it
    /// still matches; once changed by hand, or never written by the tool, it stays.
    /// </summary>
    internal static class ImportPresetOwnedFiles
    {
        private static string StorePath
        {
            get { return Path.Combine(RiryToolsPaths.Library("ImportPresets"), "owned.txt"); }
        }

        internal static void Record(string path)
        {
            // A path that cannot be stored safely is simply never treated as owned.
            if (path.IndexOfAny(new[] { '\t', '\n', '\r' }) >= 0 || !File.Exists(path)) return;
            Dictionary<string, string> entries = Load();
            entries[path] = Stamp(path);
            Save(entries);
        }

        internal static bool IsUnchanged(string path)
        {
            if (!File.Exists(path)) return false;
            string stamp;
            return Load().TryGetValue(path, out stamp) && stamp == Stamp(path);
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
