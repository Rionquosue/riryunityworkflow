using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Presets;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Folder rules by file type, shared by Set Folder Import Presets (Tools menu) and
    /// its right-click popup. A file type is everything one importer reads, so one
    /// preset fits all of it: "Image files" for every texture, "Model files" for every
    /// model, and so on. Only files directly in a folder count, never its subfolders.
    /// </summary>
    internal static class ImportPresetFolderRules
    {
        internal const string NoneChoice = "None";

        private static readonly Dictionary<Type, string> Labels = new Dictionary<Type, string>
        {
            { typeof(TextureImporter), "Image files" },
            { typeof(ModelImporter), "Model files" },
            { typeof(AudioImporter), "Audio files" },
            { typeof(VideoClipImporter), "Video files" },
            { typeof(TrueTypeFontImporter), "Font files" }
        };

        /// <summary>One file type's choice in a window: a configuration and preset, or none.</summary>
        [Serializable]
        internal sealed class Pick
        {
            public string Label;
            public string Configuration;
            public string Preset;
        }

        /// <summary>A file type found in the folders, with the library presets that fit it.</summary>
        internal sealed class FileType
        {
            internal string Label;
            internal AssetImporter Example;
            internal readonly List<ImportPresetMarker.Target> Presets = new List<ImportPresetMarker.Target>();
            // "None", then "Configuration/Preset" for each of Presets.
            internal string[] Names;
        }

        internal static string Label(AssetImporter importer)
        {
            Type type = importer.GetType();
            string label;
            if (Labels.TryGetValue(type, out label)) return label;
            string name = type.Name.EndsWith("Importer", StringComparison.Ordinal) ? type.Name.Substring(0, type.Name.Length - 8) : type.Name;
            return Regex.Replace(name, "(?<=[a-z])(?=[A-Z])", " ") + " files";
        }

        // Importers that take presets; scripts and files Unity does not import are left out.
        private static bool Listed(AssetImporter importer)
        {
            return importer != null && !(importer is MonoImporter) && importer.GetType().Name != "DefaultImporter" &&
                ImportPresetLibrary.CanSave(importer);
        }

        /// <summary>Importers of the files directly in the folder, by file type.</summary>
        internal static SortedDictionary<string, List<AssetImporter>> InFolder(string folder)
        {
            var types = new SortedDictionary<string, List<AssetImporter>>(StringComparer.OrdinalIgnoreCase);
            if (!Directory.Exists(folder)) return types;
            foreach (string file in Directory.GetFiles(folder))
            {
                string name = Path.GetFileName(file);
                if (name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) || name.StartsWith(".", StringComparison.Ordinal) ||
                    name.EndsWith("~", StringComparison.Ordinal) ||
                    string.Equals(name, ImportPresetMarker.FileName, StringComparison.OrdinalIgnoreCase))
                    continue;
                AssetImporter importer = AssetImporter.GetAtPath(folder.TrimEnd('/') + "/" + name);
                if (!Listed(importer)) continue;
                List<AssetImporter> list;
                if (!types.TryGetValue(Label(importer), out list)) types.Add(Label(importer), list = new List<AssetImporter>());
                list.Add(importer);
            }
            return types;
        }

        /// <summary>True when the folder itself holds a .blend file.</summary>
        internal static bool HasBlend(string folder)
        {
            return Directory.Exists(folder) && Directory.GetFiles(folder, "*." + BetterFbxExporter.SourceExtension).Length > 0;
        }

        /// <summary>The file types in the folders, each with the library presets that fit it.</summary>
        internal static List<FileType> Types(IEnumerable<string> folders)
        {
            var found = new SortedDictionary<string, AssetImporter>(StringComparer.OrdinalIgnoreCase);
            foreach (string folder in folders)
            {
                foreach (KeyValuePair<string, List<AssetImporter>> type in InFolder(folder))
                {
                    if (!found.ContainsKey(type.Key)) found.Add(type.Key, type.Value[0]);
                }
            }
            var types = new List<FileType>();
            foreach (KeyValuePair<string, AssetImporter> entry in found)
            {
                var type = new FileType { Label = entry.Key, Example = entry.Value };
                var names = new List<string> { NoneChoice };
                foreach (string configuration in ImportPresetLibrary.ConfigurationNames())
                {
                    string path = ImportPresetLibrary.ConfigurationPath(configuration);
                    foreach (string file in ImportPresetLibrary.PresetFiles(path))
                    {
                        Preset preset = ImportPresetLibrary.LoadPreset(Path.Combine(path, file));
                        if (preset == null || !preset.CanBeAppliedTo(entry.Value)) continue;
                        string name = Path.GetFileNameWithoutExtension(file);
                        type.Presets.Add(new ImportPresetMarker.Target { Configuration = configuration, Preset = name });
                        names.Add(configuration + "/" + name);
                    }
                }
                type.Names = names.ToArray();
                types.Add(type);
            }
            return types;
        }

        /// <summary>The first preset of the configuration that fits the file type, as a pick (none when it has none).</summary>
        internal static Pick FirstIn(FileType type, string configuration)
        {
            ImportPresetMarker.Target first = type.Presets.Find(target =>
                string.Equals(target.Configuration, configuration, StringComparison.OrdinalIgnoreCase));
            return new Pick { Label = type.Label, Configuration = first.Configuration, Preset = first.Preset };
        }

        /// <summary>Popup entries and the index of the pick; a pick not in the library is added as "(missing)".</summary>
        internal static string[] Entries(FileType type, Pick pick, out int index)
        {
            index = 0;
            if (pick == null || pick.Configuration == null) return type.Names;
            int found = type.Presets.FindIndex(target => Same(target, Target(pick)));
            if (found >= 0)
            {
                index = found + 1;
                return type.Names;
            }
            var names = new List<string>(type.Names) { pick.Configuration + "/" + (pick.Preset ?? "(configuration rules)") + " (missing)" };
            index = names.Count - 1;
            return names.ToArray();
        }

        /// <summary>The pick for a popup index; the "(missing)" entry keeps the current pick.</summary>
        internal static Pick Chosen(FileType type, Pick current, int index)
        {
            if (index == 0) return new Pick { Label = type.Label };
            if (index - 1 < type.Presets.Count)
                return new Pick { Label = type.Label, Configuration = type.Presets[index - 1].Configuration, Preset = type.Presets[index - 1].Preset };
            return current;
        }

        internal static ImportPresetMarker.Target Target(Pick pick)
        {
            return new ImportPresetMarker.Target { Configuration = pick.Configuration, Preset = pick.Preset };
        }

        private static bool Same(ImportPresetMarker.Target left, ImportPresetMarker.Target right)
        {
            return string.Equals(left.Configuration, right.Configuration, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(left.Preset, right.Preset, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The folder's rules; an empty set when it has no ImportPreset.md.</summary>
        internal static ImportPresetMarker.Rules Read(string folder)
        {
            string content = ReadOrNull(ImportPresetMarker.PathIn(folder));
            return ImportPresetMarker.Parse(content ?? "");
        }

        /// <summary>True when the folder's ImportPreset.md was edited by hand, so the tool leaves it alone.</summary>
        internal static bool HandEdited(string folder)
        {
            string marker = ImportPresetMarker.PathIn(folder);
            if (!File.Exists(marker)) return false;
            string content = ReadOrNull(marker);
            return content == null || !ImportPresetMarker.IsGenerated(content);
        }

        /// <summary>
        /// Sets the folder's preset for each chosen file type (none clears it) and, when
        /// given, Better FBX, then applies each changed preset to the files of that type
        /// already in the folder. A type set to none or left unchanged keeps its files'
        /// settings and is not reimported. Returns false, changing nothing, for a
        /// hand-edited ImportPreset.md.
        /// </summary>
        internal static bool Apply(string folder, IEnumerable<Pick> picks, bool? betterFbx, ref int processed)
        {
            if (HandEdited(folder)) return false;
            ImportPresetMarker.Rules before = Read(folder);
            ImportPresetMarker.Rules after = Read(folder);
            foreach (Pick pick in picks) after.Set(pick.Label, pick.Configuration, pick.Configuration != null ? pick.Preset : null);
            if (betterFbx.HasValue)
            {
                after.BetterFbx.Remove(BetterFbxExporter.SourceExtension);
                if (betterFbx.Value) after.BetterFbx.Add(BetterFbxExporter.SourceExtension);
            }
            bool changed;
            WriteMarker(folder, after.IsEmpty ? null : ImportPresetMarker.Generate(after), out changed);

            foreach (KeyValuePair<string, List<AssetImporter>> type in InFolder(folder))
            {
                ImportPresetMarker.Target now = after.For(type.Key);
                if (now.Configuration == null || Same(before.For(type.Key), now)) continue;
                foreach (AssetImporter listed in type.Value)
                {
                    string path = listed.assetPath;
                    // A Better FBX source is not configured itself; its exported FBX is.
                    if (after.BetterFbx.Contains(ImportPresetLibrary.NormalizeExtension(Path.GetExtension(path)))) continue;
                    // A model set to Automatically extract animations is extracted as Generic first.
                    var model = listed as ModelImporter;
                    if (model != null && ImportPresetAnimations.ShouldExtract(path))
                    {
                        List<string> skipped;
                        ImportPresetAnimations.ExtractPreservingSettings(model, AnimationExtractSettings.Load(path),
                            ImportPresetOwnedFiles.IsUnchanged, out skipped);
                    }
                    AssetImporter importer = AssetImporter.GetAtPath(path);
                    string applied;
                    if (importer == null || !ImportPresetLibrary.TryApply(importer, now.Configuration, now.Preset, out applied)) continue;
                    importer.SaveAndReimport();
                    processed++;
                }
            }
            if (betterFbx == true && !before.BetterFbx.Contains(BetterFbxExporter.SourceExtension))
            {
                foreach (string file in Directory.GetFiles(folder, "*." + BetterFbxExporter.SourceExtension))
                {
                    ImportPresetPostprocessor.ExportOne(folder.TrimEnd('/') + "/" + Path.GetFileName(file), false);
                    processed++;
                }
            }
            return true;
        }

        /// <summary>
        /// Writes the folder's ImportPreset.md, or removes it when generated is null.
        /// Returns false for a hand-edited one, which belongs to the user and is never changed.
        /// </summary>
        internal static bool WriteMarker(string folder, string generated, out bool changed)
        {
            changed = false;
            string marker = ImportPresetMarker.PathIn(folder);
            if (File.Exists(marker))
            {
                string content = ReadOrNull(marker);
                if (content == null || !ImportPresetMarker.IsGenerated(content)) return false;
                if (generated == null)
                {
                    AssetDatabase.DeleteAsset(marker);
                    changed = true;
                    return true;
                }
                if (content.Replace("\r\n", "\n") == generated) return true;
            }
            else if (generated == null)
            {
                return true;
            }
            File.WriteAllText(marker, generated, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(marker);
            changed = true;
            return true;
        }

        internal static string ReadOrNull(string path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
