using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Presets;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Import Presets for files dragged from outside Unity onto a Project folder: which presets the folder's ImportPreset.md
    /// gives each file (worked out from its extension and name, before it exists), and the one picked for each, which the
    /// postprocessor uses when the file is imported. A pick is kept for a minute after the drag.
    /// </summary>
    internal static class ImportPresetDropChoices
    {
        private const double PickLifetime = 60.0;

        /// <summary>One dragged file: its name, file type label and the presets it could get.</summary>
        internal sealed class File
        {
            internal string Name;
            internal string Label;
            internal readonly List<ImportPresetMarker.Target> Presets = new List<ImportPresetMarker.Target>();
            internal int Index;
        }

        // The importer each extension gets, for the file type label before the file exists.
        private static readonly Dictionary<string, Type> Importers = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        // Picks made by the last drag, by the new file's asset path.
        private static readonly Dictionary<string, (ImportPresetMarker.Target Target, double Time)> Picks =
            new Dictionary<string, (ImportPresetMarker.Target, double)>(StringComparer.OrdinalIgnoreCase);

        static ImportPresetDropChoices()
        {
            foreach (string extension in new[] { "png", "jpg", "jpeg", "tga", "psd", "tif", "tiff", "exr", "hdr", "bmp", "gif", "iff", "pict" })
                Importers[extension] = typeof(TextureImporter);
            foreach (string extension in new[] { "fbx", "obj", "dae", "3ds", "dxf", "blend", "max", "ma", "mb" })
                Importers[extension] = typeof(ModelImporter);
            foreach (string extension in new[] { "wav", "mp3", "ogg", "aif", "aiff", "flac", "mod", "it", "s3m", "xm" })
                Importers[extension] = typeof(AudioImporter);
            foreach (string extension in new[] { "mp4", "mov", "webm", "avi", "m4v", "mpg", "mpeg", "wmv", "ogv", "asf", "vp8" })
                Importers[extension] = typeof(VideoClipImporter);
            foreach (string extension in new[] { "ttf", "otf", "dfont" })
                Importers[extension] = typeof(TrueTypeFontImporter);
        }

        /// <summary>The dragged files the folder's rules give a preset, each with its choices; empty when the folder has none.</summary>
        internal static List<File> For(string folder, IEnumerable<string> dragged)
        {
            var files = new List<File>();
            ImportPresetMarker.Rules rules = ImportPresetMarker.ReadRules(folder);
            if (rules == null || rules.IsEmpty) return files;
            HashSet<string> decorators = ImportPresetFolderRules.Decorators(folder);
            foreach (string path in dragged)
            {
                if (Directory.Exists(path)) continue;
                string name = Path.GetFileName(path);
                Type importer;
                if (!Importers.TryGetValue(ImportPresetLibrary.NormalizeExtension(Path.GetExtension(name)), out importer)) continue;
                string label = ImportPresetFolderRules.LabelOf(importer);
                ImportPresetMarker.Target target = new ImportPresetMarker.Target();
                string decorator = ImportPresetFolderRules.DecoratorIn(name, decorators);
                if (decorator != null)
                {
                    target = rules.For(ImportPresetFolderRules.DecoratorLabel(decorator));
                    if (target.Configuration != null) label = ImportPresetFolderRules.DecoratorLabel(decorator);
                }
                if (target.Configuration == null) target = rules.For(label);
                if (target.Configuration == null) continue;
                var file = new File { Name = name, Label = label };
                if (target.Preset != null) file.Presets.Add(target);
                else AddFitting(file, target.Configuration, name, importer);
                if (file.Presets.Count > 0) files.Add(file);
            }
            return files;
        }

        // A configuration without a named preset: its presets for this extension in the order Import Presets tries them,
        // only those made for this importer.
        private static void AddFitting(File file, string configuration, string name, Type importer)
        {
            string path = ImportPresetLibrary.ConfigurationPath(configuration);
            if (path == null || !Directory.Exists(path)) return;
            ImportPresetLibrary.Config config = ImportPresetLibrary.ReadConfig(path, out string error);
            if (error != null) return;
            foreach (ImportPresetLibrary.Candidate candidate in ImportPresetLibrary.OrderedPresets(Path.GetExtension(name), config, ImportPresetLibrary.PresetFiles(path)))
            {
                Preset preset = ImportPresetLibrary.LoadPreset(Path.Combine(path, candidate.File));
                if (preset == null || preset.GetPresetType().GetManagedTypeName() != importer.FullName) continue;
                file.Presets.Add(new ImportPresetMarker.Target { Configuration = configuration, Preset = Path.GetFileNameWithoutExtension(candidate.File) });
            }
        }

        /// <summary>Keeps each file's current choice for when it is imported into the folder.</summary>
        internal static void Remember(string folder, IEnumerable<File> files)
        {
            double now = EditorApplication.timeSinceStartup;
            foreach (File file in files)
                Picks[folder.TrimEnd('/') + "/" + file.Name] = (file.Presets[file.Index], now);
        }

        /// <summary>The preset picked for a file being imported, used once; false when there is none or it is over a minute old.</summary>
        internal static bool TryTake(string assetPath, out ImportPresetMarker.Target target)
        {
            target = new ImportPresetMarker.Target();
            if (!Picks.TryGetValue(assetPath, out (ImportPresetMarker.Target Target, double Time) pick)) return false;
            Picks.Remove(assetPath);
            if (EditorApplication.timeSinceStartup - pick.Time > PickLifetime) return false;
            target = pick.Target;
            return true;
        }
    }
}

// ============================================================================
// GLOSSARY: Import Preset drop choices
// ============================================================================
//
// ImportPresetDropChoices (this file)
//   File                     A dragged file: Name, Label (file type), Presets it could get, Index of the one picked.
//   Importers                Extension -> importer type, for the label before the file exists.
//   Picks                    Picked presets by new asset path, with when they were picked.
//   For(folder, dragged)     The files the folder's ImportPreset.md gives a preset, with every preset each could get.
//   AddFitting(...)          A configuration's presets for the extension, in Import Presets' order, for that importer only.
//   Remember(folder, files)  Keeps each file's pick for its import.
//   TryTake(assetPath, out)  The pick for a file being imported (once, within a minute).
