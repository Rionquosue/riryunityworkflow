using System.IO;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>What Large Files draws. State and actions are in AutoSyncGitLargeFilesWindow.cs.</summary>
    internal sealed partial class AutoSyncGitLargeFilesWindow
    {
        internal static void Open()
        {
            AutoSyncGitLargeFilesWindow window = GetWindow<AutoSyncGitLargeFilesWindow>(true, "Large Files");
            window.minSize = new Vector2(460f, 300f);
            window.Load();
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(AutoSyncGitConfigureWindow.CloudflareWarning, MessageType.Warning);
            EditorGUILayout.LabelField(files.Count + " tracked file(s) over 100 MiB. Click one to show it in the Project window.",
                EditorStyles.wordWrappedLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (string path in files)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(AssetDatabase.LoadMainAssetAtPath(path) == null))
                    {
                        // MinWidth 0: a deep path is cut off (full in the tooltip) instead of pushing Add to ignore out of view.
                        if (GUILayout.Button(new GUIContent(path, path), EditorStyles.miniButtonLeft, GUILayout.MinWidth(0f))) Ping(path);
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
            if (GUILayout.Button("OK", GUILayout.Height(28f))) { AutoSyncGitActions.Guard(Ok); RiryToolGUI.ExitAfterModal(); }
        }
    }
}

// ============================================================================
// GLOSSARY: AutoSync Large Files window, drawing
// Fields and actions are in AutoSyncGitLargeFilesWindow.cs.
// ============================================================================
//
// AutoSyncGitLargeFilesWindow (the ! button in Configuration, or Fix Now in the Push Held dialog)
//   Open() / OnGUI()      Cloudflare warning; one button per file (shows it in the Project window) with
//                         Add to ignore; OK.
