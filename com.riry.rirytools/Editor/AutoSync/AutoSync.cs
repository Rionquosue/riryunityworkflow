using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Sync: while on, every scene or prefab save is committed to the checked-out
    /// branch, titled with the time of the save, and pushed when there is a remote.
    /// Off until the user turns it on; the setting is per user and per project (UserSettings).
    /// </summary>
    [InitializeOnLoad]
    internal static class AutoSync
    {
        private const string EnabledKey = "RiryTools.AutoSync.Enabled";
        private const string PendingKey = "RiryTools.AutoSync.Pending";
        private const string TimeFormat = "yyyy-MM-dd'T'HH:mm:sszzz";

        // One save: the commit title and the saved files. Saves made in the same editor update are one save.
        private static readonly List<AutoSyncJob.Save> queue = new List<AutoSyncJob.Save>();
        private static AutoSyncJob.Save collecting;
        private static Task worker;
        private static List<AutoSyncJob.Save> running;
        private static string reported;

        static AutoSync()
        {
            EditorSceneManager.sceneSaved += scene => Saved(scene.path);
            PrefabStage.prefabSaved += root =>
            {
                PrefabStage stage = PrefabStageUtility.GetPrefabStage(root);
                if (stage != null) Saved(stage.assetPath);
            };
            EditorApplication.update += Tick;
            EditorApplication.quitting += FlushOnQuit;
            // Saves not committed before a script reload are committed afterwards.
            AssemblyReloadEvents.beforeAssemblyReload += () =>
            {
                Wait();
                SessionState.SetString(PendingKey, Encode(Unfinished()));
            };
            queue.AddRange(Decode(SessionState.GetString(PendingKey, "")));
            SessionState.EraseString(PendingKey);
        }

        internal static bool Enabled
        {
            get { return EditorUserSettings.GetConfigValue(EnabledKey) == "1"; }
            set { EditorUserSettings.SetConfigValue(EnabledKey, value ? "1" : "0"); }
        }

        /// <summary>Queues a saved scene or prefab for the next commit.</summary>
        internal static void Saved(string assetPath)
        {
            if (!Enabled || string.IsNullOrEmpty(assetPath)) return;
            if (!assetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) &&
                !assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                return;
            if (collecting == null)
                collecting = new AutoSyncJob.Save(DateTimeOffset.Now.ToString(TimeFormat, CultureInfo.InvariantCulture));
            if (!collecting.Paths.Contains(assetPath)) collecting.Paths.Add(assetPath);
        }

        private static void Tick()
        {
            if (collecting != null)
            {
                queue.Add(collecting);
                collecting = null;
            }
            if (worker != null)
            {
                if (!worker.IsCompleted) return;
                Report(worker);
                worker = null;
                running = null;
            }
            if (queue.Count == 0) return;
            running = new List<AutoSyncJob.Save>(queue);
            queue.Clear();
            AutoSyncJob job = CreateJob(running, true);
            worker = Task.Run(() => job.Execute());
        }

        private static void FlushOnQuit()
        {
            Wait();
            List<AutoSyncJob.Save> saves = Unfinished();
            if (saves.Count == 0) return;
            // Commit locally only; a push could hold the editor open. The next push sends these commits too.
            try
            {
                CreateJob(saves, false).Execute();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("AutoSync: " + exception.Message);
            }
        }

        // A running pass has the index; give it a moment to finish.
        private static void Wait()
        {
            if (worker != null) Task.WaitAny(new[] { worker }, TimeSpan.FromSeconds(10));
        }

        // Saves not yet committed: the queue, the save being collected, and a pass that did not finish.
        private static List<AutoSyncJob.Save> Unfinished()
        {
            var saves = new List<AutoSyncJob.Save>();
            if (worker != null && !worker.IsCompleted && running != null) saves.AddRange(running);
            saves.AddRange(queue);
            if (collecting != null) saves.Add(collecting);
            return saves;
        }

        private static AutoSyncJob CreateJob(List<AutoSyncJob.Save> saves, bool push)
        {
            return new AutoSyncJob { ProjectRoot = RiryToolsPaths.ProjectRoot, Saves = saves, Push = push };
        }

        private static void Report(Task finished)
        {
            if (!finished.IsFaulted)
            {
                reported = null;
                return;
            }
            string message = finished.Exception.GetBaseException().Message;
            // The same failure repeats on every save; the Console gets it once.
            if (message != reported) Debug.LogWarning("AutoSync: " + message);
            reported = message;
        }

        // Session state holds one save per line: the title, then each path, separated by tabs.
        private static string Encode(List<AutoSyncJob.Save> saves)
        {
            var lines = new List<string>();
            foreach (AutoSyncJob.Save save in saves) lines.Add(save.Title + "\t" + string.Join("\t", save.Paths));
            return string.Join("\n", lines);
        }

        private static List<AutoSyncJob.Save> Decode(string text)
        {
            var saves = new List<AutoSyncJob.Save>();
            foreach (string line in text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] fields = line.Split('\t');
                var save = new AutoSyncJob.Save(fields[0]);
                for (int i = 1; i < fields.Length; i++) save.Paths.Add(fields[i]);
                saves.Add(save);
            }
            return saves;
        }
    }

    internal sealed class AutoSyncSaveProcessor : AssetModificationProcessor
    {
        // Saves made through Unity's save pipeline, in addition to the scene and Prefab Mode events.
        private static string[] OnWillSaveAssets(string[] paths)
        {
            foreach (string path in paths) AutoSync.Saved(path);
            return paths;
        }
    }
}
