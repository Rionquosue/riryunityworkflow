using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// What the Sync and Configuration menu entries do. The entries themselves are
    /// in Editor/UI/RiryMenus.cs; the Configuration window is drawn in Editor/UI/GitWindows.cs.
    /// </summary>
    internal static class GitActions
    {
        /// <summary>
        /// Turns Sync off, or on: asks for Git when it is missing, creates the
        /// repository from the Unity templates when there is none, and enables Git LFS.
        /// </summary>
        internal static void ToggleSync()
        {
            Guard(() =>
            {
                if (AutoSync.Enabled)
                {
                    AutoSync.Enabled = false;
                    return;
                }
                GitState.RequireGit();
                string project = RiryToolsPaths.ProjectRoot;
                if (!GitState.RepositoryExists)
                {
                    if (GitState.EnclosingRepository != null && !EditorUtility.DisplayDialog("Sync",
                            "The project is inside the repository at " + GitState.EnclosingRepository + ". Sync needs the " +
                            "project folder itself to be the repository, so a separate one is created here; the outer " +
                            "repository will see this folder as an embedded repository.", "Create", "Cancel"))
                        return;
                    EnsureTemplates();
                    Check(GitRunner.Run(project, null, 30000, "init"), "Creating the repository");
                    // Existing files are kept.
                    CopyTemplate(GitTemplates.DefaultName, GitTemplates.IgnoreExtension, false);
                    CopyTemplate(GitTemplates.DefaultName, GitTemplates.AttributesExtension, false);
                    GitState.Refresh(true);
                }
                try
                {
                    GitLfs.Enable(project);
                }
                catch (InvalidOperationException exception)
                {
                    // Commits work without LFS; it only matters for large files on a remote.
                    Debug.LogWarning("AutoSync: " + exception.Message);
                    EditorUtility.DisplayDialog("Sync", exception.Message + "\n\nSync is on without Git LFS.", "OK");
                }
                AutoSync.Enabled = true;
            });
        }

        internal static void OpenConfiguration()
        {
            Guard(() =>
            {
                GitState.RequireGit();
                GitConfigureWindow.Open();
            });
        }

        /// <summary>Creates the template folder with the Unity defaults when it has none, and says so.</summary>
        internal static void EnsureTemplates()
        {
            if (GitTemplates.EnsureDefaults())
                EditorUtility.DisplayDialog("AutoSync", "Created the Git templates folder with the Unity defaults:\n" +
                    GitTemplates.RootPath, "OK");
        }

        internal static string ProjectFile(string name)
        {
            return Path.Combine(RiryToolsPaths.ProjectRoot, name);
        }

        /// <summary>Copies a template into the project; an existing different file is replaced only when allowed and confirmed.</summary>
        internal static void CopyTemplate(string name, string extension, bool mayReplace)
        {
            string template = GitTemplates.PathFor(name, extension);
            if (!File.Exists(template)) return;
            string text = File.ReadAllText(template);
            if (File.Exists(ProjectFile(extension)) && (File.ReadAllText(ProjectFile(extension)) == text || !mayReplace ||
                    !EditorUtility.DisplayDialog("Configuration", "Replace the project's " + extension + " with " +
                        name + extension + "?", "Replace", "Keep Current")))
                return;
            File.WriteAllText(ProjectFile(extension), text);
        }

        internal static void Check(GitRunner.Result result, string action)
        {
            if (!result.Succeeded) throw new InvalidOperationException(result.Describe(action));
        }

        /// <summary>Runs a menu action; a missing git or a failed step is shown, and logged as an exception.</summary>
        internal static void Guard(Action action)
        {
            try
            {
                action();
            }
            catch (OperationCanceledException)
            {
            }
            catch (GitNotInstalledException exception)
            {
                Debug.LogException(exception);
                if (EditorUtility.DisplayDialog("Git Not Installed", exception.Message, "Download Git", "Close"))
                    Application.OpenURL(GitNotInstalledException.DownloadUrl);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Git", exception.Message, "OK");
            }
        }
    }

    /// <summary>Configuration: state and actions. The drawing is in Editor/UI/GitWindows.cs.</summary>
    internal sealed partial class GitConfigureWindow : EditorWindow
    {
        private const string KeepCurrent = "(keep current file)";
        private const string Detached = "(detached HEAD)";

        private string[] ignoreOptions;
        private string[] attributeOptions;
        private int ignoreChoice;
        private int attributeChoice;
        private string[] branches = new string[0];
        private int branchChoice;
        private List<string> remotes = new List<string>();
        private string selectedRemote;
        private string remoteUrl = "";
        private string currentRemoteUrl = "";
        private List<string> largeFiles = new List<string>();
        private Task<string> lfsQuery;
        private string lfsStatus;
        private string templateName = "";
        private Vector2 scroll;

        internal void Reload()
        {
            GitActions.Guard(GitActions.EnsureTemplates);
            ignoreOptions = Options(GitTemplates.IgnoreExtension);
            attributeOptions = Options(GitTemplates.AttributesExtension);
            ignoreChoice = Math.Max(0, Math.Min(ignoreChoice, ignoreOptions.Length - 1));
            attributeChoice = Math.Max(0, Math.Min(attributeChoice, attributeOptions.Length - 1));
            GitState.Refresh(true);
            branches = new string[0];
            remotes = new List<string>();
            selectedRemote = null;
            currentRemoteUrl = "";
            largeFiles = new List<string>();
            if (GitState.RepositoryExists)
            {
                GitActions.Guard(() =>
                {
                    string project = RiryToolsPaths.ProjectRoot;
                    remotes = GitRemotes.Names(project);
                    selectedRemote = GitRemotes.Selected(remotes);
                    if (selectedRemote != null) currentRemoteUrl = GitRemotes.Url(project, selectedRemote);
                    LoadBranches(project);
                    largeFiles = GitLargeFiles.Tracked(project);
                });
            }
            remoteUrl = currentRemoteUrl;
            StartLfsQuery();
        }

        // Local branches, then the chosen remote's branches that have no local branch
        // (git switch creates them); the current branch is first choice.
        private void LoadBranches(string project)
        {
            string current = GitRunner.Run(project, null, 10000, "symbolic-ref", "-q", "--short", "HEAD").Output.Trim();
            var names = new List<string>();
            foreach (string line in GitRunner.Run(project, null, 10000, "for-each-ref", "--format=%(refname:short)", "refs/heads")
                         .Output.Replace("\r", "").Split('\n'))
            {
                if (line.Length > 0) names.Add(line);
            }
            if (current.Length > 0 && !names.Contains(current)) names.Add(current);
            if (selectedRemote != null)
            {
                string prefix = selectedRemote + "/";
                foreach (string line in GitRunner.Run(project, null, 10000, "for-each-ref", "--format=%(refname:short)",
                             "refs/remotes/" + selectedRemote).Output.Replace("\r", "").Split('\n'))
                {
                    if (!line.StartsWith(prefix, StringComparison.Ordinal)) continue;
                    string name = line.Substring(prefix.Length);
                    if (name != "HEAD" && !names.Contains(name)) names.Add(name);
                }
            }
            if (current.Length == 0) names.Insert(0, Detached);
            branches = names.ToArray();
            branchChoice = Math.Max(0, names.IndexOf(current.Length > 0 ? current : Detached));
        }

        // Asks the chosen remote whether Git LFS is enabled, off the main thread.
        private void StartLfsQuery()
        {
            lfsQuery = null;
            lfsStatus = null;
            if (selectedRemote == null || currentRemoteUrl.Length == 0) return;
            string project = RiryToolsPaths.ProjectRoot, remote = selectedRemote, url = currentRemoteUrl;
            lfsStatus = "Checking Git LFS on " + remote + "...";
            lfsQuery = Task.Run(() => GitLfsQuery.Ask(project, remote, url));
        }

        private void OnInspectorUpdate()
        {
            if (lfsQuery == null || !lfsQuery.IsCompleted) return;
            lfsStatus = lfsQuery.IsFaulted ? "Git LFS could not be checked: " + lfsQuery.Exception.GetBaseException().Message : lfsQuery.Result;
            lfsQuery = null;
            Repaint();
        }

        private static string[] Options(string extension)
        {
            var options = new List<string>();
            if (File.Exists(GitActions.ProjectFile(extension))) options.Add(KeepCurrent);
            options.AddRange(GitTemplates.Names(extension));
            return options.ToArray();
        }

        /// <summary>Commits pending saves, then checks out the chosen branch with git switch.</summary>
        private void SwitchBranch(string name)
        {
            if (name == Detached) return;
            GitState.RequireGit();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            AutoSync.CommitNow();
            GitActions.Check(GitRunner.Run(RiryToolsPaths.ProjectRoot, null, 120000, "switch", name), "Switching to " + name);
            AssetDatabase.Refresh();
            Reload();
        }

        private void SelectRemote(string name)
        {
            GitRemotes.Select(name);
            Reload();
        }

        /// <summary>Copies the chosen templates into the project, then saves the push remote when it changed.</summary>
        private void Apply()
        {
            GitState.RequireGit();
            if (ignoreOptions.Length > 0 && ignoreOptions[ignoreChoice] != KeepCurrent)
                GitActions.CopyTemplate(ignoreOptions[ignoreChoice], GitTemplates.IgnoreExtension, true);
            if (attributeOptions.Length > 0 && attributeOptions[attributeChoice] != KeepCurrent)
                GitActions.CopyTemplate(attributeOptions[attributeChoice], GitTemplates.AttributesExtension, true);

            if (remoteUrl.Length > 0 && remoteUrl != currentRemoteUrl) SaveRemote();
            Reload();
        }

        /// <summary>Adds origin when there is no remote (Add Remote), else changes the chosen remote's URL (Change Remote).</summary>
        private void SaveRemote()
        {
            GitState.RequireGit();
            string project = RiryToolsPaths.ProjectRoot;
            GitActions.Check(selectedRemote == null
                ? GitRunner.Run(project, null, 30000, "remote", "add", GitRemotes.DefaultName, remoteUrl)
                : GitRunner.Run(project, null, 30000, "remote", "set-url", selectedRemote, remoteUrl),
                "Setting the remote");
            Reload();
        }
    }

    /// <summary>Large Files: state and actions. The drawing is in Editor/UI/GitWindows.cs.</summary>
    internal sealed partial class LargeFilesWindow : EditorWindow
    {
        private List<string> files = new List<string>();
        private readonly HashSet<string> toIgnore = new HashSet<string>();
        private Vector2 scroll;

        private void Load()
        {
            toIgnore.Clear();
            GitActions.Guard(() => files = GitLargeFiles.Tracked(RiryToolsPaths.ProjectRoot));
        }

        /// <summary>Selects and pings the file in the Project window when it is an asset.</summary>
        private static void Ping(string path)
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset == null) return;
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        /// <summary>Ignores the marked files and takes them out of the unpushed commits.</summary>
        private void Ok()
        {
            if (toIgnore.Count > 0)
            {
                string project = RiryToolsPaths.ProjectRoot;
                AutoSync.CommitNow();
                GitLargeFiles.Ignore(project, GitRemotes.Selected(GitRemotes.Names(project)), new List<string>(toIgnore));
            }
            Close();
            if (HasOpenInstances<GitConfigureWindow>()) GetWindow<GitConfigureWindow>().Reload();
        }
    }
}
