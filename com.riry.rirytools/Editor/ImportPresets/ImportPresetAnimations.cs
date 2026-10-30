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
            List<string> skipped;
            int written = ExtractClips(assetPath, settings, ImportPresetOwnedFiles.IsUnchanged, out skipped);
            ReportSkipped(assetPath, skipped);

            ImportPresetMarker.Rules rules = ImportPresetMarker.RulesFor(assetPath);
            string applied = null;
            if (rules != null)
            {
                ImportPresetMarker.Target target = rules.For(Path.GetExtension(assetPath));
                if (target.Configuration != null &&
                    ImportPresetLibrary.TryApply(importer, target.Configuration, target.Preset, out applied))
                    importer.SaveAndReimport();
            }
            Debug.Log("Import Presets: extracted " + written + " clip(s) from " + assetPath + " to " +
                Destination(assetPath, settings) + (applied != null ? ", then applied " + applied + "." : "."));
        }

        /// <summary>
        /// Extracts clips from a model whose settings may already be configured:
        /// a snapshot of its settings is restored after a temporary Generic import.
        /// </summary>
        internal static int ExtractPreservingSettings(ModelImporter importer, AnimationExtractSettings settings,
            Func<string, bool> mayReplace, out List<string> skipped)
        {
            Preset snapshot = new Preset(importer);
            bool changed = false;
            try
            {
                changed = EnsureGeneric(importer);
                return ExtractClips(importer.assetPath, settings, mayReplace, out skipped);
            }
            finally
            {
                if (changed && snapshot.ApplyTo(importer)) importer.SaveAndReimport();
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

        /// <summary>
        /// Whether clips are extracted before the model's preset is applied: the
        /// folder's rule for its file type, or the model's Automatically extract animations.
        /// </summary>
        internal static bool ShouldExtract(string modelPath, ImportPresetMarker.Rules rules)
        {
            string extension = ImportPresetLibrary.NormalizeExtension(Path.GetExtension(modelPath));
            if (rules != null && rules.ExtractAnimations.Contains(extension)) return true;
            AnimationExtractSettings settings = AnimationExtractSettings.Load(modelPath);
            return settings != null && settings.AutoExtract;
        }

        /// <summary>The settings' Destination when it is valid, else Animations/ next to the model.</summary>
        internal static string Destination(string modelPath, AnimationExtractSettings settings)
        {
            if (settings == null || settings.Destination.Length == 0) return AnimationsFolder(modelPath);
            if (DestinationProblem(settings.Destination) == null) return settings.Destination;
            Debug.LogWarning("Import Presets: " + settings.Destination + " is not a folder in Assets; extracting " +
                modelPath + " to " + AnimationsFolder(modelPath) + ".");
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

        private static int ExtractClips(string modelPath, AnimationExtractSettings settings, Func<string, bool> mayReplace,
            out List<string> skipped)
        {
            skipped = new List<string>();
            List<AnimationClip> clips = Clips(modelPath);
            if (clips.Count == 0) return 0;

            string folder = Destination(modelPath, settings);
            EnsureFolder(folder);

            string modelName = Path.GetFileNameWithoutExtension(modelPath);
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var written = new List<string>();
            foreach (AnimationClip clip in clips)
            {
                string path = folder + "/" + ClipFileName(modelName, clip.name, used);
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
                    // Copy into the existing asset so its GUID and references stay.
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
            if (settings != null && settings.AutoClean && written.Count > 0) Clean(written, settings);
            // Recorded after cleaning, so a cleaned clip still counts as unchanged.
            foreach (string path in written) ImportPresetOwnedFiles.Record(path);
            return written.Count;
        }

        private static void Clean(List<string> paths, AnimationExtractSettings settings)
        {
            var clips = new List<AnimationClip>();
            foreach (string path in paths)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip != null) clips.Add(clip);
            }
            try
            {
                Debug.Log("Import Presets: " + CleanAnimationKeys.Clean(clips, settings.CleanOptions, settings.Whitelist));
                AssetDatabase.SaveAssets();
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogWarning("Import Presets: cleaning keys failed: " + exception.Message);
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
            Debug.LogWarning("Import Presets: kept " + skipped.Count + " clip(s) from " + modelPath +
                " that were edited or not created by this tool:\n" + string.Join("\n", skipped));
        }

        /// <summary>
        /// Extract in the Extract Animation window: asks once about clips the user
        /// edited or made, then extracts the model with the window's settings.
        /// </summary>
        internal static void Extract(ModelImporter importer, AnimationExtractSettings settings)
        {
            string folder = Destination(importer.assetPath, settings);
            int blocked = 0;
            foreach (string path in PlannedPaths(importer.assetPath, folder))
            {
                if (File.Exists(path) && !ImportPresetOwnedFiles.IsUnchanged(path)) blocked++;
            }
            bool replaceAll = blocked > 0 && EditorUtility.DisplayDialog("Extract Animation",
                blocked + " clip(s) in " + folder + " were edited or not created by this tool. Replace them too?",
                "Replace", "Keep");
            List<string> skipped;
            int written = ExtractPreservingSettings(importer, settings,
                path => replaceAll || ImportPresetOwnedFiles.IsUnchanged(path), out skipped);
            ReportSkipped(importer.assetPath, skipped);
            Debug.Log("Import Presets: extracted " + written + " clip(s) from " + importer.assetPath + " to " + folder + ".");
        }

        /// <summary>The model selected in the Project window, or null.</summary>
        internal static string SelectedModel()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            return !string.IsNullOrEmpty(path) && AssetImporter.GetAtPath(path) is ModelImporter ? path : null;
        }
    }
}
