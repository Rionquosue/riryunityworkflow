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
/// GUID regeneration: finding every text file that references the selected GUIDs.
/// </summary>
internal static partial class GuidToolRegenerate
{
    private static List<FileState> EnumerateProjectTextFiles(string projectRoot)
    {
        string[] roots =
        {
            Path.Combine(projectRoot, "Assets"),
            Path.Combine(projectRoot, "ProjectSettings"),
            Path.Combine(projectRoot, "Packages"),
            // RiryTools' own per-user files (Blendshape Sync jobs hold scene GUIDs in their object IDs).
            Path.Combine(projectRoot, "UserSettings", "RiryTools")
        };

        var files = new List<FileState>();
        var pending = new Stack<KeyValuePair<string, int>>();

        foreach (string root in roots)
        {
            if (Directory.Exists(root))
                pending.Push(new KeyValuePair<string, int>(root, 0));
        }

        // Walk manually: a depth limit stops a symbolic link loop that
        // SearchOption.AllDirectories would follow forever. Linked folders are
        // otherwise scanned like real ones (OneDrive files also look like links).
        while (pending.Count > 0)
        {
            KeyValuePair<string, int> entry = pending.Pop();
            string directory = entry.Key;
            if (entry.Value > MaxDirectoryDepth)
                throw new InvalidOperationException(
                    "Folders are nested too deeply, possibly through a symbolic link loop:\n" +
                    MakeProjectRelative(projectRoot, directory));

            foreach (string child in Directory.GetDirectories(directory))
                pending.Push(new KeyValuePair<string, int>(child, entry.Value + 1));

            foreach (string fullPath in Directory.GetFiles(directory))
            {
                if (!TextExtensions.Contains(Path.GetExtension(fullPath)))
                    continue;

                var info = new FileInfo(fullPath);
                files.Add(new FileState
                {
                    FullPath = info.FullName,
                    RelativePath = MakeProjectRelative(projectRoot, info.FullName),
                    Length = info.Length,
                    LastWriteUtcTicks = info.LastWriteTimeUtc.Ticks
                });
            }
        }

        files.Sort((left, right) =>
            StringComparer.OrdinalIgnoreCase.Compare(left.RelativePath, right.RelativePath));
        ThrowIfCaseCollision(files);
        return files;
    }

    /// <summary>
    /// Paths are compared without case so Windows and macOS behave the same.
    /// Two files differing only by case would share one entry, and the
    /// references in one of them would be lost, so they stop the run.
    /// </summary>
    internal static void ThrowIfCaseCollision(List<FileState> sortedFiles)
    {
        for (int i = 1; i < sortedFiles.Count; i++)
        {
            string previous = sortedFiles[i - 1].RelativePath;
            string current = sortedFiles[i].RelativePath;
            if (!previous.Equals(current, StringComparison.OrdinalIgnoreCase))
                continue;

            throw new InvalidOperationException(
                "Two project files differ only by letter case:\n" + previous + "\n" + current +
                "\n\nRename one of them before regenerating GUIDs.");
        }
    }

