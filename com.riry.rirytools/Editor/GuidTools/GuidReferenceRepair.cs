using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>Missing GUIDs found in a set of files, with every candidate replacement found for each.</summary>
    internal sealed class RepairPlan
    {
        public readonly List<string> Files = new List<string>();
        /// <summary>File to the byte offset and value of each missing GUID in it.</summary>
        public readonly Dictionary<string, List<KeyValuePair<long, string>>> References =
            new Dictionary<string, List<KeyValuePair<long, string>>>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Missing GUID to the files that use it. Exact case: patches rewrite the bytes as found.</summary>
        public readonly SortedDictionary<string, SortedSet<string>> Missing =
            new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        /// <summary>Missing GUID to every replacement found, in the order the sources were searched.</summary>
        public readonly Dictionary<string, List<RepairCandidate>> Candidates =
            new Dictionary<string, List<RepairCandidate>>(StringComparer.Ordinal);
        public readonly Dictionary<string, string> LastKnownPaths = new Dictionary<string, string>(StringComparer.Ordinal);
        /// <summary>
        /// How many Fixes led to a missing GUID: 0 for one found in the scanned files,
        /// 1 for one a restored file needs, and so on.
        /// </summary>
        public readonly Dictionary<string, int> Depth = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// Breaks are repaired at most two levels deep in one go. A restore at the third
        /// level that still breaks something is a recursive break: references stay broken
        /// until the tool runs again.
        /// </summary>
        public const int MaxFixDepth = 2;

        public int DepthOf(string guid)
        {
            int depth;
            return Depth.TryGetValue(guid, out depth) ? depth : 0;
        }
    }

    internal enum BreakDecision
    {
        Undecided,
        Fix,
        Accept
    }

    /// <summary>
    /// One possible replacement for a missing GUID: an asset in the project to
    /// redirect to, or the missing asset restored from a Git commit.
    /// </summary>
    internal sealed class RepairCandidate
    {
        /// <summary>The asset to redirect to; null for a restore.</summary>
        public string TargetGuid;
        /// <summary>The target's asset path, or the path a restore brings the asset back to.</summary>
        public string Path;
        /// <summary>How it was found, one entry per source.</summary>
        public readonly List<string> Sources = new List<string>();

        // Restore only.
        public string Commit;
        public string RepoPath;
        /// <summary>GUIDs the restored file uses that have no asset now.</summary>
        public readonly List<string> MissingReferences = new List<string>();
        /// <summary>Another file is at Path now and would be overwritten.</summary>
        public bool PathTaken;
        /// <summary>The asset that has this GUID now, or null.</summary>
        public string GuidInUseBy;
        public BreakDecision MissingDecision;
        public BreakDecision PathDecision;
        public BreakDecision GuidDecision;

        public bool IsRestore
        {
            get { return Commit != null; }
        }

        /// <summary>A break was found that has no Fix applied (accepted or not decided); it stays broken.</summary>
        public bool Unfixed
        {
            get
            {
                return (MissingReferences.Count > 0 && MissingDecision != BreakDecision.Fix) ||
                    (PathTaken && PathDecision != BreakDecision.Fix) || GuidInUseBy != null;
            }
        }

        /// <summary>Where a restore writes: beside the taken path when that break is fixed.</summary>
        public string Destination
        {
            get { return PathTaken && PathDecision == BreakDecision.Fix ? AssetDatabase.GenerateUniqueAssetPath(Path) : Path; }
        }
    }

    /// <summary>
    /// Finds GUID references that no longer resolve in damaged or recovered scenes,
    /// prefabs and other text assets, suggests redirects from the remap ledger,
    /// GUID Tools logs and the Git history of .meta files, and
    /// rewrites the chosen ones byte-exactly with rollback.
    /// </summary>
    internal static class GuidReferenceRepair
    {
        internal const string Title = "Broken References";
        private static readonly byte[] GuidKey = Encoding.ASCII.GetBytes("guid: ");
        private static readonly Regex LogMapping = new Regex(
            @"GUID mapping prepared: (.*) \| ([0-9a-fA-F]{32}) -> ([0-9a-fA-F]{32})", RegexOptions.CultureInvariant);

        // Every branch, remote and reflog entry (Sync commits included), newest
        // first, for .meta files in the project folder.
        internal static readonly string[] HistoryArguments =
        {
            "log", "--all", "--reflog", "--topo-order", "--format=%H", "-p", "--unified=0", "--no-color",
            "--no-ext-diff", "--no-renames", "--", ":(glob)**/*.meta"
        };

        internal static void RepairSelected()
        {
            BrokenReferenceWindow.Open(SelectedFiles(), "Selected in the Project window.");
        }

        internal static RepairPlan Scan(IEnumerable<string> files, GuidToolLog log)
        {
            var plan = new RepairPlan();
            plan.Files.AddRange(files);
            var sample = new byte[4096];
            try
            {
                for (int i = 0; i < plan.Files.Count; i++)
                {
                    string fullPath = Path.Combine(RiryToolsPaths.ProjectRoot, plan.Files[i]);
                    if (EditorUtility.DisplayCancelableProgressBar(Title, "Scanning " + plan.Files[i], (float)i / plan.Files.Count))
                        throw new OperationCanceledException();
                    if (!File.Exists(fullPath) || !RegenerateGuid.LooksLikeText(fullPath, sample))
                    {
                        log.Write("Skipped missing or binary file: " + plan.Files[i]);
                        continue;
                    }
                    List<KeyValuePair<long, string>> broken = FindGuidReferences(File.ReadAllBytes(fullPath))
                        .FindAll(reference => IsMissing(reference.Value));
                    if (broken.Count == 0) continue;
                    plan.References[plan.Files[i]] = broken;
                    foreach (KeyValuePair<long, string> reference in broken)
                    {
                        SortedSet<string> users;
                        if (!plan.Missing.TryGetValue(reference.Value, out users))
                            plan.Missing[reference.Value] = users = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                        users.Add(plan.Files[i]);
                    }
                    log.Write("Missing references: " + plan.Files[i] + " | " + broken.Count);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            return plan;
        }

        /// <summary>
        /// Collects every replacement for each of <paramref name="guids"/> (all missing
        /// GUIDs when null) from every source: the ledger and its Git history, the
        /// GUID Tools logs, the Git history of .meta files (the asset now at the old
        /// path, and the old asset itself as a restore), and assets whose name matches
        /// the old asset's. Nothing is chosen here.
        /// </summary>
        internal static void Suggest(RepairPlan plan, ICollection<string> guids, GuidToolLog log)
        {
            var wanted = new HashSet<string>(guids ?? plan.Missing.Keys, StringComparer.Ordinal);
            if (wanted.Count == 0) return;
            var names = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var ledgerPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var logPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> ledger = GuidRemapLedger.Load();
            Dictionary<string, string> ledgerHistory = GuidRemapLedger.LoadWithHistory(log, ledgerPaths);
            Dictionary<string, string> logged = LoggedMappings(logPaths);
            Func<string, bool> exists = candidate => !IsMissing(candidate);

            foreach (string guid in wanted)
            {
                var found = new List<RepairCandidate>();
                AddRedirect(found, GuidRemapLedger.Resolve(guid, ledger, exists), "GUID match: remap ledger");
                AddRedirect(found, GuidRemapLedger.Resolve(guid, ledgerHistory, exists), "GUID match: remap ledger in Git history");
                AddRedirect(found, GuidRemapLedger.Resolve(guid, logged, exists), "GUID match: GUID Tools logs");
                plan.Candidates[guid] = found;
                names[guid] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                string path;
                if (ledgerPaths.TryGetValue(guid, out path)) names[guid].Add(path);
                if (logPaths.TryGetValue(guid, out path)) names[guid].Add(path);
            }

            Dictionary<string, MetaHistoryParser.Hit> history;
            string prefix = "";
            try
            {
                history = FindInHistory(wanted, log, out prefix);
            }
            catch (GitNotInstalledException exception)
            {
                log.Write("Git history not searched: " + exception.Message);
                history = new Dictionary<string, MetaHistoryParser.Hit>();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            foreach (KeyValuePair<string, MetaHistoryParser.Hit> entry in history)
            {
                string guid = entry.Key;
                plan.LastKnownPaths[guid] = entry.Value.Path;
                names[guid].Add(entry.Value.Path);
                string current = AssetDatabase.AssetPathToGUID(entry.Value.Path, AssetPathToGUIDOptions.OnlyExistingAssets);
                if (!string.IsNullOrEmpty(current) && current != guid)
                    AddRedirect(plan.Candidates[guid], current, "Git history: the asset now at " + entry.Value.Path);
                RepairCandidate restore = RestoreCandidate(guid, entry.Value, prefix, log);
                if (restore != null) plan.Candidates[guid].Add(restore);
            }

            string[] assets = AssetDatabase.GetAllAssetPaths();
            foreach (string guid in wanted)
            {
                foreach (string oldPath in names[guid]) AddNameMatches(plan.Candidates[guid], oldPath, assets, guid);
            }
        }

        private const int MaxNameMatches = 5;

        private static void AddRedirect(List<RepairCandidate> found, string target, string source)
        {
            if (string.IsNullOrEmpty(target)) return;
            RepairCandidate existing = found.Find(candidate => candidate.TargetGuid == target);
            if (existing == null)
            {
                existing = new RepairCandidate { TargetGuid = target, Path = AssetDatabase.GUIDToAssetPath(target) };
                found.Add(existing);
            }
            if (!existing.Sources.Contains(source)) existing.Sources.Add(source);
        }

        // Assets named like the old one: the same name first, then names starting with or
        // containing it (or contained in it); the same file type before others.
        private static void AddNameMatches(List<RepairCandidate> found, string oldPath, string[] assets, string guid)
        {
            string oldName = Path.GetFileNameWithoutExtension(oldPath);
            string oldExtension = Path.GetExtension(oldPath);
            if (oldName.Length == 0) return;
            var scored = new List<KeyValuePair<int, string>>();
            foreach (string asset in assets)
            {
                if (!asset.StartsWith("Assets/", StringComparison.Ordinal) || AssetDatabase.IsValidFolder(asset)) continue;
                string name = Path.GetFileNameWithoutExtension(asset);
                int score = string.Equals(name, oldName, StringComparison.OrdinalIgnoreCase) ? 4
                    : Math.Max(FuzzyMatch.Score(name, oldName), FuzzyMatch.Score(oldName, name));
                if (score < 2) continue;
                if (string.Equals(Path.GetExtension(asset), oldExtension, StringComparison.OrdinalIgnoreCase)) score += 10;
                scored.Add(new KeyValuePair<int, string>(score, asset));
            }
            scored.Sort((left, right) => left.Key != right.Key
                ? right.Key.CompareTo(left.Key)
                : string.Compare(left.Value, right.Value, StringComparison.OrdinalIgnoreCase));
            for (int i = 0; i < scored.Count && i < MaxNameMatches; i++)
            {
                string target = AssetDatabase.AssetPathToGUID(scored[i].Value);
                if (target != guid) AddRedirect(found, target, "Name match: \"" + Path.GetFileName(oldPath) + "\"");
            }
        }

        /// <summary>
        /// The missing asset as it was in Git, with what restoring only it would
        /// break: references in it to GUIDs with no asset, another file at its path,
        /// or another asset using its GUID. Null when the commit has no file there.
        /// </summary>
        private static RepairCandidate RestoreCandidate(string guid, MetaHistoryParser.Hit hit, string prefix, GuidToolLog log)
        {
            string project = RiryToolsPaths.ProjectRoot;
            string repoPath = prefix + hit.Path;
            if (GitRunner.Run(project, null, 30000, "cat-file", "-t", hit.Commit + ":" + repoPath).Output.Trim() != "blob")
            {
                log.Write("No restore for " + guid + ": " + hit.Commit + " has no file at " + repoPath);
                return null;
            }
            var candidate = new RepairCandidate
            {
                Path = hit.Path,
                Commit = hit.Commit,
                RepoPath = repoPath
            };
            candidate.Sources.Add("Git history backup: " + hit.Path + " at " + hit.Commit.Substring(0, Math.Min(8, hit.Commit.Length)));

            string size = GitRunner.Run(project, null, 30000, "cat-file", "-s", hit.Commit + ":" + repoPath).Output.Trim();
            long bytes;
            if (long.TryParse(size, out bytes) && bytes <= RegenerateGuid.ScanBlockSize * 64L)
            {
                string content = GitRunner.Run(project, null, 60000, "cat-file", "-p", hit.Commit + ":" + repoPath).Output;
                if (content.IndexOf('\0') < 0)
                {
                    foreach (KeyValuePair<long, string> reference in FindGuidReferences(Encoding.UTF8.GetBytes(content)))
                    {
                        if (reference.Value != guid && IsMissing(reference.Value) && !candidate.MissingReferences.Contains(reference.Value))
                            candidate.MissingReferences.Add(reference.Value);
                    }
                }
            }
            string fullPath = System.IO.Path.Combine(project, hit.Path);
            candidate.PathTaken = File.Exists(fullPath) || Directory.Exists(fullPath) || File.Exists(fullPath + ".meta");
            if (!IsMissing(guid)) candidate.GuidInUseBy = AssetDatabase.GUIDToAssetPath(guid);
            return candidate;
        }

        /// <summary>
        /// Adds the restored file's missing references to the plan, one level deeper
        /// than <paramref name="owner"/>, the missing GUID the restore is for, so each
        /// gets its own candidates (the Fix for that break). Returns the GUIDs added.
        /// </summary>
        internal static List<string> AddDependencies(RepairPlan plan, string owner, RepairCandidate restore, GuidToolLog log)
        {
            var added = new List<string>();
            foreach (string guid in restore.MissingReferences)
            {
                SortedSet<string> users;
                if (!plan.Missing.TryGetValue(guid, out users))
                {
                    plan.Missing[guid] = users = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                    plan.Depth[guid] = plan.DepthOf(owner) + 1;
                    added.Add(guid);
                }
                users.Add(restore.Path + " (restored)");
            }
            Suggest(plan, added, log);
            return added;
        }

        /// <summary>
        /// Old to new GUIDs from GUID Tools logs, including runs before the ledger
        /// existed; <paramref name="paths"/> collects each old GUID's asset path.
        /// </summary>
        private static Dictionary<string, string> LoggedMappings(Dictionary<string, string> paths)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string folder = RegenerateGuid.LogDirectoryPath;
            if (!Directory.Exists(folder)) return map;
            var logs = new List<string>(Directory.GetFiles(folder, "*.log", SearchOption.AllDirectories));
            // Oldest first, so a later run's mapping wins.
            logs.Sort((left, right) => File.GetLastWriteTimeUtc(left).CompareTo(File.GetLastWriteTimeUtc(right)));
            foreach (string file in logs)
            {
                using (var reader = new StreamReader(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        Match match = LogMapping.Match(line);
                        if (!match.Success) continue;
                        map[match.Groups[2].Value] = match.Groups[3].Value;
                        paths[match.Groups[2].Value] = match.Groups[1].Value;
                    }
                }
            }
            return map;
        }

        /// <summary>
        /// Restores the chosen assets from Git, then rewrites each missing GUID that
        /// has a redirect (in the scanned files and the restored ones), then reloads the
        /// open scenes from disk (discarding unsaved changes). Rolls everything back on
        /// failure. Returns the number of files changed.
        /// </summary>
        internal static int Apply(RepairPlan plan, Dictionary<string, string> redirects, List<RepairCandidate> restores,
            GuidToolLog log)
        {
            var written = new List<RegenerateGuid.WrittenOperation>();
            var restored = new List<RestoredFile>();
            SceneSetup[] sceneSetup = EditorSceneManager.GetSceneManagerSetup();
            bool committed = false;
            EditorApplication.LockReloadAssemblies();
            try
            {
                var files = new List<string>(plan.Files);
                foreach (RepairCandidate restore in restores)
                {
                    string destination = restore.Destination;
                    Restore(restore, destination, restored, log);
                    if (RegenerateGuid.UnitySerializedExtensions.Contains(Path.GetExtension(destination))) files.Add(destination);
                }
                if (restores.Count > 0) AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

                // Offsets are read again, so the restored files are rewritten too.
                RepairPlan current = restores.Count > 0 ? Scan(files, log) : plan;
                var targets = new Dictionary<string, RegenerateGuid.Target>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, string> entry in redirects)
                {
                    targets[entry.Key] = new RegenerateGuid.Target
                    {
                        AssetPath = AssetDatabase.GUIDToAssetPath(entry.Value),
                        OldGuid = entry.Key,
                        NewGuid = entry.Value,
                        OldBytes = Encoding.ASCII.GetBytes(entry.Key),
                        NewBytes = Encoding.ASCII.GetBytes(entry.Value)
                    };
                    log.Write("Redirect: " + entry.Key + " -> " + entry.Value + " (" + targets[entry.Key].AssetPath + ")");
                }

                var patches = new List<RegenerateGuid.FilePatch>();
                foreach (KeyValuePair<string, List<KeyValuePair<long, string>>> file in current.References)
                {
                    var info = new FileInfo(Path.Combine(RiryToolsPaths.ProjectRoot, file.Key));
                    var patch = new RegenerateGuid.FilePatch
                    {
                        File = new RegenerateGuid.FileState
                        {
                            FullPath = info.FullName,
                            RelativePath = file.Key,
                            Length = info.Length,
                            LastWriteUtcTicks = info.LastWriteTimeUtc.Ticks
                        }
                    };
                    foreach (KeyValuePair<long, string> reference in file.Value)
                    {
                        RegenerateGuid.Target target;
                        if (targets.TryGetValue(reference.Value, out target))
                            patch.Operations.Add(new RegenerateGuid.PatchOperation { Target = target, Offset = reference.Key });
                    }
                    if (patch.Operations.Count == 0) continue;
                    RegenerateGuid.EnsureWritable(info.FullName);
                    patches.Add(patch);
                }

                RegenerateGuid.ApplyPatches(patches, written, log);
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                committed = true;
                RegenerateGuid.ReloadScenes(sceneSetup, log);
                log.Write("Completed. Restored assets: " + restores.Count + "; redirected GUIDs: " + redirects.Count +
                    "; files changed: " + (patches.Count + restored.Count));
                return patches.Count + restored.Count;
            }
            catch (Exception)
            {
                if (!committed && (written.Count > 0 || restored.Count > 0))
                {
                    try
                    {
                        RegenerateGuid.RollBack(written, log);
                        UndoRestores(restored, log);
                        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                        RegenerateGuid.ReloadScenes(sceneSetup, log);
                    }
                    catch (Exception rollbackException)
                    {
                        log.Write("ROLLBACK FAILURE: " + rollbackException);
                        Debug.LogError(Title + ": rollback was incomplete:\n" + rollbackException);
                    }
                }
                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                EditorApplication.UnlockReloadAssemblies();
            }
        }

        /// <summary>A file a restore wrote, with what was there before (null when nothing was).</summary>
        private sealed class RestoredFile
        {
            public string FullPath;
            public byte[] Previous;
        }

        /// <summary>
        /// Writes the asset and its .meta from the candidate's commit to
        /// <paramref name="destination"/>, through Git's filters (so Git LFS files
        /// come back as their content). Files it replaces are kept for rollback.
        /// </summary>
        private static void Restore(RepairCandidate restore, string destination, List<RestoredFile> restored, GuidToolLog log)
        {
            string project = RiryToolsPaths.ProjectRoot;
            string folder = RiryToolsPaths.Library("GuidTools");
            Directory.CreateDirectory(folder);
            foreach (string suffix in new[] { "", ".meta" })
            {
                string temporary = Path.Combine(folder, "restore" + (suffix.Length > 0 ? "-meta" : ""));
                GitRunner.Result result = GitRunner.RunToFile(project, temporary, 600000, "cat-file", "--filters",
                    restore.Commit + ":" + restore.RepoPath + suffix);
                if (!result.Succeeded)
                    throw new InvalidOperationException(result.Describe("Restoring " + restore.RepoPath + suffix + " from " + restore.Commit));
                string target = Path.Combine(project, destination + suffix);
                restored.Add(new RestoredFile { FullPath = target, Previous = File.Exists(target) ? File.ReadAllBytes(target) : null });
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(temporary, target, true);
                File.Delete(temporary);
            }
            log.Write("Restored " + restore.RepoPath + " from " + restore.Commit + " to " + destination);
        }

        private static void UndoRestores(List<RestoredFile> restored, GuidToolLog log)
        {
            for (int i = restored.Count - 1; i >= 0; i--)
            {
                if (restored[i].Previous != null) File.WriteAllBytes(restored[i].FullPath, restored[i].Previous);
                else if (File.Exists(restored[i].FullPath)) File.Delete(restored[i].FullPath);
                log.Write("Rolled back restore: " + restored[i].FullPath);
            }
        }

        /// <summary>Selected text-serialized Unity files; folders are expanded recursively.</summary>
        internal static List<string> SelectedFiles()
        {
            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (UnityEngine.Object item in Selection.objects)
            {
                string path = RegenerateGuid.NormalizeAssetPath(AssetDatabase.GetAssetPath(item));
                if (string.IsNullOrEmpty(path) || !(path == "Assets" || path.StartsWith("Assets/", StringComparison.Ordinal)))
                    continue;
                string[] paths = AssetDatabase.IsValidFolder(path)
                    ? Array.ConvertAll(AssetDatabase.FindAssets("", new[] { path }), AssetDatabase.GUIDToAssetPath)
                    : new[] { path };
                foreach (string candidate in paths)
                {
                    if (RegenerateGuid.UnitySerializedExtensions.Contains(Path.GetExtension(candidate)))
                        files.Add(RegenerateGuid.NormalizeAssetPath(candidate));
                }
            }
            var sorted = new List<string>(files);
            sorted.Sort(StringComparer.OrdinalIgnoreCase);
            return sorted;
        }

        /// <summary>A GUID with no asset behind it. Built-in resources (16 leading zeros) never count.</summary>
        internal static bool IsMissing(string guid)
        {
            if (guid.StartsWith("0000000000000000", StringComparison.Ordinal)) return false;
            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ||
                string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets));
        }

        /// <summary>Byte offset and value of every 32-digit GUID written as "guid: " in serialized YAML.</summary>
        internal static List<KeyValuePair<long, string>> FindGuidReferences(byte[] bytes)
        {
            var found = new List<KeyValuePair<long, string>>();
            for (int i = 0; i + GuidKey.Length + RegenerateGuid.GuidLength <= bytes.Length; i++)
            {
                if (!StartsWith(bytes, i, GuidKey) || (i > 0 && IsWordByte(bytes[i - 1]))) continue;
                int start = i + GuidKey.Length;
                bool valid = start + RegenerateGuid.GuidLength == bytes.Length ||
                    !IsHex(bytes[start + RegenerateGuid.GuidLength]);
                for (int j = start; valid && j < start + RegenerateGuid.GuidLength; j++) valid = IsHex(bytes[j]);
                if (!valid) continue;
                found.Add(new KeyValuePair<long, string>(start,
                    Encoding.ASCII.GetString(bytes, start, RegenerateGuid.GuidLength)));
                i = start + RegenerateGuid.GuidLength - 1;
            }
            return found;
        }

        private static Dictionary<string, MetaHistoryParser.Hit> FindInHistory(HashSet<string> guids, GuidToolLog log,
            out string prefix)
        {
            string projectRoot = RiryToolsPaths.ProjectRoot;
            GitRunner.Result location = GitRunner.Run(projectRoot, null, 30000, "rev-parse", "--show-prefix");
            prefix = location.Output.Trim();
            if (!location.Succeeded)
            {
                log.Write("Git history not searched: " + location.Describe("Locating the Git repository"));
                return new Dictionary<string, MetaHistoryParser.Hit>();
            }

            var parser = new MetaHistoryParser(guids, prefix);
            bool cancelled = false;
            GitRunner.Result result = GitRunner.Stream(projectRoot, HistoryArguments, parser.Feed,
                () => parser.Complete || (cancelled = EditorUtility.DisplayCancelableProgressBar(Title,
                    "Searching Git history: " + parser.Found.Count + " of " + guids.Count + " found", 0.6f)));
            if (cancelled) throw new OperationCanceledException();
            if (!result.Succeeded && !parser.Complete) log.Write(result.Describe("Searching Git history"));
            return parser.Found;
        }

        private static bool StartsWith(byte[] bytes, int offset, byte[] token)
        {
            for (int i = 0; i < token.Length; i++)
            {
                if (bytes[offset + i] != token[i]) return false;
            }
            return true;
        }

        private static bool IsHex(byte value)
        {
            return (value >= '0' && value <= '9') || (value >= 'a' && value <= 'f') || (value >= 'A' && value <= 'F');
        }

        private static bool IsWordByte(byte value)
        {
            return IsHex(value) || (value >= 'g' && value <= 'z') || (value >= 'G' && value <= 'Z') || value == '_';
        }
    }

    /// <summary>
    /// Reads `git log -p` output for .meta files, newest first, and keeps the most
    /// recent project path that held each wanted GUID, with a commit that has it.
    /// </summary>
    internal sealed class MetaHistoryParser
    {
        internal struct Hit
        {
            public string Path;
            /// <summary>A commit holding the .meta with that GUID: the commit that added it, or the parent of the one that removed it.</summary>
            public string Commit;
        }

        private static readonly Regex CommitLine = new Regex("^[0-9a-f]{40}([0-9a-f]{24})?$", RegexOptions.CultureInvariant);
        private readonly HashSet<string> wanted;
        private readonly string prefix;
        private string commit;
        private string path;
        internal readonly Dictionary<string, Hit> Found = new Dictionary<string, Hit>(StringComparer.OrdinalIgnoreCase);

        /// <param name="prefix">The project folder relative to the repository root, such as "Game/" or "".</param>
        internal MetaHistoryParser(HashSet<string> wanted, string prefix)
        {
            this.wanted = wanted;
            this.prefix = prefix;
        }

        internal bool Complete
        {
            get { return Found.Count == wanted.Count; }
        }

        internal void Feed(string line)
        {
            if (CommitLine.IsMatch(line))
            {
                commit = line;
                path = null;
            }
            else if (line.StartsWith("diff --git ", StringComparison.Ordinal)) path = null;
            // Git ends a header path that contains a space with a tab.
            else if (line.StartsWith("--- a/", StringComparison.Ordinal) || line.StartsWith("+++ b/", StringComparison.Ordinal))
                path = line.Substring(6).TrimEnd('\t');
            else if (path != null && commit != null &&
                     (line.StartsWith("+guid: ", StringComparison.Ordinal) || line.StartsWith("-guid: ", StringComparison.Ordinal)))
            {
                string guid = line.Substring(7).Trim();
                if (wanted.Contains(guid) && !Found.ContainsKey(guid) &&
                    path.StartsWith(prefix, StringComparison.Ordinal) && path.EndsWith(".meta", StringComparison.Ordinal))
                    Found[guid] = new Hit
                    {
                        Path = path.Substring(prefix.Length, path.Length - prefix.Length - ".meta".Length),
                        Commit = line[0] == '+' ? commit : commit + "^"
                    };
            }
        }
    }
}
