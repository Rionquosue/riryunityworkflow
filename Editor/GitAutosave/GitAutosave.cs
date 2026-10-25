#if UNITY_EDITOR
// Target editor: Unity 2022.3 LTS (including 2022.3.22f1).
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Riry.UnityWorkflow.Editor
{
    /// <summary>
    /// Snapshots scenes, prefabs and .meta files to a Git autosave branch a few
    /// seconds after they change, and pushes it. Settings are per user and per
    /// project (UserSettings), and autosave is off until enabled.
    /// </summary>
    [InitializeOnLoad]
    internal static class GitAutosave
    {
        internal const string WorkflowFileName = "riry-autosave-rebase.yaml";
        private const string WorkflowTemplateGuid = "5a0f1f0c6b7e4f2d9c3a8e1b7d4c6f20";
        private const string Prefix = "Riry.GitAutosave.";
        private const string PendingKey = Prefix + "Pending";
        private const double DelaySeconds = 3.0;

        private static double due = -1;
        private static Task<string> worker;
        private static string reported;

        /// <summary>The outcome of the last pass, for the settings window.</summary>
        internal static string Status { get; private set; }

        static GitAutosave()
        {
            EditorSceneManager.sceneSaved += scene => Schedule();
            PrefabStage.prefabSaved += root => Schedule();
            EditorApplication.update += Tick;
            EditorApplication.quitting += FlushOnQuit;
            // A pass interrupted by a script reload runs again afterwards.
            AssemblyReloadEvents.beforeAssemblyReload += () =>
            {
                if (due >= 0 || (worker != null && !worker.IsCompleted)) SessionState.SetBool(PendingKey, true);
            };
            if (SessionState.GetBool(PendingKey, false)) Schedule();
        }

        /// <summary>On by default once the project is in a Git repository.</summary>
        internal static bool Enabled
        {
            get { return Get("Enabled", GitState.RepositoryExists ? "1" : "0") == "1"; }
            set { Set("Enabled", value ? "1" : "0"); }
        }

        /// <summary>"AutoSync" unless the user chose another branch, so backups always have a target.</summary>
        internal static string Branch
        {
            get { return Get("Branch", "AutoSync"); }
            set { Set("Branch", value); }
        }

        internal static string Remote
        {
            get { return Get("Remote", "origin"); }
            set { Set("Remote", value); }
        }

        internal static string BaseBranch
        {
            get { return Get("BaseBranch", "main"); }
            set { Set("BaseBranch", value); }
        }

        internal static bool PushEnabled
        {
            get { return Get("Push", "1") == "1"; }
            set { Set("Push", value ? "1" : "0"); }
        }

        private static string Get(string name, string fallback)
        {
            string value = EditorUserSettings.GetConfigValue(Prefix + name);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        private static void Set(string name, string value)
        {
            EditorUserSettings.SetConfigValue(Prefix + name, value);
        }

        /// <summary>Starts a pass after a short delay; repeated calls within the delay make one pass.</summary>
        internal static void Schedule()
        {
            if (!Enabled) return;
            due = EditorApplication.timeSinceStartup + DelaySeconds;
        }

        internal static void ScheduleNow()
        {
            due = EditorApplication.timeSinceStartup;
        }

        private static void Tick()
        {
            if (worker != null && worker.IsCompleted)
            {
                Report(worker);
                worker = null;
            }
            if (due < 0 || EditorApplication.timeSinceStartup < due || worker != null ||
                EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;
            due = -1;
            SessionState.SetBool(PendingKey, false);
            if (SettingsError() != null)
            {
                Fail(SettingsError());
                return;
            }
            // Settings are read here: EditorUserSettings is main-thread only.
            GitAutosaveJob job = CreateJob(PushEnabled);
            worker = Task.Run(() => job.Execute());
        }

        private static void FlushOnQuit()
        {
            if (due < 0 || SettingsError() != null) return;
            // Commit locally only; a push could hold the editor open on a slow network.
            try
            {
                // A running pass holds the autosave index; let it finish (WaitAny ignores its failure).
                if (worker != null) Task.WaitAny(new Task[] { worker }, TimeSpan.FromSeconds(10));
                CreateJob(false).Execute();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("AutoSync: " + exception.Message);
            }
        }

        private static GitAutosaveJob CreateJob(bool push)
        {
            return new GitAutosaveJob
            {
                ProjectRoot = RegenerateAssetGuid.ProjectRootPath,
                StateFolder = Path.Combine(RegenerateAssetGuid.ProjectRootPath, "Library", "RiryGitAutosave"),
                Branch = Branch,
                Remote = Remote,
                BaseBranch = BaseBranch,
                Push = push
            };
        }

        private static void Report(Task<string> finished)
        {
            if (finished.IsFaulted)
            {
                Fail(finished.Exception.GetBaseException().Message);
                return;
            }
            Status = DateTime.Now.ToString("HH:mm:ss") + " " + finished.Result;
            reported = null;
        }

        private static void Fail(string message)
        {
            Status = DateTime.Now.ToString("HH:mm:ss") + " failed: " + message;
            // The same failure repeats on every save; the Console gets it once.
            if (message != reported) Debug.LogWarning("AutoSync: " + message);
            reported = message;
        }

        internal static string SettingsError()
        {
            return BranchNameError(Branch, "AutoSync branch") ?? BranchNameError(BaseBranch, "Base branch") ??
                (Branch == BaseBranch ? "The AutoSync branch must differ from the base branch." : null) ??
                (Regex.IsMatch(Remote, @"^[A-Za-z0-9._/-]+$") && !Remote.StartsWith("-", StringComparison.Ordinal)
                    ? null
                    : "The remote name is not valid.");
        }

        /// <summary>The git check-ref-format rules for a branch name; null when valid.</summary>
        internal static string BranchNameError(string name, string label)
        {
            if (string.IsNullOrEmpty(name)) return label + " is empty.";
            if (name == "@" || name.StartsWith("-", StringComparison.Ordinal) || name.StartsWith("/", StringComparison.Ordinal) ||
                name.EndsWith("/", StringComparison.Ordinal) || name.EndsWith(".", StringComparison.Ordinal) ||
                name.Contains("..") || name.Contains("//") || name.Contains("@{") || name.Contains("/.") ||
                name.StartsWith(".", StringComparison.Ordinal) || name.EndsWith(".lock", StringComparison.Ordinal) ||
                name.Contains(".lock/") || Regex.IsMatch(name, @"[\x00-\x20\x7f~^:?*\[\\]"))
                return label + " \"" + name + "\" is not a valid Git branch name.";
            return null;
        }

        /// <summary>
        /// The branch filter for the rebase workflow: every branch in the autosave
        /// branch's folder ("backup/riry" gives "backup/**"), else the name itself ("AutoSync").
        /// </summary>
        internal static string WorkflowPattern(string branch)
        {
            int slash = branch.LastIndexOf('/');
            return slash > 0 ? branch.Substring(0, slash + 1) + "**" : branch;
        }

        /// <summary>The workflow template with this project's base branch and autosave branches.</summary>
        internal static string WorkflowText(string template, string baseBranch, string branch)
        {
            string quotedBase = "'" + baseBranch.Replace("'", "''") + "'";
            string quotedPattern = "'" + WorkflowPattern(branch).Replace("'", "''") + "'";
            return template
                .Replace("      - main\n", "      - " + quotedBase + "\n")
                .Replace("      - 'AutoSync'\n", "      - " + quotedPattern + "\n")
                .Replace("  BASE_BRANCH: main\n", "  BASE_BRANCH: " + quotedBase + "\n")
                .Replace("  AUTOSAVE_BRANCHES: 'AutoSync'\n", "  AUTOSAVE_BRANCHES: " + quotedPattern + "\n");
        }

        /// <summary>
        /// Writes the rebase workflow to the repository's .github/workflows. A
        /// different existing file is replaced only when <paramref name="mayReplace"/>
        /// agrees. Returns the path, or null when kept.
        /// </summary>
        internal static string InstallWorkflow(Func<string, bool> mayReplace)
        {
            var template = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetDatabase.GUIDToAssetPath(WorkflowTemplateGuid));
            if (template == null) throw new FileNotFoundException("The rebase workflow template is missing from the package.");
            GitState.Refresh(true);
            if (!GitState.RepositoryExists)
                throw new InvalidOperationException("The project folder is not a Git repository. Use GIT > Configure Sync... first.");

            string path = Path.Combine(RegenerateAssetGuid.ProjectRootPath, ".github", "workflows", WorkflowFileName);
            string text = WorkflowText(template.text.Replace("\r\n", "\n"), BaseBranch, Branch);
            if (File.Exists(path) && File.ReadAllText(path) != text && !mayReplace(path)) return null;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text, new UTF8Encoding(false));
            return path;
        }
    }

    internal sealed class GitAutosavePostprocessor : AssetPostprocessor
    {
        // Covers prefab and scene saves made by tools, imports that rewrite .meta
        // files, moves and deletions. A pass with no changes commits nothing.
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            GitAutosave.Schedule();
        }
    }
}
#endif
