using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>What the Import Preset window draws. State and actions are in Editor/ImportPresets/ImportPresetWindowActions.cs.</summary>
    internal sealed partial class ImportPresetFolderWindow
    {
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
                    "want and choose RiryTools > Save Import Settings to Preset Library.", MessageType.Info);
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
                if (GUILayout.Button("Open Library")) ImportPresetActions.OpenLibrary();
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
            if (configuration != null && index == 0)
            {
                options.Add(configuration + " (missing)");
                index = options.Count - 1;
            }
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
    }

    /// <summary>What the Save Import Preset window draws.</summary>
    internal sealed partial class ImportPresetSaveWindow
    {
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
                if (GUILayout.Button("Open Library")) ImportPresetActions.OpenLibrary();
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(error != null))
                {
                    if (GUILayout.Button("Save", GUILayout.Width(80))) Save(importer, configuration);
                }
                if (GUILayout.Button("Close", GUILayout.Width(80))) Close();
            }
        }
    }
}

// ============================================================================
// GLOSSARY: Import Preset windows
// Menu entries (Project right-click Import Preset... and Save Import Settings to
// Preset Library..., Tools > Open Import Preset Library) are in RiryMenus.cs.
// ============================================================================
//
// ImportPresetFolderWindow: edits ImportPreset.md for one or more folders
//   FIELDS (ImportPresetWindowActions.cs; [S] = serialized)
//     folders               [S] List<string>  Folders being edited.
//     defaultConfiguration  [S] string        Configuration for types without a row.
//     defaultPreset         [S] string        One preset of it, or null for its rules.
//     rows                  [S] List<Row>     Per-type rows.
//     applyExisting         [S] bool          Also apply to files already in the folders.
//     configurations            List<string>  Configurations found in the library.
//     message                   string        Library problem or the result of Apply, shown at the bottom.
//     scroll                    Vector2       Scroll position.
//     DefaultChoice / NoneChoice / AnyPreset  const string  Popup labels.
//   Row: Extensions ("fbx, obj"), Use (Default, None or a configuration), Preset,
//        Animations (extract to Animations/), BetterFbx (export .blend through Blender).
//   UI METHODS (this file)
//     Open(folders)         Opens the window for folders.
//     OnGUI()               Folder state, default row, type rows, Blender settings, Apply.
//     DrawTargetPopups(ref configuration, ref preset)  Configuration and preset popups.
//     PresetPopup(configuration, preset)  Preset popup for one configuration.
//     DrawRow(row)          One type row; returns true when Remove was clicked.
//     DrawBlenderSettings() Blender path and Better FBX options.
//     DrawFolderState(folder)  Whether the folder's ImportPreset.md is generated or hand-edited.
//   ACTIONS (ImportPresetWindowActions.cs)
//     LoadConfigurations(out error)  Library configurations, or why there are none.
//     LoadFrom(folder)      Fills the fields from a folder's ImportPreset.md.
//     Problem()             Why the rows cannot be written, or null.
//     BuildRules()          Rules from the fields.
//     Apply()               Writes ImportPreset.md and optionally applies to existing files.
//     FilesGovernedBy / ApplyToExisting / ApplyRules  Existing-file application.
//     Lists(row, extension) / IndexOf(list, value)  Small helpers.
//
// ImportPresetSaveWindow: saves an asset's import settings to the library
//   FIELDS: assetPath, choice (configuration index), newConfiguration, presetName,
//           onlyThisExtension, configurations, message.
//   Open(importer) / OnGUI()  (this file)    Save(importer, configuration)  (ImportPresetWindowActions.cs)
//
// ImportPresetActions (Editor/ImportPresets/ImportPresetActions.cs): selection helpers
//   ChooseFolderPreset / ValidateChooseFolderPreset, SaveToLibrary / ValidateSaveToLibrary,
//   OpenLibrary, SelectedFolders (selected folders, or the folder shown in the Project window),
//   IsWritableFolder.
