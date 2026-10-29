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
/// Regenerates GUIDs for selected assets while preserving text-serialized
/// references. Multi-selection and recursively selected folders are supported.
/// Folder GUIDs are never changed. Menu entries are in Editor/UI/RiryMenus.cs and
/// the GUID Tools window is in Editor/UI/GuidToolsWindow.cs.
/// </summary>
internal static class RegenerateAssetGuid
{
    private const string CacheExtension = ".rguiddb";
    private const int CacheNameLength = 40;
    private const int MaxDirectoryDepth = 64;
    private const int CacheMagic = 0x58494752; // RGIX
    private const int CacheVersion = 1;
    internal const int GuidLength = 32;
    internal const int ScanBlockSize = 256 * 1024;

    internal static string CacheDirectoryPath
    {
        get { return RiryToolsPaths.Library("GuidReferenceCache"); }
    }

    // Logs live in the project's Logs folder: an installed package folder is
    // read-only to Unity and is replaced on every package update.
    internal static string LogDirectoryPath
    {
        get { return RiryToolsPaths.Logs("GuidTools"); }
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

    internal static readonly HashSet<string> UnitySerializedExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".unity", ".prefab", ".mat", ".asset", ".controller",
            ".overridecontroller", ".anim", ".mask", ".playable", ".preset",
            ".rendertexture", ".cubemap", ".flare", ".fontsettings", ".guiskin",
            ".physicmaterial", ".physicsmaterial2d", ".terrainlayer", ".spriteatlas",
            ".spriteatlasv2", ".mixer", ".lighting", ".signal", ".timeline",
            ".shadervariants", ".inputsettings"
        };

    internal sealed class Target
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

    internal sealed class ReferenceCache
    {
        public string AssetPath;
        public string Guid;
        public long ScanStartedUtcTicks;
        public readonly Dictionary<string, CachedReference> References =
            new Dictionary<string, CachedReference>(StringComparer.OrdinalIgnoreCase);
    }

    internal sealed class CachedReference
    {
        public string RelativePath;
        public long Length;
        public long LastWriteUtcTicks;
        public List<long> Offsets;
    }

    internal sealed class FileState
    {
        public string FullPath;
        public string RelativePath;
        public long Length;
        public long LastWriteUtcTicks;
    }

    internal sealed class PatchOperation
    {
        public Target Target;
        public long Offset;
    }

    internal sealed class FilePatch
    {
        public FileState File;
        public readonly List<PatchOperation> Operations = new List<PatchOperation>();
    }

    internal sealed class WrittenOperation
    {
        public string FullPath;
        public long Offset;
        public byte[] OldBytes;
        public byte[] NewBytes;
    }

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
                Debug.LogError("Select one or more assets, or one or more folders under Assets.");
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
            LoadCaches(targets, log);

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

            // The files are already updated, so a ledger failure only warns.
            try
            {
                GuidRemapLedger.Append(targets);
                log.Write("Recorded " + targets.Count + " GUID change(s) in " + GuidRemapLedger.RelativePath);
            }
            catch (Exception exception)
            {
                log.Write("Ledger write failure: " + exception);
                Debug.LogWarning("GUID Tools could not record the GUID changes in " +
                    GuidRemapLedger.RelativePath + ":\n" + exception.Message);
            }

            ReloadScenes(sceneSetup, log);

