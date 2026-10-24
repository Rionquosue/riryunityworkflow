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

    /// <summary>Chooses the import preset configuration for one or more folders.</summary>
    internal sealed class ImportPresetFolderWindow : EditorWindow
    {
        [SerializeField] private List<string> folders = new List<string>();
        [SerializeField] private int choice;
        [SerializeField] private bool applyExisting;
        [NonSerialized] private List<string> configurations = new List<string>();
        [NonSerialized] private string message;
        private Vector2 scroll;

        internal static void Open(List<string> folders)
        {
            var window = CreateInstance<ImportPresetFolderWindow>();
            window.folders = folders;
            window.titleContent = new GUIContent("Import Preset");
            window.minSize = new Vector2(440, 320);
            window.Refresh(true);
            window.ShowUtility();
        }

        private void OnEnable()
        {
            if (folders == null) folders = new List<string>();
            Refresh(false);
        }

        private void Refresh(bool selectCurrent)
        {
            configurations = LoadConfigurations(out message);
            if (!selectCurrent || folders.Count == 0) return;
            string current = OwnConfiguration(folders[0]);
            int index = current != null ? configurations.FindIndex(name =>
                string.Equals(name, current, StringComparison.OrdinalIgnoreCase)) : -1;
            choice = index + 1;
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

        private static string OwnConfiguration(string folder)
        {
            string marker = ImportPresetMarker.PathIn(folder);
            return File.Exists(marker) ? ImportPresetMarker.ReadConfiguration(folder) : null;
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField(folders.Count == 1 ? "Folder" : "Folders", EditorStyles.boldLabel);
            foreach (string folder in folders) DrawFolderState(folder);

            EditorGUILayout.Space();
            var options = new List<string> { "None (remove this folder's preset)" };
            options.AddRange(configurations);
            choice = Mathf.Clamp(choice, 0, options.Count - 1);
            choice = EditorGUILayout.Popup("Preset configuration", choice, options.ToArray());
            if (choice > 0) DrawSummary(configurations[choice - 1]);
            if (configurations.Count == 0)
                EditorGUILayout.HelpBox("The library is empty. Right-click an asset with the import settings you " +
                    "want and choose Rirytools > Save Import Settings to Preset Library.", MessageType.Info);

            using (new EditorGUI.DisabledScope(choice == 0))
            {
                applyExisting = EditorGUILayout.ToggleLeft(
                    "Also apply to files already in the folder now", applyExisting && choice > 0);
            }
            if (applyExisting && choice > 0)
                EditorGUILayout.HelpBox("This replaces the current import settings of those files, including " +
                    "manual changes. Folders with their own ImportPreset.md are skipped.", MessageType.Warning);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Library: " + ImportPresetLibrary.RootPath, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndScrollView();

            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open library")) ImportPresetMenu.OpenLibrary();
                if (GUILayout.Button("Refresh")) Refresh(false);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Apply", GUILayout.Width(80))) Apply();
                if (GUILayout.Button("Close", GUILayout.Width(80))) Close();
            }
        }

        private void DrawFolderState(string folder)
        {
            string marker = ImportPresetMarker.PathIn(folder);
            string text;
            MessageType type = MessageType.None;
            if (File.Exists(marker))
            {
                string content = ReadOrNull(marker);
                string name = ImportPresetMarker.Parse(content);
                text = folder + ": " + (name != null ? "uses '" + name + "'" : "ImportPreset.md has no preset line");
                if (content != null && !ImportPresetMarker.IsGenerated(content))
                {
                    text += ". ImportPreset.md was edited by hand, so this tool will not change it.";
                    type = MessageType.Warning;
                }
            }
            else
            {
                string inherited = ImportPresetMarker.FindMarkerFolder(marker, File.Exists);
                text = folder + ": " + (inherited != null
                    ? "inherits '" + ImportPresetMarker.ReadConfiguration(inherited) + "' from " + inherited
                    : "no preset");
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.HelpBox(text, type);
                if (type == MessageType.Warning && GUILayout.Button("Open", GUILayout.Width(50)))
                    AssetDatabase.OpenAsset(AssetDatabase.LoadMainAssetAtPath(marker));
            }
        }

        private static void DrawSummary(string configuration)
        {
            string path = ImportPresetLibrary.ConfigurationPath(configuration);
            string error;
            ImportPresetLibrary.Config config = ImportPresetLibrary.ReadConfig(path, out error);
            List<string> files = ImportPresetLibrary.PresetFiles(path);
            var text = new StringBuilder();
            if (error != null) text.Append(error).Append('\n');
            var ruled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ImportPresetLibrary.Rule rule in config.rules)
            {
                if (rule == null || string.IsNullOrEmpty(rule.preset)) continue;
                ruled.Add(rule.preset.EndsWith(ImportPresetLibrary.PresetExtension, StringComparison.OrdinalIgnoreCase)
                    ? rule.preset : rule.preset + ImportPresetLibrary.PresetExtension);
                text.Append('.').Append(string.Join(", .", rule.extensions ?? new List<string>()))
                    .Append(": ").Append(rule.preset).Append('\n');
            }
            foreach (string file in files)
            {
                if (!ruled.Contains(file)) text.Append("Any matching file type: ").Append(file).Append('\n');
            }
            if (files.Count == 0) text.Append("This configuration has no .preset files yet.");
            EditorGUILayout.HelpBox(text.ToString().TrimEnd(), MessageType.None);
        }

        private void Apply()
        {
            string configuration = choice > 0 ? configurations[choice - 1] : null;
            var existing = new List<string>();
            if (applyExisting && configuration != null)
            {
                foreach (string folder in folders) existing.AddRange(FilesGovernedBy(folder));
                if (existing.Count > 0 && !EditorUtility.DisplayDialog("Import Preset",
                        "Replace the import settings of " + existing.Count + " existing file(s) with '" +
                        configuration + "'? This cannot be undone.", "Replace", "Cancel"))
                    return;
            }

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
                    if (configuration == null)
                    {
                        AssetDatabase.DeleteAsset(marker);
                        written++;
                        continue;
                    }
                    if (ImportPresetMarker.Parse(content) == configuration) continue;
                }
                else if (configuration == null)
                {
                    continue;
                }
                File.WriteAllText(marker, ImportPresetMarker.Generate(configuration), new UTF8Encoding(false));
                AssetDatabase.ImportAsset(marker);
                written++;
            }

            int applied = 0;
            if (existing.Count > 0) applied = ApplyToExisting(existing, configuration);

            message = "Updated " + written + " folder preset file(s).";
            if (applied > 0) message += " Applied '" + configuration + "' to " + applied + " existing file(s).";
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

        private static int ApplyToExisting(List<string> paths, string configuration)
        {
            int applied = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < paths.Count; i++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Import Preset", paths[i], (float)i / paths.Count))
                        break;
                    AssetImporter importer = AssetImporter.GetAtPath(paths[i]);
                    string file;
                    if (importer == null || !ImportPresetLibrary.TryApply(importer, configuration, out file)) continue;
                    importer.SaveAndReimport();
                    applied++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }
            return applied;
        }

        private static string ReadOrNull(string path)
        {
            try
            {
                return File.ReadAllText(path);
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
