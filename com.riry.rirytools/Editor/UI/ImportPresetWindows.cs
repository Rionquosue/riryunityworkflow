using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>What the Set Folder Import Presets window draws. State and actions are in Editor/ImportPresets/ImportPresetWindowActions.cs.</summary>
    internal sealed partial class ImportPresetFolderWindow
    {
        /// <summary>Opens the window on the folder shown in the Project window.</summary>
        internal static void Open()
        {
            ImportPresetFolderWindow window = GetWindow<ImportPresetFolderWindow>(true, "Set Folder Import Presets");
            window.minSize = new Vector2(420, 240);
            string active = ImportPresetActions.ActiveFolder();
            window.Load(Usable(active) ? active : "Assets");
            window.search = window.folder;
            window.Show();
        }

        /// <summary>Main Menu tab: shows the folder in the Project window, as Open does.</summary>
        internal void ShowActiveFolder()
        {
            string active = ImportPresetActions.ActiveFolder();
            Load(Usable(active) ? active : "Assets");
            search = folder;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            string typed = EditorGUILayout.TextField(new GUIContent("Folder", "Type part of a folder's name or path."), search);
            if (typed != search)
            {
                search = typed;
                string path = typed.Trim().TrimEnd('/');
                if (path != folder && Usable(path)) Load(path);
            }
            if (search.Trim().Length > 0 && search.Trim().TrimEnd('/') != folder)
            {
                foreach (string suggestion in Suggestions(search.Trim()))
                {
                    if (!GUILayout.Button(suggestion, EditorStyles.miniButton)) continue;
                    Load(suggestion);
                    search = suggestion;
                    GUI.FocusControl(null);
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUILayout.Space(6);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (!Usable(folder))
            {
                EditorGUILayout.HelpBox("Choose a folder in Assets.", MessageType.Info);
            }
            else
            {
                if (handEdited)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.HelpBox("ImportPreset.md in this folder was edited by hand, so this tool will not change it.",
                            MessageType.Warning);
                        if (GUILayout.Button("Open", GUILayout.Width(50)))
                            AssetDatabase.OpenAsset(AssetDatabase.LoadMainAssetAtPath(ImportPresetMarker.PathIn(folder)));
                    }
                }
                if (types.Count == 0)
                    EditorGUILayout.HelpBox("No files directly in this folder take import presets.", MessageType.Info);
                foreach (ImportPresetFolderRules.FileType type in types)
                {
                    ImportPresetFolderRules.Pick pick = PickFor(type.Label);
                    int index;
                    string[] names = ImportPresetFolderRules.Entries(type, pick, out index);
                    int chosen = EditorGUILayout.Popup(type.Label, index, names);
                    if (chosen != index) SetPick(ImportPresetFolderRules.Chosen(type, pick, chosen));
                }
                if (hasBlend)
                {
                    betterFbx = EditorGUILayout.ToggleLeft(new GUIContent("Export .blend with Better FBX",
                        "Writes <name>.fbx beside each .blend; the FBX then gets the Model files preset."), betterFbx);
                    if (betterFbx) DrawBlenderSettings();
                }
            }
            DrawTags();
            EditorGUILayout.EndScrollView();

            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(!Usable(folder) || handEdited ||
                    (hasBlend && betterFbx && BetterFbxExporter.OptionsError(BetterFbxExporter.Options) != null)))
                {
                    if (GUILayout.Button("Apply", GUILayout.Width(80))) Apply();
                }
                if (GUILayout.Button("Close", GUILayout.Width(80))) Close();
            }
            EditorGUILayout.Space(4);
        }

        // The tags in preferences.yml: each one is a file type of its own for files whose name contains it.
        private void DrawTags()
        {
            EditorGUILayout.Space(6);
            tagsExpanded = EditorGUILayout.Foldout(tagsExpanded, new GUIContent("Tags",
                "A file whose name contains a tag (any case) is listed under that tag's file type, such as normal files. " +
                "Saved for every project in " + ImportPresetPreferences.FilePath + "."), true);
            if (!tagsExpanded) return;
            using (new EditorGUI.IndentLevelScope())
            {
                List<string> tags = ImportPresetPreferences.Tags();
                foreach (string tag in tags)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(tag);
                        if (!GUILayout.Button("x", GUILayout.Width(22))) continue;
                        tags.Remove(tag);
                        SaveTags(tags);
                        GUIUtility.ExitGUI();
                    }
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    newTag = EditorGUILayout.TextField(newTag);
                    if (GUILayout.Button("Add", GUILayout.Width(60)))
                    {
                        string tag = newTag.Trim().ToLowerInvariant();
                        tagProblem = ImportPresetPreferences.ValidateTag(tag);
                        if (tagProblem == null)
                        {
                            if (!tags.Contains(tag)) tags.Add(tag);
                            SaveTags(tags);
                            newTag = "";
                            GUI.FocusControl(null);
                        }
                    }
                }
                // A tag problem shows only after Add was clicked with it.
                if (tagProblem != null) EditorGUILayout.HelpBox(tagProblem, MessageType.Warning);
            }
        }

        private void SaveTags(List<string> tags)
        {
            try
            {
                ImportPresetPreferences.SaveTags(tags);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                message = "Could not save " + ImportPresetPreferences.FilePath + ": " + exception.Message;
            }
            Scan();
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

        /// <summary>Main Menu tab: shows an asset's importer settings to save, as Open does.</summary>
        internal void UseImporter(AssetImporter importer)
        {
            if (!ImportPresetLibrary.CanSave(importer)) return;
            assetPath = importer.assetPath;
            string type = importer.GetType().Name;
            if (type.EndsWith("Importer", StringComparison.Ordinal)) type = type.Substring(0, type.Length - 8);
            string extension = ImportPresetLibrary.NormalizeExtension(Path.GetExtension(importer.assetPath));
            presetName = extension.Length > 0 ? type + " " + extension : type;
            message = null;
        }

        internal string AssetPath
        {
            get { return assetPath; }
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

            var options = new List<string> { "New configuration" };
            options.AddRange(configurations);
            choice = Mathf.Clamp(choice, 0, options.Count - 1);
            choice = EditorGUILayout.Popup("Configuration", choice, options.ToArray());
            if (choice == 0) newConfiguration = EditorGUILayout.TextField("New name", newConfiguration);
            presetName = EditorGUILayout.TextField("Preset name", presetName);

            string configuration = choice == 0 ? newConfiguration : configurations[choice - 1];
            string error = ImportPresetLibrary.ValidateName(configuration) ?? ImportPresetLibrary.ValidateName(presetName);
            // A name problem shows only after Save was clicked with it.
            if (error == null) saveTried = false;
            if (error != null && saveTried) EditorGUILayout.HelpBox(error, MessageType.Warning);
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Info);

            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open Library")) ImportPresetActions.OpenLibrary();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Save", GUILayout.Width(80)))
                {
                    if (error != null) saveTried = true;
                    else Save(importer, configuration);
                }
                if (GUILayout.Button("Close", GUILayout.Width(80))) Close();
            }
        }
    }
}

