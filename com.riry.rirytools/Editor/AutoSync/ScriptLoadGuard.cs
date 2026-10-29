using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Detects a scene or prefab that loaded with far fewer objects than its file
    /// holds (for example a package script that makes a scene load empty), names the
    /// script that logged errors during the load, blocks saving it so the good file
    /// is not overwritten, and holds the editor in a modal window until it is resolved.
    /// The window is in Editor/UI/ScriptLoadFailureWindow.cs.
    /// </summary>
    [InitializeOnLoad]
    internal static class ScriptLoadGuard
    {
        private const string GuardedKey = "RiryTools.ScriptLoadGuard.Guarded";
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
        private static readonly string[] EngineNamespaces = { "UnityEngine.", "UnityEditor.", "Unity.", "System.", "Mono.", "Riry.RiryTools." };
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
            int expected = CountSerializedObjects(Path.Combine(RiryToolsPaths.ProjectRoot, path));
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
            if (!File.Exists(fullPath) || !RegenerateGuid.LooksLikeText(fullPath, new byte[4096])) return -1;
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

}
