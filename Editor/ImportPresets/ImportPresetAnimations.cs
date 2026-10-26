#if UNITY_EDITOR
// Target editor: Unity 2022.3 LTS (including 2022.3.22f1).
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

namespace Riry.UnityWorkflow.Editor
{
    /// <summary>
    /// Copies a model's clips into Animations/ next to it while the model is
    /// imported as Generic, so the copies keep plain bone curves instead of
    /// Humanoid muscle curves. The model's real preset is applied afterwards.
    /// </summary>
    internal static class ImportPresetAnimations
    {
        internal const string FolderName = "Animations";
        private const string MenuPath = "Assets/Rirytools/Extract Animations (Generic)";

        /// <summary>Tests point pending markers at a temporary folder.</summary>
        internal static string PendingFolderOverride;

        private static string PendingFolder
        {
            get
            {
                return !string.IsNullOrEmpty(PendingFolderOverride)
                    ? PendingFolderOverride
                    : Path.Combine("Library", "RiryImportPresets", "Pending");
            }
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
            List<string> skipped;
            int written = ExtractClips(assetPath, ImportPresetOwnedFiles.IsUnchanged, out skipped);
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
            Debug.Log("Import presets: extracted " + written + " clip(s) from " + assetPath + " to " +
                AnimationsFolder(assetPath) + (applied != null ? ", then applied " + applied + "." : "."));
        }

        /// <summary>
        /// Extracts clips from a model whose settings may already be configured:
        /// a snapshot of its settings is restored after a temporary Generic import.
        /// </summary>
        internal static int ExtractPreservingSettings(ModelImporter importer, Func<string, bool> mayReplace,
            out List<string> skipped)
        {
            Preset snapshot = new Preset(importer);
            bool changed = false;
            try
            {
                changed = EnsureGeneric(importer);
                return ExtractClips(importer.assetPath, mayReplace, out skipped);
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

        /// <summary>Paths the model's clips extract to. Clip names do not depend on the rig type.</summary>
        private static List<string> PlannedPaths(string modelPath)
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var paths = new List<string>();
            string modelName = Path.GetFileNameWithoutExtension(modelPath);
            foreach (AnimationClip clip in Clips(modelPath))
                paths.Add(AnimationsFolder(modelPath) + "/" + ClipFileName(modelName, clip.name, used));
            return paths;
        }

        private static int ExtractClips(string modelPath, Func<string, bool> mayReplace, out List<string> skipped)
        {
            skipped = new List<string>();
            List<AnimationClip> clips = Clips(modelPath);
            if (clips.Count == 0) return 0;

            string folder = AnimationsFolder(modelPath);
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(ImportPresetMarker.ParentFolder(modelPath), FolderName);

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
            foreach (string path in written) ImportPresetOwnedFiles.Record(path);
            return written.Count;
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
            Debug.LogWarning("Import presets: kept " + skipped.Count + " clip(s) from " + modelPath +
                " that were edited or not created by this tool:\n" + string.Join("\n", skipped));
        }

        [MenuItem(MenuPath, false, 2003)]
        private static void ExtractSelected()
        {
            foreach (ModelImporter importer in SelectedModels())
            {
                // Ask once, before importing, about clips the user edited or made.
                int blocked = 0;
                foreach (string path in PlannedPaths(importer.assetPath))
                {
                    if (File.Exists(path) && !ImportPresetOwnedFiles.IsUnchanged(path)) blocked++;
                }
                bool replaceAll = blocked > 0 && EditorUtility.DisplayDialog("Extract Animations",
                    blocked + " clip(s) in " + AnimationsFolder(importer.assetPath) +
                    " were edited or not created by this tool. Replace them too?", "Replace", "Keep");
                List<string> skipped;
                int written = ExtractPreservingSettings(importer,
                    path => replaceAll || ImportPresetOwnedFiles.IsUnchanged(path), out skipped);
                ReportSkipped(importer.assetPath, skipped);
                Debug.Log("Import presets: extracted " + written + " clip(s) from " + importer.assetPath + ".");
            }
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateExtractSelected()
        {
            return SelectedModels().Count > 0;
        }

        private static List<ModelImporter> SelectedModels()
        {
            var importers = new List<ModelImporter>();
            foreach (string guid in Selection.assetGUIDs)
            {
                var importer = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as ModelImporter;
                if (importer != null) importers.Add(importer);
            }
            return importers;
        }
    }
}
#endif
