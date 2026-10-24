#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEditor;

namespace Riry.UnityWorkflow.Editor.Tests
{
    // Works on temporary files outside the project; no assets are imported.
    public sealed class RegenerateAssetGuidTests
    {
        private const string OldGuid = "0123456789abcdef0123456789abcdef";
        private const string NewGuid = "fedcba9876543210fedcba9876543210";
        private const string MetaPath = "Assets/Target.asset.meta";

        private string directory;
        private GuidToolLog log;
        private byte[] buffer;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "riry-guid-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            log = GuidToolLog.Disabled();
            buffer = new byte[RegenerateAssetGuid.ScanBlockSize + RegenerateAssetGuid.GuidLength - 1];
        }

        [TearDown]
        public void TearDown()
        {
            EditorUtility.ClearProgressBar();
            log.Dispose();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void ScanFindsGuidsAtStartAcrossBlockBoundaryAndAtEnd()
        {
            int boundary = RegenerateAssetGuid.ScanBlockSize - 10;
            var text = new StringBuilder();
            text.Append(OldGuid);
            text.Append('-', boundary - text.Length);
            text.Append(OldGuid);
            text.Append('-', 1000);
            int last = text.Length;
            text.Append(OldGuid);

            RegenerateAssetGuid.FileState file = WriteFile("Assets/Big.prefab", text.ToString());
            RegenerateAssetGuid.Target target = NewTarget();
            RegenerateAssetGuid.ScanOneFile(file, new List<RegenerateAssetGuid.Target> { target },
                false, buffer, log);

            Assert.That(target.Occurrences[file.RelativePath], Is.EqualTo(new long[] { 0, boundary, last }));
        }

        [Test]
        public void PatchRewritesOnlyGuidBytesAndRollBackRestoresThem()
        {
            string referenceText = "m_Mesh: {fileID: 4300000, guid: " + OldGuid + ", type: 3}\n";
            string metaText = "fileFormatVersion: 2\nguid: " + OldGuid + "\n";
            RegenerateAssetGuid.FileState reference = WriteFile("Assets/Ref.prefab", referenceText);
            RegenerateAssetGuid.FileState meta = WriteFile(MetaPath, metaText);
            RegenerateAssetGuid.Target target = NewTarget();
            var targets = new List<RegenerateAssetGuid.Target> { target };
            var files = new List<RegenerateAssetGuid.FileState> { reference, meta };
            foreach (RegenerateAssetGuid.FileState file in files)
                RegenerateAssetGuid.ScanOneFile(file, targets, false, buffer, log);

            List<RegenerateAssetGuid.FilePatch> patches = RegenerateAssetGuid.BuildPatches(targets, files, log);
            Assert.That(patches[patches.Count - 1].File.RelativePath, Is.EqualTo(MetaPath),
                "The target meta file must be patched last.");

            var written = new List<RegenerateAssetGuid.WrittenOperation>();
            RegenerateAssetGuid.ApplyPatches(patches, written, log);
            Assert.That(File.ReadAllText(reference.FullPath), Is.EqualTo(referenceText.Replace(OldGuid, NewGuid)));
            Assert.That(File.ReadAllText(meta.FullPath), Is.EqualTo(metaText.Replace(OldGuid, NewGuid)));

            RegenerateAssetGuid.RollBack(written, log);
            Assert.That(File.ReadAllText(reference.FullPath), Is.EqualTo(referenceText));
            Assert.That(File.ReadAllText(meta.FullPath), Is.EqualTo(metaText));
        }

        [Test]
        public void PatchRefusesAFileChangedAfterScanning()
        {
            RegenerateAssetGuid.FileState meta = WriteFile(MetaPath, "guid: " + OldGuid + "\n");
            RegenerateAssetGuid.Target target = NewTarget();
            var targets = new List<RegenerateAssetGuid.Target> { target };
            var files = new List<RegenerateAssetGuid.FileState> { meta };
            RegenerateAssetGuid.ScanOneFile(meta, targets, false, buffer, log);
            List<RegenerateAssetGuid.FilePatch> patches = RegenerateAssetGuid.BuildPatches(targets, files, log);

            File.WriteAllText(meta.FullPath, "guid:  " + OldGuid + "\n");

            Assert.Throws<IOException>(() => RegenerateAssetGuid.ApplyPatches(
                patches, new List<RegenerateAssetGuid.WrittenOperation>(), log));
            Assert.That(File.ReadAllText(meta.FullPath), Is.EqualTo("guid:  " + OldGuid + "\n"));
        }

        [Test]
        public void MissingTargetMetaOccurrenceBlocksPatching()
        {
            RegenerateAssetGuid.FileState reference = WriteFile("Assets/Ref.prefab", "guid: " + OldGuid);
            RegenerateAssetGuid.Target target = NewTarget();
            var targets = new List<RegenerateAssetGuid.Target> { target };
            RegenerateAssetGuid.ScanOneFile(reference, targets, false, buffer, log);

            Assert.Throws<InvalidOperationException>(() => RegenerateAssetGuid.BuildPatches(
                targets, new List<RegenerateAssetGuid.FileState> { reference }, log));
        }

        [Test]
        public void CacheDoesNotHideUncachedFilesWithOldTimestamps()
        {
            // A moved or copied file keeps an old timestamp but is not in the cache.
            RegenerateAssetGuid.FileState moved = WriteFile("Assets/Moved/Ref.prefab", "guid: " + OldGuid);
            File.SetLastWriteTimeUtc(moved.FullPath, DateTime.UtcNow.AddDays(-30));
            moved.LastWriteUtcTicks = File.GetLastWriteTimeUtc(moved.FullPath).Ticks;

            RegenerateAssetGuid.Target target = NewTarget();
            target.Cache = new RegenerateAssetGuid.ReferenceCache
            {
                AssetPath = target.AssetPath,
                Guid = OldGuid,
                ScanStartedUtcTicks = DateTime.UtcNow.Ticks
            };

            RegenerateAssetGuid.ScanReferences(new List<RegenerateAssetGuid.Target> { target },
                new List<RegenerateAssetGuid.FileState> { moved }, buffer, log);

            Assert.That(target.Occurrences.ContainsKey(moved.RelativePath), Is.True);
        }

        [Test]
        public void FilesDifferingOnlyByCaseStopTheRun()
        {
            var files = new List<RegenerateAssetGuid.FileState>
            {
                new RegenerateAssetGuid.FileState { RelativePath = "Assets/Ref.prefab" },
                new RegenerateAssetGuid.FileState { RelativePath = "Assets/ref.prefab" },
                new RegenerateAssetGuid.FileState { RelativePath = "Assets/Zebra.prefab" }
            };
            Assert.Throws<InvalidOperationException>(() => RegenerateAssetGuid.ThrowIfCaseCollision(files));
            files.RemoveAt(1);
            Assert.DoesNotThrow(() => RegenerateAssetGuid.ThrowIfCaseCollision(files));
        }

        [Test]
        public void CachePathStaysShortAndUniqueForLongOrOddNames()
        {
            string longName = "Assets/" + new string('a', 200) + ".prefab";
            string other = "Assets/" + new string('a', 200) + "b.prefab";
            string cache = RegenerateAssetGuid.GetCachePath(directory, longName);
            Assert.That(Path.GetFileName(cache).Length, Is.LessThan(80));
            Assert.That(cache, Is.Not.EqualTo(RegenerateAssetGuid.GetCachePath(directory, other)));
            Assert.That(Path.GetFileName(RegenerateAssetGuid.GetCachePath(directory, "Assets/....prefab")),
                Does.StartWith("asset_"));
        }

        [Test]
        public void PathsCompareInComposedUnicode()
        {
            // "e" + combining acute accent (NFD, as macOS may report) equals the composed form.
            Assert.That(RegenerateAssetGuid.NormalizeAssetPath("Assets\\Cafe\u0301.mat"),
                Is.EqualTo("Assets/Caf\u00e9.mat"));
        }

        private RegenerateAssetGuid.Target NewTarget()
        {
            return new RegenerateAssetGuid.Target
            {
                AssetPath = "Assets/Target.asset",
                MetaRelativePath = MetaPath,
                OldGuid = OldGuid,
                NewGuid = NewGuid,
                OldBytes = Encoding.ASCII.GetBytes(OldGuid),
                NewBytes = Encoding.ASCII.GetBytes(NewGuid)
            };
        }

        private RegenerateAssetGuid.FileState WriteFile(string relativePath, string contents)
        {
            string fullPath = Path.Combine(directory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, contents, new UTF8Encoding(false));
            var info = new FileInfo(fullPath);
            return new RegenerateAssetGuid.FileState
            {
                FullPath = info.FullName,
                RelativePath = relativePath,
                Length = info.Length,
                LastWriteUtcTicks = info.LastWriteTimeUtc.Ticks
            };
        }
    }
}
#endif
