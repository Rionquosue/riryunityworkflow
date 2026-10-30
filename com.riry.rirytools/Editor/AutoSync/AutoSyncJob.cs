using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// One Sync pass: commits each save to the checked-out branch, staging only the
    /// saved scenes and prefabs and their .meta files, then pushes when there is a
    /// remote. Other staged changes stay staged and out of the commit. Uses no
    /// Unity API, so it runs on a worker thread.
    /// </summary>
    internal sealed class AutoSyncJob
    {
        internal const string Remote = "origin";
        private const int TimeoutMs = 120000;
        // Long enough to sign in through the credential manager's window.
        private const int PushTimeoutMs = 600000;

        internal sealed class Save
        {
            internal readonly string Title;
            internal readonly List<string> Paths = new List<string>();

            internal Save(string title)
            {
                Title = title;
            }
        }

        internal string ProjectRoot;
        internal List<Save> Saves;
        internal bool Push;

        private readonly Dictionary<string, string> environment = new Dictionary<string, string>();

        /// <summary>Commits every save, then pushes when enabled. Throws on failure.</summary>
        internal void Execute()
        {
            Open();
            bool committed = false;
            foreach (Save save in Saves) committed |= Commit(save);
            if (Push && committed) PushBranch();
        }

        private void Open()
        {
            string[] location = GitState.Location(ProjectRoot);
            if (location == null)
                throw new InvalidOperationException("The project folder is not a Git repository. Turn Sync off and on to create one.");
            // The repository is always the whole project folder, never a folder above it.
            if (location[1].Length > 0)
                throw new InvalidOperationException("The Git repository must be the project folder itself (" + ProjectRoot +
                    "), but the project is inside the repository at " + location[0] + ".");
            // Paths are added literally, so names with * or [ are not patterns.
            environment["GIT_LITERAL_PATHSPECS"] = "1";
            // A repository without an identity still gets commits.
            if (Run("config", "user.name").Output.Trim().Length == 0)
                environment["GIT_AUTHOR_NAME"] = environment["GIT_COMMITTER_NAME"] = "RiryTools AutoSync";
            if (Run("config", "user.email").Output.Trim().Length == 0)
                environment["GIT_AUTHOR_EMAIL"] = environment["GIT_COMMITTER_EMAIL"] = "autosync@localhost";
        }

        /// <summary>Commits one save. Returns false when the saved files did not change.</summary>
        private bool Commit(Save save)
        {
            var paths = new List<string>();
            foreach (string path in save.Paths)
            {
                paths.Add(path);
                paths.Add(path + ".meta");
            }
            // Tracked files and untracked files that are not ignored; deleted files drop out.
            var list = new List<string> { "ls-files", "-z", "--cached", "--others", "--exclude-standard", "--" };
            list.AddRange(paths);
            var files = new List<string>();
            foreach (string file in Check(Run(list.ToArray()), "Listing the saved files").Split('\0'))
            {
                if (file.Length > 0 && !files.Contains(file) && File.Exists(Path.Combine(ProjectRoot, file))) files.Add(file);
            }
            if (files.Count == 0) return false;

            var add = new List<string> { "add", "--" };
            add.AddRange(files);
            Check(Run(add.ToArray()), "Staging the saved files");
            var diff = new List<string> { "diff", "--cached", "--quiet", "--" };
            diff.AddRange(files);
            if (Run(diff.ToArray()).Succeeded) return false;

            // With paths, git commit takes only those paths, whatever else is staged.
            var commit = new List<string> { "commit", "--quiet", "-m", save.Title, "--" };
            commit.AddRange(files);
            Check(Run(commit.ToArray()), "Committing " + save.Title);
            return true;
        }

        /// <summary>
        /// Pushes the checked-out branch to its upstream, else to origin (setting the
        /// upstream). Without either remote, or on a detached HEAD, nothing is pushed.
        /// Git asks its credential manager for any sign-in.
        /// </summary>
        private void PushBranch()
        {
            string branch = Run("symbolic-ref", "-q", "--short", "HEAD").Output.Trim();
            if (branch.Length == 0) return;
            GitRunner.Result pushed;
            if (Run("rev-parse", "-q", "--verify", "@{upstream}").Succeeded)
                pushed = GitRunner.Run(ProjectRoot, environment, PushTimeoutMs, "push", "--quiet");
            else if (Array.IndexOf(Run("remote").Output.Replace("\r", "").Split('\n'), Remote) >= 0)
                pushed = GitRunner.Run(ProjectRoot, environment, PushTimeoutMs, "push", "--quiet", "--set-upstream", Remote, branch);
            else
                return;
            Check(pushed, "Pushing " + branch);
        }

        private GitRunner.Result Run(params string[] arguments)
        {
            // Objects and refs reach the disk before git reports success, so a
            // power loss right after a commit keeps it (Git 2.36+; older Git ignores this).
            var all = new List<string> { "-c", "core.fsync=committed" };
            all.AddRange(arguments);
            return GitRunner.Run(ProjectRoot, environment, TimeoutMs, all.ToArray());
        }

        private static string Check(GitRunner.Result result, string action)
        {
            if (!result.Succeeded) throw new InvalidOperationException(result.Describe(action));
            return result.Output;
        }
    }
}
