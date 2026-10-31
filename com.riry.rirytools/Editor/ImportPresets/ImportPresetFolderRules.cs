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
    /// model, and so on. A decorator, the "_word" ending of a file name in the folder
    /// (such as _normal or _height; number-only endings are ignored), is a file type of
    /// its own, "_normal files", for every file whose name contains it; a name holding
    /// several uses the one that comes last. A decorated file with no preset of its own
    /// gets its plain type's. Only files directly in a folder count, never its subfolders.
    /// </summary>
    internal static class ImportPresetFolderRules
    {
        internal const string NoneChoice = "None";
        private const string FilesSuffix = " files";
        private static readonly Regex Word = new Regex("^[A-Za-z0-9]+$", RegexOptions.CultureInvariant);

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
            return Regex.Replace(name, "(?<=[a-z])(?=[A-Z])", " ") + FilesSuffix;
        }

        /// <summary>"_normal" for Body_normal.png, lower case; null without a "_word" ending or when it is only digits.</summary>
        internal static string Ending(string fileName)
        {
            string name = Path.GetFileNameWithoutExtension(fileName);
            int underscore = name.LastIndexOf('_');
            if (underscore < 0) return null;
            string word = name.Substring(underscore + 1);
            if (!Word.IsMatch(word) || Regex.IsMatch(word, "^[0-9]+$")) return null;
            return "_" + word.ToLowerInvariant();
        }

        /// <summary>Of the decorators the file name contains, the one that comes last (the longer at the same place); else null.</summary>
        internal static string DecoratorIn(string fileName, ICollection<string> decorators)
        {
            string name = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
            string best = null;
            int bestIndex = -1;
            foreach (string decorator in decorators)
            {
                int index = name.LastIndexOf(decorator, StringComparison.Ordinal);
                if (index < 0 || index < bestIndex || (index == bestIndex && decorator.Length <= best.Length)) continue;
                best = decorator;
                bestIndex = index;
            }
            return best;
        }

        /// <summary>"_normal files".</summary>
        internal static string DecoratorLabel(string decorator)
        {
            return decorator + FilesSuffix;
        }

        // Files directly in the folder that Unity imports: no .meta, ImportPreset.md, or names Unity skips.
        private static List<string> Files(string folder)
        {
            var files = new List<string>();
            if (!Directory.Exists(folder)) return files;
            foreach (string file in Directory.GetFiles(folder))
            {
                string name = Path.GetFileName(file);
                if (name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) || name.StartsWith(".", StringComparison.Ordinal) ||
                    name.EndsWith("~", StringComparison.Ordinal) ||
                    string.Equals(name, ImportPresetMarker.FileName, StringComparison.OrdinalIgnoreCase))
                    continue;
                files.Add(folder.TrimEnd('/') + "/" + name);
            }
            return files;
        }

        /// <summary>The decorators of the folder: every "_word" ending of its files' names.</summary>
        internal static HashSet<string> Decorators(string folder)
        {
            var decorators = new HashSet<string>(StringComparer.Ordinal);
            foreach (string path in Files(folder))
            {
                string ending = Ending(path);
                if (ending != null) decorators.Add(ending);
            }
            return decorators;
        }

        // Importers that take presets; scripts and files Unity does not import are left out.
        private static bool Listed(AssetImporter importer)
        {
            return importer != null && !(importer is MonoImporter) && importer.GetType().Name != "DefaultImporter" &&
                ImportPresetLibrary.CanSave(importer);
        }

        /// <summary>
        /// Importers of the files directly in the folder, by file type: every file under its
        /// plain type, and a decorated file under its decorator's type as well.
        /// </summary>
        internal static SortedDictionary<string, List<AssetImporter>> InFolder(string folder)
        {
            var types = new SortedDictionary<string, List<AssetImporter>>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> decorators = Decorators(folder);
            foreach (string path in Files(folder))
            {
                AssetImporter importer = AssetImporter.GetAtPath(path);
                if (!Listed(importer)) continue;
                Add(types, Label(importer), importer);
                string decorator = DecoratorIn(path, decorators);
                if (decorator != null) Add(types, DecoratorLabel(decorator), importer);
            }
            return types;
        }

        private static void Add(SortedDictionary<string, List<AssetImporter>> types, string label, AssetImporter importer)
        {
            List<AssetImporter> list;
            if (!types.TryGetValue(label, out list)) types.Add(label, list = new List<AssetImporter>());
            list.Add(importer);
        }

        /// <summary>
        /// The preset a file gets from the rules: its decorator's type when that has one,
        /// else its plain type's. Decorators are those of the file's folder.
        /// </summary>
        internal static ImportPresetMarker.Target Effective(ImportPresetMarker.Rules rules, AssetImporter importer, ICollection<string> decorators)
        {
            string decorator = DecoratorIn(importer.assetPath, decorators);
            if (decorator != null)
            {
                ImportPresetMarker.Target own = rules.For(DecoratorLabel(decorator));
                if (own.Configuration != null) return own;
            }
            return rules.For(Label(importer));
        }

        /// <summary>True when the folder itself holds a .blend file.</summary>
        internal static bool HasBlend(string folder)
        {
            return Directory.Exists(folder) && Directory.GetFiles(folder, "*." + BetterFbxExporter.SourceExtension).Length > 0;
        }

        /// <summary>The file types in the folders, each with the library presets that fit any of its files.</summary>
        internal static List<FileType> Types(IEnumerable<string> folders)
        {
            // One importer of each kind per file type: a decorator's files can be images and models alike.
            var found = new SortedDictionary<string, List<AssetImporter>>(StringComparer.OrdinalIgnoreCase);
            foreach (string folder in folders)
            {
                foreach (KeyValuePair<string, List<AssetImporter>> type in InFolder(folder))
                {
                    List<AssetImporter> kinds;
                    if (!found.TryGetValue(type.Key, out kinds)) found.Add(type.Key, kinds = new List<AssetImporter>());
                    foreach (AssetImporter importer in type.Value)
                    {
                        if (!kinds.Exists(kind => kind.GetType() == importer.GetType())) kinds.Add(importer);
                    }
                }
            }
            var types = new List<FileType>();
            foreach (KeyValuePair<string, List<AssetImporter>> entry in found)
            {
                var type = new FileType { Label = entry.Key, Example = entry.Value[0] };
                var names = new List<string> { NoneChoice };
                foreach (string configuration in ImportPresetLibrary.ConfigurationNames())
                {
                    string path = ImportPresetLibrary.ConfigurationPath(configuration);
                    foreach (string file in ImportPresetLibrary.PresetFiles(path))
                    {
                        Preset preset = ImportPresetLibrary.LoadPreset(Path.Combine(path, file));
                        if (preset == null || !entry.Value.Exists(kind => preset.CanBeAppliedTo(kind))) continue;
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

            // Each file once, by the preset it gets now (its decorator's, else its plain type's).
            HashSet<string> decorators = Decorators(folder);
            var done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, List<AssetImporter>> type in InFolder(folder))
            {
                foreach (AssetImporter listed in type.Value)
                {
                    string path = listed.assetPath;
                    if (!done.Add(path)) continue;
                    ImportPresetMarker.Target now = Effective(after, listed, decorators);
                    if (now.Configuration == null || Same(Effective(before, listed, decorators), now)) continue;
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
