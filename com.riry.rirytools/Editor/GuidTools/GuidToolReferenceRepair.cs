using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>Missing GUIDs found in a set of files, with every candidate replacement found for each.</summary>
    internal sealed class GuidToolRepairPlan
    {
        public readonly List<string> Files = new List<string>();
        /// <summary>File to the byte offset and value of each missing GUID in it.</summary>
        public readonly Dictionary<string, List<KeyValuePair<long, string>>> References =
            new Dictionary<string, List<KeyValuePair<long, string>>>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Missing GUID to the files that use it. Exact case: patches rewrite the bytes as found.</summary>
        public readonly SortedDictionary<string, SortedSet<string>> Missing =
            new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        /// <summary>
        /// Objects that are gone from an asset that still exists (a renamed mesh, a removed sub-material): "path (file ID n)"
        /// to the files that use it. Their GUID is fine, so they are listed, not redirected.
        /// </summary>
        public readonly SortedDictionary<string, SortedSet<string>> MissingObjects =
            new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        /// <summary>Missing GUID to every replacement found, in the order the sources were searched.</summary>
        public readonly Dictionary<string, List<GuidToolRepairCandidate>> Candidates =
            new Dictionary<string, List<GuidToolRepairCandidate>>(StringComparer.Ordinal);
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

    /// <summary>
    /// Candidates found so far for every missing GUID, kept between passes of one
    /// Broken References window, so a GUID's sources are searched only once.
    /// </summary>
    internal sealed class GuidToolRepairCache
    {
        public readonly Dictionary<string, List<GuidToolRepairCandidate>> Candidates =
            new Dictionary<string, List<GuidToolRepairCandidate>>(StringComparer.Ordinal);
        public readonly Dictionary<string, string> LastKnownPaths = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    internal enum GuidToolBreakDecision
    {
        Undecided,
        Fix,
        Accept
    }

    /// <summary>
    /// One possible replacement for a missing GUID: an asset in the project to
    /// redirect to, or the missing asset restored from a Git commit.
    /// </summary>
    internal sealed class GuidToolRepairCandidate
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
        /// <summary>Another file is at Path now; the restore goes beside it (Destination) and never overwrites it.</summary>
        public bool PathTaken;
        /// <summary>The asset that has this GUID now, or null.</summary>
        public string GuidInUseBy;
        public GuidToolBreakDecision MissingDecision;
        public GuidToolBreakDecision GuidDecision;

        public bool IsRestore
        {
            get { return Commit != null; }
        }

        /// <summary>A break was found that has no Fix applied (accepted or not decided); it stays broken.</summary>
        public bool Unfixed
        {
            get
            {
                return (MissingReferences.Count > 0 && MissingDecision != GuidToolBreakDecision.Fix) || GuidInUseBy != null;
            }
        }

        /// <summary>
        /// Where a restore writes: Path, or a free name beside it when Path is taken. The restored .meta keeps the missing
        /// GUID, so the references follow the restored file wherever it lands; the file at Path is never touched.
        /// </summary>
        public string Destination
        {
            // Worked out when asked, so a restore made earlier in the same Apply counts as taking its path.
            get { return GuidToolReferenceRepair.FreePath(Path); }
        }
    }

    /// <summary>
    /// Finds asset GUID references (not scripts) that no longer resolve in damaged or
    /// recovered scenes, prefabs and other text assets, suggests redirects from the remap ledger,
    /// GUID Tools logs and the Git history of .meta files, and
    /// rewrites the chosen ones byte-exactly with rollback.
    /// </summary>
    internal static class GuidToolReferenceRepair
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

        // Also in the Console, once per reason and session: without it, no Git backups or old paths are offered.
        private static readonly HashSet<string> historyReported = new HashSet<string>(StringComparer.Ordinal);

        private static void HistoryNotSearched(string reason)
        {
            if (historyReported.Add(reason))
                RiryLog.Warn(RiryLog.GuidTools, "Broken References did not search the Git history, so no Git backups or old paths are " +
                    "offered: " + reason);
        }

        internal static void RepairSelected()
        {
            GuidToolBrokenReferenceWindow.Open(SelectedFiles(), "Selected in the Project window.", true);
        }

        /// <summary>
        /// Scans the files and what they use. <paramref name="intoControllers"/> (the Project window's Repair Missing GUID
        /// References) also opens animator controllers and what they use; otherwise the walk stops at a controller, which is
        /// only checked for being present where it is referenced.
        /// </summary>
        internal static GuidToolRepairPlan Scan(IEnumerable<string> files, GuidToolLog log, bool intoControllers = false)
        {
            var plan = new GuidToolRepairPlan();
            plan.Files.AddRange(files);
            // What the files use is scanned too: a material's shader lives in the .mat, a nested prefab's references in its
            // own .prefab, not in the scene.
            var listed = new HashSet<string>(plan.Files, StringComparer.OrdinalIgnoreCase);
            var pending = new Queue<string>(plan.Files.Where(file => intoControllers || !IsController(file)));
            while (pending.Count > 0)
            {
                foreach (string dependency in AssetDatabase.GetDependencies(pending.Dequeue(), false))
                {
                    if (!dependency.StartsWith("Assets/", StringComparison.Ordinal) || (!intoControllers && IsController(dependency)) || !listed.Add(dependency)) continue;
                    plan.Files.Add(dependency);
                    pending.Enqueue(dependency);
                }
            }
            var objectIds = new Dictionary<string, HashSet<long>>(StringComparer.Ordinal);
            var sample = new byte[4096];
            try
            {
                for (int i = 0; i < plan.Files.Count; i++)
                {
                    string fullPath = Path.Combine(RiryToolsPaths.ProjectRoot, plan.Files[i]);
                    if (EditorUtility.DisplayCancelableProgressBar(Title, "Scanning " + plan.Files[i], (float)i / plan.Files.Count))
                        throw new OperationCanceledException();
                    // Outside the Project window's repair a controller is never opened: only whether it is present is checked.
                    if (!intoControllers && IsController(plan.Files[i]))
                    {
                        log.Write("Skipped animator controller: " + plan.Files[i]);
                        continue;
                    }
                    if (!File.Exists(fullPath) || !GuidToolRegenerate.LooksLikeText(fullPath, sample))
                    {
                        log.Write("Skipped missing or binary file: " + plan.Files[i]);
                        continue;
                    }
                    byte[] bytes = File.ReadAllBytes(fullPath);
                    List<KeyValuePair<long, string>> references = FindGuidReferences(bytes);
                    // Not listed: empty slots, prefab bookkeeping, and anything in a controller's unused sub-assets (Controller
                    // Cleaner removes those).
                    HashSet<long> unused = IsController(plan.Files[i]) ? UnusedIds(plan.Files[i]) : null;
                    references = references.FindAll(reference => !Nonsense(bytes, reference, unused));
                    List<KeyValuePair<long, string>> broken = references.FindAll(reference => IsMissing(reference.Value));
                    // A file's references to its own missing parts are not listed: nothing outside the file is involved.
                    string self = AssetDatabase.AssetPathToGUID(plan.Files[i]);
                    foreach (KeyValuePair<long, string> reference in references)
                    {
                        if (IsMissing(reference.Value) || reference.Value == self) continue;
                        string gone = MissingObject(bytes, reference, objectIds);
                        if (gone == null) continue;
                        SortedSet<string> users;
                        if (!plan.MissingObjects.TryGetValue(gone, out users))
                            plan.MissingObjects[gone] = users = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                        users.Add(plan.Files[i]);
                    }
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
        /// Gives the plan every replacement for each of <paramref name="guids"/> (all
        /// missing GUIDs when null), then follows every Git restore's missing references,
        /// and theirs, to the end, so everything a repair could break is found and
        /// cached now; those deeper GUIDs get candidates and a depth but are listed only
        /// once a Fix or a later pass adds them. Sources are searched once per GUID per
        /// window (<paramref name="cache"/>). Nothing is chosen here.
        /// </summary>
        internal static void Suggest(GuidToolRepairPlan plan, ICollection<string> guids, GuidToolRepairCache cache, GuidToolLog log)
        {
            var level = new HashSet<string>(guids ?? plan.Missing.Keys, StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            while (level.Count > 0)
            {
                var fresh = new HashSet<string>(StringComparer.Ordinal);
                foreach (string guid in level)
                {
                    if (!cache.Candidates.ContainsKey(guid)) fresh.Add(guid);
                }
                if (fresh.Count > 0) Collect(fresh, cache, log);
                var next = new HashSet<string>(StringComparer.Ordinal);
                foreach (string guid in level)
                {
                    seen.Add(guid);
                    if (!plan.Candidates.ContainsKey(guid))
                    {
                        // A candidate from an earlier pass is checked again: the project has changed since.
                        Refresh(cache.Candidates[guid]);
                        plan.Candidates[guid] = cache.Candidates[guid];
                        string path;
                        if (cache.LastKnownPaths.TryGetValue(guid, out path)) plan.LastKnownPaths[guid] = path;
                    }
                    foreach (GuidToolRepairCandidate candidate in plan.Candidates[guid])
                    {
                        foreach (string dependency in candidate.MissingReferences)
                        {
                            int depth = plan.DepthOf(guid) + 1;
                            if (!plan.Depth.ContainsKey(dependency) && !plan.Missing.ContainsKey(dependency)) plan.Depth[dependency] = depth;
                            if (!seen.Contains(dependency) && !level.Contains(dependency)) next.Add(dependency);
                        }
                    }
                }
                level = next;
            }
        }

        // Drops redirects to assets that are gone, and checks restores again with their choices cleared.
        private static void Refresh(List<GuidToolRepairCandidate> candidates)
        {
            candidates.RemoveAll(candidate => !candidate.IsRestore && IsMissing(candidate.TargetGuid));
            foreach (GuidToolRepairCandidate candidate in candidates)
            {
                if (!candidate.IsRestore) continue;
                candidate.MissingReferences.RemoveAll(guid => !IsMissing(guid));
                string fullPath = System.IO.Path.Combine(RiryToolsPaths.ProjectRoot, candidate.Path);
                candidate.PathTaken = File.Exists(fullPath) || Directory.Exists(fullPath) || File.Exists(fullPath + ".meta");
                candidate.MissingDecision = candidate.GuidDecision = GuidToolBreakDecision.Undecided;
            }
        }

        /// <summary>
        /// Searches every source for each of <paramref name="wanted"/> into the cache:
        /// the ledger and its Git history, the GUID Tools logs, the Git history of .meta
        /// files (the asset now at the old path, and the old asset itself as a restore),
        /// and assets whose name matches the old asset's.
        /// </summary>
        private static void Collect(HashSet<string> wanted, GuidToolRepairCache cache, GuidToolLog log)
        {
            var names = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var ledgerPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var logPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> ledger = GuidToolRemapLedger.Load();
            Dictionary<string, string> ledgerHistory = GuidToolRemapLedger.LoadWithHistory(log, ledgerPaths);
            Dictionary<string, string> logged = LoggedMappings(logPaths);
            Func<string, bool> exists = candidate => !IsMissing(candidate);

            foreach (string guid in wanted)
            {
                var found = new List<GuidToolRepairCandidate>();
                AddRedirect(found, GuidToolRemapLedger.Resolve(guid, ledger, exists), "GUID match: remap ledger");
                AddRedirect(found, GuidToolRemapLedger.Resolve(guid, ledgerHistory, exists), "GUID match: remap ledger in Git history");
                AddRedirect(found, GuidToolRemapLedger.Resolve(guid, logged, exists), "GUID match: GUID Tools logs");
                cache.Candidates[guid] = found;
                names[guid] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                string path;
                if (ledgerPaths.TryGetValue(guid, out path)) names[guid].Add(path);
                if (logPaths.TryGetValue(guid, out path)) names[guid].Add(path);
            }

            Dictionary<string, GuidToolMetaHistoryParser.Hit> history;
            string prefix = "";
            try
            {
                history = FindInHistory(wanted, log, out prefix);
            }
            catch (GitNotInstalledException exception)
            {
                log.Write("Git history not searched: " + exception.Message);
                HistoryNotSearched(exception.Message);
                history = new Dictionary<string, GuidToolMetaHistoryParser.Hit>();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            foreach (KeyValuePair<string, GuidToolMetaHistoryParser.Hit> entry in history)
            {
                string guid = entry.Key;
                cache.LastKnownPaths[guid] = entry.Value.Path;
                names[guid].Add(entry.Value.Path);
                string current = AssetDatabase.AssetPathToGUID(entry.Value.Path, AssetPathToGUIDOptions.OnlyExistingAssets);
                if (!string.IsNullOrEmpty(current) && current != guid)
                    AddRedirect(cache.Candidates[guid], current, "Git history: the asset now at " + entry.Value.Path);
                GuidToolRepairCandidate restore = RestoreCandidate(guid, entry.Value, prefix, log);
                if (restore != null) cache.Candidates[guid].Add(restore);
            }

            string[] assets = AssetDatabase.GetAllAssetPaths();
            foreach (string guid in wanted)
            {
                foreach (string oldPath in names[guid]) AddNameMatches(cache.Candidates[guid], oldPath, assets, guid);
            }
        }

        private const int MaxNameMatches = 5;

        private static void AddRedirect(List<GuidToolRepairCandidate> found, string target, string source)
        {
            if (string.IsNullOrEmpty(target)) return;
            GuidToolRepairCandidate existing = found.Find(candidate => candidate.TargetGuid == target);
            if (existing == null)
            {
                existing = new GuidToolRepairCandidate { TargetGuid = target, Path = AssetDatabase.GUIDToAssetPath(target) };
                found.Add(existing);
            }
            if (!existing.Sources.Contains(source)) existing.Sources.Add(source);
        }

        // Assets named like the old one: the same name first, then names starting with or
        // containing it (or contained in it); the same file type before others.
        private static void AddNameMatches(List<GuidToolRepairCandidate> found, string oldPath, string[] assets, string guid)
        {
            string oldName = Path.GetFileNameWithoutExtension(oldPath);
            string oldExtension = Path.GetExtension(oldPath);
            if (oldName.Length == 0) return;
            var scored = new List<KeyValuePair<int, string>>();
            foreach (string asset in assets)
            {
                if (!asset.StartsWith("Assets/", StringComparison.Ordinal) || AssetDatabase.IsValidFolder(asset) ||
                    asset.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || asset.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    continue;
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
        private static GuidToolRepairCandidate RestoreCandidate(string guid, GuidToolMetaHistoryParser.Hit hit, string prefix, GuidToolLog log)
        {
            string project = RiryToolsPaths.ProjectRoot;
            string repoPath = prefix + hit.Path;
            if (GitRunner.Run(project, null, 30000, "cat-file", "-t", hit.Commit + ":" + repoPath).Output.Trim() != "blob")
            {
                log.Write("No restore for " + guid + ": " + hit.Commit + " has no file at " + repoPath);
                return null;
            }
            var candidate = new GuidToolRepairCandidate
            {
                Path = hit.Path,
                Commit = hit.Commit,
                RepoPath = repoPath
            };
            candidate.Sources.Add("Git history backup: " + hit.Path + " at " + hit.Commit.Substring(0, Math.Min(8, hit.Commit.Length)));

            string size = GitRunner.Run(project, null, 30000, "cat-file", "-s", hit.Commit + ":" + repoPath).Output.Trim();
            long bytes;
            if (long.TryParse(size, out bytes) && bytes <= GuidToolRegenerate.ScanBlockSize * 64L)
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
        internal static List<string> AddDependencies(GuidToolRepairPlan plan, string owner, GuidToolRepairCandidate restore, GuidToolRepairCache cache,
            GuidToolLog log)
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
            Suggest(plan, added, cache, log);
            return added;
        }

        /// <summary>
        /// Old to new GUIDs from GUID Tools logs, including runs before the ledger
        /// existed; <paramref name="paths"/> collects each old GUID's asset path.
        /// </summary>
        private static Dictionary<string, string> LoggedMappings(Dictionary<string, string> paths)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string folder = GuidToolRegenerate.LogDirectoryPath;
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
        /// failure. Returns the number of files changed; <paramref name="restoredFiles"/>
        /// receives the asset paths restored.
        /// </summary>
        internal static int Apply(GuidToolRepairPlan plan, Dictionary<string, string> redirects, List<GuidToolRepairCandidate> restores,
            List<string> restoredFiles, GuidToolLog log)
        {
            var written = new List<GuidToolRegenerate.WrittenOperation>();
            var restored = new List<RestoredFile>();
            SceneSetup[] sceneSetup = EditorSceneManager.GetSceneManagerSetup();
            bool committed = false;
            EditorApplication.LockReloadAssemblies();
            try
            {
                var files = new List<string>(plan.Files);
                foreach (GuidToolRepairCandidate restore in restores)
                {
                    string destination = restore.Destination;
                    Restore(restore, destination, restored, log);
                    restoredFiles.Add(destination);
                    if (GuidToolRegenerate.UnitySerializedExtensions.Contains(Path.GetExtension(destination))) files.Add(destination);
                }
                if (restores.Count > 0) AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

                // Offsets are read again, so the restored files are rewritten too.
                GuidToolRepairPlan current = restores.Count > 0 ? Scan(files, log) : plan;
                var targets = new Dictionary<string, GuidToolRegenerate.Target>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, string> entry in redirects)
                {
                    targets[entry.Key] = new GuidToolRegenerate.Target
                    {
                        AssetPath = AssetDatabase.GUIDToAssetPath(entry.Value),
                        OldGuid = entry.Key,
                        NewGuid = entry.Value,
                        OldBytes = Encoding.ASCII.GetBytes(entry.Key),
                        NewBytes = Encoding.ASCII.GetBytes(entry.Value)
                    };
                    log.Write("Redirect: " + entry.Key + " -> " + entry.Value + " (" + targets[entry.Key].AssetPath + ")");
                }

                var patches = new List<GuidToolRegenerate.FilePatch>();
                foreach (KeyValuePair<string, List<KeyValuePair<long, string>>> file in current.References)
                {
                    var info = new FileInfo(Path.Combine(RiryToolsPaths.ProjectRoot, file.Key));
                    var patch = new GuidToolRegenerate.FilePatch
                    {
                        File = new GuidToolRegenerate.FileState
                        {
                            FullPath = info.FullName,
                            RelativePath = file.Key,
                            Length = info.Length,
                            LastWriteUtcTicks = info.LastWriteTimeUtc.Ticks
                        }
                    };
                    foreach (KeyValuePair<long, string> reference in file.Value)
                    {
                        GuidToolRegenerate.Target target;
                        if (targets.TryGetValue(reference.Value, out target))
                            patch.Operations.Add(new GuidToolRegenerate.PatchOperation { Target = target, Offset = reference.Key });
                    }
                    if (patch.Operations.Count == 0) continue;
                    GuidToolRegenerate.EnsureWritable(info.FullName);
                    patches.Add(patch);
                }

                GuidToolRegenerate.ApplyPatches(patches, written, log);
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                committed = true;
                GuidToolRegenerate.ReloadScenes(sceneSetup, log);
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
                        GuidToolRegenerate.RollBack(written, log);
                        UndoRestores(restored, log);
                        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                        GuidToolRegenerate.ReloadScenes(sceneSetup, log);
                    }
                    catch (Exception rollbackException)
                    {
                        log.Write("ROLLBACK FAILURE: " + rollbackException);
                        RiryLog.Exception(RiryLog.GuidTools, Title + ": rollback was incomplete", rollbackException);
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

        /// <summary>
        /// A free asset path beside <paramref name="path"/>: Unity's unique name, then checked on disk (the file, a folder
        /// and a .meta, also one left without its asset), numbering on until nothing is there.
        /// </summary>
        internal static string FreePath(string path)
        {
            string free = AssetDatabase.GenerateUniqueAssetPath(path);
            if (string.IsNullOrEmpty(free)) free = path;
            string folder = (Path.GetDirectoryName(path) ?? "").Replace('\\', '/');
            string name = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);
            for (int number = 1; Taken(free); number++) free = (folder.Length > 0 ? folder + "/" : "") + name + " " + number + extension;
            return free;
        }

        private static bool Taken(string assetPath)
        {
            string fullPath = Path.Combine(RiryToolsPaths.ProjectRoot, assetPath);
            return File.Exists(fullPath) || Directory.Exists(fullPath) || File.Exists(fullPath + ".meta");
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
        /// come back as their content). Never overwrites: a file already at the
        /// destination stops the restore (before it is listed for rollback, so
        /// rollback never deletes it).
        /// </summary>
        private static void Restore(GuidToolRepairCandidate restore, string destination, List<RestoredFile> restored, GuidToolLog log)
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
                if (File.Exists(target) || Directory.Exists(target))
                {
                    File.Delete(temporary);
                    throw new InvalidOperationException("Restoring " + restore.RepoPath + suffix + ": " + destination + suffix +
                        " is taken by another file, so nothing was overwritten.");
                }
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(temporary, target, false);
                restored.Add(new RestoredFile { FullPath = target, Previous = null });
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
                string path = GuidToolRegenerate.NormalizeAssetPath(AssetDatabase.GetAssetPath(item));
                if (string.IsNullOrEmpty(path) || !(path == "Assets" || path.StartsWith("Assets/", StringComparison.Ordinal)))
                    continue;
                string[] paths = AssetDatabase.IsValidFolder(path)
                    ? Array.ConvertAll(AssetDatabase.FindAssets("", new[] { path }), AssetDatabase.GUIDToAssetPath)
                    : new[] { path };
                foreach (string candidate in paths)
                {
                    if (GuidToolRegenerate.UnitySerializedExtensions.Contains(Path.GetExtension(candidate)))
                        files.Add(GuidToolRegenerate.NormalizeAssetPath(candidate));
                }
            }
            var sorted = new List<string>(files);
            sorted.Sort(StringComparer.OrdinalIgnoreCase);
            return sorted;
        }

        // A reference that changes nothing: an empty slot (file ID 0), prefab bookkeeping (the source prefab itself is kept,
        // a missing one is real), or one inside a sub-asset of the controller that nothing uses.
        private static bool Nonsense(byte[] bytes, KeyValuePair<long, string> reference, HashSet<long> unused)
        {
            if (FileIdBefore(bytes, (int)reference.Key, out long fileId) && fileId == 0) return true;
            if (PrefabBookkeeping(bytes, (int)reference.Key, false)) return true;
            return unused != null && unused.Count > 0 && DocumentId(bytes, (int)reference.Key, out long owner) && unused.Contains(owner);
        }

        // The file IDs of a controller's unused sub-assets (ControllerCleanerScan).
        private static HashSet<long> UnusedIds(string path)
        {
            var ids = new HashSet<long>();
            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(path);
            if (controller == null) return ids;
            foreach (UnityEngine.Object item in ControllerCleanerScan.Scan(controller).Unused)
            {
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(item, out string guid, out long id)) ids.Add(id);
            }
            return ids;
        }

        // The file ID of the YAML document ("--- !u!<class> &<id>") the offset is in.
        private static bool DocumentId(byte[] bytes, int offset, out long id)
        {
            id = 0;
            for (int i = offset; i >= 4; i--)
            {
                if (bytes[i] != (byte)'-' || bytes[i - 1] != (byte)'-' || bytes[i - 2] != (byte)'-' || (i > 2 && bytes[i - 3] != (byte)'\n')) continue;
                int end = i;
                while (end < bytes.Length && bytes[end] != (byte)'\n') end++;
                Match match = Regex.Match(Encoding.ASCII.GetString(bytes, i, end - i), @"&(-?\d+)");
                return match.Success && long.TryParse(match.Groups[1].Value, out id);
            }
            return false;
        }

        private static bool IsController(string path)
        {
            Type type = AssetDatabase.GetMainAssetTypeAtPath(path);
            return type != null && typeof(RuntimeAnimatorController).IsAssignableFrom(type);
        }

        // The prefab asset itself as a nested prefab's source (m_SourcePrefab): not one of the objects LoadAllAssetsAtPath gives.
        private const long PrefabAssetFileId = 100100000;

        /// <summary>
        /// "path (file ID n)" when the reference's file ID is not one of its asset's objects any more; null when it is, when
        /// it has none (0), or when the asset's objects can't be loaded.
        /// </summary>
        private static string MissingObject(byte[] bytes, KeyValuePair<long, string> reference, Dictionary<string, HashSet<long>> objectIds)
        {
            long fileId;
            if (reference.Value.StartsWith("0000000000000000", StringComparison.Ordinal) || PrefabBookkeeping(bytes, (int)reference.Key)) return null;
            if (!FileIdBefore(bytes, (int)reference.Key, out fileId) || fileId == 0 || fileId == PrefabAssetFileId) return null;
            HashSet<long> ids;
            string path = AssetDatabase.GUIDToAssetPath(reference.Value);
            if (!objectIds.TryGetValue(reference.Value, out ids))
            {
                ids = new HashSet<long>();
                foreach (UnityEngine.Object item in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (item != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(item, out string guid, out long id)) ids.Add(id);
                }
                objectIds[reference.Value] = ids;
            }
            return ids.Count == 0 || ids.Contains(fileId) ? null : path + " (file ID " + fileId + ")";
        }

        // Keys of a prefab instance's bookkeeping: which model or prefab object an override or a stripped object stands
        // for. When the model is reimported without that object, Unity drops these quietly; nothing in the build uses them.
        private static readonly HashSet<string> BookkeepingKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "target", "m_CorrespondingSourceObject", "targetCorrespondingSourceObject", "m_SourcePrefab"
        };

        private static readonly HashSet<string> BookkeepingLists = new HashSet<string>(StringComparer.Ordinal)
        {
            "m_RemovedComponents", "m_RemovedGameObjects", "m_AddedGameObjects", "m_AddedComponents"
        };

        /// <summary>
        /// Whether the reference is prefab bookkeeping (an override's target, a stripped object's source, an entry of a
        /// removed or added list) rather than a value an object uses.
        /// </summary>
        private static bool PrefabBookkeeping(byte[] bytes, int guidStart, bool sourcePrefab = true)
        {
            int lineStart = guidStart;
            while (lineStart > 0 && bytes[lineStart - 1] != (byte)'\n') lineStart--;
            string line = Encoding.ASCII.GetString(bytes, lineStart, guidStart - lineStart).TrimStart();
            bool listItem = line.StartsWith("- ", StringComparison.Ordinal);
            if (listItem) line = line.Substring(2).TrimStart();
            int colon = line.IndexOf(':');
            if (colon > 0 && !line.StartsWith("{", StringComparison.Ordinal) && BookkeepingKeys.Contains(line.Substring(0, colon)))
                return sourcePrefab || line.Substring(0, colon) != "m_SourcePrefab";
            if (!listItem) return false;
            // A bare "- {fileID: ...}" item: the key is the nearest line above that is not an item.
            int end = lineStart - 1;
            while (end > 0)
            {
                int start = end;
                while (start > 0 && bytes[start - 1] != (byte)'\n') start--;
                string above = Encoding.ASCII.GetString(bytes, start, end - start).Trim();
                if (!above.StartsWith("- ", StringComparison.Ordinal) && !above.StartsWith("{", StringComparison.Ordinal) && above.Length > 0)
                    return above.EndsWith(":", StringComparison.Ordinal) && BookkeepingLists.Contains(above.TrimEnd(':'));
                end = start - 1;
            }
            return false;
        }

        // The "fileID: n" written before "guid: " in the same {fileID: n, guid: ..., type: t} reference.
        private static bool FileIdBefore(byte[] bytes, int guidStart, out long fileId)
        {
            fileId = 0;
            int open = guidStart;
            while (open > 0 && bytes[open] != (byte)'{' && bytes[open] != (byte)'\n') open--;
            if (open < 0 || bytes[open] != (byte)'{') return false;
            string inside = Encoding.ASCII.GetString(bytes, open, guidStart - open);
            Match match = Regex.Match(inside, @"fileID:\s*(-?\d+)");
            return match.Success && long.TryParse(match.Groups[1].Value, out fileId);
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
            for (int i = 0; i + GuidKey.Length + GuidToolRegenerate.GuidLength <= bytes.Length; i++)
            {
                if (!GuidToolBytes.Matches(bytes, i, GuidKey) || (i > 0 && IsWordByte(bytes[i - 1]))) continue;
                int start = i + GuidKey.Length;
                bool valid = start + GuidToolRegenerate.GuidLength == bytes.Length ||
                    !IsHex(bytes[start + GuidToolRegenerate.GuidLength]);
                for (int j = start; valid && j < start + GuidToolRegenerate.GuidLength; j++) valid = IsHex(bytes[j]);
                if (!valid) continue;
                // Scripts are not repaired here yet: only asset references count.
                if (IsScriptReference(bytes, i))
                {
                    i = start + GuidToolRegenerate.GuidLength - 1;
                    continue;
                }
                found.Add(new KeyValuePair<long, string>(start,
                    Encoding.ASCII.GetString(bytes, start, GuidToolRegenerate.GuidLength)));
                i = start + GuidToolRegenerate.GuidLength - 1;
            }
            return found;
        }

        private static readonly byte[] ScriptKey = Encoding.ASCII.GetBytes("m_Script:");
        private static readonly byte[] ScriptFileId = Encoding.ASCII.GetBytes("fileID: 11500000");

        /// <summary>
        /// True when the GUID at <paramref name="guidKey"/> ("guid: ...") is a script:
        /// its line is an m_Script field, or its reference uses MonoScript's fileID 11500000.
        /// </summary>
        private static bool IsScriptReference(byte[] bytes, int guidKey)
        {
            int lineStart = guidKey;
            while (lineStart > 0 && bytes[lineStart - 1] != (byte)'\n') lineStart--;
            for (int i = lineStart; i < guidKey; i++)
            {
                if ((i + ScriptKey.Length <= guidKey && GuidToolBytes.Matches(bytes, i, ScriptKey)) ||
                    (i + ScriptFileId.Length <= guidKey && GuidToolBytes.Matches(bytes, i, ScriptFileId)))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// The file name each GUID last had: from the GUID remap ledger (with its Git
        /// history), then the Git history of .meta files. GUIDs not found are left out.
        /// </summary>
        internal static Dictionary<string, string> LastKnownNames(HashSet<string> guids)
        {
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (guids.Count == 0) return names;
            using (GuidToolLog log = GuidToolLog.Open("Broken reference names"))
            {
                var ledgerPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                GuidToolRemapLedger.LoadWithHistory(log, ledgerPaths);
                var rest = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string guid in guids)
                {
                    string path;
                    if (ledgerPaths.TryGetValue(guid, out path)) names[guid] = System.IO.Path.GetFileName(path);
                    else rest.Add(guid);
                }
                if (rest.Count == 0) return names;
                try
                {
                    string prefix;
                    foreach (KeyValuePair<string, GuidToolMetaHistoryParser.Hit> hit in FindInHistory(rest, log, out prefix))
                        names[hit.Key] = System.IO.Path.GetFileName(hit.Value.Path);
                }
                catch (GitNotInstalledException exception)
                {
                    log.Write("Git history not searched: " + exception.Message);
                    HistoryNotSearched(exception.Message);
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            }
            return names;
        }

        private static Dictionary<string, GuidToolMetaHistoryParser.Hit> FindInHistory(HashSet<string> guids, GuidToolLog log,
            out string prefix)
        {
            string projectRoot = RiryToolsPaths.ProjectRoot;
            GitRunner.Result location = GitRunner.Run(projectRoot, null, 30000, "rev-parse", "--show-prefix");
            prefix = location.Output.Trim();
            if (!location.Succeeded)
            {
                log.Write("Git history not searched: " + location.Describe("Locating the Git repository"));
                HistoryNotSearched(location.Describe("Locating the Git repository"));
                return new Dictionary<string, GuidToolMetaHistoryParser.Hit>();
            }

            var parser = new GuidToolMetaHistoryParser(guids, prefix);
            bool cancelled = false;
            GitRunner.Result result = GitRunner.Stream(projectRoot, HistoryArguments, parser.Feed,
                () => parser.Complete || (cancelled = EditorUtility.DisplayCancelableProgressBar(Title,
                    "Searching Git history: " + parser.Found.Count + " of " + guids.Count + " found", 0.6f)));
            if (cancelled) throw new OperationCanceledException();
            if (!result.Succeeded && !parser.Complete) log.Write(result.Describe("Searching Git history"));
            return parser.Found;
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
}

// ============================================================================
// GLOSSARY: Broken references: scan, candidates and repair
// The window is Editor/UI/GuidTools/GuidToolBrokenReferenceWindow.cs; Git history is read by GuidToolMetaHistoryParser.cs.
// ============================================================================
//
// GuidToolRepairPlan / GuidToolRepairCandidate / GuidToolBreakDecision (this file)
//   GuidToolRepairPlan: Files, References (file to offsets), Missing (GUID to files), Candidates, LastKnownPaths.
//   GuidToolRepairCandidate: TargetGuid or a restore (Commit, RepoPath), Path, Sources, and the restore's
//     breaks: MissingReferences and GuidInUseBy, each with a GuidToolBreakDecision (Undecided, Fix, Accept). PathTaken is not
//     a break: Destination is a free name beside the taken path (GuidToolReferenceRepair.FreePath), so nothing is
//     overwritten and the references follow the restored file by its GUID; Restore refuses a destination that exists.
// GuidToolRepairPlan.Depth / DepthOf(guid) / MaxFixDepth (2): levels shown per pass; a restore at the third
//   level that breaks something is a recursive break, and its references come in the next pass.
// GuidToolRepairCache: Candidates and LastKnownPaths searched so far.
// GuidToolReferenceRepair: Scan(files, log), Suggest(plan, guids, log), AddDependencies(plan, owner, restore, log),
//   Apply(plan, redirects, restores, log), SelectedFiles(), IsMissing(guid), FindGuidReferences(bytes), RepairSelected(),
//   LastKnownNames(guids) (file names from the remap ledger and .meta Git history).
