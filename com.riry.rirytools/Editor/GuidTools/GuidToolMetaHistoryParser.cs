using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Reads `git log -p` output for .meta files, newest first, and keeps the most
    /// recent project path that held each wanted GUID, with a commit that has it.
    /// </summary>
    internal sealed class GuidToolMetaHistoryParser
    {
        internal struct Hit
        {
            public string Path;
            /// <summary>A commit holding the .meta with that GUID: the commit that added it, or the parent of the one that removed it.</summary>
            public string Commit;
        }

        private static readonly Regex CommitLine = new Regex("^[0-9a-f]{40}([0-9a-f]{24})?$", RegexOptions.CultureInvariant);
        private readonly HashSet<string> wanted;
        private readonly string prefix;
        private string commit;
        private string path;
        internal readonly Dictionary<string, Hit> Found = new Dictionary<string, Hit>(StringComparer.OrdinalIgnoreCase);

        /// <param name="prefix">The project folder relative to the repository root, such as "Game/" or "".</param>
        internal GuidToolMetaHistoryParser(HashSet<string> wanted, string prefix)
        {
            this.wanted = wanted;
            this.prefix = prefix;
        }

        internal bool Complete
        {
            get { return Found.Count == wanted.Count; }
        }

        internal void Feed(string line)
        {
            if (CommitLine.IsMatch(line))
            {
                commit = line;
                path = null;
            }
            else if (line.StartsWith("diff --git ", StringComparison.Ordinal)) path = null;
            // Git ends a header path that contains a space with a tab.
            else if (line.StartsWith("--- a/", StringComparison.Ordinal) || line.StartsWith("+++ b/", StringComparison.Ordinal))
                path = line.Substring(6).TrimEnd('\t');
            else if (path != null && commit != null &&
                     (line.StartsWith("+guid: ", StringComparison.Ordinal) || line.StartsWith("-guid: ", StringComparison.Ordinal)))
            {
                string guid = line.Substring(7).Trim();
                if (wanted.Contains(guid) && !Found.ContainsKey(guid) &&
                    path.StartsWith(prefix, StringComparison.Ordinal) && path.EndsWith(".meta", StringComparison.Ordinal))
                    Found[guid] = new Hit
                    {
                        Path = path.Substring(prefix.Length, path.Length - prefix.Length - ".meta".Length),
                        Commit = line[0] == '+' ? commit : commit + "^"
                    };
            }
        }
    }
}

// ============================================================================
// GLOSSARY: .meta Git history parser
// ============================================================================
//
// GuidToolMetaHistoryParser (this file)
//   Feed(line) / Complete / Found  Reads `git log -p` output for .meta files, newest first, and keeps the most recent
//                                  project path that held each wanted GUID, with a commit that has it (Hit: Path, Commit).
