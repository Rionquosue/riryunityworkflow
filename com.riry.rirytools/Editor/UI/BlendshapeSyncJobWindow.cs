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
            if (GUI.changed) BlendshapeSyncJobs.SaveAll();
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
        internal static void Draw()
        {
            List<BlendshapeSyncWindow> jobs = BlendshapeSyncJobs.All();
            if (jobs.Count == 0)
                EditorGUILayout.HelpBox("No Blendshape Sync jobs. Add Job sets a master mesh and the meshes that follow it.",
                    MessageType.Info);
            foreach (BlendshapeSyncWindow job in jobs)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    int count = job.ChildCount;
                    string name = job.Master != null
                        ? job.Master.name + " > " + count + (count == 1 ? " child" : " children")
                        : "No master yet";
                    EditorGUILayout.LabelField(new GUIContent(name, job.Problem), GUILayout.MinWidth(120f));
                    bool auto = GUILayout.Toggle(job.Auto, new GUIContent("Auto", "Sync automatically."), GUILayout.Width(50f));
                    if (auto != job.Auto)
                    {
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
// The Tools > RiryTools > Blendshape Sync entry (Add Job) is in RiryMenus.cs.
// ============================================================================
//
// BlendshapeSyncJobs (Editor/BlendshapeSyncJobs.cs)
//   All() / Add() / Remove(job)  The jobs: Blendshape Sync windows kept running unseen (HideAndDontSave).
//   SaveAll() / Load(scene)      UserSettings/RiryTools/BlendshapeSync/<scene GUID>.json, one file per scene;
//                                loaded when the scene opens, saved when it is saved or closes, at a script
//                                reload and on quit. A closing scene's jobs end with it.
// BlendshapeSyncWindow, job part (Editor/BlendshapeSyncJob.cs)
//   JobNumber, Master, ChildCount, Auto, HasOriginals, Problem, RunSync(), RunRestore().
//   SavedJob / SavedChild / Save() / Load(saved, master) / Find(id)  Renderers saved as GlobalObjectId strings.
// BlendshapeSyncJobWindow (this file): the job popup; draws the job's own window; closing keeps the job.
// BlendshapeSyncJobList (this file): the Main Menu tab: one row per job (master > children, Auto, Sync Now,
//   Edit, Set All to Original, Remove), then Add Job.