    internal static void ScanReferences(
        List<Target> targets,
        List<FileState> files,
        byte[] scanBuffer,
        GuidToolLog log)
    {
        int reusedReferences = 0;
        int scannedFiles = 0;

        for (int fileIndex = 0; fileIndex < files.Count; fileIndex++)
        {
            FileState file = files[fileIndex];
            var targetsToScan = new List<Target>();

            foreach (Target target in targets)
            {
                CachedReference cached = null;
                bool hasCachedReference =
                    target.Cache != null &&
                    target.Cache.References.TryGetValue(file.RelativePath, out cached);

                if (hasCachedReference &&
                    cached.Length == file.Length &&
                    cached.LastWriteUtcTicks == file.LastWriteUtcTicks &&
                    CachedOffsetsStillMatch(
                        file.FullPath,
                        cached.Offsets,
                        target.OldBytes,
                        scanBuffer))
                {
                    target.Occurrences[file.RelativePath] = new List<long>(cached.Offsets);
                    reusedReferences++;
                    continue;
                }

                // Every other file is scanned. Moves, copies and some VCS
                // checkouts keep old timestamps, so modification times cannot
                // prove that an uncached file has no reference.
                targetsToScan.Add(target);
            }

            if (targetsToScan.Count == 0)
                continue;

            if ((fileIndex & 31) == 0)
            {
                float progress = files.Count == 0 ? 0.1f : 0.05f + 0.55f * fileIndex / files.Count;
                if (EditorUtility.DisplayCancelableProgressBar(
                        "Regenerating GUIDs",
                        "Scanning " + file.RelativePath,
                        progress))
                    throw new OperationCanceledException();
            }

            if (!LooksLikeText(file.FullPath, scanBuffer))
            {
                ThrowIfBinaryDependency(
                    file,
                    targetsToScan,
                    "A binary-serialized Unity asset references a GUID being changed:\n",
                    log);
                continue;
            }

            ScanOneFile(file, targetsToScan, false, scanBuffer, log);
            scannedFiles++;
        }

        log.Write("Preflight scan finished. Files scanned: " + scannedFiles +
                  "; cached references reused: " + reusedReferences + ".");
    }

    private static bool CachedOffsetsStillMatch(
        string fullPath,
        List<long> offsets,
        byte[] token,
        byte[] checkBuffer)
    {
        try
        {
            using (var stream = new FileStream(
                       fullPath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.ReadWrite))
            {
                foreach (long offset in offsets)
                {
                    stream.Position = offset;
                    int read = stream.Read(checkBuffer, 0, token.Length);
                    if (read != token.Length || !GuidToolBytes.Matches(checkBuffer, 0, token))
                        return false;
                }
            }
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    internal static void ScanOneFile(
        FileState file,
        List<Target> targets,
        bool useNewGuid,
        byte[] buffer,
        GuidToolLog log)
    {
        foreach (Target target in targets)
            target.Occurrences.Remove(file.RelativePath);

        var byPrefix = new Dictionary<uint, List<Target>>();
        foreach (Target target in targets)
        {
            byte[] token = useNewGuid ? target.NewBytes : target.OldBytes;
            uint prefix = ReadPrefix(token, 0);
            List<Target> bucket;
            if (!byPrefix.TryGetValue(prefix, out bucket))
            {
                bucket = new List<Target>();
                byPrefix.Add(prefix, bucket);
            }
            bucket.Add(target);
        }

        int carry = 0;
        long readPosition = 0;

        using (var stream = new FileStream(
                   file.FullPath,
                   FileMode.Open,
                   FileAccess.Read,
                   FileShare.ReadWrite,
                   ScanBlockSize,
                   FileOptions.SequentialScan))
        {
            while (readPosition < stream.Length)
            {
                stream.Position = readPosition;
                int bytesRead = stream.Read(buffer, carry, ScanBlockSize);
                if (bytesRead <= 0)
                    break;

                long bufferStart = readPosition - carry;
                readPosition += bytesRead;
                int total = carry + bytesRead;
                int lastStart = total - GuidLength;

                for (int i = 0; i <= lastStart; i++)
                {
                    List<Target> bucket;
                    if (!byPrefix.TryGetValue(ReadPrefix(buffer, i), out bucket))
                        continue;

                    foreach (Target target in bucket)
                    {
                        byte[] token = useNewGuid ? target.NewBytes : target.OldBytes;
                        if (!GuidToolBytes.Matches(buffer, i, token))
                            continue;

                        List<long> offsets;
                        if (!target.Occurrences.TryGetValue(file.RelativePath, out offsets))
                        {
                            offsets = new List<long>();
                            target.Occurrences.Add(file.RelativePath, offsets);
                        }
                        offsets.Add(bufferStart + i);
                    }
                }

                carry = Math.Min(GuidLength - 1, total);
                if (carry > 0)
                    Buffer.BlockCopy(buffer, total - carry, buffer, 0, carry);
            }
        }

        // One line per file with matches keeps large batches from producing
        // a line for every target/file pair.
        int matchedTargets = 0;
        int matchCount = 0;
        foreach (Target target in targets)
        {
            List<long> offsets;
            if (!target.Occurrences.TryGetValue(file.RelativePath, out offsets))
                continue;
            matchedTargets++;
            matchCount += offsets.Count;
        }

        if (matchCount > 0)
            log.Write("Scan result: " + file.RelativePath + " | targets referenced: " +
                      matchedTargets + " | occurrences: " + matchCount);
    }

    private static uint ReadPrefix(byte[] bytes, int offset)
    {
        return ((uint)bytes[offset] << 24) |
               ((uint)bytes[offset + 1] << 16) |
               ((uint)bytes[offset + 2] << 8) |
               bytes[offset + 3];
    }

    internal static bool LooksLikeText(string fullPath, byte[] sample)
    {
        const int sampleSize = 4096;

        using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            int read = stream.Read(sample, 0, Math.Min(sampleSize, sample.Length));
            for (int i = 0; i < read; i++)
            {
                byte value = sample[i];
                if (value == 0)
                    return false;
                if (value < 0x08 || (value > 0x0D && value < 0x20))
                    return false;
            }
        }

        return true;
    }

    private static void ThrowIfBinaryDependency(
        FileState file,
        List<Target> targets,
        string message,
        GuidToolLog log)
    {
        if (!UnitySerializedExtensions.Contains(Path.GetExtension(file.FullPath)))
        {
            log.Write("Skipped non-text file: " + file.RelativePath);
            return;
        }

        // Binary files outside the AssetDatabase (for example ProjectSettings)
        // cannot be inspected, so they block the run without naming a target.
        if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(file.RelativePath)))
            throw new InvalidOperationException(
                "A binary-serialized Unity file cannot be checked for GUID references:\n" +
                file.RelativePath +
                "\n\nReserialize it as text before regenerating GUIDs.");