            int referenceFileCount = CountReferenceFiles(targets);
            Debug.Log(
                "GUID regeneration completed.\n" +
                "Assets changed: " + targets.Count.ToString("N0", CultureInfo.InvariantCulture) + "\n" +
                "Reference files indexed: " + referenceFileCount.ToString("N0", CultureInfo.InvariantCulture) + "\n" +
                "Cache: " + CacheDirectoryPath);
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
                    ReloadScenes(sceneSetup, log);
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
        }
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

    internal static bool ScenesAreSaved()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).isDirty)
                return false;
        }
        return true;
    }

    internal static void ReloadScenes(SceneSetup[] setup, GuidToolLog log)
    {
        if (setup == null)
            return;

        // Untitled scenes have no file to reload; they were not dirty.
        var saved = new List<SceneSetup>();
        foreach (SceneSetup scene in setup)
        {
            if (!string.IsNullOrEmpty(scene.path))
                saved.Add(scene);
        }

        if (saved.Count == 0)
            return;

        if (!saved.Exists(scene => scene.isActive && scene.isLoaded))
        {
            SceneSetup active = saved.Find(scene => scene.isLoaded) ?? saved[0];
            foreach (SceneSetup scene in saved)
                scene.isActive = scene == active;
            active.isLoaded = true;
        }

        try
        {
            // Open an empty scene first so every scene is read again from disk
            // instead of keeping in-memory objects with the old references.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.RestoreSceneManagerSetup(saved.ToArray());
            log.Write("Reloaded open scenes from disk: " + saved.Count);
        }
        catch (Exception exception)
        {
            log.Write("Scene reload failure: " + exception);
            Debug.LogWarning(
                "GUID Tools could not reload the open scenes. Reopen them from disk " +
                "without saving first:\n" + exception.Message);
        }
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
        GuidToolLog log)
    {
        foreach (Target target in targets)
        {
            string cachePath = GetCachePath(target.AssetPath);
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
                        "Regenerating Asset GUIDs",
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

    private static void SaveCaches(
        List<Target> targets,
        string projectRoot,
        long scanStartedUtcTicks,
        GuidToolLog log)
    {
        Directory.CreateDirectory(CacheDirectoryPath);

        foreach (Target target in targets)
        {
            string cachePath = GetCachePath(target.AssetPath);
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

    internal static void RollBack(List<WrittenOperation> written, GuidToolLog log)
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

    private static bool Matches(byte[] buffer, int offset, byte[] token)
    {
        for (int i = 0; i < token.Length; i++)
        {
            if (buffer[offset + i] != token[i])
                return false;
        }
        return true;
    }

    internal static void EnsureWritable(string fullPath)
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

    internal static string GetCachePath(string assetPath)
    {
        string name = Path.GetFileNameWithoutExtension(assetPath);
        foreach (char invalid in Path.GetInvalidFileNameChars())
            name = name.Replace(invalid, '_');
        // The hash keeps names unique; a short readable part avoids Windows
        // path-length limits for long asset names.
        if (name.Length > CacheNameLength)
            name = name.Substring(0, CacheNameLength);
        name = name.TrimEnd('.', ' ');
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

        return Path.Combine(CacheDirectoryPath, name + "_" + hash + CacheExtension);
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

    internal static string NormalizeAssetPath(string path)
    {
        // macOS can report decomposed Unicode (NFD) file names while Unity
        // uses composed names (NFC); compare everything in NFC.
        return string.IsNullOrEmpty(path)
            ? path
            : path.Replace('\\', '/').Normalize(NormalizationForm.FormC);
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

internal sealed class GuidToolLog : IDisposable
{
    private const long ActiveLogLimit = 10L * 1024L * 1024L;
    private const long ArchiveLimit = 100L * 1024L * 1024L;
    private const string ActiveLogName = "guid-tools.log";

    private static readonly UTF8Encoding LogEncoding = new UTF8Encoding(false);

    private StreamWriter writer;
    private bool writeFailureReported;
    private string operation;
    private string activeLogPath;
    private string archiveDirectory;
    private long activeBytes;

    private GuidToolLog()
    {
    }

    internal static GuidToolLog Open(string operation)
    {
        var log = new GuidToolLog { operation = operation };

        try
        {
            string logDirectory = RegenerateAssetGuid.LogDirectoryPath;
            log.archiveDirectory = Path.Combine(logDirectory, "archive");
            log.activeLogPath = Path.Combine(logDirectory, ActiveLogName);

            Directory.CreateDirectory(logDirectory);
            List<string> maintenanceMessages = log.OpenWriter();

            log.Write("BEGIN " + operation);
            log.Write("Log path: " + log.activeLogPath);
            foreach (string message in maintenanceMessages)
                log.Write(message);
        }
        catch (Exception exception)
        {
            log.writer = null;
            Debug.LogWarning("GUID Tools could not open its diagnostic log:\n" + exception);
        }

        return log;
    }

    private List<string> OpenWriter()
    {
        var maintenanceMessages = new List<string>();
        RotateActiveLog(activeLogPath, archiveDirectory, maintenanceMessages);
        TrimArchive(archiveDirectory, maintenanceMessages);

        var stream = new FileStream(
            activeLogPath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.ReadWrite);
        activeBytes = stream.Length;
        writer = new StreamWriter(stream, LogEncoding);
        writer.AutoFlush = true;
        return maintenanceMessages;
    }

    internal void Write(string message)
    {
        if (writer == null)
            return;

        try
        {
            string line = "[" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) +
                          "] " + message;
            writer.WriteLine(line);
            activeBytes += LogEncoding.GetByteCount(line) + writer.NewLine.Length;

            // Enforce the limits during long runs, not only when a run starts.
            if (activeBytes > ActiveLogLimit)
            {
                writer.Dispose();
                writer = null;
                foreach (string maintenance in OpenWriter())
                    Write(maintenance);
            }
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
