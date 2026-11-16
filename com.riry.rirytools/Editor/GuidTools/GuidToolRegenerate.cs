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
/// Folder GUIDs are never changed. Menu entries are in Editor/UI/RiryToolsMenuBar.cs and
/// the GUID Tools window is in Editor/UI/GuidTools/GuidToolsWindow.cs.
/// </summary>
internal static partial class GuidToolRegenerate
{
    private const int MaxDirectoryDepth = 64;
    internal const int GuidLength = 32;
    internal const int ScanBlockSize = 256 * 1024;

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
            RiryLog.Warn(RiryLog.GuidTools,
                "Could not reload the open scenes. Reopen them from disk " +
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

    internal static string ProjectRelativeToFull(string projectRoot, string relativePath)
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
}

}

// ============================================================================
// GLOSSARY: GUID regeneration: data and setup
// GuidToolRegenerate is one partial class: Editor/GuidTools/GuidToolRegenerate(.Scan/.Patch/.Rollback).cs and Editor/UI/GuidTools/GuidToolRegenerate.Prompts.cs.
// ============================================================================
//
// GuidToolRegenerate (this part)
//   Target / ReferenceCache / CachedReference / FileState / PatchOperation / FilePatch / WrittenOperation  The run's records.
//   GuidLength / ScanBlockSize / MaxDirectoryDepth  32 hex digits; 256 KiB read blocks; 64 folder levels (stops link loops).
//   TextExtensions / UnitySerializedExtensions      Files scanned for references; files that must be text to be patched.
//   LogDirectoryPath                Logs/RiryTools/GuidTools.
//   HasValidSelection() / CollectSelectedAssetPaths()  The selected assets, folders expanded.
//   ScenesAreSaved() / ReloadScenes(setup, log)     Open scenes saved; reopened after a run.
//   BuildTargets(...)               A Target per selected asset with a new GUID.
//   ProjectRelativeToFull / MakeProjectRelative / NormalizeAssetPath  Path helpers.
