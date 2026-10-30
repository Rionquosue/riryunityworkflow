using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Edits the ImportPreset.md of one or more folders: a default configuration
    /// and per-file-type rows, with animation extraction and Better FBX options.
    /// State and actions; the drawing is in Editor/UI/ImportPresetWindows.cs.
    /// </summary>
    internal sealed partial class ImportPresetFolderWindow : EditorWindow
    {
        private const string DefaultChoice = "Default";
        private const string NoneChoice = "None";
        private const string AnyPreset = "Any (configuration rules)";

        [Serializable]
        private sealed class Row
        {
            public string Extensions = "";
            // DefaultChoice, NoneChoice or a configuration name.
            public string Use = DefaultChoice;
            public string Preset;
            public bool Animations;
            public bool BetterFbx;
        }

        [SerializeField] private List<string> folders = new List<string>();
        [SerializeField] private string defaultConfiguration;
        [SerializeField] private string defaultPreset;
        [SerializeField] private List<Row> rows = new List<Row>();
        [SerializeField] private bool applyExisting;
        [NonSerialized] private List<string> configurations = new List<string>();
        [NonSerialized] private string message;
        private Vector2 scroll;

        private void OnEnable()
        {
            if (folders == null) folders = new List<string>();
            if (rows == null) rows = new List<Row>();
            configurations = LoadConfigurations(out message);
        }

        /// <summary>Lists configurations, or reports why the library is unavailable.</summary>
        internal static List<string> LoadConfigurations(out string error)
        {
            error = null;
            try
            {
                ImportPresetLibrary.EnsureRoot();
                return ImportPresetLibrary.ConfigurationNames();
            }
            catch (Exception exception)
            {
                error = "The preset library is unavailable (" + ImportPresetLibrary.RootPath + "): " +
                    exception.Message;
                return new List<string>();
            }
        }

        private void LoadFrom(string folder)
        {
            string content = ReadOrNull(ImportPresetMarker.PathIn(folder));
            if (content == null) return;
            ImportPresetMarker.Rules rules = ImportPresetMarker.Parse(content);
            defaultConfiguration = rules.DefaultConfiguration;
            defaultPreset = rules.DefaultPreset;
            rows.Clear();
            var listed = new HashSet<string>();
            foreach (ImportPresetMarker.TypeRule rule in rules.TypeRules)
            {
                rows.Add(new Row
                {
                    Extensions = string.Join(", ", rule.Extensions),
                    Use = rule.Configuration ?? NoneChoice,
                    Preset = rule.Preset,
                    Animations = rule.Extensions.TrueForAll(rules.ExtractAnimations.Contains),
                    BetterFbx = rule.Extensions.Contains(BetterFbxExporter.SourceExtension) &&
                        rules.BetterFbx.Contains(BetterFbxExporter.SourceExtension)
                });
                listed.UnionWith(rule.Extensions);
            }
            // Options for types that use the default become rows using "Default".
            var flagged = new List<string>(rules.ExtractAnimations);
            foreach (string extension in rules.BetterFbx) if (!flagged.Contains(extension)) flagged.Add(extension);
            foreach (string extension in flagged)
            {
                if (listed.Contains(extension)) continue;
                rows.Add(new Row
                {
                    Extensions = extension,
                    Animations = rules.ExtractAnimations.Contains(extension),
                    BetterFbx = rules.BetterFbx.Contains(extension)
                });
            }
        }

        private static bool Lists(Row row, string extension)
        {
            return ImportPresetMarker.ParseExtensions(row.Extensions ?? "").Contains(extension);
        }

        private static int IndexOf(List<string> list, string value)
        {
            return list.FindIndex(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Why the rows cannot be written, or null.</summary>
        private string Problem()
        {
            var seen = new HashSet<string>();
            foreach (Row row in rows)
            {
                List<string> extensions = ImportPresetMarker.ParseExtensions(row.Extensions ?? "");
                if (extensions.Count == 0)
                    return "'" + row.Extensions + "' is not a list of file extensions such as: png, tga";
                foreach (string extension in extensions)
                {
                    if (!seen.Add(extension)) return "." + extension + " is listed in more than one row.";
                    if (extension == ImportPresetMarker.DefaultKey || extension == ImportPresetMarker.AnimationsKey ||
                        extension == ImportPresetMarker.BetterFbxKey)
                        return "'" + extension + "' is a reserved word in ImportPreset.md.";
                }
                if (row.Use == DefaultChoice && !row.Animations && !(row.BetterFbx && extensions.Contains(BetterFbxExporter.SourceExtension)))
                    return "Row '" + row.Extensions + "' uses the default and has no options, so it does nothing.";
            }
            return BetterFbxExporter.OptionsError(BetterFbxExporter.Options);
        }

        private ImportPresetMarker.Rules BuildRules()
        {
            var rules = new ImportPresetMarker.Rules
            {
                DefaultConfiguration = defaultConfiguration,
                DefaultPreset = defaultConfiguration != null ? defaultPreset : null
            };
            foreach (Row row in rows)
            {
                List<string> extensions = ImportPresetMarker.ParseExtensions(row.Extensions);
                if (row.Use != DefaultChoice)
                {
                    bool none = row.Use == NoneChoice;
                    rules.TypeRules.Add(new ImportPresetMarker.TypeRule
                    {
                        Extensions = extensions,
                        Configuration = none ? null : row.Use,
                        Preset = none ? null : row.Preset
                    });
                }
                if (row.Animations) rules.ExtractAnimations.AddRange(extensions);
                if (row.BetterFbx && extensions.Contains(BetterFbxExporter.SourceExtension))
                    rules.BetterFbx.Add(BetterFbxExporter.SourceExtension);
            }
            return rules;
        }

        private void Apply()
        {
            ImportPresetMarker.Rules rules = BuildRules();
            var existing = new List<string>();
            if (applyExisting && !rules.IsEmpty)
            {
                foreach (string folder in folders) existing.AddRange(FilesGovernedBy(folder));
                if (existing.Count > 0 && !EditorUtility.DisplayDialog("Set Folder Import Presets",
                        "Apply these rules to " + existing.Count + " existing file(s)? Their import settings " +
                        "are replaced. This cannot be undone.", "Apply", "Cancel"))
                    return;
            }

            string generated = rules.IsEmpty ? null : ImportPresetMarker.Generate(rules);
            var skipped = new List<string>();
            int written = 0;
            foreach (string folder in folders)
            {
                bool changed;
                if (!WriteMarker(folder, generated, out changed)) skipped.Add(folder);
                else if (changed) written++;
            }

            int applied = existing.Count > 0 ? ApplyToExisting(existing) : 0;
            message = "Updated " + written + " ImportPreset.md file(s).";
            if (applied > 0) message += " Processed " + applied + " existing file(s).";
            if (skipped.Count > 0)
                message += " Left hand-edited ImportPreset.md unchanged in: " + string.Join(", ", skipped) + ".";
            Repaint();
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

        internal static List<string> FilesGovernedBy(string folder)
        {
            var paths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path) || paths.Contains(path)) continue;
                if (Path.GetFileName(path) == ImportPresetMarker.FileName) continue;
                // A deeper ImportPreset.md governs its own folder.
                string governing = ImportPresetMarker.FindMarkerFolder(path, File.Exists);
                if (governing != null && governing != folder) continue;
                paths.Add(path);
            }
            return paths;
        }

        internal static int ApplyToExisting(List<string> paths)
        {
            int processed = 0;
            try
            {
                for (int i = 0; i < paths.Count; i++)
                {
                    string path = paths[i];
                    if (EditorUtility.DisplayCancelableProgressBar("Set Folder Import Presets", path, (float)i / paths.Count))
                        break;
                    if (ImportPresetPostprocessor.ExportRules(path) != null)
                    {
                        EditorUtility.ClearProgressBar();
                        ImportPresetPostprocessor.ExportOne(path, false);
                        processed++;
                        continue;
                    }
                    if (ApplyRules(path)) processed++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            return processed;
        }

        private static bool ApplyRules(string path)
        {
            ImportPresetMarker.Rules rules = ImportPresetMarker.RulesFor(path);
            AssetImporter importer = AssetImporter.GetAtPath(path);
            if (rules == null || importer == null) return false;
            string extension = ImportPresetLibrary.NormalizeExtension(Path.GetExtension(path));
            bool extracted = false;
            var model = importer as ModelImporter;
            if (model != null && ImportPresetAnimations.ShouldExtract(path, rules))
            {
                List<string> skipped;
                ImportPresetAnimations.ExtractPreservingSettings(model, AnimationExtractSettings.Load(path),
                    ImportPresetOwnedFiles.IsUnchanged, out skipped);
                extracted = true;
            }
            ImportPresetMarker.Target target = rules.For(extension);
            string applied;
            if (target.Configuration == null ||
                !ImportPresetLibrary.TryApply(importer, target.Configuration, target.Preset, out applied))
                return extracted;
            importer.SaveAndReimport();
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

    /// <summary>Saves the selected asset's import settings into the shared library.</summary>
    internal sealed partial class ImportPresetSaveWindow : EditorWindow
    {
        [SerializeField] private string assetPath;
        [SerializeField] private int choice;
        [SerializeField] private string newConfiguration = "";
        [SerializeField] private string presetName = "";
        [SerializeField] private bool onlyThisExtension = true;
        [NonSerialized] private List<string> configurations = new List<string>();
        [NonSerialized] private string message;

        private void OnEnable()
        {
            configurations = ImportPresetFolderWindow.LoadConfigurations(out message);
        }

        private void Save(AssetImporter importer, string configuration)
        {
            string target = Path.Combine(ImportPresetLibrary.ConfigurationPath(configuration),
                presetName + ImportPresetLibrary.PresetExtension);
            if (File.Exists(target) && !EditorUtility.DisplayDialog("Save Import Preset",
                    "Replace " + presetName + ImportPresetLibrary.PresetExtension + " in '" + configuration + "'?",
                    "Replace", "Cancel"))
                return;
            try
            {
                string saved = ImportPresetLibrary.SavePreset(importer, configuration, presetName, onlyThisExtension);
                configurations = ImportPresetFolderWindow.LoadConfigurations(out message);
                choice = configurations.FindIndex(name =>
                    string.Equals(name, configuration, StringComparison.OrdinalIgnoreCase)) + 1;
                message = "Saved " + saved;
            }
            catch (Exception exception)
            {
                message = "Could not save: " + exception.Message;
                Debug.LogException(exception);
            }
        }
    }
}
