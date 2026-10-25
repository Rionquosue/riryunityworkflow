#if UNITY_EDITOR
// Target editor: Unity 2022.3 LTS (including 2022.3.22f1).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Riry.UnityWorkflow.Editor
{
    /// <summary>
    /// One autosave pass: commits the project's scenes, prefabs, .meta files and
    /// GUID remap ledger to the autosave branch through a separate index, then
    /// optionally pushes. The checked-out branch and staged changes are never
    /// touched. Uses no Unity API, so it runs on a worker thread.
    /// </summary>
    internal sealed class GitAutosaveJob
    {
        private const int TimeoutMs = 120000;
        private const int PushAttempts = 3;
        private static readonly string[] SnapshotRoots = { "Assets", "Packages" };
        private static readonly string[] SnapshotExtensions = { "unity", "prefab", "meta" };
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        internal string ProjectRoot;
        internal string StateFolder;
        internal string Branch;
        internal string Remote;
        internal string BaseBranch;
        internal bool Push;

        private string repoRoot;
        private bool branchExists;
        private readonly Dictionary<string, string> environment = new Dictionary<string, string>();

        private string BranchRef
        {
            get { return "refs/heads/" + Branch; }
        }

        private string RemoteRef
        {
            get { return "refs/remotes/" + Remote + "/" + Branch; }
        }

        private string IndexBasePath
        {
            get { return Path.Combine(StateFolder, "index-base"); }
        }

        private string PathListPath
        {
            get { return Path.Combine(StateFolder, "paths"); }
        }

        // The last tip the remote accepted; commits after it have not been pushed.
        private string PushedRef
        {
            get { return "refs/riry/autosave-pushed/" + Branch; }
        }

        /// <summary>Snapshots, then pushes when enabled. Returns a status line; throws on failure.</summary>
        internal string Execute()
        {
            Open();
            string commit = Snapshot();
            string status = commit == null ? "No changes since the last snapshot." : "Saved snapshot " + commit.Substring(0, 8) + ".";
            if (Push && PushBranch()) status += " Pushed to " + Remote + "/" + Branch + ".";
            return status;
        }

        internal void Open()
        {
            GitRunner.Result top = GitRunner.Run(ProjectRoot, null, TimeoutMs, "rev-parse", "--show-toplevel");
            if (!top.Succeeded) throw new InvalidOperationException("The project is not inside a Git repository.");
            repoRoot = top.Output.Trim();
            Directory.CreateDirectory(StateFolder);

            environment["GIT_INDEX_FILE"] = Path.Combine(StateFolder, "index");
            // commit-tree needs an identity; a repository without one still gets backups.
            if (Run("config", "user.name").Output.Trim().Length == 0)
                environment["GIT_AUTHOR_NAME"] = environment["GIT_COMMITTER_NAME"] = "Riry AutoSync";
            if (Run("config", "user.email").Output.Trim().Length == 0)
                environment["GIT_AUTHOR_EMAIL"] = environment["GIT_COMMITTER_EMAIL"] = "autosave@localhost";

            if (Run("symbolic-ref", "-q", "HEAD").Output.Trim() == BranchRef)
                throw new InvalidOperationException("The AutoSync branch " + Branch +
                    " is checked out. Switch to another branch; AutoSync never moves the checked-out branch.");
        }

        /// <summary>Commits the current files when they differ from the branch tip. Returns the commit, or null.</summary>
        internal string Snapshot()
        {
            string parent = ResolveParent();
            // The index keeps file stat data between runs, so only changed files are hashed again.
            if (!File.Exists(environment["GIT_INDEX_FILE"]) || !File.Exists(IndexBasePath) ||
                File.ReadAllText(IndexBasePath) != (parent ?? ""))
            {
                Check(parent == null ? Run("read-tree", "--empty") : Run("read-tree", parent), "Preparing the AutoSync index");
                File.WriteAllText(IndexBasePath, parent ?? "", Utf8);
            }

            var list = new List<string> { "ls-files", "-z", "--cached", "--others", "--exclude-standard", "--" };
            foreach (string root in SnapshotRoots)
            {
                foreach (string extension in SnapshotExtensions)
                    list.Add(":(glob)" + root + "/**/*." + extension);
            }
            // The ignore, attribute and LFS settings travel with the backup, so a
            // checkout of the autosave branch restores LFS files correctly.
            foreach (string file in new[] { GuidRemapLedger.RelativePath, ".gitignore", ".gitattributes", ".lfsconfig" })
                list.Add(":(literal)" + file);
            string paths = Check(Run(list.ToArray()), "Listing scenes, prefabs and .meta files");
            if (paths.Length > 0)
            {
                // Listed paths are added literally, so names with * or [ are not patterns.
                File.WriteAllText(PathListPath, paths, Utf8);
                environment["GIT_LITERAL_PATHSPECS"] = "1";
                try
                {
                    Check(Run("add", "-A", "--pathspec-from-file=" + PathListPath, "--pathspec-file-nul"), "Staging the snapshot");
                }
                finally
                {
                    environment.Remove("GIT_LITERAL_PATHSPECS");
                }
            }

            string tree = Check(Run("write-tree"), "Writing the snapshot").Trim();
            if (parent != null && tree == Check(Run("rev-parse", parent + "^{tree}"), "Reading the branch tip").Trim())
                return null;

            string commit = Check(parent == null
                ? Run("commit-tree", tree, "-m", Message(null))
                : Run("commit-tree", tree, "-p", parent, "-m", Message(parent)), "Committing the snapshot").Trim();
            // The expected old value makes this fail instead of overwriting a concurrent update.
            Check(Run("update-ref", "-m", "Riry autosave", BranchRef, commit, branchExists ? parent : ""),
                "Moving " + Branch);
            File.WriteAllText(IndexBasePath, commit, Utf8);
            return commit;
        }

        private string ResolveParent()
        {
            string tip = Resolve(BranchRef);
            branchExists = tip != null;
            if (branchExists) return tip;
            // A new branch continues the remote one (another machine), else starts from the base branch.
            foreach (string candidate in new[] { RemoteRef, "refs/remotes/" + Remote + "/" + BaseBranch, "refs/heads/" + BaseBranch, "HEAD" })
            {
                tip = Resolve(candidate);
                if (tip != null) return tip;
            }
            return null;
        }

        private string Message(string parent)
        {
            if (parent == null) return "AutoSync: initial snapshot";
            string[] changed = Check(Run("diff-index", "--cached", "--name-only", parent), "Listing changed files")
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var message = new StringBuilder("AutoSync: " + changed.Length + (changed.Length == 1 ? " file\n\n" : " files\n\n"));
            for (int i = 0; i < changed.Length && i < 20; i++) message.Append(changed[i]).Append('\n');
            if (changed.Length > 20) message.Append("...and ").Append(changed.Length - 20).Append(" more\n");
            return message.ToString();
        }

        /// <summary>Pushes unpushed snapshots. Returns false when there was nothing to push.</summary>
        internal bool PushBranch()
        {
            for (int attempt = 1; ; attempt++)
            {
                string local = Resolve(BranchRef);
                if (local == null || local == Resolve(PushedRef)) return false;
                GitRunner.Result pushed = Run("push", "--porcelain", Remote, BranchRef + ":" + BranchRef);
                if (pushed.Succeeded)
                {
                    Run("update-ref", PushedRef, local);
                    return true;
                }
                if (attempt >= PushAttempts || pushed.Output.IndexOf("[rejected]", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException(pushed.Describe("Pushing " + Branch + " to " + Remote));

                // The rebase workflow rewrote the remote branch: put the commits it
                // has not seen on top of its new tip, then push again.
                Check(Run("fetch", "--quiet", Remote, "+" + BranchRef + ":" + RemoteRef), "Fetching " + Branch);
                string pushedTip = Resolve(PushedRef);
                string from = pushedTip != null && Run("merge-base", "--is-ancestor", pushedTip, local).Succeeded
                    ? pushedTip
                    : Run("merge-base", local, RemoteRef).Output.Trim();
                if (from.Length == 0)
                    throw new InvalidOperationException(Remote + "/" + Branch + " shares no history with the local AutoSync branch.");
                Check(Run("update-ref", "-m", "Riry autosave replay", BranchRef, Replay(from, local, Resolve(RemoteRef)), local),
                    "Moving " + Branch);
            }
        }

        /// <summary>
        /// Replays the first-parent commits in from..to onto <paramref name="onto"/>.
        /// A path a commit changed takes that commit's version, so the snapshot wins
        /// over the new base. Matches the rebase workflow's script.
        /// </summary>
        internal string Replay(string from, string to, string onto)
        {
            var replay = new Dictionary<string, string>(environment);
            replay["GIT_INDEX_FILE"] = Path.Combine(StateFolder, "replay-index");
            string current = onto;
            foreach (string commit in Check(RunAt(repoRoot, replay, "rev-list", "--reverse", "--first-parent", from + ".." + to),
                         "Listing unpushed snapshots").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                Check(RunAt(repoRoot, replay, "read-tree", current), "Reading " + current);
                byte[] changes = IndexInfo(Check(RunAt(repoRoot, replay, "diff-tree", "-r", "-z", "--no-renames",
                    "--no-commit-id", commit + "^", commit), "Reading snapshot " + commit));
                if (changes.Length > 0)
                    Check(GitRunner.RunWithInput(repoRoot, replay, changes, TimeoutMs, "update-index", "-z", "--index-info"),
                        "Replaying snapshot " + commit);
                string tree = Check(RunAt(repoRoot, replay, "write-tree"), "Writing the replayed snapshot").Trim();
                if (tree == Check(RunAt(repoRoot, replay, "rev-parse", current + "^{tree}"), "Reading " + current).Trim())
                    continue;

                string[] author = Check(RunAt(repoRoot, replay, "log", "-1", "--format=%an%x00%ae%x00%aI", commit),
                    "Reading snapshot " + commit).TrimEnd('\n').Split('\0');
                var authored = new Dictionary<string, string>(replay);
                authored["GIT_AUTHOR_NAME"] = author[0];
                authored["GIT_AUTHOR_EMAIL"] = author[1];
                authored["GIT_AUTHOR_DATE"] = author[2];
                current = Check(RunAt(repoRoot, authored, "commit-tree", tree, "-p", current, "-m",
                    Check(RunAt(repoRoot, replay, "log", "-1", "--format=%B", commit), "Reading snapshot " + commit)),
                    "Committing the replayed snapshot").Trim();
            }
            return current;
        }

        /// <summary>Converts diff-tree -r -z output to update-index -z --index-info input.</summary>
        internal static byte[] IndexInfo(string diffTree)
        {
            string[] fields = diffTree.Split('\0');
            var info = new StringBuilder();
            for (int i = 0; i + 1 < fields.Length; i += 2)
            {
                // ":<old mode> <new mode> <old id> <new id> <status>", then the path.
                string[] meta = fields[i].Split(' ');
                if (meta.Length < 5) continue;
                if (meta[4].StartsWith("D", StringComparison.Ordinal))
                    info.Append("0 ").Append('0', meta[2].Length);
                else
                    info.Append(meta[1]).Append(' ').Append(meta[3]);
                info.Append('\t').Append(fields[i + 1]).Append('\0');
            }
            return Utf8.GetBytes(info.ToString());
        }

        private string Resolve(string reference)
        {
            GitRunner.Result result = Run("rev-parse", "-q", "--verify", reference + "^{commit}");
            return result.Succeeded ? result.Output.Trim() : null;
        }

        private GitRunner.Result Run(params string[] arguments)
        {
            return RunAt(ProjectRoot, environment, arguments);
        }

        private static GitRunner.Result RunAt(string directory, Dictionary<string, string> variables, params string[] arguments)
        {
            // Objects and refs reach the disk before git reports success, so a
            // power loss right after a snapshot keeps it (Git 2.36+; older Git ignores this).
            var all = new List<string> { "-c", "core.fsync=committed" };
            all.AddRange(arguments);
            return GitRunner.Run(directory, variables, TimeoutMs, all.ToArray());
        }

        private static string Check(GitRunner.Result result, string action)
        {
            if (!result.Succeeded) throw new InvalidOperationException(result.Describe(action));
            return result.Output;
        }
    }
}
#endif
