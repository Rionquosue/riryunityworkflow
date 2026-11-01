using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// IconCap's files: PNGs in the export folder, each export folder's hidden
    /// .iconcap.json of jobs, the OLD_&lt;n&gt; folders a job's Run moves replaced icons
    /// into, and the capture presets in Documents/RiryTools/iconcap presets.
    /// </summary>
    internal static class IconCapFiles
    {
        internal const string JobFileName = ".iconcap.json";
        internal const string OldFolderPrefix = "OLD_";
        internal const string PresetFolderName = "iconcap presets";
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        /// <summary>A folder as typed (project-relative or full) as a full path.</summary>
        internal static string FullPath(string folder)
        {
            if (string.IsNullOrEmpty(folder)) folder = "Assets";
            return Path.GetFullPath(Path.IsPathRooted(folder) ? folder : Path.Combine(RiryToolsPaths.ProjectRoot, folder));
        }

        /// <summary>A full path as "Assets/..." when it is inside Assets, else null.</summary>
        internal static string AssetPath(string fullPath)
        {
            string assets = Path.GetFullPath(Application.dataPath);
            string full = Path.GetFullPath(fullPath);
            if (full == assets) return "Assets";
            if (!full.StartsWith(assets + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return null;
            return "Assets/" + full.Substring(assets.Length + 1).Replace('\\', '/');
        }

        /// <summary>A folder as it is shown and kept: project-relative when inside the project.</summary>
        internal static string DisplayFolder(string fullPath)
        {
            string root = Path.GetFullPath(RiryToolsPaths.ProjectRoot);
            string full = Path.GetFullPath(fullPath);
            if (full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return full.Substring(root.Length + 1).Replace('\\', '/');
            return full;
        }

        /// <summary>
        /// Writes the image as a PNG. One inside Assets is imported and highlighted in the
        /// Project window; its .meta, and so its GUID, stays when it is overwritten.
        /// </summary>
        internal static void WritePng(Texture2D image, string fullPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllBytes(fullPath, image.EncodeToPNG());
            string asset = AssetPath(fullPath);
            if (asset == null) return;
            AssetDatabase.ImportAsset(asset, ImportAssetOptions.ForceUpdate);
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Texture2D>(asset));
        }

        /// <summary>A file name that is safe on every system, ending in .png.</summary>
        internal static string PngName(string name)
        {
            if (string.IsNullOrEmpty(name)) name = "Icon";
            foreach (char invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
            return name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? name : name + ".png";
        }

        // ---- Jobs ---------------------------------------------------------------

        internal static string JobFilePath(string folderFullPath)
        {
            return Path.Combine(folderFullPath, JobFileName);
        }

        /// <summary>The folder's jobs; empty when it has none.</summary>
        internal static IconCapJobFile ReadJobs(string folderFullPath)
        {
            string path = JobFilePath(folderFullPath);
            try
            {
                if (File.Exists(path)) return JsonUtility.FromJson<IconCapJobFile>(File.ReadAllText(path)) ?? new IconCapJobFile();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("IconCap: could not read " + path + ": " + exception.Message);
            }
            return new IconCapJobFile();
        }

        /// <summary>Writes the folder's .iconcap.json, hidden on Windows.</summary>
        internal static void WriteJobs(string folderFullPath, IconCapJobFile jobs)
        {
            Directory.CreateDirectory(folderFullPath);
            string path = JobFilePath(folderFullPath);
            // A hidden file cannot be overwritten on Windows, so it is unhidden first.
            if (File.Exists(path)) File.SetAttributes(path, FileAttributes.Normal);
            File.WriteAllText(path, JsonUtility.ToJson(jobs, true), Utf8);
            if (Application.platform == RuntimePlatform.WindowsEditor)
                File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.Hidden);
        }

        /// <summary>Every folder with a .iconcap.json: those under Assets, and extra (the export folder).</summary>
        internal static List<string> JobFolders(string extraFullPath)
        {
            var folders = new List<string>();
            try
            {
                foreach (string file in Directory.GetFiles(Application.dataPath, JobFileName, SearchOption.AllDirectories))
                    folders.Add(Path.GetFullPath(Path.GetDirectoryName(file)));
            }
            catch (Exception exception)
            {
                Debug.LogWarning("IconCap: could not search Assets for jobs: " + exception.Message);
            }
            if (!string.IsNullOrEmpty(extraFullPath) && File.Exists(JobFilePath(extraFullPath)))
            {
                string extra = Path.GetFullPath(extraFullPath);
                if (!folders.Exists(folder => string.Equals(folder, extra, StringComparison.OrdinalIgnoreCase))) folders.Add(extra);
            }
            folders.Sort(StringComparer.OrdinalIgnoreCase);
            return folders;
        }

        /// <summary>A new, numbered OLD_&lt;n&gt; folder path in the export folder (not yet created).</summary>
        internal static string NextOldFolder(string folderFullPath)
        {
            int number = 1;
            while (Directory.Exists(Path.Combine(folderFullPath, OldFolderPrefix + number))) number++;
            return Path.Combine(folderFullPath, OldFolderPrefix + number);
        }

        /// <summary>
        /// Run: remakes each icon of the job. The files being replaced are first moved
        /// into a new OLD_&lt;n&gt; folder; their .meta files stay, so each new image keeps
        /// the original GUID and every reference follows it, and the moved copies are
        /// imported as new assets with new GUIDs. Returns the problems, one per line.
        /// </summary>
        internal static string Run(string folderFullPath, IconCapJob job)
        {
            var problems = new StringBuilder();
            var images = new List<KeyValuePair<string, Texture2D>>();
            try
            {
                for (int i = 0; i < job.Entries.Count; i++)
                {
                    IconCapEntry entry = job.Entries[i];
                    EditorUtility.DisplayProgressBar("IconCap", "Rendering " + entry.FileName, i / (float)Math.Max(1, job.Entries.Count));
                    string problem;
                    List<Renderer> targets = IconCapRenderer.Targets(entry.Objects, out problem);
                    if (targets == null)
                    {
                        problems.AppendLine(entry.FileName + ": " + problem);
                        continue;
                    }
                    RenderTexture image = IconCapRenderer.Render(entry.Settings, targets, entry.Settings.Size, i);
                    images.Add(new KeyValuePair<string, Texture2D>(entry.FileName, IconCapEffects.Read(image)));
                    RenderTexture.ReleaseTemporary(image);
                }

                string oldFolder = null;
                foreach (KeyValuePair<string, Texture2D> image in images)
                {
                    string target = Path.Combine(folderFullPath, image.Key);
                    if (File.Exists(target))
                    {
                        if (oldFolder == null)
                        {
                            oldFolder = NextOldFolder(folderFullPath);
                            Directory.CreateDirectory(oldFolder);
                        }
                        File.Move(target, Path.Combine(oldFolder, image.Key));
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.WriteAllBytes(target, image.Value.EncodeToPNG());
                }
                if (AssetPath(folderFullPath) != null) AssetDatabase.Refresh();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                foreach (KeyValuePair<string, Texture2D> image in images) UnityEngine.Object.DestroyImmediate(image.Value);
            }
            return problems.ToString().TrimEnd();
        }

        // ---- Presets ------------------------------------------------------------

        internal static string PresetFolder
        {
            get { return RiryToolsPaths.Documents(PresetFolderName); }
        }

        /// <summary>The saved presets' names, sorted.</summary>
        internal static List<string> PresetNames()
        {
            var names = new List<string>();
            if (!Directory.Exists(PresetFolder)) return names;
            foreach (string file in Directory.GetFiles(PresetFolder, "*.json")) names.Add(Path.GetFileNameWithoutExtension(file));
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        internal static void SavePreset(string name, IconCapSettings settings)
        {
            Directory.CreateDirectory(PresetFolder);
            File.WriteAllText(Path.Combine(PresetFolder, SafeName(name) + ".json"), JsonUtility.ToJson(settings, true), Utf8);
        }

        internal static IconCapSettings LoadPreset(string name)
        {
            string path = Path.Combine(PresetFolder, SafeName(name) + ".json");
            try
            {
                return File.Exists(path) ? JsonUtility.FromJson<IconCapSettings>(File.ReadAllText(path)) : null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("IconCap: could not read " + path + ": " + exception.Message);
                return null;
            }
        }

        private static string SafeName(string name)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
            return name.Trim();
        }
    }
}

// ============================================================================
// GLOSSARY: IconCap files
// ============================================================================
//
// IconCapFiles (this file)
//   FullPath / AssetPath / DisplayFolder  Export folder as typed, as a full path, as "Assets/...", as shown.
//   WritePng(image, path)        PNG; inside Assets it is imported and pinged (highlighted) in the Project window.
//   PngName(name)                A safe file name ending in .png.
//   JobFileName (.iconcap.json)  One per export folder: IconCapJobFile (Editor/IconCap/IconCapData.cs); hidden on Windows.
//   ReadJobs / WriteJobs / JobFolders(extra)  Read, write; every folder with one under Assets, plus extra.
//   Run(folder, job)             Renders every entry, moves the files being replaced into a new OLD_<n> (.meta files
//                                stay, so the GUIDs stay at the original paths), writes the new PNGs, refreshes.
//   NextOldFolder / OldFolderPrefix (OLD_)  The next free OLD_<n> in the export folder.
//   PresetFolder                 Documents/RiryTools/iconcap presets: <name>.json, an IconCapSettings each.
//   PresetNames / SavePreset / LoadPreset
