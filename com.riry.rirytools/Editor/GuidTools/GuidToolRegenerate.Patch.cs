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
/// GUID regeneration: rewriting the GUIDs in place, renaming GUID-named files and checking the result after Unity refreshes.
/// </summary>
internal static partial class GuidToolRegenerate
{
    /// <summary>
    /// After the change: files under UserSettings/RiryTools named by an old GUID (Blendshape Sync's
    /// per-scene jobs) are renamed to the new one. A failure only warns: the references are already done.
    /// </summary>
    private static void RenameGuidNamedFiles(List<Target> targets, string projectRoot, GuidToolLog log)
    {
        string folder = Path.Combine(projectRoot, "UserSettings", "RiryTools");
        if (!Directory.Exists(folder)) return;
        var byOld = new Dictionary<string, Target>(StringComparer.OrdinalIgnoreCase);
        foreach (Target target in targets) byOld[target.OldGuid] = target;
        foreach (string file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
        {
            Target target;
            if (!byOld.TryGetValue(Path.GetFileNameWithoutExtension(file), out target)) continue;
            string renamed = Path.Combine(Path.GetDirectoryName(file), target.NewGuid + Path.GetExtension(file));
            try
            {
                if (File.Exists(renamed))
                {
                    log.Write("Not renamed, the new name is taken: " + file);
                    RiryLog.Warn(RiryLog.GuidTools, MakeProjectRelative(projectRoot, file) + " was not renamed to " +
                        Path.GetFileName(renamed) + " because that file exists; both were kept.");
                    continue;
                }
                File.Move(file, renamed);
                log.Write("Renamed GUID-named file: " + file + " -> " + renamed);
            }
            catch (Exception exception)
            {
                log.Write("Rename failure: " + file + " | " + exception);
                RiryLog.Warn(RiryLog.GuidTools, "Could not rename " + MakeProjectRelative(projectRoot, file) + " to " +
                    Path.GetFileName(renamed) + ":\n" + exception.Message);
            }
        }
    }

    internal static List<FilePatch> BuildPatches(
        List<Target> targets,
        List<FileState> files,
        GuidToolLog log)
    {
        var fileLookup = new Dictionary<string, FileState>(StringComparer.OrdinalIgnoreCase);
        foreach (FileState file in files)
            fileLookup[file.RelativePath] = file;

        var patchLookup = new Dictionary<string, FilePatch>(StringComparer.OrdinalIgnoreCase);

        foreach (Target target in targets)
        {
            if (!target.Occurrences.ContainsKey(target.MetaRelativePath))
                throw new InvalidOperationException(
                    "The selected asset's meta file did not contain its current GUID:\n" +
                    target.MetaRelativePath);

            foreach (KeyValuePair<string, List<long>> occurrenceSet in target.Occurrences)
            {
                FileState file;
                if (!fileLookup.TryGetValue(occurrenceSet.Key, out file))
                    continue;

                FilePatch patch;
                if (!patchLookup.TryGetValue(occurrenceSet.Key, out patch))
                {
                    patch = new FilePatch { File = file };
                    patchLookup.Add(occurrenceSet.Key, patch);
                }

                foreach (long offset in occurrenceSet.Value)
                    patch.Operations.Add(new PatchOperation { Target = target, Offset = offset });
            }
        }

        var result = new List<FilePatch>(patchLookup.Values);
        foreach (FilePatch patch in result)
            patch.Operations.Sort((left, right) => left.Offset.CompareTo(right.Offset));

        result.Sort((left, right) =>
        {
            bool leftIsTargetMeta = IsTargetMeta(left.File.RelativePath, targets);
            bool rightIsTargetMeta = IsTargetMeta(right.File.RelativePath, targets);
            if (leftIsTargetMeta != rightIsTargetMeta)
                return leftIsTargetMeta ? 1 : -1;
            return StringComparer.OrdinalIgnoreCase.Compare(
                left.File.RelativePath,
                right.File.RelativePath);
        });

        foreach (FilePatch patch in result)
            log.Write("Patch planned: " + patch.File.RelativePath +
                      " | GUID occurrences: " + patch.Operations.Count);

        return result;
    }

    internal static void ApplyPatches(
        List<FilePatch> patches,
        List<WrittenOperation> written,
        GuidToolLog log)
    {
        var check = new byte[GuidLength];

        AssetDatabase.StartAssetEditing();
        try
        {
            for (int patchIndex = 0; patchIndex < patches.Count; patchIndex++)
            {
                FilePatch patch = patches[patchIndex];
                float progress = patches.Count == 0
                    ? 0.65f
                    : 0.62f + 0.22f * patchIndex / patches.Count;

                EditorUtility.DisplayProgressBar(
                    "Regenerating GUIDs",
                    "Updating " + patch.File.RelativePath,
                    progress);

                log.Write("Patching file: " + patch.File.RelativePath +
                          " | operations: " + patch.Operations.Count);

                using (var stream = new FileStream(
                           patch.File.FullPath,
                           FileMode.Open,
                           FileAccess.ReadWrite,
                           FileShare.Read))
                {
                    foreach (PatchOperation operation in patch.Operations)
                    {
                        stream.Position = operation.Offset;
                        int read = stream.Read(check, 0, check.Length);
                        if (read != GuidLength ||
                            !GuidToolBytes.Matches(check, 0, operation.Target.OldBytes))
                            throw new IOException(
                                "A file changed after it was scanned:\n" + patch.File.RelativePath);
                    }

                    foreach (PatchOperation operation in patch.Operations)
                    {
                        var writtenOperation = new WrittenOperation
                        {
                            FullPath = patch.File.FullPath,
                            Offset = operation.Offset,
                            OldBytes = operation.Target.OldBytes,
                            NewBytes = operation.Target.NewBytes
                        };

                        // Register the attempt before writing so rollback also
                        // sees a write that fails partway through the buffer.
                        written.Add(writtenOperation);
                        stream.Position = operation.Offset;
                        stream.Write(operation.Target.NewBytes, 0, GuidLength);
                    }

                    stream.Flush();
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

    }

    private static void VerifyNewGuids(List<Target> targets, GuidToolLog log)
    {
        foreach (Target target in targets)
        {
            string actual = AssetDatabase.AssetPathToGUID(target.AssetPath);
            if (!actual.Equals(target.NewGuid, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Unity did not register the new GUID for:\n" + target.AssetPath);

            log.Write("AssetDatabase verification passed: " + target.AssetPath +
                      " | " + actual);
        }
    }

    private static Dictionary<string, FileState> CapturePatchedFileStates(List<FilePatch> patches)
    {
        var result = new Dictionary<string, FileState>(StringComparer.OrdinalIgnoreCase);
        foreach (FilePatch patch in patches)
        {
            var info = new FileInfo(patch.File.FullPath);
            result[patch.File.RelativePath] = new FileState
            {
                FullPath = info.FullName,
                RelativePath = patch.File.RelativePath,
                Length = info.Length,
                LastWriteUtcTicks = info.LastWriteTimeUtc.Ticks
            };
        }
        return result;
    }

    private static void ReconcileAfterRefresh(
        List<Target> targets,
        List<FileState> preScanFiles,
        Dictionary<string, FileState> postWriteStates,
        string projectRoot,
        byte[] scanBuffer,
        GuidToolLog log)
    {
        var preLookup = new Dictionary<string, FileState>(StringComparer.OrdinalIgnoreCase);
        foreach (FileState file in preScanFiles)
            preLookup[file.RelativePath] = file;

        List<FileState> currentFiles = EnumerateProjectTextFiles(projectRoot);
        var currentPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < currentFiles.Count; index++)
        {
            FileState current = currentFiles[index];
            currentPaths.Add(current.RelativePath);

            FileState baseline;
            if (!postWriteStates.TryGetValue(current.RelativePath, out baseline))
                preLookup.TryGetValue(current.RelativePath, out baseline);

            bool changed = baseline == null ||
                           baseline.Length != current.Length ||
                           baseline.LastWriteUtcTicks != current.LastWriteUtcTicks;

            if (!changed)
                continue;

            log.Write("Post-refresh rescan required: " + current.RelativePath);

            if (!LooksLikeText(current.FullPath, scanBuffer))
            {
                ThrowIfBinaryDependency(
                    current,
                    targets,
                    "Unity produced or modified a binary asset that references a changed GUID:\n",
                    log);
                continue;
            }

            ScanOneFile(current, targets, true, scanBuffer, log);
        }

        foreach (Target target in targets)
        {
            var removed = new List<string>();
            foreach (string path in target.Occurrences.Keys)
            {
                if (!currentPaths.Contains(path))
                    removed.Add(path);
            }
            foreach (string path in removed)
            {
                target.Occurrences.Remove(path);
                log.Write("Removed deleted reference from index: " + target.AssetPath +
                          " <- " + path);
            }
        }
    }

    internal static void EnsureWritable(string fullPath)
    {
        if ((File.GetAttributes(fullPath) & FileAttributes.ReadOnly) != 0)
            throw new UnauthorizedAccessException("A file is read-only:\n" + fullPath);

        using (new FileStream(fullPath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
        {
        }
    }

    internal static void ReplaceFileAtomically(string tempPath, string finalPath)
    {
        if (!File.Exists(finalPath))
        {
            File.Move(tempPath, finalPath);
            return;
        }

        try
        {
            File.Replace(tempPath, finalPath, null);
        }
        catch (PlatformNotSupportedException)
        {
            File.Delete(finalPath);
            File.Move(tempPath, finalPath);
        }
        catch (IOException)
        {
            File.Delete(finalPath);
            File.Move(tempPath, finalPath);
        }
    }
}
}

// ============================================================================
// GLOSSARY: GUID regeneration: patch
// GuidToolRegenerate is one partial class: Editor/GuidTools/GuidToolRegenerate(.Scan/.Patch/.Rollback).cs and Editor/UI/GuidTools/GuidToolRegenerate.Prompts.cs.
// ============================================================================
//
// GuidToolRegenerate (this part)
//   BuildPatches(...) / ApplyPatches(...)  The writes per file; applied in place, each recorded for RollBack.
//   VerifyNewGuids / CapturePatchedFileStates / ReconcileAfterRefresh  Checks the result after Unity refreshes.
//   RenameGuidNamedFiles(...)       UserSettings/RiryTools files named by an old GUID are renamed afterwards.
//   EnsureWritable / ReplaceFileAtomically  File helpers (ReplaceFileAtomically is also used by GuidToolReferenceCache.Save).
