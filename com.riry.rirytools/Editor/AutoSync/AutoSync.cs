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
        // Remote URLs, one per line: the large-file notice was shown this session / turned off.
        private const string NoticeShownKey = "RiryTools.AutoSync.LargeFileNoticeShown";
        private const string NoticeOffKey = "RiryTools.AutoSync.LargeFileNoticeOff";

        // One save: the commit title and the saved files. Saves made in the same editor update are one save.
        private static readonly List<AutoSyncJob.Save> queue = new List<AutoSyncJob.Save>();
        private static AutoSyncJob.Save collecting;
        private static Task<AutoSyncJob.Held> worker;
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
            Queue(assetPath);
        }

        /// <summary>Queues any file for the next commit, such as an Extract Animation settings file.</summary>
        internal static void Track(string path)
        {
            if (!Enabled || string.IsNullOrEmpty(path)) return;
            Queue(path);
        }

        private static void Queue(string assetPath)
        {
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

        /// <summary>
        /// Commits every pending save now, without pushing (the next push sends them),
        /// so a branch switch or history rewrite starts from a settled branch.
        /// </summary>
        internal static void CommitNow()
        {
            Wait();
            if (worker != null && !worker.IsCompleted)
                throw new InvalidOperationException("Sync is still committing; try again in a moment.");
            if (worker != null)
            {
                Report(worker);
                worker = null;
                running = null;
            }
            List<AutoSyncJob.Save> saves = Unfinished();
            queue.Clear();
            collecting = null;
            if (saves.Count > 0) CreateJob(saves, false).Execute();
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
            string project = RiryToolsPaths.ProjectRoot;
            return new AutoSyncJob
            {
                ProjectRoot = project,
                Saves = saves,
                Push = push,
                Remote = GitRemotes.Selected(GitRemotes.Names(project))
            };
        }

        private static void Report(Task<AutoSyncJob.Held> finished)
        {
            if (!finished.IsFaulted)
            {
                reported = null;
                if (finished.Result != null) LargeFileNotice(finished.Result);
                return;
            }
            string message = finished.Exception.GetBaseException().Message;
            // The same failure repeats on every save; the Console gets it once.
            if (message != reported) Debug.LogWarning("AutoSync: " + message);
            reported = message;
        }

        /// <summary>
        /// Says the push is held because of files over 100 MiB: once per remote per
        /// session, never again for a remote after Do Not Show Again.
        /// </summary>
        private static void LargeFileNotice(AutoSyncJob.Held held)
        {
            List<string> shown = Lines(SessionState.GetString(NoticeShownKey, ""));
            List<string> off = Lines(EditorUserSettings.GetConfigValue(NoticeOffKey));
            if (shown.Contains(held.Url) || off.Contains(held.Url)) return;
            shown.Add(held.Url);
            SessionState.SetString(NoticeShownKey, string.Join("\n", shown));
            Debug.LogWarning("AutoSync: not pushing to " + held.Remote + " while commits hold files over 100 MiB.");
            int choice = EditorUtility.DisplayDialogComplex("Push Held",
                held.Files.Count + " file(s) over 100 MiB are in commits not yet pushed to " + held.Remote + " (" + held.Url +
                "). If the server is behind Cloudflare, the push will fail, so Sync commits without pushing until they are " +
                "removed.\n\n" + string.Join("\n", held.Files.GetRange(0, Math.Min(8, held.Files.Count))),
                "Fix Now", "Close", "Do Not Show Again");
            if (choice == 0) LargeFilesWindow.Open();
            if (choice == 2)
            {
                off.Add(held.Url);
                EditorUserSettings.SetConfigValue(NoticeOffKey, string.Join("\n", off));
            }
        }

        private static List<string> Lines(string text)
        {
            return new List<string>((text ?? "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries));
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
