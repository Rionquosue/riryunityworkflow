using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// What the Git menu entries do. The entries themselves are in
    /// Editor/UI/RiryMenus.cs; the windows are in Editor/UI/GitWindows.cs.
    /// </summary>
    [InitializeOnLoad]
    internal static class GitActions
    {
        // Session state: setup was offered in this session. User setting: Don't Ask Again was chosen.
        private const string AskedKey = "RiryTools.AutoSync.AskedToConfigure";
        private const string DontAskKey = "RiryTools.AutoSync.DontAskToConfigure";

        static GitActions()
        {
            // Offer setup once per session when the project has no repository.
            if (SessionState.GetBool(AskedKey, false) || EditorUserSettings.GetConfigValue(DontAskKey) == "1") return;
            SessionState.SetBool(AskedKey, true);
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
                bool enable = !AutoSync.Enabled;
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
                AutoSync.Enabled = enable;
                if (enable) AutoSync.ScheduleNow();
            });
        }

        internal static void SyncNow()
        {
            Guard(() =>
            {
                GitState.RequireGit();
                if (!GitState.RepositoryExists) throw new InvalidOperationException(
                    "The project is not in a Git repository. Use Git > Configure Sync... first.");
                AutoSync.ScheduleNow();
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
                EditorUtility.DisplayDialog("Git", exception.Message, "OK");
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
            return Path.Combine(RiryToolsPaths.ProjectRoot, name);
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
                GitActions.Guard(() => currentRemoteUrl = GitRunner.Run(RiryToolsPaths.ProjectRoot, null, 10000,
                    "remote", "get-url", AutoSync.Remote).Output.Trim());
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
            string project = RiryToolsPaths.ProjectRoot;
            if (!GitState.RepositoryExists)
            {
                Check(GitRunner.Run(project, null, 30000, "init"), "Creating the repository");
                Check(GitRunner.Run(project, null, 30000, "symbolic-ref", "HEAD", "refs/heads/" + AutoSync.BaseBranch),
                    "Naming the first branch");
            }

            // The project folder is the repository's top folder, so these are its top-level files.
            if (ignoreOptions.Length > 0) CopyTemplate(ignoreOptions[ignoreChoice], GitTemplates.IgnoreExtension);
            if (attributeOptions.Length > 0) CopyTemplate(attributeOptions[attributeChoice], GitTemplates.AttributesExtension);

            GitLfs.Enable(project);
            if (remoteUrl.Length > 0 && !GitState.HasRemote)
                Check(GitRunner.Run(project, null, 30000, "remote", "add", AutoSync.Remote, remoteUrl), "Adding the remote");
            GitState.Refresh(true);

            report = GitLfs.Check(project, GitState.HasRemote ? AutoSync.Remote : null);
            Reload();
            AutoSync.ScheduleNow();
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
