#if UNITY_EDITOR
// Target editor: Unity 2022.3 LTS (including 2022.3.22f1).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Riry.UnityWorkflow.Editor
{
    /// <summary>
    /// Detects a scene or prefab that loaded with far fewer objects than its file
    /// holds (for example a package script that makes a scene load empty), names the
    /// script that logged errors during the load, blocks saving it so the good file
    /// is not overwritten, and holds the editor in a modal window until it is resolved.
    /// </summary>
    [InitializeOnLoad]
    internal static class ScriptLoadGuard
    {
        private const string GuardedKey = "Riry.ScriptLoadGuard.Guarded";
        private const double CaptureSeconds = 5.0;
        private const int RecentLimit = 50;

        internal sealed class Failure
        {
            public string Path;
            public int Expected;
            public int Loaded;
            public readonly List<string> Suspects = new List<string>();
            public readonly List<string> Errors = new List<string>();
        }

        private struct LoggedError
        {
            public double Time;
            public string Message;
            public string StackTrace;
        }

        // Unity stack frames: "Namespace.Type:Method (args) (at path:line)".
        private static readonly Regex Frame = new Regex(@"^(?<type>[\w.`+<>]+)[:.](?<method>[\w`<>]+) ?\(.*?\)(?: \(at (?<path>.+?):\d+\))?\s*$",
            RegexOptions.CultureInvariant);
        private static readonly string[] EngineNamespaces = { "UnityEngine.", "UnityEditor.", "Unity.", "System.", "Mono.", "Riry.UnityWorkflow." };
        private static readonly List<LoggedError> recent = new List<LoggedError>();
        private static readonly HashSet<string> overridden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static double openingStarted;

        internal static Failure Current;

        static ScriptLoadGuard()
        {
            Application.logMessageReceived += (message, stackTrace, type) =>
            {
                if (type == LogType.Log || type == LogType.Warning) return;
                recent.Add(new LoggedError { Time = EditorApplication.timeSinceStartup, Message = message, StackTrace = stackTrace });
                if (recent.Count > RecentLimit) recent.RemoveAt(0);
            };
            EditorSceneManager.sceneOpening += (path, mode) => openingStarted = EditorApplication.timeSinceStartup;
            EditorSceneManager.sceneOpened += (scene, mode) =>
                EditorApplication.delayCall += () => CheckScene(scene.path, openingStarted);
            PrefabStage.prefabStageOpened += stage =>
                EditorApplication.delayCall += () => CheckPrefab(stage, EditorApplication.timeSinceStartup - CaptureSeconds);
            // A closed file is no longer in memory, so it can no longer be saved broken.
            EditorSceneManager.sceneClosed += scene => SetGuarded(scene.path, false);
            PrefabStage.prefabStageClosing += stage => SetGuarded(stage.assetPath, false);
            // A script reload keeps the block: the scene is still the broken one in memory.
            if (Guarded().Count > 0) EditorApplication.delayCall += ScriptLoadFailureWindow.Open;
        }

        internal static bool IsGuarded(string path)
        {
            return Guarded().Contains(path);
        }

        internal static HashSet<string> Guarded()
        {
            return new HashSet<string>(SessionState.GetString(GuardedKey, "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries),
                StringComparer.OrdinalIgnoreCase);
        }

        internal static void SetGuarded(string path, bool guarded)
        {
            HashSet<string> paths = Guarded();
            if (guarded) paths.Add(path);
            else paths.Remove(path);
            SessionState.SetString(GuardedKey, string.Join("\n", paths));
        }

        internal static void Override(string path)
        {
            overridden.Add(path);
            SetGuarded(path, false);
        }

        private static void CheckScene(string path, double since)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            if (string.IsNullOrEmpty(path) || !scene.IsValid() || !scene.isLoaded || overridden.Contains(path)) return;
            int loaded = 0;
            foreach (GameObject root in scene.GetRootGameObjects()) loaded += root.GetComponentsInChildren<Transform>(true).Length;
            Evaluate(path, loaded, since);
        }

        private static void CheckPrefab(PrefabStage stage, double since)
        {
            if (stage == null || string.IsNullOrEmpty(stage.assetPath) || overridden.Contains(stage.assetPath)) return;
            Evaluate(stage.assetPath,
                stage.prefabContentsRoot == null ? 0 : stage.prefabContentsRoot.GetComponentsInChildren<Transform>(true).Length, since);
        }

        private static void Evaluate(string path, int loaded, double since)
        {
            int expected = CountSerializedObjects(Path.Combine(RegenerateAssetGuid.ProjectRootPath, path));
            if (!LoadFailed(expected, loaded))
            {
                SetGuarded(path, false);
                return;
            }
            var failure = new Failure { Path = path, Expected = expected, Loaded = loaded };
            foreach (LoggedError error in recent)
            {
                if (error.Time < since) continue;
                failure.Errors.Add(error.Message.Split('\n')[0]);
                foreach (string suspect in Suspects(error.StackTrace))
                {
                    if (!failure.Suspects.Contains(suspect)) failure.Suspects.Add(suspect);
                }
            }
            Current = failure;
            SetGuarded(path, true);
            Debug.LogError("SCRIPT CAUSING FAILED LOAD: " + path + " has " + expected + " objects on disk but loaded " + loaded +
                ". Saving it is blocked." + (failure.Suspects.Count > 0 ? " Suspected: " + string.Join(", ", failure.Suspects) : ""));
            ScriptLoadFailureWindow.Open();
        }

        /// <summary>A load failed when the file has objects and none, or fewer than half, loaded.</summary>
        internal static bool LoadFailed(int expected, int loaded)
        {
            return expected > 0 && (loaded == 0 || loaded * 2 < expected);
        }

        /// <summary>
        /// GameObjects the file owns (prefab-owned "stripped" ones excluded), plus one
        /// for each prefab instance. -1 for a binary or unreadable file.
        /// </summary>
        internal static int CountSerializedObjects(string fullPath)
        {
            if (!File.Exists(fullPath) || !RegenerateAssetGuid.LooksLikeText(fullPath, new byte[4096])) return -1;
            int count = 0;
            foreach (string line in File.ReadLines(fullPath))
            {
                if ((line.StartsWith("--- !u!1 &", StringComparison.Ordinal) && !line.EndsWith(" stripped", StringComparison.Ordinal)) ||
                    line.StartsWith("--- !u!1001 &", StringComparison.Ordinal))
                    count++;
            }
            return count;
        }

        /// <summary>The first frame of a stack trace outside Unity, .NET and this package: "Type (path)".</summary>
        internal static List<string> Suspects(string stackTrace)
        {
            var suspects = new List<string>();
            foreach (string line in (stackTrace ?? "").Split('\n'))
            {
                Match match = Frame.Match(line.Trim());
                if (!match.Success || Array.Exists(EngineNamespaces, prefix =>
                        match.Groups["type"].Value.StartsWith(prefix, StringComparison.Ordinal)))
                    continue;
                suspects.Add(match.Groups["type"].Value +
                    (match.Groups["path"].Success ? " (" + match.Groups["path"].Value.TrimStart('.', '/') + ")" : ""));
                break;
            }
            return suspects;
        }
    }

    /// <summary>Blocks saving a scene or prefab whose load failed, so the file on disk stays intact.</summary>
    internal sealed class ScriptLoadSaveBlocker : AssetModificationProcessor
    {
        private static string[] OnWillSaveAssets(string[] paths)
        {
            var allowed = new List<string>();
            foreach (string path in paths)
            {
                if (!ScriptLoadGuard.IsGuarded(path))
                {
                    allowed.Add(path);
                    continue;
                }
                Debug.LogError("SCRIPT CAUSING FAILED LOAD: saving " + path +
                    " was blocked because it did not load correctly; the file on disk was kept.");
                EditorApplication.delayCall += ScriptLoadFailureWindow.Open;
            }
            return allowed.ToArray();
        }
    }

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
#endif
