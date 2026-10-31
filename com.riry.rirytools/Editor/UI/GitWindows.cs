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
            if (branches.Length > 0)
            {
                int chosen = EditorGUILayout.Popup(new GUIContent("Branch", "Choosing a branch checks it out (git switch)."),
                    branchChoice, branches);
                if (chosen != branchChoice)
                {
                    string branch = branches[chosen];
                    EditorApplication.delayCall += () => GitActions.Guard(() => SwitchBranch(branch));
                }
            }

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
            if (remotes.Count > 1)
            {
                int index = remotes.IndexOf(selectedRemote);
                int chosen = EditorGUILayout.Popup(new GUIContent("Remote", "The remote Sync pushes to."), index, remotes.ToArray());
                if (chosen != index)
                {
                    string remote = remotes[chosen];
                    EditorApplication.delayCall += () => GitActions.Guard(() => SelectRemote(remote));
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
                            GitActions.Guard(SaveRemote);
                    }
                    // Files over 100 MiB fail behind a Cloudflare proxy, which only affects HTTP(S).
                    if (GitRemotes.IsHttps(remoteUrl) && largeFiles.Count > 0 &&
                        GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("console.warnicon.sml").image,
                            largeFiles.Count + " tracked file(s) over 100 MiB"), GUILayout.Width(28f)))
                        LargeFilesWindow.Open();
                }
            }
            // SSH remotes (git@host:... or ssh://) are not affected.
            if (GitRemotes.IsHttps(remoteUrl)) EditorGUILayout.HelpBox(CloudflareWarning, MessageType.Warning);
            if (lfsStatus != null)
                EditorGUILayout.HelpBox(lfsStatus, lfsStatus.StartsWith("Git LFS is enabled", System.StringComparison.Ordinal)
                    ? MessageType.Info
                    : MessageType.Warning);

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

    /// <summary>What Large Files draws. State and actions are in Editor/AutoSync/GitActions.cs.</summary>
    internal sealed partial class LargeFilesWindow
    {
        internal static void Open()
        {
            LargeFilesWindow window = GetWindow<LargeFilesWindow>(true, "Large Files");
            window.minSize = new Vector2(460f, 300f);
            window.Load();
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(GitConfigureWindow.CloudflareWarning, MessageType.Warning);
            EditorGUILayout.LabelField(files.Count + " tracked file(s) over 100 MiB. Click one to show it in the Project window.",
                EditorStyles.wordWrappedLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (string path in files)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(AssetDatabase.LoadMainAssetAtPath(path) == null))
                    {
                        if (GUILayout.Button(new GUIContent(path, path), EditorStyles.miniButtonLeft)) Ping(path);
                    }
                    bool marked = toIgnore.Contains(path);
                    if (GUILayout.Toggle(marked, "Add to ignore", EditorStyles.miniButtonRight, GUILayout.Width(100f)) != marked)
                    {
                        if (marked) toIgnore.Remove(path);
                        else toIgnore.Add(path);
                    }
                }
            }
            EditorGUILayout.EndScrollView();
            if (GUILayout.Button("OK", GUILayout.Height(28f))) GitActions.Guard(Ok);
        }
    }
}

// ============================================================================
// GLOSSARY: Git windows
// The menu entries are in RiryMenus.cs; what they do is in GitActions.
// ============================================================================
//
// GitConfigureWindow (Configuration)
//   UI (this file)
//     Open()                Opens the window and loads templates, branches and remotes.
//     CloudflareWarning     const string  Warning for an HTTP(S) remote (also in Large Files).
//     OnGUI()               Repository; Branch; Templates box (popups, Template name, Save buttons, Open Template
//                           Folder); Remote (with several remotes); Push to remote with Add Remote / Change Remote
//                           and the ! button; Cloudflare warning; Git LFS status; Apply.
//     SaveButton(extension) Saves the project's .gitignore or .gitattributes as a named template.
//   FIELDS AND ACTIONS (Editor/AutoSync/GitActions.cs)
//     ignoreOptions / attributeOptions  string[]  Template popup entries.
//     ignoreChoice / attributeChoice    int       Selected entries.
//     branches / branchChoice           Local branches, the remote's other branches, the current one chosen.
//     remotes / selectedRemote          Remote names; the one Sync pushes to (GitRemotes.Selected).
//     remoteUrl             string     Push to remote, as edited.
//     currentRemoteUrl      string     URL of the chosen remote, or "".
//     largeFiles            List<string>  Tracked files over 100 MiB (shows the ! button).
//     lfsQuery / lfsStatus  The Git LFS query running off the main thread, and its result line.
//     templateName          string     Name for Save template.
//     scroll                Vector2    Scroll position.
//     KeepCurrent / Detached  const string  "(keep current file)" and "(detached HEAD)" popup entries.
//     Reload()              Creates default templates if needed; reloads branches, remotes, large files; starts the LFS query.
//     LoadBranches(project) Fills the Branch popup.
//     StartLfsQuery()       Asks the chosen remote whether Git LFS is enabled (GitLfsQuery.Ask).
//     OnInspectorUpdate()   Shows the LFS query's result when it finishes.
//     Options(extension)    Popup entries for one template type.
//     SwitchBranch(name)    Saves modified scenes if wanted, commits pending saves, runs git switch.
//     SelectRemote(name)    Stores the remote Sync pushes to and reloads.
//     Apply()               Copies the chosen templates (asks before replacing), then SaveRemote() when the URL changed.
//     SaveRemote()          Adds origin (Add Remote) or changes the chosen remote's URL (Change Remote).
//
// LargeFilesWindow (the ! button, or Fix Now in the Push Held dialog)
//   UI (this file)
//     Open() / OnGUI()      Cloudflare warning; one button per file (shows it in the Project window) with
//                           Add to ignore; OK.
//   FIELDS AND ACTIONS (Editor/AutoSync/GitActions.cs)
//     files                 List<string>     Tracked files over 100 MiB.
//     toIgnore              HashSet<string>  Files marked Add to ignore.
//     Load() / Ping(path)   Lists the files; selects and pings one in the Project window.
//     Ok()                  Adds the marked files to .gitignore and drops them from unpushed commits (GitLargeFiles.Ignore).
//
// GitActions (Editor/AutoSync/GitActions.cs)
//   ToggleSync()            Turns Sync off, or on (asks for Git, creates the repository, enables Git LFS).
//   OpenConfiguration()     Opens Configuration (throws GitNotInstalledException without git).
//   EnsureTemplates()       Creates the template folder with the Unity defaults and says so.
//   CopyTemplate(name, extension, mayReplace)  Copies one template into the project.
//   ProjectFile(name) / Check(result, action)  Small helpers.
//   Guard(action)           Runs an action; shows a missing git or a failure as a dialog.