        string[] dependencies = AssetDatabase.GetDependencies(file.RelativePath, false);
        foreach (string dependency in dependencies)
        {
            foreach (Target target in targets)
            {
                if (!dependency.Equals(target.AssetPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                throw new InvalidOperationException(
                    message + file.RelativePath +
                    "\n\nReferenced asset:\n" + target.AssetPath +
                    "\n\nReserialize the referencing asset as text before regenerating GUIDs.");
            }
        }

        log.Write("Skipped binary asset with no selected GUID dependency: " + file.RelativePath);
    }

    private static bool IsTargetMeta(string relativePath, List<Target> targets)
    {
        foreach (Target target in targets)
        {
            if (relativePath.Equals(target.MetaRelativePath, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static int CountReferenceFiles(List<Target> targets)
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Target target in targets)
        {
            foreach (string path in target.Occurrences.Keys)
                files.Add(path);
        }
        return files.Count;
    }
}
}

// ============================================================================
// GLOSSARY: GUID regeneration: scan
// GuidToolRegenerate is one partial class: Editor/GuidTools/GuidToolRegenerate(.Scan/.Patch/.Rollback).cs and Editor/UI/GuidTools/GuidToolRegenerate.Prompts.cs.
// ============================================================================
//
// GuidToolRegenerate (this part)
//   EnumerateProjectTextFiles(projectRoot) / ThrowIfCaseCollision(files)  Every text file under Assets, ProjectSettings,
//                                   Packages and UserSettings/RiryTools; two paths differing only in case stop the run.
//   ScanReferences(...) / ScanOneFile(...) / CachedOffsetsStillMatch(...)  Finds each target's old GUID bytes, reusing
//                                   cached offsets for unchanged files (GuidToolReferenceCache).
//   LooksLikeText / ThrowIfBinaryDependency  Binary files referencing a target stop the run.
//   IsTargetMeta / CountReferenceFiles / ReadPrefix  Helpers.
