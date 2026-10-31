using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// One model's Extract Animation settings, kept beside it in
    /// ".&lt;model&gt;_ani-extract.json~". Unity skips names that start with "." or end
    /// with "~", so the file gets no .meta. It exists only while there is something
    /// to keep (Automatically extract animations, or the saved cleaning settings),
    /// and is staged in Git when the project is a repository. The window is in
    /// Editor/UI/ExtractAnimationWindow.cs.
    /// </summary>
    [Serializable]
    internal sealed class AnimationExtractSettings
    {
        internal const string Suffix = "_ani-extract.json~";

        public string Destination = "";
        public bool AutoClean;
        public bool SaveCleanSettings;
        public bool AutoExtract;
        public CleanAnimationKeys.Options CleanOptions = new CleanAnimationKeys.Options();
        public List<string> Whitelist = new List<string>();

        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        /// <summary>True when the settings must be kept in the file.</summary>
        internal bool Worth
        {
            get { return AutoExtract || (AutoClean && SaveCleanSettings); }
        }

        /// <summary>".&lt;model name&gt;_ani-extract.json~" in the model's folder.</summary>
        internal static string PathFor(string modelPath)
        {
            return ImportPresetMarker.ParentFolder(modelPath) + "/." + Path.GetFileNameWithoutExtension(modelPath) + Suffix;
        }

        /// <summary>The model's saved settings, or null when it has none or they cannot be read.</summary>
        internal static AnimationExtractSettings Load(string modelPath)
        {
            string path = PathFor(modelPath);
            if (!File.Exists(path)) return null;
            try
            {
                var settings = JsonUtility.FromJson<AnimationExtractSettings>(File.ReadAllText(path));
                if (settings == null) return null;
                if (settings.Destination == null) settings.Destination = "";
                if (settings.CleanOptions == null) settings.CleanOptions = new CleanAnimationKeys.Options();
                if (settings.Whitelist == null) settings.Whitelist = new List<string>();
                return settings;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Extract Animation: could not read " + path + ": " + exception.Message);
                return null;
            }
        }

        /// <summary>
        /// Writes the settings when there is something to keep, else removes the
        /// file, then stages the change in Git. Cleaning is kept only with
        /// Save animation extract settings.
        /// </summary>
        internal static void Save(string modelPath, AnimationExtractSettings settings)
        {
            string path = PathFor(modelPath);
            bool existed = File.Exists(path);
            if (!settings.Worth)
            {
                if (!existed) return;
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
                Stage(path, true);
                return;
            }

            bool keepClean = settings.AutoClean && settings.SaveCleanSettings;
            var kept = new AnimationExtractSettings
            {
                Destination = settings.Destination,
                AutoClean = keepClean,
                SaveCleanSettings = keepClean,
                AutoExtract = settings.AutoExtract
            };
            if (keepClean)
            {
                kept.CleanOptions = settings.CleanOptions;
                kept.Whitelist = settings.Whitelist;
            }
            string text = JsonUtility.ToJson(kept, true);
            if (existed && File.ReadAllText(path) == text) return;
            // A hidden file cannot be overwritten on Windows, so it is unhidden first.
            if (existed) File.SetAttributes(path, FileAttributes.Normal);
            File.WriteAllText(path, text, Utf8);
            if (Application.platform == RuntimePlatform.WindowsEditor)
                File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.Hidden);
            Stage(path, false);
        }

        // git add (forced, so an ignore rule does not stop it); with Sync on, Sync also commits it.
        private static void Stage(string path, bool deleted)
        {
            try
            {
                if (!GitState.RepositoryExists) return;
                string project = RiryToolsPaths.ProjectRoot;
                var environment = new Dictionary<string, string> { { "GIT_LITERAL_PATHSPECS", "1" } };
                // A deleted file that was never tracked has nothing to stage.
                if (deleted && GitRunner.Run(project, environment, 10000, "ls-files", "--", path).Output.Trim().Length == 0)
                    return;
                GitRunner.Result result = GitRunner.Run(project, environment, 30000, "add", "--force", "--all", "--", path);
                if (!result.Succeeded) Debug.LogWarning("Extract Animation: " + result.Describe("Staging " + path));
                if (!deleted) AutoSync.Track(path);
            }
            catch (GitNotInstalledException)
            {
            }
        }
    }
}
