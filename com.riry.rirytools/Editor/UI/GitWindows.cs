using System.IO;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>What Configuration draws. State and actions are in Editor/AutoSync/GitActions.cs.</summary>
    internal sealed partial class GitConfigureWindow
    {
        internal static void Open()
        {
            GitConfigureWindow window = GetWindow<GitConfigureWindow>(true, "Configuration");
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

            GUILayout.Space(6f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Templates (" + GitTemplates.RootPath + ")", EditorStyles.boldLabel);
                ignoreChoice = EditorGUILayout.Popup(".gitignore", ignoreChoice, ignoreOptions);
                attributeChoice = EditorGUILayout.Popup(".gitattributes", attributeChoice, attributeOptions);
                GUILayout.Space(4f);
                EditorGUILayout.LabelField("Save the project's current files as named templates", EditorStyles.miniBoldLabel);
                templateName = EditorGUILayout.TextField("Template name", templateName).Trim();
                using (new EditorGUI.DisabledScope(ImportPresetLibrary.ValidateName(templateName) != null))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        SaveButton(GitTemplates.IgnoreExtension);
                        SaveButton(GitTemplates.AttributesExtension);
                    }
                }
                if (GUILayout.Button("Open Template Folder")) EditorUtility.RevealInFinder(GitTemplates.EnsureRoot());
            }

            GUILayout.Space(6f);
            using (new EditorGUI.DisabledScope(!GitState.RepositoryExists))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    remoteUrl = EditorGUILayout.TextField(new GUIContent("Push to remote",
                        "Each Sync commit is pushed here as " + AutoSyncJob.Remote + ". Sign-in is handled by Git's credential manager."),
                        remoteUrl).Trim();
                    // Add Remote without origin; Change Remote once it exists.
                    using (new EditorGUI.DisabledScope(remoteUrl.Length == 0 || remoteUrl == currentRemoteUrl))
                    {
                        if (GUILayout.Button(currentRemoteUrl.Length == 0 ? "Add Remote" : "Change Remote", GUILayout.Width(110f)))
                            GitActions.Guard(SaveRemote);
                    }
                }
            }
            // SSH remotes (git@host:... or ssh://) are not affected.
            if (IsHttps(remoteUrl)) EditorGUILayout.HelpBox(CloudflareWarning, MessageType.Warning);

            GUILayout.Space(6f);
            if (GUILayout.Button("Apply", GUILayout.Height(28f))) GitActions.Guard(Apply);
            EditorGUILayout.EndScrollView();
        }

        private void SaveButton(string extension)
        {
            using (new EditorGUI.DisabledScope(!File.Exists(GitActions.ProjectFile(extension))))
            {
                if (!GUILayout.Button("Save " + extension)) return;
                GitActions.Guard(() =>
                {
                    if (File.Exists(GitTemplates.PathFor(templateName, extension)) &&
                        !EditorUtility.DisplayDialog("Save Template", templateName + extension + " exists. Replace it?", "Replace", "Cancel"))
                        return;
                    GitTemplates.Save(templateName, extension, File.ReadAllText(GitActions.ProjectFile(extension)));
                    Reload();
                });
            }
        }
    }
}

// ============================================================================
// GLOSSARY: Git windows
// The menu entries are in RiryMenus.cs; what they do is in GitActions.
// ============================================================================
//
// GitConfigureWindow (Configuration...)
//   UI (this file)
//     Open()                Opens the window and loads templates and the remote.
//     CloudflareWarning     const string  Warning shown under Push to remote for an HTTPS remote.
//     OnGUI()               Repository; Templates box (popups, Template name, Save buttons, Open Template Folder);
//                           Push to remote with Add Remote / Change Remote and the Cloudflare warning; Apply.
//     SaveButton(extension) Saves the project's .gitignore or .gitattributes as a named template.
//   FIELDS AND ACTIONS (Editor/AutoSync/GitActions.cs)
//     ignoreOptions / attributeOptions  string[]  Template popup entries.
//     ignoreChoice / attributeChoice    int       Selected entries.
//     remoteUrl             string     Push to remote, as edited.
//     currentRemoteUrl      string     URL of origin, or "".
//     templateName          string     Name for Save template.
//     scroll                Vector2    Scroll position.
//     KeepCurrent           const string  "(keep current file)" popup entry.
//     Reload()              Creates default templates if needed, refreshes options and the remote.
//     Options(extension)    Popup entries for one template type.
//     Apply()               Copies the chosen templates (asks before replacing), then SaveRemote() when the URL changed.
//     SaveRemote()          Adds origin (Add Remote) or changes its URL (Change Remote).
//     IsHttps(url)          True for an http:// or https:// remote.
//
// GitActions (Editor/AutoSync/GitActions.cs)
//   ToggleSync()            Turns Sync off, or on (asks for Git, creates the repository, enables Git LFS).
//   OpenConfiguration()     Opens Configuration (throws GitNotInstalledException without git).
//   EnsureTemplates()       Creates the template folder with the Unity defaults and says so.
//   CopyTemplate(name, extension, mayReplace)  Copies one template into the project.
//   ProjectFile(name) / Check(result, action)  Small helpers.
//   Guard(action)           Runs an action; shows a missing git or a failure as a dialog.
