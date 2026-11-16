using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Riry.RiryTools.Editor
{
/// <summary>
/// GUID regeneration as the user runs it: the confirmations, the progress bar and the result dialogs around
/// the scan (GuidToolRegenerate.Scan.cs), patch (GuidToolRegenerate.Patch.cs) and rollback (GuidToolRegenerate.Rollback.cs)
/// in Editor/GuidTools/.
/// </summary>
internal static partial class GuidToolRegenerate
{
    internal static void RegenerateSelectedGuids()
    {
        GuidToolLog log = GuidToolLog.Open("Regenerate GUID");
        List<Target> targets = null;
        List<FileState> files = null;
        List<FilePatch> patches = null;
        List<WrittenOperation> written = null;
        byte[] scanBuffer = null;
        bool reloadLocked = false;
        bool committed = false;
        SceneSetup[] sceneSetup = null;
        long scanStartedUtcTicks = DateTime.UtcNow.Ticks;

        try
        {
            log.Write("Unity version: " + Application.unityVersion);
            log.Write("Serialization mode: " + EditorSettings.serializationMode);

            if (EditorSettings.serializationMode != SerializationMode.ForceText)
            {
                log.Write("Stopped: Force Text serialization is not enabled.");
                EditorUtility.DisplayDialog(
                    "Force Text Required",
                    "Set Edit > Project Settings > Editor > Asset Serialization to Force Text first.",
                    "OK");
                return;
            }

            string projectRoot = RiryToolsPaths.ProjectRoot;
            List<string> assetPaths = CollectSelectedAssetPaths();

            log.Write("Selected target count after folder expansion: " + assetPaths.Count);
            foreach (string selectedPath in assetPaths)
                log.Write("Target candidate: " + selectedPath);

            if (assetPaths.Count == 0)
            {
                log.Write("Stopped: no valid assets were selected.");
                EditorUtility.DisplayDialog("Regenerate GUID", "Select one or more assets, or one or more folders under Assets.", "OK");
                return;
            }

            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                log.Write("Stopped: Prefab Mode is open.");
                EditorUtility.DisplayDialog(
                    "Close Prefab Mode",
                    "Save and close Prefab Mode before regenerating GUIDs. Objects open in " +
                    "Prefab Mode keep the old references in memory.",
                    "OK");
                return;
            }

            targets = BuildTargets(assetPaths, projectRoot, log);
            GuidToolReferenceCache.Load(targets, log);

            string confirmation = targets.Count == 1
                ? "Regenerate the GUID for this asset and update all serialized references?\n\n" +
                  targets[0].AssetPath
                : "Regenerate GUIDs for " + targets.Count.ToString("N0", CultureInfo.InvariantCulture) +
                  " assets and update all serialized references?\n\n" +
                  "Selected folders are expanded recursively; folder GUIDs are not changed.";

            confirmation += "\n\nThis bypasses Unity Undo. Commit or back up the project first.";

            if (!EditorUtility.DisplayDialog("Regenerate GUID", confirmation, "Regenerate", "Cancel"))
            {
                log.Write("Cancelled at the confirmation dialog.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                log.Write("Cancelled while saving modified scenes.");
                return;
            }

            // "Don't Save" also returns true. Unsaved scenes would later be
            // saved over the patched files with stale in-memory references.
            if (!ScenesAreSaved())
            {
                log.Write("Stopped: open scenes have unsaved changes.");
                EditorUtility.DisplayDialog(
                    "Save Scenes First",
                    "Every open scene must be saved before regenerating GUIDs.",
                    "OK");
                return;
            }

            sceneSetup = EditorSceneManager.GetSceneManagerSetup();
            AssetDatabase.SaveAssets();

            files = EnumerateProjectTextFiles(projectRoot);
            log.Write("Eligible project text files: " + files.Count);
            scanBuffer = new byte[ScanBlockSize + GuidLength - 1];
            ScanReferences(targets, files, scanBuffer, log);
            patches = BuildPatches(targets, files, log);

            if (patches.Count == 0)
                throw new InvalidOperationException("No GUID occurrences were found. No files were changed.");

            if (!ConfirmUnusualFiles(patches, log))
                return;

            foreach (FilePatch patch in patches)
            {
                EnsureWritable(patch.File.FullPath);
                log.Write("Write access verified: " + patch.File.RelativePath);
            }

            EditorApplication.LockReloadAssemblies();
            reloadLocked = true;

            written = new List<WrittenOperation>();
            ApplyPatches(patches, written, log);

            Dictionary<string, FileState> postWriteStates = CapturePatchedFileStates(patches);

            EditorUtility.DisplayProgressBar(
                "Regenerating GUIDs",
                "Refreshing the Asset Database...",
                0.88f);

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            VerifyNewGuids(targets, log);

            ReconcileAfterRefresh(
                targets,
                files,
                postWriteStates,
                projectRoot,
                scanBuffer,
                log);

            GuidToolReferenceCache.Save(targets, projectRoot, scanStartedUtcTicks, log);
            committed = true;

            // The files are already updated, so a ledger failure only warns.
            try
            {
                GuidToolRemapLedger.Append(targets);
                log.Write("Recorded " + targets.Count + " GUID change(s) in " + GuidToolRemapLedger.RelativePath);
            }
            catch (Exception exception)
            {
                log.Write("Ledger write failure: " + exception);
                RiryLog.Warn(RiryLog.GuidTools, "Could not record the GUID changes in " +
                    GuidToolRemapLedger.RelativePath + ":\n" + exception.Message);
            }

            // Before the scenes reload, so their jobs save under the new GUID.
            RenameGuidNamedFiles(targets, projectRoot, log);
            ReloadScenes(sceneSetup, log);

            int referenceFileCount = CountReferenceFiles(targets);
            RiryLog.Info(RiryLog.GuidTools,
                "GUID regeneration completed.\n" +
                "Assets changed: " + targets.Count.ToString("N0", CultureInfo.InvariantCulture) + "\n" +
                "Reference files indexed: " + referenceFileCount.ToString("N0", CultureInfo.InvariantCulture) + "\n" +
                "Old to new GUIDs recorded in: " + GuidToolRemapLedger.RelativePath + "\n" +
                "Diagnostic log: " + LogDirectoryPath + "\n" +
                "Cache: " + GuidToolReferenceCache.CacheDirectoryPath);
            log.Write("Completed successfully. Assets changed: " + targets.Count +
                      "; reference files indexed: " + referenceFileCount + ".");
        }
        catch (OperationCanceledException)
        {
            log.Write("Cancelled during the preflight scan; no files were changed.");
            RiryLog.Info(RiryLog.GuidTools, "GUID regeneration cancelled before any files were changed.");
        }
        catch (Exception exception)
        {
            if (!committed && written != null && written.Count > 0)
            {
                try
                {
                    log.Write("Attempting rollback of " + written.Count + " written GUID occurrence(s).");
                    RollBack(written, log);
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                    ReloadScenes(sceneSetup, log);
                }
                catch (Exception rollbackException)
                {
                    log.Write("ROLLBACK FAILURE: " + rollbackException);
                    RiryLog.Exception(RiryLog.GuidTools, "GUID rollback was incomplete", rollbackException);
                }
            }

            log.Write("FAILURE: " + exception);
            RiryLog.Exception(RiryLog.GuidTools, "GUID regeneration failed", exception);
            EditorUtility.DisplayDialog(
                "GUID Regeneration Failed",
                exception.Message + "\n\nCheck the Console for details.",
                "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();

            if (reloadLocked)
                EditorApplication.UnlockReloadAssemblies();

            ClearTemporaryData(targets, files, patches, written);
            scanBuffer = null;
            log.Write("Temporary processing data released.");
            log.Dispose();
        }
    }

    /// <summary>
    /// Before anything is written: lists the files whose GUID text would change that are not Unity
    /// references (scripts, JSON, text and other non-serialized files) or that are under Packages/,
    /// where the package manager or VCC may put the original back. False when the user cancels.
    /// RiryTools' own files under UserSettings/RiryTools are expected and not listed.
    /// </summary>
    private static bool ConfirmUnusualFiles(List<FilePatch> patches, GuidToolLog log)
    {
        var unusual = new List<string>();
        foreach (FilePatch patch in patches)
        {
            string path = patch.File.RelativePath.Replace('\\', '/');
            if (path.StartsWith("UserSettings/RiryTools/", StringComparison.OrdinalIgnoreCase)) continue;
            string extension = Path.GetExtension(path);
            bool serialized = extension.Equals(".meta", StringComparison.OrdinalIgnoreCase) || UnitySerializedExtensions.Contains(extension);
            if (!serialized || path.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase)) unusual.Add(path);
        }
        if (unusual.Count == 0) return true;
        const int Shown = 15;
        string list = string.Join("\n", unusual.GetRange(0, Math.Min(Shown, unusual.Count))) +
            (unusual.Count > Shown ? "\n... and " + (unusual.Count - Shown) + " more (listed in the GUID Tools log)" : "");
        foreach (string path in unusual) log.Write("Unusual file to patch: " + path);
        bool proceed = EditorUtility.DisplayDialog("Regenerate GUID",
            "The GUID text also appears in these files, which are not Unity references or are inside Packages/:\n\n" + list +
            "\n\nThey will be rewritten too. Files under Packages/ may be put back by the package manager or VCC, which " +
            "would break those references again.", "Rewrite Them Too", "Cancel");
        if (!proceed) log.Write("Cancelled at the unusual files dialog; no files were changed.");
        return proceed;
    }
}
}

// ============================================================================
// GLOSSARY: GUID regeneration: prompts and progress
// GuidToolRegenerate is one partial class: Editor/GuidTools/GuidToolRegenerate(.Scan/.Patch/.Rollback).cs and Editor/UI/GuidTools/GuidToolRegenerate.Prompts.cs.
// ============================================================================
//
// GuidToolRegenerate (this part)
//   RegenerateSelectedGuids()       The run: checks the selection and saved scenes, confirms, scans, lists unusual files
//                                   (ConfirmUnusualFiles: non-reference and Packages files) before writing, applies with a
//                                   progress bar, saves the caches, reloads scenes; rolls back and reports on failure.
//   ConfirmUnusualFiles(patches, log)  False when the user cancels.
