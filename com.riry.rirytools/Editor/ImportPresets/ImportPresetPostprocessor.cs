using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Applies a folder's import presets to files imported for the first time;
    /// reimports never change settings, so manual changes stay. Animation
    /// extraction and Better FBX exports run after the import finishes.
    /// </summary>
    internal sealed class ImportPresetPostprocessor : AssetPostprocessor
    {
        private static readonly List<string> extractQueue = new List<string>();
        private static readonly List<string> exportQueue = new List<string>();
        private static bool scheduled;

        private void OnPreprocessAsset()
        {
            // Only a first import (no .meta settings yet) is configured. Files
            // brought in with their .meta, such as from a .unitypackage, keep theirs.
            if (!assetImporter.importSettingsMissing) return;
            string path = assetPath;
            if (string.IsNullOrEmpty(path) || Directory.Exists(path) || IsMarker(path)) return;

            ImportPresetMarker.Rules rules = ImportPresetMarker.RulesFor(path);
            if (rules == null) return;
            string extension = ImportPresetLibrary.NormalizeExtension(Path.GetExtension(path));
            // A Better FBX source is not configured itself; its exported FBX is.
            if (rules.BetterFbx.Contains(extension)) return;
            ImportPresetMarker.Target target = rules.For(extension);

            if (assetImporter is ModelImporter && rules.ExtractAnimations.Contains(extension))
            {
                // Import with default (Generic) settings first; the preset is
                // applied after the clips are copied out.
                ImportPresetAnimations.MarkPending(path);
                return;
            }
            if (target.Configuration == null) return;
            string applied;
            ImportPresetLibrary.TryApply(assetImporter, target.Configuration, target.Preset, out applied);
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved,
            string[] movedFrom)
        {
            foreach (string path in imported)
            {
                if (ImportPresetAnimations.IsPending(path))
                {
                    Enqueue(extractQueue, path);
                    continue;
                }
                ImportPresetMarker.Rules rules = ExportRules(path);
                if (rules != null) Enqueue(exportQueue, path);
            }
        }

        private static bool IsMarker(string path)
        {
            return string.Equals(Path.GetFileName(path), ImportPresetMarker.FileName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The governing rules when this file is a Better FBX source, else null.</summary>
        internal static ImportPresetMarker.Rules ExportRules(string path)
        {
            if (Directory.Exists(path) || IsMarker(path)) return null;
            // Only Blender files can be opened by Blender; any other listed type
            // (an .fbx would even overwrite itself) is ignored.
            string extension = ImportPresetLibrary.NormalizeExtension(Path.GetExtension(path));
            if (extension != BetterFbxExporter.SourceExtension) return null;
            string folder = ImportPresetMarker.FindMarkerFolder(path, File.Exists);
            if (folder == null) return null;
            ImportPresetMarker.Rules rules = ImportPresetMarker.ReadRules(folder);
            return rules != null && rules.BetterFbx.Contains(extension) ? rules : null;
        }

        [InitializeOnLoadMethod]
        private static void ResumeAfterReload()
        {
            // Pending extractions are stored on disk and survive a domain reload.
            foreach (string path in ImportPresetAnimations.PendingPaths()) Enqueue(extractQueue, path);
        }

        private static void Enqueue(List<string> queue, string path)
        {
            if (!queue.Contains(path)) queue.Add(path);
            if (scheduled) return;
            scheduled = true;
            EditorApplication.delayCall += ProcessQueues;
        }

        private static void ProcessQueues()
        {
            scheduled = false;
            // Never run Blender or reimport during Play Mode or another import.
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                scheduled = true;
                EditorApplication.delayCall += ProcessQueues;
                return;
            }

            while (exportQueue.Count > 0)
            {
                string source = exportQueue[0];
                exportQueue.RemoveAt(0);
                ExportOne(source, false);
            }
            while (extractQueue.Count > 0)
            {
                string model = extractQueue[0];
                extractQueue.RemoveAt(0);
                try
                {
                    ImportPresetAnimations.ProcessFirstImport(model);
                }
                catch (Exception exception)
                {
                    Debug.LogError("Import presets: animation extraction failed for " + model + ":\n" + exception);
                }
            }
        }

        /// <summary>Exports one Better FBX source and re-extracts clips from an FBX that already existed.</summary>
        internal static void ExportOne(string source, bool force)
        {
            if (!File.Exists(source)) return;
            try
            {
                bool existed = File.Exists(BetterFbxExporter.FbxPathFor(source));
                string message;
                string fbx = BetterFbxExporter.Export(source, force, out message);
                if (fbx == null)
                {
                    if (message != null && !message.EndsWith("is up to date.", StringComparison.Ordinal))
                        Debug.LogWarning("Import presets: " + message);
                    return;
                }
                Debug.Log("Import presets: " + message);

                // A new FBX is handled by its first import. An updated one keeps
                // its settings, so its clips are refreshed here when configured.
                ImportPresetMarker.Rules rules = ImportPresetMarker.RulesFor(fbx);
                var importer = AssetImporter.GetAtPath(fbx) as ModelImporter;
                if (!existed || importer == null || rules == null || !rules.ExtractAnimations.Contains("fbx")) return;
                List<string> skipped;
                int written = ImportPresetAnimations.ExtractPreservingSettings(importer,
                    ImportPresetOwnedFiles.IsUnchanged, out skipped);
                Debug.Log("Import presets: refreshed " + written + " clip(s) from " + fbx +
                    (skipped.Count > 0 ? "; kept " + skipped.Count + " edited clip(s)." : "."));
            }
            catch (Exception exception)
            {
                Debug.LogError("Import presets: Better FBX export failed for " + source + ":\n" + exception);
            }
        }
    }
}
