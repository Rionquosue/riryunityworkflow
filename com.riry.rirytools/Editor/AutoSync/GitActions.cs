using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
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

        private string[] ignoreOptions;
        private string[] attributeOptions;
        private int ignoreChoice;
        private int attributeChoice;
        private string remoteUrl = "";
        private string currentRemoteUrl = "";
        private string templateName = "";
        private Vector2 scroll;

        private void Reload()
        {
            GitActions.Guard(GitActions.EnsureTemplates);
            ignoreOptions = Options(GitTemplates.IgnoreExtension);
            attributeOptions = Options(GitTemplates.AttributesExtension);
            ignoreChoice = Math.Max(0, Math.Min(ignoreChoice, ignoreOptions.Length - 1));
            attributeChoice = Math.Max(0, Math.Min(attributeChoice, attributeOptions.Length - 1));
            GitState.Refresh(true);
            currentRemoteUrl = "";
            if (GitState.RepositoryExists)
                GitActions.Guard(() => currentRemoteUrl = GitRunner.Run(RiryToolsPaths.ProjectRoot, null, 10000,
                    "remote", "get-url", AutoSyncJob.Remote).Output.Trim());
            remoteUrl = currentRemoteUrl;
        }

        private static string[] Options(string extension)
        {
            var options = new List<string>();
            if (File.Exists(GitActions.ProjectFile(extension))) options.Add(KeepCurrent);
            options.AddRange(GitTemplates.Names(extension));
            return options.ToArray();
        }

        /// <summary>Copies the chosen templates into the project and adds or changes the push remote.</summary>
        private void Apply()
        {
            GitState.RequireGit();
            if (ignoreOptions.Length > 0 && ignoreOptions[ignoreChoice] != KeepCurrent)
                GitActions.CopyTemplate(ignoreOptions[ignoreChoice], GitTemplates.IgnoreExtension, true);
            if (attributeOptions.Length > 0 && attributeOptions[attributeChoice] != KeepCurrent)
                GitActions.CopyTemplate(attributeOptions[attributeChoice], GitTemplates.AttributesExtension, true);

            string project = RiryToolsPaths.ProjectRoot;
            if (remoteUrl.Length > 0 && remoteUrl != currentRemoteUrl)
                GitActions.Check(currentRemoteUrl.Length == 0
                    ? GitRunner.Run(project, null, 30000, "remote", "add", AutoSyncJob.Remote, remoteUrl)
                    : GitRunner.Run(project, null, 30000, "remote", "set-url", AutoSyncJob.Remote, remoteUrl),
                    "Setting the remote");
            Reload();
        }
    }
}
