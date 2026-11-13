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
    /// and actions; the drawing is in Editor/UI/GuidToolBrokenReferenceWindow.UI.cs.
    /// </summary>
    internal sealed partial class GuidToolBrokenReferenceWindow : EditorWindow
    {
        private List<string> files = new List<string>();
        private string reason = "";
        private GuidToolRepairPlan plan;
        // Missing GUID to the chosen candidate index; Other (the list's length) uses others.
        private readonly Dictionary<string, int> choices = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, Object> others = new Dictionary<string, Object>(StringComparer.Ordinal);
        // Restores whose Break detected button is open.
        private readonly HashSet<GuidToolRepairCandidate> expanded = new HashSet<GuidToolRepairCandidate>();
        // Candidates already searched, kept between the passes after each Apply.
        private GuidToolRepairCache cache = new GuidToolRepairCache();
        private Vector2 scroll;

        internal static void Open(List<string> files, string reason)
        {
            GuidToolBrokenReferenceWindow window = GetWindow<GuidToolBrokenReferenceWindow>(true, GuidToolReferenceRepair.Title);
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
                EditorUtility.DisplayDialog(GuidToolReferenceRepair.Title, "Open a saved scene or prefab first.", "OK");
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
            if (!nextPass) cache = new GuidToolRepairCache();
            GuidToolLog log = GuidToolLog.Open("Check for Broken References");
            try
            {
                plan = GuidToolReferenceRepair.Scan(files, log);
                GuidToolReferenceRepair.Suggest(plan, null, cache, log);
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
                RiryLog.Exception(RiryLog.GuidTools, GuidToolReferenceRepair.Title + ": the scan failed", exception);
                EditorUtility.DisplayDialog(GuidToolReferenceRepair.Title, exception.Message, "OK");
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
            foreach (KeyValuePair<string, List<GuidToolRepairCandidate>> entry in plan.Candidates)
            {
                if (entry.Value.Count == 1 && !choices.ContainsKey(entry.Key)) choices[entry.Key] = 0;
            }
        }

        private GuidToolRepairCandidate Chosen(string guid)
        {
            int index;
            List<GuidToolRepairCandidate> candidates;
            if (!choices.TryGetValue(guid, out index) || !plan.Candidates.TryGetValue(guid, out candidates) ||
                index < 0 || index >= candidates.Count)
                return null;
            return candidates[index];
        }

        private bool OtherChosen(string guid)
        {
            int index;
            List<GuidToolRepairCandidate> candidates;
            return choices.TryGetValue(guid, out index) && plan.Candidates.TryGetValue(guid, out candidates) &&
                index == candidates.Count;
        }

        /// <summary>Missing GUID to the asset GUID it is redirected to.</summary>
        private Dictionary<string, string> Redirects()
        {
            var redirects = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string missing in plan.Missing.Keys)
            {
                GuidToolRepairCandidate chosen = Chosen(missing);
                string guid = chosen != null && !chosen.IsRestore ? chosen.TargetGuid : null;
                Object other;
                long localId;
                if (OtherChosen(missing) && others.TryGetValue(missing, out other) && other != null)
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(other, out guid, out localId);
                if (!string.IsNullOrEmpty(guid) && guid != missing) redirects[missing] = guid;
            }
            return redirects;
        }

        private List<GuidToolRepairCandidate> Restores()
        {
            var restores = new List<GuidToolRepairCandidate>();
            foreach (string missing in plan.Missing.Keys)
            {
                GuidToolRepairCandidate chosen = Chosen(missing);
                if (chosen != null && chosen.IsRestore) restores.Add(chosen);
            }
            return restores;
        }

        /// <summary>Fix for a restored file's missing references: adds them to the list, with their own candidates.</summary>
        private void FixMissingReferences(string owner, GuidToolRepairCandidate restore)
        {
            GuidToolLog log = GuidToolLog.Open("Check for Broken References");
            try
            {
                // Past the third level, the references wait for the next pass, which lists them by itself.
                if (plan.DepthOf(owner) < GuidToolRepairPlan.MaxFixDepth)
                    GuidToolReferenceRepair.AddDependencies(plan, owner, restore, cache, log);
                restore.MissingDecision = GuidToolBreakDecision.Fix;
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
                EditorUtility.DisplayDialog(GuidToolReferenceRepair.Title,
                    "Close Prefab Mode without saving first, then apply the redirects.", "OK");
                return;
            }
            List<GuidToolRepairCandidate> restores = Restores();
            // A break without Fix (accepted, or not decided) stays broken after Apply.
            if (restores.Exists(restore => restore.Unfixed) &&
                !EditorUtility.DisplayDialog(GuidToolReferenceRepair.Title,
                    "This repair will cause further broken references. Please run tool again afterwards.",
                    "ACCEPT BROKEN REFERENCES", "ABORT"))
                return;
            // Saving a scene with broken references could overwrite the good file, so
            // open scenes are reloaded from disk instead of saved.
            if (!EditorUtility.DisplayDialog(GuidToolReferenceRepair.Title,
                    (restores.Count > 0 ? "Restore " + restores.Count + " asset(s) from Git, then r" : "R") +
                    "ewrite " + Redirects().Count + " missing GUID(s) on disk, then reload the open scenes from disk?" +
                    (GuidToolRegenerate.ScenesAreSaved() ? "" : "\n\nUnsaved scene changes are discarded.") +
                    "\n\nThis bypasses Unity Undo.", "Apply", "Cancel"))
                return;

            GuidToolLog log = GuidToolLog.Open("Apply Broken Reference Repairs");
            bool applied = false;
            var restoredFiles = new List<string>();
            try
            {
                int changed = GuidToolReferenceRepair.Apply(plan, Redirects(), restores, restoredFiles, log);
                RiryLog.Info(RiryLog.GuidTools, GuidToolReferenceRepair.Title + ": repaired references in " + changed + " file(s)" +
                    (restoredFiles.Count > 0 ? ", restoring " + string.Join(", ", restoredFiles) : "") + ".");
                applied = true;
            }
            catch (Exception exception)
            {
                log.Write("FAILURE: " + exception);
                RiryLog.Exception(RiryLog.GuidTools, GuidToolReferenceRepair.Title + ": the repair failed", exception);
                EditorUtility.DisplayDialog(GuidToolReferenceRepair.Title + " Failed", exception.Message, "OK");
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
                if (GuidToolRegenerate.UnitySerializedExtensions.Contains(System.IO.Path.GetExtension(restored)) && !files.Contains(restored))
                    files.Add(restored);
            }
            reason = "Next pass: what is still broken after the last Apply.";
            Rescan(true);
        }
    }
}

