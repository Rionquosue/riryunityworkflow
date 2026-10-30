using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// The small Set Folder Import Presets popup from the Project right-click menu:
    /// one import preset for all selected folders (Apply to all), or one per folder.
    /// Apply sets each folder's default in its ImportPreset.md, keeping its other
    /// rules, and applies it to the files already there. The full window, from the
    /// Tools menu, is ImportPresetFolderWindow.
    /// </summary>
    internal sealed class ImportPresetQuickWindow : EditorWindow
    {
        private const string AnyPreset = "Any (configuration rules)";

        [SerializeField] private List<string> folders = new List<string>();
        [SerializeField] private bool applyToAll = true;
        [SerializeField] private int allChoice;
        [SerializeField] private List<int> choices = new List<int>();
        // Popup entries "Configuration/Preset" and what each sets: configuration, and preset or null.
        [NonSerialized] private string[] labels;
        [NonSerialized] private List<KeyValuePair<string, string>> targets;
        [NonSerialized] private string message;
        private Vector2 scroll;

        internal static void Open(List<string> folders)
        {
            var window = CreateInstance<ImportPresetQuickWindow>();
            window.folders = folders;
            window.titleContent = new GUIContent("Set Folder Import Presets");
            window.minSize = new Vector2(380.0f, 120.0f);
            window.Load();
            foreach (string folder in folders) window.choices.Add(window.Current(folder));
            window.allChoice = window.choices.Count > 0 ? window.choices[0] : 0;
            window.ShowUtility();
        }

        private void OnEnable()
        {
            Load();
        }

        private void Load()
        {
            var names = new List<string>();
            targets = new List<KeyValuePair<string, string>>();
            foreach (string configuration in ImportPresetFolderWindow.LoadConfigurations(out message))
            {
                names.Add(configuration + "/" + AnyPreset);
                targets.Add(new KeyValuePair<string, string>(configuration, null));
                foreach (string file in ImportPresetLibrary.PresetFiles(ImportPresetLibrary.ConfigurationPath(configuration)))
                {
                    string preset = Path.GetFileNameWithoutExtension(file);
                    names.Add(configuration + "/" + preset);
                    targets.Add(new KeyValuePair<string, string>(configuration, preset));
                }
            }
            labels = names.ToArray();
        }

        // The entry for the folder's current default, or the first one.
        private int Current(string folder)
        {
            string content = ImportPresetFolderWindow.ReadOrNull(ImportPresetMarker.PathIn(folder));
            if (content == null) return 0;
            ImportPresetMarker.Rules rules = ImportPresetMarker.Parse(content);
            int index = targets.FindIndex(target =>
                string.Equals(target.Key, rules.DefaultConfiguration, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(target.Value, rules.DefaultPreset, StringComparison.OrdinalIgnoreCase));
            return Math.Max(0, index);
        }

        private void OnGUI()
        {
            if (labels.Length == 0)
            {
                EditorGUILayout.HelpBox(message ?? "The library is empty. Right-click an asset with the import settings " +
                    "you want and choose RiryTools > Save Import Preset.", MessageType.Info);
            }
            else
            {
                applyToAll = EditorGUILayout.ToggleLeft("Apply to all", applyToAll);
                scroll = EditorGUILayout.BeginScrollView(scroll);
                if (applyToAll)
                {
                    allChoice = EditorGUILayout.Popup(folders.Count == 1 ? folders[0] : folders.Count + " folders",
                        Clamp(allChoice), labels);
                }
                else
                {
                    for (int i = 0; i < folders.Count; i++)
                        choices[i] = EditorGUILayout.Popup(folders[i], Clamp(choices[i]), labels);
                }
                EditorGUILayout.EndScrollView();
            }

            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Abort", GUILayout.Width(80.0f))) Close();
                using (new EditorGUI.DisabledScope(labels.Length == 0 || folders.Count == 0))
                {
                    if (GUILayout.Button("Apply", GUILayout.Width(80.0f))) Apply();
                }
            }
            EditorGUILayout.Space(4.0f);
        }

        private int Clamp(int index)
        {
            return Math.Max(0, Math.Min(index, labels.Length - 1));
        }

        /// <summary>Sets each folder's default, then applies it to the files already in the folder.</summary>
        private void Apply()
        {
            var set = new List<string>();
            var skipped = new List<string>();
            for (int i = 0; i < folders.Count; i++)
            {
                string folder = folders[i];
                KeyValuePair<string, string> target = targets[Clamp(applyToAll ? allChoice : choices[i])];
                string content = ImportPresetFolderWindow.ReadOrNull(ImportPresetMarker.PathIn(folder));
                ImportPresetMarker.Rules rules = content != null ? ImportPresetMarker.Parse(content) : new ImportPresetMarker.Rules();
                rules.DefaultConfiguration = target.Key;
                rules.DefaultPreset = target.Value;
                bool changed;
                if (ImportPresetFolderWindow.WriteMarker(folder, ImportPresetMarker.Generate(rules), out changed)) set.Add(folder);
                else skipped.Add(folder);
            }

            var existing = new List<string>();
            foreach (string folder in set) existing.AddRange(ImportPresetFolderWindow.FilesGovernedBy(folder));
            int processed = existing.Count > 0 ? ImportPresetFolderWindow.ApplyToExisting(existing) : 0;
            Debug.Log("Import Presets: set " + set.Count + " folder(s); processed " + processed + " existing file(s).");
            if (skipped.Count > 0)
                EditorUtility.DisplayDialog("Set Folder Import Presets", "Left hand-edited ImportPreset.md unchanged in:\n" +
                    string.Join("\n", skipped), "OK");
            Close();
        }
    }
}

// ============================================================================
// GLOSSARY: Set Folder Import Presets popup (Project right-click)
// The menu entry is in RiryMenus.cs; the full window (Tools menu) is ImportPresetFolderWindow.
// ============================================================================
//
// ImportPresetQuickWindow (this file)
//   folders                  List<string>  Selected folders (ImportPresetActions.SelectedFolders).
//   applyToAll               bool          Apply to all: one popup for every folder; off, one popup per folder.
//   allChoice / choices      int / List<int>  Chosen entries, first the folder's current default.
//   labels / targets         "Configuration/Preset" entries and the configuration and preset (or null) each sets.
//   Open(folders)            Opens the popup.
//   Current(folder)          The entry for the folder's current default.
//   OnGUI()                  Apply to all; the popup(s); Abort; Apply.
//   Apply()                  Sets the default in each ImportPreset.md (other rules kept; hand-edited ones are left
//                            alone), then applies it to the files already in the folders.
