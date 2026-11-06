using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Copies a model's clips into Animations/ next to it (or the Destination in
    /// its Extract Animation settings) while the model is imported as Generic, so
    /// the copies keep plain bone curves instead of Humanoid muscle curves. The
    /// model's real preset is applied afterwards. With saved cleaning settings, the
    /// copies are cleaned with Clean Animation Keys.
    /// </summary>
    internal static class ImportPresetAnimations
    {
        internal const string FolderName = "Animations";

        private static string PendingFolder
        {
            get { return Path.Combine(RiryToolsPaths.Library("ImportPresets"), "Pending"); }
        }

        // Pending markers are files because a first import can run in an import
        // worker process, and they survive a domain reload before processing.
        internal static void MarkPending(string assetPath)
        {
            Directory.CreateDirectory(PendingFolder);
            File.WriteAllText(PendingFile(assetPath), assetPath, new UTF8Encoding(false));
        }

        internal static bool IsPending(string assetPath)
        {
            return File.Exists(PendingFile(assetPath));
        }

        internal static void ClearPending(string assetPath)
        {
            string file = PendingFile(assetPath);
            if (File.Exists(file)) File.Delete(file);
        }

        internal static List<string> PendingPaths()
        {
            var paths = new List<string>();
            if (!Directory.Exists(PendingFolder)) return paths;
            foreach (string file in Directory.GetFiles(PendingFolder, "*.txt"))
            {
                string path = File.ReadAllText(file).Trim();
                if (path.Length > 0) paths.Add(path);
            }
            return paths;
        }

        private static string PendingFile(string assetPath)
        {
            using (SHA1 sha = SHA1.Create())
            {
                byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(assetPath));
                return Path.Combine(PendingFolder, BitConverter.ToString(digest).Replace("-", "") + ".txt");
            }
        }

        /// <summary>
        /// Finishes a first import marked pending: extract as Generic, then apply
        /// the folder's preset for the model and reimport.
        /// </summary>
        internal static void ProcessFirstImport(string assetPath)
        {
            // Clear first, so the reimports below are not queued again.
            ClearPending(assetPath);
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null) return;

            EnsureGeneric(importer);
            AnimationExtractSettings settings = AnimationExtractSettings.Load(assetPath);
            Extraction result = ExtractClips(assetPath, settings, ImportPresetOwnedFiles.IsUnchanged, false);
            ReportSkipped(assetPath, result.Skipped);

            ImportPresetMarker.Rules rules = ImportPresetMarker.RulesFor(assetPath);
            string applied = null;
            if (rules != null)
            {
                ImportPresetMarker.Target target = rules.For(importer);
                if (target.Configuration != null &&
                    ImportPresetLibrary.TryApply(importer, target.Configuration, target.Preset, out applied))
                    importer.SaveAndReimport();
            }
            result.Log(assetPath, "extracted " + result.Written + " clip(s) from " + assetPath + " to " + result.Folder +
                (applied != null ? ", then applied " + applied : ""));
        }

        /// <summary>What one extraction did, so each model gets one Console line.</summary>
        internal sealed class Extraction
        {
            public int Written;
            public readonly List<string> Skipped = new List<string>();
            /// <summary>The folder the clips went to (Destination, or Animations/ when it was not valid).</summary>
            public string Folder;
            /// <summary>The cleaning's summary, or null when the clips were not cleaned.</summary>
            public string Cleaned;
            public bool CleanFailed;

            /// <summary>Logs the line with the cleaning appended, pinging the model: a warning when cleaning failed.</summary>
            internal void Log(string modelPath, string line)
            {
                line += Cleaned != null ? "; " + Cleaned : ".";
                UnityEngine.Object model = AssetDatabase.LoadMainAssetAtPath(modelPath);
                if (CleanFailed) RiryLog.Warn(RiryLog.ImportPresets, line, model);
                else RiryLog.Info(RiryLog.ImportPresets, line, model);
            }
        }

        /// <summary>
        /// Extracts clips from a model whose settings may already be configured:
        /// a snapshot of its settings is restored after a temporary Generic import.
        /// <paramref name="undoable"/>: only the Extract button's run is recorded for Undo; automatic runs
        /// (first import, re-export, Folder Rules Apply) are not, so they never take the user's next Ctrl+Z.
        /// </summary>
        internal static Extraction ExtractPreservingSettings(ModelImporter importer, AnimationExtractSettings settings,
            Func<string, bool> mayReplace, bool undoable)
        {
            Preset snapshot = new Preset(importer);
            bool changed = false;
            try
            {
                changed = EnsureGeneric(importer);
                return ExtractClips(importer.assetPath, settings, mayReplace, undoable);
            }
            finally
            {
                if (changed)
                {
                    if (snapshot.ApplyTo(importer)) importer.SaveAndReimport();
                    else
                        RiryLog.Error(RiryLog.ImportPresets, "Could not put back the import settings of " + importer.assetPath +
                            " after extracting its clips, so it was left as Generic. Set its Rig (and the rest) again in the " +
                            "Inspector, or reapply its preset.", AssetDatabase.LoadMainAssetAtPath(importer.assetPath));
                }
                UnityEngine.Object.DestroyImmediate(snapshot);
            }
        }

        private static bool EnsureGeneric(ModelImporter importer)
        {
            if (importer.animationType == ModelImporterAnimationType.Generic && importer.importAnimation)
                return false;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.SaveAndReimport();
            return true;
        }

        internal static string AnimationsFolder(string modelPath)
        {
            return ImportPresetMarker.ParentFolder(modelPath) + "/" + FolderName;
        }

        /// <summary>Whether clips are extracted before the model's preset is applied: its Automatically extract animations.</summary>
        internal static bool ShouldExtract(string modelPath)
        {
            AnimationExtractSettings settings = AnimationExtractSettings.Load(modelPath);
            return settings != null && settings.AutoExtract;
        }

        /// <summary>The settings' Destination when it is valid, else Animations/ next to the model.</summary>
        internal static string Destination(string modelPath, AnimationExtractSettings settings)
        {
            if (settings == null || settings.Destination.Length == 0) return AnimationsFolder(modelPath);
            if (DestinationProblem(settings.Destination) == null) return settings.Destination;
            // Reported once, by the extraction that uses it (ExtractClips).
            return AnimationsFolder(modelPath);
        }

        /// <summary>Why a Destination cannot be used, or null.</summary>
        internal static string DestinationProblem(string folder)
        {
            if (folder != "Assets" && !folder.StartsWith("Assets/", StringComparison.Ordinal))
                return "Destination must be a folder in Assets.";
            foreach (string part in folder.Split('/'))
            {
                if (part.Length == 0 || part == "." || part == ".." || part.Trim() != part ||
                    part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || part.IndexOfAny("/\\:*?\"<>|".ToCharArray()) >= 0)
                    return "Destination is not a valid folder path.";
            }
            return null;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = ImportPresetMarker.ParentFolder(folder);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        private static List<AnimationClip> Clips(string modelPath)
        {
            var clips = new List<AnimationClip>();
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetRepresentationsAtPath(modelPath))
            {
                var clip = asset as AnimationClip;
                if (clip != null && !clip.name.StartsWith("__preview__", StringComparison.Ordinal)) clips.Add(clip);
            }
            return clips;
        }

        /// <summary>True when the model has embedded animations.</summary>
        internal static bool HasClips(string modelPath)
        {
            return Clips(modelPath).Count > 0;
        }

        /// <summary>Paths the model's clips extract to. Clip names do not depend on the rig type.</summary>
        private static List<string> PlannedPaths(string modelPath, string folder)
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var paths = new List<string>();
            string modelName = Path.GetFileNameWithoutExtension(modelPath);
            foreach (AnimationClip clip in Clips(modelPath))
                paths.Add(folder + "/" + ClipFileName(modelName, clip.name, used));
            return paths;
        }

        /// <summary>The model's clips already extracted to the folder.</summary>
        internal static List<AnimationClip> ExtractedClips(string modelPath, string folder)
        {
            var clips = new List<AnimationClip>();
            foreach (string path in PlannedPaths(modelPath, folder))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip != null) clips.Add(clip);
            }
            return clips;
        }

        internal const string UndoName = "Extract Animation";

        private static Extraction ExtractClips(string modelPath, AnimationExtractSettings settings, Func<string, bool> mayReplace,
            bool undoable)
        {
            var result = new Extraction();
            List<string> skipped = result.Skipped;
            string folder = Destination(modelPath, settings);
            result.Folder = folder;
            List<AnimationClip> clips = Clips(modelPath);
            if (clips.Count == 0) return result;

            if (settings != null && settings.Destination.Length > 0 && DestinationProblem(settings.Destination) != null)
                RiryLog.Warn(RiryLog.ImportPresets, settings.Destination + " is not a folder in Assets; extracting " +
                    modelPath + " to " + folder + ".");
            EnsureFolder(folder);

            string modelName = Path.GetFileNameWithoutExtension(modelPath);
            string modelGuid = AssetDatabase.AssetPathToGUID(modelPath);
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var written = new List<string>();
            // One Undo step: replacing a clip (and cleaning it) undoes back to the clip as it was.
            // New clip files are not removed by Undo.
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            try
            {
                foreach (AnimationClip clip in clips)
                {
                    string path = folder + "/" + ClipFileName(modelName, clip.name, used);
                    // A clip extracted from another model of the same name is never replaced, even when asked.
                    if (ImportPresetOwnedFiles.OwnedByOther(path, modelGuid))
                    {
                        skipped.Add(path + " (extracted from " + ImportPresetOwnedFiles.OwnerPath(path) + "; set another Destination)");
                        continue;
                    }
                    var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    if ((existing != null || File.Exists(path)) && !mayReplace(path))
                    {
                        skipped.Add(path);
                        continue;
                    }
                    AnimationClip copy = UnityEngine.Object.Instantiate(clip);
                    copy.name = Path.GetFileNameWithoutExtension(path);
                    if (existing != null)
                    {
                        // Copy into the existing asset so its GUID and references stay. Recorded first,
                        // so Ctrl+Z puts the replaced clip back instead of losing it.
                        Undo.RegisterCompleteObjectUndo(existing, UndoName);
                        EditorUtility.CopySerialized(copy, existing);
                        UnityEngine.Object.DestroyImmediate(copy);
                        EditorUtility.SetDirty(existing);
                    }
                    else
                    {
                        AssetDatabase.CreateAsset(copy, path);
                    }
                    written.Add(path);
                }
                AssetDatabase.SaveAssets();
                if (settings != null && settings.AutoClean && written.Count > 0) Clean(written, settings, result);
            }
            finally
            {
                Undo.SetCurrentGroupName(UndoName);
                Undo.CollapseUndoOperations(undoGroup);
                // An automatic run leaves nothing for Ctrl+Z: its records (the replaced clips and the clean) are cleared.
                if (!undoable)
                {
                    foreach (string path in written)
                    {
                        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                        if (clip != null) Undo.ClearUndo(clip);
                    }
                }
            }
            // Recorded after cleaning, so a cleaned clip still counts as unchanged.
            foreach (string path in written) ImportPresetOwnedFiles.Record(path, modelGuid);
            result.Written = written.Count;
            return result;
        }

        // Cleans the clips; the summary goes into the model's line. A failed clip is logged by CleanAnimationKeys itself.
        private static void Clean(List<string> paths, AnimationExtractSettings settings, Extraction result)
        {
            var clips = new List<AnimationClip>();
            foreach (string path in paths)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip != null) clips.Add(clip);
            }
            try
            {
                result.Cleaned = CleanAnimationKeys.Clean(clips, settings.CleanOptions, settings.Whitelist);
                AssetDatabase.SaveAssets();
            }
            catch (InvalidOperationException exception)
            {
                result.Cleaned = "cleaning keys failed: " + exception.Message;
                result.CleanFailed = true;
            }
        }

        /// <summary>"Model_Clip.anim" with unsafe characters replaced, unique within one extraction.</summary>
        internal static string ClipFileName(string modelName, string clipName, HashSet<string> used)
        {
            string stem = Sanitize(modelName) + "_" + Sanitize(clipName);
            string name = stem + ".anim";
            for (int i = 2; !used.Add(name); i++) name = stem + "_" + i + ".anim";
            return name;
        }

        private static string Sanitize(string text)
        {
            var builder = new StringBuilder(text.Length);
            foreach (char character in text)
            {
                bool invalid = Array.IndexOf(Path.GetInvalidFileNameChars(), character) >= 0 ||
                    "/\\:*?\"<>|".IndexOf(character) >= 0 || char.IsControl(character);
                builder.Append(invalid ? '_' : character);
            }
            string result = builder.ToString().Trim().TrimEnd('.');
            return result.Length > 0 ? result : "Clip";
        }

        private static void ReportSkipped(string modelPath, List<string> skipped)
        {
            if (skipped.Count == 0) return;
            RiryLog.Warn(RiryLog.ImportPresets, "Kept " + skipped.Count + " clip(s) from " + modelPath +
                " that were edited, not created by this tool, or extracted from another model:\n" + string.Join("\n", skipped));
        }

        /// <summary>
        /// Extract in the Extract Animation window: asks once about clips the user
        /// edited or made, then extracts the model with the window's settings.
        /// </summary>
        internal static void Extract(ModelImporter importer, AnimationExtractSettings settings)
        {
            string folder = Destination(importer.assetPath, settings);
            int blocked = 0;
            string modelGuid = AssetDatabase.AssetPathToGUID(importer.assetPath);
            foreach (string path in PlannedPaths(importer.assetPath, folder))
            {
                // Another model's clips are kept whatever is chosen, so they are not asked about.
                if (File.Exists(path) && !ImportPresetOwnedFiles.IsUnchanged(path) && !ImportPresetOwnedFiles.OwnedByOther(path, modelGuid))
                    blocked++;
            }
            bool replaceAll = blocked > 0 && EditorUtility.DisplayDialog("Extract Animation",
                blocked + " clip(s) in " + folder + " were edited or not created by this tool. Replace them too?",
                "Replace", "Keep");
            Extraction result = ExtractPreservingSettings(importer, settings,
                path => replaceAll || ImportPresetOwnedFiles.IsUnchanged(path), true);
            ReportSkipped(importer.assetPath, result.Skipped);
            result.Log(importer.assetPath, "extracted " + result.Written + " clip(s) from " + importer.assetPath + " to " + result.Folder);
        }

        /// <summary>The model selected in the Project window, or null.</summary>
        internal static string SelectedModel()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            return !string.IsNullOrEmpty(path) && AssetImporter.GetAtPath(path) is ModelImporter ? path : null;
        }
    }
}
