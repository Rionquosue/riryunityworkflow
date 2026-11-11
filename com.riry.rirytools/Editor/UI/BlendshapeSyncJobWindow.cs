using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// The popup for one Blendshape Sync job (Add Job, Edit): it draws the job's own
    /// Blendshape Sync window. Closing it keeps the job running (Editor/BlendshapeSyncJobs.cs).
    /// </summary>
    internal sealed class BlendshapeSyncJobWindow : EditorWindow
    {
        [SerializeField] private BlendshapeSyncWindow job;

        internal static void Open(BlendshapeSyncWindow job)
        {
            // One popup per job: Edit on an open job brings its popup forward.
            foreach (BlendshapeSyncJobWindow open in Resources.FindObjectsOfTypeAll<BlendshapeSyncJobWindow>())
            {
                if (open.job != job) continue;
                open.Focus();
                return;
            }
            var window = CreateInstance<BlendshapeSyncJobWindow>();
            window.job = job;
            window.titleContent = new GUIContent("Blendshape Sync Job");
            window.minSize = new Vector2(440f, 320f);
            window.ShowUtility();
        }

        private void OnGUI()
        {
            if (job == null)
            {
                Close();
                return;
            }
            RiryToolsMainMenu.Call(job, "OnGUI");
            if (GUI.changed) BlendshapeSyncJobs.SaveSoon();
        }

        private void OnInspectorUpdate()
        {
            Repaint();
        }

        private void OnDestroy()
        {
            if (job != null) BlendshapeSyncJobs.SaveAll();
        }
    }

    /// <summary>The Main Menu's Blendshape Sync tab: the running jobs, and Add Job.</summary>
    internal static class BlendshapeSyncJobList
    {
        // The window width a job's row fits on one line in: its fixed widths (495), spacing and padding, plus a buffer.
        private const float WideRow = 570f;

        internal static void Draw()
        {
            List<BlendshapeSyncWindow> jobs = BlendshapeSyncJobs.All();
            if (jobs.Count == 0)
                EditorGUILayout.HelpBox("No Blendshape Sync jobs. Add Job sets a master mesh and the meshes that follow it.",
                    MessageType.Info);
            foreach (BlendshapeSyncWindow job in jobs)
            {
                // Narrower than the row needs (a docked window can be any size): the name on one line
                // and the buttons on the next, instead of cutting off Remove.
                bool wide = EditorGUIUtility.currentViewWidth >= WideRow;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                using (new EditorGUILayout.HorizontalScope())
                {
                    int count = job.ChildCount;
                    string name = job.Master != null
                        ? job.Master.name + " > " + count + (count == 1 ? " child" : " children")
                        : "No master yet";
                    EditorGUILayout.LabelField(new GUIContent(name, job.Problem), GUILayout.MinWidth(120f));
                    if (!wide)
                    {
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                    }
                    bool auto = GUILayout.Toggle(job.Auto, new GUIContent("Auto", "Sync automatically."), GUILayout.Width(50f));
                    if (auto != job.Auto)
                    {
                        Undo.RecordObject(job, BlendshapeSyncWindow.SettingsUndo);
                        job.Auto = auto;
                        BlendshapeSyncJobs.SaveAll();
                    }
                    if (GUILayout.Button("Sync Now", GUILayout.Width(80f))) job.RunSync();
                    if (GUILayout.Button("Edit", GUILayout.Width(50f))) BlendshapeSyncJobWindow.Open(job);
                    using (new EditorGUI.DisabledScope(!job.HasOriginals))
                    {
                        if (GUILayout.Button("Set All to Original", GUILayout.Width(130f))) job.RunRestore();
                    }
                    if (GUILayout.Button("Remove", GUILayout.Width(65f)))
                    {
                        // Ctrl+Z can't bring a job back, and its original weights go with it.
                        if (EditorUtility.DisplayDialog("Blendshape Sync", "Remove job " + job.JobNumber + " (" + name + ")?" +
                                (job.HasOriginals ? "\n\nIts original weights (Set All to Original) are lost with it." : "") +
                                "\n\nCtrl+Z can't bring the job back. The renderers' current weights stay.", "Remove", "Cancel"))
                            BlendshapeSyncJobs.Remove(job);
                        GUIUtility.ExitGUI();
                    }
                }
            }
            EditorGUILayout.Space(4f);
            if (GUILayout.Button("Add Job", GUILayout.Height(24f))) BlendshapeSyncJobs.Add();
        }
    }
}

// ============================================================================
// GLOSSARY: Blendshape Sync jobs
// The Tools > RiryTools > Blendshape Sync entry (Add Job) is in RiryToolsMenuBar.cs.
// ============================================================================
//
// BlendshapeSyncJobs (Editor/BlendshapeSyncJobs.cs)
//   All() / Add() / Remove(job)  The jobs: Blendshape Sync windows kept running unseen (HideAndDontSave).
//   SaveSoon()                   Popup edits: SaveAll once no edit came for SaveDelay (1 s), not on every GUI event.
//   SaveAll() / Load(scene)      UserSettings/RiryTools/BlendshapeSync/<scene GUID>.json, one file per scene;
//                                loaded when the scene opens, saved when it is saved or closes, at a script
//                                reload and on quit. A closing scene's jobs end with it.
// BlendshapeSyncWindow, job part (Editor/BlendshapeSyncJob.cs)
//   JobNumber, Master, ChildCount, Auto, HasOriginals, Problem, RunSync(), RunRestore().
//   SavedJob / SavedChild / Save() / Load(saved, master) / Find(id)  Renderers saved as GlobalObjectId strings.
//   missingChildren     [S] Saved children not found on Load; saved again with the job, so they are never dropped.
//   Save(scene) keeps saved jobs whose master is not found (Read), and never replaces a file it cannot read. Load warns
//   once about jobs and children it could not restore.
// BlendshapeSyncJobWindow (this file): the job popup; draws the job's own window; closing keeps the job.
// BlendshapeSyncJobList (this file): the Main Menu tab: one row per job (master > children, Auto, Sync Now,
//   Edit, Set All to Original, Remove (asks first: not undoable, its originals go with it)), then Add Job. Below WideRow (570) window width the buttons go on a second line.
