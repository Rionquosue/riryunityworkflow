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
                RiryLog.Exception(RiryLog.GuidTools, GuidReferenceRepair.Title + ": the scan failed", exception);
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
                RiryLog.Info(RiryLog.GuidTools, GuidReferenceRepair.Title + ": repaired references in " + changed + " file(s)" +
                    (restoredFiles.Count > 0 ? ", restoring " + string.Join(", ", restoredFiles) : "") + ".");
                applied = true;
            }
            catch (Exception exception)
            {
                log.Write("FAILURE: " + exception);
                RiryLog.Exception(RiryLog.GuidTools, GuidReferenceRepair.Title + ": the repair failed", exception);
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
