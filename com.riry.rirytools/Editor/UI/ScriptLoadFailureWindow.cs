using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>A modal window that keeps the editor unusable until the failed load is dealt with.</summary>
    internal sealed class ScriptLoadFailureWindow : EditorWindow
    {
        private Action pending;
        private Vector2 scroll;

        internal static void Open()
        {
            if (ScriptLoadGuard.Guarded().Count == 0 || HasOpenInstances<ScriptLoadFailureWindow>()) return;
            ScriptLoadFailureWindow window = CreateInstance<ScriptLoadFailureWindow>();
            window.titleContent = new GUIContent("SCRIPT CAUSING FAILED LOAD");
            window.minSize = new Vector2(520f, 360f);
            window.ShowModalUtility();
        }

        private void OnGUI()
        {
            ScriptLoadGuard.Failure failure = ScriptLoadGuard.Current;
            HashSet<string> guarded = ScriptLoadGuard.Guarded();
            string path = failure != null && guarded.Contains(failure.Path) ? failure.Path : string.Join(", ", guarded);
            GUIStyle title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, wordWrap = true };
            title.normal.textColor = new Color(1f, 0.3f, 0.3f);
            EditorGUILayout.LabelField("SCRIPT CAUSING FAILED LOAD", title, GUILayout.Height(24f));
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField(path + (failure != null && failure.Path == path
                ? " has " + failure.Expected + " objects on disk, but only " + failure.Loaded + " loaded."
                : " did not load correctly."), EditorStyles.wordWrappedLabel);
            EditorGUILayout.HelpBox(failure != null && failure.Suspects.Count > 0
                ? "Suspected script:\n" + string.Join("\n", failure.Suspects)
                : "The script could not be identified. Check the Console for errors logged while the file opened.",
                MessageType.Error);
            if (failure != null && failure.Errors.Count > 0)
                EditorGUILayout.LabelField("Errors during the load:\n" + string.Join("\n", failure.Errors.GetRange(0, Math.Min(6, failure.Errors.Count))),
                    EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.HelpBox("Saving now would overwrite the good file with the broken result, so saving is blocked. " +
                "Remove or update the script (or its package), then reload.", MessageType.Warning);
            EditorGUILayout.EndScrollView();

            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            bool prefab = stage != null && guarded.Contains(stage.assetPath);
            string scene = new List<string>(guarded).Find(candidate => candidate.EndsWith(".unity", StringComparison.OrdinalIgnoreCase));
            if (GUILayout.Button(prefab ? "Close Prefab Mode" : "Reload From Disk", GUILayout.Height(26f)))
                Finish(() =>
                {
                    foreach (string guardedPath in guarded) ScriptLoadGuard.SetGuarded(guardedPath, false);
                    if (prefab) StageUtility.GoToMainStage();
                    // Reopening runs the check again; a second failure blocks again.
                    else if (scene != null) EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
                });
            if (GUILayout.Button("Open Empty Scene"))
                Finish(() =>
                {
                    foreach (string guardedPath in guarded) ScriptLoadGuard.SetGuarded(guardedPath, false);
                    if (prefab) StageUtility.GoToMainStage();
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                });
            if (GUILayout.Button("Quit Unity Without Saving"))
                Finish(() => EditorApplication.Exit(0));
            if (GUILayout.Button("The File Loaded Correctly (Allow Saving)", EditorStyles.miniButton) &&
                EditorUtility.DisplayDialog("Allow Saving",
                    "Only continue if the file really is complete. Saving a broken load overwrites the good file.",
                    "Allow Saving", "Cancel"))
                Finish(() => { foreach (string guardedPath in guarded) ScriptLoadGuard.Override(guardedPath); });
        }

        private void Finish(Action action)
        {
            pending = action;
            Close();
        }

        private void OnDestroy()
        {
            Action action = pending;
            // Closing the window any other way brings it back while the block stands.
            EditorApplication.delayCall += () =>
            {
                if (action != null) action();
                Open();
            };
        }
    }
}

// ============================================================================
// GLOSSARY: SCRIPT CAUSING FAILED LOAD window
// ============================================================================
//
// ScriptLoadFailureWindow (this file)
//   Open()            Shows the modal window while any file is blocked.
//   OnGUI()           Title, counts, suspected script, errors, and the four buttons.
//   Finish(action)    Closes the window, then runs the chosen action.
//   OnDestroy()       Runs the action, then reopens the window if a block remains.
//   pending           Action   The button's action, run after the window closes.
//   scroll            Vector2  Scroll position.
//
// ScriptLoadGuard (Editor/AutoSync/ScriptLoadGuard.cs)
//   Current           Failure  Path, Expected and Loaded object counts, Suspects, Errors.
//   Guarded()         Paths whose saving is blocked (kept for the session).
//   IsGuarded(path) / SetGuarded(path, guarded) / Override(path)
//   LoadFailed(expected, loaded)  None, or fewer than half, of the objects loaded.
//   CountSerializedObjects(path)  GameObjects and prefab instances in a scene or prefab file.
//   Suspects(stackTrace)          First stack frame outside Unity, .NET and this package.
// ScriptLoadSaveBlocker (ScriptLoadGuard.cs): removes blocked files from every save.
