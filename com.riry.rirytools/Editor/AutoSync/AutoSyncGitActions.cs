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
    /// in Editor/UI/RiryToolsMenuBar.cs; the Configuration window is drawn in Editor/UI/AutoSyncGitWindow.cs.
    /// </summary>
    internal static class AutoSyncGitActions
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
                    Check(AutoSyncGitRunner.Run(project, null, 30000, "init"), "Creating the repository");
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
                    // Commits work without LFS; it only matters for large files on a remote. The dialog says it, so no log.
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
                AutoSyncGitConfigureWindow.Open();
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

        internal static void Check(AutoSyncGitRunner.Result result, string action)
        {
            if (!result.Succeeded) throw new InvalidOperationException(result.Describe(action));
        }

        /// <summary>Runs a menu action; a missing git is shown, a failed step shown and logged as an exception.</summary>
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
                // A setup state shown in the dialog; a stack trace would add nothing.
                if (EditorUtility.DisplayDialog("Git Not Installed", exception.Message, "Download Git", "Close"))
                    Application.OpenURL(GitNotInstalledException.DownloadUrl);
            }
            catch (Exception exception)
            {
                RiryLog.Exception(RiryLog.AutoSync, "A Git action failed", exception);
                EditorUtility.DisplayDialog("Git", exception.Message, "OK");
            }
        }
    }
}
