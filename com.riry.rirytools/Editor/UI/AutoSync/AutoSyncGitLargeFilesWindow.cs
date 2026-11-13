using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>Large Files: state and actions. The drawing is in AutoSyncGitLargeFilesWindow.UI.cs.</summary>
    internal sealed partial class AutoSyncGitLargeFilesWindow : EditorWindow
    {
        private List<string> files = new List<string>();
        private readonly HashSet<string> toIgnore = new HashSet<string>();
        private Vector2 scroll;

        private void Load()
        {
            toIgnore.Clear();
            AutoSyncGitActions.Guard(() => files = AutoSyncGitLargeFiles.Tracked(RiryToolsPaths.ProjectRoot));
        }

        /// <summary>Selects and pings the file in the Project window when it is an asset.</summary>
        private static void Ping(string path)
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset == null) return;
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        /// <summary>Ignores the marked files and takes them out of the unpushed commits.</summary>
        private void Ok()
        {
            if (toIgnore.Count > 0)
            {
                string project = RiryToolsPaths.ProjectRoot;
                AutoSync.CommitNow();
                AutoSyncGitLargeFiles.Ignore(project, AutoSyncGitRemotes.Selected(AutoSyncGitRemotes.Names(project)), new List<string>(toIgnore));
            }
            Close();
            if (HasOpenInstances<AutoSyncGitConfigureWindow>()) GetWindow<AutoSyncGitConfigureWindow>().Reload();
        }
    }
}

// ============================================================================
// GLOSSARY: AutoSync Large Files window, fields and actions
// Drawing is in AutoSyncGitLargeFilesWindow.UI.cs.
// ============================================================================
//
// AutoSyncGitLargeFilesWindow
//   files                 List<string>     Tracked files over 100 MiB.
//   toIgnore              HashSet<string>  Files marked Add to ignore.
//   Load() / Ping(path)   Lists the files; selects and pings one in the Project window.
//   Ok()                  Adds the marked files to .gitignore and drops them from unpushed commits (AutoSyncGitLargeFiles.Ignore).
