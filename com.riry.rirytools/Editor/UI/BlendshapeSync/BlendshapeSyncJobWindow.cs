using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// The popup for one Blendshape Sync job (Add Job, Edit): it draws the job's own
    /// Blendshape Sync window. Closing it keeps the job running (Editor/BlendshapeSync/BlendshapeSyncJobs.cs).
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
}

// ============================================================================
// GLOSSARY: Blendshape Sync job popup
// ============================================================================
//
// BlendshapeSyncJobWindow (this file): the job popup (Add Job, Edit); draws the job's own window; closing keeps the job.
