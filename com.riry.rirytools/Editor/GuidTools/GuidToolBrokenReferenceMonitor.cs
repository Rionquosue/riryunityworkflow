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
        /// updates after. BrokenReferenceSaveGuard skips its check then, so a save made while
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
                    !GuidToolRegenerate.LooksLikeText(fullPath, new byte[4096]))
                    return;
                foreach (KeyValuePair<long, string> reference in
                         GuidToolReferenceRepair.FindGuidReferences(System.IO.File.ReadAllBytes(fullPath)))
                {
                    if (!GuidToolReferenceRepair.IsMissing(reference.Value)) continue;
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
            if (choice == 0) BrokenReferenceWindow.OpenForOpenScenes(path + " references assets that no longer exist.");
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
                    BrokenReferenceWindow.OpenForOpenScenes(message);
            };
        }
    }

    /// <summary>
    /// Interrupts saving a scene, or the prefab open in Prefab Mode, that holds broken
    /// asset references in memory (references whose asset GUID no longer exists): the
    /// save goes ahead only after CONTINUE ANYWAY is clicked three times
    /// (BrokenSaveWindow). Missing scripts are not counted. Only the open scene or prefab
    /// being saved is checked, and nothing is checked while a scene is loading
    /// (GuidToolBrokenReferenceMonitor.Loading).
    /// </summary>
    internal sealed class BrokenReferenceSaveGuard : AssetModificationProcessor
    {
        private static string[] OnWillSaveAssets(string[] paths)
        {
            if (GuidToolBrokenReferenceMonitor.Loading) return paths;
            var allowed = new List<string>();
            foreach (string path in paths)
            {
                List<BrokenReference> broken = FindBroken(path);
                if (broken.Count > 0 && !BrokenSaveWindow.Confirm(path, Describe(broken)))
                {
                    RiryLog.Warn(RiryLog.GuidTools, GuidToolReferenceRepair.Title + ": saving " + path + " was aborted; it has " + broken.Count +
                        " broken asset reference(s). Repair them with Tools > RiryTools > GUID Tools.", AssetDatabase.LoadMainAssetAtPath(path));
                    continue;
                }
                allowed.Add(path);
            }
            return allowed.ToArray();
        }

        /// <summary>One broken reference: where it is used, and the missing asset's GUID (null when Unity does not know it).</summary>
        internal sealed class BrokenReference
        {
            internal string Where;
            internal string Guid;
        }

        /// <summary>Broken asset references in the loaded scene or Prefab Mode prefab at the path; none when it is not open.</summary>
        internal static List<BrokenReference> FindBroken(string path)
        {
            var roots = new List<GameObject>();
            if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                if (scene.IsValid() && scene.isLoaded) roots.AddRange(scene.GetRootGameObjects());
            }
            else if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage != null && stage.assetPath == path) roots.Add(stage.prefabContentsRoot);
            }
            var broken = new List<BrokenReference>();
            bool? fileNamesMissing = null;
            foreach (GameObject root in roots)
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    string objectPath = HierarchyPath.Of(transform);
                    if (PrefabUtility.IsPrefabAssetMissing(transform.gameObject))
                        broken.Add(new BrokenReference { Where = objectPath + " > Prefab", Guid = MissingPrefabGuid(transform.gameObject) });
                    foreach (Component component in transform.GetComponents<Component>())
                    {
                        // A missing script shows as a null component; scripts are not in scope yet.
                        if (component == null) continue;
                        SerializedProperty property = new SerializedObject(component).GetIterator();
                        while (property.Next(true))
                        {
                            // A reference to an asset that is gone keeps its instance ID but has no object.
                            if (property.propertyType != SerializedPropertyType.ObjectReference || property.name == "m_Script" ||
                                property.objectReferenceValue != null || property.objectReferenceInstanceIDValue == 0)
                                continue;
                            string guid;
                            if (!PointsAtMissingAsset(property.objectReferenceInstanceIDValue, path, ref fileNamesMissing, out guid)) continue;
                            broken.Add(new BrokenReference
                            {
                                Where = objectPath + " > " + ObjectNames.NicifyVariableName(component.GetType().Name) + " > " +
                                    FieldName(property.propertyPath),
                                Guid = guid
                            });
                        }
                    }
                }
            }
            return broken;
        }

        /// <summary>
        /// Whether an empty reference is a broken GUID: its asset GUID no longer exists. An
        /// empty reference whose GUID still exists, such as one left over from a scene opened
        /// earlier, is not broken. One with no GUID to look up counts only when the file on
        /// disk itself names a missing asset.
        /// </summary>
        private static bool PointsAtMissingAsset(int instanceId, string path, ref bool? fileNamesMissing, out string guid)
        {
            long localId;
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(instanceId, out guid, out localId) && !string.IsNullOrEmpty(guid))
                return GuidToolReferenceRepair.IsMissing(guid);
            guid = null;
            if (fileNamesMissing == null) fileNamesMissing = FileNamesMissingAsset(path);
            return fileNamesMissing.Value;
        }

        // The prefab asset a prefab instance came from, read from its instance's source reference.
        private static string MissingPrefabGuid(GameObject instance)
        {
            Object handle = PrefabUtility.GetPrefabInstanceHandle(instance);
            if (handle == null) return null;
            SerializedProperty source = new SerializedObject(handle).FindProperty("m_SourcePrefab");
            string guid;
            long localId;
            return source != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source.objectReferenceInstanceIDValue, out guid, out localId) &&
                !string.IsNullOrEmpty(guid) ? guid : null;
        }

        // "m_Materials.Array.data[0]" as "Materials[0]".
        private static string FieldName(string propertyPath)
        {
            var parts = new List<string>();
            foreach (string part in propertyPath.Replace(".Array.data[", "[").Split('.')) parts.Add(ObjectNames.NicifyVariableName(part));
            return string.Join(".", parts);
        }

        /// <summary>
        /// "Skin.mat in Avatar/Body > Mesh Renderer > Materials[0] (GUID:...)" for each reference: the
        /// missing asset's last known name from the GUID remap ledger or Git history, or "Unknown asset".
        /// </summary>
        private static List<string> Describe(List<BrokenReference> broken)
        {
            var guids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (BrokenReference reference in broken)
            {
                if (reference.Guid != null) guids.Add(reference.Guid);
            }
            Dictionary<string, string> names = GuidToolReferenceRepair.LastKnownNames(guids);
            var lines = new List<string>();
            foreach (BrokenReference reference in broken)
            {
                string name;
                if (reference.Guid == null || !names.TryGetValue(reference.Guid, out name)) name = "Unknown asset";
                lines.Add(name + " in " + reference.Where + " (GUID:" + (reference.Guid ?? "unknown") + ")");
            }
            return lines;
        }

        private static bool FileNamesMissingAsset(string path)
        {
            string fullPath = System.IO.Path.Combine(RiryToolsPaths.ProjectRoot, path);
            if (!System.IO.File.Exists(fullPath) || !GuidToolRegenerate.LooksLikeText(fullPath, new byte[4096])) return false;
            foreach (KeyValuePair<long, string> reference in GuidToolReferenceRepair.FindGuidReferences(System.IO.File.ReadAllBytes(fullPath)))
            {
                if (GuidToolReferenceRepair.IsMissing(reference.Value)) return true;
            }
            return false;
        }
    }
}
