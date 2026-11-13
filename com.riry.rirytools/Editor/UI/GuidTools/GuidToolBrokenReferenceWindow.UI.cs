using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Riry.RiryTools.Editor
{
    /// <summary>What the Broken References window draws. State and actions are in Editor/UI/GuidToolBrokenReferenceWindow.cs.</summary>
    internal sealed partial class GuidToolBrokenReferenceWindow
    {
        private static readonly Color Unresolved = new Color(1f, 0.35f, 0.35f);
        private static readonly Color BreakColor = new Color(1f, 0.6f, 0.1f);
        private static readonly Color RecursiveColor = new Color(1f, 0.2f, 0.2f);
        private static GUIStyle wrappedBold;
        private const string RecursiveNote = "* Broken references are inevitable. Please run tool again after applying.";

        private void OnGUI()
        {
            EditorGUILayout.LabelField(reason, EditorStyles.wordWrappedLabel);
            // Capped, so a selected folder of thousands of files cannot push Apply off the window.
            const int ShownFiles = 10;
            EditorGUILayout.LabelField("Files (" + files.Count + "): " + string.Join(", ", files.GetRange(0, Math.Min(ShownFiles, files.Count))) +
                (files.Count > ShownFiles ? " and " + (files.Count - ShownFiles) + " more" : ""), EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("Rescan")) { Rescan(); RiryToolGUI.ExitAfterModal(); }
            if (plan == null) return;
            if (plan.Missing.Count == 0)
            {
                EditorGUILayout.HelpBox("No broken references were found.", MessageType.Info);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (string missing in new List<string>(plan.Missing.Keys))
            {
                List<GuidToolRepairCandidate> candidates;
                if (!plan.Candidates.TryGetValue(missing, out candidates)) candidates = new List<GuidToolRepairCandidate>();
                bool resolved = Redirects().ContainsKey(missing) || Chosen(missing) != null && Chosen(missing).IsRestore;
                Color previous = GUI.backgroundColor;
                if (!resolved) GUI.backgroundColor = Unresolved;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    GUI.backgroundColor = previous;
                    string lastPath;
                    plan.LastKnownPaths.TryGetValue(missing, out lastPath);
                    // Wrapped, so a long last-known path is never cut off.
                    if (wrappedBold == null) wrappedBold = new GUIStyle(EditorStyles.boldLabel) { wordWrap = true };
                    EditorGUILayout.LabelField("Missing " + missing + (lastPath != null ? " (was " + lastPath + ")" : ""), wrappedBold);
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
                if (GUILayout.Button("Apply " + ready + " Repair(s)", GUILayout.Height(28f))) { ApplyRedirects(); RiryToolGUI.ExitAfterModal(); }
            }
        }

        // One choice: what it is, every way it was found, and for a restore, what it would break.
        private void DrawCandidate(string missing, int index, GuidToolRepairCandidate candidate)
        {
            int chosen;
            bool on = choices.TryGetValue(missing, out chosen) && chosen == index;
            string label = candidate.IsRestore
                ? "Restore " + candidate.Path + " from Git (" + candidate.Commit.Substring(0, Math.Min(8, candidate.Commit.Length)) + ")"
                : candidate.Path;
            // MinWidth 0: a long path is cut off (full in the tooltip) instead of widening the window.
            if (GUILayout.Toggle(on, new GUIContent(label, label), EditorStyles.radioButton, GUILayout.MinWidth(0f)) && !on)
            {
                // A chosen restore draws its breaks, so start a fresh layout pass.
                choices[missing] = index;
                GUIUtility.ExitGUI();
            }
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.LabelField(string.Join("; ", candidate.Sources), EditorStyles.wordWrappedMiniLabel);
                if (!candidate.IsRestore || !on) return;
                // A taken path is not a break: the restore goes beside it, and its references follow it by GUID.
                if (candidate.PathTaken)
                    EditorGUILayout.LabelField(candidate.Path + " is taken by another file, so it is restored as " +
                        System.IO.Path.GetFileName(candidate.Destination) + " beside it. Nothing is overwritten; references follow " +
                        "the restored file by its GUID.", EditorStyles.wordWrappedMiniLabel);
                if (candidate.MissingReferences.Count == 0 && candidate.GuidInUseBy == null) return;
                // Past the second level of fixes, what this restore breaks stays broken until the next run.
                bool recursive = plan.DepthOf(missing) >= GuidToolRepairPlan.MaxFixDepth;
                Color previous = GUI.backgroundColor;
                GUI.backgroundColor = recursive ? RecursiveColor : BreakColor;
                bool open = expanded.Contains(candidate);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(EditorGUI.indentLevel * 15f);
                    if (GUILayout.Button(recursive ? "RECURSIVE BREAK DETECTED" : "Break detected", EditorStyles.miniButton))
                    {
                        if (open) expanded.Remove(candidate);
                        else expanded.Add(candidate);
                        GUI.backgroundColor = previous;
                        GUIUtility.ExitGUI();
                    }
                }
                GUI.backgroundColor = previous;
                if (!open) return;
                if (candidate.MissingReferences.Count > 0)
                    DrawBreak("It references " + candidate.MissingReferences.Count + " asset(s) that are missing now.",
                        "Repair them too", candidate.MissingDecision,
                        // Adds boxes to the list, so after this GUI pass.
                        () => EditorApplication.delayCall += () =>
                        {
                            FixMissingReferences(missing, candidate);
                            Repaint();
                        },
                        decision => candidate.MissingDecision = decision);
                if (candidate.GuidInUseBy != null)
                    DrawBreak(candidate.GuidInUseBy + " uses this GUID now; Unity would give one of them a new GUID.",
                        null, candidate.GuidDecision, null, decision => candidate.GuidDecision = decision);
                if (recursive) EditorGUILayout.LabelField(RecursiveNote, EditorStyles.wordWrappedMiniLabel);
            }
        }

        // A break with Fix (when one exists) and Accept break; the chosen one is pressed in.
        private static void DrawBreak(string problem, string fixLabel, GuidToolBreakDecision decision, Action fix,
            Action<GuidToolBreakDecision> decide)
        {
            EditorGUILayout.HelpBox(problem, decision == GuidToolBreakDecision.Undecided ? MessageType.Warning : MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(EditorGUI.indentLevel * 15f);
                if (fix != null && GUILayout.Toggle(decision == GuidToolBreakDecision.Fix, "Fix: " + fixLabel, EditorStyles.miniButtonLeft) &&
                    decision != GuidToolBreakDecision.Fix)
                    fix();
                if (GUILayout.Toggle(decision == GuidToolBreakDecision.Accept, "Accept break",
                        fix != null ? EditorStyles.miniButtonRight : EditorStyles.miniButton) && decision != GuidToolBreakDecision.Accept)
                    decide(GuidToolBreakDecision.Accept);
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
// GLOSSARY: Broken References window, drawing
// Opened from the GUID Tools window (Check for Broken References and Repair Missing GUID References), Project right-click > RiryTools > Repair Missing GUID References (RiryToolsRightClickMenu.cs), and the GuidToolBrokenReferenceMonitor prompt. Fields and actions are in GuidToolBrokenReferenceWindow.cs.
// ============================================================================
//
// GuidToolBrokenReferenceWindow (this part)
//   Unresolved            Color   Red tint for a missing GUID with nothing chosen.
//   OnGUI()               Reason, files, Rescan, one box per missing GUID, Apply.
//   BreakColor / RecursiveColor  Orange "Break detected" and red "RECURSIVE BREAK DETECTED" buttons.
//   RecursiveNote         const string  Shown under a recursive break's options.
//   DrawCandidate(missing, index, candidate)  A radio choice, how it was found, and for a chosen restore
//                         that breaks something, the Break detected button that opens its options.
//   DrawBreak(problem, fixLabel, decision, fix, decide)  A break with Fix and Accept break.
//   DrawOther(missing, index)  "Other:" with any asset.
//
