using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Riry.RiryTools.Editor
{
    /// <summary>What the Broken References window draws. State and actions are in Editor/GuidTools/BrokenReferenceWindow.cs.</summary>
    internal sealed partial class BrokenReferenceWindow
    {
        private static readonly Color Unresolved = new Color(1f, 0.35f, 0.35f);

        private void OnGUI()
        {
            EditorGUILayout.LabelField(reason, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Files: " + string.Join(", ", files), EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("Rescan")) Rescan();
            if (plan == null) return;
            if (plan.Missing.Count == 0)
            {
                EditorGUILayout.HelpBox("No broken references were found.", MessageType.Info);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (string missing in new List<string>(plan.Missing.Keys))
            {
                List<RepairCandidate> candidates;
                if (!plan.Candidates.TryGetValue(missing, out candidates)) candidates = new List<RepairCandidate>();
                bool resolved = Redirects().ContainsKey(missing) || Chosen(missing) != null && Chosen(missing).IsRestore;
                Color previous = GUI.backgroundColor;
                if (!resolved) GUI.backgroundColor = Unresolved;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    GUI.backgroundColor = previous;
                    string lastPath;
                    plan.LastKnownPaths.TryGetValue(missing, out lastPath);
                    EditorGUILayout.LabelField("Missing " + missing + (lastPath != null ? " (was " + lastPath + ")" : ""),
                        EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Used in " + string.Join(", ", plan.Missing[missing]), EditorStyles.wordWrappedMiniLabel);
                    if (candidates.Count == 0)
                        EditorGUILayout.LabelField("No match found. Pick the asset this reference should use.", EditorStyles.miniLabel);
                    for (int c = 0; c < candidates.Count; c++) DrawCandidate(missing, c, candidates[c]);
                    DrawOther(missing, candidates.Count);
                }
            }
            EditorGUILayout.EndScrollView();

            int ready = Redirects().Count + Restores().Count;
            using (new EditorGUI.DisabledScope(ready == 0))
            {
                if (GUILayout.Button("Apply " + ready + " Repair(s)", GUILayout.Height(28f))) ApplyRedirects();
            }
        }

        // One choice: what it is, every way it was found, and for a restore, what it would break.
        private void DrawCandidate(string missing, int index, RepairCandidate candidate)
        {
            int chosen;
            bool on = choices.TryGetValue(missing, out chosen) && chosen == index;
            string label = candidate.IsRestore
                ? "Restore " + candidate.Path + " from Git (" + candidate.Commit.Substring(0, Math.Min(8, candidate.Commit.Length)) + ")"
                : candidate.Path;
            if (GUILayout.Toggle(on, label, EditorStyles.radioButton) && !on)
            {
                // A chosen restore draws its breaks, so start a fresh layout pass.
                choices[missing] = index;
                GUIUtility.ExitGUI();
            }
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.LabelField(string.Join("; ", candidate.Sources), EditorStyles.wordWrappedMiniLabel);
                if (!candidate.IsRestore || !on) return;
                if (candidate.MissingReferences.Count > 0)
                    DrawBreak("It references " + candidate.MissingReferences.Count + " asset(s) that are missing now.",
                        "Repair them too", candidate.MissingDecision,
                        // Adds boxes to the list, so after this GUI pass.
                        () => EditorApplication.delayCall += () =>
                        {
                            FixMissingReferences(candidate);
                            Repaint();
                        },
                        decision => candidate.MissingDecision = decision);
                if (candidate.PathTaken)
                    DrawBreak(candidate.Path + " is taken by another file, which restoring would overwrite.",
                        "Restore as " + System.IO.Path.GetFileName(AssetDatabase.GenerateUniqueAssetPath(candidate.Path)),
                        candidate.PathDecision, () => candidate.PathDecision = BreakDecision.Fix,
                        decision => candidate.PathDecision = decision);
                if (candidate.GuidInUseBy != null)
                    DrawBreak(candidate.GuidInUseBy + " uses this GUID now; Unity would give one of them a new GUID.",
                        null, candidate.GuidDecision, null, decision => candidate.GuidDecision = decision);
            }
        }

        // A break with Fix (when one exists) and Accept break; the chosen one is pressed in.
        private static void DrawBreak(string problem, string fixLabel, BreakDecision decision, Action fix,
            Action<BreakDecision> decide)
        {
            EditorGUILayout.HelpBox(problem, decision == BreakDecision.Undecided ? MessageType.Warning : MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(EditorGUI.indentLevel * 15f);
                if (fix != null && GUILayout.Toggle(decision == BreakDecision.Fix, "Fix: " + fixLabel, EditorStyles.miniButtonLeft) &&
                    decision != BreakDecision.Fix)
                    fix();
                if (GUILayout.Toggle(decision == BreakDecision.Accept, "Accept break",
                        fix != null ? EditorStyles.miniButtonRight : EditorStyles.miniButton) && decision != BreakDecision.Accept)
                    decide(BreakDecision.Accept);
            }
        }

        private void DrawOther(string missing, int index)
        {
            int chosen;
            bool on = choices.TryGetValue(missing, out chosen) && chosen == index;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Toggle(on, "Other:", EditorStyles.radioButton, GUILayout.Width(70f)) && !on)
                {
                    choices[missing] = index;
                    GUIUtility.ExitGUI();
                }
                Object other;
                others.TryGetValue(missing, out other);
                Object picked = EditorGUILayout.ObjectField(other, typeof(Object), false);
                if (picked != other)
                {
                    others[missing] = picked;
                    if (picked != null) choices[missing] = index;
                }
            }
        }
    }
}

