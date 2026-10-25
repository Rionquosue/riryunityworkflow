#if UNITY_EDITOR
// Target editor: Unity 2022.3 LTS (including 2022.3.22f1).
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Riry.UnityWorkflow.Editor
{
    /// <summary>The GIT menu in the main menu bar.</summary>
    [InitializeOnLoad]
    internal static class GitMenu
    {
        // U+2215 keeps "Scene/Prefab" on one menu item instead of a submenu.
        private const string AutoSyncPath = "GIT/AutoSync Scene∕Prefab";
        private const string DontAskKey = "Riry.GitAutosave.DontAskToConfigure";

        static GitMenu()
        {
            // Offer setup once per session when the project has no repository.
            if (SessionState.GetBool(DontAskKey, false) || EditorUserSettings.GetConfigValue(DontAskKey) == "1") return;
            SessionState.SetBool(DontAskKey, true);
            EditorApplication.delayCall += () =>
            {
                if (!GitState.GitInstalled || GitState.RepositoryRoot != null) return;
                int choice = EditorUtility.DisplayDialogComplex("AutoSync",
                    "This project is not in a Git repository, so scenes and prefabs are not backed up.\n\n" +
                    "Set up a repository with a .gitignore and Git LFS now?", "Configure Sync...", "Not Now", "Don't Ask Again");
                if (choice == 0) GitConfigureWindow.Open();
                if (choice == 2) EditorUserSettings.SetConfigValue(DontAskKey, "1");
            };
        }

        [MenuItem(AutoSyncPath, false, 1)]
        private static void ToggleAutoSync()
        {
            Guard(() =>
            {
                bool enable = !GitAutosave.Enabled;
                if (enable)
                {
                    GitState.RequireGit();
                    if (GitState.RepositoryRoot == null)
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

        [MenuItem(AutoSyncPath, true)]
        private static bool ValidateAutoSync()
        {
            Menu.SetChecked(AutoSyncPath, GitAutosave.Enabled);
            return true;
        }

        [MenuItem("GIT/Sync Now", false, 2)]
        private static void SyncNow()
        {
            Guard(() =>
            {
                GitState.RequireGit();
                if (GitState.RepositoryRoot == null) throw new InvalidOperationException(
                    "The project is not in a Git repository. Use GIT > Configure Sync... first.");
                GitAutosave.ScheduleNow();
            });
        }

        [MenuItem("GIT/Check for Broken References...", false, 20)]
        private static void CheckBrokenReferences()
        {
            BrokenReferenceWindow.OpenForOpenScenes("Checked from the GIT menu.");
        }

        [MenuItem("GIT/Configure Sync...", false, 40)]
        private static void ConfigureSync()
        {
            Guard(() =>
            {
                GitState.RequireGit();
                GitConfigureWindow.Open();
            });
        }

        // Offered while there is no repository or no remote. Without git it stays
        // enabled, so choosing it explains what is missing.
        [MenuItem("GIT/Configure Sync...", true)]
        private static bool ValidateConfigureSync()
        {
            return !GitState.GitInstalled || GitState.RepositoryRoot == null || !GitState.HasRemote;
        }

        [MenuItem("GIT/AutoSync Settings...", false, 41)]
        private static void Settings()
        {
            GitAutosaveWindow.Open();
        }

        [MenuItem("GIT/Help", false, 100)]
        private static void Help()
        {
            GitHelpWindow.Open();
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

    internal sealed class GitConfigureWindow : EditorWindow
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

        internal static void Open()
        {
            GitConfigureWindow window = GetWindow<GitConfigureWindow>(true, "Configure Sync");
            window.minSize = new Vector2(460f, 420f);
            window.Reload();
            window.Show();
        }

        internal const string CloudflareWarning =
            "This remote uses HTTPS. If the server is behind a Cloudflare proxy (including Zero Trust tunnels), " +
            "uploads over 100 MB are rejected and large LFS files cannot be pushed. Use an SSH remote " +
            "(git@host:owner/repo.git) for servers behind Cloudflare, with LFS over SSH enabled on the server " +
            "(Gitea/Forgejo: LFS_ALLOW_PURE_SSH = true under [server]).";

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
            GitMenu.Guard(GitTemplates.EnsureDefaults);
            ignoreOptions = Options(GitTemplates.IgnoreExtension);
            attributeOptions = Options(GitTemplates.AttributesExtension);
            ignoreChoice = Math.Max(0, Math.Min(ignoreChoice, ignoreOptions.Length - 1));
            attributeChoice = Math.Max(0, Math.Min(attributeChoice, attributeOptions.Length - 1));
            GitState.Refresh(true);
            currentRemoteUrl = "";
            if (GitState.HasRemote)
                GitMenu.Guard(() => currentRemoteUrl = GitRunner.Run(RegenerateAssetGuid.ProjectRootPath, null, 10000,
                    "remote", "get-url", GitAutosave.Remote).Output.Trim());
        }

        private static string[] Options(string extension)
        {
            var options = new List<string>();
            if (File.Exists(ProjectFile(extension))) options.Add(KeepCurrent);
            options.AddRange(GitTemplates.Names(extension));
            return options.ToArray();
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Repository", GitState.RepositoryRoot ?? "None. One is created in the project folder.",
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Remote", GitState.HasRemote ? currentRemoteUrl : "None (local backups only)",
                EditorStyles.wordWrappedLabel);

            GUILayout.Space(6f);
            EditorGUILayout.LabelField("Templates (" + ImportPresetLibrary.RootPath + ")", EditorStyles.boldLabel);
            ignoreChoice = EditorGUILayout.Popup(".gitignore", ignoreChoice, ignoreOptions);
            attributeChoice = EditorGUILayout.Popup(".gitattributes", attributeChoice, attributeOptions);

            if (!GitState.HasRemote)
                remoteUrl = EditorGUILayout.TextField(new GUIContent("Remote URL (optional)",
                    "Added as " + GitAutosave.Remote + ". Credentials are handled by Git itself."), remoteUrl).Trim();
            if (IsHttps(GitState.HasRemote ? currentRemoteUrl : remoteUrl))
                EditorGUILayout.HelpBox(CloudflareWarning, MessageType.Warning);

            if (GUILayout.Button("Apply", GUILayout.Height(28f))) GitMenu.Guard(Apply);

            GUILayout.Space(8f);
            EditorGUILayout.LabelField("Save the project's current files as named templates", EditorStyles.boldLabel);
            templateName = EditorGUILayout.TextField("Template name", templateName).Trim();
            using (new EditorGUI.DisabledScope(ImportPresetLibrary.ValidateName(templateName) != null))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    SaveButton(GitTemplates.IgnoreExtension);
                    SaveButton(GitTemplates.AttributesExtension);
                }
            }

            if (report != null) DrawReport();
            EditorGUILayout.EndScrollView();
        }

        private void SaveButton(string extension)
        {
            using (new EditorGUI.DisabledScope(!File.Exists(ProjectFile(extension))))
            {
                if (!GUILayout.Button("Save " + extension)) return;
                GitMenu.Guard(() =>
                {
                    if (File.Exists(GitTemplates.PathFor(templateName, extension)) &&
                        !EditorUtility.DisplayDialog("Save Template", templateName + extension + " exists. Replace it?", "Replace", "Cancel"))
                        return;
                    GitTemplates.Save(templateName, extension, File.ReadAllText(ProjectFile(extension)));
                    Reload();
                });
            }
        }

        private void Apply()
        {
            GitState.RequireGit();
            string project = RegenerateAssetGuid.ProjectRootPath;
            if (GitState.RepositoryRoot == null)
            {
                Check(GitRunner.Run(project, null, 30000, "init"), "Creating the repository");
                Check(GitRunner.Run(project, null, 30000, "symbolic-ref", "HEAD", "refs/heads/" + GitAutosave.BaseBranch),
                    "Naming the first branch");
            }

            // Written to the project folder: the templates' paths (/Library and so on) are relative to it.
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
            // Macros such as "lfs" only work in the repository's top-level .gitattributes.
            if (extension == GitTemplates.AttributesExtension && !IsRepositoryRoot(RegenerateAssetGuid.ProjectRootPath))
                text = GitTemplates.ExpandMacros(text);
            if (File.Exists(ProjectFile(extension)) && File.ReadAllText(ProjectFile(extension)) != text &&
                !EditorUtility.DisplayDialog("Configure Sync", "Replace the project's " + extension + " with " + option + extension + "?",
                    "Replace", "Keep Current"))
                return;
            File.WriteAllText(ProjectFile(extension), text);
        }

        private void DrawReport()
        {
            GUILayout.Space(8f);
            EditorGUILayout.LabelField("Git LFS check", EditorStyles.boldLabel);
            if (report.Upload != null)
                EditorGUILayout.HelpBox(report.Upload, report.UploadWorks ? MessageType.Info : MessageType.Error);
            if (report.NotTracked.Count == 0)
                EditorGUILayout.HelpBox("Every binary file of 1 MiB or more is stored with Git LFS.", MessageType.Info);
            else
            {
                EditorGUILayout.HelpBox(report.NotTracked.Count + " binary file(s) of 1 MiB or more are not stored with Git LFS, " +
                    "for example:\n" + string.Join("\n", report.NotTracked.GetRange(0, Math.Min(8, report.NotTracked.Count))),
                    MessageType.Warning);
                if (GUILayout.Button("Track ." + string.Join(", .", report.NotTrackedExtensions) + " with LFS"))
                {
                    GitMenu.Guard(() =>
                    {
                        GitLfs.Track(ProjectFile(GitTemplates.AttributesExtension), report.NotTrackedExtensions);
                        report = GitLfs.Check(RegenerateAssetGuid.ProjectRootPath, GitState.HasRemote ? GitAutosave.Remote : null);
                    });
                }
            }
            if (report.OverProxyLimit.Count > 0)
                EditorGUILayout.HelpBox(report.OverProxyLimit.Count + " file(s) exceed 100 MB. Over HTTPS, a server behind " +
                    "Cloudflare rejects them; use an SSH remote.\n" +
                    string.Join("\n", report.OverProxyLimit.GetRange(0, Math.Min(8, report.OverProxyLimit.Count))), MessageType.Warning);
        }

        private static void Check(GitRunner.Result result, string action)
        {
            if (!result.Succeeded) throw new InvalidOperationException(result.Describe(action));
        }

        private static bool IsRepositoryRoot(string directory)
        {
            return GitRunner.Run(directory, null, 10000, "rev-parse", "--show-prefix").Output.Trim().Length == 0;
        }
    }

    internal sealed class GitHelpWindow : EditorWindow
    {
        private Vector2 scroll;

        internal static void Open()
        {
            GitHelpWindow window = GetWindow<GitHelpWindow>(true, "GIT Help");
            window.minSize = new Vector2(460f, 440f);
            window.Show();
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            Section("What",
                "AutoSync commits your scenes, prefabs and .meta files to a Git branch (AutoSync unless you choose another) " +
                "a few seconds after they change, and pushes it when the project has a remote. It never touches the branch " +
                "you work on or what you have staged.");
            Section("Why",
                "Scenes and prefabs are the files most often lost to power loss, crashes, tools that break references, or " +
                "GUID changes. With AutoSync, any of them can be restored from the last few seconds, and " +
                "GIT > Check for Broken References can relink GUIDs from that history.");
            Section("How",
                "1. Install Git (Git for Windows includes Git LFS).\n" +
                "2. Choose GIT > Configure Sync... and pick a .gitignore and .gitattributes template. A repository is created " +
                "if needed and Git LFS is enabled, so large models, textures and audio are stored efficiently.\n" +
                "3. Optionally enter a remote URL. Sign-in is handled by Git and your credential manager, not by this tool.\n" +
                "4. Keep GIT > AutoSync Scene∕Prefab checked.\n\n" +
                "To restore a file: git checkout AutoSync -- path/to/Scene.unity path/to/Scene.unity.meta");
            Section("Large files and Cloudflare",
                "Servers behind Cloudflare proxies (including Zero Trust tunnels to Gitea or Forgejo) reject any single HTTPS " +
                "upload over 100 MB, and Git LFS cannot split uploads into chunks. Use an SSH remote (git@host:owner/repo.git) " +
                "for these servers. Git LFS only stays on SSH when the server allows it: on Gitea or Forgejo set " +
                "LFS_ALLOW_PURE_SSH = true under [server] in app.ini; otherwise LFS falls back to HTTPS.");
            Section("Templates",
                "Templates live in " + ImportPresetLibrary.RootPath + " as <name>.gitignore and <name>.gitattributes, for example " +
                "vrcproject.gitignore. Without any, GitHub's Unity.gitignore and the gitattributes project's Unity.gitattributes " +
                "are downloaded, and a built-in LFS template is added.");
            if (GUILayout.Button("Download Git (git-scm.com)", GUILayout.Height(26f)))
                Application.OpenURL(GitNotInstalledException.DownloadUrl);
            if (GUILayout.Button("Open Template Folder"))
                EditorUtility.RevealInFinder(ImportPresetLibrary.EnsureRoot());
            EditorGUILayout.EndScrollView();
        }

        private static void Section(string title, string text)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(text, EditorStyles.wordWrappedLabel);
            GUILayout.Space(6f);
        }
    }
}
#endif
