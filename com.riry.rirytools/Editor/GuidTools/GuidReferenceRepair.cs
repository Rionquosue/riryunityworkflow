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
    /// <summary>Missing GUIDs found in a set of files, with suggested redirects.</summary>
    internal sealed class RepairPlan
    {
        public readonly List<string> Files = new List<string>();
        /// <summary>File to the byte offset and value of each missing GUID in it.</summary>
        public readonly Dictionary<string, List<KeyValuePair<long, string>>> References =
            new Dictionary<string, List<KeyValuePair<long, string>>>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Missing GUID to the files that use it. Exact case: patches rewrite the bytes as found.</summary>
        public readonly SortedDictionary<string, SortedSet<string>> Missing =
            new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        public readonly Dictionary<string, string> Suggested = new Dictionary<string, string>(StringComparer.Ordinal);
        public readonly Dictionary<string, string> Sources = new Dictionary<string, string>(StringComparer.Ordinal);
        public readonly Dictionary<string, string> LastKnownPaths = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Finds GUID references that no longer resolve in damaged or recovered scenes,
    /// prefabs and other text assets, suggests redirects from the remap ledger,
    /// GUID Tools logs and the Git history of .meta files (AutoSync included), and
    /// rewrites the chosen ones byte-exactly with rollback.
    /// </summary>
    internal static class GuidReferenceRepair
    {
        internal const string Title = "Broken References";
        private static readonly byte[] GuidKey = Encoding.ASCII.GetBytes("guid: ");
        private static readonly Regex LogMapping = new Regex(
            @"GUID mapping prepared: .* \| ([0-9a-fA-F]{32}) -> ([0-9a-fA-F]{32})", RegexOptions.CultureInvariant);

        // Every branch, remote and reflog entry (AutoSync snapshots included), newest
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

        /// <summary>Suggests a redirect for each missing GUID: ledger, then GUID Tools logs, then Git history.</summary>
        internal static void Suggest(RepairPlan plan, GuidToolLog log)
        {
            Dictionary<string, string> ledger = GuidRemapLedger.Load();
            Dictionary<string, string> logged = LoggedMappings();
            var remaining = new HashSet<string>(StringComparer.Ordinal);
            foreach (string guid in plan.Missing.Keys)
            {
                string found = GuidRemapLedger.Resolve(guid, ledger, candidate => !IsMissing(candidate));
                string source = "the GUID remap ledger";
                if (found == null)
                {
                    found = GuidRemapLedger.Resolve(guid, logged, candidate => !IsMissing(candidate));
                    source = "GUID Tools logs";
                }
                if (found == null)
                {
                    remaining.Add(guid);
                    continue;
                }
                plan.Suggested[guid] = found;
                plan.Sources[guid] = source;
            }
            if (remaining.Count == 0) return;

            Dictionary<string, string> history;
            try
            {
                history = FindInHistory(remaining, log);
            }
            catch (GitNotInstalledException exception)
            {
                log.Write("Git history not searched: " + exception.Message);
                return;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            foreach (KeyValuePair<string, string> entry in history)
            {
                plan.LastKnownPaths[entry.Key] = entry.Value;
                string current = AssetDatabase.AssetPathToGUID(entry.Value, AssetPathToGUIDOptions.OnlyExistingAssets);
                if (string.IsNullOrEmpty(current) || current == entry.Key) continue;
                plan.Suggested[entry.Key] = current;
                plan.Sources[entry.Key] = "Git history (" + entry.Value + ")";
            }
        }

        /// <summary>Old to new GUIDs from GUID Tools logs, including runs before the ledger existed.</summary>
        private static Dictionary<string, string> LoggedMappings()
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
                        if (match.Success) map[match.Groups[1].Value] = match.Groups[2].Value;
                    }
                }
            }
            return map;
        }

        /// <summary>
        /// Rewrites each missing GUID that has a redirect, then reloads the open scenes
        /// from disk (discarding unsaved changes). Rolls back on failure. Returns the
        /// number of files changed.
        /// </summary>
        internal static int Apply(RepairPlan plan, Dictionary<string, string> redirects, GuidToolLog log)
        {
            var written = new List<RegenerateGuid.WrittenOperation>();
            SceneSetup[] sceneSetup = EditorSceneManager.GetSceneManagerSetup();
            bool committed = false;
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
            foreach (KeyValuePair<string, List<KeyValuePair<long, string>>> file in plan.References)
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

            EditorApplication.LockReloadAssemblies();
            try
            {
                RegenerateGuid.ApplyPatches(patches, written, log);
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                committed = true;
                RegenerateGuid.ReloadScenes(sceneSetup, log);
                log.Write("Completed. Redirected GUIDs: " + redirects.Count + "; files changed: " + patches.Count);
                return patches.Count;
            }
            catch (Exception)
            {
                if (!committed && written.Count > 0)
                {
                    try
                    {
                        RegenerateGuid.RollBack(written, log);
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

        private static Dictionary<string, string> FindInHistory(HashSet<string> guids, GuidToolLog log)
        {
            string projectRoot = RiryToolsPaths.ProjectRoot;
            GitRunner.Result location = GitRunner.Run(projectRoot, null, 30000, "rev-parse", "--show-prefix");
            if (!location.Succeeded)
            {
                log.Write("Git history not searched: " + location.Describe("Locating the Git repository"));
                return new Dictionary<string, string>();
            }

            var parser = new MetaHistoryParser(guids, location.Output.Trim());
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
    /// recent project path that held each wanted GUID.
    /// </summary>
    internal sealed class MetaHistoryParser
    {
        private readonly HashSet<string> wanted;
        private readonly string prefix;
        private string path;
        internal readonly Dictionary<string, string> Found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
            if (line.StartsWith("diff --git ", StringComparison.Ordinal)) path = null;
            // Git ends a header path that contains a space with a tab.
            else if (line.StartsWith("--- a/", StringComparison.Ordinal) || line.StartsWith("+++ b/", StringComparison.Ordinal))
                path = line.Substring(6).TrimEnd('\t');
            else if (path != null && (line.StartsWith("+guid: ", StringComparison.Ordinal) || line.StartsWith("-guid: ", StringComparison.Ordinal)))
            {
                string guid = line.Substring(7).Trim();
                if (wanted.Contains(guid) && !Found.ContainsKey(guid) &&
                    path.StartsWith(prefix, StringComparison.Ordinal) && path.EndsWith(".meta", StringComparison.Ordinal))
                    Found[guid] = path.Substring(prefix.Length, path.Length - prefix.Length - ".meta".Length);
            }
        }
    }
}