// ============================================================================
// GLOSSARY: Broken References window
// Opened from the GUID Tools window (Check for Broken References and Repair Missing GUID
// References), Project right-click > RiryTools > Repair Missing GUID References (RiryMenus.cs),
// and the BrokenReferenceMonitor prompt. Git history is searched when the project is in a repository.
// ============================================================================
//
// UI (this file)
//   Unresolved            Color   Red tint for a missing GUID with nothing chosen.
//   OnGUI()               Reason, files, Rescan, one box per missing GUID, Apply.
//   DrawCandidate(missing, index, candidate)  A radio choice, how it was found, and a chosen restore's breaks.
//   DrawBreak(problem, fixLabel, decision, fix, decide)  A break with Fix and Accept break.
//   DrawOther(missing, index)  "Other:" with any asset.
//
// FIELDS AND ACTIONS (Editor/GuidTools/BrokenReferenceWindow.cs)
//   files                 List<string>  Files scanned.
//   reason                string        Why the window opened.
//   plan                  RepairPlan    Missing GUIDs, their files and candidates.
//   choices               Dictionary<string, int>  Chosen candidate per missing GUID (its list length = Other).
//   others                Dictionary<string, Object>  The asset picked under Other.
//   scroll                Vector2       Scroll position.
//   Open(files, reason)   Opens the window and scans.
//   OpenForOpenScenes(reason)  Scans the open scenes and the prefab in Prefab Mode.
//   OpenPaths()           Paths of the open scenes and that prefab.
//   Rescan()              Scans, collects candidates, pre-selects single ones.
//   PreselectSingles()    Chooses the candidate of each missing GUID that has exactly one.
//   Chosen(guid) / OtherChosen(guid)  The chosen candidate, or whether Other is chosen.
//   Redirects()           Missing GUID to chosen asset GUID.
//   Restores()            Chosen Git restores.
//   FixMissingReferences(restore)  Adds a restored file's missing references to the list.
//   ApplyRedirects()      Checks every restore's breaks are decided, confirms, restores, rewrites, reloads.
//
// RepairPlan / RepairCandidate / BreakDecision (Editor/GuidTools/GuidReferenceRepair.cs)
//   RepairPlan: Files, References (file to offsets), Missing (GUID to files), Candidates, LastKnownPaths.
//   RepairCandidate: TargetGuid or a restore (Commit, RepoPath), Path, Sources, and the restore's
//     breaks: MissingReferences, PathTaken, GuidInUseBy, each with a BreakDecision (Undecided, Fix, Accept).
// GuidReferenceRepair: Scan(files, log), Suggest(plan, guids, log), AddDependencies(plan, restore, log),
//   Apply(plan, redirects, restores, log), SelectedFiles(), IsMissing(guid), FindGuidReferences(bytes), RepairSelected().
// BrokenReferenceMonitor (BrokenReferenceWindow.cs): offers the window when a file opens with
//   missing GUIDs or Unity logs several missing-script or missing-prefab messages.