// ============================================================================
// GLOSSARY: Import Presets windows
// Menu entries (Set Folder Import Presets, Save Import Preset, Extract Animation and
// Open Import Preset Library, under Tools > RiryTools > Import Presets and the Project
// right-click menu) are in RiryMenus.cs. The right-click Set Folder Import Presets popup
// is Editor/UI/ImportPresetQuickWindow.cs.
// ============================================================================
//
// ImportPresetFolderWindow: edits one folder's ImportPreset.md (Tools menu)
//   FIELDS (ImportPresetWindowActions.cs; [S] = serialized)
//     folder                [S] string        The folder shown; first the Project window's (ImportPresetActions.ActiveFolder).
//     search                [S] string        Folder field text; suggestions show while it is not the folder.
//     picks                 [S] List<Pick>    The chosen preset (or none) per file type.
//     betterFbx             [S] bool          Export .blend with Better FBX.
//     types                     List<FileType>  File types directly in the folder, each with the presets that fit it.
//     hasBlend / handEdited     bool          The folder holds .blend files / its ImportPreset.md was edited by hand.
//     allFolders                List<string>  Every folder in Assets, for suggestions.
//     message                   string        Result of Apply.
//   UI METHODS (this file)
//     Open()                Opens the window on the Project window's folder.
//     ShowActiveFolder()    The same for the Main Menu tab.
//     OnGUI()               Folder with suggestions; one popup per file type; Better FBX (only with .blend files); Tags; Apply; Close.
//     DrawTags() / SaveTags(tags)  The Tags foldout: preferences.yml tags with x, and Add (a problem shows after Add).
//     DrawBlenderSettings() Blender path and Better FBX options.
//   ACTIONS (ImportPresetWindowActions.cs)
//     OnEnable() / OnProjectChange()  Find the file types again (after a domain reload or an import); the picks stay.
//     LoadConfigurations(out error)  Library configurations, or why there are none (Save Import Preset).
//     Load(folder)          Shows a folder with the picks from its ImportPreset.md.
//     Scan()                Finds the file types, .blend files and a hand-edited marker.
//     Suggestions(typed)    Up to 8 folders, ranked by FuzzyMatch.Score on name or path.
//     Apply()               ImportPresetFolderRules.Apply for the folder.
//
// ImportPresetPreferences (Editor/ImportPresets/ImportPresetPreferences.cs): Documents/RiryTools/ImportPresets/preferences.yml
//   Tags() (created with DefaultTags, the Substance Painter PBR map names), SaveTags(tags), ValidateTag(tag).
//
// ImportPresetFolderRules (Editor/ImportPresets/ImportPresetFolderRules.cs): shared with the right-click popup
//   Label(importer)          "Image files", "Model files", "Audio files", "Video files", "Font files", else "<Type> files".
//   Ending / Decorators / DecoratorIn / DecoratorLabel  Tags ("normal files") and "_word" name endings (not
//                            number-only; "_normal files") as file types; a name containing several uses the one ending last.
//   InFolder(folder)         Importers of the files directly in the folder, by file type (decorated files under both types).
//   Effective(rules, importer, decorators)  A file's preset: its decorator's type's, else its plain type's.
//   Types(folders)           The file types with the library presets that fit each (Preset.CanBeAppliedTo).
//   Entries / Chosen / FirstIn  Popup entries ("None", "Configuration/Preset"), the pick for an entry, a configuration's preset.
//   Apply(folder, picks, betterFbx, ref processed)  Writes ImportPreset.md; applies only changed presets to the folder's
//                            files (none and unchanged types are not reimported); exports .blend files when Better FBX is turned on.
//   WriteMarker / ReadOrNull / Read / HandEdited  ImportPreset.md helpers; a hand-edited one is never changed.
//
// ImportPresetSaveWindow: saves an asset's import settings to the library
//   FIELDS: assetPath, choice (configuration index), newConfiguration, presetName,
//           saveTried (shows a name problem after Save), configurations, message.
//   Open(importer) / OnGUI()  (this file)    Save(importer, configuration)  (ImportPresetWindowActions.cs)
//   UseImporter(importer) / AssetPath  (this file)  For the Main Menu tab, which follows the selection.
//
// ImportPresetActions (Editor/ImportPresets/ImportPresetActions.cs): selection helpers
//   ChooseFolderPreset, QuickFolderPreset / ValidateChooseFolderPreset, SaveToLibrary / ValidateSaveToLibrary,
//   OpenLibrary, SelectedFolders (selected folders, or the folder shown in the Project window), ActiveFolder,
//   IsWritableFolder.
