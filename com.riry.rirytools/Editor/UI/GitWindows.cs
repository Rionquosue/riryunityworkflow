using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>Git > AutoSync Settings...: branch, base branch, remote, push and the rebase workflow.</summary>
    internal sealed class AutoSyncWindow : EditorWindow
    {
        internal static void Open()
        {
            AutoSyncWindow window = GetWindow<AutoSyncWindow>();
            window.titleContent = new GUIContent("AutoSync");
            window.minSize = new Vector2(380f, 300f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Commits scenes, prefabs and .meta files to the AutoSync branch a few seconds after they change, " +
                "through a separate Git index: the checked-out branch and staged changes are never touched.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            bool enabled = EditorGUILayout.Toggle("AutoSync Scene\u2215Prefab", AutoSync.Enabled);
            string branch = EditorGUILayout.DelayedTextField("AutoSync branch", AutoSync.Branch).Trim();
            string baseBranch = EditorGUILayout.DelayedTextField("Base branch", AutoSync.BaseBranch).Trim();
            bool push = EditorGUILayout.Toggle("Push", AutoSync.PushEnabled);
            string remote;
            using (new EditorGUI.DisabledScope(!push))
                remote = EditorGUILayout.DelayedTextField("Remote", AutoSync.Remote).Trim();
            if (EditorGUI.EndChangeCheck())
            {
                // Only a changed toggle is stored, so the default still follows the repository.
                if (enabled != AutoSync.Enabled) AutoSync.Enabled = enabled;
                AutoSync.Branch = branch;
                AutoSync.BaseBranch = baseBranch;
                AutoSync.PushEnabled = push;
                AutoSync.Remote = remote;
                if (enabled) AutoSync.ScheduleNow();
            }

            if (AutoSync.SettingsError() != null)
                EditorGUILayout.HelpBox(AutoSync.SettingsError(), MessageType.Error);
            EditorGUILayout.LabelField("Last run", AutoSync.Status ?? "Not run in this session.", EditorStyles.wordWrappedLabel);

            using (new EditorGUI.DisabledScope(AutoSync.SettingsError() != null))
            {
                if (GUILayout.Button("Sync Now")) AutoSync.ScheduleNow();
                GUILayout.Space(8f);
                EditorGUILayout.LabelField("Rebase workflow (GitHub Actions)", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "Replays every " + AutoSync.WorkflowPattern(AutoSync.Branch) + " branch onto " +
                    AutoSync.BaseBranch + " whenever either is pushed. Commit and push the installed file.",
                    EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button("Install Rebase Workflow")) Install();
            }
        }

        private void OnInspectorUpdate()
        {
            Repaint();
        }

        private static void Install()
        {
            try
            {
                string path = AutoSync.InstallWorkflow(existing => EditorUtility.DisplayDialog("Replace Rebase Workflow",
                    existing + " already exists with different content. Replace it?", "Replace", "Keep"));
                if (path != null)
                    EditorUtility.DisplayDialog("Rebase Workflow Installed",
                        "Written to:\n" + path + "\n\nCommit and push it to the base branch to activate it.", "OK");
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("Rebase Workflow Not Installed", exception.Message, "OK");
            }
        }
    }

    /// <summary>What Configure Sync draws. State and actions are in Editor/AutoSync/GitActions.cs.</summary>
    internal sealed partial class GitConfigureWindow
    {
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

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Repository", GitState.RepositoryExists
                ? RiryToolsPaths.ProjectRoot
                : "None. Apply creates .git in the project folder.", EditorStyles.wordWrappedLabel);
            if (GitState.EnclosingRepository != null)
                EditorGUILayout.HelpBox("The project is inside the repository at " + GitState.EnclosingRepository +
                    ". The repository must be the project folder itself, so Apply creates a separate one here; " +
                    "the outer repository will see this folder as an embedded repository.", MessageType.Warning);
            EditorGUILayout.LabelField("Remote", GitState.HasRemote ? currentRemoteUrl : "None (local backups only)",
                EditorStyles.wordWrappedLabel);

            GUILayout.Space(6f);
            EditorGUILayout.LabelField("Templates (" + GitTemplates.RootPath + ")", EditorStyles.boldLabel);
            ignoreChoice = EditorGUILayout.Popup(".gitignore", ignoreChoice, ignoreOptions);
            attributeChoice = EditorGUILayout.Popup(".gitattributes", attributeChoice, attributeOptions);

            if (!GitState.HasRemote)
                remoteUrl = EditorGUILayout.TextField(new GUIContent("Remote URL (optional)",
                    "Added as " + AutoSync.Remote + ". Credentials are handled by Git itself."), remoteUrl).Trim();
            if (IsHttps(GitState.HasRemote ? currentRemoteUrl : remoteUrl))
                EditorGUILayout.HelpBox(CloudflareWarning, MessageType.Warning);

            if (GUILayout.Button("Apply", GUILayout.Height(28f))) GitActions.Guard(Apply);

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
                GitActions.Guard(() =>
                {
                    if (File.Exists(GitTemplates.PathFor(templateName, extension)) &&
                        !EditorUtility.DisplayDialog("Save Template", templateName + extension + " exists. Replace it?", "Replace", "Cancel"))
                        return;
                    GitTemplates.Save(templateName, extension, File.ReadAllText(ProjectFile(extension)));
                    Reload();
                });
            }
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
                    GitActions.Guard(() =>
                    {
                        GitLfs.Track(ProjectFile(GitTemplates.AttributesExtension), report.NotTrackedExtensions);
                        report = GitLfs.Check(RiryToolsPaths.ProjectRoot, GitState.HasRemote ? AutoSync.Remote : null);
                    });
                }
            }
            if (report.OverProxyLimit.Count > 0)
                EditorGUILayout.HelpBox(report.OverProxyLimit.Count + " file(s) exceed 100 MB. Over HTTPS, a server behind " +
                    "Cloudflare rejects them; use an SSH remote.\n" +
                    string.Join("\n", report.OverProxyLimit.GetRange(0, Math.Min(8, report.OverProxyLimit.Count))), MessageType.Warning);
        }
    }

    internal sealed class GitHelpWindow : EditorWindow
    {
        private Vector2 scroll;

        internal static void Open()
        {
            GitHelpWindow window = GetWindow<GitHelpWindow>(true, "Git Help");
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
                "Git > Check for Broken References can relink GUIDs from that history.");
            Section("How",
                "1. Install Git (Git for Windows includes Git LFS).\n" +
                "2. Choose Git > Configure Sync... and pick a .gitignore and .gitattributes template. A repository is created " +
                "if needed and Git LFS is enabled, so large models, textures and audio are stored efficiently.\n" +
                "3. Optionally enter a remote URL. Sign-in is handled by Git and your credential manager, not by this tool.\n" +
                "4. Keep Git > AutoSync Scene∕Prefab checked.\n\n" +
                "To restore a file: git checkout AutoSync -- path/to/Scene.unity path/to/Scene.unity.meta");
            Section("Large files and Cloudflare",
                "Servers behind Cloudflare proxies (including Zero Trust tunnels to Gitea or Forgejo) reject any single HTTPS " +
                "upload over 100 MB, and Git LFS cannot split uploads into chunks. Use an SSH remote (git@host:owner/repo.git) " +
                "for these servers. Git LFS only stays on SSH when the server allows it: on Gitea or Forgejo set " +
                "LFS_ALLOW_PURE_SSH = true under [server] in app.ini; otherwise LFS falls back to HTTPS.");
            Section("Templates",
                "Templates live in " + GitTemplates.RootPath + " as <name>.gitignore and <name>.gitattributes, for example " +
                "vrcproject.gitignore. Without any, GitHub's Unity.gitignore and the gitattributes project's Unity.gitattributes " +
                "are downloaded, and a built-in LFS template is added.");
            if (GUILayout.Button("Download Git (git-scm.com)", GUILayout.Height(26f)))
                Application.OpenURL(GitNotInstalledException.DownloadUrl);
            if (GUILayout.Button("Open Template Folder"))
                EditorUtility.RevealInFinder(GitTemplates.EnsureRoot());
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

// ============================================================================
// GLOSSARY: Git windows
// The Git menu entries are in RiryMenus.cs; what they do is in GitActions.
// ============================================================================
//
// AutoSyncWindow (Git > AutoSync Settings..., this file)
//   Open() / OnGUI()        Settings fields, last run, Sync Now, Install Rebase Workflow.
//   Install()               Writes .github/workflows/rirytools-autosync-rebase.yaml (asks before replacing).
//   Settings it edits (AutoSync, Editor/AutoSync/AutoSync.cs; stored per user and project):
//     AutoSync.Enabled      bool    AutoSync on (default: on once the project is a repository).
//     AutoSync.Branch       string  Branch receiving snapshots (default "AutoSync").
//     AutoSync.BaseBranch   string  Base for a new branch and for the rebase workflow (default "main").
//     AutoSync.PushEnabled  bool    Push after each snapshot.
//     AutoSync.Remote       string  Push target (default "origin").
//     AutoSync.Status       string  Outcome of the last pass.
//     AutoSync.SettingsError()  Why the settings are invalid, or null.
//     AutoSync.ScheduleNow()    Runs a pass on the next editor update.
//     AutoSync.WorkflowPattern(branch)  Branch filter written into the workflow.
//
// GitConfigureWindow (Git > Configure Sync...)
//   UI (this file)
//     Open()                Opens the window and loads templates.
//     CloudflareWarning     const string  Warning shown for HTTPS remotes.
//     OnGUI()               Repository and remote, template popups, remote URL, Apply, Save template, LFS report.
//     SaveButton(extension) Saves the project's .gitignore or .gitattributes as a named template.
//     DrawReport()          Git LFS check results and the Track button.
//   FIELDS AND ACTIONS (GitActions.cs)
//     ignoreOptions / attributeOptions  string[]  Template popup entries.
//     ignoreChoice / attributeChoice    int       Selected entries.
//     remoteUrl             string     Remote to add when there is none.
//     currentRemoteUrl      string     URL of the configured remote.
//     templateName          string     Name for Save template.
//     report                GitLfs.Report  Last LFS check.
//     scroll                Vector2    Scroll position.
//     KeepCurrent           const string  "(keep current file)" popup entry.
//     Reload()              Downloads default templates if needed, refreshes options and state.
//     Options(extension)    Popup entries for one template type.
//     Apply()               Creates the repository, copies templates, enables LFS, adds the remote, checks LFS.
//     CopyTemplate(option, extension)  Copies one template into the project (asks before replacing).
//     IsHttps(url) / ProjectFile(name) / Check(result, action)  Small helpers.
//
// GitHelpWindow (Git > Help, this file)
//   Open() / OnGUI() / Section(title, text)  What, why and how; Download Git; Open Template Folder.
//
// GitActions (Editor/AutoSync/GitActions.cs)
//   ToggleAutoSync()        Turns AutoSync on or off (offers Configure Sync without a repository).
//   SyncNow()               Runs a pass now.
//   ConfigureSync()         Opens Configure Sync (throws GitNotInstalledException without git).
//   ConfigureSyncAvailable() True while there is no git, repository or remote.
//   Guard(action)           Runs an action; shows a missing git or a failure as a dialog.
//   Static constructor      Offers Configure Sync once per session without a repository.
