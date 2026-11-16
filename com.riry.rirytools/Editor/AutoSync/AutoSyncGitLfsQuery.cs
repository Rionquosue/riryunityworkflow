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
    /// Asks a remote's server whether Git LFS is enabled, without uploading
    /// anything: an LFS batch request for one object over HTTP(S), or
    /// git-lfs-authenticate over SSH. Uses no Unity API.
    /// </summary>
    internal static class AutoSyncGitLfsQuery
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
                if (AutoSyncGitRemotes.IsHttps(url)) return AskHttp(projectRoot, remote, url);
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
            var info = new System.Diagnostics.ProcessStartInfo(command, ImportPresetBetterFBX.JoinArguments(arguments))
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
