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

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Repository", GitState.RepositoryExists
                ? RiryToolsPaths.ProjectRoot
                : "None. Turning on Sync creates one.", EditorStyles.wordWrappedLabel);

            GUILayout.Space(6f);
            EditorGUILayout.LabelField("Templates (" + GitTemplates.RootPath + ")", EditorStyles.boldLabel);
            ignoreChoice = EditorGUILayout.Popup(".gitignore", ignoreChoice, ignoreOptions);
            attributeChoice = EditorGUILayout.Popup(".gitattributes", attributeChoice, attributeOptions);

            GUILayout.Space(6f);
            using (new EditorGUI.DisabledScope(!GitState.RepositoryExists))
                remoteUrl = EditorGUILayout.TextField(new GUIContent("Push to remote",
                    "Each Sync commit is pushed here as " + AutoSyncJob.Remote + ". Sign-in is handled by Git's credential manager."),
                    remoteUrl).Trim();

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
            if (GUILayout.Button("Open Template Folder")) EditorUtility.RevealInFinder(GitTemplates.EnsureRoot());
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
//     OnGUI()               Repository, template popups, Push to remote, Apply, Save template, Open Template Folder.
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
//     Apply()               Copies the chosen templates (asks before replacing), adds or changes origin.
//
// GitActions (Editor/AutoSync/GitActions.cs)
//   ToggleSync()            Turns Sync off, or on (asks for Git, creates the repository, enables Git LFS).
//   OpenConfiguration()     Opens Configuration (throws GitNotInstalledException without git).
//   EnsureTemplates()       Creates the template folder with the Unity defaults and says so.
//   CopyTemplate(name, extension, mayReplace)  Copies one template into the project.
//   ProjectFile(name) / Check(result, action)  Small helpers.
//   Guard(action)           Runs an action; shows a missing git or a failure as a dialog.
