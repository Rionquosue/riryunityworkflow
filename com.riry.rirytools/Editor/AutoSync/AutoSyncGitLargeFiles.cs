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
    /// Files over Cloudflare's 100 MiB proxied upload limit: listing them, and
    /// taking them out of the commits not yet pushed. A null remote means none:
    /// every commit is unpushed. Uses no Unity API.
    /// </summary>
    internal static class AutoSyncGitLargeFiles
    {
        // Cloudflare's proxied request limit on Free and Pro plans.
        internal const long ProxyLimitBytes = 100L * 1024L * 1024L;
        private const int TimeoutMs = 120000;

        /// <summary>Every file Git tracks that is over the limit on disk.</summary>
        internal static List<string> Tracked(string projectRoot)
        {
            GitRunner.Result files = GitRunner.Run(projectRoot, null, TimeoutMs, "ls-files", "-z");
            return OverLimit(projectRoot, files.Succeeded ? files.Output.Split('\0') : new string[0]);
        }

        /// <summary>Files over the limit that the commits not yet on <paramref name="remote"/> add or change.</summary>
        internal static List<string> Unpushed(string projectRoot, string remote)
        {
            var arguments = new List<string> { "log", "-z", "--name-only", "--format=", "--no-renames" };
            arguments.AddRange(NotPushed(remote));
            GitRunner.Result files = GitRunner.Run(projectRoot, null, TimeoutMs, arguments.ToArray());
            return OverLimit(projectRoot, files.Succeeded ? files.Output.Split(new[] { '\0', '\n' }) : new string[0]);
        }

        // HEAD's commits that no branch of the remote has; every commit without a remote.
        private static string[] NotPushed(string remote)
        {
            return remote == null ? new[] { "HEAD" } : new[] { "HEAD", "--not", "--remotes=" + remote };
        }

        private static List<string> OverLimit(string projectRoot, IEnumerable<string> paths)
        {
            var large = new List<string>();
            foreach (string path in paths)
            {
                if (path.Length == 0 || large.Contains(path)) continue;
                var file = new FileInfo(Path.Combine(projectRoot, path));
                if (file.Exists && file.Length > ProxyLimitBytes) large.Add(path);
            }
            return large;
        }

        /// <summary>
        /// Adds each path to .gitignore and takes it out of every commit not yet on
        /// <paramref name="remote"/>: those commits are rewritten with the path as it
        /// was before them (absent when it is new), and so is the index. Files stay on disk.
        /// </summary>
        internal static void Ignore(string projectRoot, string remote, List<string> paths)
        {
            if (paths.Count == 0) return;
            string branch = Run(projectRoot, null, "symbolic-ref", "-q", "HEAD").Trim();
            if (branch.Length == 0) throw new InvalidOperationException("HEAD is detached; switch to a branch first.");

            var list = new List<string> { "rev-list", "--reverse" };
            list.AddRange(NotPushed(remote));
            string[] commits = Run(projectRoot, null, list.ToArray()).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            list[1] = "--merges";
            if (Run(projectRoot, null, list.ToArray()).Trim().Length > 0)
                throw new InvalidOperationException("The unpushed commits include a merge, which cannot be rewritten here.");

            AppendToGitignore(projectRoot, paths);
            if (commits.Length == 0) return;

            string head = Run(projectRoot, null, "rev-parse", "HEAD").Trim();
            GitRunner.Result first = GitRunner.Run(projectRoot, null, TimeoutMs, "rev-parse", "-q", "--verify", commits[0] + "^");
            string parent = first.Succeeded ? first.Output.Trim() : null;
            byte[] baseInfo = BaseIndexInfo(projectRoot, parent, paths);

            var temporary = new Dictionary<string, string>
            {
                { "GIT_INDEX_FILE", Path.Combine(RiryToolsPaths.Library("AutoSync"), "rewrite-index") }
            };
            Directory.CreateDirectory(RiryToolsPaths.Library("AutoSync"));
            try
            {
                string current = parent;
                foreach (string commit in commits)
                {
                    Run(projectRoot, temporary, "read-tree", commit);
                    GitRunner.RunWithInput(projectRoot, temporary, baseInfo, TimeoutMs, "update-index", "-z", "--index-info").RequireSuccess("Removing the files from " + commit);
                    string tree = Run(projectRoot, temporary, "write-tree").Trim();
                    // A commit that only held these files is dropped.
                    if (current != null && tree == Run(projectRoot, null, "rev-parse", current + "^{tree}").Trim() &&
                        Run(projectRoot, null, "rev-parse", commit + "^{tree}").Trim() != tree)
                        continue;
                    current = GitRunner.RunWithInput(projectRoot, null, Rewrite(Run(projectRoot, null, "cat-file", "commit", commit), tree, current),
                        TimeoutMs, "hash-object", "-t", "commit", "-w", "--stdin").RequireSuccess("Rewriting " + commit).Trim();
                }
                Run(projectRoot, null, "update-ref", "-m", "RiryTools AutoSync: drop large files", branch, current, head);
                RiryLog.Info(RiryLog.AutoSync, "Rewrote the unpushed history of " + branch + ": " + head + " -> " + (current ?? "(no commits)") +
                    ", taking out " + string.Join(", ", paths) + ". The old commits stay in git reflog.");
            }
            finally
            {
                File.Delete(temporary["GIT_INDEX_FILE"]);
            }
            // The index matches the new tip for these paths, so they are not committed again.
            GitRunner.RunWithInput(projectRoot, null, baseInfo, TimeoutMs, "update-index", "-z", "--index-info").RequireSuccess("Unstaging the files");
        }

        // update-index --index-info lines that put each path back as it was in parent, or remove it.
        private static byte[] BaseIndexInfo(string projectRoot, string parent, List<string> paths)
        {
            var info = new StringBuilder();
            foreach (string path in paths)
            {
                string entry = parent == null ? "" :
                    GitRunner.Run(projectRoot, new Dictionary<string, string> { { "GIT_LITERAL_PATHSPECS", "1" } }, TimeoutMs,
                        "ls-tree", "-z", parent, "--", path).Output.TrimEnd('\0');
                // ls-tree: "<mode> blob <id>\t<path>"; --index-info takes the same line.
                if (entry.Length > 0) info.Append(entry).Append('\0');
                else info.Append("0 ").Append('0', 40).Append('\t').Append(path).Append('\0');
            }
            return Encoding.UTF8.GetBytes(info.ToString());
        }

        // The raw commit with a new tree and parent; a signature no longer matches, so it is left out.
        private static byte[] Rewrite(string raw, string tree, string parent)
        {
            int split = raw.IndexOf("\n\n", StringComparison.Ordinal);
            var headers = new StringBuilder("tree " + tree + "\n");
            if (parent != null) headers.Append("parent ").Append(parent).Append('\n');
            bool inSignature = false;
            foreach (string line in raw.Substring(0, split).Split('\n'))
            {
                if (inSignature && line.StartsWith(" ", StringComparison.Ordinal)) continue;
                inSignature = line.StartsWith("gpgsig", StringComparison.Ordinal);
                if (inSignature || line.StartsWith("tree ", StringComparison.Ordinal) || line.StartsWith("parent ", StringComparison.Ordinal))
                    continue;
                headers.Append(line).Append('\n');
            }
            return Encoding.UTF8.GetBytes(headers + raw.Substring(split + 1));
        }

        private static void AppendToGitignore(string projectRoot, List<string> paths)
        {
            string file = Path.Combine(projectRoot, ".gitignore");
            string text = File.Exists(file) ? File.ReadAllText(file) : "";
            var added = new StringBuilder(text.Length == 0 || text.EndsWith("\n", StringComparison.Ordinal) ? "" : "\n");
            foreach (string path in paths)
            {
                // Anchored to the project folder, with pattern characters escaped.
                string line = "/" + Regex.Replace(path, @"[\\*?\[\]!# ]", @"\$0");
                if (Array.IndexOf(text.Replace("\r", "").Split('\n'), line) < 0) added.Append(line).Append('\n');
            }
            File.AppendAllText(file, added.ToString(), new UTF8Encoding(false));
        }

        private static string Run(string projectRoot, Dictionary<string, string> environment, params string[] arguments)
        {
            return GitRunner.Run(projectRoot, environment, TimeoutMs, arguments).RequireSuccess("git " + arguments[0]);
        }
    }
}
