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
    internal static class GitRemotes
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

    /// <summary>
    /// Files over Cloudflare's 100 MiB proxied upload limit: listing them, and
    /// taking them out of the commits not yet pushed. A null remote means none:
    /// every commit is unpushed. Uses no Unity API.
    /// </summary>
    internal static class GitLargeFiles
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
                    Check(GitRunner.RunWithInput(projectRoot, temporary, baseInfo, TimeoutMs, "update-index", "-z", "--index-info"),
                        "Removing the files from " + commit);
                    string tree = Run(projectRoot, temporary, "write-tree").Trim();
                    // A commit that only held these files is dropped.
                    if (current != null && tree == Run(projectRoot, null, "rev-parse", current + "^{tree}").Trim() &&
                        Run(projectRoot, null, "rev-parse", commit + "^{tree}").Trim() != tree)
                        continue;
                    current = Check(GitRunner.RunWithInput(projectRoot, null, Rewrite(Run(projectRoot, null, "cat-file", "commit", commit), tree, current),
                        TimeoutMs, "hash-object", "-t", "commit", "-w", "--stdin"), "Rewriting " + commit).Trim();
                }
                Run(projectRoot, null, "update-ref", "-m", "RiryTools AutoSync: drop large files", branch, current, head);
            }
            finally
            {
                File.Delete(temporary["GIT_INDEX_FILE"]);
            }
            // The index matches the new tip for these paths, so they are not committed again.
            Check(GitRunner.RunWithInput(projectRoot, null, baseInfo, TimeoutMs, "update-index", "-z", "--index-info"),
                "Unstaging the files");
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
            return Check(GitRunner.Run(projectRoot, environment, TimeoutMs, arguments), "git " + arguments[0]);
        }

        private static string Check(GitRunner.Result result, string action)
        {
            if (!result.Succeeded) throw new InvalidOperationException(result.Describe(action));
            return result.Output;
        }
    }

    /// <summary>
    /// Asks a remote's server whether Git LFS is enabled, without uploading
    /// anything: an LFS batch request for one object over HTTP(S), or
    /// git-lfs-authenticate over SSH. Uses no Unity API.
    /// </summary>
    internal static class GitLfsQuery
    {
        private const int TimeoutMs = 30000;
        private const string LfsMediaType = "application/vnd.git-lfs+json";
        private static readonly Regex ScpLike = new Regex(@"^(?:([^@/]+)@)?([^:/]+):(.+)$");
        private static readonly Regex SshUrl = new Regex(@"^ssh://(?:([^@/]+)@)?([^:/]+)(?::(\d+))?/(.+)$", RegexOptions.IgnoreCase);

        /// <summary>A line for the Configuration window.</summary>
        internal static string Ask(string projectRoot, string remote, string url)
        {
            try
            {
                if (GitRemotes.IsHttps(url)) return AskHttp(projectRoot, remote, url);
                Match ssh = SshUrl.Match(url);
                if (!ssh.Success) ssh = ScpLike.Match(url);
                if (ssh.Success) return AskSsh(projectRoot, remote, ssh);
                return "Git LFS was not checked: " + remote + " is not an HTTP(S) or SSH remote.";
            }
            catch (Exception exception)
            {
                return "Git LFS could not be checked on " + remote + ": " + exception.Message;
            }
        }

        private static string AskHttp(string projectRoot, string remote, string url)
        {
            string endpoint = Config(projectRoot, "remote." + remote + ".lfsurl") ?? Config(projectRoot, "lfs.url") ??
                (url.TrimEnd('/').EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? url.TrimEnd('/') : url.TrimEnd('/') + ".git") + "/info/lfs";
            var uri = new Uri(url);
            int status = Batch(endpoint, null);
            if (status == 401 || status == 403)
            {
                // Signed in through Git's credential manager, as git itself would.
                string request = "protocol=" + uri.Scheme + "\nhost=" + uri.Authority + "\npath=" + uri.AbsolutePath.TrimStart('/') + "\n\n";
                GitRunner.Result filled = GitRunner.RunWithInput(projectRoot, null, Encoding.UTF8.GetBytes(request), 600000, "credential", "fill");
                if (!filled.Succeeded) return "Git LFS could not be checked on " + remote + ": the server asked for a sign-in and none was given.";
                string user = Field(filled.Output, "username"), password = Field(filled.Output, "password");
                status = Batch(endpoint, Convert.ToBase64String(Encoding.UTF8.GetBytes(user + ":" + password)));
                GitRunner.RunWithInput(projectRoot, null, Encoding.UTF8.GetBytes(filled.Output.Replace("\r", "") + "\n"), 30000,
                    "credential", status == 401 || status == 403 ? "reject" : "approve");
            }
            if (status == 200) return "Git LFS is enabled on " + remote + ".";
            if (status == 404) return "Git LFS is not enabled on " + remote + " (the server has no LFS endpoint).";
            return "Git LFS could not be confirmed on " + remote + " (the server answered " + status + ").";
        }

        // A download request for one object that does not exist: an LFS server answers 200
        // with an error for that object; a server without LFS has no such endpoint.
        private static int Batch(string endpoint, string basicAuth)
        {
            var request = (HttpWebRequest)WebRequest.Create(endpoint.TrimEnd('/') + "/objects/batch");
            request.Method = "POST";
            request.Timeout = TimeoutMs;
            request.Accept = LfsMediaType;
            request.ContentType = LfsMediaType;
            if (basicAuth != null) request.Headers[HttpRequestHeader.Authorization] = "Basic " + basicAuth;
            byte[] body = Encoding.UTF8.GetBytes("{\"operation\":\"download\",\"transfers\":[\"basic\"],\"objects\":[{\"oid\":\"" +
                new string('0', 64) + "\",\"size\":0}]}");
            using (Stream stream = request.GetRequestStream()) stream.Write(body, 0, body.Length);
            try
            {
                using (var response = (HttpWebResponse)request.GetResponse()) return (int)response.StatusCode;
            }
            catch (WebException exception) when (exception.Response is HttpWebResponse response)
            {
                using (response) return (int)response.StatusCode;
            }
        }

        private static string AskSsh(string projectRoot, string remote, Match ssh)
        {
            string user = ssh.Groups[1].Value, host = ssh.Groups[2].Value;
            string port = ssh.Groups.Count > 4 ? ssh.Groups[3].Value : "";
            string path = ssh.Groups[ssh.Groups.Count > 4 ? 4 : 3].Value;
            var arguments = new List<string> { "-o", "BatchMode=yes" };
            if (port.Length > 0) arguments.AddRange(new[] { "-p", port });
            arguments.Add(user.Length > 0 ? user + "@" + host : host);
            arguments.Add("git-lfs-authenticate " + path + " download");
            string command = Config(projectRoot, "core.sshCommand") ?? "ssh";
            var info = new System.Diagnostics.ProcessStartInfo(command, BetterFbxExporter.JoinArguments(arguments))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (var process = System.Diagnostics.Process.Start(info))
            {
                string output = process.StandardOutput.ReadToEnd();
                process.StandardError.ReadToEnd();
                if (!process.WaitForExit(TimeoutMs)) process.Kill();
                if (process.ExitCode == 0 && output.Contains("\"href\"")) return "Git LFS is enabled on " + remote + ".";
                return "Git LFS is not enabled on " + remote + " over SSH (git-lfs-authenticate was refused).";
            }
        }

        private static string Config(string projectRoot, string key)
        {
            GitRunner.Result result = GitRunner.Run(projectRoot, null, 10000, "config", "--get", key);
            return result.Succeeded && result.Output.Trim().Length > 0 ? result.Output.Trim() : null;
        }

        private static string Field(string credential, string name)
        {
            foreach (string line in credential.Replace("\r", "").Split('\n'))
            {
                if (line.StartsWith(name + "=", StringComparison.Ordinal)) return line.Substring(name.Length + 1);
            }
            return "";
        }
    }
}
