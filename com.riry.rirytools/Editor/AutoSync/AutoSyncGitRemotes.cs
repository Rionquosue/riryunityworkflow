using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// The project's remotes and which one Sync pushes to. The choice is per user
    /// and per project (UserSettings); without one, origin, else the first remote.
    /// </summary>
    internal static class AutoSyncGitRemotes
    {
        internal const string DefaultName = "origin";
        private const string SelectedKey = "RiryTools.AutoSync.Remote";

        /// <summary>Remote names, in Git's order.</summary>
        internal static List<string> Names(string projectRoot)
        {
            var names = new List<string>();
            foreach (string line in GitRunner.Run(projectRoot, null, 10000, "remote").Output.Replace("\r", "").Split('\n'))
            {
                if (line.Trim().Length > 0) names.Add(line.Trim());
            }
            return names;
        }

        internal static string Url(string projectRoot, string name)
        {
            GitRunner.Result result = GitRunner.Run(projectRoot, null, 10000, "remote", "get-url", name);
            return result.Succeeded ? result.Output.Trim() : "";
        }

        /// <summary>The remote Sync pushes to, or null without remotes. Main thread only.</summary>
        internal static string Selected(List<string> names)
        {
            string stored = EditorUserSettings.GetConfigValue(SelectedKey);
            if (!string.IsNullOrEmpty(stored) && names.Contains(stored)) return stored;
            if (names.Contains(DefaultName)) return DefaultName;
            return names.Count > 0 ? names[0] : null;
        }

        internal static void Select(string name)
        {
            EditorUserSettings.SetConfigValue(SelectedKey, name);
        }

        internal static bool IsHttps(string url)
        {
            return url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
        }
    }
}
