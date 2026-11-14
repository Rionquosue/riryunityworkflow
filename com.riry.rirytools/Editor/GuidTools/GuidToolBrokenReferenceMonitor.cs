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
    /// Offers Check for Broken References when an opened scene or prefab has asset
    /// GUIDs with no asset, or when Unity starts reporting missing prefabs or broken
    /// references. Missing scripts are not included yet.
    /// </summary>
    [InitializeOnLoad]
    internal static class GuidToolBrokenReferenceMonitor
    {
        private const int Threshold = 3;
        // Scenes and prefabs, one per line, whose popup was turned off with Do Not Show Again.
        private const string OffKey = "RiryTools.GuidTools.BrokenPromptOff";
        private const double WindowSeconds = 30.0;
        // Missing scripts are left out: GUID Tools only repairs asset references for now.
        private static readonly Regex Pattern = new Regex(
            @"Missing Prefab|Broken text PPtr|Could not extract GUID|missing Prefab Asset|Prefab instance problem",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Queue<double> recent = new Queue<double>();
        private static readonly HashSet<string> prompted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static bool loading;
        private static int loadNumber;

        static GuidToolBrokenReferenceMonitor()
        {
            Application.logMessageReceived += OnLog;
            EditorSceneManager.sceneOpening += (path, mode) => StartLoading();
            EditorSceneManager.sceneOpened += (scene, mode) => Inspect(scene.path);
            PrefabStage.prefabStageOpened += stage => Inspect(stage.assetPath);
        }

        /// <summary>
        /// True while a scene is loading: from the moment it starts opening until two editor
        /// updates after. GuidToolBrokenReferenceSaveGuard skips its check then, so a save made while
        /// a scene opens does not show the save warning.
        /// </summary>
        internal static bool Loading
        {
            get { return loading; }
        }

        private static void StartLoading()
        {
            loading = true;
            int number = ++loadNumber;
            // Opening is synchronous; the first delay call runs after it, the second one update later.
            EditorApplication.delayCall += () => EditorApplication.delayCall += () =>
            {
                if (number == loadNumber) loading = false;
            };
        }

        private static void OnLog(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Log || !Pattern.IsMatch(message)) return;
            recent.Enqueue(EditorApplication.timeSinceStartup);
            while (recent.Count > 0 && EditorApplication.timeSinceStartup - recent.Peek() > WindowSeconds) recent.Dequeue();
            if (recent.Count < Threshold) return;
            recent.Clear();
            Prompt(string.Join(", ", GuidToolBrokenReferenceWindow.OpenPaths()),
                "Unity is reporting missing scripts or broken references.");
        }

        private static void Inspect(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            EditorApplication.delayCall += () =>
            {
                string fullPath = System.IO.Path.Combine(RiryToolsPaths.ProjectRoot, path);
                if (ScriptLoadGuard.IsGuarded(path) || !System.IO.File.Exists(fullPath) ||
                    !GuidToolRegenerate.LooksLikeText(fullPath, new byte[4096]))
                    return;
                foreach (KeyValuePair<long, string> reference in
                         GuidToolReferenceRepair.FindGuidReferences(System.IO.File.ReadAllBytes(fullPath)))
                {
                    if (!GuidToolReferenceRepair.IsMissing(reference.Value) || GuidToolIgnoredReferences.Contains(reference.Value)) continue;
                    PromptOnOpen(path);
                    return;
                }
            };
        }

        /// <summary>Every time the scene or prefab opens, until Do Not Show Again is chosen for it.</summary>
        private static void PromptOnOpen(string path)
        {
            List<string> off = TurnedOff();
            if (off.Contains(path)) return;
            int choice = EditorUtility.DisplayDialogComplex(GuidToolReferenceRepair.Title,
                path + " references assets that no longer exist.\n\nCheck them and choose redirects now?",
                "Check for Broken References", "Not Now", "Do Not Show Again");
            if (choice == 0) GuidToolBrokenReferenceWindow.OpenForOpenScenes(path + " references assets that no longer exist.");
            if (choice != 2) return;
            off.Add(path);
            EditorUserSettings.SetConfigValue(OffKey, string.Join("\n", off));
        }

        private static List<string> TurnedOff()
        {
            return new List<string>((EditorUserSettings.GetConfigValue(OffKey) ?? "").Split(new[] { '\n' },
                StringSplitOptions.RemoveEmptyEntries));
        }

        private static void Prompt(string key, string message)
        {
            // Once per scene set per session; Not Now must not nag on every log line.
            if (key.Length == 0 || !prompted.Add(key)) return;
            EditorApplication.delayCall += () =>
            {
                if (EditorUtility.DisplayDialog(GuidToolReferenceRepair.Title, message + "\n\nCheck them and choose redirects now?",
                        "Check for Broken References", "Not Now"))
                    GuidToolBrokenReferenceWindow.OpenForOpenScenes(message);
            };
        }
    }
}

// ============================================================================
// GLOSSARY: Broken reference monitor
// ============================================================================
//
// GuidToolBrokenReferenceMonitor.Loading: true from EditorSceneManager.sceneOpening until two editor updates after the
//   scene opens (StartLoading; loadNumber keeps an earlier load from ending a later one).
// GuidToolBrokenReferenceMonitor (this file): offers the window every time a file opens with
//   missing GUIDs (PromptOnOpen; Do Not Show Again turns it off for that file, OffKey), and once per session when
//   Unity logs several missing-prefab or broken-reference messages.
