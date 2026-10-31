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
    /// Lists every missing GUID in a set of files with every replacement found for
    /// it and how each was found; the user picks one (a single one is pre-selected)
    /// or any other asset. A missing GUID with nothing chosen is shown in red. State
    /// and actions; the drawing is in Editor/UI/BrokenReferenceWindow.UI.cs.
    /// </summary>
    internal sealed partial class BrokenReferenceWindow : EditorWindow
    {
        private List<string> files = new List<string>();
        private string reason = "";
        private RepairPlan plan;
        // Missing GUID to the chosen candidate index; Other (the list's length) uses others.
        private readonly Dictionary<string, int> choices = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, Object> others = new Dictionary<string, Object>(StringComparer.Ordinal);
        // Restores whose Break detected button is open.
        private readonly HashSet<RepairCandidate> expanded = new HashSet<RepairCandidate>();
        // Candidates already searched, kept between the passes after each Apply.
        private RepairCache cache = new RepairCache();
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

        /// <summary>Scans and collects candidates; the next pass after Apply keeps the cache, a new scan does not.</summary>
        private void Rescan(bool nextPass = false)
        {
            if (!nextPass) cache = new RepairCache();
            GuidToolLog log = GuidToolLog.Open("Check for Broken References");
            try
            {
                plan = GuidReferenceRepair.Scan(files, log);
                GuidReferenceRepair.Suggest(plan, null, cache, log);
                choices.Clear();
                others.Clear();
                expanded.Clear();
                PreselectSingles();
                UpdateExitPrompt();
            }
            catch (OperationCanceledException)
            {
                plan = null;
                UpdateExitPrompt();
            }
            catch (Exception exception)
            {
                plan = null;
                UpdateExitPrompt();
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(GuidReferenceRepair.Title, exception.Message, "OK");
            }
            finally
            {
                log.Dispose();
            }
        }

        private const string ExitMessage = "Exit without repairing all broken GUID references?";

        // Closing the window while references are still broken asks first (Unity's close prompt).
        private void UpdateExitPrompt()
        {
            saveChangesMessage = ExitMessage;
            hasUnsavedChanges = plan != null && plan.Missing.Count > 0;
        }

        // Save in Unity's close prompt keeps the window open, like Cancel; Discard closes it.
        public override void SaveChanges()
        {
        }

        // A missing GUID with exactly one candidate starts with it chosen.
        private void PreselectSingles()
        {
            foreach (KeyValuePair<string, List<RepairCandidate>> entry in plan.Candidates)
            {
                if (entry.Value.Count == 1 && !choices.ContainsKey(entry.Key)) choices[entry.Key] = 0;
            }
        }

        private RepairCandidate Chosen(string guid)
        {
            int index;
            List<RepairCandidate> candidates;
            if (!choices.TryGetValue(guid, out index) || !plan.Candidates.TryGetValue(guid, out candidates) ||
                index < 0 || index >= candidates.Count)
                return null;
            return candidates[index];
        }

        private bool OtherChosen(string guid)
        {
            int index;
            List<RepairCandidate> candidates;
            return choices.TryGetValue(guid, out index) && plan.Candidates.TryGetValue(guid, out candidates) &&
                index == candidates.Count;
        }

        /// <summary>Missing GUID to the asset GUID it is redirected to.</summary>
        private Dictionary<string, string> Redirects()
        {
            var redirects = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string missing in plan.Missing.Keys)
            {
                RepairCandidate chosen = Chosen(missing);
                string guid = chosen != null && !chosen.IsRestore ? chosen.TargetGuid : null;
                Object other;
                long localId;
                if (OtherChosen(missing) && others.TryGetValue(missing, out other) && other != null)
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(other, out guid, out localId);
                if (!string.IsNullOrEmpty(guid) && guid != missing) redirects[missing] = guid;
            }
            return redirects;
        }

        private List<RepairCandidate> Restores()
        {
            var restores = new List<RepairCandidate>();
            foreach (string missing in plan.Missing.Keys)
            {
                RepairCandidate chosen = Chosen(missing);
                if (chosen != null && chosen.IsRestore) restores.Add(chosen);
            }
            return restores;
        }

        /// <summary>Fix for a restored file's missing references: adds them to the list, with their own candidates.</summary>
        private void FixMissingReferences(string owner, RepairCandidate restore)
        {
            GuidToolLog log = GuidToolLog.Open("Check for Broken References");
            try
            {
                // Past the third level, the references wait for the next pass, which lists them by itself.
                if (plan.DepthOf(owner) < RepairPlan.MaxFixDepth)
                    GuidReferenceRepair.AddDependencies(plan, owner, restore, cache, log);
                restore.MissingDecision = BreakDecision.Fix;
                PreselectSingles();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                log.Dispose();
            }
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
            List<RepairCandidate> restores = Restores();
            // A break without Fix (accepted, or not decided) stays broken after Apply.
            if (restores.Exists(restore => restore.Unfixed) &&
                !EditorUtility.DisplayDialog(GuidReferenceRepair.Title,
                    "This repair will cause further broken references. Please run tool again afterwards.",
                    "ACCEPT BROKEN REFERENCES", "ABORT"))
                return;
            // Saving a scene with broken references could overwrite the good file, so
            // open scenes are reloaded from disk instead of saved.
            if (!EditorUtility.DisplayDialog(GuidReferenceRepair.Title,
                    (restores.Count > 0 ? "Restore " + restores.Count + " asset(s) from Git, then r" : "R") +
                    "ewrite " + Redirects().Count + " missing GUID(s) on disk, then reload the open scenes from disk?" +
                    (RegenerateGuid.ScenesAreSaved() ? "" : "\n\nUnsaved scene changes are discarded.") +
                    "\n\nThis bypasses Unity Undo.", "Apply", "Cancel"))
                return;

            GuidToolLog log = GuidToolLog.Open("Apply Broken Reference Repairs");
            bool applied = false;
            var restoredFiles = new List<string>();
            try
            {
                int changed = GuidReferenceRepair.Apply(plan, Redirects(), restores, restoredFiles, log);
                Debug.Log(GuidReferenceRepair.Title + ": repaired references in " + changed + " file(s).");
                applied = true;
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
            if (!applied)
            {
                Rescan();
                return;
            }
            // The next pass: the same files plus the restored ones, with what was already found.
            foreach (string restored in restoredFiles)
            {
                if (RegenerateGuid.UnitySerializedExtensions.Contains(System.IO.Path.GetExtension(restored)) && !files.Contains(restored))
                    files.Add(restored);
            }
            reason = "Next pass: what is still broken after the last Apply.";
            Rescan(true);
        }
    }

    /// <summary>
    /// Offers Check for Broken References when an opened scene or prefab has asset
    /// GUIDs with no asset, or when Unity starts reporting missing prefabs or broken
    /// references. Missing scripts are not included yet.
    /// </summary>
    [InitializeOnLoad]
    internal static class BrokenReferenceMonitor
    {
        private const int Threshold = 3;
        private const double WindowSeconds = 30.0;
        // Missing scripts are left out: GUID Tools only repairs asset references for now.
        private static readonly Regex Pattern = new Regex(
            @"Missing Prefab|Broken text PPtr|Could not extract GUID|missing Prefab Asset|Prefab instance problem",
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

    /// <summary>
    /// Interrupts saving a scene, or the prefab open in Prefab Mode, that holds broken
    /// asset references in memory (references whose asset GUID no longer exists): the
    /// save goes ahead only after CONTINUE ANYWAY is clicked three times
    /// (BrokenSaveWindow). Missing scripts are not counted.
    /// </summary>
    internal sealed class BrokenReferenceSaveGuard : AssetModificationProcessor
    {
        private static string[] OnWillSaveAssets(string[] paths)
        {
            var allowed = new List<string>();
            foreach (string path in paths)
            {
                List<BrokenReference> broken = FindBroken(path);
                if (broken.Count > 0 && !BrokenSaveWindow.Confirm(path, Describe(broken)))
                {
                    Debug.LogWarning(GuidReferenceRepair.Title + ": saving " + path + " was aborted; it has " + broken.Count +
                        " broken asset reference(s). Repair them with Tools > RiryTools > GUID Tools.");
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
                    string objectPath = ObjectPath(transform);
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
                return GuidReferenceRepair.IsMissing(guid);
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

        // "Avatar/Body", the GameObject's place in the hierarchy.
        private static string ObjectPath(Transform transform)
        {
            string path = transform.name;
            for (Transform parent = transform.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
            return path;
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
            Dictionary<string, string> names = GuidReferenceRepair.LastKnownNames(guids);
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
            if (!System.IO.File.Exists(fullPath) || !RegenerateGuid.LooksLikeText(fullPath, new byte[4096])) return false;
            foreach (KeyValuePair<long, string> reference in GuidReferenceRepair.FindGuidReferences(System.IO.File.ReadAllBytes(fullPath)))
            {
                if (GuidReferenceRepair.IsMissing(reference.Value)) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// The modal warning before saving with broken references. CONTINUE ANYWAY counts
    /// down [2], [1] and saves on the third click; ABORT or closing skips the save.
    /// </summary>
    internal sealed class BrokenSaveWindow : EditorWindow
    {
        private const string Warning =
            "SAVING WITH BROKEN GUID'S CAN CORRUPT A SCENE. Click continue anyways three times to save regardless.";
        private const int ClicksNeeded = 3;
        private string path;
        private List<string> broken;
        private Vector2 scroll;
        private int clicksLeft = ClicksNeeded;
        private bool confirmed;
        private GUIStyle warningStyle;

        internal static bool Confirm(string path, List<string> broken)
        {
            BrokenSaveWindow window = CreateInstance<BrokenSaveWindow>();
            window.titleContent = new GUIContent(GuidReferenceRepair.Title);
            window.path = path;
            window.broken = broken;
            Rect main = EditorGUIUtility.GetMainWindowPosition();
            window.position = new Rect(main.center.x - 260f, main.center.y - 180f, 520f, 360f);
            window.minSize = new Vector2(520f, 360f);
            window.ShowModalUtility();
            return window.confirmed;
        }

        private void OnGUI()
        {
            if (warningStyle == null)
            {
                warningStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 18, wordWrap = true };
                warningStyle.normal.textColor = new Color(1f, 0.2f, 0.2f);
            }
            GUILayout.Space(8f);
            GUILayout.Label(Warning, warningStyle);
            GUILayout.Space(6f);
            EditorGUILayout.LabelField(path + ": " + broken.Count + " broken asset reference(s).", EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(4f);
            EditorGUILayout.LabelField("Broken references:", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (string line in broken) EditorGUILayout.SelectableLabel(line, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.EndScrollView();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("ABORT", GUILayout.Height(30f))) Close();
                string label = "CONTINUE ANYWAY" + (clicksLeft < ClicksNeeded ? " [" + clicksLeft + "]" : "");
                if (GUILayout.Button(label, GUILayout.Height(30f)) && --clicksLeft == 0)
                {
                    confirmed = true;
                    Close();
                }
            }
            GUILayout.Space(8f);
        }
    }
}
