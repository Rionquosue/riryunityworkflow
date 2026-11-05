using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Blendshape Sync jobs: each job is one Blendshape Sync window (a master and its
    /// children), kept running unseen, so it keeps syncing after its popup closes. Jobs
    /// are saved per scene, for this user, in UserSettings/RiryTools/BlendshapeSync/
    /// &lt;scene GUID&gt;.json and come back when the scene opens. Jobs of an unsaved
    /// scene or of Prefab Mode last until it closes. The list is in the Main Menu's
    /// Blendshape Sync tab and the popup in Editor/UI/BlendshapeSyncJobWindow.cs.
    /// </summary>
    [InitializeOnLoad]
    internal static class BlendshapeSyncJobs
    {
        [Serializable]
        private sealed class SavedJobs
        {
            public List<BlendshapeSyncWindow.SavedJob> Jobs = new List<BlendshapeSyncWindow.SavedJob>();
        }

        static BlendshapeSyncJobs()
        {
            EditorSceneManager.sceneOpened += (scene, mode) => Load(scene);
            EditorSceneManager.sceneClosing += (scene, removing) => Close(scene);
            EditorSceneManager.sceneSaved += scene => Save(scene);
            EditorApplication.quitting += SaveAll;
            AssemblyReloadEvents.beforeAssemblyReload += SaveAll;
            // After a start the open scenes' jobs are loaded; after a script reload they are still running.
            EditorApplication.delayCall += () =>
            {
                for (int i = 0; i < SceneManager.sceneCount; i++) Load(SceneManager.GetSceneAt(i));
            };
        }

        /// <summary>Every job, oldest first.</summary>
        internal static List<BlendshapeSyncWindow> All()
        {
            var jobs = new List<BlendshapeSyncWindow>();
            foreach (BlendshapeSyncWindow window in Resources.FindObjectsOfTypeAll<BlendshapeSyncWindow>())
            {
                // Jobs are numbered from 1; other unseen Blendshape Sync windows are not jobs.
                if ((window.hideFlags & HideFlags.DontSave) != 0 && window.JobNumber > 0) jobs.Add(window);
            }
            jobs.Sort((left, right) => left.JobNumber.CompareTo(right.JobNumber));
            return jobs;
        }

        /// <summary>Add Job: a new, empty job in its popup.</summary>
        internal static void Add()
        {
            BlendshapeSyncJobWindow.Open(Create());
        }

        private static BlendshapeSyncWindow Create()
        {
            int number = 0;
            foreach (BlendshapeSyncWindow job in All()) number = Math.Max(number, job.JobNumber);
            var created = ScriptableObject.CreateInstance<BlendshapeSyncWindow>();
            created.hideFlags = HideFlags.HideAndDontSave;
            created.JobNumber = number + 1;
            return created;
        }

        internal static void Remove(BlendshapeSyncWindow job)
        {
            UnityEngine.Object.DestroyImmediate(job);
            SaveAll();
        }

        private const double SaveDelay = 1.0;
        private static double saveAt = -1.0;

        /// <summary>Saves once no edit has come for SaveDelay seconds: a drag or typing makes one write, not one per event.</summary>
        internal static void SaveSoon()
        {
            if (saveAt < 0.0) EditorApplication.update += SaveWhenDue;
            saveAt = EditorApplication.timeSinceStartup + SaveDelay;
        }

        private static void SaveWhenDue()
        {
            if (EditorApplication.timeSinceStartup < saveAt) return;
            EditorApplication.update -= SaveWhenDue;
            saveAt = -1.0;
            SaveAll();
        }

        /// <summary>Saves the jobs of every open scene.</summary>
        internal static void SaveAll()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++) Save(SceneManager.GetSceneAt(i));
        }

        private static string FileFor(Scene scene)
        {
            string guid = string.IsNullOrEmpty(scene.path) ? "" : AssetDatabase.AssetPathToGUID(scene.path);
            return guid.Length == 0 ? null
                : Path.Combine(RiryToolsPaths.ProjectRoot, "UserSettings", "RiryTools", "BlendshapeSync", guid + ".json");
        }

        private static void Save(Scene scene)
        {
            string file = FileFor(scene);
            if (file == null || !scene.isLoaded) return;
            var saved = new SavedJobs();
            foreach (BlendshapeSyncWindow job in All())
            {
                if (job.Master != null && job.Master.gameObject.scene == scene) saved.Jobs.Add(job.Save());
            }
            // Saved jobs whose master is not found (not restored on load) are written back, never dropped.
            // A file that cannot be read is left as it is rather than replaced.
            SavedJobs earlier = Read(file);
            if (earlier == null && File.Exists(file)) return;
            if (earlier != null && earlier.Jobs != null)
                saved.Jobs.AddRange(earlier.Jobs.FindAll(job => job != null && BlendshapeSyncWindow.Find(job.Master) == null));
            try
            {
                if (saved.Jobs.Count == 0)
                {
                    if (File.Exists(file)) File.Delete(file);
                    return;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                File.WriteAllText(file, JsonUtility.ToJson(saved, true), new UTF8Encoding(false));
            }
            catch (Exception exception)
            {
                // Once per file and problem, however often saving is tried.
                if (unsaved.Add(file + "\n" + exception.Message))
                    Debug.LogWarning("Blendshape Sync: could not save the jobs of " + scene.path + " to " + file + ": " + exception.Message);
            }
        }

        private static void Load(Scene scene)
        {
            string file = FileFor(scene);
            if (file == null || !scene.isLoaded || !File.Exists(file)) return;
            SavedJobs saved = Read(file);
            if (saved == null || saved.Jobs == null) return;
            List<BlendshapeSyncWindow> running = All();
            int missingJobs = 0, missingChildren = 0;
            foreach (BlendshapeSyncWindow.SavedJob job in saved.Jobs)
            {
                if (job == null) continue;
                SkinnedMeshRenderer master = BlendshapeSyncWindow.Find(job.Master);
                if (master == null)
                {
                    missingJobs++;
                    continue;
                }
                // A job already running (after a script reload) is not loaded twice.
                if (running.Exists(other => other.Master == master)) continue;
                BlendshapeSyncWindow created = Create();
                missingChildren += created.Load(job, master);
                running.Add(created);
            }
            if (missingJobs + missingChildren > 0)
                Debug.LogWarning("Blendshape Sync: in " + scene.path + ", " + missingJobs + " saved job(s) whose master and " +
                    missingChildren + " child renderer(s) were not found, so they were not restored. They stay saved in " + file +
                    " and come back once the objects are found again.");
        }

        private static readonly HashSet<string> unreadable = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> unsaved = new HashSet<string>(StringComparer.Ordinal);

        // The saved jobs in the file, or null when it is missing or unreadable (reported once per file and problem).
        private static SavedJobs Read(string file)
        {
            if (!File.Exists(file)) return null;
            try
            {
                return JsonUtility.FromJson<SavedJobs>(File.ReadAllText(file));
            }
            catch (Exception exception)
            {
                if (unreadable.Add(file + "\n" + exception.Message))
                    Debug.LogWarning("Blendshape Sync: could not read " + file + "; it is left as it is and not saved over: " + exception.Message);
                return null;
            }
        }

        // A closing scene's jobs are saved, then end with it.
        private static void Close(Scene scene)
        {
            Save(scene);
            foreach (BlendshapeSyncWindow job in All())
            {
                if (job.Master != null && job.Master.gameObject.scene == scene) UnityEngine.Object.DestroyImmediate(job);
            }
        }
    }
}
