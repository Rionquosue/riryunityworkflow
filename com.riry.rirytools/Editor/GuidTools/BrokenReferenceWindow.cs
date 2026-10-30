using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Lists every missing GUID in a set of files with a redirect field, filled in
    /// from the ledger, logs and Git history where possible. A missing GUID
    /// without a redirect is shown in red until one is chosen. State and actions;
    /// the drawing is in Editor/UI/BrokenReferenceWindow.UI.cs.
    /// </summary>
    internal sealed partial class BrokenReferenceWindow : EditorWindow
    {
        private List<string> files = new List<string>();
        private string reason = "";
        private RepairPlan plan;
        private readonly Dictionary<string, Object> choices = new Dictionary<string, Object>(StringComparer.Ordinal);
        private Vector2 scroll;

        internal static void Open(List<string> files, string reason)
        {
            BrokenReferenceWindow window = GetWindow<BrokenReferenceWindow>(true, GuidReferenceRepair.Title);
            window.minSize = new Vector2(520f, 360f);
            window.files = files;
            window.reason = reason;
            window.Show();
            window.Rescan();
        }

        /// <summary>Checks the open scenes and the prefab open in Prefab Mode.</summary>
        internal static void OpenForOpenScenes(string reason)
        {
            List<string> paths = OpenPaths();
            if (paths.Count == 0)
            {
                EditorUtility.DisplayDialog(GuidReferenceRepair.Title, "Open a saved scene or prefab first.", "OK");
                return;
            }
            Open(paths, reason);
        }

        internal static List<string> OpenPaths()
        {
            var paths = new List<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (!string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path)) paths.Add(SceneManager.GetSceneAt(i).path);
            }
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && !string.IsNullOrEmpty(stage.assetPath)) paths.Add(stage.assetPath);
            return paths;
        }

        private void Rescan()
        {
            GuidToolLog log = GuidToolLog.Open("Check for Broken References");
            try
            {
                plan = GuidReferenceRepair.Scan(files, log);
                GuidReferenceRepair.Suggest(plan, log);
                choices.Clear();
                foreach (KeyValuePair<string, string> entry in plan.Suggested)
                    choices[entry.Key] = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(entry.Value));
            }
            catch (OperationCanceledException)
            {
                plan = null;
            }
            catch (Exception exception)
            {
                plan = null;
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(GuidReferenceRepair.Title, exception.Message, "OK");
            }
            finally
            {
                log.Dispose();
            }
        }

        private Dictionary<string, string> Redirects()
        {
            var redirects = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, Object> entry in choices)
            {
                string guid;
                long localId;
                if (entry.Value != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(entry.Value, out guid, out localId) &&
                    guid != entry.Key && plan.Missing.ContainsKey(entry.Key))
                    redirects[entry.Key] = guid;
            }
            return redirects;
        }

        private void ApplyRedirects()
        {
            if (EditorSettings.serializationMode != SerializationMode.ForceText)
            {
                EditorUtility.DisplayDialog("Force Text Required",
                    "Set Edit > Project Settings > Editor > Asset Serialization to Force Text first.", "OK");
                return;
            }
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                EditorUtility.DisplayDialog(GuidReferenceRepair.Title,
                    "Close Prefab Mode without saving first, then apply the redirects.", "OK");
                return;
            }
            // Saving a scene with broken references could overwrite the good file, so
            // open scenes are reloaded from disk instead of saved.
            if (!EditorUtility.DisplayDialog(GuidReferenceRepair.Title,
                    "Rewrite " + Redirects().Count + " missing GUID(s) on disk, then reload the open scenes from disk?" +
                    (RegenerateGuid.ScenesAreSaved() ? "" : "\n\nUnsaved scene changes are discarded.") +
                    "\n\nThis bypasses Unity Undo.", "Apply", "Cancel"))
                return;

            GuidToolLog log = GuidToolLog.Open("Apply Broken Reference Redirects");
            try
            {
                int changed = GuidReferenceRepair.Apply(plan, Redirects(), log);
                Debug.Log(GuidReferenceRepair.Title + ": redirected references in " + changed + " file(s).");
            }
            catch (Exception exception)
            {
                log.Write("FAILURE: " + exception);
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(GuidReferenceRepair.Title + " Failed", exception.Message, "OK");
            }
            finally
            {
                log.Dispose();
            }
            Rescan();
        }
    }

    /// <summary>
    /// Offers Check for Broken References when an opened scene or prefab has GUIDs
    /// with no asset, or when Unity starts reporting missing scripts or prefabs.
    /// </summary>
    [InitializeOnLoad]
    internal static class BrokenReferenceMonitor
    {
        private const int Threshold = 3;
        private const double WindowSeconds = 30.0;
        private static readonly Regex Pattern = new Regex(
            @"referenced script .*is missing|Missing Prefab|Broken text PPtr|Could not extract GUID|missing Prefab Asset|Prefab instance problem",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Queue<double> recent = new Queue<double>();
        private static readonly HashSet<string> prompted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static BrokenReferenceMonitor()
        {
            Application.logMessageReceived += OnLog;
            EditorSceneManager.sceneOpened += (scene, mode) => Inspect(scene.path);
            PrefabStage.prefabStageOpened += stage => Inspect(stage.assetPath);
        }

        private static void OnLog(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Log || !Pattern.IsMatch(message)) return;
            recent.Enqueue(EditorApplication.timeSinceStartup);
            while (recent.Count > 0 && EditorApplication.timeSinceStartup - recent.Peek() > WindowSeconds) recent.Dequeue();
            if (recent.Count < Threshold) return;
            recent.Clear();
            Prompt(string.Join(", ", BrokenReferenceWindow.OpenPaths()),
                "Unity is reporting missing scripts or broken references.");
        }

        private static void Inspect(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            EditorApplication.delayCall += () =>
            {
                string fullPath = System.IO.Path.Combine(RiryToolsPaths.ProjectRoot, path);
                if (ScriptLoadGuard.IsGuarded(path) || !System.IO.File.Exists(fullPath) ||
                    !RegenerateGuid.LooksLikeText(fullPath, new byte[4096]))
                    return;
                foreach (KeyValuePair<long, string> reference in
                         GuidReferenceRepair.FindGuidReferences(System.IO.File.ReadAllBytes(fullPath)))
                {
                    if (!GuidReferenceRepair.IsMissing(reference.Value)) continue;
                    Prompt(path, path + " references assets that no longer exist.");
                    return;
                }
            };
        }

        private static void Prompt(string key, string message)
        {
            // Once per scene set per session; Not Now must not nag on every log line.
            if (key.Length == 0 || !prompted.Add(key)) return;
            EditorApplication.delayCall += () =>
            {
                if (EditorUtility.DisplayDialog(GuidReferenceRepair.Title, message + "\n\nCheck them and choose redirects now?",
                        "Check for Broken References", "Not Now"))
                    BrokenReferenceWindow.OpenForOpenScenes(message);
            };
        }
    }
}
