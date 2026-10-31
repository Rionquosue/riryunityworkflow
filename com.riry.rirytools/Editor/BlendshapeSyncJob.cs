using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// What the job list and saving need from a Blendshape Sync window running as a
    /// job (Editor/BlendshapeSyncJobs.cs). The window's own behaviour is unchanged.
    /// </summary>
    internal sealed partial class BlendshapeSyncWindow
    {
        /// <summary>A saved job: renderers as GlobalObjectId strings.</summary>
        [Serializable]
        internal sealed class SavedJob
        {
            public string Master = "";
            public List<SavedChild> Children = new List<SavedChild>();
            public bool Auto;
            public bool CaseSensitive;
        }

        [Serializable]
        internal sealed class SavedChild
        {
            public string Renderer = "";
            public List<ManualLink> Links = new List<ManualLink>();
        }

        // Order in the job list.
        [SerializeField] private int jobNumber;

        internal int JobNumber
        {
            get { return jobNumber; }
            set { jobNumber = value; }
        }

        internal SkinnedMeshRenderer Master
        {
            get { return master; }
        }

        internal int ChildCount
        {
            get { return children.FindAll(child => child != null && child.Renderer != null).Count; }
        }

        /// <summary>Run automatically, as the window's own toggle sets it.</summary>
        internal bool Auto
        {
            get { return runAutomatically; }
            set
            {
                if (value == runAutomatically) return;
                runAutomatically = value;
                nextTick = EditorApplication.timeSinceStartup + Interval;
            }
        }

        internal bool HasOriginals
        {
            get { return originals.Count > 0; }
        }

        internal string Problem
        {
            get { return planError; }
        }

        internal void RunSync()
        {
            SyncNow();
        }

        internal void RunRestore()
        {
            RestoreOriginals();
        }

        internal SavedJob Save()
        {
            var saved = new SavedJob { Master = Id(master), Auto = runAutomatically, CaseSensitive = caseSensitive };
            foreach (ChildEntry child in children)
            {
                if (child == null || child.Renderer == null) continue;
                var links = new List<ManualLink>();
                foreach (ManualLink link in child.Links)
                {
                    if (link != null && !IsBlank(link)) links.Add(new ManualLink { Source = link.Source, Destination = link.Destination });
                }
                saved.Children.Add(new SavedChild { Renderer = Id(child.Renderer), Links = links });
            }
            return saved;
        }

        /// <summary>Sets the job from a saved one; children that are gone are left out.</summary>
        internal void Load(SavedJob saved, SkinnedMeshRenderer savedMaster)
        {
            master = savedMaster;
            CaptureOriginal(master);
            children.Clear();
            foreach (SavedChild child in saved.Children ?? new List<SavedChild>())
            {
                SkinnedMeshRenderer renderer = Find(child.Renderer);
                if (renderer == null) continue;
                children.Add(new ChildEntry { Renderer = renderer, Links = child.Links ?? new List<ManualLink>() });
                CaptureOriginal(renderer);
            }
            EnsureChildRow();
            caseSensitive = saved.CaseSensitive;
            runAutomatically = saved.Auto;
            nextTick = EditorApplication.timeSinceStartup + Interval;
            InvalidatePlan();
        }

        private static string Id(UnityEngine.Object target)
        {
            return target == null ? "" : GlobalObjectId.GetGlobalObjectIdSlow(target).ToString();
        }

        internal static SkinnedMeshRenderer Find(string id)
        {
            GlobalObjectId parsed;
            if (string.IsNullOrEmpty(id) || !GlobalObjectId.TryParse(id, out parsed)) return null;
            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(parsed) as SkinnedMeshRenderer;
        }
    }
}
