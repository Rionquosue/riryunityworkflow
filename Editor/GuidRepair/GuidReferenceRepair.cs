#if UNITY_EDITOR
// Target editor: Unity 2022.3 LTS (including 2022.3.22f1).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Riry.UnityWorkflow.Editor
{
    /// <summary>
    /// Relinks GUID references that no longer resolve in damaged or recovered
    /// scenes, prefabs and other text assets. A missing GUID is looked up in the
    /// remap ledger first, then in the Git history of .meta files (autosave
    /// branches included): the asset now at the path that owned it is linked.
    /// </summary>
    internal static class GuidReferenceRepair
    {
        private const string MenuPath = "Assets/Rirytools/Repair Missing GUID References";
        private const string Title = "Repair Missing GUID References";
        private const int ListedLines = 12;
        private static readonly byte[] GuidKey = Encoding.ASCII.GetBytes("guid: ");

        // Every branch, remote and reflog entry (autosaves included), newest
        // first, for .meta files in the project folder.
        internal static readonly string[] HistoryArguments =
        {
            "log", "--all", "--reflog", "--topo-order", "--format=%H", "-p", "--unified=0", "--no-color",
            "--no-ext-diff", "--no-renames", "--", ":(glob)**/*.meta"
        };

        [MenuItem(MenuPath, false, 2004)]
        internal static void RepairSelected()
        {
            GuidToolLog log = GuidToolLog.Open(Title);
            var written = new List<RegenerateAssetGuid.WrittenOperation>();
            bool reloadLocked = false;
            bool committed = false;
            SceneSetup[] sceneSetup = null;
            try
            {
                if (EditorSettings.serializationMode != SerializationMode.ForceText)
                {
                    EditorUtility.DisplayDialog("Force Text Required",
                        "Set Edit > Project Settings > Editor > Asset Serialization to Force Text first.", "OK");
                    return;
                }
                if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                {
                    EditorUtility.DisplayDialog("Close Prefab Mode",
                        "Save and close Prefab Mode before repairing references.", "OK");
                    return;
                }
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                if (!RegenerateAssetGuid.ScenesAreSaved())
                {
                    EditorUtility.DisplayDialog("Save Scenes First",
                        "Every open scene must be saved before repairing references.", "OK");
                    return;
                }
                sceneSetup = EditorSceneManager.GetSceneManagerSetup();
                AssetDatabase.SaveAssets();

                string projectRoot = RegenerateAssetGuid.ProjectRootPath;
                List<string> files = SelectedFiles();
                var references = new Dictionary<string, List<KeyValuePair<long, string>>>(StringComparer.OrdinalIgnoreCase);
                // Exact case: the patch rewrites the bytes exactly as they were found.
                var missing = new HashSet<string>(StringComparer.Ordinal);
                var sample = new byte[4096];
                for (int i = 0; i < files.Count; i++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar(Title, "Scanning " + files[i], 0.4f * i / files.Count))
                        throw new OperationCanceledException();
                    if (!RegenerateAssetGuid.LooksLikeText(Path.Combine(projectRoot, files[i]), sample))
                    {
                        log.Write("Skipped binary file: " + files[i]);
                        continue;
                    }
                    List<KeyValuePair<long, string>> broken = FindGuidReferences(File.ReadAllBytes(Path.Combine(projectRoot, files[i])))
                        .FindAll(reference => IsMissing(reference.Value));
                    if (broken.Count == 0) continue;
                    references[files[i]] = broken;
                    foreach (KeyValuePair<long, string> reference in broken) missing.Add(reference.Value);
                    log.Write("Missing references: " + files[i] + " | " + broken.Count);
                }
                EditorUtility.ClearProgressBar();
                if (missing.Count == 0)
                {
                    EditorUtility.DisplayDialog(Title, "No missing GUID references were found in " + files.Count + " file(s).", "OK");
                    return;
                }

                var resolved = new Dictionary<string, string>(StringComparer.Ordinal);
                var report = new List<string>();
                Dictionary<string, string> ledger = GuidRemapLedger.Load();
                foreach (string guid in missing)
                {
                    string found = GuidRemapLedger.Resolve(guid, ledger, candidate => !IsMissing(candidate));
                    if (found == null) continue;
                    resolved[guid] = found;
                    report.Add(guid + " -> " + AssetDatabase.GUIDToAssetPath(found) + " (ledger)");
                }
                var unresolved = new List<string>();
                if (resolved.Count < missing.Count)
                {
                    var remaining = new HashSet<string>(missing, StringComparer.Ordinal);
                    remaining.ExceptWith(resolved.Keys);
                    Dictionary<string, string> history = FindInHistory(remaining, log);
                    EditorUtility.ClearProgressBar();
                    foreach (string guid in remaining)
                    {
                        string path;
                        string current = history.TryGetValue(guid, out path)
                            ? AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets)
                            : null;
                        if (!string.IsNullOrEmpty(current) && !current.Equals(guid, StringComparison.OrdinalIgnoreCase))
                        {
                            resolved[guid] = current;
                            report.Add(guid + " -> " + path + " (Git history)");
                        }
                        else
                        {
                            unresolved.Add(guid + (path != null ? " (last at " + path + ")" : ""));
                        }
                    }
                }
                foreach (string line in report) log.Write("Relink planned: " + line);
                foreach (string line in unresolved) log.Write("Unresolved: " + line);

                string summary = missing.Count + " missing GUID(s) in " + references.Count + " file(s).\n\n" +
                    (report.Count > 0 ? "Relink:\n" + List(report) + "\n" : "") +
                    (unresolved.Count > 0 ? "Not found:\n" + List(unresolved) : "");
                if (resolved.Count == 0)
                {
                    Debug.LogWarning(Title + ": nothing could be relinked.\n" + string.Join("\n", unresolved));
                    EditorUtility.DisplayDialog(Title, summary, "OK");
                    return;
                }
                if (!EditorUtility.DisplayDialog(Title, summary + "\nThis bypasses Unity Undo.", "Relink", "Cancel"))
                    return;

                var targets = new Dictionary<string, RegenerateAssetGuid.Target>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, string> entry in resolved)
                {
                    targets[entry.Key] = new RegenerateAssetGuid.Target
                    {
                        AssetPath = AssetDatabase.GUIDToAssetPath(entry.Value),
                        OldGuid = entry.Key,
                        NewGuid = entry.Value,
                        OldBytes = Encoding.ASCII.GetBytes(entry.Key),
                        NewBytes = Encoding.ASCII.GetBytes(entry.Value)
                    };
                }
                var patches = new List<RegenerateAssetGuid.FilePatch>();
                foreach (KeyValuePair<string, List<KeyValuePair<long, string>>> file in references)
                {
                    var info = new FileInfo(Path.Combine(projectRoot, file.Key));
                    var patch = new RegenerateAssetGuid.FilePatch
                    {
                        File = new RegenerateAssetGuid.FileState
                        {
                            FullPath = info.FullName,
                            RelativePath = file.Key,
                            Length = info.Length,
                            LastWriteUtcTicks = info.LastWriteTimeUtc.Ticks
                        }
                    };
                    foreach (KeyValuePair<long, string> reference in file.Value)
                    {
                        RegenerateAssetGuid.Target target;
                        if (targets.TryGetValue(reference.Value, out target))
                            patch.Operations.Add(new RegenerateAssetGuid.PatchOperation { Target = target, Offset = reference.Key });
                    }
                    if (patch.Operations.Count == 0) continue;
                    RegenerateAssetGuid.EnsureWritable(info.FullName);
                    patches.Add(patch);
                }

                EditorApplication.LockReloadAssemblies();
                reloadLocked = true;
                RegenerateAssetGuid.ApplyPatches(patches, written, log);
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                committed = true;
                RegenerateAssetGuid.ReloadScenes(sceneSetup, log);

                Debug.Log(Title + ": relinked " + resolved.Count + " GUID(s) in " + patches.Count + " file(s)." +
                    (unresolved.Count > 0 ? " Not found:\n" + string.Join("\n", unresolved) : ""));
                log.Write("Completed. Relinked GUIDs: " + resolved.Count + "; files changed: " + patches.Count);
            }
            catch (OperationCanceledException)
            {
                log.Write("Cancelled before any file was changed.");
            }
            catch (Exception exception)
            {
                if (!committed && written.Count > 0)
                {
                    try
                    {
                        RegenerateAssetGuid.RollBack(written, log);
                        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                        RegenerateAssetGuid.ReloadScenes(sceneSetup, log);
                    }
                    catch (Exception rollbackException)
                    {
                        log.Write("ROLLBACK FAILURE: " + rollbackException);
                        Debug.LogError(Title + ": rollback was incomplete:\n" + rollbackException);
                    }
                }
                log.Write("FAILURE: " + exception);
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(Title + " Failed", exception.Message + "\n\nCheck the Console for details.", "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (reloadLocked) EditorApplication.UnlockReloadAssemblies();
                log.Dispose();
            }
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateRepairSelected()
        {
            return RegenerateAssetGuid.HasValidSelection();
        }

        /// <summary>Selected text-serialized Unity files; folders are expanded recursively.</summary>
        private static List<string> SelectedFiles()
        {
            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (UnityEngine.Object item in Selection.objects)
            {
                string path = RegenerateAssetGuid.NormalizeAssetPath(AssetDatabase.GetAssetPath(item));
                if (string.IsNullOrEmpty(path) || !(path == "Assets" || path.StartsWith("Assets/", StringComparison.Ordinal)))
                    continue;
                string[] paths = AssetDatabase.IsValidFolder(path)
                    ? Array.ConvertAll(AssetDatabase.FindAssets("", new[] { path }), AssetDatabase.GUIDToAssetPath)
                    : new[] { path };
                foreach (string candidate in paths)
                {
                    if (RegenerateAssetGuid.UnitySerializedExtensions.Contains(Path.GetExtension(candidate)))
                        files.Add(RegenerateAssetGuid.NormalizeAssetPath(candidate));
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
            for (int i = 0; i + GuidKey.Length + RegenerateAssetGuid.GuidLength <= bytes.Length; i++)
            {
                if (!StartsWith(bytes, i, GuidKey) || (i > 0 && IsWordByte(bytes[i - 1]))) continue;
                int start = i + GuidKey.Length;
                bool valid = start + RegenerateAssetGuid.GuidLength == bytes.Length ||
                    !IsHex(bytes[start + RegenerateAssetGuid.GuidLength]);
                for (int j = start; valid && j < start + RegenerateAssetGuid.GuidLength; j++) valid = IsHex(bytes[j]);
                if (!valid) continue;
                found.Add(new KeyValuePair<long, string>(start,
                    Encoding.ASCII.GetString(bytes, start, RegenerateAssetGuid.GuidLength)));
                i = start + RegenerateAssetGuid.GuidLength - 1;
            }
            return found;
        }

        private static Dictionary<string, string> FindInHistory(HashSet<string> guids, GuidToolLog log)
        {
            string projectRoot = RegenerateAssetGuid.ProjectRootPath;
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

        private static string List(List<string> lines)
        {
            string text = string.Join("\n", lines.GetRange(0, Math.Min(lines.Count, ListedLines)));
            return text + (lines.Count > ListedLines ? "\n...and " + (lines.Count - ListedLines) + " more (see the log)\n" : "\n");
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
#endif
