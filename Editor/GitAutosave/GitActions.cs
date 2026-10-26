#if UNITY_EDITOR
// Target editor: Unity 2022.3 LTS (including 2022.3.22f1).
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Riry.UnityWorkflow.Editor
{
    /// <summary>
    /// What the GIT menu entries do. The entries themselves are in
    /// Editor/UI/RiryMenus.cs; the windows are in Editor/UI/GitWindows.cs.
    /// </summary>
    [InitializeOnLoad]
    internal static class GitActions
    {
        private const string DontAskKey = "Riry.GitAutosave.DontAskToConfigure";

        static GitActions()
        {
            // Offer setup once per session when the project has no repository.
            if (SessionState.GetBool(DontAskKey, false) || EditorUserSettings.GetConfigValue(DontAskKey) == "1") return;
            SessionState.SetBool(DontAskKey, true);
            EditorApplication.delayCall += () =>
            {
                if (!GitState.GitInstalled || GitState.RepositoryExists) return;
                int choice = EditorUtility.DisplayDialogComplex("AutoSync",
                    "This project is not in a Git repository, so scenes and prefabs are not backed up.\n\n" +
                    "Set up a repository with a .gitignore and Git LFS now?", "Configure Sync...", "Not Now", "Don't Ask Again");
                if (choice == 0) GitConfigureWindow.Open();
                if (choice == 2) EditorUserSettings.SetConfigValue(DontAskKey, "1");
            };
        }

        internal static void ToggleAutoSync()
        {
            Guard(() =>
            {
                bool enable = !GitAutosave.Enabled;
                if (enable)
                {
                    GitState.RequireGit();
                    if (!GitState.RepositoryExists)
                    {
                        if (EditorUtility.DisplayDialog("AutoSync", "AutoSync needs a Git repository. Configure one now?",
                                "Configure Sync...", "Cancel"))
                            GitConfigureWindow.Open();
                        return;
                    }
                }
                GitAutosave.Enabled = enable;
                if (enable) GitAutosave.ScheduleNow();
            });
        }

        internal static void SyncNow()
        {
            Guard(() =>
            {
                GitState.RequireGit();
                if (!GitState.RepositoryExists) throw new InvalidOperationException(
                    "The project is not in a Git repository. Use GIT > Configure Sync... first.");
                GitAutosave.ScheduleNow();
            });
        }

        internal static void ConfigureSync()
        {
            Guard(() =>
            {
                GitState.RequireGit();
                GitConfigureWindow.Open();
            });
        }

        // Offered while there is no repository or no remote. Without git it stays
        // enabled, so choosing it explains what is missing.
        internal static bool ConfigureSyncAvailable()
        {
            return !GitState.GitInstalled || !GitState.RepositoryExists || !GitState.HasRemote;
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
                EditorUtility.DisplayDialog("GIT", exception.Message, "OK");
            }
        }
    }

    /// <summary>Configure Sync: state and actions. The drawing is in Editor/UI/GitWindows.cs.</summary>
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
        private GitLfs.Report report;
        private Vector2 scroll;

        private static bool IsHttps(string url)
        {
            return url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
        }

        private static string ProjectFile(string name)
        {
            return Path.Combine(RegenerateAssetGuid.ProjectRootPath, name);
        }

        private void Reload()
        {
            GitActions.Guard(GitTemplates.EnsureDefaults);
            ignoreOptions = Options(GitTemplates.IgnoreExtension);
            attributeOptions = Options(GitTemplates.AttributesExtension);
            ignoreChoice = Math.Max(0, Math.Min(ignoreChoice, ignoreOptions.Length - 1));
            attributeChoice = Math.Max(0, Math.Min(attributeChoice, attributeOptions.Length - 1));
            GitState.Refresh(true);
            currentRemoteUrl = "";
            if (GitState.HasRemote)
                GitActions.Guard(() => currentRemoteUrl = GitRunner.Run(RegenerateAssetGuid.ProjectRootPath, null, 10000,
                    "remote", "get-url", GitAutosave.Remote).Output.Trim());
        }

        private static string[] Options(string extension)
        {
            var options = new List<string>();
            if (File.Exists(ProjectFile(extension))) options.Add(KeepCurrent);
            options.AddRange(GitTemplates.Names(extension));
            return options.ToArray();
        }

        private void Apply()
        {
            GitState.RequireGit();
            string project = RegenerateAssetGuid.ProjectRootPath;
            if (!GitState.RepositoryExists)
            {
                Check(GitRunner.Run(project, null, 30000, "init"), "Creating the repository");
                Check(GitRunner.Run(project, null, 30000, "symbolic-ref", "HEAD", "refs/heads/" + GitAutosave.BaseBranch),
                    "Naming the first branch");
            }

            // The project folder is the repository's top folder, so these are its top-level files.
            if (ignoreOptions.Length > 0) CopyTemplate(ignoreOptions[ignoreChoice], GitTemplates.IgnoreExtension);
            if (attributeOptions.Length > 0) CopyTemplate(attributeOptions[attributeChoice], GitTemplates.AttributesExtension);

            GitLfs.Enable(project);
            if (remoteUrl.Length > 0 && !GitState.HasRemote)
                Check(GitRunner.Run(project, null, 30000, "remote", "add", GitAutosave.Remote, remoteUrl), "Adding the remote");
            GitState.Refresh(true);

            report = GitLfs.Check(project, GitState.HasRemote ? GitAutosave.Remote : null);
            Reload();
            GitAutosave.ScheduleNow();
        }

        private static void CopyTemplate(string option, string extension)
        {
            if (option == KeepCurrent) return;
            string text = File.ReadAllText(GitTemplates.PathFor(option, extension));
            if (File.Exists(ProjectFile(extension)) && File.ReadAllText(ProjectFile(extension)) != text &&
                !EditorUtility.DisplayDialog("Configure Sync", "Replace the project's " + extension + " with " + option + extension + "?",
                    "Replace", "Keep Current"))
                return;
            File.WriteAllText(ProjectFile(extension), text);
        }

        private static void Check(GitRunner.Result result, string action)
        {
            if (!result.Succeeded) throw new InvalidOperationException(result.Describe(action));
        }
    }
}
#endif
