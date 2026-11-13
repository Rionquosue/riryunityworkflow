using System.IO;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>What Configuration draws. State and actions are in AutoSyncGitConfigureWindow.cs.</summary>
    internal sealed partial class AutoSyncGitConfigureWindow
    {
        internal static void Open()
        {
            AutoSyncGitConfigureWindow window = GetWindow<AutoSyncGitConfigureWindow>(true, "Configuration");
            window.minSize = new Vector2(460f, 300f);
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
                : "None. Turning on Sync creates one.", EditorStyles.wordWrappedLabel);
            if (branches.Length > 0)
            {
                int chosen = EditorGUILayout.Popup(new GUIContent("Branch", "Choosing a branch checks it out (git switch)."),
                    branchChoice, branches);
                if (chosen != branchChoice)
                {
                    string branch = branches[chosen];
                    EditorApplication.delayCall += () => AutoSyncGitActions.Guard(() => SwitchBranch(branch));
                }
            }

            GUILayout.Space(6f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Templates (" + AutoSyncGitTemplates.RootPath + ")", EditorStyles.boldLabel);
                ignoreChoice = EditorGUILayout.Popup(".gitignore", ignoreChoice, ignoreOptions);
                attributeChoice = EditorGUILayout.Popup(".gitattributes", attributeChoice, attributeOptions);
                GUILayout.Space(4f);
                EditorGUILayout.LabelField("Save the project's current files as named templates", EditorStyles.miniBoldLabel);
                templateName = EditorGUILayout.TextField("Template name", templateName).Trim();
                using (new EditorGUI.DisabledScope(ImportPresetLibrary.ValidateName(templateName) != null))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        SaveButton(AutoSyncGitTemplates.IgnoreExtension);
                        SaveButton(AutoSyncGitTemplates.AttributesExtension);
                    }
                }
                if (GUILayout.Button("Open Template Folder")) EditorUtility.RevealInFinder(AutoSyncGitTemplates.EnsureRoot());
            }

            GUILayout.Space(6f);
            if (remotes.Count > 1)
            {
                int index = remotes.IndexOf(selectedRemote);
                int chosen = EditorGUILayout.Popup(new GUIContent("Remote", "The remote Sync pushes to."), index, remotes.ToArray());
                if (chosen != index)
                {
                    string remote = remotes[chosen];
                    EditorApplication.delayCall += () => AutoSyncGitActions.Guard(() => SelectRemote(remote));
                }
            }
            using (new EditorGUI.DisabledScope(!GitState.RepositoryExists))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    remoteUrl = EditorGUILayout.TextField(new GUIContent("Push to remote",
                        "Each Sync commit is pushed here. Sign-in is handled by Git's credential manager."), remoteUrl).Trim();
                    // Add Remote without a remote; Change Remote once one exists.
                    using (new EditorGUI.DisabledScope(remoteUrl.Length == 0 || remoteUrl == currentRemoteUrl))
                    {
                        if (GUILayout.Button(selectedRemote == null ? "Add Remote" : "Change Remote", GUILayout.Width(110f)))
                        {
                            AutoSyncGitActions.Guard(SaveRemote);
                            RiryToolGUI.ExitAfterModal();
                        }
                    }
                    // Files over 100 MiB fail behind a Cloudflare proxy, which only affects HTTP(S).
                    if (AutoSyncGitRemotes.IsHttps(remoteUrl) && largeFiles.Count > 0 &&
                        GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("console.warnicon.sml").image,
                            largeFiles.Count + " tracked file(s) over 100 MiB"), GUILayout.Width(28f)))
                        AutoSyncGitLargeFilesWindow.Open();
                }
            }
            // SSH remotes (git@host:... or ssh://) are not affected.
            if (AutoSyncGitRemotes.IsHttps(remoteUrl)) EditorGUILayout.HelpBox(CloudflareWarning, MessageType.Warning);
            if (lfsStatus != null)
                EditorGUILayout.HelpBox(lfsStatus, lfsStatus.StartsWith("Git LFS is enabled", System.StringComparison.Ordinal)
                    ? MessageType.Info
                    : MessageType.Warning);

            GUILayout.Space(6f);
            if (GUILayout.Button("Apply", GUILayout.Height(28f))) { AutoSyncGitActions.Guard(Apply); RiryToolGUI.ExitAfterModal(); }
            EditorGUILayout.EndScrollView();
        }

        private void SaveButton(string extension)
        {
            using (new EditorGUI.DisabledScope(!File.Exists(AutoSyncGitActions.ProjectFile(extension))))
            {
                if (!GUILayout.Button("Save " + extension)) return;
                AutoSyncGitActions.Guard(() =>
                {
                    if (File.Exists(AutoSyncGitTemplates.PathFor(templateName, extension)) &&
                        !EditorUtility.DisplayDialog("Save Template", templateName + extension + " exists. Replace it?", "Replace", "Cancel"))
                        return;
                    AutoSyncGitTemplates.Save(templateName, extension, File.ReadAllText(AutoSyncGitActions.ProjectFile(extension)));
                    Reload();
                });
                RiryToolGUI.ExitAfterModal();
            }
        }
    }
}

// ============================================================================
// GLOSSARY: AutoSync Configuration window, drawing
// The menu entries are in Editor/UI/RiryToolsMenuBar.cs; what they do is in Editor/AutoSync/AutoSyncGitActions.cs.
// Fields and actions are in AutoSyncGitConfigureWindow.cs.
// ============================================================================
//
// AutoSyncGitConfigureWindow (Configuration)
//   Open()                Opens the window and loads templates, branches and remotes.
//   CloudflareWarning     const string  Warning for an HTTP(S) remote (also in Large Files).
//   OnGUI()               Repository; Branch; Templates box (popups, Template name, Save buttons, Open Template
//                         Folder); Remote (with several remotes); Push to remote with Add Remote / Change Remote
//                         and the ! button; Cloudflare warning; Git LFS status; Apply.
//   SaveButton(extension) Saves the project's .gitignore or .gitattributes as a named template.