// ============================================================================
// GLOSSARY: Broken References window, fields and actions
// Drawing is in GuidToolBrokenReferenceWindow.UI.cs; the scan and repair are in Editor/GuidTools/GuidToolReferenceRepair.cs. Git history is searched when the project is in a repository.
// ============================================================================
//
// GuidToolBrokenReferenceWindow (this part)
//   files                 List<string>  Files scanned.
//   reason                string        Why the window opened.
//   plan                  GuidToolRepairPlan    Missing GUIDs, their files and candidates.
//   choices               Dictionary<string, int>  Chosen candidate per missing GUID (its list length = Other).
//   expanded              HashSet<GuidToolRepairCandidate>  Restores whose break options are open.
//   others                Dictionary<string, Object>  The asset picked under Other.
//   scroll                Vector2       Scroll position.
//   Open(files, reason)   Opens the window and scans.
//   OpenForOpenScenes(reason)  Scans the open scenes and the prefab in Prefab Mode.
//   OpenPaths()           Paths of the open scenes and that prefab.
//   Rescan(nextPass)      Scans, collects candidates (reusing the cache on a next pass), pre-selects single ones.
//   cache                 GuidToolRepairCache   Candidates already searched, kept between passes.
//   PreselectSingles()    Chooses the candidate of each missing GUID that has exactly one.
//   Chosen(guid) / OtherChosen(guid)  The chosen candidate, or whether Other is chosen.
//   Redirects()           Missing GUID to chosen asset GUID.
//   Restores()            Chosen Git restores.
//   FixMissingReferences(owner, restore)  Adds a restored file's missing references to the list, one level deeper.
//   ApplyRedirects()      Warns when a restore's break has no Fix (ABORT / ACCEPT BROKEN REFERENCES),
//                         confirms, restores, rewrites, reloads, then starts the next pass with the
//                         restored files added, until nothing is broken.
//
// UpdateExitPrompt() / SaveChanges(): closing with broken references
//   listed asks ExitMessage; Save keeps the window open like Cancel.
