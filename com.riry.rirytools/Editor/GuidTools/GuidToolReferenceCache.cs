using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// The GUID reference cache in Library/RiryTools/GuidReferenceCache: one .rguiddb file per regenerated asset,
    /// listing the files and offsets that referenced its GUID, so the next regeneration of that asset only rescans
    /// changed files. The only code that reads, writes or deletes it. Derived data: deleting it only costs a full scan.
    /// Cleared from the GUID Tools window (Editor/UI/GuidTools/GuidToolClearReferenceCache.cs).
    /// </summary>
    internal static class GuidToolReferenceCache
    {
        private const string CacheExtension = ".rguiddb";
        private const int CacheNameLength = 40;
        private const int CacheMagic = 0x58494752; // RGIX
        private const int CacheVersion = 1;

        internal static string CacheDirectoryPath
        {
            get { return RiryToolsPaths.Library("GuidReferenceCache"); }
        }

        // Unreadable caches found by the current Load, reported as one line.
        private static int unreadableCaches;

        internal static void Load(
            List<GuidToolRegenerate.Target> targets,
            GuidToolLog log)
        {
            unreadableCaches = 0;
            foreach (GuidToolRegenerate.Target target in targets)
            {
                string cachePath = GetCachePath(target.AssetPath);
                target.Cache = TryLoad(cachePath, target.AssetPath, target.OldGuid, log);
                log.Write((target.Cache == null ? "Cache miss: " : "Cache hit: ") +
                          target.AssetPath + " | " + cachePath);
            }
            // One line for the run: each unreadable cache is in the GUID Tools log, and is simply rebuilt.
            if (unreadableCaches > 0)
                RiryLog.Warn(RiryLog.GuidTools, "Ignored " + unreadableCaches + " unreadable GUID cache file(s) in " + CacheDirectoryPath +
                    "; they are rebuilt. Each is listed in the GUID Tools log in " + GuidToolRegenerate.LogDirectoryPath + ".");
        }

        private static GuidToolRegenerate.ReferenceCache TryLoad(
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

                    var cache = new GuidToolRegenerate.ReferenceCache
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
                        var reference = new GuidToolRegenerate.CachedReference
                        {
                            RelativePath = GuidToolRegenerate.NormalizeAssetPath(reader.ReadString()),
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
                            if (offset <= previousOffset || offset > reference.Length - GuidToolRegenerate.GuidLength)
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
                unreadableCaches++;
                return null;
            }
        }

        internal static void Save(
            List<GuidToolRegenerate.Target> targets,
            string projectRoot,
            long scanStartedUtcTicks,
            GuidToolLog log)
        {
            Directory.CreateDirectory(CacheDirectoryPath);

            foreach (GuidToolRegenerate.Target target in targets)
            {
                string cachePath = GetCachePath(target.AssetPath);
                string tempPath = cachePath + ".tmp";
                if (File.Exists(tempPath))
                    File.Delete(tempPath);

                var references = new List<GuidToolRegenerate.CachedReference>();
                foreach (KeyValuePair<string, List<long>> occurrenceSet in target.Occurrences)
                {
                    string fullPath = GuidToolRegenerate.ProjectRelativeToFull(projectRoot, occurrenceSet.Key);
                    if (!File.Exists(fullPath) || occurrenceSet.Value.Count == 0)
                        continue;

                    var info = new FileInfo(fullPath);
                    references.Add(new GuidToolRegenerate.CachedReference
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

                    foreach (GuidToolRegenerate.CachedReference reference in references)
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

                GuidToolRegenerate.ReplaceFileAtomically(tempPath, cachePath);
                log.Write("Cache saved: " + cachePath +
                          " | reference files: " + references.Count);
            }
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

        /// <summary>The cache's file count and size in bytes, read in one pass; zero when it doesn't exist.</summary>
        internal static void Measure(out int files, out long bytes)
        {
            files = 0;
            bytes = 0;
            if (!Directory.Exists(CacheDirectoryPath)) return;
            foreach (FileInfo file in new DirectoryInfo(CacheDirectoryPath).EnumerateFiles("*", SearchOption.AllDirectories))
            {
                files++;
                bytes += file.Length;
            }
        }

        /// <summary>Deletes the whole cache folder.</summary>
        internal static void Delete()
        {
            Directory.Delete(CacheDirectoryPath, true);
        }
    }
}

// ============================================================================
// GLOSSARY: GUID reference cache
// ============================================================================
//
// GuidToolReferenceCache (this file): owns Library/RiryTools/GuidReferenceCache
//   CacheDirectoryPath              The folder (RiryToolsPaths.Library).
//   CacheExtension / CacheMagic / CacheVersion  .rguiddb / RGIX / 1: the file format. CacheNameLength (40): readable
//                                   part of a file name, before an 8-byte hash of the asset path (GetCachePath).
//   Load(targets, log) / TryLoad    Each target's cache, rejected on any mismatch; unreadable ones are counted and rebuilt.
//   Save(targets, projectRoot, scanStartedUtcTicks, log)  Writes each target's references atomically.
//   Measure(out files, out bytes) / Delete()  File count and size in one pass; deletes the folder.
