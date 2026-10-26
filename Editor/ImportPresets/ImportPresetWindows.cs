#if UNITY_EDITOR
// Target editor: Unity 2022.3 LTS (including 2022.3.22f1).
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Riry.UnityWorkflow.Editor
{
    /// <summary>Project window entry points for import presets.</summary>
    internal static class ImportPresetMenu
    {
        private const string FolderMenuPath = "Assets/Rirytools/Import Preset...";
        private const string SaveMenuPath = "Assets/Rirytools/Save Import Settings to Preset Library...";
        private const string LibraryMenuPath = "Tools/Rirytools/Open Import Preset Library";

        [MenuItem(FolderMenuPath, false, 2001)]
        private static void ChooseFolderPreset()
        {
            ImportPresetFolderWindow.Open(SelectedFolders());
        }

        [MenuItem(FolderMenuPath, true)]
        private static bool ValidateChooseFolderPreset()
        {
            return SelectedFolders().Count > 0;
        }

        [MenuItem(SaveMenuPath, false, 2002)]
        private static void SaveToLibrary()
        {
            ImportPresetSaveWindow.Open(SelectedImporter());
        }

        [MenuItem(SaveMenuPath, true)]
        private static bool ValidateSaveToLibrary()
        {
            return ImportPresetLibrary.CanSave(SelectedImporter());
        }

        [MenuItem(LibraryMenuPath, false, 2040)]
        internal static void OpenLibrary()
        {
            EditorUtility.RevealInFinder(ImportPresetLibrary.EnsureRoot());
        }

        /// <summary>
        /// Selected folders, the folder of each selected file, or, after a click
        /// on empty space in the Project window, the folder being shown.
        /// </summary>
        internal static List<string> SelectedFolders()
        {
            var folders = new List<string>();
            foreach (string guid in Selection.assetGUIDs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                string folder = AssetDatabase.IsValidFolder(path) ? path : ImportPresetMarker.ParentFolder(path);
                if (folder != null && IsWritableFolder(folder) && !folders.Contains(folder)) folders.Add(folder);
            }
            if (folders.Count == 0)
            {
                string active = ActiveFolder();
                if (active != null && IsWritableFolder(active)) folders.Add(active);
            }
            return folders;
        }

        private static string ActiveFolder()
        {
            // Unity has no public API for the folder shown in the Project window.
            MethodInfo method = typeof(ProjectWindowUtil).GetMethod("GetActiveFolderPath",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (method == null) return null;
            try
            {
                string path = method.Invoke(null, null) as string;
                return !string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path) ? path : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal static bool IsWritableFolder(string folder)
        {
            if (folder == "Assets" || folder.StartsWith("Assets/", StringComparison.Ordinal)) return true;
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(folder);
            return package != null && (package.source == UnityEditor.PackageManager.PackageSource.Embedded ||
                package.source == UnityEditor.PackageManager.PackageSource.Local);
        }

        private static AssetImporter SelectedImporter()
        {
            if (Selection.assetGUIDs.Length != 1) return null;
            string path = AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0]);
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return null;
            return AssetImporter.GetAtPath(path);
        }
    }

    /// <summary>
    /// Edits the ImportPreset.md of one or more folders: a default configuration
    /// and per-file-type rows, with animation extraction and Better FBX options.
    /// </summary>
    internal sealed class ImportPresetFolderWindow : EditorWindow
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

        internal static void Open(List<string> folders)
        {
            var window = CreateInstance<ImportPresetFolderWindow>();
            window.folders = folders;
            window.titleContent = new GUIContent("Import Preset");
            window.minSize = new Vector2(480, 420);
            window.configurations = LoadConfigurations(out window.message);
            if (folders.Count > 0) window.LoadFrom(folders[0]);
            window.ShowUtility();
        }

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

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField(folders.Count == 1 ? "Folder" : "Folders", EditorStyles.boldLabel);
            foreach (string folder in folders) DrawFolderState(folder);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Default for unlisted file types", EditorStyles.boldLabel);
            DrawTargetPopups(ref defaultConfiguration, ref defaultPreset);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("File types", EditorStyles.boldLabel);
            int remove = -1;
            for (int i = 0; i < rows.Count; i++)
            {
                if (DrawRow(rows[i])) remove = i;
            }
            if (remove >= 0) rows.RemoveAt(remove);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ File type")) rows.Add(new Row());
                if (GUILayout.Button("+ Textures")) rows.Add(new Row { Extensions = "png, tga, jpg, jpeg, psd" });
                if (GUILayout.Button("+ Models")) rows.Add(new Row { Extensions = "fbx, obj" });
                if (GUILayout.Button("+ Blender"))
                    rows.Add(new Row { Extensions = BetterFbxExporter.SourceExtension, BetterFbx = true });
            }

            bool betterFbx = rows.Exists(row => row.BetterFbx && Lists(row, BetterFbxExporter.SourceExtension));
            if (betterFbx) DrawBlenderSettings();

            EditorGUILayout.Space();
            string problem = Problem();
            if (problem != null) EditorGUILayout.HelpBox(problem, MessageType.Warning);
            if (configurations.Count == 0)
                EditorGUILayout.HelpBox("The library is empty. Right-click an asset with the import settings you " +
                    "want and choose Rirytools > Save Import Settings to Preset Library.", MessageType.Info);
            applyExisting = EditorGUILayout.ToggleLeft("Also apply to files already in the folder now", applyExisting);
            if (applyExisting)
                EditorGUILayout.HelpBox("This replaces the current import settings of those files, including " +
                    "manual changes, extracts configured animations and runs configured Better FBX exports. " +
                    "Folders with their own ImportPreset.md are skipped.", MessageType.Warning);
            EditorGUILayout.LabelField("Library: " + ImportPresetLibrary.RootPath, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndScrollView();

            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open library")) ImportPresetMenu.OpenLibrary();
                if (GUILayout.Button("Refresh")) configurations = LoadConfigurations(out message);
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(problem != null))
                {
                    if (GUILayout.Button("Apply", GUILayout.Width(80))) Apply();
                }
                if (GUILayout.Button("Close", GUILayout.Width(80))) Close();
            }
        }

        private void DrawTargetPopups(ref string configuration, ref string preset)
        {
            var options = new List<string> { NoneChoice };
            options.AddRange(configurations);
            int index = configuration == null ? 0 : IndexOf(configurations, configuration) + 1;
            if (configuration != null && index == 0) options.Add(configuration + " (missing)");
            if (configuration != null && index == 0) index = options.Count - 1;
            using (new EditorGUILayout.HorizontalScope())
            {
                int chosen = EditorGUILayout.Popup(index, options.ToArray());
                if (chosen != index)
                {
                    configuration = chosen == 0 ? null : configurations[chosen - 1];
                    preset = null;
                }
                preset = PresetPopup(configuration, preset);
            }
        }

        private string PresetPopup(string configuration, string preset)
        {
            using (new EditorGUI.DisabledScope(configuration == null))
            {
                var options = new List<string> { AnyPreset };
                if (configuration != null)
                {
                    foreach (string file in ImportPresetLibrary.PresetFiles(ImportPresetLibrary.ConfigurationPath(configuration)))
                        options.Add(Path.GetFileNameWithoutExtension(file));
                }
                int index = preset == null ? 0 : IndexOf(options, preset);
                if (preset != null && index < 0)
                {
                    options.Add(preset + " (missing)");
                    index = options.Count - 1;
                }
                int chosen = EditorGUILayout.Popup(index, options.ToArray());
                if (chosen == index) return preset;
                return chosen == 0 ? null : options[chosen];
            }
        }

        /// <summary>Draws one file-type row; returns true when it should be removed.</summary>
        private bool DrawRow(Row row)
        {
            bool remove = false;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    row.Extensions = EditorGUILayout.TextField(
                        new GUIContent("Extensions", "Comma-separated, for example: png, tga"), row.Extensions);
                    remove = GUILayout.Button("Remove", GUILayout.Width(65));
                }
                var uses = new List<string> { DefaultChoice, NoneChoice };
                uses.AddRange(configurations);
                int index = IndexOf(uses, row.Use);
                if (index < 0)
                {
                    uses.Add(row.Use + " (missing)");
                    index = uses.Count - 1;
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    int chosen = EditorGUILayout.Popup("Use", index, uses.ToArray());
                    if (chosen != index)
                    {
                        row.Use = uses[chosen];
                        row.Preset = null;
                    }
                    bool named = row.Use != DefaultChoice && row.Use != NoneChoice;
                    row.Preset = named ? PresetPopup(row.Use, row.Preset) : null;
                    if (!named) PresetPopup(null, null);
                }
                row.Animations = EditorGUILayout.ToggleLeft(new GUIContent(
                    "Extract animations to Animations/ first (models)",
                    "Imports as Generic, copies every clip, then applies the preset (for example Humanoid)."),
                    row.Animations);
                if (Lists(row, BetterFbxExporter.SourceExtension))
                {
                    row.BetterFbx = EditorGUILayout.ToggleLeft(new GUIContent(
                        "Export .blend with Better FBX",
                        "Writes <name>.fbx beside the .blend; the FBX then follows the fbx row or the default."),
                        row.BetterFbx);
                }
            }
            return remove;
        }

        private static void DrawBlenderSettings()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Better FBX (saved for this computer)", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                BetterFbxExporter.BlenderPath = EditorGUILayout.TextField("Blender", BetterFbxExporter.BlenderPath);
                if (GUILayout.Button("Browse", GUILayout.Width(65)))
                {
                    string chosen = EditorUtility.OpenFilePanel("Blender executable", "", "");
                    if (!string.IsNullOrEmpty(chosen)) BetterFbxExporter.BlenderPath = chosen;
                }
            }
            string resolved = BetterFbxExporter.ResolveBlender();
            EditorGUILayout.LabelField(resolved != null ? "Using: " + resolved : "Blender not found.",
                EditorStyles.wordWrappedMiniLabel);
            BetterFbxExporter.Options = EditorGUILayout.TextField(
                new GUIContent("Exporter options", "Extra bpy.ops.better_export.fbx arguments, for example " +
                    "my_fbx_unit='m', use_triangulate=True. Every action is exported unless you set my_animation_type."), BetterFbxExporter.Options);
            string error = BetterFbxExporter.OptionsError(BetterFbxExporter.Options);
            if (error != null) EditorGUILayout.HelpBox(error, MessageType.Warning);
        }

        private void DrawFolderState(string folder)
        {
            string marker = ImportPresetMarker.PathIn(folder);
            string text;
            MessageType type = MessageType.None;
            if (File.Exists(marker))
            {
                string content = ReadOrNull(marker);
                text = folder + ": has ImportPreset.md";
                if (content != null && !ImportPresetMarker.IsGenerated(content))
                {
                    text += ", edited by hand, so this tool will not change it.";
                    type = MessageType.Warning;
                }
            }
            else
            {
                string inherited = ImportPresetMarker.FindMarkerFolder(marker, File.Exists);
                text = folder + ": " + (inherited != null ? "inherits from " + inherited : "no preset");
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.HelpBox(text, type);
                if (type == MessageType.Warning && GUILayout.Button("Open", GUILayout.Width(50)))
                    AssetDatabase.OpenAsset(AssetDatabase.LoadMainAssetAtPath(marker));
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
                    if (extension == ImportPresetAnimations.FolderName.ToLowerInvariant() ||
                        extension == "preset" || extension == "betterfbx")
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
                if (existing.Count > 0 && !EditorUtility.DisplayDialog("Import Preset",
                        "Apply these rules to " + existing.Count + " existing file(s)? Their import settings " +
                        "are replaced. This cannot be undone.", "Apply", "Cancel"))
                    return;
            }

            string generated = rules.IsEmpty ? null : ImportPresetMarker.Generate(rules);
            var skipped = new List<string>();
            int written = 0;
            foreach (string folder in folders)
            {
                string marker = ImportPresetMarker.PathIn(folder);
                if (File.Exists(marker))
                {
                    string content = ReadOrNull(marker);
                    // A hand-edited marker belongs to the user: never rewrite or delete it.
                    if (content == null || !ImportPresetMarker.IsGenerated(content))
                    {
                        skipped.Add(folder);
                        continue;
                    }
                    if (generated == null)
                    {
                        AssetDatabase.DeleteAsset(marker);
                        written++;
                        continue;
                    }
                    if (content.Replace("\r\n", "\n") == generated) continue;
                }
                else if (generated == null)
                {
                    continue;
                }
                File.WriteAllText(marker, generated, new UTF8Encoding(false));
                AssetDatabase.ImportAsset(marker);
                written++;
            }

            int applied = existing.Count > 0 ? ApplyToExisting(existing) : 0;
            message = "Updated " + written + " ImportPreset.md file(s).";
            if (applied > 0) message += " Processed " + applied + " existing file(s).";
            if (skipped.Count > 0)
                message += " Left hand-edited ImportPreset.md unchanged in: " + string.Join(", ", skipped) + ".";
            Repaint();
        }

        private static List<string> FilesGovernedBy(string folder)
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

        private static int ApplyToExisting(List<string> paths)
        {
            int processed = 0;
            try
            {
                for (int i = 0; i < paths.Count; i++)
                {
                    string path = paths[i];
                    if (EditorUtility.DisplayCancelableProgressBar("Import Preset", path, (float)i / paths.Count))
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
            if (model != null && rules.ExtractAnimations.Contains(extension))
            {
                List<string> skipped;
                ImportPresetAnimations.ExtractPreservingSettings(model, ImportPresetOwnedFiles.IsUnchanged, out skipped);
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

        private static string ReadOrNull(string path)
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
    internal sealed class ImportPresetSaveWindow : EditorWindow
    {
        [SerializeField] private string assetPath;
        [SerializeField] private int choice;
        [SerializeField] private string newConfiguration = "";
        [SerializeField] private string presetName = "";
        [SerializeField] private bool onlyThisExtension = true;
        [NonSerialized] private List<string> configurations = new List<string>();
        [NonSerialized] private string message;

        internal static void Open(AssetImporter importer)
        {
            if (!ImportPresetLibrary.CanSave(importer)) return;
            var window = CreateInstance<ImportPresetSaveWindow>();
            window.assetPath = importer.assetPath;
            string type = importer.GetType().Name;
            if (type.EndsWith("Importer", StringComparison.Ordinal)) type = type.Substring(0, type.Length - 8);
            string extension = ImportPresetLibrary.NormalizeExtension(Path.GetExtension(importer.assetPath));
            window.presetName = extension.Length > 0 ? type + " " + extension : type;
            window.titleContent = new GUIContent("Save Import Preset");
            window.minSize = new Vector2(420, 230);
            window.ShowUtility();
        }

        private void OnEnable()
        {
            configurations = ImportPresetFolderWindow.LoadConfigurations(out message);
        }

        private void OnGUI()
        {
            AssetImporter importer = string.IsNullOrEmpty(assetPath) ? null : AssetImporter.GetAtPath(assetPath);
            if (!ImportPresetLibrary.CanSave(importer))
            {
                EditorGUILayout.HelpBox("The selected asset is gone or has no importer settings to save.",
                    MessageType.Warning);
                return;
            }
            EditorGUILayout.LabelField("Asset", assetPath, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Importer", importer.GetType().Name);

            var options = new List<string> { "New configuration..." };
            options.AddRange(configurations);
            choice = Mathf.Clamp(choice, 0, options.Count - 1);
            choice = EditorGUILayout.Popup("Configuration", choice, options.ToArray());
            if (choice == 0) newConfiguration = EditorGUILayout.TextField("New name", newConfiguration);
            presetName = EditorGUILayout.TextField("Preset name", presetName);
            string extension = ImportPresetLibrary.NormalizeExtension(Path.GetExtension(assetPath));
            using (new EditorGUI.DisabledScope(extension.Length == 0))
            {
                onlyThisExtension = EditorGUILayout.ToggleLeft(
                    "Use only for ." + extension + " files (otherwise every " + importer.GetType().Name + " file)",
                    onlyThisExtension && extension.Length > 0);
            }

            string configuration = choice == 0 ? newConfiguration : configurations[choice - 1];
            string error = ImportPresetLibrary.ValidateName(configuration) ?? ImportPresetLibrary.ValidateName(presetName);
            if (error != null) EditorGUILayout.HelpBox(error, MessageType.Warning);
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Info);

            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open library")) ImportPresetMenu.OpenLibrary();
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(error != null))
                {
                    if (GUILayout.Button("Save", GUILayout.Width(80))) Save(importer, configuration);
                }
                if (GUILayout.Button("Close", GUILayout.Width(80))) Close();
            }
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
#endif
