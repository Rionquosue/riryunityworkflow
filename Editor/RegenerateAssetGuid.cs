#if UNITY_EDITOR

// Target editor version: Unity 2022.3.22f1.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Riry.UnityWorkflow.Editor
{
/// <summary>
/// Regenerates GUIDs for selected assets while preserving text-serialized
/// references. Multi-selection and recursively selected folders are supported.
/// Folder GUIDs are never changed.
/// </summary>
internal static class RegenerateAssetGuid
{
    private const string ProjectMenuPath =
        "Assets/Rirytools/Regenerate GUID";
    private const string CacheFolderName = "RegenerateGuidCache";
    private const string CacheExtension = ".rguiddb";
    private const int CacheMagic = 0x58494752; // RGIX
    private const int CacheVersion = 1;
    private const int GuidLength = 32;
    private const int ScanBlockSize = 256 * 1024;

    internal static string ProjectRootPath
    {
        get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..")); }
    }

    internal static string CacheDirectoryPath
    {
        get { return Path.Combine(ProjectRootPath, "Library", CacheFolderName); }
    }

    internal static string ScriptDirectoryPath
    {
        get
        {
            string compiledSourcePath = GetCompiledSourcePath();
            if (!string.IsNullOrEmpty(compiledSourcePath))
            {
                string fullSourcePath = Path.GetFullPath(compiledSourcePath);
                if (File.Exists(fullSourcePath))
                {
                    string compiledSourceDirectory =
                        Path.GetDirectoryName(fullSourcePath);

                    if (!string.IsNullOrEmpty(compiledSourceDirectory))
                        return compiledSourceDirectory;
                }
            }

            string filenameMatch = null;
            string[] scriptGuids = AssetDatabase.FindAssets("RegenerateAssetGuid t:MonoScript");
            foreach (string scriptGuid in scriptGuids)
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(scriptGuid);
                if (!string.Equals(
                        Path.GetFileName(scriptPath),
                        "RegenerateAssetGuid.cs",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (filenameMatch == null)
                    filenameMatch = scriptPath;

                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
                if (script == null || script.GetClass() != typeof(RegenerateAssetGuid))
                    continue;

                return GetPhysicalAssetDirectory(scriptPath);
            }

            if (filenameMatch != null)
                return GetPhysicalAssetDirectory(filenameMatch);

            return Path.Combine(Application.dataPath, "Editor");
        }
    }

    private static string GetCompiledSourcePath(
        [CallerFilePath] string sourcePath = null)
    {
        return sourcePath;
    }

    private static string GetPhysicalAssetDirectory(string scriptPath)
    {
        string physicalScriptPath = FileUtil.GetPhysicalPath(scriptPath);
        if (!string.IsNullOrEmpty(physicalScriptPath))
        {
            string physicalDirectory =
                Path.GetDirectoryName(Path.GetFullPath(physicalScriptPath));

            if (!string.IsNullOrEmpty(physicalDirectory))
                return physicalDirectory;
        }

        string logicalDirectory = Path.GetDirectoryName(scriptPath);
        return ProjectRelativeToFull(
            ProjectRootPath,
            NormalizeAssetPath(logicalDirectory));
    }

    private static readonly HashSet<string> TextExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".meta", ".unity", ".prefab", ".mat", ".asset", ".controller",
            ".overridecontroller", ".anim", ".mask", ".playable", ".preset",
            ".rendertexture", ".cubemap", ".flare", ".fontsettings", ".guiskin",
            ".physicmaterial", ".physicsmaterial2d", ".terrainlayer", ".spriteatlas",
            ".spriteatlasv2", ".shadergraph", ".shadersubgraph", ".vfx", ".asmdef",
            ".asmref", ".json", ".txt", ".yaml", ".yml", ".xml", ".uxml",
            ".uss", ".shader", ".compute", ".cginc", ".hlsl", ".glslinc",
            ".raytrace", ".cs", ".rsp", ".link", ".config", ".props", ".targets",
            ".plist", ".gradle", ".mixer", ".lighting", ".signal", ".timeline",
            ".shadervariants", ".inputactions", ".inputsettings", ".fnt"
        };

    private static readonly HashSet<string> UnitySerializedExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".unity", ".prefab", ".mat", ".asset", ".controller",
            ".overridecontroller", ".anim", ".mask", ".playable", ".preset",
            ".rendertexture", ".cubemap", ".flare", ".fontsettings", ".guiskin",
            ".physicmaterial", ".physicsmaterial2d", ".terrainlayer", ".spriteatlas",
            ".spriteatlasv2", ".mixer", ".lighting", ".signal", ".timeline",
            ".shadervariants", ".inputsettings"
        };

    private sealed class Target
    {
        public string AssetPath;
        public string MetaRelativePath;
        public string OldGuid;
        public string NewGuid;
        public byte[] OldBytes;
        public byte[] NewBytes;
        public ReferenceCache Cache;
        public readonly Dictionary<string, List<long>> Occurrences =
            new Dictionary<string, List<long>>(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class ReferenceCache
    {
        public string AssetPath;
        public string Guid;
        public long ScanStartedUtcTicks;
        public readonly Dictionary<string, CachedReference> References =
            new Dictionary<string, CachedReference>(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class CachedReference
    {
        public string RelativePath;
        public long Length;
        public long LastWriteUtcTicks;
        public List<long> Offsets;
    }

    private sealed class FileState
    {
        public string FullPath;
        public string RelativePath;
        public long Length;
        public long LastWriteUtcTicks;
    }

    private sealed class PatchOperation
    {
        public Target Target;
        public long Offset;
    }

    private sealed class FilePatch
    {
        public FileState File;
        public readonly List<PatchOperation> Operations = new List<PatchOperation>();
    }

    private sealed class WrittenOperation
    {
        public string FullPath;
        public long Offset;
        public byte[] OldBytes;
        public byte[] NewBytes;
    }

    [MenuItem(ProjectMenuPath, false, 2000)]
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

            string projectRoot = ProjectRootPath;
            List<string> assetPaths = CollectSelectedAssetPaths();

            log.Write("Selected target count after folder expansion: " + assetPaths.Count);
            foreach (string selectedPath in assetPaths)
                log.Write("Target candidate: " + selectedPath);

            if (assetPaths.Count == 0)
            {
                log.Write("Stopped: no valid assets were selected.");
                Debug.LogError("Select one or more assets, or one or more folders under Assets.");
                return;
            }

            targets = BuildTargets(assetPaths, projectRoot, log);
            LoadCaches(targets, projectRoot, log);

            string confirmation = targets.Count == 1
                ? "Regenerate the GUID for this asset and update all serialized references?\n\n" +
                  targets[0].AssetPath
                : "Regenerate GUIDs for " + targets.Count.ToString("N0", CultureInfo.InvariantCulture) +
                  " assets and update all serialized references?\n\n" +
                  "Selected folders are expanded recursively; folder GUIDs are not changed.";

            confirmation += "\n\nThis bypasses Unity Undo. Commit or back up the project first.";

            if (!EditorUtility.DisplayDialog("Regenerate Asset GUID", confirmation, "Regenerate", "Cancel"))
            {
                log.Write("Cancelled at the confirmation dialog.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                log.Write("Cancelled while saving modified scenes.");
                return;
            }

            AssetDatabase.SaveAssets();

            files = EnumerateProjectTextFiles(projectRoot);
            log.Write("Eligible project text files: " + files.Count);
            scanBuffer = new byte[ScanBlockSize + GuidLength - 1];
            ScanReferences(targets, files, scanBuffer, log);
            patches = BuildPatches(targets, files, log);

            if (patches.Count == 0)
                throw new InvalidOperationException("No GUID occurrences were found. No files were changed.");

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
                "Regenerating Asset GUIDs",
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

            SaveCaches(targets, projectRoot, scanStartedUtcTicks, log);
            committed = true;

            int referenceFileCount = CountReferenceFiles(targets);
            Debug.Log(
                "GUID regeneration completed.\n" +
                "Assets changed: " + targets.Count.ToString("N0", CultureInfo.InvariantCulture) + "\n" +
                "Reference files indexed: " + referenceFileCount.ToString("N0", CultureInfo.InvariantCulture) + "\n" +
                "Cache: Library/" + CacheFolderName);
            log.Write("Completed successfully. Assets changed: " + targets.Count +
                      "; reference files indexed: " + referenceFileCount + ".");
        }
        catch (OperationCanceledException)
        {
            log.Write("Cancelled during the preflight scan; no files were changed.");
            Debug.Log("GUID regeneration cancelled before any files were changed.");
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
                }
                catch (Exception rollbackException)
                {
                    log.Write("ROLLBACK FAILURE: " + rollbackException);
                    Debug.LogError("GUID rollback was incomplete:\n" + rollbackException);
                }
            }

            log.Write("FAILURE: " + exception);
            Debug.LogException(exception);
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
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    [MenuItem(ProjectMenuPath, true)]
    private static bool ValidateRegenerateSelectedGuids()
    {
        return HasValidSelection();
    }

    internal static bool HasValidSelection()
    {
        UnityEngine.Object[] selected = Selection.objects;
        if (selected == null || selected.Length == 0)
            return false;

        foreach (UnityEngine.Object item in selected)
        {
            string path = AssetDatabase.GetAssetPath(item);
            if (!string.IsNullOrEmpty(path) &&
                (path.Equals("Assets", StringComparison.Ordinal) ||
                 path.StartsWith("Assets/", StringComparison.Ordinal)))
                return true;
        }

        return false;
    }

    private static List<string> CollectSelectedAssetPaths()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (UnityEngine.Object item in Selection.objects)
        {
            string path = NormalizeAssetPath(AssetDatabase.GetAssetPath(item));
            if (string.IsNullOrEmpty(path) ||
                !(path.Equals("Assets", StringComparison.Ordinal) ||
                  path.StartsWith("Assets/", StringComparison.Ordinal)))
                continue;

            if (!AssetDatabase.IsValidFolder(path))
            {
                paths.Add(path);
                continue;
            }

            string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { path });
            foreach (string guid in guids)
            {
                string childPath = NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(guid));
                if (string.IsNullOrEmpty(childPath) || AssetDatabase.IsValidFolder(childPath))
                    continue;

                paths.Add(childPath);
            }
        }

        var result = new List<string>(paths);
        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    private static List<Target> BuildTargets(
        List<string> assetPaths,
        string projectRoot,
        GuidToolLog log)
    {
        var targets = new List<Target>(assetPaths.Count);
        var oldGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var newGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string assetPath in assetPaths)
        {
            string oldGuid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(oldGuid) || oldGuid.Length != GuidLength)
                throw new InvalidOperationException("Could not read the GUID for:\n" + assetPath);

            if (!oldGuids.Add(oldGuid))
                throw new InvalidOperationException(
                    "Two selected assets currently share GUID " + oldGuid +
                    ". Resolve the duplicate-meta collision before running a batch regeneration.");

            string newGuid;
            do
            {
                newGuid = System.Guid.NewGuid().ToString("N");
            }
            while (!newGuids.Add(newGuid) ||
                   !string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(newGuid)));

            string relativeMeta = NormalizeAssetPath(assetPath + ".meta");
            string fullMeta = ProjectRelativeToFull(projectRoot, relativeMeta);
            if (!File.Exists(fullMeta))
                throw new FileNotFoundException("Meta file not found.", fullMeta);

            targets.Add(new Target
            {
                AssetPath = assetPath,
                MetaRelativePath = relativeMeta,
                OldGuid = oldGuid,
                NewGuid = newGuid,
                OldBytes = Encoding.ASCII.GetBytes(oldGuid),
                NewBytes = Encoding.ASCII.GetBytes(newGuid)
            });

            log.Write("GUID mapping prepared: " + assetPath + " | " + oldGuid + " -> " + newGuid);
        }

        return targets;
    }

    private static void LoadCaches(
        List<Target> targets,
        string projectRoot,
        GuidToolLog log)
    {
        foreach (Target target in targets)
        {
            string cachePath = GetCachePath(projectRoot, target.AssetPath);
            target.Cache = TryLoadCache(cachePath, target.AssetPath, target.OldGuid, log);
            log.Write((target.Cache == null ? "Cache miss: " : "Cache hit: ") +
                      target.AssetPath + " | " + cachePath);
        }
    }

    private static ReferenceCache TryLoadCache(
        string cachePath,
        string expectedAssetPath,
        string expectedGuid,
        GuidToolLog log)
    {
        if (!File.Exists(cachePath))
            return null;

        try
        {
            using (var stream = new FileStream(cachePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                if (reader.ReadInt32() != CacheMagic || reader.ReadInt32() != CacheVersion)
                {
                    log.Write("Cache rejected due to header/version mismatch: " + cachePath);
                    return null;
                }

                var cache = new ReferenceCache
                {
                    AssetPath = reader.ReadString(),
                    Guid = reader.ReadString(),
                    ScanStartedUtcTicks = reader.ReadInt64()
                };

                if (!cache.AssetPath.Equals(expectedAssetPath, StringComparison.OrdinalIgnoreCase) ||
                    !cache.Guid.Equals(expectedGuid, StringComparison.OrdinalIgnoreCase))
                {
                    log.Write("Cache rejected due to asset path or GUID mismatch: " + cachePath);
                    return null;
                }

                int referenceCount = reader.ReadInt32();
                if (referenceCount < 0 || referenceCount > 10000000)
                {
                    log.Write("Cache rejected due to invalid reference count: " + cachePath);
                    return null;
                }

                for (int i = 0; i < referenceCount; i++)
                {
                    var reference = new CachedReference
                    {
                        RelativePath = NormalizeAssetPath(reader.ReadString()),
                        Length = reader.ReadInt64(),
                        LastWriteUtcTicks = reader.ReadInt64()
                    };

                    int offsetCount = reader.ReadInt32();
                    if (offsetCount <= 0 || offsetCount > 10000000)
                    {
                        log.Write("Cache rejected due to invalid offset count: " + cachePath);
                        return null;
                    }

                    reference.Offsets = new List<long>(offsetCount);
                    long previousOffset = -1;
                    for (int offsetIndex = 0; offsetIndex < offsetCount; offsetIndex++)
                    {
                        long offset = reader.ReadInt64();
                        if (offset <= previousOffset || offset > reference.Length - GuidLength)
                        {
                            log.Write("Cache rejected due to invalid offset data: " + cachePath);
                            return null;
                        }
                        reference.Offsets.Add(offset);
                        previousOffset = offset;
                    }

                    cache.References[reference.RelativePath] = reference;
                }

                return cache;
            }
        }
        catch (Exception exception)
        {
            log.Write("Cache read failure: " + cachePath + " | " + exception);
            Debug.LogWarning("Ignoring an unreadable GUID cache:\n" + cachePath + "\n" + exception.Message);
            return null;
        }
    }

    private static List<FileState> EnumerateProjectTextFiles(string projectRoot)
    {
        string[] roots =
        {
            Path.Combine(projectRoot, "Assets"),
            Path.Combine(projectRoot, "ProjectSettings"),
            Path.Combine(projectRoot, "Packages")
        };

        var files = new List<FileState>();

        foreach (string root in roots)
        {
            if (!Directory.Exists(root))
                continue;

            foreach (string fullPath in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
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
        return files;
    }

    private static void ScanReferences(
        List<Target> targets,
        List<FileState> files,
        byte[] scanBuffer,
        GuidToolLog log)
    {
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
                    log.Write("Cached reference reused: " + target.AssetPath + " <- " +
                              file.RelativePath + " | occurrences: " + cached.Offsets.Count);
                    continue;
                }

                bool requiresScan =
                    target.Cache == null ||
                    hasCachedReference ||
                    file.RelativePath.Equals(target.MetaRelativePath, StringComparison.OrdinalIgnoreCase) ||
                    file.LastWriteUtcTicks >= target.Cache.ScanStartedUtcTicks;

                if (requiresScan)
                    targetsToScan.Add(target);
            }

            if (targetsToScan.Count == 0)
                continue;

            log.Write("Scanning file: " + file.RelativePath +
                      " | GUID candidates: " + targetsToScan.Count);

            if ((fileIndex & 31) == 0)
            {
                float progress = files.Count == 0 ? 0.1f : 0.05f + 0.55f * fileIndex / files.Count;
                if (EditorUtility.DisplayCancelableProgressBar(
                        "Regenerating Asset GUIDs",
                        "Scanning " + file.RelativePath,
                        progress))
                    throw new OperationCanceledException();
            }

            if (!LooksLikeText(file.FullPath, scanBuffer))
            {
                Target binaryDependency;
                if (UnitySerializedExtensions.Contains(Path.GetExtension(file.FullPath)) &&
                    TryFindBinaryDependency(file.RelativePath, targetsToScan, out binaryDependency))
                    throw new InvalidOperationException(
                        "A binary-serialized Unity asset references a GUID being changed:\n" +
                        file.RelativePath +
                        "\n\nReferenced asset:\n" + binaryDependency.AssetPath +
                        "\n\nReserialize the referencing asset as text before regenerating GUIDs.");

                log.Write("Skipped non-text file with no selected GUID dependency: " +
                          file.RelativePath);
                continue;
            }

            ScanOneFile(file, targetsToScan, false, scanBuffer, log);
        }
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
                    if (read != token.Length || !Matches(checkBuffer, 0, token))
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

    private static void ScanOneFile(
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
                        if (!Matches(buffer, i, token))
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

        foreach (Target target in targets)
        {
            List<long> offsets;
            int count = target.Occurrences.TryGetValue(file.RelativePath, out offsets)
                ? offsets.Count
                : 0;
            log.Write("Scan result: " + target.AssetPath + " <- " + file.RelativePath +
                      " | occurrences: " + count);
        }
    }

    private static uint ReadPrefix(byte[] bytes, int offset)
    {
        return ((uint)bytes[offset] << 24) |
               ((uint)bytes[offset + 1] << 16) |
               ((uint)bytes[offset + 2] << 8) |
               bytes[offset + 3];
    }

    private static List<FilePatch> BuildPatches(
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

    private static void ApplyPatches(
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
                    "Regenerating Asset GUIDs",
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
                            !Matches(check, 0, operation.Target.OldBytes))
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
                        log.Write("Patched offset " + operation.Offset + " in " +
                                  patch.File.RelativePath + " | " +
                                  operation.Target.OldGuid + " -> " +
                                  operation.Target.NewGuid);
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
                Target binaryDependency;
                if (UnitySerializedExtensions.Contains(Path.GetExtension(current.FullPath)) &&
                    TryFindBinaryDependency(current.RelativePath, targets, out binaryDependency))
                    throw new InvalidOperationException(
                        "Unity produced or modified a binary asset that references:\n" +
                        binaryDependency.AssetPath + "\n\nFile:\n" + current.RelativePath);
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

    private static void SaveCaches(
        List<Target> targets,
        string projectRoot,
        long scanStartedUtcTicks,
        GuidToolLog log)
    {
        string cacheDirectory = Path.Combine(projectRoot, "Library", CacheFolderName);
        Directory.CreateDirectory(cacheDirectory);

        foreach (Target target in targets)
        {
            string cachePath = GetCachePath(projectRoot, target.AssetPath);
            string tempPath = cachePath + ".tmp";
            if (File.Exists(tempPath))
                File.Delete(tempPath);

            var references = new List<CachedReference>();
            foreach (KeyValuePair<string, List<long>> occurrenceSet in target.Occurrences)
            {
                string fullPath = ProjectRelativeToFull(projectRoot, occurrenceSet.Key);
                if (!File.Exists(fullPath) || occurrenceSet.Value.Count == 0)
                    continue;

                var info = new FileInfo(fullPath);
                references.Add(new CachedReference
                {
                    RelativePath = occurrenceSet.Key,
                    Length = info.Length,
                    LastWriteUtcTicks = info.LastWriteTimeUtc.Ticks,
                    Offsets = occurrenceSet.Value
                });
            }

            references.Sort((left, right) =>
                StringComparer.OrdinalIgnoreCase.Compare(left.RelativePath, right.RelativePath));

            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write(CacheMagic);
                writer.Write(CacheVersion);
                writer.Write(target.AssetPath);
                writer.Write(target.NewGuid);
                writer.Write(scanStartedUtcTicks);
                writer.Write(references.Count);

                foreach (CachedReference reference in references)
                {
                    writer.Write(reference.RelativePath);
                    writer.Write(reference.Length);
                    writer.Write(reference.LastWriteUtcTicks);
                    writer.Write(reference.Offsets.Count);
                    foreach (long offset in reference.Offsets)
                        writer.Write(offset);
                }

                writer.Flush();
                stream.Flush();
            }

            ReplaceFileAtomically(tempPath, cachePath);
            log.Write("Cache saved: " + cachePath +
                      " | reference files: " + references.Count);
        }
    }

    private static void RollBack(List<WrittenOperation> written, GuidToolLog log)
    {
        var byFile = new Dictionary<string, List<WrittenOperation>>(StringComparer.OrdinalIgnoreCase);
        foreach (WrittenOperation operation in written)
        {
            List<WrittenOperation> operations;
            if (!byFile.TryGetValue(operation.FullPath, out operations))
            {
                operations = new List<WrittenOperation>();
                byFile.Add(operation.FullPath, operations);
            }
            operations.Add(operation);
        }

        var check = new byte[GuidLength];
        var failures = new List<Exception>();
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (KeyValuePair<string, List<WrittenOperation>> file in byFile)
            {
                try
                {
                    using (var stream = new FileStream(
                               file.Key,
                               FileMode.Open,
                               FileAccess.ReadWrite,
                               FileShare.Read))
                    {
                        foreach (WrittenOperation operation in file.Value)
                        {
                            try
                            {
                                stream.Position = operation.Offset;
                                int read = stream.Read(check, 0, check.Length);
                                if (read != GuidLength)
                                    throw new IOException(
                                        "Rollback could not read the original GUID location.");

                                if (Matches(check, 0, operation.OldBytes))
                                {
                                    log.Write("Rollback found the original GUID already present at offset " +
                                              operation.Offset + " in " + file.Key);
                                    continue;
                                }

                                if (!Matches(check, 0, operation.NewBytes))
                                    log.Write(
                                        "Rollback found a partial or externally changed GUID at offset " +
                                        operation.Offset + " in " + file.Key +
                                        "; restoring the known original bytes.");

                                stream.Position = operation.Offset;
                                stream.Write(operation.OldBytes, 0, GuidLength);
                                log.Write("Rolled back offset " + operation.Offset +
                                          " in " + file.Key);
                            }
                            catch (Exception exception)
                            {
                                failures.Add(new IOException(
                                    "Rollback failed at offset " + operation.Offset +
                                    " in " + file.Key + ".",
                                    exception));
                            }
                        }

                        try
                        {
                            stream.Flush();
                        }
                        catch (Exception exception)
                        {
                            failures.Add(new IOException(
                                "Rollback flush failed for " + file.Key + ".",
                                exception));
                        }
                    }
                }
                catch (Exception exception)
                {
                    failures.Add(new IOException(
                        "Rollback could not process " + file.Key + ".",
                        exception));
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        if (failures.Count > 0)
            throw new AggregateException(
                "One or more files could not be completely restored.",
                failures);
    }

    private static bool LooksLikeText(string fullPath, byte[] sample)
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

    private static bool TryFindBinaryDependency(
        string relativePath,
        List<Target> targets,
        out Target matchingTarget)
    {
        matchingTarget = null;

        if (!relativePath.StartsWith("Assets/", StringComparison.Ordinal) ||
            relativePath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
        {
            // Binary project/package settings cannot be safely inspected here.
            if (targets.Count == 0)
                return false;
            matchingTarget = targets[0];
            return true;
        }

        string[] dependencies = AssetDatabase.GetDependencies(relativePath, false);
        foreach (string dependency in dependencies)
        {
            foreach (Target target in targets)
            {
                if (!dependency.Equals(target.AssetPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                matchingTarget = target;
                return true;
            }
        }

        return false;
    }

    private static bool Matches(byte[] buffer, int offset, byte[] token)
    {
        for (int i = 0; i < token.Length; i++)
        {
            if (buffer[offset + i] != token[i])
                return false;
        }
        return true;
    }

    private static void EnsureWritable(string fullPath)
    {
        if ((File.GetAttributes(fullPath) & FileAttributes.ReadOnly) != 0)
            throw new UnauthorizedAccessException("A file is read-only:\n" + fullPath);

        using (new FileStream(fullPath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
        {
        }
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

    private static string GetCachePath(string projectRoot, string assetPath)
    {
        string name = Path.GetFileNameWithoutExtension(assetPath);
        foreach (char invalid in Path.GetInvalidFileNameChars())
            name = name.Replace(invalid, '_');
        if (string.IsNullOrEmpty(name))
            name = "asset";

        string hash;
        using (SHA256 sha = SHA256.Create())
        {
            byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(assetPath.ToLowerInvariant()));
            var builder = new StringBuilder(16);
            for (int i = 0; i < 8; i++)
                builder.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
            hash = builder.ToString();
        }

        return Path.Combine(
            projectRoot,
            "Library",
            CacheFolderName,
            name + "_" + hash + CacheExtension);
    }

    private static void ReplaceFileAtomically(string tempPath, string finalPath)
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

    private static string ProjectRelativeToFull(string projectRoot, string relativePath)
    {
        return Path.GetFullPath(Path.Combine(
            projectRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string MakeProjectRelative(string projectRoot, string fullPath)
    {
        string root = Path.GetFullPath(projectRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        string full = Path.GetFullPath(fullPath);

        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Path is outside the Unity project:\n" + fullPath);

        return NormalizeAssetPath(full.Substring(root.Length));
    }

    private static string NormalizeAssetPath(string path)
    {
        return string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/');
    }

    private static void ClearTemporaryData(
        List<Target> targets,
        List<FileState> files,
        List<FilePatch> patches,
        List<WrittenOperation> written)
    {
        if (targets != null)
        {
            foreach (Target target in targets)
            {
                foreach (List<long> offsets in target.Occurrences.Values)
                    offsets.Clear();
                target.Occurrences.Clear();

                if (target.Cache != null)
                {
                    foreach (CachedReference reference in target.Cache.References.Values)
                    {
                        if (reference.Offsets != null)
                            reference.Offsets.Clear();
                    }
                    target.Cache.References.Clear();
                }

                target.OldBytes = null;
                target.NewBytes = null;
                target.Cache = null;
            }
            targets.Clear();
        }

        if (files != null)
            files.Clear();
        if (patches != null)
            patches.Clear();
        if (written != null)
            written.Clear();
    }
}

internal sealed class GuidToolsWindow : EditorWindow
{
    private const string WindowMenuPath = "Tools/Rirytools/GUID Tools";

    [MenuItem(WindowMenuPath, false, 2010)]
    private static void OpenWindow()
    {
        GuidToolsWindow window = GetWindow<GuidToolsWindow>();
        window.titleContent = new GUIContent("GUID Tools");
        window.minSize = new Vector2(360f, 190f);
        window.Show();
    }

    private void OnGUI()
    {
        GUILayout.Space(10f);
        EditorGUILayout.LabelField("Asset GUID Tools", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Regenerates GUIDs for selected assets while preserving text-serialized references. " +
            "Folders are expanded recursively without changing folder GUIDs.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(!RegenerateAssetGuid.HasValidSelection()))
        {
            if (GUILayout.Button("Regenerate Selected GUIDs", GUILayout.Height(30f)))
                RegenerateAssetGuid.RegenerateSelectedGuids();
        }

        GUILayout.Space(4f);
        if (GUILayout.Button("Delete Reference Cache", GUILayout.Height(24f)))
            ClearRegenerateGuidCache.DeleteCacheWithConfirmation();

        GUILayout.Space(6f);
        EditorGUILayout.LabelField(
            "Cache: Library/RegenerateGuidCache",
            EditorStyles.miniLabel);
        EditorGUILayout.LabelField(
            "Logs: beside RegenerateAssetGuid.cs under logs/",
            EditorStyles.miniLabel);
    }

    private void OnSelectionChange()
    {
        Repaint();
    }
}

internal sealed class GuidToolLog : IDisposable
{
    private const long ActiveLogLimit = 10L * 1024L * 1024L;
    private const long ArchiveLimit = 100L * 1024L * 1024L;
    private const string ActiveLogName = "guid-tools.log";

    private StreamWriter writer;
    private bool writeFailureReported;
    private string operation;

    private GuidToolLog()
    {
    }

    internal static GuidToolLog Open(string operation)
    {
        var log = new GuidToolLog { operation = operation };

        try
        {
            string logDirectory = Path.Combine(RegenerateAssetGuid.ScriptDirectoryPath, "logs");
            string archiveDirectory = Path.Combine(logDirectory, "archive");
            string activeLogPath = Path.Combine(logDirectory, ActiveLogName);
            var maintenanceMessages = new List<string>();

            Directory.CreateDirectory(logDirectory);
            RotateActiveLog(activeLogPath, archiveDirectory, maintenanceMessages);
            TrimArchive(archiveDirectory, maintenanceMessages);

            var stream = new FileStream(
                activeLogPath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite);
            log.writer = new StreamWriter(stream, new UTF8Encoding(false));
            log.writer.AutoFlush = true;

            log.Write("BEGIN " + operation);
            log.Write("Log path: " + activeLogPath);
            foreach (string message in maintenanceMessages)
                log.Write(message);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("GUID Tools could not open its diagnostic log:\n" + exception);
        }

        return log;
    }

    internal void Write(string message)
    {
        if (writer == null)
            return;

        try
        {
            writer.WriteLine("[" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) +
                             "] " + message);
        }
        catch (Exception exception)
        {
            if (!writeFailureReported)
            {
                writeFailureReported = true;
                Debug.LogWarning("GUID Tools diagnostic logging failed:\n" + exception);
            }
        }
    }

    public void Dispose()
    {
        if (writer == null)
            return;

        try
        {
            Write("END " + operation);
            writer.Dispose();
        }
        catch (Exception exception)
        {
            if (!writeFailureReported)
                Debug.LogWarning("GUID Tools could not close its diagnostic log:\n" + exception);
        }
        finally
        {
            writer = null;
        }
    }

    private static void RotateActiveLog(
        string activeLogPath,
        string archiveDirectory,
        List<string> maintenanceMessages)
    {
        if (!File.Exists(activeLogPath) || new FileInfo(activeLogPath).Length <= ActiveLogLimit)
            return;

        Directory.CreateDirectory(archiveDirectory);
        string archivedName =
            "guid-tools_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) +
            ".log";
        string archivedPath = Path.Combine(archiveDirectory, archivedName);

        int suffix = 1;
        while (File.Exists(archivedPath))
        {
            archivedPath = Path.Combine(
                archiveDirectory,
                Path.GetFileNameWithoutExtension(archivedName) + "_" + suffix + ".log");
            suffix++;
        }

        File.Move(activeLogPath, archivedPath);
        maintenanceMessages.Add("Rotated active log to: " + archivedPath);
    }

    private static void TrimArchive(
        string archiveDirectory,
        List<string> maintenanceMessages)
    {
        if (!Directory.Exists(archiveDirectory))
            return;

        string[] paths = Directory.GetFiles(archiveDirectory, "*.log", SearchOption.TopDirectoryOnly);
        var files = new List<FileInfo>(paths.Length);
        long totalBytes = 0;

        foreach (string path in paths)
        {
            var file = new FileInfo(path);
            files.Add(file);
            totalBytes += file.Length;
        }

        files.Sort((left, right) =>
        {
            int timeComparison = left.LastWriteTimeUtc.CompareTo(right.LastWriteTimeUtc);
            return timeComparison != 0
                ? timeComparison
                : StringComparer.OrdinalIgnoreCase.Compare(left.FullName, right.FullName);
        });

        int index = 0;
        while (totalBytes > ArchiveLimit && index < files.Count)
        {
            FileInfo oldest = files[index++];
            long length = oldest.Length;
            string path = oldest.FullName;
            oldest.Delete();
            totalBytes -= length;
            maintenanceMessages.Add("Deleted oldest archived log: " + path);
        }
    }
}

}

#endif
